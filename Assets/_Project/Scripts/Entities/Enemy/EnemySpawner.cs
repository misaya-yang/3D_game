using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Enemy;

namespace Wendao.Entities.Enemy
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        public const float CorpseVisibleSeconds = 1f;
        public const float SimulationDistance = 80f;

        [Serializable]
        private sealed class SpawnSlot
        {
            public Vector3 LocalOffset;
            public EnemyBrain Brain;
            public bool DeathObserved;
            public float DespawnAt;
            public float RespawnAt;
        }

        private readonly List<SpawnSlot> _slots = new List<SpawnSlot>();
        private static Material _greyboxMaterial;
        private static Material _eliteGreyboxMaterial;
        private static Material _bossGreyboxMaterial;

        public string EnemyId = EnemyContentIds.GreyWolf;
        [Min(1)] public int MaxAlive = 3;
        [Min(0f)] public float RespawnSeconds = 8f;
        [Min(0f)] public float CorpseSeconds = CorpseVisibleSeconds;
        public Vector3[] SpawnOffsets =
        {
            new Vector3(-1.5f, 0f, -1f),
            new Vector3(1.2f, 0f, -0.2f),
            new Vector3(0f, 0f, 1.5f)
        };
        public Vector3[] PatrolOffsets =
        {
            Vector3.zero,
            new Vector3(2f, 0f, 0f),
            new Vector3(1.5f, 0f, 2f),
            new Vector3(-1f, 0f, 1.5f)
        };

        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _slots.Count; index++)
                {
                    if (_slots[index].Brain != null
                        && !_slots[index].Brain.IsDead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int SpawnedCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _slots.Count; index++)
                {
                    if (_slots[index].Brain != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            RebuildSlots();
        }

        private void Start()
        {
            SpawnAllNow();
        }

        private void Update()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            if (!IsPlayerWithinSimulationDistance())
            {
                return;
            }

            float now = Time.time;
            for (int index = 0; index < _slots.Count; index++)
            {
                SpawnSlot slot = _slots[index];
                if (slot.Brain == null)
                {
                    if (now >= slot.RespawnAt)
                    {
                        SpawnSlotEnemy(slot, index);
                    }

                    continue;
                }

                if (!slot.Brain.IsDead)
                {
                    continue;
                }

                if (!slot.DeathObserved)
                {
                    slot.DeathObserved = true;
                    slot.DespawnAt = now + Mathf.Max(0f, CorpseSeconds);
                    slot.RespawnAt = now + Mathf.Max(0f, RespawnSeconds);
                }

                if (now < slot.DespawnAt)
                {
                    continue;
                }

                Destroy(slot.Brain.gameObject);
                slot.Brain = null;
            }
        }

        public void Configure(
            string enemyId,
            int maxAlive,
            float respawnSeconds,
            Vector3[] spawnOffsets)
        {
            Configure(
                enemyId,
                maxAlive,
                respawnSeconds,
                spawnOffsets,
                PatrolOffsets);
        }

        public void Configure(
            string enemyId,
            int maxAlive,
            float respawnSeconds,
            Vector3[] spawnOffsets,
            Vector3[] patrolOffsets)
        {
            EnemyId = enemyId ?? string.Empty;
            MaxAlive = Mathf.Max(1, maxAlive);
            RespawnSeconds = Mathf.Max(0f, respawnSeconds);
            SpawnOffsets = spawnOffsets == null
                ? Array.Empty<Vector3>()
                : (Vector3[])spawnOffsets.Clone();
            PatrolOffsets = patrolOffsets == null
                ? Array.Empty<Vector3>()
                : (Vector3[])patrolOffsets.Clone();
            ClearSpawnedEnemies();
            RebuildSlots();
        }

        public void SetPatrolOffsets(Vector3[] patrolOffsets)
        {
            PatrolOffsets = patrolOffsets == null
                ? Array.Empty<Vector3>()
                : (Vector3[])patrolOffsets.Clone();
            for (int index = 0; index < _slots.Count; index++)
            {
                EnemyBrain brain = _slots[index].Brain;
                if (brain != null)
                {
                    brain.ConfigurePatrolRoute(
                        BuildWorldPatrolRoute(brain.SpawnPosition));
                }
            }
        }

        public void SpawnAllNow()
        {
            for (int index = 0; index < _slots.Count; index++)
            {
                SpawnSlot slot = _slots[index];
                if (slot.Brain == null)
                {
                    SpawnSlotEnemy(slot, index);
                }
            }
        }

        private void RebuildSlots()
        {
            _slots.Clear();
            int count = Mathf.Max(1, MaxAlive);
            for (int index = 0; index < count; index++)
            {
                _slots.Add(
                    new SpawnSlot
                    {
                        LocalOffset = ResolveOffset(index),
                        RespawnAt = 0f
                    });
            }
        }

        private Vector3 ResolveOffset(int index)
        {
            if (SpawnOffsets != null && index < SpawnOffsets.Length)
            {
                return SpawnOffsets[index];
            }

            float angle = index * Mathf.PI * 2f / Mathf.Max(1, MaxAlive);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
        }

        private void SpawnSlotEnemy(SpawnSlot slot, int slotIndex)
        {
            EnemyData data = ConfigDatabase.Instance?.GetEnemy(EnemyId);
            if (data == null)
            {
                return;
            }

            Vector3 position = transform.TransformPoint(slot.LocalOffset);
            EnemyBrain brain = CreateRuntimeEnemy(
                data,
                position,
                gameObject.scene,
                transform,
                $"Enemy_{EnemyId}_{slotIndex + 1:00}");
            if (brain == null)
            {
                return;
            }

            brain.ConfigurePatrolRoute(BuildWorldPatrolRoute(brain.SpawnPosition));
            slot.Brain = brain;
            slot.DeathObserved = false;
            slot.DespawnAt = 0f;
            slot.RespawnAt = 0f;
        }

        public static EnemyBrain CreateRuntimeEnemy(
            EnemyData data,
            Vector3 position,
            Scene scene,
            Transform parent,
            string objectName)
        {
            if (data == null || !scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject enemyObject = data.Prefab != null
                ? UnityEngine.Object.Instantiate(
                    data.Prefab,
                    position,
                    Quaternion.identity)
                : CreateGreyboxEnemy(data, position);
            enemyObject.name = string.IsNullOrWhiteSpace(objectName)
                ? $"Enemy_{data.Id}"
                : objectName;
            SceneManager.MoveGameObjectToScene(enemyObject, scene);
            if (parent != null)
            {
                enemyObject.transform.SetParent(parent, true);
            }

            CharacterController controller =
                enemyObject.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = enemyObject.AddComponent<CharacterController>();
            }
            ConfigureController(controller, data.Rank);

            EnemyBrain brain = enemyObject.GetComponent<EnemyBrain>();
            if (brain == null)
            {
                brain = enemyObject.AddComponent<EnemyBrain>();
            }

            brain.SpawnInit(data, position);
            return brain;
        }

        private Vector3[] BuildWorldPatrolRoute(Vector3 spawnPosition)
        {
            if (PatrolOffsets == null || PatrolOffsets.Length == 0)
            {
                return Array.Empty<Vector3>();
            }

            var points = new Vector3[PatrolOffsets.Length];
            for (int index = 0; index < PatrolOffsets.Length; index++)
            {
                points[index] = spawnPosition
                    + transform.TransformVector(PatrolOffsets[index]);
            }

            return points;
        }

        private static GameObject CreateGreyboxEnemy(
            EnemyData data,
            Vector3 position)
        {
            var root = new GameObject("Enemy_Greybox");
            root.transform.position = position;
            CharacterController controller = root.AddComponent<CharacterController>();
            bool elite = data != null && data.Rank == EnemyRank.Elite;
            bool boss = data != null && data.Rank == EnemyRank.Boss;
            ConfigureController(controller, data?.Rank ?? EnemyRank.Normal);

            GameObject visual = GameObject.CreatePrimitive(
                boss ? PrimitiveType.Cube : PrimitiveType.Capsule);
            visual.name = boss
                ? "StoneGeneralVisual_Greybox"
                : elite
                    ? "EliteWolfVisual_Greybox"
                    : "WolfVisual_Greybox";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = boss
                ? new Vector3(0f, 1.6f, 0f)
                : new Vector3(0f, 0.65f, 0f);
            visual.transform.localRotation = boss
                ? Quaternion.identity
                : Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = boss
                ? new Vector3(1.8f, 3.2f, 1.5f)
                : elite
                    ? new Vector3(0.8f, 1.08f, 0.7f)
                    : new Vector3(0.65f, 0.9f, 0.55f);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            Material material = GetGreyboxMaterial(elite, boss);
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return root;
        }

        private static Material GetGreyboxMaterial(bool elite, bool boss)
        {
            Material cached = boss
                ? _bossGreyboxMaterial
                : elite
                    ? _eliteGreyboxMaterial
                    : _greyboxMaterial;
            if (cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }

            Material created = new Material(shader)
            {
                name = boss
                    ? "Enemy_StoneGeneral_Greybox_Runtime"
                    : elite
                        ? "Enemy_EliteWolf_Greybox_Runtime"
                        : "Enemy_GreyWolf_Greybox_Runtime",
                color = boss
                    ? new Color(0.18f, 0.2f, 0.24f, 1f)
                    : elite
                        ? new Color(0.42f, 0.18f, 0.14f, 1f)
                        : new Color(0.34f, 0.37f, 0.4f, 1f),
                hideFlags = HideFlags.DontSave
            };
            if (boss)
            {
                _bossGreyboxMaterial = created;
            }
            else if (elite)
            {
                _eliteGreyboxMaterial = created;
            }
            else
            {
                _greyboxMaterial = created;
            }

            return created;
        }

        private static void ConfigureController(
            CharacterController controller,
            EnemyRank rank = EnemyRank.Normal)
        {
            bool boss = rank == EnemyRank.Boss;
            bool elite = rank == EnemyRank.Elite;
            controller.height = boss ? 3.2f : elite ? 1.5f : 1.2f;
            controller.radius = boss ? 0.9f : elite ? 0.52f : 0.45f;
            controller.center = new Vector3(
                0f,
                controller.height * 0.5f,
                0f);
            controller.stepOffset = boss ? 0.4f : 0.25f;
            controller.slopeLimit = 45f;
        }

        private bool IsPlayerWithinSimulationDistance()
        {
            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            return player == null
                || Vector3.SqrMagnitude(
                    player.transform.position - transform.position)
                    <= SimulationDistance * SimulationDistance;
        }

        private void ClearSpawnedEnemies()
        {
            for (int index = 0; index < _slots.Count; index++)
            {
                if (_slots[index].Brain != null)
                {
                    Destroy(_slots[index].Brain.gameObject);
                }
            }

            _slots.Clear();
        }
    }
}
