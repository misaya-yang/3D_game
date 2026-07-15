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
    "${SCRIPT_DIR}/validate_g01_04.sh" --static-only

    local settings="Assets/_Project/Scripts/Systems/Combat/CombatFeelSettings.cs"
    local feel_contract="Assets/_Project/Scripts/Systems/Combat/ICombatFeelService.cs"
    local feel_controller="Assets/_Project/Scripts/Systems/Combat/CombatFeelController.cs"
    local hitstun_contract="Assets/_Project/Scripts/Systems/Combat/IHitstunReceiver.cs"
    local action_type="Assets/_Project/Scripts/Systems/Input/BufferedActionType.cs"
    local action_buffer="Assets/_Project/Scripts/Entities/Player/PlayerActionBuffer.cs"
    local request="Assets/_Project/Scripts/Data/Runtime/DamageRequest.cs"
    local params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local player_skill="Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local enemy="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local dummy="Assets/_Project/Scripts/Entities/Enemy/TrainingDummy.cs"
    local camera="Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs"
    local floating_text="Assets/_Project/Scripts/Systems/UI/Combat/DamageFloatingTextView.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local data_tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0105PlayModeTests.cs"

    local required_files=(
        "${settings}" "${feel_contract}" "${feel_controller}"
        "${hitstun_contract}" "${action_type}" "${action_buffer}"
        "${request}" "${params}" "${combat}" "${player}"
        "${player_combat}" "${player_skill}" "${player_bootstrap}"
        "${enemy}" "${dummy}" "${camera}" "${floating_text}"
        "${scene_bootstrap}" "${data_tests}" "${tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'InputBufferSeconds = 0\.12f' "${settings}"
    assert_pattern 'HitstopLight12Seconds = 0\.03f' "${settings}"
    assert_pattern 'HitstopLight34HeavySeconds = 0\.06f' "${settings}"
    assert_pattern 'NormalEnemyHitstunSeconds = 0\.18f' "${settings}"
    assert_pattern 'EliteHitstunMultiplier = 0\.5f' "${settings}"
    assert_pattern 'CriticalShakeIntensity = 0\.5f' "${settings}"
    assert_pattern 'CriticalShakeDuration = 0\.15f' "${settings}"
    assert_pattern 'ElementReactionFovKick = 2f' "${settings}"
    assert_pattern 'ElementReactionFovDuration = 0\.2f' "${settings}"

    assert_pattern 'public enum BufferedActionType' "${action_type}"
    assert_pattern 'LightAttack' "${action_type}"
    assert_pattern 'HeavyAttack' "${action_type}"
    assert_pattern 'Dodge' "${action_type}"
    assert_pattern 'Skill1' "${action_type}"
    assert_pattern 'DefaultExecutionOrder\(-100\)' "${action_buffer}"
    assert_pattern 'EnqueueBufferedAction\(BufferedActionType type\)' "${action_buffer}"
    assert_pattern 'CombatFeelSettings\.InputBufferSeconds' "${action_buffer}"
    assert_pattern 'public bool TryConsume' "${action_buffer}"
    assert_pattern 'public void TickBuffer' "${action_buffer}"
    assert_pattern 'HandleGameStateChanged' "${action_buffer}"
    assert_pattern 'GameState\.Dialogue' "${action_buffer}"
    assert_pattern 'GameState\.Dead' "${action_buffer}"
    assert_pattern 'GameState\.Paused' "${action_buffer}"

    assert_pattern 'TryProcessBufferedDodgeInput' "${player}"
    assert_pattern 'TryProcessBufferedCombatInput' "${player_combat}"
    assert_pattern 'TryProcessBufferedSkillInput' "${player_skill}"
    assert_pattern 'AddComponent<PlayerActionBuffer>' "${player_bootstrap}"

    assert_pattern 'public float HitstopSeconds' "${request}"
    assert_pattern 'public float HitstunSeconds' "${request}"
    assert_pattern 'public float HitstopSeconds' "${params}"
    assert_pattern 'public float HitstunSeconds' "${params}"
    assert_pattern 'HitstopSeconds = CurrentHitstopSeconds' "${player_combat}"
    assert_pattern 'CombatFeelSettings\.NormalEnemyHitstunSeconds' "${player_combat}"
    assert_pattern 'TryApplyHitstun' "${combat}"
    assert_pattern 'IHitstunReceiver' "${hitstun_contract}"
    assert_pattern 'IHitstunReceiver,' "${enemy}"
    assert_pattern 'IHitstunReceiver,' "${dummy}"
    assert_pattern 'CombatFeelSettings\.EliteHitstunMultiplier' "${enemy}"

    assert_pattern 'public sealed class CombatFeelController.*ICombatFeelService' "${feel_controller}"
    assert_pattern 'CombatEvents\.DamageApplied' "${feel_controller}"
    assert_pattern 'public void PlayHitstop\(float seconds\)' "${feel_controller}"
    assert_pattern 'Time\.unscaledDeltaTime' "${feel_controller}"
    assert_pattern 'GameState\.Paused' "${feel_controller}"
    assert_pattern 'EnsureCombatFeelController\(\)' "${scene_bootstrap}"

    local feel_bootstrap_line
    local combat_bootstrap_line
    feel_bootstrap_line="$(rg -n '^[[:space:]]+EnsureCombatFeelController\(\);' \
        "${PROJECT_ROOT}/${scene_bootstrap}" | head -n 1 | cut -d: -f1)"
    combat_bootstrap_line="$(rg -n '^[[:space:]]+EnsureCombatSystem\(\);' \
        "${PROJECT_ROOT}/${scene_bootstrap}" | head -n 1 | cut -d: -f1)"
    if [[ -z "${feel_bootstrap_line}" \
        || -z "${combat_bootstrap_line}" \
        || "${feel_bootstrap_line}" -ge "${combat_bootstrap_line}" ]]; then
        printf 'CombatFeelController must bootstrap before CombatSystem.\n' >&2
        exit 1
    fi

    assert_pattern 'HandleDamageApplied' "${camera}"
    assert_pattern 'CombatFeelSettings\.CriticalShakeIntensity' "${camera}"
    assert_pattern 'CombatFeelSettings\.CriticalShakeDuration' "${camera}"
    assert_pattern 'HandleElementReactionTriggered' "${camera}"
    assert_pattern 'PulseElementReactionFov' "${camera}"
    assert_pattern 'CombatFeelSettings\.ElementReactionFovKick' "${camera}"
    assert_pattern 'CombatEvents\.ElementReactionTriggered' "${floating_text}"
    assert_pattern 'reaction_name_shock' "${floating_text}"
    assert_pattern 'return "感电"' "${floating_text}"
    assert_pattern 'GetReactionColor' "${floating_text}"

    assert_pattern 'DamageInfo.*HitstopSeconds.*HitstunSeconds' "${data_tests}"
    assert_pattern 'DamageRequest.*HitstopSeconds.*HitstunSeconds' "${data_tests}"
    assert_pattern 'BufferedPreInputChainsAttackAndCastsSkillWhenActorBecomesFree' "${tests}"
    assert_pattern 'BufferExpiresAtOneHundredTwentyMillisecondsAndClearsOnDialogueDeath' "${tests}"
    assert_pattern 'BufferedDodgeSurvivesWindupAndCancelsAsRecoveryBegins' "${tests}"
    assert_pattern 'LightComboAndHeavyCarryTheirRequiredHitstopDurations' "${tests}"
    assert_pattern 'ResolvedDamagePlaysHitstopAndPauseSuspendsCountdown' "${tests}"
    assert_pattern 'CriticalAndElementReactionDriveExistingCameraAndColoredText' "${tests}"
    assert_pattern 'MeleeHitstunUsesFullNormalAndHalfEliteDuration' "${tests}"

    assert_pattern 'public float HitstopSeconds' docs/03_DATA_LAYER.md
    assert_pattern 'public float HitstunSeconds' docs/03_DATA_LAYER.md
    assert_pattern '默认 \*\*0\.18s\*\*' docs/04_PLAYER_COMBAT.md
    assert_pattern 'PlayerActionBuffer' docs/04_PLAYER_COMBAT.md
    assert_pattern 'reaction_name_\*' docs/04_PLAYER_COMBAT.md
    assert_pattern '^\| reaction_name_shock \| 感电 \|$' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G01-05\nphase: 1\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G01-05 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    if rg -q 'PerfectDodge|Perfect Dodge|Poise|FeatureFlags' \
        "${PROJECT_ROOT}/${settings}" \
        "${PROJECT_ROOT}/${feel_controller}" \
        "${PROJECT_ROOT}/${action_buffer}" \
        "${PROJECT_ROOT}/${player}" \
        "${PROJECT_ROOT}/${player_combat}" \
        "${PROJECT_ROOT}/${player_skill}"; then
        printf 'G01-05 contains explicitly out-of-scope behavior.\n' >&2
        exit 1
    fi

    printf 'G01-05 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testFilter Wendao.Tests.EditMode.Data \
    -testResults "${RESULTS_DIR}/G01-05-editmode.xml" \
    -logFile "${RESULTS_DIR}/G01-05-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0105PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-05-playmode.xml" \
    -logFile "${RESULTS_DIR}/G01-05-playmode.log"

printf 'G01-05 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
