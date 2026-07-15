using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Skill
{
    public sealed class SkillManager : SafeBehaviour, ISkillService,
        ISkillAnimationEventService
    {
        public const int BarSlotCount = 4;
        public const string SaveModuleName = "skills";
        public const string ManaInsufficientToastKey = "ui_skill_mana_insufficient";
        public const string ManaInsufficientToastDefault =
            "灵力不足，无法施展功法。";
        public const string UpgradeMaterialMissingToastKey =
            "ui_skill_upgrade_material_missing";
        public const string UpgradeMaterialMissingToastDefault =
            "功法残页不足，需要 {0} 页。";
        public const string UpgradeMaxLevelToastKey = "ui_skill_upgrade_max";
        public const string UpgradeMaxLevelToastDefault = "此功法已修至当前上限。";
        public const string UpgradeSuccessToastKey = "ui_skill_upgrade_success";
        public const string UpgradeSuccessToastDefault = "{0}提升至第 {1} 重。";

        private readonly List<SkillRuntime> _learned = new List<SkillRuntime>();
        private readonly string[] _equippedIds = new string[BarSlotCount];

        private ReadOnlyCollection<SkillRuntime> _readOnlyLearned;
        private IPlayerResourceService _resourceService;
        private IPlayerSkillCaster _caster;
        private SaveManager _registeredSaveManager;
        private ActiveCast _activeCast;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyList<SkillRuntime> Learned => _readOnlyLearned;
        public string[] EquippedIds => _equippedIds;
        public bool IsCasting => _activeCast != null;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ISkillService>(out ISkillService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyLearned = _learned.AsReadOnly();
            ResetStarterLoadout();
            ServiceLocator.Register<ISkillService>(this);
            _registeredService = true;
            TryRegisterSaveModule();

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
            ResolvePlayerServices();

            if (!IsGameplayRunning())
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            TickCooldowns(deltaTime);
            TickActiveCast(deltaTime);
        }

        private void OnDestroy()
        {
            CancelActiveCast();

            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<ISkillService>(out ISkillService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ISkillService>();
            }

            _registeredService = false;
        }

        public bool Learn(string skillId)
        {
            SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
            if (skill == null
                || FindRuntime(skillId) != null
                || !MeetsRealmRequirement(skill.RequiredRealm))
            {
                return false;
            }

            var runtime = new SkillRuntime
            {
                SkillId = skill.Id,
                Level = 1,
                Exp = 0f,
                CooldownRemaining = 0f
            };
            _learned.Add(runtime);
            EventBus.Publish(
                SkillEvents.SkillLearned,
                new SkillInfo
                {
                    SkillId = runtime.SkillId,
                    Level = runtime.Level
                });
            PersistChanges(false);
            return true;
        }

        public bool Equip(string skillId, int barIndex)
        {
            SkillRuntime runtime = FindRuntime(skillId);
            SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
            if (!IsValidBarIndex(barIndex)
                || runtime == null
                || skill == null
                || skill.Type == SkillType.Passive)
            {
                return false;
            }

            for (int index = 0; index < _equippedIds.Length; index++)
            {
                if (index != barIndex
                    && string.Equals(
                        _equippedIds[index],
                        skillId,
                        StringComparison.Ordinal))
                {
                    _equippedIds[index] = string.Empty;
                }
            }

            _equippedIds[barIndex] = skillId;
            PersistChanges(false);
            return true;
        }

        public bool Unequip(int barIndex)
        {
            if (!IsValidBarIndex(barIndex)
                || string.IsNullOrEmpty(_equippedIds[barIndex]))
            {
                return false;
            }

            _equippedIds[barIndex] = string.Empty;
            PersistChanges(false);
            return true;
        }

        public bool CanCast(int barIndex)
        {
            return EvaluateCast(barIndex) == CastFailure.None;
        }

        public bool TryCast(
            int barIndex,
            Vector3 targetPoint,
            GameObject targetActor)
        {
            CastFailure failure = EvaluateCast(barIndex);
            if (failure != CastFailure.None)
            {
                if (failure == CastFailure.InsufficientMana)
                {
                    PublishManaInsufficientToast();
                }

                return false;
            }

            string skillId = _equippedIds[barIndex];
            SkillData skill = ConfigDatabase.Instance.GetSkill(skillId);
            SkillRuntime runtime = FindRuntime(skillId);
            if (!_caster.BeginSkillCast())
            {
                return false;
            }

            Vector3 origin = _caster.CastOrigin;
            Vector3 fallbackDirection = _caster.Forward;
            if ((targetPoint - origin).sqrMagnitude <= 0.0001f)
            {
                targetPoint = origin + fallbackDirection * Mathf.Max(1f, skill.Range);
            }

            _activeCast = new ActiveCast
            {
                Skill = skill,
                Runtime = runtime,
                Caster = _caster,
                Origin = origin,
                TargetPoint = targetPoint,
                TargetActor = targetActor,
                PhaseElapsed = 0f,
                Released = false
            };

            if (skill.CastTime <= 0f)
            {
                TickActiveCast(0f);
            }

            return true;
        }

        public void TickCooldowns(float deltaTime)
        {
            float clampedDelta = Mathf.Max(0f, deltaTime);
            for (int index = 0; index < _learned.Count; index++)
            {
                SkillRuntime runtime = _learned[index];
                runtime.CooldownRemaining = Mathf.Max(
                    0f,
                    runtime.CooldownRemaining - clampedDelta);
            }
        }

        public bool TryReleaseAtAnimationEvent(GameObject casterActor)
        {
            ActiveCast cast = _activeCast;
            if (cast == null
                || cast.Released
                || casterActor == null
                || IsMissingUnityService(cast.Caster)
                || cast.Caster.Actor != casterActor)
            {
                return false;
            }

            ResolvePlayerServices();
            if (IsMissingUnityService(_resourceService)
                || cast.Caster.IsDead
                || cast.Caster.IsSilenced)
            {
                CancelActiveCast();
                return false;
            }

            cast.PhaseElapsed = 0f;
            return TryReleaseActiveCast(cast);
        }

        public bool TryUpgrade(string skillId)
        {
            SkillRuntime runtime = FindRuntime(skillId);
            SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
            if (runtime == null || skill == null)
            {
                return false;
            }

            int maxLevel = Mathf.Max(1, skill.MaxLevel);
            if (runtime.Level >= maxLevel)
            {
                PublishToast(
                    UpgradeMaxLevelToastKey,
                    UpgradeMaxLevelToastDefault);
                return false;
            }

            int cost = Mathf.Max(1, runtime.Level);
            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || inventory.CountItem(InventoryContentIds.SkillScroll) < cost
                || !inventory.RemoveItem(InventoryContentIds.SkillScroll, cost))
            {
                PublishToast(
                    UpgradeMaterialMissingToastKey,
                    string.Format(UpgradeMaterialMissingToastDefault, cost));
                return false;
            }

            runtime.Level++;
            PersistChanges(true);
            EventBus.Publish(
                SkillEvents.SkillUpgraded,
                new SkillUpgradeInfo
                {
                    SkillId = runtime.SkillId,
                    NewLevel = runtime.Level
                });
            PublishToast(
                UpgradeSuccessToastKey,
                string.Format(
                    UpgradeSuccessToastDefault,
                    skill.DisplayName,
                    runtime.Level));
            return true;
        }

        public float GetCooldownRemaining(int barIndex)
        {
            if (!IsValidBarIndex(barIndex))
            {
                return 0f;
            }

            return FindRuntime(_equippedIds[barIndex])?.CooldownRemaining ?? 0f;
        }

        public SkillSaveData CaptureSaveData()
        {
            var learned = new List<SkillRuntime>(_learned.Count);
            for (int index = 0; index < _learned.Count; index++)
            {
                learned.Add(CloneRuntime(_learned[index]));
            }

            return new SkillSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Learned = learned,
                EquippedIds = (string[])_equippedIds.Clone()
            };
        }

        public void RestoreSaveData(SkillSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Learned == null
                || data.EquippedIds == null
                || data.EquippedIds.Length != BarSlotCount)
            {
                throw new InvalidDataException("Skill save data is invalid.");
            }

            var restored = new List<SkillRuntime>(data.Learned.Count);
            var learnedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.Learned.Count; index++)
            {
                SkillRuntime source = data.Learned[index];
                SkillData skill = source == null
                    ? null
                    : ConfigDatabase.Instance?.GetSkill(source.SkillId);
                if (source == null
                    || skill == null
                    || !learnedIds.Add(source.SkillId)
                    || source.Level < 1
                    || source.Level > Mathf.Max(1, skill.MaxLevel)
                    || source.Exp < 0f
                    || source.CooldownRemaining < 0f
                    || source.CooldownRemaining > skill.BaseCooldown + 0.001f)
                {
                    throw new InvalidDataException(
                        "Skill save contains an invalid learned skill.");
                }

                restored.Add(CloneRuntime(source));
            }

            var equippedIds = new string[BarSlotCount];
            var equippedSet = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.EquippedIds.Length; index++)
            {
                string skillId = data.EquippedIds[index] ?? string.Empty;
                if (string.IsNullOrEmpty(skillId))
                {
                    equippedIds[index] = string.Empty;
                    continue;
                }

                SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
                if (!learnedIds.Contains(skillId)
                    || skill == null
                    || skill.Type == SkillType.Passive
                    || !equippedSet.Add(skillId))
                {
                    throw new InvalidDataException(
                        "Skill save contains an invalid equipped skill.");
                }

                equippedIds[index] = skillId;
            }

            CancelActiveCast();
            _learned.Clear();
            _learned.AddRange(restored);
            Array.Copy(equippedIds, _equippedIds, BarSlotCount);
        }

        private void TickActiveCast(float deltaTime)
        {
            if (_activeCast == null)
            {
                return;
            }

            ActiveCast cast = _activeCast;
            ResolvePlayerServices();
            if (IsMissingUnityService(cast.Caster)
                || cast.Caster.Actor == null
                || cast.Caster.IsDead
                || cast.Caster.IsSilenced
                || IsMissingUnityService(_resourceService))
            {
                CancelActiveCast();
                return;
            }

            cast.PhaseElapsed += Mathf.Max(0f, deltaTime);
            if (!cast.Released
                && cast.PhaseElapsed + 0.0001f >= Mathf.Max(0f, cast.Skill.CastTime))
            {
                cast.PhaseElapsed = Mathf.Max(
                    0f,
                    cast.PhaseElapsed - Mathf.Max(0f, cast.Skill.CastTime));
                if (!TryReleaseActiveCast(cast))
                {
                    return;
                }
            }

            if (cast.Released
                && cast.PhaseElapsed + 0.0001f
                    >= Mathf.Max(0f, cast.Skill.RecoveryTime))
            {
                FinishActiveCast();
            }
        }

        private void ReleaseSkill(ActiveCast cast)
        {
            Vector3 targetPoint = cast.TargetPoint;
            if (cast.TargetActor != null)
            {
                Collider targetCollider = cast.TargetActor.GetComponentInChildren<Collider>();
                targetPoint = targetCollider != null
                    ? targetCollider.bounds.center
                    : cast.TargetActor.transform.position + Vector3.up * 0.9f;
            }

            cast.Runtime.CooldownRemaining = Mathf.Max(0f, cast.Skill.BaseCooldown);
            float damage = Mathf.Max(
                0f,
                cast.Skill.BaseDamage
                    + cast.Skill.DamagePerLevel * Mathf.Max(0, cast.Runtime.Level - 1));
            var request = new DamageRequest
            {
                Source = cast.Caster.Actor,
                BaseDamage = damage,
                Type = ResolveDamageType(cast.Skill.Element),
                Element = ResolveElementType(cast.Skill.Element),
                Multiplier = 1f,
                CanCrit = true,
                SkillId = cast.Skill.Id,
                StatusOnHitId = cast.Skill.StatusOnHitId ?? string.Empty,
                StatusChance = cast.Skill.StatusChance
            };

            if (cast.Skill.IsProjectile)
            {
                SkillProjectile.Spawn(
                    cast.Skill.Id,
                    cast.Origin,
                    targetPoint,
                    Mathf.Max(1f, cast.Skill.Range),
                    request);
            }
            else if (TryFindDamageable(cast.TargetActor, out IDamageable damageable)
                && ServiceLocator.TryGet<ICombatService>(out ICombatService combat))
            {
                combat.DealDamage(damageable, request);
            }

            EventBus.Publish(
                SkillEvents.SkillCast,
                new SkillCastInfo
                {
                    SkillId = cast.Skill.Id,
                    Origin = cast.Origin,
                    TargetPoint = targetPoint,
                    TargetActor = cast.TargetActor
                });
        }

        private bool TryReleaseActiveCast(ActiveCast cast)
        {
            if (!_resourceService.TrySpendMana(
                    Mathf.Max(0f, cast.Skill.BaseManaCost)))
            {
                PublishManaInsufficientToast();
                CancelActiveCast();
                return false;
            }

            ReleaseSkill(cast);
            cast.Released = true;
            return true;
        }

        private CastFailure EvaluateCast(int barIndex)
        {
            if (!IsValidBarIndex(barIndex))
            {
                return CastFailure.InvalidSlot;
            }

            if (_activeCast != null)
            {
                return CastFailure.Busy;
            }

            string skillId = _equippedIds[barIndex];
            SkillRuntime runtime = FindRuntime(skillId);
            SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
            if (string.IsNullOrEmpty(skillId)
                || runtime == null
                || skill == null
                || skill.Type == SkillType.Passive)
            {
                return CastFailure.MissingSkill;
            }

            if (runtime.CooldownRemaining > 0.0001f)
            {
                return CastFailure.Cooldown;
            }

            if (!IsGameplayRunning())
            {
                return CastFailure.GameplayBlocked;
            }

            ResolvePlayerServices();
            if (IsMissingUnityService(_caster)
                || _caster.Actor == null
                || _caster.IsDead
                || _caster.IsSilenced
                || !_caster.CanBeginSkillCast)
            {
                return CastFailure.CasterBlocked;
            }

            if (IsMissingUnityService(_resourceService)
                || _resourceService.CurrentMana + 0.0001f
                    < Mathf.Max(0f, skill.BaseManaCost))
            {
                return CastFailure.InsufficientMana;
            }

            return CastFailure.None;
        }

        private void ResolvePlayerServices()
        {
            ServiceLocator.TryGet(out _resourceService);
            ServiceLocator.TryGet(out _caster);
        }

        private void FinishActiveCast()
        {
            ActiveCast cast = _activeCast;
            _activeCast = null;
            if (!IsMissingUnityService(cast?.Caster))
            {
                cast.Caster.EndSkillCast();
            }
        }

        private void CancelActiveCast()
        {
            ActiveCast cast = _activeCast;
            _activeCast = null;
            if (!IsMissingUnityService(cast?.Caster)
                && cast.Caster.Actor != null)
            {
                cast.Caster.EndSkillCast();
            }
        }

        private void ResetStarterLoadout()
        {
            CancelActiveCast();
            _learned.Clear();
            Array.Clear(_equippedIds, 0, _equippedIds.Length);
            _learned.Add(
                new SkillRuntime
                {
                    SkillId = SkillContentIds.BasicQiBolt,
                    Level = 1,
                    Exp = 0f,
                    CooldownRemaining = 0f
                });
            _equippedIds[0] = SkillContentIds.BasicQiBolt;
        }

        private bool TryRegisterSaveModule()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                return false;
            }

            _registeredSaveModule = saveManager.RegisterModule(
                SaveModuleName,
                CaptureSaveData,
                RestoreSaveData,
                ResetStarterLoadout);
            if (_registeredSaveModule)
            {
                _registeredSaveManager = saveManager;
            }

            return _registeredSaveModule;
        }

        private void RepairSaveRegistration()
        {
            SaveManager current = SaveManager.Instance;
            if (_registeredSaveManager == current && _registeredSaveModule)
            {
                return;
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            TryRegisterSaveModule();
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<ISkillService>(out ISkillService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<ISkillService>(this);
            _registeredService = true;
        }

        private SkillRuntime FindRuntime(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            for (int index = 0; index < _learned.Count; index++)
            {
                if (string.Equals(
                        _learned[index].SkillId,
                        skillId,
                        StringComparison.Ordinal))
                {
                    return _learned[index];
                }
            }

            return null;
        }

        private static SkillRuntime CloneRuntime(SkillRuntime source)
        {
            return new SkillRuntime
            {
                SkillId = source.SkillId ?? string.Empty,
                Level = source.Level,
                Exp = source.Exp,
                CooldownRemaining = source.CooldownRemaining
            };
        }

        private static bool IsValidBarIndex(int barIndex)
        {
            return barIndex >= 0 && barIndex < BarSlotCount;
        }

        private static bool IsMissingUnityService(object service)
        {
            return service == null
                || (service is UnityEngine.Object unityObject && unityObject == null);
        }

        private static bool IsGameplayRunning()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private static bool MeetsRealmRequirement(int requiredRealm)
        {
            if (requiredRealm <= 0)
            {
                return true;
            }

            int realm = SaveManager.Instance?.Profile?.Realm
                ?? (int)RealmType.QiCondensation;
            return realm >= requiredRealm;
        }

        private static DamageType ResolveDamageType(SkillElement element)
        {
            switch (element)
            {
                case SkillElement.Fire:
                    return DamageType.Fire;
                case SkillElement.Ice:
                    return DamageType.Ice;
                case SkillElement.Lightning:
                    return DamageType.Lightning;
                case SkillElement.Poison:
                    return DamageType.Poison;
                case SkillElement.Wind:
                    return DamageType.Wind;
                default:
                    return DamageType.Physical;
            }
        }

        private static ElementType ResolveElementType(SkillElement element)
        {
            switch (element)
            {
                case SkillElement.Fire:
                    return ElementType.Fire;
                case SkillElement.Ice:
                    return ElementType.Ice;
                case SkillElement.Lightning:
                    return ElementType.Lightning;
                case SkillElement.Poison:
                    return ElementType.Poison;
                case SkillElement.Wind:
                    return ElementType.Wind;
                case SkillElement.Metal:
                    return ElementType.Metal;
                case SkillElement.Earth:
                    return ElementType.Earth;
                default:
                    return ElementType.None;
            }
        }

        private static bool TryFindDamageable(
            GameObject actor,
            out IDamageable damageable)
        {
            if (actor != null)
            {
                MonoBehaviour[] components = actor.GetComponentsInParent<MonoBehaviour>(true);
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

        private static void PublishManaInsufficientToast()
        {
            PublishToast(ManaInsufficientToastKey, ManaInsufficientToastDefault);
        }

        private static void PublishToast(string key, string defaultValue)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 2.5f
                });
        }

        private static void PersistChanges(bool inventoryChanged)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.ActiveSlot < 0)
            {
                return;
            }

            if (inventoryChanged)
            {
                saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            }

            saveManager.TrySaveModule(SaveModuleName);
        }

        private sealed class ActiveCast
        {
            public SkillData Skill;
            public SkillRuntime Runtime;
            public IPlayerSkillCaster Caster;
            public Vector3 Origin;
            public Vector3 TargetPoint;
            public GameObject TargetActor;
            public float PhaseElapsed;
            public bool Released;
        }

        private enum CastFailure
        {
            None,
            InvalidSlot,
            Busy,
            MissingSkill,
            Cooldown,
            GameplayBlocked,
            CasterBlocked,
            InsufficientMana
        }
    }
}
