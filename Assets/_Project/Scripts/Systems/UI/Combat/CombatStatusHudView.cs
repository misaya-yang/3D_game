using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Inventory;
using Wendao.Systems.Player;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Combat
{
    public sealed class CombatStatusHudView : MonoBehaviour
    {
        public const string PlayerNameLocalizationKey = "ui_player_default_name";
        public const string PlayerNameDefaultValue = "无名修士";
        public const string HpLocalizationKey = "ui_player_hp";
        public const string HpDefaultValue = "气血 {0:0}/{1:0}";
        public const string ManaLocalizationKey = "ui_player_mana";
        public const string ManaDefaultValue = "灵力 {0:0}/{1:0}";
        public const string CurrencyLocalizationKey = "ui_player_currency";
        public const string CurrencyDefaultValue = "灵石 {0}";

        private const float BarFollowSpeed = 5f;

        private Image _hpFill;
        private Image _manaFill;
        private Text _hpLabel;
        private Text _manaLabel;
        private Text _currencyLabel;
        private Text _nameLabel;
        private IPlayerCharacterStatsService _stats;
        private IInventoryService _inventory;
        private float _targetHp01;
        private float _targetMana01;
        private float _lastHp = float.NaN;
        private float _lastMaxHp = float.NaN;
        private float _lastMana = float.NaN;
        private float _lastMaxMana = float.NaN;
        private int _lastCurrency = int.MinValue;

        public string HpText => _hpLabel?.text ?? string.Empty;
        public string ManaText => _manaLabel?.text ?? string.Empty;
        public string CurrencyText => _currencyLabel?.text ?? string.Empty;
        public string PlayerNameText => _nameLabel?.text ?? string.Empty;
        public float HpFillAmount => _hpFill?.fillAmount ?? 0f;
        public float ManaFillAmount => _manaFill?.fillAmount ?? 0f;

        private void Awake()
        {
            BuildView();
            Refresh(true);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
            EventBus.Subscribe<HealInfo>(
                CombatEvents.PlayerHealed,
                HandlePlayerHealed);
            EventBus.Subscribe<CurrencyChangeInfo>(
                InventoryEvents.CurrencyChanged,
                HandleCurrencyChanged);
            Refresh(true);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
            EventBus.Unsubscribe<HealInfo>(
                CombatEvents.PlayerHealed,
                HandlePlayerHealed);
            EventBus.Unsubscribe<CurrencyChangeInfo>(
                InventoryEvents.CurrencyChanged,
                HandleCurrencyChanged);
        }

        private void Update()
        {
            ResolveServices();
            if (SnapshotChanged())
            {
                Refresh(false);
            }

            float speed = BarFollowSpeed * Time.unscaledDeltaTime;
            _hpFill.fillAmount = Mathf.MoveTowards(
                _hpFill.fillAmount,
                _targetHp01,
                speed);
            _manaFill.fillAmount = Mathf.MoveTowards(
                _manaFill.fillAmount,
                _targetMana01,
                speed);
        }

        public void Refresh(bool immediate = false)
        {
            ResolveServices();
            float hp = _stats?.CurrentHp ?? 0f;
            float maxHp = _stats?.MaxHp ?? 0f;
            float mana = _stats?.CurrentMana ?? 0f;
            float maxMana = _stats?.MaxMana ?? 0f;
            int currency = _inventory?.SpiritStones ?? 0;

            _hpLabel.text = string.Format(HpDefaultValue, hp, maxHp);
            _manaLabel.text = string.Format(ManaDefaultValue, mana, maxMana);
            _currencyLabel.text = string.Format(CurrencyDefaultValue, currency);
            string displayName = SaveManager.Instance?.Profile?.DisplayName;
            _nameLabel.text = string.IsNullOrWhiteSpace(displayName)
                ? PlayerNameDefaultValue
                : displayName;

            _targetHp01 = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            _targetMana01 = maxMana > 0f ? Mathf.Clamp01(mana / maxMana) : 0f;
            if (immediate)
            {
                _hpFill.fillAmount = _targetHp01;
                _manaFill.fillAmount = _targetMana01;
            }

            _lastHp = hp;
            _lastMaxHp = maxHp;
            _lastMana = mana;
            _lastMaxMana = maxMana;
            _lastCurrency = currency;
        }

        private bool SnapshotChanged()
        {
            return _stats != null
                && (!Mathf.Approximately(_stats.CurrentHp, _lastHp)
                    || !Mathf.Approximately(_stats.MaxHp, _lastMaxHp)
                    || !Mathf.Approximately(_stats.CurrentMana, _lastMana)
                    || !Mathf.Approximately(_stats.MaxMana, _lastMaxMana))
                || _inventory != null && _inventory.SpiritStones != _lastCurrency;
        }

        private void ResolveServices()
        {
            if (!IsServiceAlive(_stats))
            {
                _stats = null;
                ServiceLocator.TryGet(out _stats);
            }

            if (!IsServiceAlive(_inventory))
            {
                _inventory = null;
                ServiceLocator.TryGet(out _inventory);
            }
        }

        private static bool IsServiceAlive(object service)
        {
            return service != null
                && (!(service is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void HandlePlayerDamaged(DamageInfo info)
        {
            Refresh(false);
        }

        private void HandlePlayerHealed(HealInfo info)
        {
            Refresh(false);
        }

        private void HandleCurrencyChanged(CurrencyChangeInfo info)
        {
            Refresh(false);
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "CombatStatusHudCanvas",
                106);
            Image panel = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "CombatStatusPanel",
                new Color(0.03f, 0.065f, 0.05f, 0.9f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(470f, 170f),
                new Vector2(250f, -245f));
            _nameLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "PlayerName",
                PlayerNameDefaultValue,
                22,
                new Color(0.92f, 0.83f, 0.58f, 1f),
                new Vector2(210f, 34f),
                new Vector2(-105f, 60f));
            _currencyLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "Currency",
                string.Format(CurrencyDefaultValue, 0),
                21,
                new Color(0.95f, 0.76f, 0.37f, 1f),
                new Vector2(190f, 34f),
                new Vector2(115f, 60f));
            _hpFill = CreateBar(
                panel.transform,
                "Hp",
                new Color(0.66f, 0.16f, 0.13f, 1f),
                new Vector2(0f, 15f),
                out _hpLabel,
                string.Format(HpDefaultValue, 0f, 0f));
            _manaFill = CreateBar(
                panel.transform,
                "Mana",
                new Color(0.15f, 0.48f, 0.72f, 1f),
                new Vector2(0f, -42f),
                out _manaLabel,
                string.Format(ManaDefaultValue, 0f, 0f));
        }

        private static Image CreateBar(
            Transform parent,
            string name,
            Color fillColor,
            Vector2 position,
            out Text label,
            string defaultText)
        {
            Image background = RuntimeUiFactory.CreateImage(
                parent,
                name + "Background",
                new Color(0.07f, 0.095f, 0.08f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(420f, 40f),
                position);
            Image fill = RuntimeUiFactory.CreateImage(
                background.transform,
                name + "Fill",
                fillColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            label = RuntimeUiFactory.CreateText(
                background.transform,
                name + "Label",
                defaultText,
                19,
                new Color(0.96f, 0.94f, 0.83f, 1f),
                new Vector2(400f, 34f),
                Vector2.zero);
            return fill;
        }
    }
}
