using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SafeZone : MonoBehaviour
    {
        public const float DefaultRecoveryMultiplier = 2f;

        private BoxCollider _trigger;
        private SafeZoneSystem _registeredSystem;

        public string Id { get; private set; } = string.Empty;
        public float RecoveryMultiplier { get; private set; } =
            DefaultRecoveryMultiplier;
        public Bounds WorldBounds => _trigger != null
            ? _trigger.bounds
            : new Bounds(transform.position, Vector3.zero);

        private void Awake()
        {
            EnsureTrigger();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            _registeredSystem?.Unregister(this);
            _registeredSystem = null;
        }

        public void Configure(
            string id,
            Vector3 size,
            float recoveryMultiplier = DefaultRecoveryMultiplier)
        {
            Id = id ?? string.Empty;
            RecoveryMultiplier = Mathf.Max(1f, recoveryMultiplier);
            EnsureTrigger();
            _trigger.center = Vector3.zero;
            _trigger.size = new Vector3(
                Mathf.Max(0.1f, size.x),
                Mathf.Max(0.1f, size.y),
                Mathf.Max(0.1f, size.z));
            TryRegister();
        }

        public bool Contains(Vector3 position)
        {
            EnsureTrigger();
            Vector3 closest = _trigger.ClosestPoint(position);
            return (closest - position).sqrMagnitude <= 0.0001f;
        }

        private void EnsureTrigger()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<BoxCollider>();
            }

            _trigger.isTrigger = true;
        }

        private void TryRegister()
        {
            if (_registeredSystem != null)
            {
                return;
            }

            if (ServiceLocator.TryGet<ISafeZoneService>(
                    out ISafeZoneService service)
                && service is SafeZoneSystem system)
            {
                _registeredSystem = system;
                system.Register(this);
            }
        }
    }
}
