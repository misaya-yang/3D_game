using UnityEngine;
using Wendao.Core;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SerendipityTrigger : MonoBehaviour
    {
        public string SerendipityId { get; private set; } = string.Empty;
        public string MapId { get; private set; } = string.Empty;
        public bool IsConsumed { get; private set; }

        private SphereCollider _trigger;
        private Renderer _visual;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = 1.4f;
        }

        private void Start()
        {
            RefreshConsumedState();
        }

        private void Update()
        {
            if (!IsConsumed)
            {
                RefreshConsumedState();
            }
        }

        public void Configure(
            string serendipityId,
            string mapId,
            Renderer visual)
        {
            SerendipityId = serendipityId ?? string.Empty;
            MapId = mapId ?? string.Empty;
            _visual = visual;
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = 1.4f;
            RefreshConsumedState();
        }

        public bool TryActivate()
        {
            if (IsConsumed
                || !ServiceLocator.TryGet<ISerendipityService>(
                    out ISerendipityService service)
                || !service.TryTrigger(
                    SerendipityId,
                    MapId,
                    transform.position))
            {
                return false;
            }

            SetConsumed(true);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (WorldActorUtility.IsPlayer(
                    other != null ? other.gameObject : null))
            {
                TryActivate();
            }
        }

        private void RefreshConsumedState()
        {
            bool consumed = !string.IsNullOrEmpty(SerendipityId)
                && ServiceLocator.TryGet<ISerendipityService>(
                    out ISerendipityService service)
                && service.HasCompleted(SerendipityId);
            SetConsumed(consumed);
        }

        private void SetConsumed(bool consumed)
        {
            IsConsumed = consumed;
            if (_trigger != null)
            {
                _trigger.enabled = !consumed;
            }

            if (_visual != null)
            {
                _visual.enabled = !consumed;
            }
        }
    }
}
