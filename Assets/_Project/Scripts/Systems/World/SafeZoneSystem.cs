using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    public sealed class SafeZoneSystem : SafeBehaviour, ISafeZoneService
    {
        private readonly List<SafeZone> _zones = new List<SafeZone>();
        private bool _registeredService;

        public int ActiveZoneCount
        {
            get
            {
                PruneDestroyedZones();
                return _zones.Count;
            }
        }

        private void Awake()
        {
            if (ServiceLocator.TryGet<ISafeZoneService>(
                    out ISafeZoneService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ISafeZoneService>(this);
            _registeredService = true;
            DontDestroyOnLoad(gameObject);
            SafeZone[] existingZones = FindObjectsByType<SafeZone>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < existingZones.Length; index++)
            {
                Register(existingZones[index]);
            }
        }

        private void OnDestroy()
        {
            _zones.Clear();
            if (_registeredService
                && ServiceLocator.TryGet<ISafeZoneService>(
                    out ISafeZoneService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ISafeZoneService>();
            }

            _registeredService = false;
        }

        public void Register(SafeZone zone)
        {
            if (zone != null && !_zones.Contains(zone))
            {
                _zones.Add(zone);
            }
        }

        public void Unregister(SafeZone zone)
        {
            if (zone != null)
            {
                _zones.Remove(zone);
            }
        }

        public bool IsPositionSafe(Vector3 position)
        {
            PruneDestroyedZones();
            for (int index = 0; index < _zones.Count; index++)
            {
                if (_zones[index].Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetRecoveryMultiplier(Vector3 position)
        {
            PruneDestroyedZones();
            float multiplier = 1f;
            for (int index = 0; index < _zones.Count; index++)
            {
                SafeZone zone = _zones[index];
                if (zone.Contains(position))
                {
                    multiplier = Mathf.Max(
                        multiplier,
                        zone.RecoveryMultiplier);
                }
            }

            return multiplier;
        }

        private void PruneDestroyedZones()
        {
            _zones.RemoveAll(zone => zone == null);
        }
    }
}
