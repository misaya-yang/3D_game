using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.NPC;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0504PlayModeTests
    {
        private string _storageRoot;
        private IQuestService _quests;
        private IInventoryService _inventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            SceneFlowBootstrap.EnsureServices();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0504Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            _quests = ServiceLocator.Get<IQuestService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
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
        public void MainChapterRegistersTenQuestsDialoguesAndNpcRoutes()
        {
            Assert.That(QuestContentIds.MainChapterOne, Has.Length.EqualTo(10));
            for (int index = 0; index < QuestContentIds.MainChapterOne.Length; index++)
            {
                int step = index + 1;
                QuestData quest = ConfigDatabase.Instance.GetQuest(
                    QuestContentIds.MainChapterOne[index]);
                Assert.That(quest, Is.Not.Null, $"main quest {step}");
                Assert.That(quest.DisplayNameKey, Is.Not.Empty);
                Assert.That(quest.DisplayName, Is.Not.Empty);
                Assert.That(quest.DescriptionKey, Is.Not.Empty);
                Assert.That(quest.Description, Is.Not.Empty);
                Assert.That(quest.Type, Is.EqualTo(QuestType.Main));
                Assert.That(quest.Objectives, Is.Not.Null.And.Not.Empty);
                Assert.That(quest.StartNpcId, Is.Not.Empty);
                Assert.That(quest.TurnInNpcId, Is.Not.Empty);
                Assert.That(
                    ConfigDatabase.Instance.GetDialogue(quest.StartDialogueId),
                    Is.Not.Null);
                Assert.That(
                    ConfigDatabase.Instance.GetDialogue(quest.CompleteDialogueId),
                    Is.Not.Null);

                if (step == 1)
                {
                    Assert.That(quest.PrerequisiteQuestIds, Is.Empty);
                }
                else
                {
                    Assert.That(
                        quest.PrerequisiteQuestIds,
                        Is.EqualTo(new[] { QuestContentIds.MainChapterOne[index - 1] }));
                }
            }

            QuestData goldenCore = ConfigDatabase.Instance.GetQuest(
                QuestContentIds.MainGoldenCoreBreakthrough);
            Assert.That(goldenCore.ObjectivesAreOrdered, Is.True);
            Assert.That(goldenCore.Objectives, Has.Length.EqualTo(3));
            Assert.That(goldenCore.Objectives[0].Type, Is.EqualTo(ObjectiveType.Collect));
            Assert.That(goldenCore.Objectives[0].LatchOnFirstAcquire, Is.True);
            Assert.That(goldenCore.Objectives[1].Type, Is.EqualTo(ObjectiveType.ReachRealm));
            Assert.That(goldenCore.Objectives[2].Type, Is.EqualTo(ObjectiveType.Reach));
            Assert.That(
                goldenCore.Objectives[2].TargetId,
                Is.EqualTo(QuestContentIds.BlackwindEntrance));
            Assert.That(goldenCore.StartNpcId, Is.EqualTo(QuestContentIds.YaoLaoNpc));
            Assert.That(
                goldenCore.TurnInNpcId,
                Is.EqualTo(QuestContentIds.BlackwindEchoNpc));
            Assert.That(
                ConfigDatabase.Instance.GetNpc(QuestContentIds.CangwuGuardNpc),
                Is.Not.Null);
            Assert.That(
                ConfigDatabase.Instance.GetNpc(QuestContentIds.BlackwindEchoNpc),
                Is.Not.Null);
        }

        [Test]
        public void MainChapterCanAdvanceFromQuestOneThroughQuestTen()
        {
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainRootAwakening,
                () => _quests.NotifyTalk(QuestContentIds.YaoLaoNpc));
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainHuntWolves,
                () => Kill(QuestContentIds.GreyWolfEnemy, 3));
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.QingxinGrass,
                    5,
                    AcquireSource.Quest),
                Is.True);
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainGatherQingxin,
                () =>
                {
                    _quests.NotifyCollect(
                        InventoryContentIds.QingxinGrass,
                        _inventory.CountItem(InventoryContentIds.QingxinGrass));
                    Kill(QuestContentIds.EliteWolfEnemy, 1);
                });
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainCraftManaPotion,
                () => EventBus.Publish(
                    AlchemyEvents.CraftCompleted,
                    new CraftResultInfo
                    {
                        RecipeId = "recipe_mana_01",
                        ResultItemId = InventoryContentIds.ManaPotion01,
                        ResultCount = 1,
                        Success = true
                    }));
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainOpenCangwuPath,
                () => _quests.NotifyReach(QuestContentIds.QingshiSecretPath));
            AssertWorldFlag(MapContentIds.CangwuPathOpenFlag, true);
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainCangwuTrial,
                () => Kill(QuestContentIds.GreyWolfEnemy, 3));
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainFoundationClue,
                () => _quests.NotifyTalk(QuestContentIds.CangwuGuardNpc));

            Assert.That(
                _quests.Accept(QuestContentIds.MainFoundationBreakthrough),
                Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.FoundationPill),
                Is.EqualTo(1));
            AssertWorldFlag(
                CultivationContentIds.FoundationPillGrantedFlag,
                true);
            AssertWorldFlag(CultivationContentIds.FoundationPityFlag, true);
            SaveManager.Instance.Profile.Realm = (int)RealmType.Foundation;
            _quests.NotifyRealm(RealmType.Foundation);
            Assert.That(
                _quests.TurnIn(QuestContentIds.MainFoundationBreakthrough),
                Is.True);

            Assert.That(
                _quests.Accept(QuestContentIds.MainGoldenCoreBreakthrough),
                Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.GoldenCorePill),
                Is.EqualTo(1));
            Assert.That(
                _quests.GetObjectiveProgress(
                    QuestContentIds.MainGoldenCoreBreakthrough,
                    0),
                Is.EqualTo(1));
            Assert.That(
                _inventory.RemoveItem(InventoryContentIds.GoldenCorePill, 1),
                Is.True);
            Assert.That(
                _quests.GetObjectiveProgress(
                    QuestContentIds.MainGoldenCoreBreakthrough,
                    0),
                Is.EqualTo(1),
                "Collect latch must survive breakthrough material consumption.");
            SaveManager.Instance.Profile.Realm = (int)RealmType.GoldenCore;
            _quests.NotifyRealm(RealmType.GoldenCore);
            _quests.NotifyReach(QuestContentIds.BlackwindEntrance);
            Assert.That(
                _quests.GetStatus(QuestContentIds.MainGoldenCoreBreakthrough),
                Is.EqualTo(QuestStatus.Completed));
            Assert.That(
                _quests.TurnIn(QuestContentIds.MainGoldenCoreBreakthrough),
                Is.True);

            AcceptCompleteAndTurnIn(
                QuestContentIds.MainDefeatStoneGeneral,
                () => Kill(QuestContentIds.StoneGeneralEnemy, 1));
            Assert.That(_quests.CompletedIds, Has.Count.EqualTo(10));
            for (int index = 0; index < QuestContentIds.MainChapterOne.Length; index++)
            {
                Assert.That(
                    _quests.GetStatus(QuestContentIds.MainChapterOne[index]),
                    Is.EqualTo(QuestStatus.TurnedIn));
            }
        }

        [Test]
        public void FoundationAcceptRewardSkipsDuplicateHistoricalPillButEnablesPity()
        {
            RestoreTurnedInThrough(7);
            SaveManager.Instance.World.QuestFlags[
                CultivationContentIds.FoundationPillGrantedFlag] = true;
            Assert.That(
                _inventory.CountItem(InventoryContentIds.FoundationPill),
                Is.Zero);

            Assert.That(
                _quests.Accept(QuestContentIds.MainFoundationBreakthrough),
                Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.FoundationPill),
                Is.Zero);
            AssertWorldFlag(CultivationContentIds.FoundationPityFlag, true);
        }

        [Test]
        public void GoldenCoreObjectivesRejectEarlyEventsAndRouteEchoDialogues()
        {
            SaveManager.Instance.Profile.Realm = (int)RealmType.Foundation;
            QuestSaveData data = BuildTurnedInThrough(8);
            QuestData quest = ConfigDatabase.Instance.GetQuest(
                QuestContentIds.MainGoldenCoreBreakthrough);
            data.Quests.Add(
                new QuestRuntimeState
                {
                    QuestId = quest.Id,
                    Status = QuestStatus.Active,
                    ObjectiveProgress = new int[3],
                    AcceptRewardsGranted = true
                });
            Object.FindAnyObjectByType<QuestManager>().RestoreSaveData(data);

            _quests.NotifyRealm(RealmType.GoldenCore);
            _quests.NotifyReach(QuestContentIds.BlackwindEntrance);
            Assert.That(
                _quests.GetObjectiveProgress(quest.Id, 1),
                Is.Zero);
            Assert.That(
                _quests.GetObjectiveProgress(quest.Id, 2),
                Is.Zero);

            _quests.NotifyCollect(InventoryContentIds.GoldenCorePill, 1);
            SaveManager.Instance.Profile.Realm = (int)RealmType.GoldenCore;
            _quests.NotifyRealm(RealmType.GoldenCore);
            _quests.NotifyReach(QuestContentIds.BlackwindEntrance);
            Assert.That(_quests.GetStatus(quest.Id), Is.EqualTo(QuestStatus.Completed));
            Assert.That(
                _quests.ResolveInteractionDialogueId(
                    QuestContentIds.BlackwindEchoNpc,
                    string.Empty),
                Is.EqualTo(quest.CompleteDialogueId));
            Assert.That(_quests.TurnIn(quest.Id), Is.True);
            Assert.That(
                _quests.ResolveInteractionDialogueId(
                    QuestContentIds.BlackwindEchoNpc,
                    string.Empty),
                Is.EqualTo(
                    ConfigDatabase.Instance.GetQuest(
                        QuestContentIds.MainDefeatStoneGeneral).StartDialogueId));
        }

        private void AcceptCompleteAndTurnIn(string questId, Action progress)
        {
            Assert.That(
                _quests.GetStatus(questId),
                Is.EqualTo(QuestStatus.Available),
                questId);
            Assert.That(_quests.Accept(questId), Is.True, questId);
            progress();
            Assert.That(
                _quests.GetStatus(questId),
                Is.EqualTo(QuestStatus.Completed),
                questId);
            Assert.That(_quests.TurnIn(questId), Is.True, questId);
        }

        private void RestoreTurnedInThrough(int step)
        {
            Object.FindAnyObjectByType<QuestManager>()
                .RestoreSaveData(BuildTurnedInThrough(step));
        }

        private static QuestSaveData BuildTurnedInThrough(int step)
        {
            var data = new QuestSaveData
            {
                Quests = new List<QuestRuntimeState>()
            };
            for (int index = 0; index < step; index++)
            {
                QuestData quest = ConfigDatabase.Instance.GetQuest(
                    QuestContentIds.MainChapterOne[index]);
                var progress = new int[quest.Objectives.Length];
                for (int objective = 0; objective < progress.Length; objective++)
                {
                    progress[objective] = Mathf.Max(
                        1,
                        quest.Objectives[objective].RequiredCount);
                }

                data.Quests.Add(
                    new QuestRuntimeState
                    {
                        QuestId = quest.Id,
                        Status = QuestStatus.TurnedIn,
                        ObjectiveProgress = progress,
                        AcceptRewardsGranted = true
                    });
            }

            return data;
        }

        private void AssertWorldFlag(string flagId, bool expected)
        {
            Assert.That(
                SaveManager.Instance.World.QuestFlags.TryGetValue(
                    flagId,
                    out bool actual),
                Is.True,
                flagId);
            Assert.That(actual, Is.EqualTo(expected), flagId);
        }

        private void Kill(string enemyId, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _quests.NotifyKill(enemyId);
            }
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<NPCController>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<QuestManager>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Combat.StatusEffectManager>();
            DestroyAll<Wendao.Systems.Combat.CombatFeelController>();
            DestroyAll<Wendao.Systems.Combat.CombatSystem>();
            DestroyAll<Wendao.Systems.Tutorial.TutorialManager>();
            DestroyAll<SceneLoader>();
            DestroyAll<Wendao.Systems.World.BlackwindDungeonSystem>();
            DestroyAll<Wendao.Systems.World.MapTravelSystem>();
            DestroyAll<Wendao.Systems.World.SafeZoneSystem>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
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
