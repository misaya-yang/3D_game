using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;

namespace Wendao.Entities.Enemy
{
    public sealed class BlackwindDungeonEncounterController : MonoBehaviour
    {
        public const int FirstFloorWaveSize = 2;
        public const int FirstFloorWaveCount = 2;
        public const int FourthFloorEnemyCount = 4;

        private readonly Dictionary<int, List<EnemyBrain>> _spawned =
            new Dictionary<int, List<EnemyBrain>>();
        private readonly Dictionary<int, int> _kills =
            new Dictionary<int, int>();
        private readonly HashSet<int> _spawnedFloors = new HashSet<int>();
        private Scene _scene;
        private bool _configured;

        public int ActiveWave { get; private set; }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Subscribe<BlackwindFloorInfo>(
                BlackwindDungeonEvents.FloorEntered,
                HandleFloorEntered);
            EventBus.Subscribe<BlackwindRunInfo>(
                BlackwindDungeonEvents.RunReset,
                HandleRunReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<BlackwindFloorInfo>(
                BlackwindDungeonEvents.FloorEntered,
                HandleFloorEntered);
            EventBus.Unsubscribe<BlackwindRunInfo>(
                BlackwindDungeonEvents.RunReset,
                HandleRunReset);
        }

        public void Configure(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            _scene = scene;
            _configured = true;
            if (ServiceLocator.TryGet<IBlackwindDungeonService>(
                out IBlackwindDungeonService dungeon))
            {
                SpawnFloor(dungeon.CurrentFloor);
            }
        }

        public int GetSpawnedCount(int floor)
        {
            if (!_spawned.TryGetValue(floor, out List<EnemyBrain> enemies))
            {
                return 0;
            }

            int count = 0;
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasSpawnedFloor(int floor)
        {
            return _spawnedFloors.Contains(floor);
        }

        public void SpawnFloor(int floor)
        {
            if (!_configured
                || floor < 1
                || floor > 5
                || _spawnedFloors.Contains(floor))
            {
                return;
            }

            _spawnedFloors.Add(floor);
            _kills[floor] = 0;
            switch (floor)
            {
                case 1:
                    ActiveWave = 1;
                    SpawnBatch(
                        floor,
                        EnemyContentIds.BlackwindSpawn,
                        FirstFloorWaveSize,
                        BlackwindDungeonFactory.GetFloorCenter(1));
                    break;
                case 2:
                    ActiveWave = 1;
                    SpawnBatch(
                        floor,
                        EnemyContentIds.EliteWolf,
                        1,
                        BlackwindDungeonFactory.GetFloorCenter(2));
                    break;
                case 3:
                    ActiveWave = 0;
                    break;
                case 4:
                    ActiveWave = 1;
                    SpawnBatch(
                        floor,
                        EnemyContentIds.BlackwindSpawn,
                        FourthFloorEnemyCount,
                        BlackwindDungeonFactory.GetFloorCenter(4));
                    break;
                case 5:
                    ActiveWave = 1;
                    SpawnBoss();
                    break;
            }
        }

        private void SpawnBatch(
            int floor,
            string enemyId,
            int count,
            Vector3 center)
        {
            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / Mathf.Max(1, count);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * 2.5f,
                    0f,
                    Mathf.Sin(angle) * 2.5f + 1f);
                SpawnEnemy(
                    floor,
                    enemyId,
                    center + offset,
                    $"Blackwind_B{floor}_{enemyId}_{ActiveWave}_{index + 1}");
            }
        }

        private EnemyBrain SpawnEnemy(
            int floor,
            string enemyId,
            Vector3 position,
            string objectName)
        {
            EnemyData data = ConfigDatabase.Instance?.GetEnemy(enemyId);
            EnemyBrain enemy = EnemySpawner.CreateRuntimeEnemy(
                data,
                position,
                _scene,
                transform,
                objectName);
            if (enemy == null)
            {
                return null;
            }

            if (!_spawned.TryGetValue(floor, out List<EnemyBrain> enemies))
            {
                enemies = new List<EnemyBrain>();
                _spawned[floor] = enemies;
            }

            enemies.Add(enemy);
            return enemy;
        }

        private void SpawnBoss()
        {
            Vector3 center = BlackwindDungeonFactory.GetFloorCenter(5)
                + Vector3.forward;
            EnemyBrain boss = SpawnEnemy(
                5,
                EnemyContentIds.StoneGeneral,
                center,
                "Boss_StoneGeneral_Blackwind");
            GameObject arenaObject = GameObject.Find(
                BlackwindDungeonFactory.BossArenaName);
            if (boss == null || arenaObject == null)
            {
                return;
            }

            BossArenaController arena = arenaObject.GetComponent<BossArenaController>()
                ?? arenaObject.AddComponent<BossArenaController>();
            arena.Configure(
                boss,
                center,
                BossArenaController.DefaultArenaRadius);
        }

        private void HandleFloorEntered(BlackwindFloorInfo info)
        {
            SpawnFloor(info.Floor);
        }

        private void HandleRunReset(BlackwindRunInfo info)
        {
            ClearEncounters();
            SpawnFloor(info.StartFloor);
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            if (!_configured
                || info.Victim == null
                || info.Victim.scene != _scene
                || !TryFindFloor(info.Victim, out int floor))
            {
                return;
            }

            _kills[floor] = _kills.TryGetValue(floor, out int current)
                ? current + 1
                : 1;
            if (!ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon))
            {
                return;
            }

            if (floor == 1 && _kills[floor] == FirstFloorWaveSize)
            {
                ActiveWave = 2;
                SpawnBatch(
                    1,
                    EnemyContentIds.BlackwindSpawn,
                    FirstFloorWaveSize,
                    BlackwindDungeonFactory.GetFloorCenter(1));
            }
            else if (floor == 1
                && _kills[floor] == FirstFloorWaveSize * FirstFloorWaveCount)
            {
                dungeon.NotifyCombatObjectiveCleared(1);
            }
            else if (floor == 2 && _kills[floor] == 1)
            {
                dungeon.NotifyCombatObjectiveCleared(2);
            }
            else if (floor == 4 && _kills[floor] == FourthFloorEnemyCount)
            {
                dungeon.NotifyCombatObjectiveCleared(4);
            }
            else if (floor == 5
                && info.EnemyId == EnemyContentIds.StoneGeneral)
            {
                dungeon.NotifyBossDefeated();
            }
        }

        private bool TryFindFloor(GameObject victim, out int floor)
        {
            foreach (KeyValuePair<int, List<EnemyBrain>> pair in _spawned)
            {
                foreach (EnemyBrain enemy in pair.Value)
                {
                    if (enemy != null && enemy.gameObject == victim)
                    {
                        floor = pair.Key;
                        return true;
                    }
                }
            }

            floor = 0;
            return false;
        }

        private void ClearEncounters()
        {
            foreach (List<EnemyBrain> enemies in _spawned.Values)
            {
                foreach (EnemyBrain enemy in enemies)
                {
                    if (enemy != null)
                    {
                        Destroy(enemy.gameObject);
                    }
                }
            }

            _spawned.Clear();
            _kills.Clear();
            _spawnedFloors.Clear();
            ActiveWave = 0;
        }
    }
}
