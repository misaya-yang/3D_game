using Wendao.Data;

namespace Wendao.Systems.Cultivation
{
    public interface IBodyRefinementService
    {
        BodyLevel Level { get; }
        float Xp { get; }
        float XpToNext { get; }
        float HpBonus { get; }
        float PhysicalDR { get; }
        float ControlResist { get; }
        bool HasCombatRevive { get; }
        bool CanGainXp { get; }

        void AddBodyXp(float amount);
        void AddBodyXpFromPotion(float amount);
        bool TryLevelUp();
        bool TryConsumeRevive();
    }
}
