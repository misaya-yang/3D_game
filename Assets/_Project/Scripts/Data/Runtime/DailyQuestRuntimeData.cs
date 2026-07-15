using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class DailyQuestRuntimeState
    {
        public string QuestId = string.Empty;
        public int Progress;
        public bool Claimed;
    }

    [Serializable]
    public sealed class DailyQuestSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public string CycleStartedUtc = string.Empty;
        public List<DailyQuestRuntimeState> Quests =
            new List<DailyQuestRuntimeState>();
    }
}
