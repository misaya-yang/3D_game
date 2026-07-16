"""Render a single headless comparison sheet for FBX asset screening.

This utility deliberately emits one contact sheet instead of opening several
Blender windows or producing a screenshot for every candidate.

Usage:
    blender --background --factory-startup \
        --python tools/blender/render_fbx_lineup.py -- \
        --output /tmp/lineup.png model-a.fbx model-b.fbx model-c.fbx
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    forwarded = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("assets", nargs="+")
    return parser.parse_args(forwarded)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.actions,
        bpy.data.armatures,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def import_fbx(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))
    return [
        obj
        for obj in bpy.context.scene.objects
        if obj not in before
    ]


def mesh_points(objects: list[bpy.types.Object]) -> list[Vector]:
    return [
        obj.matrix_world @ vertex.co
        for obj in objects
        if obj.type == "MESH"
        for vertex in obj.data.vertices
    ]


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = mesh_points(objects)
    if not points:
        raise RuntimeError("Imported asset contains no mesh vertices.")
    return (
        Vector(
            (
                min(point.x for point in points),
                min(point.y for point in points),
                min(point.z for point in points),
            )
        ),
        Vector(
            (
                max(point.x for point in points),
                max(point.y for point in points),
                max(point.z for point in points),
            )
        ),
    )


def make_stone_material(
    name: str,
    color: tuple[float, float, float, float],
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = 0.86
        principled.inputs["Metallic"].default_value = 0.04
    return material


def apply_material(
    objects: list[bpy.types.Object],
    material: bpy.types.Material,
) -> None:
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for polygon in obj.data.polygons:
            polygon.material_index = 0


def pose_idle(objects: list[bpy.types.Object]) -> None:
    armature = next(
        (obj for obj in objects if obj.type == "ARMATURE"),
        None,
    )
    if armature is None:
        return
    armature.data.pose_position = "REST"
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = None
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()


def normalize_and_place(
    objects: list[bpy.types.Object],
    target_height: float,
    x_position: float,
) -> None:
    minimum, maximum = bounds(objects)
    height = maximum.z - minimum.z
    if height <= 0.0001:
        raise RuntimeError("Imported asset has zero height.")
    scale = target_height / height
    center_x = (minimum.x + maximum.x) * 0.5
    center_y = (minimum.y + maximum.y) * 0.5
    for obj in objects:
        if obj.parent is not None:
            continue
        obj.scale *= scale
        obj.location.x += x_position - center_x * scale
        obj.location.y -= center_y * scale
        obj.location.z -= minimum.z * scale


def look_at(
    obj: bpy.types.Object,
    target: Vector,
) -> None:
    obj.rotation_euler = (
        target - obj.location
    ).to_track_quat("-Z", "Y").to_euler()


def add_area(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    color: tuple[float, float, float],
    target: Vector,
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = 5.0
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, target)


def render_lineup(output: Path, asset_paths: list[Path]) -> None:
    palette = (
        (0.24, 0.29, 0.31, 1.0),
        (0.3, 0.32, 0.31, 1.0),
        (0.27, 0.3, 0.28, 1.0),
        (0.28, 0.28, 0.32, 1.0),
    )
    spacing = 3.6
    start_x = -spacing * (len(asset_paths) - 1) * 0.5
    for index, path in enumerate(asset_paths):
        if not path.is_file():
            raise FileNotFoundError(path)
        objects = import_fbx(path)
        pose_idle(objects)
        apply_material(
            objects,
            make_stone_material(
                f"CandidateStone_{index}",
                palette[index % len(palette)],
            ),
        )
        normalize_and_place(
            objects,
            target_height=3.0,
            x_position=start_x + index * spacing,
        )

    bpy.ops.mesh.primitive_plane_add(
        size=max(18.0, len(asset_paths) * spacing + 5.0),
    )
    floor = bpy.context.object
    floor.name = "LineupFloor"
    floor.data.materials.append(
        make_stone_material(
            "LineupFloorMaterial",
            (0.035, 0.042, 0.046, 1.0),
        )
    )

    target = Vector((0.0, 0.0, 1.45))
    camera_data = bpy.data.cameras.new("LineupCamera")
    camera = bpy.data.objects.new("LineupCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = (0.0, -12.0, 3.25)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(
        5.0,
        len(asset_paths) * 3.15,
    )
    look_at(camera, target)
    bpy.context.scene.camera = camera

    add_area(
        "Key",
        (5.0, -6.0, 7.0),
        1200.0,
        (1.0, 0.82, 0.66),
        target,
    )
    add_area(
        "Fill",
        (-5.0, -2.0, 4.5),
        760.0,
        (0.48, 0.68, 1.0),
        target,
    )
    add_area(
        "Rim",
        (2.0, 4.0, 6.0),
        1050.0,
        (0.52, 0.8, 1.0),
        target,
    )

    scene = bpy.context.scene
    scene.world.color = (0.012, 0.016, 0.021)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    output.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    args = parse_args()
    reset_scene()
    render_lineup(
        Path(args.output).expanduser().resolve(),
        [
            Path(asset).expanduser().resolve()
            for asset in args.assets
        ],
    )
    print(f"FBX lineup written: {args.output}")


if __name__ == "__main__":
    main()
