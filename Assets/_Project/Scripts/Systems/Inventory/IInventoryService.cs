using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Inventory
{
    public interface IInventoryService
    {
        IReadOnlyList<InventorySlot> Slots { get; }
        int SpiritStones { get; }

        bool CanAdd(string itemId, int count);
        bool AddItem(
            string itemId,
            int count,
            AcquireSource source,
            string instanceId = null);
        bool RemoveItem(string itemId, int count);
        bool RestoreItem(string itemId, int count);
        bool RemoveAt(int slotIndex, int count);
        int CountItem(string itemId);
        void Sort();
        bool AddSpiritStones(int delta);
        EquipmentInstance GetEquipmentInstance(string instanceId);
        EquipmentInstance CreateEquipmentInstance(string equipmentDataId);

        bool TryTakeEquipmentAt(
            int slotIndex,
            out InventorySlot slot,
            out EquipmentInstance instance);
        bool TryStoreEquipment(
            string itemId,
            EquipmentInstance instance,
            bool bound,
            int preferredSlot = -1);
    }
}
