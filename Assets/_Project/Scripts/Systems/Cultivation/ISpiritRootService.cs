using Wendao.Data;

namespace Wendao.Systems.Cultivation
{
    public interface ISpiritRootService
    {
        SpiritRootType Root { get; }
        bool HasChosenRoot { get; }

        void ChooseRoot(SpiritRootType type);
        bool TryChooseRoot(SpiritRootType type);
        void RandomizeRoot();
        void RandomizeRoot(int seed);
        bool TryRandomizeRoot();
        bool TryRandomizeRoot(int seed);
        float GetCultivationMultiplier();
        float GetBodyMultiplier();
        float GetElementBonus(ElementType element);
        float GetBodyPotionMul();
        float GetPhysicalDamageBonus();
        float GetBlockPhysDrBonus();
        string GetIntroDescriptionKey();
    }
}
