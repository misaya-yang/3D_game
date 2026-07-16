# 全局优化问题台账

> 本台账由 `docs/12_GLOBAL_OPTIMIZATION_ROADMAP.md` 约束。问题先冻结再修；
> G09-03 只处理已确认的 P0/P1，P2/P3 必须路由到后续 Goal。

## 状态定义

| 状态 | 含义 |
|---|---|
| `open` | 已复现，尚未修复 |
| `fixed` | 已修复，等待完整回归 |
| `verified` | 修复证据与回归均通过 |
| `routed` | 不属于当前 Goal，已分配后续 Goal |

## 冻结清单（G09-03）

| ID | 严重度 | 状态 | 归属 Goal | 复现与实际结果 | 期望 | 修复/路由 | 前证据 | 后证据 | 回归 |
|---|---|---|---|---|---|---|---|---|---|
| G09-03-001 | P1 | verified | G09-03 | 干净存档进入青石并选定灵根后，没有进行中任务；任务追踪器直接隐藏。玩家看不到第一步应找谁、做什么。每次主线交付后到下一环接取前也出现同一空窗。 | 主线尚未结束时，即使没有 Active Quest，也持续显示下一环可执行目标、目标 NPC 与接取动作。 | `MainQuestGuidanceResolver` 按章节顺序只读解析可接、进行中、可交付与下一环；`QuestTrackerView` 不再在主线空窗隐藏。 | `docs/optimization/previews/g09-03/before/02-qingshi-hud.png`；旧 `QuestTrackerView.Refresh()` 在 `ActiveIds.Count == 0` 时隐藏 | `docs/optimization/previews/g09-03/after/01-qingshi-guidance-furnace.png` | `G0903PlayModeTests.FreshSaveShowsAvailableMainQuestAndYaoLaoMarker`、`TurnInAndNextAcceptWindowsKeepMainGuidanceVisible`；全量 221/221 |
| G09-03-002 | P1 | verified | G09-03 | 青石出生点同时可见多名 NPC；NPC 名称与交互提示仅在 3m 内出现，且没有任务可接/可交付标识。结合 G09-03-001，玩家无法从画面判断药老是谁。 | 可接/可交付主线 NPC 在合理视距内有真实 UI 图标与名称/距离提示；进入 3m 后继续沿用既有交互提示。 | `QuestWorldMarkerView` 复用 G09 `exclamation`、`target`、`checkmark` 图标，显示目标名称、距离和屏幕边缘方向；不改 NPC 造型或推进状态。 | `docs/optimization/previews/g09-03/before/02-qingshi-hud.png`；旧 `NPCController` 仅对近距 `_prompt` 可见 | `docs/optimization/previews/g09-03/after/01-qingshi-guidance-furnace.png` | `G0903PlayModeTests.FreshSaveShowsAvailableMainQuestAndYaoLaoMarker`；三次 55 步 Player 旅程 |
| G09-03-003 | P2 | routed | G09-04 | 对话画面大面积是天空，人物头部与右侧 NPC 被裁切，焦点关系弱。 | 对话镜头稳定容纳玩家、NPC 与文本，不遮挡主体。 | 路由 G09-04 镜头/手感轮；本轮不混入镜头美化。 | `docs/optimization/previews/g09-03/before/02-qingshi-dialogue.png` | G09-04 | G09-04 |
| G09-03-004 | P2 | routed | G09-07 | NPC 与角色模型风格、体量和材质一致性不足，近景可读性弱。 | 主角、NPC、敌人具有稳定轮廓与材质层级。 | 路由 G09-07 角色建模与动作轮。 | `docs/optimization/previews/g09-03/before/02-qingshi-hud.png` | G09-07 | G09-07 |
| G09-03-005 | P2 | open | G09-08 | 青石地图缺少自然边界、地标层级与道路引导，区域辨识主要依赖记忆。 | 三图具备可读路径、地标、生态分区和视觉边界。 | G09-08 已用当前 Release Player 重现并冻结三图问题集；等待本轮成组实现。 | `docs/optimization/previews/g09-08/before/01-qingshi-spawn.png` | G09-08 | G09-08 |
| G09-03-006 | P0 | verified | G09-03 | 主线 `quest_main_01_04` 要求在丹炉炼成回气丹，但发布运行时没有任何丹炉交互体；`panel_alchemy` 仅能由测试和 opt-in showcase 直接打开，正常玩家无法进入炼丹界面，主线必定中断。 | 青石镇存在可见、可交互的稳定丹炉；E/手柄交互键打开炼丹面板并触发既有教程，面板关闭后归还输入。 | 新增稳定 FurnaceId、`OnAlchemyFurnaceInteracted` 契约、青石/苍梧运行时摆放及真实 CC0 锅具/炉石；UIManager 互斥打开并关闭归还输入。 | 全仓旧调用链仅命中 UI、测试与 showcase；`docs/optimization/previews/g09-03/before/02-qingshi-hud.png` 无丹炉入口 | `docs/optimization/previews/g09-03/after/01-qingshi-guidance-furnace.png`；`after/02-alchemy-furnace.png` | `FurnaceInteractionOpensAlchemyAndLocksGameplayInput`、`MainQuestFourCompletesThroughRuntimeFurnace`；三次发布旅程 |
| G09-03-007 | P1 | verified | G09-03 | InputAction JSON 把背包与暂停手柄路径写成 `<Gamepad>/selectButton` / `<Gamepad>/startButton`；当前 Input System 的真实控制路径是 `<Gamepad>/select` / `<Gamepad>/start`。静态字符串测试误把无效路径判为通过，运行时 Action 不解析 Gamepad control。 | 手柄 Select 可打开背包，Start 可暂停；绑定测试必须验证 control 实际解析而不只匹配 JSON 字符串。 | 改为 `<Gamepad>/select` 与 `<Gamepad>/start`；测试创建真实虚拟 Gamepad 并检查解析后的 `InputControl`，同时覆盖 Modal 下移动、攻击、格挡为零。 | `TestResults/G09-03-input-playmode.xml`；旧运行时 `OpenInventory.controls` 仅含 `<Keyboard>/b` | `Assets/_Project/Resources/Input/PlayerInputActions.inputactions` | `KeyboardAndGamepadReachCoreActionsAndModalSuppressesGameplay`；目标 5/5、全量 221/221 |
| G09-03-008 | P1 | verified | G09-03 | macOS Player 使用 `resizableWindow: 0`，1920×1080 Retina 默认窗口视觉尺寸约为 960×540，用户明确反馈画面太小且不能自由缩放。 | 默认窗口在当前 Mac 上可读，并可拖拽到任意常用窗口尺寸；HUD 不裁切、不漂移。 | `ProjectSettings` 与发布构建入口统一为 Windowed、`resizableWindow=true`、2560×1440 Retina 默认尺寸；Canvas 保持 1920×1080 `ScaleWithScreenSize`。 | `docs/optimization/previews/g09-03/before/01-main-menu.png`；旧 `resizableWindow: 0` | `docs/optimization/previews/g09-03/after/01-qingshi-guidance-furnace.png`；Player 已在 1024×576、1600×900 实测 | `tools/validate_g09_03.sh` 静态锁定发布设置；Release 构建与双尺寸 Player 截图检查通过 |

## G09-03 基线结论

- 真实 macOS Player 可渲染主菜单、灵根、HUD、对话、背包、任务、地图与死亡状态。
- 七份 Player 状态日志未发现崩溃、未处理异常或 `ConfigDatabase` 安全模式。
- 关键既有 PlayMode 基线为 28/28 通过；这证明系统 API 闭环存在，但不能替代新玩家导航验收。
- 本轮最终冻结并清偿 **1 个 P0、4 个 P1**；另有 **3 个 P2** 已分别路由
  G09-04、G09-07、G09-08。所有 P0/P1 均通过目标回归、全量回归和三次发布旅程。

## 冻结清单（G09-04）

| ID | 严重度 | 状态 | 归属 Goal | 复现与实际结果 | 期望 | 修复/路由 | 前证据 | 后证据 | 回归 |
|---|---|---|---|---|---|---|---|---|---|
| G09-04-001 | P1 | fixed | G09-04 | `ThirdPersonCamera.SetCombatMode()` 在运行时代码没有调用方，仅测试和 showcase 手动调用。普通实战即使 `GameManager.IsInCombat=true`，未锁定时仍使用探索 55 FOV / 5m，而不是规格的战斗 65 FOV / 4.5m。 | 普通攻击、受击或施法进入战斗后，镜头自动使用 `04§3.2` 战斗构图；脱战后恢复探索构图。 | `ThirdPersonCamera.IsCombatMode` 只读合并现有 combat flag；未新增第二套状态。 | `docs/optimization/previews/g09-04/before/01-qingshi-normal-combat.png`；旧调用链无运行时接线 | 目标运行时断言已通过；Player 对照待视觉轮后补 | `G0904PlayModeTests.CombatFlagDrivesCameraFramingAndIncomingDamageStartsCombat` |
| G09-04-002 | P1 | fixed | G09-04 | `PlayerStats.HandleDamageApplied()` 只在 `info.Source == player` 时登记战斗活动。敌人先手命中玩家不会置 combat flag，也不会重置脱战计时；镜头/BGM/恢复状态可能仍按探索处理。 | 玩家造成伤害、施法或受到正伤害都应进入同一战斗状态并刷新脱战计时。 | Source 或 Target 属于玩家时统一登记，不改变恢复时长和敌人数值。 | `PlayerStats.HandleDamageApplied()` 旧静态调用链；三场基线 | 目标运行时断言已通过 | `G0904PlayModeTests.CombatFlagDrivesCameraFramingAndIncomingDamageStartsCombat` |
| G09-04-003 | P1 | fixed | G09-04 | UI 只调用 `IPlayerInputSource.SetEnabled(false)`；已开始的 `PlayerCombatController.TickAttack`、`PlayerController.TickDodgeState` 与 `SkillManager.TickActiveCast` 在 GameState 仍为 Playing 时继续推进。攻击可在背包打开后结算，闪避可在遮罩后继续位移，施法可在 Modal 后释放。 | 任意 Modal 打开后立即停止玩法位移与尚未结算的攻击/技能；关闭后回到稳定 Idle/Fall，不能补打一击或残留 SkillCast。Pause 则冻结而非取消。 | Attack/Dodge/Skill/Buffer 统一查询输入可用性并取消未结算动作；Paused 仍冻结并保留动作。 | G09-03 仅验证新输入为零，未覆盖“动作已开始再开 Modal”；旧 Update 路径继续 Tick | 目标运行时断言已通过 | `InputLockCancelsAttackDodgeAndSkillBeforeResolution`、`PauseFreezesActiveAttackInsteadOfCancellingIt` |
| G09-04-004 | P1 | fixed | G09-04 | `OnLockOnChanged` 只有 Camera 订阅，UI 无任何订阅者。目标切换只靠镜头 yaw/FOV，普通敌没有锁定标记或锁定血条；多敌场景无法明确判断当前目标。 | 锁定、切换、目标死亡/超距时有唯一且即时的目标指示；标记跟随真实 `ILockOnTarget.LockOnTransform`，清锁后消失。 | 新增 `LockOnMarkerView`，复用 G09 `target` 图标并跟随真实锁定锚点；Modal/HUD 隐藏与既有 UIManager 一致。 | 三张 G09-04 基线；旧 UI 无 `LockOnChanged` 订阅 | 目标运行时断言已通过；Player 对照待视觉轮后补 | `LockOnMarkerTracksRealAnchorAndClearsWhenTargetDies` |
| G09-04-005 | P2 | routed | G09-07 | 当前低模主角、普通敌与 BOSS 缺少完整攻击/受击动作重定向，截图无法表达招式节奏。 | 角色轮廓、动作与命中姿态一致且可读。 | 路由 G09-07；G09-04 只保证状态、数值、镜头和反馈时序，不扩成完整动作生产。 | `docs/optimization/previews/g09-04/before/*.png` | G09-07 | G09-07 |

## G09-04 基线结论

- `04§4.8` 的 120ms buffer、30/60ms hitstop、0.18s hitstun、暴击震屏与
  元素反应 FOV 常量已存在，当前不需要也不允许另造手感数字。
- 问题集中在“已有系统没有接上真实运行时状态”和“Modal/锁定的最后一公里反馈”。
- 本轮先冻结 **4 个 P1**，另有 **1 个 P2** 路由 G09-07；修复前不调整敌人
  HP、攻击力、技能倍率或新动作资产。

## 冻结清单（G09-08）

| ID | 严重度 | 状态 | 归属 Goal | 复现与实际结果 | 期望 | 修复/路由 | 前证据 | 后证据 | 回归 |
|---|---|---|---|---|---|---|---|---|---|
| G09-08-001 | P2 | open | G09-08 | 青石出生点看不到聚落主地标；NPC 挤在道路和桥前，树、巨石、灵草为规则散点，远景是空平面。 | 镇门/屋脊、训练坪、溪桥与区域边界形成稳定远近层级。 | 待参数化构图与成熟 CC0 建筑/自然资产替换。 | `docs/optimization/previews/g09-08/before/01-qingshi-spawn.png` | 待补 | G09-08 |
| G09-08-002 | P1 | open | G09-08 | 苍梧程序化山门横向遮住路线；传送圆盘、巨石阵和西式帐篷像测试场。 | 山门只框景；盘山道、云雾谷、洞府与雷台形成纵深且出生区无巨大调试形状。 | 待缩减遮挡、移除主题污染物并重组山林地标。 | `docs/optimization/previews/g09-08/before/02-cangwu-spawn.png` | 待补 | G09-08 |
| G09-08-003 | P1 | open | G09-08 | 黑风入口照度不足，人物与敌人粘连；重复房间、方块宝箱和孤立巨石缺少方向信号。 | 保留压迫感但敌我/机关清晰；五层由门洞、火盆、碑纹和层级色温区分。 | 待重做室内照明、楼层视觉语法和交互物外观。 | `docs/optimization/previews/g09-08/before/03-blackwind-entry.png` | 待补 | G09-08 |
| G09-08-004 | P2 | open | G09-08 | 三图使用规则坐标与轮换资源，形成同尺度树列、重复岩石墙和主题污染物。 | 确定性生态簇具备尺度/朝向变化，且各图使用独立资产表。 | 待建立配置驱动的生态散布器与重复率审计。 | 三张 G09-08 基线 | 待补 | G09-08 |
| G09-08-005 | P2 | open | G09-08 | 地标、禁放区、路线净空和环境色值散落在单个大型运行时类中，无法机器审计。 | 地图视觉配置、路线走廊、地标覆盖、资产预算和环境参数可机器读取。 | 待新增 G09-08 配置、审计报告与目标测试。 | `BudgetWorldArtBootstrap.cs` | 待补 | G09-08 |
