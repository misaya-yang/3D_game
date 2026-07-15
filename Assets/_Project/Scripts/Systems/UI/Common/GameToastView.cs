using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Common
{
    public sealed class GameToastView : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private Text _message;
        private float _remaining;

        public string CurrentLocalizationKey { get; private set; } = string.Empty;
        public string CurrentDefaultValue { get; private set; } = string.Empty;
        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        private void Awake()
        {
            BuildView();
            Hide();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ToastInfo>(UiEvents.ToastRequested, HandleToastRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ToastInfo>(UiEvents.ToastRequested, HandleToastRequested);
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                Hide();
            }
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "GameToastCanvas",
                500);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image panel = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "GameToastPanel",
                new Color(0.11f, 0.08f, 0.055f, 0.95f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(680f, 76f),
                new Vector2(0f, 120f));
            _message = RuntimeUiFactory.CreateText(
                panel.transform,
                "GameToastMessage",
                string.Empty,
                25,
                new Color(0.96f, 0.9f, 0.72f, 1f),
                new Vector2(640f, 62f),
                Vector2.zero);
        }

        private void HandleToastRequested(ToastInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.LocalizationKey)
                || string.IsNullOrWhiteSpace(info.DefaultValue))
            {
                return;
            }

            CurrentLocalizationKey = info.LocalizationKey;
            CurrentDefaultValue = info.DefaultValue;
            _message.text = info.DefaultValue;
            _remaining = Mathf.Max(0.1f, info.Duration);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void Hide()
        {
            _remaining = 0f;
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
