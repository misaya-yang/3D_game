using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.Combat
{
    public interface ICombatService
    {
        DamageInfo ComputeDamage(DamageRequest request);
        DamageInfo ComputeDamage(DamageRequest request, IDamageable target);
        void DealDamage(IDamageable target, DamageRequest request);
        bool TryMeleeHit(
            Transform attacker,
            float range,
            float angle,
            DamageRequest request);
        void RegisterActor(IDamageable actor);
        void UnregisterActor(IDamageable actor);
    }
}
