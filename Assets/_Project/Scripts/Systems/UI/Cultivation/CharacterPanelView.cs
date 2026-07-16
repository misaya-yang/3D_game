using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Player;
using Wendao.Systems.Title;
using Wendao.UI.Common;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Cultivation
{
    /// <summary>
    /// Code-first panel_character. Formal art can replace this view while
    /// retaining the service and localization contracts.
    /// </summary>
    public sealed class CharacterPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_character_title";
        public const string TitleDefaultValue = "角色";
        public const string RealmLocalizationKey = "ui_character_realm";
        public const string RealmDefaultValue = "境界：{0} 第{1}层\n修为：{2:0}/{3:0}";
        public const string SpiritRootLocalizationKey = "ui_character_spirit_root";
        public const string SpiritRootDefaultValue = "灵根：{0}";
        public const string BodyLocalizationKey = "ui_character_body";
        public const string BodyDefaultValue = "炼体：{0}\n炼体修为：{1:0}/{2:0}";
        public const string StatsLocalizationKey = "ui_character_stats";
        public const string StatsDefaultValue =
            "气血 {0:0}/{1:0}    灵力 {2:0}/{3:0}\n"
            + "攻击 {4:0.#}    防御 {5:0.#}\n"
            + "暴击 {6:P0}    暴伤 {7:P0}\n"
            + "修炼增益 {8:P0}    神识 {9:0.#}";
        public const string ActiveTitleLocalizationKey = "ui_character_active_title";
        public const string ActiveTitleDefaultValue = "称号：{0}";
        public const string NoTitleDefaultValue = "未佩戴";
        public const string BreakthroughLocalizationKey =
            "ui_character_breakthrough";
        public const string BreakthroughDefaultValue = "尝试突破";
        public const string BreakthroughReadyLocalizationKey =
            "ui_character_breakthrough_ready";
        public const string BreakthroughReadyDefaultValue =
            "气机圆满，可尝试突破（成功率 {0:P0}）";
        public const string BreakthroughBlockedLocalizationKey =
            "ui_character_breakthrough_blocked";
        public const string BreakthroughBlockedDefaultValue =
            "突破条件尚未满足，点击按钮查看详情。";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private CanvasGroup _canvasGroup;
        private Text _realmLabel;
        private Text _spiritRootLabel;
        private Text _bodyLabel;
        private Text _statsLabel;
        private Text _activeTitleLabel;
        private Text _breakthroughStatusLabel;
        private Button _breakthroughButton;
        private Button _closeButton;
        private ICultivationService _cultivation;
        private ISpiritRootService _spiritRoot;
        private IBodyRefinementService _body;
        private IPlayerCharacterStatsService _stats;
        private ITitleService _titles;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public string RealmText => _realmLabel?.text ?? string.Empty;
        public string SpiritRootText => _spiritRootLabel?.text ?? string.Empty;
        public string BodyText => _bodyLabel?.text ?? string.Empty;
        public string StatsText => _statsLabel?.text ?? string.Empty;
        public string ActiveTitleText => _activeTitleLabel?.text ?? string.Empty;
        public string BreakthroughStatusText =>
            _breakthroughStatusLabel?.text ?? string.Empty;
        public bool CanAttemptBreakthrough =>
            _breakthroughButton != null && _breakthroughButton.interactable;
        public string CurrentRealmLocalizationKey { get; private set; } =
            string.Empty;
        public string CurrentSpiritRootLocalizationKey { get; private set; } =
            string.Empty;
        public string CurrentBodyLocalizationKey { get; private set; } =
            string.Empty;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleCultivationChanged);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmChanged);
            EventBus.Subscribe<TitleInfo>(
                TitleEvents.Changed,
                HandleTitleChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleCultivationChanged);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmChanged);
            EventBus.Unsubscribe<TitleInfo>(
                TitleEvents.Changed,
                HandleTitleChanged);

            if (IsOpen)
            {
                RestoreGameplayInput();
                ApplyOpenState(false);
            }
        }

        private void Update()
        {
            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }

            if (!ServiceLocator.TryGet<IUIManager>(out _)
                && IsInputAlive()
                && _input.OpenCharacterPressedThisFrame)
            {
                SetOpen(!IsOpen);
            }

            if (IsOpen)
            {
                Refresh();
            }
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen)
            {
                return;
            }

            ResolveServices();
            if (open)
            {
                bool inputAlive = IsInputAlive();
                _inputWasEnabled = inputAlive && _input.IsEnabled;
                if (inputAlive)
                {
                    _input.SetEnabled(false);
                }

                Refresh();
            }
            else
            {
                RestoreGameplayInput();
            }

            ApplyOpenState(open);
            if (open)
            {
                SelectFirstUiElement();
            }
            else
            {
                ClearUiSelection();
            }
        }

        public void Refresh()
        {
            ResolveServices();

            RealmType realm = _cultivation?.Realm ?? RealmType.QiCondensation;
            int subStage = _cultivation?.SubStage ?? 1;
            float cultivationXp = _cultivation?.CurrentXp ?? 0f;
            float cultivationTarget = _cultivation?.XpToNext ?? 0f;
            CurrentRealmLocalizationKey = GetRealmNameLocalizationKey(realm);
            _realmLabel.text = string.Format(
                RealmDefaultValue,
                GetRealmDefaultValue(realm),
                subStage,
                cultivationXp,
                cultivationTarget);

            SpiritRootType root = _spiritRoot?.Root ?? SpiritRootType.None;
            CurrentSpiritRootLocalizationKey = GetSpiritRootNameLocalizationKey(root);
            _spiritRootLabel.text = string.Format(
                SpiritRootDefaultValue,
                GetSpiritRootDefaultValue(root));

            BodyLevel bodyLevel = _body?.Level ?? BodyLevel.Mortal;
            CurrentBodyLocalizationKey = GetBodyNameLocalizationKey(bodyLevel);
            _bodyLabel.text = string.Format(
                BodyDefaultValue,
                GetBodyDefaultValue(bodyLevel),
                _body?.Xp ?? 0f,
                _body?.XpToNext ?? 0f);

            _statsLabel.text = string.Format(
                StatsDefaultValue,
                _stats?.CurrentHp ?? 0f,
                _stats?.MaxHp ?? 0f,
                _stats?.CurrentMana ?? 0f,
                _stats?.MaxMana ?? 0f,
                _stats?.Attack ?? 0f,
                _stats?.Defense ?? 0f,
                _stats?.CritRate ?? 0f,
                _stats?.CritDamage ?? 1f,
                _stats?.CultivationSpeed ?? 0f,
                _stats?.DivineSense ?? 0f);

            TitleData activeTitle = _titles?.GetActiveTitle();
            _activeTitleLabel.text = string.Format(
                ActiveTitleDefaultValue,
                !string.IsNullOrWhiteSpace(activeTitle?.DisplayName)
                    ? activeTitle.DisplayName
                    : NoTitleDefaultValue);

            IReadOnlyList<BreakthroughBlocker> blockers =
                _cultivation?.GetBreakthroughBlockers();
            bool isReady = blockers != null && blockers.Count == 0;
            _breakthroughStatusLabel.text = isReady
                ? string.Format(
                    BreakthroughReadyDefaultValue,
                    _cultivation.GetBreakthroughSuccessRate())
                : BreakthroughBlockedDefaultValue;
            _breakthroughButton.interactable = _cultivation != null
                && !_cultivation.IsBreakthroughActive;
        }

        public bool TryBreakthrough()
        {
            ResolveServices();
            if (_cultivation == null || _cultivation.IsBreakthroughActive)
            {
                return false;
            }

            // Restore gameplay input before CultivationManager takes ownership of
            // the ceremony lock, so it can restore the pre-ceremony state exactly.
            SetOpen(false);
            return _cultivation.TryBreakthrough();
        }

        public static string GetRealmNameLocalizationKey(RealmType realm)
        {
            return CultivationHudView.GetRealmNameLocalizationKey(realm);
        }

        public static string GetSpiritRootNameLocalizationKey(SpiritRootType root)
        {
            switch (root)
            {
                case SpiritRootType.Metal: return "root_name_metal";
                case SpiritRootType.Wood: return "root_name_wood";
                case SpiritRootType.Water: return "root_name_water";
                case SpiritRootType.Fire: return "root_name_fire";
                case SpiritRootType.Earth: return "root_name_earth";
                case SpiritRootType.Heaven: return "root_name_heaven";
                case SpiritRootType.Waste: return "root_name_waste";
                default: return "root_name_none";
            }
        }

        public static string GetBodyNameLocalizationKey(BodyLevel level)
        {
            switch (level)
            {
                case BodyLevel.Copper: return "body_name_copper";
                case BodyLevel.Diamond: return "body_name_diamond";
                case BodyLevel.Immortal: return "body_name_immortal";
                case BodyLevel.Eternal: return "body_name_eternal";
                default: return "body_name_mortal";
            }
        }

        private static string GetRealmDefaultValue(RealmType realm)
        {
            RealmEntry entry = ConfigDatabase.Instance?.GetRealm((int)realm);
            return !string.IsNullOrWhiteSpace(entry?.Name)
                ? entry.Name
                : realm.ToString();
        }

        private static string GetSpiritRootDefaultValue(SpiritRootType root)
        {
            switch (root)
            {
                case SpiritRootType.Metal: return "金灵根";
                case SpiritRootType.Wood: return "木灵根";
                case SpiritRootType.Water: return "水灵根";
                case SpiritRootType.Fire: return "火灵根";
                case SpiritRootType.Earth: return "土灵根";
                case SpiritRootType.Heaven: return "天灵根";
                case SpiritRootType.Waste: return "废脉";
                default: return "未定";
            }
        }

        private static string GetBodyDefaultValue(BodyLevel level)
        {
            BodyLevelEntry[] entries = ConfigDatabase.Instance?.Body?.Levels;
            if (entries != null)
            {
                for (int index = 0; index < entries.Length; index++)
                {
                    BodyLevelEntry entry = entries[index];
                    if (entry != null
                        && entry.Level == (int)level
                        && !string.IsNullOrWhiteSpace(entry.Name))
                    {
                        return entry.Name;
                    }
                }
            }

            switch (level)
            {
                case BodyLevel.Copper: return "铜皮铁骨";
                case BodyLevel.Diamond: return "金刚不坏";
                case BodyLevel.Immortal: return "不灭金身";
                case BodyLevel.Eternal: return "万劫不灭";
                default: return "凡躯";
            }
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "CharacterCanvas",
                210);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "CharacterOverlay",
                new Color(0.01f, 0.02f, 0.018f, 0.76f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "CharacterPanel",
                new Color(0.055f, 0.09f, 0.073f, 0.985f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1000f, 880f),
                Vector2.zero);

            RuntimeUiFactory.CreateText(
                panel.transform,
                "CharacterTitle",
                TitleDefaultValue,
                42,
                new Color(0.9f, 0.82f, 0.61f, 1f),
                new Vector2(860f, 64f),
                new Vector2(0f, 382f));

            RuntimeUiFactory.CreatePanel(
                panel.transform,
                "CharacterCultivationSectionPanel",
                new Vector2(440f, 230f),
                new Vector2(-245f, 205f),
                true);
            RuntimeUiFactory.CreatePanel(
                panel.transform,
                "CharacterBodySectionPanel",
                new Vector2(440f, 230f),
                new Vector2(245f, 205f),
                true);
            RuntimeUiFactory.CreatePanel(
                panel.transform,
                "CharacterStatsSectionPanel",
                new Vector2(880f, 285f),
                new Vector2(0f, -82f),
                true);
            _realmLabel = CreateInfoText(
                panel.transform,
                "CharacterRealm",
                new Vector2(-245f, 245f),
                new Vector2(430f, 130f));
            _spiritRootLabel = CreateInfoText(
                panel.transform,
                "CharacterSpiritRoot",
                new Vector2(245f, 270f),
                new Vector2(430f, 80f));
            _bodyLabel = CreateInfoText(
                panel.transform,
                "CharacterBody",
                new Vector2(245f, 155f),
                new Vector2(430f, 130f));
            _activeTitleLabel = CreateInfoText(
                panel.transform,
                "CharacterActiveTitle",
                new Vector2(245f, 65f),
                new Vector2(430f, 54f));
            _statsLabel = CreateInfoText(
                panel.transform,
                "CharacterStats",
                new Vector2(0f, -85f),
                new Vector2(850f, 300f));
            _statsLabel.alignment = TextAnchor.UpperLeft;

            _breakthroughStatusLabel = RuntimeUiFactory.CreateText(
                panel.transform,
                "BreakthroughStatus",
                BreakthroughBlockedDefaultValue,
                22,
                new Color(0.82f, 0.88f, 0.75f, 1f),
                new Vector2(820f, 58f),
                new Vector2(0f, -252f));

            _breakthroughButton = CreateButton(
                panel.transform,
                "BreakthroughButton",
                BreakthroughDefaultValue,
                new Vector2(260f, 68f),
                new Vector2(-155f, -342f));
            _closeButton = CreateButton(
                panel.transform,
                "CharacterCloseButton",
                CloseDefaultValue,
                new Vector2(220f, 68f),
                new Vector2(170f, -342f));
            _breakthroughButton.onClick.AddListener(HandleBreakthroughClicked);
            _closeButton.onClick.AddListener(() => SetOpen(false));
        }

        private static Text CreateInfoText(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            Text text = RuntimeUiFactory.CreateText(
                parent,
                name,
                string.Empty,
                25,
                new Color(0.93f, 0.92f, 0.82f, 1f),
                size,
                position);
            text.alignment = TextAnchor.MiddleLeft;
            RuntimeUiTheme.StyleText(text, RuntimeUiTextRole.Body);
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 position)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.18f, 0.32f, 0.24f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                size,
                position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.29f, 0.47f, 0.35f, 1f);
            colors.pressedColor = new Color(0.1f, 0.2f, 0.15f, 1f);
            colors.disabledColor = new Color(0.09f, 0.12f, 0.1f, 0.7f);
            button.colors = colors;
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                25,
                new Color(0.96f, 0.93f, 0.81f, 1f),
                size - new Vector2(12f, 8f),
                Vector2.zero);
            return button;
        }

        private void HandleBreakthroughClicked()
        {
            TryBreakthrough();
        }

        private void ResolveServices()
        {
            if (!IsServiceAlive(_cultivation))
            {
                _cultivation = null;
                ServiceLocator.TryGet(out _cultivation);
            }

            if (!IsServiceAlive(_spiritRoot))
            {
                _spiritRoot = null;
                ServiceLocator.TryGet(out _spiritRoot);
            }

            if (!IsServiceAlive(_body))
            {
                _body = null;
                ServiceLocator.TryGet(out _body);
            }

            if (!IsServiceAlive(_stats))
            {
                _stats = null;
                ServiceLocator.TryGet(out _stats);
            }

            if (!IsServiceAlive(_titles))
            {
                _titles = null;
                ServiceLocator.TryGet(out _titles);
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }
        }

        private void RestoreGameplayInput()
        {
            GameManager gameManager = GameManager.Instance;
            bool gameAllowsInput = gameManager == null
                || gameManager.State == GameState.Playing;
            if (IsInputAlive())
            {
                _input.SetEnabled(_inputWasEnabled && gameAllowsInput);
            }
        }

        private bool IsInputAlive()
        {
            return IsServiceAlive(_input);
        }

        private static bool IsServiceAlive(object service)
        {
            return service != null
                && (!(service is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    (_breakthroughButton.interactable
                        ? _breakthroughButton
                        : _closeButton).gameObject);
            }
        }

        private void ClearUiSelection()
        {
            if (EventSystem.current?.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(
                    transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }

        private void HandleCultivationChanged(XpGainInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleRealmChanged(RealmChangeInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleTitleChanged(TitleInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }
    }
}
