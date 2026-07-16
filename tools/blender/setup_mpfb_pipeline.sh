#!/bin/sh
set -eu

BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
PIPELINE_HOME="${WENDAO_BLENDER_PIPELINE_HOME:-$HOME/Library/Caches/Wendao/BlenderPipeline/user}"
MANIFEST="$PIPELINE_HOME/extensions/blender_org/mpfb/blender_manifest.toml"

if [ ! -x "$BLENDER_BIN" ]; then
    echo "Blender executable not found: $BLENDER_BIN" >&2
    exit 1
fi

if [ -f "$MANIFEST" ] && grep -q 'version = "2.0.16"' "$MANIFEST"; then
    echo "MPFB 2.0.16 is ready at $PIPELINE_HOME"
    exit 0
fi

mkdir -p \
    "$PIPELINE_HOME/config" \
    "$PIPELINE_HOME/extensions" \
    "$PIPELINE_HOME/scripts" \
    "$PIPELINE_HOME/datafiles"

BLENDER_USER_RESOURCES="$PIPELINE_HOME" \
    "$BLENDER_BIN" -b --python-expr \
    "import bpy; bpy.context.preferences.system.use_online_access=True; bpy.ops.wm.save_userpref()"

BLENDER_USER_RESOURCES="$PIPELINE_HOME" \
    "$BLENDER_BIN" -b -c extension install -s -e mpfb

if [ ! -f "$MANIFEST" ]; then
    echo "MPFB installation completed without the expected manifest." >&2
    exit 1
fi

if ! grep -q 'version = "2.0.16"' "$MANIFEST"; then
    echo "Expected MPFB 2.0.16; installed manifest differs: $MANIFEST" >&2
    exit 1
fi

echo "MPFB 2.0.16 is ready at $PIPELINE_HOME"
