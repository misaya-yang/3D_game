using Wendao.Data;

namespace Wendao.Systems.Combat
{
    /// <summary>
    /// Target-side defenses applied after armor and before final damage clamping.
    /// </summary>
    public interface ICombatDefenseProvider
    {
        bool IsInvincible { get; }
        bool IsBlocking { get; }

        float GetBlockDamageReduction(DamageType damageType);
    }
}
