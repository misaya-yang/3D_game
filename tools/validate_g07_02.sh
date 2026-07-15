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
    "${SCRIPT_DIR}/validate_g07_01.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local files=(
        "Assets/_Project/Scripts/Systems/Content/MvpContentAudit.cs"
        "Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
        "Assets/_Project/Scripts/Systems/World/BlackwindDungeonFactory.cs"
        "Assets/_Project/Scripts/Systems/Crafting/GatheringContentIds.cs"
        "Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
        "Assets/_Project/Scripts/Data/ScriptableObjects/SkillData.cs"
        "Assets/_Project/Scripts/Data/ScriptableObjects/EnemyData.cs"
        "Assets/_Project/Scripts/Data/ScriptableObjects/AchievementData.cs"
        "Assets/_Project/Scripts/Data/ScriptableObjects/TitleData.cs"
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0702PlayModeTests.cs"
    )
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'class MvpContentAudit' \
        "Assets/_Project/Scripts/Systems/Content/MvpContentAudit.cs"
    assert_pattern 'class MvpBalanceAudit' \
        "Assets/_Project/Scripts/Systems/Content/MvpContentAudit.cs"
    assert_pattern 'CompletionMaximumTarget = 300' \
        "Assets/_Project/Scripts/Systems/Content/MvpContentAudit.cs"
    assert_pattern 'EstimatedMainStoryMinutes' \
        "Assets/_Project/Scripts/Systems/Content/MvpContentAudit.cs"
    assert_pattern 'RequiredGatherableCount = 8' \
        "Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
    assert_pattern 'RequiredChestCount = 3' \
        "Assets/_Project/Scripts/Systems/World/CangwuGreyboxFactory.cs"
    assert_pattern 'RequiredChestCount = 3' \
        "Assets/_Project/Scripts/Systems/World/BlackwindDungeonFactory.cs"
    assert_pattern 'DisplayNameKey' \
        "Assets/_Project/Scripts/Data/ScriptableObjects/SkillData.cs"
    assert_pattern 'ApplyLocalizationFallbackKeys' \
        "Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    assert_pattern 'MainStoryEconomyAndFireRootPacingStayInsideTargets' \
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0702PlayModeTests.cs"
    assert_pattern 'MvpContentGraphHasStableIdsAndLocalizedDefaults' \
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0702PlayModeTests.cs"

    assert_pattern 'G07-02 可复现平衡记录' docs/09_CONTENT.md
    assert_pattern '主线 10 结余.*251—293' docs/09_CONTENT.md
    if ! rg -U -q \
        'id: G07-02\nphase: 7\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G07-02 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G07-02 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G07-02-editmode.xml" \
        -logFile "${RESULTS_DIR}/G07-02-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0303PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0304PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0305PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0404PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0502PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0503PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0504PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0506PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0604PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0605PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0702PlayModeTests' \
    -testResults "${RESULTS_DIR}/G07-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G07-02-playmode.log"

printf 'G07-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
