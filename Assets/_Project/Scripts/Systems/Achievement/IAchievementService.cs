using System.Collections.Generic;

namespace Wendao.Systems.Achievement
{
    public interface IAchievementService
    {
        IReadOnlyList<string> UnlockedIds { get; }

        void OnTrigger(string triggerType, string targetId, float value);
        bool IsUnlocked(string achievementId);
        float GetProgress(string achievementId);
    }
}
