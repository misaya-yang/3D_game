using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Mount;
using Wendao.Systems.NPC;
using Wendao.Systems.Player;

namespace Wendao.CameraSystem
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public const float ExploreDistance = 5f;
        public const float CombatDistance = 4.5f;
        public const float MountedDistance = 6f;
        public const float FlyingDistance = 10f;
        public const float ExploreFov = 55f;
        public const float CombatFov = 65f;
        public const float LockOnFov = 60f;
        public const float DialogueFov = 45f;
        public const float MinimumCollisionDistance = 1.5f;
        public const float OccluderAlpha = 0.3f;
        public const float DodgeShakeIntensity = 0.3f;
        public const float DodgeShakeDuration = 0.1f;

        [Header("Orbit")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [SerializeField] private float _gamepadDegreesPerSecond = 150f;
        [SerializeField] private float _minimumPitch = -30f;
        [SerializeField] private float _maximumPitch = 70f;

        [Header("Collision")]
        [SerializeField, Min(0.01f)] private float _collisionRadius = 0.25f;
        [SerializeField, Min(0f)] private float _collisionPadding = 0.1f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        private sealed class OccluderFadeState
        {
            public Renderer Renderer;
            public MaterialPropertyBlock OriginalProperties;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly RaycastHit[] _collisionHits = new RaycastHit[24];
        private readonly HashSet<Renderer> _currentOccluders =
            new HashSet<Renderer>();
        private readonly Dictionary<Renderer, OccluderFadeState> _fadedOccluders =
            new Dictionary<Renderer, OccluderFadeState>();
        private readonly List<Renderer> _occludersToRestore =
            new List<Renderer>();
        private UnityEngine.Camera _camera;
        private IPlayerInputSource _inputSource;
        private Transform _target;
        private Transform _lockOnTarget;
        private Transform _dialogueFocus;
        private string _dialogueNpcId = string.Empty;
        private float _yaw;
        private float _pitch = 12f;
        private float _currentDistance = ExploreDistance;
        private float _shakeIntensity;
        private float _shakeRemaining;
        private float _reactionFovKick;
        private float _reactionFovDuration;
        private float _reactionFovRemaining;
        private float _reactionFovOffset;
        private bool _combatMode;
        private bool _mounted;
        private bool _flying;

        public Transform Target => _target;
        public Transform LockOnTarget => _lockOnTarget;
        public float CurrentDistance => _currentDistance;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float CurrentFov => _camera != null
            ? _camera.fieldOfView
            : ExploreFov;
        public bool IsCombatMode => _combatMode;
        public Transform DialogueFocus => _dialogueFocus;
        public float ShakeIntensity => _shakeIntensity;
        public float ShakeRemaining => _shakeRemaining;
        public float ReactionFovOffset => _reactionFovOffset;
        public float ReactionFovRemaining => _reactionFovRemaining;
        public int CriticalShakeCount { get; private set; }
        public int ReactionFovPulseCount { get; private set; }
        public int FadedOccluderCount => _fadedOccluders.Count;
        public Vector3 CurrentPivot { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _camera.fieldOfView = ExploreFov;
        }

        private void OnEnable()
        {
            ResolveInputSource();
            EventBus.Subscribe<LockOnInfo>(
                PlayerEvents.LockOnChanged,
                HandleLockOnChanged);
            EventBus.Subscribe<PlayerDodgeInfo>(
                PlayerEvents.Dodged,
                HandlePlayerDodged);
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Subscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReactionTriggered);
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
            EventBus.Subscribe<MountInfo>(
                MountEvents.MountChanged,
                HandleMountChanged);
            EventBus.Subscribe<FlightInfo>(
                MountEvents.FlightStateChanged,
                HandleFlightStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LockOnInfo>(
                PlayerEvents.LockOnChanged,
                HandleLockOnChanged);
            EventBus.Unsubscribe<PlayerDodgeInfo>(
                PlayerEvents.Dodged,
                HandlePlayerDodged);
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Unsubscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReactionTriggered);
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Started,
                HandleDialogueStarted);
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
            EventBus.Unsubscribe<MountInfo>(
                MountEvents.MountChanged,
                HandleMountChanged);
            EventBus.Unsubscribe<FlightInfo>(
                MountEvents.FlightStateChanged,
                HandleFlightStateChanged);
            _dialogueFocus = null;
            _dialogueNpcId = string.Empty;
            ClearReactionFovPulse();
            RestoreAllOccluders();
        }

        private void LateUpdate()
        {
            TickCamera(Time.unscaledDeltaTime);
        }

        public void TickCamera(float deltaTime)
        {
            if (_target == null)
            {
                return;
            }

            if (_inputSource == null)
            {
                ResolveInputSource();
            }

            if (_dialogueFocus == null
                && !string.IsNullOrEmpty(_dialogueNpcId))
            {
                _dialogueFocus = ResolveDialogueFocus(_dialogueNpcId);
            }

            float safeDeltaTime = deltaTime > 0f
                && !float.IsNaN(deltaTime)
                && !float.IsInfinity(deltaTime)
                ? deltaTime
                : 0f;
            UpdateOrbit(safeDeltaTime);
            UpdateCameraPose(safeDeltaTime);
        }

        public void SetTarget(Transform player)
        {
            if (_target != player)
            {
                ClearLockOn();
                _dialogueFocus = null;
                _dialogueNpcId = string.Empty;
                RestoreAllOccluders();
            }

            _target = player;
            if (player != null)
            {
                _yaw = player.eulerAngles.y;
            }
        }

        public void SetLockOnTarget(Transform target)
        {
            _lockOnTarget = target;
        }

        public void ClearLockOn()
        {
            _lockOnTarget = null;
        }

        public void Shake(float intensity, float duration)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, Mathf.Max(0f, intensity));
            _shakeRemaining = Mathf.Max(_shakeRemaining, Mathf.Max(0f, duration));
        }

        public void PulseElementReactionFov()
        {
            PulseFov(
                CombatFeelSettings.ElementReactionFovKick,
                CombatFeelSettings.ElementReactionFovDuration);
        }

        public void PulseFov(float kick, float duration)
        {
            if (kick <= 0f
                || duration <= 0f
                || float.IsNaN(kick)
                || float.IsInfinity(kick)
                || float.IsNaN(duration)
                || float.IsInfinity(duration))
            {
                return;
            }

            _reactionFovKick = Mathf.Max(_reactionFovKick, kick);
            _reactionFovDuration = Mathf.Max(_reactionFovDuration, duration);
            _reactionFovRemaining = Mathf.Max(
                _reactionFovRemaining,
                duration);
            _reactionFovOffset = Mathf.Max(_reactionFovOffset, kick);
            ReactionFovPulseCount++;
            if (_camera != null)
            {
                _camera.fieldOfView = GetDesiredFov() + _reactionFovOffset;
            }
        }

        public void SetCombatMode(bool inCombat)
        {
            _combatMode = inCombat;
        }

        public void SetDialogueFocus(Transform npcFace, bool enable)
        {
            _dialogueNpcId = string.Empty;
            _dialogueFocus = enable ? npcFace : null;
        }

        public void SetMounted(bool mounted)
        {
            _mounted = mounted;
            if (!mounted)
            {
                _flying = false;
            }
        }

        public void SetFlying(bool flying)
        {
            _flying = flying;
            if (flying)
            {
                _mounted = true;
            }
        }

        private void HandleMountChanged(MountInfo info)
        {
            SetMounted(info.Mounted);
        }

        private void HandleFlightStateChanged(FlightInfo info)
        {
            SetFlying(info.IsFlying);
        }

        public void SetInputSource(IPlayerInputSource inputSource)
        {
            _inputSource = inputSource;
        }

        private void UpdateOrbit(float deltaTime)
        {
            if (_dialogueFocus != null)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            if (_lockOnTarget != null)
            {
                Vector3 toTarget = _lockOnTarget.position - _target.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    _yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                }

                return;
            }

            if (_inputSource == null || !_inputSource.IsEnabled)
            {
                return;
            }

            Vector2 look = _inputSource.Look;
            float scale = _inputSource.LookIsPointerDelta
                ? _mouseSensitivity
                : _gamepadDegreesPerSecond * deltaTime;
            _yaw += look.x * scale;
            _pitch = Mathf.Clamp(
                _pitch - look.y * scale,
                _minimumPitch,
                _maximumPitch);
        }

        private void UpdateCameraPose(float deltaTime)
        {
            TickReactionFov(deltaTime);
            Vector3 pivot = GetPivot();
            CurrentPivot = pivot;
            Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (_dialogueFocus != null)
            {
                Vector3 focusDirection = _dialogueFocus.position - pivot;
                if (focusDirection.sqrMagnitude > 0.0001f)
                {
                    orbitRotation = Quaternion.LookRotation(focusDirection.normalized, Vector3.up);
                }
            }

            float desiredDistance = GetDesiredDistance();
            Vector3 backwards = orbitRotation * Vector3.back;
            _currentDistance = ResolveCollisionDistance(
                pivot,
                backwards,
                desiredDistance);

            Vector3 cameraPosition = pivot + backwards * _currentDistance;
            if (_shakeRemaining > 0f)
            {
                cameraPosition += UnityEngine.Random.insideUnitSphere
                    * _shakeIntensity;
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - deltaTime);
                if (_shakeRemaining <= 0f)
                {
                    _shakeIntensity = 0f;
                }
            }

            transform.SetPositionAndRotation(cameraPosition, orbitRotation);
            _camera.fieldOfView = Mathf.MoveTowards(
                _camera.fieldOfView,
                GetDesiredFov() + _reactionFovOffset,
                60f * deltaTime);
        }

        private Vector3 GetPivot()
        {
            Vector3 pivot;
            if (_flying)
            {
                pivot = _target.position + new Vector3(0f, 5f, 0f);
            }
            else if (_mounted)
            {
                pivot = _target.position + new Vector3(0f, 2.8f, 0f);
            }
            else
            {
                pivot = _target.position + _targetOffset;
            }

            if (_dialogueFocus == null && _lockOnTarget != null)
            {
                pivot = Vector3.Lerp(
                    pivot,
                    _lockOnTarget.position,
                    0.5f);
            }

            return pivot;
        }

        private float GetDesiredDistance()
        {
            if (_dialogueFocus != null)
            {
                return ExploreDistance;
            }

            if (_flying)
            {
                return FlyingDistance;
            }

            if (_mounted)
            {
                return MountedDistance;
            }

            return _combatMode || _lockOnTarget != null
                ? CombatDistance
                : ExploreDistance;
        }

        private float GetDesiredFov()
        {
            if (_dialogueFocus != null)
            {
                return DialogueFov;
            }

            if (_flying)
            {
                return CombatFov;
            }

            if (_mounted || _lockOnTarget != null)
            {
                return LockOnFov;
            }

            return _combatMode ? CombatFov : ExploreFov;
        }

        private float ResolveCollisionDistance(
            Vector3 pivot,
            Vector3 direction,
            float desiredDistance)
        {
            _currentOccluders.Clear();
            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                _collisionRadius,
                direction,
                _collisionHits,
                desiredDistance,
                _collisionMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = desiredDistance;
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _collisionHits[i].collider;
                if (collider == null || IsTargetCollider(collider.transform))
                {
                    continue;
                }

                Renderer occluder = collider.GetComponentInParent<Renderer>();
                if (occluder != null)
                {
                    _currentOccluders.Add(occluder);
                }

                nearestDistance = Mathf.Min(
                    nearestDistance,
                    Mathf.Max(
                        MinimumCollisionDistance,
                        _collisionHits[i].distance - _collisionPadding));
            }

            RefreshOccluderFades();
            return nearestDistance;
        }

        private void RefreshOccluderFades()
        {
            _occludersToRestore.Clear();
            foreach (KeyValuePair<Renderer, OccluderFadeState> pair
                in _fadedOccluders)
            {
                if (pair.Key == null || !_currentOccluders.Contains(pair.Key))
                {
                    _occludersToRestore.Add(pair.Key);
                }
            }

            for (int index = 0; index < _occludersToRestore.Count; index++)
            {
                RestoreOccluder(_occludersToRestore[index]);
            }

            foreach (Renderer renderer in _currentOccluders)
            {
                FadeOccluder(renderer);
            }
        }

        private void FadeOccluder(Renderer renderer)
        {
            if (renderer == null || _fadedOccluders.ContainsKey(renderer))
            {
                return;
            }

            Material material = renderer.sharedMaterial;
            int colorProperty = material != null
                && material.HasProperty(BaseColorId)
                ? BaseColorId
                : material != null && material.HasProperty(ColorId)
                    ? ColorId
                    : -1;
            if (colorProperty < 0)
            {
                return;
            }

            var original = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original);
            var faded = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(faded);
            Color color = material.GetColor(colorProperty);
            color.a = OccluderAlpha;
            faded.SetColor(colorProperty, color);
            renderer.SetPropertyBlock(faded);
            _fadedOccluders.Add(
                renderer,
                new OccluderFadeState
                {
                    Renderer = renderer,
                    OriginalProperties = original
                });
        }

        private void RestoreOccluder(Renderer renderer)
        {
            if (!_fadedOccluders.TryGetValue(
                    renderer,
                    out OccluderFadeState state))
            {
                return;
            }

            if (state.Renderer != null)
            {
                state.Renderer.SetPropertyBlock(state.OriginalProperties);
            }

            _fadedOccluders.Remove(renderer);
        }

        private void RestoreAllOccluders()
        {
            _occludersToRestore.Clear();
            foreach (Renderer renderer in _fadedOccluders.Keys)
            {
                _occludersToRestore.Add(renderer);
            }

            for (int index = 0; index < _occludersToRestore.Count; index++)
            {
                RestoreOccluder(_occludersToRestore[index]);
            }

            _currentOccluders.Clear();
        }

        private void HandleLockOnChanged(LockOnInfo info)
        {
            if (!IsEventForTargetPlayer(info.Player))
            {
                return;
            }

            if (!info.Locked || info.Target == null)
            {
                ClearLockOn();
                return;
            }

            SetLockOnTarget(ResolveLockOnTransform(info.Target));
        }

        private void HandlePlayerDodged(PlayerDodgeInfo info)
        {
            if (IsEventForTargetPlayer(info.Player))
            {
                Shake(DodgeShakeIntensity, DodgeShakeDuration);
            }
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (info.Amount <= 0f
                || !info.IsCritical
                || !IsEventForTargetPlayer(info.Source))
            {
                return;
            }

            CriticalShakeCount++;
            Shake(
                CombatFeelSettings.CriticalShakeIntensity,
                CombatFeelSettings.CriticalShakeDuration);
        }

        private void HandleElementReactionTriggered(ElementReactionInfo info)
        {
            if (info.Reaction != ElementReactionType.None
                && IsEventForTargetPlayer(info.Source))
            {
                PulseElementReactionFov();
            }
        }

        private void HandleDialogueStarted(DialogueInfo info)
        {
            _dialogueNpcId = info.NpcId ?? string.Empty;
            _dialogueFocus = ResolveDialogueFocus(_dialogueNpcId);
        }

        private void HandleDialogueEnded(DialogueInfo info)
        {
            if (string.IsNullOrEmpty(_dialogueNpcId)
                || string.IsNullOrEmpty(info.NpcId)
                || string.Equals(
                    _dialogueNpcId,
                    info.NpcId,
                    StringComparison.Ordinal))
            {
                _dialogueFocus = null;
                _dialogueNpcId = string.Empty;
            }
        }

        private bool IsEventForTargetPlayer(GameObject player)
        {
            return _target != null
                && player != null
                && (player.transform == _target
                    || player.transform.IsChildOf(_target)
                    || _target.IsChildOf(player.transform));
        }

        private void TickReactionFov(float deltaTime)
        {
            if (_reactionFovRemaining <= 0f)
            {
                _reactionFovOffset = 0f;
                return;
            }

            _reactionFovRemaining = Mathf.Max(
                0f,
                _reactionFovRemaining - deltaTime);
            _reactionFovOffset = _reactionFovDuration > 0f
                ? _reactionFovKick
                    * (_reactionFovRemaining / _reactionFovDuration)
                : 0f;
            if (_reactionFovRemaining <= 0f)
            {
                ClearReactionFovPulse();
            }
        }

        private void ClearReactionFovPulse()
        {
            _reactionFovKick = 0f;
            _reactionFovDuration = 0f;
            _reactionFovRemaining = 0f;
            _reactionFovOffset = 0f;
        }

        private static Transform ResolveLockOnTransform(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ILockOnTarget lockOnTarget
                    && lockOnTarget.CanLockOn
                    && lockOnTarget.LockOnTransform != null)
                {
                    return lockOnTarget.LockOnTransform;
                }
            }

            return target.transform;
        }

        private Transform ResolveDialogueFocus(string npcId)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                return null;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            Transform nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (!(behaviours[index] is IDialogueFocusTarget focusTarget)
                    || !string.Equals(
                        focusTarget.NpcId,
                        npcId,
                        StringComparison.Ordinal)
                    || focusTarget.DialogueFocusTransform == null)
                {
                    continue;
                }

                float distance = _target != null
                    ? Vector3.SqrMagnitude(
                        focusTarget.DialogueFocusTransform.position
                        - _target.position)
                    : 0f;
                if (nearest == null || distance < nearestDistance)
                {
                    nearest = focusTarget.DialogueFocusTransform;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private bool IsTargetCollider(Transform candidate)
        {
            return _target != null
                && (candidate == _target || candidate.IsChildOf(_target));
        }

        private bool ResolveInputSource()
        {
            return ServiceLocator.TryGet(out _inputSource);
        }
    }
}
