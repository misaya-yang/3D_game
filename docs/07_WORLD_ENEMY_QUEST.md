# 07 · 地图、昼夜天气、敌人 AI、任务、NPC、奇遇

> **版本**: v3.1

---

## 1. 地图与场景

### 1.1 MVP 三图

| MapId | 场景名 | 推荐境界 | 定位 | 飞行 |
|-------|--------|----------|------|------|
| map_qingshi | Map_Qingshi | 练气 | 新手村+平原+小林 | 否 |
| map_cangwu | Map_Cangwu | 筑基 | 山脉+洞府+传送枢纽 | 是 |
| map_blackwind | Dungeon_Blackwind | **金丹初（硬门槛）** | 5 层线性秘境+BOSS | 否 |

### 1.2 每张地图必须具备的灰盒要素

**青石原 map_qingshi**

| 区域 | 内容 |
|------|------|
| 青石镇 | NPC：掌柜、药老、宗门接引；商店；传送阵；存档点 |
| 习武坪 | 木桩、教程触发、安全区 |
| 东郊荒野 | 普通狼/狐妖刷怪点 ×4 |
| 灵草涧 | 采集点 ×6，精英「灰爪」 |
| 秘径入口 | 主线机关：需任务钥匙开通往苍梧的传送 |

G03-04 代码优先灰盒将灵草涧 6 点落实为：清心草 ×4（每次 1–2，30s 刷新）与
灵尘 ×2（每次 1–2，45s 刷新）。正式场景美术可替换模型和坐标，但必须保留
`09_CONTENT§7.1` 的稳定 NodeId、数量与交互契约。

**苍梧山脉 map_cangwu**

| 区域 | 内容 |
|------|------|
| 山门平台 | 传送、炼丹炉、任务板 |
| 盘山道 | 普通敌人 + 滚石简易机关（触发器伤害） |
| 云雾谷 | 雾天气默认，隐秘宝箱 ×2 |
| 洞府 | 支线 NPC、功法残页 |
| 雷台远眺 | 主线过场点；黑风入口（**Realm≥GoldenCore**） |

**黑风秘境 map_blackwind**

| 层 | 内容 |
|----|------|
| B1 | 小怪波次 2，教学机关：压力板开门 |
| B2 | 精英房，需击杀开门 |
| B3 | 岔路：左宝箱右近路（陷阱） |
| B4 | 多刷怪+回复泉（回 50% HP **本局一次**） |
| B5 | BOSS 黑风石将军 3 阶段 + 预警（G04-05） |

**入口**：未金丹 → Toast `ui_blackwind_gate`，禁止加载。

### 1.2.1 黑风 checkpoint（权威契约 · 见 `02§7.1.1`）

```
world.dungeonCheckpoint.map_blackwind ∈ 0..4
N → 再进出生 B(N+1)；通关 B(k)→ max(N,k)；B5 战败不回退；泉水本局
例：通关 B2 → checkpoint=2 → 再进 B3
```

G05-03 运行时由 `BlackwindDungeonSystem` 持有单局状态：进入时读取 checkpoint，
将 `CurrentFloor` 设为 `N+1`；B1 必须同时完成两波战斗与压力板，B2 击杀精英，
B3 穿过左宝箱/右陷阱后的出口，B4 清怪，才分别推进 1–4。B4
`spring_blackwind_b4_heal` 回复最大生命 50%，同一局第二次无效；退出、通关或玩家
死亡即清除使用态。B5 只在石将军死亡后标记本局完成，胜败都不改写 checkpoint=4。
苍梧入口 `entrance_blackwind_cangwu` 读取真实境界，低于金丹只发布
`ui_blackwind_gate`，不得开始场景加载。

### 1.3 传送

```csharp
public class MapTravelSystem : SafeBehaviour
{
    public IReadOnlyList<string> UnlockedMaps { get; }
    public IReadOnlyList<string> UnlockedTeleports { get; }

    public void UnlockMap(string mapId);
    public void UnlockTeleport(string teleportId);
    public bool Travel(string teleportId); // Loading + spawn
}
```

- 传送点需**首次踩点解锁**  
- 副本入口：确认框「是否进入」，是则加载；通关/退出回入口  

G05-02 运行时契约：`MapTravelSystem` 以 `world.UnlockedMaps` 与
`world.UnlockedTeleports` 为唯一解锁状态，青石镇传送点为新档默认值；
`TeleportPoint` 只接受玩家碰撞体，首次踩入后同时解锁所在地图与传送点并触发自动
存档。青石秘径的 `CangwuPathGate` 只读取
`quest_flag_main_cangwu_path_open`：未开启时显示本地化 Toast，开启后加载
`Map_Cangwu` 并在 `teleport_cangwu_gate` 出生。苍梧山门落地后第一次踩阵，才把
该传送点写入解锁列表。

### 1.4 安全区

`SafeZone` 触发器：禁止敌对 AI 进战、禁止 PVP（预留）、可回血加速 ×2。

G05-01 运行时由 `ISafeZoneService` 统一查询位置：敌人不得对安全区内玩家
`OnAggro`，已有目标进入安全区时敌人立即 Return；`CombatSystem` 拒绝敌方来源
对区内玩家的伤害。脱战恢复时 `PlayerStats` 读取区域倍率，青石镇/习武坪为 2×。
青石安全区稳定 ID 为 `safezone_qingshi_town_training`。

---

## 2. 昼夜与天气

### 2.1 DayNightCycle

- 真实 48 分钟 = 游戏 24h（可配置）  
- 事件点：6:00 / 18:00 发布 `OnDayNightChanged`  
- 夜间：部分敌人替换/攻击力 +10%（配置开关，默认开）  

### 2.2 WeatherSystem

```csharp
public class WeatherSystem : SafeBehaviour
{
    public WeatherId Current { get; }
    public void ForceWeather(WeatherId id, float duration);
    public float GetElementDamageBonus(ElementType e);
    public float GetVisionMul();      // Fog 0.75（与下表一致）
    public float GetFlightSpeedMul(); // Storm 0.8；MVP 无 Storm 可忽略
}
```

| 天气 | 权重（青石） | 效果 |
|------|--------------|------|
| Clear | 70 | 无 |
| Rain | 20 | Water/Ice/Lightning 技能 +10% |
| Fog | 10 | 视野/神识锁定距离 ×0.75 |

切换：持续 120–300s 随机 → 按权重抽 → 3s 过渡 → 事件。

### 2.3 G07-01 运行时实现

- `DayNightSystem` 以真实 2880s 对应游戏 24h，直接读写既有
  `SaveWorldData.TimeOfDay`；只在 `GameState.Playing` 推进，在 6:00/18:00 跨界时
  发布 `OnDayNightChanged`。三张玩法场景复用或补建 Directional Light，按时刻更新
  旋转、颜色、强度与环境光；夜间敌方来源伤害倍率为 1.1。
- `WeatherSystem` 仅实现 MVP 的 Clear/Rain/Fog：每段 120–300s，切换 3s，完成后
  发布 `OnWeatherChanged`。雨天 Water/Ice/Lightning 伤害随过渡插值至 +10%，并启用
  灰盒雨滴与 `AMB_Rain`；雾天视野/锁定距离插值至 ×0.75。
- 三图占位权重：青石 `70/20/10`，苍梧 `50/20/30`，黑风 `Clear 80/Fog 20`。
  黑风不生成室内雨；正式环境资产可替换表现，但不得绕开 `IWeatherService` 的数值查询。

---

## 3. 敌人 AI

### 3.1 状态机（普通/精英）

```
Idle ⇄ Patrol → Alert(发现) → Chase → Attack ⇄ Skill
                      ↓              ↓
                   Return ←—— 超 Disengage / 目标死
                      ↓
                    Dead
```

| 状态 | 行为 |
|------|------|
| Idle | 站岗，感知球 AggroRange |
| Patrol | 沿路径点，到达等待 1–2s |
| Alert | 转向玩家 0.4s，叹号 VFX |
| Chase | 追击，保持 AttackRange |
| Attack | 近战/远程按 AttackInterval |
| Skill | 精英/BOSS 释放 SkillIds |
| Return | 脱战回出生点，回满 HP |
| Dead | 掉落、XP、禁用碰撞 |

G04-01 运行时契约：青石原由 `QingshiNavigationSurface` 基于地图 Physics
Collider 构建 NavMesh；普通敌人携带 `NavMeshAgent`，实际位移沿完整路径角点
驱动 `CharacterController`，不可在路径失败时直穿已有 NavMesh 障碍。灰狼出生后
从 Idle 进入 Patrol，路径点停留 1–2s；发现玩家先进入 0.4s Alert 并显示代码优先
叹号，再进入 Chase/Attack；脱离出生点范围或目标死亡进入 Return，到家回满生命。
青石原灰狼分布为三个独立刷怪区，刷怪与死亡重生仍由各自 `EnemySpawner` 管理。

G04-02 精英契约：`enemy_wolf_elite` 复用普通敌人状态机并启用 Skill 状态；
目标处于 2.5–9m 时可释放 `skill_enemy_wolf_elite_charge`，先蓄力再锁定方向
冲锋，命中仅结算一次伤害，结束后进入冷却并回到 Chase/Attack。灵草涧独立
Spawner 保持 1 只，外观使用更大的赤褐色代码优先占位体。

### 3.2 API

```csharp
public class EnemyBrain : SafeBehaviour, IDamageable
{
    public EnemyData Data;
    public void SpawnInit(EnemyData data, Vector3 pos);
    public void TickAI(float dt);
    public void OnAggro(GameObject target);
    public void ForceState(/*...*/);
}

public class EnemySpawner : MonoBehaviour
{
    public string EnemyId;
    public int MaxAlive;
    public float RespawnSeconds;
    // 安全区外；玩家远离 80m 可暂停
}

public class LootSystem : SafeBehaviour
{
    public void DropLoot(EnemyData data, Vector3 pos);
}
```

### 3.3 BOSS 阶段

- HP 穿阈 → `OnBossPhaseChanged`  
- 过渡 1.5s：可无敌、播动画、清场地投射物  
- **G04-03**：三阶段技能差、血条、出圈重置（不含完整 telegraph 调参）  
- **G04-05**：播放 `BossSkillTelegraph`（`03`），Duration≥0.6s，RecoverStun 窗口  

黑风石将军（摘要，telegraph 实例见 `09`）：  

| 阶段 | HP | 行为 |
|------|-----|------|
| P1 | 100–70% | 锤击、石刺 + 预警 |
| P2 | 70–30% | 冲锋、召唤小怪 2 + 预警 |
| P3 | 30–0% | 狂暴 +30% 攻速，全屏砸地预警 |

**G04-05 运行时契约**：进入 BOSS 技能态时按当前阶段和 `SkillId` 查找
`BossSkillTelegraph`，灰盒预警至少保持完整 `Duration` 后才结算；命中沿用同一
`SkillId` 写入 `DamageInfo`。结算后 BOSS 在 `RecoverStun` 内停留于 Skill 态、
可受玩家伤害，形成稳定惩戒窗；出圈/转阶段/死亡会立即取消预警。

### 3.4 规则

- 不穿墙：NavMesh 或简单 CharacterController + 避障  
- 同时追击玩家上限：同屏激活 AI ≤15  
- 精英：血 ×3，攻击 ×1.5，有 1 技能  
- 世界 BOSS：MVP 不做  

### 3.5 EdgeCases

| 条件 | 行为 |
|------|------|
| 目标死 | Return |
| 出生点被堵 | 传送回出生点 |
| BOSS 战中玩家出圈 | 脱战重置 BOSS（竞技场 Trigger） |

---

### 3.6 G-VS-07 代码优先闭环

`enemy_wolf_gray` 使用 `Idle → Chase ⇄ Attack → Return` 最小状态机；受击可直接仇恨玩家，目标死亡或玩家/灰狼越过出生点 `DisengageRange` 后回巢，抵达时回满 HP。Return 被碰撞阻塞 3s 后传送出生点，满足出生点堵塞边界。移动先用 `CharacterController` 碰撞，不提前引入 G04-01 的 Patrol、Alert、NavMesh 与路径点。

青石灰盒在 `(10,0,8)` 放置一个代码优先 Spawner，同时存活 3 只灰狼；尸体保留 1s，8s 后补齐到 `MaxAlive`。灰狼占位移动参数为 `MoveSpeed=3.2`、`AggroRange=8`、`AttackRange=1.6`、`DisengageRange=14`、`AttackInterval=1.2`；HP/Atk/XP/掉落仍以 `09§5` 为数值权威。

`LootSystem` 只消费带真实 `Victim` 的 `OnEnemyKilled`，每个 Victim 只结算一次：发放原始修为后按 `EnemyData.LootTable` 掷骰。狼毫成功掉落时优先直接进入背包；背包满或背包服务暂不可用时生成 180s 世界拾取物，玩家触发拾取后仍走 `IInventoryService.AddItem(..., AcquireSource.Loot)`。ID-only 模拟事件只用于任务计数，不产生战斗奖励。

---

## 4. 任务系统

### 4.1 API

```csharp
public class QuestManager : SafeBehaviour
{
    public IReadOnlyList<string> ActiveIds { get; }
    public IReadOnlyList<string> CompletedIds { get; }

    public bool CanAccept(string questId);
    public bool Accept(string questId); // 成功后发放 AcceptRewards（一次）
    public void NotifyKill(string enemyId);
    public void NotifyCollect(string itemId, int totalCount);
    public void NotifyUseItem(string itemId);
    public void NotifyCraft(string resultItemId, int resultCount);
    public void NotifyTalk(string npcId);
    public void NotifyReach(string locationId);
    public void NotifyRealm(RealmType newRealm); // ReachRealm 目标
    public bool CanTurnIn(string questId);
    public bool TurnIn(string questId);
    public QuestStatus GetStatus(string questId);
    public string ResolveInteractionDialogueId(string npcId, string fallbackDialogueId);
}
```

### 4.2 进度管道

```
Accept → Active → 若 AcceptRewards 非空则 Inventory 发放 → 评估 Collect latch
事件(OnEnemyKilled / OnItemAcquired / OnItemUsed / OnCraftCompleted / OnRealmBreakthrough 等)
  → 匹配 Active 目标 → Current++（Collect+LatchOnFirstAcquire 则锁存）
  → ObjectivesAreOrdered=true 时，仅首个未完成目标可推进
  → OnQuestProgressed
  → 全部完成 → Completed（待交付）
  → TurnIn → Rewards → OnQuestCompleted
ReachRealm: OnRealmBreakthrough Success && NewRealm >= required(TargetId)
```

**G-VS-06 代码优先闭环**：`quest_main_01_02` 由药老对话接取，目标为 `enemy_wolf_gray ×3`，交付奖励原始修为 `100` + 灵石 `20`；杀敌来源只监听 `OnEnemyKilled`，超额封顶。真实灰狼生成、追击和掉落仍归 G-VS-07，本 Goal 不提前实现。任务状态/进度/接取奖励门禁写入独立 `quests.json`，正常交付后才发布 `OnQuestCompleted`。

日常：MVP 现实 24h 重置；**降权**——框架 API + hunt/gather 两条，不卡第一章。

### 4.3 EdgeCases

| 条件 | 行为 |
|------|------|
| 重复接取 | false |
| AcceptRewards | 只发一次 |
| Collect Latch | 突破耗丹后不回退进度 |
| 有序目标提前收到事件 | 忽略；前一目标完成后需重新收到对应事件 |
| 未完成交付 | false |
| 前置任务未完成 | Locked |
| 击杀超额 | 不超额计数 |

### 4.4 主线关键目标（内容权威在 `09`）

- `quest_main_01_08`：AcceptRewards 筑基丹×1 + pity Flag；目标含突破筑基  
- `quest_main_01_09`：AcceptRewards 凝金丹×1；目标 Collect(latch) → ReachRealm GoldenCore → Reach blackwind_entrance  
- `StartNpcId` 与 `TurnInNpcId` 可不同；NPC 先路由可交付任务，再路由可接取任务  


### 4.5 G05-05 支线与日常落地

三条支线复用 `QuestManager`、`quests.json` 与通用 NPC 路由：

- `quest_side_herb_01`：丹鼎接引接取/交付，Collect 清心草×10，奖励灵石 50 + `faction_danding` 声望 20。
- `quest_side_bandit_01`：武教习接取/交付，Kill `enemy_bandit`×8，奖励铁剑 + 原始修为 200。
- `quest_side_hermit_01`：丹鼎接引发放绑定任务物品 `item_quest_hermit_letter`，向 `npc_hermit` Talk 后交付；手书在 TurnIn 时消耗，奖励直接学习 `skill_wind_slash`。

```csharp
public interface IDailyQuestService
{
    IReadOnlyList<DailyQuestRuntimeState> Active { get; }
    DateTime CycleStartedUtc { get; }
    DateTime NextResetUtc { get; }
    DailyQuestRuntimeState GetState(string questId);
    bool IsComplete(string questId);
    bool TryClaim(string questId);
    bool Refresh(DateTime utcNow);
}
```

`quest_daily_hunt` 监听 `OnEnemyKilled`，任意敌人累计 15；`quest_daily_gather` 只计算 `OnItemAcquired.Source == Gather`，累计物品数量 5。领取后本轮不可重复领取。重置周期从 `cycleStartedUtc` 起算现实 24 小时；`dailies.json` 是可选模块，损坏只重置日常，主线/支线存档仍可载入。

---

## 5. NPC 与对话

> 编号：奇遇见 §6。

### 5.1 API

```csharp
public class DialogueManager : SafeBehaviour
{
    public bool IsOpen { get; }
    public void StartDialogue(string dialogueId, string npcId);
    public void Advance();                 // 无选项时
    public void Choose(int choiceIndex);
    public void EndDialogue(bool cancelled);
}

public class NPCController : SafeBehaviour
{
    public NPCData Data;
    public int Affection { get; }
    public void Interact(); // 距离<3m + 面向
    public void AddAffection(int delta);
}
```

### 5.2 对话规则

- Start：GameState→Dialogue，禁玩家战斗输入，镜头聚焦  
- 选项不满足好感/任务：灰显或隐藏（**MVP 隐藏**）  
- End：恢复状态，若节点带 QuestOffer 则 Accept  
- 场景切换：强制 End(cancelled=true)  
- 正常结束的 `QuestTurnInId` 仅在当前 NPC 等于任务 `TurnInNpcId` 时交付；取消不接取、不交付、不推进 Talk 目标  
- `SpeakerNameKey` / `TextKey` / `DialogueChoice.TextKey` 搭配中文 default，禁止只存玩家可见裸字符串  

### 5.3 好感

- 送礼（物品表标记）、完成相关任务 +  
- 里程碑解锁对话/任务（数据驱动）  

---

## 6. 奇遇 SerendipitySystem（G05-06 独占）

```csharp
public class SerendipitySystem : SafeBehaviour
{
    public bool TryTrigger(string id);
    public bool TryTrigger(string id, string mapId, Vector3 rewardOrigin); // Trigger 用
    public bool HasCompleted(string id);
}
```

| 地图 | 数量 | 验收 Goal |
|------|------|-----------|
| 青石 | ≥1 | G05-06 |
| 苍梧 | ≥2 | G05-06 |
| 黑风 | ≥1 | G05-06（depends G05-03 场景） |

**定义**：叙事 Trigger + 对话/独特奖励 + Once Flag。普通宝箱≠奇遇。奖励 **禁止装备**。

| Edge | 行为 |
|------|------|
| 战斗中触发 | 脱战 1s 再开对话或只发奖 |
| 背包满 | 掉地 180s |
| 已触发 | 忽略 |
| 读档 | Flag 恢复不重复 |
| 误配装备奖励 | 剥离 + LogError |

事件：`OnSerendipityTriggered`。

G05-06 代码实现选择“战斗中只发奖、不强开对话”；脱战触发则尝试播放对应叙事对话。四个稳定 Trigger 分别为青石灵草涧 1 个、苍梧云雾谷/崖边 2 个、黑风 B3 1 个。成功后将 SerendipityId 写入 `world.serendipityFlags`，并将内容 `WorldFlag` 写入 `world.questFlags`；读档后 Trigger 自动隐藏且不能再次发奖。

---

## 7. Acceptance

- [ ] 三图可加载互传，解锁逻辑正确  
- [ ] 青石原区域刷怪与采集可用  
- [ ] 黑风 5 层 + checkpoint 契约 + BOSS 三阶段  
- [ ] 金丹门禁生效  
- [ ] 昼夜/雨天效果  
- [ ] 普通怪追击/脱战回血  
- [ ] 主线含 ReachRealm / AcceptRewards / Collect latch  
- [ ] 对话分支与镜头  
- [ ] 副本脱战重置 BOSS  
- [ ] 奇遇三图配额 + Flag（G05-06）  
