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
    "${SCRIPT_DIR}/validate_g06_04.sh" --static-only
    "${SCRIPT_DIR}/check_docs_consistency.sh"

    local feedback="Assets/_Project/Scripts/Systems/Feedback"
    local files=(
        "${feedback}/FeedbackContentIds.cs"
        "${feedback}/IVfxService.cs"
        "${feedback}/VFXManager.cs"
        "${feedback}/IAudioService.cs"
        "${feedback}/AudioManager.cs"
        "${feedback}/AudioStateController.cs"
        "${feedback}/CombatFeedbackController.cs"
        "Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
        "Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
        "Assets/_Project/Scripts/Entities/Enemy/BossSkillTelegraphView.cs"
        "Assets/_Project/Scripts/Entities/Enemy/BossArenaController.cs"
        "Assets/_Project/Scripts/Systems/Skill/SkillProjectile.cs"
        "Assets/_Project/Tests/PlayMode/VerticalSlice/G0605PlayModeTests.cs"
    )
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'BGM_Explore_Qingshi' "${feedback}/FeedbackContentIds.cs"
    assert_pattern 'SFX_Skill_Lightning' "${feedback}/FeedbackContentIds.cs"
    assert_pattern 'VFX_Boss_Summon_Warning' "${feedback}/FeedbackContentIds.cs"
    assert_pattern 'class VFXManager : Singleton<VFXManager>' "${feedback}/VFXManager.cs"
    assert_pattern 'PlayAttached' "${feedback}/VFXManager.cs"
    assert_pattern 'class AudioManager : Singleton<AudioManager>' "${feedback}/AudioManager.cs"
    assert_pattern 'CrossfadeBGM' "${feedback}/AudioManager.cs"
    assert_pattern 'CombatCrossfadeSeconds = 1f' "${feedback}/AudioStateController.cs"
    assert_pattern 'CombatMusicExitDelaySeconds = 8f' "${feedback}/AudioStateController.cs"
    assert_pattern 'BgmPlaybackState\.Boss' "${feedback}/AudioStateController.cs"
    assert_pattern 'ElementReactionTriggered' "${feedback}/CombatFeedbackController.cs"
    assert_pattern 'public void OnAttackHit\(\)' "Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    assert_pattern 'public void OnSkillCastPoint\(\)' "Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    assert_pattern 'VfxContentIds\.IsKnown\(VfxId\)' "Assets/_Project/Scripts/Entities/Enemy/BossSkillTelegraphView.cs"
    assert_pattern 'SetBossEncounter' "Assets/_Project/Scripts/Entities/Enemy/BossArenaController.cs"
    assert_pattern 'SkillQiBoltProjectile' "Assets/_Project/Tests/PlayMode/VerticalSlice/G0605PlayModeTests.cs"
    assert_pattern 'BgmStateTransitionsExploreCombatBossAndBackAfterEightSeconds' "Assets/_Project/Tests/PlayMode/VerticalSlice/G0605PlayModeTests.cs"

    assert_pattern 'G06-05 音画与动画事件实现说明' docs/08_UI_META.md
    assert_pattern 'G06-05 ID 接入说明' docs/09_CONTENT.md
    if ! rg -U -q \
        'id: G06-05\nphase: 6\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G06-05 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G06-05 static validation passed.\n'
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
        -testResults "${RESULTS_DIR}/G06-05-editmode.xml" \
        -logFile "${RESULTS_DIR}/G06-05-editmode.log"
fi

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.G0101PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0302PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0403PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0404PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0605PlayModeTests' \
    -testResults "${RESULTS_DIR}/G06-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/G06-05-playmode.log"

printf 'G06-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
