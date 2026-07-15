using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.Cultivation
{
    public sealed class SpiritRootSystem : SafeBehaviour, ISpiritRootService
    {
        private bool _registeredService;

        public SpiritRootType Root
        {
            get
            {
                string savedRoot = SaveManager.Instance?.Profile?.SpiritRoot;
                return Enum.TryParse(
                        savedRoot,
                        false,
                        out SpiritRootType parsed)
                    && parsed != SpiritRootType.None
                    && Enum.IsDefined(typeof(SpiritRootType), parsed)
                    ? parsed
                    : SpiritRootType.None;
            }
        }

        public bool HasChosenRoot => Root != SpiritRootType.None;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ISpiritRootService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService current))
            {
                ServiceLocator.Register<ISpiritRootService>(this);
                _registeredService = true;
            }
            else
            {
                _registeredService = ReferenceEquals(current, this);
            }
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ISpiritRootService>();
            }

            _registeredService = false;
        }

        public void ChooseRoot(SpiritRootType type)
        {
            TryChooseRoot(type);
        }

        public bool TryChooseRoot(SpiritRootType type)
        {
            if (HasChosenRoot || !IsDefaultPickable(type))
            {
                return false;
            }

            return TryCommitRoot(type);
        }

        public void RandomizeRoot()
        {
            TryRandomizeRoot();
        }

        public void RandomizeRoot(int seed)
        {
            TryRandomizeRoot(seed);
        }

        public bool TryRandomizeRoot()
        {
            return TryRandomizeRootWithRoll(UnityEngine.Random.value);
        }

        public bool TryRandomizeRoot(int seed)
        {
            var random = new System.Random(seed);
            return TryRandomizeRootWithRoll((float)random.NextDouble());
        }

        public float GetCultivationMultiplier()
        {
            return GetEntry()?.CultivationMul ?? 1f;
        }

        public float GetBodyMultiplier()
        {
            return GetEntry()?.BodyMul ?? 1f;
        }

        public float GetElementBonus(ElementType element)
        {
            if (element == ElementType.None)
            {
                return 0f;
            }

            Dictionary<string, float> bonuses = GetEntry()?.ElementBonus;
            if (bonuses == null)
            {
                return 0f;
            }

            if (bonuses.TryGetValue(element.ToString(), out float bonus))
            {
                return bonus;
            }

            return bonuses.TryGetValue("All", out float allBonus) ? allBonus : 0f;
        }

        public float GetBodyPotionMul()
        {
            return GetEntry()?.Passives?.BodyPotionMul ?? 1f;
        }

        public float GetPhysicalDamageBonus()
        {
            return GetEntry()?.Passives?.PhysicalDamageBonus ?? 0f;
        }

        public float GetBlockPhysDrBonus()
        {
            return GetEntry()?.Passives?.BlockPhysDrBonus ?? 0f;
        }

        public string GetIntroDescriptionKey()
        {
            return GetEntry()?.IntroDescriptionKey ?? string.Empty;
        }

        private bool TryRandomizeRootWithRoll(float normalizedRoll)
        {
            SpiritRootConfig config = ConfigDatabase.Instance?.SpiritRoot;
            if (HasChosenRoot
                || config == null
                || !config.RandomEnabled
                || config.Roots == null
                || config.Roots.Length == 0)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int index = 0; index < config.Roots.Length; index++)
            {
                SpiritRootEntry entry = config.Roots[index];
                if (entry != null && entry.Type != SpiritRootType.None)
                {
                    totalWeight += Mathf.Max(0f, entry.Weight);
                }
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float target = Mathf.Clamp01(normalizedRoll) * totalWeight;
            SpiritRootType fallback = SpiritRootType.None;
            for (int index = 0; index < config.Roots.Length; index++)
            {
                SpiritRootEntry entry = config.Roots[index];
                if (entry == null
                    || entry.Type == SpiritRootType.None
                    || entry.Weight <= 0f)
                {
                    continue;
                }

                fallback = entry.Type;
                target -= entry.Weight;
                if (target <= 0f)
                {
                    return TryCommitRoot(entry.Type);
                }
            }

            return fallback != SpiritRootType.None && TryCommitRoot(fallback);
        }

        private bool TryCommitRoot(SpiritRootType type)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager?.Profile == null
                || ConfigDatabase.Instance?.GetSpiritRoot(type) == null)
            {
                return false;
            }

            saveManager.Profile.SpiritRoot = type.ToString();
            PersistProfile(saveManager);
            return true;
        }

        private SpiritRootEntry GetEntry()
        {
            return ConfigDatabase.Instance?.GetSpiritRoot(Root);
        }

        private static bool IsDefaultPickable(SpiritRootType type)
        {
            SpiritRootType[] pickable = ConfigDatabase.Instance?.SpiritRoot?.DefaultPickable;
            if (pickable == null)
            {
                return false;
            }

            for (int index = 0; index < pickable.Length; index++)
            {
                if (pickable[index] == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PersistProfile(SaveManager saveManager)
        {
            if (saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("profile");
            }
        }
    }
}
