using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;
using Wendao.Systems.Faction;

namespace Wendao.Systems.Shop
{
    public sealed class ShopSystem : SafeBehaviour, IShopService
    {
        public const string BuySuccessToastKey = "ui_shop_buy_success";
        public const string BuySuccessToastDefault =
            "购得{0} ×{1}，花费{2}灵石。";
        public const string SellSuccessToastKey = "ui_shop_sell_success";
        public const string SellSuccessToastDefault =
            "售出{0} ×{1}，获得{2}灵石。";
        public const string InsufficientFundsToastKey =
            "ui_shop_insufficient_funds";
        public const string InsufficientFundsToastDefault = "灵石不足。";
        public const string InventoryFullToastKey = "ui_shop_inventory_full";
        public const string InventoryFullToastDefault = "背包空间不足。";
        public const string TransactionFailedToastKey =
            "ui_shop_transaction_failed";
        public const string TransactionFailedToastDefault =
            "这笔交易无法完成。";
        public const string BoundItemToastKey = "ui_shop_bound_item";
        public const string BoundItemToastDefault = "绑定物品不可出售。";

        private bool _registeredService;

        public bool IsOpen => !string.IsNullOrEmpty(ActiveVendorId);
        public string ActiveVendorId { get; private set; } = string.Empty;
        public ShopFailureReason LastFailureReason { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IShopService>(out IShopService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IShopService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            RepairServiceRegistration();
        }

        private void OnDestroy()
        {
            CloseVendor();
            if (_registeredService
                && ServiceLocator.TryGet<IShopService>(out IShopService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IShopService>();
            }

            _registeredService = false;
        }

        public IReadOnlyList<string> GetStock(string npcId)
        {
            NPCData vendor = GetVendor(npcId);
            return vendor?.VendorItemIds ?? Array.Empty<string>();
        }

        public int GetBuyPrice(string npcId, string itemId)
        {
            if (!IsStockedByVendor(npcId, itemId))
            {
                return 0;
            }

            int basePrice = Math.Max(
                0,
                ConfigDatabase.Instance?.GetItem(itemId)?.BuyPrice ?? 0);
            NPCData vendor = GetVendor(npcId);
            float discount = vendor != null
                && !string.IsNullOrEmpty(vendor.FactionId)
                && ServiceLocator.TryGet<IFactionService>(
                    out IFactionService faction)
                ? faction.GetShopDiscount(vendor.FactionId)
                : 0f;
            return basePrice <= 0
                ? 0
                : Math.Max(
                    1,
                    Mathf.FloorToInt(basePrice * (1f - Mathf.Clamp01(discount))));
        }

        public bool OpenVendor(string npcId)
        {
            NPCData vendor = GetVendor(npcId);
            if (vendor == null || !IsGameplayRunning())
            {
                return Fail(ShopFailureReason.InvalidVendor, true);
            }

            if (string.Equals(ActiveVendorId, npcId, StringComparison.Ordinal))
            {
                LastFailureReason = ShopFailureReason.None;
                return true;
            }

            if (IsOpen)
            {
                CloseVendor();
            }

            ActiveVendorId = npcId;
            LastFailureReason = ShopFailureReason.None;
            EventBus.Publish(
                ShopEvents.Opened,
                new ShopOpenInfo { VendorId = npcId });
            return true;
        }

        public bool CloseVendor()
        {
            if (!IsOpen)
            {
                return false;
            }

            string vendorId = ActiveVendorId;
            ActiveVendorId = string.Empty;
            LastFailureReason = ShopFailureReason.None;
            EventBus.Publish(
                ShopEvents.Closed,
                new ShopOpenInfo { VendorId = vendorId });
            return true;
        }

        public bool Buy(string npcId, string itemId, int count)
        {
            if (GetVendor(npcId) == null)
            {
                return Fail(ShopFailureReason.InvalidVendor, true);
            }

            if (count <= 0)
            {
                return Fail(ShopFailureReason.InvalidCount, true);
            }

            if (!IsStockedByVendor(npcId, itemId))
            {
                return Fail(ShopFailureReason.NotSoldByVendor, true);
            }

            ItemData item = ConfigDatabase.Instance?.GetItem(itemId);
            if (item == null || item.BuyPrice <= 0)
            {
                return Fail(ShopFailureReason.InvalidItem, true);
            }

            int unitPrice = GetBuyPrice(npcId, itemId);
            long totalLong = (long)unitPrice * count;
            if (totalLong <= 0 || totalLong > int.MaxValue)
            {
                return Fail(ShopFailureReason.CurrencyOverflow, true);
            }

            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return Fail(ShopFailureReason.InvalidItem, true);
            }

            int total = (int)totalLong;
            if (inventory.SpiritStones < total)
            {
                return Fail(ShopFailureReason.InsufficientFunds, true);
            }

            if (!inventory.CanAdd(itemId, count))
            {
                return Fail(ShopFailureReason.InventoryFull, true);
            }

            if (!inventory.AddSpiritStones(-total))
            {
                return Fail(ShopFailureReason.InsufficientFunds, true);
            }

            if (!inventory.AddItem(itemId, count, AcquireSource.Shop))
            {
                inventory.AddSpiritStones(total);
                return Fail(ShopFailureReason.InventoryFull, true);
            }

            LastFailureReason = ShopFailureReason.None;
            PersistInventory();
            EventBus.Publish(
                ShopEvents.TransactionCompleted,
                new ShopTransactionInfo
                {
                    VendorId = npcId,
                    Type = ShopTransactionType.Buy,
                    ItemId = itemId,
                    Count = count,
                    SpiritStonesDelta = -total,
                    InventorySlot = -1
                });
            PublishToast(
                BuySuccessToastKey,
                string.Format(
                    BuySuccessToastDefault,
                    item.DisplayName,
                    count,
                    total));
            return true;
        }

        public bool Sell(int inventorySlot, int count)
        {
            if (!IsOpen || GetVendor(ActiveVendorId) == null)
            {
                return Fail(ShopFailureReason.ShopClosed, true);
            }

            if (count <= 0)
            {
                return Fail(ShopFailureReason.InvalidCount, true);
            }

            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || inventorySlot < 0
                || inventorySlot >= inventory.Slots.Count)
            {
                return Fail(ShopFailureReason.InvalidItem, true);
            }

            InventorySlot slot = inventory.Slots[inventorySlot];
            ItemData item = slot == null || slot.IsEmpty
                ? null
                : ConfigDatabase.Instance?.GetItem(slot.ItemId);
            if (item == null || count > slot.Count || item.SellPrice <= 0
                || item.Type == ItemType.Quest || item.Type == ItemType.Currency)
            {
                return Fail(ShopFailureReason.ItemNotSellable, true);
            }

            if (slot.Bound || item.IsBound)
            {
                return Fail(ShopFailureReason.BoundItem, true);
            }

            long revenueLong = (long)item.SellPrice * count;
            if (revenueLong <= 0
                || revenueLong > int.MaxValue
                || inventory.SpiritStones > int.MaxValue - revenueLong)
            {
                return Fail(ShopFailureReason.CurrencyOverflow, true);
            }

            int revenue = (int)revenueLong;
            if (!inventory.AddSpiritStones(revenue))
            {
                return Fail(ShopFailureReason.CurrencyOverflow, true);
            }

            if (!inventory.RemoveAt(inventorySlot, count))
            {
                inventory.AddSpiritStones(-revenue);
                return Fail(ShopFailureReason.InvalidItem, true);
            }

            LastFailureReason = ShopFailureReason.None;
            PersistInventory();
            EventBus.Publish(
                ShopEvents.TransactionCompleted,
                new ShopTransactionInfo
                {
                    VendorId = ActiveVendorId,
                    Type = ShopTransactionType.Sell,
                    ItemId = item.Id,
                    Count = count,
                    SpiritStonesDelta = revenue,
                    InventorySlot = inventorySlot
                });
            PublishToast(
                SellSuccessToastKey,
                string.Format(
                    SellSuccessToastDefault,
                    item.DisplayName,
                    count,
                    revenue));
            return true;
        }

        private static NPCData GetVendor(string npcId)
        {
            NPCData npc = ConfigDatabase.Instance?.GetNpc(npcId);
            return npc != null && npc.IsVendor ? npc : null;
        }

        private static bool IsStockedByVendor(string npcId, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            NPCData vendor = GetVendor(npcId);
            if (vendor?.VendorItemIds == null)
            {
                return false;
            }

            foreach (string stockedItemId in vendor.VendorItemIds)
            {
                if (string.Equals(stockedItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Fail(ShopFailureReason reason, bool publishToast)
        {
            LastFailureReason = reason;
            if (publishToast)
            {
                switch (reason)
                {
                    case ShopFailureReason.InsufficientFunds:
                        PublishToast(
                            InsufficientFundsToastKey,
                            InsufficientFundsToastDefault);
                        break;
                    case ShopFailureReason.InventoryFull:
                        PublishToast(
                            InventoryFullToastKey,
                            InventoryFullToastDefault);
                        break;
                    case ShopFailureReason.BoundItem:
                        PublishToast(BoundItemToastKey, BoundItemToastDefault);
                        break;
                    default:
                        PublishToast(
                            TransactionFailedToastKey,
                            TransactionFailedToastDefault);
                        break;
                }
            }

            return false;
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IShopService>(out IShopService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IShopService>(this);
            _registeredService = true;
        }

        private static bool IsGameplayRunning()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private static void PersistInventory()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.ActiveSlot < 0)
            {
                return;
            }

            saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            saveManager.TrySaveModule("profile");
        }

        private static void PublishToast(string key, string defaultValue)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 2.5f
                });
        }
    }
}
