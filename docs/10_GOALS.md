# 10 · 原子 Goal 清单（范围与验收）

> **版本**: v3.1（执行流程修订：2026-07-15，进度入口拆分）  
> **恢复入口**：先读 [`10_PROGRESS.md`](10_PROGRESS.md)，本文件只保存 Goal 卡片及其状态。  
> **用法**：同一时刻只推进 **1 个** Goal。优先恢复唯一的 `in_progress`；否则领取 `status: pending` 且 `depends_on` 全为 `implemented/done` 的最小编号 Goal。  
> 代码与静态结构完成后标为 `implemented`；真实 acceptance 全部通过后才改为 `done`。  
> 规格引用格式：`文档名§节`，例如 `02_ARCHITECTURE§5`。

**状态枚举**：`pending` | `in_progress` | `implemented` | `done` | `blocked`

**代码优先规则**：Unity Editor 暂不可用时，允许把编译、PlayMode、场景体验等运行时验收后置；`implemented` 可作为后续代码 Goal 的依赖满足态，但不满足最终 MVP 的 `done` 门禁。

**连续推进规则**：Goal 是范围边界，不是强制停顿点。完成当前 Goal、记录验收并同步 [`10_PROGRESS.md`](10_PROGRESS.md) 后，长期“完成整个项目”的任务可直接进入下一 Goal；禁止同时混写两个 Goal。

当前 Goal、历史证据和验收债务不再复制到本文件，统一记录在
[`10_PROGRESS.md`](10_PROGRESS.md)。旧设计稿中的 Goal YAML 仅用于决策溯源，
不得覆盖本文件卡片的 `status`。

### 硬规则（v3.1）

1. **不重编号**已有 Goal ID。  
2. 新增预估 **>1h** → 必须新 Goal 或 **显式上调 est_hours**；禁止静默塞 acceptance。  
3. **G01-05、G04-05、G05-06 保持独立**，不得并入 G01-01 / G04-03 / G05-01。  
4. 手感数字只认 `04§4.8`；突破只认 `05`；气质见 `11`（非 Goal 入口）。

---

## 进度总览

| Phase | 主题 | 状态 | 完成定义 |
|-------|------|------|----------|
| 0 | 基础设施（6） | `done` 6/6 | Boot→菜单→测试场景；Config 默认可加载 |
| VS | 垂直切片（8） | `done` 8/8 | `01§5` 八步；Tutorial 完成态 |
| 1 | 战斗打磨（5） | `done` 5/5 | 连招 + **G01-05 手感包** |
| 2 | 修炼成长（4） | **`done` 4/4** | 练气→筑基（仪式/pity）；废被动 |
| 3 | 物品技能生活（5） | `done` 5/5 | 装备精炼+6 技能+炼丹采集 |
| 4 | 敌人与 AI（5） | `done` 5/5 | BOSS 阶段 + **G04-05 预警** |
| 5 | 世界任务（6） | `done` 6/6 | 主线金丹 + checkpoint + **G05-06 奇遇** |
| 6 | UI 元系统（5） | `done` 5/5 | 教程表现升级/成就/坐骑/音频 |
| 7 | 打磨（4） | `done` 4/4 | 内容填满+经济清单+性能+总验收 |

最终状态见 [`10_PROGRESS.md`](10_PROGRESS.md)，历史测试证据见
[`history/TEST_EVIDENCE.md`](history/TEST_EVIDENCE.md)。**G02 已完成，不是待领取阶段。**

---

## Phase 0 — 基础设施

### G00-01 工程骨架与 asmdef

```yaml
id: G00-01
phase: 0
status: done
depends_on: []
est_hours: 2
refs: [02_ARCHITECTURE§1-3]
deliverables:
  - Unity 工程（6000.5.3f1 / URP 17.5）或现有工程内 Assets/_Project 目录树
  - Wendao.Core / Data / Systems / Entities / UI / Camera 的 asmdef
  - 空场景 Boot.unity, MainMenu.unity
acceptance:
  - 目录与 02§1 一致（允许空文件夹占位）
  - 全部 asmdef 引用方向正确，工程 0 error 编译
  - Boot 场景可进入 Play Mode
out_of_scope: [任何玩法逻辑, 美术资源]
```

### G00-02 EventBus + Singleton + ServiceLocator

```yaml
id: G00-02
phase: 0
status: done
depends_on: [G00-01]
est_hours: 2
refs: [02_ARCHITECTURE§4-5]
deliverables:
  - Scripts/Core/EventBus.cs
  - Scripts/Core/Singleton.cs
  - Scripts/Core/ServiceLocator.cs
  - Editor 或 Runtime 单元测试/最小测试场景验证订阅发布
acceptance:
  - Subscribe/Publish/Unsubscribe 泛型与非泛型可用
  - Handler 抛异常不阻断其他 Handler
  - Clear() 后无残留
  - Singleton DDOL 且重复实例销毁
out_of_scope: [具体游戏事件业务]
```

### G00-03 GameManager 状态机

```yaml
id: G00-03
phase: 0
status: done
depends_on: [G00-02]
est_hours: 2
refs: [02_ARCHITECTURE§6]
deliverables:
  - GameManager.cs + GameStateInfo 发布
  - 合法转换表实现
acceptance:
  - 非法转换返回 false 且不改状态
  - OnGameStateChanged 参数正确
  - IsInCombat 可独立切换
out_of_scope: [UI 表现]
```

### G00-04 数据层：Enum + EventParams + SO 壳

```yaml
id: G00-04
phase: 0
status: done
depends_on: [G00-01]
est_hours: 3
refs: [03_DATA_LAYER§1-3]
deliverables:
  - 全部 Enum
  - EventParams.cs
  - ItemData, EquipmentData, SkillData, QuestData, EnemyData, NPCData,
    CraftRecipeData, MapData, DialogueData, AchievementData, TitleData,
    MountData, StatusEffectData, SerendipityData 类定义
  - ReachRealm / BossSkillTelegraph / SkillId 字段
acceptance:
  - 编译通过
  - CreateAssetMenu 可创建每种 SO
  - 字段与 03 文档对齐（可缺视觉引用）
out_of_scope: [填具体内容资产]
```

### G00-05 SaveManager + 配置加载

```yaml
id: G00-05
phase: 0
status: done
depends_on: [G00-02, G00-04]
est_hours: 3
refs: [02_ARCHITECTURE§7, 03_DATA_LAYER§4-5]
deliverables:
  - SaveManager 多槽 JSON
  - ConfigDatabase + RealmConfig.json 等最小文件
  - SafeBehaviour, ObjectPool
acceptance:
  - 写读 profile 样例字段往返一致
  - world.json 可写 tutorialsCompleted / dungeonCheckpoint 样例
  - 损坏文件 IsCorrupted
  - RealmConfig / SpiritRootConfig 默认可加载（无 FeatureFlags 文件）
  - ObjectPool Get/Return 正常
out_of_scope: [全模块存档字段]
```

### G00-06 SceneLoader + Loading UI 壳

```yaml
id: G00-06
phase: 0
status: done
depends_on: [G00-03, G00-04]
est_hours: 2
refs: [02_ARCHITECTURE§9]
deliverables:
  - SceneLoader 异步加载
  - Loading 场景进度条（可用 UI Image）
  - OnMapLoaded 发布
acceptance:
  - MainMenu 按钮可加载空 Map_Qingshi 灰盒
  - 进度回调单调递增至 1
out_of_scope: [地图内容]
```

---

## Phase VS — 垂直切片（优先于横向功能）

> 目标：跑通 `01_VISION_MVP§5`。允许占位美术。

### G-VS-01 玩家移动 + 输入 + 摄像机基础

```yaml
id: G-VS-01
phase: VS
status: done
depends_on: [G00-06, G00-05]
est_hours: 4
refs: [04_PLAYER_COMBAT§1-3, 08_UI_META§2]
deliverables:
  - InputActions
  - PlayerController（Idle/Move/Jump/Sprint）
  - ThirdPersonCamera 跟随/旋转/基础遮挡
  - 灰盒地面 + Player Prefab
  - TutorialManager 壳 + tut_move（Toast 表现）+ world.json 持久
acceptance:
  - WASD 移动、鼠标视角、跳跃
  - 镜头不穿地（简单 collision）
  - 手柄左摇杆可移动（若有设备）
  - tut_move 可完成并 HasCompleted 读档仍在
out_of_scope: [战斗, 闪避, 遮罩挖洞表现]
```

### G-VS-02 战斗最小闭环

```yaml
id: G-VS-02
phase: VS
status: done
depends_on: [G-VS-01, G00-04, G00-05]
est_hours: 4
refs: [04_PLAYER_COMBAT§4, §4.8]
deliverables:
  - IDamageable, PlayerStats 最小 HP
  - CombatSystem 近战判定
  - 轻攻击至少 1 段 + **伤害飘字**（必做）
  - 可选 hitstop 30ms 占位（同一 Feel 参数入口）
  - 木桩敌人（静态 IDamageable）
  - tut_combat Toast + 完成态
acceptance:
  - 攻击可击杀木桩
  - 伤害字可见
  - OnPlayerDamaged / 敌人死亡有事件
  - HP 减到 0 进 Dead 状态（可先简单）
  - tut_combat 可完成并持久化
out_of_scope: [四连击, 元素, 技能, G01-05 全量 buffer]
```

### G-VS-03 背包 + 回血丹 + 装备武器

```yaml
id: G-VS-03
phase: VS
status: done
depends_on: [G-VS-02, G00-05]
est_hours: 3
refs: [06_ITEMS_EQUIP_SKILL§1-2]
deliverables:
  - InventoryManager 50 格
  - ItemUseSystem 治疗
  - EquipmentManager 武器槽
  - 最小背包 UI
acceptance:
  - 获得 item_potion_heal_01 并使用回血
  - 装备 eq_weapon_wood_sword 后 Atk 变化体现在伤害
  - 存档读写 inventory/equipment
out_of_scope: [精炼, 商店]
```

### G-VS-04 一个主动技能

```yaml
id: G-VS-04
phase: VS
status: done
depends_on: [G-VS-02]
est_hours: 3
refs: [06_ITEMS_EQUIP_SKILL§3]
deliverables:
  - SkillManager + skill_fire_ember 或 skill_basic_qi_bolt
  - 快捷栏 1 释放、CD、扣蓝
  - 最小技能面板或默认已装备
acceptance:
  - 按 1 释放造成伤害
  - 蓝不足失败提示
  - CD 期间无法连放
out_of_scope: [多技能, 升级]
```

### G-VS-05 修为与层内升级

```yaml
id: G-VS-05
phase: VS
status: done
depends_on: [G-VS-02, G00-05]
est_hours: 3
refs: [05_CULTIVATION§2-3]
deliverables:
  - SpiritRoot 选择（创建界面可简）
  - CultivationManager AddXp / 子阶升级
  - HUD 经验条
acceptance:
  - 杀木桩得修为
  - 满 XP 升练气层，属性变
  - Fire：cultivationMul=1.1 可测；文案 key root_intro_five
  - Waste（随机种子）：mul=0.55 可测；root_intro_waste
  - Heaven（随机）：mul=1.35；创角文案非裸数字（面板数字可后做）
out_of_scope: [突破大境界, 废格挡被动完整测（G02-02）]
```

### G-VS-06 一个 NPC 对话 + 一个任务

```yaml
id: G-VS-06
phase: VS
status: done
depends_on: [G-VS-03, G00-03]
est_hours: 3
refs: [07_WORLD_ENEMY_QUEST§4-5]
deliverables:
  - DialogueManager + 1 段对话
  - QuestManager Accept/Kill/TurnIn
  - quest_main_01_02 或简化「杀狼 3 只」
acceptance:
  - E 对话 → 接任务 → 杀敌计数 → 交付发奖
  - 任务追踪 HUD 更新
out_of_scope: [完整主线]
```

### G-VS-07 刷怪与掉落

```yaml
id: G-VS-07
phase: VS
status: done
depends_on: [G-VS-02, G-VS-03]
est_hours: 3
refs: [07_WORLD_ENEMY_QUEST§3]
deliverables:
  - enemy_wolf_gray + Spawner
  - 简单 Chase/Attack AI
  - LootSystem 掉狼毫
acceptance:
  - 野外狼会追击攻击
  - 击杀掉落进背包或地上拾取
  - 脱战回出生点
out_of_scope: [BOSS, 精英技能]
```

### G-VS-08 切片验收与 DebugConsole

```yaml
id: G-VS-08
phase: VS
status: done
depends_on: [G-VS-01, G-VS-02, G-VS-03, G-VS-04, G-VS-05, G-VS-06, G-VS-07]
est_hours: 2
refs: [01_VISION_MVP§5, 02_ARCHITECTURE§11, 08_UI_META§2]
deliverables:
  - DebugConsole 常用命令（含 /tutorial_skip DEV）
  - 切片检查清单勾选
acceptance:
  - 01§5 八步可走通（过慢允许 /givexp）
  - HasCompleted(tut_move) 与 tut_combat（或 /tutorial_skip 写入同 keys）
  - /give /spawn /save 可用
  - 读档后任务/背包/教程完成态不丢
out_of_scope: [内容扩展, G01-05 全量手感]
```

---

## Phase 1 — 战斗打磨

### G01-01 四连击·重击·闪避·格挡

```yaml
id: G01-01
phase: 1
status: done
depends_on: [G-VS-08]
est_hours: 4
refs: [04_PLAYER_COMBAT§2, §4.4]
deliverables:
  - 四段轻击、重击、闪避、格挡状态与统一输入消费
  - CombatSystem 对应伤害、防御、取消窗口与动画占位接口
  - G01-01 PlayMode 契约测试和静态验证入口
acceptance:
  - 连段倍率 1.0/1.1/1.25/1.5
  - 闪避无敌 0.2s 可测
  - 格挡减伤符合表（基线 0.60 物理）
  - 后摇可闪避取消
out_of_scope: [Input Buffer 全量, Hitstop 全量 — 归 G01-05]
```

### G01-02 锁定与摄像机模式

```yaml
id: G01-02
phase: 1
status: done
depends_on: [G01-01]
est_hours: 3
refs: [04_PLAYER_COMBAT§3]
deliverables:
  - PlayerTargetingController 锁定、切换与失效清理
  - ThirdPersonCamera 战斗/对话模式、碰撞与震屏接口
  - 键鼠与手柄 LockOn 输入接入
acceptance:
  - 锁定/切换/清锁定
  - 战斗 FOV、震屏、对话镜头
out_of_scope: [G01-05 输入缓冲与 hitstop, 正式 Cinemachine 镜头资源]
```

### G01-03 元素反应 + 状态

```yaml
id: G01-03
phase: 1
status: done
depends_on: [G01-01, G-VS-04]
est_hours: 4
refs: [04_PLAYER_COMBAT§4.5-4.6, 03_DATA_LAYER§4.5]
deliverables:
  - StatusEffect 叠层、持续时间与移除管道
  - Melt、Shock、BurnBurst 三种元素反应及事件
  - 反应伤害接入 CombatSystem 与测试
acceptance:
  - Melt/Shock/BurnBurst 至少 3 种可验证
  - StatusEffect 叠层与到期移除
out_of_scope: [正式元素 VFX/SFX（G06-05）, 新增规格外反应]
```

### G01-04 死亡复活与脱战回血

```yaml
id: G01-04
phase: 1
status: done
depends_on: [G01-01]
est_hours: 4
refs: [04_PLAYER_COMBAT§4.7, 01_VISION_MVP§6, 02_ARCHITECTURE§5.2/§6, 08_UI_META§1.4]
deliverables:
  - 玩家死亡/最近复活点服务与 OnPlayerRespawned
  - 当前层修为 5% 死亡惩罚与持久化
  - CombatFlag 最近伤害计时与脱战 HP/Mana 回复
  - panel_death 代码优先 UI
acceptance:
  - 死亡 UI → 复活点
  - XP 惩罚 5%
  - 脱战 5s 后回血
out_of_scope: [读档分支, 炼体复活次数, G01-05 战斗手感包, BGM 的 8s 脱战切换]
```

---

### G01-05 战斗手感包（Feel Package）

```yaml
id: G01-05
phase: 1
status: done
depends_on: [G01-01, G01-02]
est_hours: 3
refs: [04_PLAYER_COMBAT§4.8]
deliverables:
  - CombatFeelSettings（常量或 SO）
  - Input buffer 120ms
  - Hitstop 应用于 L1–L4/重击
  - Crit 与元素反应 Camera/VFX 挂钩
acceptance:
  - 缓冲期内预输入可出招
  - Editor 时间缩放下可观察 L3/L4 hitstop
  - 暴击必调用既有 Shake 参数
  - 元素反应有 FOV 回弹或彩色字
out_of_scope: [完美闪避, 韧性条, FeatureFlags 配置文件]
```

---

## Phase 2 — 修炼

### G02-01 突破流程

```yaml
id: G02-01
phase: 2
status: done
depends_on: [G-VS-05, G01-04]
est_hours: 4
refs: [05_CULTIVATION§3]
deliverables:
  - BreakthroughBlocker + ICultivationService 突破查询 API
  - Idle/BreakingThrough/BreakthroughResult 状态机与 CeremonyBeat 1–5
  - 材料、失败心魔、筑基 pity 消费/中断恢复与境界事件
  - 输入锁、3s 无敌、突破中传送排队与代码优先占位演出
  - G02-01 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - 练气 9 层后可突破筑基（需筑基丹）
  - 成功才耗材料；失败不耗
  - 五拍演出 + CeremonyBeat；3s 无敌
  - GetBreakthroughBlockers 驱动 Toast
  - pity 边界：08 Flag、消费、中断恢复
  - 金丹可破；元婴 CanBreakthrough=false
out_of_scope: [突破辅助丹加成]
```

### G02-02 炼体

```yaml
id: G02-02
phase: 2
status: done
depends_on: [G02-01]
est_hours: 2
refs: [05_CULTIVATION§2, §4]
deliverables:
  - IBodyRefinementService + BodyRefinementManager
  - 受伤与锻体丹炼体 XP 管道及灵根倍率
  - 炼体等级 HP/物理减伤接入 PlayerStats/CombatSystem
  - 废灵根 0.70 物理格挡与锻体丹 1.25 倍
  - G02-02 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - 受伤与丹药加炼体 XP
  - 升级加 HP/DR
  - 废：body XP ≥ 对照×1.5；锻体丹×1.25
  - 格挡 BlockPhysDR_final==0.70（对照 0.60）
  - 木桩格挡实伤约为对照 0.75 倍
out_of_scope: [炼体存档字段与角色面板（G02-04）]
```

### G02-03 PlayerStats 完整聚合

```yaml
id: G02-03
phase: 2
status: done
depends_on: [G02-01, G-VS-03]
est_hours: 2
refs: [05_CULTIVATION§1]
deliverables:
  - BaseFromRealm / FromEquipment / FromTitle / FromBuffs / Final 属性缓存
  - 显式 Recalculate 与称号、固定 Buff 注入入口
  - 灵根元素、炼体 HP 百分比与平坦 Buff 的权威合成顺序
  - ICombatStatsProvider 攻防暴击全部读取 Final
  - G02-03 PlayMode 契约测试与静态验证脚本
acceptance:
  - 境界+装备+称号（可先 mock）正确加算
  - Recalculate 后战斗用 Final
out_of_scope: [完整称号解锁与装备流程（G06-04）, 角色面板与全字段存档（G02-04）]
```

### G02-04 角色面板 UI + 存档全字段 profile

```yaml
id: G02-04
phase: 2
status: done
depends_on: [G02-02, G02-03]
est_hours: 2
refs: [08_UI_META§1.4]
deliverables:
  - panel_character 代码优先 UI、C 键统一 Input Action 与玩法输入锁
  - 境界/修为、灵根、炼体、Final 战斗属性只读展示
  - 突破状态/成功率与 TryBreakthrough 按钮接入
  - profile.json bodyLevel/bodyXp 向后兼容字段与校验
  - G02-04 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - C 打开面板显示境界炼体灵根
  - 面板显示 Final 气血/灵力/攻防/暴击等属性，关闭后输入恢复
  - 突破按钮可用；开始仪式前正确归还面板输入锁
  - bodyLevel/bodyXp 存档恢复一致，切槽不残留影子状态
out_of_scope: [正式美术资源与面板互斥收尾（G06-01）, 属性对比演出（G06-01）]
```

---

## Phase 3 — 物品·技能·生活

### G03-01 精炼系统

```yaml
id: G03-01
phase: 3
status: done
depends_on: [G-VS-03]
est_hours: 2
refs: [06_ITEMS_EQUIP_SKILL§2.2]
deliverables:
  - IRefineService + RefineSystem，随场景流启动并注册 ServiceLocator
  - 成功率、材料消耗与每级属性倍率集中到 FormulaLibrary
  - 精炼石运行时内容、稳定 ID 与玩家可见 localization key
  - 合法尝试消耗、OnEquipmentUpgraded 事件、Final 属性重算与即时持久化
  - G03-01 PlayMode 契约测试与静态验证脚本
acceptance:
  - 成功率为 max(0.4, 0.95 - 0.03 × currentLevel)
  - 材料消耗为 1 + floor(currentLevel / 2)，成功后属性相对 Base +5%/级
  - 失败消耗材料但不掉级、不毁装；未穿戴、材料不足、满级不消耗
  - 成功等级与失败后的材料余量均立即保存，读档恢复一致
out_of_scope: [正式精炼面板与美术资源, 套装效果与镶嵌 UI]
```

### G03-02 技能扩展到 6 + 升级

```yaml
id: G03-02
phase: 3
status: done
depends_on: [G-VS-04, G01-03]
est_hours: 3
refs: [09_CONTENT§4, 06_ITEMS_EQUIP_SKILL§3]
deliverables:
  - 09§4 七门功法运行时内容与稳定 SkillContentIds（其中六门可释放）
  - Skill1–4 键鼠/手柄读取、统一动作缓冲与任意槽释放
  - 四槽 SkillQuickbarView + K 键 panel_skill + 真实 drag/drop 装备
  - item_skill_scroll 内容、按当前等级消耗、OnSkillUpgraded 与即时存档
  - 技能释放登记战斗活动，阻止施法途中触发脱战回蓝
  - G03-02 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - 七门内容功法均可学习；六门 Active/Ultimate 可释放，Passive 不可入栏
  - 已学功法可拖到四个快捷栏槽，重复装备会从旧槽移除并持久化
  - 键盘 1–4 与手柄方向键均绑定，槽 4 输入实际释放槽 4 功法
  - 升级消耗 item_skill_scroll × 当前等级，伤害按每级增量生效并持久化
  - 材料不足或满级不消耗；K 面板开关正确锁定并恢复玩法输入
out_of_scope: [技能解锁任务与商店来源, 正式技能动画/VFX/SFX（G06-05）]
```

### G03-03 炼丹

```yaml
id: G03-03
phase: 3
status: done
depends_on: [G-VS-03]
est_hours: 3
refs: [06_ITEMS_EQUIP_SKILL§4.1, 09_CONTENT§7]
deliverables:
  - recipe_heal_01 / mana_01 / body_01 / xp_01 四张稳定丹方与完整材料、产物占位内容
  - AlchemySystem 等级门禁、材料事务、等级 successBonus、成功熟练度与失败主材退款
  - OnCraftCompleted / OnCraftFailed 事件与成功/失败 localization toast
  - alchemy.json 累计熟练度模块；每次有效尝试同步即时保存 inventory.json
  - 可由丹炉交互打开的四丹方 AlchemyPanelView，展示材料、等级和实际成功率
  - 回气丹/聚气丹 RestoreMana / AddCultivationXp 使用效果
  - G03-03 静态验证与 EditMode/PlayMode 契约测试
acceptance:
  - 四张 09§7 丹方均可查询，等级和材料不足时不消耗、不发布结果事件
  - 成功扣材料、给产物和熟练度并发布 OnCraftCompleted；失败发布 OnCraftFailed
  - 失败只消耗灵尘，主材静默退回且不伪造 OnItemAcquired
  - 累计 XP 200 升至 2 级；回血丹成功率由 0.90 提升至 0.93 并可读档恢复
  - panel_alchemy 列出四丹方、可完成真实炼制，并正确锁定/恢复玩法输入
out_of_scope: [丹炉场景实体与正式摆放, 采集点和材料来源（G03-04）, 正式 UI 美术资源]
```

### G03-04 采集

```yaml
id: G03-04
phase: 3
status: done
depends_on: [G-VS-03]
est_hours: 2
refs: [06_ITEMS_EQUIP_SKILL§4.2, 07_WORLD_ENEMY_QUEST§1.2]
deliverables:
  - IGatheringService + GatheringSystem，随场景流启动并注册 ServiceLocator
  - GatherableObject 稳定节点配置、距离交互、读条占用与可视刷新状态
  - 青石镇灵草涧 6 个灰盒采集点：4 个清心草、2 个灵尘
  - 1.5 秒读条、玩法输入锁、OnPlayerDamaged 受伤打断与本地化提示
  - 采集成功通过 AcquireSource.Gather 入包并即时保存 inventory.json
  - G03-04 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - 青石镇灵草涧存在 6 个稳定 ID 采集点，4 个产清心草、2 个产灵尘
  - 有效交互进入 1.5 秒读条；完成后按节点数量范围发放材料并持久化
  - 读条中玩家受到正伤害会立即打断，不发材料且不进入刷新冷却
  - 采尽节点进入配置冷却，时间耗尽后恢复交互；刷新边界使用确定性容差归零
out_of_scope: [正式场景美术与手工摆放, 采集等级成长, 工具消耗与其他地图采集点]
```

### G03-05 商店

```yaml
id: G03-05
phase: 3
status: done
depends_on: [G-VS-03, G-VS-06]
est_hours: 2
refs: [06_ITEMS_EQUIP_SKILL§5, 09_CONTENT§13]
deliverables:
  - IShopService + ShopSystem，随场景流启动并注册 ServiceLocator
  - ItemData BuyPrice、ShopOpenInfo/ShopTransactionInfo 与商店事件契约
  - npc_zhanggui / dlg_shop_zhanggui / 铁剑运行时内容与青石镇灰盒 NPC
  - 张掌柜固定货单：回血丹 10、精炼石 25、铁剑 80 灵石
  - panel_shop 购买/背包出售 UI、NPC 交易交互、玩法输入锁与本地化提示
  - 原子买卖校验、AcquireSource.Shop、成功后 inventory/profile 即时保存
  - G03-05 EditMode/PlayMode 契约测试与静态验证脚本
acceptance:
  - 与张掌柜交互可打开商店，三项货单价格与 09§13 一致并可实际购买
  - 买入成功按总价扣灵石并入包；出售按 SellPrice 发放灵石并移除指定数量
  - 灵石不足、背包满、绑定/不可售物品、非法数量与溢出均不产生部分写入
  - 成功交易发布 OnShopTransactionCompleted；买入仍发布 OnItemAcquired(Shop)
  - 交易结果即时写 inventory.json 与 profile.json，关闭面板后恢复玩法输入
out_of_scope: [声望折扣（G06-04）, 正式商店美术资源, 动态库存与限购刷新]
```

---

## Phase 4 — 敌人

### G04-01 普通 AI 完善 + NavMesh

```yaml
id: G04-01
phase: 4
status: done
depends_on: [G-VS-07]
est_hours: 3
refs: [07_WORLD_ENEMY_QUEST§3.1]
deliverables:
  - EnemyBrain Patrol/Alert/Chase/Attack/Return 状态闭环
  - NavMesh 或等效地面导航、出生点回归与多刷怪点
  - G04-01 PlayMode 契约测试和静态验证入口
acceptance:
  - Patrol/Alert/Chase/Return
  - 不穿地形
  - 多刷怪点
out_of_scope: [精英技能（G04-02）, BOSS 阶段逻辑（G04-03）]
```

### G04-02 精英灰爪

```yaml
id: G04-02
phase: 4
status: done
depends_on: [G04-01]
est_hours: 2
refs: [09_CONTENT§5]
deliverables:
  - enemy_wolf_gray_elite 稳定内容与精英属性倍率
  - 灰爪冲锋的预备、位移、命中与冷却行为
  - G04-02 PlayMode 契约测试和静态验证入口
acceptance:
  - 血攻倍率
  - 冲锋技能
out_of_scope: [BOSS 多阶段逻辑, 正式美术与动画]
```

### G04-03 BOSS 黑风石将军

```yaml
id: G04-03
phase: 4
status: done
depends_on: [G04-01, G01-02]
est_hours: 5
refs: [07_WORLD_ENEMY_QUEST§3.3]
deliverables:
  - enemy_boss_stone_general 三阶段状态机与技能组
  - 阶段事件、BOSS 血条、战斗区域与出圈重置
  - G04-03 PlayMode 契约测试和静态验证入口
acceptance:
  - 3 阶段技能差异
  - 阶段事件与血条
  - 出圈重置
out_of_scope: [完整 Telegraph 调参 — 归 G04-05]
```

### G04-04 掉落表与经济

```yaml
id: G04-04
phase: 4
status: done
depends_on: [G04-01, G-VS-03]
est_hours: 2
refs: [09_CONTENT§5, §13]
deliverables:
  - LootTable/LootEntry 概率结算与确定性测试入口
  - 普通、精英、BOSS 掉落表及灵石奖励
  - 背包满时沿用地面掉落回退路径
acceptance:
  - 按 LootEntry 概率掉落
  - 灵石掉落
out_of_scope: [动态掉率活动, 联机掉落归属, G07-02 最终经济平衡]
```

### G04-05 BOSS 预警与 SkillId

```yaml
id: G04-05
phase: 4
status: done
depends_on: [G04-03, G01-02]
est_hours: 3
refs: [03_DATA_LAYER§BossSkillTelegraph, 07_WORLD_ENEMY_QUEST§3.3, 09_CONTENT]
deliverables:
  - BossSkillTelegraph 数据与每阶段预警占位表现
  - RecoverStun 可输出窗口与阶段技能 SkillId 贯通
  - G04-05 PlayMode 契约测试和静态验证入口
acceptance:
  - 每阶段至少 1 个 Duration≥0.6s 预警 VFX
  - RecoverStun 窗口内可稳定输出（日志/木桩）
  - DamageInfo.SkillId 在 boss 技能命中非空
out_of_scope: [第二只 BOSS, 练习模式]
```

---

## Phase 5 — 世界与任务

### G05-01 青石原内容摆放

```yaml
id: G05-01
phase: 5
status: done
depends_on: [G04-01, G03-04]
est_hours: 4
refs: [07_WORLD_ENEMY_QUEST§1.2, 09_CONTENT§11]
deliverables:
  - Map_Qingshi 镇、坪、荒野、灵草涧、秘径五区灰盒
  - 安全区、刷怪点、采集点、NPC 与传送交互摆放
  - G05-01 PlayMode 契约测试和静态验证入口
acceptance:
  - 镇/坪/荒野/灵草涧/秘径 可辨
  - 安全区生效
out_of_scope: [正式地形美术, 奇遇内容（G05-06）, 昼夜天气（G07-01）]
```

### G05-02 苍梧山脉 + 传送解锁

```yaml
id: G05-02
phase: 5
status: done
depends_on: [G05-01, G00-06]
est_hours: 4
refs: [07_WORLD_ENEMY_QUEST§1]
deliverables:
  - Map_Cangwu 苍梧山脉灰盒、落点与返回路径
  - TravelPoint 解锁/持久化与青石秘径任务门禁
  - G05-02 PlayMode 契约测试和静态验证入口
acceptance:
  - 地图加载
  - 传送点踩点解锁
  - 主线开门后可进
out_of_scope: [苍梧奇遇（G05-06）, 正式环境美术, 黑风秘境内部]
```

### G05-03 黑风秘境 5 层

```yaml
id: G05-03
phase: 5
status: done
depends_on: [G05-02, G04-03]
est_hours: 6
refs: [07_WORLD_ENEMY_QUEST§1.2, 02_ARCHITECTURE§7.1.1]
deliverables:
  - Map_Blackwind B1—B5 灰盒流程、机关、精英、泉与石将军
  - 金丹入口门禁、B2 checkpoint 与 B3 恢复落点
  - 本局一次泉水、失败不回退 checkpoint 的运行时状态
  - G05-03 PlayMode 契约测试和静态验证入口
acceptance:
  - 5 层流程可通关（机关/精英/泉/BOSS）
  - 金丹门禁：未金丹不可进
  - checkpoint：通关 B2 后 world==2；再进出生 B3
  - B5 失败 checkpoint 不回退；泉水本局一次，退出后再进可再喝
out_of_scope: [SerendipitySystem / 奇遇内容 — 归 G05-06]
```

### G05-04 主线 10 环 + 对话

```yaml
id: G05-04
phase: 5
status: done
depends_on: [G-VS-06, G05-03, G02-01]
est_hours: 5
refs: [09_CONTENT§1, 07_WORLD_ENEMY_QUEST§4]
deliverables:
  - quest_main_01_01—10 的 QuestData、目标、前置、奖励与稳定 localization key
  - 01—10 起始/完成对话与 NPC 主线交互路由
  - Craft/UseItem/Reach/ReachRealm 等主线目标事件接入和有序目标推进
  - 08 筑基丹条件奖励、foundation pity Flag 与 09 凝金丹 Collect latch
  - 黑风入口 Reach 联动、任务存档兼容与 G05-04 契约测试/验证脚本
acceptance:
  - quest_main_01_01～10 可完成
  - 08：AcceptRewards 筑基丹 + pity；前置锁定正确
  - 09：AcceptRewards 凝金丹 + Collect latch + ReachRealm GoldenCore + 入口
  - 突破耗丹后 Collect 进度不回退
out_of_scope: [支线与日常（G05-05）, 奇遇（G05-06）, 正式对话演出与配音（G06-05）]
```

### G05-05 支线 3 + 日常框架

```yaml
id: G05-05
phase: 5
status: done
depends_on: [G05-04]
est_hours: 3
refs: [09_CONTENT§6]
deliverables:
  - 09§6 三条支线的 QuestData、对话、奖励与 NPC 路由
  - DailyQuestManager 重置框架与 hunt/gather 两类模板
  - 日常存档、事件与不阻断主线的失败/刷新规则
acceptance:
  - ≥3 支线
  - 日常框架 API + hunt/gather 2 条可重置（不卡主线）
out_of_scope: [大量日常内容]
```

### G05-06 奇遇系统（全图）

```yaml
id: G05-06
phase: 5
status: done
depends_on: [G05-01, G05-02, G05-03]
est_hours: 3
refs: [07_WORLD_ENEMY_QUEST§6, 09_CONTENT§14, 03_DATA_LAYER§SerendipityData]
deliverables:
  - SerendipitySystem
  - 资产：青石≥1、苍梧≥2、黑风≥1（含 Trigger）
  - world.json serendipityFlags 读写
  - OnSerendipityTriggered
acceptance:
  - 三图 once 触发并写 Flag；读档不重复
  - 奖励无装备
  - 黑风条目在 map_blackwind 可触发
out_of_scope: [程序化奇遇]
```

---

## Phase 6 — UI 与元系统

### G06-01 HUD/面板收尾

```yaml
id: G06-01
phase: 6
status: done
depends_on: [G02-04, G03-02, G05-04]
est_hours: 4
refs: [08_UI_META§1]
deliverables:
  - HUD、背包、角色、技能、任务、炼丹、商店等 MVP 面板统一导航
  - Esc 返回/关闭优先级、面板互斥与输入锁所有权
  - 事件驱动刷新和代码优先视觉一致性收尾
acceptance:
  - 全部 MVP 面板可用
  - Esc 逻辑、互斥
  - 事件驱动刷新
out_of_scope: [正式 UI 美术重制, 教程遮罩（G06-02）, 音频 VFX（G06-05）]
```

### G06-02 教程表现升级

```yaml
id: G06-02
phase: 6
status: done
depends_on: [G06-01]
est_hours: 3
refs: [08_UI_META§2]
deliverables:
  - 复用 TutorialManager 的 ≥6 个教程步骤与触发器
  - 遮罩挖洞、指引文案、强制/非强制输入规则
  - 旧档完成态兼容和教程回归测试
acceptance:
  - ≥6 教程 ID 可触发（遮罩挖洞表现）
  - 若 tut_move/tut_combat 已完成则 **不重播**
  - 强制/非强制规则正确
  - 完成态仍写同一 world.json keys
out_of_scope: [重做 TutorialManager 存储]
```

### G06-03 坐骑与飞行

```yaml
id: G06-03
phase: 6
status: done
depends_on: [G-VS-01, G02-01]
est_hours: 3
refs: [08_UI_META§3]
deliverables:
  - IMountService、地面坐骑上/下马与移动倍率
  - 筑基飞剑解锁、起降、飞行移动和地图禁飞区
  - mounts.json 或既有 profile/world 约定下的持久化接入
acceptance:
  - 马加速
  - 筑基+飞剑可飞，地图限制
out_of_scope: [坐骑养成, 空战, 正式坐骑模型与动画]
```

### G06-04 宗门声望 + 成就称号

```yaml
id: G06-04
phase: 6
status: done
depends_on: [G05-05, G03-05]
est_hours: 3
refs: [08_UI_META§4-5, 09_CONTENT§8-9]
deliverables:
  - 宗门声望累计、等级与商店折扣接入
  - ≥10 个 AchievementData、解锁事件和存档
  - TitleData 解锁/装备、PlayerStats 属性聚合与面板
acceptance:
  - 声望等级折扣
  - ≥10 成就
  - 称号加属性
out_of_scope: [宗门经营玩法, 联机排行, 规格外成就批量填充]
```

### G06-05 音频 VFX 动画事件挂钩

```yaml
id: G06-05
phase: 6
status: done
depends_on: [G01-01, G03-02]
est_hours: 3
refs: [08_UI_META§6, 09_CONTENT§15]
deliverables:
  - 攻击动画事件与伤害帧接入
  - MVP 技能/反应/BOSS 的 VFX、SFX 占位资源与稳定 ID
  - 探索/战斗/BOSS BGM 状态机及 Crossfade
acceptance:
  - 攻击帧出伤
  - 技能有 VFX/SFX 占位
  - BGM 探索/战斗/BOSS 切换；进战 Crossfade **≤1s**
  - ID 与 09§15 表一致
out_of_scope: [正式配音, 全量动作捕捉, 商业级音频母带]
```

---

## Phase 7 — 打磨

### G07-01 昼夜天气

```yaml
id: G07-01
phase: 7
status: done
depends_on: [G05-01]
est_hours: 2
refs: [07_WORLD_ENEMY_QUEST§2]
deliverables:
  - DayNightSystem 光照时间循环与 world 时间持久化
  - WeatherSystem 晴/雨/雾切换、事件和元素修正
  - 三张地图的兼容占位配置
acceptance:
  - 光照循环
  - 晴雨雾切换 + 元素加成
out_of_scope: [季节系统, 动态积雪, 写实云层渲染]
```

### G07-02 内容表填满与平衡

```yaml
id: G07-02
phase: 7
status: done
depends_on: [G05-05, G05-06, G04-04]
est_hours: 4
refs: [09_CONTENT§13-17]
deliverables:
  - 09§13—17 MVP 内容资产、稳定 ID 与 localization 覆盖检查
  - 主线 2—4 小时可复现节拍预算与经济检查记录（真人墙钟走查归 G07-04）
  - 缺失内容/失衡项的可复现修正与回归入口
acceptance:
  - 09 中 MVP 表均有资产（含奇遇/must-ship 文案）
  - 主线节拍预算 2–4h 可通，且境界/经济门槛由运行时数据回归
  - 经济检查清单 §13 执行并记录
out_of_scope: [MVP 外地图与境界, 长线运营经济, 正式全量美术替换]
```

### G07-03 边界条件与性能

```yaml
id: G07-03
phase: 7
status: done
depends_on: [G06-05, G07-01]
est_hours: 4
refs: [01_VISION_MVP§7, 旧边界清单并入下节]
deliverables:
  - 通用边界条件清单的自动化/人工回归
  - 1080p 中画质性能采样与主要热点修正
  - 60 分钟确定性加速稳定性巡检、错误日志和缓存清理（真人墙钟走查归 G07-04）
acceptance:
  - 背包满/蓝不足/重复接任务等无崩溃
  - 1080p 中画质切片场景 ≥30fps
  - 3,600 模拟秒巡检无 Error/Exception，最终真人墙钟走查入口已保留
out_of_scope: [高端平台专项优化, 主机认证, 联机压力测试]
```

### G07-04 MVP 总验收

```yaml
id: G07-04
phase: 7
status: done
depends_on: [G07-02, G07-03, G06-04]
est_hours: 2
refs: [01_VISION_MVP§8]
deliverables:
  - 01§8 逐项签核记录与所有 Goal 最终状态核对
  - 新档完整主线、存读档、死亡恢复与三图通关走查
  - 最终构建、已知问题清单和缓存/产物清理
acceptance:
  - 01§8 全部勾选
  - 本文件 Phase0–7 关键路径 Goal 全 done
out_of_scope: [MVP 后续内容, 联机版本, 平台商店发布]
```

---

## 跨 Goal 通用验收（每次提交）

- [ ] 无编译错误  
- [ ] 不引入对未完成系统的硬依赖  
- [ ] 新增公开事件已写入 `02_ARCHITECTURE§5.2`  
- [ ] 新增 ID 已写入 `09_CONTENT`  
- [ ] 存档字段变更有版本注释或迁移说明  

---

## 边界条件速查（G07-03 用）

| 系统 | 条件 | 期望 |
|------|------|------|
| Inventory | 满 | 掉地 180s |
| Skill | 无蓝/CD | UI 提示，不进状态 |
| Equipment | 境界不足 | 不可穿 |
| Cultivation | 突破中攻击 | 无敌 |
| Quest | 重复接 | false |
| Dialogue | 切场景 | 强制结束 |
| Enemy | 目标死 | Return |
| Save | 盘满/损坏 | Toast/标记，不崩 |

---

## Goal 模式提示词模板（给 AI）

```
你正在连续开发《问道长生》。
1. 打开 docs/10_PROGRESS.md，核对它指向唯一的 in_progress Goal
2. 运行 ./tools/goal_status.sh --current，只读取该 Goal 卡片与 refs 列出的规格章节
3. 只交付 deliverables，遵守 out_of_scope
4. 代码与静态结构完成后将卡片 status 改为 implemented
5. 仅当 Unity acceptance 全部通过后才改为 done
6. 同步 docs/10_PROGRESS.md；当前 Goal 关账后自动进入下一个
同一时刻不要混写多个 Goal，也不要重做历史 done。
规格小歧义按现有架构作最小决定并同步文档，不得因此停摆。
```

如需限定范围，用户应明确写“只实现 Goal {ID}，完成后停下”。

---

## Checklist：垂直切片（G-VS-08）

> 代码优先核对（2026-07-15）：下列 `[x]` 仅表示实现路径与自动化回归已就绪，**不等于 Unity 真人走查通过**；运行时签核见 `GVS08_VERTICAL_SLICE_CHECKLIST.md`。

- [x] 新游戏选灵根进青石（代码路径/既有回归）  
- [x] 移动/战斗教程完成态与 `/tutorial_skip` 同 keys（联合回归）  
- [x] 击杀 ≥5 敌 + 伤害字（代码路径/既有回归）  
- [x] NPC 任务（代码路径/既有回归）  
- [x] 丹药与武器（代码路径/既有回归）  
- [x] 释放功法（代码路径/既有回归）  
- [x] 修为升级及 `/givexp`（代码路径/命令回归）  
- [x] 存读档含任务/背包/教程完成态（联合回归）  


---

## v3.1 Goal 门禁备忘

- 实现提交只挂 Goal ID，不另开 feat 编号 backlog。  
- **G05-06** 必须在 G05-01/02/03 之后领取（奇遇系统唯一所有者）。  
- **G01-05 / G04-05** 不得并入父 Goal。  
