using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wendao.Data
{
    [Serializable]
    public sealed class InventorySlot
    {
        public string ItemId = string.Empty;
        public int Count;
        public bool Bound;
        public string InstanceId = string.Empty;
        public string ExtraJson = string.Empty;

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;
    }

    [Serializable]
    public sealed class EquipmentInstance
    {
        public string InstanceId = string.Empty;
        public string EquipmentDataId = string.Empty;
        public int RefineLevel;
        public int Durability;
        public string[] GemIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class InventorySaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<InventorySlot> Slots = new List<InventorySlot>();
        public int SpiritStones;
        public List<EquipmentInstance> EquipmentInstances =
            new List<EquipmentInstance>();
    }

    [Serializable]
    public sealed class EquippedItemRecord
    {
        public EquipmentSlot Slot;
        public string ItemId = string.Empty;
        public bool Bound;
        public EquipmentInstance Instance = new EquipmentInstance();
    }

    [Serializable]
    public sealed class EquipmentSaveData
    {
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public List<EquippedItemRecord> Worn = new List<EquippedItemRecord>();
    }
}
