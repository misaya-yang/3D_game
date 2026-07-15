using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Quest
{
    public interface IQuestService
    {
        IReadOnlyList<string> ActiveIds { get; }
        IReadOnlyList<string> CompletedIds { get; }

        bool CanAccept(string questId);
        bool Accept(string questId);
        void NotifyKill(string enemyId);
        void NotifyCollect(string itemId, int totalCount);
        void NotifyUseItem(string itemId);
        void NotifyCraft(string resultItemId, int resultCount);
        void NotifyTalk(string npcId);
        void NotifyReach(string locationId);
        void NotifyRealm(RealmType newRealm);
        bool CanTurnIn(string questId);
        bool TurnIn(string questId);
        QuestStatus GetStatus(string questId);
        QuestData GetQuestData(string questId);
        int GetObjectiveProgress(string questId, int objectiveIndex);
        string ResolveInteractionDialogueId(string npcId, string fallbackDialogueId);
    }
}
