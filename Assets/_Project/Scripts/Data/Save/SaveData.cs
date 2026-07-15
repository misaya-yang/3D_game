using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wendao.Data
{
    public static class SaveSchema
    {
        public const int CurrentVersion = 1;
    }

    [Serializable]
    public sealed class SaveMetadata
    {
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public string DisplayName = string.Empty;
        public int Realm = 1;
        public int SubStage = 1;
        public float PlayTimeSeconds;
        public string MainQuestIndex = string.Empty;
        public int SpiritStones;
        public string SavedAt = string.Empty;

        [JsonIgnore] public int Slot = -1;
        [JsonIgnore] public bool Exists;
        [JsonIgnore] public bool IsCorrupted;
    }

    [Serializable]
    public sealed class SaveProfileData
    {
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public string DisplayName = string.Empty;
        public int Realm = 1;
        public int SubStage = 1;
        public float CultivationXp;
        public string SpiritRoot = string.Empty;
        public int BodyLevel;
        public float BodyXp;
        public float PlayTimeSeconds;
        public string MainQuestIndex = string.Empty;
        public int SpiritStones;
        public Dictionary<string, int> FactionReputation =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class SaveWorldData
    {
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<string> UnlockedMaps = new List<string>();
        public List<string> UnlockedTeleports = new List<string>();
        public float TimeOfDay = 10f;
        public List<string> TutorialsCompleted = new List<string>();
        public List<string> SerendipityFlags = new List<string>();
        public Dictionary<string, int> DungeonCheckpoint =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public Dictionary<string, bool> QuestFlags =
            new Dictionary<string, bool>(StringComparer.Ordinal);
    }
}
