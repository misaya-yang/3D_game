using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0506PlayModeTests
    {
        private string _storageRoot;
        private ISerendipityService _serendipities;
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
                "WendaoG0506Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            _serendipities = ServiceLocator.Get<ISerendipityService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            GameManager.Instance.SetCombatFlag(true);
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
        public void RegistersFourNarrativeEntriesWithNoEquipmentRewards()
        {
            Assert.That(SerendipityContentIds.All, Has.Length.EqualTo(4));
            int qingshi = 0;
            int cangwu = 0;
            int blackwind = 0;
            for (int index = 0; index < SerendipityContentIds.All.Length; index++)
            {
                SerendipityData data = ConfigDatabase.Instance.GetSerendipity(
                    SerendipityContentIds.All[index]);
                Assert.That(data, Is.Not.Null);
                Assert.That(data.OnceOnly, Is.True);
                Assert.That(data.TriggerId, Is.Not.Empty);
                Assert.That(data.WorldFlag, Is.Not.Empty);
                Assert.That(
                    ConfigDatabase.Instance.GetDialogue(data.DialogueId),
                    Is.Not.Null);
                ItemStack[] rewards = data.Rewards.Items ?? Array.Empty<ItemStack>();
                for (int reward = 0; reward < rewards.Length; reward++)
                {
                    Assert.That(
                        ConfigDatabase.Instance.GetItem(rewards[reward].ItemId).Type,
                        Is.Not.EqualTo(ItemType.Equipment));
                }

                if (data.MapId == MapContentIds.QingshiMap) qingshi++;
                if (data.MapId == MapContentIds.CangwuMap) cangwu++;
                if (data.MapId == MapContentIds.BlackwindMap) blackwind++;
            }

            Assert.That(qingshi, Is.EqualTo(1));
            Assert.That(cangwu, Is.EqualTo(2));
            Assert.That(blackwind, Is.EqualTo(1));
        }

        [Test]
        public void AllThreeMapsTriggerOnceGrantRewardsFlagsAndEvents()
        {
            int eventCount = 0;
            EventBus.Subscribe<SerendipityInfo>(
                SerendipityEvents.Triggered,
                _ => eventCount++);

            AssertTrigger(SerendipityContentIds.QingshiHerbSpirit);
            AssertTrigger(SerendipityContentIds.CangwuMistStele);
            AssertTrigger(SerendipityContentIds.CangwuCliffBox);
            AssertTrigger(SerendipityContentIds.BlackwindEchoCache);

            Assert.That(eventCount, Is.EqualTo(4));
            Assert.That(
                SaveManager.Instance.World.SerendipityFlags,
                Has.Count.EqualTo(4));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.QingxinGrass),
                Is.EqualTo(3));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.SkillScroll),
                Is.EqualTo(1));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.SpiritDust),
                Is.EqualTo(5));
            Assert.That(
                _inventory.CountItem("item_mat_black_stone"),
                Is.EqualTo(3));
            Assert.That(_inventory.SpiritStones, Is.EqualTo(60));
            Assert.That(
                SaveManager.Instance.World.QuestFlags[
                    "lore_cangwu_mist_stele"],
                Is.True);
        }

        [Test]
        public void SaveLoadKeepsOnceFlagAndPreventsDuplicateReward()
        {
            string id = SerendipityContentIds.QingshiHerbSpirit;
            SerendipityData data = ConfigDatabase.Instance.GetSerendipity(id);
            Assert.That(
                _serendipities.TryTrigger(id, data.MapId, Vector3.zero),
                Is.True);
            int stones = _inventory.SpiritStones;
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            SaveManager.Instance.World.SerendipityFlags.Clear();
            SaveManager.Instance.World.QuestFlags.Clear();
            Assert.That(SaveManager.Instance.LoadGame(0), Is.True);
            Assert.That(_serendipities.HasCompleted(id), Is.True);
            Assert.That(
                _serendipities.TryTrigger(id, data.MapId, Vector3.zero),
                Is.False);
            Assert.That(_inventory.SpiritStones, Is.EqualTo(stones));
        }

        [Test]
        public void FullInventoryDropsRewardAndEquipmentMisconfigurationIsStripped()
        {
            while (_inventory.CanAdd(InventoryContentIds.IronSword, 1))
            {
                Assert.That(
                    _inventory.AddItem(
                        InventoryContentIds.IronSword,
                        1,
                        AcquireSource.Cheat),
                    Is.True);
            }

            ILootService loot = ServiceLocator.Get<ILootService>();
            SerendipityData herb = ConfigDatabase.Instance.GetSerendipity(
                SerendipityContentIds.QingshiHerbSpirit);
            Assert.That(
                _serendipities.TryTrigger(
                    herb.Id,
                    herb.MapId,
                    new Vector3(3f, 0f, 4f)),
                Is.True);
            Assert.That(loot.ActiveWorldPickupCount, Is.EqualTo(1));

            SerendipityData cliff = ConfigDatabase.Instance.GetSerendipity(
                SerendipityContentIds.CangwuCliffBox);
            cliff.Rewards = new QuestReward
            {
                Items = new[]
                {
                    new ItemStack
                    {
                        ItemId = InventoryContentIds.IronSword,
                        Count = 1
                    }
                }
            };
            int ironBefore = _inventory.CountItem(InventoryContentIds.IronSword);
            LogAssert.Expect(
                LogType.Error,
                new Regex("equipment reward 'eq_weapon_iron_sword' was stripped"));
            Assert.That(
                _serendipities.TryTrigger(
                    cliff.Id,
                    cliff.MapId,
                    Vector3.zero),
                Is.True);
            Assert.That(
                _inventory.CountItem(InventoryContentIds.IronSword),
                Is.EqualTo(ironBefore));
        }

        [UnityTest]
        public IEnumerator RuntimeBootstrapCreatesOneTwoOneTriggersAcrossMaps()
        {
            Scene qingshi = GetOrCreateScene(
                SceneLoader.DefaultMapSceneName,
                out bool createdQingshi);
            Scene cangwu = GetOrCreateScene(
                SceneLoader.CangwuMapSceneName,
                out bool createdCangwu);
            Scene blackwind = GetOrCreateScene(
                SceneLoader.BlackwindDungeonSceneName,
                out bool createdBlackwind);

            Assert.That(
                SerendipityRuntimeBootstrap.EnsureForScene(qingshi),
                Is.EqualTo(1));
            Assert.That(
                SerendipityRuntimeBootstrap.EnsureForScene(cangwu),
                Is.EqualTo(2));
            Assert.That(
                SerendipityRuntimeBootstrap.EnsureForScene(blackwind),
                Is.EqualTo(1));
            Assert.That(CountTriggersInScene(qingshi), Is.EqualTo(1));
            Assert.That(CountTriggersInScene(cangwu), Is.EqualTo(2));
            Assert.That(CountTriggersInScene(blackwind), Is.EqualTo(1));

            if (createdQingshi)
            {
                yield return SceneManager.UnloadSceneAsync(qingshi);
            }

            if (createdCangwu)
            {
                yield return SceneManager.UnloadSceneAsync(cangwu);
            }

            if (createdBlackwind)
            {
                yield return SceneManager.UnloadSceneAsync(blackwind);
            }
        }

        private static Scene GetOrCreateScene(string sceneName, out bool created)
        {
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                created = false;
                return existing;
            }

            created = true;
            return SceneManager.CreateScene(sceneName);
        }

        private static int CountTriggersInScene(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<SerendipityTrigger>(true).Length;
            }

            return count;
        }

        private void AssertTrigger(string id)
        {
            SerendipityData data = ConfigDatabase.Instance.GetSerendipity(id);
            Assert.That(
                _serendipities.TryTrigger(id, data.MapId, Vector3.zero),
                Is.True,
                id);
            Assert.That(_serendipities.HasCompleted(id), Is.True, id);
            Assert.That(
                SaveManager.Instance.World.QuestFlags[data.WorldFlag],
                Is.True,
                id);
            Assert.That(
                _serendipities.TryTrigger(id, data.MapId, Vector3.zero),
                Is.False,
                id + " must be once-only");
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<SerendipityTrigger>();
            DestroyAll<WorldItemPickup>();
            DestroyAll<SerendipitySystem>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<DailyQuestManager>();
            DestroyAll<QuestManager>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
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
