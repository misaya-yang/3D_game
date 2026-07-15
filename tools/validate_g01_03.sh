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
    "${SCRIPT_DIR}/validate_g01_02.sh" --static-only

    local enums="Assets/_Project/Scripts/Data/Enums/GameEnums.cs"
    local request="Assets/_Project/Scripts/Data/Runtime/DamageRequest.cs"
    local params="Assets/_Project/Scripts/Data/Runtime/EventParams.cs"
    local status_data="Assets/_Project/Scripts/Data/ScriptableObjects/StatusEffectData.cs"
    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local formula="Assets/_Project/Scripts/Data/Config/FormulaLibrary.cs"
    local events="Assets/_Project/Scripts/Systems/Combat/CombatEvents.cs"
    local status_ids="Assets/_Project/Scripts/Systems/Combat/StatusEffectContentIds.cs"
    local status_contract="Assets/_Project/Scripts/Systems/Combat/IStatusEffectService.cs"
    local status_manager="Assets/_Project/Scripts/Systems/Combat/StatusEffectManager.cs"
    local reaction="Assets/_Project/Scripts/Systems/Combat/ElementReactionResolver.cs"
    local teams="Assets/_Project/Scripts/Systems/Combat/ICombatTeamProvider.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local skill="Assets/_Project/Scripts/Systems/Skill/SkillManager.cs"
    local bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local player_skill="Assets/_Project/Scripts/Entities/Player/PlayerSkillController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local enemy="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local dummy="Assets/_Project/Scripts/Entities/Enemy/TrainingDummy.cs"
    local data_tests="Assets/_Project/Tests/EditMode/Data/DataSchemaTests.cs"
    local config_tests="Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/G0103PlayModeTests.cs"

    local required_files=(
        "${enums}" "${request}" "${params}" "${status_data}"
        "${config}" "${formula}" "${events}" "${status_ids}"
        "${status_contract}" "${status_manager}" "${reaction}" "${teams}"
        "${combat}" "${skill}" "${bootstrap}" "${player}"
        "${player_combat}" "${player_skill}" "${player_stats}"
        "${enemy}" "${dummy}" "${data_tests}" "${config_tests}" "${tests}"
    )
    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'public enum ElementReactionType' "${enums}"
    assert_pattern 'Melt' "${enums}"
    assert_pattern 'BurnBurst' "${enums}"
    assert_pattern 'Shock' "${enums}"
    assert_pattern 'Spread' "${enums}"
    assert_pattern 'Sever' "${enums}"
    assert_pattern 'public enum StatusEffectChangeType' "${enums}"
    assert_pattern 'public enum CombatTeam' "${enums}"

    assert_pattern 'public bool IgnoreAttackScaling' "${request}"
    assert_pattern 'public string StatusOnHitId' "${request}"
    assert_pattern 'public float StatusChance' "${request}"
    assert_pattern 'public ElementReactionType Reaction' "${params}"
    assert_pattern 'public float ReactionMultiplier' "${params}"
    assert_pattern 'public struct StatusEffectInfo' "${params}"
    assert_pattern 'public struct ElementReactionInfo' "${params}"

    assert_pattern 'public string DisplayNameKey' "${status_data}"
    assert_pattern 'public ElementType AuraElement' "${status_data}"
    assert_pattern 'public float DotBaseDamageMultiplier' "${status_data}"
    assert_pattern 'public float DamageDealtMod' "${status_data}"
    assert_pattern 'public float ReapplyCooldown' "${status_data}"
    assert_pattern 'public string PromoteAtMaxStacksStatusId' "${status_data}"
    assert_pattern 'GetStatusEffect\(string id\)' "${config}"
    assert_pattern 'RegisterBuiltInStatusEffects\(\)' "${config}"
    assert_pattern '"status_burn"' "${config}"
    assert_pattern '"status_chill"' "${config}"
    assert_pattern '"status_freeze"' "${config}"
    assert_pattern 'dotBaseDamageMultiplier: 0\.05f' "${config}"
    assert_pattern 'reapplyCooldown: 8f' "${config}"

    assert_pattern 'StatusEffectChanged = "OnStatusEffectChanged"' "${events}"
    assert_pattern 'ElementReactionTriggered' "${events}"
    assert_pattern 'public interface IStatusEffectService' "${status_contract}"
    assert_pattern 'public sealed class StatusEffectManager : SafeBehaviour, IStatusEffectService' "${status_manager}"
    assert_pattern 'public bool TryApply' "${status_manager}"
    assert_pattern 'StatusEffectChangeType\.StackChanged' "${status_manager}"
    assert_pattern 'StatusEffectChangeType\.Expired' "${status_manager}"
    assert_pattern 'TickPeriodicDamage' "${status_manager}"
    assert_pattern 'IgnoreAttackScaling = true' "${status_manager}"
    assert_pattern 'TryPromote' "${status_manager}"
    assert_pattern 'CopyAuraStatuses' "${status_manager}"

    assert_pattern 'FormulaLibrary\.MeltMultiplier' "${reaction}"
    assert_pattern 'FormulaLibrary\.BurnBurstMultiplier' "${reaction}"
    assert_pattern 'FormulaLibrary\.ShockMultiplier' "${reaction}"
    assert_pattern 'FormulaLibrary\.SeverMultiplier' "${reaction}"
    assert_pattern 'ElementReactionResolver\.Resolve' "${combat}"
    assert_pattern 'amount \*= Mathf\.Max\(0f, reaction\.DamageMultiplier\)' "${combat}"
    assert_pattern 'CommitReaction' "${combat}"
    assert_pattern 'StatusEffectContentIds\.ShockStun' "${combat}"
    assert_pattern 'StatusEffectContentIds\.SeverDefense' "${combat}"
    assert_pattern 'FormulaLibrary\.SpreadRadius' "${combat}"
    assert_pattern 'CombatEvents\.ElementReactionTriggered' "${combat}"
    assert_pattern 'TryApplyOnHitStatus' "${combat}"

    assert_pattern 'cast\.Skill\.StatusOnHitId' "${skill}"
    assert_pattern 'cast\.Skill\.StatusChance' "${skill}"
    assert_pattern 'EnsureStatusEffectManager\(\)' "${bootstrap}"
    local status_bootstrap_line
    local combat_bootstrap_line
    status_bootstrap_line="$(rg -n '^[[:space:]]+EnsureStatusEffectManager\(\);' \
        "${PROJECT_ROOT}/${bootstrap}" | head -n 1 | cut -d: -f1)"
    combat_bootstrap_line="$(rg -n '^[[:space:]]+EnsureCombatSystem\(\);' \
        "${PROJECT_ROOT}/${bootstrap}" | head -n 1 | cut -d: -f1)"
    if [[ -z "${status_bootstrap_line}" \
        || -z "${combat_bootstrap_line}" \
        || "${status_bootstrap_line}" -ge "${combat_bootstrap_line}" ]]; then
        printf 'StatusEffectManager must bootstrap before CombatSystem.\n' >&2
        exit 1
    fi
    assert_pattern 'ICombatTeamProvider,' "${player_stats}"
    assert_pattern 'ICombatTeamProvider,' "${enemy}"
    assert_pattern 'ICombatTeamProvider,' "${dummy}"
    assert_pattern 'IsStunned\(gameObject\)' "${player}"
    assert_pattern 'IsActionBlockedByStatus' "${player_combat}"
    assert_pattern 'IsSilenced\(gameObject\)' "${player_skill}"
    assert_pattern 'GetMoveSpeedMultiplier\(gameObject\)' "${enemy}"
    assert_pattern 'GetDamageDealtMultiplier\(request\.Source\)' "${combat}"

    assert_pattern 'MeltShockAndBurnBurstUseAuthoritativeMultipliers' "${tests}"
    assert_pattern 'StatusEffectsStackRefreshAndEmitExpiryRemoval' "${tests}"
    assert_pattern 'OnHitBurnUsesFivePercentSkillBasePerTick' "${tests}"
    assert_pattern 'HeartDemonReducesFinalDamageByTenPercent' "${tests}"
    assert_pattern 'ChillPromotesToFreezeAndHonoursEightSecondReapplyCooldown' "${tests}"
    assert_pattern 'WindSpreadsAurasAndMetalAppliesSeverDefenseBreak' "${tests}"
    assert_pattern 'StatusEffectData.*DisplayNameKey' "${data_tests}"
    assert_pattern 'GetStatusEffect\("status_chill"\)' "${config_tests}"

    assert_pattern '^\| OnStatusEffectChanged \| StatusEffectInfo \| StatusEffectManager \| HUD, VFX, 控制状态调试 \|$' docs/02_ARCHITECTURE.md
    assert_pattern '^\| OnElementReactionTriggered \| ElementReactionInfo \| CombatSystem \| G01-05 Camera, 彩色飘字, VFX, Audio \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public struct StatusEffectInfo' docs/03_DATA_LAYER.md
    assert_pattern 'public struct ElementReactionInfo' docs/03_DATA_LAYER.md
    assert_pattern '同一目标同时带多种可反应异常时' docs/04_PLAYER_COMBAT.md
    assert_pattern 'status_wood_mark' docs/04_PLAYER_COMBAT.md
    assert_pattern '^### 4\.1 状态与元素反应 ID（G01-03）$' docs/09_CONTENT.md
    assert_pattern '^\| status_burn \| 灼烧 / status_name_burn \|' docs/09_CONTENT.md
    assert_pattern '^\| reaction_name_melt \| 融化 \|$' docs/09_CONTENT.md

    if ! rg -U -q \
        'id: G01-03\nphase: 1\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G01-03 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    if rg -q 'FOV|Shake|Hitstop|HitStop|InputBuffer|BufferedAction' \
        "${PROJECT_ROOT}/${status_manager}" \
        "${PROJECT_ROOT}/${reaction}"; then
        printf 'G01-03 contains G01-05 combat-feel behavior.\n' >&2
        exit 1
    fi

    printf 'G01-03 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testResults "${RESULTS_DIR}/G01-03-editmode.xml" \
    -logFile "${RESULTS_DIR}/G01-03-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.G0103PlayModeTests \
    -testResults "${RESULTS_DIR}/G01-03-playmode.xml" \
    -logFile "${RESULTS_DIR}/G01-03-playmode.log"

printf 'G01-03 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
