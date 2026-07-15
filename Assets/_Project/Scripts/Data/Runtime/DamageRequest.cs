using System;
using UnityEngine;

namespace Wendao.Data
{
    [Serializable]
    public struct DamageRequest
    {
        public GameObject Source;
        public float BaseDamage;
        public DamageType Type;
        public ElementType Element;
        public float Multiplier;
        public bool CanCrit;
        public string SkillId;
        public bool IgnoreAttackScaling;
        public string StatusOnHitId;
        public float StatusChance;
        public float HitstopSeconds;
        public float HitstunSeconds;
    }
}
