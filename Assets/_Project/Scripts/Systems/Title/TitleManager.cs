using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Player;

namespace Wendao.Systems.Title
{
    public sealed class TitleManager : SafeBehaviour, ITitleService
    {
        public const string SaveModuleName = "titles";

        private readonly List<string> _unlockedTitleIds = new List<string>();
        private ReadOnlyCollection<string> _readOnlyUnlockedTitleIds;
        private SaveManager _registeredSaveManager;
        private IPlayerTitleStatsSink _appliedSink;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public string ActiveTitleId { get; private set; } = string.Empty;
        public IReadOnlyList<string> UnlockedTitleIds =>
            _readOnlyUnlockedTitleIds;
        public float ActiveMaxHpPercent => string.Equals(
                ActiveTitleId,
                TitleContentIds.Tiegu,
                StringComparison.Ordinal)
            ? TitleContentIds.TieguMaxHpPercent
            : 0f;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ITitleService>(out ITitleService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyUnlockedTitleIds = _unlockedTitleIds.AsReadOnly();
            ServiceLocator.Register<ITitleService>(this);
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
            ApplyActiveBonus(false);
        }

        private void OnDestroy()
        {
            ClearAppliedBonus();
            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<ITitleService>(out ITitleService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ITitleService>();
            }

            _registeredService = false;
        }

        public bool IsUnlocked(string titleId)
        {
            return !string.IsNullOrEmpty(titleId)
                && _unlockedTitleIds.Contains(titleId);
        }

        public bool Unlock(string titleId)
        {
            TitleData title = ConfigDatabase.Instance?.GetTitle(titleId);
            if (title == null)
            {
                return false;
            }

            if (IsUnlocked(titleId))
            {
                return true;
            }

            _unlockedTitleIds.Add(titleId);
            PublishToast(
                TitleContentIds.UnlockedToastKey,
                string.Format(TitleContentIds.UnlockedToastDefault, title.DisplayName));
            PersistChanges();
            return Equip(titleId);
        }

        public bool Equip(string titleId)
        {
            TitleData title = ConfigDatabase.Instance?.GetTitle(titleId);
            if (title == null || !IsUnlocked(titleId))
            {
                return false;
            }

            ActiveTitleId = titleId;
            ApplyActiveBonus(true);
            EventBus.Publish(
                TitleEvents.Changed,
                new TitleInfo
                {
                    TitleId = titleId,
                    Equipped = true
                });
            PublishToast(
                TitleContentIds.EquippedToastKey,
                string.Format(TitleContentIds.EquippedToastDefault, title.DisplayName));
            PersistChanges();
            return true;
        }

        public void Unequip()
        {
            if (string.IsNullOrEmpty(ActiveTitleId))
            {
                return;
            }

            string previous = ActiveTitleId;
            ActiveTitleId = string.Empty;
            ApplyActiveBonus(true);
            EventBus.Publish(
                TitleEvents.Changed,
                new TitleInfo
                {
                    TitleId = previous,
                    Equipped = false
                });
            PersistChanges();
        }

        public StatBlock GetActiveBonus()
        {
            return CopyStats(GetActiveTitle()?.Bonus);
        }

        public TitleData GetActiveTitle()
        {
            return ConfigDatabase.Instance?.GetTitle(ActiveTitleId);
        }

        public TitleSaveData CaptureSaveData()
        {
            return new TitleSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                UnlockedTitleIds = new List<string>(_unlockedTitleIds),
                ActiveTitleId = ActiveTitleId ?? string.Empty
            };
        }

        public void RestoreSaveData(TitleSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.UnlockedTitleIds == null)
            {
                throw new InvalidDataException("Title save data is invalid.");
            }

            var restored = new List<string>(data.UnlockedTitleIds.Count);
            for (int index = 0; index < data.UnlockedTitleIds.Count; index++)
            {
                string titleId = data.UnlockedTitleIds[index];
                if (ConfigDatabase.Instance?.GetTitle(titleId) == null
                    || restored.Contains(titleId))
                {
                    throw new InvalidDataException(
                        "Title save contains an invalid title id.");
                }

                restored.Add(titleId);
            }

            string activeTitleId = data.ActiveTitleId ?? string.Empty;
            if (!string.IsNullOrEmpty(activeTitleId)
                && !restored.Contains(activeTitleId))
            {
                throw new InvalidDataException(
                    "Title save selected a locked title.");
            }

            _unlockedTitleIds.Clear();
            _unlockedTitleIds.AddRange(restored);
            ActiveTitleId = activeTitleId;
            ApplyActiveBonus(true);
        }

        public void ResetTitles()
        {
            _unlockedTitleIds.Clear();
            ActiveTitleId = string.Empty;
            ApplyActiveBonus(true);
        }

        private void ApplyActiveBonus(bool force)
        {
            IPlayerTitleStatsSink sink = null;
            if (ServiceLocator.TryGet<IPlayerCharacterStatsService>(
                    out IPlayerCharacterStatsService stats))
            {
                sink = stats as IPlayerTitleStatsSink;
            }

            if (!force && ReferenceEquals(sink, _appliedSink))
            {
                return;
            }

            if (_appliedSink != null && !ReferenceEquals(_appliedSink, sink))
            {
                _appliedSink.ApplyTitleBonus(ZeroStats(), 0f);
            }

            _appliedSink = sink;
            _appliedSink?.ApplyTitleBonus(
                GetActiveBonus(),
                ActiveMaxHpPercent);
        }

        private void ClearAppliedBonus()
        {
            _appliedSink?.ApplyTitleBonus(ZeroStats(), 0f);
            _appliedSink = null;
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
                ResetTitles,
                optional: true);
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
            if (ServiceLocator.TryGet<ITitleService>(out ITitleService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<ITitleService>(this);
            _registeredService = true;
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

        private static void PersistChanges()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(SaveModuleName);
            }
        }
    }
}
