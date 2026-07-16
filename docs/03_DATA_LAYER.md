# 03 · 数据层（Schema 权威源）

> **版本**: v3.1  
> 所有 SO / struct / enum / JSON 配置以本文为准。系统文档可复述，冲突时以本文为准。

---

## 1. 枚举

```csharp
public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Wind, Dark, True }
public enum ElementType { None, Metal, Wood, Water, Fire, Earth, Wind, Lightning, Ice, Poison, Dark }
public enum ElementReactionType { None, Melt, BurnBurst, Shock, Spread, Sever }
public enum StatusEffectChangeType { Applied, Refreshed, StackChanged, Removed, Expired, Promoted }
public enum CombatTeam { Neutral, Player, Enemy }
public enum RealmType { Mortal = 0, QiCondensation = 1, Foundation = 2, GoldenCore = 3, NascentSoul = 4 }
public enum SpiritRootType { None, Metal, Wood, Water, Fire, Earth, Heaven, Waste }
public enum EquipmentSlot { Weapon, Head, Chest, Legs, Boots, Accessory1, Accessory2, Treasure } // 法宝
public enum ItemType { Consumable, Material, Equipment, Quest, Currency, Recipe, Talisman }
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }
public enum SkillType { Active, Passive, Ultimate }
public enum SkillElement { None, Fire, Ice, Lightning, Poison, Wind, Metal, Earth }
public enum QuestType { Main, Side, Daily, Weekly }
public enum QuestStatus { Locked, Available, Active, Completed, Failed, TurnedIn }
public enum ObjectiveType { Kill, Collect, Talk, Reach, UseItem, Craft, Survive, ReachRealm } // ReachRealm: TargetId = RealmType name e.g. GoldenCore
public enum EnemyRank { Normal, Elite, Boss, WorldBoss }
public enum CraftType { Alchemy, Smithing, Talisman, Formation } // MVP 仅用 Alchemy
public enum XpSourceType { Combat, Quest, Breakthrough, Consume, Other }
public enum AcquireSource { Loot, Quest, Craft, Shop, Cheat, Gather }
public enum ShopTransactionType { Buy, Sell }
public enum WeatherId { Clear, Rain, Fog, Storm, Snow } // MVP: Clear, Rain, Fog
public enum BodyLevel { Mortal = 0, Copper = 1, Diamond = 2, Immortal = 3, Eternal = 4 }
public enum PlayerState { Idle, Move, Sprint, Jump, Fall, LightAttack, HeavyAttack, Dodge, Block, BlockHit, Stagger, SkillCast, Dead } // Mounted/Flying 由 G06-03 作为正交移动模式维护，避免破坏既有战斗状态序列
public enum GameState { Boot, MainMenu, Loading, Playing, Paused, Dialogue, Cutscene, Dead } // 物理文件归 Core/GameState.cs
```

`GameState` 的枚举 Schema 仍以本节为准，但代码物理归属 `Wendao.Core`，避免 Core 的 `GameManager` 反向依赖 `Wendao.Data`。

---

## 2. 运行时事件参数

```csharp
// Data/Runtime/EventParams.cs + DamageRequest.cs + AlchemyRuntimeData.cs
public struct DamageInfo {
    public GameObject Target, Source;
    public float Amount;
    public DamageType Type;
    public bool IsCritical, IsKillingBlow;
    public Vector3 HitPoint;
    public ElementType Element;
    public string SkillId; // 可空；来自 DamageRequest.SkillId
    public ElementReactionType Reaction;
    public float ReactionMultiplier; // 无反应为 1
    public float HitstopSeconds; // 已解析命中停顿；0 表示不触发
    public float HitstunSeconds;  // 普通敌基础受击硬直；接收方再乘 Rank 系数
}

public struct DamageRequest {
    public GameObject Source;
    public float BaseDamage;
    public DamageType Type;
    public ElementType Element;
    public float Multiplier;      // <=0 时 CombatSystem 按 1 兜底
    public bool CanCrit;
    public string SkillId;       // 普攻可空字符串
    public bool IgnoreAttackScaling; // DoT 等已结算基础值使用
    public string StatusOnHitId; // 可空；命中且造成正伤害后附加
    public float StatusChance;   // 0..1
    public float HitstopSeconds; // 普攻段数在发起请求时写入
    public float HitstunSeconds; // 普通敌基础受击硬直
}

public struct HealInfo {
    public GameObject Target;
    public float Amount;
    public string SourceId;
}

public struct DeathInfo {
    public GameObject Victim, Killer;
    public Vector3 Position;
    public string LastHitSkillId; // 击杀技能；未知则 ""
}

public struct PlayerRespawnInfo {
    public GameObject Player;
    public string RespawnPointId;
    public Vector3 Position;
}

public struct EnemyDeathInfo {
    public string EnemyId;
    public EnemyRank Rank;
    public GameObject Victim, Killer;
    public Vector3 Position;
}

public struct XpGainInfo {
    public float Amount;
    public XpSourceType Source;
    public float MultiplierApplied;
}

public struct CultivationXpPenaltyInfo {
    public float PreviousXp, AmountLost, CurrentXp;
    public float Percent;
}

public struct RealmChangeInfo {
    public RealmType PrevRealm, NewRealm;
    public int PrevSubStage, NewSubStage;
    public bool Success;
    public float SuccessRate;
}

// CultivationManager 查询返回；不是事件，也不进入存档。
public struct BreakthroughBlocker {
    public string Code;
    public string MessageKey;
    public string RelatedItemId;
    public string[] AcquisitionHintKeys;
}

public struct ItemAcquireInfo {
    public string ItemId;
    public int Count;
    public AcquireSource Source;
}

public struct ItemUseInfo {
    public string ItemId;
    public int SlotIndex;
}

public struct CurrencyChangeInfo {
    public int Previous, Current, Delta;
}

public struct EquipmentChangeInfo {
    public EquipmentSlot Slot;
    public string OldItemId, NewItemId; // 空字符串表示空
}

public struct EquipmentUpgradeInfo {
    public EquipmentSlot Slot;
    public string ItemId;
    public int NewRefineLevel;
    public bool Success;
}

public struct SkillInfo { public string SkillId; public int Level; }
public struct SkillCastInfo {
    public string SkillId;
    public Vector3 Origin, TargetPoint;
    public GameObject TargetActor; // 可 null
}

public struct SkillUpgradeInfo { public string SkillId; public int NewLevel; }

public struct QuestInfo { public string QuestId; public QuestStatus Status; }
public struct QuestProgressInfo {
    public string QuestId;
    public int ObjectiveIndex;
    public int Current, Required;
}

public struct DailyQuestProgressInfo {
    public string QuestId;
    public int Current, Required;
}
public struct DailyQuestResetInfo {
    public string CycleStartedUtc, NextResetUtc;
}
public struct DailyQuestClaimInfo { public string QuestId; }

public struct DialogueInfo {
    public string NpcId;
    public string DialogueId;
    public bool Cancelled; // Start 恒 false；切场景/主动取消结束为 true
}

public struct PlayerDodgeInfo {
    public GameObject Player;
    public Vector3 Direction;
}

public struct LockOnInfo {
    public GameObject Player;
    public GameObject Target; // 敌人根对象；清锁时 null
    public bool Locked;
}

public struct StatusEffectInfo {
    public GameObject Target, Source;
    public string StatusId;
    public int Stacks;
    public float RemainingDuration;
    public StatusEffectChangeType Change;
}

public struct ElementReactionInfo {
    public GameObject Target, Source;
    public ElementReactionType Reaction;
    public ElementType AttackElement;
    public string ExistingStatusId;
    public float DamageMultiplier;
    public int SpreadTargetCount;
}

public struct CraftResultInfo {
    public string RecipeId;
    public string ResultItemId;
    public int ResultCount;
    public bool Success;
}

public struct AlchemyFurnaceInfo {
    public string FurnaceId;
}

public struct ShopOpenInfo {
    public string VendorId;
}

public struct ShopTransactionInfo {
    public string VendorId;
    public ShopTransactionType Type;
    public string ItemId;
    public int Count;
    public int SpiritStonesDelta;
    public int InventorySlot; // 买入为 -1；卖出为原背包格
}

public struct AffectionInfo {
    public string NpcId;
    public int OldValue, NewValue;
    public string MilestoneId; // 可空
}

public struct AchievementInfo {
    public string Id, DisplayName, Description;
}

public struct TitleInfo { public string TitleId; public bool Equipped; }

public struct FactionReputationInfo {
    public string FactionId;
    public int Previous, Current;
    public int OldRank, NewRank;
}

public struct DayNightInfo { public bool IsNight; public float TimeOfDay; }
public struct WeatherInfo { public WeatherId Weather; public float Intensity; }
public struct MapInfo { public string MapId; public string SpawnId; }

public struct BossPhaseInfo {
    public string BossId;
    public int OldPhase, NewPhase;
    public float HpPercent;
}

public struct MountInfo { public string MountId; public bool Mounted; }
public struct FlightInfo { public bool IsFlying; }
public struct SerendipityInfo {
    public string SerendipityId;
    public string MapId;
    public string WorldFlag;
}
public struct TutorialPromptInfo {
    public string TutorialId, StepId;
    public string LocalizationKey, DefaultValue;
    public float Duration; // 0 表示保持到步骤完成
    public bool IsForced, CanDismiss;
    public Rect FocusRectNormalized; // 0..1 屏幕归一化挖洞区域
}
public struct ToastInfo {
    public string LocalizationKey, DefaultValue;
    public float Duration;
}
public struct GameStateInfo { public GameState Prev, Next; } // 物理文件归 Core/GameStateInfo.cs
```

`GameStateInfo` 与 `GameState` 同属 Core-owned Schema 例外；Data/UI 通过正常的 Core 下层依赖复用它。

---

## 3. ScriptableObject 定义

### 3.1 ItemData

```csharp
[CreateAssetMenu(menuName = "问道/物品/ItemData")]
public class ItemData : ScriptableObject
{
    public string Id;                 // item_potion_heal_01
    public string DisplayNameKey;
    public string DisplayName;
    public string DescriptionKey;
    [TextArea] public string Description;
    public ItemType Type;
    public ItemRarity Rarity;
    public Sprite Icon;
    public int MaxStack = 99;
    public bool IsBound;               // 绑定不可交易
    public int BuyPrice;              // 灵石；0=不可从商店购入
    public int SellPrice;             // 灵石；0=不可出售
    public int RequiredRealm;         // 0=无限制，对应 RealmType int
    public UseEffect[] UseEffects;    // Consumable
    public string EquipmentDataId;    // Type=Equipment 时指向 EquipmentData.Id
    public string[] AcquisitionHintKeys; // 获取途径文案 key（突破材料必填）
}

[Serializable]
public class UseEffect
{
    public UseEffectType EffectType;  // Heal, RestoreMana, AddCultivationXp, AddBodyXp, Buff
    public float Value;
    public float Duration;            // Buff 用
    public string StatusEffectId;     // 可选
}

public enum UseEffectType { Heal, RestoreMana, AddCultivationXp, AddBodyXp, ApplyStatus, LearnSkill }
```

Item 玩家可见文案显式保存 `DisplayNameKey` / `DescriptionKey`；标准内容仍使用名称 `item_name_{Id}`、描述 `item_desc_{Id}`，`DisplayName` / `Description` 保存中文 default value。旧 SO 缺 key 时可按稳定 ID 派生回退。

### 3.2 EquipmentData

```csharp
[CreateAssetMenu(menuName = "问道/物品/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public EquipmentSlot Slot;
    public ItemRarity Rarity;
    public int RequiredRealm;
    public StatBlock BaseStats;
    public string SetId;              // 空=无套装
    public int MaxRefineLevel = 10;
    public GameObject VisualPrefab;   // 可选外观
    public int MaxGemSockets;         // MVP 可 0
    public int MaxDurability = 100;   // 0=无耐久
}

[Serializable]
public class StatBlock
{
    public float MaxHp, MaxMana, Attack, Defense;
    public float CritRate, CritDamage;     // 0.05 = 5%, CritDamage 默认 1.5
    public float MoveSpeed, AttackSpeed;
    public float FireBonus, IceBonus, LightningBonus, PoisonBonus, WindBonus;
    public float CultivationSpeed;         // 修炼速度加成 0.1=+10%
    public float DivineSense;              // 神识锁定距离增量（m）；基础范围 14m

    public static StatBlock operator +(StatBlock a, StatBlock b); // 实现逐字段加
    public StatBlock Multiply(float m); // 精炼倍率用
}
```

### 3.3 SkillData

```csharp
[CreateAssetMenu(menuName = "问道/功法/SkillData")]
public class SkillData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public string DescriptionKey;
    [TextArea] public string Description;
    public SkillType Type;
    public SkillElement Element;
    public Sprite Icon;
    public int MaxLevel = 5;
    public int RequiredRealm;
    public float BaseCooldown;
    public float BaseManaCost;
    public float BaseDamage;          // 主动
    public float DamagePerLevel;
    public float CastTime;            // 前摇秒
    public float RecoveryTime;        // 后摇秒
    public float Range;
    public float Radius;              // AoE，0=单体
    public bool IsProjectile;
    public string ProjectileVfxId;
    public string ImpactVfxId;
    public string StatusOnHitId;      // 可空
    public float StatusChance = 1f;
    public SpiritRootType[] PreferredRoots; // 可选；创角推荐功法
    public AnimationClip CastAnimation; // 可空则用通用
    public LevelScaling[] LevelTable; // 可选覆盖
}

[Serializable]
public class LevelScaling
{
    public int Level;
    public float Damage, Cooldown, ManaCost;
}
```

### 3.4 QuestData

```csharp
[CreateAssetMenu(menuName = "问道/任务/QuestData")]
public class QuestData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public string DescriptionKey;
    [TextArea] public string Description;
    public QuestType Type;
    public int RequiredRealm;
    public string[] PrerequisiteQuestIds;
    public QuestObjective[] Objectives;
    public bool ObjectivesAreOrdered; // true 时只允许推进首个未完成目标
    public QuestReward AcceptRewards; // 可空；Accept() 成功后立即发放（只发一次）
    public ItemStack[] TurnInCosts;    // 交付前检查并消耗；失败时回滚
    public QuestReward Rewards;       // TurnIn 奖励
    public string StartDialogueId;
    public string CompleteDialogueId;
    public string StartNpcId;         // 可空；空时回退 TurnInNpcId
    public string TurnInNpcId;
}

[Serializable]
public class QuestObjective
{
    public ObjectiveType Type;
    public string TargetId;           // enemyId / itemId / npcId / mapId / RealmType name
    public int RequiredCount;
    public string DescriptionKey;
    public string Description;
    public bool LatchOnFirstAcquire;  // Collect：首次满足后不因 RemoveItem 回退；突破材料默认 true
}

[Serializable]
public class QuestReward
{
    public float CultivationXp;
    public int SpiritStones;
    public ItemStack[] Items;
    public int FactionRep;            // 可 0
    public string FactionId;
    public string[] SkillIds;         // 直接调用 SkillManager.Learn；已学视为满足
}

[Serializable]
public class ItemStack { public string ItemId; public int Count; }
```

### 3.5 EnemyData

```csharp
[CreateAssetMenu(menuName = "问道/敌人/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public EnemyRank Rank;
    public RealmType Realm;
    public int SubStage;
    public float MaxHp, Attack, Defense, MoveSpeed;
    public float AggroRange, AttackRange, DisengageRange;
    public float AttackInterval;
    public float CultivationXpReward;
    public int MinSpiritStones;       // 击杀直接入账；0..Max 可产生 0
    public int MaxSpiritStones;       // 0 表示不掉灵石
    public LootEntry[] LootTable;
    public string[] SkillIds;         // BOSS/精英技能
    public BossPhase[] BossPhases;    // Rank=Boss 时
    public GameObject Prefab;
}

[Serializable]
public class LootEntry
{
    public string ItemId;
    public int MinCount, MaxCount;
    public float DropChance;          // 0~1
}

[Serializable]
public class BossPhase
{
    public int PhaseIndex;            // 0,1,2
    public float HpThreshold;         // 进入该阶段的 HP% 上界，如 0.7
    public string[] SkillIds;
    public string PhaseVfxId;
    public bool InvulnerableDuringTransition;
    public BossSkillTelegraph[] Telegraphs; // 预警；G04-05 验收
}

public enum TelegraphShape { Circle, Line, Sector, FullScreen }

[Serializable]
public class BossSkillTelegraph
{
    public string SkillId;
    public TelegraphShape Shape;
    public float Duration;          // 预警可读时间，验收 ≥0.6
    public float RadiusOrLength;
    public float Angle;             // Sector 度数
    public string VfxId;
    public float RecoverStun;       // 技能后硬直=惩戒窗口
    public bool Interruptible;      // MVP 默认 false
}
```

G04-04 将灵石保持为 `InventorySaveData.SpiritStones`，不伪装成背包物品。
每次有效死亡在 `[MinSpiritStones, MaxSpiritStones]` 闭区间独立取整；区间
`0..0` 表示无灵石，物品仍逐条按 `LootEntry.DropChance` 独立结算。背包满时
物品落到世界，灵石不占格子并直接入账。

Enemy 名称同样由稳定 ID 派生 `enemy_name_{Id}`，写入 `DisplayNameKey`；
`DisplayName` 为中文 default value。

### 3.6 NPCData

```csharp
[CreateAssetMenu(menuName = "问道/NPC/NPCData")]
public class NPCData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public string DefaultDialogueId;
    public string FactionId;
    public bool IsVendor;
    public string[] VendorItemIds;
    public AffectionMilestone[] AffectionMilestones;
    public GameObject Prefab;
}

[Serializable]
public class AffectionMilestone
{
    public int RequiredAffection;
    public string MilestoneId;
    public string UnlockDialogueId;
    public string UnlockQuestId;
}
```

### 3.7 CraftRecipeData

```csharp
[CreateAssetMenu(menuName = "问道/配方/CraftRecipeData")]
public class CraftRecipeData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey;
    public string DisplayName;
    public CraftType CraftType;
    public int RequiredCraftLevel;
    public float BaseSuccessRate;     // 0~1
    public float CraftTime;           // 秒，MVP 可瞬间或短读条
    public CraftIngredient[] Ingredients;
    public CraftResult SuccessResult;
    public CraftResult FailResult;    // 可空
}

[Serializable]
public class CraftIngredient
{
    public string ItemId;
    public int Count;
    public bool ConsumedOnFail = true;
}

[Serializable]
public class CraftResult
{
    public string ItemId;
    public int MinCount, MaxCount;
}
```

`ConfigDatabase` 以稳定 `RecipeId` 注册 `Resources/SO/Recipes`，并在正式
ScriptableObject 尚未导入时提供 `09_CONTENT§7` 的同 ID 代码占位；正式资源优先，
重复 ID 不覆盖。

### 3.8 MapData / DialogueData / Achievement / Title / Mount / Status

```csharp
[CreateAssetMenu(menuName = "问道/世界/MapData")]
public class MapData : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public string SceneName;
    public int RecommendedRealm;
    public string[] TeleportPointIds;
    public WeatherWeight[] WeatherPool;
    public bool AllowFlight;
    public AudioClip DefaultBgm;
}

[Serializable]
public class WeatherWeight { public WeatherId Weather; public float Weight; }

[CreateAssetMenu(menuName = "问道/对话/DialogueData")]
public class DialogueData : ScriptableObject
{
    public string Id;
    public DialogueNode[] Nodes;
}

[Serializable]
public class DialogueNode
{
    public string NodeId;
    public string SpeakerNameKey;
    public string SpeakerName;
    public string TextKey;
    [TextArea] public string Text;
    public DialogueChoice[] Choices;  // 空=点继续走 NextNodeId
    public string NextNodeId;
    public string QuestOfferId;       // 可选：该句结束后接任务
    public string QuestTurnInId;      // 可选：该句正常结束后交任务
    public bool End;
}

[Serializable]
public class DialogueChoice
{
    public string TextKey;
    public string Text;
    public string NextNodeId;
    public int RequiredAffection;
    public string RequiredQuestId;
    public string SetFlag;            // 可选世界/NPC flag
}

[CreateAssetMenu(menuName = "问道/成就/AchievementData")]
public class AchievementData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey, DisplayName;
    public string DescriptionKey, Description;
    public string TriggerType;        // KillTotal, RealmReached, QuestCompleted, ...
    public string TargetId;
    public float RequiredValue;
    public string RewardTitleId;
    public ItemStack[] RewardItems;
    public int RewardSpiritStones;
}

[CreateAssetMenu(menuName = "问道/称号/TitleData")]
public class TitleData : ScriptableObject
{
    public string Id;
    public string DisplayNameKey, DisplayName;
    public string DescriptionKey, Description;
    public StatBlock Bonus;
    public bool ShowInNameplate = true;
}

[CreateAssetMenu(menuName = "问道/坐骑/MountData")]
public class MountData : ScriptableObject
{
    public string Id, DisplayName;
    public float SpeedMultiplier;     // 1.5
    public bool CanFly;
    public int RequiredRealm;
    public GameObject Prefab;
}

[Serializable]
public sealed class MountSaveData
{
    public int SchemaVersion;
    public List<string> UnlockedMountIds;
    public string SelectedMountId;
}

[Serializable]
public sealed class AchievementProgress
{
    public string AchievementId;
    public float Value;
}

[Serializable]
public sealed class AchievementSaveData
{
    public int SchemaVersion;
    public List<AchievementProgress> Progress;
    public List<string> UnlockedIds;
}

[Serializable]
public sealed class TitleSaveData
{
    public int SchemaVersion;
    public List<string> UnlockedTitleIds;
    public string ActiveTitleId;
}

[CreateAssetMenu(menuName = "问道/状态/StatusEffectData")]
public class StatusEffectData : ScriptableObject
{
    public string Id, DisplayNameKey, DisplayName;
    public float Duration;
    public int MaxStacks = 1;
    public bool IsDebuff;
    public ElementType AuraElement;   // 供元素反应查询；None=不可反应标记
    public float DotDamagePerSecond;
    public float DotBaseDamageMultiplier; // 如灼烧每秒 0.05 × 技能基础
    public float DotInterval = 1f;
    public DamageType DotType;
    public float MoveSpeedMod;        // -0.3 = 减速 30%
    public float AttackMod;
    public float DamageDealtMod;      // -0.1 = 最终造成伤害 -10%
    public float DefenseMod;
    public bool Stun, Root, Silence;
    public float ReapplyCooldown;     // 从首次生效起计
    public string PromoteAtMaxStacksStatusId; // 如寒意叠满转冻结
}

[CreateAssetMenu(menuName = "问道/世界/SerendipityData")]
public class SerendipityData : ScriptableObject
{
    public string Id;
    public string MapId;
    public string TriggerId;
    public bool OnceOnly = true;           // MVP 恒 true
    public string RequiredQuestId;         // 可空
    public RealmType RequiredRealm;        // Mortal=无限制
    public QuestReward Rewards;            // 禁止 Equipment 条目（Editor 校验）
    public string DialogueId;
    public string WorldFlag;
}
```

G07-02 统一要求玩家可见内容保留 key 与中文 default：装备/配方名称分别使用
`item_name_{Id}` / `recipe_name_{Id}`，技能使用 `skill_name_{Id}` 与
`skill_desc_{Id}`，敌人使用 `enemy_name_{Id}`，成就和称号使用
`achievement_name|desc_{Id}` 与 `title_name|desc_{Id}`。旧 SO 缺 key 时
`ConfigDatabase` 按稳定 ID 补全，不改 ID、不迁移存档。

G06-03 将 `MountSaveData` 注册为可选自定义存档模块 `mounts`，物理文件为
`SaveSlot_N/mounts.json`。只保存已解锁坐骑与当前选择；是否骑乘、是否飞行属于场景运行态，
读档后统一从未骑乘状态开始。旧存档缺少 `mounts.json` 时回退为仅解锁
`mount_spirit_horse`，不会判定整档损坏。

G06-04 继续复用 `profile.json/FactionReputation`：字典中存在 `faction_danding`
即表示已加入，值为当前声望。成就进度/完成态写 `achievements.json`，称号解锁/佩戴态写
`titles.json`；两者都是可选模块，旧档缺失时从空状态启动。运行时恢复顺序不重复发奖，
称号恢复后只重放属性聚合。

---

## 4. JSON 配置

路径：`StreamingAssets/Config/`

### 4.1 RealmConfig.json

```json
{
  "realms": [
    {
      "realm": 1,
      "name": "练气",
      "subStages": 9,
      "xpPerSubStage": [100, 200, 350, 550, 800, 1200, 1700, 2400, 3200],
      "baseStatsPerSubStage": {
        "maxHp": [100, 120, 145, 175, 210, 250, 300, 360, 430],
        "maxMana": [50, 60, 72, 86, 100, 120, 140, 165, 190],
        "attack": [10, 12, 15, 18, 22, 27, 33, 40, 48],
        "defense": [5, 6, 8, 10, 12, 15, 18, 22, 26]
      },
      "breakthroughToNext": {
        "baseSuccessRate": 0.85,
        "minSubStage": 9,
        "failXpPenaltyPercent": 0.2,
        "requiredItemId": "item_pill_foundation"
      }
    },
    {
      "realm": 2,
      "name": "筑基",
      "subStages": 3,
      "xpPerSubStage": [5000, 8000, 12000],
      "baseStatsPerSubStage": {
        "maxHp": [600, 750, 950],
        "maxMana": [250, 320, 400],
        "attack": [65, 80, 100],
        "defense": [35, 45, 55]
      },
      "breakthroughToNext": {
        "baseSuccessRate": 0.65,
        "minSubStage": 3,
        "failXpPenaltyPercent": 0.2,
        "requiredItemId": "item_pill_goldencore"
      }
    },
    {
      "realm": 3,
      "name": "金丹",
      "subStages": 3,
      "xpPerSubStage": [20000, 35000, 50000],
      "baseStatsPerSubStage": {
        "maxHp": [1400, 1800, 2300],
        "maxMana": [550, 700, 900],
        "attack": [140, 175, 220],
        "defense": [75, 95, 120]
      },
      "breakthroughToNext": null
    },
    {
      "realm": 4,
      "name": "元婴",
      "subStages": 3,
      "xpPerSubStage": [80000, 120000, 180000],
      "baseStatsPerSubStage": {
        "maxHp": [3200, 4000, 5000],
        "maxMana": [1200, 1500, 1900],
        "attack": [300, 380, 480],
        "defense": [160, 200, 250]
      },
      "breakthroughToNext": null
    }
  ]
}
```

### 4.2 SpiritRootConfig.json

```json
{
  "roots": [
    { "type": "Metal",  "cultivationMul": 1.1, "elementBonus": { "Metal": 0.15 }, "bodyMul": 1.0, "uniquePassiveId": "passive_element_affinity", "introDescriptionKey": "root_intro_five" },
    { "type": "Wood",   "cultivationMul": 1.1, "elementBonus": { "Wood": 0.15 },  "bodyMul": 1.0, "uniquePassiveId": "passive_element_affinity", "introDescriptionKey": "root_intro_five" },
    { "type": "Water",  "cultivationMul": 1.1, "elementBonus": { "Water": 0.15, "Ice": 0.10 }, "bodyMul": 1.0, "uniquePassiveId": "passive_element_affinity", "introDescriptionKey": "root_intro_five" },
    { "type": "Fire",   "cultivationMul": 1.1, "elementBonus": { "Fire": 0.15 },  "bodyMul": 1.0, "uniquePassiveId": "passive_element_affinity", "introDescriptionKey": "root_intro_five" },
    { "type": "Earth",  "cultivationMul": 1.1, "elementBonus": { "Earth": 0.15 }, "bodyMul": 1.15, "uniquePassiveId": null, "introDescriptionKey": "root_intro_five" },
    { "type": "Heaven", "cultivationMul": 1.35,"elementBonus": { "All": 0.10 },   "bodyMul": 1.0, "weight": 0.05, "uniquePassiveId": "passive_heaven_gift", "introDescriptionKey": "root_intro_heaven" },
    { "type": "Waste",  "cultivationMul": 0.55,"elementBonus": {}, "bodyMul": 1.5, "weight": 0.15,
      "uniquePassiveId": "passive_waste_iron_vein", "introDescriptionKey": "root_intro_waste",
      "passives": { "blockPhysDrBonus": 0.10, "physicalDamageBonus": 0.10, "bodyPotionMul": 1.25, "grantPassiveSkillIdOnCreate": null } }
  ],
  "defaultPickable": ["Metal","Wood","Water","Fire","Earth"],
  "randomEnabled": true
}
```

> Water **必须**保留 Ice 0.10。废灵根格挡：`BlockPhysDR_final = min(0.85, BaseBlockPhysDR(0.60) + 0.10) = 0.70`（伤害管道 step 8）。天/废仅随机，不可创角点选。

### 4.3 BodyRefinementConfig.json

```json
{
  "levels": [
    { "level": 0, "name": "凡躯", "xpToNext": 0,     "hpBonus": 0,   "physDR": 0,    "controlResist": 0,   "revive": false },
    { "level": 1, "name": "铜皮铁骨", "xpToNext": 1000, "hpBonus": 0.2, "physDR": 0.1,  "controlResist": 0,   "revive": false },
    { "level": 2, "name": "金刚不坏", "xpToNext": 4000, "hpBonus": 0.5, "physDR": 0.25, "controlResist": 0.3, "revive": false },
    { "level": 3, "name": "不灭金身", "xpToNext": 12000,"hpBonus": 1.0, "physDR": 0.4,  "controlResist": 1.0, "revive": false },
    { "level": 4, "name": "万劫不灭", "xpToNext": 0,    "hpBonus": 2.0, "physDR": 0.5,  "controlResist": 1.0, "revive": true }
  ],
  "xpFromDamageTakenMul": 0.1
}
```

`xpToNext` 沿用既有字段名，但语义是目标等级的**累计解锁阈值**：
铜皮铁骨 1000、金刚不坏 4000、不灭金身 12000；升级不扣累计炼体 XP。

### 4.4 CraftLevelConfig.json

```json
{
  "alchemy": [
    { "level": 1, "xpRequired": 0,     "maxRecipeTier": 1, "successBonus": 0 },
    { "level": 2, "xpRequired": 200,   "maxRecipeTier": 2, "successBonus": 0.03 },
    { "level": 3, "xpRequired": 600,   "maxRecipeTier": 2, "successBonus": 0.06 },
    { "level": 4, "xpRequired": 1500,  "maxRecipeTier": 3, "successBonus": 0.10 },
    { "level": 5, "xpRequired": 3000,  "maxRecipeTier": 3, "successBonus": 0.14 },
    { "level": 6, "xpRequired": 6000,  "maxRecipeTier": 4, "successBonus": 0.18 },
    { "level": 7, "xpRequired": 10000, "maxRecipeTier": 4, "successBonus": 0.22 },
    { "level": 8, "xpRequired": 15000, "maxRecipeTier": 5, "successBonus": 0.26 },
    { "level": 9, "xpRequired": 25000, "maxRecipeTier": 5, "successBonus": 0.30 },
    { "level": 10,"xpRequired": 40000, "maxRecipeTier": 6, "successBonus": 0.35 }
  ]
}
```

### 4.5 FormulaLibrary（代码常量）

```csharp
public static class FormulaLibrary
{
    // 最终伤害
    // raw = skillBase * (1 + attack/100) * skillCoefficient
    // mitigated = raw * (100 / (100 + defense))
    // elemental = mitigated * (1 + elementBonus - elementResist)
    // crit = elemental * critDamage if roll < critRate
    // true damage 跳过 defense

    public const float DefenseConstant = 100f;
    public const float BaseCritDamage = 1.5f;
    public const float GlobalDamageMin = 1f;

    // 元素反应（攻击元素 × 目标已有异常）
    // Fire+Ice → Melt 1.5x
    // Fire+Poison → BurnBurst 1.3x + clear poison
    // Lightning+Water/Ice → Shock 1.4x + brief stun 0.5s
    // Wind+Any → Spread 将异常扩散到半径 4m
    // Metal+Wood → Sever 1.25x + 破防 10% 3s

    public const float MeltMultiplier = 1.5f;
    public const float BurnBurstMultiplier = 1.3f;
    public const float ShockMultiplier = 1.4f;
    public const float SeverMultiplier = 1.25f;
    public const float SpreadRadius = 4f;

    // 精炼: baseStats * (1 + 0.05 * refineLevel)
    public const float RefineStatPerLevel = 0.05f;
    public const float RefineBaseSuccess = 0.95f; // 每级 -3%，最低 0.4
    public const int RefineMaterialBaseCost = 1;
    public const int RefineLevelsPerMaterialIncrease = 2;
    // 1 + floor(max(0, currentRefineLevel) / 2)
    public static int GetRefineMaterialCost(int currentRefineLevel);

    // 死亡惩罚
    public const float DeathXpPenaltyPercent = 0.05f;

    // 玩家脱战回复（最后一次造成/受到正伤害起算）
    public const float OutOfCombatRecoveryDelay = 5f;
    public const float OutOfCombatHpRecoveryPerSecond = 0.02f;
    public const float OutOfCombatManaRecoveryPerSecond = 0.03f;
}
```

---

## 5. 配置加载 API

```csharp
public class ConfigDatabase : Singleton<ConfigDatabase>
{
    public RealmConfig Realm { get; private set; }
    public SpiritRootConfig SpiritRoot { get; private set; }
    public BodyRefinementConfig Body { get; private set; }
    public CraftLevelConfig Craft { get; private set; }

    public void LoadAll(); // Boot 时调用；失败则进入安全模式并 LogError
    public ItemData GetItem(string id);
    public EquipmentData GetEquipment(string id);
    public SkillData GetSkill(string id);
    public QuestData GetQuest(string id);
    public DialogueData GetDialogue(string id);
    public NPCData GetNpc(string id);
    public EnemyData GetEnemy(string id);
    public StatusEffectData GetStatusEffect(string id);
    public SerendipityData GetSerendipity(string id);
    // ... 各 SO 通过 Addressables 或 Resources 注册表
}
```

MVP：SO 用 `Resources/SO/` 或直接场景引用表；JSON 用 `StreamingAssets`。代码优先阶段还会为缺失正式资产的垂直切片内容与 `09§4.1` 状态库注册同 ID 运行时占位；正式 `Resources/SO` 资产始终优先。

G05-06 将 `Resources/SO/Serendipities` 纳入同一稳定 ID 注册表；正式资产缺失时注册 `09§14` 四条代码占位与对应对话。

### 5.1 G00-05 最小存档 DTO

完整模块字段由对应系统 Goal 追加；基础层只锁定版本、角色摘要以及 v3.1 世界闭环字段：

```csharp
public static class SaveSchema { public const int CurrentVersion = 1; }

public sealed class SaveProfileData
{
    public int SchemaVersion;
    public string DisplayName;
    public int Realm, SubStage;
    public float CultivationXp;
    public string SpiritRoot;
    public int BodyLevel;
    public float BodyXp;
    public float PlayTimeSeconds;
    public string MainQuestIndex;
    public int SpiritStones;
    public Dictionary<string, int> FactionReputation;
}

public sealed class SaveWorldData
{
    public int SchemaVersion;
    public List<string> UnlockedMaps, UnlockedTeleports;
    public float TimeOfDay;
    public List<string> TutorialsCompleted, SerendipityFlags;
    public Dictionary<string, int> DungeonCheckpoint;
    public Dictionary<string, bool> QuestFlags;
}

public sealed class SaveMetadata
{
    public int SchemaVersion;
    public string DisplayName;
    public int Realm, SubStage;
    public float PlayTimeSeconds;
    public string MainQuestIndex;
    public int SpiritStones;
    public string SavedAt;
    // Slot / Exists / IsCorrupted 仅运行时，不写 JSON
}
```

JSON 字段统一 camelCase；`schemaVersion` 必须存在且当前为 `1`。`dungeonCheckpoint.map_blackwind` 只允许 `0..4`。Serializer 使用 Unity 官方 `com.unity.nuget.newtonsoft-json`，因为 `JsonUtility` 不支持本 Schema 的 Dictionary。

### 5.1.1 G07-01 world 时间约束

`world.timeOfDay` 沿用 schema v1 既有字段，不新增迁移：合法域为有限数
`[0, 24)`，新档默认 `10.0`。`DayNightSystem` 运行中直接回写该字段，常规
`SaveManager.SaveGame/TrySaveModule("world")` 负责落盘；读档替换 `SaveWorldData`
实例后系统会重新绑定并刷新场景光照。天气按场景重新抽取，不写入存档。

G05-03 的副本事件参数不写入 JSON：`BlackwindFloorInfo { floor: 1..5,
checkpoint: 0..4 }` 用于进层/通层/通关通知；`BlackwindRunInfo { startFloor,
checkpoint, wasFailure }` 用于开局与死亡重建。持久化仍只写
`world.dungeonCheckpoint.map_blackwind`，压力板、波次和 B4 泉水使用态均为本局内存。

G-VS-05 直接启用既有 `profile.json` 字段，不新增 Schema：`spiritRoot` 为空表示尚未创角选根，非空时必须是 `SpiritRootType` 中除 `None` 外的枚举名；`cultivationXp` 必须是有限非负数。`realm`、`subStage`、`cultivationXp` 与 `spiritRoot` 由 Cultivation/SpiritRoot 服务动态读取，因此 `LoadGame` 替换 Profile 后无需复制运行时影子状态。

G02-04 在 schema v1 上向后兼容追加 `bodyLevel` / `bodyXp`：前者必须是
`BodyLevel` 的 `0..4`，后者必须是有限非负累计经验；旧档缺字段由 CLR
默认值恢复为凡躯 `0 / 0`。BodyRefinement 服务与 Cultivation/SpiritRoot 一样
动态读取当前 Profile，切换槽位后不得沿用旧的运行时影子状态。

G05-05 同样在 schema v1 向后兼容追加 `factionReputation` 字典；旧档缺失时为空。当前仅用于保存支线奖励，宗门等级、折扣与 UI 仍由 G06-04 实现。

### 5.2 G-VS-03 背包与装备 DTO

```csharp
[Serializable]
public sealed class InventorySlot
{
    public string ItemId;
    public int Count;
    public bool Bound;
    public string InstanceId;
    public string ExtraJson;
}

[Serializable]
public sealed class EquipmentInstance
{
    public string InstanceId;
    public string EquipmentDataId;
    public int RefineLevel;
    public int Durability;
    public string[] GemIds;
}

public sealed class InventorySaveData
{
    public int SchemaVersion;
    public List<InventorySlot> Slots; // 写盘固定 50 格
    public int SpiritStones;
    public List<EquipmentInstance> EquipmentInstances; // 仅未穿戴
}

public sealed class EquippedItemRecord
{
    public EquipmentSlot Slot;
    public string ItemId;
    public bool Bound;
    public EquipmentInstance Instance;
}

public sealed class EquipmentSaveData
{
    public int SchemaVersion;
    public List<EquippedItemRecord> Worn;
}
```

装备实例所有权互斥：在背包时属于 `InventorySaveData.EquipmentInstances`，穿戴后移入 `EquipmentSaveData.Worn`。G-VS-03 只允许 `Weapon` 进入 Worn；G03-01 启用既有 `RefineLevel` 字段，合法范围仍为 `0..EquipmentData.MaxRefineLevel`，精炼不得复制或转移实例所有权。

### 5.3 G-VS-04 功法 DTO

```csharp
[Serializable]
public sealed class SkillRuntime
{
    public string SkillId;
    public int Level;
    public float Exp;
    public float CooldownRemaining;
}

[Serializable]
public sealed class SkillSaveData
{
    public int SchemaVersion;
    public List<SkillRuntime> Learned;
    public string[] EquippedIds; // 固定 length 4
}
```

`SkillManager` 将该 DTO 写入 `skills.json`；缺失旧模块时重置为仅学会并在槽 1 装备 `skill_basic_qi_bolt`。读档必须拒绝未知 SkillId、重复技能、越界等级、负经验/冷却以及未学习却装备的条目。

G03-02 不改 schema：`Learned` 可保存 `09_CONTENT§4` 的 7 个稳定 ID，等级仍为
`1..SkillData.MaxLevel`；`EquippedIds` 仍固定 4 槽、只允许已学习的非 Passive
技能且不可重复。升级材料继续由 `inventory.json` 的 `item_skill_scroll` 持有。

### 5.4 G-VS-06 任务 DTO

```csharp
[Serializable]
public sealed class QuestRuntimeState
{
    public string QuestId;
    public QuestStatus Status;          // Active / Completed / TurnedIn
    public int[] ObjectiveProgress;     // 与 QuestData.Objectives 等长
    public bool AcceptRewardsGranted;   // 接取奖励一次性门禁
}

[Serializable]
public sealed class QuestSaveData
{
    public int SchemaVersion;
    public List<QuestRuntimeState> Quests;
}
```

`QuestManager` 将 DTO 写入 `quests.json`。`Completed` 表示目标已满、等待交付；正常交付并发奖后才写 `TurnedIn` 与 `OnQuestCompleted`。读档拒绝未知/重复 QuestId、目标数组长度不符、越界进度、未标记接取奖励以及状态和完成度矛盾的数据；缺失旧模块时重置为空任务表。

### 5.5 G05-05 日常任务 DTO

```csharp
[Serializable]
public sealed class DailyQuestRuntimeState
{
    public string QuestId;
    public int Progress;
    public bool Claimed;
}

[Serializable]
public sealed class DailyQuestSaveData
{
    public int SchemaVersion;
    public string CycleStartedUtc; // ISO-8601 UTC
    public List<DailyQuestRuntimeState> Quests;
}
```

`DailyQuestManager` 将两条固定模板写入可选模块 `dailies.json`。必须恰好包含 hunt/gather 两个已知 ID，进度限制在 `0..RequiredCount`；满现实 24 小时或系统时钟回拨即重建本轮。该模块缺失、JSON 损坏或逻辑非法时只重置日常，不能令主存档加载失败。

### 5.6 G03-03 炼丹 DTO

```csharp
[Serializable]
public sealed class AlchemySaveData
{
    public int SchemaVersion;
    public int Level;
    public float Xp;
}
```

`AlchemySystem` 将累计熟练度写入 `alchemy.json`；缺失旧模块时恢复为
`Level=1 / Xp=0`。`XpRequired` 是累计等级阈值，读档拒绝非当前 schema、非有限或
负 XP，以及与累计阈值推导等级不一致的数据。背包材料与丹药仍只写
`inventory.json`，一次炼制结束后两个模块同步保存。

### 5.7 G09-01 全局设置 DTO

全局设置不属于任一存档槽，固定写入
`Application.persistentDataPath/Settings/settings.json`。读取失败或版本不符时回退默认值；
保存沿用 `JsonStorage.TryWriteAtomic` 的临时文件 + 备份替换策略。

```csharp
[Serializable]
public sealed class GameSettingsData
{
    public int SchemaVersion = SaveSchema.CurrentVersion;
    public float MasterVolume = 1f; // 0..1
    public float BgmVolume = 0.65f; // 0..1
    public float SfxVolume = 0.8f;  // 0..1
    public bool Fullscreen = false;
}
```

运行时加载与 UI 预览均先 clamp 音量；设置页只有“应用并返回”成功原子写入后才
更新持久化值。按 Esc 返回时恢复打开设置页之前的音量预览。

---

## 6. Acceptance（数据层）

- [ ] 全部 enum 编译通过  
- [ ] EventParams 覆盖 EventBus 表  
- [ ] 每种 SO 可 CreateAssetMenu 创建  
- [ ] RealmConfig 加载后，练气 1→2 升级 XP 与表一致  
- [ ] FormulaLibrary 单测：防御减伤、暴击、精炼倍率  
