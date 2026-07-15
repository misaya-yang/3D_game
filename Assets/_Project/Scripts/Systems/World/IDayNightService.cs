namespace Wendao.Systems.World
{
    public interface IDayNightService
    {
        float TimeOfDay { get; }
        bool IsNight { get; }
        float EnemyAttackMultiplier { get; }
        float CycleDurationSeconds { get; }

        void SetTimeOfDay(float hour);
        void TickTime(float deltaTime);
        void RefreshFromSave();
    }
}
