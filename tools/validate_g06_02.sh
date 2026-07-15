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
    "${SCRIPT_DIR}/validate_g06_01.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local manager="Assets/_Project/Scripts/Systems/Tutorial/TutorialManager.cs"
    local contract="Assets/_Project/Scripts/Systems/Tutorial/ITutorialService.cs"
    local input_action="Assets/_Project/Scripts/Systems/Tutorial/TutorialInputAction.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local overlay="Assets/_Project/Scripts/Systems/UI/Tutorial/TutorialToastView.cs"
    local alchemy="Assets/_Project/Scripts/Systems/UI/Crafting/AlchemyPanelView.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0602PlayModeTests.cs"
    local files=("${manager}" "${contract}" "${input_action}" "${input_reader}" "${overlay}" "${alchemy}" "${bootstrap}" "${params}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'SkillTutorialId = "tut_skill"' "${manager}"
    assert_pattern 'InventoryTutorialId = "tut_inv"' "${manager}"
    assert_pattern 'CultivationTutorialId = "tut_cult"' "${manager}"
    assert_pattern 'DungeonTutorialId = "tut_dungeon"' "${manager}"
    assert_pattern 'FlightTutorialId = "tut_flight"' "${manager}"
    assert_pattern 'AlchemyTutorialId = "tut_alchemy"' "${manager}"
    assert_pattern 'RequestStart\(string tutorialId\)' "${manager}"
    assert_pattern 'DismissCurrent\(\)' "${manager}"
    assert_pattern 'AllowsInput\(TutorialInputAction action\)' "${manager}"
    assert_pattern 'InventoryEvents\.ItemAcquired' "${manager}"
    assert_pattern 'SkillEvents\.SkillLearned' "${manager}"
    assert_pattern 'CultivationEvents\.XpGained' "${manager}"
    assert_pattern 'BlackwindDungeonEvents\.FloorEntered' "${manager}"
    assert_pattern 'AlchemyEvents\.CraftCompleted' "${manager}"
    assert_pattern 'IsAllowedByTutorial' "${input_reader}"
    assert_pattern 'TutorialInputAction\.Pause' "${input_reader}"
    assert_pattern 'new Image\[4\]' "${overlay}"
    assert_pattern 'TutorialMask' "${overlay}"
    assert_pattern 'raycastTarget = visible && blocksInput' "${overlay}"
    assert_pattern 'FocusRectNormalized' "${overlay}"
    assert_pattern 'tutorial\.RequestStart\(TutorialManager\.AlchemyTutorialId\)' "${alchemy}"
    assert_pattern 'if \(isGameplayScene' "${bootstrap}"
    assert_pattern 'public bool IsForced' "${params}"
    assert_pattern 'public Rect FocusRectNormalized' "${params}"
    assert_pattern 'EightKnownTutorialsAllPublishARealFourPieceCutout' "${tests}"
    assert_pattern 'ExistingWorldCompletionKeysSurviveLoadAndNeverReplay' "${tests}"

    assert_pattern 'FocusRectNormalized' docs/03_DATA_LAYER.md
    assert_pattern 'G06-02 实现说明' docs/08_UI_META.md
    assert_pattern 'tutorial_alchemy_overview' docs/09_CONTENT.md
    assert_pattern 'ui_tutorial_dismiss' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G06-02\nphase: 6\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G06-02 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G06-02 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G06-02-editmode.xml" \
        -logFile "${RESULTS_DIR}/G06-02-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS01PlayModeTests.MoveTutorialCompletesPersistsAndDoesNotReplayAfterLoad;Wendao.Tests.PlayMode.VerticalSlice.GVS02PlayModeTests.LightAttackKillsDummyShowsDamageAndPersistsCombatTutorial;Wendao.Tests.PlayMode.VerticalSlice.GVS08PlayModeTests.TutorialSkipWritesTheSameCompletionKeysAndSurvivesLoad;Wendao.Tests.PlayMode.VerticalSlice.GVS08PlayModeTests.SaveRoundTripPreservesQuestInventoryAndTutorialTogether;Wendao.Tests.PlayMode.VerticalSlice.G0303PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0601PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0602PlayModeTests' \
    -testResults "${RESULTS_DIR}/G06-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G06-02-playmode.log"

printf 'G06-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
