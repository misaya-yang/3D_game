using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Feedback;
using Wendao.Systems.World;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Common
{
    public sealed class PausePanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_pause_title";
        public const string TitleDefaultValue = "静心凝神";
        public const string ContinueLocalizationKey = "ui_pause_continue";
        public const string ContinueDefaultValue = "继续游历";
        public const string SaveLocalizationKey = "ui_pause_save";
        public const string SaveDefaultValue = "保存进度";
        public const string SettingsLocalizationKey = "ui_pause_settings";
        public const string SettingsDefaultValue = "游历设置";
        public const string ExitLocalizationKey = "ui_pause_exit";
        public const string ExitDefaultValue = "返回主界面";
        public const string SaveSuccessLocalizationKey = "ui_save_success";
        public const string SaveSuccessDefaultValue = "进度已保存";
        public const string SaveUnavailableLocalizationKey = "ui_save_unavailable";
        public const string SaveUnavailableDefaultValue = "当前没有可用存档槽";
        public const string ExitConfirmLocalizationKey = "ui_exit_confirm";
        public const string ExitConfirmDefaultValue = "保存进度并返回主界面？";
        public const string SettingsTitleLocalizationKey = "ui_settings_title";
        public const string SettingsTitleDefaultValue = "游历设置";
        public const string MasterVolumeLocalizationKey = "ui_settings_master";
        public const string MasterVolumeDefaultValue = "总音量";
        public const string BgmVolumeLocalizationKey = "ui_settings_bgm";
        public const string BgmVolumeDefaultValue = "音乐";
        public const string SfxVolumeLocalizationKey = "ui_settings_sfx";
        public const string SfxVolumeDefaultValue = "音效";
        public const string DisplayModeLocalizationKey = "ui_settings_display";
        public const string DisplayModeDefaultValue = "显示模式";
        public const string WindowedLocalizationKey = "ui_settings_windowed";
        public const string WindowedDefaultValue = "窗口模式";
        public const string FullscreenLocalizationKey = "ui_settings_fullscreen";
        public const string FullscreenDefaultValue = "全屏模式";
        public const string ResetLocalizationKey = "ui_settings_reset";
        public const string ResetDefaultValue = "恢复默认";
        public const string ApplyLocalizationKey = "ui_settings_apply";
        public const string ApplyDefaultValue = "应用并返回";
        public const string SettingsSavedLocalizationKey = "ui_settings_saved";
        public const string SettingsSavedDefaultValue = "设置已应用";
        public const string SettingsSaveFailedLocalizationKey = "ui_settings_save_failed";
        public const string SettingsSaveFailedDefaultValue = "设置保存失败，请重试";
        public const string BackHintLocalizationKey = "ui_settings_back_hint";
        public const string BackHintDefaultValue = "Esc 返回 · 方向键 / 手柄可调节";
        public const string PauseHintLocalizationKey = "ui_pause_continue_hint";
        public const string PauseHintDefaultValue = "Esc 继续游历";
        public const string PercentLocalizationKey = "ui_settings_percent";
        public const string PercentDefaultValue = "{0}%";

        private CanvasGroup _canvasGroup;
        private GameObject _menuPanel;
        private GameObject _settingsPanel;
        private Button _continueButton;
        private Slider _masterSlider;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _masterValue;
        private Text _bgmValue;
        private Text _sfxValue;
        private Text _displayValue;
        private Button _displayButton;
        private GameSettingsData _originalSettings;
        private GameSettingsData _pendingSettings;

        public bool IsOpen { get; private set; }
        public bool IsSettingsOpen => IsOpen
            && _settingsPanel != null
            && _settingsPanel.activeSelf;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
            {
                return;
            }

            if (!open && IsSettingsOpen)
            {
                RestoreSettingsPreview();
            }

            ApplyOpenState(open);
            if (open)
            {
                ShowMenuPanel();
            }
            else if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void ContinueGame()
        {
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager manager))
            {
                manager.HidePanel(UiPanelIds.Pause);
            }
        }

        public bool SaveGame()
        {
            SaveManager saveManager = SaveManager.Instance;
            bool success = saveManager != null
                && saveManager.ActiveSlot >= 0
                && saveManager.SaveGame(saveManager.ActiveSlot);
            ShowToast(success ? SaveSuccessDefaultValue : SaveUnavailableDefaultValue);
            return success;
        }

        // Kept as the public command entry used by existing scene automation.
        public void ShowSettingsSummary()
        {
            _originalSettings = GameSettingsStore.LoadOrDefault();
            _pendingSettings = _originalSettings.Clone();
            SetSliderValues(_pendingSettings);
            _menuPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            RuntimeUiTheme.Focus(_masterSlider);
        }

        public bool TryCloseSubpanel()
        {
            if (!IsSettingsOpen)
            {
                return false;
            }

            RestoreSettingsPreview();
            ShowMenuPanel();
            return true;
        }

        public void ResetSettings()
        {
            _pendingSettings = new GameSettingsData();
            SetSliderValues(_pendingSettings);
            PreviewAudioSettings();
        }

        public void ToggleFullscreen()
        {
            if (_pendingSettings == null)
            {
                return;
            }

            _pendingSettings.Fullscreen = !_pendingSettings.Fullscreen;
            RefreshSettingsLabels();
        }

        public bool ApplySettings()
        {
            string error = null;
            if (_pendingSettings == null
                || !GameSettingsStore.TrySave(_pendingSettings, out error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError(error, this);
                }
                ShowToast(SettingsSaveFailedDefaultValue);
                return false;
            }

            GameSettingsRuntime.Apply(_pendingSettings, true);
            _originalSettings = _pendingSettings.Clone();
            ShowToast(SettingsSavedDefaultValue);
            ShowMenuPanel();
            return true;
        }

        public void RequestReturnToMenu()
        {
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager manager))
            {
                manager.ShowConfirm(
                    ExitConfirmDefaultValue,
                    () => SaveAndReturnToMenu());
            }
            else
            {
                SaveAndReturnToMenu();
            }
        }

        public bool SaveAndReturnToMenu()
        {
            SaveGame();
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager manager))
            {
                manager.HidePanel(UiPanelIds.Pause);
            }

            return SceneLoader.Instance != null
                && SceneLoader.Instance.LoadMainMenu();
        }

        private static void ShowToast(string message)
        {
            if (ServiceLocator.TryGet<IUIManager>(out IUIManager manager))
            {
                manager.ShowToast(message);
            }
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "PausePanelCanvas",
                500);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "PauseOverlay",
                RuntimeUiTheme.Overlay,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            _menuPanel = BuildMenuPanel(overlay.transform);
            _settingsPanel = BuildSettingsPanel(overlay.transform);
            _settingsPanel.SetActive(false);
        }

        private GameObject BuildMenuPanel(Transform parent)
        {
            Image panel = RuntimeUiFactory.CreatePanel(
                parent,
                "PausePanel",
                new Vector2(650f, 760f),
                Vector2.zero);
            Text title = RuntimeUiFactory.CreateText(
                panel.transform,
                "PauseTitle",
                TitleDefaultValue,
                48,
                RuntimeUiTheme.Gold,
                new Vector2(520f, 80f),
                new Vector2(0f, 290f));
            RuntimeUiTheme.StyleText(title, RuntimeUiTextRole.Title);

            _continueButton = RuntimeUiFactory.CreateButton(
                panel.transform,
                "ContinueButton",
                ContinueDefaultValue,
                new Vector2(430f, 82f),
                new Vector2(0f, 155f),
                true,
                "return");
            Button save = RuntimeUiFactory.CreateButton(
                panel.transform,
                "SaveButton",
                SaveDefaultValue,
                new Vector2(430f, 82f),
                new Vector2(0f, 50f),
                false,
                "save");
            Button settings = RuntimeUiFactory.CreateButton(
                panel.transform,
                "SettingsButton",
                SettingsDefaultValue,
                new Vector2(430f, 82f),
                new Vector2(0f, -55f),
                false,
                "gear");
            Button exit = RuntimeUiFactory.CreateButton(
                panel.transform,
                "ExitButton",
                ExitDefaultValue,
                new Vector2(430f, 82f),
                new Vector2(0f, -160f),
                false,
                "home");

            Text hint = RuntimeUiFactory.CreateText(
                panel.transform,
                "PauseHint",
                PauseHintDefaultValue,
                21,
                RuntimeUiTheme.Muted,
                new Vector2(480f, 44f),
                new Vector2(0f, -288f));
            RuntimeUiTheme.StyleText(hint, RuntimeUiTextRole.Muted);

            _continueButton.onClick.AddListener(ContinueGame);
            save.onClick.AddListener(() => SaveGame());
            settings.onClick.AddListener(ShowSettingsSummary);
            exit.onClick.AddListener(RequestReturnToMenu);
            return panel.gameObject;
        }

        private GameObject BuildSettingsPanel(Transform parent)
        {
            Image panel = RuntimeUiFactory.CreatePanel(
                parent,
                "SettingsPanel",
                new Vector2(880f, 800f),
                Vector2.zero);
            Text title = RuntimeUiFactory.CreateText(
                panel.transform,
                "SettingsTitle",
                SettingsTitleDefaultValue,
                46,
                RuntimeUiTheme.Gold,
                new Vector2(680f, 70f),
                new Vector2(0f, 320f));
            RuntimeUiTheme.StyleText(title, RuntimeUiTextRole.Title);

            _masterSlider = BuildSettingsSlider(
                panel.transform,
                "MasterVolumeSlider",
                MasterVolumeDefaultValue,
                175f,
                out _masterValue);
            _bgmSlider = BuildSettingsSlider(
                panel.transform,
                "BgmVolumeSlider",
                BgmVolumeDefaultValue,
                55f,
                out _bgmValue);
            _sfxSlider = BuildSettingsSlider(
                panel.transform,
                "SfxVolumeSlider",
                SfxVolumeDefaultValue,
                -65f,
                out _sfxValue);

            Text displayLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "DisplayLabel",
                DisplayModeDefaultValue,
                25,
                RuntimeUiTheme.Parchment,
                new Vector2(190f, 54f),
                new Vector2(-285f, -182f));
            displayLabel.alignment = TextAnchor.MiddleLeft;
            _displayButton = RuntimeUiFactory.CreateButton(
                panel.transform,
                "DisplayModeButton",
                WindowedDefaultValue,
                new Vector2(360f, 66f),
                new Vector2(95f, -182f),
                false,
                "menuGrid");
            _displayValue = _displayButton.GetComponentInChildren<Text>();

            Button reset = RuntimeUiFactory.CreateButton(
                panel.transform,
                "ResetSettingsButton",
                ResetDefaultValue,
                new Vector2(260f, 72f),
                new Vector2(-160f, -290f),
                false,
                "return");
            Button apply = RuntimeUiFactory.CreateButton(
                panel.transform,
                "ApplySettingsButton",
                ApplyDefaultValue,
                new Vector2(300f, 72f),
                new Vector2(160f, -290f),
                true,
                "checkmark");
            Text hint = RuntimeUiFactory.CreateText(
                panel.transform,
                "SettingsHint",
                BackHintDefaultValue,
                20,
                RuntimeUiTheme.Muted,
                new Vector2(680f, 40f),
                new Vector2(0f, -355f));
            RuntimeUiTheme.StyleText(hint, RuntimeUiTextRole.Muted);

            _masterSlider.onValueChanged.AddListener(_ => HandleSliderChanged());
            _bgmSlider.onValueChanged.AddListener(_ => HandleSliderChanged());
            _sfxSlider.onValueChanged.AddListener(_ => HandleSliderChanged());
            _displayButton.onClick.AddListener(ToggleFullscreen);
            reset.onClick.AddListener(ResetSettings);
            apply.onClick.AddListener(() => ApplySettings());
            return panel.gameObject;
        }

        private static Slider BuildSettingsSlider(
            Transform parent,
            string name,
            string label,
            float y,
            out Text valueText)
        {
            Text labelText = RuntimeUiFactory.CreateText(
                parent,
                name + "Label",
                label,
                25,
                RuntimeUiTheme.Parchment,
                new Vector2(190f, 54f),
                new Vector2(-285f, y));
            labelText.alignment = TextAnchor.MiddleLeft;
            Slider slider = RuntimeUiFactory.CreateSlider(
                parent,
                name,
                new Vector2(390f, 46f),
                new Vector2(65f, y));
            valueText = RuntimeUiFactory.CreateText(
                parent,
                name + "Value",
                "100%",
                23,
                RuntimeUiTheme.GoldSoft,
                new Vector2(110f, 50f),
                new Vector2(330f, y));
            RuntimeUiTheme.StyleText(valueText, RuntimeUiTextRole.Heading);
            return slider;
        }

        private void HandleSliderChanged()
        {
            if (_pendingSettings == null)
            {
                return;
            }

            _pendingSettings.MasterVolume = _masterSlider.value;
            _pendingSettings.BgmVolume = _bgmSlider.value;
            _pendingSettings.SfxVolume = _sfxSlider.value;
            RefreshSettingsLabels();
            PreviewAudioSettings();
        }

        private void SetSliderValues(GameSettingsData settings)
        {
            _masterSlider.SetValueWithoutNotify(settings.MasterVolume);
            _bgmSlider.SetValueWithoutNotify(settings.BgmVolume);
            _sfxSlider.SetValueWithoutNotify(settings.SfxVolume);
            RefreshSettingsLabels();
        }

        private void RefreshSettingsLabels()
        {
            _masterValue.text = FormatPercent(_masterSlider.value);
            _bgmValue.text = FormatPercent(_bgmSlider.value);
            _sfxValue.text = FormatPercent(_sfxSlider.value);
            if (_pendingSettings != null)
            {
                _displayValue.text = _pendingSettings.Fullscreen
                    ? FullscreenDefaultValue
                    : WindowedDefaultValue;
            }
        }

        private void PreviewAudioSettings()
        {
            GameSettingsRuntime.Apply(_pendingSettings, false);
        }

        private void RestoreSettingsPreview()
        {
            if (_originalSettings != null)
            {
                GameSettingsRuntime.Apply(_originalSettings, false);
            }

            _pendingSettings = null;
        }

        private void ShowMenuPanel()
        {
            _settingsPanel.SetActive(false);
            _menuPanel.SetActive(true);
            RuntimeUiTheme.Focus(_continueButton);
        }

        private static string FormatPercent(float value)
        {
            return string.Format(
                PercentDefaultValue,
                Mathf.RoundToInt(Mathf.Clamp01(value) * 100f));
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }
    }
}
