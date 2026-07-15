using Wendao.Data;

namespace Wendao.Systems.Combat
{
    public interface IDamageable
    {
        float CurrentHp { get; }
        float MaxHp { get; }
        bool IsDead { get; }

        void ApplyDamage(DamageInfo info);
        void ApplyHeal(float amount, string sourceId);
    }
}
