using System;

namespace Wendao.Data
{
    [Serializable]
    public sealed class BodyRefinementConfig
    {
        public BodyLevelEntry[] Levels = Array.Empty<BodyLevelEntry>();
        public float XpFromDamageTakenMul;
    }

    [Serializable]
    public sealed class BodyLevelEntry
    {
        public int Level;
        public string Name = string.Empty;
        public float XpToNext;
        public float HpBonus;
        public float PhysDr;
        public float ControlResist;
        public bool Revive;
    }
}
