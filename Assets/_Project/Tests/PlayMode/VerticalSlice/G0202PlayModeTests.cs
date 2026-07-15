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
    public sealed class G0202PlayModeTests
    {
        private string _storageRoot;
        private BodyRefinementManager _body;
        private IInventoryService _inventory;
        private IItemUseService _itemUse;
        private CombatSystem _combat;
        private PlayerController _controller;
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
                "WendaoG0202Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);
            SaveManager.Instance.Profile.SpiritRoot = SpiritRootType.Fire.ToString();

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _body = Object.FindAnyObjectByType<BodyRefinementManager>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _itemUse = ServiceLocator.Get<IItemUseService>();
            _combat = Object.FindAnyObjectByType<CombatSystem>();
            _controller = Object.FindAnyObjectByType<PlayerController>();
            _player = Object.FindAnyObjectByType<PlayerStats>();

            Assert.That(_body, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_itemUse, Is.Not.Null);
            Assert.That(_combat, Is.Not.Null);
            Assert.That(_controller, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Mortal));
            Assert.That(_body.Xp, Is.Zero);
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

        [TestCase(SpiritRootType.Fire, 10f, 200f)]
        [TestCase(SpiritRootType.Earth, 11.5f, 200f)]
        [TestCase(SpiritRootType.Waste, 15f, 250f)]
        public void DamageAndBodyPotionUseApplyRootSpecificXpMultipliers(
            SpiritRootType root,
            float expectedDamageXp,
            float expectedPotionXp)
        {
            SetRoot(root);
            _player.ConfigureBaseStats(1000f, 0f, 0f);
            _player.SetHp(_player.MaxHp);

            GameObject source = new GameObject("[G02-02 Damage Source]");
            try
            {
                _combat.DealDamage(_player, CreatePhysicalRequest(source, 100f));
                Assert.That(
                    _body.Xp,
                    Is.EqualTo(expectedDamageXp).Within(0.001f));

                Assert.That(
                    _inventory.AddItem(
                        InventoryContentIds.BodyPotion01,
                        1,
                        AcquireSource.Cheat),
                    Is.True);
                int slotIndex = FindSlot(InventoryContentIds.BodyPotion01);
                Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(_itemUse.CanUse(slotIndex), Is.True);
                Assert.That(_itemUse.Use(slotIndex), Is.True);
                Assert.That(
                    _body.Xp,
                    Is.EqualTo(expectedDamageXp + expectedPotionXp)
                        .Within(0.001f));
                Assert.That(
                    _inventory.CountItem(InventoryContentIds.BodyPotion01),
                    Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void LevelUpUsesCumulativeThresholdsAndAddsHpAndPhysicalDr()
        {
            SetRoot(SpiritRootType.Fire);
            _player.ConfigureBaseStats(100f, 0f, 0f);
            _player.SetHp(_player.MaxHp);

            _body.AddBodyXp(999f);
            Assert.That(_body.TryLevelUp(), Is.False);
            _body.AddBodyXp(1f);
            Assert.That(_body.TryLevelUp(), Is.True);
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Copper));
            Assert.That(_body.HpBonus, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(_body.PhysicalDR, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(_player.MaxHp, Is.EqualTo(120f).Within(0.001f));

            DamageInfo copperDamage = _combat.ComputeDamage(
                CreatePhysicalRequest(null, 100f),
                _player);
            Assert.That(copperDamage.Amount, Is.EqualTo(90f).Within(0.001f));

            _body.AddBodyXp(3000f);
            Assert.That(_body.TryLevelUp(), Is.True);
            Assert.That(_body.Level, Is.EqualTo(BodyLevel.Diamond));
            Assert.That(_body.HpBonus, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(_body.PhysicalDR, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(_body.ControlResist, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(_player.MaxHp, Is.EqualTo(150f).Within(0.001f));

            DamageRequest trueDamageRequest = CreatePhysicalRequest(null, 100f);
            trueDamageRequest.Type = DamageType.True;
            Assert.That(
                _combat.ComputeDamage(trueDamageRequest, _player).Amount,
                Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void WasteRootBlockingHasPointSevenDrAndTakesThreeQuartersDamage()
        {
            _player.ConfigureBaseStats(1000f, 0f, 0f);
            _player.SetHp(_player.MaxHp);
            _controller.ForceState(PlayerState.Idle);
            Assert.That(_controller.TryStartBlock(), Is.True);

            SetRoot(SpiritRootType.Fire);
            DamageRequest request = CreatePhysicalRequest(null, 100f);
            float baselineDamage = _combat.ComputeDamage(request, _player).Amount;
            Assert.That(
                _player.GetBlockDamageReduction(DamageType.Physical),
                Is.EqualTo(0.60f).Within(0.0001f));
            Assert.That(baselineDamage, Is.EqualTo(40f).Within(0.001f));

            SetRoot(SpiritRootType.Waste);
            float wasteDamage = _combat.ComputeDamage(request, _player).Amount;
            Assert.That(
                _player.GetBlockDamageReduction(DamageType.Physical),
                Is.EqualTo(0.70f).Within(0.0001f));
            Assert.That(wasteDamage, Is.EqualTo(30f).Within(0.001f));
            Assert.That(
                wasteDamage / baselineDamage,
                Is.EqualTo(0.75f).Within(0.001f));

            _combat.DealDamage(_player, request);
            Assert.That(_player.CurrentHp, Is.EqualTo(970f).Within(0.001f));
        }

        private void SetRoot(SpiritRootType root)
        {
            SaveManager.Instance.Profile.SpiritRoot = root.ToString();
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

        private static DamageRequest CreatePhysicalRequest(
            GameObject source,
            float damage)
        {
            return new DamageRequest
            {
                Source = source,
                BaseDamage = damage,
                Type = DamageType.Physical,
                Element = ElementType.None,
                Multiplier = 1f,
                CanCrit = false,
                IgnoreAttackScaling = true,
                SkillId = string.Empty
            };
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.MainMenu),
                    Is.True);
            }

            if (gameManager.State != GameState.Playing)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Loading),
                    Is.True);
                Assert.That(
                    gameManager.TrySetState(GameState.Playing),
                    Is.True);
            }

            gameManager.SetCombatFlag(false);
        }

        private static void DestroyRuntimeObjects()
        {
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
