using System;

namespace Wendao.Data
{
    [Serializable]
    public sealed class AlchemySaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public int Level = 1;
        public float Xp;
    }
}
