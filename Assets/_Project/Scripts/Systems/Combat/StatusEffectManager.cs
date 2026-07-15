using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.Combat
{
    public sealed class StatusEffectManager : SafeBehaviour, IStatusEffectService
    {
        private const float Epsilon = 0.0001f;

        private static readonly ElementType[] AuraPriority =
        {
            ElementType.Ice,
            ElementType.Poison,
            ElementType.Water,
            ElementType.Wood,
            ElementType.Fire,
            ElementType.Earth,
            ElementType.Lightning,
            ElementType.Wind,
            ElementType.Metal,
            ElementType.Dark
        };

        private readonly Dictionary<EntityId, TargetState> _targets =
            new Dictionary<EntityId, TargetState>();
        private bool _registeredService;

        public int ActiveTargetCount => _targets.Count;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IStatusEffectService>(
                    out IStatusEffectService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IStatusEffectService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            RepairServiceRegistration();
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.State == GameState.Playing)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            _targets.Clear();
            if (_registeredService
                && ServiceLocator.TryGet<IStatusEffectService>(
                    out IStatusEffectService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IStatusEffectService>();
            }

            _registeredService = false;
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IStatusEffectService>(
                    out IStatusEffectService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IStatusEffectService>(this);
            _registeredService = true;
        }

        public void Apply(
            string statusId,
            GameObject target,
            GameObject source,
            int stacks = 1)
        {
            TryApply(statusId, target, source, stacks, 0f);
        }

        public bool TryApply(
            string statusId,
            GameObject target,
            GameObject source,
            int stacks,
            float sourceBaseDamage)
        {
            StatusEffectData data = ResolveData(statusId);
            if (data == null
                || target == null
                || stacks <= 0
                || data.Duration <= 0f
                || IsDead(target))
            {
                return false;
            }

            TargetState state = GetOrCreateState(target);
            if (state.Cooldowns.TryGetValue(data.Id, out float cooldown)
                && cooldown > Epsilon)
            {
                return false;
            }

            int maximumStacks = Mathf.Max(1, data.MaxStacks);
            if (state.Effects.TryGetValue(data.Id, out ActiveStatus active))
            {
                int previousStacks = active.Stacks;
                active.Stacks = Mathf.Min(
                    maximumStacks,
                    active.Stacks + stacks);
                active.RemainingDuration = Mathf.Max(0f, data.Duration);
                active.Source = source != null ? source : active.Source;
                if (sourceBaseDamage > 0f)
                {
                    active.SourceBaseDamage = sourceBaseDamage;
                }

                PublishChanged(
                    state,
                    active,
                    active.Stacks != previousStacks
                        ? StatusEffectChangeType.StackChanged
                        : StatusEffectChangeType.Refreshed);
                TryPromote(state, active);
                return true;
            }

            active = new ActiveStatus
            {
                Data = data,
                Source = source,
                Stacks = Mathf.Min(maximumStacks, stacks),
                RemainingDuration = Mathf.Max(0f, data.Duration),
                DotElapsed = 0f,
                SourceBaseDamage = Mathf.Max(0f, sourceBaseDamage)
            };
            state.Effects.Add(data.Id, active);
            if (data.ReapplyCooldown > 0f)
            {
                state.Cooldowns[data.Id] = data.ReapplyCooldown;
            }

            PublishChanged(state, active, StatusEffectChangeType.Applied);
            TryPromote(state, active);
            return true;
        }

        public void Remove(string statusId, GameObject target)
        {
            if (!TryGetState(target, out TargetState state)
                || string.IsNullOrEmpty(statusId))
            {
                return;
            }

            RemoveInternal(state, statusId, StatusEffectChangeType.Removed);
            RemoveStateIfEmpty(state);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
                || _targets.Count == 0)
            {
                return;
            }

            var states = new List<TargetState>(_targets.Values);
            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                TargetState state = states[stateIndex];
                if (state.Target == null)
                {
                    _targets.Remove(state.TargetId);
                    continue;
                }

                TickCooldowns(state, deltaTime);
                if (IsDead(state.Target))
                {
                    ClearState(state, StatusEffectChangeType.Removed);
                    _targets.Remove(state.TargetId);
                    continue;
                }

                TickEffects(state, deltaTime);
                RemoveStateIfEmpty(state);
            }
        }

        public bool Has(string statusId, GameObject target)
        {
            return TryGetState(target, out TargetState state)
                && !string.IsNullOrEmpty(statusId)
                && state.Effects.ContainsKey(statusId);
        }

        public void ClearAll(GameObject target)
        {
            if (!TryGetState(target, out TargetState state))
            {
                return;
            }

            ClearState(state, StatusEffectChangeType.Removed);
            _targets.Remove(state.TargetId);
        }

        public int GetStacks(string statusId, GameObject target)
        {
            return TryGetActive(statusId, target, out ActiveStatus active)
                ? active.Stacks
                : 0;
        }

        public float GetRemainingDuration(string statusId, GameObject target)
        {
            return TryGetActive(statusId, target, out ActiveStatus active)
                ? Mathf.Max(0f, active.RemainingDuration)
                : 0f;
        }

        public float GetAttackMultiplier(GameObject target)
        {
            return GetStatMultiplier(target, active => active.Data.AttackMod);
        }

        public float GetDamageDealtMultiplier(GameObject target)
        {
            return GetStatMultiplier(target, active => active.Data.DamageDealtMod);
        }

        public float GetDefenseMultiplier(GameObject target)
        {
            return GetStatMultiplier(target, active => active.Data.DefenseMod);
        }

        public float GetMoveSpeedMultiplier(GameObject target)
        {
            return GetStatMultiplier(target, active => active.Data.MoveSpeedMod);
        }

        public bool IsStunned(GameObject target)
        {
            return Any(target, active => active.Data.Stun);
        }

        public bool IsRooted(GameObject target)
        {
            return Any(target, active => active.Data.Root);
        }

        public bool IsSilenced(GameObject target)
        {
            return Any(target, active => active.Data.Silence);
        }

        public bool TryGetStatusForAura(
            GameObject target,
            ElementType auraElement,
            out string statusId)
        {
            if (auraElement != ElementType.None
                && TryGetState(target, out TargetState state))
            {
                foreach (KeyValuePair<string, ActiveStatus> pair in state.Effects)
                {
                    if (pair.Value.Data.AuraElement == auraElement)
                    {
                        statusId = pair.Key;
                        return true;
                    }
                }
            }

            statusId = string.Empty;
            return false;
        }

        public bool TryGetFirstAura(
            GameObject target,
            out ElementType auraElement,
            out string statusId)
        {
            for (int index = 0; index < AuraPriority.Length; index++)
            {
                if (TryGetStatusForAura(target, AuraPriority[index], out statusId))
                {
                    auraElement = AuraPriority[index];
                    return true;
                }
            }

            auraElement = ElementType.None;
            statusId = string.Empty;
            return false;
        }

        public int CopyAuraStatuses(
            GameObject fromTarget,
            GameObject toTarget,
            GameObject source)
        {
            if (fromTarget == null
                || toTarget == null
                || fromTarget == toTarget
                || !TryGetState(fromTarget, out TargetState fromState))
            {
                return 0;
            }

            int copied = 0;
            var snapshot = new List<ActiveStatus>(fromState.Effects.Values);
            for (int index = 0; index < snapshot.Count; index++)
            {
                ActiveStatus original = snapshot[index];
                if (original.Data.AuraElement == ElementType.None
                    || !TryApply(
                        original.Data.Id,
                        toTarget,
                        source,
                        original.Stacks,
                        original.SourceBaseDamage))
                {
                    continue;
                }

                copied++;
            }

            return copied;
        }

        private static StatusEffectData ResolveData(string statusId)
        {
            return string.IsNullOrEmpty(statusId)
                ? null
                : ConfigDatabase.Instance?.GetStatusEffect(statusId);
        }

        private TargetState GetOrCreateState(GameObject target)
        {
            EntityId targetId = target.GetEntityId();
            if (_targets.TryGetValue(targetId, out TargetState state))
            {
                if (state.Target == target)
                {
                    return state;
                }

                _targets.Remove(targetId);
            }

            state = new TargetState(targetId, target);
            _targets.Add(targetId, state);
            return state;
        }

        private bool TryGetState(GameObject target, out TargetState state)
        {
            if (target != null
                && _targets.TryGetValue(target.GetEntityId(), out state)
                && state.Target == target)
            {
                return true;
            }

            state = null;
            return false;
        }

        private bool TryGetActive(
            string statusId,
            GameObject target,
            out ActiveStatus active)
        {
            if (TryGetState(target, out TargetState state)
                && !string.IsNullOrEmpty(statusId)
                && state.Effects.TryGetValue(statusId, out active))
            {
                return true;
            }

            active = null;
            return false;
        }

        private void TryPromote(TargetState state, ActiveStatus active)
        {
            string promotedStatusId = active.Data.PromoteAtMaxStacksStatusId;
            if (string.IsNullOrEmpty(promotedStatusId)
                || active.Stacks < Mathf.Max(1, active.Data.MaxStacks)
                || !CanApply(promotedStatusId, state))
            {
                return;
            }

            GameObject source = active.Source;
            float sourceBaseDamage = active.SourceBaseDamage;
            RemoveInternal(
                state,
                active.Data.Id,
                StatusEffectChangeType.Promoted);
            TryApply(
                promotedStatusId,
                state.Target,
                source,
                1,
                sourceBaseDamage);
        }

        private static bool CanApply(string statusId, TargetState state)
        {
            StatusEffectData data = ResolveData(statusId);
            return data != null
                && data.Duration > 0f
                && (!state.Cooldowns.TryGetValue(statusId, out float cooldown)
                    || cooldown <= Epsilon);
        }

        private void TickEffects(TargetState state, float deltaTime)
        {
            var effects = new List<ActiveStatus>(state.Effects.Values);
            for (int index = 0; index < effects.Count; index++)
            {
                ActiveStatus active = effects[index];
                if (!state.Effects.TryGetValue(active.Data.Id, out ActiveStatus current)
                    || !ReferenceEquals(active, current))
                {
                    continue;
                }

                float activeDelta = Mathf.Min(
                    Mathf.Max(0f, active.RemainingDuration),
                    deltaTime);
                TickPeriodicDamage(state, active, activeDelta);
                if (state.Target == null || IsDead(state.Target))
                {
                    ClearState(state, StatusEffectChangeType.Removed);
                    return;
                }

                if (!state.Effects.TryGetValue(active.Data.Id, out current)
                    || !ReferenceEquals(active, current))
                {
                    continue;
                }

                active.RemainingDuration = Mathf.Max(
                    0f,
                    active.RemainingDuration - deltaTime);
                if (active.RemainingDuration <= Epsilon)
                {
                    RemoveInternal(
                        state,
                        active.Data.Id,
                        StatusEffectChangeType.Expired);
                }
            }
        }

        private static void TickCooldowns(TargetState state, float deltaTime)
        {
            if (state.Cooldowns.Count == 0)
            {
                return;
            }

            var statusIds = new List<string>(state.Cooldowns.Keys);
            for (int index = 0; index < statusIds.Count; index++)
            {
                string statusId = statusIds[index];
                float remaining = Mathf.Max(
                    0f,
                    state.Cooldowns[statusId] - deltaTime);
                if (remaining <= Epsilon)
                {
                    state.Cooldowns.Remove(statusId);
                }
                else
                {
                    state.Cooldowns[statusId] = remaining;
                }
            }
        }

        private static void TickPeriodicDamage(
            TargetState state,
            ActiveStatus active,
            float deltaTime)
        {
            float damagePerSecond = (
                    Mathf.Max(0f, active.Data.DotDamagePerSecond)
                    + Mathf.Max(0f, active.SourceBaseDamage)
                        * Mathf.Max(0f, active.Data.DotBaseDamageMultiplier))
                * Mathf.Max(1, active.Stacks);
            if (damagePerSecond <= 0f || deltaTime <= 0f)
            {
                return;
            }

            float interval = Mathf.Max(Epsilon, active.Data.DotInterval);
            float damagePerTick = damagePerSecond * interval;
            active.DotElapsed += deltaTime;
            while (active.DotElapsed + Epsilon >= interval)
            {
                active.DotElapsed -= interval;
                if (!TryFindDamageable(state.Target, out IDamageable damageable)
                    || damageable.IsDead
                    || !ServiceLocator.TryGet<ICombatService>(
                        out ICombatService combat))
                {
                    return;
                }

                combat.DealDamage(
                    damageable,
                    new DamageRequest
                    {
                        Source = active.Source,
                        BaseDamage = damagePerTick,
                        Type = active.Data.DotType,
                        Element = ElementType.None,
                        Multiplier = 1f,
                        CanCrit = false,
                        SkillId = active.Data.Id,
                        IgnoreAttackScaling = true,
                        StatusOnHitId = string.Empty,
                        StatusChance = 0f
                    });
                if (damageable.IsDead)
                {
                    return;
                }
            }
        }

        private static bool TryFindDamageable(
            GameObject target,
            out IDamageable damageable)
        {
            if (target != null)
            {
                MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
                for (int index = 0; index < components.Length; index++)
                {
                    if (components[index] is IDamageable candidate)
                    {
                        damageable = candidate;
                        return true;
                    }
                }
            }

            damageable = null;
            return false;
        }

        private static bool IsDead(GameObject target)
        {
            return TryFindDamageable(target, out IDamageable damageable)
                && damageable.IsDead;
        }

        private float GetStatMultiplier(
            GameObject target,
            Func<ActiveStatus, float> selector)
        {
            if (!TryGetState(target, out TargetState state))
            {
                return 1f;
            }

            float modifier = 0f;
            foreach (ActiveStatus active in state.Effects.Values)
            {
                modifier += selector(active) * Mathf.Max(1, active.Stacks);
            }

            return Mathf.Max(0f, 1f + modifier);
        }

        private bool Any(GameObject target, Func<ActiveStatus, bool> predicate)
        {
            if (!TryGetState(target, out TargetState state))
            {
                return false;
            }

            foreach (ActiveStatus active in state.Effects.Values)
            {
                if (predicate(active))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveInternal(
            TargetState state,
            string statusId,
            StatusEffectChangeType change)
        {
            if (!state.Effects.TryGetValue(statusId, out ActiveStatus active))
            {
                return;
            }

            state.Effects.Remove(statusId);
            EventBus.Publish(
                CombatEvents.StatusEffectChanged,
                new StatusEffectInfo
                {
                    Target = state.Target,
                    Source = active.Source,
                    StatusId = statusId,
                    Stacks = 0,
                    RemainingDuration = 0f,
                    Change = change
                });
        }

        private void ClearState(
            TargetState state,
            StatusEffectChangeType change)
        {
            var statusIds = new List<string>(state.Effects.Keys);
            for (int index = 0; index < statusIds.Count; index++)
            {
                RemoveInternal(state, statusIds[index], change);
            }

            state.Cooldowns.Clear();
        }

        private void RemoveStateIfEmpty(TargetState state)
        {
            if (state.Effects.Count == 0 && state.Cooldowns.Count == 0)
            {
                _targets.Remove(state.TargetId);
            }
        }

        private static void PublishChanged(
            TargetState state,
            ActiveStatus active,
            StatusEffectChangeType change)
        {
            EventBus.Publish(
                CombatEvents.StatusEffectChanged,
                new StatusEffectInfo
                {
                    Target = state.Target,
                    Source = active.Source,
                    StatusId = active.Data.Id,
                    Stacks = active.Stacks,
                    RemainingDuration = active.RemainingDuration,
                    Change = change
                });
        }

        private sealed class TargetState
        {
            public TargetState(EntityId targetId, GameObject target)
            {
                TargetId = targetId;
                Target = target;
            }

            public EntityId TargetId { get; }
            public GameObject Target { get; }
            public Dictionary<string, ActiveStatus> Effects { get; } =
                new Dictionary<string, ActiveStatus>(StringComparer.Ordinal);
            public Dictionary<string, float> Cooldowns { get; } =
                new Dictionary<string, float>(StringComparer.Ordinal);
        }

        private sealed class ActiveStatus
        {
            public StatusEffectData Data;
            public GameObject Source;
            public int Stacks;
            public float RemainingDuration;
            public float DotElapsed;
            public float SourceBaseDamage;
        }
    }
}
