using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Faction;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.Title;
using Wendao.Systems.World;

namespace Wendao.Systems.Achievement
{
    public sealed class AchievementManager : SafeBehaviour, IAchievementService
    {
        public const string SaveModuleName = "achievements";
        public const int AlchemyReputationReward = 30;

        private readonly Dictionary<string, float> _progress =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<string> _unlockedIds = new List<string>();

        private ReadOnlyCollection<string> _readOnlyUnlockedIds;
        private SaveManager _registeredSaveManager;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyList<string> UnlockedIds => _readOnlyUnlockedIds;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IAchievementService>(
                    out IAchievementService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyUnlockedIds = _unlockedIds.AsReadOnly();
            ServiceLocator.Register<IAchievementService>(this);
            _registeredService = true;
            TryRegisterSaveModule();
            SubscribeEvents();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected override void SafeStart()
        {
            EvaluateCurrentRealm();
            EvaluateWasteBodyAchievement();
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
            if (!IsUnlocked(AchievementContentIds.WasteBody))
            {
                EvaluateWasteBodyAchievement();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<IAchievementService>(
                    out IAchievementService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IAchievementService>();
            }

            _registeredService = false;
        }

        public void OnTrigger(string triggerType, string targetId, float value)
        {
            if (string.IsNullOrWhiteSpace(triggerType)
                || value <= 0f
                || float.IsNaN(value)
                || float.IsInfinity(value))
            {
                return;
            }

            IReadOnlyCollection<AchievementData> achievements =
                ConfigDatabase.Instance?.Achievements;
            if (achievements == null)
            {
                return;
            }

            bool changed = false;
            foreach (AchievementData achievement in achievements)
            {
                if (achievement == null
                    || IsUnlocked(achievement.Id)
                    || !string.Equals(
                        achievement.TriggerType,
                        triggerType,
                        StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(achievement.TargetId)
                        && !string.Equals(
                            achievement.TargetId,
                            targetId ?? string.Empty,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                _progress.TryGetValue(achievement.Id, out float previous);
                float current = Mathf.Min(float.MaxValue, previous + value);
                _progress[achievement.Id] = current;
                changed = true;
                if (current + 0.0001f >= Mathf.Max(1f, achievement.RequiredValue))
                {
                    Unlock(achievement);
                }
            }

            if (changed)
            {
                PersistChanges();
            }
        }

        public bool IsUnlocked(string achievementId)
        {
            return !string.IsNullOrEmpty(achievementId)
                && _unlockedIds.Contains(achievementId);
        }

        public float GetProgress(string achievementId)
        {
            return !string.IsNullOrEmpty(achievementId)
                && _progress.TryGetValue(achievementId, out float value)
                ? value
                : 0f;
        }

        public AchievementSaveData CaptureSaveData()
        {
            var progress = new List<AchievementProgress>(_progress.Count);
            foreach (KeyValuePair<string, float> entry in _progress)
            {
                progress.Add(
                    new AchievementProgress
                    {
                        AchievementId = entry.Key,
                        Value = entry.Value
                    });
            }

            progress.Sort((left, right) => string.CompareOrdinal(
                left.AchievementId,
                right.AchievementId));
            return new AchievementSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Progress = progress,
                UnlockedIds = new List<string>(_unlockedIds)
            };
        }

        public void RestoreSaveData(AchievementSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Progress == null
                || data.UnlockedIds == null)
            {
                throw new InvalidDataException("Achievement save data is invalid.");
            }

            var restoredProgress = new Dictionary<string, float>(
                StringComparer.Ordinal);
            for (int index = 0; index < data.Progress.Count; index++)
            {
                AchievementProgress progress = data.Progress[index];
                if (progress == null
                    || ConfigDatabase.Instance?.GetAchievement(
                        progress.AchievementId) == null
                    || progress.Value < 0f
                    || float.IsNaN(progress.Value)
                    || float.IsInfinity(progress.Value)
                    || restoredProgress.ContainsKey(progress.AchievementId))
                {
                    throw new InvalidDataException(
                        "Achievement save contains invalid progress.");
                }

                restoredProgress.Add(progress.AchievementId, progress.Value);
            }

            var restoredUnlocked = new List<string>(data.UnlockedIds.Count);
            for (int index = 0; index < data.UnlockedIds.Count; index++)
            {
                string achievementId = data.UnlockedIds[index];
                if (ConfigDatabase.Instance?.GetAchievement(achievementId) == null
                    || restoredUnlocked.Contains(achievementId))
                {
                    throw new InvalidDataException(
                        "Achievement save contains an invalid unlocked id.");
                }

                restoredUnlocked.Add(achievementId);
            }

            _progress.Clear();
            foreach (KeyValuePair<string, float> entry in restoredProgress)
            {
                _progress.Add(entry.Key, entry.Value);
            }

            _unlockedIds.Clear();
            _unlockedIds.AddRange(restoredUnlocked);
        }

        public void ResetAchievements()
        {
            _progress.Clear();
            _unlockedIds.Clear();
        }

        private void Unlock(AchievementData achievement)
        {
            if (achievement == null || IsUnlocked(achievement.Id))
            {
                return;
            }

            _unlockedIds.Add(achievement.Id);
            GrantRewards(achievement);
            EventBus.Publish(
                AchievementEvents.Unlocked,
                new AchievementInfo
                {
                    Id = achievement.Id,
                    DisplayName = achievement.DisplayName,
                    Description = achievement.Description
                });
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = AchievementContentIds.UnlockToastKeyPrefix
                        + achievement.Id,
                    DefaultValue = string.Format(
                        AchievementContentIds.UnlockToastDefault,
                        achievement.DisplayName),
                    Duration = 3f
                });
        }

        private static void GrantRewards(AchievementData achievement)
        {
            if (ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                ItemStack[] items = achievement.RewardItems ?? Array.Empty<ItemStack>();
                for (int index = 0; index < items.Length; index++)
                {
                    ItemStack item = items[index];
                    if (item != null && item.Count > 0)
                    {
                        inventory.AddItem(
                            item.ItemId,
                            item.Count,
                            AcquireSource.Quest);
                    }
                }

                if (achievement.RewardSpiritStones > 0)
                {
                    inventory.AddSpiritStones(achievement.RewardSpiritStones);
                }
            }

            if (!string.IsNullOrEmpty(achievement.RewardTitleId)
                && ServiceLocator.TryGet<ITitleService>(out ITitleService titles))
            {
                titles.Unlock(achievement.RewardTitleId);
            }

            if (string.Equals(
                    achievement.Id,
                    AchievementContentIds.Alchemy10,
                    StringComparison.Ordinal)
                && ServiceLocator.TryGet<IFactionService>(
                    out IFactionService faction))
            {
                faction.AddRep(
                    FactionContentIds.Danding,
                    AlchemyReputationReward);
            }
        }

        private void SubscribeEvents()
        {
            EventBus.Subscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Subscribe<QuestInfo>(
                QuestEvents.Completed,
                HandleQuestCompleted);
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Subscribe<SerendipityInfo>(
                SerendipityEvents.Triggered,
                HandleSerendipityTriggered);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Unsubscribe<QuestInfo>(
                QuestEvents.Completed,
                HandleQuestCompleted);
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Unsubscribe<SerendipityInfo>(
                SerendipityEvents.Triggered,
                HandleSerendipityTriggered);
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            OnTrigger("KillTotal", string.Empty, 1f);
            OnTrigger("KillEnemy", info.EnemyId, 1f);
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (!info.Success)
            {
                return;
            }

            TriggerRealmAchievements(info.NewRealm);
        }

        private void HandleQuestCompleted(QuestInfo info)
        {
            OnTrigger("QuestCompleted", info.QuestId, 1f);
        }

        private void HandleCraftCompleted(CraftResultInfo info)
        {
            if (info.Success)
            {
                OnTrigger("CraftCount", string.Empty, 1f);
            }
        }

        private void HandleSerendipityTriggered(SerendipityInfo info)
        {
            OnTrigger("Flag", info.WorldFlag, 1f);
        }

        private void EvaluateCurrentRealm()
        {
            RealmType realm = ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                ? cultivation.Realm
                : (RealmType)Mathf.Clamp(
                    SaveManager.Instance?.Profile?.Realm ?? 0,
                    0,
                    (int)RealmType.NascentSoul);
            TriggerRealmAchievements(realm);
        }

        private void TriggerRealmAchievements(RealmType realm)
        {
            if (realm >= RealmType.Foundation)
            {
                OnTrigger("RealmReached", RealmType.Foundation.ToString(), 1f);
            }

            if (realm >= RealmType.GoldenCore)
            {
                OnTrigger("RealmReached", RealmType.GoldenCore.ToString(), 1f);
            }
        }

        private void EvaluateWasteBodyAchievement()
        {
            if (ServiceLocator.TryGet<ISpiritRootService>(
                    out ISpiritRootService root)
                && ServiceLocator.TryGet<IBodyRefinementService>(
                    out IBodyRefinementService body)
                && root.Root == SpiritRootType.Waste
                && body.Level >= BodyLevel.Copper)
            {
                OnTrigger("BodyLevel+Root", "Waste&Copper", 1f);
            }
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
                ResetAchievements,
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
            if (ServiceLocator.TryGet<IAchievementService>(
                    out IAchievementService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IAchievementService>(this);
            _registeredService = true;
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
