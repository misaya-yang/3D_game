using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Player;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Combat
{
    public sealed class DeathView : MonoBehaviour
    {
        public const string PanelId = "panel_death";
        public const string MessageLocalizationKey = "ui_death_revive";
        public const string MessageDefaultValue =
            "道消身陨——于最近传送阵苏醒。";
        public const string PenaltyLocalizationKey = "ui_death_xp_penalty";
        public const string PenaltyDefaultValue = "当前层修为损失 {0:0}%";
        public const string RespawnLocalizationKey = "ui_death_respawn";
        public const string RespawnDefaultValue = "于最近传送阵复生";

        private CanvasGroup _canvasGroup;
        private Text _penaltyText;
        private Button _respawnButton;
        private IPlayerRespawnService _respawnService;

        public bool IsOpen { get; private set; }
        public string CurrentMessageLocalizationKey { get; private set; } =
            MessageLocalizationKey;
        public string CurrentPenaltyLocalizationKey { get; private set; } =
            PenaltyLocalizationKey;
        public float DisplayedPenaltyPercent { get; private set; } =
            FormulaLibrary.DeathXpPenaltyPercent;
        public bool RespawnButtonInteractable =>
            _respawnButton != null && _respawnButton.interactable;

        private void Awake()
        {
            BuildView();
            SetPenaltyPercent(FormulaLibrary.DeathXpPenaltyPercent);
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            EventBus.Subscribe<PlayerRespawnInfo>(
                PlayerEvents.Respawned,
                HandlePlayerRespawned);
            EventBus.Subscribe<CultivationXpPenaltyInfo>(
                CultivationEvents.DeathXpPenaltyApplied,
                HandleDeathXpPenaltyApplied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            EventBus.Unsubscribe<PlayerRespawnInfo>(
                PlayerEvents.Respawned,
                HandlePlayerRespawned);
            EventBus.Unsubscribe<CultivationXpPenaltyInfo>(
                CultivationEvents.DeathXpPenaltyApplied,
                HandleDeathXpPenaltyApplied);
        }

        private void Update()
        {
            GameManager gameManager = GameManager.Instance;
            if (!IsOpen
                && gameManager != null
                && gameManager.State == GameState.Dead)
            {
                Show();
            }
            else if (IsOpen
                && gameManager != null
                && gameManager.State != GameState.Dead)
            {
                ApplyVisible(false);
            }

            if (!IsOpen)
            {
                return;
            }

            ResolveRespawnService();
            _respawnButton.interactable =
                _respawnService != null && _respawnService.CanRespawn;
        }

        public bool TryRespawn()
        {
            ResolveRespawnService();
            return IsOpen
                && _respawnService != null
                && _respawnService.TryRespawnAtNearestPoint();
        }

        private void Show()
        {
            ResolveRespawnService();
            _respawnButton.interactable =
                _respawnService != null && _respawnService.CanRespawn;
            ApplyVisible(true);
            if (_respawnButton.interactable)
            {
                _respawnButton.Select();
            }
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "DeathCanvas",
                500);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "DeathOverlay",
                new Color(0.015f, 0.012f, 0.012f, 0.94f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);

            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "DeathPanel",
                new Color(0.055f, 0.045f, 0.04f, 0.98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(920f, 430f),
                Vector2.zero);

            RuntimeUiFactory.CreateText(
                panel.transform,
                "DeathMessage",
                MessageDefaultValue,
                42,
                new Color(0.85f, 0.76f, 0.65f, 1f),
                new Vector2(800f, 100f),
                new Vector2(0f, 105f));
            _penaltyText = RuntimeUiFactory.CreateText(
                panel.transform,
                "DeathPenalty",
                string.Empty,
                25,
                new Color(0.72f, 0.62f, 0.54f, 1f),
                new Vector2(700f, 52f),
                new Vector2(0f, 20f));

            Image buttonImage = RuntimeUiFactory.CreateImage(
                panel.transform,
                "DeathRespawnButton",
                new Color(0.25f, 0.18f, 0.12f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 76f),
                new Vector2(0f, -105f));
            _respawnButton = buttonImage.gameObject.AddComponent<Button>();
            _respawnButton.targetGraphic = buttonImage;
            ColorBlock colors = _respawnButton.colors;
            colors.highlightedColor = new Color(0.42f, 0.31f, 0.2f, 1f);
            colors.pressedColor = new Color(0.14f, 0.09f, 0.06f, 1f);
            colors.disabledColor = new Color(0.09f, 0.075f, 0.065f, 0.75f);
            _respawnButton.colors = colors;
            _respawnButton.onClick.AddListener(() => TryRespawn());
            RuntimeUiFactory.CreateText(
                buttonImage.transform,
                "Label",
                RespawnDefaultValue,
                25,
                new Color(0.95f, 0.88f, 0.73f, 1f),
                new Vector2(340f, 62f),
                Vector2.zero);
        }

        private void ResolveRespawnService()
        {
            if (_respawnService is Object staleService
                && staleService == null)
            {
                _respawnService = null;
            }

            if (_respawnService == null)
            {
                if (ServiceLocator.TryGet(
                        out IPlayerRespawnService candidate)
                    && (!(candidate is Object candidateObject)
                        || candidateObject != null))
                {
                    _respawnService = candidate;
                }
            }
        }

        private void SetPenaltyPercent(float percent)
        {
            DisplayedPenaltyPercent = Mathf.Clamp01(percent);
            if (_penaltyText != null)
            {
                _penaltyText.text = string.Format(
                    PenaltyDefaultValue,
                    DisplayedPenaltyPercent * 100f);
            }
        }

        private void ApplyVisible(bool visible)
        {
            IsOpen = visible;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void HandlePlayerDied(DeathInfo info)
        {
            Show();
        }

        private void HandlePlayerRespawned(PlayerRespawnInfo info)
        {
            ApplyVisible(false);
        }

        private void HandleDeathXpPenaltyApplied(
            CultivationXpPenaltyInfo info)
        {
            SetPenaltyPercent(info.Percent);
        }
    }
}
