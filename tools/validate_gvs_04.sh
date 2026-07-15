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
    local input_actions="Assets/_Project/Resources/Input/PlayerInputActions.inputactions"
    if ! jq -e --arg action "${action}" --arg path "${path}" \
        '.maps[] | .bindings[] | select(.action == $action and .path == $path)' \
        "${PROJECT_ROOT}/${input_actions}" >/dev/null; then
        printf 'Input action %s is missing binding %s.\n' "${action}" "${path}" >&2
        exit 1
    fi
}

run_static_validation() {
    "${SCRIPT_DIR}/validate_gvs_03.sh" --static-only

    local enum_file="Assets/_Project/Scripts/Data/Enums/GameEnums.cs"
    local runtime_data="Assets/_Project/Scripts/Data/Runtime/SkillRuntimeData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local player_combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local player_skill="Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local skill_contract="Assets/_Project/Scripts/Systems/Skill/ISkillService.cs"
    local caster_contract="Assets/_Project/Scripts/Systems/Skill/IPlayerSkillCaster.cs"
    local resource_contract="Assets/_Project/Scripts/Systems/Skill/IPlayerResourceService.cs"
    local skill_events="Assets/_Project/Scripts/Systems/Skill/SkillEvents.cs"
    local skill_manager="Assets/_Project/Scripts/Systems/Skill/SkillManager.cs"
    local projectile="Assets/_Project/Scripts/Systems/Skill/SkillProjectile.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local quickbar="Assets/_Project/Scripts/Systems/UI/Skill/SkillQuickbarView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS04PlayModeTests.cs"

    local required_files=(
        "${enum_file}"
        "${runtime_data}"
        "${config}"
        "${input_contract}"
        "${input_reader}"
        "${player}"
        "${player_stats}"
        "${player_combat}"
        "${player_skill}"
        "${player_bootstrap}"
        "${skill_contract}"
        "${caster_contract}"
        "${resource_contract}"
        "${skill_events}"
        "${skill_manager}"
        "${projectile}"
        "${scene_bootstrap}"
        "${quickbar}"
        "${ui_bootstrap}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'LightAttack,' "${enum_file}"
    assert_pattern 'SkillCast,' "${enum_file}"
    assert_pattern 'public sealed class SkillRuntime' "${runtime_data}"
    assert_pattern 'public float CooldownRemaining;' "${runtime_data}"
    assert_pattern 'public sealed class SkillSaveData' "${runtime_data}"
    assert_pattern 'public string\[\] EquippedIds = new string\[4\]' "${runtime_data}"

    assert_pattern 'skill_basic_qi_bolt' "${config}"
    assert_pattern 'RegisterBuiltInSkill' "${config}"
    assert_pattern 'VFX_Skill_QiBolt_Projectile' "${config}"
    assert_pattern 'VFX_Skill_QiBolt_Impact' "${config}"

    assert_json_binding Skill1 '<Keyboard>/1'
    assert_json_binding Skill1 '<Gamepad>/dpad/up'
    assert_pattern 'bool Skill1PressedThisFrame' "${input_contract}"
    assert_pattern '_skill1Action = _playerMap\.FindAction\("Skill1", true\)' "${input_reader}"
    assert_pattern '_skill1Action\.WasPressedThisFrame\(\)' "${input_reader}"

    assert_pattern 'IPlayerResourceService' "${player_stats}"
    assert_pattern '_maxMana = 50f' "${player_stats}"
    assert_pattern 'public float CurrentMana' "${player_stats}"
    assert_pattern 'public bool TrySpendMana' "${player_stats}"
    assert_pattern 'PlayerState\.SkillCast' "${player}"
    assert_pattern 'PlayerState\.SkillCast' "${player_combat}"
    assert_pattern 'public sealed class PlayerSkillController' "${player_skill}"
    assert_pattern 'Skill1PressedThisFrame' "${player_skill}"
    assert_pattern 'ForceState\(PlayerState\.SkillCast\)' "${player_skill}"
    assert_pattern 'AddComponent<PlayerSkillController>' "${player_bootstrap}"

    assert_pattern 'public interface ISkillService' "${skill_contract}"
    assert_pattern 'public interface IPlayerSkillCaster' "${caster_contract}"
    assert_pattern 'public interface IPlayerResourceService' "${resource_contract}"
    assert_pattern 'SkillLearned = "OnSkillLearned"' "${skill_events}"
    assert_pattern 'SkillCast = "OnSkillCast"' "${skill_events}"
    assert_pattern 'public sealed class SkillManager : SafeBehaviour, ISkillService' "${skill_manager}"
    assert_pattern 'SaveModuleName = "skills"' "${skill_manager}"
    assert_pattern 'ResetStarterLoadout' "${skill_manager}"
    assert_pattern 'public bool CanCast\(int barIndex\)' "${skill_manager}"
    assert_pattern 'public bool TryCast\(' "${skill_manager}"
    assert_pattern 'TrySpendMana' "${skill_manager}"
    assert_pattern 'SkillProjectile\.Spawn' "${skill_manager}"
    assert_pattern 'SkillEvents\.SkillCast' "${skill_manager}"
    assert_pattern 'ManaInsufficientToastKey = "ui_skill_mana_insufficient"' "${skill_manager}"
    assert_pattern 'public bool TryUpgrade\(string skillId\)' "${skill_manager}"

    assert_pattern 'public sealed class SkillProjectile : MonoBehaviour' "${projectile}"
    assert_pattern 'Physics\.SphereCastNonAlloc' "${projectile}"
    assert_pattern '_combatService\.DealDamage' "${projectile}"
    assert_pattern 'DefaultSpeed = 14f' "${projectile}"

    assert_pattern 'AddComponent<SkillManager>' "${scene_bootstrap}"
    assert_pattern 'public sealed class SkillQuickbarView : MonoBehaviour' "${quickbar}"
    assert_pattern 'ui_skill_cooldown' "${quickbar}"
    assert_pattern 'ui_player_mana' "${quickbar}"
    assert_pattern 'GetCooldownRemaining\(index\)' "${quickbar}"
    assert_pattern 'AddComponent<SkillQuickbarView>' "${ui_bootstrap}"

    assert_pattern 'public enum PlayerState .*SkillCast, Dead' docs/03_DATA_LAYER.md
    assert_pattern 'public sealed class SkillSaveData' docs/03_DATA_LAYER.md
    assert_pattern 'G-VS-04 新增消费 Skill1' docs/04_PLAYER_COMBAT.md
    assert_pattern 'BaseDamage `30`、ManaCost `10`、Cooldown `1\.5s`' docs/06_ITEMS_EQUIP_SKILL.md
    assert_pattern '^\| skill_basic_qi_bolt \| 30 \| 5 \| 10 \| 1\.5s \| 0\.2s \| 0\.3s \| 12m \| true \| — \|$' docs/09_CONTENT.md
    assert_pattern '^\| ui_skill_mana_insufficient \|' docs/09_CONTENT.md

    assert_pattern 'QiBoltContentInputDefaultLoadoutAndQuickbarMatchContract' "${tests}"
    assert_pattern 'PressingSkillOneDealsDamageConsumesManaAndStartsCooldown' "${tests}"
    assert_pattern 'InsufficientManaRejectsCastWithoutStateOrCooldownAndShowsToast' "${tests}"
    assert_pattern 'SkillLoadoutAndCooldownRoundTripThroughSkillsModule' "${tests}"

    printf 'G-VS-04 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testFilter Wendao.Tests.EditMode \
    -testResults "${RESULTS_DIR}/GVS-04-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-04-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS04PlayModeTests \
    -testResults "${RESULTS_DIR}/GVS-04-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-04-playmode.log"

printf 'G-VS-04 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
