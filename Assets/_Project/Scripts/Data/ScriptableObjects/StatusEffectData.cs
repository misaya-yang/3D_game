using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "StatusEffect_New", menuName = "问道/状态/StatusEffectData")]
    public class StatusEffectData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public float Duration;
        public int MaxStacks = 1;
        public bool IsDebuff;
        public ElementType AuraElement;
        public float DotDamagePerSecond;
        public float DotBaseDamageMultiplier;
        public float DotInterval = 1f;
        public DamageType DotType;
        public float MoveSpeedMod;
        public float AttackMod;
        public float DamageDealtMod;
        public float DefenseMod;
        public bool Stun;
        public bool Root;
        public bool Silence;
        public float ReapplyCooldown;
        public string PromoteAtMaxStacksStatusId;
    }
}
