using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;

namespace Wendao.Entities.Enemy
{
    public enum EnemyBrainState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Attack,
        Skill,
        Return,
        Dead
    }

    [RequireComponent(typeof(CharacterController), typeof(NavMeshAgent))]
    public sealed class EnemyBrain : SafeBehaviour,
        IDamageable,
        ICombatStatsProvider,
        ICombatTeamProvider,
        ICombatDefenseProvider,
        ILockOnTarget,
        IHitstunReceiver,
        ICombatDeathHandler
    {
        public const float DefaultLockOnDisengageRange = 14f;
        public const float IdleBeforePatrolSeconds = 0.6f;
        public const float AlertDurationSeconds = 0.4f;
        public const float PatrolWaitMinSeconds = 1f;
        public const float PatrolWaitMaxSeconds = 2f;
        public const float PatrolArrivalDistance = 0.25f;
        public const float ReturnArrivalDistance = 0.2f;
        public const float ReturnTeleportSeconds = 3f;
        public const float NavigationSampleDistance = 2f;
        public const float NavigationRefreshSeconds = 0.2f;
        public const float EliteChargeWindupSeconds = 0.4f;
        public const float EliteChargeDurationSeconds = 0.7f;
        public const float EliteChargeSpeedMultiplier = 2.4f;
        public const float EliteChargeDamageMultiplier = 1.5f;
        public const float EliteChargeCooldownSeconds = 6f;
        public const float BossPhaseTransitionSeconds = 1.5f;
        public const float BossSkillCastSeconds = 0.35f;
        public const float BossSkillRecoverySeconds = 0.25f;
        public const float BossSkillCooldownSeconds = 2.5f;
        public const float BossRageAttackSpeedMultiplier = 1.3f;

        private CharacterController _controller;
        private NavMeshAgent _agent;
        private NavMeshPath _navigationPath;
        private Vector3[] _navigationCorners = Array.Empty<Vector3>();
        private readonly List<Vector3> _patrolPoints = new List<Vector3>();
        private Transform _lockOnAnchor;
        private GameObject _alertIndicator;
        private Collider[] _colliders = Array.Empty<Collider>();
        private bool[] _colliderAliveStates = Array.Empty<bool>();
        private ICombatService _combatService;
        private IStatusEffectService _statusEffects;
        private IDamageable _targetDamageable;
        private GameObject _targetObject;
        private bool _registeredActor;
        private bool _deathHandled;
        private float _attackCooldown;
        private float _returnElapsed;
        private float _hitstunRemaining;
        private float _idleElapsed;
        private float _alertElapsed;
        private float _patrolWaitRemaining;
        private float _navigationRefreshRemaining;
        private int _patrolPointIndex;
        private int _navigationCornerIndex;
        private Vector3 _navigationDestination;
        private bool _hasNavigationDestination;
        private float _skillCooldownRemaining;
        private float _skillElapsed;
        private float _chargeElapsed;
        private Vector3 _chargeDirection;
        private bool _chargeReleased;
        private bool _chargeHit;
        private readonly List<EnemyBrain> _bossSummons = new List<EnemyBrain>();
        private float _bossTransitionRemaining;
        private float _bossSkillCooldownRemaining;
        private float _bossSkillElapsed;
        private bool _bossSkillResolved;
        private int _bossSkillCursor;
        private float _bossSkillCastDuration = BossSkillCastSeconds;
        private float _bossSkillRecoveryDuration = BossSkillRecoverySeconds;
        private BossSkillTelegraph _activeBossTelegraph;
        private BossSkillTelegraphView _bossTelegraphView;

        public EnemyData Data { get; private set; }
        public EnemyBrainState State { get; private set; } = EnemyBrainState.Idle;
        public Vector3 SpawnPosition { get; private set; }
        public GameObject Target => _targetObject;
        public int PatrolPointCount => _patrolPoints.Count;
        public int PatrolPointIndex => _patrolPointIndex;
        public float PatrolWaitRemaining => _patrolWaitRemaining;
        public bool IsAlertIndicatorVisible => _alertIndicator != null
            && _alertIndicator.activeSelf;
        public bool IsOnNavMesh => _agent != null
            && _agent.enabled
            && _agent.isOnNavMesh;
        public int LastPathCornerCount => _navigationCorners.Length;
        public NavMeshPathStatus LastPathStatus => _navigationPath == null
            ? NavMeshPathStatus.PathInvalid
            : _navigationPath.status;
        public string ActiveSkillId { get; private set; } = string.Empty;
        public string LastSkillId { get; private set; } = string.Empty;
        public int SkillUseCount { get; private set; }
        public float SkillCooldownRemaining => _skillCooldownRemaining;
        public bool IsCharging => State == EnemyBrainState.Skill
            && _chargeReleased;
        public int CurrentBossPhase { get; private set; }
        public float BossTransitionRemaining => _bossTransitionRemaining;
        public bool IsBossTransitioning => _bossTransitionRemaining > 0f;
        public string LastBossSkillId { get; private set; } = string.Empty;
        public int BossSkillUseCount { get; private set; }
        public int LivingBossSummonCount => CountLivingBossSummons();
        public BossSkillTelegraph ActiveBossTelegraph => _activeBossTelegraph;
        public BossSkillTelegraphView BossTelegraphView => _bossTelegraphView;
        public bool IsBossTelegraphVisible => _bossTelegraphView != null
            && _bossTelegraphView.IsVisible;
        public float BossTelegraphRemaining => !_bossSkillResolved
            && !string.IsNullOrEmpty(ActiveSkillId)
                ? Mathf.Max(0f, _bossSkillCastDuration - _bossSkillElapsed)
                : 0f;
        public bool IsInBossRecovery => State == EnemyBrainState.Skill
            && _bossSkillResolved
            && !string.IsNullOrEmpty(ActiveSkillId)
            && _bossSkillElapsed
                < _bossSkillCastDuration + _bossSkillRecoveryDuration;
        public float BossRecoveryRemaining => IsInBossRecovery
            ? Mathf.Max(
                0f,
                _bossSkillCastDuration
                    + _bossSkillRecoveryDuration
                    - _bossSkillElapsed)
            : 0f;
        public float EffectiveAttackInterval => Data != null
            && Data.Rank == EnemyRank.Boss
            && CurrentBossPhase >= 2
                ? Mathf.Max(
                    0.1f,
                    Data.AttackInterval / BossRageAttackSpeedMultiplier)
                : Mathf.Max(0.1f, Data?.AttackInterval ?? 0.1f);
        public IReadOnlyList<string> CurrentBossSkillIds =>
            ResolveCurrentBossPhase()?.SkillIds ?? Array.Empty<string>();
        public string NameLocalizationKey => Data == null
            ? string.Empty
            : "enemy_name_" + Data.Id;
        public float CurrentHp { get; private set; }
        public float MaxHp => Mathf.Max(1f, Data?.MaxHp ?? 1f);
        public bool IsDead => CurrentHp <= 0f || State == EnemyBrainState.Dead;
        public float Attack => Mathf.Max(0f, Data?.Attack ?? 0f);
        public float Defense => Mathf.Max(0f, Data?.Defense ?? 0f);
        public float CritRate => 0f;
        public float CritDamage => 1.5f;
        public float HitstunMultiplier => Data != null
            && Data.Rank == EnemyRank.Elite
                ? CombatFeelSettings.EliteHitstunMultiplier
                : 1f;
        public float HitstunRemaining => _hitstunRemaining;
        public CombatTeam Team => CombatTeam.Enemy;
        public bool IsInvincible => IsBossTransitioning
            && (ResolveCurrentBossPhase()?.InvulnerableDuringTransition ?? false);
        public bool IsBlocking => false;
        public bool CanLockOn => isActiveAndEnabled && !IsDead;
        public Transform LockOnTransform => _lockOnAnchor != null
            ? _lockOnAnchor
            : transform;
        public float LockOnDisengageRange => Mathf.Max(
            0.1f,
            Data?.DisengageRange ?? DefaultLockOnDisengageRange);

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _navigationPath = new NavMeshPath();
            ConfigureNavigationAgent();
            EnsureLockOnAnchor();
            EnsureAlertIndicator();
            CaptureColliderStates();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            TryRegisterActor();
        }

        private void Update()
        {
            if (!_registeredActor)
            {
                TryRegisterActor();
            }

            GameManager gameManager = GameManager.Instance;
            if (Data == null
                || IsDead
                || (gameManager != null
                    && gameManager.State != GameState.Playing))
            {
                return;
            }

            TickAI(Time.deltaTime);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            UnregisterActor();
        }

        protected override void SafeStart()
        {
            TryRegisterActor();
        }

        public void SpawnInit(EnemyData data, Vector3 position)
        {
            Data = data;
            ConfigureNavigationAgent();
            Vector3 spawn = SampleNavigationPosition(position, out Vector3 sampled)
                ? sampled
                : position;
            Teleport(spawn);
            SpawnPosition = transform.position;
            CurrentHp = MaxHp;
            _deathHandled = false;
            _attackCooldown = 0f;
            _returnElapsed = 0f;
            _hitstunRemaining = 0f;
            _idleElapsed = 0f;
            _alertElapsed = 0f;
            _patrolWaitRemaining = 0f;
            _patrolPointIndex = 0;
            _skillCooldownRemaining = 0f;
            _skillElapsed = 0f;
            _chargeElapsed = 0f;
            _chargeDirection = Vector3.zero;
            _chargeReleased = false;
            _chargeHit = false;
            ActiveSkillId = string.Empty;
            LastSkillId = string.Empty;
            SkillUseCount = 0;
            CurrentBossPhase = 0;
            _bossTransitionRemaining = 0f;
            _bossSkillCooldownRemaining = 0f;
            _bossSkillElapsed = 0f;
            _bossSkillResolved = false;
            _bossSkillCursor = 0;
            _bossSkillCastDuration = BossSkillCastSeconds;
            _bossSkillRecoveryDuration = BossSkillRecoverySeconds;
            _activeBossTelegraph = null;
            if (_bossTelegraphView != null)
            {
                _bossTelegraphView.Hide();
            }
            LastBossSkillId = string.Empty;
            BossSkillUseCount = 0;
            _bossSummons.Clear();
            ClearTarget();
            ClearNavigationPath();
            State = EnemyBrainState.Idle;
            SetAlertIndicator(false);
            BindAgentToNavigation();
            SetAgentStopped(false);
            SetCollidersAlive(true);
        }

        public void ConfigurePatrolRoute(IReadOnlyList<Vector3> worldPoints)
        {
            _patrolPoints.Clear();
            if (worldPoints != null)
            {
                for (int index = 0; index < worldPoints.Count; index++)
                {
                    Vector3 point = worldPoints[index];
                    if (IsFinite(point))
                    {
                        _patrolPoints.Add(point);
                    }
                }
            }

            _patrolPointIndex = ResolveInitialPatrolPointIndex();
            _patrolWaitRemaining = 0f;
            ClearNavigationPath();
        }

        public void TickAI(float deltaTime)
        {
            if (Data == null
                || IsDead
                || deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            ResolveCombatService();
            ResolveStatusEffectService();
            if (HasLivingTarget()
                && IsPositionInsideSafeZone(
                    _targetObject.transform.position))
            {
                BeginReturn();
                return;
            }

            _skillCooldownRemaining = Mathf.Max(
                0f,
                _skillCooldownRemaining - deltaTime);
            _bossSkillCooldownRemaining = Mathf.Max(
                0f,
                _bossSkillCooldownRemaining - deltaTime);
            if (IsBossTransitioning)
            {
                _bossTransitionRemaining = Mathf.Max(
                    0f,
                    _bossTransitionRemaining - deltaTime);
                if (_bossTransitionRemaining <= 0f)
                {
                    State = HasLivingTarget()
                        ? EnemyBrainState.Chase
                        : EnemyBrainState.Idle;
                    SetAgentStopped(!HasLivingTarget());
                }

                return;
            }
            if (_hitstunRemaining > 0f)
            {
                _hitstunRemaining = Mathf.Max(
                    0f,
                    _hitstunRemaining - deltaTime);
                return;
            }

            if (_statusEffects != null && _statusEffects.IsStunned(gameObject))
            {
                return;
            }

            switch (State)
            {
                case EnemyBrainState.Idle:
                    TickIdle(deltaTime);
                    break;
                case EnemyBrainState.Patrol:
                    TickPatrol(deltaTime);
                    break;
                case EnemyBrainState.Alert:
                    TickAlert(deltaTime);
                    break;
                case EnemyBrainState.Chase:
                    TickChase(deltaTime);
                    break;
                case EnemyBrainState.Attack:
                    TickAttack(deltaTime);
                    break;
                case EnemyBrainState.Skill:
                    TickSkill(deltaTime);
                    break;
                case EnemyBrainState.Return:
                    TickReturn(deltaTime);
                    break;
            }
        }

        public void OnAggro(GameObject target)
        {
            if (Data == null
                || IsDead
                || target == null
                || IsPositionInsideSafeZone(target.transform.position))
            {
                return;
            }

            IDamageable damageable = FindDamageable(target);
            if (damageable == null || damageable.IsDead)
            {
                return;
            }

            if (State == EnemyBrainState.Skill
                && target == _targetObject
                && _targetDamageable == damageable)
            {
                return;
            }

            _targetObject = target;
            _targetDamageable = damageable;
            _returnElapsed = 0f;
            BeginAlert();
        }

        public void ForceState(EnemyBrainState next)
        {
            if (IsDead)
            {
                State = EnemyBrainState.Dead;
                return;
            }

            switch (next)
            {
                case EnemyBrainState.Return:
                    BeginReturn();
                    break;
                case EnemyBrainState.Idle:
                    EnterIdle();
                    break;
                case EnemyBrainState.Patrol:
                    ClearTarget();
                    SetAlertIndicator(false);
                    State = _patrolPoints.Count > 0
                        ? EnemyBrainState.Patrol
                        : EnemyBrainState.Idle;
                    _patrolWaitRemaining = 0f;
                    ClearNavigationPath();
                    break;
                case EnemyBrainState.Alert:
                    if (HasLivingTarget())
                    {
                        BeginAlert();
                    }
                    break;
                case EnemyBrainState.Chase:
                case EnemyBrainState.Attack:
                    if (HasLivingTarget())
                    {
                        ClearActiveSkill();
                        SetAlertIndicator(false);
                        State = next;
                        if (next == EnemyBrainState.Attack)
                        {
                            _attackCooldown = 0f;
                        }
                    }
                    break;
                case EnemyBrainState.Skill:
                    TryBeginEliteCharge();
                    break;
                case EnemyBrainState.Dead:
                    break;
            }
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (IsDead || IsInvincible || info.Amount <= 0f)
            {
                return;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - info.Amount);
            if (!IsDead && info.Source != null)
            {
                GameObject player = ResolvePlayerRoot(info.Source);
                if (player != null)
                {
                    OnAggro(player);
                }
            }

            if (CurrentHp > 0f && Data?.Rank == EnemyRank.Boss)
            {
                EvaluateBossPhaseTransition();
            }

            if (CurrentHp <= 0f)
            {
                State = EnemyBrainState.Dead;
                ClearTarget();
                ClearNavigationPath();
                SetAlertIndicator(false);
                SetAgentStopped(true);
                ClearActiveSkill();
                if (Data?.Rank == EnemyRank.Boss)
                {
                    ClearBossSummons();
                }
                SetCollidersAlive(false);
            }
        }

        public void ApplyHitstun(float seconds)
        {
            if (IsDead
                || seconds <= 0f
                || float.IsNaN(seconds)
                || float.IsInfinity(seconds))
            {
                return;
            }

            _hitstunRemaining = Mathf.Max(_hitstunRemaining, seconds);
        }

        public void HandleDeath(DamageInfo killingBlow)
        {
            if (!IsDead || _deathHandled || Data == null)
            {
                return;
            }

            _deathHandled = true;
            State = EnemyBrainState.Dead;
            SetAlertIndicator(false);
            SetAgentStopped(true);
            ClearActiveSkill();
            if (Data.Rank == EnemyRank.Boss)
            {
                ClearBossSummons();
            }
            EventBus.Publish(
                CombatEvents.EnemyKilled,
                new EnemyDeathInfo
                {
                    EnemyId = Data.Id,
                    Rank = Data.Rank,
                    Victim = gameObject,
                    Killer = killingBlow.Source,
                    Position = transform.position
                });
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        public float GetBlockDamageReduction(DamageType damageType)
        {
            return 0f;
        }

        public void ResetBossEncounter()
        {
            if (Data == null || Data.Rank != EnemyRank.Boss || IsDead)
            {
                return;
            }

            ResetBossRuntimeState();
            CurrentHp = MaxHp;
            Teleport(SpawnPosition);
            EnterIdle();
        }

        private void TickIdle(float deltaTime)
        {
            if (HorizontalDistance(transform.position, SpawnPosition)
                > Mathf.Max(0.1f, Data.DisengageRange))
            {
                BeginReturn();
                return;
            }

            if (TryDetectPlayer())
            {
                return;
            }

            _idleElapsed += deltaTime;
            if (_patrolPoints.Count > 0
                && _idleElapsed >= IdleBeforePatrolSeconds)
            {
                _idleElapsed = 0f;
                _patrolWaitRemaining = 0f;
                State = EnemyBrainState.Patrol;
                ClearNavigationPath();
            }
        }

        private void TickPatrol(float deltaTime)
        {
            if (HorizontalDistance(transform.position, SpawnPosition)
                > Mathf.Max(0.1f, Data.DisengageRange))
            {
                BeginReturn();
                return;
            }

            if (TryDetectPlayer())
            {
                return;
            }

            if (_patrolPoints.Count == 0)
            {
                EnterIdle();
                return;
            }

            if (_patrolWaitRemaining > 0f)
            {
                _patrolWaitRemaining = Mathf.Max(
                    0f,
                    _patrolWaitRemaining - deltaTime);
                if (_patrolWaitRemaining <= 0f)
                {
                    AdvancePatrolPoint();
                }

                return;
            }

            Vector3 destination = _patrolPoints[_patrolPointIndex];
            if (HorizontalDistance(transform.position, destination)
                <= PatrolArrivalDistance)
            {
                _patrolWaitRemaining = ResolvePatrolWaitSeconds();
                SetAgentStopped(true);
                return;
            }

            SetAgentStopped(false);
            MoveTowards(destination, deltaTime);
        }

        private void TickAlert(float deltaTime)
        {
            if (ShouldDisengage())
            {
                BeginReturn();
                return;
            }

            FaceTowards(_targetObject.transform.position, deltaTime);
            _alertElapsed += deltaTime;
            if (_alertElapsed < AlertDurationSeconds)
            {
                return;
            }

            SetAlertIndicator(false);
            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance <= Mathf.Max(0.1f, Data.AttackRange))
            {
                State = EnemyBrainState.Attack;
                _attackCooldown = 0f;
                SetAgentStopped(true);
            }
            else
            {
                State = EnemyBrainState.Chase;
                SetAgentStopped(false);
            }
        }

        private void TickChase(float deltaTime)
        {
            if (ShouldDisengage())
            {
                BeginReturn();
                return;
            }

            if (TryBeginBossSkill() || TryBeginEliteCharge())
            {
                return;
            }

            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance <= Mathf.Max(0.1f, Data.AttackRange))
            {
                State = EnemyBrainState.Attack;
                _attackCooldown = 0f;
                SetAgentStopped(true);
                return;
            }

            SetAgentStopped(false);
            MoveTowards(_targetObject.transform.position, deltaTime);
        }

        private void TickAttack(float deltaTime)
        {
            if (ShouldDisengage())
            {
                BeginReturn();
                return;
            }

            if (TryBeginBossSkill())
            {
                return;
            }

            float attackRange = Mathf.Max(0.1f, Data.AttackRange);
            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance > attackRange * 1.15f)
            {
                State = EnemyBrainState.Chase;
                SetAgentStopped(false);
                return;
            }

            SetAgentStopped(true);
            FaceTowards(_targetObject.transform.position, deltaTime);
            _attackCooldown -= deltaTime;
            if (_attackCooldown > 0f
                || _combatService == null
                || _targetDamageable == null
                || _targetDamageable.IsDead)
            {
                return;
            }

            _combatService.DealDamage(
                _targetDamageable,
                new DamageRequest
                {
                    Source = gameObject,
                    BaseDamage = Mathf.Max(0f, Data.Attack),
                    Type = DamageType.Physical,
                    Element = ElementType.None,
                    Multiplier = 1f,
                    CanCrit = false,
                    SkillId = string.Empty
                });
            _attackCooldown = EffectiveAttackInterval;
        }

        private void TickSkill(float deltaTime)
        {
            if (Data?.Rank == EnemyRank.Boss)
            {
                TickBossSkill(deltaTime);
                return;
            }

            if (ShouldDisengage())
            {
                BeginReturn();
                return;
            }

            _skillElapsed += deltaTime;
            float chargeDeltaTime = deltaTime;
            if (!_chargeReleased)
            {
                FaceTowards(_targetObject.transform.position, deltaTime);
                if (_skillElapsed < EliteChargeWindupSeconds)
                {
                    return;
                }

                _chargeDirection = _targetObject.transform.position
                    - transform.position;
                _chargeDirection.y = 0f;
                if (_chargeDirection.sqrMagnitude <= 0.0001f)
                {
                    FinishEliteCharge();
                    return;
                }

                _chargeDirection.Normalize();
                _chargeReleased = true;
                _chargeElapsed = 0f;
                SkillUseCount++;
                LastSkillId = ActiveSkillId;
                chargeDeltaTime = Mathf.Max(
                    0f,
                    _skillElapsed - EliteChargeWindupSeconds);
                if (chargeDeltaTime <= 0f)
                {
                    return;
                }
            }

            _chargeElapsed += chargeDeltaTime;
            MoveCharge(chargeDeltaTime);
            TryDealChargeDamage();
            if (_chargeElapsed >= EliteChargeDurationSeconds)
            {
                FinishEliteCharge();
            }
        }

        private bool TryBeginEliteCharge()
        {
            if (Data == null
                || Data.Rank != EnemyRank.Elite
                || !HasSkill(Wendao.Systems.Enemy.EnemyContentIds.EliteWolfCharge)
                || !HasLivingTarget()
                || _skillCooldownRemaining > 0f)
            {
                return false;
            }

            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            float minimumDistance = Mathf.Max(
                2.5f,
                Data.AttackRange * 1.5f);
            float maximumDistance = Mathf.Min(
                Mathf.Max(minimumDistance, Data.AggroRange),
                9f);
            if (distance < minimumDistance || distance > maximumDistance)
            {
                return false;
            }

            ActiveSkillId = Wendao.Systems.Enemy.EnemyContentIds.EliteWolfCharge;
            _skillElapsed = 0f;
            _chargeElapsed = 0f;
            _chargeDirection = Vector3.zero;
            _chargeReleased = false;
            _chargeHit = false;
            ClearNavigationPath();
            SetAgentStopped(true);
            SetAlertIndicator(false);
            State = EnemyBrainState.Skill;
            return true;
        }

        private void MoveCharge(float deltaTime)
        {
            ResolveStatusEffectService();
            if (_chargeDirection.sqrMagnitude <= 0.0001f
                || (_statusEffects != null
                    && _statusEffects.IsRooted(gameObject)))
            {
                return;
            }

            FaceDirection(_chargeDirection, deltaTime);
            Vector3 displacement = _chargeDirection
                * ResolveMoveSpeed()
                * EliteChargeSpeedMultiplier
                * deltaTime;
            if (_controller != null && _controller.enabled)
            {
                _controller.Move(displacement);
            }
            else
            {
                transform.position += displacement;
            }

            SyncAgentPosition();
        }

        private void TryDealChargeDamage()
        {
            if (_chargeHit
                || _combatService == null
                || _targetDamageable == null
                || _targetDamageable.IsDead
                || HorizontalDistance(
                    transform.position,
                    _targetObject.transform.position)
                    > Mathf.Max(0.1f, Data.AttackRange + 0.8f))
            {
                return;
            }

            _combatService.DealDamage(
                _targetDamageable,
                new DamageRequest
                {
                    Source = gameObject,
                    BaseDamage = Mathf.Max(0f, Data.Attack),
                    Type = DamageType.Physical,
                    Element = ElementType.None,
                    Multiplier = EliteChargeDamageMultiplier,
                    CanCrit = false,
                    SkillId = ActiveSkillId
                });
            _chargeHit = true;
        }

        private void FinishEliteCharge()
        {
            ActiveSkillId = string.Empty;
            _skillCooldownRemaining = EliteChargeCooldownSeconds;
            _skillElapsed = 0f;
            _chargeElapsed = 0f;
            _chargeDirection = Vector3.zero;
            _chargeReleased = false;
            _chargeHit = false;
            if (!HasLivingTarget())
            {
                BeginReturn();
                return;
            }

            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance <= Mathf.Max(0.1f, Data.AttackRange * 1.15f))
            {
                State = EnemyBrainState.Attack;
                _attackCooldown = 0f;
                SetAgentStopped(true);
            }
            else
            {
                State = EnemyBrainState.Chase;
                SetAgentStopped(false);
            }
        }

        private bool TryBeginBossSkill()
        {
            if (Data == null
                || Data.Rank != EnemyRank.Boss
                || IsBossTransitioning
                || !HasLivingTarget()
                || _bossSkillCooldownRemaining > 0f)
            {
                return false;
            }

            BossPhase phase = ResolveCurrentBossPhase();
            if (phase?.SkillIds == null || phase.SkillIds.Length == 0)
            {
                return false;
            }

            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance > Mathf.Max(Data.AggroRange, 12f))
            {
                return false;
            }

            int skillIndex = _bossSkillCursor % phase.SkillIds.Length;
            string skillId = phase.SkillIds[skillIndex];
            _bossSkillCursor = (_bossSkillCursor + 1) % phase.SkillIds.Length;
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            ActiveSkillId = skillId;
            _bossSkillElapsed = 0f;
            _bossSkillResolved = false;
            _activeBossTelegraph = ResolveBossTelegraph(phase, skillId);
            _bossSkillCastDuration = Mathf.Max(
                0.01f,
                _activeBossTelegraph?.Duration ?? BossSkillCastSeconds);
            _bossSkillRecoveryDuration = Mathf.Max(
                0f,
                _activeBossTelegraph?.RecoverStun
                    ?? BossSkillRecoverySeconds);
            if (_activeBossTelegraph != null)
            {
                EnsureBossTelegraphView();
                _bossTelegraphView.Show(
                    _activeBossTelegraph,
                    _targetObject.transform.position);
            }
            ClearNavigationPath();
            SetAgentStopped(true);
            SetAlertIndicator(false);
            State = EnemyBrainState.Skill;
            return true;
        }

        private void TickBossSkill(float deltaTime)
        {
            if (ShouldDisengage())
            {
                BeginReturn();
                return;
            }

            FaceTowards(_targetObject.transform.position, deltaTime);
            _bossSkillElapsed += deltaTime;
            if (!_bossSkillResolved && _bossTelegraphView != null)
            {
                _bossTelegraphView.SetProgress(
                    _bossSkillElapsed / Mathf.Max(0.01f, _bossSkillCastDuration));
            }

            if (!_bossSkillResolved
                && _bossSkillElapsed >= _bossSkillCastDuration)
            {
                _bossTelegraphView?.Hide();
                ResolveBossSkill(ActiveSkillId);
                _bossSkillResolved = true;
                LastBossSkillId = ActiveSkillId;
                BossSkillUseCount++;
            }

            if (_bossSkillElapsed
                < _bossSkillCastDuration + _bossSkillRecoveryDuration)
            {
                return;
            }

            ClearActiveSkill();
            _bossSkillCooldownRemaining = BossSkillCooldownSeconds;
            float distance = HorizontalDistance(
                transform.position,
                _targetObject.transform.position);
            if (distance <= Mathf.Max(0.1f, Data.AttackRange * 1.15f))
            {
                State = EnemyBrainState.Attack;
                _attackCooldown = 0f;
                SetAgentStopped(true);
            }
            else
            {
                State = EnemyBrainState.Chase;
                SetAgentStopped(false);
            }
        }

        private void ResolveBossSkill(string skillId)
        {
            switch (skillId)
            {
                case Wendao.Systems.Enemy.EnemyContentIds.StoneGeneralSlam:
                    DealBossSkillDamage(skillId, 4f, 1.25f);
                    break;
                case Wendao.Systems.Enemy.EnemyContentIds.StoneGeneralSpike:
                    DealBossSkillDamage(skillId, 8f, 1f);
                    break;
                case Wendao.Systems.Enemy.EnemyContentIds.StoneGeneralCharge:
                    LungeBossTowardsTarget(3.5f);
                    DealBossSkillDamage(skillId, 3.5f, 1.35f);
                    break;
                case Wendao.Systems.Enemy.EnemyContentIds.StoneGeneralSummon:
                    SummonBlackwindAdds();
                    break;
                case Wendao.Systems.Enemy.EnemyContentIds.StoneGeneralRageSlam:
                    DealBossSkillDamage(skillId, 12f, 1.6f);
                    break;
            }
        }

        private static BossSkillTelegraph ResolveBossTelegraph(
            BossPhase phase,
            string skillId)
        {
            BossSkillTelegraph[] telegraphs = phase?.Telegraphs;
            if (telegraphs == null || string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            for (int index = 0; index < telegraphs.Length; index++)
            {
                BossSkillTelegraph telegraph = telegraphs[index];
                if (telegraph != null
                    && string.Equals(
                        telegraph.SkillId,
                        skillId,
                        StringComparison.Ordinal))
                {
                    return telegraph;
                }
            }

            return null;
        }

        private void EnsureBossTelegraphView()
        {
            if (_bossTelegraphView != null)
            {
                return;
            }

            var telegraphObject = new GameObject(
                BossSkillTelegraphView.ObjectName);
            telegraphObject.transform.SetParent(transform, false);
            _bossTelegraphView =
                telegraphObject.AddComponent<BossSkillTelegraphView>();
        }

        private void DealBossSkillDamage(
            string skillId,
            float range,
            float multiplier)
        {
            if (_combatService == null
                || _targetDamageable == null
                || _targetDamageable.IsDead
                || HorizontalDistance(
                    transform.position,
                    _targetObject.transform.position) > range)
            {
                return;
            }

            _combatService.DealDamage(
                _targetDamageable,
                new DamageRequest
                {
                    Source = gameObject,
                    BaseDamage = Mathf.Max(0f, Data.Attack),
                    Type = DamageType.Physical,
                    Element = ElementType.None,
                    Multiplier = multiplier,
                    CanCrit = false,
                    SkillId = skillId
                });
        }

        private void LungeBossTowardsTarget(float maximumDistance)
        {
            if (!HasLivingTarget())
            {
                return;
            }

            Vector3 direction = _targetObject.transform.position
                - transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            direction /= distance;
            Vector3 displacement = direction
                * Mathf.Min(Mathf.Max(0f, maximumDistance), distance);
            if (_controller != null && _controller.enabled)
            {
                _controller.Move(displacement);
            }
            else
            {
                transform.position += displacement;
            }

            FaceDirection(direction, 1f);
            SyncAgentPosition();
        }

        private void SummonBlackwindAdds()
        {
            PruneBossSummons();
            if (_bossSummons.Count >= 2 || !HasLivingTarget())
            {
                return;
            }

            EnemyData summonData = ConfigDatabase.Instance?.GetEnemy(
                Wendao.Systems.Enemy.EnemyContentIds.BlackwindSpawn);
            if (summonData == null)
            {
                return;
            }

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }

            right.Normalize();
            for (int index = 0; index < 2; index++)
            {
                Vector3 offset = right * (index == 0 ? -2f : 2f);
                EnemyBrain summon = EnemySpawner.CreateRuntimeEnemy(
                    summonData,
                    transform.position + offset,
                    gameObject.scene,
                    null,
                    $"Enemy_BlackwindSummon_{GetEntityId().GetHashCode()}_{index + 1}");
                if (summon == null)
                {
                    continue;
                }

                summon.OnAggro(_targetObject);
                _bossSummons.Add(summon);
            }
        }

        private void EvaluateBossPhaseTransition()
        {
            BossPhase[] phases = Data?.BossPhases;
            if (phases == null || phases.Length == 0)
            {
                return;
            }

            float hpPercent = Mathf.Clamp01(CurrentHp / MaxHp);
            int nextPhase = CurrentBossPhase;
            for (int index = 0; index < phases.Length; index++)
            {
                BossPhase phase = phases[index];
                if (phase != null
                    && phase.PhaseIndex > nextPhase
                    && hpPercent <= phase.HpThreshold)
                {
                    nextPhase = phase.PhaseIndex;
                }
            }

            if (nextPhase <= CurrentBossPhase)
            {
                return;
            }

            int previousPhase = CurrentBossPhase;
            CurrentBossPhase = nextPhase;
            _bossTransitionRemaining = BossPhaseTransitionSeconds;
            _bossSkillCooldownRemaining = 0f;
            _bossSkillCursor = 0;
            ClearActiveSkill();
            ClearNavigationPath();
            ClearBossSummons();
            SetAgentStopped(true);
            State = EnemyBrainState.Skill;
            EventBus.Publish(
                Wendao.Systems.Combat.CombatEvents.BossPhaseChanged,
                new BossPhaseInfo
                {
                    BossId = Data.Id,
                    OldPhase = previousPhase,
                    NewPhase = CurrentBossPhase,
                    HpPercent = hpPercent
                });
        }

        private BossPhase ResolveCurrentBossPhase()
        {
            BossPhase[] phases = Data?.BossPhases;
            if (phases == null)
            {
                return null;
            }

            for (int index = 0; index < phases.Length; index++)
            {
                if (phases[index] != null
                    && phases[index].PhaseIndex == CurrentBossPhase)
                {
                    return phases[index];
                }
            }

            return null;
        }

        private void ResetBossRuntimeState()
        {
            ClearBossSummons();
            CurrentBossPhase = 0;
            _bossTransitionRemaining = 0f;
            _bossSkillCooldownRemaining = 0f;
            _bossSkillElapsed = 0f;
            _bossSkillResolved = false;
            _bossSkillCursor = 0;
            _bossSkillCastDuration = BossSkillCastSeconds;
            _bossSkillRecoveryDuration = BossSkillRecoverySeconds;
            _activeBossTelegraph = null;
            _bossTelegraphView?.Hide();
            LastBossSkillId = string.Empty;
            BossSkillUseCount = 0;
        }

        private int CountLivingBossSummons()
        {
            PruneBossSummons();
            int count = 0;
            for (int index = 0; index < _bossSummons.Count; index++)
            {
                if (_bossSummons[index] != null
                    && !_bossSummons[index].IsDead)
                {
                    count++;
                }
            }

            return count;
        }

        private void PruneBossSummons()
        {
            for (int index = _bossSummons.Count - 1; index >= 0; index--)
            {
                if (_bossSummons[index] == null || _bossSummons[index].IsDead)
                {
                    _bossSummons.RemoveAt(index);
                }
            }
        }

        private void ClearBossSummons()
        {
            for (int index = 0; index < _bossSummons.Count; index++)
            {
                if (_bossSummons[index] != null)
                {
                    Destroy(_bossSummons[index].gameObject);
                }
            }

            _bossSummons.Clear();
        }

        private void TickReturn(float deltaTime)
        {
            _returnElapsed += deltaTime;
            float distance = HorizontalDistance(transform.position, SpawnPosition);
            if (distance <= ReturnArrivalDistance
                || _returnElapsed >= ReturnTeleportSeconds)
            {
                Teleport(SpawnPosition);
                CurrentHp = MaxHp;
                _returnElapsed = 0f;
                if (Data.Rank == EnemyRank.Boss)
                {
                    ResetBossRuntimeState();
                }
                EnterIdle();
                return;
            }

            SetAgentStopped(false);
            MoveTowards(SpawnPosition, deltaTime);
        }

        private bool ShouldDisengage()
        {
            if (!HasLivingTarget())
            {
                return true;
            }

            float disengage = Mathf.Max(0.1f, Data.DisengageRange);
            return HorizontalDistance(transform.position, SpawnPosition) > disengage
                || HorizontalDistance(_targetObject.transform.position, SpawnPosition)
                    > disengage;
        }

        private bool HasLivingTarget()
        {
            return _targetObject != null
                && _targetDamageable != null
                && !_targetDamageable.IsDead;
        }

        private bool HasSkill(string skillId)
        {
            if (Data?.SkillIds == null || string.IsNullOrEmpty(skillId))
            {
                return false;
            }

            for (int index = 0; index < Data.SkillIds.Length; index++)
            {
                if (string.Equals(
                        Data.SkillIds[index],
                        skillId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void BeginReturn()
        {
            ClearTarget();
            _returnElapsed = 0f;
            _alertElapsed = 0f;
            if (Data?.Rank == EnemyRank.Boss)
            {
                ClearBossSummons();
            }
            ClearActiveSkill();
            SetAlertIndicator(false);
            ClearNavigationPath();
            SetAgentStopped(false);
            State = EnemyBrainState.Return;
        }

        private void BeginAlert()
        {
            _alertElapsed = 0f;
            _attackCooldown = 0f;
            ClearActiveSkill();
            ClearNavigationPath();
            SetAgentStopped(true);
            SetAlertIndicator(true);
            State = EnemyBrainState.Alert;
        }

        private void EnterIdle()
        {
            ClearTarget();
            _idleElapsed = 0f;
            _alertElapsed = 0f;
            _patrolWaitRemaining = 0f;
            ClearActiveSkill();
            SetAlertIndicator(false);
            ClearNavigationPath();
            SetAgentStopped(false);
            State = EnemyBrainState.Idle;
        }

        private void HandlePlayerDied(DeathInfo info)
        {
            if (!IsDead
                && (info.Victim == null || info.Victim == _targetObject)
                && (State == EnemyBrainState.Alert
                    || State == EnemyBrainState.Chase
                    || State == EnemyBrainState.Attack
                    || State == EnemyBrainState.Skill))
            {
                BeginReturn();
            }
        }

        private void ClearActiveSkill()
        {
            ActiveSkillId = string.Empty;
            _skillElapsed = 0f;
            _chargeElapsed = 0f;
            _chargeDirection = Vector3.zero;
            _chargeReleased = false;
            _chargeHit = false;
            _bossSkillElapsed = 0f;
            _bossSkillResolved = false;
            _bossSkillCastDuration = BossSkillCastSeconds;
            _bossSkillRecoveryDuration = BossSkillRecoverySeconds;
            _activeBossTelegraph = null;
            _bossTelegraphView?.Hide();
        }

        private void MoveTowards(Vector3 destination, float deltaTime)
        {
            ResolveStatusEffectService();
            if (_statusEffects != null && _statusEffects.IsRooted(gameObject))
            {
                return;
            }

            _navigationRefreshRemaining = Mathf.Max(
                0f,
                _navigationRefreshRemaining - deltaTime);
            bool navigationAvailable;
            if (!TryGetNavigationDirection(
                    destination,
                    out Vector3 direction,
                    out navigationAvailable))
            {
                if (navigationAvailable)
                {
                    return;
                }

                direction = destination - transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            direction.Normalize();
            FaceDirection(direction, deltaTime);
            Vector3 displacement = direction
                * ResolveMoveSpeed()
                * deltaTime;
            if (_controller != null && _controller.enabled)
            {
                _controller.Move(displacement);
            }
            else
            {
                transform.position += displacement;
            }

            SyncAgentPosition();
        }

        private bool TryGetNavigationDirection(
            Vector3 destination,
            out Vector3 direction,
            out bool navigationAvailable)
        {
            direction = Vector3.zero;
            navigationAvailable = SampleNavigationPosition(
                transform.position,
                out Vector3 source);
            if (!navigationAvailable)
            {
                ClearNavigationPath();
                return false;
            }

            BindAgentToNavigation();
            bool destinationChanged = !_hasNavigationDestination
                || HorizontalDistance(destination, _navigationDestination) > 0.25f;
            if (_navigationCorners.Length == 0
                || destinationChanged
                || _navigationRefreshRemaining <= 0f)
            {
                if (!BuildNavigationPath(source, destination))
                {
                    return false;
                }
            }

            while (_navigationCornerIndex < _navigationCorners.Length
                && HorizontalDistance(
                    transform.position,
                    _navigationCorners[_navigationCornerIndex]) <= 0.18f)
            {
                _navigationCornerIndex++;
            }

            if (_navigationCornerIndex >= _navigationCorners.Length)
            {
                return false;
            }

            direction = _navigationCorners[_navigationCornerIndex]
                - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f;
        }

        private bool BuildNavigationPath(Vector3 source, Vector3 destination)
        {
            if (!SampleNavigationPosition(destination, out Vector3 sampledDestination)
                || !NavMesh.CalculatePath(
                    source,
                    sampledDestination,
                    NavMesh.AllAreas,
                    _navigationPath)
                || _navigationPath.status != NavMeshPathStatus.PathComplete)
            {
                ClearNavigationPath();
                return false;
            }

            _navigationCorners = _navigationPath.corners;
            if (_navigationCorners == null || _navigationCorners.Length == 0)
            {
                _navigationCorners = Array.Empty<Vector3>();
                return false;
            }

            _navigationCornerIndex = _navigationCorners.Length > 1 ? 1 : 0;
            _navigationDestination = destination;
            _hasNavigationDestination = true;
            _navigationRefreshRemaining = NavigationRefreshSeconds;
            if (IsOnNavMesh)
            {
                _agent.speed = ResolveMoveSpeed();
                _agent.stoppingDistance = State == EnemyBrainState.Chase
                    ? Mathf.Max(0.05f, Data.AttackRange * 0.8f)
                    : 0.05f;
                _agent.SetDestination(sampledDestination);
            }

            return true;
        }

        private void ClearNavigationPath()
        {
            _navigationCorners = Array.Empty<Vector3>();
            _navigationCornerIndex = 0;
            _navigationRefreshRemaining = 0f;
            _hasNavigationDestination = false;
            if (IsOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        private bool TryDetectPlayer()
        {
            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            if (player == null
                || player.IsDead
                || IsPositionInsideSafeZone(player.transform.position)
                || HorizontalDistance(transform.position, player.transform.position)
                    > Mathf.Max(0.1f, Data.AggroRange))
            {
                return false;
            }

            OnAggro(player.gameObject);
            return State == EnemyBrainState.Alert;
        }

        private static bool IsPositionInsideSafeZone(Vector3 position)
        {
            return ServiceLocator.TryGet<Wendao.Systems.World.ISafeZoneService>(
                    out Wendao.Systems.World.ISafeZoneService safeZones)
                && safeZones.IsPositionSafe(position);
        }

        private void AdvancePatrolPoint()
        {
            if (_patrolPoints.Count == 0)
            {
                EnterIdle();
                return;
            }

            _patrolPointIndex = (_patrolPointIndex + 1) % _patrolPoints.Count;
            _patrolWaitRemaining = 0f;
            ClearNavigationPath();
            SetAgentStopped(false);
        }

        private int ResolveInitialPatrolPointIndex()
        {
            if (_patrolPoints.Count <= 1)
            {
                return 0;
            }

            int nearestIndex = 0;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < _patrolPoints.Count; index++)
            {
                float distance = HorizontalDistance(
                    transform.position,
                    _patrolPoints[index]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            return (nearestIndex + 1) % _patrolPoints.Count;
        }

        private float ResolvePatrolWaitSeconds()
        {
            float seed = Mathf.Abs(
                SpawnPosition.x * 0.173f
                + SpawnPosition.z * 0.319f
                + _patrolPointIndex * 0.431f);
            return Mathf.Lerp(
                PatrolWaitMinSeconds,
                PatrolWaitMaxSeconds,
                Mathf.Repeat(seed, 1f));
        }

        private float ResolveMoveSpeed()
        {
            return Mathf.Max(0f, Data?.MoveSpeed ?? 0f)
                * (_statusEffects?.GetMoveSpeedMultiplier(gameObject) ?? 1f);
        }

        private void FaceTowards(Vector3 destination, float deltaTime)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            FaceDirection(direction, deltaTime);
        }

        private void FaceDirection(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Mathf.Clamp01(12f * deltaTime));
        }

        private void ResolveCombatService()
        {
            if (_combatService == null)
            {
                ServiceLocator.TryGet(out _combatService);
            }
        }

        private void ResolveStatusEffectService()
        {
            if (_statusEffects == null)
            {
                ServiceLocator.TryGet(out _statusEffects);
            }
        }

        private void ConfigureNavigationAgent()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_agent == null)
            {
                return;
            }

            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.autoTraverseOffMeshLink = false;
            bool boss = Data != null && Data.Rank == EnemyRank.Boss;
            bool elite = Data != null && Data.Rank == EnemyRank.Elite;
            _agent.radius = boss ? 0.9f : elite ? 0.5f : 0.42f;
            _agent.height = boss ? 3.2f : elite ? 1.5f : 1.2f;
            _agent.baseOffset = 0f;
            _agent.speed = Mathf.Max(0.1f, Data?.MoveSpeed ?? 3.2f);
            _agent.acceleration = 20f;
            _agent.angularSpeed = 720f;
            _agent.stoppingDistance = 0.05f;
        }

        private bool BindAgentToNavigation()
        {
            if (_agent == null || !_agent.enabled)
            {
                return false;
            }

            if (_agent.isOnNavMesh)
            {
                _agent.nextPosition = transform.position;
                return true;
            }

            if (!SampleNavigationPosition(transform.position, out Vector3 sampled))
            {
                return false;
            }

            if (HorizontalDistance(transform.position, sampled) > 0.01f)
            {
                Teleport(sampled);
            }

            bool warped = _agent.Warp(sampled);
            if (warped)
            {
                _agent.nextPosition = transform.position;
            }

            return warped;
        }

        private void SyncAgentPosition()
        {
            if (_agent == null || !_agent.enabled)
            {
                return;
            }

            if (!_agent.isOnNavMesh)
            {
                BindAgentToNavigation();
                return;
            }

            _agent.nextPosition = transform.position;
        }

        private void SetAgentStopped(bool stopped)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = stopped;
            }
        }

        private static bool SampleNavigationPosition(
            Vector3 position,
            out Vector3 sampled)
        {
            if (NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    NavigationSampleDistance,
                    NavMesh.AllAreas))
            {
                sampled = hit.position;
                return true;
            }

            sampled = position;
            return false;
        }

        private void EnsureLockOnAnchor()
        {
            _lockOnAnchor = transform.Find("LockOnAnchor");
            if (_lockOnAnchor != null)
            {
                return;
            }

            var anchor = new GameObject("LockOnAnchor");
            _lockOnAnchor = anchor.transform;
            _lockOnAnchor.SetParent(transform, false);
            _lockOnAnchor.localPosition = _controller != null
                ? _controller.center
                : new Vector3(0f, 0.6f, 0f);
        }

        private void EnsureAlertIndicator()
        {
            Transform existing = transform.Find("Visual_Alert_Exclamation");
            if (existing != null)
            {
                _alertIndicator = existing.gameObject;
                _alertIndicator.SetActive(false);
                return;
            }

            _alertIndicator = new GameObject("Visual_Alert_Exclamation");
            _alertIndicator.transform.SetParent(transform, false);
            _alertIndicator.transform.localPosition = new Vector3(0f, 1.75f, 0f);

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "AlertBar";
            bar.transform.SetParent(_alertIndicator.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            bar.transform.localScale = new Vector3(0.12f, 0.42f, 0.12f);
            ConfigureAlertPrimitive(bar);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "AlertDot";
            dot.transform.SetParent(_alertIndicator.transform, false);
            dot.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            dot.transform.localScale = Vector3.one * 0.14f;
            ConfigureAlertPrimitive(dot);
            _alertIndicator.SetActive(false);
        }

        private void SetAlertIndicator(bool visible)
        {
            if (_alertIndicator == null)
            {
                EnsureAlertIndicator();
            }

            if (_alertIndicator != null)
            {
                _alertIndicator.SetActive(visible);
            }
        }

        private static void ConfigureAlertPrimitive(GameObject primitive)
        {
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (renderer == null || shader == null)
            {
                return;
            }

            var material = new Material(shader)
            {
                name = "Enemy_Alert_Runtime",
                color = new Color(1f, 0.72f, 0.08f, 1f),
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = material;
        }

        private bool TryRegisterActor()
        {
            if (_registeredActor)
            {
                return true;
            }

            ResolveCombatService();
            if (_combatService == null)
            {
                return false;
            }

            _combatService.RegisterActor(this);
            _registeredActor = true;
            return true;
        }

        private void UnregisterActor()
        {
            if (!_registeredActor)
            {
                return;
            }

            _combatService?.UnregisterActor(this);
            _combatService = null;
            _registeredActor = false;
        }

        private void ClearTarget()
        {
            _targetObject = null;
            _targetDamageable = null;
        }

        private void Teleport(Vector3 position)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (wasEnabled)
            {
                _controller.enabled = false;
            }

            transform.position = position;
            if (wasEnabled)
            {
                _controller.enabled = true;
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.Warp(position);
                _agent.nextPosition = transform.position;
            }
        }

        private void CaptureColliderStates()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            _colliderAliveStates = new bool[_colliders.Length];
            for (int index = 0; index < _colliders.Length; index++)
            {
                _colliderAliveStates[index] = _colliders[index] != null
                    && _colliders[index].enabled;
            }
        }

        private void SetCollidersAlive(bool alive)
        {
            if (_colliders.Length == 0)
            {
                CaptureColliderStates();
            }

            for (int index = 0; index < _colliders.Length; index++)
            {
                if (_colliders[index] != null)
                {
                    _colliders[index].enabled = alive
                        && _colliderAliveStates[index];
                }
            }
        }

        private static IDamageable FindDamageable(GameObject target)
        {
            MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(
                true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private static GameObject ResolvePlayerRoot(GameObject source)
        {
            if (source == null)
            {
                return null;
            }

            PlayerStats player = source.GetComponentInParent<PlayerStats>();
            return player != null ? player.gameObject : null;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z)
                && !float.IsInfinity(value.z);
        }
    }
}
