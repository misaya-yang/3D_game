# G09-01 · UI/交互联合审计与改造验收

> 审计时间：2026-07-16  
> 证据来源：当前仓库重新构建的 macOS Release Player；除主菜单小窗口为本机窗口截图外，其余均由同一 Player 在 1280×720 下通过仅命令行启用的状态入口截取。所有图片均已逐张打开检查。  
> 审计模式：UX + Accessibility 风险联合审计；同视口改造后证据补录于文末。

## Audit scope

主流程：启动 → 主菜单 → 灵根选择 → 青石村探索 HUD → 背包 / 角色 / 功法 / 任务 / 地图 → 暂停。后续补充对话、商店、炼丹、战斗、死亡与设置的改造后证据。

用户目标：第一次启动后，玩家无需猜测即可进入游戏、理解当下状态、找到首个目标，并能可靠地用键鼠或手柄打开、操作和退出所有核心界面。

## 编号步骤与健康度

| Step | 截图 | 状态 | 结论 |
|---|---|---|---|
| 1 | [`01-main-menu-small-window.png`](previews/before/01-main-menu-small-window.png) | 差 | 只有标题和一个很小的按钮；没有视觉锚点、输入提示、焦点反馈或失败反馈。Retina 小窗口下按钮实际显示高度不足。 |
| 2 | [`03-root-1280x720.png`](previews/before/03-root-1280x720.png) | 危险 | 五系按钮第一次点击就写入不可逆存档，而“踏上仙途”仅负责关闭面板；文案表达的确认语义与代码不一致。旧 HUD 与快捷栏仍在背后。 |
| 3 | [`02-qingshi-hud-1280x720.png`](previews/before/02-qingshi-hud-1280x720.png) | 差 | 修为与人物资源拆成两块不透明黑盒；快捷栏过宽；区域调试色块和 3D 地面文字成为主要画面，底部区域板遮挡视野。 |
| 4 | [`04-inventory-1280x720.png`](previews/before/04-inventory-1280x720.png) | 危险 | 50 个“空”形成噪声；常驻快捷栏 sorting order 高于背包，压住选择说明和操作按钮。空状态没有获得物品的下一步。 |
| 5 | [`05-character-1280x720.png`](previews/before/05-character-1280x720.png) | 差 | 属性缺少分组和视觉扫描顺序；快捷栏覆盖底部操作区；突破不可用原因埋在小字里。 |
| 6 | [`06-skill-1280x720.png`](previews/before/06-skill-1280x720.png) | 一般偏差 | 唯一可见说明依赖拖拽，缺少键盘/手柄等价路径；内容稀少但画布巨大；快捷栏与面板没有形成清楚的装备关系。 |
| 7 | [`07-quest-1280x720.png`](previews/before/07-quest-1280x720.png) | 差 | 当前只显示日常时没有主线空状态解释；关闭按钮被快捷栏覆盖；任务层级和奖励状态不够明显。 |
| 8 | [`08-map-1280x720.png`](previews/before/08-map-1280x720.png) | 一般偏差 | 名为“山河舆图”但实质是传送列表；已选、锁定和可传送只靠低对比文字颜色区分。 |
| 9 | [`09-pause-1280x720.png`](previews/before/09-pause-1280x720.png) | 一般 | 基础动作完整，但四个按钮权重完全相同；“设置”只是 Toast，不是真设置；没有可见键盘/手柄焦点。 |

## Strengths

- 所有核心系统已有稳定 Panel ID、ServiceLocator 查询和 EventBus 刷新路径，适合在不改玩法数值的前提下换皮和重排。
- 主界面中文语气一致，深青、金色和米白已经形成可延续的仙侠色彩方向。
- 背包、角色、功法、任务和地图已有明确关闭按钮与输入锁代码，重构不需要推倒业务系统。

## UX risks

### P0 / P1

1. 灵根按钮在确认前已经永久写档，属于不可逆选择的确认语义错误。
2. HUD 快捷栏 sorting order 为 230，高于背包 200、角色 210、功法/任务/地图 220，真实遮挡操作按钮。
3. `WorldAreaMarker` 把任务触发区域渲染成巨大色板和 3D 文字；这不是地图美术，而是开发调试辅助泄漏到发行体验。
4. 面板打开后没有统一初始焦点和可见 selected 状态，手柄/键盘可达性无法成立。

### Structural

- 颜色、按钮、面板、字号在每个 View 内重复硬编码，修改一次不能全局收敛。
- HUD 同时重复显示灵力；修为、人物资源、货币和快捷栏缺少统一对齐基线。
- 多数空状态只显示“空”或大片空白，没有告诉玩家为何为空、接下来去哪里。
- 主菜单启动失败无反馈，加载中也没有明确状态。

## Accessibility risks

- 截图可确认：小窗口正文和按钮显著缩小；低对比灰绿文字较多；缺少可见焦点；多个状态只靠颜色区分。
- 代码可确认：此前运行时 `InputSystemUIInputModule` 未显式绑定默认 UI actions，面板也没有统一首焦点策略。
- 尚不能从截图确认：VoiceOver 语义、长时间运动敏感、色弱模拟、不同手柄型号、所有文本在 200% UI 缩放下的裁切。这些必须靠运行时输入与尺寸测试，不能宣称 WCAG 合规。

## Opportunity areas / recommendations

1. 建立 `RuntimeUiTheme`，把色彩、层级、CC0 九宫格面板、按钮状态、图标、文字角色和焦点策略收为单一入口。
2. 主菜单加入真正的视觉锚点、足够大的主动作、输入提示、加载/失败反馈，并默认聚焦主动作。
3. 灵根改为“预览 → 可反复比较 → 确认后写档”，创角期间隐藏全部旧 HUD。
4. 普通全屏面板打开时隐藏 HUD；功法面板只保留用于装备的快捷栏；所有面板首焦点可见。
5. 隐藏区域调试板和 3D 标签，进入区域时改用 2.4 秒本地化 Toast。
6. 用真实图标、分组、容量/空状态和主次按钮提高扫描效率；设置必须变成可操作功能。

## Evidence limits and verification gaps

- Computer Use 能看到 Unity Player 窗口，但 Unity 渲染区没有 AX 子控件，因此按钮语义和焦点必须通过真实键盘/手柄行为与代码测试补证。
- 改造前截图没有覆盖对话、商店、炼丹、死亡与设置子流程，因此这些界面只提供改造后真实 Player 证据，不伪造前后对照。

## 改造后同视口对照

除主菜单小窗口外，下表改造后截图均来自本 Goal 重新构建的同一 macOS Release Player，视口为 1280×720。图片已逐张打开检查，不以“成功生成截图”代替视觉验收。

| 流程 | 改造前 | 改造后 | 结果 |
|---|---|---|---|
| 主菜单 | [`before/01`](previews/before/01-main-menu-small-window.png) | [`after/01`](previews/after/01-main-menu-small-window.png) | 生成式远景形成视觉锚点；主动作、输入提示、加载中与失败状态可见。 |
| 灵根 | [`before/03`](previews/before/03-root-1280x720.png) | [`after/02`](previews/after/02-root-1280x720.png) | 改为可反复预览，只有“踏上仙途”确认后才写档；选择期间隐藏 HUD。 |
| 探索 HUD | [`before/02`](previews/before/02-qingshi-hud-1280x720.png) | [`after/03`](previews/after/03-hud-1280x720.png) | 状态集中到左上、六个可点击导航在右上、快捷栏缩小；开发区域板和 3D 标签不再渲染。 |
| 背包 | [`before/04`](previews/before/04-inventory-1280x720.png) | [`after/04`](previews/after/04-inventory-1280x720.png) | 50 个“空”噪声移除，容量、空状态与下一步提示清楚，HUD 不再遮挡。 |
| 角色 | [`before/05`](previews/before/05-character-1280x720.png) | [`after/05`](previews/after/05-character-1280x720.png) | 境界、炼体和战斗属性分组，突破不可用原因位于主动作附近。 |
| 功法 | [`before/06`](previews/before/06-skill-1280x720.png) | [`after/06`](previews/after/06-skill-1280x720.png) | 已学、已选与升级区分清楚；仅该面板保留快捷栏用于装备。 |
| 任务 | [`before/07`](previews/before/07-quest-1280x720.png) | [`after/07`](previews/after/07-quest-1280x720.png) | 两栏层级稳定，目标进度和领取状态不再与关闭动作争抢空间。 |
| 地图 | [`before/08`](previews/before/08-map-1280x720.png) | [`after/08`](previews/after/08-map-1280x720.png) | 可传送、已选与锁定同时由图标、文字和控件状态表达。 |
| 暂停 | [`before/09`](previews/before/09-pause-1280x720.png) | [`after/09`](previews/after/09-pause-1280x720.png) | 恢复、设置、主菜单与退出有主次和图标；初始焦点可见。 |

## 新增流程证据

| 状态 | 真实 Player 截图 | 检查结论 |
|---|---|---|
| 设置 | [`10-settings`](previews/after/10-settings-1280x720.png) | 三路音量可预览、取消可回滚、应用后原子写入全局设置；窗口/全屏可切换。 |
| 炼丹与首次引导 | [`11-alchemy`](previews/after/11-alchemy-1280x720.png) | 丹方、等级、材料、成功率和主动作均可见；首次引导用挖洞遮罩说明下一步。 |
| 商店 | [`12-shop`](previews/after/12-shop-1280x720.png) | 买卖分栏、余额和价格可扫描；空背包只保留安静空槽与出售说明。 |
| 对话 | [`13-dialogue`](previews/after/13-dialogue-1280x720.png) | HUD/导航自动退场，发言者、正文、选择/继续提示位于稳定底部区域。 |
| 死亡 | [`14-death`](previews/after/14-death-1280x720.png) | 原因、5% 修为损失与唯一复生动作清楚，其他 HUD 全部隐藏。 |
| 加载 | [`15-loading`](previews/after/15-loading-1280x720.png) | 使用同一远景与主题面板，进度条和百分比同时表达状态。 |
| BOSS 战斗 | [`19-combat`](previews/after/19-combat-1280x720.png) | BOSS 名称、阶段、血条、锁定圈、玩家资源和快捷栏同时可读；场景美术问题转入 G09-02。 |

1920×1080 复核：[`HUD`](previews/after/16-hud-1920x1080.png)、[`背包`](previews/after/17-inventory-1920x1080.png)、[`设置`](previews/after/18-settings-1920x1080.png) 均无关键裁切、重叠或小于 40 px 的主操作。

## 已关闭的 P0 / P1 与真实漏洞

1. 灵根选择从“按钮即永久写档”改为“预览 → 明确确认 → 写档”；随机稀有灵根也以同一种子精确提交预览结果。
2. 面板互斥、HUD/快捷栏 sorting 遮挡和首焦点统一由 `UIManager`、`RuntimeUiTheme` 与启用默认 actions 的 `InputSystemUIInputModule` 管理。
3. 世界区域调试板与 3D 文字不再进入发行画面，区域名称改为短时本地化 Toast。
4. “设置”从占位 Toast 变为真实音量、显示模式、取消回滚与原子 JSON 持久化。
5. 修复 `AudioStateController` 持有已销毁 `AudioSource` 时的空引用，并增加目标回归。
6. 修复对话结束/应用退出时继续访问已销毁 `PlayerInputReader` 的 `MissingReferenceException`；输入源、对话管理器和对话 View 均加入 Unity 对象存活检查。
7. 对话、死亡、加载等非游玩状态现在会关闭状态 HUD、顶部导航和快捷栏，返回 Playing 后再恢复。

## 验收边界

- Unity 自动化已验证默认 UI actions 已绑定并启用、六个导航按钮可点击、面板互斥与焦点存在、主菜单按钮可完成 MainMenu → Loading → 青石村流程。
- 本机 Computer Use 可检查原生 Player 渲染，但其合成点击/按键不会进入 Unity Input System，因此不能伪称已完成物理手柄真人走查；物理设备主观手感仍是发布前人工检查项。
- 正式角色、场景、动画和关卡终稿属于 Goal 的 `out_of_scope`，本轮只保证当前低成本美术下 UI 可读、流程可操作且无已知 P0/P1 软锁。
