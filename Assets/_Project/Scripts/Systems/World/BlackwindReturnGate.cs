using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindReturnGate : MonoBehaviour
    {
        private bool _travelRequested;

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryReturn(other != null ? other.gameObject : null);
        }

        public bool TryReturn(GameObject actor)
        {
            if (_travelRequested
                || !WorldActorUtility.IsPlayer(actor)
                || !ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService dungeon)
                || !dungeon.IsRunCompleted
                || SceneLoader.Instance == null)
            {
                return false;
            }

            _travelRequested = SceneLoader.Instance.LoadMap(
                MapContentIds.CangwuMap,
                MapContentIds.CangwuGateTeleport);
            return _travelRequested;
        }
    }
}
