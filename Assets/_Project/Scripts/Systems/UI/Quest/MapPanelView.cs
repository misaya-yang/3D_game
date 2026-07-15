using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Systems.Input;
using Wendao.Systems.World;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Quest
{
    public sealed class MapPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_map_title";
        public const string TitleDefaultValue = "山河舆图";
        public const string HelpLocalizationKey = "ui_map_help";
        public const string HelpDefaultValue = "选择已解锁传送阵前往对应地域。";
        public const string QingshiLocalizationKey = "map_name_qingshi";
        public const string QingshiDefaultValue = "青石镇传送阵";
        public const string CangwuLocalizationKey = "map_name_cangwu";
        public const string CangwuDefaultValue = "苍梧山门传送阵";
        public const string LockedLocalizationKey = "ui_map_locked";
        public const string LockedDefaultValue = "尚未解锁";
        public const string TravelLocalizationKey = "ui_map_travel";
        public const string TravelDefaultValue = "传送";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private static readonly string[] TeleportIds =
        {
            MapContentIds.QingshiTownTeleport,
            MapContentIds.CangwuGateTeleport
        };

        private static readonly string[] TeleportNames =
        {
            QingshiDefaultValue,
            CangwuDefaultValue
        };

        private readonly Button[] _teleportButtons = new Button[TeleportIds.Length];
        private readonly Text[] _teleportLabels = new Text[TeleportIds.Length];

        private CanvasGroup _canvasGroup;
        private Text _selection;
        private Button _travelButton;
        private Button _closeButton;
        private IMapTravelService _travel;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public string SelectedTeleportId { get; private set; } = string.Empty;
        public int UnlockedButtonCount { get; private set; }
        public bool CanTravel => _travelButton != null && _travelButton.interactable;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnDisable()
        {
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
            if (open && _travel == null)
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
            else if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SelectTeleport(string teleportId)
        {
            ResolveServices();
            SelectedTeleportId = _travel?.IsTeleportUnlocked(teleportId) == true
                ? teleportId
                : string.Empty;
            RefreshSelection();
        }

        public bool TryTravelSelected()
        {
            ResolveServices();
            if (_travel == null || string.IsNullOrEmpty(SelectedTeleportId))
            {
                return false;
            }

            string destination = SelectedTeleportId;
            SetOpen(false);
            bool success = _travel.Travel(destination);
            if (!success)
            {
                SetOpen(true);
            }

            return success;
        }

        public void Refresh()
        {
            ResolveServices();
            UnlockedButtonCount = 0;
            for (int index = 0; index < TeleportIds.Length; index++)
            {
                bool unlocked = _travel?.IsTeleportUnlocked(TeleportIds[index]) == true;
                _teleportLabels[index].text = unlocked
                    ? TeleportNames[index]
                    : TeleportNames[index] + " · " + LockedDefaultValue;
                _teleportButtons[index].interactable = unlocked;
                if (unlocked)
                {
                    UnlockedButtonCount++;
                }
            }

            if (_travel?.IsTeleportUnlocked(SelectedTeleportId) != true)
            {
                SelectedTeleportId = string.Empty;
                for (int index = 0; index < TeleportIds.Length; index++)
                {
                    if (_travel?.IsTeleportUnlocked(TeleportIds[index]) == true)
                    {
                        SelectedTeleportId = TeleportIds[index];
                        break;
                    }
                }
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            int selected = Array.IndexOf(TeleportIds, SelectedTeleportId);
            bool valid = selected >= 0
                && _travel?.IsTeleportUnlocked(SelectedTeleportId) == true;
            _selection.text = valid
                ? TeleportNames[selected]
                : LockedDefaultValue;
            _travelButton.interactable = valid;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "MapPanelCanvas",
                220);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "MapOverlay",
                new Color(0.012f, 0.025f, 0.022f, 0.86f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);
            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "MapPanel",
                new Color(0.055f, 0.09f, 0.07f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1060f, 760f),
                Vector2.zero);
            RuntimeUiFactory.CreateText(
                panel.transform,
                "MapTitle",
                TitleDefaultValue,
                40,
                new Color(0.92f, 0.79f, 0.5f, 1f),
                new Vector2(900f, 60f),
                new Vector2(0f, 315f));
            RuntimeUiFactory.CreateText(
                panel.transform,
                "MapHelp",
                HelpDefaultValue,
                23,
                new Color(0.8f, 0.87f, 0.76f, 1f),
                new Vector2(900f, 50f),
                new Vector2(0f, 250f));

            for (int index = 0; index < TeleportIds.Length; index++)
            {
                int captured = index;
                Image image = RuntimeUiFactory.CreateImage(
                    panel.transform,
                    "Teleport" + index,
                    new Color(0.12f, 0.26f, 0.19f, 0.96f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(700f, 90f),
                    new Vector2(0f, 130f - index * 120f));
                _teleportButtons[index] = image.gameObject.AddComponent<Button>();
                _teleportButtons[index].onClick.AddListener(
                    () => SelectTeleport(TeleportIds[captured]));
                _teleportLabels[index] = RuntimeUiFactory.CreateText(
                    image.transform,
                    "Label",
                    TeleportNames[index],
                    27,
                    new Color(0.94f, 0.91f, 0.76f, 1f),
                    new Vector2(660f, 70f),
                    Vector2.zero);
            }

            _selection = RuntimeUiFactory.CreateText(
                panel.transform,
                "MapSelection",
                LockedDefaultValue,
                24,
                new Color(0.56f, 0.9f, 0.7f, 1f),
                new Vector2(650f, 55f),
                new Vector2(0f, -140f));
            _travelButton = CreateButton(
                panel.transform,
                "MapTravel",
                TravelDefaultValue,
                new Vector2(-155f, -245f));
            _travelButton.onClick.AddListener(() => TryTravelSelected());
            _closeButton = CreateButton(
                panel.transform,
                "MapClose",
                CloseDefaultValue,
                new Vector2(155f, -245f));
            _closeButton.onClick.AddListener(() => SetOpen(false));
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
                new Vector2(250f, 70f),
                position);
            Button button = image.gameObject.AddComponent<Button>();
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                24,
                new Color(0.97f, 0.92f, 0.74f, 1f),
                new Vector2(230f, 56f),
                Vector2.zero);
            return button;
        }

        private void ResolveServices()
        {
            if (_travel == null)
            {
                ServiceLocator.TryGet(out _travel);
            }

            if (!IsInputAlive())
            {
                _input = null;
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

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            for (int index = 0; index < _teleportButtons.Length; index++)
            {
                if (_teleportButtons[index].interactable)
                {
                    EventSystem.current.SetSelectedGameObject(
                        _teleportButtons[index].gameObject);
                    return;
                }
            }

            EventSystem.current.SetSelectedGameObject(_closeButton.gameObject);
        }
    }
}
