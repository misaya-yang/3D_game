using System;
using System.Collections;
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
using Wendao.Systems.Skill;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0505PlayModeTests
    {
        private string _storageRoot;
        private IQuestService _quests;
        private IDailyQuestService _dailies;
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
                "WendaoG0505Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            _quests = ServiceLocator.Get<IQuestService>();
            _dailies = ServiceLocator.Get<IDailyQuestService>();
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
        public void RegistersThreeSideQuestsTwoDailiesAndNpcRoutes()
        {
            Assert.That(QuestContentIds.SideQuests, Has.Length.EqualTo(3));
            for (int index = 0; index < QuestContentIds.SideQuests.Length; index++)
            {
                QuestData quest = ConfigDatabase.Instance.GetQuest(
                    QuestContentIds.SideQuests[index]);
                Assert.That(quest, Is.Not.Null);
                Assert.That(quest.Type, Is.EqualTo(QuestType.Side));
                Assert.That(quest.DisplayNameKey, Is.Not.Empty);
                Assert.That(quest.DescriptionKey, Is.Not.Empty);
                Assert.That(quest.Objectives, Is.Not.Empty);
                Assert.That(ConfigDatabase.Instance.GetNpc(quest.StartNpcId), Is.Not.Null);
                Assert.That(ConfigDatabase.Instance.GetNpc(quest.TurnInNpcId), Is.Not.Null);
                Assert.That(
                    ConfigDatabase.Instance.GetDialogue(quest.StartDialogueId),
                    Is.Not.Null);
                Assert.That(
                    ConfigDatabase.Instance.GetDialogue(quest.CompleteDialogueId),
                    Is.Not.Null);
            }

            Assert.That(QuestContentIds.DailyQuests, Has.Length.EqualTo(2));
            for (int index = 0; index < QuestContentIds.DailyQuests.Length; index++)
            {
                QuestData daily = ConfigDatabase.Instance.GetQuest(
                    QuestContentIds.DailyQuests[index]);
                Assert.That(daily, Is.Not.Null);
                Assert.That(daily.Type, Is.EqualTo(QuestType.Daily));
                Assert.That(daily.Objectives, Has.Length.EqualTo(1));
            }

            Assert.That(
                ConfigDatabase.Instance.GetEnemy(QuestContentIds.BanditEnemy),
                Is.Not.Null);
            ItemData letter = ConfigDatabase.Instance.GetItem(
                QuestContentIds.HermitLetterItem);
            Assert.That(letter, Is.Not.Null);
            Assert.That(letter.DisplayNameKey, Is.Not.Empty);
            Assert.That(letter.DescriptionKey, Is.Not.Empty);
        }

        [Test]
        public void ThreeSideQuestsCompleteWithExactRewards()
        {
            Assert.That(_quests.Accept(QuestContentIds.SideHerb), Is.True);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.QingxinGrass,
                    10,
                    AcquireSource.Gather),
                Is.True);
            Assert.That(
                _quests.GetStatus(QuestContentIds.SideHerb),
                Is.EqualTo(QuestStatus.Completed));
            int stonesBefore = _inventory.SpiritStones;
            Assert.That(_quests.TurnIn(QuestContentIds.SideHerb), Is.True);
            Assert.That(_inventory.SpiritStones, Is.EqualTo(stonesBefore + 50));
            Assert.That(
                SaveManager.Instance.Profile.FactionReputation[
                    QuestContentIds.DandingFaction],
                Is.EqualTo(20));

            Assert.That(_quests.Accept(QuestContentIds.SideBandit), Is.True);
            for (int index = 0; index < 8; index++)
            {
                _quests.NotifyKill(QuestContentIds.BanditEnemy);
            }

            Assert.That(_quests.TurnIn(QuestContentIds.SideBandit), Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.IronSword),
                Is.EqualTo(1));

            Assert.That(_quests.Accept(QuestContentIds.SideHermit), Is.True);
            Assert.That(
                _inventory.CountItem(QuestContentIds.HermitLetterItem),
                Is.EqualTo(1));
            _quests.NotifyTalk(QuestContentIds.HermitNpc);
            Assert.That(_quests.TurnIn(QuestContentIds.SideHermit), Is.True);
            Assert.That(
                _inventory.CountItem(QuestContentIds.HermitLetterItem),
                Is.Zero);
            Assert.That(
                HasLearned(ServiceLocator.Get<ISkillService>(), SkillContentIds.WindSlash),
                Is.True);
        }

        [Test]
        public void HuntAndGatherDailiesProgressClaimAndDoNotDoubleClaim()
        {
            for (int index = 0; index < 15; index++)
            {
                EventBus.Publish(
                    CombatEvents.EnemyKilled,
                    new EnemyDeathInfo
                    {
                        EnemyId = QuestContentIds.GreyWolfEnemy,
                        Rank = EnemyRank.Normal
                    });
            }

            EventBus.Publish(
                InventoryEvents.ItemAcquired,
                new ItemAcquireInfo
                {
                    ItemId = InventoryContentIds.QingxinGrass,
                    Count = 5,
                    Source = AcquireSource.Gather
                });

            Assert.That(_dailies.IsComplete(QuestContentIds.DailyHunt), Is.True);
            Assert.That(_dailies.IsComplete(QuestContentIds.DailyGather), Is.True);
            int stonesBefore = _inventory.SpiritStones;
            Assert.That(_dailies.TryClaim(QuestContentIds.DailyHunt), Is.True);
            Assert.That(_inventory.SpiritStones, Is.EqualTo(stonesBefore + 30));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.CultivationPotion01),
                Is.EqualTo(1));
            Assert.That(_dailies.TryClaim(QuestContentIds.DailyHunt), Is.False);

            Assert.That(_dailies.TryClaim(QuestContentIds.DailyGather), Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.SpiritDust),
                Is.EqualTo(5));
        }

        [Test]
        public void DailyCycleResetsAfterTwentyFourHoursAndCorruptionIsOptional()
        {
            DateTime cycle = _dailies.CycleStartedUtc;
            EventBus.Publish(
                CombatEvents.EnemyKilled,
                new EnemyDeathInfo
                {
                    EnemyId = QuestContentIds.GreyWolfEnemy,
                    Rank = EnemyRank.Normal
                });
            Assert.That(
                _dailies.GetState(QuestContentIds.DailyHunt).Progress,
                Is.EqualTo(1));
            Assert.That(_dailies.Refresh(cycle.AddHours(23.9d)), Is.False);
            Assert.That(_dailies.Refresh(cycle.AddHours(24d)), Is.True);
            Assert.That(
                _dailies.GetState(QuestContentIds.DailyHunt).Progress,
                Is.Zero);

            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            string dailyPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                DailyQuestManager.SaveModuleName + ".json");
            File.WriteAllText(dailyPath, "{broken-json");
            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            Assert.That(
                _dailies.GetState(QuestContentIds.DailyHunt).Progress,
                Is.Zero);
            Assert.That(
                _quests.GetStatus(QuestContentIds.MainRootAwakening),
                Is.EqualTo(QuestStatus.Available),
                "An optional daily failure must not block the main quest save.");
        }

        private static bool HasLearned(ISkillService skills, string skillId)
        {
            for (int index = 0; index < skills.Learned.Count; index++)
            {
                if (skills.Learned[index].SkillId == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<NPCController>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<DailyQuestManager>();
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
