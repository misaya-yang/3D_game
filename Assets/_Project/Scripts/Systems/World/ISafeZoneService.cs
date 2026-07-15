using UnityEngine;

namespace Wendao.Systems.World
{
    public interface ISafeZoneService
    {
        bool IsPositionSafe(Vector3 position);
        float GetRecoveryMultiplier(Vector3 position);
    }
}
