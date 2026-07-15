using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Quest
{
    public sealed class DailyQuestManager : SafeBehaviour, IDailyQuestService
    {
        public const string SaveModuleName = "dailies";
        public static readonly TimeSpan ResetInterval = TimeSpan.FromHours(24d);

        private readonly List<DailyQuestRuntimeState> _active =
            new List<DailyQuestRuntimeState>();
        private ReadOnlyCollection<DailyQuestRuntimeState> _readOnlyActive;
        private SaveManager _registeredSaveManager;
        private DateTime _cycleStartedUtc;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyList<DailyQuestRuntimeState> Active => _readOnlyActive;
        public DateTime CycleStartedUtc => _cycleStartedUtc;
        public DateTime NextResetUtc => _cycleStartedUtc + ResetInterval;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IDailyQuestService>(
                    out IDailyQuestService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyActive = _active.AsReadOnly();
            ResetCycle(DateTime.UtcNow, false);
            ServiceLocator.Register<IDailyQuestService>(this);
            _registeredService = true;
            TryRegisterSaveModule();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Subscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
            Refresh(DateTime.UtcNow);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
        }

        private void OnDestroy()
        {
            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<IDailyQuestService>(
                    out IDailyQuestService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IDailyQuestService>();
            }

            _registeredService = false;
        }

        public DailyQuestRuntimeState GetState(string questId)
        {
            for (int index = 0; index < _active.Count; index++)
            {
                if (string.Equals(
                    _active[index].QuestId,
                    questId,
                    StringComparison.Ordinal))
                {
                    return _active[index];
                }
            }

            return null;
        }

        public bool IsComplete(string questId)
        {
            DailyQuestRuntimeState state = GetState(questId);
            QuestData quest = ConfigDatabase.Instance?.GetQuest(questId);
            return state != null
                && quest?.Objectives != null
                && quest.Objectives.Length == 1
                && state.Progress >= Mathf.Max(
                    1,
                    quest.Objectives[0].RequiredCount);
        }

        public bool TryClaim(string questId)
        {
            Refresh(DateTime.UtcNow);
            DailyQuestRuntimeState state = GetState(questId);
            QuestData quest = ConfigDatabase.Instance?.GetQuest(questId);
            if (state == null
                || state.Claimed
                || !IsComplete(questId)
                || !CanGrant(quest?.Rewards)
                || !Grant(quest.Rewards))
            {
                return false;
            }

            state.Claimed = true;
            Persist();
            EventBus.Publish(
                DailyQuestEvents.Claimed,
                new DailyQuestClaimInfo { QuestId = questId });
            return true;
        }

        public bool Refresh(DateTime utcNow)
        {
            DateTime normalized = NormalizeUtc(utcNow);
            if (_cycleStartedUtc != default
                && normalized >= _cycleStartedUtc
                && normalized - _cycleStartedUtc < ResetInterval)
            {
                return false;
            }

            ResetCycle(normalized, true);
            return true;
        }

        public DailyQuestSaveData CaptureSaveData()
        {
            var states = new List<DailyQuestRuntimeState>(_active.Count);
            for (int index = 0; index < _active.Count; index++)
            {
                states.Add(Clone(_active[index]));
            }

            return new DailyQuestSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                CycleStartedUtc = _cycleStartedUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                Quests = states
            };
        }

        public void RestoreSaveData(DailyQuestSaveData data)
        {
            if (!TryValidate(data, out DateTime cycleStartedUtc))
            {
                ResetCycle(DateTime.UtcNow, true);
                return;
            }

            _active.Clear();
            for (int index = 0; index < data.Quests.Count; index++)
            {
                _active.Add(Clone(data.Quests[index]));
            }

            _cycleStartedUtc = cycleStartedUtc;
            Refresh(DateTime.UtcNow);
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            if (!string.IsNullOrEmpty(info.EnemyId))
            {
                AddProgress(QuestContentIds.DailyHunt, 1);
            }
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            if (info.Source == AcquireSource.Gather && info.Count > 0)
            {
                AddProgress(QuestContentIds.DailyGather, info.Count);
            }
        }

        private void AddProgress(string questId, int amount)
        {
            Refresh(DateTime.UtcNow);
            DailyQuestRuntimeState state = GetState(questId);
            QuestData quest = ConfigDatabase.Instance?.GetQuest(questId);
            if (state == null
                || state.Claimed
                || quest?.Objectives == null
                || quest.Objectives.Length != 1)
            {
                return;
            }

            int required = Mathf.Max(1, quest.Objectives[0].RequiredCount);
            int next = Mathf.Min(required, state.Progress + Mathf.Max(0, amount));
            if (next == state.Progress)
            {
                return;
            }

            state.Progress = next;
            Persist();
            EventBus.Publish(
                DailyQuestEvents.Progressed,
                new DailyQuestProgressInfo
                {
                    QuestId = questId,
                    Current = next,
                    Required = required
                });
        }

        private void ResetCycle(
            DateTime utcNow,
            bool publish,
            bool persist = true)
        {
            _cycleStartedUtc = NormalizeUtc(utcNow);
            _active.Clear();
            for (int index = 0; index < QuestContentIds.DailyQuests.Length; index++)
            {
                _active.Add(
                    new DailyQuestRuntimeState
                    {
                        QuestId = QuestContentIds.DailyQuests[index],
                        Progress = 0,
                        Claimed = false
                    });
            }

            if (persist)
            {
                Persist();
            }
            if (publish)
            {
                EventBus.Publish(
                    DailyQuestEvents.Reset,
                    new DailyQuestResetInfo
                    {
                        CycleStartedUtc = _cycleStartedUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                        NextResetUtc = NextResetUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture)
                    });
            }
        }

        private static bool CanGrant(QuestReward reward)
        {
            if (reward == null
                || reward.SpiritStones < 0
                || reward.CultivationXp != 0f
                || reward.FactionRep != 0
                || (reward.SkillIds?.Length ?? 0) != 0
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return false;
            }

            if (inventory.SpiritStones > int.MaxValue - reward.SpiritStones)
            {
                return false;
            }

            ItemStack[] items = reward.Items ?? Array.Empty<ItemStack>();
            for (int index = 0; index < items.Length; index++)
            {
                if (items[index] == null
                    || items[index].Count <= 0
                    || !inventory.CanAdd(
                        items[index].ItemId,
                        items[index].Count))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Grant(QuestReward reward)
        {
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            ItemStack[] items = reward.Items ?? Array.Empty<ItemStack>();
            for (int index = 0; index < items.Length; index++)
            {
                if (!inventory.AddItem(
                    items[index].ItemId,
                    items[index].Count,
                    AcquireSource.Quest))
                {
                    return false;
                }
            }

            return reward.SpiritStones == 0
                || inventory.AddSpiritStones(reward.SpiritStones);
        }

        private static bool TryValidate(
            DailyQuestSaveData data,
            out DateTime cycleStartedUtc)
        {
            cycleStartedUtc = default;
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || !DateTime.TryParse(
                    data.CycleStartedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out cycleStartedUtc)
                || data.Quests == null
                || data.Quests.Count != QuestContentIds.DailyQuests.Length)
            {
                return false;
            }

            cycleStartedUtc = NormalizeUtc(cycleStartedUtc);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.Quests.Count; index++)
            {
                DailyQuestRuntimeState state = data.Quests[index];
                QuestData quest = state == null
                    ? null
                    : ConfigDatabase.Instance?.GetQuest(state.QuestId);
                if (state == null
                    || quest?.Type != QuestType.Daily
                    || quest.Objectives == null
                    || quest.Objectives.Length != 1
                    || state.Progress < 0
                    || state.Progress > Mathf.Max(
                        1,
                        quest.Objectives[0].RequiredCount)
                    || !seen.Add(state.QuestId))
                {
                    return false;
                }
            }

            for (int index = 0; index < QuestContentIds.DailyQuests.Length; index++)
            {
                if (!seen.Contains(QuestContentIds.DailyQuests[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static DailyQuestRuntimeState Clone(DailyQuestRuntimeState source)
        {
            return new DailyQuestRuntimeState
            {
                QuestId = source?.QuestId ?? string.Empty,
                Progress = source?.Progress ?? 0,
                Claimed = source?.Claimed ?? false
            };
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
                () => ResetCycle(DateTime.UtcNow, true, false),
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

            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            TryRegisterSaveModule();
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IDailyQuestService>(
                    out IDailyQuestService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IDailyQuestService>(this);
            _registeredService = true;
        }

        private void Persist()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (_registeredSaveModule
                && saveManager != null
                && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(SaveModuleName);
            }
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
