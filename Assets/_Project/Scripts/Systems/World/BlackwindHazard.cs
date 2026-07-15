using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindHazard : MonoBehaviour
    {
        public const float DefaultDamage = 25f;

        [SerializeField, Min(1f)] private float _damage = DefaultDamage;

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other != null ? other.gameObject : null);
        }

        public bool TryDamage(GameObject actor)
        {
            if (!WorldActorUtility.TryGetPlayerDamageable(
                    actor,
                    out IDamageable damageable)
                || !ServiceLocator.TryGet<ICombatService>(
                    out ICombatService combat))
            {
                return false;
            }

            combat.DealDamage(
                damageable,
                new DamageRequest
                {
                    Source = gameObject,
                    BaseDamage = Mathf.Max(1f, _damage),
                    Multiplier = 1f,
                    Type = DamageType.True,
                    CanCrit = false,
                    SkillId = MapContentIds.BlackwindSpikeHazard
                });
            return true;
        }
    }
}
