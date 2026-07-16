# G09-03 端到端可玩性审计

> 日期：2026-07-16  
> Unity：6000.5.3f1  
> 平台：macOS Player（基线 960×540；修复证据 1280×720；缩放抽检
> 1024×576 与 1600×900）  
> 当前阶段：验收完成

## 1. 冻结玩家旅程

本轮固定以下关键路径，后续三次验收必须使用相同顺序与干净的隔离存档：

1. Boot → MainMenu。
2. 新建默认槽位 → 青石加载 → 选择灵根。
3. 找到药老并开始主线；完成首个对话、首战、首个任务交付。
4. 完成采集、炼丹、秘径与苍梧跨图。
5. 完成两次境界成长并通过金丹门禁。
6. 进入黑风五层、推进 checkpoint、击败石将军。
7. 触发死亡 → 最近传送阵复生。
8. 保存、破坏运行时状态、读档并核对任务/境界/地图/checkpoint/背包。
9. 键鼠与手柄分别覆盖主菜单确认、移动/战斗、交互、背包、对话与暂停；模态 UI 打开时玩法输入为零。

## 2. 真实 Player 基线

| 状态 | 截图 | 日志 | 结果 |
|---|---|---|---|
| 主菜单 | `previews/g09-03/before/01-main-menu.png` | 系统 Player.log | 可渲染；无异常 |
| 灵根选择 | `previews/g09-03/before/02-qingshi-root.png` | `TestResults/G09-03/baseline-root.log` | Modal 可渲染；背景玩法被遮罩 |
| 青石 HUD | `previews/g09-03/before/02-qingshi-hud.png` | `TestResults/G09-03/baseline-hud.log` | HUD 可渲染；确认缺少下一主线引导 |
| 对话 | `previews/g09-03/before/02-qingshi-dialogue.png` | `TestResults/G09-03/baseline-dialogue.log` | 对话可渲染；构图问题路由 G09-04 |
| 背包 | `previews/g09-03/before/02-qingshi-inventory.png` | `TestResults/G09-03/baseline-inventory.log` | 空态与关闭路径可见 |
| 任务 | `previews/g09-03/before/02-qingshi-quest.png` | `TestResults/G09-03/baseline-quest.log` | 日常进度与领奖路径可见 |
| 地图 | `previews/g09-03/before/02-qingshi-map.png` | `TestResults/G09-03/baseline-map.log` | 已解锁/锁定与关闭路径可见 |
| 死亡 | `previews/g09-03/before/02-qingshi-death.png` | `TestResults/G09-03/baseline-death.log` | 失败原因、惩罚与复生路径可见 |

Player 状态启动均使用发布构建的 opt-in showcase 参数；普通启动行为未改变。
日志扫描未命中 `NullReferenceException`、`Unhandled Exception`、
`ConfigDatabase entered safe mode` 或启动拒绝。

## 3. 自动化基线

现有关键旅程相关用例过滤运行：

```text
G0704 + G0601 + G0901 + G0104 + GVS08 + G0502 + G0503
28/28 Passed
```

证据：

- `TestResults/G09-03-baseline-targeted-playmode.xml`
- `TestResults/G09-03-baseline-targeted-playmode.log`

既有用例覆盖存读档、三图加载、UI 互斥、死亡复生、教程完成态与地图/黑风状态，
但大量步骤直接调用系统 API，尚不能证明“玩家能从画面知道下一步”。因此 G09-03
新增的旅程入口必须同时记录状态断言与屏幕引导断言。

## 4. 基线问题冻结

权威问题表：`docs/optimization/ISSUE_LEDGER.md`。

当前 P1 根因不是系统不存在，而是系统之间缺少最后一公里的导航：

- `QuestTrackerView` 在无 Active Quest 时完全隐藏，主线接取空窗没有引导。
- `NPCController` 只有 3m 内交互文字，没有可接/可交付任务的远距标记。

静态调用链审计另确认一个 P0：主线 04 依赖炼丹，但发布运行时没有任何丹炉交互体，
正常玩家无法打开 `panel_alchemy`。这不是表现问题，而是确定性的主线断链。

真实 Input System 状态测试还确认手柄背包/暂停路径使用了属性名而非 control path：
`selectButton` / `startButton` 不会解析到 `Gamepad` 控件。既有测试只比较 JSON
字符串，因此产生了假绿。

本轮计划以现有 Quest/NPC 数据和 G09 图标资产补齐导航，并补上规格内丹炉交互入口；
不改变任务内容、地图美术、战斗数值或对话镜头。

用户随后确认 macOS 窗口太小且不可自由缩放。静态审计对应
`ProjectSettings.resizableWindow: 0`，因此作为 G09-03-008 纳入同轮 P1：
发布入口现统一使用 Windowed、可缩放窗口及 2560×1440 Retina 默认尺寸。

## 5. 修复后证据

| 范围 | 证据 | 结果 |
|---|---|---|
| 下一主线与世界标记 | `previews/g09-03/after/01-qingshi-guidance-furnace.png` | 无 Active Quest 时仍显示药老、接取动作和距离 |
| 丹炉真实入口 | `previews/g09-03/after/02-alchemy-furnace.png` | 青石丹炉可交互，炼丹 Modal 打开并锁定玩法输入 |
| 黑风终点 | `previews/g09-03/after/03-blackwind-complete.png` | 五层、BOSS、checkpoint、死亡复生和读档旅程可达 |
| 窗口缩放 | 1024×576、1280×720、1600×900 Player 抽检 | HUD、任务提示、菜单栏与技能栏无裁切或错位 |

自动化结果：

- G09-03 目标 PlayMode：5/5。
- 全量 EditMode：34/34。
- 全量 PlayMode：221/221。
- macOS Release：134,696,660 bytes，3 warnings，0 errors。
- Boot Player smoke：通过。
- 干净隔离存档发布旅程：`run-1`、`run-2`、`run-3` 均为
  55/55 步通过；最后一步均在黑风洞核对存读档回环。

原始本机证据：

- `TestResults/G09-03-targeted-playmode.xml`
- `TestResults/G09-03-full-editmode.xml`
- `TestResults/G09-03-full-playmode.xml`
- `TestResults/G09-03/journey-run-1.json`
- `TestResults/G09-03/journey-run-2.json`
- `TestResults/G09-03/journey-run-3.json`
- `TestResults/G07-04-build.json`

## 6. 结论

- [x] 可重复旅程测试入口与隔离存档。
- [x] 键鼠/手柄输入到主循环的真实 Input System 状态测试。
- [x] G09-03 P0/P1 修复前后证据。
- [x] 干净旅程连续 3 次成功的 JSON/日志。
- [x] 目标、全量、Release、Boot 与三图关键流程门禁。

G09-03 已满足 `done`。对话镜头、角色一致性和地图构图仍按台账路由到
G09-04、G09-07、G09-08，不在本轮扩项。
