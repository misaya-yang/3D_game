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
    "${SCRIPT_DIR}/validate_g04_03.sh" --static-only

    local enemy_data="Assets/_Project/Scripts/Data/ScriptableObjects/EnemyData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local loot="Assets/_Project/Scripts/Systems/Loot/LootSystem.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0404PlayModeTests.cs"
    local files=("${enemy_data}" "${config}" "${loot}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'public int MinSpiritStones;' "${enemy_data}"
    assert_pattern 'public int MaxSpiritStones;' "${enemy_data}"
    assert_pattern 'greyWolf.MinSpiritStones = 0;' "${config}"
    assert_pattern 'greyWolf.MaxSpiritStones = 2;' "${config}"
    assert_pattern 'eliteWolf.MinSpiritStones = 8;' "${config}"
    assert_pattern 'eliteWolf.MaxSpiritStones = 15;' "${config}"
    assert_pattern 'DropSpiritStones\(data\);' "${loot}"
    assert_pattern '_random.Next\(' "${loot}"
    assert_pattern 'inventory.AddSpiritStones\(count\)' "${loot}"

    assert_pattern 'EnemyLootTablesExposeIndependentItemAndCurrencyRanges' "${tests}"
    assert_pattern 'LootEntriesRespectDropChanceAndInclusiveCountRange' "${tests}"
    assert_pattern 'SpiritStoneLootBypassesFullInventoryAndUpdatesProfile' "${tests}"
    assert_pattern 'EnemyDeathGrantsEliteLootAndCurrencyOnlyOnce' "${tests}"
    assert_pattern 'G04-04 掉落结算' docs/09_CONTENT.md
    assert_pattern 'MinSpiritStones' docs/03_DATA_LAYER.md

    if ! rg -U -q \
        'id: G04-04\nphase: 4\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G04-04 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G04-04 static validation passed.\n'
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
    -testResults "${RESULTS_DIR}/G04-04-editmode.xml" \
    -logFile "${RESULTS_DIR}/G04-04-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0402PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0403PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0404PlayModeTests' \
    -testResults "${RESULTS_DIR}/G04-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/G04-04-playmode.log"

printf 'G04-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
