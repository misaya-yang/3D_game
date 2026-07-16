"""Deterministic G09-07 character generation and optimization pipeline.

This script replaces the previous primitive-based character patching workflow.
It uses MPFB only as a CC0 human/topology generator, then builds the project's
own cultivator outfit, hair and accessories before transferring the result to
the existing Quaternius animation rig.

Run through:
    ./tools/blender/run_character_pipeline.sh --profile cultivator

The wrapper keeps MPFB in an isolated cache and does not modify the user's
normal Blender extension configuration.
"""

from __future__ import annotations

import argparse
import bmesh
import hashlib
import importlib
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CONFIG = (
    REPO_ROOT / "tools" / "blender" / "config" / "g09_07_characters.json"
)


@dataclass(frozen=True)
class Bounds:
    minimum: Vector
    maximum: Vector

    @property
    def size(self) -> Vector:
        return self.maximum - self.minimum

    @property
    def center(self) -> Vector:
        return (self.minimum + self.maximum) * 0.5


@dataclass
class PipelineContext:
    config: dict
    profile_name: str
    profile: dict
    shared: dict
    materials: dict[str, bpy.types.Material]
    source_objects: list[bpy.types.Object]
    armature: bpy.types.Object
    donor: bpy.types.Object
    body: bpy.types.Object
    generated_meshes: list[bpy.types.Object]
    body_bounds: Bounds
    target_bounds: Bounds
    character_height: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", default="cultivator")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG))
    parser.add_argument(
        "--skip-preview",
        action="store_true",
        help="Export and validate without rendering the single offline preview.",
    )
    try:
        separator = sys.argv.index("--")
        return parser.parse_args(sys.argv[separator + 1 :])
    except ValueError:
        return parser.parse_args([])


def resolve_repo_path(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else (REPO_ROOT / path).resolve()


def load_config(path: Path, profile_name: str) -> tuple[dict, dict, dict]:
    with path.open("r", encoding="utf-8") as stream:
        config = json.load(stream)
    profiles = config.get("profiles", {})
    if profile_name not in profiles:
        raise KeyError(f"Unknown character profile: {profile_name}")
    return config, config["shared"], profiles[profile_name]


def dynamic_import(suffix: str, key: str):
    for module_name in tuple(sys.modules):
        if not module_name.endswith(suffix):
            continue
        module = importlib.import_module(module_name)
        if not hasattr(module, key):
            raise AttributeError(f"{module_name} does not expose {key}")
        return getattr(module, key)
    raise RuntimeError(
        "MPFB is not enabled. Run tools/blender/run_character_pipeline.sh "
        "instead of invoking Blender directly."
    )


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in tuple(bpy.data.collections):
        if collection.name != "Collection" and collection.users == 0:
            bpy.data.collections.remove(collection)


def import_fbx(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))
    return [obj for obj in bpy.context.scene.objects if obj not in before]


def find_unique(
    objects: Iterable[bpy.types.Object],
    *,
    name: str | None = None,
    object_type: str | None = None,
) -> bpy.types.Object:
    matches = [
        obj
        for obj in objects
        if (name is None or obj.name == name)
        and (object_type is None or obj.type == object_type)
    ]
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one object name={name!r} type={object_type!r}; "
            f"found {[obj.name for obj in matches]}"
        )
    return matches[0]


def bounds_for_objects(objects: Iterable[bpy.types.Object]) -> Bounds:
    points: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(
            obj.matrix_world @ vertex.co
            for vertex in obj.data.vertices
        )
    if not points:
        raise RuntimeError("Cannot calculate bounds without mesh objects")
    return Bounds(
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def mesh_bounds(obj: bpy.types.Object) -> Bounds:
    return bounds_for_objects([obj])


def make_material(name: str, specification: dict) -> bpy.types.Material:
    rgba = tuple(float(value) for value in specification["rgba"])
    material = bpy.data.materials.new(name)
    material.diffuse_color = rgba
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = rgba
        principled.inputs["Roughness"].default_value = float(
            specification.get("roughness", 0.75)
        )
        metallic = principled.inputs.get("Metallic")
        if metallic is not None:
            metallic.default_value = float(specification.get("metallic", 0.0))
        if "emission" in specification:
            emission = tuple(float(value) for value in specification["emission"])
            emission_color = principled.inputs.get("Emission Color")
            emission_strength = principled.inputs.get("Emission Strength")
            if emission_color is not None:
                emission_color.default_value = emission
            if emission_strength is not None:
                emission_strength.default_value = float(
                    specification.get("emission_strength", 0.0)
                )
    return material


def build_materials(shared: dict) -> dict[str, bpy.types.Material]:
    return {
        key: make_material(f"Wendao_{key}", specification)
        for key, specification in shared["palette"].items()
    }


def assign_material(
    obj: bpy.types.Object,
    material: bpy.types.Material,
) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def shade_smooth(obj: bpy.types.Object) -> None:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def set_active(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj: bpy.types.Object, modifier_name: str) -> None:
    set_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier_name)


def add_solidify(
    obj: bpy.types.Object,
    thickness: float,
    *,
    offset: float = 0.0,
) -> None:
    modifier = obj.modifiers.new("WendaoSolidify", "SOLIDIFY")
    modifier.thickness = thickness
    modifier.offset = offset
    modifier.use_even_offset = True
    apply_modifier(obj, modifier.name)


def decimate(obj: bpy.types.Object, ratio: float) -> None:
    if ratio >= 0.999 or len(obj.data.polygons) < 500:
        return
    modifier = obj.modifiers.new("WendaoDecimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.1, min(1.0, ratio))
    modifier.use_collapse_triangulate = True
    apply_modifier(obj, modifier.name)


def offset_along_normals(obj: bpy.types.Object, distance: float) -> None:
    obj.data.update()
    normals = [vertex.normal.copy() for vertex in obj.data.vertices]
    for vertex, normal in zip(obj.data.vertices, normals, strict=True):
        vertex.co += normal * distance
    obj.data.update()


def group_vertex_bounds(
    source: bpy.types.Object,
    group_names: Iterable[str],
) -> Bounds:
    group_indices = {
        source.vertex_groups[name].index
        for name in group_names
        if name in source.vertex_groups
    }
    points = [
        vertex.co.copy()
        for vertex in source.data.vertices
        if any(
            element.group in group_indices and element.weight > 0.0001
            for element in vertex.groups
        )
    ]
    if not points:
        raise RuntimeError(f"Empty source groups: {tuple(group_names)}")
    return Bounds(
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def extract_surface(
    source: bpy.types.Object,
    group_names: Iterable[str],
    name: str,
    *,
    reference_bounds: Bounds | None = None,
    predicate: Callable[[Vector, Vector], bool] | None = None,
) -> bpy.types.Object:
    group_indices = {
        source.vertex_groups[group_name].index
        for group_name in group_names
        if group_name in source.vertex_groups
    }
    if not group_indices:
        raise RuntimeError(f"Missing vertex groups for {name}: {tuple(group_names)}")

    modifier_visibility = [
        (modifier, modifier.show_viewport, modifier.show_render)
        for modifier in source.modifiers
    ]
    for modifier, _show_viewport, _show_render in modifier_visibility:
        modifier.show_viewport = False
        modifier.show_render = False
    bpy.context.view_layer.update()
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    try:
        evaluated = source.evaluated_get(dependency_graph)
        baked_mesh = bpy.data.meshes.new_from_object(
            evaluated,
            preserve_all_data_layers=True,
            depsgraph=dependency_graph,
        )
    finally:
        for modifier, show_viewport, show_render in modifier_visibility:
            modifier.show_viewport = show_viewport
            modifier.show_render = show_render
        bpy.context.view_layer.update()

    duplicate = source.copy()
    duplicate.data = baked_mesh
    duplicate.name = name
    duplicate.data.name = f"{name}_Mesh"
    bpy.context.scene.collection.objects.link(duplicate)
    duplicate.animation_data_clear()
    for modifier in tuple(duplicate.modifiers):
        duplicate.modifiers.remove(modifier)

    bounds = reference_bounds or group_vertex_bounds(source, group_names)
    size = bounds.size
    height = max(size.z, 0.0001)
    center = bounds.center

    mesh = bmesh.new()
    mesh.from_mesh(duplicate.data)
    deformation = mesh.verts.layers.deform.active
    remove: list[bmesh.types.BMVert] = []
    for vertex in mesh.verts:
        weights = vertex[deformation] if deformation is not None else {}
        in_group = any(weights.get(index, 0.0) > 0.0001 for index in group_indices)
        normalized = Vector(
            (
                (vertex.co.x - center.x) / height,
                (vertex.co.y - center.y) / height,
                (vertex.co.z - bounds.minimum.z) / height,
            )
        )
        if not in_group or (
            predicate is not None and not predicate(vertex.co, normalized)
        ):
            remove.append(vertex)
    bmesh.ops.delete(mesh, geom=remove, context="VERTS")
    mesh.to_mesh(duplicate.data)
    mesh.free()
    duplicate.data.update()
    if len(duplicate.data.polygons) == 0:
        raise RuntimeError(f"Surface extraction produced no polygons: {name}")
    return duplicate


def transform_meshes(
    meshes: Iterable[bpy.types.Object],
    current_bounds: Bounds,
    target_bounds: Bounds,
    height_ratio: float,
) -> float:
    scale = (
        target_bounds.size.z
        * height_ratio
        / max(current_bounds.size.z, 0.0001)
    )
    scaled_center = current_bounds.center * scale
    scaled_bottom = current_bounds.minimum.z * scale
    translation = Vector(
        (
            target_bounds.center.x - scaled_center.x,
            target_bounds.center.y - scaled_center.y,
            target_bounds.minimum.z - scaled_bottom,
        )
    )
    transform = Matrix.Translation(translation) @ Matrix.Scale(scale, 4)
    for obj in meshes:
        obj.data.transform(transform)
        obj.data.update()
        obj.matrix_world = Matrix.Identity(4)
    return scale


def clear_vertex_groups(obj: bpy.types.Object) -> None:
    for group in tuple(obj.vertex_groups):
        obj.vertex_groups.remove(group)


def parent_preserve_world(
    obj: bpy.types.Object,
    parent: bpy.types.Object,
) -> None:
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def add_armature_modifier(
    obj: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    for modifier in tuple(obj.modifiers):
        if modifier.type == "ARMATURE":
            obj.modifiers.remove(modifier)
    modifier = obj.modifiers.new("WendaoArmature", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True


def transfer_weights(
    donor: bpy.types.Object,
    target: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    clear_vertex_groups(target)
    source_group_names = [
        group.name
        for group in donor.vertex_groups
        if group.name in armature.data.bones
    ]
    if len(source_group_names) < 8:
        raise RuntimeError(
            f"Weight donor {donor.name} has only "
            f"{len(source_group_names)} armature groups"
        )
    for group_name in source_group_names:
        target.vertex_groups.new(name=group_name)
    modifier = target.modifiers.new("WendaoWeightTransfer", "DATA_TRANSFER")
    modifier.object = donor
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    modifier.mix_mode = "REPLACE"
    modifier.mix_factor = 1.0
    apply_modifier(target, modifier.name)

    deform_groups = {
        group.name
        for group in target.vertex_groups
        if group.name in armature.data.bones
    }
    if len(deform_groups) < 8:
        raise RuntimeError(
            f"Weight transfer to {target.name} produced only "
            f"{len(deform_groups)} armature groups"
        )
    parent_preserve_world(target, armature)
    add_armature_modifier(target, armature)


def bind_rigid(
    obj: bpy.types.Object,
    armature: bpy.types.Object,
    bone_name: str,
) -> None:
    if bone_name not in armature.data.bones:
        raise KeyError(f"Missing rigid bind bone: {bone_name}")
    clear_vertex_groups(obj)
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    parent_preserve_world(obj, armature)
    add_armature_modifier(obj, armature)


def create_curve_mesh(
    name: str,
    points: Iterable[Vector],
    bevel_depth: float,
    material: bpy.types.Material,
    *,
    cyclic: bool = False,
    bevel_resolution: int = 2,
    resolution: int = 2,
) -> bpy.types.Object:
    point_list = [Vector(point) for point in points]
    curve_data = bpy.data.curves.new(f"{name}_Curve", "CURVE")
    curve_data.dimensions = "3D"
    curve_data.resolution_u = resolution
    curve_data.bevel_depth = bevel_depth
    curve_data.bevel_resolution = bevel_resolution
    curve_data.resolution_u = resolution
    spline = curve_data.splines.new("NURBS")
    spline.points.add(len(point_list) - 1)
    for spline_point, point in zip(spline.points, point_list, strict=True):
        spline_point.co = (*point, 1.0)
    spline.use_cyclic_u = cyclic
    spline.use_endpoint_u = not cyclic
    spline.order_u = min(3, len(point_list))

    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.scene.collection.objects.link(obj)
    curve_data.materials.append(material)
    set_active(obj)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    shade_smooth(obj)
    return obj


def create_ellipse(
    name: str,
    center: Vector,
    radius_x: float,
    radius_y: float,
    bevel_depth: float,
    material: bpy.types.Material,
    *,
    count: int = 40,
) -> bpy.types.Object:
    points = [
        Vector(
            (
                center.x + math.cos(index / count * math.tau) * radius_x,
                center.y + math.sin(index / count * math.tau) * radius_y,
                center.z,
            )
        )
        for index in range(count)
    ]
    return create_curve_mesh(
        name,
        points,
        bevel_depth,
        material,
        cyclic=True,
        bevel_resolution=3,
    )


def create_scaled_ico(
    name: str,
    location: Vector,
    scale: Vector,
    material: bpy.types.Material,
    *,
    subdivisions: int = 2,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions,
        radius=1.0,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign_material(obj, material)
    shade_smooth(obj)
    return obj


def create_beveled_box(
    name: str,
    location: Vector,
    scale: Vector,
    material: bpy.types.Material,
    *,
    rotation: Vector | None = None,
    bevel: float = 0.02,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    if rotation is not None:
        obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("WendaoBevel", "BEVEL")
    modifier.width = bevel
    modifier.segments = 3
    apply_modifier(obj, modifier.name)
    assign_material(obj, material)
    shade_smooth(obj)
    return obj


def create_ribbon(
    name: str,
    points: Iterable[Vector],
    width: float,
    thickness: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    point_list = [Vector(point) for point in points]
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for index, point in enumerate(point_list):
        previous = point_list[max(0, index - 1)]
        following = point_list[min(len(point_list) - 1, index + 1)]
        tangent = (following - previous).normalized()
        across = Vector((-tangent.z, 0.0, tangent.x)).normalized() * width * 0.5
        vertices.append(tuple(point - across))
        vertices.append(tuple(point + across))
    for index in range(len(point_list) - 1):
        start = index * 2
        faces.append((start, start + 1, start + 3, start + 2))
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    assign_material(obj, material)
    add_solidify(obj, thickness, offset=0.0)
    shade_smooth(obj)
    return obj


def create_oval_tube(
    name: str,
    center: Vector,
    height: float,
    material: bpy.types.Material,
    *,
    rings: list[tuple[float, float, float, float]],
    segments: int = 28,
    thickness: float = 0.008,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for z_ratio, radius_x, radius_y, y_offset in rings:
        for index in range(segments):
            angle = index / segments * math.tau
            vertices.append(
                (
                    center.x + math.cos(angle) * radius_x * height,
                    center.y
                    + y_offset * height
                    + math.sin(angle) * radius_y * height,
                    center.z + z_ratio * height,
                )
            )
    for ring_index in range(len(rings) - 1):
        start = ring_index * segments
        next_start = (ring_index + 1) * segments
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append(
                (
                    start + index,
                    start + next_index,
                    next_start + next_index,
                    next_start + index,
                )
            )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    assign_material(obj, material)
    add_solidify(obj, thickness, offset=0.0)
    shade_smooth(obj)
    return obj


def create_skirt_panel(
    name: str,
    center: Vector,
    height: float,
    material: bpy.types.Material,
    *,
    angle_center_degrees: float,
    angle_span_degrees: float = 72.0,
    waist_z: float = 0.505,
    hem_z: float = 0.335,
    angular_segments: int = 7,
    vertical_segments: int = 4,
    thickness: float = 0.008,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    center_angle = math.radians(angle_center_degrees)
    half_span = math.radians(angle_span_degrees) * 0.5
    for vertical in range(vertical_segments + 1):
        unit_z = vertical / vertical_segments
        z_ratio = waist_z + (hem_z - waist_z) * unit_z
        radius_x = (0.145 + 0.022 * unit_z) * height
        radius_y = (0.080 + 0.018 * unit_z) * height
        for horizontal in range(angular_segments + 1):
            unit_angle = horizontal / angular_segments
            angle = center_angle - half_span + unit_angle * half_span * 2.0
            wave = math.sin(unit_angle * math.pi) * 0.006 * height * unit_z
            vertices.append(
                (
                    center.x + math.cos(angle) * radius_x,
                    center.y + math.sin(angle) * radius_y - wave,
                    center.z + z_ratio * height,
                )
            )
    stride = angular_segments + 1
    for vertical in range(vertical_segments):
        for horizontal in range(angular_segments):
            current = vertical * stride + horizontal
            faces.append(
                (
                    current,
                    current + 1,
                    current + stride + 1,
                    current + stride,
                )
            )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    assign_material(obj, material)
    add_solidify(obj, thickness, offset=0.0)
    shade_smooth(obj)
    return obj


def create_bone_helix(
    armature: bpy.types.Object,
    bone_name: str,
    name: str,
    material: bpy.types.Material,
    *,
    radius: float,
    bevel_depth: float,
    start_fraction: float,
    end_fraction: float,
    turns: float,
    point_count: int = 56,
) -> bpy.types.Object:
    bone = armature.data.bones[bone_name]
    head = armature.matrix_world @ bone.head_local
    tail = armature.matrix_world @ bone.tail_local
    axis = tail - head
    length = axis.length
    if length <= 0.0001:
        raise RuntimeError(f"Zero-length bone: {bone_name}")
    direction = axis.normalized()
    reference = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(reference)) > 0.92:
        reference = Vector((1.0, 0.0, 0.0))
    first = direction.cross(reference).normalized()
    second = direction.cross(first).normalized()
    points = []
    for index in range(point_count):
        unit = index / (point_count - 1)
        fraction = start_fraction + (end_fraction - start_fraction) * unit
        angle = unit * turns * math.tau
        center = head + direction * length * fraction
        points.append(
            center
            + first * math.cos(angle) * radius
            + second * math.sin(angle) * radius
        )
    obj = create_curve_mesh(
        name,
        points,
        bevel_depth,
        material,
        bevel_resolution=2,
    )
    bind_rigid(obj, armature, bone_name)
    return obj


def create_human(profile: dict) -> bpy.types.Object:
    human_service = dynamic_import(
        "mpfb.services.humanservice",
        "HumanService",
    )
    target_service = dynamic_import(
        "mpfb.services.targetservice",
        "TargetService",
    )
    location_service = dynamic_import(
        "mpfb.services.locationservice",
        "LocationService",
    )
    macro = target_service.get_default_macro_info_dict()
    macro.update(
        {
            key: float(value)
            for key, value in profile.get("macro", {}).items()
        }
    )
    human = human_service.create_human(
        mask_helpers=True,
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=macro,
    )
    targets_root = Path(location_service.get_mpfb_data("targets"))
    for target in profile.get("detail_targets", []):
        target_path = targets_root / target["path"]
        if not target_path.is_file():
            raise FileNotFoundError(target_path)
        target_service.load_target(
            human,
            str(target_path),
            weight=float(target.get("weight", 1.0)),
        )
    bpy.context.view_layer.update()
    return human


def build_base_surfaces(
    human: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    profile: dict,
) -> tuple[bpy.types.Object, list[bpy.types.Object], list[bpy.types.Object]]:
    body_reference = group_vertex_bounds(human, ["body"])
    cloth_reference = group_vertex_bounds(human, ["helper-tights"])
    geometry = profile["geometry"]

    body = extract_surface(
        human,
        ["body"],
        "Cultivator_Body",
        reference_bounds=body_reference,
    )
    assign_material(body, materials["skin"])
    shade_smooth(body)
    decimate(body, float(geometry.get("body_decimate_ratio", 1.0)))

    garments: list[bpy.types.Object] = []
    underrobe = extract_surface(
        human,
        ["helper-tights"],
        "Cultivator_UnderRobe",
        reference_bounds=cloth_reference,
        predicate=lambda _co, normalized: (
            0.46 <= normalized.z <= 0.89
        ),
    )
    assign_material(underrobe, materials["linen"])
    garments.append(underrobe)

    outer_vest = extract_surface(
        human,
        ["helper-tights"],
        "Cultivator_OuterVest",
        reference_bounds=cloth_reference,
        predicate=lambda _co, normalized: (
            0.49 <= normalized.z <= 0.86
            and abs(normalized.x) <= 0.165
        ),
    )
    assign_material(outer_vest, materials["robe_blue"])
    garments.append(outer_vest)

    pants = extract_surface(
        human,
        ["helper-tights"],
        "Cultivator_Pants",
        reference_bounds=cloth_reference,
        predicate=lambda _co, normalized: 0.06 <= normalized.z <= 0.56,
    )
    assign_material(pants, materials["pants"])
    garments.append(pants)

    boots = extract_surface(
        human,
        ["helper-tights"],
        "Cultivator_Boots",
        reference_bounds=cloth_reference,
        predicate=lambda _co, normalized: normalized.z <= 0.25,
    )
    assign_material(boots, materials["leather"])
    garments.append(boots)

    belt_band = extract_surface(
        human,
        ["helper-tights"],
        "Cultivator_BeltBand",
        reference_bounds=cloth_reference,
        predicate=lambda _co, normalized: 0.492 <= normalized.z <= 0.545,
    )
    assign_material(belt_band, materials["leather"])
    garments.append(belt_band)

    cloth_offset = float(geometry["cloth_offset"])
    outer_offset = float(geometry["outer_cloth_offset"])
    thickness = float(geometry["cloth_thickness"])
    for garment in garments:
        offset_along_normals(
            garment,
            outer_offset
            if garment.name in {"Cultivator_OuterVest", "Cultivator_BeltBand"}
            else cloth_offset,
        )
        add_solidify(
            garment,
            thickness * (0.55 if garment.name == "Cultivator_OuterVest" else 1.0),
            offset=0.0,
        )
        shade_smooth(garment)

    face_parts: list[bpy.types.Object] = []
    for group_name, object_name in (
        ("helper-l-eye", "Cultivator_Eye_L"),
        ("helper-r-eye", "Cultivator_Eye_R"),
    ):
        eye = extract_surface(
            human,
            [group_name],
            object_name,
            reference_bounds=group_vertex_bounds(human, [group_name]),
        )
        assign_material(eye, materials["eye_white"])
        shade_smooth(eye)
        face_parts.append(eye)

    hair_cap = extract_surface(
        human,
        ["helper-hair"],
        "Cultivator_HairCap",
        reference_bounds=group_vertex_bounds(human, ["helper-hair"]),
    )
    assign_material(hair_cap, materials["hair"])
    offset_along_normals(hair_cap, cloth_offset * 0.75)
    add_solidify(hair_cap, thickness * 0.6, offset=0.0)
    shade_smooth(hair_cap)
    face_parts.append(hair_cap)
    return body, garments, face_parts


def style_underrobe(
    underrobe: bpy.types.Object,
    body_bounds: Bounds,
    materials: dict[str, bpy.types.Material],
) -> None:
    underrobe.data.materials.clear()
    underrobe.data.materials.append(materials["linen"])
    underrobe.data.materials.append(materials["robe_blue"])
    height = body_bounds.size.z
    center_x = body_bounds.center.x
    minimum_z = body_bounds.minimum.z
    for polygon in underrobe.data.polygons:
        polygon_center = sum(
            (
                underrobe.data.vertices[index].co
                for index in polygon.vertices
            ),
            Vector(),
        ) / len(polygon.vertices)
        normalized_x = abs(polygon_center.x - center_x) / height
        normalized_z = (polygon_center.z - minimum_z) / height
        polygon.material_index = (
            1
            if normalized_x <= 0.135 and 0.49 <= normalized_z <= 0.84
            else 0
        )


def build_face_and_hair(context: PipelineContext) -> list[bpy.types.Object]:
    body_bounds = context.body_bounds
    height = context.character_height
    center = body_bounds.center
    front = body_bounds.minimum.y
    back = body_bounds.maximum.y
    top = body_bounds.maximum.z
    geometry = context.profile["geometry"]
    bevel = float(geometry["hair_bevel"])
    hair = context.materials["hair"]
    dark_eye = context.materials["eye_dark"]
    created: list[bpy.types.Object] = []

    bangs: tuple[tuple[float, float], ...] = ()
    for index, (x_ratio, drop_ratio) in enumerate(bangs):
        points = [
            Vector(
                (
                    center.x + x_ratio * height * 0.65,
                    front + 0.010 * height,
                    top - 0.018 * height,
                )
            ),
            Vector(
                (
                    center.x + x_ratio * height * 0.92,
                    front - 0.008 * height,
                        top - 0.055 * height,
                )
            ),
            Vector(
                (
                    center.x + x_ratio * height,
                    front - 0.012 * height,
                    top - drop_ratio * height,
                )
            ),
        ]
        strand = create_curve_mesh(
            f"Cultivator_Bang_{index:02d}",
            points,
            bevel * (0.78 + index % 2 * 0.12),
            hair,
            bevel_resolution=2,
        )
        bind_rigid(strand, context.armature, "Head")
        created.append(strand)

    for side, sign in (("L", -1.0), ("R", 1.0)):
        side_lock = create_curve_mesh(
            f"Cultivator_SideLock_{side}",
            [
                Vector(
                    (
                        center.x + sign * 0.060 * height,
                        front + 0.005 * height,
                        top - 0.045 * height,
                    )
                ),
                Vector(
                    (
                        center.x + sign * 0.073 * height,
                        front - 0.004 * height,
                        top - 0.120 * height,
                    )
                ),
                Vector(
                    (
                        center.x + sign * 0.061 * height,
                        front + 0.004 * height,
                        top - 0.185 * height,
                    )
                ),
            ],
            bevel * 0.9,
            hair,
            bevel_resolution=2,
        )
        bind_rigid(side_lock, context.armature, "Head")
        created.append(side_lock)

    tail_anchor = Vector(
        (
            center.x,
            back + 0.015 * height,
            top - 0.055 * height,
        )
    )
    clump_count = int(geometry.get("hair_clump_count", 11))
    for index in range(clump_count):
        unit = index / max(clump_count - 1, 1)
        offset = (unit - 0.5) * 0.065 * height
        wave = math.sin(unit * math.tau) * 0.018 * height
        strand = create_curve_mesh(
            f"Cultivator_Ponytail_{index:02d}",
            [
                tail_anchor
                + Vector((offset * 0.35, 0.0, -abs(offset) * 0.12)),
                tail_anchor
                + Vector((offset * 0.75, 0.075 * height, -0.085 * height)),
                tail_anchor
                + Vector((offset + wave, 0.125 * height, -0.205 * height)),
                tail_anchor
                + Vector((offset * 0.65, 0.110 * height, -0.330 * height)),
            ],
            bevel * (0.75 + (index % 3) * 0.08),
            hair,
            bevel_resolution=2,
        )
        bind_rigid(strand, context.armature, "Head")
        created.append(strand)

    tie = create_ellipse(
        "Cultivator_HairTie",
        tail_anchor + Vector((0.0, 0.002 * height, -0.008 * height)),
        0.034 * height,
        0.022 * height,
        bevel * 0.65,
        context.materials["bronze"],
        count=32,
    )
    tie.rotation_euler.x = math.radians(90.0)
    bind_rigid(tie, context.armature, "Head")
    created.append(tie)

    eye_meshes = [
        obj
        for obj in context.generated_meshes
        if obj.name in {"Cultivator_Eye_L", "Cultivator_Eye_R"}
    ]
    for eye in eye_meshes:
        eye_bounds = mesh_bounds(eye)
        width = eye_bounds.size.x
        pupil = create_scaled_ico(
            eye.name.replace("Eye_", "Pupil_"),
            Vector(
                (
                    eye_bounds.center.x,
                    eye_bounds.minimum.y - 0.0025 * height,
                    eye_bounds.center.z,
                )
            ),
            Vector((width * 0.24, width * 0.07, width * 0.30)),
            dark_eye,
            subdivisions=2,
        )
        bind_rigid(pupil, context.armature, "Head")
        created.append(pupil)

        eyebrow = create_curve_mesh(
            eye.name.replace("Eye_", "Eyebrow_"),
            [
                Vector(
                    (
                        eye_bounds.center.x - width * 0.55,
                        front - 0.004 * height,
                        eye_bounds.maximum.z + 0.012 * height,
                    )
                ),
                Vector(
                    (
                        eye_bounds.center.x,
                        front - 0.006 * height,
                        eye_bounds.maximum.z + 0.019 * height,
                    )
                ),
                Vector(
                    (
                        eye_bounds.center.x + width * 0.58,
                        front - 0.004 * height,
                        eye_bounds.maximum.z + 0.010 * height,
                    )
                ),
            ],
            bevel * 0.34,
            hair,
            bevel_resolution=2,
        )
        bind_rigid(eyebrow, context.armature, "Head")
        created.append(eyebrow)
    return created


def build_clothing_details(context: PipelineContext) -> list[bpy.types.Object]:
    bounds = context.body_bounds
    height = context.character_height
    center = bounds.center
    front = center.y - 0.086 * height
    geometry = context.profile["geometry"]
    created: list[bpy.types.Object] = []

    origin = Vector((center.x, center.y, bounds.minimum.z))
    panel_materials = (
        context.materials["robe_blue_dark"],
        context.materials["robe_blue"],
        context.materials["robe_blue_dark"],
        context.materials["linen"],
    )
    for index, angle in enumerate((-135.0, -45.0, 45.0, 135.0)):
        panel = create_skirt_panel(
            f"Cultivator_RobePanel_{index:02d}",
            origin,
            height,
            panel_materials[index],
            angle_center_degrees=angle,
            thickness=float(geometry["cloth_thickness"]),
        )
        transfer_weights(context.body, panel, context.armature)
        created.append(panel)

    collar_specs = (
        (
            "Cultivator_Collar_L",
            [
                (-0.016, 0.825),
                (0.045, 0.735),
                (0.080, 0.650),
            ],
        ),
        (
            "Cultivator_Collar_R",
            [
                (0.016, 0.825),
                (-0.045, 0.735),
                (-0.080, 0.650),
            ],
        ),
    )
    for name, normalized_points in collar_specs:
        collar = create_ribbon(
            name,
            [
                Vector(
                    (
                        center.x + x_ratio * height,
                        front - 0.010 * height,
                        bounds.minimum.z + z_ratio * height,
                    )
                )
                for x_ratio, z_ratio in normalized_points
            ],
            0.013 * height,
            0.0025 * height,
            context.materials["linen"],
        )
        bind_rigid(collar, context.armature, "Torso")
        created.append(collar)
    return created


def create_jian(context: PipelineContext) -> list[bpy.types.Object]:
    height = context.character_height
    bounds = context.body_bounds
    center = bounds.center
    back = bounds.maximum.y
    blade_length = 0.55 * height
    blade_width = 0.024 * height
    blade_thickness = 0.0065 * height
    grip_length = 0.115 * height

    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    sections = (
        (0.0, blade_width, blade_thickness),
        (blade_length * 0.90, blade_width * 0.82, blade_thickness),
    )
    for z, width, thickness in sections:
        vertices.extend(
            (
                (-width, 0.0, z),
                (0.0, -thickness, z),
                (width, 0.0, z),
                (0.0, thickness, z),
            )
        )
    faces.extend(
        (
            (0, 4, 5, 1),
            (1, 5, 6, 2),
            (2, 6, 7, 3),
            (3, 7, 4, 0),
            (0, 1, 2, 3),
        )
    )
    tip = len(vertices)
    vertices.append((0.0, 0.0, blade_length))
    faces.extend(
        (
            (4, tip, 5),
            (5, tip, 6),
            (6, tip, 7),
            (7, tip, 4),
        )
    )
    mesh = bpy.data.meshes.new("Cultivator_Jian_Blade_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    blade = bpy.data.objects.new("Cultivator_Jian_Blade", mesh)
    bpy.context.scene.collection.objects.link(blade)
    assign_material(blade, context.materials["steel"])
    shade_smooth(blade)

    guard = create_beveled_box(
        "Cultivator_Jian_Guard",
        Vector((0.0, 0.0, -0.010 * height)),
        Vector((0.082 * height, 0.016 * height, 0.016 * height)),
        context.materials["bronze"],
        bevel=0.010 * height,
    )
    grip = create_beveled_box(
        "Cultivator_Jian_Grip",
        Vector((0.0, 0.0, -grip_length * 0.55)),
        Vector((0.018 * height, 0.018 * height, grip_length * 0.5)),
        context.materials["leather"],
        bevel=0.007 * height,
    )
    pommel = create_scaled_ico(
        "Cultivator_Jian_Pommel",
        Vector((0.0, 0.0, -grip_length * 1.08)),
        Vector((0.027 * height, 0.022 * height, 0.028 * height)),
        context.materials["bronze"],
        subdivisions=2,
    )
    sword_parts = [blade, guard, grip, pommel]
    sword_matrix = (
        Matrix.Translation(
            Vector(
                (
                    center.x + 0.12 * height,
                    back + 0.040 * height,
                    bounds.minimum.z + 0.39 * height,
                )
            )
        )
        @ Matrix.Rotation(math.radians(-28.0), 4, "Y")
        @ Matrix.Rotation(math.radians(5.0), 4, "X")
    )
    for obj in sword_parts:
        obj.matrix_world = sword_matrix @ obj.matrix_world
        bind_rigid(obj, context.armature, "Torso")
    return sword_parts


def build_accessories(context: PipelineContext) -> list[bpy.types.Object]:
    bounds = context.body_bounds
    height = context.character_height
    center = bounds.center
    front = center.y - 0.086 * height
    created = create_jian(context)

    pouch = create_beveled_box(
        "Cultivator_HerbPouch",
        Vector(
            (
                center.x - 0.155 * height,
                front - 0.012 * height,
                bounds.minimum.z + 0.455 * height,
            )
        ),
        Vector((0.030 * height, 0.018 * height, 0.040 * height)),
        context.materials["leather_worn"],
        rotation=Vector((0.0, 0.0, math.radians(-8.0))),
        bevel=0.010 * height,
    )
    bind_rigid(pouch, context.armature, "Hips")
    created.append(pouch)

    talisman = create_beveled_box(
        "Cultivator_JadeTalisman",
        Vector(
            (
                center.x + 0.145 * height,
                front - 0.018 * height,
                bounds.minimum.z + 0.435 * height,
            )
        ),
        Vector((0.015 * height, 0.005 * height, 0.028 * height)),
        context.materials["jade"],
        rotation=Vector((math.radians(8.0), 0.0, math.radians(-8.0))),
        bevel=0.008 * height,
    )
    bind_rigid(talisman, context.armature, "Hips")
    created.append(talisman)

    cord = create_curve_mesh(
        "Cultivator_TalismanCord",
        [
            Vector(
                (
                    center.x + 0.122 * height,
                    front - 0.012 * height,
                    bounds.minimum.z + 0.505 * height,
                )
            ),
            Vector(
                (
                    center.x + 0.145 * height,
                    front - 0.018 * height,
                    bounds.minimum.z + 0.475 * height,
                )
            ),
            talisman.location
            + Vector((0.0, 0.0, 0.045 * height)),
        ],
        0.006 * height,
        context.materials["leather_worn"],
        bevel_resolution=2,
    )
    bind_rigid(cord, context.armature, "Hips")
    created.append(cord)
    return created


def delete_objects(objects: Iterable[bpy.types.Object]) -> None:
    targets = [obj for obj in objects if obj is not None]
    if not targets:
        return
    bpy.ops.object.select_all(action="DESELECT")
    for obj in targets:
        if obj.name in bpy.context.scene.objects:
            obj.hide_set(False)
            obj.hide_viewport = False
            obj.select_set(True)
    bpy.ops.object.delete(use_global=False)


def select_export_objects(
    armature: bpy.types.Object,
    meshes: Iterable[bpy.types.Object],
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.hide_viewport = False
    armature.select_set(True)
    for obj in meshes:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature


def export_fbx(
    path: Path,
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    select_export_objects(armature, meshes)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        path_mode="STRIP",
    )


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(
    name: str,
    location: Vector,
    target: Vector,
    energy: float,
    color: tuple[float, float, float],
    size: float,
) -> bpy.types.Object:
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.color = color
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    look_at(light, target)
    return light


def render_preview(
    path: Path,
    context: PipelineContext,
) -> None:
    armature = context.armature
    if armature.animation_data is not None:
        armature.animation_data.action = None
    armature.data.pose_position = "REST"
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()

    bounds = bounds_for_objects(context.generated_meshes)
    target = bounds.center + Vector((0.0, 0.0, bounds.size.z * 0.03))
    distance = max(bounds.size.z * 2.15, 5.5)
    camera_data = bpy.data.cameras.new("G0907PreviewCamera")
    camera = bpy.data.objects.new("G0907PreviewCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = target + Vector(
        (bounds.size.z * 0.32, -distance, bounds.size.z * 0.08)
    )
    camera_data.lens = 64.0
    look_at(camera, target)
    bpy.context.scene.camera = camera

    preview_objects = [
        camera,
        add_area_light(
            "G0907Key",
            target + Vector((3.8, -4.2, 4.8)),
            target,
            1050.0,
            (1.0, 0.86, 0.70),
            4.2,
        ),
        add_area_light(
            "G0907Fill",
            target + Vector((-3.3, -1.8, 3.2)),
            target,
            620.0,
            (0.50, 0.72, 1.0),
            4.8,
        ),
        add_area_light(
            "G0907Rim",
            target + Vector((2.5, 3.8, 4.8)),
            target,
            950.0,
            (0.44, 0.82, 0.72),
            3.4,
        ),
    ]
    bpy.ops.mesh.primitive_plane_add(
        size=max(bounds.size.z * 5.0, 15.0),
        location=(bounds.center.x, bounds.center.y, bounds.minimum.z - 0.008),
    )
    floor = bpy.context.object
    floor.name = "G0907PreviewFloor"
    floor_material = make_material(
        "G0907PreviewFloorMaterial",
        {
            "rgba": [0.055, 0.068, 0.066, 1.0],
            "roughness": 0.92,
            "metallic": 0.0,
        },
    )
    assign_material(floor, floor_material)
    preview_objects.append(floor)

    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(path)
    scene.world.color = (0.015, 0.022, 0.021)
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    delete_objects(preview_objects)


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_character(context: PipelineContext) -> dict:
    meshes = context.generated_meshes
    total_triangles = sum(triangle_count(obj) for obj in meshes)
    budget = int(context.shared["triangle_budget"]["lod0"])
    if total_triangles > budget:
        raise RuntimeError(
            f"LOD0 triangle budget exceeded: {total_triangles} > {budget}"
        )
    if len(context.armature.data.bones) < 30:
        raise RuntimeError("Animation armature has too few bones")

    actions = sorted(action.name for action in bpy.data.actions)
    required_action_tokens = ("|Idle", "|Run", "|Death")
    for token in required_action_tokens:
        if not any(name.endswith(token) for name in actions):
            raise RuntimeError(f"Missing required animation: {token}")

    finite_vertices = True
    for obj in meshes:
        for vertex in obj.data.vertices:
            if not all(math.isfinite(value) for value in vertex.co):
                finite_vertices = False
                break
    if not finite_vertices:
        raise RuntimeError("Generated character contains non-finite vertices")

    bounds = bounds_for_objects(meshes)
    ground_error = abs(bounds.minimum.z - context.target_bounds.minimum.z)
    if ground_error > context.character_height * 0.025:
        raise RuntimeError(
            f"Foot grounding drift is too large: {ground_error:.4f}"
        )

    body_deform_groups = [
        group.name
        for group in context.body.vertex_groups
        if group.name in context.armature.data.bones
    ]
    if len(body_deform_groups) < 15:
        raise RuntimeError(
            f"Body has too few deform groups: {len(body_deform_groups)}"
        )

    return {
        "total_triangles": total_triangles,
        "triangle_budget": budget,
        "bounds": {
            "minimum": [round(value, 6) for value in bounds.minimum],
            "maximum": [round(value, 6) for value in bounds.maximum],
            "size": [round(value, 6) for value in bounds.size],
        },
        "ground_error": round(ground_error, 6),
        "bone_count": len(context.armature.data.bones),
        "body_deform_group_count": len(body_deform_groups),
        "actions": actions,
        "objects": [
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "triangles": triangle_count(obj),
                "materials": [
                    slot.material.name
                    for slot in obj.material_slots
                    if slot.material is not None
                ],
            }
            for obj in sorted(meshes, key=lambda item: item.name)
        ],
    }


def write_manifest(
    path: Path,
    context: PipelineContext,
    validation: dict,
    blend_path: Path,
    fbx_path: Path,
    preview_path: Path | None,
) -> None:
    manifest = {
        "schema_version": 1,
        "name": context.profile.get("display_name", context.profile_name),
        "profile": context.profile_name,
        "generator": "tools/blender/character_pipeline.py",
        "blender_version": bpy.app.version_string,
        "mpfb_version": "2.0.16",
        "licenses": {
            "mpfb_code": "GPL-3.0-or-later (build tool only, not shipped)",
            "mpfb_assets": "CC0-1.0",
            "quaternius_animation_rig": "CC0-1.0",
            "project_generated_geometry": "project-owned",
        },
        "sources": {
            "design_reference": context.config["design_reference"],
            "source_rig": context.shared["source_rig"],
        },
        "outputs": {
            "blend": str(blend_path.relative_to(REPO_ROOT)),
            "fbx": str(fbx_path.relative_to(REPO_ROOT)),
            "preview": (
                str(preview_path.relative_to(REPO_ROOT))
                if preview_path is not None
                else None
            ),
        },
        "validation": validation,
        "hashes": {
            "blend_sha256": sha256(blend_path),
            "fbx_sha256": sha256(fbx_path),
            "preview_sha256": (
                sha256(preview_path)
                if preview_path is not None and preview_path.is_file()
                else None
            ),
        },
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as stream:
        json.dump(manifest, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def prepare_pipeline(
    config: dict,
    shared: dict,
    profile_name: str,
    profile: dict,
) -> PipelineContext:
    reset_scene()
    materials = build_materials(shared)
    source_path = resolve_repo_path(shared["source_rig"])
    if not source_path.is_file():
        raise FileNotFoundError(source_path)

    source_objects = import_fbx(source_path)
    armature = find_unique(
        source_objects,
        name=shared["rig_name"],
        object_type="ARMATURE",
    )
    armature.data.pose_position = "REST"
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    donor = find_unique(
        source_objects,
        name=shared["source_body_names"][0],
        object_type="MESH",
    )
    target_bounds = mesh_bounds(donor)

    human = create_human(profile)
    body, garments, face_parts = build_base_surfaces(
        human,
        materials,
        profile,
    )
    all_surfaces = [body, *garments, *face_parts]
    current_body_bounds = mesh_bounds(body)
    transform_meshes(
        all_surfaces,
        current_body_bounds,
        target_bounds,
        float(shared["target_source_height_ratio"]),
    )
    current_body_bounds = mesh_bounds(body)

    transfer_weights(donor, body, armature)
    for garment in garments:
        transfer_weights(body, garment, armature)
    for face_part in face_parts:
        bind_rigid(face_part, armature, "Head")

    delete_objects([human])
    generated_meshes = [body, *garments, *face_parts]
    context = PipelineContext(
        config=config,
        profile_name=profile_name,
        profile=profile,
        shared=shared,
        materials=materials,
        source_objects=source_objects,
        armature=armature,
        donor=donor,
        body=body,
        generated_meshes=generated_meshes,
        body_bounds=bounds_for_objects(generated_meshes),
        target_bounds=target_bounds,
        character_height=target_bounds.size.z
        * float(shared["target_source_height_ratio"]),
    )

    context.generated_meshes.extend(build_face_and_hair(context))
    context.generated_meshes.extend(build_clothing_details(context))
    context.generated_meshes.extend(build_accessories(context))

    source_meshes = [
        obj
        for obj in source_objects
        if obj.type == "MESH" and obj not in context.generated_meshes
    ]
    delete_objects(source_meshes)
    context.source_objects = [armature]
    context.body_bounds = bounds_for_objects(context.generated_meshes)
    armature.data.pose_position = "POSE"
    return context


def main() -> None:
    args = parse_args()
    config_path = resolve_repo_path(args.config)
    config, shared, profile = load_config(config_path, args.profile)
    context = prepare_pipeline(
        config,
        shared,
        args.profile,
        profile,
    )

    output = profile["output"]
    blend_path = resolve_repo_path(output["blend"])
    fbx_path = resolve_repo_path(output["fbx"])
    manifest_path = resolve_repo_path(output["manifest"])
    preview_path = (
        None
        if args.skip_preview
        else resolve_repo_path(output["preview"])
    )

    validation = validate_character(context)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    export_fbx(fbx_path, context.armature, context.generated_meshes)
    if preview_path is not None:
        render_preview(preview_path, context)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    write_manifest(
        manifest_path,
        context,
        validation,
        blend_path,
        fbx_path,
        preview_path,
    )
    print(
        "G09-07 character pipeline complete: "
        f"profile={args.profile} "
        f"triangles={validation['total_triangles']} "
        f"bones={validation['bone_count']} "
        f"fbx={fbx_path}"
    )


if __name__ == "__main__":
    main()
