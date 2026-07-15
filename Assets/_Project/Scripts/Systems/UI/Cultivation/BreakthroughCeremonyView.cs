using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Cultivation
{
    public sealed class BreakthroughCeremonyView : MonoBehaviour
    {
        private GameObject _visualRoot;
        private Image _overlay;
        private Image _focus;
        private Text _resultLabel;
        private ICultivationService _cultivation;
        private int _lastBeat = -1;
        private bool _resultKnown;
        private bool _resultSucceeded;

        public int CurrentBeat { get; private set; }
        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;
        public string CurrentLocalizationKey { get; private set; } = string.Empty;
        public string CurrentText => _resultLabel != null
            ? _resultLabel.text
            : string.Empty;

        private void Awake()
        {
            BuildView();
            Refresh(0);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleBreakthroughSucceeded);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthroughFailed,
                HandleBreakthroughFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleBreakthroughSucceeded);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthroughFailed,
                HandleBreakthroughFailed);
        }

        private void Update()
        {
            if (_cultivation == null)
            {
                ServiceLocator.TryGet(out _cultivation);
            }

            int beat = _cultivation?.CeremonyBeat ?? 0;
            if (beat != _lastBeat)
            {
                Refresh(beat);
            }
        }

        public void Refresh(int beat)
        {
            int normalizedBeat = Mathf.Clamp(beat, 0, 5);
            if (normalizedBeat == 1 && _lastBeat != 1)
            {
                _resultKnown = false;
                _resultSucceeded = false;
                CurrentLocalizationKey = string.Empty;
            }

            CurrentBeat = normalizedBeat;
            _lastBeat = normalizedBeat;
            if (_visualRoot == null)
            {
                return;
            }

            bool visible = normalizedBeat > 0;
            _visualRoot.SetActive(visible);
            if (!visible)
            {
                _resultLabel.text = string.Empty;
                CurrentLocalizationKey = string.Empty;
                return;
            }

            ApplyBeatAppearance(normalizedBeat);
        }

        private void HandleBreakthroughSucceeded(RealmChangeInfo info)
        {
            _resultKnown = true;
            _resultSucceeded = true;
            CurrentLocalizationKey = CultivationContentIds.SuccessMessageKey;
            if (CurrentBeat >= 4)
            {
                ApplyResultText();
                ApplyBeatAppearance(CurrentBeat);
            }
        }

        private void HandleBreakthroughFailed(RealmChangeInfo info)
        {
            _resultKnown = true;
            _resultSucceeded = false;
            CurrentLocalizationKey =
                CultivationContentIds.FailureCeremonyMessageKey;
            if (CurrentBeat >= 4)
            {
                ApplyResultText();
                ApplyBeatAppearance(CurrentBeat);
            }
        }

        private void ApplyBeatAppearance(int beat)
        {
            Color overlayColor;
            Color focusColor;
            float scale;
            switch (beat)
            {
                case 1:
                    overlayColor = new Color(0.02f, 0.04f, 0.035f, 0.26f);
                    focusColor = new Color(0.72f, 0.62f, 0.35f, 0.18f);
                    scale = 0.45f;
                    break;
                case 2:
                    overlayColor = new Color(0.015f, 0.09f, 0.075f, 0.34f);
                    focusColor = new Color(0.25f, 0.82f, 0.62f, 0.28f);
                    scale = 0.68f;
                    break;
                case 3:
                    overlayColor = new Color(0.045f, 0.07f, 0.12f, 0.46f);
                    focusColor = new Color(0.58f, 0.76f, 1f, 0.42f);
                    scale = 0.95f;
                    break;
                case 4:
                    overlayColor = _resultKnown && !_resultSucceeded
                        ? new Color(0.07f, 0.015f, 0.09f, 0.56f)
                        : new Color(0.12f, 0.09f, 0.02f, 0.48f);
                    focusColor = _resultKnown && !_resultSucceeded
                        ? new Color(0.24f, 0.08f, 0.28f, 0.52f)
                        : new Color(1f, 0.78f, 0.28f, 0.56f);
                    scale = 1.08f;
                    break;
                default:
                    overlayColor = _resultKnown && !_resultSucceeded
                        ? new Color(0.045f, 0.015f, 0.055f, 0.32f)
                        : new Color(0.08f, 0.065f, 0.025f, 0.3f);
                    focusColor = _resultKnown && !_resultSucceeded
                        ? new Color(0.18f, 0.08f, 0.2f, 0.28f)
                        : new Color(0.92f, 0.75f, 0.36f, 0.3f);
                    scale = 1.18f;
                    break;
            }

            _overlay.color = overlayColor;
            _focus.color = focusColor;
            _focus.rectTransform.localScale = Vector3.one * scale;
            ApplyResultText();
        }

        private void ApplyResultText()
        {
            if (!_resultKnown || CurrentBeat < 4)
            {
                _resultLabel.text = string.Empty;
                return;
            }

            _resultLabel.text = _resultSucceeded
                ? CultivationContentIds.SuccessDefaultValue
                : CultivationContentIds.FailureCeremonyDefaultValue;
            _resultLabel.color = _resultSucceeded
                ? new Color(1f, 0.88f, 0.52f, 1f)
                : new Color(0.8f, 0.66f, 0.88f, 1f);
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "BreakthroughCeremonyCanvas",
                260);
            _visualRoot = canvas.gameObject;

            _overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "BreakthroughOverlay",
                Color.clear,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(_overlay.rectTransform);
            _overlay.raycastTarget = false;

            _focus = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "BreakthroughFocus",
                Color.clear,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(720f, 720f),
                Vector2.zero);
            _focus.raycastTarget = false;

            _resultLabel = RuntimeUiFactory.CreateText(
                canvas.transform,
                "BreakthroughResultLabel",
                string.Empty,
                46,
                Color.white,
                new Vector2(980f, 110f),
                new Vector2(0f, -250f));
            _resultLabel.raycastTarget = false;
        }
    }
}
