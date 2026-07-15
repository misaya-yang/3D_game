using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Enemy;

namespace Wendao.Entities.Enemy
{
    public sealed class EnemySpawnService : MonoBehaviour, IEnemySpawnService
    {
        public const int MaximumSpawnCount = 20;
        public const float SpawnDistance = 4f;
        public const float SpawnSpacing = 1.75f;

        private bool _registered;
        private int _spawnSequence;

        private void Awake()
        {
            TryRegisterService();
        }

        private void Update()
        {
            if (!_registered)
            {
                TryRegisterService();
            }
        }

        private void OnDestroy()
        {
            if (_registered
                && ServiceLocator.TryGet<IEnemySpawnService>(
                    out IEnemySpawnService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IEnemySpawnService>();
            }

            _registered = false;
        }

        public bool TrySpawn(string enemyId, int count, out int spawnedCount)
        {
            spawnedCount = 0;
            EnemyData data = ConfigDatabase.Instance?.GetEnemy(enemyId);
            Scene scene = gameObject.scene;
            if (data == null
                || count < 1
                || count > MaximumSpawnCount
                || !scene.IsValid()
                || !scene.isLoaded)
            {
                return false;
            }

            PlayerController player = FindAnyObjectByType<PlayerController>();
            Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 origin = player != null
                ? player.transform.position + forward * SpawnDistance
                : transform.position + forward * SpawnDistance;
            for (int index = 0; index < count; index++)
            {
                float centeredIndex = index - (count - 1) * 0.5f;
                Vector3 position = origin + right * (centeredIndex * SpawnSpacing);
                _spawnSequence++;
                EnemyBrain brain = EnemySpawner.CreateRuntimeEnemy(
                    data,
                    position,
                    scene,
                    null,
                    $"Enemy_Debug_{enemyId}_{_spawnSequence:000}");
                if (brain == null)
                {
                    break;
                }

                spawnedCount++;
            }

            return spawnedCount == count;
        }

        public bool TryDefeatAll(out int defeatedCount)
        {
            defeatedCount = 0;
            if (!ServiceLocator.TryGet<ICombatService>(out ICombatService combat))
            {
                return false;
            }

            PlayerController player = FindAnyObjectByType<PlayerController>();
            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyBrain enemy = enemies[index];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                combat.DealDamage(
                    enemy,
                    new DamageRequest
                    {
                        Source = player != null ? player.gameObject : gameObject,
                        BaseDamage = enemy.CurrentHp + enemy.MaxHp + 1f,
                        Multiplier = 1f,
                        Type = DamageType.True,
                        Element = ElementType.None,
                        CanCrit = false,
                        SkillId = string.Empty
                    });
                if (enemy.IsDead)
                {
                    defeatedCount++;
                }
            }

            return true;
        }

        private bool TryRegisterService()
        {
            if (_registered)
            {
                _registered = ServiceLocator.TryGet<IEnemySpawnService>(
                        out IEnemySpawnService current)
                    && ReferenceEquals(current, this);
                if (_registered)
                {
                    return true;
                }
            }

            if (ServiceLocator.TryGet<IEnemySpawnService>(
                    out IEnemySpawnService existing))
            {
                _registered = ReferenceEquals(existing, this);
                return _registered;
            }

            ServiceLocator.Register<IEnemySpawnService>(this);
            _registered = true;
            return true;
        }
    }
}
