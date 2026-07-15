using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Skill;
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Skill
{
    public sealed class SkillPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_skill_panel_title";
        public const string TitleDefaultValue = "功法";
        public const string HelpLocalizationKey = "ui_skill_panel_help";
        public const string HelpDefaultValue = "拖动已学功法到下方快捷栏；按 K 关闭。";
        public const string LevelLocalizationKey = "ui_skill_level";
        public const string LevelDefaultValue = "第 {0} 重";
        public const string SelectionLocalizationKey = "ui_skill_selection";
        public const string SelectionDefaultValue =
            "已选：{0} · 第 {1} 重 · 升级需 {2} 页功法残页";
        public const string NoSelectionLocalizationKey = "ui_skill_no_selection";
        public const string NoSelectionDefaultValue = "尚未选择功法";
        public const string UpgradeLocalizationKey = "ui_skill_upgrade";
        public const string UpgradeDefaultValue = "参悟升级";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private readonly Button[] _skillButtons =
            new Button[SkillContentIds.All.Length];
        private readonly Text[] _skillLabels =
            new Text[SkillContentIds.All.Length];
        private readonly SkillDragSource[] _dragSources =
            new SkillDragSource[SkillContentIds.All.Length];

        private CanvasGroup _canvasGroup;
        private Text _selectionLabel;
        private Button _upgradeButton;
        private Button _closeButton;
        private ISkillService _skillService;
        private IInventoryService _inventory;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public string SelectedSkillId { get; private set; } = string.Empty;
        public int LearnedButtonCount { get; private set; }

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SkillInfo>(
                SkillEvents.SkillLearned,
                HandleSkillLearned);
            EventBus.Subscribe<SkillUpgradeInfo>(
                SkillEvents.SkillUpgraded,
                HandleSkillUpgraded);
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SkillInfo>(
                SkillEvents.SkillLearned,
                HandleSkillLearned);
            EventBus.Unsubscribe<SkillUpgradeInfo>(
                SkillEvents.SkillUpgraded,
                HandleSkillUpgraded);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);

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

            if (IsInputAlive() && _input.OpenSkillPressedThisFrame)
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
            if (open && _skillService == null)
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

        public void Refresh()
        {
            ResolveServices();
            LearnedButtonCount = 0;
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                SkillRuntime runtime = _skillService != null
                    && index < _skillService.Learned.Count
                    ? _skillService.Learned[index]
                    : null;
                SkillData skill = runtime == null
                    ? null
                    : ConfigDatabase.Instance?.GetSkill(runtime.SkillId);
                bool visible = runtime != null && skill != null;
                _skillButtons[index].gameObject.SetActive(visible);
                _dragSources[index].Configure(visible ? runtime.SkillId : string.Empty);
                if (!visible)
                {
                    continue;
                }

                _skillLabels[index].text = skill.Type == SkillType.Passive
                    ? $"{skill.DisplayName} · {string.Format(LevelDefaultValue, runtime.Level)} · 被动"
                    : $"{skill.DisplayName} · {string.Format(LevelDefaultValue, runtime.Level)}";
                LearnedButtonCount++;
            }

            if (FindRuntime(SelectedSkillId) == null)
            {
                SelectedSkillId = _skillService != null
                    && _skillService.Learned.Count > 0
                    ? _skillService.Learned[0].SkillId
                    : string.Empty;
            }

            RefreshSelection();
        }

        public void SelectSkill(string skillId)
        {
            SelectedSkillId = FindRuntime(skillId) != null
                ? skillId
                : string.Empty;
            RefreshSelection();
        }

        public bool TryUpgradeSelected()
        {
            ResolveServices();
            bool upgraded = _skillService != null
                && !string.IsNullOrEmpty(SelectedSkillId)
                && _skillService.TryUpgrade(SelectedSkillId);
            Refresh();
            return upgraded;
        }

        public SkillDragSource GetDragSource(string skillId)
        {
            for (int index = 0; index < _dragSources.Length; index++)
            {
                if (string.Equals(
                        _dragSources[index].SkillId,
                        skillId,
                        System.StringComparison.Ordinal))
                {
                    return _dragSources[index];
                }
            }

            return null;
        }

        public string GetRowText(int index)
        {
            return index >= 0
                && index < _skillLabels.Length
                && _skillLabels[index] != null
                ? _skillLabels[index].text
                : string.Empty;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "SkillPanelCanvas",
                220);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "SkillPanelOverlay",
                new Color(0.015f, 0.025f, 0.02f, 0.72f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "SkillPanel",
                new Color(0.065f, 0.095f, 0.08f, 0.98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(900f, 820f),
                new Vector2(0f, 45f));

            RuntimeUiFactory.CreateText(
                panel.transform,
                "SkillPanelTitle",
                TitleDefaultValue,
                38,
                new Color(0.88f, 0.82f, 0.62f, 1f),
                new Vector2(760f, 58f),
                new Vector2(0f, 350f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "SkillPanelHelp",
                HelpDefaultValue,
                20,
                new Color(0.7f, 0.82f, 0.72f, 1f),
                new Vector2(760f, 48f),
                new Vector2(0f, 300f));

            for (int index = 0; index < _skillButtons.Length; index++)
            {
                int capturedIndex = index;
                Button button = CreateButton(
                    panel.transform,
                    $"LearnedSkill_{index + 1}",
                    string.Empty,
                    21,
                    new Vector2(680f, 54f),
                    new Vector2(0f, 235f - index * 62f));
                Text label = button.GetComponentInChildren<Text>();
                SkillDragSource source = button.gameObject.AddComponent<SkillDragSource>();
                button.onClick.AddListener(() =>
                {
                    if (capturedIndex < _skillService.Learned.Count)
                    {
                        SelectSkill(_skillService.Learned[capturedIndex].SkillId);
                    }
                });
                _skillButtons[index] = button;
                _skillLabels[index] = label;
                _dragSources[index] = source;
            }

            _selectionLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "SkillSelection",
                NoSelectionDefaultValue,
                21,
                new Color(0.94f, 0.92f, 0.82f, 1f),
                new Vector2(760f, 50f),
                new Vector2(0f, -235f));
            _upgradeButton = CreateButton(
                panel.transform,
                "SkillUpgradeButton",
                UpgradeDefaultValue,
                23,
                new Vector2(210f, 60f),
                new Vector2(-130f, -315f));
            _closeButton = CreateButton(
                panel.transform,
                "SkillCloseButton",
                CloseDefaultValue,
                23,
                new Vector2(210f, 60f),
                new Vector2(130f, -315f));
            _upgradeButton.onClick.AddListener(() => TryUpgradeSelected());
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

        private void RefreshSelection()
        {
            SkillRuntime runtime = FindRuntime(SelectedSkillId);
            SkillData skill = runtime == null
                ? null
                : ConfigDatabase.Instance?.GetSkill(runtime.SkillId);
            if (runtime == null || skill == null)
            {
                _selectionLabel.text = NoSelectionDefaultValue;
                _upgradeButton.interactable = false;
                return;
            }

            _selectionLabel.text = string.Format(
                SelectionDefaultValue,
                skill.DisplayName,
                runtime.Level,
                runtime.Level);
            _upgradeButton.interactable = runtime.Level < Mathf.Max(1, skill.MaxLevel)
                && (_inventory?.CountItem(InventoryContentIds.SkillScroll) ?? 0)
                    >= runtime.Level;
        }

        private SkillRuntime FindRuntime(string skillId)
        {
            if (_skillService == null || string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            for (int index = 0; index < _skillService.Learned.Count; index++)
            {
                SkillRuntime runtime = _skillService.Learned[index];
                if (string.Equals(
                        runtime.SkillId,
                        skillId,
                        System.StringComparison.Ordinal))
                {
                    return runtime;
                }
            }

            return null;
        }

        private void ResolveServices()
        {
            if (_skillService == null)
            {
                ServiceLocator.TryGet(out _skillService);
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
                && (!(_input is Object unityObject) || unityObject != null);
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Button target = LearnedButtonCount > 0 ? _skillButtons[0] : _closeButton;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
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

        private void HandleSkillLearned(SkillInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleSkillUpgraded(SkillUpgradeInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (IsOpen
                && string.Equals(
                    info.ItemId,
                    InventoryContentIds.SkillScroll,
                    System.StringComparison.Ordinal))
            {
                Refresh();
            }
        }
    }
}
