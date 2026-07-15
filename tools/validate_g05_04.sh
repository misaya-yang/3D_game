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
    "${SCRIPT_DIR}/validate_g05_03.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local quest_data="Assets/_Project/Scripts/Data/ScriptableObjects/QuestData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local ids="Assets/_Project/Scripts/Systems/Quest/QuestContentIds.cs"
    local quest="Assets/_Project/Scripts/Systems/Quest/QuestManager.cs"
    local contract="Assets/_Project/Scripts/Systems/Quest/IQuestService.cs"
    local npc="Assets/_Project/Scripts/Entities/NPC/NPCController.cs"
    local npc_bootstrap="Assets/_Project/Scripts/Entities/NPC/NpcRuntimeBootstrap.cs"
    local area="Assets/_Project/Scripts/Systems/World/WorldAreaMarker.cs"
    local gate="Assets/_Project/Scripts/Systems/World/BlackwindDungeonGate.cs"
    local cangwu="Assets/_Project/Scripts/Entities/Enemy/CangwuTrialRuntimeBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0504PlayModeTests.cs"
    local files=("${quest_data}" "${config}" "${ids}" "${quest}" "${contract}" "${npc}" "${npc_bootstrap}" "${area}" "${gate}" "${cangwu}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'ObjectivesAreOrdered' "${quest_data}"
    assert_pattern 'StartNpcId' "${quest_data}"
    assert_pattern 'MainChapterOne' "${ids}"
    assert_pattern 'MainGoldenCoreBreakthrough = "quest_main_01_09"' "${ids}"
    assert_pattern 'MainDefeatStoneGeneral = "quest_main_01_10"' "${ids}"
    assert_pattern 'RegisterBuiltInMainQuestContent' "${config}"
    assert_pattern 'item_pill_foundation' "${config}"
    assert_pattern 'item_pill_goldencore' "${config}"
    assert_pattern 'Ordered = true' "${config}"
    assert_pattern 'NotifyCraft' "${contract}"
    assert_pattern 'ResolveInteractionDialogueId' "${contract}"
    assert_pattern 'AlchemyEvents.CraftCompleted' "${quest}"
    assert_pattern 'FoundationPillGrantedFlag' "${quest}"
    assert_pattern 'CanProgressObjective' "${quest}"
    assert_pattern 'MapContentIds.CangwuPathOpenFlag' "${quest}"
    assert_pattern 'ResolveInteractionDialogueId' "${npc}"
    assert_pattern 'NPC_CangwuGuard_Greybox' "${npc_bootstrap}"
    assert_pattern 'NPC_BlackwindEcho_B5_Greybox' "${npc_bootstrap}"
    assert_pattern 'quests.NotifyReach\(AreaId\)' "${area}"
    assert_pattern 'quests.NotifyReach\(QuestContentIds.BlackwindEntrance\)' "${gate}"
    assert_pattern 'Spawner_CangwuMountainRoadTrial' "${cangwu}"

    assert_pattern 'MainChapterRegistersTenQuestsDialoguesAndNpcRoutes' "${tests}"
    assert_pattern 'MainChapterCanAdvanceFromQuestOneThroughQuestTen' "${tests}"
    assert_pattern 'FoundationAcceptRewardSkipsDuplicateHistoricalPillButEnablesPity' "${tests}"
    assert_pattern 'GoldenCoreObjectivesRejectEarlyEventsAndRouteEchoDialogues' "${tests}"
    assert_pattern 'foundation_pill_granted' docs/09_CONTENT.md
    assert_pattern 'ObjectivesAreOrdered' docs/03_DATA_LAYER.md
    assert_pattern 'NotifyCraft' docs/07_WORLD_ENEMY_QUEST.md

    if ! rg -U -q \
        'id: G05-04\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-04 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-04 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G05-04-editmode.xml" \
        -logFile "${RESULTS_DIR}/G05-04-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0503PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-04-playmode.log"

printf 'G05-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
