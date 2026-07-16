# 02 · 项目架构与约定

---

## 1. 目录结构

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/                 # EventBus, Singleton, GameManager, ObjectPool, SafeBehaviour
│   │   ├── Data/
│   │   │   ├── ScriptableObjects/
│   │   │   ├── Runtime/          # struct: DamageInfo, 事件参数
│   │   │   ├── Config/           # 从 JSON 加载的配置容器
│   │   │   ├── Save/             # SaveManager、版本化 DTO、JSON 文件存储
│   │   │   └── Enums/
│   │   ├── Systems/
│   │   │   ├── Cultivation/
│   │   │   ├── Combat/
│   │   │   ├── Inventory/
│   │   │   ├── Equipment/
│   │   │   ├── Skill/
│   │   │   ├── Crafting/         # Alchemy, Gathering（MVP）
│   │   │   ├── Quest/
│   │   │   ├── NPC/
│   │   │   ├── Enemy/
│   │   │   ├── Mount/
│   │   │   ├── Faction/
│   │   │   ├── Achievement/
│   │   │   ├── World/            # SceneLoader, DayNight, Weather, MapUnlock
│   │   │   └── UI/
│   │   ├── Entities/             # Player, NPC, Enemy
│   │   ├── Camera/
│   │   ├── Audio/
│   │   ├── VFX/
│   │   ├── Network/              # INetworkAdapter + LocalNetworkAdapter
│   │   └── Utils/
│   ├── Prefabs/
│   ├── ScriptableObjects/
│   ├── Scenes/
│   │   ├── Core/                 # Boot, MainMenu, Loading
│   │   ├── Maps/                 # Map_Qingshi, Map_Cangwu
│   │   └── Dungeons/             # Dungeon_Blackwind
│   ├── UI/
│   ├── Art/
│   ├── Audio/
│   ├── Animations/
│   ├── Addressables/
│   ├── Resources/                 # 代码优先阶段：InputActions + 灰盒 Player Prefab
│   └── Settings/                 # URP；正式资产接线后可迁移 InputActions 引用
├── StreamingAssets/Config/       # Unity 固定根目录；RealmConfig.json 等随 Player 打包
```

## 2. 命名约定

```csharp
// 类型: PascalCase
public class CultivationManager : MonoBehaviour { }
public interface IDamageable { }
public struct DamageInfo { }

// 私有字段 _camelCase；公共属性 PascalCase
private float _currentHealth;
public float CurrentHealth { get; private set; }

// SO 资产文件: {Type}_{Subtype}_{Name}.asset
// Item_Potion_Healing_01.asset
// Skill_Fire_EmberBolt.asset
// Enemy_Beast_GrayWolf.asset

// 事件名: On + 名词 + 过去式
// OnEnemyKilled, OnRealmBreakthrough

// 枚举值 PascalCase；ID 字符串 snake_case 或 前缀_描述
// "item_potion_heal_01", "quest_main_01_01"
```

## 3. 程序集定义（asmdef）

| 程序集 | 路径 | 依赖 | 职责 |
|--------|------|------|------|
| Wendao.Core | Scripts/Core | 无 | EventBus, Singleton, Pool, SafeBehaviour |
| Wendao.Data | Scripts/Data | Core、Unity.Newtonsoft.Json（官方包） | SO、enum、struct、配置、JSON 存档 |
| Wendao.Systems | Scripts/Systems | Core, Data | 全部玩法系统 |
| Wendao.Entities | Scripts/Entities | Core、Systems、Data、Camera、Unity.InputSystem | Player/NPC/Enemy 行为与输入适配 |
| Wendao.UI | Scripts/Systems/UI + UI 绑定 | Core、Systems、Data、Entities、Unity.InputSystem、UnityEngine.UI | UI 展示层；可读取实体公开状态，不得被 Entities 反向引用 |
| Wendao.Camera | Scripts/Camera | Core、Data、Systems | 摄像机（经 `IPlayerInputSource` 查询输入） |

**规则**：上层可依赖下层；**禁止** Entities ↔ UI 循环引用；系统间**禁止**直接持有对方 MonoBehaviour 引用，统一走 EventBus 或接口服务定位（见下）。

## 4. 服务定位（轻量）

```csharp
// Core/ServiceLocator.cs — 仅用于「跨系统查询」，不替代事件
public static class ServiceLocator
{
    public static void Register<T>(T service) where T : class;
    public static void Unregister<T>() where T : class;
    public static T Get<T>() where T : class;           // 找不到抛 InvalidOperationException
    public static bool TryGet<T>(out T service) where T : class;
    public static void Clear();                         // 场景卸载时
}
```

注册时机：各 Manager `Awake` 注册，`OnDestroy` 注销。  
**允许** Get：PlayerCombat/Enemy 查 `ICombatService` / `IStatusEffectService`、Combat 查 PlayerStats 与 `IStatusEffectService`、Equipment 查 `IInventoryService`、ItemUse 查 `IPlayerHealthService`、PlayerStats 查 `IEquipmentService` / `IPlayerResourceService` / `ICultivationService`、Skill 查 `IPlayerSkillCaster` / `IPlayerResourceService` / `ICombatService`、Cultivation 查 `ISpiritRootService` / `ICultivationStatsProvider`、Quest 查 `IInventoryService` / `ICultivationService`、Loot 查 `IInventoryService` / `ICultivationService`、Dialogue/NPC 查 `IQuestService` / `IPlayerInputSource`、Camera/Tutorial/UI 查 `IPlayerInputSource`，各 HUD 查对应只读服务；DEV `DebugConsoleService` 可查上述服务及 Entities 注册的 `IDebugPlayerService` / `IEnemySpawnService`。  
**禁止** Get 替代事件：状态变化仍须 Publish，方便 UI/成就监听。

## 5. EventBus

### 5.1 API

```csharp
public static class EventBus
{
    public static void Subscribe<T>(string eventName, Action<T> handler);
    public static void Unsubscribe<T>(string eventName, Action<T> handler);
    public static void Publish<T>(string eventName, T args); // 每 handler try/catch
    public static void Subscribe(string eventName, Action handler);
    public static void Unsubscribe(string eventName, Action handler);
    public static void Publish(string eventName);
    public static void Clear(); // 切场景调用
}
```

### 5.2 事件清单（权威）

| 事件名 | 参数类型 | 发布者 | 典型监听 |
|--------|----------|--------|----------|
| OnDamageApplied | DamageInfo | CombatSystem | 伤害飘字, VFX, Audio |
| OnPlayerDamaged | DamageInfo | PlayerStats | HUD, VFX, Audio |
| OnPlayerDied | DeathInfo | PlayerStats | UI, Save, Ach |
| OnPlayerRespawned | PlayerRespawnInfo | PlayerStats | Death UI, Camera, Save |
| OnPlayerHealed | HealInfo | PlayerStats/Item | HUD, VFX |
| OnPlayerDodged | PlayerDodgeInfo | PlayerController | Camera, VFX, Audio |
| OnLockOnChanged | LockOnInfo | PlayerTargetingController | Camera, HUD |
| OnStatusEffectChanged | StatusEffectInfo | StatusEffectManager | HUD, VFX, 控制状态调试 |
| OnElementReactionTriggered | ElementReactionInfo | CombatSystem | G01-05 Camera, 彩色飘字, VFX, Audio |
| OnEnemyKilled | EnemyDeathInfo | Enemy/TrainingDummy | Quest, Loot, Ach, XP, Tutorial |
| OnBossPhaseChanged | BossPhaseInfo | BossAI | HUD, Audio, VFX |
| OnXpGained | XpGainInfo | Cultivation | HUD, Ach |
| OnDeathXpPenaltyApplied | CultivationXpPenaltyInfo | Cultivation | Death UI, Cultivation HUD |
| OnRealmBreakthrough | RealmChangeInfo | Cultivation | UI, Stats, Quest, VFX |
| OnRealmBreakthroughFailed | RealmChangeInfo | Cultivation | UI, Stats |
| OnSkillLearned | SkillInfo | Skill | UI, Ach |
| OnSkillCast | SkillCastInfo | Skill | VFX, Audio, Camera |
| OnSkillUpgraded | SkillUpgradeInfo | Skill | UI, Save |
| OnItemAcquired | ItemAcquireInfo | Inventory | UI, Quest, Ach |
| OnItemUsed | ItemUseInfo | ItemUse | Stats, VFX |
| OnCurrencyChanged | CurrencyChangeInfo | Inventory | HUD, Shop UI |
| OnEquipmentChanged | EquipmentChangeInfo | Equipment | Stats, UI |
| OnEquipmentUpgraded | EquipmentUpgradeInfo | Refine | Stats, UI |
| OnQuestAccepted | QuestInfo | Quest | UI |
| OnQuestProgressed | QuestProgressInfo | Quest | UI |
| OnQuestCompleted | QuestInfo | Quest | UI, Cultivation, Faction |
| OnDailyQuestProgressed | DailyQuestProgressInfo | DailyQuestManager | UI |
| OnDailyQuestReset | DailyQuestResetInfo | DailyQuestManager | UI |
| OnDailyQuestClaimed | DailyQuestClaimInfo | DailyQuestManager | UI, Ach |
| OnDialogueStarted | DialogueInfo | Dialogue | Input, UI, Camera |
| OnDialogueEnded | DialogueInfo | Dialogue | Input, UI, Quest, Camera |
| OnDayNightChanged | DayNightInfo | DayNight | Light, Enemy, Audio |
| OnWeatherChanged | WeatherInfo | Weather | VFX, Skill, Audio |
| OnMapLoaded | MapInfo | SceneLoader | 各系统 Init |
| OnBlackwindRunStarted | BlackwindRunInfo | BlackwindDungeonSystem | Encounter/门/层 UI |
| OnBlackwindFloorEntered | BlackwindFloorInfo | BlackwindDungeonSystem | Encounter 生成当前层波次 |
| OnBlackwindFloorCompleted | BlackwindFloorInfo | BlackwindDungeonSystem | 封印门、Save、任务 |
| OnBlackwindRunReset | BlackwindRunInfo | BlackwindDungeonSystem | Encounter 清场并按 checkpoint 重建 |
| OnBlackwindRunCompleted | BlackwindFloorInfo | BlackwindDungeonSystem | 回程门、任务、成就 |
| OnAlchemyFurnaceInteracted | AlchemyFurnaceInfo | AlchemyFurnaceInteractable | Alchemy UI |
| OnCraftCompleted | CraftResultInfo | Alchemy | Inventory, UI, Ach |
| OnCraftFailed | CraftResultInfo | Alchemy | UI |
| OnShopOpened | ShopOpenInfo | ShopSystem | Shop UI, Input |
| OnShopClosed | ShopOpenInfo | ShopSystem | Shop UI, Input |
| OnShopTransactionCompleted | ShopTransactionInfo | ShopSystem | Shop UI, Save, Ach |
| OnAffectionChanged | AffectionInfo | NPC | UI, Quest |
| OnAchievementUnlocked | AchievementInfo | Achievement | UI, Title |
| OnTitleChanged | TitleInfo | Title | Stats, UI |
| OnFactionReputationChanged | FactionReputationInfo | Faction | Shop, UI, Achievement |
| OnMountChanged | MountInfo | Mount | Player, Camera |
| OnFlightStateChanged | FlightInfo | Mount/Player | Player, Camera, Audio |
| OnGameStateChanged | GameStateInfo | GameManager | Input, UI |
| OnTutorialPrompted | TutorialPromptInfo | TutorialManager | Tutorial Toast UI |
| OnToastRequested | ToastInfo | ItemUse/Equipment/各系统 | Top Toast UI |
| OnSerendipityTriggered | SerendipityInfo | Serendipity | UI, Quest, Ach |

**禁止**新增 `OnBreakthroughPhase`（突破演出用 `CultivationManager.CeremonyBeat` 只读，见 `05`）。

```csharp
public struct SerendipityInfo {
    public string SerendipityId;
    public string MapId;
    public string WorldFlag;
}

public struct BlackwindFloorInfo {
    public int Floor;       // 1..5
    public int Checkpoint;  // 0..4
}
public struct BlackwindRunInfo {
    public int StartFloor;  // checkpoint + 1
    public int Checkpoint;
    public bool WasFailure;
}
```

**新增事件必须**：更新本表 + `03_DATA_LAYER` 参数 struct + 至少一个监听说明。

**伤害/死亡 SkillId**：`DamageInfo.SkillId`、`DeathInfo.LastHitSkillId` 由 `CombatSystem.DealDamage` 从 `DamageRequest.SkillId` 写入（`03`）。

## 6. 游戏状态机

```
Boot → MainMenu → Loading → Playing ⇄ (Dialogue | CombatFlag | Cutscene | Dead | Paused)
Playing → Loading（地图传送）
Paused → MainMenu（退出）
Dead → Playing（复活）或 MainMenu（读档）
```

| 转换 | 条件 |
|------|------|
| Boot→MainMenu | 单例就绪 + Splash 结束 |
| MainMenu→Loading | 选槽位（新游戏用空模板） |
| Loading→Playing | 场景 100% + 玩家数据注入 |
| Playing→Loading | 已解锁地图/传送点切图 |
| Playing→Paused | Esc 且非 Dialogue/Cutscene |
| Playing→Dialogue | `DialogueManager.StartDialogue` |
| Playing→Dead | HP≤0 且无炼体复活次数 |
| Dead→Playing | 选复活点复活 |
| 任意→MainMenu | 确认退出（先 AutoSave） |

`CombatFlag` **不是**硬状态：用 `GameManager.IsInCombat` bool 驱动 BGM/FOV，不阻塞 Esc。

```csharp
public enum GameState { Boot, MainMenu, Loading, Playing, Paused, Dialogue, Cutscene, Dead }

public class GameManager : Singleton<GameManager>
{
    public GameState State { get; private set; }
    public bool IsInCombat { get; private set; }
    public bool TrySetState(GameState next); // 校验合法转换，失败返回 false
    public void SetCombatFlag(bool inCombat);
}
```

## 7. 存档

### 7.1 布局

```
Application.persistentDataPath/Saves/SaveSlot_{0|1|2}/
├── meta.json             # 槽位预览：名字、境界、时长、时间戳
├── profile.json
├── inventory.json
├── equipment.json
├── skills.json
├── quests.json
├── dailies.json          # 可选模块；损坏时重置，不阻断主线读档
├── npcs.json
├── factions.json
├── world.json            # 见 §7.1.1
├── achievements.json
└── settings.json         # 可全局一份，槽位外 Settings/
```

### 7.1.1 profile.json / world.json / meta.json 字段（v3.1）

```json
// profile.json（G02-04 后的角色身份/修炼/炼体字段）
{
  "schemaVersion": 1,
  "displayName": "",
  "realm": 1,
  "subStage": 1,
  "cultivationXp": 0,
  "spiritRoot": "",
  "bodyLevel": 0,
  "bodyXp": 0,
  "playTimeSeconds": 0,
  "mainQuestIndex": "quest_main_01_01",
  "spiritStones": 0,
  "factionReputation": { "faction_danding": 20 }
}

// world.json
{
  "schemaVersion": 1,
  "unlockedMaps": ["map_qingshi"],
  "unlockedTeleports": [],
  "timeOfDay": 10.0,
  "tutorialsCompleted": ["tut_move"],
  "serendipityFlags": ["serendipity_qingshi_01"],
  "dungeonCheckpoint": { "map_blackwind": 0 },
  "questFlags": {
    "questFlag_main_foundation_pity": false,
    "foundation_pill_granted": false
  }
}

// meta.json 额外建议
{
  "schemaVersion": 1,
  "displayName": "",
  "realm": 1,
  "subStage": 1,
  "playTimeSeconds": 0,
  "mainQuestIndex": "quest_main_01_01",
  "spiritStones": 0,
  "savedAt": ""
}
```

`bodyLevel` 取 `BodyLevel` 的整数值 `0..4`，`bodyXp` 为有限非负累计经验。
二者是 schema v1 的向后兼容追加字段：旧档缺失时按 `0 / 0`（凡躯）读取，
不为这次纯追加改动抬高 `schemaVersion`。炼体服务直接读取当前 Profile，
`LoadGame` 替换 Profile 后不得继续使用旧槽位的运行时影子值。

**黑风 checkpoint 整数契约（权威）**：

```
dungeonCheckpoint.map_blackwind ∈ 0..4
N = 再入时出生楼层 B(N+1)；首次 0→B1
通关 B(k)（k=1..4）：checkpoint = max(N, k)  // 例：通关 B2→2→再进 B3
通关 B5：checkpoint 保持 4（可重刷）；B5 战败不回退
泉水 B4：运行时 potionUsedFlags 本局，退出/通关/B5 失败后清除
```

### 7.1.2 inventory.json / equipment.json（G-VS-03）

`inventory.json` 固定保存 50 个槽位、灵石和**未穿戴**装备实例；`equipment.json` 只保存已穿戴实例。实例在两个模块间转移所有权，不得同时出现。

```json
// inventory.json
{
  "schemaVersion": 1,
  "slots": [
    { "itemId": "item_potion_heal_01", "count": 2, "bound": false,
      "instanceId": "", "extraJson": "" }
  ],
  "spiritStones": 0,
  "equipmentInstances": []
}

// equipment.json
{
  "schemaVersion": 1,
  "worn": [
    { "slot": "Weapon", "itemId": "eq_weapon_wood_sword", "bound": false,
      "instance": { "instanceId": "...", "equipmentDataId": "eq_weapon_wood_sword",
        "refineLevel": 0, "durability": 100, "gemIds": [] } }
  ]
}
```

### 7.1.3 quests.json（G-VS-06）

```json
{
  "schemaVersion": 1,
  "quests": [
    {
      "questId": "quest_main_01_02",
      "status": "Active",
      "objectiveProgress": [2],
      "acceptRewardsGranted": true
    }
  ]
}
```

`Completed` 是待交付态，仍出现在任务追踪；发放 TurnIn 奖励后写 `TurnedIn` 并从追踪移入完成清单。接取、每次有效进度与交付均立即保存 `quests` 模块；完整 `SaveGame` 仍负责跨模块一致快照。

### 7.1.4 dailies.json（G05-05）

```json
{
  "schemaVersion": 1,
  "cycleStartedUtc": "2026-07-15T03:00:00.0000000Z",
  "quests": [
    { "questId": "quest_daily_hunt", "progress": 0, "claimed": false },
    { "questId": "quest_daily_gather", "progress": 0, "claimed": false }
  ]
}
```

每轮从 `cycleStartedUtc` 起满现实 24 小时重置，不按本地午夜计算；系统时钟回拨也会重建本轮，避免永久卡住。`dailies` 是可选存档模块：缺失、逻辑非法或 JSON 损坏时只重置两条日常，`profile/world/quests` 仍正常载入。

### 7.2 API

```csharp
public class SaveManager : Singleton<SaveManager>
{
    public const int SlotCount = 3;
    public bool SaveGame(int slot);
    public bool LoadGame(int slot);
    public bool DeleteSave(int slot);
    public SaveMetadata GetMetadata(int slot); // 损坏则 IsCorrupted=true
    public SaveMetadata[] GetAllSaves();
    public void AutoSave(); // 节流：最短间隔 60s；关键节点强制
    public void SaveModule(string moduleName); // profile/inventory/...
    public bool RegisterModule<T>(string name, Func<T> capture,
        Action<T> restore, Action reset = null); // 缺旧模块文件时 reset，禁止串档
}
```

物理归属：`Scripts/Data/Save/SaveManager.cs`。存档与配置需要保留 JSON 字典形状，故由 Data 层引用 Unity 官方 `Unity.Newtonsoft.Json`；Core 不反向依赖 Data。每个文件先写 `.tmp`，已有文件替换时保留一份 `.bak`；`meta.json` 在整槽保存中最后写入，作为槽位提交标记。

**自动存档触发**：传送、任务完成、突破成功/失败、进入副本、退出到主菜单。

### 7.3 损坏处理

- 读 JSON 失败 → 标记 corrupted，主菜单显示「损坏」，提供删除  
- 写盘 IOException → 弹 Toast「存档失败」，不崩溃  

## 8. 对象池

```csharp
public class ObjectPool<T> where T : Component
{
    public ObjectPool(T prefab, int prewarm, Transform parent = null);
    public T Get();
    public void Return(T instance);
    public void Prewarm(int count);
    public void Clear();
}
```

用于：伤害数字、投射物、通用 VFX、敌人子弹（可选）。

## 9. 场景流程

| 场景 | 用途 | 常驻 |
|------|------|------|
| Boot | 初始化单例、Addressables | 否 |
| MainMenu | 存档选择 | 否 |
| Loading | 异步加载目标场景 | 否 |
| Map_* / Dungeon_* | 玩法 | GameManager 等 DDOL |

`Wendao.Systems.World.SceneLoader.LoadMap(string mapId, string spawnId)` → Loading → 发布 `OnMapLoaded`。SceneLoader 物理归属 Systems/World，因为它同时依赖 Core 状态机与 Data.MapInfo；禁止为放进 Core 制造 Core→Data 反向依赖。

```csharp
public sealed class SceneLoader : Singleton<SceneLoader>
{
    public float Progress { get; }            // 当前序列 0..1，只增不减
    public bool IsLoading { get; }
    public event Action<float> ProgressChanged; // LoadingView 紧耦合展示回调，非全局业务事件
    public bool LoadMap(string mapId, string spawnId);
    public bool LoadMainMenu();
}
```

Boot 通过 `SceneFlowBootstrap` 创建 GameManager / SaveManager / ConfigDatabase / SpiritRootSystem / CultivationManager / InventoryManager / LootSystem / ItemUseSystem / EquipmentManager / QuestManager / DialogueManager / SceneLoader / StatusEffectManager / CombatSystem / SkillManager / TutorialManager，加载默认 Config 后进入 MainMenu。StatusEffectManager 必须先于 CombatSystem 创建，保证反应管道首帧可查询。切图时先加载 Loading，再异步准备目标场景；目标可激活前进度最多 0.95，激活且状态进入 Playing 后必须报告精确 `1`，随后发布一次 `OnMapLoaded(MapInfo)`。玩家可见的主菜单与加载文案必须使用 `09§16` localization key + 中文 default。

## 10. 错误处理

- 业务逻辑继承 `SafeBehaviour`，`SafeStart` 包 try/catch  
- EventBus Publish 隔离 handler 异常  
- 公共 API 失败返回 `bool` 或 `Result`，**不抛**业务异常（配置缺失可抛，便于开发期发现）  

## 11. 调试命令（Development Build）

| 命令 | 作用 |
|------|------|
| /god | 无敌 |
| /killall | 杀附近敌人 |
| /setrealm r s | 设境界与子阶 |
| /givexp n | 加修为 |
| /give id n | 给物品 |
| /spawn id [n] | 刷怪（默认 1，只允许已注册 EnemyData） |
| /tp mapId spawnId | 传送 |
| /save | 强制存档 |
| /timescale n | 时间缩放 |
| /tutorial_skip | 跳过切片移动/战斗教程，复用同一 `tutorialsCompleted` keys |

G-VS-08 实现约束：命令解析位于 Systems，通过 `ServiceLocator` 查询；`/spawn`、`/god` 分别由 Entities 的 `IEnemySpawnService`、`IDebugPlayerService` 承接，不允许 Systems 反向引用 Entities。`/tutorial_skip` 必须调用 `TutorialManager` 的正常完成路径，不得另建 debug 存档字段。

正式版：命令服务、控制台 UI 与启动挂载均由 `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` 包裹。
