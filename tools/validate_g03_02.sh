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
    "${SCRIPT_DIR}/validate_g03_01.sh" --static-only

    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local events="Assets/_Project/Scripts/Systems/Skill/SkillEvents.cs"
    local manager="Assets/_Project/Scripts/Systems/Skill/SkillManager.cs"
    local inventory="Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local buffer="Assets/_Project/Scripts/Entities/Player/PlayerActionBuffer.cs"
    local controller="Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    local quickbar="Assets/_Project/Scripts/Systems/UI/Skill/SkillQuickbarView.cs"
    local drag="Assets/_Project/Scripts/Systems/UI/Skill/SkillDragSource.cs"
    local drop="Assets/_Project/Scripts/Systems/UI/Skill/SkillQuickbarSlotDropTarget.cs"
    local panel="Assets/_Project/Scripts/Systems/UI/Skill/SkillPanelView.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0302PlayModeTests.cs"

    for file_path in \
        "${config}" "${events}" "${manager}" "${inventory}" \
        "${input_contract}" "${input_reader}" "${buffer}" "${controller}" \
        "${quickbar}" "${drag}" "${drop}" "${panel}" "${bootstrap}" \
        "${tests}"; do
        assert_file "${file_path}"
    done

    local skill_id
    for skill_id in \
        skill_basic_qi_bolt \
        skill_fire_ember \
        skill_ice_needle \
        skill_lightning_chain \
        skill_wind_slash \
        skill_pass_iron_skin \
        skill_ult_fire_wave; do
        assert_pattern "${skill_id}" "${config}"
        assert_pattern "${skill_id}" "${events}"
        assert_pattern "^\\| ${skill_id} \\|" docs/09_CONTENT.md
        assert_pattern "skill_name_${skill_id}" docs/09_CONTENT.md
    done

    assert_pattern 'SkillScroll = "item_skill_scroll"' "${inventory}"
    assert_pattern 'skillScroll\.Id = "item_skill_scroll"' "${config}"
    assert_pattern 'SkillUpgraded = "OnSkillUpgraded"' "${events}"
    assert_pattern 'RemoveItem\(InventoryContentIds\.SkillScroll, cost\)' "${manager}"
    assert_pattern 'runtime\.Level\+\+' "${manager}"
    assert_pattern 'SkillEvents\.SkillUpgraded' "${manager}"
    assert_pattern 'TrySaveModule\(InventoryManager\.SaveModuleName\)' "${manager}"
    assert_pattern 'TrySaveModule\(SaveModuleName\)' "${manager}"

    for skill_number in 2 3 4; do
        assert_pattern "bool Skill${skill_number}PressedThisFrame" "${input_contract}"
        assert_pattern "FindAction\\(\"Skill${skill_number}\", true\\)" "${input_reader}"
        assert_pattern "BufferedActionType\.Skill${skill_number}" "${buffer}"
    done
    assert_pattern 'bool OpenSkillPressedThisFrame' "${input_contract}"
    assert_pattern 'FindAction\("OpenSkill", true\)' "${input_reader}"
    assert_pattern 'GetRequestedBarIndex' "${controller}"
    assert_pattern 'SkillManager\.BarSlotCount' "${controller}"

    assert_pattern 'new Image\[SkillManager\.BarSlotCount\]' "${quickbar}"
    assert_pattern 'SkillQuickbarSlotDropTarget' "${quickbar}"
    assert_pattern 'IBeginDragHandler' "${drag}"
    assert_pattern 'IDropHandler' "${drop}"
    assert_pattern 'public sealed class SkillPanelView' "${panel}"
    assert_pattern 'OpenSkillPressedThisFrame' "${panel}"
    assert_pattern 'TryUpgradeSelected' "${panel}"
    assert_pattern 'AddComponent<SkillPanelView>' "${bootstrap}"

    assert_pattern 'OnSkillUpgraded' docs/02_ARCHITECTURE.md
    assert_pattern 'G03-02 不改 schema' docs/03_DATA_LAYER.md
    assert_pattern 'G03-02 注册 `09_CONTENT§4` 的 7 门功法' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern 'ui_skill_upgrade_material_missing' docs/09_CONTENT.md
    assert_pattern 'item_name_item_skill_scroll' docs/09_CONTENT.md

    assert_pattern 'SevenContentSkillsCanBeLearnedAndAllFourInputSlotsAreBound' "${tests}"
    assert_pattern 'DraggingLearnedSkillsEquipsAllFourQuickbarSlotsAndPersists' "${tests}"
    assert_pattern 'SkillPanelLocksAndRestoresGameplayInput' "${tests}"
    assert_pattern 'SkillFourInputCastsTheSkillEquippedInSlotFour' "${tests}"
    assert_pattern 'SkillScrollUpgradeConsumesLevelCostRaisesDamageAndPersists' "${tests}"

    printf 'G03-02 static validation passed.\n'
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
    G03-02-editmode

run_unity_tests \
    PlayMode \
    'Wendao.Tests.PlayMode.VerticalSlice.GVS04PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0103PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0302PlayModeTests' \
    G03-02-playmode

printf 'G03-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
