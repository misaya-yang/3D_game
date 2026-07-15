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
    "${SCRIPT_DIR}/validate_g04_02.sh" --static-only

    local ids="Assets/_Project/Scripts/Systems/Enemy/EnemyContentIds.cs"
    local events="Assets/_Project/Scripts/Systems/Combat/CombatEvents.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local arena="Assets/_Project/Scripts/Entities/Enemy/BossArenaController.cs"
    local bootstrap="Assets/_Project/Scripts/Entities/Enemy/StoneGeneralRuntimeBootstrap.cs"
    local bar="Assets/_Project/Scripts/Systems/UI/Combat/BossHealthBarView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0403PlayModeTests.cs"

    local files=("${ids}" "${events}" "${config}" "${brain}" "${arena}" "${bootstrap}" "${bar}" "${ui_bootstrap}" "${tests}")
    local file_path
    for file_path in "${files[@]}"; do assert_file "${file_path}"; done

    assert_pattern 'StoneGeneral = "enemy_boss_stone_general"' "${ids}"
    assert_pattern 'BlackwindSpawn = "enemy_blackwind_spawn"' "${ids}"
    assert_pattern 'BossPhaseChanged = "OnBossPhaseChanged"' "${events}"
    assert_pattern 'stoneGeneral.MaxHp = 12000f' "${config}"
    assert_pattern 'stoneGeneral.Attack = 70f' "${config}"
    assert_pattern 'stoneGeneral.CultivationXpReward = 2000f' "${config}"
    assert_pattern 'stoneGeneral.BossPhases = new\[\]' "${config}"
    assert_pattern 'PhaseIndex = 0' "${config}"
    assert_pattern 'PhaseIndex = 1' "${config}"
    assert_pattern 'PhaseIndex = 2' "${config}"

    assert_pattern 'BossPhaseTransitionSeconds = 1.5f' "${brain}"
    assert_pattern 'BossRageAttackSpeedMultiplier = 1.3f' "${brain}"
    assert_pattern 'EvaluateBossPhaseTransition' "${brain}"
    assert_pattern 'CombatEvents.BossPhaseChanged' "${brain}"
    assert_pattern 'SummonBlackwindAdds' "${brain}"
    assert_pattern 'ResetBossEncounter' "${brain}"
    assert_pattern 'ICombatDefenseProvider' "${brain}"

    assert_pattern 'public sealed class BossArenaController' "${arena}"
    assert_pattern 'DefaultArenaRadius = 6.5f' "${arena}"
    assert_pattern '_boss.ResetBossEncounter' "${arena}"
    assert_pattern 'public static class StoneGeneralRuntimeBootstrap' "${bootstrap}"
    assert_pattern 'Arena_StoneGeneral_Runtime' "${bootstrap}"
    assert_pattern 'SphereCollider' "${bootstrap}"

    assert_pattern 'public sealed class BossHealthBarView' "${bar}"
    assert_pattern 'enemy_name_enemy_boss_stone_general' "${bar}"
    assert_pattern 'ui_boss_phase' "${bar}"
    assert_pattern 'CombatEvents.BossPhaseChanged' "${bar}"
    assert_pattern 'AddComponent<BossHealthBarView>' "${ui_bootstrap}"

    assert_pattern 'StoneGeneralContentArenaAndBossBarMatchSpec' "${tests}"
    assert_pattern 'HpThresholdsPublishPhaseEventsAndGrantTransitionInvincibility' "${tests}"
    assert_pattern 'EachBossPhaseSelectsItsOwnSkillSet' "${tests}"
    assert_pattern 'LeavingArenaResetsBossHealthPhaseTargetAndBossBar' "${tests}"
    assert_pattern '^\| OnBossPhaseChanged \| BossPhaseInfo \| BossAI \| HUD, Audio, VFX \|$' docs/02_ARCHITECTURE.md
    assert_pattern '^\| enemy_name_enemy_boss_stone_general \| 黑风石将军 \|$' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G04-03\nphase: 4\nstatus: (in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G04-03 must be active or delivered in docs/10_GOALS.md.\n' >&2
        exit 1
    fi

    printf 'G04-03 static validation passed.\n'
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
    -testResults "${RESULTS_DIR}/G04-03-editmode.xml" \
    -logFile "${RESULTS_DIR}/G04-03-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode -nographics -projectPath "${PROJECT_ROOT}" \
    -runTests -testPlatform PlayMode \
    -testFilter 'Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0401PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0402PlayModeTests;Wendao.Tests.PlayMode.VerticalSlice.G0403PlayModeTests' \
    -testResults "${RESULTS_DIR}/G04-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G04-03-playmode.log"

printf 'G04-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
