using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
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
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Cultivation;
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class GVS06PlayModeTests
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
                "WendaoGVS06Tests_" + Guid.NewGuid().ToString("N"));
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
            TrainingDummyRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            NpcRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            UnlockHuntQuest();
            NPCController yaoLao = FindNpc(QuestContentIds.YaoLaoNpc);
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            Assert.That(yaoLao, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            player.transform.position = yaoLao.transform.position
                + new Vector3(0f, 0f, -1.5f);
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
        public void HuntQuestDialogueNpcAndInteractBindingsMatchContentContract()
        {
            QuestData quest = ConfigDatabase.Instance.GetQuest(
                QuestContentIds.MainHuntWolves);
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.DisplayNameKey, Is.EqualTo("quest_name_main_01_02"));
            Assert.That(quest.DisplayName, Is.EqualTo("东郊猎狼"));
            Assert.That(quest.Type, Is.EqualTo(QuestType.Main));
            Assert.That(quest.Objectives, Has.Length.EqualTo(1));
            Assert.That(quest.Objectives[0].Type, Is.EqualTo(ObjectiveType.Kill));
            Assert.That(
                quest.Objectives[0].TargetId,
                Is.EqualTo(QuestContentIds.GreyWolfEnemy));
            Assert.That(quest.Objectives[0].RequiredCount, Is.EqualTo(3));
            Assert.That(quest.Rewards.CultivationXp, Is.EqualTo(700f));
            Assert.That(quest.Rewards.SpiritStones, Is.EqualTo(10));
            Assert.That(quest.TurnInNpcId, Is.EqualTo(QuestContentIds.YaoLaoNpc));

            DialogueData start = ConfigDatabase.Instance.GetDialogue(
                QuestContentIds.HuntStartDialogue);
            DialogueData complete = ConfigDatabase.Instance.GetDialogue(
                QuestContentIds.HuntCompleteDialogue);
            NPCData npcData = ConfigDatabase.Instance.GetNpc(
                QuestContentIds.YaoLaoNpc);
            Assert.That(start, Is.Not.Null);
            Assert.That(start.Nodes, Has.Length.EqualTo(4));
            Assert.That(start.Nodes[0].TextKey, Is.EqualTo("dlg_main_01_02_start_01"));
            Assert.That(start.Nodes[1].Choices, Has.Length.EqualTo(2));
            Assert.That(
                start.Nodes[2].QuestOfferId,
                Is.EqualTo(QuestContentIds.MainHuntWolves));
            Assert.That(complete, Is.Not.Null);
            Assert.That(
                complete.Nodes[0].QuestTurnInId,
                Is.EqualTo(QuestContentIds.MainHuntWolves));
            Assert.That(npcData, Is.Not.Null);
            Assert.That(npcData.DisplayNameKey, Is.EqualTo("npc_name_yaolao"));

            InputActionAsset asset = Resources.Load<InputActionAsset>(
                PlayerInputReader.DefaultActionAssetResourcePath);
            InputAction interact = asset
                .FindActionMap(PlayerInputReader.PlayerActionMapName, true)
                .FindAction("Interact", true);
            AssertBinding(interact, "<Keyboard>/e");
            AssertBinding(interact, "<Gamepad>/buttonWest");
            Assert.That(
                ServiceLocator.Get<IPlayerInputSource>()
                    .InteractPressedThisFrame,
                Is.False);

            NPCController npc = FindNpc(QuestContentIds.YaoLaoNpc);
            DialogueView dialogueView =
                Object.FindAnyObjectByType<DialogueView>();
            QuestTrackerView tracker =
                Object.FindAnyObjectByType<QuestTrackerView>();
            Assert.That(npc, Is.Not.Null);
            Assert.That(npc.Data.Id, Is.EqualTo(QuestContentIds.YaoLaoNpc));
            Assert.That(
                Vector3.Distance(npc.transform.position, playerPosition()),
                Is.LessThanOrEqualTo(NPCController.InteractionDistance));
            Assert.That(
                npc.CurrentPromptLocalizationKey,
                Is.EqualTo(NPCController.InteractionPromptLocalizationKey));
            Assert.That(dialogueView, Is.Not.Null);
            Assert.That(tracker, Is.Not.Null);
        }

        [Test]
        public void NpcDialogueAcceptsQuestLocksInputAndUpdatesTracker()
        {
            NPCController npc = FindNpc(QuestContentIds.YaoLaoNpc);
            IDialogueService dialogue = ServiceLocator.Get<IDialogueService>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            DialogueView view = Object.FindAnyObjectByType<DialogueView>();
            QuestTrackerView tracker =
                Object.FindAnyObjectByType<QuestTrackerView>();

            var startedCalls = 0;
            var endedCalls = 0;
            var acceptedCalls = 0;
            DialogueInfo endedInfo = default;
            Action<DialogueInfo> started = _ => startedCalls++;
            Action<DialogueInfo> ended = info =>
            {
                endedCalls++;
                endedInfo = info;
            };
            Action<QuestInfo> accepted = _ => acceptedCalls++;
            EventBus.Subscribe(DialogueEvents.Started, started);
            EventBus.Subscribe(DialogueEvents.Ended, ended);
            EventBus.Subscribe(QuestEvents.Accepted, accepted);
            try
            {
                Assert.That(npc.TryInteract(), Is.True);
                Assert.That(dialogue.IsOpen, Is.True);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Dialogue));
                Assert.That(input.IsEnabled, Is.False);
                Assert.That(view.IsVisible, Is.True);
                Assert.That(view.CurrentTextLocalizationKey,
                    Is.EqualTo("dlg_main_01_02_start_01"));

                dialogue.Advance();
                view.Refresh();
                Assert.That(dialogue.VisibleChoices, Has.Count.EqualTo(2));
                Assert.That(view.VisibleChoiceCount, Is.EqualTo(2));
                dialogue.Choose(0);
                view.Refresh();
                Assert.That(
                    dialogue.CurrentNode.QuestOfferId,
                    Is.EqualTo(QuestContentIds.MainHuntWolves));
                dialogue.Advance();
            }
            finally
            {
                EventBus.Unsubscribe(DialogueEvents.Started, started);
                EventBus.Unsubscribe(DialogueEvents.Ended, ended);
                EventBus.Unsubscribe(QuestEvents.Accepted, accepted);
            }

            Assert.That(startedCalls, Is.EqualTo(1));
            Assert.That(endedCalls, Is.EqualTo(1));
            Assert.That(acceptedCalls, Is.EqualTo(1));
            Assert.That(endedInfo.Cancelled, Is.False);
            Assert.That(dialogue.IsOpen, Is.False);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            Assert.That(input.IsEnabled, Is.True);
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Active));
            tracker.Refresh();
            Assert.That(tracker.IsVisible, Is.True);
            Assert.That(
                tracker.CurrentQuestId,
                Is.EqualTo(QuestContentIds.MainHuntWolves));
            Assert.That(tracker.QuestText, Is.EqualTo("东郊猎狼"));
            Assert.That(tracker.ObjectiveText, Does.Contain("0/3"));
        }

        [Test]
        public void CancelledDialogueRestoresInputWithoutAcceptingQuest()
        {
            NPCController npc = FindNpc(QuestContentIds.YaoLaoNpc);
            IDialogueService dialogue = ServiceLocator.Get<IDialogueService>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            DialogueInfo endedInfo = default;
            Action<DialogueInfo> ended = info => endedInfo = info;
            EventBus.Subscribe(DialogueEvents.Ended, ended);
            try
            {
                Assert.That(npc.TryInteract(), Is.True);
                dialogue.EndDialogue(true);
            }
            finally
            {
                EventBus.Unsubscribe(DialogueEvents.Ended, ended);
            }

            Assert.That(endedInfo.Cancelled, Is.True);
            Assert.That(input.IsEnabled, Is.True);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Available));
        }

        [Test]
        public void WolfKillEventsCapProgressAndTurnInGrantsRewardExactlyOnce()
        {
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            ICultivationService cultivation =
                ServiceLocator.Get<ICultivationService>();
            IDialogueService dialogue = ServiceLocator.Get<IDialogueService>();
            ISpiritRootService spiritRoot =
                ServiceLocator.Get<ISpiritRootService>();
            QuestData quest = ConfigDatabase.Instance.GetQuest(
                QuestContentIds.MainHuntWolves);
            NPCController npc = FindNpc(QuestContentIds.YaoLaoNpc);
            QuestTrackerView tracker =
                Object.FindAnyObjectByType<QuestTrackerView>();
            Assert.That(quests.Accept(QuestContentIds.MainHuntWolves), Is.True);

            var progressCalls = 0;
            var completedCalls = 0;
            var xpGainCalls = 0;
            XpGainInfo grantedXp = default;
            Action<QuestProgressInfo> progressed = _ => progressCalls++;
            Action<QuestInfo> completed = _ => completedCalls++;
            Action<XpGainInfo> xpGained = info =>
            {
                xpGainCalls++;
                grantedXp = info;
            };
            EventBus.Subscribe(QuestEvents.Progressed, progressed);
            EventBus.Subscribe(QuestEvents.Completed, completed);
            EventBus.Subscribe(CultivationEvents.XpGained, xpGained);
            try
            {
                for (int index = 0; index < 4; index++)
                {
                    PublishWolfKilled();
                }

                Assert.That(
                    quests.GetObjectiveProgress(
                        QuestContentIds.MainHuntWolves,
                        0),
                    Is.EqualTo(3));
                Assert.That(
                    quests.GetStatus(QuestContentIds.MainHuntWolves),
                    Is.EqualTo(QuestStatus.Completed));
                tracker.Refresh();
                Assert.That(
                    tracker.CurrentObjectiveLocalizationKey,
                    Is.EqualTo(QuestTrackerView.ReadyTurnInLocalizationKey));
                Assert.That(tracker.ObjectiveText, Is.EqualTo("目标已完成，可以交付"));

                Assert.That(npc.TryInteract(), Is.True);
                Assert.That(
                    dialogue.CurrentDialogueId,
                    Is.EqualTo(QuestContentIds.HuntCompleteDialogue));
                dialogue.Advance();
            }
            finally
            {
                EventBus.Unsubscribe(QuestEvents.Progressed, progressed);
                EventBus.Unsubscribe(QuestEvents.Completed, completed);
                EventBus.Unsubscribe(CultivationEvents.XpGained, xpGained);
            }

            Assert.That(progressCalls, Is.EqualTo(3));
            Assert.That(completedCalls, Is.EqualTo(1));
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.TurnedIn));
            Assert.That(
                quests.CompletedIds,
                Does.Contain(QuestContentIds.MainHuntWolves));
            Assert.That(inventory.SpiritStones, Is.EqualTo(10));
            Assert.That(xpGainCalls, Is.EqualTo(1));
            Assert.That(grantedXp.Source, Is.EqualTo(XpSourceType.Quest));
            Assert.That(
                grantedXp.Amount,
                Is.EqualTo(
                    quest.Rewards.CultivationXp
                    * spiritRoot.GetCultivationMultiplier()).Within(0.001f));
            int rewardedSubStage = cultivation.SubStage;
            float rewardedXp = cultivation.CurrentXp;
            Assert.That(quests.TurnIn(QuestContentIds.MainHuntWolves), Is.False);
            Assert.That(inventory.SpiritStones, Is.EqualTo(10));
            Assert.That(xpGainCalls, Is.EqualTo(1));
            Assert.That(cultivation.SubStage, Is.EqualTo(rewardedSubStage));
            Assert.That(cultivation.CurrentXp, Is.EqualTo(rewardedXp).Within(0.001f));
            tracker.Refresh();
            Assert.That(tracker.IsVisible, Is.False);
        }

        [Test]
        public void ActiveQuestProgressRoundTripsThroughQuestsModule()
        {
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            QuestManager manager = Object.FindAnyObjectByType<QuestManager>();
            QuestTrackerView tracker =
                Object.FindAnyObjectByType<QuestTrackerView>();
            Assert.That(manager, Is.Not.Null);
            Assert.That(quests.Accept(QuestContentIds.MainHuntWolves), Is.True);
            PublishWolfKilled();
            PublishWolfKilled();
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            string path = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                QuestManager.SaveModuleName + ".json");
            Assert.That(File.Exists(path), Is.True);
            string json = File.ReadAllText(path);
            StringAssert.Contains(QuestContentIds.MainHuntWolves, json);
            StringAssert.Contains("acceptRewardsGranted", json);

            QuestSaveData mutated = manager.CaptureSaveData();
            QuestRuntimeState mutatedHunt = mutated.Quests.Find(
                state => state.QuestId == QuestContentIds.MainHuntWolves);
            Assert.That(mutatedHunt, Is.Not.Null);
            mutatedHunt.ObjectiveProgress[0] = 3;
            mutatedHunt.Status = QuestStatus.Completed;
            manager.RestoreSaveData(mutated);
            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Completed));
            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);

            Assert.That(
                quests.GetStatus(QuestContentIds.MainHuntWolves),
                Is.EqualTo(QuestStatus.Active));
            Assert.That(
                quests.GetObjectiveProgress(
                    QuestContentIds.MainHuntWolves,
                    0),
                Is.EqualTo(2));
            Assert.That(quests.Accept(QuestContentIds.MainHuntWolves), Is.False);
            tracker.Refresh();
            Assert.That(tracker.ObjectiveText, Does.Contain("2/3"));
        }

        private static void PublishWolfKilled()
        {
            EventBus.Publish(
                CombatEvents.EnemyKilled,
                new EnemyDeathInfo
                {
                    EnemyId = QuestContentIds.GreyWolfEnemy,
                    Rank = EnemyRank.Normal,
                    Position = Vector3.zero
                });
        }

        private static void UnlockHuntQuest()
        {
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            Assert.That(
                quests.GetStatus(QuestContentIds.MainRootAwakening),
                Is.EqualTo(QuestStatus.Available));
            Assert.That(
                quests.Accept(QuestContentIds.MainRootAwakening),
                Is.True);
            quests.NotifyTalk(QuestContentIds.YaoLaoNpc);
            Assert.That(
                quests.TurnIn(QuestContentIds.MainRootAwakening),
                Is.True);
        }

        private static void AssertBinding(InputAction action, string effectivePath)
        {
            bool found = false;
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.effectivePath == effectivePath)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True, $"{action.name} must contain {effectivePath}.");
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
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
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static NPCController FindNpc(string npcId)
        {
            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsInactive.Include);
            foreach (NPCController npc in npcs)
            {
                if (npc != null
                    && string.Equals(npc.Data?.Id, npcId, StringComparison.Ordinal))
                {
                    PlayerController player =
                        Object.FindAnyObjectByType<PlayerController>();
                    if (player != null)
                    {
                        npc.transform.position = player.transform.position
                            + new Vector3(0f, 0f, 1.5f);
                        Physics.SyncTransforms();
                    }

                    return npc;
                }
            }

            return null;
        }

        private static Vector3 playerPosition()
        {
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            return player != null ? player.transform.position : Vector3.positiveInfinity;
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
