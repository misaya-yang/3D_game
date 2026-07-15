using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Tests.EditMode.Data
{
    public class DataSchemaTests
    {
        [Test]
        public void ScriptableObjectSchemas_HaveExpectedCreateMenusAndCanInstantiate()
        {
            var schemas = new Dictionary<Type, string>
            {
                { typeof(ItemData), "问道/物品/ItemData" },
                { typeof(EquipmentData), "问道/物品/EquipmentData" },
                { typeof(SkillData), "问道/功法/SkillData" },
                { typeof(QuestData), "问道/任务/QuestData" },
                { typeof(EnemyData), "问道/敌人/EnemyData" },
                { typeof(NPCData), "问道/NPC/NPCData" },
                { typeof(CraftRecipeData), "问道/配方/CraftRecipeData" },
                { typeof(MapData), "问道/世界/MapData" },
                { typeof(DialogueData), "问道/对话/DialogueData" },
                { typeof(AchievementData), "问道/成就/AchievementData" },
                { typeof(TitleData), "问道/称号/TitleData" },
                { typeof(MountData), "问道/坐骑/MountData" },
                { typeof(StatusEffectData), "问道/状态/StatusEffectData" },
                { typeof(SerendipityData), "问道/世界/SerendipityData" }
            };

            Assert.That(schemas.Count, Is.EqualTo(14));

            foreach (var pair in schemas)
            {
                Type type = pair.Key;
                Assert.That(typeof(ScriptableObject).IsAssignableFrom(type), Is.True, type.FullName);

                var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>();
                Assert.That(menu, Is.Not.Null, type.FullName);
                Assert.That(menu.menuName, Is.EqualTo(pair.Value), type.FullName);

                ScriptableObject instance = null;
                try
                {
                    instance = ScriptableObject.CreateInstance(type);
                    Assert.That(instance, Is.Not.Null, type.FullName);
                }
                finally
                {
                    if (instance != null)
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        [Test]
        public void EnumSchemas_MatchAuthoritativeNamesAndOrder()
        {
            var schemas = new Dictionary<Type, string[]>
            {
                { typeof(DamageType), Names("Physical", "Fire", "Ice", "Lightning", "Poison", "Wind", "Dark", "True") },
                { typeof(ElementType), Names("None", "Metal", "Wood", "Water", "Fire", "Earth", "Wind", "Lightning", "Ice", "Poison", "Dark") },
                { typeof(ElementReactionType), Names("None", "Melt", "BurnBurst", "Shock", "Spread", "Sever") },
                { typeof(StatusEffectChangeType), Names("Applied", "Refreshed", "StackChanged", "Removed", "Expired", "Promoted") },
                { typeof(CombatTeam), Names("Neutral", "Player", "Enemy") },
                { typeof(RealmType), Names("Mortal", "QiCondensation", "Foundation", "GoldenCore", "NascentSoul") },
                { typeof(SpiritRootType), Names("None", "Metal", "Wood", "Water", "Fire", "Earth", "Heaven", "Waste") },
                { typeof(EquipmentSlot), Names("Weapon", "Head", "Chest", "Legs", "Boots", "Accessory1", "Accessory2", "Treasure") },
                { typeof(ItemType), Names("Consumable", "Material", "Equipment", "Quest", "Currency", "Recipe", "Talisman") },
                { typeof(ItemRarity), Names("Common", "Uncommon", "Rare", "Epic", "Legendary") },
                { typeof(SkillType), Names("Active", "Passive", "Ultimate") },
                { typeof(SkillElement), Names("None", "Fire", "Ice", "Lightning", "Poison", "Wind", "Metal", "Earth") },
                { typeof(QuestType), Names("Main", "Side", "Daily", "Weekly") },
                { typeof(QuestStatus), Names("Locked", "Available", "Active", "Completed", "Failed", "TurnedIn") },
                { typeof(ObjectiveType), Names("Kill", "Collect", "Talk", "Reach", "UseItem", "Craft", "Survive", "ReachRealm") },
                { typeof(EnemyRank), Names("Normal", "Elite", "Boss", "WorldBoss") },
                { typeof(CraftType), Names("Alchemy", "Smithing", "Talisman", "Formation") },
                { typeof(XpSourceType), Names("Combat", "Quest", "Breakthrough", "Consume", "Other") },
                { typeof(AcquireSource), Names("Loot", "Quest", "Craft", "Shop", "Cheat", "Gather") },
                { typeof(WeatherId), Names("Clear", "Rain", "Fog", "Storm", "Snow") },
                { typeof(BodyLevel), Names("Mortal", "Copper", "Diamond", "Immortal", "Eternal") },
                { typeof(UseEffectType), Names("Heal", "RestoreMana", "AddCultivationXp", "AddBodyXp", "ApplyStatus", "LearnSkill") },
                { typeof(TelegraphShape), Names("Circle", "Line", "Sector", "FullScreen") },
                { typeof(PlayerState), Names("Idle", "Move", "Sprint", "Jump", "Fall", "LightAttack", "HeavyAttack", "Dodge", "Block", "BlockHit", "Stagger", "SkillCast", "Dead") },
                { typeof(GameState), Names("Boot", "MainMenu", "Loading", "Playing", "Paused", "Dialogue", "Cutscene", "Dead") }
            };

            foreach (var pair in schemas)
            {
                Assert.That(Enum.GetNames(pair.Key), Is.EqualTo(pair.Value), pair.Key.FullName);

                Array values = Enum.GetValues(pair.Key);
                for (int i = 0; i < values.Length; i++)
                {
                    Assert.That(Convert.ToInt32(values.GetValue(i)), Is.EqualTo(i), pair.Key.FullName);
                }
            }
        }

        [Test]
        public void PublicFieldSchemas_MatchAuthoritativeContracts()
        {
            var schemas = new Dictionary<Type, string[]>
            {
                { typeof(ItemData), Names("Id", "DisplayNameKey", "DisplayName", "DescriptionKey", "Description", "Type", "Rarity", "Icon", "MaxStack", "IsBound", "BuyPrice", "SellPrice", "RequiredRealm", "UseEffects", "EquipmentDataId", "AcquisitionHintKeys") },
                { typeof(UseEffect), Names("EffectType", "Value", "Duration", "StatusEffectId") },
                { typeof(EquipmentData), Names("Id", "DisplayNameKey", "DisplayName", "Slot", "Rarity", "RequiredRealm", "BaseStats", "SetId", "MaxRefineLevel", "VisualPrefab", "MaxGemSockets", "MaxDurability") },
                { typeof(StatBlock), Names("MaxHp", "MaxMana", "Attack", "Defense", "CritRate", "CritDamage", "MoveSpeed", "AttackSpeed", "FireBonus", "IceBonus", "LightningBonus", "PoisonBonus", "WindBonus", "CultivationSpeed", "DivineSense") },
                { typeof(InventorySlot), Names("ItemId", "Count", "Bound", "InstanceId", "ExtraJson") },
                { typeof(EquipmentInstance), Names("InstanceId", "EquipmentDataId", "RefineLevel", "Durability", "GemIds") },
                { typeof(InventorySaveData), Names("SchemaVersion", "Slots", "SpiritStones", "EquipmentInstances") },
                { typeof(EquippedItemRecord), Names("Slot", "ItemId", "Bound", "Instance") },
                { typeof(EquipmentSaveData), Names("SchemaVersion", "Worn") },
                { typeof(SkillRuntime), Names("SkillId", "Level", "Exp", "CooldownRemaining") },
                { typeof(SkillSaveData), Names("SchemaVersion", "Learned", "EquippedIds") },
                { typeof(AlchemySaveData), Names("SchemaVersion", "Level", "Xp") },
                { typeof(MountSaveData), Names("SchemaVersion", "UnlockedMountIds", "SelectedMountId") },
                { typeof(AchievementProgress), Names("AchievementId", "Value") },
                { typeof(AchievementSaveData), Names("SchemaVersion", "Progress", "UnlockedIds") },
                { typeof(TitleSaveData), Names("SchemaVersion", "UnlockedTitleIds", "ActiveTitleId") },
                { typeof(QuestRuntimeState), Names("QuestId", "Status", "ObjectiveProgress", "AcceptRewardsGranted") },
                { typeof(QuestSaveData), Names("SchemaVersion", "Quests") },
                { typeof(DailyQuestRuntimeState), Names("QuestId", "Progress", "Claimed") },
                { typeof(DailyQuestSaveData), Names("SchemaVersion", "CycleStartedUtc", "Quests") },
                { typeof(SkillData), Names("Id", "DisplayNameKey", "DisplayName", "DescriptionKey", "Description", "Type", "Element", "Icon", "MaxLevel", "RequiredRealm", "BaseCooldown", "BaseManaCost", "BaseDamage", "DamagePerLevel", "CastTime", "RecoveryTime", "Range", "Radius", "IsProjectile", "ProjectileVfxId", "ImpactVfxId", "StatusOnHitId", "StatusChance", "PreferredRoots", "CastAnimation", "LevelTable") },
                { typeof(LevelScaling), Names("Level", "Damage", "Cooldown", "ManaCost") },
                { typeof(QuestData), Names("Id", "DisplayNameKey", "DisplayName", "DescriptionKey", "Description", "Type", "RequiredRealm", "PrerequisiteQuestIds", "Objectives", "ObjectivesAreOrdered", "AcceptRewards", "TurnInCosts", "Rewards", "StartDialogueId", "CompleteDialogueId", "StartNpcId", "TurnInNpcId") },
                { typeof(QuestObjective), Names("Type", "TargetId", "RequiredCount", "DescriptionKey", "Description", "LatchOnFirstAcquire") },
                { typeof(QuestReward), Names("CultivationXp", "SpiritStones", "Items", "FactionRep", "FactionId", "SkillIds") },
                { typeof(ItemStack), Names("ItemId", "Count") },
                { typeof(EnemyData), Names("Id", "DisplayNameKey", "DisplayName", "Rank", "Realm", "SubStage", "MaxHp", "Attack", "Defense", "MoveSpeed", "AggroRange", "AttackRange", "DisengageRange", "AttackInterval", "CultivationXpReward", "MinSpiritStones", "MaxSpiritStones", "LootTable", "SkillIds", "BossPhases", "Prefab") },
                { typeof(LootEntry), Names("ItemId", "MinCount", "MaxCount", "DropChance") },
                { typeof(BossPhase), Names("PhaseIndex", "HpThreshold", "SkillIds", "PhaseVfxId", "InvulnerableDuringTransition", "Telegraphs") },
                { typeof(BossSkillTelegraph), Names("SkillId", "Shape", "Duration", "RadiusOrLength", "Angle", "VfxId", "RecoverStun", "Interruptible") },
                { typeof(NPCData), Names("Id", "DisplayNameKey", "DisplayName", "DefaultDialogueId", "FactionId", "IsVendor", "VendorItemIds", "AffectionMilestones", "Prefab") },
                { typeof(AffectionMilestone), Names("RequiredAffection", "MilestoneId", "UnlockDialogueId", "UnlockQuestId") },
                { typeof(CraftRecipeData), Names("Id", "DisplayNameKey", "DisplayName", "CraftType", "RequiredCraftLevel", "BaseSuccessRate", "CraftTime", "Ingredients", "SuccessResult", "FailResult") },
                { typeof(CraftIngredient), Names("ItemId", "Count", "ConsumedOnFail") },
                { typeof(CraftResult), Names("ItemId", "MinCount", "MaxCount") },
                { typeof(MapData), Names("Id", "DisplayName", "SceneName", "RecommendedRealm", "TeleportPointIds", "WeatherPool", "AllowFlight", "DefaultBgm") },
                { typeof(WeatherWeight), Names("Weather", "Weight") },
                { typeof(DialogueData), Names("Id", "Nodes") },
                { typeof(DialogueNode), Names("NodeId", "SpeakerNameKey", "SpeakerName", "TextKey", "Text", "Choices", "NextNodeId", "QuestOfferId", "QuestTurnInId", "End") },
                { typeof(DialogueChoice), Names("TextKey", "Text", "NextNodeId", "RequiredAffection", "RequiredQuestId", "SetFlag") },
                { typeof(AchievementData), Names("Id", "DisplayNameKey", "DisplayName", "DescriptionKey", "Description", "TriggerType", "TargetId", "RequiredValue", "RewardTitleId", "RewardItems", "RewardSpiritStones") },
                { typeof(TitleData), Names("Id", "DisplayNameKey", "DisplayName", "DescriptionKey", "Description", "Bonus", "ShowInNameplate") },
                { typeof(MountData), Names("Id", "DisplayName", "SpeedMultiplier", "CanFly", "RequiredRealm", "Prefab") },
                { typeof(StatusEffectData), Names("Id", "DisplayNameKey", "DisplayName", "Duration", "MaxStacks", "IsDebuff", "AuraElement", "DotDamagePerSecond", "DotBaseDamageMultiplier", "DotInterval", "DotType", "MoveSpeedMod", "AttackMod", "DamageDealtMod", "DefenseMod", "Stun", "Root", "Silence", "ReapplyCooldown", "PromoteAtMaxStacksStatusId") },
                { typeof(SerendipityData), Names("Id", "MapId", "TriggerId", "OnceOnly", "RequiredQuestId", "RequiredRealm", "Rewards", "DialogueId", "WorldFlag") },
                { typeof(DamageInfo), Names("Target", "Source", "Amount", "Type", "IsCritical", "IsKillingBlow", "HitPoint", "Element", "SkillId", "Reaction", "ReactionMultiplier", "HitstopSeconds", "HitstunSeconds") },
                { typeof(DamageRequest), Names("Source", "BaseDamage", "Type", "Element", "Multiplier", "CanCrit", "SkillId", "IgnoreAttackScaling", "StatusOnHitId", "StatusChance", "HitstopSeconds", "HitstunSeconds") },
                { typeof(HealInfo), Names("Target", "Amount", "SourceId") },
                { typeof(PlayerDodgeInfo), Names("Player", "Direction") },
                { typeof(LockOnInfo), Names("Player", "Target", "Locked") },
                { typeof(StatusEffectInfo), Names("Target", "Source", "StatusId", "Stacks", "RemainingDuration", "Change") },
                { typeof(ElementReactionInfo), Names("Target", "Source", "Reaction", "AttackElement", "ExistingStatusId", "DamageMultiplier", "SpreadTargetCount") },
                { typeof(DeathInfo), Names("Victim", "Killer", "Position", "LastHitSkillId") },
                { typeof(EnemyDeathInfo), Names("EnemyId", "Rank", "Victim", "Killer", "Position") },
                { typeof(XpGainInfo), Names("Amount", "Source", "MultiplierApplied") },
                { typeof(BreakthroughBlocker), Names("Code", "MessageKey", "RelatedItemId", "AcquisitionHintKeys") },
                { typeof(RealmChangeInfo), Names("PrevRealm", "NewRealm", "PrevSubStage", "NewSubStage", "Success", "SuccessRate") },
                { typeof(ItemAcquireInfo), Names("ItemId", "Count", "Source") },
                { typeof(ItemUseInfo), Names("ItemId", "SlotIndex") },
                { typeof(CurrencyChangeInfo), Names("Previous", "Current", "Delta") },
                { typeof(EquipmentChangeInfo), Names("Slot", "OldItemId", "NewItemId") },
                { typeof(EquipmentUpgradeInfo), Names("Slot", "ItemId", "NewRefineLevel", "Success") },
                { typeof(SkillInfo), Names("SkillId", "Level") },
                { typeof(SkillCastInfo), Names("SkillId", "Origin", "TargetPoint", "TargetActor") },
                { typeof(SkillUpgradeInfo), Names("SkillId", "NewLevel") },
                { typeof(QuestInfo), Names("QuestId", "Status") },
                { typeof(QuestProgressInfo), Names("QuestId", "ObjectiveIndex", "Current", "Required") },
                { typeof(DailyQuestProgressInfo), Names("QuestId", "Current", "Required") },
                { typeof(DailyQuestResetInfo), Names("CycleStartedUtc", "NextResetUtc") },
                { typeof(DailyQuestClaimInfo), Names("QuestId") },
                { typeof(DialogueInfo), Names("NpcId", "DialogueId", "Cancelled") },
                { typeof(CraftResultInfo), Names("RecipeId", "ResultItemId", "ResultCount", "Success") },
                { typeof(ShopOpenInfo), Names("VendorId") },
                { typeof(ShopTransactionInfo), Names("VendorId", "Type", "ItemId", "Count", "SpiritStonesDelta", "InventorySlot") },
                { typeof(AffectionInfo), Names("NpcId", "OldValue", "NewValue", "MilestoneId") },
                { typeof(AchievementInfo), Names("Id", "DisplayName", "Description") },
                { typeof(TitleInfo), Names("TitleId", "Equipped") },
                { typeof(FactionReputationInfo), Names("FactionId", "Previous", "Current", "OldRank", "NewRank") },
                { typeof(DayNightInfo), Names("IsNight", "TimeOfDay") },
                { typeof(WeatherInfo), Names("Weather", "Intensity") },
                { typeof(MapInfo), Names("MapId", "SpawnId") },
                { typeof(BossPhaseInfo), Names("BossId", "OldPhase", "NewPhase", "HpPercent") },
                { typeof(MountInfo), Names("MountId", "Mounted") },
                { typeof(FlightInfo), Names("IsFlying") },
                { typeof(SerendipityInfo), Names("SerendipityId", "MapId", "WorldFlag") },
                { typeof(TutorialPromptInfo), Names("TutorialId", "StepId", "LocalizationKey", "DefaultValue", "Duration", "IsForced", "CanDismiss", "FocusRectNormalized") },
                { typeof(ToastInfo), Names("LocalizationKey", "DefaultValue", "Duration") },
                { typeof(GameStateInfo), Names("Prev", "Next") }
            };

            foreach (var pair in schemas)
            {
                string[] actual = pair.Key
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(field => field.Name)
                    .ToArray();

                CollectionAssert.AreEquivalent(pair.Value, actual, pair.Key.FullName);
                Assert.That(actual.Length, Is.EqualTo(pair.Value.Length), pair.Key.FullName);

                if (!typeof(ScriptableObject).IsAssignableFrom(pair.Key))
                {
                    Assert.That(pair.Key.IsSerializable, Is.True, pair.Key.FullName);
                }
            }
        }

        [Test]
        public void SchemaDefaults_AreSafeAndMatchDocumentedValues()
        {
            var assets = new List<ScriptableObject>();

            try
            {
                var item = Create<ItemData>(assets);
                Assert.That(item.MaxStack, Is.EqualTo(99));
                Assert.That(item.UseEffects, Is.Not.Null.And.Empty);
                Assert.That(item.AcquisitionHintKeys, Is.Not.Null.And.Empty);

                var equipment = Create<EquipmentData>(assets);
                Assert.That(equipment.BaseStats, Is.Not.Null);
                Assert.That(equipment.MaxRefineLevel, Is.EqualTo(10));
                Assert.That(equipment.MaxDurability, Is.EqualTo(100));
                Assert.That(equipment.BaseStats.CritDamage, Is.EqualTo(1.5f));

                var skill = Create<SkillData>(assets);
                Assert.That(skill.MaxLevel, Is.EqualTo(5));
                Assert.That(skill.StatusChance, Is.EqualTo(1f));
                Assert.That(skill.PreferredRoots, Is.Not.Null.And.Empty);
                Assert.That(skill.LevelTable, Is.Not.Null.And.Empty);

                var skillRuntime = new SkillRuntime();
                Assert.That(skillRuntime.SkillId, Is.Empty);
                Assert.That(skillRuntime.Level, Is.EqualTo(1));
                var skillSave = new SkillSaveData();
                Assert.That(skillSave.Learned, Is.Not.Null.And.Empty);
                Assert.That(skillSave.EquippedIds, Has.Length.EqualTo(4));

                var mountSave = new MountSaveData();
                Assert.That(mountSave.UnlockedMountIds, Is.Not.Null.And.Empty);
                Assert.That(mountSave.SelectedMountId, Is.Empty);
                var achievementSave = new AchievementSaveData();
                Assert.That(achievementSave.Progress, Is.Not.Null.And.Empty);
                Assert.That(achievementSave.UnlockedIds, Is.Not.Null.And.Empty);
                var titleSave = new TitleSaveData();
                Assert.That(titleSave.UnlockedTitleIds, Is.Not.Null.And.Empty);
                Assert.That(titleSave.ActiveTitleId, Is.Empty);

                var questSave = new QuestSaveData();
                Assert.That(questSave.Quests, Is.Not.Null.And.Empty);
                var questRuntime = new QuestRuntimeState();
                Assert.That(questRuntime.QuestId, Is.Empty);
                Assert.That(questRuntime.ObjectiveProgress, Is.Not.Null.And.Empty);

                var blocker = new BreakthroughBlocker
                {
                    Code = "MissingItem",
                    MessageKey = "ui_bt_block_item",
                    RelatedItemId = "item_pill_foundation",
                    AcquisitionHintKeys = new[]
                    {
                        "hint_quest_main_08_yaolao"
                    }
                };
                Assert.That(blocker.AcquisitionHintKeys, Has.Length.EqualTo(1));

                var quest = Create<QuestData>(assets);
                Assert.That(quest.PrerequisiteQuestIds, Is.Not.Null.And.Empty);
                Assert.That(quest.Objectives, Is.Not.Null.And.Empty);
                Assert.That(quest.AcceptRewards, Is.Null);
                Assert.That(quest.Rewards, Is.Not.Null);
                Assert.That(quest.Rewards.Items, Is.Not.Null.And.Empty);

                var enemy = Create<EnemyData>(assets);
                Assert.That(enemy.LootTable, Is.Not.Null.And.Empty);
                Assert.That(enemy.SkillIds, Is.Not.Null.And.Empty);
                Assert.That(enemy.BossPhases, Is.Not.Null.And.Empty);

                var npc = Create<NPCData>(assets);
                Assert.That(npc.VendorItemIds, Is.Not.Null.And.Empty);
                Assert.That(npc.AffectionMilestones, Is.Not.Null.And.Empty);

                var recipe = Create<CraftRecipeData>(assets);
                Assert.That(recipe.Ingredients, Is.Not.Null.And.Empty);
                Assert.That(recipe.SuccessResult, Is.Not.Null);
                Assert.That(recipe.FailResult, Is.Null);
                Assert.That(new CraftIngredient().ConsumedOnFail, Is.True);

                var map = Create<MapData>(assets);
                Assert.That(map.TeleportPointIds, Is.Not.Null.And.Empty);
                Assert.That(map.WeatherPool, Is.Not.Null.And.Empty);

                var dialogue = Create<DialogueData>(assets);
                Assert.That(dialogue.Nodes, Is.Not.Null.And.Empty);
                Assert.That(new DialogueNode().Choices, Is.Not.Null.And.Empty);

                var achievement = Create<AchievementData>(assets);
                Assert.That(achievement.RewardItems, Is.Not.Null.And.Empty);

                var title = Create<TitleData>(assets);
                Assert.That(title.Bonus, Is.Not.Null);
                Assert.That(title.ShowInNameplate, Is.True);
                Assert.That(title.Bonus.CritDamage, Is.Zero);

                Create<MountData>(assets);

                var status = Create<StatusEffectData>(assets);
                Assert.That(status.MaxStacks, Is.EqualTo(1));
                Assert.That(status.DotInterval, Is.EqualTo(1f));

                var serendipity = Create<SerendipityData>(assets);
                Assert.That(serendipity.OnceOnly, Is.True);
                Assert.That(serendipity.RequiredRealm, Is.EqualTo(RealmType.Mortal));
                Assert.That(serendipity.Rewards, Is.Not.Null);
                Assert.That(serendipity.Rewards.Items, Is.Not.Null.And.Empty);
            }
            finally
            {
                foreach (ScriptableObject asset in assets)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }

        [Test]
        public void StatBlock_AddAndMultiply_OperateOnEveryPublicStat()
        {
            FieldInfo[] fields = typeof(StatBlock)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var left = new StatBlock();
            var right = new StatBlock();

            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].SetValue(left, i + 1f);
                fields[i].SetValue(right, (i + 1f) * 10f);
            }

            StatBlock sum = left + right;
            StatBlock scaled = left.Multiply(2.5f);
            StatBlock nullSafe = (StatBlock)null + left;

            for (int i = 0; i < fields.Length; i++)
            {
                float leftValue = i + 1f;
                Assert.That((float)fields[i].GetValue(sum), Is.EqualTo(leftValue * 11f).Within(0.0001f), fields[i].Name);
                Assert.That((float)fields[i].GetValue(scaled), Is.EqualTo(leftValue * 2.5f).Within(0.0001f), fields[i].Name);
                Assert.That((float)fields[i].GetValue(nullSafe), Is.EqualTo(leftValue).Within(0.0001f), fields[i].Name);
            }
        }

        private static string[] Names(params string[] values)
        {
            return values;
        }

        private static T Create<T>(ICollection<ScriptableObject> assets)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
