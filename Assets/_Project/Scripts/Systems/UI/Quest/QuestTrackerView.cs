using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Quest;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Quest
{
    public sealed class QuestTrackerView : MonoBehaviour
    {
        public const string EmptyLocalizationKey = "ui_quest_tracker_empty";
        public const string EmptyDefaultValue = "暂无追踪任务";
        public const string ProgressLocalizationKey = "ui_quest_progress";
        public const string ProgressDefaultValue = "{0} {1}/{2}";
        public const string ReadyTurnInLocalizationKey =
            "ui_quest_ready_turn_in";
        public const string ReadyTurnInDefaultValue = "目标已完成，可以交付";
        public const string AcceptGuidanceLocalizationKey =
            "ui_quest_guidance_accept";
        public const string AcceptGuidanceDefaultValue =
            "前往{0}交谈，接取主线";
        public const string TurnInGuidanceLocalizationKey =
            "ui_quest_guidance_turn_in";
        public const string TurnInGuidanceDefaultValue =
            "返回{0}交付任务";

        private CanvasGroup _canvasGroup;
        private Text _questName;
        private Text _objective;
        private IQuestService _quests;
        private string _lastSignature = string.Empty;

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;
        public string CurrentQuestId { get; private set; } = string.Empty;
        public string QuestText => _questName?.text ?? string.Empty;
        public string ObjectiveText => _objective?.text ?? string.Empty;
        public string CurrentQuestNameLocalizationKey { get; private set; } =
            string.Empty;
        public string CurrentObjectiveLocalizationKey { get; private set; } =
            string.Empty;
        public MainQuestGuidanceKind CurrentGuidanceKind { get; private set; } =
            MainQuestGuidanceKind.None;

        private void Awake()
        {
            BuildView();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<QuestInfo>(
                QuestEvents.Accepted,
                HandleQuestChanged);
            EventBus.Subscribe<QuestProgressInfo>(
                QuestEvents.Progressed,
                HandleQuestProgressed);
            EventBus.Subscribe<QuestInfo>(
                QuestEvents.Completed,
                HandleQuestChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestInfo>(
                QuestEvents.Accepted,
                HandleQuestChanged);
            EventBus.Unsubscribe<QuestProgressInfo>(
                QuestEvents.Progressed,
                HandleQuestProgressed);
            EventBus.Unsubscribe<QuestInfo>(
                QuestEvents.Completed,
                HandleQuestChanged);
        }

        private void Update()
        {
            if (_quests == null)
            {
                ServiceLocator.TryGet(out _quests);
            }

            string signature = BuildSignature();
            if (signature != _lastSignature)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            ServiceLocator.TryGet(out _quests);
            if (_quests == null)
            {
                ShowEmpty();
                return;
            }

            if (MainQuestGuidanceResolver.TryResolve(
                    _quests,
                    out MainQuestGuidance guidance))
            {
                ShowMainGuidance(guidance);
                _lastSignature = BuildSignature();
                return;
            }

            if (_quests.ActiveIds.Count == 0)
            {
                ShowEmpty();
                return;
            }

            CurrentQuestId = _quests.ActiveIds[0];
            CurrentGuidanceKind = MainQuestGuidanceKind.Objective;
            QuestData quest = _quests.GetQuestData(CurrentQuestId);
            if (quest == null)
            {
                ApplyVisible(false);
                return;
            }

            ApplyVisible(true);
            CurrentQuestNameLocalizationKey = quest.DisplayNameKey ?? string.Empty;
            _questName.text = quest.DisplayName ?? string.Empty;
            if (_quests.GetStatus(CurrentQuestId) == QuestStatus.Completed)
            {
                CurrentObjectiveLocalizationKey = ReadyTurnInLocalizationKey;
                _objective.text = ReadyTurnInDefaultValue;
            }
            else
            {
                ShowFirstIncompleteObjective(quest);
            }

            _lastSignature = BuildSignature();
        }

        private void ShowMainGuidance(MainQuestGuidance guidance)
        {
            QuestData quest = guidance?.Quest;
            if (quest == null)
            {
                ShowEmpty();
                return;
            }

            ApplyVisible(true);
            CurrentQuestId = quest.Id;
            CurrentGuidanceKind = guidance.Kind;
            CurrentQuestNameLocalizationKey =
                quest.DisplayNameKey ?? string.Empty;
            _questName.text = quest.DisplayName ?? string.Empty;

            switch (guidance.Kind)
            {
                case MainQuestGuidanceKind.Accept:
                    CurrentObjectiveLocalizationKey =
                        AcceptGuidanceLocalizationKey;
                    _objective.text = string.Format(
                        AcceptGuidanceDefaultValue,
                        ResolveNpcName(guidance.TargetId));
                    break;
                case MainQuestGuidanceKind.TurnIn:
                    CurrentObjectiveLocalizationKey =
                        TurnInGuidanceLocalizationKey;
                    _objective.text = string.Format(
                        TurnInGuidanceDefaultValue,
                        ResolveNpcName(guidance.TargetId));
                    break;
                default:
                    ShowFirstIncompleteObjective(quest);
                    break;
            }
        }

        private void ShowFirstIncompleteObjective(QuestData quest)
        {
            QuestObjective[] objectives = quest.Objectives
                ?? System.Array.Empty<QuestObjective>();
            for (int index = 0; index < objectives.Length; index++)
            {
                QuestObjective objective = objectives[index];
                if (objective == null)
                {
                    continue;
                }

                int required = Mathf.Max(1, objective.RequiredCount);
                int current = _quests.GetObjectiveProgress(quest.Id, index);
                if (current >= required && index + 1 < objectives.Length)
                {
                    continue;
                }

                CurrentObjectiveLocalizationKey =
                    objective.DescriptionKey ?? string.Empty;
                _objective.text = string.Format(
                    ProgressDefaultValue,
                    objective.Description ?? string.Empty,
                    current,
                    required);
                return;
            }

            CurrentObjectiveLocalizationKey = ReadyTurnInLocalizationKey;
            _objective.text = ReadyTurnInDefaultValue;
        }

        private string BuildSignature()
        {
            if (_quests == null)
            {
                return "empty";
            }

            if (MainQuestGuidanceResolver.TryResolve(
                    _quests,
                    out MainQuestGuidance guidance))
            {
                QuestData main = guidance.Quest;
                var mainSignature = string.Concat(
                    "main:",
                    main?.Id,
                    ":",
                    guidance.Kind,
                    ":",
                    guidance.TargetId);
                int mainCount = main?.Objectives?.Length ?? 0;
                for (int index = 0; index < mainCount; index++)
                {
                    mainSignature += ":"
                        + _quests.GetObjectiveProgress(main.Id, index);
                }

                return mainSignature;
            }

            if (_quests.ActiveIds.Count == 0)
            {
                return "empty";
            }

            string questId = _quests.ActiveIds[0];
            QuestData quest = _quests.GetQuestData(questId);
            string signature = questId + ":" + _quests.GetStatus(questId);
            int count = quest?.Objectives?.Length ?? 0;
            for (int index = 0; index < count; index++)
            {
                signature += ":" + _quests.GetObjectiveProgress(questId, index);
            }

            return signature;
        }

        private void ShowEmpty()
        {
            CurrentQuestId = string.Empty;
            CurrentGuidanceKind = MainQuestGuidanceKind.None;
            CurrentQuestNameLocalizationKey = EmptyLocalizationKey;
            CurrentObjectiveLocalizationKey = string.Empty;
            _questName.text = EmptyDefaultValue;
            _objective.text = string.Empty;
            _lastSignature = BuildSignature();
            ApplyVisible(false);
        }

        private static string ResolveNpcName(string npcId)
        {
            NPCData npc = ConfigDatabase.Instance?.GetNpc(npcId);
            return string.IsNullOrWhiteSpace(npc?.DisplayName)
                ? npcId ?? string.Empty
                : npc.DisplayName;
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "QuestTrackerCanvas",
                110);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image panel = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "QuestTrackerPanel",
                new Color(0.035f, 0.07f, 0.055f, 0.88f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(460f, 126f),
                new Vector2(-250f, -90f));
            _questName = RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestTrackerName",
                EmptyDefaultValue,
                24,
                new Color(0.92f, 0.83f, 0.58f, 1f),
                new Vector2(410f, 42f),
                new Vector2(0f, 30f));
            _objective = RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestTrackerObjective",
                string.Empty,
                20,
                new Color(0.86f, 0.9f, 0.81f, 1f),
                new Vector2(410f, 52f),
                new Vector2(0f, -28f));
        }

        private void ApplyVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void HandleQuestChanged(QuestInfo info)
        {
            Refresh();
        }

        private void HandleQuestProgressed(QuestProgressInfo info)
        {
            Refresh();
        }
    }
}
