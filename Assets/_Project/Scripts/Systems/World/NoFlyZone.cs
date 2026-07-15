using UnityEngine;
using Wendao.Core;
using Wendao.Systems.Mount;
using Wendao.Systems.Player;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(Collider))]
    public sealed class NoFlyZone : MonoBehaviour
    {
        [SerializeField] private string _zoneId = "no_fly_zone";

        public string ZoneId => _zoneId;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        public void Configure(string zoneId)
        {
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                _zoneId = zoneId;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            SetActiveForPlayer(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            SetActiveForPlayer(other, false);
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet<IMountService>(out IMountService mounts))
            {
                mounts.SetNoFlyZoneActive(_zoneId, false);
            }
        }

        private void SetActiveForPlayer(Collider other, bool active)
        {
            if (other == null
                || !ServiceLocator.TryGet<IPlayerMountLocomotion>(
                    out IPlayerMountLocomotion player)
                || player.Actor == null
                || (other.gameObject != player.Actor
                    && !other.transform.IsChildOf(player.Actor.transform))
                || !ServiceLocator.TryGet<IMountService>(out IMountService mounts))
            {
                return;
            }

            mounts.SetNoFlyZoneActive(_zoneId, active);
        }
    }
}
