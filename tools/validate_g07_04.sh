#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"

STATIC_ONLY=false
case "${1:-}" in
    "") ;;
    --static-only) STATIC_ONLY=true ;;
    *) printf 'Usage: %s [--static-only]\n' "$0" >&2; exit 64 ;;
esac

assert_file() {
    [[ -f "${PROJECT_ROOT}/$1" ]] || {
        printf 'Required file missing: %s\n' "$1" >&2
        exit 1
    }
}

assert_pattern() {
    local pattern="$1"
    local file_path="$2"
    rg -q "${pattern}" "${PROJECT_ROOT}/${file_path}" || {
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    }
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g07_03.sh" --static-only

    local files=(
        Assets/_Project/Editor/Wendao.Editor.asmdef
        Assets/_Project/Editor/WendaoBuild.cs
        Assets/_Project/Tests/PlayMode/VerticalSlice/G0704PlayModeTests.cs
        Assets/_Project/Tests/RuntimeSupport/Wendao.TestSupport.asmdef
        Assets/StreamingAssets/Config/RealmConfig.json
        Assets/StreamingAssets/Config/SpiritRootConfig.json
        Assets/StreamingAssets/Config/BodyRefinementConfig.json
        Assets/StreamingAssets/Config/CraftLevelConfig.json
        tools/build_macos.sh
        tools/smoke_macos_player.sh
    )
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    [[ ! -d "${PROJECT_ROOT}/Assets/_Project/StreamingAssets" ]] || {
        printf 'Unity StreamingAssets must live directly under Assets/.\n' >&2
        exit 1
    }
    assert_pattern 'FreshSaveCompletesChapterAndRoundTripsCriticalMvpState' \
        Assets/_Project/Tests/PlayMode/VerticalSlice/G0704PlayModeTests.cs
    assert_pattern 'AllThreeMvpMapsLoadWithRuntimePlayerAndWorldRoots' \
        Assets/_Project/Tests/PlayMode/VerticalSlice/G0704PlayModeTests.cs
    assert_pattern 'BuildPipeline\.BuildPlayer' \
        Assets/_Project/Editor/WendaoBuild.cs
    assert_pattern 'BuildTarget\.StandaloneOSX' \
        Assets/_Project/Editor/WendaoBuild.cs
    assert_pattern 'productName: "\\u95EE\\u9053\\u957F\\u751F"' \
        ProjectSettings/ProjectSettings.asset
    assert_pattern 'path: Assets/_Project/Scenes/Core/Boot\.unity' \
        ProjectSettings/EditorBuildSettings.asset
    assert_pattern 'path: Assets/_Project/Scenes/Dungeons/Dungeon_Blackwind\.unity' \
        ProjectSettings/EditorBuildSettings.asset
    if ! rg -U -q \
        'id: G07-04\nphase: 7\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G07-04 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G07-04 static validation passed.\n'
}

run_static_validation
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testResults "${RESULTS_DIR}/G07-04-editmode.xml" \
    -logFile "${RESULTS_DIR}/G07-04-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testResults "${RESULTS_DIR}/G07-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/G07-04-playmode.log"

"${SCRIPT_DIR}/build_macos.sh"
"${SCRIPT_DIR}/smoke_macos_player.sh"

printf 'G07-04 final validation passed. Results are in %s.\n' "${RESULTS_DIR}"
