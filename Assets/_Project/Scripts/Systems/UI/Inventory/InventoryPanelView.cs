using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Inventory
{
    public sealed class InventoryPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_inventory_title";
        public const string TitleDefaultValue = "背包";
        public const string EmptySlotLocalizationKey = "ui_inventory_empty_slot";
        public const string EmptySlotDefaultValue = "空";
        public const string NoSelectionLocalizationKey = "ui_inventory_no_selection";
        public const string NoSelectionDefaultValue = "请选择物品";
        public const string SelectedLocalizationKey = "ui_inventory_selected";
        public const string SelectedDefaultValue = "已选择：{0}";
        public const string StackCountLocalizationKey = "ui_inventory_stack_count";
        public const string StackCountDefaultValue = "{0} ×{1}";
        public const string UseLocalizationKey = "ui_inventory_use";
        public const string UseDefaultValue = "使用";
        public const string EquipLocalizationKey = "ui_inventory_equip";
        public const string EquipDefaultValue = "装备";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private readonly Button[] _slotButtons = new Button[InventoryManager.Capacity];
        private readonly Text[] _slotLabels = new Text[InventoryManager.Capacity];

        private CanvasGroup _canvasGroup;
        private Text _selectionText;
        private Button _useButton;
        private Button _equipButton;
        private Button _closeButton;
        private IInventoryService _inventory;
        private IItemUseService _itemUse;
        private IEquipmentService _equipment;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;
        private int _selectedSlot = -1;

        public bool IsOpen { get; private set; }
        public int SelectedSlot => _selectedSlot;
        public int SlotButtonCount => _slotButtons.Length;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
            EventBus.Subscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Subscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
            EventBus.Unsubscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Unsubscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);

            if (IsOpen)
            {
                RestoreGameplayInput();
                ApplyOpenState(false);
            }
        }

        private void Update()
        {
            if (ServiceLocator.TryGet<IUIManager>(out _))
            {
                return;
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }

            if (IsInputAlive() && _input.OpenInventoryPressedThisFrame)
            {
                SetOpen(!IsOpen);
            }
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
            {
                return;
            }

            ResolveServices();
            if (open && _inventory == null)
            {
                return;
            }

            if (open)
            {
                bool inputAlive = IsInputAlive();
                _inputWasEnabled = inputAlive && _input.IsEnabled;
                if (inputAlive)
                {
                    _input.SetEnabled(false);
                }

                Refresh();
            }
            else
            {
                RestoreGameplayInput();
            }

            ApplyOpenState(open);
            if (open)
            {
                SelectFirstUiElement();
            }
            else
            {
                ClearUiSelection();
            }
        }

        public void SelectSlot(int slotIndex)
        {
            if (_inventory == null)
            {
                ResolveServices();
            }

            _selectedSlot = slotIndex >= 0
                && slotIndex < InventoryManager.Capacity
                && _inventory != null
                && slotIndex < _inventory.Slots.Count
                && !_inventory.Slots[slotIndex].IsEmpty
                ? slotIndex
                : -1;
            RefreshSelection();
        }

        public void Refresh()
        {
            ResolveServices();
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                InventorySlot slot = _inventory != null
                    && index < _inventory.Slots.Count
                    ? _inventory.Slots[index]
                    : null;
                ItemData item = slot == null || slot.IsEmpty
                    ? null
                    : ConfigDatabase.Instance?.GetItem(slot.ItemId);
                _slotLabels[index].text = item == null
                    ? EmptySlotDefaultValue
                    : slot.Count > 1
                        ? string.Format(
                            StackCountDefaultValue,
                            item.DisplayName,
                            slot.Count)
                        : item.DisplayName;
                _slotButtons[index].interactable = item != null;
            }

            if (_selectedSlot >= 0
                && (_inventory == null
                    || _selectedSlot >= _inventory.Slots.Count
                    || _inventory.Slots[_selectedSlot].IsEmpty))
            {
                _selectedSlot = -1;
            }

            RefreshSelection();
        }

        public string GetSlotLocalizationKey(int slotIndex)
        {
            if (_inventory == null
                || slotIndex < 0
                || slotIndex >= _inventory.Slots.Count
                || _inventory.Slots[slotIndex].IsEmpty)
            {
                return EmptySlotLocalizationKey;
            }

            return GetItemNameLocalizationKey(_inventory.Slots[slotIndex].ItemId);
        }

        public static string GetItemNameLocalizationKey(string itemId)
        {
            return string.IsNullOrEmpty(itemId)
                ? EmptySlotLocalizationKey
                : "item_name_" + itemId;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "InventoryCanvas",
                200);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "InventoryOverlay",
                new Color(0.015f, 0.025f, 0.02f, 0.72f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "InventoryPanel",
                new Color(0.065f, 0.095f, 0.08f, 0.98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(920f, 1000f),
                Vector2.zero);

            RuntimeUiFactory.CreateText(
                panel.transform,
                "InventoryTitle",
                TitleDefaultValue,
                38,
                new Color(0.88f, 0.82f, 0.62f, 1f),
                new Vector2(760f, 60f),
                new Vector2(0f, 445f));

            var gridObject = new GameObject(
                "InventoryGrid",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            gridObject.transform.SetParent(panel.transform, false);
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(790f, 620f);
            gridRect.anchoredPosition = new Vector2(0f, 50f);
            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 54f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            for (int index = 0; index < InventoryManager.Capacity; index++)
            {
                int capturedIndex = index;
                Button button = CreateButton(
                    gridObject.transform,
                    $"InventorySlot_{index:00}",
                    EmptySlotDefaultValue,
                    18,
                    new Vector2(150f, 54f),
                    Vector2.zero);
                button.onClick.AddListener(() => SelectSlot(capturedIndex));
                _slotButtons[index] = button;
                _slotLabels[index] = button.GetComponentInChildren<Text>();
            }

            _selectionText = RuntimeUiFactory.CreateText(
                panel.transform,
                "InventorySelection",
                NoSelectionDefaultValue,
                24,
                new Color(0.94f, 0.92f, 0.82f, 1f),
                new Vector2(760f, 54f),
                new Vector2(0f, -340f));

            _useButton = CreateButton(
                panel.transform,
                "UseButton",
                UseDefaultValue,
                24,
                new Vector2(180f, 62f),
                new Vector2(-220f, -420f));
            _equipButton = CreateButton(
                panel.transform,
                "EquipButton",
                EquipDefaultValue,
                24,
                new Vector2(180f, 62f),
                new Vector2(0f, -420f));
            _closeButton = CreateButton(
                panel.transform,
                "CloseButton",
                CloseDefaultValue,
                24,
                new Vector2(180f, 62f),
                new Vector2(220f, -420f));
            _useButton.onClick.AddListener(HandleUseClicked);
            _equipButton.onClick.AddListener(HandleEquipClicked);
            _closeButton.onClick.AddListener(() => SetOpen(false));
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
                new Color(0.18f, 0.3f, 0.24f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.44f, 0.34f, 1f);
            colors.pressedColor = new Color(0.11f, 0.2f, 0.15f, 1f);
            colors.disabledColor = new Color(0.1f, 0.13f, 0.11f, 0.65f);
            button.colors = colors;
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                fontSize,
                new Color(0.95f, 0.92f, 0.8f, 1f),
                size - new Vector2(12f, 8f),
                Vector2.zero);
            return button;
        }

        private void HandleUseClicked()
        {
            ResolveServices();
            if (_selectedSlot >= 0)
            {
                _itemUse?.Use(_selectedSlot);
            }

            Refresh();
        }

        private void HandleEquipClicked()
        {
            ResolveServices();
            if (_selectedSlot >= 0)
            {
                _equipment?.EquipFromInventory(_selectedSlot);
            }

            Refresh();
        }

        private void RefreshSelection()
        {
            ItemData selectedItem = null;
            if (_selectedSlot >= 0
                && _inventory != null
                && _selectedSlot < _inventory.Slots.Count)
            {
                selectedItem = ConfigDatabase.Instance?.GetItem(
                    _inventory.Slots[_selectedSlot].ItemId);
            }

            _selectionText.text = selectedItem == null
                ? NoSelectionDefaultValue
                : string.Format(
                    SelectedDefaultValue,
                    selectedItem.DisplayName);
            _useButton.interactable = selectedItem?.Type == ItemType.Consumable;
            _equipButton.interactable = selectedItem?.Type == ItemType.Equipment;
        }

        private void ResolveServices()
        {
            if (_inventory == null)
            {
                ServiceLocator.TryGet(out _inventory);
            }

            if (_itemUse == null)
            {
                ServiceLocator.TryGet(out _itemUse);
            }

            if (_equipment == null)
            {
                ServiceLocator.TryGet(out _equipment);
            }

            if (_input == null)
            {
                ServiceLocator.TryGet(out _input);
            }
        }

        private void RestoreGameplayInput()
        {
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

            Button target = _selectedSlot >= 0
                && _selectedSlot < _slotButtons.Length
                && _slotButtons[_selectedSlot].interactable
                ? _slotButtons[_selectedSlot]
                : null;
            if (target == null)
            {
                foreach (Button button in _slotButtons)
                {
                    if (button.interactable)
                    {
                        target = button;
                        break;
                    }
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

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleItemUsed(ItemUseInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleEquipmentChanged(EquipmentChangeInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }
    }
}
