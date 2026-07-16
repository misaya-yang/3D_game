using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Mount;

namespace Wendao.Entities.Player
{
    public enum PlayerAttackType
    {
        None,
        Light,
        Heavy
    }

    [RequireComponent(typeof(PlayerController), typeof(PlayerStats))]
    public sealed class PlayerCombatController : SafeBehaviour
    {
        public const int MaximumLightComboStep = 4;
        public const float ComboWindow = 0.35f;

        public const float Light1Multiplier = 1f;
        public const float Light2Multiplier = 1.1f;
        public const float Light3Multiplier = 1.25f;
        public const float Light4Multiplier = 1.5f;
        public const float HeavyAttackMultiplier = 2f;

        public const float Light1Windup = 0.1f;
        public const float Light2Windup = 0.1f;
        public const float Light3Windup = 0.12f;
        public const float Light4Windup = 0.15f;
        public const float HeavyAttackWindup = 0.35f;

        public const float Light1Recovery = 0.25f;
        public const float Light2Recovery = 0.25f;
        public const float Light3Recovery = 0.3f;
        public const float Light4Recovery = 0.45f;
        public const float HeavyAttackRecovery = 0.5f;

        // G-VS-02 compatibility aliases for its original L1 contract.
        public const float LightAttackWindup = 0.1f;
        public const float LightAttackRecovery = 0.25f;

        [Header("G01-01 Melee Attacks")]
        [SerializeField, Min(1f)] private float _baseDamage = 10f;
        [SerializeField, Min(0.1f)] private float _range = 2.5f;
        [SerializeField, Range(1f, 360f)] private float _angle = 100f;

        private PlayerController _playerController;
        private PlayerStats _playerStats;
        private PlayerActionBuffer _actionBuffer;
        private IPlayerInputSource _inputSource;
        private ICombatService _combatService;
        private IStatusEffectService _statusEffects;
        private float _attackElapsed;
        private float _comboWindowRemaining;
        private bool _hitApplied;
        private bool _nextLightQueued;

        public bool IsAttacking { get; private set; }
        public bool LastAttackHit { get; private set; }
        public PlayerAttackType AttackType { get; private set; }
        public int CurrentComboStep { get; private set; }
        public float ComboWindowRemaining => _comboWindowRemaining;
        public float AttackElapsed => _attackElapsed;
        public float CurrentMultiplier => AttackType == PlayerAttackType.Heavy
            ? HeavyAttackMultiplier
            : GetLightMultiplier(CurrentComboStep);
        public float CurrentWindup => AttackType == PlayerAttackType.Heavy
            ? HeavyAttackWindup
            : GetLightWindup(CurrentComboStep);
        public float CurrentRecovery => AttackType == PlayerAttackType.Heavy
            ? HeavyAttackRecovery
            : GetLightRecovery(CurrentComboStep);
        public float CurrentHitstopSeconds =>
            AttackType == PlayerAttackType.Heavy
                ? CombatFeelSettings.HitstopLight34HeavySeconds
                : CombatFeelSettings.GetLightHitstopSeconds(CurrentComboStep);
        public bool IsInRecovery => IsAttacking
            && _hitApplied
            && _attackElapsed < CurrentWindup + CurrentRecovery;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerStats = GetComponent<PlayerStats>();
            _actionBuffer = GetComponent<PlayerActionBuffer>();
        }

        protected override void SafeStart()
        {
            ResolveServices();
        }

        private void Update()
        {
            if (_playerStats.IsDead)
            {
                CancelAttackAndCombo(false);
                return;
            }

            if (!IsGameplayRunning())
            {
                if (!IsGameplayPaused())
                {
                    CancelAttackAndCombo(true);
                }

                return;
            }

            ResolveServices();
            if (_inputSource != null && !_inputSource.IsEnabled)
            {
                CancelAttackAndCombo(true);
                return;
            }

            if (IsActionBlockedByStatus())
            {
                CancelAttackAndCombo(true);
                return;
            }

            if (IsAttacking)
            {
                bool consumedAction = TryProcessBufferedCombatInput();
                if (consumedAction && !IsAttacking)
                {
                    return;
                }

                TickAttack(Time.deltaTime);
                return;
            }

            if (_playerController.State == PlayerState.Dodge
                || _playerController.State == PlayerState.Block
                || _playerController.State == PlayerState.BlockHit
                || _playerController.State == PlayerState.SkillCast
                || _playerController.State == PlayerState.Stagger)
            {
                ResetComboContinuation();
                return;
            }

            if (TryProcessBufferedCombatInput())
            {
                return;
            }

            TickComboContinuation(Time.deltaTime);
        }

        public bool TryStartLightAttack()
        {
            if (IsAttacking
                || _playerStats.IsDead
                || IsMounted()
                || !ResolveServices()
                || !IsGameplayRunning()
                || IsActionBlockedByStatus()
                || !CanStartLightAttackFrom(_playerController.State))
            {
                return false;
            }

            int nextStep = _comboWindowRemaining > 0f
                && CurrentComboStep >= 1
                && CurrentComboStep < MaximumLightComboStep
                ? CurrentComboStep + 1
                : 1;
            BeginLightAttack(nextStep);
            return true;
        }

        public bool TryStartHeavyAttack()
        {
            if (IsAttacking
                || _playerStats.IsDead
                || IsMounted()
                || !ResolveServices()
                || !IsGameplayRunning()
                || IsActionBlockedByStatus()
                || !CanStartHeavyAttackFrom(_playerController.State))
            {
                return false;
            }

            ResetComboContinuation();
            IsAttacking = true;
            LastAttackHit = false;
            AttackType = PlayerAttackType.Heavy;
            CurrentComboStep = 0;
            _attackElapsed = 0f;
            _hitApplied = false;
            _nextLightQueued = false;
            _playerController.ForceState(PlayerState.HeavyAttack);
            return true;
        }

        public bool TryQueueNextLightAttack()
        {
            if (!IsAttacking
                || AttackType != PlayerAttackType.Light
                || CurrentComboStep < 1
                || CurrentComboStep >= MaximumLightComboStep
                || !_hitApplied
                || _attackElapsed > CurrentWindup + ComboWindow)
            {
                return false;
            }

            _nextLightQueued = true;
            return true;
        }

        public bool TryCancelRecoveryIntoDodge()
        {
            return TryCancelRecoveryIntoDodge(
                _playerController.ResolveRequestedDodgeDirection());
        }

        public bool TryCancelRecoveryIntoDodge(Vector3 direction)
        {
            if (!IsInRecovery || !_playerController.IsDodgeReady)
            {
                return false;
            }

            // This dedicated recovery path is the only attack-state bypass.
            // PlayerController's normal dodge entry still rejects attack windup.
            CancelAttackAndCombo(false);
            _playerController.ForceState(PlayerState.Idle);
            return _playerController.TryStartDodge(direction);
        }

        public void TickAttack(float deltaTime)
        {
            if (!IsAttacking
                || deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            _attackElapsed += deltaTime;
            if (!_hitApplied
                && _attackElapsed + 0.0001f >= CurrentWindup)
            {
                ApplyCurrentAttackHit();
            }

            if (_attackElapsed + 0.0001f
                >= CurrentWindup + CurrentRecovery)
            {
                FinishCurrentAttack();
            }
        }

        // Animation Event entry point. The timer in TickAttack remains a
        // greybox fallback, while a real clip can place this event on its
        // exact contact frame. _hitApplied makes both paths idempotent.
        public void OnAttackHit()
        {
            if (!IsAttacking || _hitApplied || !ResolveServices())
            {
                return;
            }

            ApplyCurrentAttackHit();
        }

        // Reserved Animation Event entry point until a stable footstep ID is
        // added to the content table. Keeping the method prevents clips from
        // depending on implementation components.
        public void OnStep()
        {
        }

        public void OnAnimEnd()
        {
            if (!IsAttacking)
            {
                return;
            }

            if (!_hitApplied)
            {
                OnAttackHit();
            }

            FinishCurrentAttack();
        }

        public void TickComboContinuation(float deltaTime)
        {
            if (IsAttacking
                || _comboWindowRemaining <= 0f
                || deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            _comboWindowRemaining = Mathf.Max(
                0f,
                _comboWindowRemaining - deltaTime);
            if (_comboWindowRemaining <= 0f)
            {
                CurrentComboStep = 0;
            }
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

        public void SetCombatService(ICombatService combatService)
        {
            _combatService = combatService;
        }

        public static float GetLightMultiplier(int comboStep)
        {
            switch (comboStep)
            {
                case 1:
                    return Light1Multiplier;
                case 2:
                    return Light2Multiplier;
                case 3:
                    return Light3Multiplier;
                case 4:
                    return Light4Multiplier;
                default:
                    return 0f;
            }
        }

        public static float GetLightWindup(int comboStep)
        {
            switch (comboStep)
            {
                case 1:
                    return Light1Windup;
                case 2:
                    return Light2Windup;
                case 3:
                    return Light3Windup;
                case 4:
                    return Light4Windup;
                default:
                    return 0f;
            }
        }

        public static float GetLightRecovery(int comboStep)
        {
            switch (comboStep)
            {
                case 1:
                    return Light1Recovery;
                case 2:
                    return Light2Recovery;
                case 3:
                    return Light3Recovery;
                case 4:
                    return Light4Recovery;
                default:
                    return 0f;
            }
        }

        public bool TryProcessBufferedCombatInput()
        {
            if (!CanReadCombatInput())
            {
                return false;
            }

            if (IsAttacking)
            {
                if (HasRequestedAction(BufferedActionType.Dodge)
                    && TryCancelRecoveryIntoDodge())
                {
                    ConsumeRequestedAction(BufferedActionType.Dodge);
                    return true;
                }

                if (HasRequestedAction(BufferedActionType.LightAttack)
                    && TryQueueNextLightAttack())
                {
                    ConsumeRequestedAction(BufferedActionType.LightAttack);
                    return true;
                }

                return false;
            }

            if (HasRequestedAction(BufferedActionType.HeavyAttack)
                && TryStartHeavyAttack())
            {
                ConsumeRequestedAction(BufferedActionType.HeavyAttack);
                return true;
            }

            if (HasRequestedAction(BufferedActionType.LightAttack)
                && TryStartLightAttack())
            {
                ConsumeRequestedAction(BufferedActionType.LightAttack);
                return true;
            }

            return false;
        }

        private void BeginLightAttack(int comboStep)
        {
            IsAttacking = true;
            LastAttackHit = false;
            AttackType = PlayerAttackType.Light;
            CurrentComboStep = Mathf.Clamp(
                comboStep,
                1,
                MaximumLightComboStep);
            _attackElapsed = 0f;
            _comboWindowRemaining = 0f;
            _hitApplied = false;
            _nextLightQueued = false;
            _playerController.ForceState(PlayerState.LightAttack);
        }

        private void ApplyCurrentAttackHit()
        {
            _hitApplied = true;
            LastAttackHit = _combatService.TryMeleeHit(
                transform,
                _range,
                _angle,
                new DamageRequest
                {
                    Source = gameObject,
                    BaseDamage = _baseDamage,
                    Type = DamageType.Physical,
                    Element = ElementType.None,
                    Multiplier = CurrentMultiplier,
                    CanCrit = true,
                    SkillId = string.Empty,
                    HitstopSeconds = CurrentHitstopSeconds,
                    HitstunSeconds =
                        CombatFeelSettings.NormalEnemyHitstunSeconds
                });
        }

        private void FinishCurrentAttack()
        {
            PlayerAttackType completedType = AttackType;
            int completedStep = CurrentComboStep;
            float completedRecovery = CurrentRecovery;
            bool continueLight = completedType == PlayerAttackType.Light
                && completedStep < MaximumLightComboStep
                && _nextLightQueued;

            IsAttacking = false;
            AttackType = PlayerAttackType.None;
            _attackElapsed = 0f;
            _hitApplied = false;
            _nextLightQueued = false;
            RestoreLocomotionState();

            if (continueLight)
            {
                BeginLightAttack(completedStep + 1);
                return;
            }

            if (completedType == PlayerAttackType.Light
                && completedStep < MaximumLightComboStep)
            {
                CurrentComboStep = completedStep;
                _comboWindowRemaining = Mathf.Max(
                    0f,
                    ComboWindow - completedRecovery);
                return;
            }

            ResetComboContinuation();
        }

        private bool CanReadCombatInput()
        {
            if (_combatService == null)
            {
                ResolveServices();
            }

            PlayerActionBuffer actionBuffer = ResolveActionBuffer();
            bool hasInputPath = (actionBuffer != null
                    && actionBuffer.IsConsumptionEnabled)
                || (_inputSource != null && _inputSource.IsEnabled);
            return hasInputPath
                && _combatService != null
                && IsGameplayRunning();
        }

        private bool ResolveServices()
        {
            if (_inputSource == null)
            {
                ServiceLocator.TryGet(out _inputSource);
            }

            if (_combatService == null)
            {
                ServiceLocator.TryGet(out _combatService);
            }

            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }

            return _combatService != null;
        }

        private PlayerActionBuffer ResolveActionBuffer()
        {
            if (_actionBuffer == null)
            {
                _actionBuffer = GetComponent<PlayerActionBuffer>();
            }

            return _actionBuffer;
        }

        private bool HasRequestedAction(BufferedActionType type)
        {
            PlayerActionBuffer actionBuffer = ResolveActionBuffer();
            if (actionBuffer != null)
            {
                return actionBuffer.IsConsumptionEnabled
                    && actionBuffer.HasBufferedAction(type);
            }

            if (_inputSource == null || !_inputSource.IsEnabled)
            {
                return false;
            }

            switch (type)
            {
                case BufferedActionType.LightAttack:
                    return _inputSource.LightAttackPressedThisFrame;
                case BufferedActionType.HeavyAttack:
                    return _inputSource.HeavyAttackPressedThisFrame;
                case BufferedActionType.Dodge:
                    return _inputSource.DodgePressedThisFrame;
                default:
                    return false;
            }
        }

        private void ConsumeRequestedAction(BufferedActionType type)
        {
            ResolveActionBuffer()?.TryConsume(type);
        }

        private bool IsActionBlockedByStatus()
        {
            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }

            return _statusEffects != null
                && _statusEffects.IsStunned(gameObject);
        }

        private void CancelAttackAndCombo(bool restoreState)
        {
            IsAttacking = false;
            AttackType = PlayerAttackType.None;
            _attackElapsed = 0f;
            _hitApplied = false;
            _nextLightQueued = false;
            ResetComboContinuation();
            if (restoreState)
            {
                RestoreLocomotionState();
            }
        }

        private void ResetComboContinuation()
        {
            _comboWindowRemaining = 0f;
            if (!IsAttacking || AttackType != PlayerAttackType.Light)
            {
                CurrentComboStep = 0;
            }
        }

        private void RestoreLocomotionState()
        {
            if (_playerController.State == PlayerState.LightAttack
                || _playerController.State == PlayerState.HeavyAttack)
            {
                _playerController.ForceState(
                    _playerController.IsGrounded
                        ? PlayerState.Idle
                        : PlayerState.Fall);
            }
        }

        private static bool CanStartLightAttackFrom(PlayerState state)
        {
            return state == PlayerState.Idle
                || state == PlayerState.Move
                || state == PlayerState.Sprint
                || state == PlayerState.Jump
                || state == PlayerState.Fall;
        }

        private static bool CanStartHeavyAttackFrom(PlayerState state)
        {
            return state == PlayerState.Idle
                || state == PlayerState.Move
                || state == PlayerState.Sprint;
        }

        private static bool IsMounted()
        {
            return ServiceLocator.TryGet<IMountService>(out IMountService mounts)
                && mounts.IsMounted;
        }

        private static bool IsGameplayRunning()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private static bool IsGameplayPaused()
        {
            return GameManager.Instance?.State == GameState.Paused;
        }
    }
}
