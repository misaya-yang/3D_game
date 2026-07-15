using System.Collections.Generic;

namespace Wendao.Systems.World
{
    public interface IMapTravelService
    {
        IReadOnlyList<string> UnlockedMaps { get; }
        IReadOnlyList<string> UnlockedTeleports { get; }

        bool IsMapUnlocked(string mapId);
        bool IsTeleportUnlocked(string teleportId);
        void UnlockMap(string mapId);
        void UnlockTeleport(string teleportId);
        bool Travel(string teleportId);
    }
}
