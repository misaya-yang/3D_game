# 08 · UI、新手指引、坐骑、宗门、成就称号

> **版本**: v3.1  
> **localization**：所有玩家可见字符串 **必须** 有 key，中文为 default value。  
> **手柄**：战斗 + HUD + 背包 + 对话 **必做**；炼丹/地图可键鼠优先。

---

## 1. UI 架构

### 1.1 层级（Canvas Sort）

| Order | 层 | 内容 |
|-------|-----|------|
| 0 | WorldSpace 可选 | 名字板、伤害字（也可用 Screen） |
| 100 | HUD | 血蓝、快捷栏、任务追踪、小地图 |
| 200 | Panels | 背包、角色、技能、地图、任务、炼丹 |
| 300 | Modal | 确认框、商店、对话 |
| 400 | Overlay | 教程遮罩、黑屏过渡 |
| 500 | Top | Toast、成就弹窗、暂停菜单 |

### 1.2 UIManager

```csharp
public interface IUIManager // 场景内 UIManager 实现并注册 ServiceLocator
{
    public void ShowPanel(string panelId);
    public void HidePanel(string panelId);
    public void HideAllPanels();
    public bool IsPanelOpen(string panelId);
    public void ShowToast(string message, float duration = 2f);
    public void ShowConfirm(string msg, Action onYes, Action onNo = null);
    public void SetHudVisible(bool visible);
}
```

G06-01 采用场景内协调器而非跨场景 UI 单例：`SceneUiBootstrap` 先创建
`UIManager`，再创建各 View。协调器统一读取 B/C/K/J/M/Esc，处理全屏面板互斥、
Esc 顶层关闭优先级、Pause 状态与聚合玩法输入锁；面板仍保留脱离协调器时的独立
开关兼容。商店/炼丹由交互系统打开后，协调器在同帧收口其它全屏面板；
`OnDialogueStarted` 会关闭全部普通面板。这样切图销毁 UI 时不会遗留静态面板引用。

**规则**：

- Playing 下开 Panel → 可选放慢时间 `timeScale=0`（**MVP：不暂停**，仅禁移动用 Input flag）  
- Esc：若有 Panel 先关 Panel，否则 Pause  
- 同一时间全屏 Panel 互斥（背包/角色/技能/地图/任务）  
- 对话打开时关闭其它 Panel  

### 1.3 HUD 必含

| 元件 | 数据源 | 刷新 |
|------|--------|------|
| HP/Mana 条 | PlayerStats | 事件+每帧插值 |
| 境界/名字 | Cultivation | 突破事件 |
| 快捷栏 1–4 | SkillManager | CD 填充图 |
| 经验条 | Cultivation | OnXpGained |
| 任务追踪 3 条 | Quest | 进度事件 |
| 锁定目标血条 | Combat | 锁定时 |
| BOSS 血条 | Boss | 相位名 |
| 货币灵石 | Inventory | 变化时 |
| 交互提示 | Interact | E 键提示 |

### 1.4 面板清单（MVP）

| PanelId | 功能 | 快捷键 |
|---------|------|--------|
| panel_inventory | 50 格、使用、装备 | B |
| panel_character | 属性、灵根、炼体、突破按钮 | C |
| panel_skill | 已学列表、拖到栏 | K |
| panel_quest | 任务列表与描述 | J |
| panel_map | 已解锁传送 | M |
| panel_alchemy | 配方列表、炼制 | 交互丹炉 |
| panel_shop | 买/卖 | 交互商人 |
| panel_dialogue | 文本+选项 | 系统 |
| panel_pause | 继续/设置/存档/退出 | Esc |
| panel_death | 复活/读档 | 系统 |
| panel_create | 灵根选择 | 新游戏 |
| panel_tutorial | 遮罩高亮 | 系统 |

G-VS-03 的最小 `panel_inventory` 运行时生成 50 个槽位，B/手柄 Select 开关；打开时保留 UI Action 读取但禁用移动、视角和战斗输入，关闭后恢复原输入状态。正式 UI 资源可替换视图，服务 API 与 localization key 不变。

G03-03 的最小 `panel_alchemy` 运行时生成 4 个丹方条目，展示等级、累计熟练度、
材料持有量与含等级加成后的成功率；由丹炉交互调用 `SetOpen(true)`，打开/关闭时与
其他全屏面板一致锁定并恢复玩法输入。丹炉实体和场景摆放归后续世界内容 Goal。

G09-03 补齐发布运行时缺失的丹炉入口：青石镇
`furnace_qingshi_town` 与苍梧山门 `furnace_cangwu_gate` 使用真实 CC0 锅具/炉石
资产，E/手柄 West 发布 `OnAlchemyFurnaceInteracted`，由 UI 协调器互斥打开
`panel_alchemy`；关闭必须经 `IUIManager.HidePanel` 归还玩法输入。

G03-02 的代码优先 `panel_skill` 由 K 开关，展示全部已学功法、等级与下一次
残页消耗；条目可拖到始终可见的四槽快捷栏，面板打开时禁用玩法输入。
正式 UI 资源可替换视图，但保留 4 槽 drop contract、升级入口与 localization key。

G-VS-05 的代码优先界面包含 Order 300 的 `panel_create` 灵根选择 Modal 与 Order 105 的修为 HUD。创角仅显示五系按钮和“随机感应”；天/废不可点选、只能随机出现。选定前锁定玩法输入，展示 `root_intro_*` 后确认恢复；HUD 读取 `ICultivationService`，显示境界/层数与 `OnXpGained` 驱动的修为条。正式美术替换时保留服务 API、输入锁与 `09§16` localization key/default。

G-VS-06 增加 Order 300 的最小对话 Modal 与 Order 110 的任务追踪。靠近药老显示 `ui_npc_interact`，E/手柄 West 进入对话；处于 `Dialogue` 时移动、视角和战斗输入禁用，但 Interact 仍可推进第一可见选项。追踪 HUD 监听 Accept/Progress/Completed，并在目标完成后显示 `ui_quest_ready_turn_in`；读档替换任务状态时以轻量签名检测补一次刷新。

G09-03 将任务追踪升级为主线连续引导：没有 Active Quest 时不再隐藏，而是显示下一
可接主线及目标 NPC；可接/目标/可交付状态由真实 G09 图标构成屏幕边缘标记并显示
距离。标记只查询 `IQuestService` 与场景实体，不自动接取、推进或交付任务。

G01-04 增加 Order 500 的代码优先 `panel_death`：监听 `OnPlayerDied` 显示
死亡提示、当前层修为 5% 惩罚和“最近传送阵复生”按钮；按钮通过
`IPlayerRespawnService` 查询，不直接依赖 Entities。`OnPlayerRespawned` 后关闭。
读档按钮与炼体额外复活次数不在本 Goal。

G02-04 增加 Order 210 的代码优先 `panel_character`：C 键通过统一 Input
Action 开关；打开时禁用玩法输入，关闭时精确恢复原状态。面板通过
`ICultivationService`、`ISpiritRootService`、`IBodyRefinementService` 与
`IPlayerCharacterStatsService` 查询境界、灵根、炼体及 Final 战斗属性，UI
不得反向引用 Entities。突破按钮先关闭面板并归还输入，再调用
`ICultivationService.TryBreakthrough()`，由突破状态机接管输入锁与阻断 Toast。

G06-01 补齐 `panel_quest`、`panel_map` 与 `panel_pause`：任务面板合并主/支线和
24 小时日常进度/领奖；地图面板只列出 `IMapTravelService` 已解锁传送；暂停菜单
提供继续、保存、设置摘要与确认返回主界面。HUD 增加玩家名、HP/Mana 与灵石，
监听受伤、治疗、`OnCurrencyChanged`，条形视觉允许每帧平滑；所有面板打开时全量
刷新，运行中变化由系统事件驱动。

### 1.5 刷新策略

- **事件驱动**为主，禁止全 UI 每帧查全部系统  
- 进度条视觉可每帧 lerp  
- 打开面板时 `Refresh()` 全量刷一次  

### 1.6 UI 线框（文字线框，便于实现）

```
HUD:
┌──────────────────────────────────────────────────────────┐
│ [境界] 名字          灵石:9999              小地图/时钟   │
│ HP ████████░░  Mana ██████░░░░                           │
│ XP ──────────                                            │
│                                                          │
│ 任务:                                                    │
│ · 猎狼 2/5                                               │
│                                                          │
│                     [BOSS========--]                     │
│                                                          │
│  [1][2][3][4]                              交互[E]       │
└──────────────────────────────────────────────────────────┘
```

---

## 2. 新手指引 TutorialManager

```csharp
public class TutorialManager : SafeBehaviour
{
    public bool IsActive { get; }
    public bool IsForced { get; }
    public IReadOnlyList<string> KnownTutorialIds { get; }
    public bool TryStart(string tutorialId); // 已完成则 false，不重播
    public bool RequestStart(string tutorialId); // 忙时去重排队
    public void CompleteStep(string stepId);
    public void Complete(string tutorialId);
    public bool DismissCurrent(); // 仅非强制教程
    public void Skip(); // 仅 DEVELOPMENT：写同一完成 keys
    public bool HasCompleted(string tutorialId);
    public bool AllowsInput(TutorialInputAction action);
    public void RepublishActivePrompt(); // 场景 UI 重建后重放当前遮罩
}
```

**单一完成态（KD-7）**：

| 阶段 | 表现 | 完成态 |
|------|------|--------|
| **G-VS-01/02 起** | Toast + 可选提示 | 写入 `world.json tutorialsCompleted` |
| **G06-02** | 遮罩挖洞升级 | `HasCompleted` → **skip 不重播** |

| tutorialId | 触发 | 步骤 | 强制 |
|------------|------|------|------|
| tut_move | 首次进入青石 | 移动→视角→跳 | 是 |
| tut_combat | 首次进战 | G-VS-02：轻击→击破木桩 | 是 |
| tut_skill | 学会第一功法 | 开面板→装备→释放 | 否 |
| tut_inv | 首次获得物品 | 开背包→使用/装备 | 否 |
| tut_cult | 首次可突破 | 角色面板→突破 | 否 |
| tut_dungeon | 首次进黑风 | 介绍→机关→BOSS | 否 |
| tut_flight | 筑基且有飞剑 | 召唤→起飞→降落 | 是 |
| tut_alchemy | 首次点丹炉 | 材料→配方→炼 | 否 |

G06-02 实现说明：保留上述八个稳定 ID 与同一个
`world.tutorialsCompleted` 列表。移动/战斗/飞行属于强制教程，运行时输入源只放行
当前步骤动作与 Pause；其余教程不禁玩法输入，可点“知道了”完成。新触发若遇到正在
播放的教程会去重排队，避免首次掉落/学会功法事件丢失。遮罩由上下左右四块暗幕围出
真实透明洞，强制教程仅在洞外拦截 UI 指针；切图重建 View 后通过
`RepublishActivePrompt()` 恢复同一步。现阶段已接线首次物品、首次功法、可突破、
黑风入场与丹炉交互；`tut_flight` 的实际触发由 G06-03 接入。

非强制教程的最小步骤在对应成功行为（使用/装备、施放功法、突破、进入黑风楼层、
完成一次炼丹）时自动完成，也可由玩家主动确认；已完成 ID 无论新旧存档都不重播。

G-VS-08：**必须** `HasCompleted(tut_move/tut_combat)`（或 DEV `/tutorial_skip`），取消「可后补先手动会玩」作为完成标准。

`tut_combat` 的垂直切片完成态只覆盖当时已解锁的 L1 最小闭环。G01-01 解锁重击/闪避/格挡后可追加非强制提示，但禁止清除 `tut_combat` 或强制重播，以维护 KD-7 单一完成态。

### 2.1 必出文案 key（全文见 `09`）

`root_intro_*`、`ui_death_revive`、`ui_bt_success`/`ui_bt_fail`、`ui_blackwind_gate`。

### 2.2 角色面板

- 天灵根显示 **1.35×** 修炼倍率（KD-24）  
- 突破按钮旁展示 `GetBreakthroughBlockers` 与成功率  
- 突破结果：属性对比面板（仪式拍 5）

---

## 3. 坐骑 MountManager

```csharp
public class MountManager : SafeBehaviour
{
    public bool IsMounted { get; }
    public bool IsFlying { get; }
    public bool TryMount(string mountId);
    public void Dismount();
    public bool TryTakeOff();   // 需 CanFly && Realm>=Foundation && 地图 AllowFlight
    public void Land();
}
```

| 规则 | 值 |
|------|-----|
| MVP 坐骑 | mount_spirit_horse 速度 ×1.5，不可飞 |
| 飞剑 | mount_flying_sword 筑基+，可飞 |
| 马上攻击 | **不可**，需下马 |
| 受伤 | 不强制下马；BOSS 战可禁飞行 |
| 飞行高度 | 最高 40m，用地面探测 |

### 3.1 G06-03 实现说明

- `MountManager` 以 `IMountService` 注册；V 键召唤/收起当前坐骑。炼气期默认灵马，
  达到筑基后自动解锁并选择 `mount_flying_sword`，召剑成功后直接起飞。
- 灵马把玩家地面移动倍率设为 1.5；飞剑水平/垂直移动复用既有 Sprint/Walk 速度，
  Space 按住上升、F 按住下降。每帧地面探测将相对高度限制在 40m 内，落地时回收到
  最近有效地面。
- `Dungeon_Blackwind` 整图禁飞；`NoFlyZone` 支持 BOSS 场等局部禁飞体积，飞行中进入会
  立即安全落地。骑乘时轻重攻击、功法、格挡和闪避均拒绝，受伤不会自动下马。
- 解锁列表与当前选择写入 `mounts.json`；骑乘/飞行运行态不跨读档。无正式模型时由系统
  生成灵马/飞剑色块占位，ID、事件与后续 Prefab 接口保持稳定。
- `OnMountChanged` / `OnFlightStateChanged` 驱动摄像机骑乘与飞行构图；首次实际起飞触发
  `tut_flight`，收剑落地完成该强制步骤。

---

## 4. 宗门 FactionManager

```csharp
public class FactionManager : SafeBehaviour
{
    public int GetRep(string factionId);
    public void AddRep(string factionId, int delta);
    public int GetRank(string factionId); // 0–5
    public float GetShopDiscount(string factionId);
    public bool HasJoined(string factionId);
    public bool Join(string factionId);
}
```

**MVP 宗门**：`faction_danding` 丹鼎宗（青石接引）。

| Rank | 声望 | 折扣 | 权限 |
|------|------|------|------|
| 0 陌生 | 0 | 0% | 基础对话 |
| 1 挂名 | 100 | 5% | 日常任务 |
| 2 外门 | 300 | 10% | 商店进阶 |
| 3 内门 | 600 | 15% | 功法库（1 技能） |
| 4 核心 | 1000 | 20% | 称号 |
| 5 长老 | 2000 | 25% | 预留 |

### 4.1 G06-04 宗门实现说明

- `FactionManager` 以 `IFactionService` 注册，`profile.FactionReputation` 的键存在即代表已加入；
  任务奖励和成就奖励统一调用 `AddRep`，发布 `OnFactionReputationChanged` 并即时保存 profile。
- 张掌柜归属 `faction_danding`，`ShopSystem.GetBuyPrice` 按当前 Rank 折扣后向下取整，最低
  价格为 1；卖价不受宗门折扣影响。
- 当前 MVP 只实现丹鼎宗 API、声望阶位和折扣，不扩展宗门经营或功法库 UI。

---

## 5. 成就与称号

```csharp
public class AchievementManager : SafeBehaviour
{
    public void OnTrigger(string triggerType, string targetId, float value);
    public bool IsUnlocked(string id);
}

public class TitleManager : SafeBehaviour
{
    public string ActiveTitleId { get; }
    public bool Equip(string titleId);
    public void Unequip();
    public StatBlock GetActiveBonus();
}
```

监听：OnEnemyKilled、OnRealmBreakthrough、OnQuestCompleted、OnItemAcquired、OnCraftCompleted 等 → `OnTrigger`。  
解锁：发奖、OnAchievementUnlocked；有 `RewardTitleId` 则 Title 解锁。

**MVP 成就 ≥10** 见 `09_CONTENT.md`。

### 5.1 G06-04 成就与称号实现说明

- `ConfigDatabase` 注册 `09§8–9` 的 10 个 `AchievementData` 与 5 个 `TitleData`；监听击杀、
  境界、任务、炼丹、奇遇 Flag，并轮询废脉炼体组合条件。解锁时先锁定完成态，再发物品、
  灵石、声望或称号奖励，发布 `OnAchievementUnlocked` 与 localization toast。
- `achievements.json` 保存每项累计值和已解锁 ID；`titles.json` 保存解锁列表与当前佩戴项。
  读档不会重复发奖。成就奖励称号会自动佩戴，之后仍可通过 `ITitleService.Equip/Unequip`
  切换。
- `TitleManager` 通过 `IPlayerTitleStatsSink` 写入 `PlayerStats.FromTitle`；铁骨的 MaxHp+5%
  作为独立百分比参与每次重算。角色面板新增当前称号行并响应 `OnTitleChanged`。

---

## 6. 集成：动画 / VFX / 音频（接口级）

实现细节可薄，但 **API 必须存在**，系统只调 ID：

```csharp
// 动画：Player/Enemy Animator 参数
// Speed, IsGrounded, AttackIndex, SkillCast, Dodge, Block, Hit, Die, Mounted, Flying
// Animation Events: OnAttackHit, OnSkillCastPoint, OnStep, OnAnimEnd

public class VFXManager : Singleton<VFXManager>
{
    public GameObject Play(string vfxId, Vector3 pos, Quaternion rot, float duration = 2f);
    public GameObject PlayAttached(string vfxId, Transform parent, float duration);
    public void Stop(GameObject instance);
}

public class AudioManager : Singleton<AudioManager>
{
    public void PlayBGM(string id, float fade = 1f);
    public void CrossfadeBGM(string id, float duration = 2f);
    public void PlaySFX(string id, float volume = 1f);
    public void PlaySFXAt(string id, Vector3 pos);
    public void SetAmbience(string id);
    public void SetVolumes(float master, float bgm, float sfx);
}
```

无正式资产时：用色块 Particle + 蜂鸣 AudioClip 占位，**ID 与 09 命名表一致**。

### 6.1 G06-05 音画与动画事件实现说明

- `VFXManager` 与 `AudioManager` 作为跨场景服务启动，严格接受 `09§12` 的稳定 ID；
  无资产时按 ID 生成短寿命色块 Particle、蜂鸣 SFX 与循环音调 BGM，不让玩法代码依赖
  prefab/clip 路径。`CombatFeedbackController` 统一监听伤害、元素反应、施法、闪避、
  治疗和突破事件。
- 玩家攻击片段可在接触帧调用 `OnAttackHit`，技能片段可调用 `OnSkillCastPoint`；两者均
  保留既有 Windup/CastTime 计时兜底，且事件路径幂等，灰盒无 Animator 时仍可完整战斗。
  `OnStep`、`OnAnimEnd` 也已作为稳定 Animation Event 入口保留。
- 技能弹道读取 `SkillData.ProjectileVfxId/ImpactVfxId`；石将军预警读取
  `BossSkillTelegraph.VfxId`；BOSS 战圈通过 `IAudioStateService` 切换专属 BGM。
- `AudioStateController` 按青石/苍梧/黑风、普通战斗、石将军三层状态切换。进战
  Crossfade 固定 1s；既有战斗标记在最后活动 5s 后清除，音乐再保留 3s 尾段，合计满足
  `09§15` 的脱战 8s。

---

## 7. Acceptance

- [ ] HUD 数值与战斗一致  
- [ ] 面板互斥与 Esc 逻辑  
- [ ] 对话/死亡/暂停层正确  
- [ ] 教程单存储：VS Toast 与 G06 不重播  
- [ ] ≥6 教程可触发完成  
- [ ] 天灵根面板 1.35×；突破 Blockers UI  
- [ ] 手柄：战斗/HUD/背包/对话可导航  
- [ ] 文案均有 localization key  
- [x] 坐骑加速；筑基飞剑可飞  
- [x] 声望升级改折扣  
- [x] 成就弹窗 + 称号加属性  
- [x] VFX/Audio 接口可被战斗调用  
