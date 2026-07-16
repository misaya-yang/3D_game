"""Render each mesh of a character FBX for hybrid-model selection.

Usage:
    blender --background --factory-startup \
      --python tools/blender/render_character_parts.py -- \
      Assets/_Project/Resources/Art/Budget/Characters/Monk.fbx \
      docs/art/previews/g09-07/parts/monk
"""

from __future__ import annotations

import math
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
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_light(
    name: str,
    rotation: tuple[float, float, float],
    energy: float,
    color: tuple[float, float, float],
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = 5.0
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = (
        4.0 * math.sin(rotation[1]),
        -4.0 * math.cos(rotation[1]),
        3.5,
    )
    look_at(obj, Vector((0.0, 0.0, 1.35)))


def configure_texture(fbx_path: Path, meshes: list[bpy.types.Object]) -> None:
    texture_path = fbx_path.with_suffix(".png")
    if not texture_path.is_file():
        return
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    for obj in meshes:
        for material in obj.data.materials:
            if material is None:
                continue
            material.use_nodes = True
            nodes = material.node_tree.nodes
            principled = nodes.get("Principled BSDF")
            if principled is None:
                continue
            texture = nodes.new("ShaderNodeTexImage")
            texture.image = image
            material.node_tree.links.new(
                texture.outputs["Color"],
                principled.inputs["Base Color"],
            )
            material.node_tree.links.new(
                texture.outputs["Alpha"],
                principled.inputs["Alpha"],
            )
            principled.inputs["Roughness"].default_value = 0.72


def main() -> None:
    try:
        separator = sys.argv.index("--")
        fbx_path = (REPO_ROOT / sys.argv[separator + 1]).resolve()
        output_dir = (REPO_ROOT / sys.argv[separator + 2]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit("Expected FBX path and output directory after --") from exc

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    import_fbx(fbx_path)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    configure_texture(fbx_path, meshes)

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = (0.0, -6.2, 1.45)
    camera_data.lens = 58.0
    look_at(camera, Vector((0.0, 0.0, 1.35)))
    bpy.context.scene.camera = camera

    add_light("Key", (0.0, -0.65, 0.0), 900.0, (1.0, 0.9, 0.78))
    add_light("Fill", (0.0, 0.75, 0.0), 650.0, (0.65, 0.78, 1.0))
    world = bpy.context.scene.world
    world.color = (0.055, 0.065, 0.075)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    output_dir.mkdir(parents=True, exist_ok=True)

    for selected in meshes:
        for obj in meshes:
            obj.hide_render = obj is not selected
        safe_name = selected.name.replace("/", "_").replace("\\", "_")
        scene.render.filepath = str(output_dir / f"{safe_name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"Rendered {selected.name}")


if __name__ == "__main__":
    main()
