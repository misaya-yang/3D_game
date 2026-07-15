# G-VS-08 垂直切片验收记录

> 日期：2026-07-15  
> 记录类型：G-VS-08 历史验收清单；**不是当前开发进度入口**  
> 判定：最终全量 PlayMode 202/202 与 G07-04 新档综合回归已将 G-VS-08 标为 `done`。
> 下方真人走查保留为主观手感复核，不再是代码 MVP 门禁。

## 代码与自动化覆盖

| # | `01§5` 步骤 | 代码证据 | 自动化证据 | 代码就绪 |
|---|-------------|----------|------------|----------|
| 1 | 新游戏、选灵根、进入青石原 | `SpiritRootSystem`、`SpiritRootSelectionView`、`SceneLoader` | G-VS-01/G-VS-05 回归 | [x] |
| 2 | 完成移动/战斗教程 | `TutorialManager`；DEV `/tutorial_skip` 复用 `TryStart`→`Skip`→`Complete` | `TutorialSkipWritesTheSameCompletionKeysAndSurvivesLoad` | [x] |
| 3 | 击杀至少 5 个练气妖兽、伤害字可见 | `CombatSystem`、`EnemyBrain`、`DamageFloatingTextView` | G-VS-02/G-VS-07 回归；`/spawn`、`/killall` 命令回归 | [x] |
| 4 | NPC 对话并接任务 | `DialogueManager`、`QuestManager`、药老灰盒 | G-VS-06 回归 | [x] |
| 5 | 使用回血丹、装备木剑 | `ItemUseSystem`、`EquipmentManager`、背包 UI | G-VS-03 回归 | [x] |
| 6 | 释放一个功法 | `SkillManager`、快捷栏 1、引气弹 | G-VS-04 回归 | [x] |
| 7 | 修为升级 | `CultivationManager`、HUD；DEV `/givexp` | G-VS-05 与 `CommonDevelopmentCommandsExecuteThroughServiceBoundaries` | [x] |
| 8 | 存读档后三态一致 | `SaveManager` + inventory/quests/world modules | `SaveRoundTripPreservesQuestInventoryAndTutorialTogether` | [x] |

## 可选真人手感走查（安装后执行）

- [ ] Development Build / Editor 中反引号打开控制台，正式非 Development Build 不存在控制台。
- [ ] 从主菜单开始新游戏，选择一种灵根后进入青石原。
- [ ] 完成 `tut_move` 与 `tut_combat`；另开新档验证 `/tutorial_skip` 后二者 `HasCompleted == true`。
- [ ] 使用 `/spawn enemy_wolf_gray 5` 后亲手击杀至少 5 只灰狼，确认伤害字可见。
- [ ] 与药老对话、接受「东郊猎狼」、推进并交付任务。
- [ ] 使用回血丹（满血时不消耗），装备木剑并观察伤害变化。
- [ ] 按 1 释放引气弹，确认扣蓝与冷却。
- [ ] 正常击杀或 `/givexp` 升一层修为，确认 HUD 与属性刷新。
- [ ] 执行 `/give item_potion_heal_01 2`、推进任务、完成教程，再 `/save`；重启读档后核对背包、任务进度、教程完成态。
- [ ] 连续完整走查 25–40 分钟，无阻断性错误。

## 自动化入口

```bash
./tools/validate_gvs_08.sh --static-only
./tools/validate_gvs_08.sh
```

第二条命令只有在 Unity 6000.5.3f1 可执行文件就绪后才会运行 EditMode 与 G-VS-08 PlayMode 测试。
