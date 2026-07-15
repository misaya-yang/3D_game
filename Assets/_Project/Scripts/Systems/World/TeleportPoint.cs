using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class TeleportPoint : MonoBehaviour
    {
        public const float TriggerRadius = 1.6f;

        [SerializeField] private string _teleportId = string.Empty;
        [SerializeField] private string _mapId = string.Empty;

        public string TeleportId => _teleportId ?? string.Empty;
        public string MapId => _mapId ?? string.Empty;

        private void Awake()
        {
            ConfigureCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDiscover(other != null ? other.gameObject : null);
        }

        public void Configure(string teleportId, string mapId)
        {
            _teleportId = teleportId?.Trim() ?? string.Empty;
            _mapId = mapId?.Trim() ?? string.Empty;
            ConfigureCollider();
        }

        public bool TryDiscover(GameObject traveler)
        {
            if (traveler == null
                || !WorldActorUtility.IsPlayer(traveler)
                || string.IsNullOrWhiteSpace(TeleportId)
                || !ServiceLocator.TryGet<IMapTravelService>(
                    out IMapTravelService travel))
            {
                return false;
            }

            bool wasUnlocked = travel.IsTeleportUnlocked(TeleportId);
            travel.UnlockMap(MapId);
            travel.UnlockTeleport(TeleportId);
            return !wasUnlocked && travel.IsTeleportUnlocked(TeleportId);
        }

        private void ConfigureCollider()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                return;
            }

            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;
        }
    }
}
