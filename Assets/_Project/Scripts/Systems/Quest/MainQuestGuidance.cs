using System;
using Wendao.Data;

namespace Wendao.Systems.Quest
{
    public enum MainQuestGuidanceKind
    {
        None,
        Accept,
        Objective,
        TurnIn
    }

    public sealed class MainQuestGuidance
    {
        public MainQuestGuidanceKind Kind;
        public QuestData Quest;
        public QuestStatus Status;
        public QuestObjective Objective;
        public int ObjectiveIndex = -1;
        public string TargetId = string.Empty;
    }

    /// <summary>
    /// Resolves the one critical main-chapter action a player can perform now.
    /// This is deliberately read-only: it never accepts, progresses or turns in
    /// quests on the player's behalf.
    /// </summary>
    public static class MainQuestGuidanceResolver
    {
        public static bool TryResolve(
            IQuestService quests,
            out MainQuestGuidance guidance)
        {
            guidance = null;
            if (quests == null)
            {
                return false;
            }

            for (int index = 0;
                 index < QuestContentIds.MainChapterOne.Length;
                 index++)
            {
                string questId = QuestContentIds.MainChapterOne[index];
                QuestData quest = quests.GetQuestData(questId);
                if (quest == null)
                {
                    continue;
                }

                QuestStatus status = quests.GetStatus(questId);
                switch (status)
                {
                    case QuestStatus.TurnedIn:
                        continue;
                    case QuestStatus.Available:
                        guidance = new MainQuestGuidance
                        {
                            Kind = MainQuestGuidanceKind.Accept,
                            Quest = quest,
                            Status = status,
                            TargetId = ResolveStartNpcId(quest)
                        };
                        return true;
                    case QuestStatus.Active:
                        return TryResolveObjective(
                            quests,
                            quest,
                            status,
                            out guidance);
                    case QuestStatus.Completed:
                        guidance = new MainQuestGuidance
                        {
                            Kind = MainQuestGuidanceKind.TurnIn,
                            Quest = quest,
                            Status = status,
                            TargetId = ResolveTurnInNpcId(quest)
                        };
                        return true;
                    default:
                        // The first unfinished main quest is authoritative.
                        // If it is locked, later chapter steps must not leak.
                        return false;
                }
            }

            return false;
        }

        private static bool TryResolveObjective(
            IQuestService quests,
            QuestData quest,
            QuestStatus status,
            out MainQuestGuidance guidance)
        {
            QuestObjective[] objectives =
                quest.Objectives ?? Array.Empty<QuestObjective>();
            for (int index = 0; index < objectives.Length; index++)
            {
                QuestObjective objective = objectives[index];
                if (objective == null)
                {
                    continue;
                }

                int required = Math.Max(1, objective.RequiredCount);
                if (quests.GetObjectiveProgress(quest.Id, index) >= required)
                {
                    continue;
                }

                guidance = new MainQuestGuidance
                {
                    Kind = MainQuestGuidanceKind.Objective,
                    Quest = quest,
                    Status = status,
                    Objective = objective,
                    ObjectiveIndex = index,
                    TargetId = objective.TargetId ?? string.Empty
                };
                return true;
            }

            guidance = new MainQuestGuidance
            {
                Kind = MainQuestGuidanceKind.TurnIn,
                Quest = quest,
                Status = QuestStatus.Completed,
                TargetId = ResolveTurnInNpcId(quest)
            };
            return true;
        }

        private static string ResolveStartNpcId(QuestData quest)
        {
            return string.IsNullOrWhiteSpace(quest?.StartNpcId)
                ? quest?.TurnInNpcId ?? string.Empty
                : quest.StartNpcId;
        }

        private static string ResolveTurnInNpcId(QuestData quest)
        {
            return string.IsNullOrWhiteSpace(quest?.TurnInNpcId)
                ? quest?.StartNpcId ?? string.Empty
                : quest.TurnInNpcId;
        }
    }
}
