namespace Wendao.Systems.World
{
    public interface IBlackwindDungeonService
    {
        int Checkpoint { get; }
        int CurrentFloor { get; }
        bool IsRunActive { get; }
        bool IsRunCompleted { get; }
        bool IsPressurePlateActive { get; }
        bool IsHealingSpringUsed { get; }

        bool IsFloorComplete(int floor);
        void BeginRun();
        void EndRun();
        bool EnterFloor(int floor);
        bool ActivatePressurePlate();
        bool NotifyCombatObjectiveCleared(int floor);
        bool CompleteExplorationFloor(int floor);
        bool TryUseHealingSpring();
        void NotifyBossDefeated();
    }
}
