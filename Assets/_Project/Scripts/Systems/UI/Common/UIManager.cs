using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems;
using Wendao.Systems.Input;
using Wendao.Systems.NPC;
using Wendao.Systems.Shop;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Crafting;
using Wendao.UI.Cultivation;
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.SceneFlow;
using Wendao.UI.Shop;
using Wendao.UI.Skill;

namespace Wendao.UI.Common
{
    /// <summary>
    /// Scene-local coordinator for panel navigation. Gameplay systems remain
    /// service-driven; this view only owns UI priority, cancellation and the
    /// aggregate gameplay-input lock.
    /// </summary>
    public sealed class UIManager : MonoBehaviour, IUIManager
    {
        public const string ToastLocalizationKey = "ui_runtime_message";
        public const string ConfirmYesLocalizationKey = "ui_common_confirm";
        public const string ConfirmYesDefaultValue = "确认";
        public const string ConfirmNoLocalizationKey = "ui_common_cancel";
        public const string ConfirmNoDefaultValue = "取消";

        private static readonly string[] ClosePriority =
        {
            UiPanelIds.Shop,
            UiPanelIds.Alchemy,
            UiPanelIds.Quest,
            UiPanelIds.Map,
            UiPanelIds.Skill,
            UiPanelIds.Character,
            UiPanelIds.Inventory
        };

        private readonly Dictionary<string, bool> _knownOpen =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private IPlayerInputSource _input;
        private bool _registered;
        private bool _observedOpenPanel;
        private bool _ownsPause;
        private CanvasGroup _confirmGroup;
        private Text _confirmMessage;
        private Action _confirmYes;
        private Action _confirmNo;

        public bool HasOpenPanel
        {
            get
            {
                for (int index = 0; index < ClosePriority.Length; index++)
                {
                    if (IsPanelOpen(ClosePriority[index]))
                    {
                        return true;
                    }
                }

                return IsPanelOpen(UiPanelIds.Pause) || IsConfirmOpen;
            }
        }

        public bool IsConfirmOpen =>
            _confirmGroup != null && _confirmGroup.alpha > 0.5f;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IUIManager>(this);
            _registered = true;
            BuildConfirmView();
            SnapshotOpenPanels();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
        }

        private void OnDestroy()
        {
            if (_registered
                && ServiceLocator.TryGet<IUIManager>(out IUIManager current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IUIManager>();
            }

            _registered = false;
            if (_ownsPause && Time.timeScale <= 0.0001f)
            {
                Time.timeScale = 1f;
            }

            _ownsPause = false;
        }

        private void Update()
        {
            ResolveInput();
            if (IsInputAlive())
            {
                if (_input.OpenInventoryPressedThisFrame)
                {
                    TogglePanel(UiPanelIds.Inventory);
                }
                else if (_input.OpenCharacterPressedThisFrame)
                {
                    TogglePanel(UiPanelIds.Character);
                }
                else if (_input.OpenSkillPressedThisFrame)
                {
                    TogglePanel(UiPanelIds.Skill);
                }
                else if (_input.OpenQuestPressedThisFrame)
                {
                    TogglePanel(UiPanelIds.Quest);
                }
                else if (_input.OpenMapPressedThisFrame)
                {
                    TogglePanel(UiPanelIds.Map);
                }
                else if (_input.PausePressedThisFrame)
                {
                    HandleCancel();
                }
            }

            ReconcileExternalPanelOpen();
            ReconcileGameplayInput();
        }

        public void ShowPanel(string panelId)
        {
            if (string.IsNullOrEmpty(panelId) || IsPanelOpen(panelId))
            {
                return;
            }

            if (panelId == UiPanelIds.Pause)
            {
                ShowPause();
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            HideAllExcept(panelId);
            ApplyPanelState(panelId, true);
            ReconcileGameplayInput();
            SnapshotOpenPanels();
        }

        public void HidePanel(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
            {
                return;
            }

            if (panelId == UiPanelIds.Pause)
            {
                HidePause();
            }
            else
            {
                ApplyPanelState(panelId, false);
            }

            ReconcileGameplayInput();
            SnapshotOpenPanels();
        }

        public void HideAllPanels()
        {
            CloseConfirm(false);
            for (int index = 0; index < ClosePriority.Length; index++)
            {
                ApplyPanelState(ClosePriority[index], false);
            }

            HidePause();
            ReconcileGameplayInput();
            SnapshotOpenPanels();
        }

        public bool IsPanelOpen(string panelId)
        {
            switch (panelId)
            {
                case UiPanelIds.Inventory:
                    return Find<InventoryPanelView>()?.IsOpen == true;
                case UiPanelIds.Character:
                    return Find<CharacterPanelView>()?.IsOpen == true;
                case UiPanelIds.Skill:
                    return Find<SkillPanelView>()?.IsOpen == true;
                case UiPanelIds.Quest:
                    return Find<QuestPanelView>()?.IsOpen == true;
                case UiPanelIds.Map:
                    return Find<MapPanelView>()?.IsOpen == true;
                case UiPanelIds.Alchemy:
                    return Find<AlchemyPanelView>()?.IsOpen == true;
                case UiPanelIds.Shop:
                    return Find<ShopPanelView>()?.IsOpen == true;
                case UiPanelIds.Pause:
                    return Find<PausePanelView>()?.IsOpen == true;
                default:
                    return false;
            }
        }

        public void TogglePanel(string panelId)
        {
            if (IsPanelOpen(panelId))
            {
                HidePanel(panelId);
            }
            else
            {
                ShowPanel(panelId);
            }
        }

        public bool CloseTopPanel()
        {
            if (IsConfirmOpen)
            {
                CloseConfirm(false);
                return true;
            }

            for (int index = 0; index < ClosePriority.Length; index++)
            {
                string panelId = ClosePriority[index];
                if (!IsPanelOpen(panelId))
                {
                    continue;
                }

                HidePanel(panelId);
                return true;
            }

            return false;
        }

        public void HandleCancel()
        {
            if (IsConfirmOpen)
            {
                CloseTopPanel();
                return;
            }

            if (IsPanelOpen(UiPanelIds.Pause))
            {
                HidePanel(UiPanelIds.Pause);
                return;
            }

            if (CloseTopPanel())
            {
                return;
            }

            ShowPanel(UiPanelIds.Pause);
        }

        public void ShowToast(string message, float duration = 2f)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = ToastLocalizationKey,
                    DefaultValue = message ?? string.Empty,
                    Duration = Mathf.Max(0.1f, duration)
                });
        }

        public void ShowConfirm(
            string message,
            Action onYes,
            Action onNo = null)
        {
            _confirmMessage.text = message ?? string.Empty;
            _confirmYes = onYes;
            _confirmNo = onNo;
            SetCanvasGroup(_confirmGroup, true);
            ReconcileGameplayInput();
        }

        public void SetHudVisible(bool visible)
        {
            SetViewEnabled<CombatStatusHudView>(visible);
            SetViewEnabled<CultivationHudView>(visible);
            SetViewEnabled<SkillQuickbarView>(visible);
            SetViewEnabled<QuestTrackerView>(visible);
            SetViewEnabled<BossHealthBarView>(visible);
        }

        private void ShowPause()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                return;
            }

            for (int index = 0; index < ClosePriority.Length; index++)
            {
                ApplyPanelState(ClosePriority[index], false);
            }

            if (!gameManager.TrySetState(GameState.Paused))
            {
                return;
            }

            Time.timeScale = 0f;
            _ownsPause = true;
            Find<PausePanelView>()?.SetOpen(true);
        }

        private void HidePause()
        {
            PausePanelView pause = Find<PausePanelView>();
            if (pause?.IsOpen == true)
            {
                pause.SetOpen(false);
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Paused)
            {
                gameManager.TrySetState(GameState.Playing);
            }

            if (_ownsPause && Time.timeScale <= 0.0001f)
            {
                Time.timeScale = 1f;
            }

            _ownsPause = false;
        }

        private void ApplyPanelState(string panelId, bool open)
        {
            switch (panelId)
            {
                case UiPanelIds.Inventory:
                    Find<InventoryPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Character:
                    Find<CharacterPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Skill:
                    Find<SkillPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Quest:
                    Find<QuestPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Map:
                    Find<MapPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Alchemy:
                    Find<AlchemyPanelView>()?.SetOpen(open);
                    break;
                case UiPanelIds.Shop:
                    if (!open)
                    {
                        Find<ShopPanelView>()?.Close();
                    }

                    break;
            }
        }

        private void HideAllExcept(string panelId)
        {
            CloseConfirm(false);
            for (int index = 0; index < ClosePriority.Length; index++)
            {
                string current = ClosePriority[index];
                if (!string.Equals(current, panelId, StringComparison.Ordinal))
                {
                    ApplyPanelState(current, false);
                }
            }
        }

        private void ReconcileExternalPanelOpen()
        {
            string winner = string.Empty;
            for (int index = 0; index < ClosePriority.Length; index++)
            {
                string panelId = ClosePriority[index];
                bool open = IsPanelOpen(panelId);
                bool wasOpen = _knownOpen.TryGetValue(panelId, out bool known)
                    && known;
                if (open && !wasOpen && string.IsNullOrEmpty(winner))
                {
                    winner = panelId;
                }
            }

            if (!string.IsNullOrEmpty(winner))
            {
                HideAllExcept(winner);
            }

            SnapshotOpenPanels();
        }

        private void ReconcileGameplayInput()
        {
            ResolveInput();
            if (!IsInputAlive())
            {
                return;
            }

            bool shouldLock = HasOpenPanel;
            if (shouldLock)
            {
                _observedOpenPanel = true;
                if (_input.IsEnabled)
                {
                    _input.SetEnabled(false);
                }

                return;
            }

            if (!_observedOpenPanel)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            bool canRestore = gameManager == null
                || gameManager.State == GameState.Playing;
            if (canRestore && !_input.IsEnabled)
            {
                _input.SetEnabled(true);
            }

            _observedOpenPanel = false;
        }

        private void SnapshotOpenPanels()
        {
            for (int index = 0; index < ClosePriority.Length; index++)
            {
                string panelId = ClosePriority[index];
                _knownOpen[panelId] = IsPanelOpen(panelId);
            }
        }

        private void ResolveInput()
        {
            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void HandleDialogueStarted(DialogueInfo info)
        {
            HideAllPanels();
        }

        private void BuildConfirmView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "ConfirmCanvas",
                360);
            _confirmGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "ConfirmOverlay",
                new Color(0.01f, 0.018f, 0.014f, 0.72f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);
            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "ConfirmPanel",
                new Color(0.05f, 0.09f, 0.07f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(680f, 300f),
                Vector2.zero);
            _confirmMessage = RuntimeUiFactory.CreateText(
                panel.transform,
                "ConfirmMessage",
                string.Empty,
                28,
                new Color(0.92f, 0.9f, 0.78f, 1f),
                new Vector2(590f, 140f),
                new Vector2(0f, 42f));
            Button yes = CreateButton(
                panel.transform,
                "ConfirmYes",
                ConfirmYesDefaultValue,
                new Vector2(-150f, -90f));
            Button no = CreateButton(
                panel.transform,
                "ConfirmNo",
                ConfirmNoDefaultValue,
                new Vector2(150f, -90f));
            yes.onClick.AddListener(() => CloseConfirm(true));
            no.onClick.AddListener(() => CloseConfirm(false));
            SetCanvasGroup(_confirmGroup, false);
        }

        private void CloseConfirm(bool accepted)
        {
            if (!IsConfirmOpen)
            {
                return;
            }

            Action callback = accepted ? _confirmYes : _confirmNo;
            _confirmYes = null;
            _confirmNo = null;
            SetCanvasGroup(_confirmGroup, false);
            callback?.Invoke();
            ReconcileGameplayInput();
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.18f, 0.38f, 0.29f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(220f, 70f),
                position);
            Button button = image.gameObject.AddComponent<Button>();
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                24,
                new Color(0.97f, 0.92f, 0.74f, 1f),
                new Vector2(200f, 56f),
                Vector2.zero);
            return button;
        }

        private static void SetCanvasGroup(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static T Find<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindAnyObjectByType<T>(
                FindObjectsInactive.Include);
        }

        private static void SetViewEnabled<T>(bool visible)
            where T : Behaviour
        {
            T view = Find<T>();
            if (view != null)
            {
                view.gameObject.SetActive(visible);
            }
        }
    }
}
