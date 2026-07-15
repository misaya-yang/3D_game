using System;

namespace Wendao.Data
{
    [Serializable]
    public sealed class RealmConfig
    {
        public RealmEntry[] Realms = Array.Empty<RealmEntry>();
    }

    [Serializable]
    public sealed class RealmEntry
    {
        public int Realm;
        public string Name = string.Empty;
        public int SubStages;
        public float[] XpPerSubStage = Array.Empty<float>();
        public RealmBaseStats BaseStatsPerSubStage = new RealmBaseStats();
        public BreakthroughConfig BreakthroughToNext;
    }

    [Serializable]
    public sealed class RealmBaseStats
    {
        public float[] MaxHp = Array.Empty<float>();
        public float[] MaxMana = Array.Empty<float>();
        public float[] Attack = Array.Empty<float>();
        public float[] Defense = Array.Empty<float>();
    }

    [Serializable]
    public sealed class BreakthroughConfig
    {
        public float BaseSuccessRate;
        public int MinSubStage;
        public float FailXpPenaltyPercent;
        public string RequiredItemId = string.Empty;
    }
}
