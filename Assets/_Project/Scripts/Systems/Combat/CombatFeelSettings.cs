namespace Wendao.Systems.Combat
{
    /// <summary>
    /// G01-05 combat-feel constants. The numeric authority is
    /// docs/04_PLAYER_COMBAT.md section 4.8.
    /// </summary>
    public static class CombatFeelSettings
    {
        public const float InputBufferSeconds = 0.12f;
        public const float HitstopLight12Seconds = 0.03f;
        public const float HitstopLight34HeavySeconds = 0.06f;
        public const float NormalEnemyHitstunSeconds = 0.18f;
        public const float EliteHitstunMultiplier = 0.5f;
        public const float CriticalShakeIntensity = 0.5f;
        public const float CriticalShakeDuration = 0.15f;
        public const float ElementReactionFovKick = 2f;
        public const float ElementReactionFovDuration = 0.2f;

        public static float GetLightHitstopSeconds(int comboStep)
        {
            return comboStep <= 2
                ? HitstopLight12Seconds
                : HitstopLight34HeavySeconds;
        }
    }
}
