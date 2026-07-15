using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Mount;
using Wendao.Systems.Skill;

namespace Wendao.Entities.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerStats))]
    public sealed class PlayerSkillController : SafeBehaviour, IPlayerSkillCaster
    {
        [SerializeField, Min(0f)] private float _castOriginHeight = 1.1f;
        [SerializeField, Min(0f)] private float _castOriginForwardOffset = 0.55f;

        private PlayerController _playerController;
        private PlayerStats _playerStats;
        private PlayerActionBuffer _actionBuffer;
        private IPlayerInputSource _inputSource;
        private ISkillService _skillService;
        private IStatusEffectService _statusEffects;
        private bool _registeredCaster;

        public bool LastAnimationCastPointReleased { get; private set; }

        public GameObject Actor => gameObject;
        public Vector3 CastOrigin => transform.position
            + Vector3.up * _castOriginHeight
            + Forward * _castOriginForwardOffset;
        public Vector3 Forward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : Vector3.forward;
            }
        }

        public bool IsDead => _playerStats == null || _playerStats.IsDead;
        public bool IsSilenced
        {
            get
            {
                ResolveStatusEffectService();
                return _statusEffects != null
                    && (_statusEffects.IsSilenced(gameObject)
                        || _statusEffects.IsStunned(gameObject));
            }
        }
        public bool CanBeginSkillCast
        {
            get
            {
                if (IsDead
                    || IsSilenced
                    || _playerController == null
                    || (ServiceLocator.TryGet<IMountService>(
                            out IMountService mounts)
                        && mounts.IsMounted))
                {
                    return false;
                }

                PlayerState state = _playerController.State;
                return state == PlayerState.Idle
                    || state == PlayerState.Move
                    || state == PlayerState.Sprint;
            }
        }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerStats = GetComponent<PlayerStats>();
            _actionBuffer = GetComponent<PlayerActionBuffer>();
        }

        private void OnEnable()
        {
            TryRegisterCaster();
        }

        private void Update()
        {
            if (!_registeredCaster)
            {
                TryRegisterCaster();
            }

            ResolveServices();
            TryProcessBufferedSkillInput();
        }

        private void OnDisable()
        {
            if (_registeredCaster
                && ServiceLocator.TryGet<IPlayerSkillCaster>(
                    out IPlayerSkillCaster current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerSkillCaster>();
            }

            _registeredCaster = false;
        }

        protected override void SafeStart()
        {
            ResolveServices();
        }

        public bool BeginSkillCast()
        {
            if (!CanBeginSkillCast)
            {
                return false;
            }

            _playerController.ForceState(PlayerState.SkillCast);
            return true;
        }

        public void EndSkillCast()
        {
            if (_playerController != null
                && _playerController.State == PlayerState.SkillCast)
            {
                _playerController.ForceState(
                    _playerController.IsGrounded
                        ? PlayerState.Idle
                        : PlayerState.Fall);
            }
        }

        // Animation Event entry point. SkillManager retains its CastTime
        // fallback so greybox actors without an Animator still release skills.
        public void OnSkillCastPoint()
        {
            ResolveServices();
            LastAnimationCastPointReleased = _skillService
                is ISkillAnimationEventService animationEvents
                && animationEvents.TryReleaseAtAnimationEvent(gameObject);
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

        public void SetSkillService(ISkillService skillService)
        {
            _skillService = skillService;
        }

        public bool TryProcessBufferedSkillInput()
        {
            PlayerActionBuffer actionBuffer = ResolveActionBuffer();
            int barIndex = GetRequestedBarIndex(actionBuffer);
            if (barIndex < 0 || _skillService == null || !CanBeginSkillCast)
            {
                return false;
            }

            Transform lockTarget = _playerController.LockTarget;
            GameObject targetActor = lockTarget != null
                ? lockTarget.gameObject
                : null;
            Vector3 targetPoint = lockTarget != null
                ? lockTarget.position + Vector3.up * 0.9f
                : CastOrigin + Forward * GetSkillRange(barIndex);

            // Once the actor is free to attempt the cast, consume even if the
            // skill service rejects mana/cooldown; this avoids a toast every frame.
            actionBuffer?.TryConsume(GetBufferedAction(barIndex));
            _skillService.TryCast(
                barIndex,
                targetPoint,
                targetActor);
            return true;
        }

        private void ResolveServices()
        {
            if (_inputSource == null)
            {
                ServiceLocator.TryGet(out _inputSource);
            }

            if (_skillService == null)
            {
                ServiceLocator.TryGet(out _skillService);
            }

            ResolveStatusEffectService();
        }

        private void ResolveStatusEffectService()
        {
            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }
        }

        private PlayerActionBuffer ResolveActionBuffer()
        {
            if (_actionBuffer == null)
            {
                _actionBuffer = GetComponent<PlayerActionBuffer>();
            }

            return _actionBuffer;
        }

        private bool TryRegisterCaster()
        {
            if (_registeredCaster)
            {
                return true;
            }

            if (ServiceLocator.TryGet<IPlayerSkillCaster>(
                    out IPlayerSkillCaster existing))
            {
                _registeredCaster = ReferenceEquals(existing, this);
                return _registeredCaster;
            }

            ServiceLocator.Register<IPlayerSkillCaster>(this);
            _registeredCaster = true;
            return true;
        }

        private int GetRequestedBarIndex(PlayerActionBuffer actionBuffer)
        {
            if (actionBuffer != null)
            {
                if (!actionBuffer.IsConsumptionEnabled)
                {
                    return -1;
                }

                for (int barIndex = 0; barIndex < SkillManager.BarSlotCount; barIndex++)
                {
                    if (actionBuffer.HasBufferedAction(GetBufferedAction(barIndex)))
                    {
                        return barIndex;
                    }
                }

                return -1;
            }

            if (_inputSource == null || !_inputSource.IsEnabled)
            {
                return -1;
            }

            if (_inputSource.Skill1PressedThisFrame)
            {
                return 0;
            }

            if (_inputSource.Skill2PressedThisFrame)
            {
                return 1;
            }

            if (_inputSource.Skill3PressedThisFrame)
            {
                return 2;
            }

            return _inputSource.Skill4PressedThisFrame ? 3 : -1;
        }

        private static BufferedActionType GetBufferedAction(int barIndex)
        {
            return (BufferedActionType)((int)BufferedActionType.Skill1 + barIndex);
        }

        private float GetSkillRange(int barIndex)
        {
            string[] equipped = _skillService?.EquippedIds;
            string skillId = equipped != null
                && barIndex >= 0
                && barIndex < equipped.Length
                ? equipped[barIndex]
                : SkillContentIds.BasicQiBolt;
            SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
            return Mathf.Max(1f, skill?.Range ?? 12f);
        }
    }
}
