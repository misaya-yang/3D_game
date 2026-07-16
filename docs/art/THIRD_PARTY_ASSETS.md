# 第三方美术资产清单

> 最后筛选：2026-07-16  
> 原则：优先 CC0；仓库只保留 Unity 当前使用的模型、贴图、许可证与可重复同步信息，不纳入重复的 Blend、OBJ、glTF 和整包预览。

## 来源与授权

| 包 | 当前用途 | 上游页面 | 授权 | 仓库内凭据 |
|----|----------|----------|------|------------|
| Quaternius Universal Base Characters | 主角、守卫、药师、隐士、山贼的头脸、眼睛、眉毛与作者头发 | <https://quaternius.com/packs/universalbasecharacters.html> | CC0 1.0 | `Quaternius_Universal_Base_Characters_CC0.txt` |
| Quaternius Modular Character Outfits - Fantasy | 五个人形角色的 Peasant/Ranger 模块衣装 | <https://quaternius.com/packs/modularcharacteroutfitsfantasy.html> | CC0 1.0 | `Quaternius_Modular_Character_Outfits_Fantasy_CC0.txt` |
| Quaternius RPG Character Pack | 五个人形角色的动作骨架、剑、弓、法杖和匕首；旧职业整模只作源资产 | <https://quaternius.com/packs/rpgcharacters.html> | CC0 1.0 | `Quaternius_RPG_Character_Pack_CC0.txt` |
| Quaternius Ultimate Animated Animal Pack | 灰狼与精英狼共用的 Wolf 网格，以尺寸和阵营色区分 | <https://quaternius.com/packs/ultimateanimatedanimals.html> | CC0 1.0 | `Quaternius_Ultimate_Animated_Animal_Pack_CC0.txt` |
| Quaternius Ultimate Monsters | 石将军使用 `Orc_Skull` 动画网格、58-bone 骨架和狼牙锤 | <https://quaternius.com/packs/ultimatemonsters.html> | CC0 1.0 | `Quaternius_Ultimate_Monsters_CC0.txt` |
| Kenney Nature Kit | 树木、岩石、竹子、桥、营地、道路、围栏、遗迹；石将军叠加三类作者岩石部件 | <https://kenney.nl/assets/nature-kit> | CC0 1.0 | `Kenney_Nature_Kit_CC0.txt` |
| Kenney Modular Dungeon Kit | 黑风洞五层房间、门、走廊与墙体模块 | <https://kenney.nl/assets/modular-dungeon-kit> | CC0 1.0 | `Kenney_Modular_Dungeon_Kit_CC0.txt` |
| Poly Haven | 青石村草土路、苍梧山林地、黑风洞岩地的 1K diffuse | <https://polyhaven.com/textures> | CC0 1.0 | `Poly_Haven_CC0_Sources.txt` |

许可证位于 `Assets/_Project/Art/ThirdParty/Licenses/`。这些资产均允许个人和商业项目使用且不要求署名；本项目仍保留作者、页面与哈希，方便审计和后续替换。

## 当前精选内容

```text
Assets/_Project/Resources/Art/Budget/
├── Characters/    # 五个模块化成品 + 6 个动作/武器源角色 FBX
│   └── CultivatorTextures/ # 五个人形共用的皮肤、眼睛、头发、布衣、Ranger BaseColor
├── Creatures/     # Wolf、生成后的 StoneGeneral；Goleling 仅保留历史审计兼容
├── Nature/        # 41 个青石/苍梧环境和交互物模型
├── Dungeon/       # 12 个黑风洞模块 + colormap
└── Surfaces/      # 3 张 Poly Haven 1K diffuse JPG

Assets/_Project/Art/ThirdParty/
├── Licenses/      # 原始许可证与 Poly Haven 逐资产来源
└── SHA256SUMS     # 当前精选美术内容的 SHA-256
```

当前合计至少 61 个 FBX。运行时通过 `BudgetArtCatalog` 的语义路径取用，不把玩法 ID 绑定到第三方文件名。玩家、NPC、狼、精英、石将军和人形敌人都只替换视觉子节点；已有根对象、碰撞体、任务/敌人 ID 与交互组件保持不变。

## 可重复同步

```bash
./tools/art/sync_budget_assets.sh
./tools/art/sync_character_sources.sh
```

第一个脚本同步环境、动作/武器源角色、生物精选集和石将军 `Orc_Skull` 源文件；
第二个脚本通过 itch.io 的免费 Standard 下载流程固定校验两个人形角色包哈希，
只提取 G09-07 当前使用的模型、贴图与许可证。
下载缓存默认位于 `/tmp/wendao-assets`，可用 `WENDAO_ASSET_CACHE` 改写。验证不会依赖缓存：

```bash
shasum -a 256 -c Assets/_Project/Art/ThirdParty/SHA256SUMS
```

## 使用边界

- 这些 CC0 内容是统一低模方向的可发布替身，不冒充原创 4K 终稿。
- 主角的两个完整上游 zip 只留在缓存；仓库只提交生成 FBX、项目 Blend/manifest、
  五张实际使用的 BaseColor、许可证和哈希。
- 石将军源 `Orc_Skull.fbx` 只留在缓存；仓库提交生成后的 Blend/FBX/manifest，
  manifest 固定源文件和三类 Kenney 岩石的 SHA-256。
- Poly Haven 只保留实际使用的 1K diffuse；法线、置换、粗糙度和预览图不入库。
- 现有 Kenney 低模网格通过项目侧的石材、环境、地牢和阵营材质分级统一色域；关键碰撞仍由灰盒层负责。
- 后续正式替换时保持 `BudgetArtCatalog` 语义路径，或同步修改映射与测试，不修改玩法内容 ID。
