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

assert_json_binding() {
    local action="$1"
    local path="$2"
    local input_actions="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    if ! jq -e --arg action "${action}" --arg path "${path}" \
        '.maps[] | .bindings[] | select(.action == $action and .path == $path)' \
        "${PROJECT_ROOT}/${input_actions}" >/dev/null; then
        printf 'Input action %s is missing binding %s.\n' \
            "${action}" "${path}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_gvs_05.sh" --static-only

    local event_params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local quest_runtime="Assets/_Project/Scripts/Data/Runtime/QuestRuntimeData.cs"
    local quest_data="Assets/_Project/Scripts/Data/ScriptableObjects/QuestData.cs"
    local dialogue_data="Assets/_Project/Scripts/Data/ScriptableObjects/DialogueData.cs"
    local npc_data="Assets/_Project/Scripts/Data/ScriptableObjects/NPCData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local quest_ids="Assets/_Project/Scripts/Systems/Quest/QuestContentIds.cs"
    local quest_events="Assets/_Project/Scripts/Systems/Quest/QuestEvents.cs"
    local quest_contract="Assets/_Project/Scripts/Systems/Quest/IQuestService.cs"
    local quest_manager="Assets/_Project/Scripts/Systems/Quest/QuestManager.cs"
    local dialogue_events="Assets/_Project/Scripts/Systems/NPC/DialogueEvents.cs"
    local dialogue_contract="Assets/_Project/Scripts/Systems/NPC/IDialogueService.cs"
    local dialogue_manager="Assets/_Project/Scripts/Systems/NPC/DialogueManager.cs"
    local npc_controller="Assets/_Project/Scripts/Entities/NPC/NPCController.cs"
    local npc_bootstrap="Assets/_Project/Scripts/Entities/NPC/NpcRuntimeBootstrap.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local dialogue_view="Assets/_Project/Scripts/Systems/UI/NPC/DialogueView.cs"
    local quest_tracker="Assets/_Project/Scripts/Systems/UI/Quest/QuestTrackerView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local data_tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS06PlayModeTests.cs"

    local required_files=(
        "${event_params}"
        "${quest_runtime}"
        "${quest_data}"
        "${dialogue_data}"
        "${npc_data}"
        "${config}"
        "${input_contract}"
        "${input_reader}"
        "${quest_ids}"
        "${quest_events}"
        "${quest_contract}"
        "${quest_manager}"
        "${dialogue_events}"
        "${dialogue_contract}"
        "${dialogue_manager}"
        "${npc_controller}"
        "${npc_bootstrap}"
        "${player_bootstrap}"
        "${scene_bootstrap}"
        "${dialogue_view}"
        "${quest_tracker}"
        "${ui_bootstrap}"
        "${data_tests}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_json_binding Interact '<Keyboard>/e'
    assert_json_binding Interact '<Gamepad>/buttonWest'
    assert_pattern 'bool InteractPressedThisFrame' "${input_contract}"
    assert_pattern '_interactAction = _playerMap\.FindAction\("Interact", true\)' "${input_reader}"
    assert_pattern 'CanReadUi\(_interactAction\)' "${input_reader}"

    assert_pattern 'public bool Cancelled;' "${event_params}"
    assert_pattern 'public sealed class QuestRuntimeState' "${quest_runtime}"
    assert_pattern 'public bool AcceptRewardsGranted;' "${quest_runtime}"
    assert_pattern 'public sealed class QuestSaveData' "${quest_runtime}"
    assert_pattern 'public string DisplayNameKey;' "${quest_data}"
    assert_pattern 'public string DescriptionKey;' "${quest_data}"
    assert_pattern 'public string SpeakerNameKey;' "${dialogue_data}"
    assert_pattern 'public string TextKey;' "${dialogue_data}"
    assert_pattern 'public string QuestTurnInId;' "${dialogue_data}"
    assert_pattern 'public string DisplayNameKey;' "${npc_data}"

    assert_pattern 'MainHuntWolves = "quest_main_01_02"' "${quest_ids}"
    assert_pattern 'GreyWolfEnemy = "enemy_wolf_gray"' "${quest_ids}"
    assert_pattern 'YaoLaoNpc = "npc_yaolao"' "${quest_ids}"
    assert_pattern 'HuntStartDialogue = "dlg_main_01_02_start"' "${quest_ids}"
    assert_pattern 'HuntCompleteDialogue = "dlg_main_01_02_complete"' "${quest_ids}"

    assert_pattern 'huntQuest\.Id = "quest_main_01_02"' "${config}"
    assert_pattern 'TargetId = "enemy_wolf_gray"' "${config}"
    assert_pattern 'RequiredCount = 3' "${config}"
    assert_pattern 'CultivationXp = 700f' "${config}"
    assert_pattern 'SpiritStones = 10' "${config}"
    assert_pattern 'QuestOfferId = "quest_main_01_02"' "${config}"
    assert_pattern 'QuestTurnInId = "quest_main_01_02"' "${config}"
    assert_pattern 'yaoLao\.Id = "npc_yaolao"' "${config}"
    assert_pattern 'GetQuest\(string id\)' "${config}"
    assert_pattern 'GetDialogue\(string id\)' "${config}"
    assert_pattern 'GetNpc\(string id\)' "${config}"

    assert_pattern 'Accepted = "OnQuestAccepted"' "${quest_events}"
    assert_pattern 'Progressed = "OnQuestProgressed"' "${quest_events}"
    assert_pattern 'Completed = "OnQuestCompleted"' "${quest_events}"
    assert_pattern 'public interface IQuestService' "${quest_contract}"
    assert_pattern 'bool Accept\(string questId\)' "${quest_contract}"
    assert_pattern 'void NotifyKill\(string enemyId\)' "${quest_contract}"
    assert_pattern 'bool TurnIn\(string questId\)' "${quest_contract}"
    assert_pattern 'public sealed class QuestManager : SafeBehaviour, IQuestService' "${quest_manager}"
    assert_pattern 'SaveModuleName = "quests"' "${quest_manager}"
    assert_pattern 'CombatEvents.EnemyKilled' "${quest_manager}"
    assert_pattern 'Mathf\.Min\(required, current \+ Mathf\.Max\(0, value\)\)' "${quest_manager}"
    assert_pattern 'state.Status = QuestStatus.Completed' "${quest_manager}"
    assert_pattern 'state.Status = QuestStatus.TurnedIn' "${quest_manager}"
    assert_pattern 'QuestEvents.Completed' "${quest_manager}"
    assert_pattern 'AcceptRewardsGranted = true' "${quest_manager}"
    assert_pattern 'RestoreSaveData\(QuestSaveData data\)' "${quest_manager}"

    assert_pattern 'Started = "OnDialogueStarted"' "${dialogue_events}"
    assert_pattern 'Ended = "OnDialogueEnded"' "${dialogue_events}"
    assert_pattern 'public interface IDialogueService' "${dialogue_contract}"
    assert_pattern 'void EndDialogue\(bool cancelled\)' "${dialogue_contract}"
    assert_pattern 'public sealed class DialogueManager : SafeBehaviour, IDialogueService' "${dialogue_manager}"
    assert_pattern 'gameManager\.TrySetState\(GameState\.Dialogue\)' "${dialogue_manager}"
    assert_pattern 'gameManager\.TrySetState\(GameState\.Playing\)' "${dialogue_manager}"
    assert_pattern 'CurrentNode\.QuestOfferId' "${dialogue_manager}"
    assert_pattern 'CurrentNode\.QuestTurnInId' "${dialogue_manager}"
    assert_pattern 'EndDialogue\(true\)' "${dialogue_manager}"
    assert_pattern 'SceneManager\.activeSceneChanged' "${dialogue_manager}"

    assert_pattern 'public sealed class NPCController : SafeBehaviour' "${npc_controller}"
    assert_pattern 'InteractionDistance = 3f' "${npc_controller}"
    assert_pattern '_input\.InteractPressedThisFrame' "${npc_controller}"
    assert_pattern 'ResolveInteractionDialogueId' "${npc_controller}"
    assert_pattern 'public static class NpcRuntimeBootstrap' "${npc_bootstrap}"
    assert_pattern 'new Vector3\(-2f, 1f, 1\.5f\)' "${npc_bootstrap}"
    assert_pattern 'NpcRuntimeBootstrap\.Install\(\)' "${player_bootstrap}"
    assert_pattern 'AddComponent<QuestManager>' "${scene_bootstrap}"
    assert_pattern 'AddComponent<DialogueManager>' "${scene_bootstrap}"

    assert_pattern 'public sealed class DialogueView : MonoBehaviour' "${dialogue_view}"
    assert_pattern 'ui_dialogue_continue' "${dialogue_view}"
    assert_pattern 'InteractPressedThisFrame' "${dialogue_view}"
    assert_pattern 'public sealed class QuestTrackerView : MonoBehaviour' "${quest_tracker}"
    assert_pattern 'QuestEvents.Progressed' "${quest_tracker}"
    assert_pattern 'ui_quest_ready_turn_in' "${quest_tracker}"
    assert_pattern 'AddComponent<DialogueView>' "${ui_bootstrap}"
    assert_pattern 'AddComponent<QuestTrackerView>' "${ui_bootstrap}"

    if rg -q 'class EnemyBrain|class EnemySpawner|NavMesh|DropLoot' \
        "${PROJECT_ROOT}/${quest_manager}" \
        "${PROJECT_ROOT}/${dialogue_manager}" \
        "${PROJECT_ROOT}/${npc_controller}" \
        "${PROJECT_ROOT}/${npc_bootstrap}"; then
        printf 'G-VS-06 contains out-of-scope wolf AI, spawning, or loot logic.\n' >&2
        exit 1
    fi

    if rg -U -q \
        'id: G05-04\nphase: 5\nstatus: pending' \
        "${PROJECT_ROOT}/docs/10_GOALS.md" \
        && rg -q 'quest_main_01_0(1|3|4|5|6|7|8|9)|quest_main_01_10' \
            "${PROJECT_ROOT}/${config}" \
            "${PROJECT_ROOT}/${quest_manager}" \
            "${PROJECT_ROOT}/${dialogue_manager}"; then
        printf 'G-VS-06 contains out-of-scope full-main-quest content.\n' >&2
        exit 1
    fi

    assert_pattern 'QuestRuntimeState' "${data_tests}"
    assert_pattern 'QuestTurnInId' "${data_tests}"
    assert_pattern 'DialogueId", "Cancelled' "${data_tests}"
    assert_pattern 'G-VS-06 代码优先闭环' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern '真实灰狼生成、追击和掉落仍归 G-VS-07' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern '^\| quest_main_01_02 \| Main \| npc_yaolao \| Kill enemy_wolf_gray ×3 \| 原始修为 700，灵石 10 \|$' docs/09_CONTENT.md
    assert_pattern '^\| dlg_main_01_02_complete \| 猎狼交付 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_npc_interact \| \[E\] 与\{0\}交谈 \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_quest_ready_turn_in \| 目标已完成，可以交付 \|$' docs/09_CONTENT.md
    assert_pattern '### 5\.4 G-VS-06 任务 DTO' docs/03_DATA_LAYER.md
    assert_pattern '### 7\.1\.3 quests\.json（G-VS-06）' docs/02_ARCHITECTURE.md
    assert_pattern 'G-VS-06 增加 Order 300' docs/08_UI_META.md
    assert_pattern 'id: G-VS-06' docs/10_GOALS.md
    assert_pattern 'status: (implemented|done)' docs/10_GOALS.md

    assert_pattern 'HuntQuestDialogueNpcAndInteractBindingsMatchContentContract' "${tests}"
    assert_pattern 'NpcDialogueAcceptsQuestLocksInputAndUpdatesTracker' "${tests}"
    assert_pattern 'CancelledDialogueRestoresInputWithoutAcceptingQuest' "${tests}"
    assert_pattern 'WolfKillEventsCapProgressAndTurnInGrantsRewardExactlyOnce' "${tests}"
    assert_pattern 'ActiveQuestProgressRoundTripsThroughQuestsModule' "${tests}"

    printf 'G-VS-06 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
}

run_static_validation

if [[ "${STATIC_ONLY}" == true ]]; then
    exit 0
fi

if [[ ! -x "${UNITY_EDITOR}" ]]; then
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    printf 'Install Unity 6000.5.3f1 or set UNITY_EDITOR to its executable path.\n' >&2
    exit 2
fi

mkdir -p "${RESULTS_DIR}"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode \
    -testResults "${RESULTS_DIR}/GVS-06-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-06-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests \
    -testResults "${RESULTS_DIR}/GVS-06-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-06-playmode.log"

printf 'G-VS-06 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
