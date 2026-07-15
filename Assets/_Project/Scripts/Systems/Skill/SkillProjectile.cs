using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Feedback;

namespace Wendao.Systems.Skill
{
    public sealed class SkillProjectile : MonoBehaviour
    {
        public const float DefaultSpeed = 14f;
        public const float CollisionRadius = 0.15f;

        private const int MaximumCastHits = 16;

        private readonly RaycastHit[] _hits = new RaycastHit[MaximumCastHits];

        private DamageRequest _damageRequest;
        private ICombatService _combatService;
        private Vector3 _direction;
        private float _remainingRange;
        private Material _runtimeMaterial;
        private IVfxService _vfxService;
        private GameObject _travelVfx;
        private string _impactVfxId = string.Empty;
        private bool _initialized;

        public string SkillId { get; private set; }
        public float TravelledDistance { get; private set; }

        public static SkillProjectile Spawn(
            string skillId,
            Vector3 origin,
            Vector3 targetPoint,
            float range,
            DamageRequest damageRequest)
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "SkillProjectile_" + skillId;
            projectileObject.transform.position = origin;
            projectileObject.transform.localScale = Vector3.one * 0.3f;

            Collider visualCollider = projectileObject.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
                Destroy(visualCollider);
            }

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = null;
            if (renderer != null && shader != null)
            {
                material = new Material(shader)
                {
                    name = "QiBolt_Greybox_Runtime",
                    color = new Color(0.38f, 0.92f, 0.72f, 1f)
                };
                renderer.sharedMaterial = material;
            }

            SkillProjectile projectile = projectileObject.AddComponent<SkillProjectile>();
            projectile.Initialize(
                skillId,
                origin,
                targetPoint,
                range,
                damageRequest,
                material);
            return projectile;
        }

        public void Initialize(
            string skillId,
            Vector3 origin,
            Vector3 targetPoint,
            float range,
            DamageRequest damageRequest,
            Material runtimeMaterial = null)
        {
            SkillId = skillId ?? string.Empty;
            transform.position = origin;
            Vector3 direction = targetPoint - origin;
            _direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            _remainingRange = Mathf.Max(0.01f, range);
            _damageRequest = damageRequest;
            _runtimeMaterial = runtimeMaterial;
            TravelledDistance = 0f;
            _initialized = true;
            ServiceLocator.TryGet(out _combatService);
            ServiceLocator.TryGet(out _vfxService);
            SkillData skill = ConfigDatabase.Instance?.GetSkill(SkillId);
            if (skill != null)
            {
                _impactVfxId = skill.ImpactVfxId ?? string.Empty;
                if (_vfxService != null
                    && VfxContentIds.IsKnown(skill.ProjectileVfxId))
                {
                    float duration = _remainingRange / DefaultSpeed + 0.5f;
                    _travelVfx = _vfxService.PlayAttached(
                        skill.ProjectileVfxId,
                        transform,
                        duration);
                }
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (_combatService == null)
            {
                ServiceLocator.TryGet(out _combatService);
            }

            float distance = Mathf.Min(
                _remainingRange,
                DefaultSpeed * Mathf.Max(0f, Time.deltaTime));
            if (distance <= 0f)
            {
                DestroyProjectile(false);
                return;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                CollisionRadius,
                _direction,
                _hits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            IDamageable closestTarget = null;
            float closestDistance = float.MaxValue;
            for (int index = 0; index < hitCount; index++)
            {
                IDamageable candidate = FindDamageable(_hits[index].collider);
                GameObject candidateObject = GetActorObject(candidate);
                if (candidate == null
                    || candidate.IsDead
                    || candidateObject == null
                    || candidateObject == _damageRequest.Source
                    || (_damageRequest.Source != null
                        && candidateObject.transform.IsChildOf(
                            _damageRequest.Source.transform))
                    || _hits[index].distance >= closestDistance)
                {
                    continue;
                }

                closestTarget = candidate;
                closestDistance = _hits[index].distance;
            }

            if (closestTarget != null && _combatService != null)
            {
                transform.position += _direction * Mathf.Max(0f, closestDistance);
                _combatService.DealDamage(closestTarget, _damageRequest);
                DestroyProjectile(true);
                return;
            }

            transform.position += _direction * distance;
            TravelledDistance += distance;
            _remainingRange -= distance;
            if (_remainingRange <= 0.0001f)
            {
                DestroyProjectile(false);
            }
        }

        private void OnDestroy()
        {
            if (_travelVfx != null)
            {
                _vfxService?.Stop(_travelVfx);
                _travelVfx = null;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        private void DestroyProjectile(bool playImpact)
        {
            _initialized = false;
            if (playImpact
                && _vfxService != null
                && VfxContentIds.IsKnown(_impactVfxId))
            {
                _vfxService.Play(
                    _impactVfxId,
                    transform.position,
                    Quaternion.identity,
                    0.8f);
            }

            Destroy(gameObject);
        }

        private static IDamageable FindDamageable(Collider collider)
        {
            Transform current = collider != null ? collider.transform : null;
            while (current != null)
            {
                MonoBehaviour[] components = current.GetComponents<MonoBehaviour>();
                for (int index = 0; index < components.Length; index++)
                {
                    if (components[index] is IDamageable damageable)
                    {
                        return damageable;
                    }
                }

                current = current.parent;
            }

            return null;
        }

        private static GameObject GetActorObject(IDamageable actor)
        {
            return actor is Component component ? component.gameObject : null;
        }
    }
}
