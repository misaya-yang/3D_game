using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Debugging;
using Wendao.Systems.Input;
using Wendao.Systems.Player;

namespace Wendao.Entities.Player
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : SafeBehaviour,
        IDebugPlayerService,
        IPlayerMountLocomotion
    {
        public const float DodgeDistance = 5f;
        public const float DodgeDuration = 0.35f;
        public const float DodgeInvincibilityDuration = 0.2f;
        public const float DodgeCooldown = 0.8f;
        public const float BlockMoveSpeedMultiplier = 0.4f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _walkSpeed = 5f;
        [SerializeField, Min(0f)] private float _sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float _acceleration = 40f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 720f;
        [SerializeField, Min(0f)] private float _jumpSpeed = 7f;
        [SerializeField] private float _gravity = -20f;

        private CharacterController _characterController;
        private PlayerActionBuffer _actionBuffer;
        private IPlayerInputSource _inputSource;
        private IStatusEffectService _statusEffects;
        private Vector3 _horizontalVelocity;
        private Vector3 _dodgeDirection;
        private float _verticalVelocity;
        private float _dodgeElapsed;
        private float _dodgeCooldownRemaining;
        private float _dodgeDistanceTravelled;
        private bool _inputEnabled = true;
        private bool _debugGodMode;
        private bool _registeredDebugPlayerService;
        private bool _registeredMountLocomotion;
        private bool _isMounted;
        private bool _flightEnabled;
        private float _mountMoveSpeedMultiplier = 1f;
        private float _maximumFlightHeight = 40f;
        private float _flightGroundHeight;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public bool IsInvincible => _debugGodMode
            || (State == PlayerState.Dodge
                && _dodgeElapsed < DodgeInvincibilityDuration);
        public bool IsBlocking => State == PlayerState.Block
            || State == PlayerState.BlockHit;
        public bool IsDodgeReady => _dodgeCooldownRemaining <= 0f
            && State != PlayerState.Dead
            && State != PlayerState.Dodge
            && !IsStatusMovementBlocked()
            && CanProcessGameplayInput();
        public bool GodModeEnabled => _debugGodMode;
        public Transform LockTarget { get; private set; }
        public Vector3 HorizontalVelocity => _horizontalVelocity;
        public float VerticalVelocity => _verticalVelocity;
        public float DodgeElapsed => _dodgeElapsed;
        public float DodgeCooldownRemaining => _dodgeCooldownRemaining;
        public float DodgeDistanceTravelled => _dodgeDistanceTravelled;
        public float CurrentMoveSpeedMultiplier =>
            (IsBlocking ? BlockMoveSpeedMultiplier : 1f)
            * GetStatusMoveSpeedMultiplier()
            * _mountMoveSpeedMultiplier;
        public bool IsGrounded => _characterController != null
            && _characterController.isGrounded;
        public GameObject Actor => gameObject;
        public bool IsMounted => _isMounted;
        public bool IsFlying => _flightEnabled;
        public bool CanChangeMountState => !_flightEnabled
            && IsGrounded
            && (State == PlayerState.Idle
                || State == PlayerState.Move
                || State == PlayerState.Sprint);

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _actionBuffer = GetComponent<PlayerActionBuffer>();
            _characterController.height = 1.8f;
            _characterController.radius = 0.35f;
            _characterController.center = new Vector3(0f, 0.9f, 0f);
            _characterController.stepOffset = 0.3f;
            _characterController.slopeLimit = 45f;
            TryRegisterDebugPlayerService();
            TryRegisterMountLocomotion();
        }

        protected override void SafeStart()
        {
            ResolveInputSource();
            EnsureGreyboxBody();
        }

        private void Update()
        {
            if (!_registeredDebugPlayerService)
            {
                TryRegisterDebugPlayerService();
            }

            if (!_registeredMountLocomotion)
            {
                TryRegisterMountLocomotion();
            }

            if (State == PlayerState.Dead)
            {
                _horizontalVelocity = Vector3.zero;
                return;
            }

            ResolveStatusEffectService();

            if (!CanProcessGameplayInput())
            {
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    Vector3.zero,
                    _acceleration * Time.deltaTime);
                return;
            }

            if (_inputEnabled && _inputSource == null)
            {
                ResolveInputSource();
            }

            TickDodgeState(Time.deltaTime);
            if (State == PlayerState.Dodge)
            {
                return;
            }

            bool stunned = _statusEffects != null
                && _statusEffects.IsStunned(gameObject);
            if (stunned)
            {
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    Vector3.zero,
                    _acceleration * Time.deltaTime);
                StopBlock();
                return;
            }

            if (_flightEnabled)
            {
                TickFlightMovement(Time.deltaTime);
                return;
            }

            if (_inputEnabled && _inputSource != null)
            {
                if (TryProcessBufferedDodgeInput())
                {
                    return;
                }

                if (_inputSource.BlockHeld)
                {
                    if (State == PlayerState.BlockHit)
                    {
                        SetState(PlayerState.Block);
                    }

                    TryStartBlock();
                }
                else
                {
                    StopBlock();
                }
            }
            else
            {
                StopBlock();
            }

            bool actionLocked = State == PlayerState.LightAttack
                || State == PlayerState.HeavyAttack
                || State == PlayerState.SkillCast
                || State == PlayerState.Stagger;
            bool blocking = IsBlocking;
            bool rooted = _statusEffects != null
                && _statusEffects.IsRooted(gameObject);
            TickMovement(
                Time.deltaTime,
                _inputEnabled
                    && _inputSource != null
                    && !actionLocked
                    && !rooted,
                !actionLocked && !blocking,
                (blocking ? BlockMoveSpeedMultiplier : 1f)
                    * GetStatusMoveSpeedMultiplier()
                    * _mountMoveSpeedMultiplier);
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                ResolveActionBuffer()?.Clear();
                _horizontalVelocity = Vector3.zero;
                if (IsBlocking)
                {
                    StopBlock();
                }
                else if (State != PlayerState.LightAttack
                    && State != PlayerState.HeavyAttack
                    && State != PlayerState.SkillCast
                    && State != PlayerState.Dodge
                    && State != PlayerState.Dead)
                {
                    SetState(PlayerState.Idle);
                }
            }
        }

        public void ForceState(PlayerState state)
        {
            if (state == PlayerState.LightAttack
                || state == PlayerState.HeavyAttack
                || state == PlayerState.Dodge
                || state == PlayerState.SkillCast
                || state == PlayerState.Stagger
                || state == PlayerState.Dead)
            {
                _horizontalVelocity = Vector3.zero;
            }

            SetState(state);
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _characterController.enabled = wasEnabled;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _dodgeElapsed = 0f;
            _dodgeCooldownRemaining = 0f;
            _dodgeDistanceTravelled = 0f;
            SetState(PlayerState.Idle);
        }

        public void SetInputSource(IPlayerInputSource inputSource)
        {
            _inputSource = inputSource;
            ResolveActionBuffer()?.SetInputSource(inputSource);
        }

        public void SetActionBuffer(PlayerActionBuffer actionBuffer)
        {
            _actionBuffer = actionBuffer;
        }

        public void SetMountedState(bool mounted, float speedMultiplier)
        {
            _isMounted = mounted;
            _mountMoveSpeedMultiplier = mounted
                ? Mathf.Max(0f, speedMultiplier)
                : 1f;
            if (!mounted && _flightEnabled)
            {
                SetFlyingState(false, _maximumFlightHeight);
            }
        }

        public bool SetFlyingState(bool flying, float maximumHeight)
        {
            if (flying)
            {
                if (!_isMounted || !CanChangeMountState)
                {
                    return false;
                }

                _maximumFlightHeight = Mathf.Max(1f, maximumHeight);
                _flightGroundHeight = TryGetGroundHeight(out float groundHeight)
                    ? groundHeight
                    : transform.position.y;
                _flightEnabled = true;
                _verticalVelocity = 0f;
                _horizontalVelocity = Vector3.zero;
                _characterController.Move(Vector3.up * 0.35f);
                SetState(PlayerState.Idle);
                return true;
            }

            if (!_flightEnabled)
            {
                return true;
            }

            _flightEnabled = false;
            if (TryGetGroundHeight(out float landingHeight))
            {
                Vector3 landingPosition = transform.position;
                landingPosition.y = landingHeight + 0.05f;
                TeleportTo(landingPosition, transform.rotation);
            }
            else
            {
                _horizontalVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                SetState(PlayerState.Fall);
            }

            return true;
        }

        internal void SetLockTarget(Transform target)
        {
            LockTarget = target;
        }

        internal void RotateTowards(Vector3 worldDirection, float deltaTime)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.0001f
                || deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                worldDirection.normalized,
                Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * deltaTime);
        }

        public bool TryStartDodge()
        {
            return TryStartDodge(ResolveRequestedDodgeDirection());
        }

        public bool TryProcessBufferedDodgeInput()
        {
            PlayerActionBuffer actionBuffer = ResolveActionBuffer();
            bool requested = actionBuffer != null
                ? actionBuffer.IsConsumptionEnabled
                    && actionBuffer.HasBufferedAction(BufferedActionType.Dodge)
                : _inputSource != null
                    && _inputSource.IsEnabled
                    && _inputSource.DodgePressedThisFrame;
            if (!requested || !TryStartDodge())
            {
                return false;
            }

            actionBuffer?.TryConsume(BufferedActionType.Dodge);
            return true;
        }

        public bool TryStartDodge(Vector3 direction)
        {
            if (_isMounted || !IsDodgeReady || !CanStartDodgeFrom(State))
            {
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            _dodgeDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            _horizontalVelocity = Vector3.zero;
            _dodgeElapsed = 0f;
            _dodgeDistanceTravelled = 0f;
            _dodgeCooldownRemaining = DodgeCooldown;
            SetState(PlayerState.Dodge);
            EventBus.Publish(
                PlayerEvents.Dodged,
                new PlayerDodgeInfo
                {
                    Player = gameObject,
                    Direction = _dodgeDirection
                });
            return true;
        }

        public void TickDodgeState(float deltaTime)
        {
            if (deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            _dodgeCooldownRemaining = Mathf.Max(
                0f,
                _dodgeCooldownRemaining - deltaTime);
            if (State != PlayerState.Dodge)
            {
                return;
            }

            float remainingDuration = Mathf.Max(
                0f,
                DodgeDuration - _dodgeElapsed);
            float stepDuration = Mathf.Min(deltaTime, remainingDuration);
            float dodgeSpeed = DodgeDistance / DodgeDuration;
            if (stepDuration > 0f)
            {
                _characterController.Move(
                    _dodgeDirection * (dodgeSpeed * stepDuration));
                _dodgeDistanceTravelled += dodgeSpeed * stepDuration;
                _dodgeElapsed += stepDuration;
            }

            if (_dodgeElapsed + 0.0001f >= DodgeDuration)
            {
                _dodgeElapsed = DodgeDuration;
                _dodgeDistanceTravelled = DodgeDistance;
                SetState(IsGrounded ? PlayerState.Idle : PlayerState.Fall);
            }
        }

        public Vector3 ResolveRequestedDodgeDirection()
        {
            if (_inputSource == null)
            {
                ResolveInputSource();
            }

            Vector3 requested = _inputSource != null
                ? GetCameraRelativeDirection(
                    Vector2.ClampMagnitude(_inputSource.Move, 1f))
                : Vector3.zero;
            if (requested.sqrMagnitude <= 0.0001f)
            {
                requested = transform.forward;
                requested.y = 0f;
            }

            return requested.sqrMagnitude > 0.0001f
                ? requested.normalized
                : Vector3.forward;
        }

        public bool TryStartBlock()
        {
            if (_isMounted)
            {
                return false;
            }

            if (State == PlayerState.Block || State == PlayerState.BlockHit)
            {
                return true;
            }

            if (State != PlayerState.Idle
                && State != PlayerState.Move
                && State != PlayerState.Sprint)
            {
                return false;
            }

            SetState(PlayerState.Block);
            return true;
        }

        public void StopBlock()
        {
            if (!IsBlocking)
            {
                return;
            }

            SetState(IsGrounded ? PlayerState.Idle : PlayerState.Fall);
        }

        public void NotifyBlockHit()
        {
            if (IsBlocking)
            {
                SetState(PlayerState.BlockHit);
            }
        }

        public bool SetGodMode(bool enabled)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _debugGodMode = enabled;
            return true;
#else
            _debugGodMode = false;
            return false;
#endif
        }

        private bool TryRegisterDebugPlayerService()
        {
            if (_registeredDebugPlayerService)
            {
                _registeredDebugPlayerService =
                    ServiceLocator.TryGet<IDebugPlayerService>(
                        out IDebugPlayerService current)
                    && ReferenceEquals(current, this);
                if (_registeredDebugPlayerService)
                {
                    return true;
                }
            }

            if (ServiceLocator.TryGet<IDebugPlayerService>(
                    out IDebugPlayerService existing))
            {
                _registeredDebugPlayerService = ReferenceEquals(existing, this);
                return _registeredDebugPlayerService;
            }

            ServiceLocator.Register<IDebugPlayerService>(this);
            _registeredDebugPlayerService = true;
            return true;
        }

        private bool TryRegisterMountLocomotion()
        {
            if (_registeredMountLocomotion)
            {
                _registeredMountLocomotion =
                    ServiceLocator.TryGet<IPlayerMountLocomotion>(
                        out IPlayerMountLocomotion current)
                    && ReferenceEquals(current, this);
                if (_registeredMountLocomotion)
                {
                    return true;
                }
            }

            if (ServiceLocator.TryGet<IPlayerMountLocomotion>(
                    out IPlayerMountLocomotion existing))
            {
                _registeredMountLocomotion = ReferenceEquals(existing, this);
                return _registeredMountLocomotion;
            }

            ServiceLocator.Register<IPlayerMountLocomotion>(this);
            _registeredMountLocomotion = true;
            return true;
        }

        private void ResolveStatusEffectService()
        {
            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }
        }

        private float GetStatusMoveSpeedMultiplier()
        {
            ResolveStatusEffectService();
            return _statusEffects?.GetMoveSpeedMultiplier(gameObject) ?? 1f;
        }

        private bool IsStatusMovementBlocked()
        {
            ResolveStatusEffectService();
            return _statusEffects != null
                && (_statusEffects.IsStunned(gameObject)
                    || _statusEffects.IsRooted(gameObject));
        }

        private void OnDestroy()
        {
            if (_registeredDebugPlayerService
                && ServiceLocator.TryGet<IDebugPlayerService>(
                    out IDebugPlayerService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IDebugPlayerService>();
            }

            _registeredDebugPlayerService = false;
            if (_registeredMountLocomotion
                && ServiceLocator.TryGet<IPlayerMountLocomotion>(
                    out IPlayerMountLocomotion locomotion)
                && ReferenceEquals(locomotion, this))
            {
                ServiceLocator.Unregister<IPlayerMountLocomotion>();
            }

            _registeredMountLocomotion = false;
        }

        private void TickFlightMovement(float deltaTime)
        {
            Vector2 moveInput = _inputEnabled && _inputSource != null
                ? Vector2.ClampMagnitude(_inputSource.Move, 1f)
                : Vector2.zero;
            Vector3 desiredDirection = GetCameraRelativeDirection(moveInput);
            float horizontalSpeed = _sprintSpeed
                * Mathf.Max(1f, _mountMoveSpeedMultiplier);
            Vector3 targetVelocity = desiredDirection * horizontalSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                targetVelocity,
                _acceleration * deltaTime);

            float verticalInput = 0f;
            if (_inputEnabled && _inputSource != null)
            {
                if (_inputSource.JumpHeld)
                {
                    verticalInput = 1f;
                }
                else if (_inputSource.BlockHeld)
                {
                    verticalInput = -1f;
                }
            }

            if (TryGetGroundHeight(out float groundHeight))
            {
                _flightGroundHeight = groundHeight;
            }

            float minimumHeight = _flightGroundHeight + 0.6f;
            float maximumHeight = _flightGroundHeight + _maximumFlightHeight;
            float verticalSpeed = _walkSpeed * verticalInput;
            if ((transform.position.y >= maximumHeight && verticalSpeed > 0f)
                || (transform.position.y <= minimumHeight && verticalSpeed < 0f))
            {
                verticalSpeed = 0f;
            }

            _verticalVelocity = verticalSpeed;
            Vector3 displacement = (_horizontalVelocity
                + Vector3.up * _verticalVelocity) * deltaTime;
            _characterController.Move(displacement);

            Vector3 correctedPosition = transform.position;
            correctedPosition.y = Mathf.Clamp(
                correctedPosition.y,
                minimumHeight,
                maximumHeight);
            if (!Mathf.Approximately(correctedPosition.y, transform.position.y))
            {
                bool wasEnabled = _characterController.enabled;
                _characterController.enabled = false;
                transform.position = correctedPosition;
                _characterController.enabled = wasEnabled;
            }

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    desiredDirection,
                    Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    desiredRotation,
                    _rotationSpeed * deltaTime);
            }

            SetState(desiredDirection.sqrMagnitude > 0.0001f
                || Mathf.Abs(verticalInput) > 0.0001f
                ? PlayerState.Move
                : PlayerState.Idle);
        }

        private bool TryGetGroundHeight(out float groundHeight)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                transform.position + Vector3.up * 1f,
                Vector3.down,
                120f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            groundHeight = 0f;
            bool found = false;
            for (int index = 0; index < hits.Length; index++)
            {
                Transform hitTransform = hits[index].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hits[index].distance < nearestDistance)
                {
                    nearestDistance = hits[index].distance;
                    groundHeight = hits[index].point.y;
                    found = true;
                }
            }

            return found;
        }

        private void TickMovement(
            float deltaTime,
            bool acceptInput,
            bool updateLocomotionState,
            float speedMultiplier)
        {
            bool wasGrounded = _characterController.isGrounded;
            if (wasGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            Vector2 moveInput = acceptInput
                ? Vector2.ClampMagnitude(_inputSource.Move, 1f)
                : Vector2.zero;
            Vector3 desiredDirection = GetCameraRelativeDirection(moveInput);
            bool isSprinting = acceptInput
                && speedMultiplier >= 0.999f
                && _inputSource.SprintHeld
                && desiredDirection.sqrMagnitude > 0f;
            float targetSpeed = (isSprinting ? _sprintSpeed : _walkSpeed)
                * Mathf.Max(0f, speedMultiplier);
            Vector3 targetVelocity = desiredDirection * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                targetVelocity,
                _acceleration * deltaTime);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    desiredDirection,
                    Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    desiredRotation,
                    _rotationSpeed * deltaTime);
            }

            bool jumped = acceptInput
                && !IsBlocking
                && wasGrounded
                && _inputSource.JumpPressedThisFrame;
            if (jumped)
            {
                _verticalVelocity = _jumpSpeed;
                SetState(PlayerState.Jump);
            }

            _verticalVelocity += _gravity * deltaTime;
            Vector3 displacement = (_horizontalVelocity
                + Vector3.up * _verticalVelocity) * deltaTime;
            _characterController.Move(displacement);

            if (!updateLocomotionState)
            {
                return;
            }

            if (!_characterController.isGrounded)
            {
                SetState(_verticalVelocity > 0f ? PlayerState.Jump : PlayerState.Fall);
            }
            else if (!jumped)
            {
                if (desiredDirection.sqrMagnitude <= 0.0001f)
                {
                    SetState(PlayerState.Idle);
                }
                else
                {
                    SetState(isSprinting ? PlayerState.Sprint : PlayerState.Move);
                }
            }
        }

        private bool ResolveInputSource()
        {
            return ServiceLocator.TryGet(out _inputSource);
        }

        private PlayerActionBuffer ResolveActionBuffer()
        {
            if (_actionBuffer == null)
            {
                _actionBuffer = GetComponent<PlayerActionBuffer>();
            }

            return _actionBuffer;
        }

        private static bool CanStartDodgeFrom(PlayerState state)
        {
            return state == PlayerState.Idle
                || state == PlayerState.Move
                || state == PlayerState.Sprint;
        }

        private static bool CanProcessGameplayInput()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private Vector3 GetCameraRelativeDirection(Vector2 moveInput)
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Vector3 forward = mainCamera != null
                ? mainCamera.transform.forward
                : Vector3.forward;
            Vector3 right = mainCamera != null
                ? mainCamera.transform.right
                : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 direction = forward * moveInput.y + right * moveInput.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void SetState(PlayerState state)
        {
            State = state;
        }

        private void EnsureGreyboxBody()
        {
            if (transform.Find("GreyboxBody") != null)
            {
                return;
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "GreyboxBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(bodyCollider);
                }
                else
                {
                    DestroyImmediate(bodyCollider);
                }
            }

            Renderer renderer = body.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (renderer != null && shader != null)
            {
                renderer.sharedMaterial = new Material(shader)
                {
                    name = "Player_Greybox_Runtime",
                    color = new Color(0.32f, 0.55f, 0.52f, 1f)
                };
            }
        }
    }
}
