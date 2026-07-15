using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Cultivation
{
    public enum BreakthroughState
    {
        Idle,
        BreakingThrough,
        BreakthroughResult
    }

    public interface ICultivationService
    {
        RealmType Realm { get; }
        int SubStage { get; }
        float CurrentXp { get; }
        float XpToNext { get; }
        BreakthroughState CurrentBreakthroughState { get; }
        bool IsBreakingThrough { get; }
        bool IsBreakthroughActive { get; }
        bool IsBreakthroughInvincible { get; }
        int CeremonyBeat { get; }

        void AddXp(float amount, XpSourceType source);
        float ApplyDeathXpPenalty(float percent);
        bool CanLevelSubStage();
        bool TryAdvanceSubStage();
        bool CanBreakthrough();
        IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers();
        float GetBreakthroughSuccessRate();
        bool TryBreakthrough();
    }
}
