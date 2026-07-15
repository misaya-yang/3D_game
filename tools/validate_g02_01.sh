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
    "${SCRIPT_DIR}/validate_g01_05.sh" --static-only

    local blocker="Assets/_Project/Scripts/Data/Runtime/BreakthroughBlocker.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local contract="Assets/_Project/Scripts/Systems/Cultivation/ICultivationService.cs"
    local content="Assets/_Project/Scripts/Systems/Cultivation/CultivationContentIds.cs"
    local manager="Assets/_Project/Scripts/Systems/Cultivation/CultivationManager.cs"
    local inventory_ids="Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
    local quest_ids="Assets/_Project/Scripts/Systems/Quest/QuestContentIds.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local scene_loader="Assets/_Project/Scripts/Systems/World/SceneLoader.cs"
    local ceremony="Assets/_Project/Scripts/Systems/UI/Cultivation/BreakthroughCeremonyView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local schema_tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"
    local config_tests="Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0201PlayModeTests.cs"

    local required_files=(
        "${blocker}" "${config}" "${contract}" "${content}" "${manager}"
        "${inventory_ids}" "${quest_ids}" "${player}" "${scene_loader}"
        "${ceremony}" "${ui_bootstrap}" "${schema_tests}" "${config_tests}"
        "${tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'public struct BreakthroughBlocker' "${blocker}"
    assert_pattern 'public string Code' "${blocker}"
    assert_pattern 'public string MessageKey' "${blocker}"
    assert_pattern 'public string RelatedItemId' "${blocker}"
    assert_pattern 'public string\[\] AcquisitionHintKeys' "${blocker}"

    assert_pattern 'public enum BreakthroughState' "${contract}"
    assert_pattern 'bool IsBreakingThrough' "${contract}"
    assert_pattern 'bool IsBreakthroughActive' "${contract}"
    assert_pattern 'bool IsBreakthroughInvincible' "${contract}"
    assert_pattern 'int CeremonyBeat' "${contract}"
    assert_pattern 'IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers' "${contract}"
    assert_pattern 'float GetBreakthroughSuccessRate' "${contract}"
    assert_pattern 'bool TryBreakthrough' "${contract}"

    assert_pattern 'BreakthroughDurationSeconds = 3f' "${manager}"
    assert_pattern 'BreakthroughResultDurationSeconds = 2f' "${manager}"
    assert_pattern 'BeatTwoStartSeconds = 0\.3f' "${manager}"
    assert_pattern 'BeatThreeStartSeconds = 1\.8f' "${manager}"
    assert_pattern 'BeatFiveStartSeconds = 1\.2f' "${manager}"
    assert_pattern 'MaxNormalSuccessRate = 0\.95f' "${manager}"
    assert_pattern 'EnterState\(BreakthroughState\.BreakingThrough\)' "${manager}"
    assert_pattern 'EnterState\(BreakthroughState\.BreakthroughResult\)' "${manager}"
    assert_pattern 'SetFoundationPity\(false\)' "${manager}"
    assert_pattern 'SetFoundationPity\(true\)' "${manager}"
    assert_pattern 'inventory\.RemoveItem\(_activeRequiredItemId, 1\)' "${manager}"
    assert_pattern 'StatusEffectContentIds\.HeartDemon' "${manager}"
    assert_pattern 'CultivationEvents\.RealmBreakthroughFailed' "${manager}"
    assert_pattern 'UiEvents\.ToastRequested' "${manager}"
    assert_pattern 'QuestContentIds\.MainFoundationBreakthrough' "${manager}"
    assert_pattern 'SceneManager\.sceneUnloaded' "${manager}"
    assert_pattern 'GameState\.Paused' "${manager}"

    assert_pattern 'item_pill_foundation' "${config}"
    assert_pattern 'item_pill_goldencore' "${config}"
    assert_pattern 'foundationPill\.MaxStack = 5' "${config}"
    assert_pattern 'goldenCorePill\.MaxStack = 5' "${config}"
    assert_pattern 'FoundationPill = "item_pill_foundation"' "${inventory_ids}"
    assert_pattern 'GoldenCorePill = "item_pill_goldencore"' "${inventory_ids}"
    assert_pattern 'MainFoundationBreakthrough = "quest_main_01_08"' "${quest_ids}"

    assert_pattern 'cultivation\.IsBreakthroughInvincible' "${player}"
    assert_pattern 'CultivationEvents\.RealmBreakthrough' "${player}"
    assert_pattern 'public bool HasQueuedMapLoad' "${scene_loader}"
    assert_pattern 'cultivation\.IsBreakthroughActive' "${scene_loader}"
    assert_pattern 'public bool CancelQueuedMapLoad' "${scene_loader}"
    assert_pattern 'public sealed class BreakthroughCeremonyView' "${ceremony}"
    assert_pattern 'CurrentBeat' "${ceremony}"
    assert_pattern 'case 1:' "${ceremony}"
    assert_pattern 'case 2:' "${ceremony}"
    assert_pattern 'case 3:' "${ceremony}"
    assert_pattern 'case 4:' "${ceremony}"
    assert_pattern 'AddComponent<BreakthroughCeremonyView>' "${ui_bootstrap}"

    assert_pattern 'BreakthroughBlocker.*Code.*MessageKey.*RelatedItemId.*AcquisitionHintKeys' "${schema_tests}"
    assert_pattern 'item_pill_foundation' "${config_tests}"
    assert_pattern 'BlockersDriveToastForStageItemCombatStateAndClosedRealm' "${tests}"
    assert_pattern 'FiveBeatSuccessLocksInputGrantsThreeSecondInvincibilityAndConsumesPill' "${tests}"
    assert_pattern 'FailureKeepsPillAppliesPenaltyHeartDemonAndDetailedToast' "${tests}"
    assert_pattern 'FoundationPityConsumesOnStartRestoresOnInterruptAndPersistsAfterRoll' "${tests}"
    assert_pattern 'FoundationCanReachGoldenCorePityIsIgnoredAndPauseSuspendsCeremony' "${tests}"
    assert_pattern 'TeleportRequestQueuesUntilBreakthroughCeremonyEnds' "${tests}"

    assert_pattern 'public struct BreakthroughBlocker' docs/03_DATA_LAYER.md
    assert_pattern '^\| ui_bt_fail_detail \|' docs/09_CONTENT.md
    assert_pattern '^\| ui_bt_block_item \|' docs/09_CONTENT.md
    assert_pattern '状态机（逻辑 3 态）' docs/05_CULTIVATION.md

    if rg -q 'AuxiliaryPill|BreakthroughAuxiliary|PerfectDodge|Poise' \
        "${PROJECT_ROOT}/${manager}" \
        "${PROJECT_ROOT}/${ceremony}"; then
        printf 'G02-01 contains explicitly out-of-scope behavior.\n' >&2
        exit 1
    fi

    printf 'G02-01 static validation passed.\n'
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

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G02-01-editmode.xml" \
    -logFile "${RESULTS_DIR}/G02-01-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0201PlayModeTests \
    -testResults "${RESULTS_DIR}/G02-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G02-01-playmode.log"

printf 'G02-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
