using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Skill_New", menuName = "问道/功法/SkillData")]
    public class SkillData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public string DescriptionKey;
        [TextArea] public string Description;
        public SkillType Type;
        public SkillElement Element;
        public Sprite Icon;
        public int MaxLevel = 5;
        public int RequiredRealm;
        public float BaseCooldown;
        public float BaseManaCost;
        public float BaseDamage;
        public float DamagePerLevel;
        public float CastTime;
        public float RecoveryTime;
        public float Range;
        public float Radius;
        public bool IsProjectile;
        public string ProjectileVfxId;
        public string ImpactVfxId;
        public string StatusOnHitId;
        public float StatusChance = 1f;
        public SpiritRootType[] PreferredRoots = Array.Empty<SpiritRootType>();
        public AnimationClip CastAnimation;
        public LevelScaling[] LevelTable = Array.Empty<LevelScaling>();
    }

    [Serializable]
    public class LevelScaling
    {
        public int Level;
        public float Damage;
        public float Cooldown;
        public float ManaCost;
    }
}
