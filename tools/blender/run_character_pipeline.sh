#!/bin/sh
set -eu

REPO_ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
BLENDER_BIN="${BLENDER_BIN:-$(command -v blender || true)}"
if [ -z "${BLENDER_BIN}" ]; then
    BLENDER_BIN="/Applications/Blender.app/Contents/MacOS/Blender"
fi

if [ ! -x "${BLENDER_BIN}" ]; then
    printf 'Blender executable not found: %s\n' "${BLENDER_BIN}" >&2
    exit 1
fi

"$REPO_ROOT/tools/art/sync_character_sources.sh"

"$BLENDER_BIN" -b --factory-startup \
    --python "$REPO_ROOT/tools/blender/quaternius_character_pipeline.py" \
    -- "$@"
