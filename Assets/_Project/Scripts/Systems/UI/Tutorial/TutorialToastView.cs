using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Tutorial;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Tutorial
{
    /// <summary>
    /// Backwards-compatible view name upgraded from a toast to a four-piece
    /// cutout overlay. The transparent center remains a real input hole.
    /// </summary>
    public sealed class TutorialToastView : MonoBehaviour
    {
        public const string DismissLocalizationKey = "ui_tutorial_dismiss";
        public const string DismissDefaultValue = "知道了";

        private readonly Image[] _maskPieces = new Image[4];
        private readonly Image[] _focusBorder = new Image[4];

        private CanvasGroup _canvasGroup;
        private Text _message;
        private Button _dismissButton;
        private float _remaining;
        private bool _autoHide;

        public string CurrentLocalizationKey { get; private set; } = string.Empty;
        public string CurrentDefaultValue { get; private set; } = string.Empty;
        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;
        public bool IsForcedPrompt { get; private set; }
        public bool CanDismiss { get; private set; }
        public Rect CurrentFocusRectNormalized { get; private set; }
        public int VisibleMaskCount { get; private set; }

        private void Awake()
        {
            BuildView();
            Hide();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TutorialPromptInfo>(
                TutorialManager.TutorialPromptedEvent,
                HandlePrompted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TutorialPromptInfo>(
                TutorialManager.TutorialPromptedEvent,
                HandlePrompted);
        }

        private void Update()
        {
            if (!IsVisible || !_autoHide)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                Hide();
            }
        }

        public bool DismissCurrent()
        {
            if (!CanDismiss
                || !ServiceLocator.TryGet<ITutorialService>(
                    out ITutorialService tutorial)
                || !tutorial.DismissCurrent())
            {
                return false;
            }

            return true;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "TutorialOverlayCanvas",
                400);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            for (int index = 0; index < _maskPieces.Length; index++)
            {
                _maskPieces[index] = RuntimeUiFactory.CreateImage(
                    canvas.transform,
                    "TutorialMask" + index,
                    new Color(0.005f, 0.012f, 0.009f, 0.82f),
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
            }

            for (int index = 0; index < _focusBorder.Length; index++)
            {
                _focusBorder[index] = RuntimeUiFactory.CreateImage(
                    canvas.transform,
                    "TutorialFocusBorder" + index,
                    new Color(0.78f, 0.91f, 0.55f, 0.98f),
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                _focusBorder[index].raycastTarget = false;
            }

            Image panel = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "TutorialPromptPanel",
                new Color(0.055f, 0.09f, 0.072f, 0.98f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(920f, 118f),
                new Vector2(0f, -92f));
            panel.raycastTarget = false;

            _message = RuntimeUiFactory.CreateText(
                panel.transform,
                "TutorialPromptMessage",
                string.Empty,
                28,
                new Color(0.96f, 0.94f, 0.84f, 1f),
                new Vector2(690f, 92f),
                new Vector2(-90f, 0f));
            _message.raycastTarget = false;

            Image dismissImage = RuntimeUiFactory.CreateImage(
                panel.transform,
                "TutorialDismiss",
                new Color(0.19f, 0.39f, 0.29f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(170f, 64f),
                new Vector2(340f, 0f));
            _dismissButton = dismissImage.gameObject.AddComponent<Button>();
            _dismissButton.onClick.AddListener(() => DismissCurrent());
            Text dismissLabel = RuntimeUiFactory.CreateText(
                dismissImage.transform,
                "Label",
                DismissDefaultValue,
                23,
                new Color(0.98f, 0.93f, 0.74f, 1f),
                new Vector2(150f, 50f),
                Vector2.zero);
            dismissLabel.raycastTarget = false;
        }

        private void HandlePrompted(TutorialPromptInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.LocalizationKey)
                || string.IsNullOrWhiteSpace(info.DefaultValue))
            {
                return;
            }

            CurrentLocalizationKey = info.LocalizationKey;
            CurrentDefaultValue = info.DefaultValue;
            IsForcedPrompt = info.IsForced;
            CanDismiss = info.CanDismiss;
            CurrentFocusRectNormalized = NormalizeRect(info.FocusRectNormalized);
            _message.text = info.DefaultValue;
            _autoHide = info.Duration > 0f;
            _remaining = _autoHide ? Mathf.Max(0.1f, info.Duration) : 0f;
            bool showMask = info.StepId != TutorialManager.CompleteStepId;
            ApplyFocus(CurrentFocusRectNormalized, showMask, info.IsForced);
            _dismissButton.gameObject.SetActive(info.CanDismiss);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = info.CanDismiss;
            _canvasGroup.blocksRaycasts = info.IsForced || info.CanDismiss;
        }

        private void ApplyFocus(Rect focus, bool visible, bool blocksInput)
        {
            VisibleMaskCount = visible ? _maskPieces.Length : 0;
            for (int index = 0; index < _maskPieces.Length; index++)
            {
                _maskPieces[index].gameObject.SetActive(visible);
                _maskPieces[index].raycastTarget = visible && blocksInput;
                _focusBorder[index].gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            float left = focus.xMin;
            float right = focus.xMax;
            float bottom = focus.yMin;
            float top = focus.yMax;
            SetAnchors(_maskPieces[0].rectTransform, 0f, 0f, left, 1f);
            SetAnchors(_maskPieces[1].rectTransform, right, 0f, 1f, 1f);
            SetAnchors(_maskPieces[2].rectTransform, left, 0f, right, bottom);
            SetAnchors(_maskPieces[3].rectTransform, left, top, right, 1f);

            const float border = 0.004f;
            SetAnchors(
                _focusBorder[0].rectTransform,
                left - border,
                bottom - border,
                left + border,
                top + border);
            SetAnchors(
                _focusBorder[1].rectTransform,
                right - border,
                bottom - border,
                right + border,
                top + border);
            SetAnchors(
                _focusBorder[2].rectTransform,
                left - border,
                bottom - border,
                right + border,
                bottom + border);
            SetAnchors(
                _focusBorder[3].rectTransform,
                left - border,
                top - border,
                right + border,
                top + border);
        }

        private static Rect NormalizeRect(Rect rect)
        {
            float width = Mathf.Clamp(rect.width, 0.05f, 0.95f);
            float height = Mathf.Clamp(rect.height, 0.05f, 0.95f);
            float x = Mathf.Clamp(rect.x, 0f, 1f - width);
            float y = Mathf.Clamp(rect.y, 0f, 1f - height);
            return new Rect(x, y, width, height);
        }

        private static void SetAnchors(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(
                Mathf.Clamp01(minX),
                Mathf.Clamp01(minY));
            rect.anchorMax = new Vector2(
                Mathf.Clamp01(maxX),
                Mathf.Clamp01(maxY));
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Hide()
        {
            _remaining = 0f;
            _autoHide = false;
            IsForcedPrompt = false;
            CanDismiss = false;
            CurrentFocusRectNormalized = Rect.zero;
            VisibleMaskCount = 0;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
