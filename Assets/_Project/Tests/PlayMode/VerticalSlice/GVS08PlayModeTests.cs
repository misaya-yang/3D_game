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
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Debugging;
using Wendao.Systems.Enemy;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Cultivation;
using Wendao.UI.Debugging;
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS08PlayModeTests
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
                "WendaoGVS08Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
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
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            EnemySpawner spawner = WolfRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            Assert.That(spawner, Is.Not.Null);
            spawner.SpawnAllNow();
            yield return null;

            Assert.That(
                ServiceLocator.TryGet<IDebugConsoleService>(out _),
                Is.True);
            UnlockHuntQuest();
            Assert.That(
                ServiceLocator.TryGet<IEnemySpawnService>(out _),
                Is.True);
            Assert.That(
                Object.FindAnyObjectByType<DebugConsoleView>(),
                Is.Not.Null);
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
        public void CommonDevelopmentCommandsExecuteThroughServiceBoundaries()
        {
            IDebugConsoleService console =
                ServiceLocator.Get<IDebugConsoleService>();
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            IDebugPlayerService playerDebug =
                ServiceLocator.Get<IDebugPlayerService>();
            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();

            DebugCommandResult help = console.Execute("/help");
            AssertSuccess(help, DebugConsoleService.HelpLocalizationKey);
            StringAssert.Contains("/give", help.DefaultValue);
            StringAssert.Contains("/spawn", help.DefaultValue);
            StringAssert.Contains("/tutorial_skip", help.DefaultValue);

            DebugCommandResult give = console.Execute(
                "/give item_potion_heal_01 2");
            AssertSuccess(give, DebugConsoleService.GiveSuccessLocalizationKey);
            Assert.That(
                inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(2));

            float xpBefore = cultivation.CurrentXp;
            DebugCommandResult giveXp = console.Execute("/givexp 10");
            AssertSuccess(
                giveXp,
                DebugConsoleService.GiveXpSuccessLocalizationKey);
            Assert.That(cultivation.CurrentXp, Is.GreaterThan(xpBefore));

            int enemiesBefore = Object.FindObjectsByType<EnemyBrain>().Length;
            DebugCommandResult spawn = console.Execute(
                "/spawn enemy_wolf_gray 2");
            AssertSuccess(
                spawn,
                DebugConsoleService.SpawnSuccessLocalizationKey);
            Assert.That(
                Object.FindObjectsByType<EnemyBrain>().Length,
                Is.EqualTo(enemiesBefore + 2));

            Assert.That(playerDebug.GodModeEnabled, Is.False);
            DebugCommandResult god = console.Execute("/god on");
            AssertSuccess(god, DebugConsoleService.GodSuccessLocalizationKey);
            Assert.That(playerDebug.GodModeEnabled, Is.True);
            float hpBefore = player.CurrentHp;
            player.ApplyDamage(
                new DamageInfo
                {
                    Amount = 50f,
                    Type = DamageType.True
                });
            Assert.That(player.CurrentHp, Is.EqualTo(hpBefore));
            AssertSuccess(
                console.Execute("/god off"),
                DebugConsoleService.GodSuccessLocalizationKey);

            DebugCommandResult setRealm = console.Execute("/setrealm 2 1");
            AssertSuccess(
                setRealm,
                DebugConsoleService.SetRealmSuccessLocalizationKey);
            Assert.That(cultivation.Realm, Is.EqualTo(RealmType.Foundation));
            Assert.That(cultivation.SubStage, Is.EqualTo(1));

            DebugCommandResult timeScale = console.Execute("/timescale 0.5");
            AssertSuccess(
                timeScale,
                DebugConsoleService.TimeScaleSuccessLocalizationKey);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Time.timeScale = 1f;

            DebugCommandResult killAll = console.Execute("/killall");
            AssertSuccess(
                killAll,
                DebugConsoleService.KillAllSuccessLocalizationKey);
            Assert.That(
                Array.TrueForAll(
                    Object.FindObjectsByType<EnemyBrain>(),
                    enemy => enemy.IsDead),
                Is.True);

            DebugCommandResult save = console.Execute("/save");
            AssertSuccess(save, DebugConsoleService.SaveSuccessLocalizationKey);
            Assert.That(SaveManager.Instance.GetMetadata(0).Exists, Is.True);

            DebugCommandResult invalid = console.Execute("/give missing_item 1");
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(
                invalid.LocalizationKey,
                Is.EqualTo(DebugConsoleService.InvalidArgumentLocalizationKey));
        }

        [Test]
        public void TutorialSkipWritesTheSameCompletionKeysAndSurvivesLoad()
        {
            IDebugConsoleService console =
                ServiceLocator.Get<IDebugConsoleService>();
            ITutorialService tutorials = ServiceLocator.Get<ITutorialService>();

            DebugCommandResult result = console.Execute("/tutorial_skip");
            AssertSuccess(
                result,
                DebugConsoleService.TutorialSkipSuccessLocalizationKey);
            Assert.That(
                tutorials.HasCompleted(TutorialManager.MoveTutorialId),
                Is.True);
            Assert.That(
                tutorials.HasCompleted(TutorialManager.CombatTutorialId),
                Is.True);
            CollectionAssert.Contains(
                SaveManager.Instance.World.TutorialsCompleted,
                TutorialManager.MoveTutorialId);
            CollectionAssert.Contains(
                SaveManager.Instance.World.TutorialsCompleted,
                TutorialManager.CombatTutorialId);

            string worldPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "world.json");
            string worldJson = File.ReadAllText(worldPath);
            StringAssert.Contains(TutorialManager.MoveTutorialId, worldJson);
            StringAssert.Contains(TutorialManager.CombatTutorialId, worldJson);

            SaveManager.Instance.World.TutorialsCompleted.Clear();
            Assert.That(
                tutorials.HasCompleted(TutorialManager.MoveTutorialId),
                Is.False);
            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            Assert.That(
                tutorials.HasCompleted(TutorialManager.MoveTutorialId),
                Is.True);
            Assert.That(
                tutorials.HasCompleted(TutorialManager.CombatTutorialId),
                Is.True);
        }

        [Test]
        public void SaveRoundTripPreservesQuestInventoryAndTutorialTogether()
        {
            IDebugConsoleService console =
                ServiceLocator.Get<IDebugConsoleService>();
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            ITutorialService tutorials = ServiceLocator.Get<ITutorialService>();
            QuestManager questManager =
                Object.FindAnyObjectByType<QuestManager>();
            Assert.That(questManager, Is.Not.Null);

            Assert.That(quests.Accept(QuestContentIds.MainHuntWolves), Is.True);
            quests.NotifyKill(QuestContentIds.GreyWolfEnemy);
            AssertSuccess(
                console.Execute("/give item_potion_heal_01 2"),
                DebugConsoleService.GiveSuccessLocalizationKey);
            AssertSuccess(
                console.Execute("/tutorial_skip"),
                DebugConsoleService.TutorialSkipSuccessLocalizationKey);
            AssertSuccess(
                console.Execute("/save"),
                DebugConsoleService.SaveSuccessLocalizationKey);

            Assert.That(
                inventory.RemoveItem(InventoryContentIds.HealPotion01, 2),
                Is.True);
            QuestSaveData mutatedQuest = questManager.CaptureSaveData();
            QuestRuntimeState mutatedHunt = mutatedQuest.Quests.Find(
                state => state.QuestId == QuestContentIds.MainHuntWolves);
            Assert.That(mutatedHunt, Is.Not.Null);
            mutatedHunt.ObjectiveProgress[0] = 3;
            mutatedHunt.Status = QuestStatus.Completed;
            questManager.RestoreSaveData(mutatedQuest);
            SaveManager.Instance.World.TutorialsCompleted.Clear();
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Completed));

            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            Assert.That(
                inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(2));
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Active));
            Assert.That(
                quests.GetObjectiveProgress(
                    QuestContentIds.MainHuntWolves,
                    0),
                Is.EqualTo(1));
            Assert.That(
                tutorials.HasCompleted(TutorialManager.MoveTutorialId),
                Is.True);
            Assert.That(
                tutorials.HasCompleted(TutorialManager.CombatTutorialId),
                Is.True);

            Assert.That(
                File.Exists(
                    Path.Combine(
                        _storageRoot,
                        "SaveSlot_0",
                        InventoryManager.SaveModuleName + ".json")),
                Is.True);
            Assert.That(
                File.Exists(
                    Path.Combine(
                        _storageRoot,
                        "SaveSlot_0",
                        QuestManager.SaveModuleName + ".json")),
                Is.True);
        }

        [Test]
        public void ConsoleUiLocksGameplayInputAndShowsLocalizedCommandResult()
        {
            DebugConsoleView view =
                Object.FindAnyObjectByType<DebugConsoleView>();
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            Assert.That(view, Is.Not.Null);
            Assert.That(input.IsEnabled, Is.True);

            view.SetOpen(true);
            Assert.That(view.IsOpen, Is.True);
            Assert.That(input.IsEnabled, Is.False);

            DebugCommandResult result = view.ExecuteCommand("/help");
            AssertSuccess(result, DebugConsoleService.HelpLocalizationKey);
            Assert.That(view.LastCommand, Is.EqualTo("/help"));
            Assert.That(view.HistoryText, Does.Contain("/help"));
            Assert.That(view.HistoryText, Does.Contain(result.DefaultValue));
            Assert.That(
                DebugConsoleView.TitleLocalizationKey,
                Is.EqualTo("ui_debug_console_title"));
            Assert.That(
                DebugConsoleView.PlaceholderLocalizationKey,
                Is.EqualTo("ui_debug_console_placeholder"));

            view.SetOpen(false);
            Assert.That(view.IsOpen, Is.False);
            Assert.That(input.IsEnabled, Is.True);
        }

        private static void AssertSuccess(
            DebugCommandResult result,
            string localizationKey)
        {
            Assert.That(result.Succeeded, Is.True, result.DefaultValue);
            Assert.That(result.LocalizationKey, Is.EqualTo(localizationKey));
            Assert.That(result.DefaultValue, Is.Not.Empty);
        }

        private static void UnlockHuntQuest()
        {
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            Assert.That(
                quests.Accept(QuestContentIds.MainRootAwakening),
                Is.True);
            quests.NotifyTalk(QuestContentIds.YaoLaoNpc);
            Assert.That(
                quests.TurnIn(QuestContentIds.MainRootAwakening),
                Is.True);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<DialogueView>();
            DestroyAll<QuestTrackerView>();
            DestroyAll<SpiritRootSelectionView>();
            DestroyAll<CultivationHudView>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<GameToastView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<DialogueManager>();
            DestroyAll<QuestManager>();
            DestroyAll<LootSystem>();
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
