using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Enemy;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0404PlayModeTests
    {
        private string _storageRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0404Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void EnemyLootTablesExposeIndependentItemAndCurrencyRanges()
        {
            EnemyData wolf = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.GreyWolf);
            EnemyData elite = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.EliteWolf);
            EnemyData spawn = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.BlackwindSpawn);
            EnemyData boss = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.StoneGeneral);

            Assert.That(wolf.LootTable, Has.Length.EqualTo(1));
            Assert.That(wolf.LootTable[0].ItemId,
                Is.EqualTo(InventoryContentIds.WolfHair));
            Assert.That(wolf.LootTable[0].DropChance, Is.EqualTo(0.4f));
            Assert.That(wolf.MinSpiritStones, Is.EqualTo(0));
            Assert.That(wolf.MaxSpiritStones, Is.EqualTo(2));

            Assert.That(elite.LootTable, Has.Length.EqualTo(1));
            Assert.That(elite.LootTable[0].ItemId,
                Is.EqualTo(InventoryContentIds.BeastCore01));
            Assert.That(elite.LootTable[0].DropChance, Is.EqualTo(1f));
            Assert.That(elite.MinSpiritStones, Is.EqualTo(8));
            Assert.That(elite.MaxSpiritStones, Is.EqualTo(15));

            Assert.That(spawn.MinSpiritStones, Is.EqualTo(0));
            Assert.That(spawn.MaxSpiritStones, Is.EqualTo(2));
            Assert.That(boss.MinSpiritStones, Is.EqualTo(0));
            Assert.That(boss.MaxSpiritStones, Is.EqualTo(0));
        }

        [Test]
        public void LootEntriesRespectDropChanceAndInclusiveCountRange()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            LootSystem loot = Object.FindAnyObjectByType<LootSystem>();
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            try
            {
                data.LootTable = new[]
                {
                    new LootEntry
                    {
                        ItemId = InventoryContentIds.WolfHair,
                        MinCount = 2,
                        MaxCount = 4,
                        DropChance = 0f
                    }
                };
                loot.ConfigureRandomSeed(17);
                loot.DropLoot(data, Vector3.zero);
                Assert.That(
                    inventory.CountItem(InventoryContentIds.WolfHair),
                    Is.EqualTo(0));

                data.LootTable[0].DropChance = 1f;
                loot.ConfigureRandomSeed(17);
                loot.DropLoot(data, Vector3.zero);
                Assert.That(
                    inventory.CountItem(InventoryContentIds.WolfHair),
                    Is.InRange(2, 4));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void SpiritStoneLootBypassesFullInventoryAndUpdatesProfile()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            LootSystem loot = Object.FindAnyObjectByType<LootSystem>();
            Assert.That(
                inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    InventoryManager.Capacity,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(inventory.Slots, Has.Count.EqualTo(InventoryManager.Capacity));

            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            try
            {
                data.LootTable = Array.Empty<LootEntry>();
                data.MinSpiritStones = 12;
                data.MaxSpiritStones = 12;
                loot.DropLoot(data, Vector3.zero);

                Assert.That(inventory.SpiritStones, Is.EqualTo(12));
                Assert.That(SaveManager.Instance.Profile.SpiritStones,
                    Is.EqualTo(12));
                Assert.That(loot.ActiveWorldPickupCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void EnemyDeathGrantsEliteLootAndCurrencyOnlyOnce()
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            LootSystem loot = Object.FindAnyObjectByType<LootSystem>();
            loot.ConfigureRandomSeed(23);
            var victim = new GameObject("EliteLootVictim");
            var info = new EnemyDeathInfo
            {
                EnemyId = EnemyContentIds.EliteWolf,
                Rank = EnemyRank.Elite,
                Victim = victim,
                Position = Vector3.zero
            };

            EventBus.Publish(CombatEvents.EnemyKilled, info);
            int firstBalance = inventory.SpiritStones;
            Assert.That(
                inventory.CountItem(InventoryContentIds.BeastCore01),
                Is.EqualTo(1));
            Assert.That(firstBalance, Is.InRange(8, 15));

            EventBus.Publish(CombatEvents.EnemyKilled, info);
            Assert.That(
                inventory.CountItem(InventoryContentIds.BeastCore01),
                Is.EqualTo(1));
            Assert.That(inventory.SpiritStones, Is.EqualTo(firstBalance));
            Object.DestroyImmediate(victim);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<Wendao.Entities.Player.PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<Wendao.CameraSystem.ThirdPersonCamera>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<LootSystem>();
            DestroyAll<BodyRefinementManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<Wendao.Systems.Inventory.ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include);
            foreach (T instance in instances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
        }
    }
}
