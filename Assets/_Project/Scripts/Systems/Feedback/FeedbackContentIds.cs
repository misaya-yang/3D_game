using System;

namespace Wendao.Systems.Feedback
{
    public static class AudioContentIds
    {
        public const string ExploreQingshi = "BGM_Explore_Qingshi";
        public const string ExploreCangwu = "BGM_Explore_Cangwu";
        public const string CombatNormal = "BGM_Combat_Normal";
        public const string BossStoneGeneral = "BGM_Boss_StoneGeneral";
        public const string CityQingshi = "BGM_City_Qingshi";

        public const string UiClick = "SFX_UI_Click";
        public const string UiQuestComplete = "SFX_UI_QuestComplete";
        public const string UiLevelUp = "SFX_UI_LevelUp";
        public const string UiError = "SFX_UI_Error";
        public const string CombatHitLight = "SFX_Combat_Hit_Light";
        public const string CombatHitHeavy = "SFX_Combat_Hit_Heavy";
        public const string CombatDodge = "SFX_Combat_Dodge";
        public const string SkillFire = "SFX_Skill_Fire";
        public const string SkillIce = "SFX_Skill_Ice";
        public const string SkillLightning = "SFX_Skill_Lightning";

        public const string WindPlain = "AMB_Wind_Plain";
        public const string WindMountain = "AMB_Wind_Mountain";
        public const string Rain = "AMB_Rain";

        public static readonly string[] Bgm =
        {
            ExploreQingshi,
            ExploreCangwu,
            CombatNormal,
            BossStoneGeneral,
            CityQingshi
        };

        public static readonly string[] Sfx =
        {
            UiClick,
            UiQuestComplete,
            UiLevelUp,
            UiError,
            CombatHitLight,
            CombatHitHeavy,
            CombatDodge,
            SkillFire,
            SkillIce,
            SkillLightning
        };

        public static readonly string[] Ambience =
        {
            WindPlain,
            WindMountain,
            Rain
        };

        public static bool IsBgm(string id)
        {
            return Contains(Bgm, id);
        }

        public static bool IsSfx(string id)
        {
            return Contains(Sfx, id);
        }

        public static bool IsAmbience(string id)
        {
            return Contains(Ambience, id);
        }

        private static bool Contains(string[] values, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class VfxContentIds
    {
        public const string HitPhysical = "VFX_Hit_Physical";
        public const string HitFire = "VFX_Hit_Fire";
        public const string HitIce = "VFX_Hit_Ice";
        public const string SkillEmber = "VFX_Skill_Ember";
        public const string SkillQiBoltProjectile =
            "VFX_Skill_QiBolt_Projectile";
        public const string SkillQiBoltImpact = "VFX_Skill_QiBolt_Impact";
        public const string BossSlamWarning = "VFX_Boss_Slam_Warning";
        public const string BossChargeWarning = "VFX_Boss_Charge_Warning";
        public const string BossSummonWarning = "VFX_Boss_Summon_Warning";
        public const string RealmBreakthrough = "VFX_Realm_Breakthrough";
        public const string LootDrop = "VFX_Loot_Drop";
        public const string Heal = "VFX_Heal";

        public static readonly string[] All =
        {
            HitPhysical,
            HitFire,
            HitIce,
            SkillEmber,
            SkillQiBoltProjectile,
            SkillQiBoltImpact,
            BossSlamWarning,
            BossChargeWarning,
            BossSummonWarning,
            RealmBreakthrough,
            LootDrop,
            Heal
        };

        public static bool IsKnown(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int index = 0; index < All.Length; index++)
            {
                if (string.Equals(All[index], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
