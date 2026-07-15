using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Shop;
using Wendao.Systems.World;
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;
using Wendao.UI.Shop;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0305PlayModeTests
    {
        private string _storageRoot;
        private IShopService _shop;
        private IInventoryService _inventory;
        private PlayerController _player;
        private NPCController _zhanggui;
        private ShopPanelView _view;

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
                "WendaoG0305Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager save = SaveManager.Instance;
            save.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(save.SaveGame(0), Is.True, save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            _player = PlayerRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _shop = ServiceLocator.Get<IShopService>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _zhanggui = FindNpc(ShopContentIds.ZhangguiNpc);
            _view = Object.FindAnyObjectByType<ShopPanelView>(
                FindObjectsInactive.Include);
            Assert.That(_shop, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_player, Is.Not.Null);
            Assert.That(_zhanggui, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);
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
        public void ZhangguiOffersAuthoritativeStockAndInteractionOpensPanel()
        {
            NPCData vendor = ConfigDatabase.Instance.GetNpc(
                ShopContentIds.ZhangguiNpc);
            Assert.That(vendor, Is.Not.Null);
            Assert.That(vendor.IsVendor, Is.True);
            Assert.That(vendor.VendorItemIds, Is.EqualTo(ShopContentIds.ZhangguiStock));
            Assert.That(
                ConfigDatabase.Instance.GetItem(InventoryContentIds.HealPotion01)
                    .BuyPrice,
                Is.EqualTo(10));
            Assert.That(
                ConfigDatabase.Instance.GetItem(InventoryContentIds.RefineStone)
                    .BuyPrice,
                Is.EqualTo(25));
            Assert.That(
                ConfigDatabase.Instance.GetItem(InventoryContentIds.IronSword)
                    .BuyPrice,
                Is.EqualTo(80));
            Assert.That(
                ConfigDatabase.Instance.GetEquipment(InventoryContentIds.IronSword)
                    .BaseStats.Attack,
                Is.EqualTo(18f));

            MovePlayerNear(_zhanggui);
            Assert.That(
                _zhanggui.CurrentPromptLocalizationKey,
                Is.EqualTo(NPCController.VendorInteractionPromptLocalizationKey));
            Assert.That(_zhanggui.TryInteract(), Is.True);
            Assert.That(_shop.IsOpen, Is.True);
            Assert.That(_shop.ActiveVendorId, Is.EqualTo(ShopContentIds.ZhangguiNpc));
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_view.StockButtonCount, Is.EqualTo(3));
            StringAssert.Contains("回血丹", _view.GetStockRowText(0));
            StringAssert.Contains("10", _view.GetStockRowText(0));
            Assert.That(ServiceLocator.Get<IPlayerInputSource>().IsEnabled, Is.False);

            _view.Close();
            Assert.That(_shop.IsOpen, Is.False);
            Assert.That(_view.IsOpen, Is.False);
            Assert.That(ServiceLocator.Get<IPlayerInputSource>().IsEnabled, Is.True);
        }

        [Test]
        public void BuyDeductsSpiritStonesAddsItemPublishesAndPersists()
        {
            Assert.That(_inventory.AddSpiritStones(100), Is.True);
            Assert.That(_shop.OpenVendor(ShopContentIds.ZhangguiNpc), Is.True);
            ItemAcquireInfo acquired = default;
            ShopTransactionInfo transaction = default;
            var acquiredCalls = 0;
            var transactionCalls = 0;
            Action<ItemAcquireInfo> acquiredHandler = info =>
            {
                if (info.Source == AcquireSource.Shop)
                {
                    acquired = info;
                    acquiredCalls++;
                }
            };
            Action<ShopTransactionInfo> transactionHandler = info =>
            {
                transaction = info;
                transactionCalls++;
            };
            EventBus.Subscribe(InventoryEvents.ItemAcquired, acquiredHandler);
            EventBus.Subscribe(ShopEvents.TransactionCompleted, transactionHandler);
            try
            {
                Assert.That(
                    _shop.Buy(
                        ShopContentIds.ZhangguiNpc,
                        InventoryContentIds.HealPotion01,
                        2),
                    Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(InventoryEvents.ItemAcquired, acquiredHandler);
                EventBus.Unsubscribe(
                    ShopEvents.TransactionCompleted,
                    transactionHandler);
            }

            Assert.That(_inventory.SpiritStones, Is.EqualTo(80));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(2));
            Assert.That(acquiredCalls, Is.EqualTo(1));
            Assert.That(acquired.ItemId, Is.EqualTo(InventoryContentIds.HealPotion01));
            Assert.That(acquired.Count, Is.EqualTo(2));
            Assert.That(acquired.Source, Is.EqualTo(AcquireSource.Shop));
            Assert.That(transactionCalls, Is.EqualTo(1));
            Assert.That(transaction.Type, Is.EqualTo(ShopTransactionType.Buy));
            Assert.That(transaction.SpiritStonesDelta, Is.EqualTo(-20));
            Assert.That(
                Object.FindAnyObjectByType<GameToastView>().CurrentLocalizationKey,
                Is.EqualTo(ShopSystem.BuySuccessToastKey));

            string inventoryJson = ReadInventoryJson();
            StringAssert.Contains(InventoryContentIds.HealPotion01, inventoryJson);
            StringAssert.Contains("\"spiritStones\": 80", inventoryJson);
            StringAssert.Contains("\"spiritStones\": 80", ReadProfileJson());
        }

        [Test]
        public void InsufficientSpiritStonesFailsWithoutChangingInventory()
        {
            Assert.That(_inventory.AddSpiritStones(9), Is.True);
            Assert.That(_shop.OpenVendor(ShopContentIds.ZhangguiNpc), Is.True);
            var transactionCalls = 0;
            Action<ShopTransactionInfo> handler = _ => transactionCalls++;
            EventBus.Subscribe(ShopEvents.TransactionCompleted, handler);
            try
            {
                Assert.That(
                    _shop.Buy(
                        ShopContentIds.ZhangguiNpc,
                        InventoryContentIds.HealPotion01,
                        1),
                    Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(ShopEvents.TransactionCompleted, handler);
            }

            Assert.That(
                _shop.LastFailureReason,
                Is.EqualTo(ShopFailureReason.InsufficientFunds));
            Assert.That(_inventory.SpiritStones, Is.EqualTo(9));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.Zero);
            Assert.That(transactionCalls, Is.Zero);
            Assert.That(
                Object.FindAnyObjectByType<GameToastView>().CurrentLocalizationKey,
                Is.EqualTo(ShopSystem.InsufficientFundsToastKey));
        }

        [Test]
        public void SellUsesSellPriceAndPersistsInventoryAndCurrencyTogether()
        {
            Assert.That(_inventory.AddSpiritStones(20), Is.True);
            Assert.That(_shop.OpenVendor(ShopContentIds.ZhangguiNpc), Is.True);
            Assert.That(
                _shop.Buy(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.HealPotion01,
                    1),
                Is.True);
            int slotIndex = FindSlot(InventoryContentIds.HealPotion01);
            Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));
            ShopTransactionInfo transaction = default;
            Action<ShopTransactionInfo> handler = info => transaction = info;
            EventBus.Subscribe(ShopEvents.TransactionCompleted, handler);
            try
            {
                Assert.That(_shop.Sell(slotIndex, 1), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(ShopEvents.TransactionCompleted, handler);
            }

            Assert.That(_inventory.SpiritStones, Is.EqualTo(12));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.Zero);
            Assert.That(transaction.Type, Is.EqualTo(ShopTransactionType.Sell));
            Assert.That(transaction.ItemId, Is.EqualTo(InventoryContentIds.HealPotion01));
            Assert.That(transaction.SpiritStonesDelta, Is.EqualTo(2));
            Assert.That(
                Object.FindAnyObjectByType<GameToastView>().CurrentLocalizationKey,
                Is.EqualTo(ShopSystem.SellSuccessToastKey));

            string inventoryJson = ReadInventoryJson();
            StringAssert.DoesNotContain(InventoryContentIds.HealPotion01, inventoryJson);
            StringAssert.Contains("\"spiritStones\": 12", inventoryJson);
            StringAssert.Contains("\"spiritStones\": 12", ReadProfileJson());
        }

        [Test]
        public void FullInventoryRejectsPurchaseWithoutCharging()
        {
            Assert.That(_inventory.AddSpiritStones(100), Is.True);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.WoodSword,
                    InventoryManager.Capacity,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(_shop.OpenVendor(ShopContentIds.ZhangguiNpc), Is.True);

            Assert.That(
                _shop.Buy(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.HealPotion01,
                    1),
                Is.False);
            Assert.That(
                _shop.LastFailureReason,
                Is.EqualTo(ShopFailureReason.InventoryFull));
            Assert.That(_inventory.SpiritStones, Is.EqualTo(100));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.Zero);
        }

        [Test]
        public void InvalidBoundAndOverflowingTransactionsDoNotMutateState()
        {
            Assert.That(_shop.OpenVendor(ShopContentIds.ZhangguiNpc), Is.True);
            Assert.That(
                _shop.Buy(
                    ShopContentIds.ZhangguiNpc,
                    InventoryContentIds.HealPotion01,
                    0),
                Is.False);
            Assert.That(
                _shop.LastFailureReason,
                Is.EqualTo(ShopFailureReason.InvalidCount));

            ItemData healPotion = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.HealPotion01);
            healPotion.IsBound = true;
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.HealPotion01,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            int boundSlot = FindSlot(InventoryContentIds.HealPotion01);
            Assert.That(_shop.Sell(boundSlot, 1), Is.False);
            Assert.That(
                _shop.LastFailureReason,
                Is.EqualTo(ShopFailureReason.BoundItem));
            Assert.That(
                _inventory.CountItem(InventoryContentIds.HealPotion01),
                Is.EqualTo(1));

            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.WolfHair,
                    1,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(_inventory.AddSpiritStones(int.MaxValue), Is.True);
            int wolfHairSlot = FindSlot(InventoryContentIds.WolfHair);
            Assert.That(_shop.Sell(wolfHairSlot, 1), Is.False);
            Assert.That(
                _shop.LastFailureReason,
                Is.EqualTo(ShopFailureReason.CurrencyOverflow));
            Assert.That(_inventory.SpiritStones, Is.EqualTo(int.MaxValue));
            Assert.That(_inventory.CountItem(InventoryContentIds.WolfHair), Is.EqualTo(1));
        }

        private string ReadInventoryJson()
        {
            return File.ReadAllText(Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                InventoryManager.SaveModuleName + ".json"));
        }

        private string ReadProfileJson()
        {
            return File.ReadAllText(Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "profile.json"));
        }

        private int FindSlot(string itemId)
        {
            for (int index = 0; index < _inventory.Slots.Count; index++)
            {
                InventorySlot slot = _inventory.Slots[index];
                if (slot != null
                    && !slot.IsEmpty
                    && string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static NPCController FindNpc(string npcId)
        {
            NPCController[] npcs = Object.FindObjectsByType<NPCController>(FindObjectsInactive.Include);
            foreach (NPCController npc in npcs)
            {
                if (npc != null
                    && string.Equals(npc.Data?.Id, npcId, StringComparison.Ordinal))
                {
                    return npc;
                }
            }

            return null;
        }

        private void MovePlayerNear(NPCController npc)
        {
            _player.TeleportTo(
                npc.transform.position + new Vector3(0f, 0f, -1f),
                Quaternion.identity);
            Physics.SyncTransforms();
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
            DestroyAll<ShopPanelView>();
            DestroyAll<ShopSystem>();
            DestroyAll<NPCController>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<Wendao.Systems.Inventory.ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Combat.StatusEffectManager>();
            DestroyAll<Wendao.Systems.Combat.CombatSystem>();
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
