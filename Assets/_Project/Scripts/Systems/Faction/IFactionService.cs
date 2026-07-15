namespace Wendao.Systems.Faction
{
    public interface IFactionService
    {
        int GetRep(string factionId);
        void AddRep(string factionId, int delta);
        int GetRank(string factionId);
        float GetShopDiscount(string factionId);
        bool HasJoined(string factionId);
        bool Join(string factionId);
    }
}
