using UnityEngine;
using Wendao.Core;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class BlackwindHealingSpring : MonoBehaviour
    {
        public const float HealFraction = 0.5f;

        private void Awake()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDrink(other != null ? other.gameObject : null);
        }

        public bool TryDrink(GameObject actor)
        {
            if (!WorldActorUtility.TryGetPlayerHealth(
                    actor,
                    out IPlayerHealthService health)
                || !ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                || !dungeon.TryUseHealingSpring())
            {
                return false;
            }

            health.ApplyHeal(
                health.MaxHp * HealFraction,
                MapContentIds.BlackwindHealingSpring);
            return true;
        }
    }
}
