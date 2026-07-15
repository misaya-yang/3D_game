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
    "${SCRIPT_DIR}/validate_gvs_06.sh" --static-only

    local config="Assets/_Project/Scripts/Data/Config/ConfigDatabase.cs"
    local enemy_data="Assets/_Project/Scripts/Data/ScriptableObjects/EnemyData.cs"
    local item_data="Assets/_Project/Scripts/Data/ScriptableObjects/ItemData.cs"
    local enemy_ids="Assets/_Project/Scripts/Systems/Enemy/EnemyContentIds.cs"
    local inventory_events="Assets/_Project/Scripts/Systems/Inventory/InventoryEvents.cs"
    local enemy_brain="Assets/_Project/Scripts/Entities/Enemy/EnemyBrain.cs"
    local enemy_spawner="Assets/_Project/Scripts/Entities/Enemy/EnemySpawner.cs"
    local wolf_bootstrap="Assets/_Project/Scripts/Entities/Enemy/WolfRuntimeBootstrap.cs"
    local loot_contract="Assets/_Project/Scripts/Systems/Loot/ILootService.cs"
    local loot_system="Assets/_Project/Scripts/Systems/Loot/LootSystem.cs"
    local world_pickup="Assets/_Project/Scripts/Systems/Loot/WorldItemPickup.cs"
    local player_bootstrap="Assets/_Project/Scripts/Entities/Player/PlayerRuntimeBootstrap.cs"
    local scene_bootstrap="Assets/_Project/Scripts/Systems/World/SceneFlowBootstrap.cs"
    local config_tests="Assets/_Project/Tests/EditMode/Data/ConfigDatabaseTests.cs"
    local tests="Assets/_Project/Tests/PlayMode/VerticalSlice/GVS07PlayModeTests.cs"

    local required_files=(
        "${config}"
        "${enemy_data}"
        "${item_data}"
        "${enemy_ids}"
        "${inventory_events}"
        "${enemy_brain}"
        "${enemy_spawner}"
        "${wolf_bootstrap}"
        "${loot_contract}"
        "${loot_system}"
        "${world_pickup}"
        "${player_bootstrap}"
        "${scene_bootstrap}"
        "${config_tests}"
        "${tests}"
    )

    local file_path
    for file_path in "${required_files[@]}"; do
        assert_file "${file_path}"
    done

    assert_pattern 'GreyWolf = "enemy_wolf_gray"' "${enemy_ids}"
    assert_pattern 'WolfHair = "item_mat_wolf_hair"' "${inventory_events}"
    assert_pattern 'public EnemyData GetEnemy\(string id\)' "${config}"
    assert_pattern 'public bool RegisterEnemy\(EnemyData enemy\)' "${config}"
    assert_pattern 'Resources.LoadAll<EnemyData>\("SO/Enemies"\)' "${config}"
    assert_pattern 'greyWolf.Id = "enemy_wolf_gray"' "${config}"
    assert_pattern 'greyWolf.MaxHp = 80f' "${config}"
    assert_pattern 'greyWolf.Attack = 8f' "${config}"
    assert_pattern 'greyWolf.MoveSpeed = 3.2f' "${config}"
    assert_pattern 'greyWolf.AggroRange = 8f' "${config}"
    assert_pattern 'greyWolf.AttackRange = 1.6f' "${config}"
    assert_pattern 'greyWolf.DisengageRange = 14f' "${config}"
    assert_pattern 'greyWolf.AttackInterval = 1.2f' "${config}"
    assert_pattern 'greyWolf.CultivationXpReward = 15f' "${config}"
    assert_pattern 'DropChance = 0.4f' "${config}"
    assert_pattern 'wolfHair.MaxStack = 99' "${config}"

    assert_pattern 'public enum EnemyBrainState' "${enemy_brain}"
    assert_pattern 'Idle,' "${enemy_brain}"
    assert_pattern 'Chase,' "${enemy_brain}"
    assert_pattern 'Attack,' "${enemy_brain}"
    assert_pattern 'Return,' "${enemy_brain}"
    assert_pattern 'public sealed class EnemyBrain : SafeBehaviour' "${enemy_brain}"
    assert_pattern '"enemy_name_" \+ Data.Id' "${enemy_brain}"
    assert_pattern 'IDamageable' "${enemy_brain}"
    assert_pattern 'ICombatDeathHandler' "${enemy_brain}"
    assert_pattern 'public void SpawnInit\(EnemyData data, Vector3 position\)' "${enemy_brain}"
    assert_pattern 'public void TickAI\(float deltaTime\)' "${enemy_brain}"
    assert_pattern 'public void OnAggro\(GameObject target\)' "${enemy_brain}"
    assert_pattern '_controller.Move\(displacement\)' "${enemy_brain}"
    assert_pattern '_combatService.DealDamage' "${enemy_brain}"
    assert_pattern 'State = EnemyBrainState.Return' "${enemy_brain}"
    assert_pattern 'CurrentHp = MaxHp' "${enemy_brain}"
    assert_pattern 'ReturnTeleportSeconds = 3f' "${enemy_brain}"
    assert_pattern 'CombatEvents.EnemyKilled' "${enemy_brain}"
    assert_pattern 'CombatEvents.PlayerDied' "${enemy_brain}"

    assert_pattern 'public sealed class EnemySpawner : MonoBehaviour' "${enemy_spawner}"
    assert_pattern 'public int MaxAlive = 3' "${enemy_spawner}"
    assert_pattern 'public float RespawnSeconds = 8f' "${enemy_spawner}"
    assert_pattern 'SimulationDistance = 80f' "${enemy_spawner}"
    assert_pattern 'public void SpawnAllNow\(\)' "${enemy_spawner}"
    assert_pattern 'SpawnSlotEnemy\(slot, index\)' "${enemy_spawner}"
    assert_pattern 'public static class WolfRuntimeBootstrap' "${wolf_bootstrap}"
    assert_pattern 'new Vector3\(10f, 0f, 8f\)' "${wolf_bootstrap}"
    assert_pattern 'EnemyContentIds.GreyWolf' "${wolf_bootstrap}"
    assert_pattern 'WolfRuntimeBootstrap.Install\(\)' "${player_bootstrap}"

    assert_pattern 'public interface ILootService' "${loot_contract}"
    assert_pattern 'void DropLoot\(EnemyData data, Vector3 position\)' "${loot_contract}"
    assert_pattern 'public sealed class LootSystem : SafeBehaviour, ILootService' "${loot_system}"
    assert_pattern 'CombatEvents.EnemyKilled' "${loot_system}"
    assert_pattern 'info.Victim == null' "${loot_system}"
    assert_pattern '_rewardedVictims.Add' "${loot_system}"
    assert_pattern 'data.CultivationXpReward' "${loot_system}"
    assert_pattern 'AcquireSource.Loot' "${loot_system}"
    assert_pattern 'SpawnWorldPickup' "${loot_system}"
    assert_pattern 'public sealed class WorldItemPickup : SafeBehaviour' "${world_pickup}"
    assert_pattern 'DefaultLifetimeSeconds = 180f' "${world_pickup}"
    assert_pattern 'public bool TryCollect\(\)' "${world_pickup}"
    assert_pattern 'AddComponent<LootSystem>' "${scene_bootstrap}"

    if rg -U -q \
        'id: G04-01\nphase: 4\nstatus: pending' \
        "${PROJECT_ROOT}/docs/10_GOALS.md" \
        && rg -q 'NavMesh|Patrol|Alert|BossPhase|SkillIds' \
            "${PROJECT_ROOT}/${enemy_brain}" \
            "${PROJECT_ROOT}/${enemy_spawner}" \
            "${PROJECT_ROOT}/${wolf_bootstrap}"; then
        printf 'G-VS-07 contains out-of-scope full AI, elite skill, or boss logic.\n' >&2
        exit 1
    fi

    assert_pattern 'GetEnemy\("enemy_wolf_gray"\)' "${config_tests}"
    assert_pattern 'G-VS-07 代码优先闭环' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern '不提前引入 G04-01 的 Patrol、Alert、NavMesh' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'ID-only 模拟事件只用于任务计数' docs/07_WORLD_ENEMY_QUEST.md
    assert_pattern 'G-VS-07 的代码优先灰狼占位参数' docs/09_CONTENT.md
    assert_pattern '^\| enemy_name_enemy_wolf_gray \| 灰狼 \|$' docs/09_CONTENT.md
    assert_pattern '^\| item_name_item_mat_wolf_hair \| 狼毫 \|$' docs/09_CONTENT.md
    assert_pattern 'public EnemyData GetEnemy\(string id\);' docs/03_DATA_LAYER.md
    assert_pattern 'InventoryManager / LootSystem / ItemUseSystem' docs/02_ARCHITECTURE.md
    if ! rg -U -q \
        'id: G-VS-07\nphase: VS\nstatus: (implemented|done)' \
        "${PROJECT_ROOT}/docs/10_GOALS.md"; then
        printf 'G-VS-07 must be marked implemented after static delivery.\n' >&2
        exit 1
    fi

    assert_pattern 'GreyWolfContentSpawnerAndLocalizationContractMatchSpec' "${tests}"
    assert_pattern 'WolfChasesAttacksAndReturnsHomeAtFullHealth' "${tests}"
    assert_pattern 'KillingRealWolfAdvancesQuestGrantsXpAndDropsWolfHairOnce' "${tests}"
    assert_pattern 'FullInventoryFallsBackToCollectibleWorldPickup' "${tests}"
    assert_pattern 'SpawnerHonorsMaxAliveAndRespawnsDeadWolf' "${tests}"

    printf 'G-VS-07 static validation passed. Unity compilation and PlayMode acceptance remain pending.\n'
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
    -testResults "${RESULTS_DIR}/GVS-07-editmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-07-editmode.log"

"${UNITY_EDITOR}" \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -runTests \
    -testPlatform PlayMode \
    -testFilter Wendao.Tests.PlayMode.VerticalSlice.GVS07PlayModeTests \
    -testResults "${RESULTS_DIR}/GVS-07-playmode.xml" \
    -logFile "${RESULTS_DIR}/GVS-07-playmode.log"

printf 'G-VS-07 Unity validation passed. Results are in %s.\n' "${RESULTS_DIR}"
