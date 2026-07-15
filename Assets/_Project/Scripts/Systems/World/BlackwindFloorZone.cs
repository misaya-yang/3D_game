using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindFloorZone : MonoBehaviour
    {
        [SerializeField, Range(1, 5)] private int _floor = 1;

        public int Floor => _floor;

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryEnter(other != null ? other.gameObject : null);
        }

        public void Configure(int floor)
        {
            _floor = Mathf.Clamp(floor, 1, 5);
        }

        public bool TryEnter(GameObject actor)
        {
            return WorldActorUtility.IsPlayer(actor)
                && ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                && dungeon.EnterFloor(_floor);
        }
    }
}
