using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindFloorCompletionTrigger : MonoBehaviour
    {
        [SerializeField, Range(1, 5)] private int _floor = 3;

        public int Floor => _floor;

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryComplete(other != null ? other.gameObject : null);
        }

        public void Configure(int floor)
        {
            _floor = Mathf.Clamp(floor, 1, 5);
        }

        public bool TryComplete(GameObject actor)
        {
            return WorldActorUtility.IsPlayer(actor)
                && ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                && dungeon.CompleteExplorationFloor(_floor);
        }
    }
}
