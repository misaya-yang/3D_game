"""Inspect the local character FBX files used by the Unity runtime.

The report is intentionally machine-readable so G09-07 can make asset choices
from the files that are already downloaded instead of guessing from filenames.

Usage:
    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --factory-startup \
        --python tools/blender/audit_character_assets.py
"""

from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_ROOT = REPO_ROOT / "Assets" / "_Project" / "Resources" / "Art" / "Budget"
OUTPUT_PATH = REPO_ROOT / "docs" / "art" / "g09-07-character-assets.json"
ASSET_PATHS = (
    ASSET_ROOT / "Characters" / "Cultivator.fbx",
    ASSET_ROOT / "Characters" / "NpcGuard_Modular.fbx",
    ASSET_ROOT / "Characters" / "NpcHealer_Modular.fbx",
    ASSET_ROOT / "Characters" / "NpcHermit_Modular.fbx",
    ASSET_ROOT / "Characters" / "Bandit_Modular.fbx",
    ASSET_ROOT / "Creatures" / "Wolf.fbx",
    ASSET_ROOT / "Creatures" / "StoneGeneral.fbx",
    ASSET_ROOT / "Creatures" / "Goleling_Evolved.fbx",
)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for armature in list(bpy.data.armatures):
        bpy.data.armatures.remove(armature)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)


def import_fbx(path: Path) -> None:
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(path))
    else:
        bpy.ops.import_scene.fbx(filepath=str(path))


def object_bounds(objects: list[bpy.types.Object]) -> dict[str, list[float]]:
    points: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(
            obj.matrix_world @ vertex.co
            for vertex in obj.data.vertices
        )

    if not points:
        return {"min": [0.0, 0.0, 0.0], "max": [0.0, 0.0, 0.0], "size": [0.0, 0.0, 0.0]}

    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    size = maximum - minimum
    return {
        "min": [round(value, 5) for value in minimum],
        "max": [round(value, 5) for value in maximum],
        "size": [round(value, 5) for value in size],
    }


def animation_names(obj: bpy.types.Object) -> list[str]:
    data = obj.animation_data
    if data is None:
        return []

    names: list[str] = []
    if data.action is not None:
        names.append(data.action.name)
    for track in data.nla_tracks:
        for strip in track.strips:
            action = getattr(strip, "action", None)
            names.append(action.name if action is not None else strip.name)
    return sorted(set(names))


def action_record(action: bpy.types.Action) -> dict[str, object]:
    frame_range = getattr(action, "frame_range", (0.0, 0.0))
    channel_count = len(getattr(action, "fcurves", ()))
    slot_count = len(getattr(action, "slots", ()))
    return {
        "name": action.name,
        "frame_range": [round(float(frame_range[0]), 3), round(float(frame_range[1]), 3)],
        "channels": channel_count,
        "slots": slot_count,
    }


def audit_asset(path: Path) -> dict[str, object]:
    reset_scene()
    import_fbx(path)
    objects = list(bpy.context.scene.objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    mesh_records = []
    for obj in meshes:
        mesh_records.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "materials": [slot.material.name if slot.material else "" for slot in obj.material_slots],
                "parent": obj.parent.name if obj.parent else "",
                "animations": animation_names(obj),
                "bounds": object_bounds([obj]),
            }
        )

    armature_records = []
    for obj in armatures:
        armature_records.append(
            {
                "name": obj.name,
                "bones": len(obj.data.bones),
                "bone_names": [bone.name for bone in obj.data.bones],
                "animations": animation_names(obj),
            }
        )

    try:
        asset_label = str(path.relative_to(REPO_ROOT))
    except ValueError:
        asset_label = str(path)

    return {
        "asset": asset_label,
        "file_size_bytes": path.stat().st_size,
        "bounds": object_bounds(objects),
        "mesh_count": len(meshes),
        "armature_count": len(armatures),
        "vertex_count": sum(len(obj.data.vertices) for obj in meshes),
        "polygon_count": sum(len(obj.data.polygons) for obj in meshes),
        "objects": [
            {
                "name": obj.name,
                "type": obj.type,
                "parent": obj.parent.name if obj.parent else "",
                "animations": animation_names(obj),
            }
            for obj in objects
        ],
        "meshes": mesh_records,
        "armatures": armature_records,
        "actions": [action_record(action) for action in bpy.data.actions],
    }


def main() -> None:
    missing = [str(path) for path in ASSET_PATHS if not path.is_file()]
    if missing:
        raise FileNotFoundError(f"Missing character assets: {missing}")

    report = {
        "blender_version": bpy.app.version_string,
        "assets": [audit_asset(path) for path in ASSET_PATHS],
    }
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
