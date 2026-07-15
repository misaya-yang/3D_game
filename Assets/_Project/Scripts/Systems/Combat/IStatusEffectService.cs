using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.Combat
{
    public interface IStatusEffectService
    {
        void Apply(
            string statusId,
            GameObject target,
            GameObject source,
            int stacks = 1);
        bool TryApply(
            string statusId,
            GameObject target,
            GameObject source,
            int stacks,
            float sourceBaseDamage);
        void Remove(string statusId, GameObject target);
        void Tick(float deltaTime);
        bool Has(string statusId, GameObject target);
        void ClearAll(GameObject target);
        int GetStacks(string statusId, GameObject target);
        float GetRemainingDuration(string statusId, GameObject target);
        float GetAttackMultiplier(GameObject target);
        float GetDamageDealtMultiplier(GameObject target);
        float GetDefenseMultiplier(GameObject target);
        float GetMoveSpeedMultiplier(GameObject target);
        bool IsStunned(GameObject target);
        bool IsRooted(GameObject target);
        bool IsSilenced(GameObject target);
        bool TryGetStatusForAura(
            GameObject target,
            ElementType auraElement,
            out string statusId);
        bool TryGetFirstAura(
            GameObject target,
            out ElementType auraElement,
            out string statusId);
        int CopyAuraStatuses(
            GameObject fromTarget,
            GameObject toTarget,
            GameObject source);
    }
}
