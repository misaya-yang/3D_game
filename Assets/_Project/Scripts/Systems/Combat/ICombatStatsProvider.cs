namespace Wendao.Systems.Combat
{
    /// <summary>
    /// Minimal G-VS-02 stat surface. G02-03 replaces the backing values with the
    /// fully aggregated PlayerStats result without changing CombatSystem.
    /// </summary>
    public interface ICombatStatsProvider
    {
        float Attack { get; }
        float Defense { get; }
        float CritRate { get; }
        float CritDamage { get; }
    }
}
