using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.World;

namespace Wendao.Systems.Combat
{
    public sealed class CombatSystem : SafeBehaviour, ICombatService
    {
        private const int MaximumMeleeColliders = 32;

        [SerializeField] private LayerMask _meleeMask = ~0;

        private readonly Collider[] _meleeColliders =
            new Collider[MaximumMeleeColliders];
        private readonly HashSet<IDamageable> _actors =
            new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _hitThisSwing =
            new HashSet<IDamageable>();

        private IStatusEffectService _statusEffects;
        private bool _registeredService;

        public int RegisteredActorCount => _actors.Count;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ICombatService>(out ICombatService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ICombatService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            _actors.Clear();
            _hitThisSwing.Clear();
            _statusEffects = null;

            if (!_registeredService)
            {
                return;
            }

            if (ServiceLocator.TryGet<ICombatService>(out ICombatService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ICombatService>();
            }

            _registeredService = false;
        }

        public DamageInfo ComputeDamage(DamageRequest request)
        {
            return ComputeDamage(request, null);
        }

        public DamageInfo ComputeDamage(DamageRequest request, IDamageable target)
        {
            GameObject targetObject = GetActorObject(target);
            ResolveStatusEffectService();
            ElementReactionResolution reaction =
                ElementReactionResolver.Resolve(
                    request.Element,
                    targetObject,
                    _statusEffects);
            return ComputeDamageInternal(request, target, reaction);
        }

        private DamageInfo ComputeDamageInternal(
            DamageRequest request,
            IDamageable target,
            ElementReactionResolution reaction)
        {
            GameObject targetObject = GetActorObject(target);
            ICombatStatsProvider sourceStats = FindStatsProvider(request.Source);
            ICombatStatsProvider targetStats = target as ICombatStatsProvider;
            if (targetStats == null)
            {
                targetStats = FindStatsProvider(GetActorObject(target));
            }

            ICombatDefenseProvider targetDefense =
                target as ICombatDefenseProvider;
            if (targetDefense == null)
            {
                targetDefense = FindDefenseProvider(GetActorObject(target));
            }

            ICombatDamageReductionProvider targetDamageReduction =
                target as ICombatDamageReductionProvider;
            if (targetDamageReduction == null)
            {
                targetDamageReduction = FindDamageReductionProvider(
                    GetActorObject(target));
            }

            ResolveStatusEffectService();
            float attack = request.IgnoreAttackScaling
                ? 0f
                : Mathf.Max(0f, sourceStats?.Attack ?? 0f);
            if (!request.IgnoreAttackScaling && _statusEffects != null)
            {
                attack *= _statusEffects.GetAttackMultiplier(request.Source);
            }

            float multiplier = request.Multiplier > 0f ? request.Multiplier : 1f;
            float amount = Mathf.Max(0f, request.BaseDamage)
                * (1f + attack / 100f)
                * multiplier;
            if (_statusEffects != null)
            {
                amount *= _statusEffects.GetDamageDealtMultiplier(request.Source);
            }

            if (request.Element != ElementType.None
                && ServiceLocator.TryGet<IWeatherService>(
                    out IWeatherService weather))
            {
                amount *= 1f + Mathf.Max(
                    0f,
                    weather.GetElementDamageBonus(request.Element));
            }

            if (FindTeam(request.Source) == CombatTeam.Enemy
                && ServiceLocator.TryGet<IDayNightService>(
                    out IDayNightService dayNight))
            {
                amount *= Mathf.Max(0f, dayNight.EnemyAttackMultiplier);
            }

            float critRate = Mathf.Clamp01(sourceStats?.CritRate ?? 0f);
            bool isCritical = request.CanCrit
                && critRate > 0f
                && UnityEngine.Random.value < critRate;
            if (isCritical)
            {
                amount *= Mathf.Max(1f, sourceStats?.CritDamage ?? 1.5f);
            }

            if (request.Type != DamageType.True)
            {
                float defense = Mathf.Max(0f, targetStats?.Defense ?? 0f);
                if (_statusEffects != null)
                {
                    defense *= _statusEffects.GetDefenseMultiplier(targetObject);
                }

                amount *= 100f / (100f + defense);
            }

            amount *= Mathf.Max(0f, reaction.DamageMultiplier);

            if (request.Type != DamageType.True
                && targetDamageReduction != null)
            {
                float reduction = Mathf.Clamp01(
                    targetDamageReduction.GetDamageReduction(request.Type));
                amount *= 1f - reduction;
            }

            if (targetDefense != null)
            {
                if (targetDefense.IsInvincible)
                {
                    amount = 0f;
                }
                else
                {
                    float blockReduction = Mathf.Clamp01(
                        targetDefense.GetBlockDamageReduction(request.Type));
                    amount *= 1f - blockReduction;
                }
            }

            float finalAmount = amount > 0f
                ? Mathf.Max(1f, amount)
                : 0f;

            return new DamageInfo
            {
                Target = GetActorObject(target),
                Source = request.Source,
                Amount = finalAmount,
                Type = request.Type,
                IsCritical = isCritical,
                IsKillingBlow = false,
                HitPoint = GetActorHitPoint(target),
                Element = request.Element,
                SkillId = request.SkillId ?? string.Empty,
                Reaction = reaction.Reaction,
                ReactionMultiplier = reaction.DamageMultiplier,
                HitstopSeconds = Mathf.Max(0f, request.HitstopSeconds),
                HitstunSeconds = Mathf.Max(0f, request.HitstunSeconds)
            };
        }

        public void DealDamage(IDamageable target, DamageRequest request)
        {
            DealDamageInternal(target, request, GetActorHitPoint(target));
        }

        public bool TryMeleeHit(
            Transform attacker,
            float range,
            float angle,
            DamageRequest request)
        {
            if (attacker == null || range <= 0f)
            {
                return false;
            }

            Vector3 origin = attacker.position + Vector3.up * 0.9f;
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                range,
                _meleeColliders,
                _meleeMask,
                QueryTriggerInteraction.Ignore);
            _hitThisSwing.Clear();

            Vector3 attackerForward = attacker.forward;
            attackerForward.y = 0f;
            if (attackerForward.sqrMagnitude <= 0.0001f)
            {
                attackerForward = Vector3.forward;
            }
            else
            {
                attackerForward.Normalize();
            }

            float halfAngle = Mathf.Clamp(angle, 0f, 360f) * 0.5f;
            bool hitAny = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = _meleeColliders[i];
                IDamageable candidate = FindDamageable(candidateCollider);
                if (candidate == null
                    || candidate.IsDead
                    || !_hitThisSwing.Add(candidate))
                {
                    continue;
                }

                GameObject targetObject = GetActorObject(candidate);
                if (targetObject == null
                    || targetObject == attacker.gameObject
                    || targetObject.transform.IsChildOf(attacker)
                    || attacker.IsChildOf(targetObject.transform))
                {
                    continue;
                }

                Vector3 hitPoint = candidateCollider.bounds.center;
                Vector3 toTarget = hitPoint - attacker.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude <= 0.0001f
                    || (halfAngle < 180f
                        && Vector3.Angle(attackerForward, toTarget) > halfAngle))
                {
                    continue;
                }

                DamageRequest resolvedRequest = request;
                if (resolvedRequest.Source == null)
                {
                    resolvedRequest.Source = attacker.gameObject;
                }

                DealDamageInternal(candidate, resolvedRequest, hitPoint);
                hitAny = true;
            }

            _hitThisSwing.Clear();
            return hitAny;
        }

        public void RegisterActor(IDamageable actor)
        {
            if (actor != null)
            {
                _actors.Add(actor);
            }
        }

        public void UnregisterActor(IDamageable actor)
        {
            if (actor != null)
            {
                _actors.Remove(actor);
                ResolveStatusEffectService();
                _statusEffects?.ClearAll(GetActorObject(actor));
            }
        }

        private static ICombatStatsProvider FindStatsProvider(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            MonoBehaviour[] components = actor.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ICombatStatsProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        private static ICombatDefenseProvider FindDefenseProvider(
            GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            MonoBehaviour[] components = actor.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ICombatDefenseProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        private static ICombatDamageReductionProvider
            FindDamageReductionProvider(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            MonoBehaviour[] components = actor.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is ICombatDamageReductionProvider provider)
                {
                    return provider;
                }
            }

            return null;
        }

        private static IDamageable FindDamageable(Collider candidateCollider)
        {
            if (candidateCollider == null)
            {
                return null;
            }

            Transform current = candidateCollider.transform;
            while (current != null)
            {
                MonoBehaviour[] components = current.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IDamageable damageable)
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

        private static Vector3 GetActorHitPoint(IDamageable actor)
        {
            if (!(actor is Component component))
            {
                return Vector3.zero;
            }

            Collider collider = component.GetComponent<Collider>();
            return collider != null ? collider.bounds.center : component.transform.position;
        }

        private void DealDamageInternal(
            IDamageable target,
            DamageRequest request,
            Vector3 hitPoint)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            GameObject targetObject = GetActorObject(target);
            if (IsBlockedBySafeZone(request.Source, targetObject))
            {
                return;
            }

            ResolveStatusEffectService();
            ElementReactionResolution reaction =
                ElementReactionResolver.Resolve(
                    request.Element,
                    targetObject,
                    _statusEffects);
            DamageInfo info = ComputeDamageInternal(request, target, reaction);
            info.HitPoint = hitPoint;
            info.IsKillingBlow = info.Amount > 0f
                && info.Amount >= target.CurrentHp;
            bool wasDead = target.IsDead;
            target.ApplyDamage(info);
            if (info.Amount > 0f && !target.IsDead)
            {
                TryApplyHitstun(targetObject, info.HitstunSeconds);
            }

            EventBus.Publish(CombatEvents.DamageApplied, info);
            int spreadTargetCount = 0;
            if (info.Amount > 0f && reaction.Reaction != ElementReactionType.None)
            {
                spreadTargetCount = CommitReaction(
                    reaction,
                    target,
                    request.Source);
                EventBus.Publish(
                    CombatEvents.ElementReactionTriggered,
                    new ElementReactionInfo
                    {
                        Target = targetObject,
                        Source = request.Source,
                        Reaction = reaction.Reaction,
                        AttackElement = reaction.AttackElement,
                        ExistingStatusId = reaction.ExistingStatusId,
                        DamageMultiplier = reaction.DamageMultiplier,
                        SpreadTargetCount = spreadTargetCount
                    });
            }

            if (info.Amount > 0f && !target.IsDead)
            {
                TryApplyOnHitStatus(targetObject, request);
            }

            if (!wasDead
                && target.IsDead
                && target is ICombatDeathHandler deathHandler)
            {
                deathHandler.HandleDeath(info);
            }

            if (target.IsDead)
            {
                _statusEffects?.ClearAll(targetObject);
            }
        }

        private static void TryApplyHitstun(
            GameObject targetObject,
            float baseDuration)
        {
            if (targetObject == null || baseDuration <= 0f)
            {
                return;
            }

            MonoBehaviour[] components =
                targetObject.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (!(components[index] is IHitstunReceiver receiver))
                {
                    continue;
                }

                receiver.ApplyHitstun(
                    baseDuration * Mathf.Max(0f, receiver.HitstunMultiplier));
                return;
            }
        }

        private int CommitReaction(
            ElementReactionResolution reaction,
            IDamageable target,
            GameObject source)
        {
            if (_statusEffects == null)
            {
                return 0;
            }

            GameObject targetObject = GetActorObject(target);
            switch (reaction.Reaction)
            {
                case ElementReactionType.BurnBurst:
                    _statusEffects.Remove(
                        reaction.ExistingStatusId,
                        targetObject);
                    break;
                case ElementReactionType.Shock:
                    if (!target.IsDead)
                    {
                        _statusEffects.Apply(
                            StatusEffectContentIds.ShockStun,
                            targetObject,
                            source);
                    }

                    break;
                case ElementReactionType.Sever:
                    if (!target.IsDead)
                    {
                        _statusEffects.Apply(
                            StatusEffectContentIds.SeverDefense,
                            targetObject,
                            source);
                    }

                    break;
                case ElementReactionType.Spread:
                    return SpreadAuras(target, source);
            }

            return 0;
        }

        private int SpreadAuras(IDamageable primaryTarget, GameObject source)
        {
            GameObject primaryObject = GetActorObject(primaryTarget);
            if (primaryObject == null)
            {
                return 0;
            }

            CombatTeam sourceTeam = FindTeam(source);
            CombatTeam targetTeam = FindTeam(primaryObject);
            int affectedTargets = 0;
            foreach (IDamageable actor in _actors)
            {
                GameObject actorObject = GetActorObject(actor);
                if (actor == null
                    || actor.IsDead
                    || actorObject == null
                    || actorObject == primaryObject
                    || actorObject == source
                    || Vector3.Distance(
                        actorObject.transform.position,
                        primaryObject.transform.position)
                        > FormulaLibrary.SpreadRadius)
                {
                    continue;
                }

                CombatTeam actorTeam = FindTeam(actorObject);
                if ((sourceTeam != CombatTeam.Neutral && actorTeam == sourceTeam)
                    || (targetTeam != CombatTeam.Neutral
                        && actorTeam != CombatTeam.Neutral
                        && actorTeam != targetTeam))
                {
                    continue;
                }

                if (_statusEffects.CopyAuraStatuses(
                        primaryObject,
                        actorObject,
                        source) > 0)
                {
                    affectedTargets++;
                }
            }

            return affectedTargets;
        }

        private void TryApplyOnHitStatus(
            GameObject target,
            DamageRequest request)
        {
            if (_statusEffects == null
                || target == null
                || string.IsNullOrEmpty(request.StatusOnHitId))
            {
                return;
            }

            float chance = Mathf.Clamp01(request.StatusChance);
            if (chance <= 0f || UnityEngine.Random.value > chance)
            {
                return;
            }

            _statusEffects.TryApply(
                request.StatusOnHitId,
                target,
                request.Source,
                1,
                request.BaseDamage);
        }

        private void ResolveStatusEffectService()
        {
            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }
        }

        private static CombatTeam FindTeam(GameObject actor)
        {
            if (actor == null)
            {
                return CombatTeam.Neutral;
            }

            MonoBehaviour[] components = actor.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is ICombatTeamProvider provider)
                {
                    return provider.Team;
                }
            }

            return CombatTeam.Neutral;
        }

        private static bool IsBlockedBySafeZone(
            GameObject source,
            GameObject target)
        {
            if (target == null
                || !ServiceLocator.TryGet<ISafeZoneService>(
                    out ISafeZoneService safeZones))
            {
                return false;
            }

            CombatTeam sourceTeam = FindTeam(source);
            if (sourceTeam == CombatTeam.Enemy
                && safeZones.IsPositionSafe(target.transform.position))
            {
                return true;
            }

            return sourceTeam == CombatTeam.Player
                && FindTeam(target) == CombatTeam.Player
                && (safeZones.IsPositionSafe(source.transform.position)
                    || safeZones.IsPositionSafe(target.transform.position));
        }
    }
}
