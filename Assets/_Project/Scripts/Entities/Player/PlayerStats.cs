using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Inventory;
using Wendao.Systems.Player;
using Wendao.Systems.Skill;
using Wendao.Systems.World;

namespace Wendao.Entities.Player
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerStats : SafeBehaviour,
        IDamageable,
        ICombatStatsProvider,
        ICombatDefenseProvider,
        ICombatDamageReductionProvider,
        ICombatTeamProvider,
        ICombatDeathHandler,
        IPlayerHealthService,
        IPlayerResourceService,
        IPlayerRespawnService,
        ICultivationStatsProvider,
        IPlayerCharacterStatsService,
        IPlayerTitleStatsSink
    {
        public const float PhysicalBlockDamageReduction = 0.6f;
        public const float ElementalBlockDamageReduction = 0.3f;

        [Header("G-VS-02 Base Stats")]
        [SerializeField, Min(1f)] private float _maxHp = 100f;
        [SerializeField, Min(0f)] private float _maxMana = 50f;
        [SerializeField, Min(0f)] private float _attack = 10f;
        [SerializeField, Min(0f)] private float _defense = 5f;
        [SerializeField, Range(0f, 1f)] private float _critRate;
        [SerializeField, Min(1f)] private float _critDamage = 1.5f;

        private ICombatService _combatService;
        private IEquipmentService _equipmentService;
        private PlayerController _playerController;
        private bool _registeredActor;
        private bool _registeredHealthService;
        private bool _registeredResourceService;
        private bool _registeredRespawnService;
        private bool _registeredCultivationStats;
        private bool _registeredCharacterStats;
        private bool _deathHandled;
        private float _outOfCombatElapsed =
            FormulaLibrary.OutOfCombatRecoveryDelay;
        private RealmType _lastRealm;
        private int _lastSubStage;
        private bool _recalculating;
        private float _cachedBodyHpBonus = float.NaN;
        private float _titleMaxHpPercent;

        public StatBlock BaseFromRealm { get; private set; } = ZeroStats();
        public StatBlock FromEquipment { get; private set; } = ZeroStats();
        public StatBlock FromTitle { get; private set; } = ZeroStats();
        public StatBlock FromBuffs { get; private set; } = ZeroStats();
        public StatBlock Final { get; private set; } = ZeroStats();

        public float CurrentHp { get; private set; }
        public float MaxHp
        {
            get
            {
                EnsureBodyAggregationCurrent();
                return Mathf.Max(1f, Final?.MaxHp ?? 1f);
            }
        }
        public float CurrentMana { get; private set; }
        public float MaxMana => Mathf.Max(0f, Final?.MaxMana ?? 0f);
        public bool IsDead => CurrentHp <= 0f;
        public bool CanRespawn => IsDead && TryGetNearestRespawnPoint(out _);
        public string NearestRespawnPointId =>
            TryGetNearestRespawnPoint(out RespawnPoint point)
                ? point.Id
                : string.Empty;
        public float OutOfCombatElapsed => _outOfCombatElapsed;
        public float Attack => Mathf.Max(0f, Final?.Attack ?? 0f);
        public float Defense => Mathf.Max(0f, Final?.Defense ?? 0f);
        public float CritRate => Mathf.Clamp01(Final?.CritRate ?? 0f);
        public float CritDamage => Mathf.Max(1f, Final?.CritDamage ?? 1f);
        public float CultivationSpeed => Final?.CultivationSpeed ?? 0f;
        public float DivineSense => Mathf.Max(0f, Final?.DivineSense ?? 0f);
        public bool IsInvincible => (_playerController != null
                && _playerController.IsInvincible)
            || (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                && cultivation.IsBreakthroughInvincible);
        public bool IsBlocking => _playerController != null
            && _playerController.IsBlocking;
        public CombatTeam Team => CombatTeam.Player;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            Recalculate();
            RefreshRealmBaseStats(false);
            CurrentHp = MaxHp;
            CurrentMana = MaxMana;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);
            EventBus.Subscribe<EquipmentUpgradeInfo>(
                InventoryEvents.EquipmentUpgraded,
                HandleEquipmentUpgraded);
            EventBus.Subscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Subscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            TryRegisterHealthService();
            TryRegisterResourceService();
            TryRegisterRespawnService();
            TryRegisterCultivationStats();
            TryRegisterCharacterStats();
            TryRegisterActor();
        }

        private void Update()
        {
            if (!_registeredActor)
            {
                TryRegisterActor();
            }

            if (!_registeredHealthService)
            {
                TryRegisterHealthService();
            }

            if (!_registeredResourceService)
            {
                TryRegisterResourceService();
            }

            if (!_registeredRespawnService)
            {
                TryRegisterRespawnService();
            }

            if (!_registeredCultivationStats)
            {
                TryRegisterCultivationStats();
            }

            if (!_registeredCharacterStats)
            {
                TryRegisterCharacterStats();
            }

            RefreshRealmBaseStatsIfChanged();
            TickRecovery(Time.deltaTime);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EquipmentChangeInfo>(
                InventoryEvents.EquipmentChanged,
                HandleEquipmentChanged);
            EventBus.Unsubscribe<EquipmentUpgradeInfo>(
                InventoryEvents.EquipmentUpgraded,
                HandleEquipmentUpgraded);
            EventBus.Unsubscribe<XpGainInfo>(
                CultivationEvents.XpGained,
                HandleXpGained);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Unsubscribe<SkillCastInfo>(
                SkillEvents.SkillCast,
                HandleSkillCast);
            GameManager.Instance?.SetCombatFlag(false);
            UnregisterActor();
            UnregisterHealthService();
            UnregisterResourceService();
            UnregisterRespawnService();
            UnregisterCultivationStats();
            UnregisterCharacterStats();
        }

        protected override void SafeStart()
        {
            TryRegisterActor();
        }

        public void ConfigureBaseStats(
            float maxHp,
            float attack,
            float defense,
            float critRate = 0f,
            float critDamage = 1.5f,
            bool refillHp = true)
        {
            _maxHp = Mathf.Max(1f, maxHp);
            _attack = Mathf.Max(0f, attack);
            _defense = Mathf.Max(0f, defense);
            _critRate = Mathf.Clamp01(critRate);
            _critDamage = Mathf.Max(1f, critDamage);
            Recalculate();
            CurrentHp = refillHp
                ? MaxHp
                : Mathf.Clamp(CurrentHp, 0f, MaxHp);
            _deathHandled = CurrentHp <= 0f;
        }

        public void SetHp(float value)
        {
            CurrentHp = Mathf.Clamp(value, 0f, MaxHp);
            _deathHandled = CurrentHp <= 0f;
        }

        public void ConfigureMana(float maxMana, bool refillMana = true)
        {
            _maxMana = Mathf.Max(0f, maxMana);
            Recalculate();
            CurrentMana = refillMana
                ? MaxMana
                : Mathf.Clamp(CurrentMana, 0f, MaxMana);
        }

        public void SetMana(float value)
        {
            CurrentMana = Mathf.Clamp(value, 0f, MaxMana);
        }

        public float ApplyManaDelta(float delta)
        {
            float previous = CurrentMana;
            SetMana(CurrentMana + delta);
            return CurrentMana - previous;
        }

        public void ApplyHpDelta(float delta)
        {
            if (delta >= 0f)
            {
                ApplyHeal(delta, string.Empty);
                return;
            }

            SetHp(CurrentHp + delta);
        }

        public void SetTitleBonus(StatBlock bonus)
        {
            ApplyTitleBonus(bonus, 0f);
        }

        public void ApplyTitleBonus(StatBlock bonus, float maxHpPercent)
        {
            FromTitle = CopyStats(bonus);
            _titleMaxHpPercent = Mathf.Max(0f, maxHpPercent);
            Recalculate();
        }

        public void SetBuffBonus(StatBlock bonus)
        {
            FromBuffs = CopyStats(bonus);
            Recalculate();
        }

        public void Recalculate()
        {
            if (_recalculating)
            {
                return;
            }

            _recalculating = true;
            try
            {
                BaseFromRealm = new StatBlock
                {
                    MaxHp = Mathf.Max(1f, _maxHp),
                    MaxMana = Mathf.Max(0f, _maxMana),
                    Attack = Mathf.Max(0f, _attack),
                    Defense = Mathf.Max(0f, _defense),
                    CritRate = Mathf.Clamp01(_critRate),
                    CritDamage = Mathf.Max(1f, _critDamage)
                };

                ResolveEquipmentService();
                FromEquipment = CopyStats(
                    _equipmentService?.GetEquipmentStats());
                FromTitle = FromTitle ?? ZeroStats();
                FromBuffs = FromBuffs ?? ZeroStats();

                StatBlock fixedStats = BaseFromRealm
                    + FromEquipment
                    + FromTitle
                    + BuildSpiritRootStats();
                float bodyHpBonus = ResolveBodyHpBonus();
                fixedStats.MaxHp *= (1f + bodyHpBonus)
                    * (1f + _titleMaxHpPercent);

                StatBlock result = fixedStats + FromBuffs;
                result.MaxHp = Mathf.Max(1f, result.MaxHp);
                result.MaxMana = Mathf.Max(0f, result.MaxMana);
                result.Attack = Mathf.Max(0f, result.Attack);
                result.Defense = Mathf.Max(0f, result.Defense);
                result.CritRate = Mathf.Clamp01(result.CritRate);
                result.CritDamage = Mathf.Max(1f, result.CritDamage);
                result.DivineSense = Mathf.Max(0f, result.DivineSense);
                Final = result;
                _cachedBodyHpBonus = bodyHpBonus;

                CurrentHp = Mathf.Clamp(CurrentHp, 0f, Final.MaxHp);
                CurrentMana = Mathf.Clamp(CurrentMana, 0f, Final.MaxMana);
            }
            finally
            {
                _recalculating = false;
            }
        }

        public bool TrySpendMana(float amount)
        {
            if (amount < 0f || CurrentMana + 0.0001f < amount)
            {
                return false;
            }

            CurrentMana = Mathf.Max(0f, CurrentMana - amount);
            return true;
        }

        public void ApplyDamage(DamageInfo info)
        {
            bool wasBlocking = IsBlocking && info.Type != DamageType.True;
            if (IsDead
                || IsInvincible
                || info.Amount <= 0f)
            {
                return;
            }

            float previousHp = CurrentHp;
            CurrentHp = Mathf.Max(0f, CurrentHp - info.Amount);
            info.Amount = previousHp - CurrentHp;
            RegisterCombatActivity();
            if (wasBlocking)
            {
                _playerController.NotifyBlockHit();
            }

            info.Target = gameObject;
            info.IsKillingBlow = IsDead;
            EventBus.Publish(CombatEvents.PlayerDamaged, info);
        }

        public float GetBlockDamageReduction(DamageType damageType)
        {
            if (!IsBlocking || damageType == DamageType.True)
            {
                return 0f;
            }

            if (damageType != DamageType.Physical)
            {
                return ElementalBlockDamageReduction;
            }

            float rootBonus = ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService spiritRoot)
                ? spiritRoot.GetBlockPhysDrBonus()
                : 0f;
            return Mathf.Min(
                0.85f,
                PhysicalBlockDamageReduction + Mathf.Max(0f, rootBonus));
        }

        public float GetDamageReduction(DamageType damageType)
        {
            if (damageType != DamageType.Physical
                || !ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService body))
            {
                return 0f;
            }

            return Mathf.Clamp01(body.PhysicalDR);
        }

        public void HandleDeath(DamageInfo killingBlow)
        {
            if (!IsDead || _deathHandled)
            {
                return;
            }

            _deathHandled = true;
            _playerController.SetInputEnabled(false);
            _playerController.ForceState(PlayerState.Dead);
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.SetCombatFlag(false);
                if (gameManager.State == GameState.Playing)
                {
                    gameManager.TrySetState(GameState.Dead);
                }
            }

            _outOfCombatElapsed = FormulaLibrary.OutOfCombatRecoveryDelay;
            if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
            {
                cultivation.ApplyDeathXpPenalty(
                    FormulaLibrary.DeathXpPenaltyPercent);
            }

            EventBus.Publish(
                CombatEvents.PlayerDied,
                new DeathInfo
                {
                    Victim = gameObject,
                    Killer = killingBlow.Source,
                    Position = transform.position,
                    LastHitSkillId = killingBlow.SkillId ?? string.Empty
                });
        }

        public bool TryRespawnAtNearestPoint()
        {
            if (!IsDead || !TryGetNearestRespawnPoint(out RespawnPoint point))
            {
                return false;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Dead)
            {
                return false;
            }

            _playerController.TeleportTo(
                point.transform.position,
                point.transform.rotation);
            CurrentHp = MaxHp;
            CurrentMana = MaxMana;
            _deathHandled = false;
            _outOfCombatElapsed = FormulaLibrary.OutOfCombatRecoveryDelay;
            _playerController.ForceState(PlayerState.Idle);
            _playerController.SetInputEnabled(true);

            if (gameManager != null)
            {
                gameManager.SetCombatFlag(false);
                if (!gameManager.TrySetState(GameState.Playing))
                {
                    return false;
                }
            }

            EventBus.Publish(
                PlayerEvents.Respawned,
                new PlayerRespawnInfo
                {
                    Player = gameObject,
                    RespawnPointId = point.Id,
                    Position = point.transform.position
                });
            return true;
        }

        public void TickRecovery(float deltaTime)
        {
            if (deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime)
                || IsDead)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                return;
            }

            float delay = FormulaLibrary.OutOfCombatRecoveryDelay;
            float previousElapsed = _outOfCombatElapsed;
            float elapsedAfterTick = _outOfCombatElapsed + deltaTime;
            _outOfCombatElapsed = Mathf.Min(delay, elapsedAfterTick);
            if (gameManager.IsInCombat && _outOfCombatElapsed >= delay)
            {
                gameManager.SetCombatFlag(false);
            }

            if (gameManager.IsInCombat)
            {
                return;
            }

            float recoveryDuration = previousElapsed >= delay
                ? deltaTime
                : Mathf.Max(0f, elapsedAfterTick - delay);
            if (recoveryDuration <= 0f)
            {
                return;
            }

            float recoveryMultiplier =
                ServiceLocator.TryGet<ISafeZoneService>(
                    out ISafeZoneService safeZones)
                    ? safeZones.GetRecoveryMultiplier(transform.position)
                    : 1f;
            recoveryDuration *= Mathf.Max(1f, recoveryMultiplier);

            ApplyHeal(
                MaxHp
                    * FormulaLibrary.OutOfCombatHpRecoveryPerSecond
                    * recoveryDuration,
                PlayerRecoveryContentIds.OutOfCombatRegeneration);
            ApplyManaDelta(
                MaxMana
                    * FormulaLibrary.OutOfCombatManaRecoveryPerSecond
                    * recoveryDuration);
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            float previousHp = CurrentHp;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
            float applied = CurrentHp - previousHp;
            if (applied <= 0f)
            {
                return;
            }

            EventBus.Publish(
                CombatEvents.PlayerHealed,
                new HealInfo
                {
                    Target = gameObject,
                    Amount = applied,
                    SourceId = sourceId ?? string.Empty
                });
        }

        private bool TryRegisterActor()
        {
            if (_registeredActor)
            {
                return true;
            }

            if (!ServiceLocator.TryGet(out _combatService))
            {
                return false;
            }

            _combatService.RegisterActor(this);
            _registeredActor = true;
            return true;
        }

        private bool TryRegisterHealthService()
        {
            if (_registeredHealthService)
            {
                return true;
            }

            if (ServiceLocator.TryGet<IPlayerHealthService>(
                    out IPlayerHealthService existing))
            {
                _registeredHealthService = ReferenceEquals(existing, this);
                return _registeredHealthService;
            }

            ServiceLocator.Register<IPlayerHealthService>(this);
            _registeredHealthService = true;
            return true;
        }

        private void UnregisterHealthService()
        {
            if (_registeredHealthService
                && ServiceLocator.TryGet<IPlayerHealthService>(
                    out IPlayerHealthService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerHealthService>();
            }

            _registeredHealthService = false;
        }

        private bool TryRegisterResourceService()
        {
            if (_registeredResourceService)
            {
                return true;
            }

            if (ServiceLocator.TryGet<IPlayerResourceService>(
                    out IPlayerResourceService existing))
            {
                _registeredResourceService = ReferenceEquals(existing, this);
                return _registeredResourceService;
            }

            ServiceLocator.Register<IPlayerResourceService>(this);
            _registeredResourceService = true;
            return true;
        }

        private void UnregisterResourceService()
        {
            if (_registeredResourceService
                && ServiceLocator.TryGet<IPlayerResourceService>(
                    out IPlayerResourceService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerResourceService>();
            }

            _registeredResourceService = false;
        }

        private bool TryRegisterRespawnService()
        {
            if (_registeredRespawnService)
            {
                return true;
            }

            if (ServiceLocator.TryGet<IPlayerRespawnService>(
                    out IPlayerRespawnService existing))
            {
                _registeredRespawnService = ReferenceEquals(existing, this);
                return _registeredRespawnService;
            }

            ServiceLocator.Register<IPlayerRespawnService>(this);
            _registeredRespawnService = true;
            return true;
        }

        private void UnregisterRespawnService()
        {
            if (_registeredRespawnService
                && ServiceLocator.TryGet<IPlayerRespawnService>(
                    out IPlayerRespawnService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerRespawnService>();
            }

            _registeredRespawnService = false;
        }

        private bool TryRegisterCultivationStats()
        {
            if (_registeredCultivationStats)
            {
                return true;
            }

            if (ServiceLocator.TryGet<ICultivationStatsProvider>(
                    out ICultivationStatsProvider existing))
            {
                _registeredCultivationStats = ReferenceEquals(existing, this);
                return _registeredCultivationStats;
            }

            ServiceLocator.Register<ICultivationStatsProvider>(this);
            _registeredCultivationStats = true;
            return true;
        }

        private void UnregisterCultivationStats()
        {
            if (_registeredCultivationStats
                && ServiceLocator.TryGet<ICultivationStatsProvider>(
                    out ICultivationStatsProvider current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ICultivationStatsProvider>();
            }

            _registeredCultivationStats = false;
        }

        private bool TryRegisterCharacterStats()
        {
            if (_registeredCharacterStats)
            {
                return true;
            }

            if (ServiceLocator.TryGet<IPlayerCharacterStatsService>(
                    out IPlayerCharacterStatsService existing))
            {
                _registeredCharacterStats = ReferenceEquals(existing, this);
                return _registeredCharacterStats;
            }

            ServiceLocator.Register<IPlayerCharacterStatsService>(this);
            _registeredCharacterStats = true;
            return true;
        }

        private void UnregisterCharacterStats()
        {
            if (_registeredCharacterStats
                && ServiceLocator.TryGet<IPlayerCharacterStatsService>(
                    out IPlayerCharacterStatsService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerCharacterStatsService>();
            }

            _registeredCharacterStats = false;
        }

        private void HandleEquipmentChanged(EquipmentChangeInfo info)
        {
            Recalculate();
            CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp);
            CurrentMana = Mathf.Clamp(CurrentMana, 0f, MaxMana);
        }

        private void HandleEquipmentUpgraded(EquipmentUpgradeInfo info)
        {
            if (!info.Success)
            {
                return;
            }

            Recalculate();
            CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp);
            CurrentMana = Mathf.Clamp(CurrentMana, 0f, MaxMana);
        }

        private void HandleXpGained(XpGainInfo info)
        {
            RefreshRealmBaseStatsIfChanged();
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (info.Success)
            {
                RefreshRealmBaseStats(false);
            }
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (info.Amount > 0f
                && (IsPlayerActor(info.Source) || IsPlayerActor(info.Target)))
            {
                RegisterCombatActivity();
            }
        }

        private bool IsPlayerActor(GameObject actor)
        {
            return actor != null
                && (actor.transform == transform
                    || actor.transform.IsChildOf(transform)
                    || transform.IsChildOf(actor.transform));
        }

        private void HandleSkillCast(SkillCastInfo info)
        {
            RegisterCombatActivity();
        }

        private void RegisterCombatActivity()
        {
            _outOfCombatElapsed = 0f;
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                gameManager.SetCombatFlag(true);
            }
        }

        private bool TryGetNearestRespawnPoint(out RespawnPoint point)
        {
            return RespawnPoint.TryFindNearest(
                transform.position,
                gameObject.scene,
                out point);
        }

        private void RefreshRealmBaseStatsIfChanged()
        {
            if (!ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                || (cultivation.Realm == _lastRealm
                    && cultivation.SubStage == _lastSubStage))
            {
                return;
            }

            RefreshRealmBaseStats(false);
        }

        private void RefreshRealmBaseStats(bool refillResources)
        {
            if (!ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
            {
                return;
            }

            RealmEntry realm = ConfigDatabase.Instance?.GetRealm(
                (int)cultivation.Realm);
            int stageIndex = Mathf.Clamp(
                cultivation.SubStage - 1,
                0,
                Mathf.Max(0, (realm?.SubStages ?? 1) - 1));
            RealmBaseStats stats = realm?.BaseStatsPerSubStage;
            if (realm == null
                || stats == null
                || stats.MaxHp == null
                || stats.MaxMana == null
                || stats.Attack == null
                || stats.Defense == null
                || stageIndex >= stats.MaxHp.Length
                || stageIndex >= stats.MaxMana.Length
                || stageIndex >= stats.Attack.Length
                || stageIndex >= stats.Defense.Length)
            {
                return;
            }

            float oldMaxHp = MaxHp;
            float oldMaxMana = MaxMana;
            bool wasDead = CurrentHp <= 0f;
            _maxHp = Mathf.Max(1f, stats.MaxHp[stageIndex]);
            _maxMana = Mathf.Max(0f, stats.MaxMana[stageIndex]);
            _attack = Mathf.Max(0f, stats.Attack[stageIndex]);
            _defense = Mathf.Max(0f, stats.Defense[stageIndex]);
            _lastRealm = cultivation.Realm;
            _lastSubStage = cultivation.SubStage;
            Recalculate();

            if (refillResources)
            {
                CurrentHp = MaxHp;
                CurrentMana = MaxMana;
                return;
            }

            CurrentHp = wasDead
                ? 0f
                : Mathf.Clamp(
                    CurrentHp + Mathf.Max(0f, MaxHp - oldMaxHp),
                    0f,
                    MaxHp);
            CurrentMana = Mathf.Clamp(
                CurrentMana + Mathf.Max(0f, MaxMana - oldMaxMana),
                0f,
                MaxMana);
        }

        private void ResolveEquipmentService()
        {
            bool isAlive = _equipmentService != null
                && (!(_equipmentService is UnityEngine.Object unityObject)
                    || unityObject != null);
            if (!isAlive)
            {
                _equipmentService = null;
                ServiceLocator.TryGet(out _equipmentService);
            }
        }

        private void EnsureBodyAggregationCurrent()
        {
            if (_recalculating)
            {
                return;
            }

            float bodyHpBonus = ResolveBodyHpBonus();
            if (Mathf.Approximately(bodyHpBonus, _cachedBodyHpBonus))
            {
                return;
            }

            float oldMaxHp = Mathf.Max(1f, Final?.MaxHp ?? 1f);
            bool wasDead = CurrentHp <= 0f;
            Recalculate();
            if (!wasDead)
            {
                CurrentHp = Mathf.Clamp(
                    CurrentHp + Mathf.Max(0f, Final.MaxHp - oldMaxHp),
                    0f,
                    Final.MaxHp);
            }
        }

        private static float ResolveBodyHpBonus()
        {
            return ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService body)
                ? Mathf.Max(0f, body.HpBonus)
                : 0f;
        }

        private static StatBlock BuildSpiritRootStats()
        {
            var stats = ZeroStats();
            if (!ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService spiritRoot))
            {
                return stats;
            }

            stats.FireBonus = spiritRoot.GetElementBonus(ElementType.Fire);
            stats.IceBonus = spiritRoot.GetElementBonus(ElementType.Ice);
            stats.LightningBonus = spiritRoot.GetElementBonus(
                ElementType.Lightning);
            stats.PoisonBonus = spiritRoot.GetElementBonus(ElementType.Poison);
            stats.WindBonus = spiritRoot.GetElementBonus(ElementType.Wind);
            return stats;
        }

        private static StatBlock CopyStats(StatBlock source)
        {
            if (source == null)
            {
                return ZeroStats();
            }

            return new StatBlock
            {
                MaxHp = source.MaxHp,
                MaxMana = source.MaxMana,
                Attack = source.Attack,
                Defense = source.Defense,
                CritRate = source.CritRate,
                CritDamage = source.CritDamage,
                MoveSpeed = source.MoveSpeed,
                AttackSpeed = source.AttackSpeed,
                FireBonus = source.FireBonus,
                IceBonus = source.IceBonus,
                LightningBonus = source.LightningBonus,
                PoisonBonus = source.PoisonBonus,
                WindBonus = source.WindBonus,
                CultivationSpeed = source.CultivationSpeed,
                DivineSense = source.DivineSense
            };
        }

        private static StatBlock ZeroStats()
        {
            return new StatBlock { CritDamage = 0f };
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
    }
}
