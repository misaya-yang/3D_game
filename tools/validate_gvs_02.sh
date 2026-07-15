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
    "${SCRIPT_DIR}/validate_gvs_01.sh" --static-only

    local damage_request="Assets/_Project/Scripts/Data/Runtime/DamageRequest.cs"
    local enum_file="Assets/_Project/Scripts/Data/Enums/GameEnums.cs"
    local input_contract="Assets/_Project/Scripts/Systems/Input/IPlayerInputSource.cs"
    local input_reader="Assets/_Project/Scripts/Entities/Player/PlayerInputReader.cs"
    local damageable="Assets/_Project/Scripts/Systems/Combat/IDamageable.cs"
    local stat_provider="Assets/_Project/Scripts/Systems/Combat/ICombatStatsProvider.cs"
    local death_handler="Assets/_Project/Scripts/Systems/Combat/ICombatDeathHandler.cs"
    local combat_contract="Assets/_Project/Scripts/Systems/Combat/ICombatService.cs"
    local combat_events="Assets/_Project/Scripts/Systems/Combat/CombatEvents.cs"
    local combat="Assets/_Project/Scripts/Systems/Combat/CombatSystem.cs"
    local player="Assets/_Project/Scripts/Entities/Player/PlayerController.cs"
    local player_stats="Assets/_Project/Scripts/Entities/Player/PlayerStats.cs"
    local player_combat="Assets/_Project/Scripts/Entities/Player/PlayerCombatController.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local dummy="Assets/_Project/Scripts/Entities/Enemy/TrainingDummy.cs"
    local dummy_bootstrap="Assets/_Project/Scripts/Entities/Enemy/TrainingDummyRuntimeBootstrap.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local damage_view="Assets/_Project/Scripts/Systems/UI/Combat/DamageFloatingTextView.cs"
    local ui_bootstrap="Assets/_Project/Scripts/Systems/UI/SceneFlow/SceneUiBootstrap.cs"
    local tutorial="Assets/_Project/Scripts/Systems/Tutorial/TutorialManager.cs"
    local prefab="Assets/_Project/Resources/Prefabs/Player/Player_Greybox.prefab"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS02PlayModeTests.cs"

    local required_files=(
        "${damage_request}"
        "${enum_file}"
        "${input_contract}"
        "${input_reader}"
        "${damageable}"
        "${stat_provider}"
        "${death_handler}"
        "${combat_contract}"
        "${combat_events}"
        "${combat}"
        "${player}"
        "${player_stats}"
        "${player_stats}.meta"
        "${player_combat}"
        "${player_combat}.meta"
        "${player_bootstrap}"
        "${dummy}"
        "${dummy_bootstrap}"
        "${scene_bootstrap}"
        "${damage_view}"
        "${ui_bootstrap}"
        "${tutorial}"
        "${prefab}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_json_binding LightAttack '<Mouse>/leftButton'
    assert_json_binding LightAttack '<Gamepad>/rightTrigger'
    assert_pattern 'bool LightAttackPressedThisFrame' "${input_contract}"
    assert_pattern '_lightAttackAction = _playerMap\.FindAction\("LightAttack", true\)' "${input_reader}"
    assert_pattern '_lightAttackAction\.WasPressedThisFrame\(\)' "${input_reader}"

    assert_pattern 'LightAttack,' "${enum_file}"
    assert_pattern 'Dead' "${enum_file}"
    assert_pattern 'public struct DamageRequest' "${damage_request}"
    assert_pattern 'public float BaseDamage;' "${damage_request}"
    assert_pattern 'public float Multiplier;' "${damage_request}"
    assert_pattern 'public string SkillId;' "${damage_request}"

    assert_pattern 'public interface IDamageable' "${damageable}"
    assert_pattern 'float CurrentHp \{ get; \}' "${damageable}"
    assert_pattern 'float MaxHp \{ get; \}' "${damageable}"
    assert_pattern 'bool IsDead \{ get; \}' "${damageable}"
    assert_pattern 'void ApplyDamage\(DamageInfo info\)' "${damageable}"
    assert_pattern 'void ApplyHeal\(float amount, string sourceId\)' "${damageable}"
    assert_pattern 'public interface ICombatStatsProvider' "${stat_provider}"
    assert_pattern 'public interface ICombatDeathHandler' "${death_handler}"
    assert_pattern 'public interface ICombatService' "${combat_contract}"

    assert_pattern 'public sealed class CombatSystem : SafeBehaviour, ICombatService' "${combat}"
    assert_pattern 'ServiceLocator\.Register<ICombatService>' "${combat}"
    assert_pattern 'public DamageInfo ComputeDamage\(DamageRequest request, IDamageable target\)' "${combat}"
    assert_pattern '1f \+ attack / 100f' "${combat}"
    assert_pattern '100f / \(100f \+ defense\)' "${combat}"
    assert_pattern 'request\.Type != DamageType\.True' "${combat}"
    assert_pattern 'Mathf\.Max\(1f, amount\)' "${combat}"
    assert_pattern 'Physics\.OverlapSphereNonAlloc' "${combat}"
    assert_pattern 'EventBus\.Publish\(CombatEvents\.DamageApplied, info\)' "${combat}"
    assert_pattern 'deathHandler\.HandleDeath\(info\)' "${combat}"
    assert_pattern 'public void RegisterActor\(IDamageable actor\)' "${combat}"
    assert_pattern 'public void UnregisterActor\(IDamageable actor\)' "${combat}"

    assert_pattern 'ICombatDeathHandler' "${player_stats}"
    assert_pattern '_maxHp = 100f' "${player_stats}"
    assert_pattern '_attack = 10f' "${player_stats}"
    assert_pattern 'private float _critRate;' "${player_stats}"
    assert_pattern 'CombatEvents\.PlayerDamaged' "${player_stats}"
    assert_pattern 'CombatEvents\.PlayerDied' "${player_stats}"
    assert_pattern 'PlayerState\.Dead' "${player_stats}"
    assert_pattern 'gameManager\.TrySetState\(GameState\.Dead\)' "${player_stats}"

    assert_pattern 'public sealed class PlayerCombatController : SafeBehaviour' "${player_combat}"
    assert_pattern 'LightAttackWindup = 0\.1f' "${player_combat}"
    assert_pattern 'LightAttackRecovery = 0\.25f' "${player_combat}"
    assert_pattern '_baseDamage = 10f' "${player_combat}"
    assert_pattern '_combatService\.TryMeleeHit' "${player_combat}"
    assert_pattern 'Light1Multiplier = 1f' "${player_combat}"
    assert_pattern 'Multiplier = CurrentMultiplier' "${player_combat}"
    assert_pattern 'PlayerState\.LightAttack' "${player_combat}"
    assert_pattern 'State == PlayerState\.Dead' "${player}"
    assert_pattern 'State == PlayerState\.LightAttack' "${player}"

    assert_pattern 'TrainingDummyRuntimeBootstrap\.Install\(\)' "${player_bootstrap}"
    assert_pattern 'playerObject\.AddComponent<PlayerStats>\(\)' "${player_bootstrap}"
    assert_pattern 'playerObject\.AddComponent<PlayerCombatController>\(\)' "${player_bootstrap}"
    assert_pattern 'ICombatDeathHandler' "${dummy}"
    assert_pattern 'DefaultMaxHp = 30f' "${dummy}"
    assert_pattern 'CombatEvents\.EnemyKilled' "${dummy}"
    assert_pattern 'CombatContentIds\.TrainingDummyEnemyId' "${dummy}"
    assert_pattern 'new Vector3\(0f, 1f, 2\.2f\)' "${dummy_bootstrap}"
    assert_pattern 'new GameObject\("\[CombatSystem\]"\)\.AddComponent<CombatSystem>\(\)' "${scene_bootstrap}"

    assert_pattern 'public sealed class DamageFloatingTextView : MonoBehaviour' "${damage_view}"
    assert_pattern 'CombatEvents\.DamageApplied' "${damage_view}"
    assert_pattern 'Mathf\.CeilToInt\(info\.Amount\)' "${damage_view}"
    assert_pattern 'ActiveNumberCount' "${damage_view}"
    assert_pattern 'AddComponent<DamageFloatingTextView>\(\)' "${ui_bootstrap}"

    assert_pattern 'CombatTutorialId = "tut_combat"' "${tutorial}"
    assert_pattern 'CombatLightStepId = "light_attack"' "${tutorial}"
    assert_pattern 'CombatDefeatStepId = "defeat_dummy"' "${tutorial}"
    assert_pattern 'CombatEvents\.EnemyKilled' "${tutorial}"
    assert_pattern 'CombatContentIds\.TrainingDummyEnemyId' "${tutorial}"
    assert_pattern 'HasCompleted\(CombatTutorialId\)' "${tutorial}"
    assert_pattern 'public void RepublishActivePrompt\(\)' "${tutorial}"
    assert_pattern 'tutorialService\.RepublishActivePrompt\(\)' "${ui_bootstrap}"

    assert_pattern '^\| OnDamageApplied \| DamageInfo \| CombatSystem \| 伤害飘字, VFX, Audio \|$' docs/02_ARCHITECTURE.md
    assert_pattern 'public struct DamageRequest' docs/03_DATA_LAYER.md
    assert_pattern 'public enum PlayerState .*LightAttack' docs/03_DATA_LAYER.md
    assert_pattern '^\| BaseDamage \| 10 \|$' docs/04_PLAYER_COMBAT.md
    assert_pattern '^\| 判定距离 \| 2\.5 m \|$' docs/04_PLAYER_COMBAT.md
    assert_pattern '^\| 判定扇形 \| 100° \|$' docs/04_PLAYER_COMBAT.md
    assert_pattern '^\| enemy_training_dummy \| 练功木桩 \| Normal \| — \| 30 \| 0 \| 25 \| 0 \| G-VS-05：击杀得原始修为 25，无掉落 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_combat_light_attack \| 按鼠标左键或手柄右扳机进行轻击。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_combat_defeat_dummy \| 击破前方木桩，完成战斗练习。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tutorial_combat_complete \| 基础战斗已掌握。 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tut_combat \| 首次进入战斗 \|$' docs/09_CONTENT.md
    assert_pattern '^\| tut_combat \| 首次进战 \| G-VS-02：轻击→击破木桩 \| 是 \|$' docs/08_UI_META.md

    assert_pattern '^guid: d384a23aa7334f99bfebdcd7de6737a7$' "${player_stats}.meta"
    assert_pattern '^guid: 5c29438cec8c4c17afae531485ea8593$' "${player_combat}.meta"
    assert_pattern 'guid: d384a23aa7334f99bfebdcd7de6737a7, type: 3' "${prefab}"
    assert_pattern 'guid: 5c29438cec8c4c17afae531485ea8593, type: 3' "${prefab}"

    assert_pattern 'LightAttackInputSupportsMouseAndGamepad' "${tests}"
    assert_pattern 'DamagePipelineAppliesAttackMultiplierDefenseAndTrueDamage' "${tests}"
    assert_pattern 'LightAttackKillsDummyShowsDamageAndPersistsCombatTutorial' "${tests}"
    assert_pattern 'LethalPlayerDamagePublishesEventsAndEntersDeadState' "${tests}"
    assert_pattern 'reloadedSave\.LoadGame\(0\)' "${tests}"

    printf 'G-VS-02 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testResults "${RESULTS_DIR}/GVS-02-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-02-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode \
    -testResults "${RESULTS_DIR}/GVS-02-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-02-playmode.log"

printf 'G-VS-02 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
