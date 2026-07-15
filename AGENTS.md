# AGENTS.md — 《问道长生》AI 开发约定

你是本仓库的实现代理。规格在 `docs/`，**以 Goal 模式推进**。  
规格版本：**v3.1**（开发流程修订：2026-07-15，进度入口拆分）。

## 首次接手必读顺序

1. `docs/00_INDEX.md`
2. `docs/01_VISION_MVP.md`
3. `docs/11_FANTASY_FEEL.md`（气质意图；**不要**当数值源）
4. `docs/10_PROGRESS.md` ← **当前进度的唯一恢复入口**
5. 运行 `./tools/goal_status.sh --current` ← 只展开当前 Goal 卡片

同一项目的连续开发不重复通读以上全文。恢复任务时只读
`docs/10_PROGRESS.md` 的“现在从这里继续”、`goal_status.sh --current` 输出及其 `refs`；仅在
MVP 边界、权威冲突或规格版本发生变化时回读入口文档。

争议溯源：`docs/design/FANTASY_GAMEPLAY_OPTIMIZATION.md`（Approved）。

## 冲突优先级（硬）

```
01 不做清单  >  03/09 数值权威  >  系统文档机制  >  11 体验意图  >  内容文案
```

## 硬规则

1. **同一时刻只推进一个 Goal**。先恢复唯一的 `in_progress`；没有活跃 Goal 时，领取 `status: pending` 且依赖已 `implemented/done` 的最小编号。当前 Goal 达到 `done`（或仅能达到 `implemented`）并同步 `10_PROGRESS.md` 后，可在同一总体开发任务中继续下一个 Goal，不必等待用户重复下令。
2. 实现前阅读该 Goal 的 `refs` **系统章节**；冲突时服从上方优先级。
3. **禁止**实现当前 Goal 的 `out_of_scope`。
4. 新增事件 / 物品 ID / 存档字段必须回写：
   - 事件 → `docs/02_ARCHITECTURE.md` §5.2  
   - ID/表 → `docs/09_CONTENT.md`  
   - Schema → `docs/03_DATA_LAYER.md`
5. 系统间通信优先 `EventBus`；跨系统查询用 `ServiceLocator`；禁止乱加单例耦合。
6. MVP 不做：联机实现、元婴玩法、完整炼器/制符/阵法 UI、钓鱼家园、完美闪避/韧性、石将军练习模式、FeatureFlags 运行时文件等（见 `01`）。
7. 无正式美术时允许占位，但 **ID 与 API 必须按规格**。
8. 代码与静态结构交付完成后标记 `implemented`；对照 `acceptance` 完成真实自测后才可改为 `done`。Editor 不可用时允许后置运行时验收，但禁止虚报 `done`。
9. **工时硬拆分**：新增预估 >1h 必须新 Goal 或显式上调 `est_hours`；**禁止**把 `G01-05` / `G04-05` / `G05-06` 并入父 Goal。
10. 玩家可见字符串：**必须** localization key，中文为 default value。
11. 手感数字只改 `04§4.8`；突破仪式逻辑只改 `05`；**不要**往 `11` 写毫秒表。
12. `done` 是历史完成事实：除非回归失败或用户明确要求重做，不重新实现、不把它当成下一任务。发现状态与代码不一致时，先修正快照和状态再开发。
13. Unity Editor 已占用项目时，不反复启动 batchmode。保留静态校验结果并把运行时验收记为待执行；Editor 可释放后再跑一次目标测试与一次必要回归。
14. 长期“完成整个项目”的任务默认连续推进：每个 Goal 内仍遵守范围边界，但 Goal 间不需要逐项向用户索要确认。
15. **小冲突不停摆**：字段命名、占位表现、测试组织等不改变 MVP 范围的歧义，由实现代理按现有架构作最小决定并同步权威文档；只有缺少会改变产品方向的用户选择或外部权限时才算阻塞。
16. 编译/测试失败是待诊断问题，不是自动停止理由；先修复当前 Goal，禁止借此回滚到历史 `done` Goal 或反复重读全部规格。

## 连续开发提示（默认）

```
按 docs/10_PROGRESS.md 当前指针继续开发。
用 ./tools/goal_status.sh --current 读取当前卡片；不要通读 10_GOALS.md。
同一时刻只推进一个 Goal；完成并更新快照后自动进入下一个，
直到总体任务完成或出现必须由用户决定的阻塞。
```

只有用户明确说“只做 Goal {ID}”时，才在该 Goal 完成后停下。

## 当前入口

始终先读 `docs/10_PROGRESS.md`，再运行 `./tools/goal_status.sh --current`。
不要在 `AGENTS.md` 写死具体 Goal，也不要从推荐起始链或旧设计稿
重跑已经 `implemented/done` 的 Goal。若进度指针与卡片状态冲突，先运行
`tools/check_docs_consistency.sh` 并修正文档，再继续开发。

## 技术锁定

Unity 6000.5.3f1 · URP 17.5 · C# · Input System · 单机 JSON 存档。
