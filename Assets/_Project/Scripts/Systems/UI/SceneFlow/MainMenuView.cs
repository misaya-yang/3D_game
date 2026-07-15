using UnityEngine;
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

        public Button StartButton { get; private set; }

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

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(transform, "MainMenuCanvas");

            Image background = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "Background",
                new Color(0.055f, 0.075f, 0.07f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(background.rectTransform);

            RuntimeUiFactory.CreateText(
                background.transform,
                "Title",
                TitleDefaultValue,
                64,
                new Color(0.86f, 0.81f, 0.61f, 1f),
                new Vector2(720f, 100f),
                new Vector2(0f, 150f));

            Image buttonImage = RuntimeUiFactory.CreateImage(
                background.transform,
                "StartGameButton",
                new Color(0.25f, 0.38f, 0.31f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(300f, 72f),
                new Vector2(0f, -30f));
            StartButton = buttonImage.gameObject.AddComponent<Button>();
            StartButton.targetGraphic = buttonImage;
            ColorBlock colors = StartButton.colors;
            colors.highlightedColor = new Color(0.36f, 0.52f, 0.42f, 1f);
            colors.pressedColor = new Color(0.18f, 0.28f, 0.23f, 1f);
            colors.disabledColor = new Color(0.15f, 0.18f, 0.16f, 0.7f);
            StartButton.colors = colors;

            RuntimeUiFactory.CreateText(
                buttonImage.transform,
                "Label",
                StartGameDefaultValue,
                30,
                new Color(0.96f, 0.94f, 0.84f, 1f),
                new Vector2(280f, 60f),
                Vector2.zero);
            StartButton.onClick.AddListener(HandleStartClicked);
        }

        private void HandleStartClicked()
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader != null
                && EnsureDefaultSaveReady()
                && loader.LoadMap(SceneLoader.DefaultMapId, string.Empty))
            {
                StartButton.interactable = false;
            }
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
