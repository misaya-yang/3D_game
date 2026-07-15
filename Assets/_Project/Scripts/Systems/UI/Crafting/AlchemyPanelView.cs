using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Crafting;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Tutorial;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Crafting
{
    public sealed class AlchemyPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_alchemy_title";
        public const string TitleDefaultValue = "炼丹";
        public const string HelpLocalizationKey = "ui_alchemy_help";
        public const string HelpDefaultValue = "选择丹方，备齐材料后引动丹火。";
        public const string LevelLocalizationKey = "ui_alchemy_level";
        public const string LevelDefaultValue = "丹道 {0} 级 · 熟练度 {1:0}";
        public const string RecipeRowLocalizationKey = "ui_alchemy_recipe_row";
        public const string RecipeRowDefaultValue = "{0} · 成功率 {1:P0}";
        public const string LockedLocalizationKey = "ui_alchemy_locked";
        public const string LockedDefaultValue = "{0} · 需丹道 {1} 级";
        public const string SelectionLocalizationKey = "ui_alchemy_selection";
        public const string SelectionDefaultValue = "{0}\n材料：{1}\n成功率：{2:P0}";
        public const string MissingMaterialsLocalizationKey =
            "ui_alchemy_missing_materials";
        public const string MissingMaterialsDefaultValue = "等级不足、材料欠缺或背包已满。";
        public const string CraftLocalizationKey = "ui_alchemy_craft";
        public const string CraftDefaultValue = "开炉炼制";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private readonly Button[] _recipeButtons =
            new Button[AlchemyContentIds.Recipes.Length];
        private readonly Text[] _recipeLabels =
            new Text[AlchemyContentIds.Recipes.Length];

        private CanvasGroup _canvasGroup;
        private Text _levelLabel;
        private Text _selectionLabel;
        private Text _statusLabel;
        private Button _craftButton;
        private Button _closeButton;
        private IAlchemyService _alchemy;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public string SelectedRecipeId { get; private set; } = string.Empty;
        public int RecipeButtonCount => _recipeButtons.Length;
        public string CurrentStatusLocalizationKey { get; private set; } = string.Empty;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftFailed,
                HandleCraftFailed);
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftFailed,
                HandleCraftFailed);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);

            if (IsOpen)
            {
                RestoreGameplayInput();
                ApplyOpenState(false);
            }
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
            {
                return;
            }

            ResolveServices();
            if (open && (_alchemy == null || _inventory == null))
            {
                return;
            }

            if (open)
            {
                if (ServiceLocator.TryGet<ITutorialService>(
                        out ITutorialService tutorial))
                {
                    tutorial.RequestStart(TutorialManager.AlchemyTutorialId);
                }

                bool inputAlive = IsInputAlive();
                _inputWasEnabled = inputAlive && _input.IsEnabled;
                if (inputAlive)
                {
                    _input.SetEnabled(false);
                }

                if (string.IsNullOrEmpty(SelectedRecipeId))
                {
                    SelectedRecipeId = AlchemyContentIds.Recipes[0];
                }

                CurrentStatusLocalizationKey = string.Empty;
                _statusLabel.text = string.Empty;
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

        public void SelectRecipe(string recipeId)
        {
            SelectedRecipeId = ConfigDatabase.Instance?.GetRecipe(recipeId) != null
                ? recipeId
                : string.Empty;
            RefreshSelection();
        }

        public bool TryCraftSelected()
        {
            ResolveServices();
            if (_alchemy == null
                || string.IsNullOrEmpty(SelectedRecipeId)
                || !_alchemy.CanCraft(SelectedRecipeId))
            {
                CurrentStatusLocalizationKey = MissingMaterialsLocalizationKey;
                _statusLabel.text = MissingMaterialsDefaultValue;
                RefreshSelection();
                return false;
            }

            bool success = _alchemy.Craft(SelectedRecipeId);
            Refresh();
            return success;
        }

        public void Refresh()
        {
            ResolveServices();
            _levelLabel.text = string.Format(
                LevelDefaultValue,
                _alchemy?.Level ?? 1,
                _alchemy?.Xp ?? 0f);
            for (int index = 0; index < AlchemyContentIds.Recipes.Length; index++)
            {
                CraftRecipeData recipe = ConfigDatabase.Instance?.GetRecipe(
                    AlchemyContentIds.Recipes[index]);
                bool known = recipe != null;
                _recipeButtons[index].gameObject.SetActive(known);
                if (!known)
                {
                    continue;
                }

                _recipeLabels[index].text = (_alchemy?.Level ?? 1)
                    < recipe.RequiredCraftLevel
                    ? string.Format(
                        LockedDefaultValue,
                        recipe.DisplayName,
                        recipe.RequiredCraftLevel)
                    : string.Format(
                        RecipeRowDefaultValue,
                        recipe.DisplayName,
                        _alchemy?.GetSuccessRate(recipe.Id) ?? 0f);
            }

            if (ConfigDatabase.Instance?.GetRecipe(SelectedRecipeId) == null)
            {
                SelectedRecipeId = AlchemyContentIds.Recipes[0];
            }

            RefreshSelection();
        }

        public string GetRecipeRowText(int index)
        {
            return index >= 0
                && index < _recipeLabels.Length
                && _recipeLabels[index] != null
                ? _recipeLabels[index].text
                : string.Empty;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "AlchemyCanvas",
                230);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "AlchemyOverlay",
                new Color(0.018f, 0.022f, 0.016f, 0.78f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "AlchemyPanel",
                new Color(0.09f, 0.08f, 0.055f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(900f, 820f),
                Vector2.zero);

            RuntimeUiFactory.CreateText(
                panel.transform,
                "AlchemyTitle",
                TitleDefaultValue,
                38,
                new Color(0.92f, 0.76f, 0.42f, 1f),
                new Vector2(760f, 58f),
                new Vector2(0f, 350f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "AlchemyHelp",
                HelpDefaultValue,
                20,
                new Color(0.72f, 0.78f, 0.65f, 1f),
                new Vector2(760f, 42f),
                new Vector2(0f, 304f));
            _levelLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "AlchemyLevel",
                string.Empty,
                22,
                new Color(0.9f, 0.88f, 0.72f, 1f),
                new Vector2(760f, 44f),
                new Vector2(0f, 260f));

            for (int index = 0; index < _recipeButtons.Length; index++)
            {
                int captured = index;
                Button button = CreateButton(
                    panel.transform,
                    $"AlchemyRecipe_{index + 1}",
                    string.Empty,
                    22,
                    new Vector2(700f, 58f),
                    new Vector2(0f, 190f - index * 68f));
                button.onClick.AddListener(
                    () => SelectRecipe(AlchemyContentIds.Recipes[captured]));
                _recipeButtons[index] = button;
                _recipeLabels[index] = button.GetComponentInChildren<Text>();
            }

            _selectionLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "AlchemySelection",
                string.Empty,
                21,
                new Color(0.94f, 0.92f, 0.82f, 1f),
                new Vector2(760f, 118f),
                new Vector2(0f, -110f));
            _statusLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "AlchemyStatus",
                string.Empty,
                21,
                new Color(0.88f, 0.65f, 0.38f, 1f),
                new Vector2(760f, 44f),
                new Vector2(0f, -205f));
            _craftButton = CreateButton(
                panel.transform,
                "AlchemyCraftButton",
                CraftDefaultValue,
                23,
                new Vector2(220f, 62f),
                new Vector2(-135f, -290f));
            _closeButton = CreateButton(
                panel.transform,
                "AlchemyCloseButton",
                CloseDefaultValue,
                23,
                new Vector2(220f, 62f),
                new Vector2(135f, -290f));
            _craftButton.onClick.AddListener(() => TryCraftSelected());
            _closeButton.onClick.AddListener(() => SetOpen(false));
        }

        private void RefreshSelection()
        {
            CraftRecipeData recipe = ConfigDatabase.Instance?.GetRecipe(
                SelectedRecipeId);
            if (recipe == null)
            {
                _selectionLabel.text = string.Empty;
                _craftButton.interactable = false;
                return;
            }

            var materials = new StringBuilder();
            for (int index = 0; index < recipe.Ingredients.Length; index++)
            {
                CraftIngredient ingredient = recipe.Ingredients[index];
                ItemData item = ConfigDatabase.Instance.GetItem(ingredient.ItemId);
                if (index > 0)
                {
                    materials.Append("　");
                }

                materials.Append(item?.DisplayName ?? ingredient.ItemId);
                materials.Append(' ');
                materials.Append(_inventory?.CountItem(ingredient.ItemId) ?? 0);
                materials.Append('/');
                materials.Append(ingredient.Count);
            }

            _selectionLabel.text = string.Format(
                SelectionDefaultValue,
                recipe.DisplayName,
                materials,
                _alchemy?.GetSuccessRate(recipe.Id) ?? 0f);
            _craftButton.interactable = _alchemy?.CanCraft(recipe.Id) ?? false;
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
                new Color(0.26f, 0.19f, 0.095f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.4f, 0.29f, 0.13f, 1f);
            colors.pressedColor = new Color(0.17f, 0.12f, 0.06f, 1f);
            colors.disabledColor = new Color(0.12f, 0.1f, 0.07f, 0.65f);
            button.colors = colors;
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                fontSize,
                new Color(0.96f, 0.9f, 0.72f, 1f),
                size - new Vector2(12f, 8f),
                Vector2.zero);
            return button;
        }

        private void ResolveServices()
        {
            if (_alchemy == null)
            {
                ServiceLocator.TryGet(out _alchemy);
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
                && (!(_input is UnityEngine.Object unityObject) || unityObject != null);
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    _recipeButtons[0].gameObject);
            }
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

        private void HandleCraftCompleted(CraftResultInfo info)
        {
            if (!string.Equals(
                    info.RecipeId,
                    SelectedRecipeId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ItemData item = ConfigDatabase.Instance?.GetItem(info.ResultItemId);
            CurrentStatusLocalizationKey = AlchemySystem.SuccessToastKey;
            _statusLabel.text = string.Format(
                AlchemySystem.SuccessToastDefault,
                item?.DisplayName ?? info.ResultItemId,
                info.ResultCount);
        }

        private void HandleCraftFailed(CraftResultInfo info)
        {
            if (!string.Equals(
                    info.RecipeId,
                    SelectedRecipeId,
                    StringComparison.Ordinal))
            {
                return;
            }

            CurrentStatusLocalizationKey = AlchemySystem.FailureToastKey;
            _statusLabel.text = AlchemySystem.FailureToastDefault;
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }
    }
}
