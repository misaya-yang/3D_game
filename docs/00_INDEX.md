# 《问道长生》规格总索引（AI Goal 模式专用）

> **版本**: v3.1  
> **日期**: 2026-07-15  
> **引擎**: Unity 6000.5.3f1 · URP 17.5 · C# 9+  
> **受众**: 人类设计师 + AI 开发代理（Codex / Claude / Grok Goal 模式）  
> **设计基线**: [`design/FANTASY_GAMEPLAY_OPTIMIZATION.md`](design/FANTASY_GAMEPLAY_OPTIMIZATION.md)（Approved Rev.2.3）

---

## 0. 如何用本规格开发（必读）

### 0.1 单入口原则

| 你要做的事 | 读这些文件（按序） |
|-----------|-------------------|
| 第一次接手或规格版本变化 | 本文 → [`01_VISION_MVP.md`](01_VISION_MVP.md) → [`11_FANTASY_FEEL.md`](11_FANTASY_FEEL.md)（气质）→ [`10_PROGRESS.md`](10_PROGRESS.md) → `./tools/goal_status.sh --current` |
| 恢复连续开发 | [`10_PROGRESS.md`](10_PROGRESS.md) 当前指针 → `./tools/goal_status.sh --current` → 该卡 `refs` |
| 实现某个系统 | [`10_GOALS.md`](10_GOALS.md) 找到目标 → 打开该 Goal 的 `refs` **系统文档**（不要只靠 11） |
| 查数据结构 | [`03_DATA_LAYER.md`](03_DATA_LAYER.md) |
| 查数值/内容 | [`09_CONTENT.md`](09_CONTENT.md) |
| 查架构/约定 | [`02_ARCHITECTURE.md`](02_ARCHITECTURE.md) |
| 查体验意图 | [`11_FANTASY_FEEL.md`](11_FANTASY_FEEL.md)（**无数值权威**） |

**禁止**在未读对应 Goal 的验收标准前开始写代码。  
**禁止**跨 Phase 实现未解锁依赖的系统。  
**禁止**在 `11` 中查找 buffer/hitstop 等数字（权威在 `04§4.8`）。

### 0.2 文档冲突优先级（权威序）

```
01 不做清单  >  03/09 数值权威  >  系统文档机制  >  11 体验意图  >  内容文案
```

### 0.3 Goal 模式工作流

```
1. 打开 10_PROGRESS.md 的“现在从这里继续”
2. 运行 `./tools/goal_status.sh --current` 只展开当前卡片；需要预览后继时运行 `./tools/goal_status.sh --next`
3. 阅读 Goal 卡片：范围 / 交付物 / 验收标准 / 引用文档
4. 只实现该 Goal 范围（Out of Scope 一律不做）
5. 代码与静态结构完成后标为 implemented；Editor 可用时逐项执行 acceptance，全部通过才标为 done
6. 同步进度指针与 Goal 卡片状态；总体任务是“完成整个项目”时，直接领取下一 Goal，无需等待新的用户指令
```

`implemented` 只表示代码交付完成、运行时验收待执行，不等于产品完成。最终 MVP 仍要求相关 Goal 全部 `done`。

**硬拆分**：任何使 Goal 预估新增 **>1h** 的工作必须 **新 Goal 或显式上调 est_hours**；禁止静默塞 acceptance。  
独立 Goal（不得并回父卡）：`G01-05`、`G04-05`、`G05-06`。

Goal 是**范围和验收原子**，不是会话停止点。同一时刻不得混写两个 Goal，
但一个会话可以在关账并更新快照后连续推进多个 Goal。每个 Goal 预计约
1–4 小时（个别如 G05-03 可至 6h）。

Unity Editor 已打开并占用项目时，不循环启动 batchmode；先完成静态工作，
将运行时验收留在当前 Goal，Editor 可用后只执行一次目标测试与必要回归。

规格小歧义不构成阻塞：不改变 MVP 边界时，优先沿用现有 API/ID、作最小实现并
同步权威文档。编译或测试失败应在当前 Goal 内诊断，不能据此重开历史 `done`。

### 0.4 规格书写约定

每个**系统规格**必须包含：Schema / StateMachine / API / Pipeline / EdgeCases / Acceptance。  

**例外**：`11_FANTASY_FEEL.md` 为意图文档，**免六段**，且**非 Goal 入口**。

每个 **Goal 卡片** 必须包含：`id` / `phase` / `depends_on` / `est_hours` / `deliverables` / `acceptance` / `refs` / `out_of_scope`。

### 0.5 文档地图

| 文件 | 内容 | 何时读 |
|------|------|--------|
| [`01_VISION_MVP.md`](01_VISION_MVP.md) | 愿景、循环、MVP、金丹节拍、不做清单 | 开局 / 砍需求 |
| [`02_ARCHITECTURE.md`](02_ARCHITECTURE.md) | 目录、EventBus、GameState、存档 | 写代码前 |
| [`03_DATA_LAYER.md`](03_DATA_LAYER.md) | SO、struct、配置、公式 | 建数据层 |
| [`04_PLAYER_COMBAT.md`](04_PLAYER_COMBAT.md) | 输入、镜头、战斗、**§4.8 手感权威** | Phase 1 |
| [`05_CULTIVATION.md`](05_CULTIVATION.md) | 灵根被动、突破仪式、炼体 | Phase 2 |
| [`06_ITEMS_EQUIP_SKILL.md`](06_ITEMS_EQUIP_SKILL.md) | 背包、装备、功法、生活技能 | Phase 2–3 |
| [`07_WORLD_ENEMY_QUEST.md`](07_WORLD_ENEMY_QUEST.md) | 地图、天气、AI、任务、奇遇、checkpoint | Phase 4–5 |
| [`08_UI_META.md`](08_UI_META.md) | UI、教程单存储、坐骑、宗门、成就 | Phase 3+6 |
| [`09_CONTENT.md`](09_CONTENT.md) | 内容表、must-ship 文案、经济 | 填内容 |
| [`10_PROGRESS.md`](10_PROGRESS.md) | **当前指针、剩余工作、下一步（唯一恢复入口）** | 每次恢复任务 / 切换 Goal |
| [`10_GOALS.md`](10_GOALS.md) | 原子 Goal 范围、依赖、交付与验收卡片 | 由 `goal_status.sh` 定点读取 / 规划依赖 |
| [`history/TEST_EVIDENCE.md`](history/TEST_EVIDENCE.md) | 历史 Unity XML、验证入口、G02 代码证据与验收债务 | 查证“是否真的做过”时 |
| [`11_FANTASY_FEEL.md`](11_FANTASY_FEEL.md) | 仙侠气质意图（无数值） | 开局气质 / 文案音频 |
| [`12_GLOBAL_OPTIMIZATION_ROADMAP.md`](12_GLOBAL_OPTIMIZATION_ROADMAP.md) | Post-MVP 全局优化轮次、状态、证据与总完成条件 | 规划优化轮次；禁止跨轮盲改 |
| [`design/FANTASY_GAMEPLAY_OPTIMIZATION.md`](design/FANTASY_GAMEPLAY_OPTIMIZATION.md) | v3.1 设计决策全文 | 争议 / 溯源 |
| [`GDD.md`](GDD.md) | 入口转发 | 历史 |

### 0.6 版本与变更规则

- 改机制/数值 → 系统文档 + `09` + Goal 验收  
- 改架构 → `02` + `10` 依赖  
- 新增系统 → 六段规格 + Goal  
- **v3.1**：体验层（手感/仪式/金丹节拍/奇遇/教程单存储/权威序）

---

## 1. 一句话产品定义

**第三人称动作仙侠 ARPG**：玩家在开放式修仙大地图中战斗、采集、修炼突破，经历「探索 → 击杀 → 成长 → 解锁新区域」的闭环；MVP 交付单机垂直切片（青石原 → 苍梧山脉 → 黑风秘境，含筑基与**金丹**主线记忆点）。

## 2. 技术栈锁定

| 项 | 选型 |
|----|------|
| 引擎 | Unity 6000.5.3f1 |
| 渲染 | URP |
| 输入 | Input System（键鼠 + 手柄核心） |
| 寻址 | Addressables（可后接） |
| 网络 | MVP 单机；`INetworkAdapter` 接口 |
| 存档 | JSON 分文件 + 3 槽 |

## 3. 成功标准

见 [`01_VISION_MVP.md`](01_VISION_MVP.md) §8 与 [`10_GOALS.md`](10_GOALS.md) Phase 终态。
