# 10 · 开发进度（唯一恢复入口）

> **更新时间**：2026-07-15  
> **用途**：这里只记录“现在做到哪里、下一步是什么”。  
> Goal 的范围、依赖和验收标准仍在 [`10_GOALS.md`](10_GOALS.md)；机制与数值仍以对应系统文档为准。

## 5 秒结论

- **MVP Phase 0—7 已全部完成，没有待领取 Goal。**
- 最终结果：EditMode 34/34、PlayMode 202/202、Metal 性能 1/1、macOS Release 与 Player 启动冒烟通过。
- 最终签核和已知非阻断项见 [`history/MVP_RELEASE_SIGNOFF.md`](history/MVP_RELEASE_SIGNOFF.md)。
- 历史 XML 与验证脚本见 [`history/TEST_EVIDENCE.md`](history/TEST_EVIDENCE.md)。

## 现在从这里继续

| 项 | 当前值 |
|----|--------|
| 当前 Goal | `G07-04` |
| 名称 | MVP 总验收 |
| 状态 | `done` |
| 验收规格 | [`01_VISION_MVP.md`](01_VISION_MVP.md) §8、[`history/MVP_RELEASE_SIGNOFF.md`](history/MVP_RELEASE_SIGNOFF.md) |
| 已落地 | 全新档十环主线/三图/死亡/存读档综合回归；全量 34/34 + 202/202；Universal macOS Release（约 114MiB）；Player 配置与 Boot 冒烟；所有 Goal 最终状态 `done` |
| 未完成 | 无代码 MVP 阻断项 |
| 下一动作 | 可直接试玩 `Builds/macOS/WendaoChangsheng.app`；后续正式美术、Developer ID/notarization 另开 Post-MVP Goal |
| 后继选择 | 无；MVP 已完成 |

若开启 Post-MVP 开发，按以下顺序读取：

1. 本文件的“现在从这里继续”。
2. 先新增一张范围明确的 Goal 卡片，再将它标为唯一 `in_progress`。
3. 只读取新卡片 `refs` 指向的具体章节。

不要为恢复上下文重复通读全部规格，也不要从旧设计稿里的示例 `status` 推断进度。

## 已交付总览

| 范围 | 状态 | 说明 |
|------|------|------|
| G00 / G-VS / G01 | `done` 19/19 | 最终全量 34/34 EditMode、202/202 PlayMode 已清偿历史验收债务 |
| G02-01—G02-04 | `done` 4/4 | 修炼阶段已完成，证据见 `history/TEST_EVIDENCE.md` |
| G03-01—G03-05 | `done` 5/5 | 物品、技能、炼丹、采集、商店已完成目标回归 |
| G04-01—G04-05 | `done` 5/5 | Phase 4 敌人、BOSS 与掉落经济已全部通过目标回归 |
| G05-01—G05-06 | `done` 6/6 | 三图、副本、主支线、日常与三图奇遇均通过目标回归 |
| G06-01 | `done` | 统一 UI 导航、互斥与事件刷新已通过目标回归 |
| G06-02 | `done` | 八教程挖洞遮罩、输入规则与旧档兼容已通过目标回归 |
| G06-03 | `done` | 灵马、飞剑、禁飞和 mounts.json 已通过目标回归 |
| G06-04 | `done` | 丹鼎宗、10 成就、5 称号和属性/存档闭环已通过回归 |
| G06-05 | `done` | 动画事件、占位音画和三态 BGM 已通过回归 |
| G07-01 | `done` | 昼夜、天气、三图环境与战斗修正已通过回归 |
| G07-02 | `done` | 内容密度、localization、经济闭区间和节拍预算已通过回归 |
| G07-03 | `done` | 边界、性能与 3,600 模拟秒稳定性回归已通过 |
| G07-04 | `done` | MVP 逐项签核、Release 构建和 Player 冒烟已通过 |

所有 MVP Goal 均为 `done`。只有新的可复现回归失败或明确的 Post-MVP 范围才应重新开启开发。

## 已知非阻断项

- 正式美术替换、Apple Developer ID 签名/notarization 与真人主观手感复核不属于代码 MVP；详见最终签核。
- 旧 90/97 记录已由最终 202/202 推翻并清偿。

## 更新规则

每次 Goal 切换只做四件事：

1. 更新本文件“现在从这里继续”的 Goal、状态、已落地、剩余与下一动作。
2. 同步 [`10_GOALS.md`](10_GOALS.md) 对应卡片的 `status`。
3. 验收通过时记录测试结果文件；未运行 Unity 时最多标 `implemented`。
4. 运行 `tools/check_docs_consistency.sh`；开发期只允许一个 `in_progress`，全量完成时允许零个。

当前无后继 Goal；不得靠旧对话记忆猜测 Post-MVP 范围。

若本文件与 Goal 卡片状态冲突，立即运行一致性检查并修正文档；不要猜测。
