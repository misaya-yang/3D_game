using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Shop;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Shop
{
    public sealed class ShopPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_shop_title";
        public const string TitleDefaultValue = "张掌柜 · 杂货铺";
        public const string HelpLocalizationKey = "ui_shop_help";
        public const string HelpDefaultValue = "购入补给，或将未绑定物品换作灵石。";
        public const string BalanceLocalizationKey = "ui_shop_balance";
        public const string BalanceDefaultValue = "灵石：{0}";
        public const string BuyRowLocalizationKey = "ui_shop_buy_row";
        public const string BuyRowDefaultValue = "{0}　{1}灵石";
        public const string SellRowLocalizationKey = "ui_shop_sell_row";
        public const string SellRowDefaultValue = "{0} ×{1}　卖价 {2}";
        public const string BuyHeadingLocalizationKey = "ui_shop_buy_heading";
        public const string BuyHeadingDefaultValue = "购买";
        public const string SellHeadingLocalizationKey = "ui_shop_sell_heading";
        public const string SellHeadingDefaultValue = "出售";
        public const string NoSelectionLocalizationKey = "ui_shop_no_selection";
        public const string NoSelectionDefaultValue = "选择背包物品后可出售一件。";
        public const string SelectedLocalizationKey = "ui_shop_selected";
        public const string SelectedDefaultValue = "出售：{0} · 单价 {1}";
        public const string EmptySlotLocalizationKey = "ui_inventory_empty_slot";
        public const string EmptySlotDefaultValue = "空";
        public const string SellLocalizationKey = "ui_shop_sell";
        public const string SellDefaultValue = "出售一件";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private const int StockCapacity = 3;

        private readonly Button[] _stockButtons = new Button[StockCapacity];
        private readonly Text[] _stockLabels = new Text[StockCapacity];
        private readonly Button[] _sellSlotButtons =
            new Button[InventoryManager.Capacity];
        private readonly Text[] _sellSlotLabels =
            new Text[InventoryManager.Capacity];

        private CanvasGroup _canvasGroup;
        private Text _titleLabel;
        private Text _balanceLabel;
        private Text _selectionLabel;
        private Text _statusLabel;
        private Button _sellButton;
        private Button _closeButton;
        private IShopService _shop;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;
        private int _selectedSellSlot = -1;

        public bool IsOpen { get; private set; }
        public string CurrentVendorId { get; private set; } = string.Empty;
        public string CurrentStatusLocalizationKey { get; private set; } = string.Empty;
        public int SelectedSellSlot => _selectedSellSlot;
        public int StockButtonCount => _stockButtons.Length;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ShopOpenInfo>(ShopEvents.Opened, HandleShopOpened);
            EventBus.Subscribe<ShopOpenInfo>(ShopEvents.Closed, HandleShopClosed);
            EventBus.Subscribe<ShopTransactionInfo>(
                ShopEvents.TransactionCompleted,
                HandleTransactionCompleted);
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ShopOpenInfo>(ShopEvents.Opened, HandleShopOpened);
            EventBus.Unsubscribe<ShopOpenInfo>(ShopEvents.Closed, HandleShopClosed);
            EventBus.Unsubscribe<ShopTransactionInfo>(
                ShopEvents.TransactionCompleted,
                HandleTransactionCompleted);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);

            if (IsOpen)
            {
                ResolveServices();
                _shop?.CloseVendor();
                RestoreGameplayInput();
                ApplyOpenState(false);
            }
        }

        public string GetStockRowText(int index)
        {
            return index >= 0 && index < _stockLabels.Length
                ? _stockLabels[index]?.text ?? string.Empty
                : string.Empty;
        }

        public bool TryBuyItem(string itemId, int count = 1)
        {
            ResolveServices();
            bool success = _shop != null
                && IsOpen
                && _shop.Buy(CurrentVendorId, itemId, count);
            CurrentStatusLocalizationKey = success
                ? ShopSystem.BuySuccessToastKey
                : GetFailureLocalizationKey(_shop?.LastFailureReason
                    ?? ShopFailureReason.InvalidItem);
            _statusLabel.text = success
                ? string.Empty
                : GetFailureDefaultValue(_shop?.LastFailureReason
                    ?? ShopFailureReason.InvalidItem);
            Refresh();
            return success;
        }

        public void SelectSellSlot(int slotIndex)
        {
            ResolveServices();
            _selectedSellSlot = IsSellableSlot(slotIndex) ? slotIndex : -1;
            RefreshSelection();
        }

        public bool TrySellSelected(int count = 1)
        {
            ResolveServices();
            bool success = _shop != null
                && IsOpen
                && _selectedSellSlot >= 0
                && _shop.Sell(_selectedSellSlot, count);
            CurrentStatusLocalizationKey = success
                ? ShopSystem.SellSuccessToastKey
                : GetFailureLocalizationKey(_shop?.LastFailureReason
                    ?? ShopFailureReason.InvalidItem);
            _statusLabel.text = success
                ? string.Empty
                : GetFailureDefaultValue(_shop?.LastFailureReason
                    ?? ShopFailureReason.InvalidItem);
            if (success && !IsSellableSlot(_selectedSellSlot))
            {
                _selectedSellSlot = -1;
            }

            Refresh();
            return success;
        }

        public void Close()
        {
            ResolveServices();
            if (_shop == null || !_shop.CloseVendor())
            {
                RestoreGameplayInput();
                ApplyOpenState(false);
            }
        }

        public void Refresh()
        {
            ResolveServices();
            _balanceLabel.text = string.Format(
                BalanceDefaultValue,
                _inventory?.SpiritStones ?? 0);

            NPCData vendor = ConfigDatabase.Instance?.GetNpc(CurrentVendorId);
            _titleLabel.text = TitleDefaultValue;
            string[] stock = vendor?.VendorItemIds ?? Array.Empty<string>();
            for (int index = 0; index < _stockButtons.Length; index++)
            {
                string itemId = index < stock.Length ? stock[index] : string.Empty;
                ItemData item = ConfigDatabase.Instance?.GetItem(itemId);
                int price = _shop?.GetBuyPrice(CurrentVendorId, itemId) ?? 0;
                _stockLabels[index].text = item == null
                    ? EmptySlotDefaultValue
                    : string.Format(BuyRowDefaultValue, item.DisplayName, price);
                _stockButtons[index].interactable = item != null && price > 0;
            }

            for (int index = 0; index < _sellSlotButtons.Length; index++)
            {
                InventorySlot slot = _inventory != null
                    && index < _inventory.Slots.Count
                    ? _inventory.Slots[index]
                    : null;
                ItemData item = slot == null || slot.IsEmpty
                    ? null
                    : ConfigDatabase.Instance?.GetItem(slot.ItemId);
                bool sellable = IsSellable(slot, item);
                _sellSlotLabels[index].text = item == null
                    ? EmptySlotDefaultValue
                    : string.Format(
                        SellRowDefaultValue,
                        item.DisplayName,
                        slot.Count,
                        item.SellPrice);
                _sellSlotButtons[index].interactable = sellable;
            }

            if (!IsSellableSlot(_selectedSellSlot))
            {
                _selectedSellSlot = -1;
            }

            RefreshSelection();
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "ShopCanvas",
                240);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "ShopOverlay",
                new Color(0.025f, 0.018f, 0.012f, 0.82f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "ShopPanel",
                new Color(0.105f, 0.075f, 0.04f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1360f, 960f),
                Vector2.zero);

            _titleLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "ShopTitle",
                TitleDefaultValue,
                38,
                new Color(0.95f, 0.78f, 0.42f, 1f),
                new Vector2(1120f, 58f),
                new Vector2(0f, 420f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "ShopHelp",
                HelpDefaultValue,
                20,
                new Color(0.78f, 0.72f, 0.58f, 1f),
                new Vector2(1120f, 42f),
                new Vector2(0f, 377f));
            _balanceLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "ShopBalance",
                string.Empty,
                24,
                new Color(0.96f, 0.88f, 0.58f, 1f),
                new Vector2(1120f, 44f),
                new Vector2(0f, 335f));

            RuntimeUiFactory.CreateText(
                panel.transform,
                "BuyHeading",
                BuyHeadingDefaultValue,
                28,
                new Color(0.93f, 0.81f, 0.54f, 1f),
                new Vector2(360f, 48f),
                new Vector2(-430f, 275f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "SellHeading",
                SellHeadingDefaultValue,
                28,
                new Color(0.93f, 0.81f, 0.54f, 1f),
                new Vector2(720f, 48f),
                new Vector2(260f, 275f));

            for (int index = 0; index < _stockButtons.Length; index++)
            {
                int captured = index;
                Button button = CreateButton(
                    panel.transform,
                    $"ShopStock_{index + 1}",
                    string.Empty,
                    22,
                    new Vector2(380f, 64f),
                    new Vector2(-430f, 205f - index * 78f));
                button.onClick.AddListener(() => BuyStockAt(captured));
                _stockButtons[index] = button;
                _stockLabels[index] = button.GetComponentInChildren<Text>();
            }

            var gridObject = new GameObject(
                "ShopSellGrid",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            gridObject.transform.SetParent(panel.transform, false);
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(700f, 540f);
            gridRect.anchoredPosition = new Vector2(260f, -5f);
            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(132f, 48f);
            grid.spacing = new Vector2(8f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            for (int index = 0; index < _sellSlotButtons.Length; index++)
            {
                int captured = index;
                Button button = CreateButton(
                    gridObject.transform,
                    $"ShopSellSlot_{index:00}",
                    EmptySlotDefaultValue,
                    15,
                    new Vector2(132f, 48f),
                    Vector2.zero);
                button.onClick.AddListener(() => SelectSellSlot(captured));
                _sellSlotButtons[index] = button;
                _sellSlotLabels[index] = button.GetComponentInChildren<Text>();
            }

            _selectionLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "ShopSelection",
                NoSelectionDefaultValue,
                21,
                new Color(0.94f, 0.9f, 0.76f, 1f),
                new Vector2(1140f, 46f),
                new Vector2(0f, -335f));
            _statusLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "ShopStatus",
                string.Empty,
                20,
                new Color(0.9f, 0.64f, 0.34f, 1f),
                new Vector2(1140f, 42f),
                new Vector2(0f, -375f));
            _sellButton = CreateButton(
                panel.transform,
                "ShopSellButton",
                SellDefaultValue,
                22,
                new Vector2(210f, 58f),
                new Vector2(-120f, -425f));
            _closeButton = CreateButton(
                panel.transform,
                "ShopCloseButton",
                CloseDefaultValue,
                22,
                new Vector2(210f, 58f),
                new Vector2(120f, -425f));
            _sellButton.onClick.AddListener(() => TrySellSelected());
            _closeButton.onClick.AddListener(Close);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            int fontSize,
            Vector2 size,
            Vector2 position)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.28f, 0.19f, 0.09f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.44f, 0.31f, 0.14f, 1f);
            colors.pressedColor = new Color(0.18f, 0.12f, 0.055f, 1f);
            colors.disabledColor = new Color(0.12f, 0.095f, 0.065f, 0.65f);
            button.colors = colors;
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                fontSize,
                new Color(0.97f, 0.92f, 0.78f, 1f),
                size - new Vector2(10f, 6f),
                Vector2.zero);
            return button;
        }

        private void BuyStockAt(int index)
        {
            NPCData vendor = ConfigDatabase.Instance?.GetNpc(CurrentVendorId);
            if (vendor?.VendorItemIds == null
                || index < 0
                || index >= vendor.VendorItemIds.Length)
            {
                return;
            }

            TryBuyItem(vendor.VendorItemIds[index]);
        }

        private void RefreshSelection()
        {
            ItemData item = null;
            if (_selectedSellSlot >= 0
                && _inventory != null
                && _selectedSellSlot < _inventory.Slots.Count)
            {
                item = ConfigDatabase.Instance?.GetItem(
                    _inventory.Slots[_selectedSellSlot].ItemId);
            }

            _selectionLabel.text = item == null
                ? NoSelectionDefaultValue
                : string.Format(SelectedDefaultValue, item.DisplayName, item.SellPrice);
            _sellButton.interactable = item != null;
        }

        private bool IsSellableSlot(int slotIndex)
        {
            if (_inventory == null
                || slotIndex < 0
                || slotIndex >= _inventory.Slots.Count)
            {
                return false;
            }

            InventorySlot slot = _inventory.Slots[slotIndex];
            ItemData item = slot == null || slot.IsEmpty
                ? null
                : ConfigDatabase.Instance?.GetItem(slot.ItemId);
            return IsSellable(slot, item);
        }

        private static bool IsSellable(InventorySlot slot, ItemData item)
        {
            return slot != null
                && !slot.IsEmpty
                && !slot.Bound
                && item != null
                && !item.IsBound
                && item.SellPrice > 0
                && item.Type != ItemType.Quest
                && item.Type != ItemType.Currency;
        }

        private void ResolveServices()
        {
            if (_shop == null)
            {
                ServiceLocator.TryGet(out _shop);
            }

            if (_inventory == null)
            {
                ServiceLocator.TryGet(out _inventory);
            }

            if (_input == null)
            {
                ServiceLocator.TryGet(out _input);
            }
        }

        private void HandleShopOpened(ShopOpenInfo info)
        {
            ResolveServices();
            if (_shop == null
                || !string.Equals(
                    _shop.ActiveVendorId,
                    info.VendorId,
                    StringComparison.Ordinal))
            {
                return;
            }

            CurrentVendorId = info.VendorId;
            CurrentStatusLocalizationKey = string.Empty;
            _statusLabel.text = string.Empty;
            _selectedSellSlot = -1;
            bool inputAlive = IsInputAlive();
            _inputWasEnabled = inputAlive && _input.IsEnabled;
            if (inputAlive)
            {
                _input.SetEnabled(false);
            }

            Refresh();
            ApplyOpenState(true);
            SelectFirstUiElement();
        }

        private void HandleShopClosed(ShopOpenInfo info)
        {
            if (!IsOpen
                || !string.Equals(
                    CurrentVendorId,
                    info.VendorId,
                    StringComparison.Ordinal))
            {
                return;
            }

            CurrentVendorId = string.Empty;
            _selectedSellSlot = -1;
            RestoreGameplayInput();
            ApplyOpenState(false);
            ClearUiSelection();
        }

        private void HandleTransactionCompleted(ShopTransactionInfo info)
        {
            if (IsOpen
                && string.Equals(
                    CurrentVendorId,
                    info.VendorId,
                    StringComparison.Ordinal))
            {
                Refresh();
            }
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void RestoreGameplayInput()
        {
            ResolveServices();
            GameManager gameManager = GameManager.Instance;
            bool gameAllowsInput = gameManager == null
                || gameManager.State == GameState.Playing;
            if (IsInputAlive())
            {
                _input.SetEnabled(_inputWasEnabled && gameAllowsInput);
            }
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Button target = null;
            foreach (Button button in _stockButtons)
            {
                if (button.interactable)
                {
                    target = button;
                    break;
                }
            }

            EventSystem.current.SetSelectedGameObject(
                (target != null ? target : _closeButton).gameObject);
        }

        private void ClearUiSelection()
        {
            if (EventSystem.current?.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(
                    transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }

        private static string GetFailureLocalizationKey(ShopFailureReason reason)
        {
            switch (reason)
            {
                case ShopFailureReason.InsufficientFunds:
                    return ShopSystem.InsufficientFundsToastKey;
                case ShopFailureReason.InventoryFull:
                    return ShopSystem.InventoryFullToastKey;
                case ShopFailureReason.BoundItem:
                    return ShopSystem.BoundItemToastKey;
                default:
                    return ShopSystem.TransactionFailedToastKey;
            }
        }

        private static string GetFailureDefaultValue(ShopFailureReason reason)
        {
            switch (reason)
            {
                case ShopFailureReason.InsufficientFunds:
                    return ShopSystem.InsufficientFundsToastDefault;
                case ShopFailureReason.InventoryFull:
                    return ShopSystem.InventoryFullToastDefault;
                case ShopFailureReason.BoundItem:
                    return ShopSystem.BoundItemToastDefault;
                default:
                    return ShopSystem.TransactionFailedToastDefault;
            }
        }
    }
}
