namespace Wendao.Systems.Combat
{
    public static class CombatEvents
    {
        public const string DamageApplied = "OnDamageApplied";
        public const string PlayerDamaged = "OnPlayerDamaged";
        public const string PlayerDied = "OnPlayerDied";
        public const string PlayerHealed = "OnPlayerHealed";
        public const string EnemyKilled = "OnEnemyKilled";
        public const string BossPhaseChanged = "OnBossPhaseChanged";
        public const string StatusEffectChanged = "OnStatusEffectChanged";
        public const string ElementReactionTriggered =
            "OnElementReactionTriggered";
    }

    public static class CombatContentIds
    {
        public const string TrainingDummyEnemyId = "enemy_training_dummy";
    }
}
