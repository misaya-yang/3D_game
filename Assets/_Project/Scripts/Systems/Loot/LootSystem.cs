using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Inventory;
using Wendao.Systems.Feedback;

namespace Wendao.Systems.Loot
{
    public sealed class LootSystem : SafeBehaviour, ILootService
    {
        private readonly HashSet<GameObject> _rewardedVictims =
            new HashSet<GameObject>();
        private static Material _pickupMaterial;

        private System.Random _random;
        private bool _isPrimary;
        private bool _registeredService;

        public int ActiveWorldPickupCount =>
            FindObjectsByType<WorldItemPickup>(FindObjectsInactive.Exclude).Length;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ILootService>(out ILootService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _isPrimary = true;
            _random = new System.Random(Environment.TickCount);
            ServiceLocator.Register<ILootService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (!_isPrimary)
            {
                return;
            }

            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
        }

        private void Update()
        {
            if (!_isPrimary)
            {
                return;
            }

            if (ServiceLocator.TryGet<ILootService>(out ILootService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<ILootService>(this);
            _registeredService = true;
        }

        private void OnDisable()
        {
            if (!_isPrimary)
            {
                return;
            }

            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
        }

        private void OnDestroy()
        {
            _rewardedVictims.Clear();
            if (_registeredService
                && ServiceLocator.TryGet<ILootService>(
                    out ILootService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ILootService>();
            }

            _registeredService = false;
            _isPrimary = false;
        }

        public void ConfigureRandomSeed(int seed)
        {
            _random = new System.Random(seed);
        }

        public void DropLoot(EnemyData data, Vector3 position)
        {
            LootEntry[] entries = data?.LootTable ?? Array.Empty<LootEntry>();
            EnsureRandom();
            for (int index = 0; index < entries.Length; index++)
            {
                LootEntry entry = entries[index];
                if (!IsValidEntry(entry)
                    || _random.NextDouble() >= Mathf.Clamp01(entry.DropChance))
                {
                    continue;
                }

                int minimum = Mathf.Max(1, entry.MinCount);
                int maximum = Mathf.Max(minimum, entry.MaxCount);
                int count = minimum == maximum
                    ? minimum
                    : _random.Next(minimum, maximum + 1);
                if (!TryPutInInventory(entry.ItemId, count))
                {
                    SpawnWorldPickup(entry.ItemId, count, position);
                }
            }

            DropSpiritStones(data);
        }

        public WorldItemPickup SpawnWorldPickup(
            string itemId,
            int count,
            Vector3 position)
        {
            if (string.IsNullOrEmpty(itemId)
                || count <= 0
                || ConfigDatabase.Instance?.GetItem(itemId) == null)
            {
                return null;
            }

            var pickupObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickupObject.name = "WorldLoot_" + itemId;
            pickupObject.transform.position = position + Vector3.up * 0.35f;
            pickupObject.transform.localScale = Vector3.one * 0.35f;
            Renderer renderer = pickupObject.GetComponent<Renderer>();
            Material material = GetPickupMaterial();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            WorldItemPickup pickup = pickupObject.AddComponent<WorldItemPickup>();
            pickup.Configure(itemId, count);
            if (ServiceLocator.TryGet<IVfxService>(out IVfxService vfx))
            {
                vfx.PlayAttached(
                    VfxContentIds.LootDrop,
                    pickupObject.transform,
                    1.5f);
            }

            return pickup;
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            // A real death always supplies Victim. Quest tests may publish a
            // synthetic ID-only event and must not mint combat rewards.
            _rewardedVictims.RemoveWhere(victim => victim == null);
            if (info.Victim == null
                || !_rewardedVictims.Add(info.Victim))
            {
                return;
            }

            EnemyData data = ConfigDatabase.Instance?.GetEnemy(info.EnemyId);
            if (data == null)
            {
                return;
            }

            if (data.CultivationXpReward > 0f
                && ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
            {
                cultivation.AddXp(
                    data.CultivationXpReward,
                    XpSourceType.Combat);
            }

            DropLoot(data, info.Position);
        }

        private static bool IsValidEntry(LootEntry entry)
        {
            return entry != null
                && !string.IsNullOrEmpty(entry.ItemId)
                && entry.MinCount > 0
                && entry.MaxCount >= entry.MinCount
                && entry.DropChance > 0f
                && ConfigDatabase.Instance?.GetItem(entry.ItemId) != null;
        }

        private static bool TryPutInInventory(string itemId, int count)
        {
            return ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                && inventory.AddItem(itemId, count, AcquireSource.Loot);
        }

        private void DropSpiritStones(EnemyData data)
        {
            if (data == null
                || data.MinSpiritStones < 0
                || data.MaxSpiritStones < data.MinSpiritStones
                || data.MaxSpiritStones <= 0
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return;
            }

            int count = data.MinSpiritStones == data.MaxSpiritStones
                ? data.MinSpiritStones
                : _random.Next(
                    data.MinSpiritStones,
                    data.MaxSpiritStones + 1);
            if (count > 0)
            {
                inventory.AddSpiritStones(count);
            }
        }

        private void EnsureRandom()
        {
            if (_random == null)
            {
                _random = new System.Random(Environment.TickCount);
            }
        }

        private static Material GetPickupMaterial()
        {
            if (_pickupMaterial != null)
            {
                return _pickupMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }

            _pickupMaterial = new Material(shader)
            {
                name = "WorldLoot_Greybox_Runtime",
                color = new Color(0.73f, 0.66f, 0.45f, 1f),
                hideFlags = HideFlags.DontSave
            };
            return _pickupMaterial;
        }
    }
}
