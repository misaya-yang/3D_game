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
    "${SCRIPT_DIR}/validate_g05_06.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local manager="Assets/_Project/Scripts/Systems/UI/Common/UIManager.cs"
    local contract="Assets/_Project/Scripts/Systems/UI/Common/IUIManager.cs"
    local quest="Assets/_Project/Scripts/Systems/UI/Quest/QuestPanelView.cs"
    local map="Assets/_Project/Scripts/Systems/UI/Quest/MapPanelView.cs"
    local pause="Assets/_Project/Scripts/Systems/UI/Common/PausePanelView.cs"
    local hud="Assets/_Project/Scripts/Systems/UI/Combat/CombatStatusHudView.cs"
    local input="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local inventory="Assets/_Project/Scripts/Systems/Inventory/InventoryManager.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0601PlayModeTests.cs"
    local files=("${manager}" "${contract}" "${quest}" "${map}" "${pause}" "${hud}" "${input}" "${bootstrap}" "${inventory}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'ShowPanel\(string panelId\)' "${contract}"
    assert_pattern 'CloseTopPanel\(\)' "${contract}"
    assert_pattern 'HandleCancel\(\)' "${contract}"
    assert_pattern 'UiPanelIds\.Inventory' "${manager}"
    assert_pattern 'UiPanelIds\.Quest' "${manager}"
    assert_pattern 'UiPanelIds\.Map' "${manager}"
    assert_pattern 'UiPanelIds\.Pause' "${manager}"
    assert_pattern 'DialogueEvents\.Started' "${manager}"
    assert_pattern 'ReconcileExternalPanelOpen' "${manager}"
    assert_pattern 'ReconcileGameplayInput' "${manager}"
    assert_pattern 'OpenQuestPressedThisFrame' "${input}"
    assert_pattern 'OpenMapPressedThisFrame' "${input}"
    assert_pattern 'PausePressedThisFrame' "${input}"
    assert_pattern 'QuestEvents\.Progressed' "${quest}"
    assert_pattern 'DailyQuestEvents\.Progressed' "${quest}"
    assert_pattern 'TryClaimSelectedDaily' "${quest}"
    assert_pattern 'IMapTravelService' "${map}"
    assert_pattern 'SaveAndReturnToMenu' "${pause}"
    assert_pattern 'InventoryEvents\.CurrencyChanged' "${hud}"
    assert_pattern 'CombatEvents\.PlayerDamaged' "${hud}"
    assert_pattern 'InventoryEvents\.CurrencyChanged' "${inventory}"
    assert_pattern 'AddComponent<UIManager>' "${bootstrap}"
    assert_pattern 'AddComponent<QuestPanelView>' "${bootstrap}"
    assert_pattern 'AddComponent<MapPanelView>' "${bootstrap}"
    assert_pattern 'AddComponent<PausePanelView>' "${bootstrap}"
    assert_pattern 'FullscreenPanelsAreMutuallyExclusive' "${tests}"
    assert_pattern 'InteractionPanelOpeningWinsAndDialogueClosesAllPanels' "${tests}"

    assert_pattern 'OnCurrencyChanged' docs/02_ARCHITECTURE.md
    assert_pattern 'CurrencyChangeInfo' docs/03_DATA_LAYER.md
    assert_pattern 'G06-01 采用场景内协调器' docs/08_UI_META.md
    assert_pattern 'ui_quest_panel_title' docs/09_CONTENT.md
    assert_pattern 'ui_pause_title' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G06-01\nphase: 6\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G06-01 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G06-01 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G06-01-editmode.xml" \
        -logFile "${RESULTS_DIR}/G06-01-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS03PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0204PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0302PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0303PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0305PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0601PlayModeTests' \
    -testResults "${RESULTS_DIR}/G06-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G06-01-playmode.log"

printf 'G06-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
