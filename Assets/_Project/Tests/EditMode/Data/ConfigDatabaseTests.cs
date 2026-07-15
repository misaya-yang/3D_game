using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wendao.Data;
using Object = UnityEngine.Object;

namespace Wendao.Tests.EditMode.Data
{
    public sealed class ConfigDatabaseTests
    {
        private ConfigDatabase _database;

        [SetUp]
        public void SetUp()
        {
            var gameObject = new GameObject("ConfigDatabase Test");
            _database = gameObject.AddComponent<ConfigDatabase>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_database != null)
            {
                Object.DestroyImmediate(_database.gameObject);
            }
        }

        [Test]
        public void DefaultConfigs_LoadAllAuthoritativeTables()
        {
            string configDirectory = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "Config");

            Assert.That(
                _database.TryLoadAllFromDirectory(configDirectory),
                Is.True,
                _database.LastError);
            Assert.That(_database.IsSafeMode, Is.False);

            RealmEntry qi = _database.GetRealm(1);
            Assert.That(qi, Is.Not.Null);
            Assert.That(qi.SubStages, Is.EqualTo(9));
            Assert.That(qi.XpPerSubStage[0], Is.EqualTo(100f));
            Assert.That(qi.BreakthroughToNext.RequiredItemId, Is.EqualTo("item_pill_foundation"));

            SpiritRootEntry water = _database.GetSpiritRoot(SpiritRootType.Water);
            Assert.That(water.ElementBonus["Water"], Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(water.ElementBonus["Ice"], Is.EqualTo(0.10f).Within(0.0001f));

            SpiritRootEntry waste = _database.GetSpiritRoot(SpiritRootType.Waste);
            Assert.That(waste.Passives, Is.Not.Null);
            Assert.That(waste.Passives.BodyPotionMul, Is.EqualTo(1.25f));
            Assert.That(_database.Body.Levels, Has.Length.EqualTo(5));
            Assert.That(_database.Craft.Alchemy, Has.Length.EqualTo(10));
            Assert.That(_database.GetItem("item_potion_heal_01"), Is.Not.Null);
            ItemData bodyPotion = _database.GetItem("item_potion_body_01");
            Assert.That(bodyPotion, Is.Not.Null);
            Assert.That(bodyPotion.MaxStack, Is.EqualTo(10));
            Assert.That(bodyPotion.UseEffects, Has.Length.EqualTo(1));
            Assert.That(
                bodyPotion.UseEffects[0].EffectType,
                Is.EqualTo(UseEffectType.AddBodyXp));
            Assert.That(bodyPotion.UseEffects[0].Value, Is.EqualTo(200f));
            ItemData foundationPill = _database.GetItem("item_pill_foundation");
            Assert.That(foundationPill, Is.Not.Null);
            Assert.That(foundationPill.Type, Is.EqualTo(ItemType.Consumable));
            Assert.That(foundationPill.MaxStack, Is.EqualTo(5));
            Assert.That(
                foundationPill.AcquisitionHintKeys,
                Does.Contain("hint_quest_main_08_yaolao"));
            ItemData goldenCorePill = _database.GetItem("item_pill_goldencore");
            Assert.That(goldenCorePill, Is.Not.Null);
            Assert.That(goldenCorePill.MaxStack, Is.EqualTo(5));
            Assert.That(
                _database.GetEquipment("eq_weapon_wood_sword").BaseStats.Attack,
                Is.EqualTo(8f));
            SkillData qiBolt = _database.GetSkill("skill_basic_qi_bolt");
            Assert.That(qiBolt, Is.Not.Null);
            Assert.That(qiBolt.BaseDamage, Is.EqualTo(30f));
            Assert.That(qiBolt.BaseManaCost, Is.EqualTo(10f));
            Assert.That(qiBolt.BaseCooldown, Is.EqualTo(1.5f));
            Assert.That(_database.GetItem("item_skill_scroll"), Is.Not.Null);
            Assert.That(_database.GetItem("item_mat_qingxin_grass"), Is.Not.Null);
            Assert.That(_database.GetItem("item_mat_spirit_dust"), Is.Not.Null);
            Assert.That(_database.GetItem("item_mat_beast_core_1"), Is.Not.Null);
            Assert.That(
                _database.GetItem("item_potion_mana_01").UseEffects[0].EffectType,
                Is.EqualTo(UseEffectType.RestoreMana));
            Assert.That(
                _database.GetItem("item_potion_xp_01").UseEffects[0].EffectType,
                Is.EqualTo(UseEffectType.AddCultivationXp));
            Assert.That(_database.GetRecipe("recipe_heal_01"), Is.Not.Null);
            Assert.That(_database.GetRecipe("recipe_mana_01"), Is.Not.Null);
            Assert.That(_database.GetRecipe("recipe_body_01"), Is.Not.Null);
            Assert.That(_database.GetRecipe("recipe_xp_01"), Is.Not.Null);
            string[] builtInSkillIds =
            {
                "skill_basic_qi_bolt",
                "skill_fire_ember",
                "skill_ice_needle",
                "skill_lightning_chain",
                "skill_wind_slash",
                "skill_pass_iron_skin",
                "skill_ult_fire_wave"
            };
            foreach (string skillId in builtInSkillIds)
            {
                Assert.That(_database.GetSkill(skillId), Is.Not.Null, skillId);
            }
            QuestData hunt = _database.GetQuest("quest_main_01_02");
            Assert.That(hunt, Is.Not.Null);
            Assert.That(
                hunt.PrerequisiteQuestIds,
                Is.EqualTo(new[] { "quest_main_01_01" }));
            Assert.That(hunt.Objectives[0].TargetId, Is.EqualTo("enemy_wolf_gray"));
            Assert.That(hunt.Objectives[0].RequiredCount, Is.EqualTo(3));
            Assert.That(
                _database.GetDialogue("dlg_main_01_02_start").Nodes,
                Has.Length.EqualTo(4));
            Assert.That(_database.GetNpc("npc_yaolao"), Is.Not.Null);
            for (int step = 1; step <= 10; step++)
            {
                string suffix = step.ToString("00");
                QuestData main = _database.GetQuest("quest_main_01_" + suffix);
                Assert.That(main, Is.Not.Null, suffix);
                Assert.That(
                    _database.GetDialogue("dlg_main_01_" + suffix + "_start"),
                    Is.Not.Null,
                    suffix);
                Assert.That(
                    _database.GetDialogue("dlg_main_01_" + suffix + "_complete"),
                    Is.Not.Null,
                    suffix);
            }

            QuestData goldenCore = _database.GetQuest("quest_main_01_09");
            Assert.That(goldenCore.ObjectivesAreOrdered, Is.True);
            Assert.That(goldenCore.Objectives, Has.Length.EqualTo(3));
            Assert.That(goldenCore.Objectives[0].LatchOnFirstAcquire, Is.True);
            Assert.That(
                goldenCore.AcceptRewards.Items[0].ItemId,
                Is.EqualTo("item_pill_goldencore"));
            Assert.That(_database.GetNpc("npc_cangwu_guard"), Is.Not.Null);
            Assert.That(_database.GetNpc("npc_blackwind_echo"), Is.Not.Null);
            ItemData wolfHair = _database.GetItem("item_mat_wolf_hair");
            Assert.That(wolfHair, Is.Not.Null);
            Assert.That(wolfHair.Type, Is.EqualTo(ItemType.Material));
            Assert.That(wolfHair.MaxStack, Is.EqualTo(99));
            EnemyData greyWolf = _database.GetEnemy("enemy_wolf_gray");
            Assert.That(greyWolf, Is.Not.Null);
            Assert.That(greyWolf.MaxHp, Is.EqualTo(80f));
            Assert.That(greyWolf.Attack, Is.EqualTo(8f));
            Assert.That(greyWolf.CultivationXpReward, Is.EqualTo(15f));
            Assert.That(greyWolf.MinSpiritStones, Is.EqualTo(0));
            Assert.That(greyWolf.MaxSpiritStones, Is.EqualTo(2));
            Assert.That(greyWolf.LootTable, Has.Length.EqualTo(1));
            Assert.That(
                greyWolf.LootTable[0].ItemId,
                Is.EqualTo("item_mat_wolf_hair"));
            Assert.That(greyWolf.LootTable[0].DropChance, Is.EqualTo(0.4f));

            EnemyData eliteWolf = _database.GetEnemy("enemy_wolf_elite");
            Assert.That(eliteWolf, Is.Not.Null);
            Assert.That(eliteWolf.DisplayName, Is.EqualTo("灰爪"));
            Assert.That(eliteWolf.Rank, Is.EqualTo(EnemyRank.Elite));
            Assert.That(eliteWolf.SubStage, Is.EqualTo(6));
            Assert.That(eliteWolf.MaxHp, Is.EqualTo(800f));
            Assert.That(eliteWolf.Attack, Is.EqualTo(28f));
            Assert.That(eliteWolf.CultivationXpReward, Is.EqualTo(120f));
            Assert.That(eliteWolf.MinSpiritStones, Is.EqualTo(8));
            Assert.That(eliteWolf.MaxSpiritStones, Is.EqualTo(15));
            Assert.That(eliteWolf.LootTable, Has.Length.EqualTo(1));
            Assert.That(
                eliteWolf.LootTable[0].ItemId,
                Is.EqualTo("item_mat_beast_core_1"));
            Assert.That(eliteWolf.LootTable[0].DropChance, Is.EqualTo(1f));
            Assert.That(
                eliteWolf.SkillIds,
                Is.EqualTo(new[] { "skill_enemy_wolf_elite_charge" }));

            EnemyData stoneGeneral = _database.GetEnemy(
                "enemy_boss_stone_general");
            Assert.That(stoneGeneral, Is.Not.Null);
            Assert.That(stoneGeneral.DisplayName, Is.EqualTo("黑风石将军"));
            Assert.That(stoneGeneral.Rank, Is.EqualTo(EnemyRank.Boss));
            Assert.That(stoneGeneral.MaxHp, Is.EqualTo(12000f));
            Assert.That(stoneGeneral.Attack, Is.EqualTo(70f));
            Assert.That(stoneGeneral.CultivationXpReward, Is.EqualTo(2000f));
            Assert.That(stoneGeneral.MinSpiritStones, Is.EqualTo(0));
            Assert.That(stoneGeneral.MaxSpiritStones, Is.EqualTo(0));
            Assert.That(stoneGeneral.BossPhases, Has.Length.EqualTo(3));
            Assert.That(
                stoneGeneral.BossPhases[0].SkillIds,
                Is.EqualTo(new[] { "boss_sg_slam", "boss_sg_spike" }));
            Assert.That(
                stoneGeneral.BossPhases[1].SkillIds,
                Is.EqualTo(new[] { "boss_sg_charge", "boss_sg_summon" }));
            Assert.That(
                stoneGeneral.BossPhases[2].SkillIds,
                Is.EqualTo(new[] { "boss_sg_rage_slam" }));
            Assert.That(stoneGeneral.BossPhases[0].Telegraphs,
                Has.Length.EqualTo(2));
            Assert.That(stoneGeneral.BossPhases[1].Telegraphs,
                Has.Length.EqualTo(2));
            Assert.That(stoneGeneral.BossPhases[2].Telegraphs,
                Has.Length.EqualTo(1));
            foreach (BossPhase phase in stoneGeneral.BossPhases)
            {
                Assert.That(
                    Array.TrueForAll(
                        phase.Telegraphs,
                        telegraph => telegraph != null
                            && telegraph.Duration >= 0.6f
                            && telegraph.RecoverStun > 0f
                            && !string.IsNullOrEmpty(telegraph.SkillId)
                            && !string.IsNullOrEmpty(telegraph.VfxId)),
                    Is.True,
                    $"Phase {phase.PhaseIndex} telegraph contract");
            }
            Assert.That(
                _database.GetEnemy("enemy_blackwind_spawn"),
                Is.Not.Null);

            StatusEffectData chill = _database.GetStatusEffect("status_chill");
            Assert.That(chill, Is.Not.Null);
            Assert.That(chill.DisplayNameKey, Is.EqualTo("status_name_chill"));
            Assert.That(chill.MaxStacks, Is.EqualTo(2));
            Assert.That(
                chill.PromoteAtMaxStacksStatusId,
                Is.EqualTo("status_freeze"));
            StatusEffectData freeze = _database.GetStatusEffect("status_freeze");
            Assert.That(freeze.Stun, Is.True);
            Assert.That(freeze.ReapplyCooldown, Is.EqualTo(8f));
            Assert.That(
                _database.GetStatusEffect("status_heart_demon").DamageDealtMod,
                Is.EqualTo(-0.1f));
        }

        [Test]
        public void MissingConfigDirectory_EntersSafeModeWithoutNullProperties()
        {
            Assert.That(
                _database.TryLoadAllFromDirectory(
                    Path.Combine(Path.GetTempPath(), "missing-wendao-config", Guid.NewGuid().ToString("N"))),
                Is.False);

            Assert.That(_database.IsSafeMode, Is.True);
            Assert.That(_database.LastError, Is.Not.Empty);
            Assert.That(_database.Realm, Is.Not.Null);
            Assert.That(_database.SpiritRoot, Is.Not.Null);
            Assert.That(_database.Body, Is.Not.Null);
            Assert.That(_database.Craft, Is.Not.Null);
        }

        [Test]
        public void ContentRegistry_RejectsDuplicatesAndResolvesStableIds()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var equipment = ScriptableObject.CreateInstance<EquipmentData>();
            var skill = ScriptableObject.CreateInstance<SkillData>();
            var recipe = ScriptableObject.CreateInstance<CraftRecipeData>();
            var quest = ScriptableObject.CreateInstance<QuestData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            var npc = ScriptableObject.CreateInstance<NPCData>();
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            var status = ScriptableObject.CreateInstance<StatusEffectData>();
            item.Id = "item_test";
            equipment.Id = "equipment_test";
            skill.Id = "skill_test";
            recipe.Id = "recipe_test";
            quest.Id = "quest_test";
            dialogue.Id = "dialogue_test";
            npc.Id = "npc_test";
            enemy.Id = "enemy_test";
            status.Id = "status_test";

            try
            {
                Assert.That(_database.RegisterItem(item), Is.True);
                Assert.That(_database.RegisterItem(item), Is.False);
                Assert.That(_database.RegisterEquipment(equipment), Is.True);
                Assert.That(_database.RegisterEquipment(equipment), Is.False);
                Assert.That(_database.RegisterSkill(skill), Is.True);
                Assert.That(_database.RegisterRecipe(recipe), Is.True);
                Assert.That(_database.RegisterRecipe(recipe), Is.False);
                Assert.That(_database.RegisterQuest(quest), Is.True);
                Assert.That(_database.RegisterDialogue(dialogue), Is.True);
                Assert.That(_database.RegisterNpc(npc), Is.True);
                Assert.That(_database.RegisterEnemy(enemy), Is.True);
                Assert.That(_database.RegisterEnemy(enemy), Is.False);
                Assert.That(_database.RegisterStatusEffect(status), Is.True);
                Assert.That(_database.RegisterStatusEffect(status), Is.False);
                Assert.That(_database.GetItem("item_test"), Is.SameAs(item));
                Assert.That(
                    _database.GetEquipment("equipment_test"),
                    Is.SameAs(equipment));
                Assert.That(_database.GetSkill("skill_test"), Is.SameAs(skill));
                Assert.That(_database.GetRecipe("recipe_test"), Is.SameAs(recipe));
                Assert.That(_database.GetQuest("quest_test"), Is.SameAs(quest));
                Assert.That(
                    _database.GetDialogue("dialogue_test"),
                    Is.SameAs(dialogue));
                Assert.That(_database.GetNpc("npc_test"), Is.SameAs(npc));
                Assert.That(_database.GetEnemy("enemy_test"), Is.SameAs(enemy));
                Assert.That(
                    _database.GetStatusEffect("status_test"),
                    Is.SameAs(status));
                Assert.That(_database.GetItem("missing"), Is.Null);
                Assert.That(_database.GetEnemy("missing"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(equipment);
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(quest);
                Object.DestroyImmediate(dialogue);
                Object.DestroyImmediate(npc);
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(status);
            }
        }

        [Test]
        public void FormulaLibrary_ImplementsDefenseCriticalAndRefineContracts()
        {
            Assert.That(
                FormulaLibrary.ApplyDefense(200f, 100f),
                Is.EqualTo(100f).Within(0.0001f));
            Assert.That(
                FormulaLibrary.ApplyDefense(200f, 999f, true),
                Is.EqualTo(200f).Within(0.0001f));
            Assert.That(
                FormulaLibrary.ApplyCritical(100f, true, FormulaLibrary.BaseCritDamage),
                Is.EqualTo(150f).Within(0.0001f));
            Assert.That(
                FormulaLibrary.GetRefineStatMultiplier(10),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(
                FormulaLibrary.GetRefineSuccessRate(100),
                Is.EqualTo(FormulaLibrary.RefineMinimumSuccess).Within(0.0001f));
            Assert.That(FormulaLibrary.GetRefineMaterialCost(0), Is.EqualTo(1));
            Assert.That(FormulaLibrary.GetRefineMaterialCost(1), Is.EqualTo(1));
            Assert.That(FormulaLibrary.GetRefineMaterialCost(2), Is.EqualTo(2));
            Assert.That(FormulaLibrary.GetRefineMaterialCost(9), Is.EqualTo(5));
        }
    }
}
