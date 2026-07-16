# 自动化验收记录

> **用途**：保存已经运行过的 Unity 测试证据；不作为当前 Goal 指针。  
> **当前任务**：始终以 [`../10_PROGRESS.md`](../10_PROGRESS.md) 为准。  
> **记录日期**：2026-07-16；结果来自本机验证生成的 `TestResults/*.xml`。
> `TestResults/` 属于可再生输出并由 `.gitignore` 排除；新检出应运行对应
> `tools/validate_*.sh` 重新生成，不把历史 XML 当作仓库源文件。

## 已签核 Goal

| Goal | EditMode | PlayMode（含依赖回归） | 验证入口 |
|------|----------|--------------------------|----------|
| `G02-01` | 16/16 | 6/6 | `tools/validate_g02_01.sh` |
| `G02-02` | 16/16 | 5/5 | `tools/validate_g02_02.sh` |
| `G02-03` | 16/16 | 3/3 | `tools/validate_g02_03.sh` |
| `G02-04` | 16/16 | 11/11 | `tools/validate_g02_04.sh` |
| `G03-01` | 16/16 | 12/12 | `tools/validate_g03_01.sh` |
| `G03-02` | 16/16 | 16/16 | `tools/validate_g03_02.sh` |
| `G03-03` | 16/16 | 10/10 | `tools/validate_g03_03.sh` |
| `G03-04` | 16/16 | 14/14 | `tools/validate_g03_04.sh` |
| `G03-05` | 16/16 | 16/16 | `tools/validate_g03_05.sh` |
| `G04-01` | 16/16 | 9/9 | `tools/validate_g04_01.sh` |
| `G04-02` | 16/16 | 11/11 | `tools/validate_g04_02.sh` |
| `G04-03` | 16/16 | 15/15 | `tools/validate_g04_03.sh` |
| `G04-04` | 16/16 | 19/19 | `tools/validate_g04_04.sh` |
| `G04-05` | 16/16 | 23/23 | `tools/validate_g04_05.sh` |
| `G05-01` | 16/16 | 19/19 | `tools/validate_g05_01.sh` |
| `G05-02` | 16/16 | 9/9 | `tools/validate_g05_02.sh` |
| `G05-03` | 16/16 | 17/17 | `tools/validate_g05_03.sh` |
| `G05-04` | 16/16 | 13/13 | `tools/validate_g05_04.sh` |
| `G05-05` | 16/16 | 17/17 | `tools/validate_g05_05.sh` |
| `G05-06` | 16/16 | 22/22 | `tools/validate_g05_06.sh` |
| `G06-01` | 16/16 | 33/33 | `tools/validate_g06_01.sh` |
| `G06-02` | 16/16 | 18/18 | `tools/validate_g06_02.sh` |
| `G06-03` | 16/16 | 8/8 | `tools/validate_g06_03.sh` |
| `G06-04` | 16/16 | 11/11 | `tools/validate_g06_04.sh` |
| `G06-05` | 16/16 | 25/25 | `tools/validate_g06_05.sh` |
| `G07-01` | 16/16 | 35/35 | `tools/validate_g07_01.sh` |
| `G07-02` | 16/16 | 50/50 | `tools/validate_g07_02.sh` |
| `G07-03` | 16/16 | 50/50 + Metal 1/1 | `tools/validate_g07_03.sh` |
| `G07-04` | 34/34 全量 | 202/202 全量 + Build/Smoke | `tools/validate_g07_04.sh` |
| `G08-03` | 34/34 全量 | 205/205 全量 + Build/Smoke | `tools/validate_g08_03.sh` |
| `G09-01` | 34/34 全量 | 212/212 全量 + Build/Smoke | `tools/validate_g09_01.sh` |
| `G09-02` | 34/34 全量 | 216/216 全量 + Build/Smoke | `tools/validate_g09_02.sh` |
| `G09-03` | 34/34 全量 | 221/221 全量 + Build/Smoke + Player Journey 3/3 | `tools/validate_g09_03.sh` |
| `G09-07` | 34/34 全量 | 234/234 全量 + Metal 1/1 + Build/Smoke/Player | `tools/validate_g09_07.sh` |

表内 PlayMode 数量是对应验证脚本实际运行的目标测试与依赖回归总数，不等于
单个 Goal 新增用例数。运行验证脚本后，原始结果会生成到
`TestResults/<Goal>-editmode.xml` 与 `TestResults/<Goal>-playmode.xml`；这些文件
是本机测试产物，不进入 Git。

`G06-01` 新增目标用例为 5/5；33/33 是背包、角色、技能、炼丹、商店、任务与
统一 UI 导航的依赖回归总数。

`G06-02` 新增目标用例为 4/4；18/18 同时覆盖旧移动/战斗教程、DEV 跳过与读档、
炼丹触发、G06-01 UI 导航，确认扩展后仍使用同一 `world.json` 完成 key。

`G06-05` 新增目标用例为 5/5；25/25 同时覆盖 G01-01 攻击连段、G03-02 技能、
G04-03 石将军与 G04-04 掉落，确认动画事件、占位反馈和 BGM 接入没有破坏既有链路。

`G07-01` 新增目标用例为 5/5；35/35 同时覆盖锁定、元素反应、技能、敌人、青石
场景与飞行，确认天气倍率、光照接入和持久化没有改变晴天/白天基线。

`G07-02` 新增目标用例为 5/5；50/50 同时覆盖炼丹、采集、商店、掉落、苍梧、
黑风、主线、奇遇、成就与音画，确认内容图、本地化 schema、地图密度、251—293
终局经济闭区间及 195 分钟节拍预算没有破坏既有链路。

`G07-03` 新增目标用例为 4/4；50/50 同时覆盖背包满、技能资源、装备境界、重复
任务、对话切场景、灰盒材质复用和 3,600 模拟秒稳定性。独立 Metal 图形回归 1/1
在 1920×1080 Medium 下满足 30fps、15s 加载与 6GiB 内存预算；原始性能结果保存
为 `TestResults/G07-03-performance.xml`。

`G07-04` 新增综合用例为 2/2，并执行仓库全部测试：EditMode 34/34、PlayMode
202/202。随后构建约 114MiB 的 macOS Universal Release，并通过配置随包、
代码签名结构和 Player Boot 启动冒烟。完整映射见 `MVP_RELEASE_SIGNOFF.md`。

`G08-03`—`G09-02` 依次把全量 PlayMode 基线提升到 205、212、216。`G09-02`
目标 10/10 覆盖精修资源、主角/狼/石将军、三图纹理、苍梧 NavMesh 与相机遮挡
恢复；随后全量 34/34 + 216/216、134,663,203-byte macOS Release 构建及 Boot
Player 冒烟通过。视觉证据见 [`../art/G09-02_VISUAL_AUDIT.md`](../art/G09-02_VISUAL_AUDIT.md)。

`G09-03` 新增 5/5 目标用例，覆盖连续主线引导、世界任务标记、真实丹炉入口、
键鼠/手柄 control 解析与 Modal 输入抑制。全量 34/34 + 221/221、134,696,660-byte
macOS Release、Boot smoke 通过；发布 Player 使用隔离存档连续 3 次完成相同
55 步旅程，覆盖青石、苍梧、黑风、死亡复生与存读档。

`G09-07` 静态与目标范围通过 `tools/validate_g09_07.sh` 和
`G09-07-playmode.xml` 8/8：覆盖模块化主角、
守卫、药师、隐士、山贼的资源/动作/材质绑定，主角 Attack/Roll/Death，狼 Idle/死亡，
以及新石将军的作者部件、玉核材质、14 clips、非零动作曲线和运行时 Idle。五个人形
manifest 均为 44 bones、≥11 clips、脚底误差 0；石将军为 7,977 tris、58 bones、
14 clips、脚底误差 0。随后全量 34/34 + 234/234、独立 Metal 20 角色门禁
4762.8fps / 253.1MiB、162,358,884-byte macOS Release、Boot smoke 与单窗口
自动退出 Player 固定机位通过。许可证和全部运行时资源哈希通过。

## 进行中 Goal 证据

`G09-08` 已领取，尚未产生可签核证据。

## G02 代码证据

G02 已开发并签核，不应重新领取：

- 突破与 pity：`Assets/_Project/Scripts/Systems/Cultivation/CultivationManager.cs`
- 炼体：`Assets/_Project/Scripts/Systems/Cultivation/BodyRefinementManager.cs`
- 属性聚合：`Assets/_Project/Scripts/Entities/Player/PlayerStats.cs`
- 角色面板：`Assets/_Project/Scripts/Systems/UI/Cultivation/CharacterPanelView.cs`
- 目标测试：`Assets/_Project/Tests/PlayMode/VerticalSlice/G0201PlayModeTests.cs` 至 `G0204PlayModeTests.cs`

## 全局验收债务结论

- 旧 90/97 已清偿：最终全量为 202/202，G00 / G-VS / G01 已统一签核为 `done`。
- [`../GVS08_VERTICAL_SLICE_CHECKLIST.md`](../GVS08_VERTICAL_SLICE_CHECKLIST.md) 保留为可选真人手感复核，不再是代码 MVP 门禁。

只有新回归失败能推翻对应 `done`；更新状态时必须同时记录新的 XML 路径和失败原因。
