#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
readonly UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"
readonly RESULTS_DIR="${PROJECT_ROOT}/TestResults"

STATIC_ONLY=false
RUN_EDITMODE=true
case "${1:-}" in
    "") ;;
    --static-only) STATIC_ONLY=true ;;
    --playmode-only) RUN_EDITMODE=false ;;
    *) printf 'Usage: %s [--static-only|--playmode-only]\n' "$0" >&2; exit 64 ;;
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
    "${SCRIPT_DIR}/validate_g05_05.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local system="Assets/_Project/Scripts/Systems/World/SerendipitySystem.cs"
    local contract="Assets/_Project/Scripts/Systems/World/ISerendipityService.cs"
    local ids="Assets/_Project/Scripts/Systems/World/SerendipityContentIds.cs"
    local trigger="Assets/_Project/Scripts/Systems/World/SerendipityTrigger.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SerendipityRuntimeBootstrap.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local data="Assets/_Project/Scripts/Data/ScriptableObjects/SerendipityData.cs"
    local loot="Assets/_Project/Scripts/Systems/Loot/ILootService.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0506PlayModeTests.cs"
    local files=("${system}" "${contract}" "${ids}" "${trigger}" "${bootstrap}" "${config}" "${data}" "${loot}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'TryTrigger\(string id\)' "${contract}"
    assert_pattern 'HasCompleted' "${contract}"
    assert_pattern 'SerendipityFlags.Add' "${system}"
    assert_pattern 'QuestFlags\[data.WorldFlag\]' "${system}"
    assert_pattern 'item.Type == ItemType.Equipment' "${system}"
    assert_pattern 'SpawnWorldPickup' "${system}"
    assert_pattern 'SerendipityEvents.Triggered' "${system}"
    assert_pattern 'GameManager.Instance' "${system}"
    assert_pattern 'Serendipity_QingshiHerbSpirit' "${bootstrap}"
    assert_pattern 'Serendipity_CangwuMistStele' "${bootstrap}"
    assert_pattern 'Serendipity_CangwuCliffBox' "${bootstrap}"
    assert_pattern 'Serendipity_BlackwindEchoCache' "${bootstrap}"
    assert_pattern 'GetFloorCenter\(3\)' "${bootstrap}"
    assert_pattern 'RegisterBuiltInSerendipityContent' "${config}"
    assert_pattern 'item_mat_black_stone' "${config}"
    assert_pattern 'AllThreeMapsTriggerOnceGrantRewardsFlagsAndEvents' "${tests}"
    assert_pattern 'RuntimeBootstrapCreatesOneTwoOneTriggersAcrossMaps' "${tests}"

    assert_pattern 'OnSerendipityTriggered' docs/02_ARCHITECTURE.md
    assert_pattern 'G05-06 代码实现选择' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'trigger_serendipity_blackwind_echo_cache' docs/09_CONTENT.md
    assert_pattern 'serendipity_text_cangwu_mist_stele' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G05-06\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-06 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-06 static validation passed.\n'
}

run_static_validation
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
if [[ "${RUN_EDITMODE}" == true ]]; then
    "${UNITY_EDITOR}" \
        -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
        -runTests -testPlatform EditMode \
        -testFilter Wendao.Tests.EditMode.Data \
        -testResults "${RESULTS_DIR}/G05-06-editmode.xml" \
        -logFile "${RESULTS_DIR}/G05-06-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0503PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0505PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0506PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-06-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-06-playmode.log"

printf 'G05-06 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
