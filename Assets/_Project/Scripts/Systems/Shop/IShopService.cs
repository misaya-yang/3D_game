using System.Collections.Generic;

namespace Wendao.Systems.Shop
{
    public enum ShopFailureReason
    {
        None,
        ShopClosed,
        InvalidVendor,
        InvalidItem,
        InvalidCount,
        NotSoldByVendor,
        InsufficientFunds,
        InventoryFull,
        ItemNotSellable,
        BoundItem,
        CurrencyOverflow
    }

    public interface IShopService
    {
        bool IsOpen { get; }
        string ActiveVendorId { get; }
        ShopFailureReason LastFailureReason { get; }

        IReadOnlyList<string> GetStock(string npcId);
        int GetBuyPrice(string npcId, string itemId);
        bool OpenVendor(string npcId);
        bool CloseVendor();
        bool Buy(string npcId, string itemId, int count);
        bool Sell(int inventorySlot, int count);
    }
}
