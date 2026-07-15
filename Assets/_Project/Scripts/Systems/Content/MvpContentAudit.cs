using System;
using System.Collections.Generic;
using Wendao.Data;
using Wendao.Systems.Achievement;
using Wendao.Systems.Crafting;
using Wendao.Systems.Enemy;
using Wendao.Systems.Feedback;
using Wendao.Systems.Inventory;
using Wendao.Systems.Quest;
using Wendao.Systems.Skill;
using Wendao.Systems.Title;
using Wendao.Systems.World;

namespace Wendao.Systems.Content
{
    public sealed class MvpContentAuditResult
    {
        private readonly List<string> _issues = new List<string>();

        public IReadOnlyList<string> Issues => _issues;
        public bool IsValid => _issues.Count == 0;

        internal void Add(string issue)
        {
            if (!string.IsNullOrWhiteSpace(issue))
            {
                _issues.Add(issue);
            }
        }
    }

    public static class MvpContentAudit
    {
        public const int RequiredMainQuestCount = 10;
        public const int RequiredSerendipityCount = 4;
        public const int RequiredAchievementCount = 10;
        public const int RequiredTitleCount = 5;
        public const int RequiredRecipeCount = 4;

        public static MvpContentAuditResult Evaluate(ConfigDatabase database)
        {
            var result = new MvpContentAuditResult();
            if (database == null)
            {
                result.Add("ConfigDatabase is missing.");
                return result;
            }

            AuditMainQuests(database, result);
            AuditItemsAndRecipes(database, result);
            AuditSkillsAndEnemies(database, result);
            AuditAchievementsAndTitles(database, result);
            AuditSerendipities(database, result);
            AuditFeedbackIds(result);
            AuditMapIds(result);
            return result;
        }

        private static void AuditMainQuests(
            ConfigDatabase database,
            MvpContentAuditResult result)
        {
            if (QuestContentIds.MainChapterOne.Length != RequiredMainQuestCount)
            {
                result.Add("Main quest ID table must contain ten entries.");
            }

            for (int index = 0; index < QuestContentIds.MainChapterOne.Length; index++)
            {
                string id = QuestContentIds.MainChapterOne[index];
                QuestData quest = database.GetQuest(id);
                if (quest == null)
                {
                    result.Add("Missing main quest: " + id);
                    continue;
                }

                RequireLocalized(
                    result,
                    id,
                    quest.DisplayNameKey,
                    quest.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    id,
                    quest.DescriptionKey,
                    quest.Description,
                    "description");
                if (index > 0
                    && !Contains(
                        quest.PrerequisiteQuestIds,
                        QuestContentIds.MainChapterOne[index - 1]))
                {
                    result.Add(id + " does not depend on the previous main quest.");
                }

                QuestObjective[] objectives = quest.Objectives
                    ?? Array.Empty<QuestObjective>();
                if (objectives.Length == 0)
                {
                    result.Add(id + " has no objective.");
                }

                for (int objectiveIndex = 0;
                     objectiveIndex < objectives.Length;
                     objectiveIndex++)
                {
                    QuestObjective objective = objectives[objectiveIndex];
                    if (objective == null)
                    {
                        result.Add(id + " has a null objective.");
                        continue;
                    }

                    RequireLocalized(
                        result,
                        id + "/objective-" + objectiveIndex,
                        objective.DescriptionKey,
                        objective.Description,
                        "text");
                }

                AuditDialogue(database, quest.StartDialogueId, result);
                AuditDialogue(database, quest.CompleteDialogueId, result);
                if (database.GetNpc(quest.StartNpcId) == null)
                {
                    result.Add(id + " has an unresolved start NPC.");
                }

                if (database.GetNpc(quest.TurnInNpcId) == null)
                {
                    result.Add(id + " has an unresolved turn-in NPC.");
                }
            }

            foreach (QuestData quest in database.Quests)
            {
                if (quest == null)
                {
                    continue;
                }

                RequireLocalized(
                    result,
                    quest.Id,
                    quest.DisplayNameKey,
                    quest.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    quest.Id,
                    quest.DescriptionKey,
                    quest.Description,
                    "description");
            }

            foreach (NPCData npc in database.Npcs)
            {
                if (npc != null)
                {
                    RequireLocalized(
                        result,
                        npc.Id,
                        npc.DisplayNameKey,
                        npc.DisplayName,
                        "name");
                }
            }
        }

        private static void AuditDialogue(
            ConfigDatabase database,
            string dialogueId,
            MvpContentAuditResult result)
        {
            DialogueData dialogue = database.GetDialogue(dialogueId);
            if (dialogue == null)
            {
                result.Add("Missing dialogue: " + dialogueId);
                return;
            }

            DialogueNode[] nodes = dialogue.Nodes ?? Array.Empty<DialogueNode>();
            if (nodes.Length == 0)
            {
                result.Add(dialogueId + " has no dialogue node.");
                return;
            }

            for (int index = 0; index < nodes.Length; index++)
            {
                DialogueNode node = nodes[index];
                if (node == null)
                {
                    result.Add(dialogueId + " has a null dialogue node.");
                    continue;
                }

                RequireLocalized(
                    result,
                    dialogueId + "/node-" + index,
                    node.TextKey,
                    node.Text,
                    "text");
                if (!string.IsNullOrWhiteSpace(node.SpeakerName))
                {
                    RequireLocalized(
                        result,
                        dialogueId + "/node-" + index,
                        node.SpeakerNameKey,
                        node.SpeakerName,
                        "speaker");
                }

                DialogueChoice[] choices = node.Choices
                    ?? Array.Empty<DialogueChoice>();
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    DialogueChoice choice = choices[choiceIndex];
                    if (choice != null)
                    {
                        RequireLocalized(
                            result,
                            dialogueId + "/choice-" + choiceIndex,
                            choice.TextKey,
                            choice.Text,
                            "text");
                    }
                }
            }
        }

        private static void AuditItemsAndRecipes(
            ConfigDatabase database,
            MvpContentAuditResult result)
        {
            foreach (ItemData item in database.Items)
            {
                if (item == null)
                {
                    continue;
                }

                RequireLocalized(
                    result,
                    item.Id,
                    item.DisplayNameKey,
                    item.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    item.Id,
                    item.DescriptionKey,
                    item.Description,
                    "description");
            }

            foreach (EquipmentData equipment in database.Equipment)
            {
                if (equipment != null)
                {
                    RequireLocalized(
                        result,
                        equipment.Id,
                        equipment.DisplayNameKey,
                        equipment.DisplayName,
                        "name");
                }
            }

            if (AlchemyContentIds.Recipes.Length != RequiredRecipeCount)
            {
                result.Add("Alchemy recipe ID table must contain four entries.");
            }

            foreach (string recipeId in AlchemyContentIds.Recipes)
            {
                CraftRecipeData recipe = database.GetRecipe(recipeId);
                if (recipe == null)
                {
                    result.Add("Missing recipe: " + recipeId);
                    continue;
                }

                RequireLocalized(
                    result,
                    recipe.Id,
                    recipe.DisplayNameKey,
                    recipe.DisplayName,
                    "name");
                CraftIngredient[] ingredients = recipe.Ingredients
                    ?? Array.Empty<CraftIngredient>();
                if (ingredients.Length == 0)
                {
                    result.Add(recipe.Id + " has no ingredients.");
                }

                foreach (CraftIngredient ingredient in ingredients)
                {
                    if (ingredient == null
                        || ingredient.Count <= 0
                        || database.GetItem(ingredient.ItemId) == null)
                    {
                        result.Add(recipe.Id + " has an unresolved ingredient.");
                    }
                }

                if (recipe.SuccessResult == null
                    || database.GetItem(recipe.SuccessResult.ItemId) == null)
                {
                    result.Add(recipe.Id + " has an unresolved result item.");
                }
            }
        }

        private static void AuditSkillsAndEnemies(
            ConfigDatabase database,
            MvpContentAuditResult result)
        {
            foreach (string skillId in SkillContentIds.All)
            {
                SkillData skill = database.GetSkill(skillId);
                if (skill == null)
                {
                    result.Add("Missing skill: " + skillId);
                    continue;
                }

                RequireLocalized(
                    result,
                    skill.Id,
                    skill.DisplayNameKey,
                    skill.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    skill.Id,
                    skill.DescriptionKey,
                    skill.Description,
                    "description");
            }

            foreach (EnemyData enemy in database.Enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                RequireLocalized(
                    result,
                    enemy.Id,
                    enemy.DisplayNameKey,
                    enemy.DisplayName,
                    "name");
                if (enemy.MaxSpiritStones < enemy.MinSpiritStones)
                {
                    result.Add(enemy.Id + " has an invalid spirit-stone range.");
                }
            }
        }

        private static void AuditAchievementsAndTitles(
            ConfigDatabase database,
            MvpContentAuditResult result)
        {
            if (database.Achievements.Count != RequiredAchievementCount)
            {
                result.Add("MVP must register exactly ten achievements.");
            }

            foreach (AchievementData achievement in database.Achievements)
            {
                if (achievement == null)
                {
                    continue;
                }

                RequireLocalized(
                    result,
                    achievement.Id,
                    achievement.DisplayNameKey,
                    achievement.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    achievement.Id,
                    achievement.DescriptionKey,
                    achievement.Description,
                    "description");
                if (!string.IsNullOrWhiteSpace(achievement.RewardTitleId)
                    && database.GetTitle(achievement.RewardTitleId) == null)
                {
                    result.Add(achievement.Id + " has an unresolved title reward.");
                }
            }

            if (database.Titles.Count != RequiredTitleCount)
            {
                result.Add("MVP must register exactly five titles.");
            }

            foreach (TitleData title in database.Titles)
            {
                if (title == null)
                {
                    continue;
                }

                RequireLocalized(
                    result,
                    title.Id,
                    title.DisplayNameKey,
                    title.DisplayName,
                    "name");
                RequireLocalized(
                    result,
                    title.Id,
                    title.DescriptionKey,
                    title.Description,
                    "description");
            }
        }

        private static void AuditSerendipities(
            ConfigDatabase database,
            MvpContentAuditResult result)
        {
            if (SerendipityContentIds.All.Length != RequiredSerendipityCount)
            {
                result.Add("Serendipity ID table must contain four entries.");
            }

            foreach (string id in SerendipityContentIds.All)
            {
                SerendipityData data = database.GetSerendipity(id);
                if (data == null)
                {
                    result.Add("Missing serendipity: " + id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(data.MapId)
                    || string.IsNullOrWhiteSpace(data.TriggerId)
                    || string.IsNullOrWhiteSpace(data.WorldFlag))
                {
                    result.Add(id + " has incomplete stable IDs.");
                }

                AuditDialogue(database, data.DialogueId, result);
            }
        }

        private static void AuditFeedbackIds(MvpContentAuditResult result)
        {
            if (AudioContentIds.Bgm.Length != 5
                || AudioContentIds.Sfx.Length != 10
                || AudioContentIds.Ambience.Length != 3
                || VfxContentIds.All.Length != 12)
            {
                result.Add("Audio/VFX MVP ID counts do not match section 12.");
            }
        }

        private static void AuditMapIds(MvpContentAuditResult result)
        {
            if (GatheringContentIds.QingshiNodes.Length
                    < QingshiGreyboxFactory.RequiredGatherableCount
                || GatheringContentIds.CangwuNodes.Length
                    < CangwuGreyboxFactory.RequiredGatherableCount
                || CangwuGreyboxFactory.RequiredSpawnPointCount < 5
                || BlackwindDungeonFactory.RequiredChestCount < 3)
            {
                result.Add("Map density ID tables do not meet section 11.");
            }

            string[] chestIds =
            {
                MapContentIds.CangwuMistChest,
                MapContentIds.CangwuCaveChest,
                MapContentIds.CangwuTerraceChest,
                MapContentIds.BlackwindEntryChest,
                MapContentIds.BlackwindBranchChest,
                MapContentIds.BlackwindDeepChest
            };
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in chestIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                {
                    result.Add("Map chest IDs must be stable and unique.");
                }
            }
        }

        private static void RequireLocalized(
            MvpContentAuditResult result,
            string contentId,
            string key,
            string defaultValue,
            string field)
        {
            if (string.IsNullOrWhiteSpace(key)
                || string.IsNullOrWhiteSpace(defaultValue))
            {
                result.Add(contentId + " has incomplete localized " + field + ".");
            }
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (string.Equals(value, expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class MvpBalanceAuditResult
    {
        public int MainQuestSpiritStones;
        public int GuaranteedAchievementSpiritStones;
        public int MandatoryDropMinimum;
        public int MandatoryDropMaximum;
        public int CompletionMinimum;
        public int CompletionMaximum;
        public float FireXpBeforeFoundation;
        public float RequiredXpBeforeFoundation;
        public float FireXpBeforeGoldenCore;
        public float RequiredXpBeforeGoldenCore;
        public int EastField15MinuteMinimumStones;
        public int EastField15MinuteMaximumStones;
        public float EastField15MinuteCultivationXp;
        public int EstimatedMainStoryMinutes;
        public bool IsValid;
        public string FailureReason = string.Empty;
    }

    public static class MvpBalanceAudit
    {
        public const float FireCultivationMultiplier = 1.1f;
        public const int CompletionMinimumTarget = 50;
        public const int CompletionMaximumTarget = 300;
        public const int MainStoryMinimumMinutes = 120;
        public const int MainStoryMaximumMinutes = 240;
        public const int EastFieldRouteNormalKills = 7;

        public static MvpBalanceAuditResult Evaluate(ConfigDatabase database)
        {
            var result = new MvpBalanceAuditResult();
            if (database == null)
            {
                result.FailureReason = "ConfigDatabase is missing.";
                return result;
            }

            float rawXpBeforeFoundation = 0f;
            for (int index = 0; index < 7; index++)
            {
                QuestData quest = database.GetQuest(
                    QuestContentIds.MainChapterOne[index]);
                if (quest == null)
                {
                    result.FailureReason = "A main quest is missing.";
                    return result;
                }

                rawXpBeforeFoundation += quest.Rewards?.CultivationXp ?? 0f;
            }

            EnemyData wolf = database.GetEnemy(EnemyContentIds.GreyWolf);
            EnemyData elite = database.GetEnemy(EnemyContentIds.EliteWolf);
            if (wolf == null || elite == null)
            {
                result.FailureReason = "Mandatory enemy balance data is missing.";
                return result;
            }

            rawXpBeforeFoundation += wolf.CultivationXpReward * 6f;
            rawXpBeforeFoundation += elite.CultivationXpReward;
            result.FireXpBeforeFoundation =
                rawXpBeforeFoundation * FireCultivationMultiplier;
            result.RequiredXpBeforeFoundation = SumRealmXp(
                database.GetRealm((int)RealmType.QiCondensation));

            QuestData foundationReward = database.GetQuest(
                QuestContentIds.MainFoundationBreakthrough);
            result.FireXpBeforeGoldenCore =
                (foundationReward?.Rewards?.CultivationXp ?? 0f)
                * FireCultivationMultiplier;
            result.RequiredXpBeforeGoldenCore = SumRealmXp(
                database.GetRealm((int)RealmType.Foundation));

            foreach (string questId in QuestContentIds.MainChapterOne)
            {
                QuestData quest = database.GetQuest(questId);
                result.MainQuestSpiritStones +=
                    quest?.Rewards?.SpiritStones ?? 0;
            }

            result.GuaranteedAchievementSpiritStones =
                (database.GetAchievement(AchievementContentIds.RealmFoundation)
                    ?.RewardSpiritStones ?? 0)
                + (database.GetAchievement(AchievementContentIds.RealmGoldenCore)
                    ?.RewardSpiritStones ?? 0);

            const int mandatoryNormalKills = 14;
            const int mandatoryEliteKills = 2;
            result.MandatoryDropMinimum =
                mandatoryNormalKills * wolf.MinSpiritStones
                + mandatoryEliteKills * elite.MinSpiritStones;
            result.MandatoryDropMaximum =
                mandatoryNormalKills * wolf.MaxSpiritStones
                + mandatoryEliteKills * elite.MaxSpiritStones;
            result.CompletionMinimum = result.MainQuestSpiritStones
                + result.GuaranteedAchievementSpiritStones
                + result.MandatoryDropMinimum;
            result.CompletionMaximum = result.MainQuestSpiritStones
                + result.GuaranteedAchievementSpiritStones
                + result.MandatoryDropMaximum;

            result.EastField15MinuteMinimumStones =
                EastFieldRouteNormalKills * wolf.MinSpiritStones;
            result.EastField15MinuteMaximumStones =
                EastFieldRouteNormalKills * wolf.MaxSpiritStones;
            result.EastField15MinuteCultivationXp =
                EastFieldRouteNormalKills
                * wolf.CultivationXpReward
                * FireCultivationMultiplier;

            const int questAndDialogueMinutes = 60;
            const int traversalMinutes = 35;
            const int gatherAndCraftMinutes = 15;
            const int requiredCombatMinutes = 47;
            const int breakthroughAndMenusMinutes = 18;
            const int explorationAndRetryBufferMinutes = 20;
            result.EstimatedMainStoryMinutes = questAndDialogueMinutes
                + traversalMinutes
                + gatherAndCraftMinutes
                + requiredCombatMinutes
                + breakthroughAndMenusMinutes
                + explorationAndRetryBufferMinutes;

            bool shopValid = IsShopSinkValid(
                database.GetItem(InventoryContentIds.HealPotion01),
                10)
                && IsShopSinkValid(
                    database.GetItem(InventoryContentIds.RefineStone),
                    25)
                && IsShopSinkValid(
                    database.GetItem(InventoryContentIds.IronSword),
                    80);
            result.IsValid = result.CompletionMinimum >= CompletionMinimumTarget
                && result.CompletionMaximum <= CompletionMaximumTarget
                && result.FireXpBeforeFoundation + 0.01f
                    >= result.RequiredXpBeforeFoundation
                && result.FireXpBeforeGoldenCore + 0.01f
                    >= result.RequiredXpBeforeGoldenCore
                && result.EstimatedMainStoryMinutes >= MainStoryMinimumMinutes
                && result.EstimatedMainStoryMinutes <= MainStoryMaximumMinutes
                && shopValid;
            if (!result.IsValid)
            {
                result.FailureReason = "MVP pacing, completion balance, or shop sink is outside its target.";
            }

            return result;
        }

        private static bool IsShopSinkValid(ItemData item, int expectedBuyPrice)
        {
            return item != null
                && item.BuyPrice == expectedBuyPrice
                && item.SellPrice == expectedBuyPrice / 4;
        }

        private static float SumRealmXp(RealmEntry realm)
        {
            float total = 0f;
            float[] values = realm?.XpPerSubStage ?? Array.Empty<float>();
            foreach (float value in values)
            {
                total += Math.Max(0f, value);
            }

            return total;
        }
    }
}
