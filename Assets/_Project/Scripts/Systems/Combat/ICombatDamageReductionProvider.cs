using Wendao.Data;

namespace Wendao.Systems.Combat
{
    public interface ICombatDamageReductionProvider
    {
        float GetDamageReduction(DamageType damageType);
    }
}
