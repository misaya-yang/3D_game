using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
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
        public const string SettingsDefaultValue = "设置";
        public const string ExitLocalizationKey = "ui_pause_exit";
        public const string ExitDefaultValue = "返回主界面";
        public const string SaveSuccessLocalizationKey = "ui_save_success";
        public const string SaveSuccessDefaultValue = "进度已保存";
        public const string SaveUnavailableLocalizationKey = "ui_save_unavailable";
        public const string SaveUnavailableDefaultValue = "当前没有可用存档槽";
        public const string SettingsMvpLocalizationKey = "ui_settings_mvp";
        public const string SettingsMvpDefaultValue = "当前使用默认画面与音量设置";
        public const string ExitConfirmLocalizationKey = "ui_exit_confirm";
        public const string ExitConfirmDefaultValue = "保存进度并返回主界面？";

        private CanvasGroup _canvasGroup;
        private Button _continueButton;

        public bool IsOpen { get; private set; }

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

            ApplyOpenState(open);
            if (open && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    _continueButton.gameObject);
            }
            else if (!open && EventSystem.current != null)
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

        public void ShowSettingsSummary()
        {
            ShowToast(SettingsMvpDefaultValue);
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
                new Color(0.008f, 0.015f, 0.012f, 0.88f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);
            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "PausePanel",
                new Color(0.045f, 0.08f, 0.062f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(600f, 720f),
                Vector2.zero);
            RuntimeUiFactory.CreateText(
                panel.transform,
                "PauseTitle",
                TitleDefaultValue,
                44,
                new Color(0.94f, 0.81f, 0.48f, 1f),
                new Vector2(500f, 70f),
                new Vector2(0f, 285f));

            _continueButton = CreateButton(
                panel.transform,
                "Continue",
                ContinueDefaultValue,
                150f);
            Button save = CreateButton(
                panel.transform,
                "Save",
                SaveDefaultValue,
                45f);
            Button settings = CreateButton(
                panel.transform,
                "Settings",
                SettingsDefaultValue,
                -60f);
            Button exit = CreateButton(
                panel.transform,
                "Exit",
                ExitDefaultValue,
                -165f);
            _continueButton.onClick.AddListener(ContinueGame);
            save.onClick.AddListener(() => SaveGame());
            settings.onClick.AddListener(ShowSettingsSummary);
            exit.onClick.AddListener(RequestReturnToMenu);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float y)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.16f, 0.34f, 0.26f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(410f, 78f),
                new Vector2(0f, y));
            Button button = image.gameObject.AddComponent<Button>();
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                27,
                new Color(0.97f, 0.92f, 0.74f, 1f),
                new Vector2(390f, 62f),
                Vector2.zero);
            return button;
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
