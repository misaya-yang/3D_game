using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class SkillRuntime
    {
        public string SkillId = string.Empty;
        public int Level = 1;
        public float Exp;
        public float CooldownRemaining;
    }

    [Serializable]
    public sealed class SkillSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<SkillRuntime> Learned = new List<SkillRuntime>();
        public string[] EquippedIds = new string[4];
    }
}
