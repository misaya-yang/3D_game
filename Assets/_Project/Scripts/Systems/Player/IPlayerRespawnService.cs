namespace Wendao.Systems.Player
{
    public interface IPlayerRespawnService
    {
        bool CanRespawn { get; }
        string NearestRespawnPointId { get; }

        bool TryRespawnAtNearestPoint();
    }
}
