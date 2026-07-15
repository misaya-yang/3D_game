using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Systems.Combat;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Combat
{
    public sealed class BossHealthBarView : MonoBehaviour
    {
        public const string NameLocalizationKey =
            "enemy_name_enemy_boss_stone_general";
        public const string NameDefaultValue = "黑风石将军";
        public const string PhaseLocalizationKey = "ui_boss_phase";
        public const string PhaseDefaultFormat = "第 {0} 阶段";

        private GameObject _panel;
        private Image _fill;
        private Text _nameLabel;
        private Text _phaseLabel;
        private EnemyBrain _boss;

        public bool IsVisible => _panel != null && _panel.activeSelf;
        public float DisplayedHp01 { get; private set; }
        public int DisplayedPhase { get; private set; }
        public string CurrentNameLocalizationKey => NameLocalizationKey;
        public string CurrentPhaseLocalizationKey => PhaseLocalizationKey;

        private void Awake()
        {
            BuildView();
            RefreshNow();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossPhaseInfo>(
                CombatEvents.BossPhaseChanged,
                HandleBossPhaseChanged);
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossPhaseInfo>(
                CombatEvents.BossPhaseChanged,
                HandleBossPhaseChanged);
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
        }

        private void Update()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_boss == null || _boss.IsDead)
            {
                _boss = FindBoss();
            }

            if (_boss == null || _boss.IsDead)
            {
                SetVisible(false);
                DisplayedHp01 = 0f;
                DisplayedPhase = 0;
                return;
            }

            BossArenaController arena =
                Object.FindAnyObjectByType<BossArenaController>();
            bool visible = _boss.Target != null
                || _boss.IsBossTransitioning
                || (arena != null && arena.IsPlayerInside);
            DisplayedHp01 = Mathf.Clamp01(_boss.CurrentHp / _boss.MaxHp);
            DisplayedPhase = _boss.CurrentBossPhase + 1;
            if (_fill != null)
            {
                _fill.fillAmount = DisplayedHp01;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = NameDefaultValue;
            }

            if (_phaseLabel != null)
            {
                _phaseLabel.text = string.Format(
                    PhaseDefaultFormat,
                    DisplayedPhase);
            }

            SetVisible(visible);
        }

        private void HandleBossPhaseChanged(BossPhaseInfo info)
        {
            if (info.BossId
                == Wendao.Systems.Enemy.EnemyContentIds.StoneGeneral)
            {
                RefreshNow();
            }
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (_boss != null && info.Target == _boss.gameObject)
            {
                RefreshNow();
            }
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "BossHealthCanvas",
                180);
            _panel = new GameObject("BossHealthPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(720f, 92f);
            panelRect.anchoredPosition = new Vector2(0f, -42f);

            _nameLabel = RuntimeUiFactory.CreateText(
                _panel.transform,
                "BossName",
                NameDefaultValue,
                30,
                new Color(1f, 0.9f, 0.72f, 1f),
                new Vector2(420f, 38f),
                new Vector2(-70f, -5f));
            _nameLabel.fontStyle = FontStyle.Bold;
            _phaseLabel = RuntimeUiFactory.CreateText(
                _panel.transform,
                "BossPhase",
                string.Format(PhaseDefaultFormat, 1),
                23,
                new Color(0.85f, 0.82f, 0.76f, 1f),
                new Vector2(170f, 34f),
                new Vector2(250f, -5f));

            RuntimeUiFactory.CreateImage(
                _panel.transform,
                "BossHealthBackground",
                new Color(0.08f, 0.07f, 0.07f, 0.92f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(680f, 24f),
                new Vector2(0f, -38f));
            _fill = RuntimeUiFactory.CreateImage(
                _panel.transform,
                "BossHealthFill",
                new Color(0.66f, 0.14f, 0.1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(668f, 14f),
                new Vector2(0f, -38f));
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fill.fillAmount = 1f;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null && _panel.activeSelf != visible)
            {
                _panel.SetActive(visible);
            }
        }

        private static EnemyBrain FindBoss()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index] != null
                    && enemies[index].Data != null
                    && enemies[index].Data.Id
                        == Wendao.Systems.Enemy.EnemyContentIds.StoneGeneral)
                {
                    return enemies[index];
                }
            }

            return null;
        }
    }
}
