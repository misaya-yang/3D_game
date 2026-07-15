using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindPressurePlate : MonoBehaviour
    {
        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.5f, 0.8f, 2.5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryActivate(other != null ? other.gameObject : null);
        }

        public bool TryActivate(GameObject actor)
        {
            return WorldActorUtility.IsPlayer(actor)
                && ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                && dungeon.ActivatePressurePlate();
        }
    }
}
