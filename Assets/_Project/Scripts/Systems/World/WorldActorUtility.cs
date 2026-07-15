using UnityEngine;
using Wendao.Systems.Combat;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.World
{
    public static class WorldActorUtility
    {
        public static bool IsPlayer(GameObject actor)
        {
            return TryGetPlayerHealth(actor, out _);
        }

        public static bool TryGetPlayerHealth(
            GameObject actor,
            out IPlayerHealthService health)
        {
            health = null;
            if (actor == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = actor.GetComponentsInParent<MonoBehaviour>(
                true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlayerHealthService candidate)
                {
                    health = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetPlayerDamageable(
            GameObject actor,
            out IDamageable damageable)
        {
            damageable = null;
            if (actor == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = actor.GetComponentsInParent<MonoBehaviour>(
                true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlayerHealthService
                    && behaviour is IDamageable candidate)
                {
                    damageable = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
