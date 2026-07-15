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
    *)
        printf 'Usage: %s [--static-only]\n' "$0" >&2
        exit 64
        ;;
esac

assert_file() {
    if [[ ! -f "${PROJECT_ROOT}/$1" ]]; then
        printf 'Required file missing: %s\n' "$1" >&2
        exit 1
    fi
}

assert_pattern() {
    local pattern="$1"
    local file_path="$2"
    if ! rg -q "${pattern}" "${PROJECT_ROOT}/${file_path}"; then
        printf 'Required contract not found in %s: %s\n' \
            "${file_path}" "${pattern}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g03_03.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/Crafting/GatheringContentIds.cs"
    local contract="Assets/_Project/Scripts/Systems/Crafting/IGatheringService.cs"
    local gatherable="Assets/_Project/Scripts/Systems/Crafting/GatherableObject.cs"
    local system="Assets/_Project/Scripts/Systems/Crafting/GatheringSystem.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local factory="Assets/_Project/Scripts/Systems/World/QingshiGreyboxFactory.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0304PlayModeTests.cs"

    for file_path in \
        "${ids}" "${contract}" "${gatherable}" "${system}" \
        "${bootstrap}" "${factory}" "${tests}"; do
        assert_file "${file_path}"
    done

    local node_id
    for node_id in \
        gather_qingshi_qingxin_01 \
        gather_qingshi_qingxin_02 \
        gather_qingshi_qingxin_03 \
        gather_qingshi_qingxin_04 \
        gather_qingshi_spirit_dust_01 \
        gather_qingshi_spirit_dust_02; do
        assert_pattern "${node_id}" "${ids}"
        assert_pattern "^\\| ${node_id} \\|" docs/09_CONTENT.md
    done

    assert_pattern 'GatherDurationSeconds = 1\.5f' "${system}"
    assert_pattern 'CombatEvents\.PlayerDamaged' "${system}"
    assert_pattern 'AcquireSource\.Gather' "${system}"
    assert_pattern 'TickGathering\(float deltaTime\)' "${system}"
    assert_pattern 'TickRespawn\(float deltaTime\)' "${gatherable}"
    assert_pattern 'RequiredToolItemId' "${gatherable}"
    assert_pattern 'AddComponent<GatheringSystem>' "${bootstrap}"
    assert_pattern 'RequiredGatherableCount = 6' "${factory}"
    assert_pattern 'EnsureGatherables' "${factory}"
    assert_pattern 'InventoryContentIds\.QingxinGrass' "${factory}"
    assert_pattern 'InventoryContentIds\.SpiritDust' "${factory}"

    assert_pattern 'OnItemAcquired\(AcquireSource\.Gather\)' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern '灵草涧 6 点落实为' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'ui_gather_progress' docs/09_CONTENT.md
    assert_pattern 'ui_gather_interrupted' docs/09_CONTENT.md

    assert_pattern 'QingshiHerbCreekContainsSixStableGatheringNodes' "${tests}"
    assert_pattern 'OnePointFiveSecondReadAwardsGatherLootAndPersistsInventory' "${tests}"
    assert_pattern 'PlayerDamageInterruptsReadWithoutLootOrCooldown' "${tests}"
    assert_pattern 'DepletedNodeReturnsOnlyAfterItsRespawnTime' "${tests}"

    printf 'G03-04 static validation passed.\n'
}

run_static_validation

if [[ "${STATIC_ONLY}" == true ]]; then
    exit 0
fi

if [[ ! -x "${UNITY_EDITOR}" ]]; then
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

run_unity_tests() {
    local platform="$1"
    local filter="$2"
    local result_name="$3"
    local result_path="${RESULTS_DIR}/${result_name}.xml"
    local log_path="${RESULTS_DIR}/${result_name}.log"
    local unity_exit=0

    rm -f "${result_path}" "${log_path}"
    "${UNITY_EDITOR}" \
        -batchmode \
        -nographics \
        -projectPath "${PROJECT_ROOT}" \
        -runTests \
        -testPlatform "${platform}" \
        -testFilter "${filter}" \
        -testResults "${result_path}" \
        -logFile "${log_path}" || unity_exit=$?

    if [[ ! -f "${result_path}" ]] \
        || ! rg -q '<test-run .*result="Passed".*failed="0"' \
            "${result_path}"; then
        printf '%s validation failed (Unity exit %s). See %s\n' \
            "${platform}" "${unity_exit}" "${log_path}" >&2
        if [[ "${unity_exit}" -eq 0 ]]; then
            unity_exit=1
        fi
        return "${unity_exit}"
    fi

    if [[ "${unity_exit}" -ne 0 ]]; then
        printf '%s tests passed; ignoring Unity shutdown exit %s.\n' \
            "${platform}" "${unity_exit}"
    fi
}

run_unity_tests \
    EditMode \
    Wendao.Tests.EditMode.Data \
    G03-04-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.GVS03PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0303PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0304PlayModeTests' \
    G03-04-playmode

printf 'G03-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
