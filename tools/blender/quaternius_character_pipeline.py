"""G09-07 production character pipeline using curated CC0 modular assets.

The visual source is Quaternius Universal Base Characters plus the free
Ranger/Peasant subset from Modular Character Outfits - Fantasy. Blender runs
headlessly and performs deterministic assembly, palette grading, weight
transfer to the project's existing animated rig, validation and export.

Run through:
    ./tools/blender/run_character_pipeline.sh --profile cultivator
"""

from __future__ import annotations

import argparse
import bmesh
import hashlib
import json
import math
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import character_pipeline as common


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CONFIG = (
    REPO_ROOT
    / "tools"
    / "blender"
    / "config"
    / "g09_07_modular_characters.json"
)


@dataclass
class ModularContext:
    config: dict
    shared: dict
    profile_name: str
    profile: dict
    selected_root: Path
    armature: bpy.types.Object
    donor: bpy.types.Object
    generated_meshes: list[bpy.types.Object]
    materials: dict[str, bpy.types.Material]
    body_bounds: common.Bounds
    target_bounds: common.Bounds
    character_height: float
    selected_sources: list[Path]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", default="cultivator")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG))
    parser.add_argument(
        "--asset-cache",
        default=os.environ.get("WENDAO_ASSET_CACHE", "/tmp/wendao-assets"),
    )
    parser.add_argument(
        "--skip-preview",
        action="store_true",
        help="Export and validate without the single final offline preview.",
    )
    try:
        separator = sys.argv.index("--")
        return parser.parse_args(sys.argv[separator + 1 :])
    except ValueError:
        return parser.parse_args([])


def load_config(
    path: Path,
    profile_name: str,
) -> tuple[dict, dict, dict]:
    with path.open("r", encoding="utf-8") as stream:
        config = json.load(stream)
    if config.get("source_backend") != "quaternius_modular":
        raise RuntimeError(
            f"Unsupported production backend: {config.get('source_backend')}"
        )
    profiles = config.get("profiles", {})
    if profile_name not in profiles:
        raise KeyError(f"Unknown character profile: {profile_name}")
    return config, config["shared"], profiles[profile_name]


def source_path(root: Path, relative: str) -> Path:
    path = (root / relative).resolve()
    if not path.is_file():
        raise FileNotFoundError(
            f"Missing selected character source: {path}\n"
            "Run ./tools/art/sync_character_sources.sh first."
        )
    return path


def bake_world_transform(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        return
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.data.transform(world)
    obj.matrix_world = Matrix.Identity(4)
    for modifier in tuple(obj.modifiers):
        obj.modifiers.remove(modifier)
    obj.data.update()
    obj.update_tag()
    bpy.context.view_layer.update()


def import_selected_meshes(
    path: Path,
    mesh_names: Iterable[str],
) -> list[bpy.types.Object]:
    imported = common.import_fbx(path)
    requested = set(mesh_names)
    meshes = [
        obj
        for obj in imported
        if obj.type == "MESH" and obj.name in requested
    ]
    found = {obj.name for obj in meshes}
    if found != requested:
        raise RuntimeError(
            f"{path.name}: expected meshes {sorted(requested)}, "
            f"found {sorted(found)}"
        )
    for obj in meshes:
        bake_world_transform(obj)
    common.delete_objects([obj for obj in imported if obj not in meshes])
    return meshes


def extract_weighted_head(
    source: bpy.types.Object,
    name: str,
    cut_ratio: float,
) -> bpy.types.Object:
    duplicate = source.copy()
    duplicate.data = source.data.copy()
    duplicate.name = name
    duplicate.data.name = f"{name}_Mesh"
    bpy.context.scene.collection.objects.link(duplicate)

    bounds = common.mesh_bounds(duplicate)
    cut_z = bounds.minimum.z + bounds.size.z * cut_ratio
    group_indices = {
        duplicate.vertex_groups[group_name].index
        for group_name in ("Head", "neck_01")
        if group_name in duplicate.vertex_groups
    }
    if not group_indices:
        raise RuntimeError("Universal base mesh has no head or neck weights")

    mesh = bmesh.new()
    mesh.from_mesh(duplicate.data)
    deformation = mesh.verts.layers.deform.active
    remove: list[bmesh.types.BMVert] = []
    for vertex in mesh.verts:
        weights = vertex[deformation] if deformation is not None else {}
        weighted = any(
            weights.get(group_index, 0.0) > 0.025
            for group_index in group_indices
        )
        if not weighted or vertex.co.z < cut_z:
            remove.append(vertex)
    bmesh.ops.delete(mesh, geom=remove, context="VERTS")
    mesh.to_mesh(duplicate.data)
    mesh.free()
    duplicate.data.update()
    if len(duplicate.data.polygons) < 100:
        raise RuntimeError("Head extraction produced insufficient geometry")
    return duplicate


def soften_head_shape(
    head: bpy.types.Object,
    specification: dict,
) -> None:
    bounds = common.mesh_bounds(head)
    center = bounds.center
    height = max(bounds.size.z, 0.0001)
    head_width = float(specification.get("head_width_scale", 1.0))
    jaw_width = float(specification.get("jaw_width_scale", head_width))
    face_depth = float(specification.get("face_depth_scale", 1.0))

    for vertex in head.data.vertices:
        normalized_z = (vertex.co.z - bounds.minimum.z) / height
        if normalized_z <= 0.50:
            transition = max(0.0, normalized_z / 0.50)
            width_scale = jaw_width + (head_width - jaw_width) * transition
        else:
            width_scale = head_width
        vertex.co.x = center.x + (vertex.co.x - center.x) * width_scale
        vertex.co.y = center.y + (vertex.co.y - center.y) * face_depth
    head.data.update()


def make_textured_material(
    name: str,
    texture_path: Path,
    specification: dict,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    rgba = tuple(float(value) for value in specification["rgba"])
    material.diffuse_color = rgba
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (500.0, 0.0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (240.0, 0.0)
    principled.inputs["Roughness"].default_value = float(
        specification.get("roughness", 0.75)
    )
    principled.inputs["Metallic"].default_value = float(
        specification.get("metallic", 0.0)
    )

    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.pack()
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    texture.location = (-520.0, 40.0)

    links = material.node_tree.links
    # Keep the image directly connected so Blender's FBX exporter can
    # recognize and embed it. Unity applies the project palette at runtime.
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def build_materials(
    selected_root: Path,
    shared: dict,
    profile: dict,
) -> dict[str, bpy.types.Material]:
    palette = shared["palette"]
    texture_paths = profile["source"]["textures"]
    textures = {
        key: source_path(selected_root, value)
        for key, value in texture_paths.items()
    }
    ranger_cloth = dict(palette["robe_dark"])
    ranger_cloth["rgba"] = [0.29, 0.39, 0.40, 1.0]

    return {
        "skin": make_textured_material(
            "Wendao_Skin",
            textures["skin"],
            palette["skin"],
        ),
        "eye": make_textured_material(
            "Wendao_Eye",
            textures["eye"],
            palette["eye"],
        ),
        "hair": make_textured_material(
            "Wendao_Hair",
            textures["hair"],
            palette["hair"],
        ),
        "robe": make_textured_material(
            "Wendao_Robe",
            textures["peasant"],
            palette["robe"],
        ),
        "robe_dark": make_textured_material(
            "Wendao_RobeDark",
            textures["peasant"],
            palette["robe_dark"],
        ),
        "ranger_cloth": make_textured_material(
            "Wendao_RangerCloth",
            textures["ranger"],
            ranger_cloth,
        ),
        "ranger_leather": make_textured_material(
            "Wendao_RangerLeather",
            textures["ranger"],
            palette["leather"],
        ),
        "bronze": common.make_material(
            "Wendao_Bronze",
            palette["bronze"],
        ),
        "steel": common.make_material(
            "Wendao_Steel",
            palette["steel"],
        ),
        "jade": common.make_material(
            "Wendao_Jade",
            palette["jade"],
        ),
    }


def replace_material_slots(
    obj: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    role: str,
) -> None:
    original_names = [
        material.name if material is not None else ""
        for material in obj.data.materials
    ]
    polygon_material_indices = [
        polygon.material_index
        for polygon in obj.data.polygons
    ]
    replacements: list[bpy.types.Material] = []
    for original_name in original_names:
        lowered = original_name.lower()
        if "regular_male" in lowered or "superhero_male" in lowered:
            replacement = materials["skin"]
        elif "eye" in lowered:
            replacement = materials["eye"]
        elif "hair" in lowered:
            replacement = materials["hair"]
        elif role == "robe":
            replacement = materials["robe"]
        elif role == "robe_dark":
            replacement = materials["robe_dark"]
        elif role in {"boots", "bracer", "pauldron"}:
            replacement = materials["ranger_leather"]
        else:
            replacement = materials["ranger_cloth"]
        replacements.append(replacement)

    if not replacements:
        replacements = [
            materials[
                "ranger_leather"
                if role in {"boots", "bracer", "pauldron"}
                else role
            ]
        ]
    obj.data.materials.clear()
    for replacement in replacements:
        obj.data.materials.append(replacement)
    for polygon, original_index in zip(
        obj.data.polygons,
        polygon_material_indices,
        strict=True,
    ):
        polygon.material_index = min(
            original_index,
            len(replacements) - 1,
        )


def rename_outfit_mesh(
    obj: bpy.types.Object,
    role: str,
) -> None:
    if "Body_Belt" in obj.name:
        target = "Cultivator_" + obj.name.split("Male_", 1)[-1]
    elif obj.name.endswith("_Body"):
        target = "Cultivator_Body"
    elif obj.name.endswith("_Legs"):
        target = "Cultivator_Pants"
    elif "Bracer" in obj.name:
        target = "Cultivator_Bracers"
    elif "Arms" in obj.name:
        target = "Cultivator_Arms"
    elif "Boot" in obj.name or "Feet" in obj.name:
        target = "Cultivator_Boots"
    elif "Pauldron" in obj.name:
        target = "Cultivator_Pauldron"
    elif "Hood" in obj.name:
        target = "Cultivator_Hood"
    else:
        target = f"Cultivator_{obj.name}"
    obj.name = target
    obj.data.name = f"{target}_Mesh"


def import_modular_character(
    selected_root: Path,
    profile: dict,
    materials: dict[str, bpy.types.Material],
) -> tuple[list[bpy.types.Object], bpy.types.Object, list[Path]]:
    source_spec = profile["source"]
    selected_sources: list[Path] = []

    base_spec = source_spec["base_character"]
    base_path = source_path(selected_root, base_spec["path"])
    selected_sources.append(base_path)
    imported = common.import_fbx(base_path)
    base_body = common.find_unique(
        imported,
        name=base_spec["head_mesh"],
        object_type="MESH",
    )
    eyes = common.find_unique(
        imported,
        name=base_spec["eyes_mesh"],
        object_type="MESH",
    )
    eyebrows = common.find_unique(
        imported,
        name=base_spec["eyebrows_mesh"],
        object_type="MESH",
    )
    for obj in (base_body, eyes, eyebrows):
        bake_world_transform(obj)

    head = extract_weighted_head(
        base_body,
        "Cultivator_Head",
        float(base_spec["head_cut_ratio"]),
    )
    soften_head_shape(head, profile.get("shape", {}))
    eyes.name = "Cultivator_Eyes"
    eyes.data.name = "Cultivator_Eyes_Mesh"
    eyebrows.name = "Cultivator_Eyebrows"
    eyebrows.data.name = "Cultivator_Eyebrows_Mesh"
    replace_material_slots(head, materials, "skin")
    replace_material_slots(eyes, materials, "eye")
    replace_material_slots(eyebrows, materials, "hair")
    common.delete_objects(
        [obj for obj in imported if obj not in {eyes, eyebrows}]
    )

    outfit_meshes: list[bpy.types.Object] = []
    for part in source_spec["outfit_parts"]:
        part_path = source_path(selected_root, part["path"])
        selected_sources.append(part_path)
        meshes = import_selected_meshes(part_path, part["meshes"])
        for mesh in meshes:
            role = part["role"]
            if role == "ranger":
                if "Bracer" in mesh.name:
                    role = "bracer"
                elif "Boot" in mesh.name or "Feet" in mesh.name:
                    role = "boots"
                elif "Pauldron" in mesh.name or "Belt" in mesh.name:
                    role = "pauldron"
            rename_outfit_mesh(mesh, part["role"])
            replace_material_slots(mesh, materials, role)
            common.shade_smooth(mesh)
            outfit_meshes.append(mesh)

    hair_spec = source_spec["hair"]
    hair_path = source_path(selected_root, hair_spec["path"])
    selected_sources.append(hair_path)
    hair_import = common.import_fbx(hair_path)
    hair = common.find_unique(
        hair_import,
        name=hair_spec["mesh"],
        object_type="MESH",
    )
    bake_world_transform(hair)
    hair.name = "Cultivator_Hair"
    hair.data.name = "Cultivator_Hair_Mesh"
    replace_material_slots(hair, materials, "hair")
    common.shade_smooth(hair)
    common.delete_objects([obj for obj in hair_import if obj is not hair])

    meshes = [head, eyes, eyebrows, hair, *outfit_meshes]
    return meshes, head, selected_sources


def center_mesh_geometry(obj: bpy.types.Object) -> common.Bounds:
    bounds = common.mesh_bounds(obj)
    obj.data.transform(Matrix.Translation(-bounds.center))
    obj.data.update()
    return common.mesh_bounds(obj)


def orient_longest_axis_to_z(obj: bpy.types.Object) -> None:
    bounds = common.mesh_bounds(obj)
    sizes = list(bounds.size)
    longest = max(range(3), key=lambda index: sizes[index])
    if longest == 0:
        obj.data.transform(Matrix.Rotation(math.radians(-90.0), 4, "Y"))
    elif longest == 1:
        obj.data.transform(Matrix.Rotation(math.radians(90.0), 4, "X"))
    obj.data.update()


def import_weapon(
    path: Path,
    armature: bpy.types.Object,
    body_bounds: common.Bounds,
    materials: dict[str, bpy.types.Material],
) -> bpy.types.Object:
    existing_actions = set(bpy.data.actions)
    imported = common.import_fbx(path)
    sword = common.find_unique(
        imported,
        name="Warrior_Sword",
        object_type="MESH",
    )
    bake_world_transform(sword)
    common.delete_objects([obj for obj in imported if obj is not sword])
    center_mesh_geometry(sword)
    orient_longest_axis_to_z(sword)
    bounds = center_mesh_geometry(sword)

    target_length = body_bounds.size.z * 0.54
    scale = target_length / max(bounds.size.z, 0.0001)
    sword.data.transform(Matrix.Scale(scale, 4))
    sword.data.update()
    sword.name = "Cultivator_Jian"
    sword.data.name = "Cultivator_Jian_Mesh"
    common.assign_material(sword, materials["steel"])
    common.shade_smooth(sword)
    for action in tuple(bpy.data.actions):
        if action not in existing_actions:
            bpy.data.actions.remove(action)

    height = body_bounds.size.z
    sword.matrix_world = (
        Matrix.Translation(
            Vector(
                (
                    body_bounds.center.x + height * 0.075,
                    body_bounds.maximum.y + height * 0.015,
                    body_bounds.minimum.z + height * 0.49,
                )
            )
        )
        @ Matrix.Rotation(math.radians(-18.0), 4, "Y")
        @ Matrix.Rotation(math.radians(2.0), 4, "X")
    )
    common.bind_rigid(sword, armature, "Torso")
    return sword


def preserve_rig_accessories(
    source_objects: list[bpy.types.Object],
    profile: dict,
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    preserved: list[bpy.types.Object] = []
    for specification in profile.get("rig_accessories", []):
        source_name = specification["source"]
        accessory = common.find_unique(
            source_objects,
            name=source_name,
            object_type="MESH",
        )
        target_name = specification.get("target", source_name)
        accessory.name = target_name
        accessory.data.name = f"{target_name}_Mesh"
        common.assign_material(
            accessory,
            materials[specification.get("material", "steel")],
        )
        common.shade_smooth(accessory)
        preserved.append(accessory)
    return preserved


def rotate_to_project_forward(meshes: Iterable[bpy.types.Object]) -> None:
    rotation = Matrix.Rotation(math.radians(180.0), 4, "Z")
    for mesh in meshes:
        mesh.data.transform(rotation)
        mesh.data.update()


def refine_hair_fit(
    hair: bpy.types.Object,
    specification: dict,
) -> None:
    bounds = common.mesh_bounds(hair)
    height = max(bounds.size.z, 0.0001)
    depth = max(bounds.size.y, 0.0001)
    lift = float(specification.get("fringe_lift_ratio", 0.0))
    pushback = float(
        specification.get("fringe_pushback_ratio", 0.0)
    )
    for vertex in hair.data.vertices:
        front_factor = 1.0 - max(
            0.0,
            min(1.0, (vertex.co.y - bounds.minimum.y) / (depth * 0.72)),
        )
        height_factor = 1.0 - max(
            0.0,
            min(1.0, (vertex.co.z - bounds.minimum.z) / (height * 0.72)),
        )
        influence = front_factor * height_factor
        vertex.co.z += height * lift * influence
        vertex.co.y += depth * pushback * front_factor
    hair.data.update()


def transform_to_animation_rig(
    meshes: list[bpy.types.Object],
    target_bounds: common.Bounds,
    target_height_ratio: float,
) -> tuple[common.Bounds, float]:
    source_bounds = common.bounds_for_objects(meshes)
    common.transform_meshes(
        meshes,
        source_bounds,
        target_bounds,
        target_height_ratio,
    )
    transformed = common.bounds_for_objects(meshes)
    return transformed, transformed.size.z


def bind_character(
    meshes: list[bpy.types.Object],
    head: bpy.types.Object,
    donor: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    rigid_names = {
        "Cultivator_Eyes",
        "Cultivator_Eyebrows",
        "Cultivator_Hair",
    }
    for mesh in meshes:
        if mesh.name in rigid_names:
            common.bind_rigid(mesh, armature, "Head")
        else:
            common.transfer_weights(donor, mesh, armature)

    head_groups = {
        group.name
        for group in head.vertex_groups
        if group.name in armature.data.bones
    }
    if "Head" not in head_groups:
        raise RuntimeError("Transferred head is not weighted to the Head bone")


def validate_character(context: ModularContext) -> dict:
    meshes = context.generated_meshes
    total_triangles = sum(common.triangle_count(obj) for obj in meshes)
    budget = int(context.shared["triangle_budget"]["lod0"])
    if total_triangles > budget:
        raise RuntimeError(
            f"LOD0 triangle budget exceeded: {total_triangles} > {budget}"
        )
    if len(context.armature.data.bones) < 30:
        raise RuntimeError("Animation armature has too few bones")

    names = {obj.name for obj in meshes}
    required_meshes = profile_required_meshes(context.profile)
    for required in required_meshes:
        if required not in names:
            raise RuntimeError(f"Missing required visible mesh: {required}")

    actions = sorted(action.name for action in bpy.data.actions)
    for token in ("|Idle", "|Run", "|Death"):
        if not any(name.endswith(token) for name in actions):
            raise RuntimeError(f"Missing required animation: {token}")

    for obj in meshes:
        for vertex in obj.data.vertices:
            if not all(math.isfinite(value) for value in vertex.co):
                raise RuntimeError(
                    f"Non-finite vertex found in {obj.name}"
                )

    object_bounds = {
        obj.name: common.mesh_bounds(obj)
        for obj in meshes
    }
    bounds = common.bounds_for_objects(meshes)
    ground_error = abs(
        bounds.minimum.z - context.target_bounds.minimum.z
    )
    if ground_error > context.character_height * 0.03:
        diagnostics = {
            name: {
                "min": [round(value, 4) for value in item.minimum],
                "max": [round(value, 4) for value in item.maximum],
            }
            for name, item in object_bounds.items()
        }
        raise RuntimeError(
            f"Foot grounding drift is too large: {ground_error:.4f}; "
            f"object bounds={json.dumps(diagnostics, sort_keys=True)}"
        )

    body = bpy.data.objects.get("Cultivator_Body")
    deform_groups = [
        group.name
        for group in body.vertex_groups
        if group.name in context.armature.data.bones
    ]
    if len(deform_groups) < 10:
        raise RuntimeError(
            f"Body has too few deform groups: {len(deform_groups)}"
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
        "actions": actions,
        "objects": [
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "triangles": common.triangle_count(obj),
                "materials": [
                    slot.material.name
                    for slot in obj.material_slots
                    if slot.material is not None
                ],
                "deform_groups": len(
                    [
                        group
                        for group in obj.vertex_groups
                        if group.name in context.armature.data.bones
                    ]
                ),
            }
            for obj in sorted(meshes, key=lambda item: item.name)
        ],
    }


def profile_required_meshes(profile: dict) -> list[str]:
    required = [
        "Cultivator_Body",
        "Cultivator_Head",
        "Cultivator_Hair",
    ]
    if profile.get("player_sword", False):
        required.append("Cultivator_Jian")
    required.extend(
        specification.get("target", specification["source"])
        for specification in profile.get("rig_accessories", [])
    )
    return required


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(
    path: Path,
    context: ModularContext,
    validation: dict,
    blend_path: Path,
    fbx_path: Path,
    preview_path: Path | None,
) -> None:
    manifest = {
        "schema_version": 2,
        "name": context.profile.get(
            "display_name",
            context.profile_name,
        ),
        "profile": context.profile_name,
        "generator": "tools/blender/quaternius_character_pipeline.py",
        "blender_version": bpy.app.version_string,
        "licenses": {
            "universal_base_characters": "CC0-1.0",
            "modular_character_outfits_fantasy": "CC0-1.0",
            "quaternius_animation_rig": "CC0-1.0",
            "project_assembly_and_palette": "project-owned",
        },
        "source_packages": context.config["source_packages"],
        "sources": {
            "design_reference": context.config["design_reference"],
            "animation_rig": context.profile.get("rig", {}).get(
                "animation_rig",
                context.shared["animation_rig"],
            ),
            "weapon_source": (
                context.shared["weapon_source"]
                if context.profile.get("player_sword", False)
                else None
            ),
            "selected_files": [
                {
                    "path": str(path.relative_to(context.selected_root)),
                    "sha256": sha256(path),
                }
                for path in sorted(set(context.selected_sources))
            ],
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
    asset_cache: Path,
) -> ModularContext:
    common.reset_scene()
    selected_root = (
        asset_cache / shared["selected_source_root"]
    ).resolve()
    materials = build_materials(selected_root, shared, profile)

    rig_spec = profile.get("rig", {})
    rig_path = common.resolve_repo_path(
        rig_spec.get("animation_rig", shared["animation_rig"])
    )
    if not rig_path.is_file():
        raise FileNotFoundError(rig_path)
    source_rig_objects = common.import_fbx(rig_path)
    armature = common.find_unique(
        source_rig_objects,
        name=rig_spec.get("rig_name", shared["rig_name"]),
        object_type="ARMATURE",
    )
    donor = common.find_unique(
        source_rig_objects,
        name=rig_spec.get(
            "weight_donor_name",
            shared["weight_donor_name"],
        ),
        object_type="MESH",
    )
    armature.data.pose_position = "REST"
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    target_bounds = common.mesh_bounds(donor)

    meshes, head, selected_sources = import_modular_character(
        selected_root,
        profile,
        materials,
    )
    rotate_to_project_forward(meshes)
    hair = next(
        mesh
        for mesh in meshes
        if mesh.name == "Cultivator_Hair"
    )
    refine_hair_fit(hair, profile["source"]["hair"])
    body_bounds, character_height = transform_to_animation_rig(
        meshes,
        target_bounds,
        float(shared["target_height_ratio"]),
    )
    bind_character(meshes, head, donor, armature)

    meshes.extend(
        preserve_rig_accessories(
            source_rig_objects,
            profile,
            materials,
        )
    )
    if profile.get("player_sword", False):
        weapon_path = common.resolve_repo_path(shared["weapon_source"])
        if not weapon_path.is_file():
            raise FileNotFoundError(weapon_path)
        meshes.append(
            import_weapon(
                weapon_path,
                armature,
                body_bounds,
                materials,
            )
        )

    common.delete_objects(
        [
            obj
            for obj in source_rig_objects
            if obj.type == "MESH" and obj not in meshes
        ]
    )
    armature_name = rig_spec.get(
        "output_armature_name",
        "CultivatorArmature",
    )
    armature.name = armature_name
    armature.data.name = armature_name
    armature.data.pose_position = "POSE"
    bpy.context.view_layer.update()

    return ModularContext(
        config=config,
        shared=shared,
        profile_name=profile_name,
        profile=profile,
        selected_root=selected_root,
        armature=armature,
        donor=donor,
        generated_meshes=meshes,
        materials=materials,
        body_bounds=common.bounds_for_objects(meshes),
        target_bounds=target_bounds,
        character_height=character_height,
        selected_sources=selected_sources,
    )


def main() -> None:
    args = parse_args()
    # Generated source files are reproducible. Disable Blender's rolling
    # .blend1 backups so the repository does not accumulate opaque cache-like
    # copies after every headless regeneration.
    bpy.context.preferences.filepaths.save_version = 0
    config_path = common.resolve_repo_path(args.config)
    config, shared, profile = load_config(config_path, args.profile)
    context = prepare_pipeline(
        config,
        shared,
        args.profile,
        profile,
        Path(args.asset_cache),
    )

    output = profile["output"]
    blend_path = common.resolve_repo_path(output["blend"])
    fbx_path = common.resolve_repo_path(output["fbx"])
    manifest_path = common.resolve_repo_path(output["manifest"])
    preview_path = (
        None
        if args.skip_preview
        else common.resolve_repo_path(output["preview"])
    )

    validation = validate_character(context)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    common.export_fbx(
        fbx_path,
        context.armature,
        context.generated_meshes,
    )
    if preview_path is not None:
        common.render_preview(preview_path, context)
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
        "G09-07 modular character pipeline complete: "
        f"profile={args.profile} "
        f"triangles={validation['total_triangles']} "
        f"bones={validation['bone_count']} "
        f"fbx={fbx_path}"
    )


if __name__ == "__main__":
    main()
