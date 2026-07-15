using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class SpiritRootConfig
    {
        public SpiritRootEntry[] Roots = Array.Empty<SpiritRootEntry>();
        public SpiritRootType[] DefaultPickable = Array.Empty<SpiritRootType>();
        public bool RandomEnabled;
    }

    [Serializable]
    public sealed class SpiritRootEntry
    {
        public SpiritRootType Type;
        public float CultivationMul = 1f;
        public Dictionary<string, float> ElementBonus =
            new Dictionary<string, float>(StringComparer.Ordinal);
        public float BodyMul = 1f;
        public float Weight = 1f;
        public string UniquePassiveId;
        public string IntroDescriptionKey = string.Empty;
        public SpiritRootPassiveConfig Passives;
    }

    [Serializable]
    public sealed class SpiritRootPassiveConfig
    {
        public float BlockPhysDrBonus;
        public float PhysicalDamageBonus;
        public float BodyPotionMul = 1f;
        public string GrantPassiveSkillIdOnCreate;
    }
}
