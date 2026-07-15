using System.Collections.Generic;
using Wendao.Data;

namespace Wendao.Systems.Equipment
{
    public interface IEquipmentService
    {
        IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> Worn { get; }

        bool EquipFromInventory(int inventorySlot);
        bool Unequip(EquipmentSlot slot);
        int GetRefineLevel(EquipmentSlot slot);
        StatBlock GetEquipmentStats();
    }
}
