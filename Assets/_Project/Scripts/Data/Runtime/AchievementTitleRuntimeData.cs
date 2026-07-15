using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class AchievementProgress
    {
        public string AchievementId = string.Empty;
        public float Value;
    }

    [Serializable]
    public sealed class AchievementSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<AchievementProgress> Progress =
            new List<AchievementProgress>();
        public List<string> UnlockedIds = new List<string>();
    }

    [Serializable]
    public sealed class TitleSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<string> UnlockedTitleIds = new List<string>();
        public string ActiveTitleId = string.Empty;
    }
}
