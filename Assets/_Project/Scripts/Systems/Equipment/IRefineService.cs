using Wendao.Data;

namespace Wendao.Systems.Equipment
{
    public interface IRefineService
    {
        float GetSuccessRate(int currentLevel);
        int GetRequiredMaterialCount(int currentLevel);
        bool CanRefine(EquipmentSlot slot);

        /// <summary>
        /// Consumes materials for a valid attempt and returns whether the roll
        /// upgraded the item. A failed roll returns false without lowering level.
        /// </summary>
        bool TryRefine(EquipmentSlot slot);
    }
}
