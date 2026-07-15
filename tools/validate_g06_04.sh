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
    "${SCRIPT_DIR}/validate_g06_03.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local faction="Assets/_Project/Scripts/Systems/Faction/FactionManager.cs"
    local achievement="Assets/_Project/Scripts/Systems/Achievement/AchievementManager.cs"
    local title="Assets/_Project/Scripts/Systems/Title/TitleManager.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local shop="Assets/_Project/Scripts/Systems/Shop/ShopSystem.cs"
    local stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local panel="Assets/_Project/Scripts/Systems/UI/Cultivation/CharacterPanelView.cs"
    local save_data="Assets/_Project/Scripts/Data/Runtime/AchievementTitleRuntimeData.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0604PlayModeTests.cs"
    local files=("${faction}" "${achievement}" "${title}" "${config}" "${shop}" "${stats}" "${panel}" "${save_data}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'RankThresholds' "${faction}"
    assert_pattern 'GetShopDiscount' "${faction}"
    assert_pattern 'FactionEvents\.ReputationChanged' "${faction}"
    assert_pattern 'faction\.GetShopDiscount' "${shop}"
    assert_pattern 'Mathf\.FloorToInt' "${shop}"
    assert_pattern 'RegisterBuiltInAchievementsAndTitles' "${config}"
    assert_pattern 'RegisterBuiltInAchievement' "${config}"
    assert_pattern 'RegisterBuiltInTitle' "${config}"
    assert_pattern 'SaveModuleName = "achievements"' "${achievement}"
    assert_pattern 'AchievementEvents\.Unlocked' "${achievement}"
    assert_pattern 'AlchemyReputationReward = 30' "${achievement}"
    assert_pattern 'SaveModuleName = "titles"' "${title}"
    assert_pattern 'TitleEvents\.Changed' "${title}"
    assert_pattern 'ActiveMaxHpPercent' "${title}"
    assert_pattern 'IPlayerTitleStatsSink' "${stats}"
    assert_pattern '_titleMaxHpPercent' "${stats}"
    assert_pattern 'CharacterActiveTitle' "${panel}"
    assert_pattern 'AchievementSaveData' "${save_data}"
    assert_pattern 'TitleSaveData' "${save_data}"
    assert_pattern 'TenAchievementsUnlockRewardsAndTitlesModifyPlayerStats' "${tests}"
    assert_pattern 'AchievementTitleAndFactionSaveRoundTrip' "${tests}"

    assert_pattern 'G06-04 宗门实现说明' docs/08_UI_META.md
    assert_pattern 'G06-04 成就与称号实现说明' docs/08_UI_META.md
    assert_pattern 'achievement_unlocked_ach_waste_body' docs/09_CONTENT.md
    assert_pattern 'FactionReputationInfo' docs/03_DATA_LAYER.md

    if ! rg -U -q \
        'id: G06-04\nphase: 6\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G06-04 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G06-04 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G06-04-editmode.xml" \
        -logFile "${RESULTS_DIR}/G06-04-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0305PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0505PlayModeTests.ThreeSideQuestsCompleteWithExactRewards;Wendao.Tests.PlayMode.VerticalSlice.G0604PlayModeTests' \
    -testResults "${RESULTS_DIR}/G06-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/G06-04-playmode.log"

printf 'G06-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
