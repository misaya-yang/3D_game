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
    "${SCRIPT_DIR}/validate_g03_02.sh" --static-only

    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local runtime="Assets/_Project/Scripts/Data/Runtime/AlchemyRuntimeData.cs"
    local events="Assets/_Project/Scripts/Systems/Crafting/AlchemyEvents.cs"
    local contract="Assets/_Project/Scripts/Systems/Crafting/IAlchemyService.cs"
    local system="Assets/_Project/Scripts/Systems/Crafting/AlchemySystem.cs"
    local panel="Assets/_Project/Scripts/Systems/UI/Crafting/AlchemyPanelView.cs"
    local systems_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0303PlayModeTests.cs"

    for file_path in \
        "${config}" "${runtime}" "${events}" "${contract}" "${system}" \
        "${panel}" "${systems_bootstrap}" "${ui_bootstrap}" "${tests}"; do
        assert_file "${file_path}"
    done

    local recipe_id
    for recipe_id in \
        recipe_heal_01 \
        recipe_mana_01 \
        recipe_body_01 \
        recipe_xp_01; do
        assert_pattern "${recipe_id}" "${config}"
        assert_pattern "${recipe_id}" "${events}"
        assert_pattern "^\\| ${recipe_id} \\|" docs/09_CONTENT.md
    done

    assert_pattern 'CraftCompleted = "OnCraftCompleted"' "${events}"
    assert_pattern 'CraftFailed = "OnCraftFailed"' "${events}"
    assert_pattern 'GetSuccessBonus\(Level\)' "${system}"
    assert_pattern 'recipe\.BaseSuccessRate \+ GetSuccessBonus' "${system}"
    assert_pattern 'ingredient\.ConsumedOnFail' "${system}"
    assert_pattern 'inventory\.RestoreItem' "${system}"
    assert_pattern 'SuccessXpPerRecipeLevel' "${system}"
    assert_pattern 'TrySaveModule\(InventoryManager\.SaveModuleName\)' "${system}"
    assert_pattern 'TrySaveModule\(SaveModuleName\)' "${system}"
    assert_pattern 'public sealed class AlchemySaveData' "${runtime}"
    assert_pattern 'AddComponent<AlchemySystem>' "${systems_bootstrap}"
    assert_pattern 'AddComponent<AlchemyPanelView>' "${ui_bootstrap}"
    assert_pattern 'public sealed class AlchemyPanelView' "${panel}"
    assert_pattern 'RecipeButtonCount' "${panel}"
    assert_pattern 'TryCraftSelected' "${panel}"

    assert_pattern 'OnCraftCompleted' docs/02_ARCHITECTURE.md
    assert_pattern 'G03-03 炼丹 DTO' docs/03_DATA_LAYER.md
    assert_pattern '成功熟练度为 `100 × RequiredCraftLevel`' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern '失败规则：灵尘为催化剂并消耗' docs/09_CONTENT.md
    assert_pattern 'ui_alchemy_success' docs/09_CONTENT.md
    assert_pattern 'item_name_item_mat_qingxin_grass' docs/09_CONTENT.md

    assert_pattern 'FourRecipesAndTheirMaterialsMatchTheContentTable' "${tests}"
    assert_pattern 'SuccessfulCraftConsumesMaterialsAwardsProductXpAndEvent' "${tests}"
    assert_pattern 'FailedCraftPublishesFailureConsumesCatalystAndRefundsMainMaterial' "${tests}"
    assert_pattern 'CumulativeXpRaisesLevelAddsSuccessBonusAndRoundTrips' "${tests}"
    assert_pattern 'MinimalAlchemyPanelListsRecipesCraftsAndRestoresGameplayInput' "${tests}"

    printf 'G03-03 static validation passed.\n'
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
    G03-03-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.GVS03PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0303PlayModeTests' \
    G03-03-playmode

printf 'G03-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
