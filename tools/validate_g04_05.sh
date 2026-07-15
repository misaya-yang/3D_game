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
    *) printf 'Usage: %s [--static-only]\n' "$0" >&2; exit 64 ;;
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
    "${SCRIPT_DIR}/validate_g04_04.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/Enemy/EnemyContentIds.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local view="Assets/_Project/Scripts/Entities/Enemy/BossSkillTelegraphView.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0405PlayModeTests.cs"
    local files=("${ids}" "${config}" "${brain}" "${view}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'StoneGeneralSlamWarningVfx' "${ids}"
    assert_pattern 'StoneGeneralChargeWarningVfx' "${ids}"
    assert_pattern 'StoneGeneralSummonWarningVfx' "${ids}"
    assert_pattern 'Duration = 0.6f' "${config}"
    assert_pattern 'Duration = 0.8f' "${config}"
    assert_pattern 'Duration = 1f' "${config}"
    assert_pattern 'RecoverStun = 1.5f' "${config}"
    assert_pattern 'VFX_Boss_Charge_Warning' "${config}"
    assert_pattern 'VFX_Boss_Summon_Warning' "${config}"

    assert_pattern 'ActiveBossTelegraph' "${brain}"
    assert_pattern 'BossTelegraphRemaining' "${brain}"
    assert_pattern 'IsInBossRecovery' "${brain}"
    assert_pattern 'ResolveBossTelegraph' "${brain}"
    assert_pattern 'SkillId = skillId' "${brain}"
    assert_pattern 'public sealed class BossSkillTelegraphView' "${view}"
    assert_pattern 'BossSkillTelegraph_Greybox' "${view}"
    assert_pattern 'TelegraphShape.Line' "${view}"

    assert_pattern 'EveryBossPhaseHasReadableTelegraphForEverySkill' "${tests}"
    assert_pattern 'TelegraphViewPersistsUntilImpactThenUsesConfiguredRecovery' "${tests}"
    assert_pattern 'BossHitCarriesSkillIdAndRecoveryWindowIsPunishable' "${tests}"
    assert_pattern 'LeavingArenaCancelsActiveTelegraph' "${tests}"
    assert_pattern 'VFX_Boss_Charge_Warning' docs/09_CONTENT.md
    assert_pattern 'Duration≥0.6s' docs/07_WORLD_ENEMY_QUEST.md

    if ! rg -U -q \
        'id: G04-05\nphase: 4\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G04-05 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G04-05 static validation passed.\n'
}

run_static_validation
[[ "${STATIC_ONLY}" == true ]] && exit 0

[[ -x "${UNITY_EDITOR}" ]] || {
    printf 'Unity Editor not found or not executable: %s\n' "${UNITY_EDITOR}" >&2
    exit 2
}

mkdir -p "${RESULTS_DIR}"
"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform EditMode \
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G04-05-editmode.xml" \
    -logFile "${RESULTS_DIR}/G04-05-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0402PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0403PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0404PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0405PlayModeTests' \
    -testResults "${RESULTS_DIR}/G04-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/G04-05-playmode.log"

printf 'G04-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
