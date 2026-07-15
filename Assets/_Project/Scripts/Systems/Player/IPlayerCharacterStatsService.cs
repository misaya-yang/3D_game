namespace Wendao.Systems.Player
{
    /// <summary>
    /// Read-only player snapshot consumed by panel_character without coupling UI
    /// to the Entities assembly.
    /// </summary>
    public interface IPlayerCharacterStatsService
    {
        float CurrentHp { get; }
        float MaxHp { get; }
        float CurrentMana { get; }
        float MaxMana { get; }
        float Attack { get; }
        float Defense { get; }
        float CritRate { get; }
        float CritDamage { get; }
        float CultivationSpeed { get; }
        float DivineSense { get; }
    }
}
