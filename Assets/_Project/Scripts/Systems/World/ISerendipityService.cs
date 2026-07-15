using UnityEngine;

namespace Wendao.Systems.World
{
    public interface ISerendipityService
    {
        bool TryTrigger(string id);
        bool TryTrigger(string id, string mapId, Vector3 rewardOrigin);
        bool HasCompleted(string id);
    }
}
