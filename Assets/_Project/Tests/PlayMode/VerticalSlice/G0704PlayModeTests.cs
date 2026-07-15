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
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0704PlayModeTests
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
            PlayerRuntimeBootstrap.Install();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0704Tests_" + Guid.NewGuid().ToString("N"));
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
        public void FreshSaveCompletesChapterAndRoundTripsCriticalMvpState()
        {
            CompleteMainChapter();

            IMapTravelService travel = ServiceLocator.Get<IMapTravelService>();
            travel.UnlockMap(MapContentIds.QingshiMap);
            travel.UnlockMap(MapContentIds.CangwuMap);
            travel.UnlockMap(MapContentIds.BlackwindMap);
            travel.UnlockTeleport(MapContentIds.QingshiTownTeleport);
            travel.UnlockTeleport(MapContentIds.CangwuGateTeleport);
            SaveManager.Instance.World.DungeonCheckpoint[
                MapContentIds.BlackwindMap] =
                BlackwindDungeonSystem.MaximumCheckpoint;
            SaveManager.Instance.World.TutorialsCompleted.Add(
                TutorialManager.MoveTutorialId);
            SaveManager.Instance.World.TutorialsCompleted.Add(
                TutorialManager.CombatTutorialId);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.HealPotion01,
                    2,
                    AcquireSource.Quest),
                Is.True);
            int potionCount = _inventory.CountItem(
                InventoryContentIds.HealPotion01);

            Assert.That(SaveManager.Instance.SaveGame(0),
                Is.True,
                SaveManager.Instance.LastError);

            SaveManager.Instance.Profile.Realm = (int)RealmType.QiCondensation;
            SaveManager.Instance.World.UnlockedMaps.Clear();
            SaveManager.Instance.World.UnlockedTeleports.Clear();
            SaveManager.Instance.World.TutorialsCompleted.Clear();
            SaveManager.Instance.World.DungeonCheckpoint.Clear();
            while (_inventory.RemoveItem(InventoryContentIds.HealPotion01, 1))
            {
            }
            Object.FindAnyObjectByType<QuestManager>()
                .RestoreSaveData(new QuestSaveData());

            Assert.That(SaveManager.Instance.LoadGame(0),
                Is.True,
                SaveManager.Instance.LastError);
            Assert.That(
                SaveManager.Instance.Profile.Realm,
                Is.EqualTo((int)RealmType.GoldenCore));
            Assert.That(_quests.CompletedIds, Has.Count.EqualTo(10));
            foreach (string questId in QuestContentIds.MainChapterOne)
            {
                Assert.That(
                    _quests.GetStatus(questId),
                    Is.EqualTo(QuestStatus.TurnedIn),
                    questId);
            }

            Assert.That(
                SaveManager.Instance.World.UnlockedMaps,
                Is.EquivalentTo(new[]
                {
                    MapContentIds.QingshiMap,
                    MapContentIds.CangwuMap,
                    MapContentIds.BlackwindMap
                }));
            Assert.That(
                SaveManager.Instance.World.UnlockedTeleports,
                Is.EquivalentTo(new[]
                {
                    MapContentIds.QingshiTownTeleport,
                    MapContentIds.CangwuGateTeleport
                }));
            Assert.That(
                SaveManager.Instance.World.DungeonCheckpoint[
                    MapContentIds.BlackwindMap],
                Is.EqualTo(BlackwindDungeonSystem.MaximumCheckpoint));
            Assert.That(
                SaveManager.Instance.World.TutorialsCompleted,
                Does.Contain(TutorialManager.MoveTutorialId));
            Assert.That(
                SaveManager.Instance.World.TutorialsCompleted,
                Does.Contain(TutorialManager.CombatTutorialId));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(potionCount));
        }

        [UnityTest]
        public IEnumerator AllThreeMvpMapsLoadWithRuntimePlayerAndWorldRoots()
        {
            string[] scenes =
            {
                SceneLoader.DefaultMapSceneName,
                SceneLoader.CangwuMapSceneName,
                SceneLoader.BlackwindDungeonSceneName,
                SceneLoader.DefaultMapSceneName
            };

            foreach (string sceneName in scenes)
            {
                Assert.That(Application.CanStreamedLevelBeLoaded(sceneName),
                    Is.True,
                    sceneName);
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, sceneName);
                yield return load;

                Scene scene = SceneManager.GetActiveScene();
                Assert.That(scene.name, Is.EqualTo(sceneName));
                EnsureWorld(sceneName, scene);
                PlayerRuntimeBootstrap.EnsureForScene(scene);
                yield return null;

                Assert.That(
                    Object.FindAnyObjectByType<PlayerController>(),
                    Is.Not.Null,
                    sceneName);
                Assert.That(
                    Object.FindAnyObjectByType<PlayerStats>(),
                    Is.Not.Null,
                    sceneName);
            }

            Assert.That(
                GameObject.Find(QingshiGreyboxFactory.RootName),
                Is.Not.Null);
        }

        private void CompleteMainChapter()
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
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainCangwuTrial,
                () => Kill(QuestContentIds.GreyWolfEnemy, 3));
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainFoundationClue,
                () => _quests.NotifyTalk(QuestContentIds.CangwuGuardNpc));

            Assert.That(
                _quests.Accept(QuestContentIds.MainFoundationBreakthrough),
                Is.True);
            SaveManager.Instance.Profile.Realm = (int)RealmType.Foundation;
            _quests.NotifyRealm(RealmType.Foundation);
            Assert.That(
                _quests.TurnIn(QuestContentIds.MainFoundationBreakthrough),
                Is.True);

            Assert.That(
                _quests.Accept(QuestContentIds.MainGoldenCoreBreakthrough),
                Is.True);
            _quests.NotifyCollect(InventoryContentIds.GoldenCorePill, 1);
            SaveManager.Instance.Profile.Realm = (int)RealmType.GoldenCore;
            _quests.NotifyRealm(RealmType.GoldenCore);
            _quests.NotifyReach(QuestContentIds.BlackwindEntrance);
            Assert.That(
                _quests.TurnIn(QuestContentIds.MainGoldenCoreBreakthrough),
                Is.True);
            AcceptCompleteAndTurnIn(
                QuestContentIds.MainDefeatStoneGeneral,
                () => Kill(QuestContentIds.StoneGeneralEnemy, 1));
        }

        private void AcceptCompleteAndTurnIn(string questId, Action progress)
        {
            Assert.That(_quests.Accept(questId), Is.True, questId);
            progress();
            Assert.That(
                _quests.GetStatus(questId),
                Is.EqualTo(QuestStatus.Completed),
                questId);
            Assert.That(_quests.TurnIn(questId), Is.True, questId);
        }

        private void Kill(string enemyId, int count)
        {
            for (int index = 0; index < count; index++)
            {
                _quests.NotifyKill(enemyId);
            }
        }

        private static void EnsureWorld(string sceneName, Scene scene)
        {
            if (sceneName == SceneLoader.DefaultMapSceneName)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
            }
            else if (sceneName == SceneLoader.CangwuMapSceneName)
            {
                CangwuGreyboxFactory.EnsureCreated(scene);
            }
            else if (sceneName == SceneLoader.BlackwindDungeonSceneName)
            {
                BlackwindDungeonFactory.EnsureCreated(scene);
            }
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
            DestroyAll<TutorialManager>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<DialogueManager>();
            DestroyAll<QuestManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<BodyRefinementManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<RefineSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatFeelController>();
            DestroyAll<CombatSystem>();
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<SafeZoneSystem>();
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
