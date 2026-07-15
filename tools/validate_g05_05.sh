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
    "${SCRIPT_DIR}/validate_g05_04.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local ids="Assets/_Project/Scripts/Systems/Quest/QuestContentIds.cs"
    local quest="Assets/_Project/Scripts/Systems/Quest/QuestManager.cs"
    local daily="Assets/_Project/Scripts/Systems/Quest/DailyQuestManager.cs"
    local daily_contract="Assets/_Project/Scripts/Systems/Quest/IDailyQuestService.cs"
    local events="Assets/_Project/Scripts/Systems/Quest/DailyQuestEvents.cs"
    local runtime="Assets/_Project/Scripts/Data/Runtime/DailyQuestRuntimeData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local save="Assets/_Project/Scripts/Data/Save/SaveManager.cs"
    local npc="Assets/_Project/Scripts/Entities/NPC/NpcRuntimeBootstrap.cs"
    local bandit="Assets/_Project/Scripts/Entities/Enemy/BanditRuntimeBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0505PlayModeTests.cs"
    local files=("${ids}" "${quest}" "${daily}" "${daily_contract}" "${events}" "${runtime}" "${config}" "${save}" "${npc}" "${bandit}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'SideQuests' "${ids}"
    assert_pattern 'DailyQuests' "${ids}"
    assert_pattern 'item_quest_hermit_letter' "${config}"
    assert_pattern 'skill_wind_slash' "${config}"
    assert_pattern 'TurnInCosts' "${quest}"
    assert_pattern 'FactionReputation' "${quest}"
    assert_pattern 'ResetInterval = TimeSpan.FromHours\(24d\)' "${daily}"
    assert_pattern 'AcquireSource.Gather' "${daily}"
    assert_pattern 'optional: true' "${daily}"
    assert_pattern 'IsOptional' "${save}"
    assert_pattern 'NPC_DandingGuide_Greybox' "${npc}"
    assert_pattern 'NPC_Hermit_Greybox' "${npc}"
    assert_pattern 'Spawner_QingshiBandits' "${bandit}"
    assert_pattern 'OnDailyQuestProgressed' "${events}"
    assert_pattern 'ThreeSideQuestsCompleteWithExactRewards' "${tests}"
    assert_pattern 'DailyCycleResetsAfterTwentyFourHoursAndCorruptionIsOptional' "${tests}"

    assert_pattern 'OnDailyQuestReset' docs/02_ARCHITECTURE.md
    assert_pattern 'dailies.json' docs/03_DATA_LAYER.md
    assert_pattern 'quest_side_hermit_01' docs/09_CONTENT.md
    assert_pattern 'item_name_item_quest_hermit_letter' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G05-05\nphase: 5\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G05-05 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G05-05 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G05-05-editmode.xml" \
        -logFile "${RESULTS_DIR}/G05-05-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS06PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0503PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0505PlayModeTests' \
    -testResults "${RESULTS_DIR}/G05-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/G05-05-playmode.log"

printf 'G05-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
