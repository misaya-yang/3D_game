using Wendao.Systems.Inventory;

namespace Wendao.Systems.Shop
{
    public static class ShopContentIds
    {
        public const string ZhangguiNpc = "npc_zhanggui";
        public const string ZhangguiDialogue = "dlg_shop_zhanggui";

        public static readonly string[] ZhangguiStock =
        {
            InventoryContentIds.HealPotion01,
            InventoryContentIds.RefineStone,
            InventoryContentIds.IronSword
        };
    }
}
