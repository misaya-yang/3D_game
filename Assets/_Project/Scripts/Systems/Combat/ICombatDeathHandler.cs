using Wendao.Data;

namespace Wendao.Systems.Combat
{
    /// <summary>
    /// Runs after CombatSystem has applied HP loss and published damage feedback,
    /// preserving the authoritative damage-before-death event order.
    /// </summary>
    public interface ICombatDeathHandler
    {
        void HandleDeath(DamageInfo killingBlow);
    }
}
