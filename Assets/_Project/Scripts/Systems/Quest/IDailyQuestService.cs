using System;
using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Quest
{
    public interface IDailyQuestService
    {
        IReadOnlyList<DailyQuestRuntimeState> Active { get; }
        DateTime CycleStartedUtc { get; }
        DateTime NextResetUtc { get; }

        DailyQuestRuntimeState GetState(string questId);
        bool IsComplete(string questId);
        bool TryClaim(string questId);
        bool Refresh(DateTime utcNow);
        DailyQuestSaveData CaptureSaveData();
        void RestoreSaveData(DailyQuestSaveData data);
    }
}
