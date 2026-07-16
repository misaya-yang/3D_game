# G09-07 · 角色视觉审计、方向冻结与生成管线

> 状态：`done`  
> 视觉基准：[`player_wanderer_turnaround_v1.png`](reference/player/player_wanderer_turnaround_v1.png)  
> 主角基准：[`01-cultivator-modular-v2.png`](previews/g09-07/final/01-cultivator-modular-v2.png)  
> BOSS 基准：[`02-stone-general-open-source-v2.png`](previews/g09-07/boss/02-stone-general-open-source-v2.png)  
> 原则：成熟底模负责人体、服装和头发质量；项目 Python 只做可重复的选型、组装、配色、转绑、校验与导出。

## 结论

旧主角不是“差一点精修”，而是资产等级错误：身体主体只有 823 个顶点，脸、手、
衣服和头发没有足够拓扑承载年轻游侠修士。继续叠方块、圆锥和纯色材质只能稳定地产出
“方块人”。

本轮做过两次技术验证：

1. MPFB 2.0.16 能在 Blender 5.2 中稳定脚本化生成人体，证明自动化链路可行。
2. MPFB 人体外再手搓衣服和头发，视觉仍然像技术样机，不达到本项目角色标准。

因此人形生产链已切换到 Quaternius 的 CC0 通用人体与模块化奇幻服装。主角、守卫、
药师、隐士和山贼共用成熟头脸、眼睛、头发与模块衣装，再分别转绑到仓库已有的
44-bone 职业动作骨架。五者共享拓扑和贴图规范，但通过衣装、武器、轮廓与阵营色区分，
不再直接把 Monk/Ranger/Cleric/Wizard/Rogue 当正式角色外观。

旧石将军同样属于资产等级错误：它是 Warrior 人形换成灰色后再叠基础几何。当前版本
改用 Ultimate Monsters 的 `Orc_Skull` 成熟网格、58-bone 动作骨架和狼牙锤，再叠加
Kenney 作者岩石部件、暗色石材和小面积青玉核心。MPFB 保留为特殊人体备用，不承担
正式衣装；旧方块人、混合人形与旧石将军生成脚本和中间截图已经清理。

## 原画到模型的硬约束

### 身份与轮廓

- 20 岁上下的年轻男性，不留胡须，不使用头盔或遮脸兜帽。
- 约 7.2—7.5 头身；手掌、护腕和肩甲不得压过头脸成为最大识别点。
- 深色束发或干净分发必须露出眼睛；不再接受头套、圆锥发束或遮脸发帘。
- 灰蓝布衣、深色宽裤、皮革护腕/靴子和单侧肩甲形成轻装游侠层次。
- 直剑贴背放置，不遮脸、不穿过镜头，也不替换玩法武器与碰撞。

### 与 UI 的统一

角色沿用运行时 UI 已冻结的“墨黑、青玉、旧金、米白”语言：

- 主体：低饱和灰蓝与炭黑。
- 皮革：棕黑，不使用高饱和橙色。
- 金属：低亮钢与旧铜，不做塑料高光。
- 灵力点缀：只允许小面积青玉色。

运行时由 `CultivatorPlayerStyle` 与 `ModularCharacterStyle` 将五张 CC0 BaseColor
映射到皮肤、眼睛、头发、布衣和 Ranger 部件，再按玩家、守卫、药师、隐士、山贼
分别施加色板。`StoneGeneralStyle` 单独恢复暗石层级和青玉发光核心。贴图细节得以
保留，色域仍与 UI、场景和阵营色一致。

## 资产预算

| 项 | LOD0 目标 | 当前 |
|---|---:|---:|
| 单角色总计 | ≤52k tris | 30,981 tris |
| 头脸 | ≤9k tris | 4,888 tris（头、眼、眉） |
| 头发 | ≤9k tris | 1,301 tris |
| 衣装与身体可见部件 | ≤26k tris | 24,080 tris |
| 武器与附件 | ≤8k tris | 712 tris |
| 骨骼 | 复用现有动作骨架 | 44 bones |
| 动作 | Idle/Run/Attack/Roll/Hit/Death | 11 clips |
| 脚底误差 | ≤角色高 3% | 0 |

这仍是第三人称 ARPG 的中等面数角色，不冒充原创 4K 雕刻；目标是近景像正常角色，
战斗距离能先读出身份和阵营。

### 当前成品预算

| 角色 | Tris | Bones | Clips | 脚底误差 | 识别点 |
|---|---:|---:|---:|---:|---|
| 主角 | 30,981 | 44 | 11 | 0 | 灰蓝轻装、单肩甲、背剑 |
| 青石守卫 | 32,315 | 44 | 14 | 0 | Ranger 全装、弓、冷青灰 |
| 药师 | 20,377 | 44 | 15 | 0 | 软布衣、法杖、药草绿 |
| 隐士 | 19,959 | 44 | 15 | 0 | 软布衣、法杖、银发蓝灰 |
| 山贼 | 32,463 | 44 | 12 | 0 | 兜帽、双层皮革、匕首、暗红 |
| 石将军 | 7,977 | 58 | 14 | 0 | 巨型骷髅石躯、狼牙锤、青玉核 |

## 当前 P0 / P1

| 级别 | 问题 | 当前处理 |
|---|---|---|
| P0 | 旧主角近景像面罩和方块人 | 已替换为 15,997 顶点模块化模型 |
| P0 | 头发遮脸或由圆锥拼成 | 已改为作者网格，并由管线自动抬升刘海露出眼睛 |
| P0 | 皮肤、衣装和护具共用错误材质 | 已拆为 5 张贴图和角色专用运行时映射 |
| P1 | 手掌、护腕比例像玩具 | 已采用完整手部和 Ranger 护腕拓扑 |
| P1 | 武器离身、挡镜头 | 静态位置已收回背部；Player 固定机位与 Attack/Roll/Death 回归均未出现阻断镜头 |
| P1 | NPC 和人形敌直接复用旧职业模型 | 已切换四个模块化变体，并接入独立色板与武器 |
| P1 | 石将军像换色人形/方块盔甲 | 已切换开源怪物骨架、作者岩石部件和玉核轮廓 |
| P1 | 精英狼只靠文字区分 | 以 24% 更大体型、褐红阵营色和同骨架攻击动作区分；20 角色 Metal 队列与运行时动作回归通过 |

## 开源方案筛选

| 方案 | Blender 5.2 实测与维护状态 | 决策 |
|---|---|---|
| MB-Lab 1.8.1 | 2024 年归档，开发迁移到 CharMorph | 不进入生产链 |
| CharMorph 0.4.0-3 | 插件可注册，但发行包不含人物库 | 备用交互工具 |
| MPFB 2.0.16 | 可脚本创建 19,158 顶点人体和 Unity 骨架 | 特殊人体/NPC 备用 |
| Universal Base Characters | Standard 包含可用头脸、眼睛和头发，CC0 | 生产底模 |
| Modular Character Outfits - Fantasy | Standard 包含 Ranger/Peasant 模块，CC0 | 生产衣装 |
| RPG Character Pack | 项目已有完整动作与武器，CC0 | 动作骨架与剑来源 |
| Ultimate Monsters | 50 个完整动画怪物；实测筛选 BlueDemon/Orc_Skull/Yeti | `Orc_Skull` 作为石将军成熟底模 |
| Kenney Nature Kit | 已有 CC0 岩石部件 | 石将军肩甲、胸甲、护臂和冠石 |

上游包哈希和实际选用文件哈希写入
[`Cultivator_Modular_v2_manifest.json`](../../ArtSource/Characters/Player/Cultivator_Modular_v2_manifest.json)。
Unity 使用的许可证和精选内容同时纳入 `ThirdParty/SHA256SUMS`。

## 单一生产管线

权威配置：
[`g09_07_modular_characters.json`](../../tools/blender/config/g09_07_modular_characters.json)

人形执行入口：

```bash
./tools/blender/run_character_pipeline.sh --profile cultivator
./tools/blender/run_character_pipeline.sh --profile npc_guard
./tools/blender/run_character_pipeline.sh --profile npc_healer
./tools/blender/run_character_pipeline.sh --profile npc_hermit
./tools/blender/run_character_pipeline.sh --profile human_enemy
```

BOSS 执行入口：

```bash
./tools/art/sync_budget_assets.sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup \
  --python tools/blender/build_stone_general.py
```

管线固定执行：

1. 校验并缓存两个 CC0 Standard 包，只提取当前实际选用文件。
2. 导入通用男性头脸、眼睛、眉毛和作者头发。
3. 按 profile 组合 Peasant/Ranger 身体、裤子、手臂、护腕、靴子、肩甲或兜帽。
4. 自动调整头宽、下颌、脸深和刘海，不手搓可见人体。
5. 统一朝向与比例，将所有可变形部件转绑到现有 Monk 动作骨架。
6. 按角色保留剑、弓、法杖或匕首；石将军保留怪物狼牙锤。
7. 导出 Blend、FBX、manifest；将精选贴图和许可证同步到 Unity。
8. 检查三角面、骨骼、动作、脚底、材质槽、非有限坐标和输出哈希。

MPFB 备用入口为：

```bash
./tools/blender/run_mpfb_character_pipeline.sh --profile cultivator
```

## 截图与窗口纪律

- 日常迭代使用 manifest、FBX 审计和 Unity 自动化测试，不启动可见窗口。
- Blender 只在候选发生实质变化时生成一张 768×768 离线图。
- Unity Player 最终验收只开一个窗口；不并行启动多个 Player 或 Blender GUI。
- Blender MCP 只用于最后的小范围交互精修，不作为批量生产和验收依赖。

## 最终签核证据

- Blender 5.2：五个人形均 ≤32,463 tris、44 bones、≥11 clips、脚底误差 0；
  石将军为 7,977 tris、58 bones、14 clips、脚底误差 0。
- Unity 导入：57 个 Transform；`CultivatorArmature`、`Cultivator_Head`、
  `Cultivator_Hair`、`Cultivator_Jian` 和动作曲线路径完整。
- 石将军 Unity 导入包含 `StoneGeneral_Body`、`StoneGeneral_Maul`、
  `StoneGeneral_Core`，14 个 legacy clip 均有非零曲线。
- G09-07 目标 PlayMode：8/8 通过；覆盖主角 Idle/Attack/Roll/Death、五张贴图、
  四个人形变体、NPC/人形敌/狼/BOSS Idle、BOSS 曲线绑定和狼死亡动作。
- 全量回归：EditMode 34/34、PlayMode 234/234。
- 独立 Metal 性能门禁：1920×1080 Medium、20 个角色、196 个 Renderer，
  平均 4762.8fps、最差帧 0.32ms、内存 253.1MiB，超过 30fps / 6GiB 门槛。
- macOS Release：162,358,884 bytes，9.47s，0 error；Boot Player 冒烟通过。
- 最终 Player 固定机位串行启动、单窗口、自动退出：
  [`02-full-character-lineup-1920x1080.png`](previews/g09-07/final/02-full-character-lineup-1920x1080.png)，
  SHA-256 `11bc221eadecc725949325afac032168bb58695ed5a3baf9cf21f26bd3340a41`。
- 原有玩法根对象、碰撞体、敌人/任务 ID 与战斗数值未改。
- 旧 `.blend1`、失败方块人/混合人形生成物和重复工作截图已删除；仓库只保留当前
  生产 Blend、FBX、manifest、主角基准、BOSS 基准和三张失败前对照。

## 签核结论

G09-07 已达到 `done`。当前结果是可运行、可复现、许可完整的中等面数生产基线，
不等同于原画级原创 4K 终稿；后续若提升主角近景写实度，应在同一配置管线中替换
更高等级底模，而不是重新回到基础几何手搓。
