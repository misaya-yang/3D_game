namespace Wendao.Systems.Skill
{
    public static class SkillEvents
    {
        public const string SkillLearned = "OnSkillLearned";
        public const string SkillCast = "OnSkillCast";
        public const string SkillUpgraded = "OnSkillUpgraded";
    }

    public static class SkillContentIds
    {
        public const string BasicQiBolt = "skill_basic_qi_bolt";
        public const string FireEmber = "skill_fire_ember";
        public const string IceNeedle = "skill_ice_needle";
        public const string LightningChain = "skill_lightning_chain";
        public const string WindSlash = "skill_wind_slash";
        public const string IronSkin = "skill_pass_iron_skin";
        public const string FireWave = "skill_ult_fire_wave";

        public static readonly string[] All =
        {
            BasicQiBolt,
            FireEmber,
            IceNeedle,
            LightningChain,
            WindSlash,
            IronSkin,
            FireWave
        };
    }
}
