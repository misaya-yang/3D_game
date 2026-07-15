using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Faction;
using Wendao.Systems.Crafting;
using Wendao.Systems.Inventory;
using Wendao.Systems.NPC;
using Wendao.Systems.Skill;
using Wendao.Systems.World;

namespace Wendao.Systems.Quest
{
    public sealed class QuestManager : SafeBehaviour, IQuestService
    {
        public const string SaveModuleName = "quests";

        private readonly Dictionary<string, QuestRuntimeState> _states =
            new Dictionary<string, QuestRuntimeState>(StringComparer.Ordinal);
        private readonly List<string> _activeIds = new List<string>();
        private readonly List<string> _completedIds = new List<string>();

        private ReadOnlyCollection<string> _readOnlyActiveIds;
        private ReadOnlyCollection<string> _readOnlyCompletedIds;
        private SaveManager _registeredSaveManager;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyList<string> ActiveIds => _readOnlyActiveIds;
        public IReadOnlyList<string> CompletedIds => _readOnlyCompletedIds;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IQuestService>(out IQuestService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyActiveIds = _activeIds.AsReadOnly();
            _readOnlyCompletedIds = _completedIds.AsReadOnly();
            ServiceLocator.Register<IQuestService>(this);
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
            EventBus.Subscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Subscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Subscribe<MapInfo>(
                SceneLoader.MapLoadedEvent,
                HandleMapLoaded);
            EventBus.Subscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDeathInfo>(
                CombatEvents.EnemyKilled,
                HandleEnemyKilled);
            EventBus.Unsubscribe<ItemAcquireInfo>(
                InventoryEvents.ItemAcquired,
                HandleItemAcquired);
            EventBus.Unsubscribe<ItemUseInfo>(
                InventoryEvents.ItemUsed,
                HandleItemUsed);
            EventBus.Unsubscribe<CraftResultInfo>(
                AlchemyEvents.CraftCompleted,
                HandleCraftCompleted);
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Unsubscribe<MapInfo>(
                SceneLoader.MapLoadedEvent,
                HandleMapLoaded);
            EventBus.Unsubscribe<DialogueInfo>(
                DialogueEvents.Ended,
                HandleDialogueEnded);
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
                && ServiceLocator.TryGet<IQuestService>(
                    out IQuestService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IQuestService>();
            }

            _registeredService = false;
        }

        public bool CanAccept(string questId)
        {
            return GetStatus(questId) == QuestStatus.Available;
        }

        public bool Accept(string questId)
        {
            QuestData quest = GetQuestData(questId);
            QuestReward acceptReward = ResolveAcceptReward(quest);
            if (quest == null
                || !CanAccept(questId)
                || !CanGrantReward(acceptReward))
            {
                return false;
            }

            var state = new QuestRuntimeState
            {
                QuestId = quest.Id,
                Status = QuestStatus.Active,
                ObjectiveProgress = new int[quest.Objectives?.Length ?? 0],
                AcceptRewardsGranted = false
            };
            _states.Add(quest.Id, state);
            if (!GrantReward(acceptReward))
            {
                _states.Remove(quest.Id);
                return false;
            }

            state.AcceptRewardsGranted = true;
            ApplyAcceptSideEffects(quest, acceptReward);
            InitializeCollectProgress(quest, state);
            RefreshCompletionStatus(quest, state);
            RebuildIdViews();
            Persist();
            EventBus.Publish(
                QuestEvents.Accepted,
                new QuestInfo
                {
                    QuestId = quest.Id,
                    Status = state.Status
                });
            return true;
        }

        public void NotifyKill(string enemyId)
        {
            UpdateMatchingObjectives(
                ObjectiveType.Kill,
                enemyId,
                1,
                false);
        }

        public void NotifyCollect(string itemId, int totalCount)
        {
            UpdateMatchingObjectives(
                ObjectiveType.Collect,
                itemId,
                Mathf.Max(0, totalCount),
                true);
        }

        public void NotifyUseItem(string itemId)
        {
            UpdateMatchingObjectives(
                ObjectiveType.UseItem,
                itemId,
                int.MaxValue,
                true);
        }

        public void NotifyCraft(string resultItemId, int resultCount)
        {
            UpdateMatchingObjectives(
                ObjectiveType.Craft,
                resultItemId,
                Mathf.Max(1, resultCount),
                false);
        }

        public void NotifyTalk(string npcId)
        {
            UpdateMatchingObjectives(
                ObjectiveType.Talk,
                npcId,
                int.MaxValue,
                true);
        }

        public void NotifyReach(string locationId)
        {
            UpdateMatchingObjectives(
                ObjectiveType.Reach,
                locationId,
                int.MaxValue,
                true);
        }

        public void NotifyRealm(RealmType newRealm)
        {
            bool changed = false;
            foreach (QuestRuntimeState state in _states.Values)
            {
                if (state.Status != QuestStatus.Active)
                {
                    continue;
                }

                QuestData quest = GetQuestData(state.QuestId);
                if (quest?.Objectives == null)
                {
                    continue;
                }

                for (int index = 0; index < quest.Objectives.Length; index++)
                {
                    QuestObjective objective = quest.Objectives[index];
                    if (objective == null
                        || objective.Type != ObjectiveType.ReachRealm
                        || !CanProgressObjective(quest, state, index)
                        || !Enum.TryParse(
                            objective.TargetId,
                            false,
                            out RealmType requiredRealm)
                        || newRealm < requiredRealm)
                    {
                        continue;
                    }

                    changed |= SetObjectiveProgress(
                        quest,
                        state,
                        index,
                        Mathf.Max(1, objective.RequiredCount));
                }

                if (RefreshCompletionStatus(quest, state))
                {
                    changed = true;
                }
            }

            FinishProgressUpdate(changed);
        }

        public bool CanTurnIn(string questId)
        {
            return _states.TryGetValue(questId ?? string.Empty, out QuestRuntimeState state)
                && state.Status == QuestStatus.Completed;
        }

        public bool TurnIn(string questId)
        {
            QuestData quest = GetQuestData(questId);
            if (quest == null
                || !CanTurnIn(questId)
                || !CanConsumeTurnInCosts(quest)
                || !CanGrantReward(quest.Rewards)
                || !ConsumeTurnInCosts(quest))
            {
                return false;
            }

            if (!GrantReward(quest.Rewards))
            {
                RestoreTurnInCosts(quest);
                return false;
            }

            QuestRuntimeState state = _states[questId];
            state.Status = QuestStatus.TurnedIn;
            ApplyTurnInSideEffects(quest);
            RebuildIdViews();
            Persist();
            EventBus.Publish(
                QuestEvents.Completed,
                new QuestInfo
                {
                    QuestId = quest.Id,
                    Status = QuestStatus.TurnedIn
                });
            return true;
        }

        public QuestStatus GetStatus(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return QuestStatus.Locked;
            }

            if (_states.TryGetValue(questId, out QuestRuntimeState state))
            {
                return state.Status;
            }

            QuestData quest = GetQuestData(questId);
            if (quest == null || !MeetsRealmRequirement(quest))
            {
                return QuestStatus.Locked;
            }

            string[] prerequisites = quest.PrerequisiteQuestIds
                ?? Array.Empty<string>();
            for (int index = 0; index < prerequisites.Length; index++)
            {
                if (GetStatus(prerequisites[index]) != QuestStatus.TurnedIn)
                {
                    return QuestStatus.Locked;
                }
            }

            return QuestStatus.Available;
        }

        public QuestData GetQuestData(string questId)
        {
            return ConfigDatabase.Instance?.GetQuest(questId);
        }

        public int GetObjectiveProgress(string questId, int objectiveIndex)
        {
            return _states.TryGetValue(
                    questId ?? string.Empty,
                    out QuestRuntimeState state)
                && state.ObjectiveProgress != null
                && objectiveIndex >= 0
                && objectiveIndex < state.ObjectiveProgress.Length
                ? state.ObjectiveProgress[objectiveIndex]
                : 0;
        }

        public string ResolveInteractionDialogueId(
            string npcId,
            string fallbackDialogueId)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                return fallbackDialogueId ?? string.Empty;
            }

            string completed = FindQuestDialogue(
                npcId,
                QuestStatus.Completed,
                true);
            if (!string.IsNullOrEmpty(completed))
            {
                return completed;
            }

            string available = FindQuestDialogue(
                npcId,
                QuestStatus.Available,
                false);
            if (!string.IsNullOrEmpty(available))
            {
                return available;
            }

            string active = FindQuestDialogue(
                npcId,
                QuestStatus.Active,
                false);
            return string.IsNullOrEmpty(active)
                ? fallbackDialogueId ?? string.Empty
                : active;
        }

        public QuestSaveData CaptureSaveData()
        {
            var ids = new List<string>(_states.Keys);
            ids.Sort(StringComparer.Ordinal);
            var quests = new List<QuestRuntimeState>(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                quests.Add(CloneState(_states[ids[index]]));
            }

            return new QuestSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Quests = quests
            };
        }

        public void RestoreSaveData(QuestSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Quests == null)
            {
                throw new InvalidDataException("Quest save data is invalid.");
            }

            var restored = new Dictionary<string, QuestRuntimeState>(
                StringComparer.Ordinal);
            for (int index = 0; index < data.Quests.Count; index++)
            {
                QuestRuntimeState source = data.Quests[index];
                QuestData quest = source == null
                    ? null
                    : GetQuestData(source.QuestId);
                if (!IsValidSavedState(source, quest)
                    || restored.ContainsKey(source.QuestId))
                {
                    throw new InvalidDataException(
                        "Quest save contains an invalid quest state.");
                }

                restored.Add(source.QuestId, CloneState(source));
            }

            _states.Clear();
            foreach (KeyValuePair<string, QuestRuntimeState> pair in restored)
            {
                _states.Add(pair.Key, pair.Value);
            }

            RebuildIdViews();
        }

        private void UpdateMatchingObjectives(
            ObjectiveType type,
            string targetId,
            int value,
            bool absolute)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            bool changed = false;
            foreach (QuestRuntimeState state in _states.Values)
            {
                if (state.Status != QuestStatus.Active)
                {
                    continue;
                }

                QuestData quest = GetQuestData(state.QuestId);
                if (quest?.Objectives == null)
                {
                    continue;
                }

                for (int index = 0; index < quest.Objectives.Length; index++)
                {
                    QuestObjective objective = quest.Objectives[index];
                    if (objective == null
                        || objective.Type != type
                        || !CanProgressObjective(quest, state, index)
                        || !string.Equals(
                            objective.TargetId,
                            targetId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int required = Mathf.Max(1, objective.RequiredCount);
                    int current = state.ObjectiveProgress[index];
                    int next;
                    if (!absolute)
                    {
                        next = Mathf.Min(required, current + Mathf.Max(0, value));
                    }
                    else if (type == ObjectiveType.Collect
                        && objective.LatchOnFirstAcquire)
                    {
                        next = Mathf.Max(current, Mathf.Min(required, value));
                    }
                    else
                    {
                        next = Mathf.Min(required, value);
                    }

                    changed |= SetObjectiveProgress(quest, state, index, next);
                }

                if (RefreshCompletionStatus(quest, state))
                {
                    changed = true;
                }
            }

            FinishProgressUpdate(changed);
        }

        private bool SetObjectiveProgress(
            QuestData quest,
            QuestRuntimeState state,
            int objectiveIndex,
            int next)
        {
            QuestObjective objective = quest.Objectives[objectiveIndex];
            int required = Mathf.Max(1, objective.RequiredCount);
            int clamped = Mathf.Clamp(next, 0, required);
            if (state.ObjectiveProgress[objectiveIndex] == clamped)
            {
                return false;
            }

            state.ObjectiveProgress[objectiveIndex] = clamped;
            EventBus.Publish(
                QuestEvents.Progressed,
                new QuestProgressInfo
                {
                    QuestId = quest.Id,
                    ObjectiveIndex = objectiveIndex,
                    Current = clamped,
                    Required = required
                });
            return true;
        }

        private bool RefreshCompletionStatus(
            QuestData quest,
            QuestRuntimeState state)
        {
            if (state.Status != QuestStatus.Active || !IsAllComplete(quest, state))
            {
                return false;
            }

            state.Status = QuestStatus.Completed;
            return true;
        }

        private void FinishProgressUpdate(bool changed)
        {
            if (!changed)
            {
                return;
            }

            RebuildIdViews();
            Persist();
        }

        private void InitializeCollectProgress(
            QuestData quest,
            QuestRuntimeState state)
        {
            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || quest.Objectives == null)
            {
                return;
            }

            for (int index = 0; index < quest.Objectives.Length; index++)
            {
                QuestObjective objective = quest.Objectives[index];
                if (objective != null && objective.Type == ObjectiveType.Collect)
                {
                    SetObjectiveProgress(
                        quest,
                        state,
                        index,
                        inventory.CountItem(objective.TargetId));
                }
            }
        }

        private bool CanGrantReward(QuestReward reward)
        {
            if (reward == null)
            {
                return true;
            }

            if (!IsFiniteNonNegative(reward.CultivationXp)
                || reward.SpiritStones < 0
                || reward.FactionRep < 0
                || (reward.FactionRep > 0
                    && string.IsNullOrWhiteSpace(reward.FactionId)))
            {
                return false;
            }

            SaveProfileData profile = SaveManager.Instance?.Profile;
            if (reward.FactionRep > 0
                && (profile?.FactionReputation == null
                    || (profile.FactionReputation.TryGetValue(
                            reward.FactionId,
                            out int currentRep)
                        && currentRep > int.MaxValue - reward.FactionRep)))
            {
                return false;
            }

            ItemStack[] items = reward.Items ?? Array.Empty<ItemStack>();
            bool needsInventory = reward.SpiritStones > 0 || items.Length > 0;
            IInventoryService inventory = null;
            if (needsInventory
                && !ServiceLocator.TryGet<IInventoryService>(
                    out inventory))
            {
                return false;
            }

            if (reward.SpiritStones > 0
                && inventory.SpiritStones > int.MaxValue - reward.SpiritStones)
            {
                return false;
            }

            for (int index = 0; index < items.Length; index++)
            {
                ItemStack item = items[index];
                if (item == null
                    || item.Count <= 0
                    || !inventory.CanAdd(item.ItemId, item.Count))
                {
                    return false;
                }
            }

            if (reward.CultivationXp > 0f
                && !ServiceLocator.TryGet<ICultivationService>(out _))
            {
                return false;
            }

            string[] skillIds = reward.SkillIds ?? Array.Empty<string>();
            if (skillIds.Length == 0)
            {
                return true;
            }

            if (!ServiceLocator.TryGet<ISkillService>(out ISkillService skills))
            {
                return false;
            }

            int realm = SaveManager.Instance?.Profile?.Realm
                ?? (int)RealmType.Mortal;
            for (int index = 0; index < skillIds.Length; index++)
            {
                SkillData skill = ConfigDatabase.Instance?.GetSkill(skillIds[index]);
                if (skill == null
                    || skill.RequiredRealm > realm
                    || HasLearnedSkill(skills, skill.Id))
                {
                    if (skill == null || skill.RequiredRealm > realm)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool GrantReward(QuestReward reward)
        {
            if (reward == null)
            {
                return true;
            }

            ServiceLocator.TryGet<IInventoryService>(out IInventoryService inventory);
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

            if (reward.SpiritStones > 0
                && !inventory.AddSpiritStones(reward.SpiritStones))
            {
                return false;
            }

            if (reward.CultivationXp > 0f)
            {
                ServiceLocator.Get<ICultivationService>().AddXp(
                    reward.CultivationXp,
                    XpSourceType.Quest);
            }

            string[] skillIds = reward.SkillIds ?? Array.Empty<string>();
            if (skillIds.Length > 0)
            {
                ISkillService skills = ServiceLocator.Get<ISkillService>();
                for (int index = 0; index < skillIds.Length; index++)
                {
                    if (!HasLearnedSkill(skills, skillIds[index])
                        && !skills.Learn(skillIds[index]))
                    {
                        return false;
                    }
                }
            }

            if (reward.FactionRep > 0)
            {
                if (ServiceLocator.TryGet<IFactionService>(
                        out IFactionService faction))
                {
                    faction.AddRep(reward.FactionId, reward.FactionRep);
                }
                else
                {
                    SaveManager saveManager = SaveManager.Instance;
                    SaveProfileData profile = saveManager.Profile;
                    profile.FactionReputation.TryGetValue(
                        reward.FactionId,
                        out int current);
                    profile.FactionReputation[reward.FactionId] =
                        current + reward.FactionRep;
                    if (saveManager.ActiveSlot >= 0)
                    {
                        saveManager.TrySaveModule("profile");
                    }
                }
            }

            return true;
        }

        private QuestReward ResolveAcceptReward(QuestData quest)
        {
            if (quest != null
                && string.Equals(
                    quest.Id,
                    QuestContentIds.MainFoundationBreakthrough,
                    StringComparison.Ordinal)
                && HasWorldFlag(
                    CultivationContentIds.FoundationPillGrantedFlag))
            {
                return null;
            }

            return quest?.AcceptRewards;
        }

        private static bool CanProgressObjective(
            QuestData quest,
            QuestRuntimeState state,
            int objectiveIndex)
        {
            if (quest == null
                || state?.ObjectiveProgress == null
                || !quest.ObjectivesAreOrdered)
            {
                return true;
            }

            for (int index = 0; index < objectiveIndex; index++)
            {
                QuestObjective prior = quest.Objectives[index];
                if (prior == null
                    || state.ObjectiveProgress[index]
                        < Mathf.Max(1, prior.RequiredCount))
                {
                    return false;
                }
            }

            return true;
        }

        private string FindQuestDialogue(
            string npcId,
            QuestStatus wantedStatus,
            bool completeDialogue)
        {
            string dialogue = FindQuestDialogueIn(
                QuestContentIds.MainChapterOne,
                npcId,
                wantedStatus,
                completeDialogue);
            return string.IsNullOrEmpty(dialogue)
                ? FindQuestDialogueIn(
                    QuestContentIds.SideQuests,
                    npcId,
                    wantedStatus,
                    completeDialogue)
                : dialogue;
        }

        private string FindQuestDialogueIn(
            string[] questIds,
            string npcId,
            QuestStatus wantedStatus,
            bool completeDialogue)
        {
            for (int index = 0; index < questIds.Length; index++)
            {
                QuestData quest = GetQuestData(questIds[index]);
                if (quest == null || GetStatus(quest.Id) != wantedStatus)
                {
                    continue;
                }

                string configuredNpc = completeDialogue
                    ? quest.TurnInNpcId
                    : ResolveStartNpcId(quest);
                if (string.Equals(
                    configuredNpc,
                    npcId,
                    StringComparison.Ordinal))
                {
                    return completeDialogue
                        ? quest.CompleteDialogueId
                        : quest.StartDialogueId;
                }
            }

            return string.Empty;
        }

        private static bool HasLearnedSkill(
            ISkillService skills,
            string skillId)
        {
            if (skills?.Learned == null)
            {
                return false;
            }

            for (int index = 0; index < skills.Learned.Count; index++)
            {
                if (string.Equals(
                    skills.Learned[index]?.SkillId,
                    skillId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanConsumeTurnInCosts(QuestData quest)
        {
            ItemStack[] costs = quest?.TurnInCosts ?? Array.Empty<ItemStack>();
            if (costs.Length == 0)
            {
                return true;
            }

            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return false;
            }

            for (int index = 0; index < costs.Length; index++)
            {
                ItemStack cost = costs[index];
                if (cost == null
                    || cost.Count <= 0
                    || inventory.CountItem(cost.ItemId) < cost.Count)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ConsumeTurnInCosts(QuestData quest)
        {
            ItemStack[] costs = quest?.TurnInCosts ?? Array.Empty<ItemStack>();
            if (costs.Length == 0)
            {
                return true;
            }

            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            int consumed = 0;
            for (; consumed < costs.Length; consumed++)
            {
                if (!inventory.RemoveItem(
                        costs[consumed].ItemId,
                        costs[consumed].Count))
                {
                    for (int rollback = 0; rollback < consumed; rollback++)
                    {
                        inventory.RestoreItem(
                            costs[rollback].ItemId,
                            costs[rollback].Count);
                    }

                    return false;
                }
            }

            return true;
        }

        private static void RestoreTurnInCosts(QuestData quest)
        {
            ItemStack[] costs = quest?.TurnInCosts ?? Array.Empty<ItemStack>();
            if (costs.Length == 0
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return;
            }

            for (int index = 0; index < costs.Length; index++)
            {
                inventory.RestoreItem(costs[index].ItemId, costs[index].Count);
            }
        }

        private static string ResolveStartNpcId(QuestData quest)
        {
            return string.IsNullOrEmpty(quest?.StartNpcId)
                ? quest?.TurnInNpcId ?? string.Empty
                : quest.StartNpcId;
        }

        private static void ApplyAcceptSideEffects(
            QuestData quest,
            QuestReward grantedReward)
        {
            if (quest == null
                || !string.Equals(
                    quest.Id,
                    QuestContentIds.MainFoundationBreakthrough,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (RewardContainsItem(
                    grantedReward,
                    InventoryContentIds.FoundationPill))
            {
                SetWorldFlag(
                    CultivationContentIds.FoundationPillGrantedFlag,
                    true);
            }

            SetWorldFlag(CultivationContentIds.FoundationPityFlag, true);
            PersistWorld();
        }

        private static void ApplyTurnInSideEffects(QuestData quest)
        {
            if (quest == null
                || !string.Equals(
                    quest.Id,
                    QuestContentIds.MainOpenCangwuPath,
                    StringComparison.Ordinal))
            {
                return;
            }

            SetWorldFlag(MapContentIds.CangwuPathOpenFlag, true);
            PersistWorld();
        }

        private static bool RewardContainsItem(
            QuestReward reward,
            string itemId)
        {
            ItemStack[] items = reward?.Items ?? Array.Empty<ItemStack>();
            for (int index = 0; index < items.Length; index++)
            {
                if (items[index] != null
                    && items[index].Count > 0
                    && string.Equals(
                        items[index].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasWorldFlag(string flagId)
        {
            SaveWorldData world = SaveManager.Instance?.World;
            return world?.QuestFlags != null
                && world.QuestFlags.TryGetValue(flagId, out bool enabled)
                && enabled;
        }

        private static void SetWorldFlag(string flagId, bool enabled)
        {
            SaveWorldData world = SaveManager.Instance?.World;
            if (world == null || string.IsNullOrEmpty(flagId))
            {
                return;
            }

            if (world.QuestFlags == null)
            {
                world.QuestFlags = new Dictionary<string, bool>(
                    StringComparer.Ordinal);
            }

            world.QuestFlags[flagId] = enabled;
        }

        private static void PersistWorld()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("world");
            }
        }

        private bool MeetsRealmRequirement(QuestData quest)
        {
            int realm = SaveManager.Instance?.Profile?.Realm
                ?? (int)RealmType.Mortal;
            return realm >= quest.RequiredRealm;
        }

        private static bool IsAllComplete(
            QuestData quest,
            QuestRuntimeState state)
        {
            QuestObjective[] objectives = quest?.Objectives
                ?? Array.Empty<QuestObjective>();
            if (state?.ObjectiveProgress == null
                || state.ObjectiveProgress.Length != objectives.Length)
            {
                return false;
            }

            for (int index = 0; index < objectives.Length; index++)
            {
                if (objectives[index] == null
                    || state.ObjectiveProgress[index]
                        < Mathf.Max(1, objectives[index].RequiredCount))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidSavedState(
            QuestRuntimeState state,
            QuestData quest)
        {
            if (state == null
                || quest == null
                || !state.AcceptRewardsGranted
                || state.ObjectiveProgress == null
                || quest.Objectives == null
                || state.ObjectiveProgress.Length != quest.Objectives.Length
                || (state.Status != QuestStatus.Active
                    && state.Status != QuestStatus.Completed
                    && state.Status != QuestStatus.TurnedIn))
            {
                return false;
            }

            for (int index = 0; index < quest.Objectives.Length; index++)
            {
                QuestObjective objective = quest.Objectives[index];
                if (objective == null
                    || state.ObjectiveProgress[index] < 0
                    || state.ObjectiveProgress[index]
                        > Mathf.Max(1, objective.RequiredCount))
                {
                    return false;
                }
            }

            bool allComplete = IsAllComplete(quest, state);
            return state.Status == QuestStatus.Active
                ? !allComplete
                : allComplete;
        }

        private void RebuildIdViews()
        {
            _activeIds.Clear();
            _completedIds.Clear();
            foreach (QuestRuntimeState state in _states.Values)
            {
                if (state.Status == QuestStatus.Active
                    || state.Status == QuestStatus.Completed)
                {
                    _activeIds.Add(state.QuestId);
                }

                if (state.Status == QuestStatus.TurnedIn)
                {
                    _completedIds.Add(state.QuestId);
                }
            }

            _activeIds.Sort(StringComparer.Ordinal);
            _completedIds.Sort(StringComparer.Ordinal);
        }

        private void ResetQuests()
        {
            _states.Clear();
            RebuildIdViews();
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
                ResetQuests);
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
            if (ServiceLocator.TryGet<IQuestService>(out IQuestService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IQuestService>(this);
            _registeredService = true;
        }

        private void Persist()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(SaveModuleName);
            }
        }

        private void HandleEnemyKilled(EnemyDeathInfo info)
        {
            NotifyKill(info.EnemyId);
        }

        private void HandleItemAcquired(ItemAcquireInfo info)
        {
            int total = ServiceLocator.TryGet<IInventoryService>(
                out IInventoryService inventory)
                ? inventory.CountItem(info.ItemId)
                : info.Count;
            NotifyCollect(info.ItemId, total);
        }

        private void HandleItemUsed(ItemUseInfo info)
        {
            NotifyUseItem(info.ItemId);
        }

        private void HandleCraftCompleted(CraftResultInfo info)
        {
            if (info.Success)
            {
                NotifyCraft(info.ResultItemId, info.ResultCount);
            }
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (info.Success)
            {
                NotifyRealm(info.NewRealm);
            }
        }

        private void HandleMapLoaded(MapInfo info)
        {
            NotifyReach(info.MapId);
        }

        private void HandleDialogueEnded(DialogueInfo info)
        {
            if (!info.Cancelled)
            {
                NotifyTalk(info.NpcId);
            }
        }

        private static QuestRuntimeState CloneState(QuestRuntimeState source)
        {
            return new QuestRuntimeState
            {
                QuestId = source.QuestId,
                Status = source.Status,
                ObjectiveProgress = source.ObjectiveProgress != null
                    ? (int[])source.ObjectiveProgress.Clone()
                    : Array.Empty<int>(),
                AcceptRewardsGranted = source.AcceptRewardsGranted
            };
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
