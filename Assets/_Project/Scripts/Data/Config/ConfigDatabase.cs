using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wendao.Core;
using UnityEngine;

namespace Wendao.Data
{
    public sealed class ConfigDatabase : Singleton<ConfigDatabase>
    {
        private sealed class BuiltInMainQuestDefinition
        {
            public int Step;
            public string DisplayName;
            public string Description;
            public int RequiredRealm;
            public string[] Prerequisites;
            public QuestObjective[] Objectives;
            public bool Ordered;
            public QuestReward AcceptRewards;
            public QuestReward Rewards;
            public string StartNpcId;
            public string TurnInNpcId;
            public string StartSpeakerKey;
            public string StartSpeaker;
            public string CompleteSpeakerKey;
            public string CompleteSpeaker;
            public string StartText;
            public string CompleteText;
        }

        private readonly Dictionary<string, ItemData> _items =
            new Dictionary<string, ItemData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EquipmentData> _equipment =
            new Dictionary<string, EquipmentData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SkillData> _skills =
            new Dictionary<string, SkillData>(StringComparer.Ordinal);
        private readonly Dictionary<string, CraftRecipeData> _recipes =
            new Dictionary<string, CraftRecipeData>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestData> _quests =
            new Dictionary<string, QuestData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueData> _dialogues =
            new Dictionary<string, DialogueData>(StringComparer.Ordinal);
        private readonly Dictionary<string, NPCData> _npcs =
            new Dictionary<string, NPCData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EnemyData> _enemies =
            new Dictionary<string, EnemyData>(StringComparer.Ordinal);
        private readonly Dictionary<string, StatusEffectData> _statusEffects =
            new Dictionary<string, StatusEffectData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SerendipityData> _serendipities =
            new Dictionary<string, SerendipityData>(StringComparer.Ordinal);
        private readonly Dictionary<string, AchievementData> _achievements =
            new Dictionary<string, AchievementData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TitleData> _titles =
            new Dictionary<string, TitleData>(StringComparer.Ordinal);
        private readonly List<ScriptableObject> _generatedContent =
            new List<ScriptableObject>();

        public RealmConfig Realm { get; private set; } = new RealmConfig();
        public SpiritRootConfig SpiritRoot { get; private set; } = new SpiritRootConfig();
        public BodyRefinementConfig Body { get; private set; } = new BodyRefinementConfig();
        public CraftLevelConfig Craft { get; private set; } = new CraftLevelConfig();
        public IReadOnlyCollection<ItemData> Items => _items.Values;
        public IReadOnlyCollection<EquipmentData> Equipment => _equipment.Values;
        public IReadOnlyCollection<SkillData> Skills => _skills.Values;
        public IReadOnlyCollection<CraftRecipeData> Recipes => _recipes.Values;
        public IReadOnlyCollection<QuestData> Quests => _quests.Values;
        public IReadOnlyCollection<DialogueData> Dialogues => _dialogues.Values;
        public IReadOnlyCollection<NPCData> Npcs => _npcs.Values;
        public IReadOnlyCollection<EnemyData> Enemies => _enemies.Values;
        public IReadOnlyCollection<StatusEffectData> StatusEffects =>
            _statusEffects.Values;
        public IReadOnlyCollection<SerendipityData> Serendipities =>
            _serendipities.Values;
        public IReadOnlyCollection<AchievementData> Achievements =>
            _achievements.Values;
        public IReadOnlyCollection<TitleData> Titles => _titles.Values;
        public bool IsSafeMode { get; private set; }
        public string LastError { get; private set; }

        public void LoadAll()
        {
            string configDirectory = Path.Combine(Application.streamingAssetsPath, "Config");
            if (!TryLoadAllFromDirectory(configDirectory))
            {
                Debug.LogError($"ConfigDatabase entered safe mode: {LastError}", this);
            }
        }

        public bool TryLoadAllFromDirectory(string configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                return EnterSafeMode("Config directory is required.");
            }

            if (!JsonStorage.TryRead(
                    Path.Combine(configDirectory, "RealmConfig.json"),
                    out RealmConfig realm,
                    out string error)
                || !JsonStorage.TryRead(
                    Path.Combine(configDirectory, "SpiritRootConfig.json"),
                    out SpiritRootConfig spiritRoot,
                    out error)
                || !JsonStorage.TryRead(
                    Path.Combine(configDirectory, "BodyRefinementConfig.json"),
                    out BodyRefinementConfig body,
                    out error)
                || !JsonStorage.TryRead(
                    Path.Combine(configDirectory, "CraftLevelConfig.json"),
                    out CraftLevelConfig craft,
                    out error))
            {
                return EnterSafeMode(error);
            }

            if (!ValidateRealm(realm, out error)
                || !ValidateSpiritRoot(spiritRoot, out error)
                || !ValidateBody(body, out error)
                || !ValidateCraft(craft, out error))
            {
                return EnterSafeMode(error);
            }

            Realm = realm;
            SpiritRoot = spiritRoot;
            Body = body;
            Craft = craft;
            IsSafeMode = false;
            LastError = null;
            LoadResourcesContent();
            return true;
        }

        public RealmEntry GetRealm(int realm)
        {
            return Realm?.Realms?.FirstOrDefault(entry => entry != null && entry.Realm == realm);
        }

        public SpiritRootEntry GetSpiritRoot(SpiritRootType type)
        {
            return SpiritRoot?.Roots?.FirstOrDefault(entry => entry != null && entry.Type == type);
        }

        public ItemData GetItem(string id)
        {
            return !string.IsNullOrEmpty(id) && _items.TryGetValue(id, out ItemData item)
                ? item
                : null;
        }

        public SkillData GetSkill(string id)
        {
            return !string.IsNullOrEmpty(id) && _skills.TryGetValue(id, out SkillData skill)
                ? skill
                : null;
        }

        public CraftRecipeData GetRecipe(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _recipes.TryGetValue(id, out CraftRecipeData recipe)
                ? recipe
                : null;
        }

        public EquipmentData GetEquipment(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _equipment.TryGetValue(id, out EquipmentData equipment)
                ? equipment
                : null;
        }

        public QuestData GetQuest(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _quests.TryGetValue(id, out QuestData quest)
                ? quest
                : null;
        }

        public DialogueData GetDialogue(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _dialogues.TryGetValue(id, out DialogueData dialogue)
                ? dialogue
                : null;
        }

        public NPCData GetNpc(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _npcs.TryGetValue(id, out NPCData npc)
                ? npc
                : null;
        }

        public EnemyData GetEnemy(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _enemies.TryGetValue(id, out EnemyData enemy)
                ? enemy
                : null;
        }

        public StatusEffectData GetStatusEffect(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _statusEffects.TryGetValue(id, out StatusEffectData status)
                ? status
                : null;
        }

        public SerendipityData GetSerendipity(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _serendipities.TryGetValue(id, out SerendipityData data)
                ? data
                : null;
        }

        public AchievementData GetAchievement(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _achievements.TryGetValue(id, out AchievementData data)
                ? data
                : null;
        }

        public TitleData GetTitle(string id)
        {
            return !string.IsNullOrEmpty(id)
                && _titles.TryGetValue(id, out TitleData data)
                ? data
                : null;
        }

        public bool RegisterItem(ItemData item)
        {
            return RegisterContent(item, _items);
        }

        public bool RegisterSkill(SkillData skill)
        {
            return RegisterContent(skill, _skills);
        }

        public bool RegisterRecipe(CraftRecipeData recipe)
        {
            return RegisterContent(recipe, _recipes);
        }

        public bool RegisterEquipment(EquipmentData equipment)
        {
            return RegisterContent(equipment, _equipment);
        }

        public bool RegisterQuest(QuestData quest)
        {
            return RegisterContent(quest, _quests);
        }

        public bool RegisterDialogue(DialogueData dialogue)
        {
            return RegisterContent(dialogue, _dialogues);
        }

        public bool RegisterNpc(NPCData npc)
        {
            return RegisterContent(npc, _npcs);
        }

        public bool RegisterEnemy(EnemyData enemy)
        {
            return RegisterContent(enemy, _enemies);
        }

        public bool RegisterStatusEffect(StatusEffectData status)
        {
            return RegisterContent(status, _statusEffects);
        }

        public bool RegisterSerendipity(SerendipityData data)
        {
            return RegisterContent(data, _serendipities);
        }

        public bool RegisterAchievement(AchievementData data)
        {
            return RegisterContent(data, _achievements);
        }

        public bool RegisterTitle(TitleData data)
        {
            return RegisterContent(data, _titles);
        }

        public void LoadResourcesContent()
        {
            DestroyGeneratedContent();
            _items.Clear();
            _equipment.Clear();
            _skills.Clear();
            _recipes.Clear();
            _quests.Clear();
            _dialogues.Clear();
            _npcs.Clear();
            _enemies.Clear();
            _statusEffects.Clear();
            _serendipities.Clear();
            _achievements.Clear();
            _titles.Clear();

            foreach (ItemData item in Resources.LoadAll<ItemData>("SO/Items"))
            {
                RegisterItem(item);
            }

            foreach (SkillData skill in Resources.LoadAll<SkillData>("SO/Skills"))
            {
                RegisterSkill(skill);
            }

            foreach (CraftRecipeData recipe in Resources.LoadAll<CraftRecipeData>(
                         "SO/Recipes"))
            {
                RegisterRecipe(recipe);
            }

            foreach (EquipmentData equipment in Resources.LoadAll<EquipmentData>(
                         "SO/Equipment"))
            {
                RegisterEquipment(equipment);
            }

            foreach (QuestData quest in Resources.LoadAll<QuestData>("SO/Quests"))
            {
                RegisterQuest(quest);
            }

            foreach (DialogueData dialogue in Resources.LoadAll<DialogueData>(
                         "SO/Dialogues"))
            {
                RegisterDialogue(dialogue);
            }

            foreach (NPCData npc in Resources.LoadAll<NPCData>("SO/NPCs"))
            {
                RegisterNpc(npc);
            }

            foreach (EnemyData enemy in Resources.LoadAll<EnemyData>("SO/Enemies"))
            {
                RegisterEnemy(enemy);
            }

            foreach (StatusEffectData status in Resources.LoadAll<StatusEffectData>(
                         "SO/StatusEffects"))
            {
                RegisterStatusEffect(status);
            }

            foreach (SerendipityData data in Resources.LoadAll<SerendipityData>(
                         "SO/Serendipities"))
            {
                RegisterSerendipity(data);
            }

            foreach (AchievementData data in Resources.LoadAll<AchievementData>(
                         "SO/Achievements"))
            {
                RegisterAchievement(data);
            }

            foreach (TitleData data in Resources.LoadAll<TitleData>("SO/Titles"))
            {
                RegisterTitle(data);
            }

            RegisterBuiltInVerticalSliceContent();
            RegisterBuiltInStatusEffects();
            RegisterBuiltInAchievementsAndTitles();
            ApplyLocalizationFallbackKeys();
        }

        protected override void OnSingletonAwake()
        {
            Realm = new RealmConfig();
            SpiritRoot = new SpiritRootConfig();
            Body = new BodyRefinementConfig();
            Craft = new CraftLevelConfig();
            IsSafeMode = false;
            LastError = null;
            LoadResourcesContent();
        }

        protected override void OnSingletonDestroyed()
        {
            DestroyGeneratedContent();
        }

        private bool EnterSafeMode(string error)
        {
            Realm = new RealmConfig();
            SpiritRoot = new SpiritRootConfig();
            Body = new BodyRefinementConfig();
            Craft = new CraftLevelConfig();
            _items.Clear();
            _equipment.Clear();
            _skills.Clear();
            _recipes.Clear();
            _quests.Clear();
            _dialogues.Clear();
            _npcs.Clear();
            _enemies.Clear();
            _statusEffects.Clear();
            _serendipities.Clear();
            _achievements.Clear();
            _titles.Clear();
            IsSafeMode = true;
            LastError = string.IsNullOrEmpty(error) ? "Unknown config error." : error;
            LoadResourcesContent();
            return false;
        }

        private static bool RegisterContent<T>(T content, IDictionary<string, T> registry)
            where T : ScriptableObject
        {
            if (content == null)
            {
                return false;
            }

            string id;
            if (content is ItemData item)
            {
                id = item.Id;
            }
            else if (content is SkillData skill)
            {
                id = skill.Id;
            }
            else if (content is CraftRecipeData recipe)
            {
                id = recipe.Id;
            }
            else if (content is EquipmentData equipment)
            {
                id = equipment.Id;
            }
            else if (content is QuestData quest)
            {
                id = quest.Id;
            }
            else if (content is DialogueData dialogue)
            {
                id = dialogue.Id;
            }
            else if (content is NPCData npc)
            {
                id = npc.Id;
            }
            else if (content is EnemyData enemy)
            {
                id = enemy.Id;
            }
            else if (content is StatusEffectData status)
            {
                id = status.Id;
            }
            else if (content is SerendipityData serendipity)
            {
                id = serendipity.Id;
            }
            else if (content is AchievementData achievement)
            {
                id = achievement.Id;
            }
            else if (content is TitleData title)
            {
                id = title.Id;
            }
            else
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(id) || registry.ContainsKey(id))
            {
                return false;
            }

            registry.Add(id, content);
            return true;
        }

        private void RegisterBuiltInVerticalSliceContent()
        {
            if (GetItem("item_mat_wolf_hair") == null)
            {
                ItemData wolfHair = CreateGenerated<ItemData>(
                    "Item_WolfHair_Runtime");
                wolfHair.Id = "item_mat_wolf_hair";
                wolfHair.DisplayName = "狼毫";
                wolfHair.Description = "灰狼颈背的硬毫，可作任务与炼丹材料。";
                wolfHair.Type = ItemType.Material;
                wolfHair.Rarity = ItemRarity.Common;
                wolfHair.MaxStack = 99;
                wolfHair.IsBound = false;
                wolfHair.SellPrice = 5;
                wolfHair.RequiredRealm = 0;
                wolfHair.UseEffects = Array.Empty<UseEffect>();
                RegisterItem(wolfHair);
            }

            if (GetItem("item_potion_heal_01") == null)
            {
                ItemData healPotion = CreateGenerated<ItemData>("Item_HealPotion01_Runtime");
                healPotion.Id = "item_potion_heal_01";
                healPotion.DisplayName = "回血丹";
                healPotion.Description = "服用后恢复八十点气血。";
                healPotion.Type = ItemType.Consumable;
                healPotion.Rarity = ItemRarity.Common;
                healPotion.MaxStack = 20;
                healPotion.RequiredRealm = 0;
                healPotion.UseEffects = new[]
                {
                    new UseEffect
                    {
                        EffectType = UseEffectType.Heal,
                        Value = 80f
                    }
                };
                RegisterItem(healPotion);
            }

            if (GetItem("item_mat_refine_stone") == null)
            {
                ItemData refineStone = CreateGenerated<ItemData>(
                    "Item_RefineStone_Runtime");
                refineStone.Id = "item_mat_refine_stone";
                refineStone.DisplayName = "精炼石";
                refineStone.Description = "蕴含稳定灵力，可用于提升装备基础属性。";
                refineStone.Type = ItemType.Material;
                refineStone.Rarity = ItemRarity.Common;
                refineStone.MaxStack = 99;
                refineStone.IsBound = false;
                refineStone.SellPrice = 6;
                refineStone.RequiredRealm = 0;
                refineStone.UseEffects = Array.Empty<UseEffect>();
                RegisterItem(refineStone);
            }

            if (GetItem("item_skill_scroll") == null)
            {
                ItemData skillScroll = CreateGenerated<ItemData>(
                    "Item_SkillScroll_Runtime");
                skillScroll.Id = "item_skill_scroll";
                skillScroll.DisplayName = "功法残页";
                skillScroll.Description = "记载残缺运功法门，可用于提升已学功法。";
                skillScroll.Type = ItemType.Material;
                skillScroll.Rarity = ItemRarity.Uncommon;
                skillScroll.MaxStack = 99;
                skillScroll.IsBound = false;
                skillScroll.SellPrice = 12;
                skillScroll.RequiredRealm = 0;
                skillScroll.UseEffects = Array.Empty<UseEffect>();
                RegisterItem(skillScroll);
            }

            if (GetItem("item_potion_body_01") == null)
            {
                ItemData bodyPotion = CreateGenerated<ItemData>(
                    "Item_BodyPotion01_Runtime");
                bodyPotion.Id = "item_potion_body_01";
                bodyPotion.DisplayName = "锻体丹";
                bodyPotion.Description = "服用后增加二百点炼体经验。";
                bodyPotion.Type = ItemType.Consumable;
                bodyPotion.Rarity = ItemRarity.Uncommon;
                bodyPotion.MaxStack = 10;
                bodyPotion.RequiredRealm = 0;
                bodyPotion.UseEffects = new[]
                {
                    new UseEffect
                    {
                        EffectType = UseEffectType.AddBodyXp,
                        Value = 200f
                    }
                };
                RegisterItem(bodyPotion);
            }

            if (GetItem("item_pill_foundation") == null)
            {
                ItemData foundationPill = CreateGenerated<ItemData>(
                    "Item_FoundationPill_Runtime");
                foundationPill.Id = "item_pill_foundation";
                foundationPill.DisplayName = "筑基丹";
                foundationPill.Description =
                    "练气修士冲击筑基时所需的破境丹药。";
                foundationPill.Type = ItemType.Consumable;
                foundationPill.Rarity = ItemRarity.Rare;
                foundationPill.MaxStack = 5;
                foundationPill.RequiredRealm = (int)RealmType.QiCondensation;
                foundationPill.UseEffects = Array.Empty<UseEffect>();
                foundationPill.AcquisitionHintKeys = new[]
                {
                    "hint_quest_main_08_yaolao"
                };
                RegisterItem(foundationPill);
            }

            if (GetItem("item_pill_goldencore") == null)
            {
                ItemData goldenCorePill = CreateGenerated<ItemData>(
                    "Item_GoldenCorePill_Runtime");
                goldenCorePill.Id = "item_pill_goldencore";
                goldenCorePill.DisplayName = "凝金丹";
                goldenCorePill.Description =
                    "筑基修士凝结金丹时所需的破境丹药。";
                goldenCorePill.Type = ItemType.Consumable;
                goldenCorePill.Rarity = ItemRarity.Epic;
                goldenCorePill.MaxStack = 5;
                goldenCorePill.RequiredRealm = (int)RealmType.Foundation;
                goldenCorePill.UseEffects = Array.Empty<UseEffect>();
                goldenCorePill.AcquisitionHintKeys = new[]
                {
                    "hint_quest_main_09_yaolao"
                };
                RegisterItem(goldenCorePill);
            }

            if (GetEquipment("eq_weapon_wood_sword") == null)
            {
                EquipmentData woodSword = CreateGenerated<EquipmentData>(
                    "Equipment_WoodSword_Runtime");
                woodSword.Id = "eq_weapon_wood_sword";
                woodSword.DisplayName = "木剑";
                woodSword.Slot = EquipmentSlot.Weapon;
                woodSword.Rarity = ItemRarity.Common;
                woodSword.RequiredRealm = (int)RealmType.QiCondensation;
                woodSword.BaseStats = new StatBlock
                {
                    Attack = 8f,
                    CritDamage = 0f
                };
                woodSword.MaxRefineLevel = 10;
                woodSword.MaxGemSockets = 0;
                woodSword.MaxDurability = 100;
                RegisterEquipment(woodSword);
            }

            if (GetItem("eq_weapon_wood_sword") == null)
            {
                ItemData woodSwordItem = CreateGenerated<ItemData>(
                    "Item_WoodSword_Runtime");
                woodSwordItem.Id = "eq_weapon_wood_sword";
                woodSwordItem.DisplayName = "木剑";
                woodSwordItem.Description = "青石镇常见的练习木剑，攻击加八。";
                woodSwordItem.Type = ItemType.Equipment;
                woodSwordItem.Rarity = ItemRarity.Common;
                woodSwordItem.MaxStack = 1;
                woodSwordItem.RequiredRealm = (int)RealmType.QiCondensation;
                woodSwordItem.EquipmentDataId = "eq_weapon_wood_sword";
                RegisterItem(woodSwordItem);
            }

            RegisterBuiltInSkill(
                "skill_basic_qi_bolt",
                "引气弹",
                "凝聚灵气，向前射出一道引气弹。",
                SkillType.Active,
                SkillElement.None,
                RealmType.QiCondensation,
                30f,
                5f,
                10f,
                1.5f,
                0.2f,
                0.3f,
                12f,
                0f,
                true,
                "VFX_Skill_QiBolt_Projectile",
                "VFX_Skill_QiBolt_Impact");
            RegisterBuiltInSkill(
                "skill_fire_ember",
                "星火诀",
                "弹指凝出星火，命中后灼烧敌人。",
                SkillType.Active,
                SkillElement.Fire,
                RealmType.QiCondensation,
                38f,
                7f,
                14f,
                3f,
                0.25f,
                0.35f,
                10f,
                0f,
                true,
                "VFX_Skill_Ember",
                "VFX_Hit_Fire",
                "status_burn",
                1f,
                SpiritRootType.Fire);
            RegisterBuiltInSkill(
                "skill_ice_needle",
                "寒针",
                "凝寒成针，命中后令敌人身染寒意。",
                SkillType.Active,
                SkillElement.Ice,
                RealmType.Foundation,
                35f,
                7f,
                12f,
                2.5f,
                0.2f,
                0.3f,
                13f,
                0f,
                true,
                "VFX_Hit_Ice",
                "VFX_Hit_Ice",
                "status_chill",
                1f,
                SpiritRootType.Water,
                SpiritRootType.Heaven);
            RegisterBuiltInSkill(
                "skill_lightning_chain",
                "引雷索",
                "牵引雷光破敌，并短暂扰乱敌人行动。",
                SkillType.Active,
                SkillElement.Lightning,
                RealmType.Foundation,
                75f,
                12f,
                22f,
                7f,
                0.35f,
                0.45f,
                11f,
                0f,
                true,
                "VFX_Hit_Fire",
                "VFX_Hit_Fire",
                "status_shock_stun",
                1f,
                SpiritRootType.Heaven);
            RegisterBuiltInSkill(
                "skill_wind_slash",
                "风刃",
                "引风化刃，以迅疾灵压斩向前方。",
                SkillType.Active,
                SkillElement.Wind,
                RealmType.QiCondensation,
                68f,
                10f,
                18f,
                4.5f,
                0.25f,
                0.35f,
                12f,
                0f,
                true,
                "VFX_Hit_Physical",
                "VFX_Hit_Physical",
                preferredRoots: new[]
                {
                    SpiritRootType.Wood,
                    SpiritRootType.Heaven
                });
            RegisterBuiltInSkill(
                "skill_pass_iron_skin",
                "铁肤",
                "运转气血淬炼皮肉，增强近身承伤能力。",
                SkillType.Passive,
                SkillElement.Earth,
                RealmType.QiCondensation,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                false,
                string.Empty,
                string.Empty,
                preferredRoots: new[]
                {
                    SpiritRootType.Earth,
                    SpiritRootType.Waste
                });
            RegisterBuiltInSkill(
                "skill_ult_fire_wave",
                "炎浪",
                "聚拢烈焰化作炎浪，重创前方敌人。",
                SkillType.Ultimate,
                SkillElement.Fire,
                RealmType.GoldenCore,
                170f,
                20f,
                40f,
                20f,
                0.8f,
                0.8f,
                12f,
                0f,
                true,
                "VFX_Skill_Ember",
                "VFX_Hit_Fire",
                "status_burn",
                1f,
                SpiritRootType.Fire,
                SpiritRootType.Heaven);

            if (GetQuest("quest_main_01_02") == null)
            {
                QuestData huntQuest = CreateGenerated<QuestData>(
                    "Quest_Main0102_Runtime");
                huntQuest.Id = "quest_main_01_02";
                huntQuest.DisplayNameKey = "quest_name_main_01_02";
                huntQuest.DisplayName = "东郊猎狼";
                huntQuest.DescriptionKey = "quest_desc_main_01_02";
                huntQuest.Description =
                    "前往东郊，击杀三只灰狼，再回来向药老复命。";
                huntQuest.Type = QuestType.Main;
                huntQuest.RequiredRealm = (int)RealmType.QiCondensation;
                huntQuest.PrerequisiteQuestIds = Array.Empty<string>();
                huntQuest.Objectives = new[]
                {
                    new QuestObjective
                    {
                        Type = ObjectiveType.Kill,
                        TargetId = "enemy_wolf_gray",
                        RequiredCount = 3,
                        DescriptionKey = "quest_obj_main_01_02_kill_wolves",
                        Description = "击杀灰狼"
                    }
                };
                huntQuest.ObjectivesAreOrdered = false;
                huntQuest.AcceptRewards = null;
                huntQuest.Rewards = new QuestReward
                {
                    CultivationXp = 700f,
                    SpiritStones = 10,
                    Items = Array.Empty<ItemStack>()
                };
                huntQuest.StartDialogueId = "dlg_main_01_02_start";
                huntQuest.CompleteDialogueId = "dlg_main_01_02_complete";
                huntQuest.StartNpcId = "npc_yaolao";
                huntQuest.TurnInNpcId = "npc_yaolao";
                RegisterQuest(huntQuest);
            }

            if (GetDialogue("dlg_main_01_02_start") == null)
            {
                DialogueData startDialogue = CreateGenerated<DialogueData>(
                    "Dialogue_Main0102Start_Runtime");
                startDialogue.Id = "dlg_main_01_02_start";
                startDialogue.Nodes = new[]
                {
                    new DialogueNode
                    {
                        NodeId = "greeting",
                        SpeakerNameKey = "npc_name_yaolao",
                        SpeakerName = "药老",
                        TextKey = "dlg_main_01_02_start_01",
                        Text = "东郊近来狼患渐重，已有行商不敢出镇。",
                        NextNodeId = "offer"
                    },
                    new DialogueNode
                    {
                        NodeId = "offer",
                        SpeakerNameKey = "npc_name_yaolao",
                        SpeakerName = "药老",
                        TextKey = "dlg_main_01_02_start_02",
                        Text = "你既已引气入体，可愿去除掉三只灰狼？",
                        Choices = new[]
                        {
                            new DialogueChoice
                            {
                                TextKey = "dlg_choice_main_01_02_accept",
                                Text = "我这便去。",
                                NextNodeId = "accepted"
                            },
                            new DialogueChoice
                            {
                                TextKey = "dlg_choice_main_01_02_later",
                                Text = "容我再准备一番。",
                                NextNodeId = "declined"
                            }
                        }
                    },
                    new DialogueNode
                    {
                        NodeId = "accepted",
                        SpeakerNameKey = "npc_name_yaolao",
                        SpeakerName = "药老",
                        TextKey = "dlg_main_01_02_start_03",
                        Text = "莫要轻敌。事成之后，回来寻我。",
                        QuestOfferId = "quest_main_01_02",
                        End = true
                    },
                    new DialogueNode
                    {
                        NodeId = "declined",
                        SpeakerNameKey = "npc_name_yaolao",
                        SpeakerName = "药老",
                        TextKey = "dlg_main_01_02_start_04",
                        Text = "修行不争一时，准备妥当再来。",
                        End = true
                    }
                };
                RegisterDialogue(startDialogue);
            }

            if (GetDialogue("dlg_main_01_02_complete") == null)
            {
                DialogueData completeDialogue = CreateGenerated<DialogueData>(
                    "Dialogue_Main0102Complete_Runtime");
                completeDialogue.Id = "dlg_main_01_02_complete";
                completeDialogue.Nodes = new[]
                {
                    new DialogueNode
                    {
                        NodeId = "turn_in",
                        SpeakerNameKey = "npc_name_yaolao",
                        SpeakerName = "药老",
                        TextKey = "dlg_main_01_02_complete_01",
                        Text = "气息沉稳了几分。此行所得，才是修行的开端。",
                        QuestTurnInId = "quest_main_01_02",
                        End = true
                    }
                };
                RegisterDialogue(completeDialogue);
            }

            if (GetNpc("npc_yaolao") == null)
            {
                NPCData yaoLao = CreateGenerated<NPCData>("NPC_YaoLao_Runtime");
                yaoLao.Id = "npc_yaolao";
                yaoLao.DisplayNameKey = "npc_name_yaolao";
                yaoLao.DisplayName = "药老";
                yaoLao.DefaultDialogueId = "dlg_main_01_02_start";
                yaoLao.FactionId = string.Empty;
                yaoLao.IsVendor = false;
                yaoLao.VendorItemIds = Array.Empty<string>();
                yaoLao.AffectionMilestones = Array.Empty<AffectionMilestone>();
                RegisterNpc(yaoLao);
            }

            if (GetEnemy("enemy_wolf_gray") == null)
            {
                EnemyData greyWolf = CreateGenerated<EnemyData>(
                    "Enemy_GreyWolf_Runtime");
                greyWolf.Id = "enemy_wolf_gray";
                greyWolf.DisplayName = "灰狼";
                greyWolf.Rank = EnemyRank.Normal;
                greyWolf.Realm = RealmType.QiCondensation;
                greyWolf.SubStage = 1;
                greyWolf.MaxHp = 80f;
                greyWolf.Attack = 8f;
                greyWolf.Defense = 0f;
                greyWolf.MoveSpeed = 3.2f;
                greyWolf.AggroRange = 8f;
                greyWolf.AttackRange = 1.6f;
                greyWolf.DisengageRange = 14f;
                greyWolf.AttackInterval = 1.2f;
                greyWolf.CultivationXpReward = 15f;
                greyWolf.MinSpiritStones = 0;
                greyWolf.MaxSpiritStones = 2;
                greyWolf.LootTable = new[]
                {
                    new LootEntry
                    {
                        ItemId = "item_mat_wolf_hair",
                        MinCount = 1,
                        MaxCount = 1,
                        DropChance = 0.4f
                    }
                };
                greyWolf.SkillIds = Array.Empty<string>();
                greyWolf.BossPhases = Array.Empty<BossPhase>();
                RegisterEnemy(greyWolf);
            }

            if (GetEnemy("enemy_wolf_elite") == null)
            {
                EnemyData eliteWolf = CreateGenerated<EnemyData>(
                    "Enemy_EliteWolf_Runtime");
                eliteWolf.Id = "enemy_wolf_elite";
                eliteWolf.DisplayName = "灰爪";
                eliteWolf.Rank = EnemyRank.Elite;
                eliteWolf.Realm = RealmType.QiCondensation;
                eliteWolf.SubStage = 6;
                eliteWolf.MaxHp = 800f;
                eliteWolf.Attack = 28f;
                eliteWolf.Defense = 0f;
                eliteWolf.MoveSpeed = 3.8f;
                eliteWolf.AggroRange = 10f;
                eliteWolf.AttackRange = 1.8f;
                eliteWolf.DisengageRange = 18f;
                eliteWolf.AttackInterval = 1.4f;
                eliteWolf.CultivationXpReward = 120f;
                eliteWolf.MinSpiritStones = 8;
                eliteWolf.MaxSpiritStones = 15;
                eliteWolf.LootTable = new[]
                {
                    new LootEntry
                    {
                        ItemId = "item_mat_beast_core_1",
                        MinCount = 1,
                        MaxCount = 1,
                        DropChance = 1f
                    }
                };
                eliteWolf.SkillIds = new[]
                {
                    "skill_enemy_wolf_elite_charge"
                };
                eliteWolf.BossPhases = Array.Empty<BossPhase>();
                RegisterEnemy(eliteWolf);
            }

            if (GetEnemy("enemy_blackwind_spawn") == null)
            {
                EnemyData blackwindSpawn = CreateGenerated<EnemyData>(
                    "Enemy_BlackwindSpawn_Runtime");
                blackwindSpawn.Id = "enemy_blackwind_spawn";
                blackwindSpawn.DisplayName = "黑风小妖";
                blackwindSpawn.Rank = EnemyRank.Normal;
                blackwindSpawn.Realm = RealmType.Foundation;
                blackwindSpawn.SubStage = 1;
                blackwindSpawn.MaxHp = 350f;
                blackwindSpawn.Attack = 40f;
                blackwindSpawn.Defense = 5f;
                blackwindSpawn.MoveSpeed = 3.4f;
                blackwindSpawn.AggroRange = 10f;
                blackwindSpawn.AttackRange = 1.8f;
                blackwindSpawn.DisengageRange = 16f;
                blackwindSpawn.AttackInterval = 1.4f;
                blackwindSpawn.CultivationXpReward = 60f;
                blackwindSpawn.MinSpiritStones = 0;
                blackwindSpawn.MaxSpiritStones = 2;
                blackwindSpawn.LootTable = Array.Empty<LootEntry>();
                blackwindSpawn.SkillIds = Array.Empty<string>();
                blackwindSpawn.BossPhases = Array.Empty<BossPhase>();
                RegisterEnemy(blackwindSpawn);
            }

            if (GetEnemy("enemy_boss_stone_general") == null)
            {
                EnemyData stoneGeneral = CreateGenerated<EnemyData>(
                    "Enemy_StoneGeneral_Runtime");
                stoneGeneral.Id = "enemy_boss_stone_general";
                stoneGeneral.DisplayName = "黑风石将军";
                stoneGeneral.Rank = EnemyRank.Boss;
                stoneGeneral.Realm = RealmType.GoldenCore;
                stoneGeneral.SubStage = 1;
                stoneGeneral.MaxHp = 12000f;
                stoneGeneral.Attack = 70f;
                stoneGeneral.Defense = 20f;
                stoneGeneral.MoveSpeed = 2.4f;
                stoneGeneral.AggroRange = 7f;
                stoneGeneral.AttackRange = 2.8f;
                stoneGeneral.DisengageRange = 7f;
                stoneGeneral.AttackInterval = 1.8f;
                stoneGeneral.CultivationXpReward = 2000f;
                stoneGeneral.MinSpiritStones = 0;
                stoneGeneral.MaxSpiritStones = 0;
                stoneGeneral.LootTable = Array.Empty<LootEntry>();
                stoneGeneral.SkillIds = new[]
                {
                    "boss_sg_slam",
                    "boss_sg_spike",
                    "boss_sg_charge",
                    "boss_sg_summon",
                    "boss_sg_rage_slam"
                };
                stoneGeneral.BossPhases = new[]
                {
                    new BossPhase
                    {
                        PhaseIndex = 0,
                        HpThreshold = 1f,
                        SkillIds = new[]
                        {
                            "boss_sg_slam",
                            "boss_sg_spike"
                        },
                        PhaseVfxId = string.Empty,
                        InvulnerableDuringTransition = false,
                        Telegraphs = new[]
                        {
                            new BossSkillTelegraph
                            {
                                SkillId = "boss_sg_slam",
                                Shape = TelegraphShape.Circle,
                                Duration = 0.7f,
                                RadiusOrLength = 4f,
                                Angle = 360f,
                                VfxId = "VFX_Boss_Slam_Warning",
                                RecoverStun = 1f,
                                Interruptible = false
                            },
                            new BossSkillTelegraph
                            {
                                SkillId = "boss_sg_spike",
                                Shape = TelegraphShape.Circle,
                                Duration = 0.6f,
                                RadiusOrLength = 8f,
                                Angle = 360f,
                                VfxId = "VFX_Boss_Slam_Warning",
                                RecoverStun = 0.8f,
                                Interruptible = false
                            }
                        }
                    },
                    new BossPhase
                    {
                        PhaseIndex = 1,
                        HpThreshold = 0.7f,
                        SkillIds = new[]
                        {
                            "boss_sg_charge",
                            "boss_sg_summon"
                        },
                        PhaseVfxId = string.Empty,
                        InvulnerableDuringTransition = true,
                        Telegraphs = new[]
                        {
                            new BossSkillTelegraph
                            {
                                SkillId = "boss_sg_charge",
                                Shape = TelegraphShape.Line,
                                Duration = 0.8f,
                                RadiusOrLength = 8f,
                                Angle = 0f,
                                VfxId = "VFX_Boss_Charge_Warning",
                                RecoverStun = 1.2f,
                                Interruptible = false
                            },
                            new BossSkillTelegraph
                            {
                                SkillId = "boss_sg_summon",
                                Shape = TelegraphShape.Circle,
                                Duration = 0.7f,
                                RadiusOrLength = 5f,
                                Angle = 360f,
                                VfxId = "VFX_Boss_Summon_Warning",
                                RecoverStun = 0.5f,
                                Interruptible = false
                            }
                        }
                    },
                    new BossPhase
                    {
                        PhaseIndex = 2,
                        HpThreshold = 0.3f,
                        SkillIds = new[]
                        {
                            "boss_sg_rage_slam"
                        },
                        PhaseVfxId = string.Empty,
                        InvulnerableDuringTransition = true,
                        Telegraphs = new[]
                        {
                            new BossSkillTelegraph
                            {
                                SkillId = "boss_sg_rage_slam",
                                Shape = TelegraphShape.FullScreen,
                                Duration = 1f,
                                RadiusOrLength = 12f,
                                Angle = 360f,
                                VfxId = "VFX_Boss_Slam_Warning",
                                RecoverStun = 1.5f,
                                Interruptible = false
                            }
                        }
                    }
                };
                RegisterEnemy(stoneGeneral);
            }

            RegisterBuiltInShopContent();
            RegisterBuiltInAlchemyContent();
            RegisterBuiltInMainQuestContent();
            RegisterBuiltInSideAndDailyQuestContent();
            RegisterBuiltInSerendipityContent();
        }

        private void RegisterBuiltInShopContent()
        {
            ItemData healPotion = GetItem("item_potion_heal_01");
            if (healPotion != null)
            {
                healPotion.BuyPrice = 10;
                healPotion.SellPrice = 2;
            }

            ItemData refineStone = GetItem("item_mat_refine_stone");
            if (refineStone != null)
            {
                refineStone.BuyPrice = 25;
                refineStone.SellPrice = 6;
            }

            if (GetEquipment("eq_weapon_iron_sword") == null)
            {
                EquipmentData ironSword = CreateGenerated<EquipmentData>(
                    "Equipment_IronSword_Runtime");
                ironSword.Id = "eq_weapon_iron_sword";
                ironSword.DisplayName = "铁剑";
                ironSword.Slot = EquipmentSlot.Weapon;
                ironSword.Rarity = ItemRarity.Common;
                ironSword.RequiredRealm = (int)RealmType.QiCondensation;
                ironSword.BaseStats = new StatBlock
                {
                    Attack = 18f,
                    CritDamage = 0f
                };
                ironSword.MaxRefineLevel = 10;
                ironSword.MaxGemSockets = 0;
                ironSword.MaxDurability = 120;
                RegisterEquipment(ironSword);
            }

            if (GetItem("eq_weapon_iron_sword") == null)
            {
                ItemData ironSwordItem = CreateGenerated<ItemData>(
                    "Item_IronSword_Runtime");
                ironSwordItem.Id = "eq_weapon_iron_sword";
                ironSwordItem.DisplayName = "铁剑";
                ironSwordItem.Description = "青石铁匠锻造的长剑，攻击加十八。";
                ironSwordItem.Type = ItemType.Equipment;
                ironSwordItem.Rarity = ItemRarity.Common;
                ironSwordItem.MaxStack = 1;
                ironSwordItem.IsBound = false;
                ironSwordItem.BuyPrice = 80;
                ironSwordItem.SellPrice = 20;
                ironSwordItem.RequiredRealm = (int)RealmType.QiCondensation;
                ironSwordItem.UseEffects = Array.Empty<UseEffect>();
                ironSwordItem.EquipmentDataId = "eq_weapon_iron_sword";
                ironSwordItem.AcquisitionHintKeys = Array.Empty<string>();
                RegisterItem(ironSwordItem);
            }

            if (GetDialogue("dlg_shop_zhanggui") == null)
            {
                DialogueData dialogue = CreateGenerated<DialogueData>(
                    "Dialogue_ShopZhanggui_Runtime");
                dialogue.Id = "dlg_shop_zhanggui";
                dialogue.Nodes = new[]
                {
                    new DialogueNode
                    {
                        NodeId = "greeting",
                        SpeakerNameKey = "npc_name_zhanggui",
                        SpeakerName = "张掌柜",
                        TextKey = "dlg_shop_zhanggui_01",
                        Text = "客官慢看，小店货真价实。",
                        End = true
                    }
                };
                RegisterDialogue(dialogue);
            }

            if (GetNpc("npc_zhanggui") == null)
            {
                NPCData zhanggui = CreateGenerated<NPCData>(
                    "NPC_Zhanggui_Runtime");
                zhanggui.Id = "npc_zhanggui";
                zhanggui.DisplayNameKey = "npc_name_zhanggui";
                zhanggui.DisplayName = "张掌柜";
                zhanggui.DefaultDialogueId = "dlg_shop_zhanggui";
                zhanggui.FactionId = "faction_danding";
                zhanggui.IsVendor = true;
                zhanggui.VendorItemIds = new[]
                {
                    "item_potion_heal_01",
                    "item_mat_refine_stone",
                    "eq_weapon_iron_sword"
                };
                zhanggui.AffectionMilestones = Array.Empty<AffectionMilestone>();
                RegisterNpc(zhanggui);
            }
        }

        private void RegisterBuiltInAlchemyContent()
        {
            RegisterBuiltInAlchemyItem(
                "item_mat_qingxin_grass",
                "清心草",
                "叶脉清凉的低阶灵草，是炼制恢复丹药的常用主材。",
                ItemType.Material,
                ItemRarity.Common,
                99,
                4,
                Array.Empty<UseEffect>());
            RegisterBuiltInAlchemyItem(
                "item_mat_spirit_dust",
                "灵尘",
                "细碎灵力凝成的粉尘，可稳定低阶丹炉中的药性。",
                ItemType.Material,
                ItemRarity.Common,
                99,
                3,
                Array.Empty<UseEffect>());
            RegisterBuiltInAlchemyItem(
                "item_mat_beast_core_1",
                "一品妖核",
                "低阶妖兽体内凝结的精华，可用于炼制聚气丹。",
                ItemType.Material,
                ItemRarity.Uncommon,
                99,
                15,
                Array.Empty<UseEffect>());
            RegisterBuiltInAlchemyItem(
                "item_potion_mana_01",
                "回气丹",
                "服用后恢复五十点灵力。",
                ItemType.Consumable,
                ItemRarity.Common,
                20,
                12,
                new[]
                {
                    new UseEffect
                    {
                        EffectType = UseEffectType.RestoreMana,
                        Value = 50f
                    }
                });
            RegisterBuiltInAlchemyItem(
                "item_potion_xp_01",
                "聚气丹",
                "服用后增加三百点修为。",
                ItemType.Consumable,
                ItemRarity.Uncommon,
                10,
                30,
                new[]
                {
                    new UseEffect
                    {
                        EffectType = UseEffectType.AddCultivationXp,
                        Value = 300f
                    }
                });

            RegisterBuiltInAlchemyRecipe(
                "recipe_heal_01",
                "回血丹",
                1,
                0.9f,
                "item_potion_heal_01",
                new CraftIngredient
                {
                    ItemId = "item_mat_qingxin_grass",
                    Count = 2,
                    ConsumedOnFail = false
                },
                new CraftIngredient
                {
                    ItemId = "item_mat_spirit_dust",
                    Count = 1,
                    ConsumedOnFail = true
                });
            RegisterBuiltInAlchemyRecipe(
                "recipe_mana_01",
                "回气丹",
                1,
                0.9f,
                "item_potion_mana_01",
                new CraftIngredient
                {
                    ItemId = "item_mat_qingxin_grass",
                    Count = 1,
                    ConsumedOnFail = false
                },
                new CraftIngredient
                {
                    ItemId = "item_mat_spirit_dust",
                    Count = 2,
                    ConsumedOnFail = true
                });
            RegisterBuiltInAlchemyRecipe(
                "recipe_body_01",
                "锻体丹",
                2,
                0.75f,
                "item_potion_body_01",
                new CraftIngredient
                {
                    ItemId = "item_mat_wolf_hair",
                    Count = 3,
                    ConsumedOnFail = false
                },
                new CraftIngredient
                {
                    ItemId = "item_mat_spirit_dust",
                    Count = 2,
                    ConsumedOnFail = true
                });
            RegisterBuiltInAlchemyRecipe(
                "recipe_xp_01",
                "聚气丹",
                3,
                0.7f,
                "item_potion_xp_01",
                new CraftIngredient
                {
                    ItemId = "item_mat_beast_core_1",
                    Count = 1,
                    ConsumedOnFail = false
                },
                new CraftIngredient
                {
                    ItemId = "item_mat_spirit_dust",
                    Count = 3,
                    ConsumedOnFail = true
                });
        }

        private void RegisterBuiltInMainQuestContent()
        {
            var yaoLaoKey = "npc_name_yaolao";
            var guardKey = "npc_name_cangwu_guard";
            var echoKey = "npc_name_blackwind_echo";
            BuiltInMainQuestDefinition[] definitions =
            {
                new BuiltInMainQuestDefinition
                {
                    Step = 1,
                    DisplayName = "灵根初醒",
                    Description = "接受药老的灵根测试，正式踏上修行之路。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = Array.Empty<string>(),
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Talk,
                            "npc_yaolao",
                            1,
                            "quest_obj_main_01_01_talk_yaolao",
                            "听药老讲解引气诀")
                    },
                    Rewards = MainReward(500f, 0),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "灵根既醒，先让我看看你能否守住这一缕气。",
                    CompleteText = "气沉丹田，意守灵台。《引气诀》的门，你已推开了。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 2,
                    DisplayName = "东郊猎狼",
                    Description = "前往东郊，击杀三只灰狼，再回来向药老复命。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(1) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Kill,
                            "enemy_wolf_gray",
                            3,
                            "quest_obj_main_01_02_kill_wolves",
                            "击杀灰狼")
                    },
                    Rewards = MainReward(700f, 10),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "东郊狼患渐重，去除掉三只灰狼。",
                    CompleteText = "气息沉稳了几分。此行所得，才是修行的开端。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 3,
                    DisplayName = "灵草涧",
                    Description = "采集五株清心草，并除掉盘踞灵草涧的灰爪。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(2) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Collect,
                            "item_mat_qingxin_grass",
                            5,
                            "quest_obj_main_01_03_collect_qingxin",
                            "收集清心草"),
                        MainObjective(
                            ObjectiveType.Kill,
                            "enemy_wolf_elite",
                            1,
                            "quest_obj_main_01_03_kill_elite",
                            "击败灰爪")
                    },
                    Rewards = MainReward(
                        1200f,
                        10,
                        new ItemStack
                        {
                            ItemId = "item_mat_spirit_dust",
                            Count = 2
                        }),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "灵草涧有清心草，也有一头成了气候的灰爪。",
                    CompleteText = "草叶无损，妖气已散。你的心比出发时更稳。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 4,
                    DisplayName = "炉火初试",
                    Description = "在丹炉炼成一枚回气丹。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(3) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Craft,
                            "item_potion_mana_01",
                            1,
                            "quest_obj_main_01_04_craft_mana",
                            "炼制回气丹")
                    },
                    Rewards = MainReward(1400f, 10),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "修士不能只会争斗。用带回的灵草炼一枚回气丹。",
                    CompleteText = "火候尚嫩，药性却已收住。第一炉，算成了。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 5,
                    DisplayName = "秘径北行",
                    Description = "前往青石镇西北的秘径入口，寻找通往苍梧的旧路。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(4) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Reach,
                            "area_qingshi_secret_path",
                            1,
                            "quest_obj_main_01_05_reach_path",
                            "抵达秘径入口")
                    },
                    Rewards = MainReward(1600f, 10),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "镇西北有条封了多年的秘径，去认一认路。",
                    CompleteText = "旧禁已解。沿秘径北上，便是苍梧山门。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 6,
                    DisplayName = "苍梧试炼",
                    Description = "通过山门弟子的盘山道试炼。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(5) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Kill,
                            "enemy_wolf_gray",
                            3,
                            "quest_obj_main_01_06_kill_trial",
                            "清理盘山道妖兽")
                    },
                    Rewards = MainReward(1800f, 10),
                    StartNpcId = "npc_cangwu_guard",
                    TurnInNpcId = "npc_cangwu_guard",
                    StartSpeakerKey = guardKey,
                    StartSpeaker = "山门弟子",
                    CompleteSpeakerKey = guardKey,
                    CompleteSpeaker = "山门弟子",
                    StartText = "苍梧不收空口求道之人。先清理盘山道的妖兽。",
                    CompleteText = "身手尚可。山中有一桩筑基机缘，也许与你有缘。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 7,
                    DisplayName = "筑基机缘",
                    Description = "向山门弟子追问筑基机缘的去向。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(6) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Talk,
                            "npc_cangwu_guard",
                            1,
                            "quest_obj_main_01_07_talk_guard",
                            "询问筑基机缘")
                    },
                    Rewards = MainReward(2200f, 0),
                    StartNpcId = "npc_cangwu_guard",
                    TurnInNpcId = "npc_cangwu_guard",
                    StartSpeakerKey = guardKey,
                    StartSpeaker = "山门弟子",
                    CompleteSpeakerKey = guardKey,
                    CompleteSpeaker = "山门弟子",
                    StartText = "药老曾托人留话：你的筑基之物，仍要回青石取。",
                    CompleteText = "回去吧。破境之前，先问清自己为何求道。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 8,
                    DisplayName = "一朝筑基",
                    Description = "服下筑基丹，将练气修为推至筑基境。",
                    RequiredRealm = (int)RealmType.QiCondensation,
                    Prerequisites = new[] { MainQuestId(7) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.ReachRealm,
                            RealmType.Foundation.ToString(),
                            1,
                            "quest_obj_main_01_08_reach_foundation",
                            "突破至筑基境")
                    },
                    AcceptRewards = MainReward(
                        0f,
                        0,
                        new ItemStack
                        {
                            ItemId = "item_pill_foundation",
                            Count = 1
                        }),
                    Rewards = MainReward(23000f, 10),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_yaolao",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = yaoLaoKey,
                    CompleteSpeaker = "药老",
                    StartText = "这枚筑基丹只替你推门，门后的路仍要自己走。",
                    CompleteText = "道基已立。往后每一步，都比从前更重。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 9,
                    DisplayName = "金丹叩关",
                    Description = "收好凝金丹，结成金丹后前往黑风秘境入口。",
                    RequiredRealm = (int)RealmType.Foundation,
                    Prerequisites = new[] { MainQuestId(8) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Collect,
                            "item_pill_goldencore",
                            1,
                            "quest_obj_main_01_09_collect_pill",
                            "收下凝金丹",
                            true),
                        MainObjective(
                            ObjectiveType.ReachRealm,
                            RealmType.GoldenCore.ToString(),
                            1,
                            "quest_obj_main_01_09_reach_goldencore",
                            "突破至金丹境"),
                        MainObjective(
                            ObjectiveType.Reach,
                            "blackwind_entrance",
                            1,
                            "quest_obj_main_01_09_reach_entrance",
                            "抵达黑风秘境入口")
                    },
                    Ordered = true,
                    AcceptRewards = MainReward(
                        0f,
                        0,
                        new ItemStack
                        {
                            ItemId = "item_pill_goldencore",
                            Count = 1
                        }),
                    Rewards = MainReward(1500f, 10),
                    StartNpcId = "npc_yaolao",
                    TurnInNpcId = "npc_blackwind_echo",
                    StartSpeakerKey = yaoLaoKey,
                    StartSpeaker = "药老",
                    CompleteSpeakerKey = echoKey,
                    CompleteSpeaker = "残魂",
                    StartText = "凝金一关，先凝心，再凝丹。黑风入口会验证你的道行。",
                    CompleteText = "金丹气息……终于又有人走到这里。石将军在最深处等你。"
                },
                new BuiltInMainQuestDefinition
                {
                    Step = 10,
                    DisplayName = "黑风问道",
                    Description = "深入黑风秘境，击败镇守最深处的石将军。",
                    RequiredRealm = (int)RealmType.GoldenCore,
                    Prerequisites = new[] { MainQuestId(9) },
                    Objectives = new[]
                    {
                        MainObjective(
                            ObjectiveType.Kill,
                            "enemy_boss_stone_general",
                            1,
                            "quest_obj_main_01_10_kill_boss",
                            "击败黑风石将军")
                    },
                    Rewards = MainReward(2000f, 15),
                    StartNpcId = "npc_blackwind_echo",
                    TurnInNpcId = "npc_blackwind_echo",
                    StartSpeakerKey = echoKey,
                    StartSpeaker = "残魂",
                    CompleteSpeakerKey = echoKey,
                    CompleteSpeaker = "残魂",
                    StartText = "碑纹引来无数贪念。击败石将军，证明你不是下一个夺碑者。",
                    CompleteText = "石锁已断，碑纹却指向更远处。拓跋渊……他还活着。"
                }
            };

            for (int index = 0; index < definitions.Length; index++)
            {
                RegisterBuiltInMainQuest(definitions[index]);
            }

            EnsureBuiltInMainQuestNpc(
                "npc_yaolao",
                yaoLaoKey,
                "药老",
                MainDialogueId(1, false));
            EnsureBuiltInMainQuestNpc(
                "npc_cangwu_guard",
                guardKey,
                "山门弟子",
                MainDialogueId(6, false));
            EnsureBuiltInMainQuestNpc(
                "npc_blackwind_echo",
                echoKey,
                "残魂",
                MainDialogueId(9, true));
        }

        private void RegisterBuiltInMainQuest(
            BuiltInMainQuestDefinition definition)
        {
            string questId = MainQuestId(definition.Step);
            QuestData quest = GetQuest(questId);
            if (quest == null)
            {
                quest = CreateGenerated<QuestData>(
                    $"Quest_Main01{definition.Step:00}_Runtime");
                quest.Id = questId;
                RegisterQuest(quest);
            }

            quest.DisplayNameKey = $"quest_name_main_01_{definition.Step:00}";
            quest.DisplayName = definition.DisplayName;
            quest.DescriptionKey = $"quest_desc_main_01_{definition.Step:00}";
            quest.Description = definition.Description;
            quest.Type = QuestType.Main;
            quest.RequiredRealm = definition.RequiredRealm;
            quest.PrerequisiteQuestIds = definition.Prerequisites
                ?? Array.Empty<string>();
            quest.Objectives = definition.Objectives
                ?? Array.Empty<QuestObjective>();
            quest.ObjectivesAreOrdered = definition.Ordered;
            quest.AcceptRewards = definition.AcceptRewards;
            quest.Rewards = definition.Rewards ?? MainReward(0f, 0);
            quest.StartDialogueId = MainDialogueId(definition.Step, false);
            quest.CompleteDialogueId = MainDialogueId(definition.Step, true);
            quest.StartNpcId = definition.StartNpcId;
            quest.TurnInNpcId = definition.TurnInNpcId;

            RegisterBuiltInMainDialogue(
                quest.StartDialogueId,
                quest.Id,
                false,
                definition.StartSpeakerKey,
                definition.StartSpeaker,
                definition.StartText);
            RegisterBuiltInMainDialogue(
                quest.CompleteDialogueId,
                quest.Id,
                true,
                definition.CompleteSpeakerKey,
                definition.CompleteSpeaker,
                definition.CompleteText);
        }

        private void RegisterBuiltInSideAndDailyQuestContent()
        {
            RegisterBuiltInBanditEnemy();
            RegisterBuiltInQuestItem(
                "item_quest_hermit_letter",
                "item_name_item_quest_hermit_letter",
                "药老手书",
                "item_desc_item_quest_hermit_letter",
                "药老封好的手书，需亲手交给苍梧洞府隐士。");

            RegisterBuiltInAuxiliaryQuest(
                "quest_side_herb_01",
                "quest_name_side_herb_01",
                "清心十株",
                "quest_desc_side_herb_01",
                "为丹鼎接引采回十株清心草。",
                QuestType.Side,
                new[]
                {
                    MainObjective(
                        ObjectiveType.Collect,
                        "item_mat_qingxin_grass",
                        10,
                        "quest_obj_side_herb_01_collect",
                        "收集清心草")
                },
                null,
                Array.Empty<ItemStack>(),
                AuxiliaryReward(
                    0f,
                    50,
                    20,
                    "faction_danding"),
                "npc_danding_guide",
                "npc_danding_guide",
                "dlg_side_herb_01_start",
                "dlg_side_herb_01_complete",
                "npc_name_danding_guide",
                "丹鼎接引",
                "丹炉近来缺一味清凉主材，劳你采十株清心草。",
                "叶脉清透，药性未散。宗门会记下这份心意。");

            RegisterBuiltInAuxiliaryQuest(
                "quest_side_bandit_01",
                "quest_name_side_bandit_01",
                "散修之患",
                "quest_desc_side_bandit_01",
                "清理青石原上劫掠行人的八名散修。",
                QuestType.Side,
                new[]
                {
                    MainObjective(
                        ObjectiveType.Kill,
                        "enemy_bandit",
                        8,
                        "quest_obj_side_bandit_01_kill",
                        "击败劫掠散修")
                },
                null,
                Array.Empty<ItemStack>(),
                AuxiliaryReward(
                    200f,
                    0,
                    0,
                    string.Empty,
                    new ItemStack
                    {
                        ItemId = "eq_weapon_iron_sword",
                        Count = 1
                    }),
                "npc_trainer",
                "npc_trainer",
                "dlg_side_bandit_01_start",
                "dlg_side_bandit_01_complete",
                "npc_name_trainer",
                "武教习",
                "镇外有散修拦路劫掠，除掉八人再回来。",
                "出手有分寸，也护住了行路之人。这柄铁剑归你。");

            RegisterBuiltInAuxiliaryQuest(
                "quest_side_hermit_01",
                "quest_name_side_hermit_01",
                "洞府手书",
                "quest_desc_side_hermit_01",
                "将药老手书送至苍梧洞府隐士手中。",
                QuestType.Side,
                new[]
                {
                    MainObjective(
                        ObjectiveType.Talk,
                        "npc_hermit",
                        1,
                        "quest_obj_side_hermit_01_deliver",
                        "向洞府隐士送达药老手书")
                },
                AuxiliaryReward(
                    0f,
                    0,
                    0,
                    string.Empty,
                    new ItemStack
                    {
                        ItemId = "item_quest_hermit_letter",
                        Count = 1
                    }),
                new[]
                {
                    new ItemStack
                    {
                        ItemId = "item_quest_hermit_letter",
                        Count = 1
                    }
                },
                AuxiliaryReward(
                    0f,
                    0,
                    0,
                    string.Empty,
                    Array.Empty<ItemStack>(),
                    "skill_wind_slash"),
                "npc_danding_guide",
                "npc_hermit",
                "dlg_side_hermit_01_start",
                "dlg_hermit_side",
                "npc_name_danding_guide",
                "丹鼎接引",
                "这封手书须交给苍梧洞府的隐士，途中莫要拆封。",
                "故人字迹依旧。你替我送来此信，我便传你一式风刃。");

            RegisterBuiltInAuxiliaryQuest(
                "quest_daily_hunt",
                "quest_name_daily_hunt",
                "今日猎妖",
                "quest_desc_daily_hunt",
                "在本轮日常中击败任意十五名敌人。",
                QuestType.Daily,
                new[]
                {
                    MainObjective(
                        ObjectiveType.Kill,
                        "*",
                        15,
                        "quest_obj_daily_hunt",
                        "击败任意敌人")
                },
                null,
                Array.Empty<ItemStack>(),
                AuxiliaryReward(
                    0f,
                    30,
                    0,
                    string.Empty,
                    new ItemStack
                    {
                        ItemId = "item_potion_xp_01",
                        Count = 1
                    }),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            RegisterBuiltInAuxiliaryQuest(
                "quest_daily_gather",
                "quest_name_daily_gather",
                "今日采撷",
                "quest_desc_daily_gather",
                "在本轮日常中完成五次采集。",
                QuestType.Daily,
                new[]
                {
                    MainObjective(
                        ObjectiveType.Collect,
                        "*",
                        5,
                        "quest_obj_daily_gather",
                        "采集任意材料")
                },
                null,
                Array.Empty<ItemStack>(),
                AuxiliaryReward(
                    0f,
                    0,
                    0,
                    string.Empty,
                    new ItemStack
                    {
                        ItemId = "item_mat_spirit_dust",
                        Count = 5
                    }),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            EnsureBuiltInMainQuestNpc(
                "npc_danding_guide",
                "npc_name_danding_guide",
                "丹鼎接引",
                "dlg_side_herb_01_start");
            EnsureBuiltInMainQuestNpc(
                "npc_trainer",
                "npc_name_trainer",
                "武教习",
                "dlg_side_bandit_01_start");
            EnsureBuiltInMainQuestNpc(
                "npc_hermit",
                "npc_name_hermit",
                "洞府隐士",
                "dlg_hermit_side");
        }

        private void RegisterBuiltInBanditEnemy()
        {
            if (GetEnemy("enemy_bandit") != null)
            {
                return;
            }

            EnemyData bandit = CreateGenerated<EnemyData>(
                "Enemy_Bandit_Runtime");
            bandit.Id = "enemy_bandit";
            bandit.DisplayName = "散修";
            bandit.Rank = EnemyRank.Normal;
            bandit.Realm = RealmType.QiCondensation;
            bandit.SubStage = 5;
            bandit.MaxHp = 200f;
            bandit.Attack = 20f;
            bandit.Defense = 4f;
            bandit.MoveSpeed = 3f;
            bandit.AggroRange = 9f;
            bandit.AttackRange = 1.8f;
            bandit.DisengageRange = 16f;
            bandit.AttackInterval = 1.4f;
            bandit.CultivationXpReward = 40f;
            bandit.MinSpiritStones = 0;
            bandit.MaxSpiritStones = 2;
            bandit.LootTable = Array.Empty<LootEntry>();
            bandit.SkillIds = Array.Empty<string>();
            bandit.BossPhases = Array.Empty<BossPhase>();
            RegisterEnemy(bandit);
        }

        private void RegisterBuiltInSerendipityContent()
        {
            RegisterBuiltInBlackStoneItem();
            RegisterBuiltInSerendipity(
                "serendipity_qingshi_herb_spirit",
                "map_qingshi",
                "trigger_serendipity_qingshi_herb_spirit",
                "flag_serendipity_qingshi_herb_spirit",
                "dlg_serendipity_qingshi_herb_spirit",
                "serendipity_speaker_herb_spirit",
                "草木灵息",
                "serendipity_text_qingshi_herb_spirit",
                "溪畔灵气聚而不散，几株清心草在微光中自行舒展。",
                10,
                new ItemStack
                {
                    ItemId = "item_mat_qingxin_grass",
                    Count = 3
                });
            RegisterBuiltInSerendipity(
                "serendipity_cangwu_mist_stele",
                "map_cangwu",
                "trigger_serendipity_cangwu_mist_stele",
                "lore_cangwu_mist_stele",
                "dlg_serendipity_cangwu_mist_stele",
                "serendipity_speaker_mist_stele",
                "雾中古碑",
                "serendipity_text_cangwu_mist_stele",
                "碑纹只显一瞬：长生非夺天地，而在不负此心。",
                0,
                new ItemStack
                {
                    ItemId = "item_skill_scroll",
                    Count = 1
                });
            RegisterBuiltInSerendipity(
                "serendipity_cangwu_cliff_box",
                "map_cangwu",
                "trigger_serendipity_cangwu_cliff_box",
                "flag_serendipity_cangwu_cliff_box",
                "dlg_serendipity_cangwu_cliff_box",
                "serendipity_speaker_cliff_box",
                "崖间旧匣",
                "serendipity_text_cangwu_cliff_box",
                "风蚀木匣卡在崖缝，匣中灵尘仍保持着微弱光泽。",
                20,
                new ItemStack
                {
                    ItemId = "item_mat_spirit_dust",
                    Count = 5
                });
            RegisterBuiltInSerendipity(
                "serendipity_blackwind_echo_cache",
                "map_blackwind",
                "trigger_serendipity_blackwind_echo_cache",
                "flag_serendipity_blackwind_echo_cache",
                "dlg_serendipity_blackwind_echo_cache",
                "serendipity_speaker_blackwind_echo",
                "黑风残响",
                "serendipity_text_blackwind_echo_cache",
                "残响散去后，石缝里露出一包未被阴风侵蚀的旧物。",
                30,
                new ItemStack
                {
                    ItemId = "item_mat_black_stone",
                    Count = 3
                });
        }

        private void RegisterBuiltInSerendipity(
            string id,
            string mapId,
            string triggerId,
            string worldFlag,
            string dialogueId,
            string speakerKey,
            string speaker,
            string textKey,
            string text,
            int spiritStones,
            params ItemStack[] items)
        {
            if (GetSerendipity(id) == null)
            {
                SerendipityData data = CreateGenerated<SerendipityData>(
                    "Serendipity_" + id + "_Runtime");
                data.Id = id;
                data.MapId = mapId;
                data.TriggerId = triggerId;
                data.OnceOnly = true;
                data.RequiredQuestId = string.Empty;
                data.RequiredRealm = RealmType.Mortal;
                data.Rewards = new QuestReward
                {
                    CultivationXp = 0f,
                    SpiritStones = Mathf.Max(0, spiritStones),
                    Items = items ?? Array.Empty<ItemStack>(),
                    FactionRep = 0,
                    FactionId = string.Empty,
                    SkillIds = Array.Empty<string>()
                };
                data.DialogueId = dialogueId;
                data.WorldFlag = worldFlag;
                RegisterSerendipity(data);
            }

            if (GetDialogue(dialogueId) == null)
            {
                DialogueData dialogue = CreateGenerated<DialogueData>(
                    "Dialogue_" + dialogueId + "_Runtime");
                dialogue.Id = dialogueId;
                dialogue.Nodes = new[]
                {
                    new DialogueNode
                    {
                        NodeId = "reveal",
                        SpeakerNameKey = speakerKey,
                        SpeakerName = speaker,
                        TextKey = textKey,
                        Text = text,
                        End = true
                    }
                };
                RegisterDialogue(dialogue);
            }
        }

        private void RegisterBuiltInBlackStoneItem()
        {
            if (GetItem("item_mat_black_stone") != null)
            {
                return;
            }

            ItemData item = CreateGenerated<ItemData>(
                "Item_BlackStone_Runtime");
            item.Id = "item_mat_black_stone";
            item.DisplayNameKey = "item_name_item_mat_black_stone";
            item.DisplayName = "黑风石屑";
            item.DescriptionKey = "item_desc_item_mat_black_stone";
            item.Description = "被黑风侵蚀的石屑，仍残留微弱阴灵气息。";
            item.Type = ItemType.Material;
            item.Rarity = ItemRarity.Uncommon;
            item.MaxStack = 99;
            item.IsBound = false;
            item.BuyPrice = 0;
            item.SellPrice = 8;
            item.RequiredRealm = 0;
            item.UseEffects = Array.Empty<UseEffect>();
            item.AcquisitionHintKeys = new[]
            {
                "hint_serendipity_blackwind_echo_cache"
            };
            RegisterItem(item);
        }

        private void RegisterBuiltInAuxiliaryQuest(
            string questId,
            string displayNameKey,
            string displayName,
            string descriptionKey,
            string description,
            QuestType type,
            QuestObjective[] objectives,
            QuestReward acceptRewards,
            ItemStack[] turnInCosts,
            QuestReward rewards,
            string startNpcId,
            string turnInNpcId,
            string startDialogueId,
            string completeDialogueId,
            string speakerKey,
            string speaker,
            string startText,
            string completeText)
        {
            QuestData quest = GetQuest(questId);
            if (quest == null)
            {
                quest = CreateGenerated<QuestData>(
                    "Quest_" + questId + "_Runtime");
                quest.Id = questId;
                RegisterQuest(quest);
            }

            quest.DisplayNameKey = displayNameKey;
            quest.DisplayName = displayName;
            quest.DescriptionKey = descriptionKey;
            quest.Description = description;
            quest.Type = type;
            quest.RequiredRealm = (int)RealmType.QiCondensation;
            quest.PrerequisiteQuestIds = Array.Empty<string>();
            quest.Objectives = objectives ?? Array.Empty<QuestObjective>();
            quest.ObjectivesAreOrdered = false;
            quest.AcceptRewards = acceptRewards;
            quest.TurnInCosts = turnInCosts ?? Array.Empty<ItemStack>();
            quest.Rewards = rewards ?? AuxiliaryReward(0f, 0, 0, string.Empty);
            quest.StartNpcId = startNpcId ?? string.Empty;
            quest.TurnInNpcId = turnInNpcId ?? string.Empty;
            quest.StartDialogueId = startDialogueId ?? string.Empty;
            quest.CompleteDialogueId = completeDialogueId ?? string.Empty;

            if (!string.IsNullOrEmpty(startDialogueId))
            {
                RegisterBuiltInMainDialogue(
                    startDialogueId,
                    questId,
                    false,
                    speakerKey,
                    speaker,
                    startText);
            }

            if (!string.IsNullOrEmpty(completeDialogueId))
            {
                string completeSpeakerKey = string.Equals(
                    turnInNpcId,
                    "npc_hermit",
                    StringComparison.Ordinal)
                    ? "npc_name_hermit"
                    : speakerKey;
                string completeSpeaker = string.Equals(
                    turnInNpcId,
                    "npc_hermit",
                    StringComparison.Ordinal)
                    ? "洞府隐士"
                    : speaker;
                RegisterBuiltInMainDialogue(
                    completeDialogueId,
                    questId,
                    true,
                    completeSpeakerKey,
                    completeSpeaker,
                    completeText);
            }
        }

        private void RegisterBuiltInQuestItem(
            string id,
            string displayNameKey,
            string displayName,
            string descriptionKey,
            string description)
        {
            if (GetItem(id) != null)
            {
                return;
            }

            ItemData item = CreateGenerated<ItemData>("Item_" + id + "_Runtime");
            item.Id = id;
            item.DisplayNameKey = displayNameKey;
            item.DisplayName = displayName;
            item.DescriptionKey = descriptionKey;
            item.Description = description;
            item.Type = ItemType.Quest;
            item.Rarity = ItemRarity.Common;
            item.MaxStack = 1;
            item.IsBound = true;
            item.BuyPrice = 0;
            item.SellPrice = 0;
            item.RequiredRealm = (int)RealmType.QiCondensation;
            item.UseEffects = Array.Empty<UseEffect>();
            item.AcquisitionHintKeys = new[] { "hint_quest_side_hermit_01" };
            RegisterItem(item);
        }

        private static QuestReward AuxiliaryReward(
            float cultivationXp,
            int spiritStones,
            int factionRep,
            string factionId,
            params ItemStack[] items)
        {
            return AuxiliaryReward(
                cultivationXp,
                spiritStones,
                factionRep,
                factionId,
                items,
                Array.Empty<string>());
        }

        private static QuestReward AuxiliaryReward(
            float cultivationXp,
            int spiritStones,
            int factionRep,
            string factionId,
            ItemStack[] items,
            params string[] skillIds)
        {
            return new QuestReward
            {
                CultivationXp = Mathf.Max(0f, cultivationXp),
                SpiritStones = Mathf.Max(0, spiritStones),
                Items = items ?? Array.Empty<ItemStack>(),
                FactionRep = Mathf.Max(0, factionRep),
                FactionId = factionId ?? string.Empty,
                SkillIds = skillIds ?? Array.Empty<string>()
            };
        }

        private void RegisterBuiltInMainDialogue(
            string dialogueId,
            string questId,
            bool turnIn,
            string speakerKey,
            string speaker,
            string text)
        {
            if (GetDialogue(dialogueId) != null)
            {
                return;
            }

            DialogueData dialogue = CreateGenerated<DialogueData>(
                "Dialogue_" + dialogueId + "_Runtime");
            dialogue.Id = dialogueId;
            dialogue.Nodes = new[]
            {
                new DialogueNode
                {
                    NodeId = turnIn ? "turn_in" : "offer",
                    SpeakerNameKey = speakerKey,
                    SpeakerName = speaker,
                    TextKey = dialogueId + "_01",
                    Text = text,
                    QuestOfferId = turnIn ? string.Empty : questId,
                    QuestTurnInId = turnIn ? questId : string.Empty,
                    End = true
                }
            };
            RegisterDialogue(dialogue);
        }

        private void EnsureBuiltInMainQuestNpc(
            string npcId,
            string displayNameKey,
            string displayName,
            string defaultDialogueId)
        {
            NPCData npc = GetNpc(npcId);
            if (npc == null)
            {
                npc = CreateGenerated<NPCData>("NPC_" + npcId + "_Runtime");
                npc.Id = npcId;
                npc.FactionId = string.Empty;
                npc.IsVendor = false;
                npc.VendorItemIds = Array.Empty<string>();
                npc.AffectionMilestones = Array.Empty<AffectionMilestone>();
                RegisterNpc(npc);
            }

            npc.DisplayNameKey = displayNameKey;
            npc.DisplayName = displayName;
            npc.DefaultDialogueId = defaultDialogueId;
        }

        private static QuestObjective MainObjective(
            ObjectiveType type,
            string targetId,
            int requiredCount,
            string descriptionKey,
            string description,
            bool latch = false)
        {
            return new QuestObjective
            {
                Type = type,
                TargetId = targetId,
                RequiredCount = Mathf.Max(1, requiredCount),
                DescriptionKey = descriptionKey,
                Description = description,
                LatchOnFirstAcquire = latch
            };
        }

        private static QuestReward MainReward(
            float cultivationXp,
            int spiritStones,
            params ItemStack[] items)
        {
            return new QuestReward
            {
                CultivationXp = Mathf.Max(0f, cultivationXp),
                SpiritStones = Mathf.Max(0, spiritStones),
                Items = items ?? Array.Empty<ItemStack>(),
                FactionRep = 0,
                FactionId = string.Empty
            };
        }

        private static string MainQuestId(int step)
        {
            return $"quest_main_01_{Mathf.Clamp(step, 1, 10):00}";
        }

        private static string MainDialogueId(int step, bool complete)
        {
            return $"dlg_main_01_{Mathf.Clamp(step, 1, 10):00}_{(complete ? "complete" : "start")}";
        }

        private void RegisterBuiltInAlchemyItem(
            string id,
            string displayName,
            string description,
            ItemType type,
            ItemRarity rarity,
            int maxStack,
            int sellPrice,
            UseEffect[] useEffects)
        {
            if (GetItem(id) != null)
            {
                return;
            }

            ItemData item = CreateGenerated<ItemData>("Item_" + id + "_Runtime");
            item.Id = id;
            item.DisplayName = displayName;
            item.Description = description;
            item.Type = type;
            item.Rarity = rarity;
            item.MaxStack = Mathf.Max(1, maxStack);
            item.IsBound = false;
            item.SellPrice = Mathf.Max(0, sellPrice);
            item.RequiredRealm = 0;
            item.UseEffects = useEffects ?? Array.Empty<UseEffect>();
            item.AcquisitionHintKeys = Array.Empty<string>();
            RegisterItem(item);
        }

        private void RegisterBuiltInAlchemyRecipe(
            string id,
            string displayName,
            int requiredLevel,
            float successRate,
            string resultItemId,
            params CraftIngredient[] ingredients)
        {
            if (GetRecipe(id) != null)
            {
                return;
            }

            CraftRecipeData recipe = CreateGenerated<CraftRecipeData>(
                "Recipe_" + id + "_Runtime");
            recipe.Id = id;
            recipe.DisplayName = displayName;
            recipe.CraftType = CraftType.Alchemy;
            recipe.RequiredCraftLevel = Mathf.Max(1, requiredLevel);
            recipe.BaseSuccessRate = Mathf.Clamp01(successRate);
            recipe.CraftTime = 0.5f;
            recipe.Ingredients = ingredients ?? Array.Empty<CraftIngredient>();
            recipe.SuccessResult = new CraftResult
            {
                ItemId = resultItemId,
                MinCount = 1,
                MaxCount = 1
            };
            recipe.FailResult = null;
            RegisterRecipe(recipe);
        }

        private void RegisterBuiltInAchievementsAndTitles()
        {
            RegisterBuiltInTitle(
                "title_yaotu_apprentice",
                "斩妖学徒",
                "初经百战，已懂得在妖兽利爪下守住心神。",
                new StatBlock { Attack = 2f, CritDamage = 0f });
            RegisterBuiltInTitle(
                "title_yaotu",
                "斩妖人",
                "千战留名，寻常妖物闻风退避。",
                new StatBlock
                {
                    Attack = 5f,
                    CritRate = 0.01f,
                    CritDamage = 0f
                });
            RegisterBuiltInTitle(
                "title_wendao",
                "问道者",
                "走过青石十问，初心仍在大道之上。",
                new StatBlock { CultivationSpeed = 0.05f, CritDamage = 0f });
            RegisterBuiltInTitle(
                "title_poshi",
                "破石",
                "击破黑风石将军后留下的战名。",
                new StatBlock { Defense = 8f, CritDamage = 0f });
            RegisterBuiltInTitle(
                "title_tiegu",
                "铁骨",
                "废脉炼体，骨血如铁，最大气血提高百分之五。",
                new StatBlock { CritDamage = 0f });

            RegisterBuiltInAchievement(
                "ach_first_kill",
                "初入仙途",
                "首次击败敌人。",
                "KillTotal",
                string.Empty,
                1f,
                string.Empty,
                0,
                new ItemStack
                {
                    ItemId = "item_potion_heal_01",
                    Count = 5
                });
            RegisterBuiltInAchievement(
                "ach_kill_100",
                "小试牛刀",
                "累计击败一百名敌人。",
                "KillTotal",
                string.Empty,
                100f,
                "title_yaotu_apprentice",
                0);
            RegisterBuiltInAchievement(
                "ach_kill_1000",
                "斩妖能手",
                "累计击败一千名敌人。",
                "KillTotal",
                string.Empty,
                1000f,
                "title_yaotu",
                0);
            RegisterBuiltInAchievement(
                "ach_realm_found",
                "筑基有成",
                "成功踏入筑基境。",
                "RealmReached",
                "Foundation",
                1f,
                string.Empty,
                50);
            RegisterBuiltInAchievement(
                "ach_realm_core",
                "金丹初成",
                "成功凝结金丹。",
                "RealmReached",
                "GoldenCore",
                1f,
                string.Empty,
                100);
            RegisterBuiltInAchievement(
                "ach_quest_ch1",
                "青石问道",
                "完成青石主线十问。",
                "QuestCompleted",
                "quest_main_01_10",
                1f,
                "title_wendao",
                0);
            RegisterBuiltInAchievement(
                "ach_alchemy_10",
                "丹火初燃",
                "成功炼丹十次。",
                "CraftCount",
                string.Empty,
                10f,
                string.Empty,
                0);
            RegisterBuiltInAchievement(
                "ach_secret_chest",
                "云雾藏宝",
                "在苍梧断崖发现旧匣。",
                "Flag",
                "flag_serendipity_cangwu_cliff_box",
                1f,
                string.Empty,
                30);
            RegisterBuiltInAchievement(
                "ach_boss_stone",
                "石将军终结者",
                "击败黑风石将军。",
                "KillEnemy",
                "enemy_boss_stone_general",
                1f,
                "title_poshi",
                0);
            RegisterBuiltInAchievement(
                "ach_waste_body",
                "废脉锻体",
                "以废脉之身炼成铜皮铁骨。",
                "BodyLevel+Root",
                "Waste&Copper",
                1f,
                "title_tiegu",
                0);
        }

        private void RegisterBuiltInTitle(
            string id,
            string displayName,
            string description,
            StatBlock bonus)
        {
            if (GetTitle(id) != null)
            {
                return;
            }

            TitleData title = CreateGenerated<TitleData>("Title_" + id + "_Runtime");
            title.Id = id;
            title.DisplayName = displayName;
            title.Description = description;
            title.Bonus = bonus ?? new StatBlock();
            title.ShowInNameplate = true;
            RegisterTitle(title);
        }

        private void RegisterBuiltInAchievement(
            string id,
            string displayName,
            string description,
            string triggerType,
            string targetId,
            float requiredValue,
            string rewardTitleId,
            int rewardSpiritStones,
            params ItemStack[] rewardItems)
        {
            if (GetAchievement(id) != null)
            {
                return;
            }

            AchievementData achievement = CreateGenerated<AchievementData>(
                "Achievement_" + id + "_Runtime");
            achievement.Id = id;
            achievement.DisplayName = displayName;
            achievement.Description = description;
            achievement.TriggerType = triggerType;
            achievement.TargetId = targetId ?? string.Empty;
            achievement.RequiredValue = Mathf.Max(1f, requiredValue);
            achievement.RewardTitleId = rewardTitleId ?? string.Empty;
            achievement.RewardItems = rewardItems ?? Array.Empty<ItemStack>();
            achievement.RewardSpiritStones = Mathf.Max(0, rewardSpiritStones);
            RegisterAchievement(achievement);
        }

        private void RegisterBuiltInStatusEffects()
        {
            RegisterBuiltInStatus(
                "status_burn",
                "status_name_burn",
                "灼烧",
                3f,
                3,
                ElementType.Fire,
                DamageType.Fire,
                dotDamagePerSecond: 0f,
                dotBaseDamageMultiplier: 0.05f);
            RegisterBuiltInStatus(
                "status_chill",
                "status_name_chill",
                "寒意",
                3f,
                2,
                ElementType.Ice,
                DamageType.Ice,
                moveSpeedMod: -0.3f,
                promoteAtMaxStacksStatusId: "status_freeze");
            RegisterBuiltInStatus(
                "status_freeze",
                "status_name_freeze",
                "冻结",
                1.5f,
                1,
                ElementType.Ice,
                DamageType.Ice,
                stun: true,
                reapplyCooldown: 8f);
            RegisterBuiltInStatus(
                "status_shock_stun",
                "status_name_shock_stun",
                "感电",
                0.5f,
                1,
                ElementType.None,
                DamageType.Lightning,
                stun: true);
            RegisterBuiltInStatus(
                "status_poison",
                "status_name_poison",
                "中毒",
                4f,
                3,
                ElementType.Poison,
                DamageType.Poison,
                dotDamagePerSecond: 4f);
            RegisterBuiltInStatus(
                "status_wet",
                "status_name_wet",
                "潮湿",
                4f,
                1,
                ElementType.Water,
                DamageType.Physical);
            RegisterBuiltInStatus(
                "status_wood_mark",
                "status_name_wood_mark",
                "木灵印",
                4f,
                1,
                ElementType.Wood,
                DamageType.Physical);
            RegisterBuiltInStatus(
                "status_sever_def",
                "status_name_sever_def",
                "破甲",
                3f,
                1,
                ElementType.None,
                DamageType.Physical,
                defenseMod: -0.1f);
            RegisterBuiltInStatus(
                "status_heart_demon",
                "status_name_heart_demon",
                "心魔",
                30f,
                1,
                ElementType.None,
                DamageType.Dark,
                damageDealtMod: -0.1f);
        }

        private void RegisterBuiltInSkill(
            string id,
            string displayName,
            string description,
            SkillType type,
            SkillElement element,
            RealmType requiredRealm,
            float baseDamage,
            float damagePerLevel,
            float manaCost,
            float cooldown,
            float castTime,
            float recoveryTime,
            float range,
            float radius,
            bool projectile,
            string projectileVfxId,
            string impactVfxId,
            string statusOnHitId = "",
            float statusChance = 0f,
            params SpiritRootType[] preferredRoots)
        {
            if (GetSkill(id) != null)
            {
                return;
            }

            SkillData skill = CreateGenerated<SkillData>(
                "Skill_" + id + "_Runtime");
            skill.Id = id;
            skill.DisplayName = displayName;
            skill.Description = description;
            skill.Type = type;
            skill.Element = element;
            skill.MaxLevel = 5;
            skill.RequiredRealm = (int)requiredRealm;
            skill.BaseCooldown = Mathf.Max(0f, cooldown);
            skill.BaseManaCost = Mathf.Max(0f, manaCost);
            skill.BaseDamage = Mathf.Max(0f, baseDamage);
            skill.DamagePerLevel = Mathf.Max(0f, damagePerLevel);
            skill.CastTime = Mathf.Max(0f, castTime);
            skill.RecoveryTime = Mathf.Max(0f, recoveryTime);
            skill.Range = Mathf.Max(0f, range);
            skill.Radius = Mathf.Max(0f, radius);
            skill.IsProjectile = projectile;
            skill.ProjectileVfxId = projectileVfxId ?? string.Empty;
            skill.ImpactVfxId = impactVfxId ?? string.Empty;
            skill.StatusOnHitId = statusOnHitId ?? string.Empty;
            skill.StatusChance = Mathf.Clamp01(statusChance);
            skill.PreferredRoots = preferredRoots ?? Array.Empty<SpiritRootType>();
            skill.LevelTable = Array.Empty<LevelScaling>();
            RegisterSkill(skill);
        }

        private void RegisterBuiltInStatus(
            string id,
            string displayNameKey,
            string displayName,
            float duration,
            int maxStacks,
            ElementType auraElement,
            DamageType dotType,
            float dotDamagePerSecond = 0f,
            float dotBaseDamageMultiplier = 0f,
            float moveSpeedMod = 0f,
            float attackMod = 0f,
            float damageDealtMod = 0f,
            float defenseMod = 0f,
            bool stun = false,
            bool root = false,
            bool silence = false,
            float reapplyCooldown = 0f,
            string promoteAtMaxStacksStatusId = "")
        {
            if (GetStatusEffect(id) != null)
            {
                return;
            }

            StatusEffectData status = CreateGenerated<StatusEffectData>(
                "Status_" + id + "_Runtime");
            status.Id = id;
            status.DisplayNameKey = displayNameKey;
            status.DisplayName = displayName;
            status.Duration = duration;
            status.MaxStacks = Mathf.Max(1, maxStacks);
            status.IsDebuff = true;
            status.AuraElement = auraElement;
            status.DotDamagePerSecond = Mathf.Max(0f, dotDamagePerSecond);
            status.DotBaseDamageMultiplier = Mathf.Max(
                0f,
                dotBaseDamageMultiplier);
            status.DotInterval = 1f;
            status.DotType = dotType;
            status.MoveSpeedMod = moveSpeedMod;
            status.AttackMod = attackMod;
            status.DamageDealtMod = damageDealtMod;
            status.DefenseMod = defenseMod;
            status.Stun = stun;
            status.Root = root;
            status.Silence = silence;
            status.ReapplyCooldown = Mathf.Max(0f, reapplyCooldown);
            status.PromoteAtMaxStacksStatusId =
                promoteAtMaxStacksStatusId ?? string.Empty;
            RegisterStatusEffect(status);
        }

        private void ApplyLocalizationFallbackKeys()
        {
            foreach (ItemData item in _items.Values)
            {
                item.DisplayNameKey = EnsureKey(
                    item.DisplayNameKey,
                    "item_name_" + item.Id);
                item.DescriptionKey = EnsureKey(
                    item.DescriptionKey,
                    "item_desc_" + item.Id);
            }

            foreach (EquipmentData equipment in _equipment.Values)
            {
                equipment.DisplayNameKey = EnsureKey(
                    equipment.DisplayNameKey,
                    "item_name_" + equipment.Id);
            }

            foreach (SkillData skill in _skills.Values)
            {
                skill.DisplayNameKey = EnsureKey(
                    skill.DisplayNameKey,
                    "skill_name_" + skill.Id);
                skill.DescriptionKey = EnsureKey(
                    skill.DescriptionKey,
                    "skill_desc_" + skill.Id);
            }

            foreach (CraftRecipeData recipe in _recipes.Values)
            {
                recipe.DisplayNameKey = EnsureKey(
                    recipe.DisplayNameKey,
                    "recipe_name_" + recipe.Id);
            }

            foreach (EnemyData enemy in _enemies.Values)
            {
                enemy.DisplayNameKey = EnsureKey(
                    enemy.DisplayNameKey,
                    "enemy_name_" + enemy.Id);
            }

            foreach (AchievementData achievement in _achievements.Values)
            {
                achievement.DisplayNameKey = EnsureKey(
                    achievement.DisplayNameKey,
                    "achievement_name_" + achievement.Id);
                achievement.DescriptionKey = EnsureKey(
                    achievement.DescriptionKey,
                    "achievement_desc_" + achievement.Id);
            }

            foreach (TitleData title in _titles.Values)
            {
                title.DisplayNameKey = EnsureKey(
                    title.DisplayNameKey,
                    "title_name_" + title.Id);
                title.DescriptionKey = EnsureKey(
                    title.DescriptionKey,
                    "title_desc_" + title.Id);
            }
        }

        private static string EnsureKey(string current, string fallback)
        {
            return string.IsNullOrWhiteSpace(current) ? fallback : current;
        }

        private T CreateGenerated<T>(string objectName) where T : ScriptableObject
        {
            T content = ScriptableObject.CreateInstance<T>();
            content.name = objectName;
            content.hideFlags = HideFlags.DontSave;
            _generatedContent.Add(content);
            return content;
        }

        private void DestroyGeneratedContent()
        {
            foreach (ScriptableObject content in _generatedContent)
            {
                if (content == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(content);
                }
                else
                {
                    DestroyImmediate(content);
                }
            }

            _generatedContent.Clear();
        }

        private static bool ValidateRealm(RealmConfig config, out string error)
        {
            if (config?.Realms == null || config.Realms.Length == 0)
            {
                error = "RealmConfig.realms is empty.";
                return false;
            }

            var seen = new HashSet<int>();
            foreach (RealmEntry realm in config.Realms)
            {
                if (realm == null || !seen.Add(realm.Realm) || realm.SubStages <= 0)
                {
                    error = "RealmConfig contains a null, duplicate, or invalid realm.";
                    return false;
                }

                RealmBaseStats stats = realm.BaseStatsPerSubStage;
                if (realm.XpPerSubStage == null
                    || realm.XpPerSubStage.Length != realm.SubStages
                    || stats == null
                    || stats.MaxHp == null
                    || stats.MaxHp.Length != realm.SubStages
                    || stats.MaxMana == null
                    || stats.MaxMana.Length != realm.SubStages
                    || stats.Attack == null
                    || stats.Attack.Length != realm.SubStages
                    || stats.Defense == null
                    || stats.Defense.Length != realm.SubStages)
                {
                    error = $"Realm {realm.Realm} stage tables do not match subStages.";
                    return false;
                }
            }

            if (!seen.SetEquals(new[] { 1, 2, 3, 4 }))
            {
                error = "RealmConfig must contain realms 1 through 4 exactly once.";
                return false;
            }

            RealmEntry qi = config.Realms.FirstOrDefault(entry => entry.Realm == 1);
            if (qi == null || qi.XpPerSubStage.Length == 0 || !Approximately(qi.XpPerSubStage[0], 100f))
            {
                error = "RealmConfig is missing the authoritative Qi Condensation first-stage XP.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateSpiritRoot(SpiritRootConfig config, out string error)
        {
            if (config?.Roots == null || config.Roots.Length == 0)
            {
                error = "SpiritRootConfig.roots is empty.";
                return false;
            }

            var seen = new HashSet<SpiritRootType>();
            foreach (SpiritRootEntry root in config.Roots)
            {
                if (root == null
                    || root.Type == SpiritRootType.None
                    || !seen.Add(root.Type)
                    || root.ElementBonus == null
                    || root.CultivationMul <= 0f
                    || root.BodyMul <= 0f)
                {
                    error = "SpiritRootConfig contains an invalid or duplicate root.";
                    return false;
                }
            }

            var expectedRoots = new HashSet<SpiritRootType>
            {
                SpiritRootType.Metal,
                SpiritRootType.Wood,
                SpiritRootType.Water,
                SpiritRootType.Fire,
                SpiritRootType.Earth,
                SpiritRootType.Heaven,
                SpiritRootType.Waste
            };
            if (!seen.SetEquals(expectedRoots))
            {
                error = "SpiritRootConfig must contain all seven playable/random roots.";
                return false;
            }

            SpiritRootEntry water = config.Roots.FirstOrDefault(root => root.Type == SpiritRootType.Water);
            if (water == null
                || !water.ElementBonus.TryGetValue("Water", out float waterBonus)
                || !water.ElementBonus.TryGetValue("Ice", out float iceBonus)
                || !Approximately(waterBonus, 0.15f)
                || !Approximately(iceBonus, 0.10f))
            {
                error = "Water spirit root must retain Water 0.15 and Ice 0.10 bonuses.";
                return false;
            }

            var expectedPickable = new HashSet<SpiritRootType>
            {
                SpiritRootType.Metal,
                SpiritRootType.Wood,
                SpiritRootType.Water,
                SpiritRootType.Fire,
                SpiritRootType.Earth
            };
            if (config.DefaultPickable == null
                || config.DefaultPickable.Length != expectedPickable.Count
                || !expectedPickable.SetEquals(config.DefaultPickable))
            {
                error = "defaultPickable must contain only the five standard roots.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateBody(BodyRefinementConfig config, out string error)
        {
            if (config?.Levels == null || config.Levels.Length == 0)
            {
                error = "BodyRefinementConfig.levels is empty.";
                return false;
            }

            var seen = new HashSet<int>();
            if (config.Levels.Any(level => level == null || !seen.Add(level.Level)))
            {
                error = "BodyRefinementConfig contains a null or duplicate level.";
                return false;
            }


            if (!seen.SetEquals(new[] { 0, 1, 2, 3, 4 }))
            {
                error = "BodyRefinementConfig must contain body levels 0 through 4.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateCraft(CraftLevelConfig config, out string error)
        {
            if (config?.Alchemy == null || config.Alchemy.Length == 0)
            {
                error = "CraftLevelConfig.alchemy is empty.";
                return false;
            }

            var seen = new HashSet<int>();
            if (config.Alchemy.Any(level => level == null || level.Level <= 0 || !seen.Add(level.Level)))
            {
                error = "CraftLevelConfig contains a null, duplicate, or invalid level.";
                return false;
            }

            if (!seen.SetEquals(Enumerable.Range(1, 10)))
            {
                error = "CraftLevelConfig must contain alchemy levels 1 through 10.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.0001f;
        }
    }
}
