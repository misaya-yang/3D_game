namespace Wendao.Systems.Crafting
{
    public interface IGatheringService
    {
        int Level { get; }
        bool IsGathering { get; }
        GatherableObject ActiveGatherable { get; }
        float Progress01 { get; }
        float RemainingSeconds { get; }

        bool CanGather(GatherableObject gatherable);
        bool Gather(GatherableObject gatherable);
        bool CancelActiveGather();
    }
}
