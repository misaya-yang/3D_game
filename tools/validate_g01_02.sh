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

assert_json_binding() {
    local action="$1"
    local path="$2"
    local input_asset="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    if ! jq -e --arg action "${action}" --arg path "${path}" \
        '.maps[] | .bindings[] | select(.action == $action and .path == $path)' \
        "${PROJECT_ROOT}/${input_asset}" >/dev/null; then
        printf 'Input action %s is missing binding %s.\n' \
            "${action}" "${path}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_g01_01.sh" --static-only

    local event_params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local player_events="Assets/_Project/Scripts/Systems/Player/PlayerEvents.cs"
    local lock_contract="Assets/_Project/Scripts/Systems/Combat/ILockOnTarget.cs"
    local dialogue_focus_contract="Assets/_Project/Scripts/Systems/NPC/IDialogueFocusTarget.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local targeting="Assets/_Project/Scripts/Entities/Player/PlayerTargetingController.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local enemy="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local dummy="Assets/_Project/Scripts/Entities/Enemy/TrainingDummy.cs"
    local npc="Assets/_Project/Scripts/Entities/NPC/NPCController.cs"
    local camera="Assets/_Project/Scripts/Camera/ThirdPersonCamera.cs"
    local data_tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0102PlayModeTests.cs"

    local required_files=(
        "${event_params}"
        "${input_contract}"
        "${input_reader}"
        "${player_events}"
        "${lock_contract}"
        "${dialogue_focus_contract}"
        "${player}"
        "${player_stats}"
        "${targeting}"
        "${player_bootstrap}"
        "${enemy}"
        "${dummy}"
        "${npc}"
        "${camera}"
        "${data_tests}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_json_binding LockOn '<Mouse>/middleButton'
    assert_json_binding LockOn '<Keyboard>/tab'
    assert_json_binding LockOn '<Gamepad>/rightStickPress'
    assert_pattern 'bool LockOnPressedThisFrame' "${input_contract}"
    assert_pattern '_lockOnAction = _playerMap\.FindAction\("LockOn", true\)' "${input_reader}"

    assert_pattern 'Dodged = "OnPlayerDodged"' "${player_events}"
    assert_pattern 'LockOnChanged = "OnLockOnChanged"' "${player_events}"
    assert_pattern 'public struct PlayerDodgeInfo' "${event_params}"
    assert_pattern 'public struct LockOnInfo' "${event_params}"
    assert_pattern 'PlayerEvents\.Dodged' "${player}"
    assert_pattern 'PlayerEvents\.LockOnChanged' "${targeting}"

    assert_pattern 'public interface ILockOnTarget : IDamageable' "${lock_contract}"
    assert_pattern 'public interface IDialogueFocusTarget' "${dialogue_focus_contract}"
    assert_pattern 'ILockOnTarget,' "${enemy}"
    assert_pattern 'ILockOnTarget,' "${dummy}"
    assert_pattern 'IDialogueFocusTarget' "${npc}"
    assert_pattern 'public float DivineSense' "${player_stats}"

    assert_pattern 'BaseDivineSenseRange = 14f' "${targeting}"
    assert_pattern '_playerController\.RotateTowards\(direction, deltaTime\)' "${targeting}"
    assert_pattern 'internal void RotateTowards\(Vector3 worldDirection, float deltaTime\)' "${player}"
    assert_pattern 'public bool TryCycleLockOn\(\)' "${targeting}"
    assert_pattern 'CollectCandidates\(\)' "${targeting}"
    assert_pattern '_candidates\.Sort\(CompareCandidates\)' "${targeting}"
    assert_pattern 'ValidateCurrentTarget\(\)' "${targeting}"
    assert_pattern 'target\.LockOnDisengageRange' "${targeting}"
    assert_pattern 'PlayerAttackType\.Light' "${targeting}"
    assert_pattern 'AddComponent<PlayerTargetingController>' "${player_bootstrap}"

    assert_pattern 'ExploreFov = 55f' "${camera}"
    assert_pattern 'CombatFov = 65f' "${camera}"
    assert_pattern 'LockOnFov = 60f' "${camera}"
    assert_pattern 'DialogueFov = 45f' "${camera}"
    assert_pattern 'MinimumCollisionDistance = 1\.5f' "${camera}"
    assert_pattern 'OccluderAlpha = 0\.3f' "${camera}"
    assert_pattern 'DodgeShakeIntensity = 0\.3f' "${camera}"
    assert_pattern 'DodgeShakeDuration = 0\.1f' "${camera}"
    assert_pattern 'public void TickCamera\(float deltaTime\)' "${camera}"
    assert_pattern 'Vector3\.Lerp' "${camera}"
    assert_pattern 'HandleLockOnChanged' "${camera}"
    assert_pattern 'HandlePlayerDodged' "${camera}"
    assert_pattern 'HandleDialogueStarted' "${camera}"
    assert_pattern 'ResolveDialogueFocus' "${camera}"
    assert_pattern 'MaterialPropertyBlock' "${camera}"
    assert_pattern 'RestoreAllOccluders' "${camera}"

    assert_pattern 'PlayerDodgeInfo.*Names\("Player", "Direction"\)' "${data_tests}"
    assert_pattern 'LockOnInfo.*Names\("Player", "Target", "Locked"\)' "${data_tests}"
    assert_pattern 'NearestTargetLocksAndRepeatedInputCyclesCandidates' "${tests}"
    assert_pattern 'DeadAndOutOfRangeTargetsClearLockOn' "${tests}"
    assert_pattern 'LockedLightAttackTurnsPlayerTowardTarget' "${tests}"
    assert_pattern 'CameraAppliesCombatLockDialogueFovsAndDodgeShake' "${tests}"
    assert_pattern 'CameraCollisionClampsDistanceAndRestoresOccluderFade' "${tests}"

    assert_pattern '^\| OnPlayerDodged \| PlayerDodgeInfo \| PlayerController \| Camera, VFX, Audio \|$' docs/02_ARCHITECTURE.md
    assert_pattern '^\| OnLockOnChanged \| LockOnInfo \| PlayerTargetingController \| Camera, HUD \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public struct PlayerDodgeInfo' docs/03_DATA_LAYER.md
    assert_pattern 'public struct LockOnInfo' docs/03_DATA_LAYER.md
    assert_pattern '基础范围 14m' docs/03_DATA_LAYER.md
    assert_pattern '基础神识锁定范围为 \*\*14m\*\*' docs/04_PLAYER_COMBAT.md
    assert_pattern 'G01-02 新增消费 LockOn' docs/04_PLAYER_COMBAT.md

    if ! rg -U -q \
        'id: G01-02\nphase: 1\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G01-02 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    if rg -U -q \
        'id: G01-05\nphase: 1\nstatus: (pending|in_progress|implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md" \
        && rg -q 'InputBuffer|BufferedAction|EnqueueBuffered|Hitstop|HitStop|Melt|BurnBurst' \
            "${PROJECT_ROOT}/${targeting}" \
            "${PROJECT_ROOT}/${camera}"; then
        printf 'G01-02 contains later combat-feel or element-reaction behavior.\n' >&2
        exit 1
    fi

    while IFS= read -r implementation; do
        local relative="${implementation#${PROJECT_ROOT}/}"
        assert_pattern 'LockOnPressedThisFrame' "${relative}"
    done < <(rg -l ': IPlayerInputSource' \
        "${PROJECT_ROOT}/Assets/_Project" \
        --glob '*.cs')

    printf 'G01-02 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testFilter Wendao.Tests.EditMode.Data.DataSchemaTests \
    -testResults "${RESULTS_DIR}/G01-02-editmode.xml" \
    -logFile "${RESULTS_DIR}/G01-02-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0102PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/G01-02-playmode.log"

printf 'G01-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
