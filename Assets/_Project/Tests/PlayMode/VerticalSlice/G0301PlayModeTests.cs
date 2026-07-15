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
using Wendao.Systems.Inventory;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0301PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private RefineSystem _refine;
        private IEquipmentService _equipment;
        private IInventoryService _inventory;
        private PlayerStats _player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0301Tests_" + Guid.NewGuid().ToString("N"));
            _save = SaveManager.Instance;
            _save.ConfigureStorageRoot(_storageRoot);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _refine = Object.FindAnyObjectByType<RefineSystem>();
            _equipment = ServiceLocator.Get<IEquipmentService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _player = Object.FindAnyObjectByType<PlayerStats>();
            Assert.That(_refine, Is.Not.Null);
            Assert.That(_equipment, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
            Assert.That(
                ConfigDatabase.Instance.GetItem(RefineSystem.MaterialItemId),
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
        public void SuccessRateAndMaterialCostMatchAuthoritativeFormula()
        {
            Assert.That(_refine.GetSuccessRate(0), Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(_refine.GetSuccessRate(10), Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(_refine.GetSuccessRate(100), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(_refine.GetRequiredMaterialCount(0), Is.EqualTo(1));
            Assert.That(_refine.GetRequiredMaterialCount(1), Is.EqualTo(1));
            Assert.That(_refine.GetRequiredMaterialCount(2), Is.EqualTo(2));
            Assert.That(_refine.GetRequiredMaterialCount(9), Is.EqualTo(5));
        }

        [Test]
        public void SuccessfulRefineConsumesStoneRaisesFinalStatsAndPersistsInstance()
        {
            EquipWoodSword();
            AddRefineStones(3);
            _refine.SetRandomValueProvider(() => 0f);
            float attackBefore = _player.Attack;
            EquipmentUpgradeInfo published = default;
            var publishCount = 0;
            Action<EquipmentUpgradeInfo> handler = info =>
            {
                published = info;
                publishCount++;
            };
            EventBus.Subscribe(InventoryEvents.EquipmentUpgraded, handler);
            try
            {
                Assert.That(_refine.CanRefine(EquipmentSlot.Weapon), Is.True);
                Assert.That(_refine.TryRefine(EquipmentSlot.Weapon), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(InventoryEvents.EquipmentUpgraded, handler);
            }

            EquipmentInstance instance = GetWornWeapon();
            Assert.That(instance.RefineLevel, Is.EqualTo(1));
            Assert.That(
                _inventory.CountItem(RefineSystem.MaterialItemId),
                Is.EqualTo(2));
            Assert.That(publishCount, Is.EqualTo(1));
            Assert.That(published.Success, Is.True);
            Assert.That(published.NewRefineLevel, Is.EqualTo(1));
            Assert.That(published.ItemId, Is.EqualTo(InventoryContentIds.WoodSword));
            Assert.That(_player.Attack, Is.EqualTo(attackBefore + 0.4f).Within(0.001f));

            string equipmentPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                EquipmentManager.SaveModuleName + ".json");
            StringAssert.Contains("\"refineLevel\": 1", File.ReadAllText(equipmentPath));

            instance.RefineLevel = 0;
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            Assert.That(GetWornWeapon().RefineLevel, Is.EqualTo(1));
            Assert.That(_player.Attack, Is.EqualTo(attackBefore + 0.4f).Within(0.001f));
        }

        [Test]
        public void FailedRefineConsumesScaledMaterialsWithoutDroppingLevel()
        {
            EquipWoodSword();
            EquipmentInstance instance = GetWornWeapon();
            instance.RefineLevel = 2;
            AddRefineStones(3);
            _refine.SetRandomValueProvider(() => 1f);
            EquipmentUpgradeInfo published = default;
            var publishCount = 0;
            Action<EquipmentUpgradeInfo> handler = info =>
            {
                published = info;
                publishCount++;
            };
            EventBus.Subscribe(InventoryEvents.EquipmentUpgraded, handler);
            try
            {
                Assert.That(_refine.TryRefine(EquipmentSlot.Weapon), Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(InventoryEvents.EquipmentUpgraded, handler);
            }

            Assert.That(instance.RefineLevel, Is.EqualTo(2));
            Assert.That(
                _inventory.CountItem(RefineSystem.MaterialItemId),
                Is.EqualTo(1));
            Assert.That(publishCount, Is.EqualTo(1));
            Assert.That(published.Success, Is.False);
            Assert.That(published.NewRefineLevel, Is.EqualTo(2));
        }

        [Test]
        public void MissingTargetMaterialAndMaxLevelRejectWithoutConsumption()
        {
            AddRefineStones(2);
            var eventCount = 0;
            Action<EquipmentUpgradeInfo> handler = _ => eventCount++;
            EventBus.Subscribe(InventoryEvents.EquipmentUpgraded, handler);
            try
            {
                Assert.That(_refine.TryRefine(EquipmentSlot.Weapon), Is.False);
                Assert.That(
                    _inventory.CountItem(RefineSystem.MaterialItemId),
                    Is.EqualTo(2));

                EquipWoodSword();
                EquipmentInstance instance = GetWornWeapon();
                EquipmentData data = ConfigDatabase.Instance.GetEquipment(
                    instance.EquipmentDataId);
                instance.RefineLevel = data.MaxRefineLevel;
                Assert.That(_refine.CanRefine(EquipmentSlot.Weapon), Is.False);
                Assert.That(_refine.TryRefine(EquipmentSlot.Weapon), Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(InventoryEvents.EquipmentUpgraded, handler);
            }

            Assert.That(
                _inventory.CountItem(RefineSystem.MaterialItemId),
                Is.EqualTo(2));
            Assert.That(eventCount, Is.Zero);
        }

        private void EquipWoodSword()
        {
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            int slot = FindSlot(InventoryContentIds.WoodSword);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(_equipment.EquipFromInventory(slot), Is.True);
        }

        private void AddRefineStones(int count)
        {
            Assert.That(
                _inventory.AddItem(
                    RefineSystem.MaterialItemId,
                    count,
                    AcquireSource.Cheat),
                Is.True);
        }

        private EquipmentInstance GetWornWeapon()
        {
            Assert.That(
                _equipment.Worn.TryGetValue(
                    EquipmentSlot.Weapon,
                    out EquipmentInstance instance),
                Is.True);
            Assert.That(instance, Is.Not.Null);
            return instance;
        }

        private int FindSlot(string itemId)
        {
            for (int index = 0; index < _inventory.Slots.Count; index++)
            {
                if (string.Equals(
                        _inventory.Slots[index]?.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (gameManager.State != GameState.Playing)
            {
                Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
                Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            }

            gameManager.SetCombatFlag(false);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<RefineSystem>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<Wendao.UI.Inventory.InventoryPanelView>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<PlayerController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<BodyRefinementManager>();
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
