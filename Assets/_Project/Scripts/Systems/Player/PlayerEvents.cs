namespace Wendao.Systems.Player
{
    public static class PlayerEvents
    {
        public const string Dodged = "OnPlayerDodged";
        public const string LockOnChanged = "OnLockOnChanged";
        public const string Respawned = "OnPlayerRespawned";
    }

    public static class PlayerRecoveryContentIds
    {
        public const string OutOfCombatRegeneration =
            "recovery_out_of_combat";
    }
}
