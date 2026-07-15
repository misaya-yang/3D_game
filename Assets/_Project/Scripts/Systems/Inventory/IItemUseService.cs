namespace Wendao.Systems.Inventory
{
    public interface IItemUseService
    {
        bool CanUse(int slotIndex);
        bool Use(int slotIndex);
    }
}
