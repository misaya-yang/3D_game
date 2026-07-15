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
    "${SCRIPT_DIR}/validate_g05_01.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/World/MapContentIds.cs"
    local api="Assets/_Project/Scripts/Systems/World/IMapTravelService.cs"
    local travel="Assets/_Project/Scripts/Systems/World/MapTravelSystem.cs"
    local point="Assets/_Project/Scripts/Systems/World/TeleportPoint.cs"
    local gate="Assets/_Project/Scripts/Systems/World/CangwuPathGate.cs"
    local factory="Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
    local loader="Assets/_Project/Scripts/Systems/World/SceneLoader.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0502PlayModeTests.cs"
    local scene="Assets/_Project/Scenes/Maps/Map_Cangwu.unity"
    local files=("${ids}" "${api}" "${travel}" "${point}" "${gate}" "${factory}" "${loader}" "${player}" "${tests}" "${scene}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'map_cangwu' "${ids}"
    assert_pattern 'teleport_cangwu_gate' "${ids}"
    assert_pattern 'quest_flag_main_cangwu_path_open' "${ids}"
    assert_pattern 'public sealed class MapTravelSystem' "${travel}"
    assert_pattern 'UnlockedTeleports' "${travel}"
    assert_pattern 'public bool Travel' "${travel}"
    assert_pattern 'public sealed class TeleportPoint' "${point}"
    assert_pattern 'TryDiscover' "${point}"
    assert_pattern 'public sealed class CangwuPathGate' "${gate}"
    assert_pattern 'RequiredAreaCount = 5' "${factory}"
    assert_pattern 'Area_CangwuGatePlatform' "${factory}"
    assert_pattern 'Area_CangwuMountainRoad' "${factory}"
    assert_pattern 'Area_CangwuMistValley' "${factory}"
    assert_pattern 'Area_CangwuCave' "${factory}"
    assert_pattern 'Area_CangwuThunderTerrace' "${factory}"
    assert_pattern 'Map_Cangwu' ProjectSettings/EditorBuildSettings.asset
    assert_pattern 'CangwuMapSceneName' "${loader}"
    assert_pattern 'FindRequestedSpawn' "${player}"

    assert_pattern 'CangwuSceneIsRegisteredAndContainsFiveLocalizedAreas' "${tests}"
    assert_pattern 'CangwuPlayerSpawnsAtRequestedTeleportPoint' "${tests}"
    assert_pattern 'FirstStepOnTeleportUnlocksAndPersistsMapAndPoint' "${tests}"
    assert_pattern 'MainQuestFlagControlsSecretPathAndOpenGateLoadsCangwu' "${tests}"
    assert_pattern 'teleport_cangwu_gate' docs/09_CONTENT.md
    assert_pattern 'quest_flag_main_cangwu_path_open' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'ui_cangwu_path_locked' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G05-02\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-02 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-02 static validation passed.\n'
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
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G05-02-editmode.xml" \
    -logFile "${RESULTS_DIR}/G05-02-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.SceneFlow.SceneLoaderPlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0501PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0502PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-02-playmode.log"

printf 'G05-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
