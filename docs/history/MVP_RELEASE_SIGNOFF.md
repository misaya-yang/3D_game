# MVP Release 签核记录

> 签核日期：2026-07-15  
> Unity：6000.5.3f1  
> 目标：macOS Universal（arm64 + x86_64）  
> 结论：MVP 代码、自动化验收与可运行 Release 构建完成。

## 01§8 验收映射

| MVP 条目 | 结果 | 可复现证据 |
|----------|------|------------|
| 垂直切片 8 步 | 通过 | G-VS-01—08 + 全量 PlayMode 202/202 |
| 第一章十环主线 | 通过 | `FreshSaveCompletesChapterAndRoundTripsCriticalMvpState` |
| 黑风五层 checkpoint / 石将军 | 通过 | G05-03、G04-03、G04-05 回归 |
| 练气→筑基→金丹 | 通过 | G02-01、G05-04 与 G07-04 新档综合回归 |
| 三图奇遇 once Flag | 通过 | G05-06 三图配额、奖励、读档不重播回归 |
| 三图往返 | 通过 | `AllThreeMvpMapsLoadWithRuntimePlayerAndWorldRoots` |
| 任务/背包/境界/教程/checkpoint 存读档 | 通过 | G07-04 全新档综合 round-trip |
| 稳定性 | 通过 | G07-03 3,600 模拟秒：60 save、6 load、0 Error/Exception |
| Phase 0—7 状态 | 通过 | `docs/10_GOALS.md` 全部 `done` |

死亡惩罚、最近传送点复活、HP/Mana 回满和修为持久化由
`G0104PlayModeTests.DeathPenalizesOnceShowsUiPersistsAndRespawnsAtNearestPoint`
覆盖，并包含在最终 202/202 中。

## 最终自动化结果

- EditMode：**34/34**，`TestResults/G07-04-editmode.xml`
- PlayMode：**202/202**，`TestResults/G07-04-playmode.xml`
- G07-03 Metal 性能：**1/1**，`TestResults/G07-03-performance.xml`
- Player 启动冒烟：`TestResults/G07-04-smoke.json`
- 静态/测试/构建/启动统一入口：`./tools/validate_g07_04.sh`

旧记录中的 90/97 全量失败已清偿：早期 Editor-only 组件探针被移入
`UNITY_INCLUDE_TESTS` 运行时支持程序集；同帧输入测试改为确定性时间片；全项目已迁移
Unity 6.5 的对象查询 API。

## Release 构建

- 产物：`Builds/macOS/WendaoChangsheng.app`
- 产品名：`问道长生`
- Bundle ID：`com.wendao.changsheng`
- 架构：Universal `arm64 + x86_64`
- 大小：118,836,454 bytes（约 114 MiB）
- 场景：Boot、MainMenu、Loading、青石原、苍梧山脉、黑风秘境，共 6 个
- BuildPipeline：Succeeded，0 error；3 条 Unity ILPP/构建链工具提示，无 C# warning
- `codesign --verify --deep --strict`：通过；当前为本机 ad-hoc 签名
- Player 冒烟：Unity 6000.5.3f1 初始化、Boot 场景载入、配置非 safe mode、无阻断异常

四份数值 JSON 已迁至 Unity 规定的 `Assets/StreamingAssets/Config`，并在最终 `.app`
的 `Contents/Resources/Data/StreamingAssets/Config` 中核验存在。此前嵌套在
`Assets/_Project/StreamingAssets` 导致 Player safe mode 的发布问题已修复。

## 已知非阻断项

1. 当前地图、角色、音画仍以规格允许的灰盒和占位资产为主；正式美术替换不属于代码 MVP。
2. 产物尚未使用 Apple Developer ID 签名或 notarization；商店/公开分发属于 G07-04 的 out of scope。
3. 自动化覆盖功能、状态、性能代理和稳定性；真人对镜头、打击感、文案节奏的主观手感复核仍可按
   `docs/GVS08_VERTICAL_SLICE_CHECKLIST.md` 执行，但不再阻塞代码 MVP 状态。
4. Release 启动冒烟使用 NullGfx batch player；真实 Metal 性能证据由 G07-03 独立图形测试提供。

## 磁盘策略

最终 `.app` 与 XML/JSON 证据保留；`Library/`、`Logs/`、`Temp/`、`obj/` 和测试日志在签核后删除。
需要继续开发时 Unity 会自动重建 `Library/`，需要重建发布包时运行 `./tools/build_macos.sh`。
