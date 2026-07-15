using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.Loot
{
    public interface ILootService
    {
        int ActiveWorldPickupCount { get; }

        void DropLoot(EnemyData data, Vector3 position);
        WorldItemPickup SpawnWorldPickup(
            string itemId,
            int count,
            Vector3 position);
    }
}
