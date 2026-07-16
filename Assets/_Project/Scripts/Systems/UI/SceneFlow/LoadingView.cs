using UnityEngine;
using UnityEngine.UI;
using Wendao.Systems.World;

namespace Wendao.UI.SceneFlow
{
    public sealed class LoadingView : MonoBehaviour
    {
        public const string LoadingLocalizationKey = "ui_loading_entering_world";
        public const string LoadingDefaultValue = "正在步入仙途…";
        public const string ProgressLocalizationKey = "ui_loading_progress_percent";
        public const string ProgressDefaultFormat = "进度 {0}%";

        private SceneLoader _loader;

        public Image ProgressFill { get; private set; }
        public Text ProgressText { get; private set; }
        public float DisplayedProgress { get; private set; }

        private void Awake()
        {
            BuildView();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (_loader == null)
            {
                TryBind();
            }
        }

        private void OnDisable()
        {
            if (_loader != null)
            {
                _loader.ProgressChanged -= HandleProgressChanged;
                _loader = null;
            }
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(transform, "LoadingCanvas");

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
                "LoadingAtmosphere",
                new Color(0.012f, 0.025f, 0.022f, 0.66f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(atmosphere.rectTransform);

            Image panel = RuntimeUiFactory.CreatePanel(
                atmosphere.transform,
                "LoadingPanel",
                new Vector2(980f, 230f),
                new Vector2(0f, -315f));
            panel.color = new Color(0.22f, 0.32f, 0.26f, 0.94f);

            Text loadingLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "LoadingLabel",
                LoadingDefaultValue,
                36,
                RuntimeUiTheme.Gold,
                new Vector2(820f, 58f),
                new Vector2(0f, 68f));
            RuntimeUiTheme.StyleText(loadingLabel, RuntimeUiTextRole.Title);

            Image track = RuntimeUiFactory.CreateImage(
                panel.transform,
                "ProgressTrack",
                RuntimeUiTheme.SurfaceInset,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(800f, 34f),
                new Vector2(0f, 2f));
            RuntimeUiTheme.StylePanel(track, true);

            ProgressFill = RuntimeUiFactory.CreateImage(
                track.transform,
                "ProgressFill",
                RuntimeUiTheme.JadeBright,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(ProgressFill.rectTransform);
            ProgressFill.type = Image.Type.Filled;
            ProgressFill.fillMethod = Image.FillMethod.Horizontal;
            ProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            ProgressFill.fillAmount = 0f;

            ProgressText = RuntimeUiFactory.CreateText(
                panel.transform,
                "ProgressText",
                string.Format(ProgressDefaultFormat, 0),
                22,
                RuntimeUiTheme.Parchment,
                new Vector2(220f, 48f),
                new Vector2(0f, -62f));
            HandleProgressChanged(0f);
        }

        private void TryBind()
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader == null || ReferenceEquals(loader, _loader))
            {
                return;
            }

            if (_loader != null)
            {
                _loader.ProgressChanged -= HandleProgressChanged;
            }

            _loader = loader;
            _loader.ProgressChanged += HandleProgressChanged;
            HandleProgressChanged(_loader.Progress);
        }

        private void HandleProgressChanged(float progress)
        {
            DisplayedProgress = Mathf.Clamp01(progress);
            if (ProgressFill != null)
            {
                ProgressFill.fillAmount = DisplayedProgress;
            }

            if (ProgressText != null)
            {
                ProgressText.text = string.Format(
                    ProgressDefaultFormat,
                    Mathf.RoundToInt(DisplayedProgress * 100f));
            }
        }
    }
}
