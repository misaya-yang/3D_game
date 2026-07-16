#!/bin/sh
set -eu

REPO_ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
BLENDER_BIN="${BLENDER_BIN:-$(command -v blender || true)}"
if [ -z "${BLENDER_BIN}" ]; then
    BLENDER_BIN="/Applications/Blender.app/Contents/MacOS/Blender"
fi
PIPELINE_HOME="${WENDAO_BLENDER_PIPELINE_HOME:-$HOME/Library/Caches/Wendao/BlenderPipeline/user}"

"$REPO_ROOT/tools/blender/setup_mpfb_pipeline.sh"

BLENDER_USER_RESOURCES="$PIPELINE_HOME" \
    "$BLENDER_BIN" -b \
    --python "$REPO_ROOT/tools/blender/character_pipeline.py" \
    -- "$@"
