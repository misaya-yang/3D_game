using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Loot
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WorldItemPickup : SafeBehaviour
    {
        public const float DefaultLifetimeSeconds = 180f;

        private float _remainingLifetime = DefaultLifetimeSeconds;

        public string ItemId { get; private set; } = string.Empty;
        public int Count { get; private set; }
        public bool IsCollected { get; private set; }
        public string NameLocalizationKey => string.IsNullOrEmpty(ItemId)
            ? string.Empty
            : "item_name_" + ItemId;

        private void Awake()
        {
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void Update()
        {
            if (IsCollected)
            {
                return;
            }

            _remainingLifetime -= Mathf.Max(0f, Time.deltaTime);
            if (_remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            TryCollect();
        }

        public void Configure(
            string itemId,
            int count,
            float lifetimeSeconds = DefaultLifetimeSeconds)
        {
            ItemId = itemId ?? string.Empty;
            Count = Mathf.Max(0, count);
            _remainingLifetime = Mathf.Max(0.1f, lifetimeSeconds);
            IsCollected = false;
        }

        public bool TryCollect()
        {
            if (IsCollected
                || string.IsNullOrEmpty(ItemId)
                || Count <= 0
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || !inventory.AddItem(ItemId, Count, AcquireSource.Loot))
            {
                return false;
            }

            IsCollected = true;
            Destroy(gameObject);
            return true;
        }

        private static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(
                true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IPlayerHealthService)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
