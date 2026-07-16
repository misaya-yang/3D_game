"""Render an action from an exported FBX to verify animation baking."""

from __future__ import annotations

import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]


def import_fbx(path: Path) -> None:
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    color: tuple[float, float, float],
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = 5.0
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, Vector((0.0, 0.0, 1.35)))


def main() -> None:
    try:
        separator = sys.argv.index("--")
        fbx_path = (REPO_ROOT / sys.argv[separator + 1]).resolve()
        action_suffix = sys.argv[separator + 2]
        output_path = (REPO_ROOT / sys.argv[separator + 3]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit(
            "Expected FBX path, action suffix and output path after --"
        ) from exc

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    import_fbx(fbx_path)
    armature = next(
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    )
    action = next(
        (
            candidate
            for candidate in bpy.data.actions
            if candidate.name.endswith(action_suffix)
        ),
        None,
    )
    if action is None:
        raise RuntimeError(f"Action not found: {action_suffix}")
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.data.pose_position = "POSE"
    armature.animation_data.action = action
    start, end = action.frame_range
    bpy.context.scene.frame_set(round(start + (end - start) * 0.35))

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = (0.0, -6.0, 1.42)
    camera_data.lens = 58.0
    look_at(camera, Vector((0.0, 0.0, 1.35)))
    bpy.context.scene.camera = camera
    add_area("Key", (3.5, -4.0, 4.5), 950.0, (1.0, 0.88, 0.72))
    add_area("Fill", (-3.0, -1.5, 3.2), 650.0, (0.62, 0.78, 1.0))
    add_area("Rim", (2.0, 3.0, 4.8), 800.0, (0.72, 0.86, 1.0))

    scene = bpy.context.scene
    scene.world.color = (0.045, 0.055, 0.065)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    print(
        f"Rendered {action.name} at frame {scene.frame_current}: {output_path}"
    )


if __name__ == "__main__":
    main()
