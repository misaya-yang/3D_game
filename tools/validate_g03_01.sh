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
    "${SCRIPT_DIR}/validate_g02_04.sh" --static-only

    local contract="Assets/_Project/Scripts/Systems/Equipment/IRefineService.cs"
    local refine="Assets/_Project/Scripts/Systems/Equipment/RefineSystem.cs"
    local formulas="Assets/_Project/Scripts/Data/Config/FormulaLibrary.cs"
    local inventory_events="Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0301PlayModeTests.cs"

    for file_path in \
        "${contract}" \
        "${refine}" \
        "${formulas}" \
        "${inventory_events}" \
        "${config}" \
        "${player}" \
        "${bootstrap}" \
        "${tests}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'GetSuccessRate\(int currentLevel\)' "${contract}"
    assert_pattern 'GetRequiredMaterialCount\(int currentLevel\)' "${contract}"
    assert_pattern 'TryRefine\(EquipmentSlot slot\)' "${contract}"
    assert_pattern 'GetRefineStatMultiplier' "${formulas}"
    assert_pattern 'GetRefineSuccessRate' "${formulas}"
    assert_pattern 'GetRefineMaterialCost' "${formulas}"
    assert_pattern 'RefineMinimumSuccess = 0\.4f' "${formulas}"

    assert_pattern 'RefineStone = "item_mat_refine_stone"' "${inventory_events}"
    assert_pattern 'EquipmentUpgraded = "OnEquipmentUpgraded"' "${inventory_events}"
    assert_pattern 'RemoveItem\(MaterialItemId, materialCount\)' "${refine}"
    assert_pattern 'currentLevel \+ 1' "${refine}"
    assert_pattern 'InventoryEvents\.EquipmentUpgraded' "${refine}"
    assert_pattern 'TrySaveModule\(InventoryManager\.SaveModuleName\)' "${refine}"
    assert_pattern 'TrySaveModule\(EquipmentManager\.SaveModuleName\)' "${refine}"
    assert_pattern 'Register<IRefineService>' "${refine}"
    assert_pattern 'EnsureRefineSystem\(\)' "${bootstrap}"
    assert_pattern 'HandleEquipmentUpgraded' "${player}"
    assert_pattern 'refineStone\.Id = "item_mat_refine_stone"' "${config}"

    if rg -q 'item_material_refine_stone' \
        "${PROJECT_ROOT}/docs" \
        "${PROJECT_ROOT}/Assets/_Project/Scripts"; then
        printf 'Deprecated refine material ID is still present.\n' >&2
        exit 1
    fi
    assert_pattern 'max\(0\.4, 0\.95 - 0\.03 \* currentLevel\)' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern '1 \+ level/2' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern 'G03-01 启用既有 `RefineLevel` 字段' docs/03_DATA_LAYER.md
    assert_pattern 'item_name_item_mat_refine_stone' docs/09_CONTENT.md
    assert_pattern 'ui_refine_success' docs/09_CONTENT.md
    assert_pattern 'ui_refine_fail' docs/09_CONTENT.md

    assert_pattern 'SuccessRateAndMaterialCostMatchAuthoritativeFormula' "${tests}"
    assert_pattern 'SuccessfulRefineConsumesStoneRaisesFinalStatsAndPersistsInstance' "${tests}"
    assert_pattern 'FailedRefineConsumesScaledMaterialsWithoutDroppingLevel' "${tests}"
    assert_pattern 'MissingTargetMaterialAndMaxLevelRejectWithoutConsumption' "${tests}"

    printf 'G03-01 static validation passed.\n'
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
    G03-01-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.GVS03PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0203PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0301PlayModeTests' \
    G03-01-playmode

printf 'G03-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
