using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Achievement;
using Wendao.Systems.Faction;
using Wendao.Systems.Inventory;
using Wendao.Systems.Shop;
using Wendao.Systems.Title;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0604PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private ConfigDatabase _database;
        private InventoryManager _inventory;
        private PlayerStats _stats;
        private FactionManager _faction;
        private TitleManager _titles;
        private AchievementManager _achievements;
        private ShopSystem _shop;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0604Tests_" + Guid.NewGuid().ToString("N"));
            CreateRuntime();
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);
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
        public void BuiltInDatabaseContainsTenAchievementsAndFiveTitles()
        {
            Assert.That(_database.Achievements.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(_database.Titles.Count, Is.GreaterThanOrEqualTo(5));
            string[] expectedAchievements =
            {
                AchievementContentIds.FirstKill,
                AchievementContentIds.Kill100,
                AchievementContentIds.Kill1000,
                AchievementContentIds.RealmFoundation,
                AchievementContentIds.RealmGoldenCore,
                AchievementContentIds.QuestChapterOne,
                AchievementContentIds.Alchemy10,
                AchievementContentIds.SecretChest,
                AchievementContentIds.StoneGeneral,
                AchievementContentIds.WasteBody
            };
            for (int index = 0; index < expectedAchievements.Length; index++)
            {
                Assert.That(
                    _database.GetAchievement(expectedAchievements[index]),
                    Is.Not.Null,
                    expectedAchievements[index]);
            }
        }

        [Test]
        public void DandingRanksApplyExpectedShopDiscounts()
        {
            Assert.That(_faction.Join(FactionContentIds.Danding), Is.True);
            Assert.That(_faction.GetRank(FactionContentIds.Danding), Is.Zero);
            Assert.That(
                _shop.GetBuyPrice(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.IronSword),
                Is.EqualTo(80));

            _faction.AddRep(FactionContentIds.Danding, 100);
            Assert.That(_faction.GetRank(FactionContentIds.Danding), Is.EqualTo(1));
            Assert.That(
                _faction.GetShopDiscount(FactionContentIds.Danding),
                Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(
                _shop.GetBuyPrice(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.IronSword),
                Is.EqualTo(76));

            _faction.AddRep(FactionContentIds.Danding, 1900);
            Assert.That(_faction.GetRank(FactionContentIds.Danding), Is.EqualTo(5));
            Assert.That(
                _shop.GetBuyPrice(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.IronSword),
                Is.EqualTo(60));
        }

        [Test]
        public void TenAchievementsUnlockRewardsAndTitlesModifyPlayerStats()
        {
            float baseAttack = _stats.Attack;
            float baseMaxHp = _stats.MaxHp;
            int potionBefore = _inventory.CountItem(
                InventoryContentIds.HealPotion01);

            _achievements.OnTrigger("KillTotal", string.Empty, 1f);
            Assert.That(
                _achievements.IsUnlocked(AchievementContentIds.FirstKill),
                Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(potionBefore + 5));

            _achievements.OnTrigger("KillTotal", string.Empty, 999f);
            _achievements.OnTrigger("RealmReached", "Foundation", 1f);
            _achievements.OnTrigger("RealmReached", "GoldenCore", 1f);
            _achievements.OnTrigger(
                "QuestCompleted",
                "quest_main_01_10",
                1f);
            _achievements.OnTrigger("CraftCount", string.Empty, 10f);
            _achievements.OnTrigger(
                "Flag",
                "flag_serendipity_cangwu_cliff_box",
                1f);
            _achievements.OnTrigger(
                "KillEnemy",
                "enemy_boss_stone_general",
                1f);
            _achievements.OnTrigger(
                "BodyLevel+Root",
                "Waste&Copper",
                1f);

            Assert.That(_achievements.UnlockedIds.Count, Is.EqualTo(10));
            Assert.That(
                _faction.GetRep(FactionContentIds.Danding),
                Is.EqualTo(AchievementManager.AlchemyReputationReward));
            Assert.That(_titles.UnlockedTitleIds.Count, Is.EqualTo(5));

            Assert.That(_titles.Equip(TitleContentIds.Yaotu), Is.True);
            Assert.That(_stats.Attack, Is.EqualTo(baseAttack + 5f));
            Assert.That(_stats.CritRate, Is.EqualTo(0.01f).Within(0.0001f));

            Assert.That(_titles.Equip(TitleContentIds.Tiegu), Is.True);
            Assert.That(_titles.ActiveMaxHpPercent, Is.EqualTo(0.05f));
            Assert.That(_stats.MaxHp, Is.EqualTo(baseMaxHp * 1.05f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator AchievementTitleAndFactionSaveRoundTrip()
        {
            _faction.AddRep(FactionContentIds.Danding, 600);
            _achievements.OnTrigger("KillTotal", string.Empty, 100f);
            Assert.That(_titles.Equip(TitleContentIds.YaotuApprentice), Is.True);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            string slot = Path.Combine(_storageRoot, "SaveSlot_0");
            Assert.That(
                File.Exists(Path.Combine(slot, AchievementManager.SaveModuleName + ".json")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(slot, TitleManager.SaveModuleName + ".json")),
                Is.True);

            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            CreateRuntime();
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            yield return null;

            Assert.That(
                _faction.GetRep(FactionContentIds.Danding),
                Is.EqualTo(600));
            Assert.That(_faction.GetRank(FactionContentIds.Danding), Is.EqualTo(3));
            Assert.That(
                _achievements.IsUnlocked(AchievementContentIds.Kill100),
                Is.True);
            Assert.That(
                _achievements.GetProgress(AchievementContentIds.Kill100),
                Is.EqualTo(100f));
            Assert.That(
                _titles.ActiveTitleId,
                Is.EqualTo(TitleContentIds.YaotuApprentice));
            Assert.That(_stats.Attack, Is.EqualTo(12f).Within(0.001f));
        }

        private void CreateRuntime()
        {
            _save = new GameObject("[G0604Save]").AddComponent<SaveManager>();
            _save.ConfigureStorageRoot(_storageRoot);
            _database = new GameObject("[G0604Config]")
                .AddComponent<ConfigDatabase>();
            _database.LoadAll();
            _inventory = new GameObject("[G0604Inventory]")
                .AddComponent<InventoryManager>();

            var player = new GameObject("[G0604Player]");
            player.AddComponent<PlayerController>();
            _stats = player.AddComponent<PlayerStats>();

            _faction = new GameObject("[G0604Faction]")
                .AddComponent<FactionManager>();
            _titles = new GameObject("[G0604Titles]")
                .AddComponent<TitleManager>();
            _achievements = new GameObject("[G0604Achievements]")
                .AddComponent<AchievementManager>();
            _shop = new GameObject("[G0604Shop]").AddComponent<ShopSystem>();
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<AchievementManager>();
            DestroyAll<TitleManager>();
            DestroyAll<FactionManager>();
            DestroyAll<ShopSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<PlayerStats>();
            DestroyAll<SaveManager>();
            DestroyAll<GameManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }
    }
}
