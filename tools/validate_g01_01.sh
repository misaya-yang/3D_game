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
    "${SCRIPT_DIR}/validate_gvs_08.sh" --static-only

    local enums="Assets/_Project/Scripts/Data/Enums/GameEnums.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local defense_contract="Assets/_Project/Scripts/Systems/Combat/ICombatDefenseProvider.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local input_asset="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0101PlayModeTests.cs"

    local required_files=(
        "${enums}"
        "${input_contract}"
        "${input_reader}"
        "${player}"
        "${player_combat}"
        "${player_stats}"
        "${defense_contract}"
        "${combat}"
        "${input_asset}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'bool HeavyAttackPressedThisFrame' "${input_contract}"
    assert_pattern 'bool DodgePressedThisFrame' "${input_contract}"
    assert_pattern 'bool BlockHeld' "${input_contract}"
    assert_pattern 'FindAction\("HeavyAttack", true\)' "${input_reader}"
    assert_pattern 'FindAction\("Dodge", true\)' "${input_reader}"
    assert_pattern 'FindAction\("Block", true\)' "${input_reader}"
    assert_pattern '"action": "HeavyAttack"' "${input_asset}"
    assert_pattern '"action": "Dodge"' "${input_asset}"
    assert_pattern '"action": "Block"' "${input_asset}"

    assert_pattern 'HeavyAttack,' "${enums}"
    assert_pattern 'Dodge,' "${enums}"
    assert_pattern 'Block,' "${enums}"
    assert_pattern 'BlockHit,' "${enums}"
    assert_pattern 'Stagger,' "${enums}"

    assert_pattern 'DodgeDistance = 5f' "${player}"
    assert_pattern 'DodgeDuration = 0\.35f' "${player}"
    assert_pattern 'DodgeInvincibilityDuration = 0\.2f' "${player}"
    assert_pattern 'DodgeCooldown = 0\.8f' "${player}"
    assert_pattern 'BlockMoveSpeedMultiplier = 0\.4f' "${player}"
    assert_pattern 'public bool TryStartDodge\(Vector3 direction\)' "${player}"
    assert_pattern 'public void TickDodgeState\(float deltaTime\)' "${player}"
    assert_pattern 'public bool TryStartBlock\(\)' "${player}"
    assert_pattern 'public void NotifyBlockHit\(\)' "${player}"

    assert_pattern 'MaximumLightComboStep = 4' "${player_combat}"
    assert_pattern 'ComboWindow = 0\.35f' "${player_combat}"
    assert_pattern 'Light1Multiplier = 1f' "${player_combat}"
    assert_pattern 'Light2Multiplier = 1\.1f' "${player_combat}"
    assert_pattern 'Light3Multiplier = 1\.25f' "${player_combat}"
    assert_pattern 'Light4Multiplier = 1\.5f' "${player_combat}"
    assert_pattern 'HeavyAttackMultiplier = 2f' "${player_combat}"
    assert_pattern 'public bool TryQueueNextLightAttack\(\)' "${player_combat}"
    assert_pattern 'public bool TryStartHeavyAttack\(\)' "${player_combat}"
    assert_pattern 'public bool TryCancelRecoveryIntoDodge\(Vector3 direction\)' "${player_combat}"
    assert_pattern 'Multiplier = CurrentMultiplier' "${player_combat}"

    assert_pattern 'public interface ICombatDefenseProvider' "${defense_contract}"
    assert_pattern 'PhysicalBlockDamageReduction = 0\.6f' "${player_stats}"
    assert_pattern 'ElementalBlockDamageReduction = 0\.3f' "${player_stats}"
    assert_pattern 'ICombatDefenseProvider,' "${player_stats}"
    assert_pattern 'public float GetBlockDamageReduction' "${player_stats}"
    assert_pattern 'targetDefense\.IsInvincible' "${combat}"
    assert_pattern 'targetDefense\.GetBlockDamageReduction' "${combat}"
    assert_pattern 'amount > 0f' "${combat}"

    assert_pattern 'FourHitComboUsesSpecifiedMultipliersAndTimings' "${tests}"
    assert_pattern 'ComboWindowExpiresAndRestartsAtLightOne' "${tests}"
    assert_pattern 'HeavyAttackUsesSpecifiedMultiplierWindupAndRecovery' "${tests}"
    assert_pattern 'DodgeHasPointTwoSecondInvincibilityFiveMeterTravelAndCooldown' "${tests}"
    assert_pattern 'BlockReducesPhysicalAndElementalButNotTrueDamage' "${tests}"
    assert_pattern 'AttackWindupCannotCancelButRecoveryCanCancelIntoDodge' "${tests}"

    assert_pattern 'ICombatDefenseProvider.GetBlockDamageReduction' docs/04_PLAYER_COMBAT.md
    assert_pattern 'G01-01 在该最小管道上接入四段轻击' docs/04_PLAYER_COMBAT.md
    assert_pattern 'HeavyAttack, Dodge, Block, BlockHit, Stagger' docs/03_DATA_LAYER.md

    if ! rg -U -q \
        'id: G01-01\nphase: 1\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G01-01 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    if rg -U -q \
        'id: G01-05\nphase: 1\nstatus: (pending|in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md" \
        && rg -q 'InputBuffer|BufferedAction|EnqueueBuffered|Hitstop|HitStop' \
            "${PROJECT_ROOT}/${player}" \
            "${PROJECT_ROOT}/${player_combat}" \
            "${PROJECT_ROOT}/${combat}"; then
        printf 'G01-01 contains G01-05 out-of-scope buffering or hitstop behavior.\n' >&2
        exit 1
    fi

    while IFS= read -r implementation; do
        local relative="${implementation#${PROJECT_ROOT}/}"
        assert_pattern 'HeavyAttackPressedThisFrame' "${relative}"
        assert_pattern 'DodgePressedThisFrame' "${relative}"
        assert_pattern 'BlockHeld' "${relative}"
    done < <(rg -l ': IPlayerInputSource' \
        "${PROJECT_ROOT}/Assets/_Project" \
        --glob '*.cs')

    printf 'G01-01 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0101PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-01-playmode.xml" \
    -logFile "${RESULTS_DIR}/G01-01-playmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS02PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-01-gvs02-regression.xml" \
    -logFile "${RESULTS_DIR}/G01-01-gvs02-regression.log"

printf 'G01-01 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
