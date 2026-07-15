using System;

namespace Wendao.Data
{
    [Serializable]
    public sealed class CraftLevelConfig
    {
        public CraftLevelEntry[] Alchemy = Array.Empty<CraftLevelEntry>();
    }

    [Serializable]
    public sealed class CraftLevelEntry
    {
        public int Level;
        public float XpRequired;
        public int MaxRecipeTier;
        public float SuccessBonus;
    }
}
