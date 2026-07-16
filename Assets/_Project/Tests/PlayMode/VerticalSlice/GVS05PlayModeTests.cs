using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Cultivation;
using Wendao.UI.Inventory;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS05PlayModeTests
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
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoGVS05Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (gameManager.State == GameState.MainMenu)
            {
                Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
            }

            if (gameManager.State == GameState.Loading)
            {
                Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            }

            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            TrainingDummyRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
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
        public void CreationUiSelectsFiveElementRootOnceAndRestoresInputOnConfirm()
        {
            ISpiritRootService roots = ServiceLocator.Get<ISpiritRootService>();
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            SpiritRootSelectionView view =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.PickableButtonCount, Is.EqualTo(5));
            Assert.That(input.IsEnabled, Is.False);
            Assert.That(roots.HasChosenRoot, Is.False);
            Assert.That(roots.TryChooseRoot(SpiritRootType.Heaven), Is.False);
            Assert.That(roots.TryChooseRoot(SpiritRootType.Waste), Is.False);

            Assert.That(view.SelectRoot(SpiritRootType.Fire), Is.True);
            Assert.That(roots.HasChosenRoot, Is.False);
            Assert.That(
                view.SelectedIntroLocalizationKey,
                Is.EqualTo(SpiritRootSelectionView.FiveIntroLocalizationKey));
            Assert.That(view.SelectRoot(SpiritRootType.Water), Is.True);
            Assert.That(roots.HasChosenRoot, Is.False);
            Assert.That(SaveManager.Instance.Profile.SpiritRoot, Is.Empty);

            view.ConfirmSelection();
            Assert.That(roots.Root, Is.EqualTo(SpiritRootType.Water));
            Assert.That(roots.GetCultivationMultiplier(), Is.EqualTo(1.1f));
            Assert.That(
                roots.GetElementBonus(ElementType.Water),
                Is.EqualTo(0.15f));
            Assert.That(
                roots.GetIntroDescriptionKey(),
                Is.EqualTo(SpiritRootSelectionView.FiveIntroLocalizationKey));
            Assert.That(SaveManager.Instance.Profile.SpiritRoot, Is.EqualTo("Water"));
            Assert.That(view.IsOpen, Is.False);
            Assert.That(input.IsEnabled, Is.True);
        }

        [Test]
        public void SeededRandomCanProduceHeavenAndWasteWithSpecifiedMultipliersAndCopy()
        {
            ISpiritRootService roots = ServiceLocator.Get<ISpiritRootService>();
            int heavenSeed = FindSeed(
                5f / 5.2f,
                5.05f / 5.2f);
            int wasteSeed = FindSeed(
                5.05f / 5.2f,
                1f);

            Assert.That(roots.TryRandomizeRoot(heavenSeed), Is.True);
            Assert.That(roots.Root, Is.EqualTo(SpiritRootType.Heaven));
            Assert.That(roots.GetCultivationMultiplier(), Is.EqualTo(1.35f));
            Assert.That(roots.GetElementBonus(ElementType.Fire), Is.EqualTo(0.1f));
            Assert.That(
                roots.GetIntroDescriptionKey(),
                Is.EqualTo(SpiritRootSelectionView.HeavenIntroLocalizationKey));
            StringAssert.DoesNotContain(
                "1.35",
                SpiritRootSelectionView.HeavenIntroDefaultValue);
            StringAssert.DoesNotContain(
                "倍",
                SpiritRootSelectionView.HeavenIntroDefaultValue);

            SaveManager.Instance.Profile.SpiritRoot = string.Empty;
            Assert.That(roots.TryRandomizeRoot(wasteSeed), Is.True);
            Assert.That(roots.Root, Is.EqualTo(SpiritRootType.Waste));
            Assert.That(roots.GetCultivationMultiplier(), Is.EqualTo(0.55f));
            Assert.That(roots.GetBodyMultiplier(), Is.EqualTo(1.5f));
            Assert.That(roots.GetBodyPotionMul(), Is.EqualTo(1.25f));
            Assert.That(
                roots.GetIntroDescriptionKey(),
                Is.EqualTo(SpiritRootSelectionView.WasteIntroLocalizationKey));
        }

        [Test]
        public void RandomPreviewCommitsTheExactRareRootOnlyAfterConfirmation()
        {
            ISpiritRootService roots = ServiceLocator.Get<ISpiritRootService>();
            SpiritRootSelectionView view =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            int heavenSeed = FindSeed(5f / 5.2f, 5.05f / 5.2f);

            Assert.That(view.SelectRandom(heavenSeed), Is.True);
            Assert.That(view.SelectedRoot, Is.EqualTo(SpiritRootType.Heaven));
            Assert.That(roots.HasChosenRoot, Is.False);
            Assert.That(SaveManager.Instance.Profile.SpiritRoot, Is.Empty);

            view.ConfirmSelection();
            Assert.That(roots.Root, Is.EqualTo(SpiritRootType.Heaven));
            Assert.That(view.IsOpen, Is.False);
        }

        [Test]
        public void KillingTrainingDummyGrantsCultivationXpThroughCombatEvent()
        {
            SpiritRootSelectionView view =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            Assert.That(view.SelectRoot(SpiritRootType.Fire), Is.True);
            view.ConfirmSelection();

            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            ICombatService combat = ServiceLocator.Get<ICombatService>();
            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();
            TrainingDummy dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(player, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);
            dummy.ConfigureStats(1f, 0f);

            XpGainInfo received = default;
            var eventCount = 0;
            Action<XpGainInfo> handler = info =>
            {
                received = info;
                eventCount++;
            };
            EventBus.Subscribe(CultivationEvents.XpGained, handler);
            try
            {
                combat.DealDamage(
                    dummy,
                    new DamageRequest
                    {
                        Source = player.gameObject,
                        BaseDamage = 100f,
                        Multiplier = 1f,
                        Type = DamageType.Physical,
                        Element = ElementType.None,
                        CanCrit = false,
                        SkillId = string.Empty
                    });
            }
            finally
            {
                EventBus.Unsubscribe(CultivationEvents.XpGained, handler);
            }

            Assert.That(dummy.IsDead, Is.True);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(received.Source, Is.EqualTo(XpSourceType.Combat));
            Assert.That(received.Amount, Is.EqualTo(27.5f).Within(0.001f));
            Assert.That(
                received.MultiplierApplied,
                Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(cultivation.CurrentXp, Is.EqualTo(27.5f).Within(0.001f));
        }

        [Test]
        public void FullXpAdvancesQiSubstageChangesStatsAndRefreshesHud()
        {
            SpiritRootSelectionView view =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            Assert.That(view.SelectRoot(SpiritRootType.Fire), Is.True);
            view.ConfirmSelection();

            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            CultivationHudView hud =
                Object.FindAnyObjectByType<CultivationHudView>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(cultivation.Realm, Is.EqualTo(RealmType.QiCondensation));
            Assert.That(cultivation.SubStage, Is.EqualTo(1));
            Assert.That(stats.MaxHp, Is.EqualTo(100f));
            Assert.That(stats.MaxMana, Is.EqualTo(50f));
            Assert.That(stats.Attack, Is.EqualTo(10f));
            Assert.That(stats.Defense, Is.EqualTo(5f));

            cultivation.AddXp(100f, XpSourceType.Quest);

            Assert.That(cultivation.SubStage, Is.EqualTo(2));
            Assert.That(cultivation.CurrentXp, Is.EqualTo(10f).Within(0.001f));
            Assert.That(cultivation.XpToNext, Is.EqualTo(200f));
            Assert.That(stats.MaxHp, Is.EqualTo(120f));
            Assert.That(stats.MaxMana, Is.EqualTo(60f));
            Assert.That(stats.Attack, Is.EqualTo(12f));
            Assert.That(stats.Defense, Is.EqualTo(6f));
            Assert.That(stats.CurrentHp, Is.EqualTo(120f));
            Assert.That(stats.CurrentMana, Is.EqualTo(60f));
            Assert.That(hud.RealmText, Does.Contain("练气 第2层"));
            Assert.That(hud.XpText, Does.Contain("修为 10/200"));
            Assert.That(hud.FillAmount, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(
                hud.CurrentRealmLocalizationKey,
                Is.EqualTo("realm_name_qi_condensation"));
        }

        [UnityTest]
        public IEnumerator RootAndCultivationProgressRoundTripThroughProfile()
        {
            SpiritRootSelectionView view =
                Object.FindAnyObjectByType<SpiritRootSelectionView>();
            Assert.That(view.SelectRoot(SpiritRootType.Water), Is.True);
            view.ConfirmSelection();

            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            ISpiritRootService roots = ServiceLocator.Get<ISpiritRootService>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            CultivationHudView hud =
                Object.FindAnyObjectByType<CultivationHudView>();
            cultivation.AddXp(100f, XpSourceType.Other);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            SaveManager.Instance.Profile.SpiritRoot = "Fire";
            SaveManager.Instance.Profile.SubStage = 1;
            SaveManager.Instance.Profile.CultivationXp = 0f;
            yield return null;
            Assert.That(stats.MaxHp, Is.EqualTo(100f));

            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            yield return null;

            Assert.That(roots.Root, Is.EqualTo(SpiritRootType.Water));
            Assert.That(cultivation.SubStage, Is.EqualTo(2));
            Assert.That(cultivation.CurrentXp, Is.EqualTo(10f).Within(0.001f));
            Assert.That(stats.MaxHp, Is.EqualTo(120f));
            Assert.That(stats.Attack, Is.EqualTo(12f));
            Assert.That(hud.RealmText, Does.Contain("练气 第2层"));
            Assert.That(hud.XpText, Does.Contain("修为 10/200"));
        }

        private static int FindSeed(double minInclusive, double maxExclusive)
        {
            for (int seed = 0; seed < 100000; seed++)
            {
                double roll = new System.Random(seed).NextDouble();
                if (roll >= minInclusive && roll < maxExclusive)
                {
                    return seed;
                }
            }

            Assert.Fail(
                $"No deterministic seed found for [{minInclusive}, {maxExclusive}).");
            return -1;
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<SpiritRootSelectionView>();
            DestroyAll<CultivationHudView>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<GameToastView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
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
