using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Player;
using Wendao.Systems.World;

namespace Wendao.Entities.Player
{
    [DefaultExecutionOrder(-40)]
    [RequireComponent(
        typeof(PlayerController),
        typeof(PlayerStats),
        typeof(PlayerCombatController))]
    public sealed class PlayerTargetingController : SafeBehaviour
    {
        public const float BaseDivineSenseRange = 14f;

        private readonly List<ILockOnTarget> _candidates =
            new List<ILockOnTarget>();

        private PlayerController _playerController;
        private PlayerStats _playerStats;
        private PlayerCombatController _playerCombat;
        private IPlayerInputSource _inputSource;
        private ILockOnTarget _currentTarget;

        public ILockOnTarget CurrentTarget => _currentTarget;
        public Transform CurrentTargetTransform => ResolveTargetTransform(
            _currentTarget);
        public float CurrentDivineSenseRange =>
            (BaseDivineSenseRange
                + (_playerStats != null ? _playerStats.DivineSense : 0f))
            * ResolveVisionMultiplier();
        public int LastCandidateCount { get; private set; }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerStats = GetComponent<PlayerStats>();
            _playerCombat = GetComponent<PlayerCombatController>();
        }

        protected override void SafeStart()
        {
            ResolveInputSource();
        }

        private void Update()
        {
            TickTargeting(Time.deltaTime);
        }

        private void OnDisable()
        {
            ClearLockOn();
        }

        public void TickTargeting(float deltaTime)
        {
            if (_playerStats.IsDead
                || _playerController.State == PlayerState.Dead)
            {
                ClearLockOn();
                return;
            }

            ValidateCurrentTarget();

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            if (_inputSource == null)
            {
                ResolveInputSource();
            }

            if (_inputSource != null
                && _inputSource.IsEnabled
                && _inputSource.LockOnPressedThisFrame)
            {
                TryCycleLockOn();
            }

            if (deltaTime > 0f
                && !float.IsNaN(deltaTime)
                && !float.IsInfinity(deltaTime))
            {
                TurnLightAttackTowardTarget(deltaTime);
            }
        }

        public bool TryCycleLockOn()
        {
            CollectCandidates();
            if (_candidates.Count == 0)
            {
                ClearLockOn();
                return false;
            }

            _candidates.Sort(CompareCandidates);
            int currentIndex = FindCurrentCandidateIndex();
            int nextIndex = currentIndex >= 0
                ? (currentIndex + 1) % _candidates.Count
                : 0;
            ApplyTarget(_candidates[nextIndex]);
            return true;
        }

        public void ClearLockOn()
        {
            ApplyTarget(null);
        }

        public void SetInputSource(IPlayerInputSource inputSource)
        {
            _inputSource = inputSource;
        }

        private void CollectCandidates()
        {
            _candidates.Clear();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ILockOnTarget target
                    && IsAcquisitionCandidate(target)
                    && !_candidates.Contains(target))
                {
                    _candidates.Add(target);
                }
            }

            LastCandidateCount = _candidates.Count;
        }

        private bool IsAcquisitionCandidate(ILockOnTarget target)
        {
            Component component = target as Component;
            Transform targetTransform = ResolveTargetTransform(target);
            if (component == null
                || targetTransform == null
                || !target.CanLockOn
                || component.transform == transform
                || component.transform.IsChildOf(transform))
            {
                return false;
            }

            float range = Mathf.Min(
                CurrentDivineSenseRange,
                Mathf.Max(0.1f, target.LockOnDisengageRange));
            return HorizontalDistanceSquared(
                    transform.position,
                    component.transform.position)
                <= range * range;
        }

        private void ValidateCurrentTarget()
        {
            if (_currentTarget == null)
            {
                return;
            }

            Component component = _currentTarget as Component;
            Transform targetTransform = ResolveTargetTransform(_currentTarget);
            if (component == null || targetTransform == null)
            {
                ClearLockOn();
                return;
            }

            float disengageRange = Mathf.Max(
                0.1f,
                _currentTarget.LockOnDisengageRange
                    * ResolveVisionMultiplier());
            if (!_currentTarget.CanLockOn
                || HorizontalDistanceSquared(
                        transform.position,
                        component.transform.position)
                    > disengageRange * disengageRange)
            {
                ClearLockOn();
            }
        }

        private int CompareCandidates(
            ILockOnTarget left,
            ILockOnTarget right)
        {
            Component leftComponent = left as Component;
            Component rightComponent = right as Component;
            float leftDistance = leftComponent != null
                ? HorizontalDistanceSquared(
                    transform.position,
                    leftComponent.transform.position)
                : float.MaxValue;
            float rightDistance = rightComponent != null
                ? HorizontalDistanceSquared(
                    transform.position,
                    rightComponent.transform.position)
                : float.MaxValue;
            int distanceOrder = leftDistance.CompareTo(rightDistance);
            if (distanceOrder != 0)
            {
                return distanceOrder;
            }

            int leftId = leftComponent != null
                ? leftComponent.GetEntityId().GetHashCode()
                : int.MaxValue;
            int rightId = rightComponent != null
                ? rightComponent.GetEntityId().GetHashCode()
                : int.MaxValue;
            return leftId.CompareTo(rightId);
        }

        private int FindCurrentCandidateIndex()
        {
            for (int index = 0; index < _candidates.Count; index++)
            {
                if (ReferenceEquals(_candidates[index], _currentTarget))
                {
                    return index;
                }
            }

            return -1;
        }

        private void ApplyTarget(ILockOnTarget target)
        {
            if (ReferenceEquals(_currentTarget, target))
            {
                return;
            }

            _currentTarget = target;
            Transform targetTransform = ResolveTargetTransform(target);
            _playerController.SetLockTarget(targetTransform);

            Component targetComponent = target as Component;
            EventBus.Publish(
                PlayerEvents.LockOnChanged,
                new LockOnInfo
                {
                    Player = gameObject,
                    Target = targetComponent != null
                        ? targetComponent.gameObject
                        : null,
                    Locked = targetTransform != null
                });
        }

        private void TurnLightAttackTowardTarget(float deltaTime)
        {
            Transform targetTransform = CurrentTargetTransform;
            if (targetTransform == null
                || !_playerCombat.IsAttacking
                || _playerCombat.AttackType != PlayerAttackType.Light)
            {
                return;
            }

            Vector3 direction = targetTransform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _playerController.RotateTowards(direction, deltaTime);
        }

        private bool ResolveInputSource()
        {
            return ServiceLocator.TryGet(out _inputSource);
        }

        private static float ResolveVisionMultiplier()
        {
            return ServiceLocator.TryGet<IWeatherService>(
                    out IWeatherService weather)
                ? Mathf.Clamp(weather.GetVisionMul(), 0.1f, 1f)
                : 1f;
        }

        private static Transform ResolveTargetTransform(ILockOnTarget target)
        {
            Component component = target as Component;
            if (component == null || target == null)
            {
                return null;
            }

            return target.LockOnTransform != null
                ? target.LockOnTransform
                : component.transform;
        }

        private static float HorizontalDistanceSquared(
            Vector3 left,
            Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return x * x + z * z;
        }
    }
}
