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
                new Color(0.035f, 0.05f, 0.047f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(background.rectTransform);

            RuntimeUiFactory.CreateText(
                background.transform,
                "LoadingLabel",
                LoadingDefaultValue,
                34,
                new Color(0.86f, 0.81f, 0.61f, 1f),
                new Vector2(700f, 60f),
                new Vector2(0f, 70f));

            Image track = RuntimeUiFactory.CreateImage(
                background.transform,
                "ProgressTrack",
                new Color(0.13f, 0.17f, 0.15f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(640f, 26f),
                Vector2.zero);

            ProgressFill = RuntimeUiFactory.CreateImage(
                track.transform,
                "ProgressFill",
                new Color(0.49f, 0.66f, 0.48f, 1f),
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
                background.transform,
                "ProgressText",
                string.Format(ProgressDefaultFormat, 0),
                24,
                new Color(0.82f, 0.86f, 0.80f, 1f),
                new Vector2(220f, 48f),
                new Vector2(0f, -55f));
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
