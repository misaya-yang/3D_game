using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;

namespace Wendao.Systems.World
{
    public sealed class SerendipitySystem : SafeBehaviour, ISerendipityService
    {
        private bool _registeredService;

        private void Awake()
        {
            if (ServiceLocator.TryGet<ISerendipityService>(
                    out ISerendipityService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<ISerendipityService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (ServiceLocator.TryGet<ISerendipityService>(
                    out ISerendipityService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<ISerendipityService>(this);
            _registeredService = true;
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<ISerendipityService>(
                    out ISerendipityService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<ISerendipityService>();
            }

            _registeredService = false;
        }

        public bool TryTrigger(string id)
        {
            return TryTrigger(id, ResolveCurrentMapId(), Vector3.zero);
        }

        public bool TryTrigger(string id, string mapId, Vector3 rewardOrigin)
        {
            SerendipityData data = ConfigDatabase.Instance?.GetSerendipity(id);
            SaveManager saveManager = SaveManager.Instance;
            SaveProfileData profile = saveManager?.Profile;
            SaveWorldData world = saveManager?.World;
            if (data == null
                || world?.SerendipityFlags == null
                || world.QuestFlags == null
                || profile == null
                || !string.Equals(data.MapId, mapId, StringComparison.Ordinal)
                || profile.Realm < (int)data.RequiredRealm
                || (data.OnceOnly && HasCompleted(data.Id))
                || !MeetsQuestRequirement(data)
                || !TryGrantReward(data, rewardOrigin))
            {
                return false;
            }

            if (!world.SerendipityFlags.Contains(data.Id))
            {
                world.SerendipityFlags.Add(data.Id);
            }

            if (!string.IsNullOrEmpty(data.WorldFlag))
            {
                world.QuestFlags[data.WorldFlag] = true;
            }

            if (saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("world");
            }

            EventBus.Publish(
                SerendipityEvents.Triggered,
                new SerendipityInfo
                {
                    SerendipityId = data.Id,
                    MapId = data.MapId,
                    WorldFlag = data.WorldFlag
                });

            if (GameManager.Instance?.IsInCombat != true
                && !string.IsNullOrEmpty(data.DialogueId)
                && ServiceLocator.TryGet<IDialogueService>(
                    out IDialogueService dialogue))
            {
                dialogue.TryStartDialogue(data.DialogueId, data.TriggerId);
            }

            return true;
        }

        public bool HasCompleted(string id)
        {
            List<string> flags = SaveManager.Instance?.World?.SerendipityFlags;
            return !string.IsNullOrEmpty(id)
                && flags != null
                && flags.Contains(id);
        }

        private static bool MeetsQuestRequirement(SerendipityData data)
        {
            if (string.IsNullOrEmpty(data.RequiredQuestId))
            {
                return true;
            }

            return ServiceLocator.TryGet<IQuestService>(out IQuestService quests)
                && quests.GetStatus(data.RequiredQuestId) == QuestStatus.TurnedIn;
        }

        private static bool TryGrantReward(
            SerendipityData data,
            Vector3 rewardOrigin)
        {
            QuestReward reward = data.Rewards ?? new QuestReward();
            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || reward.SpiritStones < 0
                || inventory.SpiritStones > int.MaxValue - reward.SpiritStones)
            {
                return false;
            }

            if (reward.CultivationXp != 0f
                || reward.FactionRep != 0
                || (reward.SkillIds?.Length ?? 0) > 0)
            {
                Debug.LogError(
                    $"Serendipity '{data.Id}' contains unsupported non-item rewards; they were stripped.");
            }

            ItemStack[] items = reward.Items ?? Array.Empty<ItemStack>();
            var validItems = new List<ItemStack>(items.Length);
            for (int index = 0; index < items.Length; index++)
            {
                ItemStack stack = items[index];
                ItemData item = stack == null
                    ? null
                    : ConfigDatabase.Instance?.GetItem(stack.ItemId);
                if (stack == null || stack.Count <= 0 || item == null)
                {
                    return false;
                }

                if (item.Type == ItemType.Equipment)
                {
                    Debug.LogError(
                        $"Serendipity '{data.Id}' equipment reward '{item.Id}' was stripped.");
                    continue;
                }

                validItems.Add(stack);
            }

            if (reward.SpiritStones > 0
                && !inventory.AddSpiritStones(reward.SpiritStones))
            {
                return false;
            }

            ServiceLocator.TryGet<ILootService>(out ILootService loot);
            for (int index = 0; index < validItems.Count; index++)
            {
                ItemStack stack = validItems[index];
                if (!inventory.AddItem(
                        stack.ItemId,
                        stack.Count,
                        AcquireSource.Quest)
                    && (loot == null
                        || loot.SpawnWorldPickup(
                            stack.ItemId,
                            stack.Count,
                            rewardOrigin) == null))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ResolveCurrentMapId()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            switch (sceneName)
            {
                case SceneLoader.DefaultMapSceneName:
                    return MapContentIds.QingshiMap;
                case SceneLoader.CangwuMapSceneName:
                    return MapContentIds.CangwuMap;
                case SceneLoader.BlackwindDungeonSceneName:
                    return MapContentIds.BlackwindMap;
                default:
                    return string.Empty;
            }
        }
    }
}
