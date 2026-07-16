using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Systems.Crafting;
using Wendao.Systems.Quest;
using Wendao.Systems.World;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Quest
{
    public sealed class QuestWorldMarkerView : MonoBehaviour
    {
        public const string AcceptLocalizationKey =
            "ui_quest_marker_accept";
        public const string AcceptDefaultValue = "接取 · {0}";
        public const string ObjectiveLocalizationKey =
            "ui_quest_marker_objective";
        public const string ObjectiveDefaultValue = "{0}";
        public const string TurnInLocalizationKey =
            "ui_quest_marker_turn_in";
        public const string TurnInDefaultValue = "交付 · {0}";
        public const string DistanceLocalizationKey =
            "ui_quest_marker_distance";
        public const string DistanceDefaultValue = "{0} · {1:0}米";

        private const float ResolveInterval = 0.2f;

        private CanvasGroup _canvasGroup;
        private RectTransform _markerRect;
        private Image _icon;
        private Text _label;
        private IQuestService _quests;
        private PlayerController _player;
        private Transform _target;
        private MainQuestGuidance _guidance;
        private float _resolveRemaining;

        public bool IsVisible =>
            _canvasGroup != null && _canvasGroup.alpha > 0f;
        public string TargetId => _guidance?.TargetId ?? string.Empty;
        public string LabelText => _label?.text ?? string.Empty;
        public Transform TargetTransform => _target;

        private void Awake()
        {
            BuildView();
            ApplyVisible(false);
        }

        private void Update()
        {
            _resolveRemaining -= Time.unscaledDeltaTime;
            if (_resolveRemaining <= 0f)
            {
                _resolveRemaining = ResolveInterval;
                ResolveTarget();
            }

            UpdateMarkerPosition();
        }

        public void RefreshNow()
        {
            _resolveRemaining = ResolveInterval;
            ResolveTarget();
            UpdateMarkerPosition();
        }

        private void ResolveTarget()
        {
            if (_quests == null)
            {
                ServiceLocator.TryGet(out _quests);
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerController>();
            }

            if (!MainQuestGuidanceResolver.TryResolve(
                    _quests,
                    out _guidance))
            {
                _target = null;
                ApplyVisible(false);
                return;
            }

            _target = FindTargetTransform(_guidance);
            if (_target == null)
            {
                ApplyVisible(false);
                return;
            }

            ApplyIcon(_guidance.Kind);
            UpdateLabel();
            ApplyVisible(true);
        }

        private void UpdateMarkerPosition()
        {
            Camera camera = Camera.main;
            if (!IsVisible || _target == null || camera == null)
            {
                return;
            }

            Vector3 viewport = camera.WorldToViewportPoint(
                _target.position + Vector3.up * 1.8f);
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            _markerRect.anchorMin = new Vector2(
                Mathf.Clamp(viewport.x, 0.08f, 0.92f),
                Mathf.Clamp(viewport.y, 0.13f, 0.88f));
            _markerRect.anchorMax = _markerRect.anchorMin;
            _markerRect.anchoredPosition = Vector2.zero;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_guidance?.Quest == null || _label == null)
            {
                return;
            }

            string action;
            switch (_guidance.Kind)
            {
                case MainQuestGuidanceKind.Accept:
                    action = string.Format(
                        AcceptDefaultValue,
                        _guidance.Quest.DisplayName);
                    break;
                case MainQuestGuidanceKind.TurnIn:
                    action = string.Format(
                        TurnInDefaultValue,
                        _guidance.Quest.DisplayName);
                    break;
                default:
                    action = string.Format(
                        ObjectiveDefaultValue,
                        _guidance.Objective?.Description
                            ?? _guidance.Quest.DisplayName);
                    break;
            }

            float distance = _player != null && _target != null
                ? Vector3.Distance(
                    _player.transform.position,
                    _target.position)
                : 0f;
            _label.text = string.Format(
                DistanceDefaultValue,
                action,
                distance);
        }

        private static Transform FindTargetTransform(
            MainQuestGuidance guidance)
        {
            if (guidance == null)
            {
                return null;
            }

            if (guidance.Kind == MainQuestGuidanceKind.Accept
                || guidance.Kind == MainQuestGuidanceKind.TurnIn
                || guidance.Objective?.Type == ObjectiveType.Talk)
            {
                Transform npc = FindNpc(guidance.TargetId);
                if (npc != null)
                {
                    return npc;
                }

                return FindCrossMapRoute(guidance.TargetId);
            }

            switch (guidance.Objective?.Type)
            {
                case ObjectiveType.Reach:
                    return FindReachTarget(guidance.TargetId);
                case ObjectiveType.Kill:
                    return FindEnemy(guidance.TargetId);
                case ObjectiveType.Collect:
                    return FindGatherable(guidance.TargetId);
                case ObjectiveType.Craft:
                    return FindAnyObjectByType<AlchemyFurnaceInteractable>()
                        ?.transform;
                default:
                    return null;
            }
        }

        private static Transform FindNpc(string npcId)
        {
            NPCController[] npcs = FindObjectsByType<NPCController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < npcs.Length; index++)
            {
                if (npcs[index] != null && npcs[index].NpcId == npcId)
                {
                    return npcs[index].transform;
                }
            }

            return null;
        }

        private static Transform FindReachTarget(string targetId)
        {
            WorldAreaMarker[] areas = FindObjectsByType<WorldAreaMarker>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < areas.Length; index++)
            {
                if (areas[index] != null && areas[index].AreaId == targetId)
                {
                    return areas[index].transform;
                }
            }

            if (targetId == QuestContentIds.BlackwindEntrance)
            {
                return FindAnyObjectByType<BlackwindDungeonGate>()?.transform;
            }

            return null;
        }

        private static Transform FindEnemy(string enemyId)
        {
            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            PlayerController player = FindAnyObjectByType<PlayerController>();
            EnemyBrain best = null;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyBrain enemy = enemies[index];
                if (enemy == null
                    || enemy.IsDead
                    || enemy.Data?.Id != enemyId)
                {
                    continue;
                }

                float distance = player == null
                    ? 0f
                    : Vector3.SqrMagnitude(
                        player.transform.position
                        - enemy.transform.position);
                if (best == null || distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }

            return best?.transform;
        }

        private static Transform FindGatherable(string itemId)
        {
            GatherableObject[] nodes = FindObjectsByType<GatherableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            PlayerController player = FindAnyObjectByType<PlayerController>();
            GatherableObject best = null;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < nodes.Length; index++)
            {
                GatherableObject node = nodes[index];
                if (node == null
                    || !node.IsAvailable
                    || node.ItemId != itemId)
                {
                    continue;
                }

                float distance = player == null
                    ? 0f
                    : Vector3.SqrMagnitude(
                        player.transform.position
                        - node.transform.position);
                if (best == null || distance < bestDistance)
                {
                    best = node;
                    bestDistance = distance;
                }
            }

            return best?.transform;
        }

        private static Transform FindCrossMapRoute(string targetNpcId)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == SceneLoader.DefaultMapSceneName
                && targetNpcId == QuestContentIds.CangwuGuardNpc)
            {
                return FindAnyObjectByType<CangwuPathGate>()?.transform;
            }

            if (sceneName == SceneLoader.CangwuMapSceneName
                && targetNpcId == QuestContentIds.BlackwindEchoNpc)
            {
                return FindAnyObjectByType<BlackwindDungeonGate>()?.transform;
            }

            return null;
        }

        private void ApplyIcon(MainQuestGuidanceKind kind)
        {
            string iconName;
            switch (kind)
            {
                case MainQuestGuidanceKind.Accept:
                    iconName = "exclamation";
                    _icon.color = RuntimeUiTheme.Gold;
                    break;
                case MainQuestGuidanceKind.TurnIn:
                    iconName = "checkmark";
                    _icon.color = RuntimeUiTheme.Positive;
                    break;
                default:
                    iconName = "target";
                    _icon.color = RuntimeUiTheme.JadeBright;
                    break;
            }

            _icon.sprite = RuntimeUiTheme.GetIcon(iconName);
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "QuestWorldMarkerCanvas",
                115);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image panel = RuntimeUiFactory.CreatePanel(
                canvas.transform,
                "QuestWorldMarkerPanel",
                new Vector2(350f, 68f),
                Vector2.zero,
                true);
            _markerRect = panel.rectTransform;
            _icon = RuntimeUiFactory.CreateIcon(
                panel.transform,
                "QuestWorldMarkerIcon",
                "target",
                new Vector2(38f, 38f),
                new Vector2(-140f, 0f),
                RuntimeUiTheme.JadeBright);
            _label = RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestWorldMarkerLabel",
                string.Empty,
                20,
                RuntimeUiTheme.Parchment,
                new Vector2(270f, 54f),
                new Vector2(28f, 0f));
            _label.alignment = TextAnchor.MiddleLeft;
            panel.raycastTarget = false;
        }

        private void ApplyVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
