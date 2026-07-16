# G09-04 战斗、镜头与输入手感审计

> 日期：2026-07-16  
> Unity：6000.5.3f1  
> 数字权威：`docs/04_PLAYER_COMBAT.md` §2.2、§3.2、§4.4、§4.8  
> 当前阶段：代码与目标回归完成；按用户视觉优先级暂存为 `implemented`

## 1. 固定验收场景

本轮只使用三组既有内容，不调整敌人数值：

1. 青石普通战：移动、四段轻击、重击、闪避、格挡、技能与普通狼。
2. 苍梧精英战：锁定/切换、受击、精英 0.5 倍 hitstun、资源不足与脱战恢复。
3. 黑风石将军：BOSS 血条、阶段切换、死亡恢复、镜头碰撞与战斗状态清理。

每组同时覆盖键鼠与手柄核心绑定；背包、任务、地图、炼丹、商店等 Modal
打开时不得继续位移、攻击或施法。Pause 仍按原契约冻结时间，不取消动作。

## 2. 数字基线

| 范围 | 权威值 | 当前代码 |
|---|---|---|
| 移动/冲刺/转向 | 5m/s、8m/s、720°/s | `PlayerController` 一致 |
| 闪避 | 5m / 0.35s；无敌 0.2s；CD 0.8s | `PlayerController` 一致 |
| 四段轻击 | 1.0/1.1/1.25/1.5；0.1—0.15s 前摇；0.25—0.45s 后摇 | `PlayerCombatController` 一致 |
| 重击 | 2.0；0.35s 前摇；0.5s 后摇 | 一致 |
| Input buffer | 120ms unscaled | `CombatFeelSettings` / `PlayerActionBuffer` 一致 |
| Hitstop | L1/L2 30ms；L3/L4/重击 60ms | 一致 |
| Hitstun | 普通 0.18s；精英 ×0.5 | 一致 |
| 镜头 | 探索 55/5m；战斗 65/4.5m；锁定 60/4.5m | 常量一致，但战斗运行时未接线 |

结论：本轮不改权威数字，优先修复状态接线、动作取消和反馈可见性。

## 3. 发布 Player 基线

| 状态 | 证据 | 结论 |
|---|---|---|
| 青石普通场景 | `previews/g09-04/before/01-qingshi-normal-combat.png` | 敌人可见，但普通战斗没有自动战斗镜头或锁定标记 |
| 苍梧精英场景 | `previews/g09-04/before/02-cangwu-elite-combat.png` | 多目标环境中当前锁定对象不可见 |
| 黑风场景 | `previews/g09-04/before/03-blackwind-boss-combat.png` | 黑风情绪基线可渲染；完整动作表现路由角色轮 |

三份日志未命中 `NullReferenceException`、未处理异常或安全模式。

## 4. 静态调用链结论

权威问题表：`docs/optimization/ISSUE_LEDGER.md`。

- `ThirdPersonCamera.SetCombatMode` 没有真实运行时调用方。
- `PlayerStats` 只把“玩家造成伤害”计为战斗活动，忽略“玩家受到伤害”。
- Modal 只关闭新输入；已启动的 Attack/Dodge/Skill 生命周期仍可继续 Tick。
- `OnLockOnChanged` 没有 UI 订阅者，普通敌当前锁定目标不可见。

以上均不需要新增战斗数值或敌人内容，属于 G09-04 范围内的 P1 接线/反馈问题。

## 5. 当前实现证据

- [x] 新增 G09-04 目标回归，四个冻结 P1 为 5/5 Passed。
- [x] 战斗镜头接入真实 combat flag；受击会登记战斗活动。
- [x] Modal 取消未结算 Attack/Dodge/Skill，Pause 保持冻结。
- [x] 锁定标记跟随真实锚点并在死亡/清锁后消失。
- [x] Transparent 与销毁中的遮挡物不会永久隐藏。

证据：

- `TestResults/G09-04-targeted-playmode.xml`：5/5 Passed。
- `Assets/_Project/Tests/PlayMode/VerticalSlice/G0904PlayModeTests.cs`。

## 6. 视觉优先调整后的待办

- [ ] 同路径修复后截图：普通战斗镜头、锁定目标、Modal 动作取消、BOSS 战。
- [ ] 键鼠/手柄等价输入，buffer、连段、闪避、锁定、hitstop、hitstun 回归。
- [ ] Opaque/Transparent 遮挡与销毁场景均恢复 Renderer 状态。
- [ ] 全量 EditMode/PlayMode、Release、Boot 与三场战斗 Player smoke。

用户要求先解决角色与三图观感，因此本 Goal 不虚报 `done`：当前标记
`implemented`，待 `G09-07`、`G09-08` 完成后恢复同路径 Player 对照和最终门禁。
