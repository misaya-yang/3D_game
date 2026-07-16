using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wendao.Data;
using Wendao.Systems.World;

namespace Wendao.UI.SceneFlow
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_main_menu_title";
        public const string TitleDefaultValue = "问道长生";
        public const string StartGameLocalizationKey = "ui_main_menu_start_game";
        public const string StartGameDefaultValue = "踏入仙途";
        public const string SubtitleLocalizationKey = "ui_main_menu_subtitle";
        public const string SubtitleDefaultValue = "一念入山海 · 一剑问长生";
        public const string StartHintLocalizationKey = "ui_main_menu_start_hint";
        public const string StartHintDefaultValue = "鼠标点击 / Enter / 手柄 A 确认";
        public const string StartingLocalizationKey = "ui_main_menu_starting";
        public const string StartingDefaultValue = "正在入境……";
        public const string StartUnavailableLocalizationKey =
            "ui_main_menu_start_unavailable";
        public const string StartUnavailableDefaultValue =
            "暂时无法进入，请稍候重试。";

        public Button StartButton { get; private set; }

        private Text _statusText;
        private bool _startRequested;

        private void Awake()
        {
            BuildView();
        }

        private void OnDestroy()
        {
            if (StartButton != null)
            {
                StartButton.onClick.RemoveListener(HandleStartClicked);
            }
        }

        private void Update()
        {
            if (_startRequested || StartButton == null || !StartButton.interactable)
            {
                return;
            }

            bool keyboardSubmit = Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
            bool gamepadSubmit = Gamepad.current != null
                && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (keyboardSubmit || gamepadSubmit)
            {
                HandleStartClicked();
            }
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(transform, "MainMenuCanvas");

            Image background = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "Background",
                Color.white,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(background.rectTransform);
            background.sprite = RuntimeUiTheme.MainMenuBackground;
            background.type = Image.Type.Simple;

            Image atmosphere = RuntimeUiFactory.CreateImage(
                background.transform,
                "AtmosphereOverlay",
                new Color(0.018f, 0.035f, 0.03f, 0.38f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(atmosphere.rectTransform);
            atmosphere.raycastTarget = false;

            Image panel = RuntimeUiFactory.CreatePanel(
                background.transform,
                "MainMenuPanel",
                new Vector2(670f, 650f),
                new Vector2(-470f, 0f));
            panel.color = new Color(0.07f, 0.12f, 0.10f, 0.9f);

            Text title = RuntimeUiFactory.CreateText(
                panel.transform,
                "Title",
                TitleDefaultValue,
                78,
                RuntimeUiTheme.Gold,
                new Vector2(560f, 118f),
                new Vector2(0f, 175f));
            RuntimeUiTheme.StyleText(title, RuntimeUiTextRole.Title);

            Text subtitle = RuntimeUiFactory.CreateText(
                panel.transform,
                "Subtitle",
                SubtitleDefaultValue,
                27,
                RuntimeUiTheme.GoldSoft,
                new Vector2(540f, 64f),
                new Vector2(0f, 90f));
            RuntimeUiTheme.StyleText(subtitle, RuntimeUiTextRole.Heading);

            StartButton = RuntimeUiFactory.CreateButton(
                panel.transform,
                "StartGameButton",
                StartGameDefaultValue,
                new Vector2(390f, 96f),
                new Vector2(0f, -35f),
                true,
                "target");
            StartButton.onClick.AddListener(HandleStartClicked);

            Text hint = RuntimeUiFactory.CreateText(
                panel.transform,
                "StartHint",
                StartHintDefaultValue,
                22,
                RuntimeUiTheme.Muted,
                new Vector2(540f, 52f),
                new Vector2(0f, -115f));
            RuntimeUiTheme.StyleText(hint, RuntimeUiTextRole.Muted);

            _statusText = RuntimeUiFactory.CreateText(
                panel.transform,
                "Status",
                string.Empty,
                23,
                RuntimeUiTheme.GoldSoft,
                new Vector2(540f, 72f),
                new Vector2(0f, -205f));
            RuntimeUiTheme.StyleText(_statusText, RuntimeUiTextRole.Accent);

            RuntimeUiTheme.Focus(StartButton);
        }

        private void HandleStartClicked()
        {
            if (StartButton == null || !StartButton.interactable)
            {
                return;
            }

            SceneLoader loader = SceneLoader.Instance;
            bool saveReady = EnsureDefaultSaveReady();
            bool started = loader != null
                && saveReady
                && loader.LoadMap(SceneLoader.DefaultMapId, string.Empty);
            if (started)
            {
                Debug.Log("Main menu accepted the start request.", this);
                _startRequested = true;
                StartButton.interactable = false;
                _statusText.text = StartingDefaultValue;
                return;
            }

            Debug.LogError(
                "Main menu rejected the start request. "
                + $"SaveReady={saveReady}; "
                + $"SaveError={SaveManager.Instance?.LastError ?? "unavailable"}; "
                + $"SceneError={loader?.LastError ?? "unavailable"}.",
                this);
            _statusText.text = StartUnavailableDefaultValue;
            RuntimeUiTheme.StyleText(_statusText, RuntimeUiTextRole.Warning);
            RuntimeUiTheme.Focus(StartButton);
        }

        private static bool EnsureDefaultSaveReady()
        {
            const int defaultSlot = 0;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                return false;
            }

            if (saveManager.ActiveSlot >= 0)
            {
                return true;
            }

            SaveMetadata metadata = saveManager.GetMetadata(defaultSlot);
            if (metadata.Exists)
            {
                return !metadata.IsCorrupted && saveManager.LoadGame(defaultSlot);
            }

            return saveManager.SaveGame(defaultSlot);
        }
    }
}
