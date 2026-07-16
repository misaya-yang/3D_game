using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Cultivation
{
    public sealed class CultivationHudView : MonoBehaviour
    {
        public const string RealmStageLocalizationKey = "ui_cultivation_realm_stage";
        public const string RealmStageDefaultValue = "{0} 第{1}层";
        public const string XpLocalizationKey = "ui_cultivation_xp";
        public const string XpDefaultValue = "修为 {0:0}/{1:0}";

        private Image _xpFill;
        private Text _realmLabel;
        private Text _xpLabel;
        private ICultivationService _cultivation;
        private RealmType _lastRealm;
        private int _lastSubStage = -1;
        private float _lastXp = -1f;
        private float _lastXpToNext = -1f;

        public string RealmText => _realmLabel != null
            ? _realmLabel.text
            : string.Empty;
        public string XpText => _xpLabel != null ? _xpLabel.text : string.Empty;
        public float FillAmount => _xpFill != null ? _xpFill.fillAmount : 0f;
        public string CurrentRealmLocalizationKey { get; private set; } = string.Empty;

        private void Awake()
        {
            BuildView();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Subscribe<CultivationXpPenaltyInfo>(
                CultivationEvents.DeathXpPenaltyApplied,
                HandleXpPenaltyApplied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Unsubscribe<CultivationXpPenaltyInfo>(
                CultivationEvents.DeathXpPenaltyApplied,
                HandleXpPenaltyApplied);
        }

        private void Update()
        {
            if (_cultivation == null)
            {
                ServiceLocator.TryGet(out _cultivation);
            }

            if (_cultivation != null
                && (_cultivation.Realm != _lastRealm
                    || _cultivation.SubStage != _lastSubStage
                    || !Mathf.Approximately(_cultivation.CurrentXp, _lastXp)
                    || !Mathf.Approximately(_cultivation.XpToNext, _lastXpToNext)))
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            ServiceLocator.TryGet(out _cultivation);
            if (_cultivation == null)
            {
                _realmLabel.text = string.Empty;
                _xpLabel.text = string.Format(XpDefaultValue, 0f, 0f);
                _xpFill.fillAmount = 0f;
                return;
            }

            RealmEntry realm = ConfigDatabase.Instance?.GetRealm(
                (int)_cultivation.Realm);
            string realmName = realm?.Name ?? _cultivation.Realm.ToString();
            CurrentRealmLocalizationKey = GetRealmNameLocalizationKey(
                _cultivation.Realm);
            _realmLabel.text = string.Format(
                RealmStageDefaultValue,
                realmName,
                _cultivation.SubStage);
            _xpLabel.text = string.Format(
                XpDefaultValue,
                _cultivation.CurrentXp,
                _cultivation.XpToNext);
            _xpFill.fillAmount = _cultivation.XpToNext > 0f
                ? Mathf.Clamp01(_cultivation.CurrentXp / _cultivation.XpToNext)
                : 0f;

            _lastRealm = _cultivation.Realm;
            _lastSubStage = _cultivation.SubStage;
            _lastXp = _cultivation.CurrentXp;
            _lastXpToNext = _cultivation.XpToNext;
        }

        public static string GetRealmNameLocalizationKey(RealmType realm)
        {
            switch (realm)
            {
                case RealmType.QiCondensation:
                    return "realm_name_qi_condensation";
                case RealmType.Foundation:
                    return "realm_name_foundation";
                case RealmType.GoldenCore:
                    return "realm_name_golden_core";
                case RealmType.NascentSoul:
                    return "realm_name_nascent_soul";
                default:
                    return "realm_name_mortal";
            }
        }

        private void HandleXpGained(XpGainInfo info)
        {
            Refresh();
        }

        private void HandleXpPenaltyApplied(CultivationXpPenaltyInfo info)
        {
            Refresh();
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "CultivationHudCanvas",
                105);
            Image panel = RuntimeUiFactory.CreatePanel(
                canvas.transform,
                "CultivationHudPanel",
                new Vector2(438f, 104f),
                Vector2.zero);
            panel.rectTransform.anchorMin = new Vector2(0f, 1f);
            panel.rectTransform.anchorMax = new Vector2(0f, 1f);
            panel.rectTransform.anchoredPosition = new Vector2(235f, -72f);
            panel.color = new Color(0.08f, 0.14f, 0.115f, 0.88f);
            _realmLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "CultivationRealmLabel",
                string.Empty,
                22,
                RuntimeUiTheme.GoldSoft,
                new Vector2(392f, 34f),
                new Vector2(0f, 29f));
            _realmLabel.alignment = TextAnchor.MiddleLeft;
            RuntimeUiTheme.StyleText(_realmLabel, RuntimeUiTextRole.Heading);

            Image barBackground = RuntimeUiFactory.CreateImage(
                panel.transform,
                "CultivationXpBackground",
                RuntimeUiTheme.SurfaceInset,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(392f, 25f),
                new Vector2(0f, -8f));
            RuntimeUiTheme.StylePanel(barBackground, true);
            _xpFill = RuntimeUiFactory.CreateImage(
                barBackground.transform,
                "CultivationXpFill",
                RuntimeUiTheme.Jade,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(_xpFill.rectTransform);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;
            _xpFill.fillOrigin = 0;
            _xpFill.fillAmount = 0f;

            _xpLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "CultivationXpLabel",
                string.Format(XpDefaultValue, 0f, 0f),
                17,
                RuntimeUiTheme.Muted,
                new Vector2(392f, 28f),
                new Vector2(0f, -35f));
            _xpLabel.alignment = TextAnchor.MiddleRight;
            RuntimeUiTheme.StyleText(_xpLabel, RuntimeUiTextRole.Muted);
        }
    }
}
