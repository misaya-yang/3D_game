using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.Quest;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Quest
{
    public sealed class QuestPanelView : MonoBehaviour
    {
        public const string TitleLocalizationKey = "ui_quest_panel_title";
        public const string TitleDefaultValue = "任务";
        public const string ActiveLocalizationKey = "ui_quest_active";
        public const string ActiveDefaultValue = "进行中";
        public const string DailyLocalizationKey = "ui_quest_daily";
        public const string DailyDefaultValue = "每日委托";
        public const string EmptyLocalizationKey = "ui_quest_panel_empty";
        public const string EmptyDefaultValue =
            "暂无任务。与带有任务标记的修士交谈可接取委托。";
        public const string ProgressLocalizationKey = "ui_quest_progress";
        public const string ProgressDefaultValue = "{0}  {1}/{2}";
        public const string ReadyLocalizationKey = "ui_quest_ready_turn_in";
        public const string ReadyDefaultValue = "目标已完成，可以交付";
        public const string DailyReadyLocalizationKey = "ui_daily_ready";
        public const string DailyReadyDefaultValue = "委托完成，可领取奖励";
        public const string DailyClaimedLocalizationKey = "ui_daily_claimed";
        public const string DailyClaimedDefaultValue = "今日奖励已领取";
        public const string ClaimLocalizationKey = "ui_daily_claim";
        public const string ClaimDefaultValue = "领取奖励";
        public const string CloseLocalizationKey = "ui_common_close";
        public const string CloseDefaultValue = "关闭";

        private const int MaximumRows = 10;

        private readonly Button[] _rows = new Button[MaximumRows];
        private readonly Text[] _rowLabels = new Text[MaximumRows];
        private readonly string[] _rowQuestIds = new string[MaximumRows];
        private readonly bool[] _rowIsDaily = new bool[MaximumRows];

        private CanvasGroup _canvasGroup;
        private Text _description;
        private Text _progress;
        private Button _claimButton;
        private Button _closeButton;
        private IQuestService _quests;
        private IDailyQuestService _dailies;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;

        public bool IsOpen { get; private set; }
        public int VisibleRowCount { get; private set; }
        public string SelectedQuestId { get; private set; } = string.Empty;
        public bool SelectedIsDaily { get; private set; }
        public string DescriptionText => _description?.text ?? string.Empty;
        public string ProgressText => _progress?.text ?? string.Empty;
        public bool CanClaimDaily =>
            _claimButton != null && _claimButton.interactable;

        private void Awake()
        {
            BuildView();
            ApplyOpenState(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<QuestInfo>(QuestEvents.Accepted, HandleQuestChanged);
            EventBus.Subscribe<QuestInfo>(QuestEvents.Completed, HandleQuestChanged);
            EventBus.Subscribe<QuestProgressInfo>(
                QuestEvents.Progressed,
                HandleQuestProgressed);
            EventBus.Subscribe<DailyQuestProgressInfo>(
                DailyQuestEvents.Progressed,
                HandleDailyProgressed);
            EventBus.Subscribe<DailyQuestResetInfo>(
                DailyQuestEvents.Reset,
                HandleDailyReset);
            EventBus.Subscribe<DailyQuestClaimInfo>(
                DailyQuestEvents.Claimed,
                HandleDailyClaimed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestInfo>(QuestEvents.Accepted, HandleQuestChanged);
            EventBus.Unsubscribe<QuestInfo>(QuestEvents.Completed, HandleQuestChanged);
            EventBus.Unsubscribe<QuestProgressInfo>(
                QuestEvents.Progressed,
                HandleQuestProgressed);
            EventBus.Unsubscribe<DailyQuestProgressInfo>(
                DailyQuestEvents.Progressed,
                HandleDailyProgressed);
            EventBus.Unsubscribe<DailyQuestResetInfo>(
                DailyQuestEvents.Reset,
                HandleDailyReset);
            EventBus.Unsubscribe<DailyQuestClaimInfo>(
                DailyQuestEvents.Claimed,
                HandleDailyClaimed);

            if (IsOpen)
            {
                RestoreGameplayInput();
                ApplyOpenState(false);
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
            else if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SelectQuest(string questId, bool daily)
        {
            SelectedQuestId = questId ?? string.Empty;
            SelectedIsDaily = daily;
            RefreshSelection();
        }

        public bool TryClaimSelectedDaily()
        {
            ResolveServices();
            bool claimed = SelectedIsDaily
                && _dailies != null
                && _dailies.TryClaim(SelectedQuestId);
            Refresh();
            return claimed;
        }

        public void Refresh()
        {
            ResolveServices();
            VisibleRowCount = 0;
            int row = 0;
            if (_quests != null)
            {
                for (int index = 0;
                     index < _quests.ActiveIds.Count && row < MaximumRows;
                     index++)
                {
                    string questId = _quests.ActiveIds[index];
                    QuestData quest = _quests.GetQuestData(questId);
                    if (quest == null)
                    {
                        continue;
                    }

                    ConfigureRow(row++, questId, false, quest.DisplayName);
                }
            }

            if (_dailies != null)
            {
                for (int index = 0;
                     index < _dailies.Active.Count && row < MaximumRows;
                     index++)
                {
                    DailyQuestRuntimeState state = _dailies.Active[index];
                    QuestData quest = ConfigDatabase.Instance?.GetQuest(state.QuestId);
                    if (quest == null)
                    {
                        continue;
                    }

                    string label = DailyDefaultValue + " · " + quest.DisplayName;
                    ConfigureRow(row++, state.QuestId, true, label);
                }
            }

            VisibleRowCount = row;
            for (int index = row; index < MaximumRows; index++)
            {
                _rowQuestIds[index] = string.Empty;
                _rowIsDaily[index] = false;
                _rows[index].gameObject.SetActive(false);
            }

            if (!ContainsSelection())
            {
                if (VisibleRowCount > 0)
                {
                    SelectedQuestId = _rowQuestIds[0];
                    SelectedIsDaily = _rowIsDaily[0];
                }
                else
                {
                    SelectedQuestId = string.Empty;
                    SelectedIsDaily = false;
                }
            }

            RefreshSelection();
        }

        private void ConfigureRow(
            int index,
            string questId,
            bool daily,
            string label)
        {
            _rowQuestIds[index] = questId;
            _rowIsDaily[index] = daily;
            _rows[index].gameObject.SetActive(true);
            _rowLabels[index].text = label ?? questId;
        }

        private bool ContainsSelection()
        {
            for (int index = 0; index < VisibleRowCount; index++)
            {
                if (_rowIsDaily[index] == SelectedIsDaily
                    && string.Equals(
                        _rowQuestIds[index],
                        SelectedQuestId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshSelection()
        {
            QuestData quest = ConfigDatabase.Instance?.GetQuest(SelectedQuestId);
            if (quest == null)
            {
                _description.text = EmptyDefaultValue;
                _progress.text = string.Empty;
                _claimButton.gameObject.SetActive(false);
                return;
            }

            _description.text = quest.DisplayName + "\n\n" + quest.Description;
            if (SelectedIsDaily)
            {
                RefreshDailySelection(quest);
            }
            else
            {
                RefreshNormalSelection(quest);
            }
        }

        private void RefreshNormalSelection(QuestData quest)
        {
            _claimButton.gameObject.SetActive(false);
            if (_quests == null)
            {
                _progress.text = string.Empty;
                return;
            }

            if (_quests.GetStatus(quest.Id) == QuestStatus.Completed)
            {
                _progress.text = ReadyDefaultValue;
                return;
            }

            QuestObjective[] objectives = quest.Objectives
                ?? Array.Empty<QuestObjective>();
            for (int index = 0; index < objectives.Length; index++)
            {
                QuestObjective objective = objectives[index];
                if (objective == null)
                {
                    continue;
                }

                int required = Mathf.Max(1, objective.RequiredCount);
                int current = _quests.GetObjectiveProgress(quest.Id, index);
                if (current < required || index == objectives.Length - 1)
                {
                    _progress.text = string.Format(
                        ProgressDefaultValue,
                        objective.Description,
                        current,
                        required);
                    return;
                }
            }

            _progress.text = ReadyDefaultValue;
        }

        private void RefreshDailySelection(QuestData quest)
        {
            DailyQuestRuntimeState state = _dailies?.GetState(quest.Id);
            QuestObjective objective = quest.Objectives != null
                && quest.Objectives.Length > 0
                ? quest.Objectives[0]
                : null;
            int required = Mathf.Max(1, objective?.RequiredCount ?? 1);
            int current = Mathf.Clamp(state?.Progress ?? 0, 0, required);
            bool complete = current >= required;
            bool claimed = state?.Claimed == true;
            _progress.text = claimed
                ? DailyClaimedDefaultValue
                : complete
                    ? DailyReadyDefaultValue
                    : string.Format(
                        ProgressDefaultValue,
                        objective?.Description ?? string.Empty,
                        current,
                        required);
            _claimButton.gameObject.SetActive(true);
            _claimButton.interactable = complete && !claimed;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "QuestPanelCanvas",
                220);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image overlay = RuntimeUiFactory.CreateImage(
                canvas.transform,
                "QuestOverlay",
                new Color(0.015f, 0.03f, 0.024f, 0.84f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RuntimeUiFactory.Stretch(overlay.rectTransform);
            Image panel = RuntimeUiFactory.CreateImage(
                overlay.transform,
                "QuestPanel",
                new Color(0.045f, 0.085f, 0.065f, 0.99f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1320f, 900f),
                Vector2.zero);
            RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestTitle",
                TitleDefaultValue,
                40,
                new Color(0.94f, 0.82f, 0.52f, 1f),
                new Vector2(1180f, 60f),
                new Vector2(0f, 390f));

            RuntimeUiFactory.CreatePanel(
                panel.transform,
                "QuestListSectionPanel",
                new Vector2(520f, 660f),
                new Vector2(-360f, 0f),
                true);
            RuntimeUiFactory.CreatePanel(
                panel.transform,
                "QuestDetailSectionPanel",
                new Vector2(650f, 660f),
                new Vector2(300f, 0f),
                true);
            RuntimeUiFactory.CreateIcon(
                panel.transform,
                "QuestTitleIcon",
                "menuList",
                new Vector2(38f, 38f),
                new Vector2(-120f, 390f),
                RuntimeUiTheme.GoldSoft);

            for (int index = 0; index < MaximumRows; index++)
            {
                int captured = index;
                Image rowImage = RuntimeUiFactory.CreateImage(
                    panel.transform,
                    "QuestRow" + index,
                    new Color(0.1f, 0.2f, 0.15f, 0.95f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(470f, 58f),
                    new Vector2(-360f, 300f - index * 66f));
                _rows[index] = rowImage.gameObject.AddComponent<Button>();
                _rows[index].onClick.AddListener(
                    () => SelectQuest(
                        _rowQuestIds[captured],
                        _rowIsDaily[captured]));
                _rowLabels[index] = RuntimeUiFactory.CreateText(
                    rowImage.transform,
                    "Label",
                    EmptyDefaultValue,
                    21,
                    new Color(0.93f, 0.91f, 0.78f, 1f),
                    new Vector2(440f, 48f),
                    Vector2.zero);
            }

            _description = RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestDescription",
                EmptyDefaultValue,
                26,
                new Color(0.9f, 0.91f, 0.82f, 1f),
                new Vector2(610f, 450f),
                new Vector2(300f, 105f));
            _description.alignment = TextAnchor.UpperLeft;
            _progress = RuntimeUiFactory.CreateText(
                panel.transform,
                "QuestProgress",
                string.Empty,
                24,
                new Color(0.59f, 0.9f, 0.68f, 1f),
                new Vector2(610f, 120f),
                new Vector2(300f, -185f));
            _claimButton = CreateButton(
                panel.transform,
                "DailyClaim",
                ClaimDefaultValue,
                new Vector2(300f, -300f));
            _claimButton.onClick.AddListener(() => TryClaimSelectedDaily());
            _closeButton = CreateButton(
                panel.transform,
                "QuestClose",
                CloseDefaultValue,
                new Vector2(0f, -390f));
            _closeButton.onClick.AddListener(() => SetOpen(false));
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position)
        {
            Image image = RuntimeUiFactory.CreateImage(
                parent,
                name,
                new Color(0.17f, 0.36f, 0.27f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(250f, 66f),
                position);
            Button button = image.gameObject.AddComponent<Button>();
            RuntimeUiFactory.CreateText(
                image.transform,
                "Label",
                label,
                23,
                new Color(0.96f, 0.91f, 0.72f, 1f),
                new Vector2(230f, 54f),
                Vector2.zero);
            return button;
        }

        private void ResolveServices()
        {
            if (_quests == null)
            {
                ServiceLocator.TryGet(out _quests);
            }

            if (_dailies == null)
            {
                ServiceLocator.TryGet(out _dailies);
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
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void ApplyOpenState(bool open)
        {
            IsOpen = open;
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.interactable = open;
            _canvasGroup.blocksRaycasts = open;
        }

        private void SelectFirstUiElement()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            GameObject target = VisibleRowCount > 0
                ? _rows[0].gameObject
                : _closeButton.gameObject;
            EventSystem.current.SetSelectedGameObject(target);
        }

        private void HandleQuestChanged(QuestInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleQuestProgressed(QuestProgressInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleDailyProgressed(DailyQuestProgressInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleDailyReset(DailyQuestResetInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void HandleDailyClaimed(DailyQuestClaimInfo info)
        {
            if (IsOpen)
            {
                Refresh();
            }
        }
    }
}
