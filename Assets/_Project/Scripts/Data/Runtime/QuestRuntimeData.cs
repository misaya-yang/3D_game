using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class QuestRuntimeState
    {
        public string QuestId = string.Empty;
        public QuestStatus Status = QuestStatus.Active;
        public int[] ObjectiveProgress = Array.Empty<int>();
        public bool AcceptRewardsGranted;
    }

    [Serializable]
    public sealed class QuestSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<QuestRuntimeState> Quests = new List<QuestRuntimeState>();
    }
}
