using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;

namespace Wendao.Systems.Cultivation
{
    public sealed class BodyRefinementManager : SafeBehaviour,
        IBodyRefinementService
    {
        private const float DefaultDamageXpMultiplier = 0.1f;

        private bool _registeredService;
        private bool _reviveAvailable;
        private BodyLevel _fallbackLevel = BodyLevel.Mortal;
        private float _fallbackXp;

        public BodyLevel Level
        {
            get
            {
                SaveProfileData profile = SaveManager.Instance?.Profile;
                int value = profile?.BodyLevel ?? (int)_fallbackLevel;
                return Enum.IsDefined(typeof(BodyLevel), value)
                    ? (BodyLevel)value
                    : BodyLevel.Mortal;
            }
        }

        public float Xp
        {
            get
            {
                float value = SaveManager.Instance?.Profile?.BodyXp
                    ?? _fallbackXp;
                return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            }
        }

        public float XpToNext
        {
            get
            {
                if (!CanGainXp)
                {
                    return 0f;
                }

                BodyLevelEntry next = GetEntry((BodyLevel)((int)Level + 1));
                return Mathf.Max(0f, next?.XpToNext ?? 0f);
            }
        }
        public float HpBonus => Mathf.Max(0f, GetCurrentEntry()?.HpBonus ?? 0f);
        public float PhysicalDR => Mathf.Clamp01(
            GetCurrentEntry()?.PhysDr ?? 0f);
        public float ControlResist => Mathf.Clamp01(
            GetCurrentEntry()?.ControlResist ?? 0f);
        public bool HasCombatRevive => GetCurrentEntry()?.Revive ?? false;
        public bool CanGainXp => Level < BodyLevel.Eternal;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IBodyRefinementService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService current))
            {
                ServiceLocator.Register<IBodyRefinementService>(this);
                _registeredService = true;
            }
            else
            {
                _registeredService = ReferenceEquals(current, this);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IBodyRefinementService>();
            }

            _registeredService = false;
        }

        public void AddBodyXp(float amount)
        {
            if (!CanGainXp
                || amount <= 0f
                || float.IsNaN(amount)
                || float.IsInfinity(amount))
            {
                return;
            }

            SetXp(Mathf.Min(float.MaxValue, Xp + amount));
            PersistProfile();
        }

        public void AddBodyXpFromPotion(float amount)
        {
            if (amount <= 0f
                || float.IsNaN(amount)
                || float.IsInfinity(amount))
            {
                return;
            }

            float multiplier = ResolveSpiritRoot()?.GetBodyPotionMul() ?? 1f;
            AddBodyXp(amount * Mathf.Max(0f, multiplier));
        }

        public bool TryLevelUp()
        {
            if (!CanGainXp)
            {
                return false;
            }

            BodyLevel nextLevel = (BodyLevel)((int)Level + 1);
            BodyLevelEntry nextEntry = GetEntry(nextLevel);
            if (nextEntry == null
                || nextEntry.XpToNext <= 0f
                || Xp + 0.0001f < nextEntry.XpToNext)
            {
                return false;
            }

            SetLevel(nextLevel);
            if (nextEntry.Revive)
            {
                _reviveAvailable = true;
            }

            PersistProfile();

            return true;
        }

        public bool TryConsumeRevive()
        {
            if (!HasCombatRevive || !_reviveAvailable)
            {
                return false;
            }

            _reviveAvailable = false;
            return true;
        }

        private void HandlePlayerDamaged(DamageInfo info)
        {
            if (info.Target == null || info.Amount <= 0f || !CanGainXp)
            {
                return;
            }

            float configuredMultiplier = ConfigDatabase.Instance?.Body
                    ?.XpFromDamageTakenMul
                ?? DefaultDamageXpMultiplier;
            if (configuredMultiplier <= 0f)
            {
                configuredMultiplier = DefaultDamageXpMultiplier;
            }

            float rootMultiplier = ResolveSpiritRoot()?.GetBodyMultiplier() ?? 1f;
            AddBodyXp(
                info.Amount
                * configuredMultiplier
                * Mathf.Max(0f, rootMultiplier));
        }

        private BodyLevelEntry GetCurrentEntry()
        {
            return GetEntry(Level);
        }

        private static BodyLevelEntry GetEntry(BodyLevel level)
        {
            BodyLevelEntry[] levels = ConfigDatabase.Instance?.Body?.Levels;
            if (levels == null)
            {
                return null;
            }

            int target = (int)level;
            for (int index = 0; index < levels.Length; index++)
            {
                BodyLevelEntry entry = levels[index];
                if (entry != null && entry.Level == target)
                {
                    return entry;
                }
            }

            return null;
        }

        private static ISpiritRootService ResolveSpiritRoot()
        {
            return ServiceLocator.TryGet<ISpiritRootService>(
                out ISpiritRootService spiritRoot)
                ? spiritRoot
                : null;
        }

        private void SetLevel(BodyLevel level)
        {
            _fallbackLevel = level;
            SaveProfileData profile = SaveManager.Instance?.Profile;
            if (profile != null)
            {
                profile.BodyLevel = (int)level;
            }
        }

        private void SetXp(float value)
        {
            _fallbackXp = Mathf.Max(0f, value);
            SaveProfileData profile = SaveManager.Instance?.Profile;
            if (profile != null)
            {
                profile.BodyXp = _fallbackXp;
            }
        }

        private static void PersistProfile()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("profile");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
