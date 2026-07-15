using System;
using System.Collections.Generic;

namespace Wendao.Data
{
    [Serializable]
    public sealed class MountSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<string> UnlockedMountIds = new List<string>();
        public string SelectedMountId = string.Empty;
    }
}
