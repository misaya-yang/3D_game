# 09 · 内容数据（世界观、表、清单）

> **版本**: v3.1  
> 实现期按表创建 SO/JSON。ID **稳定不可随意改名**（存档依赖）。

---

## 1. 世界观（浓缩）

**背景**：灵气复苏百年，东域散修与宗门并立。上古「问道天碑」破碎，碑纹散落九州。玩家为青石镇孤儿，觉醒灵根后踏上求长生之路。

**主线第一章《青石问道》**（MVP，粗测 **2–4h**）：

| 节 | QuestId | 摘要 |
|----|--------|------|
| 1 | quest_main_01_01 | 药老测试灵根，授《引气诀》 |
| 2 | quest_main_01_02 | 东郊猎狼，取狼毫 |
| 3 | quest_main_01_03 | 灵草涧采「清心草」×5，遇精英灰爪 |
| 4 | quest_main_01_04 | 回镇炼一枚回气丹（引导炼丹） |
| 5 | quest_main_01_05 | 开秘径，往苍梧拜山 |
| 6 | quest_main_01_06 | 苍梧试炼：盘山道杀敌 |
| 7 | quest_main_01_07 | 筑基机缘线索（若发丹：设 foundation_pill_granted） |
| 8 | quest_main_01_08 | **AcceptRewards 筑基丹×1**（药老；若 flag 已发则跳过）+ **pity**；突破筑基 |
| 9 | quest_main_01_09 | **AcceptRewards 凝金丹×1** → Collect(latch) → **ReachRealm GoldenCore** → Reach 黑风入口 |
| 10 | quest_main_01_10 | 击败黑风石将军，第一章完；拓跋渊钩子 |

**quest_main_01_09 目标（有序）**：

1. Collect `item_pill_goldencore`（`LatchOnFirstAcquire=true`；Accept 发放）  
2. ReachRealm `GoldenCore`  
3. Reach `blackwind_entrance`  

入口硬门槛：未金丹显示 `ui_blackwind_gate`。

主线运行时 Flag：`foundation_pill_granted`（历史上是否已发筑基丹）、
`questFlag_main_foundation_pity`（第 8 环筑基保底）、
`quest_flag_main_cangwu_path_open`（第 5 环交付后开启苍梧秘径）。

**筑基丹/凝金丹**：主线发放；杂货店不卖；AcquisitionHintKeys 指向药老主线。

**主题对立**：丹鼎宗 vs 夺碑散修「拓跋渊」（第二章预留）。

---

## 2. NPC 表

| NpcId | 名 | 地图 | 职能 |
|-------|----|------|------|
| npc_yaolao | 药老 | 青石镇 | 主线、卖丹材、教炼丹 |
| npc_zhanggui | 张掌柜 | 青石镇 | 杂货商人 |
| npc_danding_guide | 丹鼎接引 | 青石镇 | 入宗、声望 |
| npc_trainer | 教习武 | 习武坪 | 战斗提示 |
| npc_cangwu_guard | 山门弟子 | 苍梧 | 传送/试炼 |
| npc_hermit | 洞府隐士 | 苍梧洞府 | 支线功法 |
| npc_blackwind_echo | 残魂 | 黑风 B1 | 副本说明 |

---

## 3. 物品表（MVP 最小集）

### 3.1 消耗品

| ItemId | 名 | 效果 | 堆叠 |
|--------|----|------|------|
| item_potion_heal_01 | 回血丹 | Heal 80 | 20 |
| item_potion_heal_02 | 大回血丹 | Heal 250 | 20 |
| item_potion_mana_01 | 回气丹 | Mana 50 | 20 |
| item_potion_xp_01 | 聚气丹 | +300 修为 | 10 |
| item_potion_body_01 | 锻体丹 | +200 炼体 | 10 |
| item_pill_foundation | 筑基丹 | 练气→筑基材料；主线 08 发；hint_quest_main_08_yaolao | 5 |
| item_pill_goldencore | 凝金丹 | 筑基→金丹材料；主线 09 发；hint_quest_main_09_yaolao | 5 |

### 3.2 材料

| ItemId | 名 | 用途 |
|--------|----|------|
| item_mat_wolf_hair | 狼毫 | 任务/卖 |
| item_mat_qingxin_grass | 清心草 | 炼丹 |
| item_mat_spirit_dust | 灵尘 | 通用 |
| item_mat_refine_stone | 精炼石 | 装备精炼 |
| item_mat_beast_core_1 | 一品妖核 | 炼丹/卖 |
| item_mat_black_stone | 黑风石屑 | 副本 |
| item_skill_scroll | 功法残页 | 技能升级 |
| item_quest_hermit_letter | 药老手书 | 隐士支线绑定交付物；不可交易 |

### 3.3 装备

| ItemId / EquipId | 槽 | 境界 | 主属性 |
|------------------|----|------|--------|
| eq_weapon_wood_sword | Weapon | 练气 | Atk+8 |
| eq_weapon_iron_sword | Weapon | 练气中 | Atk+18 |
| eq_weapon_cangwu_blade | Weapon | 筑基 | Atk+40 |
| eq_chest_cloth | Chest | 练气 | Def+5 Hp+20 |
| eq_chest_leather | Chest | 练气 | Def+12 Hp+40 |
| eq_boots_cloth | Boots | 练气 | Move+0.2 Def+3 |
| eq_acc_spirit_ring | Accessory1 | 练气 | Mana+30 Crit+2% |

G-VS-03 代码优先阶段由 `ConfigDatabase` 为 `item_potion_heal_01` 与 `eq_weapon_wood_sword` 注册运行时占位 SO（权威 ID/数值/API 不变）；正式 `Resources/SO` 资产导入后，同 ID 资产优先并替换占位。

### 3.4 货币

灵石：`SpiritStones` 整型字段，非背包物品。显示用图标 `ui_icon_spirit_stone`。

---

## 4. 功法表（≥6）

| SkillId | 名 | 元素 | 类型 | 解锁 |
|---------|----|------|------|------|
| skill_basic_qi_bolt | 引气弹 | None | Active | 主线 01 |
| skill_fire_ember | 星火诀 | Fire | Active | 练气 3 / 商店 |
| skill_ice_needle | 寒针 | Ice | Active | 苍梧 |
| skill_lightning_chain | 引雷索 | Lightning | Active | 筑基 |
| skill_wind_slash | 风刃 | Wind | Active | 隐士支线 |
| skill_pass_iron_skin | 铁肤 | Earth | Passive | 炼体 1 奖励 |
| skill_ult_fire_wave | 炎浪 | Fire | Ultimate | 金丹 / 副本 |

数值默认见 SkillData 字段；设计中心：练气技能伤害 25–40，筑基 60–90，大招 150+。

G03-02 的代码优先占位以此表为权威（正式 `Resources/SO` 同 ID 资产导入后替换占位）：

| SkillId | Base | 每级 | Mana | CD | Cast | Recovery | Range | Projectile | 命中状态 |
|---------|-----:|-----:|-----:|---:|-----:|---------:|------:|------------|----------|
| skill_basic_qi_bolt | 30 | 5 | 10 | 1.5s | 0.2s | 0.3s | 12m | true | — |
| skill_fire_ember | 38 | 7 | 14 | 3s | 0.25s | 0.35s | 10m | true | status_burn |
| skill_ice_needle | 35 | 7 | 12 | 2.5s | 0.2s | 0.3s | 13m | true | status_chill |
| skill_lightning_chain | 75 | 12 | 22 | 7s | 0.35s | 0.45s | 11m | true | status_shock_stun |
| skill_wind_slash | 68 | 10 | 18 | 4.5s | 0.25s | 0.35s | 12m | true | — |
| skill_pass_iron_skin | 0 | 0 | 0 | 0 | 0 | 0 | 0 | false | 被动，不可入栏 |
| skill_ult_fire_wave | 170 | 20 | 40 | 20s | 0.8s | 0.8s | 12m | true | status_burn |

- 默认只学会并在槽 1 装备 `skill_basic_qi_bolt`；其余功法按解锁条件调用
  `Learn`。7 门功法均可学习，其中 6 门非被动功法可进入快捷栏。
- 快捷栏固定 4 槽，键盘 1–4 / 手柄方向键释放；K 打开功法面板，将已学
  功法拖到任意槽。相同功法只占一个槽，拖到新槽会从旧槽移除。
- 功法升级消耗 `item_skill_scroll × 当前等级`，最高 5 级；伤害为
  `Base + 每级 × (Level-1)`，成功发布 `OnSkillUpgraded` 并立即保存背包和功法。

### 4.1 状态与元素反应 ID（G01-03）

正式 `Resources/SO/StatusEffects` 缺失时，`ConfigDatabase` 注册同 ID 运行时占位；正式资产优先。Mod 数值按层相加，持续时间在重复施加时刷新。

| StatusId | 中文 / Key | Duration | MaxStacks | Aura | 数值契约 |
|----------|------------|---------:|----------:|------|----------|
| status_burn | 灼烧 / status_name_burn | 3s | 3 | Fire | 每层每秒 `0.05 × 命中技能 BaseDamage` |
| status_chill | 寒意 / status_name_chill | 3s | 2 | Ice | MoveSpeed -30%；2 层提升冻结 |
| status_freeze | 冻结 / status_name_freeze | 1.5s | 1 | Ice | Stun；首次生效起 8s 内不重复 |
| status_shock_stun | 感电 / status_name_shock_stun | 0.5s | 1 | None | Stun |
| status_poison | 中毒 / status_name_poison | 4s | 3 | Poison | 每层每秒 4 点 Poison DoT |
| status_wet | 潮湿 / status_name_wet | 4s | 1 | Water | 反应标记 |
| status_wood_mark | 木灵印 / status_name_wood_mark | 4s | 1 | Wood | 反应标记 |
| status_sever_def | 破甲 / status_name_sever_def | 3s | 1 | None | Defense -10% |
| status_heart_demon | 心魔 / status_name_heart_demon | 30s | 1 | None | 最终造成伤害 -10% |

| Reaction enum | 中文 / Key | 契约 |
|---------------|------------|------|
| Melt | 融化 / reaction_name_melt | 1.5x |
| BurnBurst | 燃爆 / reaction_name_burn_burst | 1.3x，清除 Poison Aura |
| Shock | 感电 / reaction_name_shock | 1.4x，附加 status_shock_stun |
| Spread | 扩散 / reaction_name_spread | 复制全部 Aura 至 4m 内同受击阵营 Actor，不二次反应 |
| Sever | 断灵 / reaction_name_sever | 1.25x，伤害后附加 status_sever_def |

### 4.2 玩家资源来源 ID（G01-04）

| SourceId | 用途 |
|----------|------|
| recovery_out_of_combat | 脱战 5s 后的 HP/Mana 持续回复；HealInfo.SourceId 使用 |

---

## 5. 敌人表

| EnemyId | 名 | Rank | 境界 | HP | Atk | XP | 灵石 | 掉落要点 |
|---------|----|------|------|----|-----|----|------|----------|
| enemy_training_dummy | 练功木桩 | Normal | — | 30 | 0 | 25 | 0 | G-VS-05：击杀得原始修为 25，无掉落 |
| enemy_wolf_gray | 灰狼 | Normal | 练气1 | 80 | 8 | 15 | 0–2 | 狼毫 40% |
| enemy_fox_spirit | 狐妖 | Normal | 练气3 | 140 | 14 | 28 | 0–2 | 灵尘 |
| enemy_bandit | 散修 | Normal | 练气5 | 200 | 20 | 40 | 0–2 | 灵石掉落表 |
| enemy_wolf_elite | 灰爪 | Elite | 练气6 | 800 | 28 | 120 | 8–15 | 妖核 100% |
| enemy_stone_golem | 石傀 | Normal | 筑基初 | 500 | 35 | 80 | 0–2 | 石屑 |
| enemy_blackwind_spawn | 黑风小妖 | Normal | 筑基 | 350 | 40 | 60 | 0–2 | — |
| enemy_boss_stone_general | 黑风石将军 | Boss | 金丹感 | 12000 | 70 | 2000 | 0 | 专属刃/材料 |

G-VS-07 的代码优先灰狼占位参数：`MoveSpeed=3.2`、`AggroRange=8`、`AttackRange=1.6`、`DisengageRange=14`、`AttackInterval=1.2`；首个 Spawner 同时存活 3、重生 8s。G04-01 在不新增敌人 ID 的前提下扩为三个刷怪区，容量分别为 3 / 2 / 2（全图共 7 只），统一配置四点巡逻路线。`item_mat_wolf_hair` 为 Common Material、堆叠 99、基础卖价 5；正式 SO 导入后保留相同 ID 与数值契约。

**灰爪**：`skill_enemy_wolf_elite_charge` 冲锋技能 1；基础蓄力 0.4s、冲锋
0.7s、移动速度倍率 2.4、伤害倍率 1.5、冷却 6s。HP/Atk 相对灰狼分别为
10× / 3.5×；灵草涧单点刷新 1 只，死亡后 15s 重生。  

**石将军 Telegraph 实例（G04-05）**：

| 阶段 | SkillId | Shape | Duration | RecoverStun | Vfx |
|------|---------|-------|----------|-------------|-----|
| P1 | boss_sg_slam | Circle | 0.7 | 1.0 | VFX_Boss_Slam_Warning |
| P1 | boss_sg_spike | Circle | 0.6 | 0.8 | VFX_Boss_Slam_Warning |
| P2 | boss_sg_charge | Line | 0.8 | 1.2 | VFX_Boss_Charge_Warning |
| P2 | boss_sg_summon | Circle | 0.7 | 0.5 | VFX_Boss_Summon_Warning |
| P3 | boss_sg_rage_slam | FullScreen | 1.0 | 1.5 | VFX_Boss_Slam_Warning |

练习模式：**Post-MVP**；MVP 仅 `/spawn`。

**G04-04 掉落结算**：每条 `LootEntry` 独立掷 `DropChance`，成功后在
`MinCount..MaxCount` 闭区间取整；背包满则生成 180s 世界拾取物。灵石不作为
物品，在 `EnemyData.MinSpiritStones..MaxSpiritStones` 闭区间取整后直接写入
背包货币余额；普通怪 0–2、精英 8–15 与 §13 一致。

---

## 6. 任务表（支线/日常）

G-VS-06 的 `quest_main_01_02` 代码优先占位（后续主线扩写保留 ID、目标与奖励契约）：

| QuestId | 类型 | 接取/交付 NPC | 目标 | TurnIn 奖励 |
|---------|------|--------------|------|-------------|
| quest_main_01_02 | Main | npc_yaolao | Kill enemy_wolf_gray ×3 | 原始修为 700，灵石 10 |

| QuestId | 类型 | 接取→交付 | 目标 | 奖励摘要 |
|---------|------|-----------|------|----------|
| quest_side_herb_01 | Side | npc_danding_guide→同 NPC | 采清心草×10 | 灵石 50，faction_danding 声望 20 |
| quest_side_bandit_01 | Side | npc_trainer→同 NPC | 杀 enemy_bandit×8 | 铁剑，原始修为 200 |
| quest_side_hermit_01 | Side | npc_danding_guide→npc_hermit | 接取获 item_quest_hermit_letter；向隐士送达并消耗 | skill_wind_slash |
| quest_daily_hunt | Daily | 无 NPC | 任意杀敌×15 | 灵石 30，聚气丹×1 |
| quest_daily_gather | Daily | 无 NPC | Gather 来源采集数量×5 | 灵尘×5 |

日常从 `cycleStartedUtc` 起满现实 24h 重置；两条均写入可选 `dailies.json`，损坏时重置且不得阻断主线。

主线 10 环见 §1。

---

## 7. 炼丹配方

| RecipeId | 产物 | 等级 | 成功率 | 材料 |
|----------|------|------|--------|------|
| recipe_heal_01 | 回血丹 | 1 | 0.9 | 清心草×2 灵尘×1 |
| recipe_mana_01 | 回气丹 | 1 | 0.9 | 清心草×1 灵尘×2 |
| recipe_xp_01 | 聚气丹 | 3 | 0.7 | 妖核×1 灵尘×3 |
| recipe_body_01 | 锻体丹 | 2 | 0.75 | 狼毫×3 灵尘×2 |

失败规则：灵尘为催化剂并消耗；清心草、狼毫、一品妖核作为主材退回。

### 7.1 青石原采集点

| NodeId | 产物 | 数量 | 等级 | 刷新 |
|--------|------|------|------|------|
| gather_qingshi_qingxin_01 | 清心草 | 1–2 | 1 | 30s |
| gather_qingshi_qingxin_02 | 清心草 | 1–2 | 1 | 30s |
| gather_qingshi_qingxin_03 | 清心草 | 1–2 | 1 | 30s |
| gather_qingshi_qingxin_04 | 清心草 | 1–2 | 1 | 30s |
| gather_qingshi_spirit_dust_01 | 灵尘 | 1–2 | 1 | 45s |
| gather_qingshi_spirit_dust_02 | 灵尘 | 1–2 | 1 | 45s |

---

## 8. 成就（≥10）

| Id | 名 | Trigger | 目标 | 奖励 |
|----|-----|---------|------|------|
| ach_first_kill | 初入仙途 | KillTotal | 1 | 回血丹×5 |
| ach_kill_100 | 小试牛刀 | KillTotal | 100 | 称号 斩妖学徒 |
| ach_kill_1000 | 斩妖能手 | KillTotal | 1000 | 称号 斩妖人 |
| ach_realm_found | 筑基有成 | RealmReached | Foundation | 灵石 50 |
| ach_realm_core | 金丹初成 | RealmReached | GoldenCore | 灵石 100 |
| ach_quest_ch1 | 青石问道 | QuestCompleted | quest_main_01_10 | 称号 问道者 |
| ach_alchemy_10 | 丹火初燃 | CraftCount | 10 | 声望 30 |
| ach_secret_chest | 云雾藏宝 | Flag | flag_serendipity_cangwu_cliff_box | 灵石 30 |
| ach_boss_stone | 石将军终结者 | KillEnemy | enemy_boss_stone_general | 称号 破石 |
| ach_waste_body | 废脉锻体 | BodyLevel+Root | Waste&Copper | 称号 铁骨 |

---

## 9. 称号

| TitleId | 名 | 加成 |
|---------|----|------|
| title_yaotu_apprentice | 斩妖学徒 | Atk+2 |
| title_yaotu | 斩妖人 | Atk+5 Crit+1% |
| title_wendao | 问道者 | CultivationSpeed+5% |
| title_poshi | 破石 | Def+8 |
| title_tiegu | 铁骨 | MaxHp+5% |

---

## 10. 对话 ID 清单（主线最小）

| DialogueId | 用途 |
|------------|------|
| dlg_main_01_01_start | 药老开场 |
| dlg_main_01_01_complete | 授功法 |
| dlg_main_01_02_start | 猎狼 |
| dlg_main_01_02_complete | 猎狼交付 |
| dlg_main_01_03_start | 灵草 |
| dlg_main_01_03_complete | 灵草交付 |
| dlg_main_01_04_start / complete | 炼制回气丹 / 交付 |
| dlg_main_01_05_start / complete | 探查秘径 / 开路 |
| dlg_main_01_06_start / complete | 苍梧试炼 / 交付 |
| dlg_main_01_07_start / complete | 筑基线索 / 交付 |
| dlg_main_01_08_start / complete | 领取筑基丹 / 筑基完成 |
| dlg_main_01_09_start / complete | 领取凝金丹 / 黑风残魂交付 |
| dlg_main_01_10_start / complete | 石将军 / 第一章完 |
| dlg_shop_zhanggui | 商店寒暄 |
| dlg_faction_join | 入丹鼎 |
| dlg_side_herb_01_start / complete | 清心草支线接取 / 交付 |
| dlg_side_bandit_01_start / complete | 散修支线接取 / 交付 |
| dlg_side_hermit_01_start | 隐士支线接取与手书发放 |
| dlg_hermit_side | 隐士支线交付 |
| dlg_blackwind_intro | 副本残魂 |
| dlg_serendipity_qingshi_herb_spirit | 青石草木灵息奇遇 |
| dlg_serendipity_cangwu_mist_stele | 苍梧雾中古碑奇遇 |
| dlg_serendipity_cangwu_cliff_box | 苍梧崖间旧匣奇遇 |
| dlg_serendipity_blackwind_echo_cache | 黑风残响秘藏奇遇 |

节点文本可在 SO 内撰写 3–6 句/段，无需全文案到小说篇幅。

---

## 11. 地图刷怪/采集点（数量指标）

| 地图 | 刷怪点 | 同时存活上限 | 采集点 | 宝箱 |
|------|--------|--------------|--------|------|
| 青石 | ≥4 | 12 | ≥6 | ≥2 |
| 苍梧 | ≥5 | 15 | ≥8 | ≥3 |
| 黑风 | 波次脚本 | 按房间 | 0 | ≥3 |

### 11.1 地图出生/传送点 ID

| Id | 地图 | 用途 |
|----|------|------|
| teleport_qingshi_town | map_qingshi | 青石镇默认出生点与 G01-04 最近点复活 |
| teleport_cangwu_gate | map_cangwu | 苍梧山门出生/复活点；首次踩点后解锁 |
| spawn_blackwind_b1 | map_blackwind | checkpoint 0 再入出生 B1 |
| spawn_blackwind_b2 | map_blackwind | checkpoint 1 再入出生 B2 |
| spawn_blackwind_b3 | map_blackwind | checkpoint 2 再入出生 B3 |
| spawn_blackwind_b4 | map_blackwind | checkpoint 3 再入出生 B4 |
| spawn_blackwind_b5 | map_blackwind | checkpoint 4 再入出生 B5 |

### 11.2 青石原灰盒区域与宝箱 ID（G05-01）

| Id | 名称/用途 |
|----|-----------|
| area_qingshi_town | 青石镇 |
| area_qingshi_training_ground | 习武坪 |
| area_qingshi_east_wilderness | 东郊荒野 |
| area_qingshi_herb_creek | 灵草涧 |
| area_qingshi_secret_path | 秘径入口 |
| safezone_qingshi_town_training | 青石镇与习武坪安全区，恢复 2× |
| chest_qingshi_wilderness_01 | 东郊荒野宝箱灰盒 |
| chest_qingshi_secret_path_01 | 秘径宝箱灰盒 |
| furnace_qingshi_town | 青石镇主线炼丹交互丹炉 |

### 11.3 苍梧山脉灰盒区域与门禁 ID（G05-02）

| Id | 名称/用途 |
|----|-----------|
| area_cangwu_gate_platform | 山门平台 |
| area_cangwu_mountain_road | 盘山道 |
| area_cangwu_mist_valley | 云雾谷 |
| area_cangwu_cave | 洞府 |
| area_cangwu_thunder_terrace | 雷台远眺 |
| quest_flag_main_cangwu_path_open | `quest_main_01_05` 完成后开启青石秘径 |
| gather_cangwu_qingxin_01—04 | 苍梧清心草采集点，共 4 个 |
| gather_cangwu_spirit_dust_01—04 | 苍梧灵尘采集点，共 4 个 |
| chest_cangwu_mist_01 | 云雾谷宝箱灰盒 |
| chest_cangwu_cave_01 | 洞府宝箱灰盒 |
| chest_cangwu_terrace_01 | 雷台宝箱灰盒 |
| furnace_cangwu_gate | 苍梧山门平台炼丹交互丹炉 |

苍梧刷怪点由 `Spawner_CangwuMountainRoadTrial` 的 3 个出生槽与
`Spawner_CangwuMistValley` 的 2 个出生槽组成，共 5 个稳定位置；同时存活上限为 5，
低于地图总上限 15。

### 11.4 黑风秘境稳定 ID（G05-03）

| Id | 名称/用途 |
|----|-----------|
| entrance_blackwind_cangwu | 苍梧雷台的金丹门禁入口 |
| area_blackwind_b1 | B1 双波与压力板教学层 |
| area_blackwind_b2 | B2 精英封印层 |
| area_blackwind_b3 | B3 左宝箱/右陷阱岔路层 |
| area_blackwind_b4 | B4 多怪与回复泉层 |
| area_blackwind_b5 | B5 黑风石将军层 |
| chest_blackwind_b1_supply | B1 补给宝箱灰盒 |
| chest_blackwind_b3_branch | B3 左路宝箱灰盒 |
| chest_blackwind_b4_cache | B4 深层宝箱灰盒 |
| spring_blackwind_b4_heal | B4 本局一次、回复最大生命 50% |
| hazard_blackwind_b3_spikes | B3 右路尖刺陷阱伤害来源 |

---

## 12. 音频/VFX ID 最小集

```
BGM_Explore_Qingshi
BGM_Explore_Cangwu
BGM_Combat_Normal
BGM_Boss_StoneGeneral
BGM_City_Qingshi
SFX_UI_Click, SFX_UI_QuestComplete, SFX_UI_LevelUp, SFX_UI_Error
SFX_Combat_Hit_Light, SFX_Combat_Hit_Heavy, SFX_Combat_Dodge
SFX_Skill_Fire, SFX_Skill_Ice, SFX_Skill_Lightning
AMB_Wind_Plain, AMB_Wind_Mountain, AMB_Rain
VFX_Hit_Physical, VFX_Hit_Fire, VFX_Hit_Ice
VFX_Skill_Ember, VFX_Skill_QiBolt_Projectile, VFX_Skill_QiBolt_Impact
VFX_Boss_Slam_Warning, VFX_Boss_Charge_Warning, VFX_Boss_Summon_Warning
VFX_Realm_Breakthrough, VFX_Loot_Drop, VFX_Heal
```

### 12.1 G06-05 ID 接入说明

`FeedbackContentIds` 是上表的运行时镜像：BGM 5 个、SFX 10 个、Ambience 3 个、
VFX 12 个。管理器拒绝空 ID 与表外 ID；技能、元素反应、石将军预警、突破、治疗与
地面掉落只通过这些 ID 请求占位反馈。正式资源替换时保持 ID 不变即可，无需修改玩法
系统。

---

## 13. 经济与抽测（G07-02）

| 行为 | 灵石 |
|------|------|
| 普通怪 | 0–2 |
| 精英 | 8–15 |
| 主线任务 | 0（纯引导）/ 10–15 |
| 支线/日常 | 30–80 |
| 回血丹买价 | 10 |
| 精炼石买价 | 25 |
| 铁剑买价 | 80 |

卖价 = 买价 ×0.25。**调参指南**（非硬验收）：东郊约 20–40 灵石/时；通关结余理想 80–200。

G03-05 张掌柜固定货单：`item_potion_heal_01`（买 10 / 卖 2）、
`item_mat_refine_stone`（买 25 / 卖 6）、`eq_weapon_iron_sword`
（买 80 / 卖 20）。整数卖价向下取整；筑基丹、凝金丹不进入货单。

**G07-02 硬检查清单**：

1. 练气中、Fire 灵根、主线 02 后，仅东郊 15 min（禁 /give），记录 Δ灵石/修为  
2. 通关主线 10 后灵石 **∈ [50, 300]**  
3. Sink 烟测：回血丹/精炼石仍可买  
4. 小时率不进 CI，除非连续两次结余越界  

### 13.1 G07-02 可复现平衡记录（2026-07-15）

`MvpBalanceAudit` 直接读取运行时 `ConfigDatabase`，不维护第二份奖励表：

| 检查 | 结果 | 推导 |
|------|------|------|
| 主线任务灵石 | 85 | 01/07 为纯引导 0；02—06、08—09 各 10；10 为 15 |
| 必得境界成就 | 150 | 筑基 50 + 金丹 100 |
| 必打敌人掉落 | 16—58 | 14 普通怪（0—2）+ 2 精英（8—15） |
| 主线 10 结余 | **251—293** | 禁 `/give`、不计可选支线/奇遇且未购买时的保守闭区间 |
| 东郊 15 min Fire 代理 | 灵石 0—14 / 修为 115.5 | 主线 02 后单轮 7 个普通刷怪点；不驻点刷重生 |
| Fire 练气蓄满 | 10,571 / 10,500 | 主线 01—07 的 9,400 基础修为 + 必打怪 210，再乘 1.1 |
| Fire 筑基蓄满 | 25,300 / 25,000 | 主线 08 的 23,000 基础修为乘 1.1 |
| 主线节奏代理 | **195 min（3h15m）** | 对话任务 60 + 跑图 35 + 采集炼丹 15 + 必打战斗 47 + 突破菜单 18 + 探索/重试缓冲 20 |

195 分钟是可复现的内容节拍预算，不伪装成真人墙钟录像；最终新档真人全流程留在
G07-04 总验收。当前模型保证 Fire 灵根不依赖纯刷怪即可跨过两次境界门槛，且商店三项
Sink 仍固定为 10 / 25 / 80。对应回归入口：`tools/validate_g07_02.sh`。

---

## 14. 奇遇表（G05-06）

| Id | 地图 / TriggerId | DialogueId / WorldFlag | 摘要奖励（无装备） |
|----|------------------|------------------------|-------------------|
| serendipity_qingshi_herb_spirit | 青石 / trigger_serendipity_qingshi_herb_spirit | dlg_serendipity_qingshi_herb_spirit / flag_serendipity_qingshi_herb_spirit | 清心草×3 + 灵石 10 |
| serendipity_cangwu_mist_stele | 苍梧 / trigger_serendipity_cangwu_mist_stele | dlg_serendipity_cangwu_mist_stele / lore_cangwu_mist_stele | lore Flag + 残页×1 |
| serendipity_cangwu_cliff_box | 苍梧 / trigger_serendipity_cangwu_cliff_box | dlg_serendipity_cangwu_cliff_box / flag_serendipity_cangwu_cliff_box | 灵尘×5 + 灵石 20 |
| serendipity_blackwind_echo_cache | 黑风 / trigger_serendipity_blackwind_echo_cache | dlg_serendipity_blackwind_echo_cache / flag_serendipity_blackwind_echo_cache | 黑风石屑×3 + 灵石 30 |

---

## 15. BGM 状态机绑定

| 转移 | BGM ID | 验收 |
|------|--------|------|
| 青石探索 | BGM_Explore_Qingshi | 进图 |
| 苍梧探索 | BGM_Explore_Cangwu | 进图 |
| 进战 | BGM_Combat_Normal | **≤1s** Crossfade（G06-05） |
| 脱战 8s | 回 Explore | |
| 石将军 | BGM_Boss_StoneGeneral | 进战圈 |
| 城内 | BGM_City_Qingshi | 安全区可选 |

G06-05 运行时以 `AudioStateController` 统一决策：青石进
`BGM_Explore_Qingshi`，苍梧与黑风进 `BGM_Explore_Cangwu`；普通进战使用 1s
Crossfade，石将军战圈覆盖为 `BGM_Boss_StoneGeneral`。最后战斗活动后，
`PlayerStats` 的 5s 战斗标记延迟与音乐状态机 3s 尾段共同构成表中的 8s 脱战返回。

---

## 16. Must-ship 文案（localization key + 中文 default）

| Key | 中文 |
|-----|------|
| root_intro_five | 五行之力，各有所长。择一深耕，可引元素相生相克。 |
| root_intro_waste | 废脉难修法，却可锤炼肉身。路途漫长，厚积薄发。 |
| root_intro_heaven | 天灵罕见，气海充盈。修炼速度显著提升，万事慎傲。 |
| root_name_metal | 金灵根 |
| root_name_wood | 木灵根 |
| root_name_water | 水灵根 |
| root_name_fire | 火灵根 |
| root_name_earth | 土灵根 |
| root_name_heaven | 天灵根 |
| root_name_waste | 废脉 |
| root_name_none | 未定 |
| body_name_mortal | 凡躯 |
| body_name_copper | 铜皮铁骨 |
| body_name_diamond | 金刚不坏 |
| body_name_immortal | 不灭金身 |
| body_name_eternal | 万劫不灭 |
| realm_name_mortal | 凡人 |
| realm_name_qi_condensation | 练气 |
| realm_name_foundation | 筑基 |
| realm_name_golden_core | 金丹 |
| realm_name_nascent_soul | 元婴 |
| ui_root_selection_title | 择定灵根 |
| ui_root_selection_prompt | 灵根一经选定，便不可更改。 |
| ui_root_random | 随机感应 |
| ui_root_preview_hint | 先行感应，可在确认前反复比较。 |
| ui_root_selected | 已择：{0} |
| ui_root_confirm | 踏上仙途 |
| ui_cultivation_realm_stage | {0} 第{1}层 |
| ui_cultivation_xp | 修为 {0:0}/{1:0} |
| ui_character_title | 角色 |
| ui_character_realm | 境界：{0} 第{1}层\n修为：{2:0}/{3:0} |
| ui_character_spirit_root | 灵根：{0} |
| ui_character_body | 炼体：{0}\n炼体修为：{1:0}/{2:0} |
| ui_character_stats | 气血 {0:0}/{1:0}；灵力 {2:0}/{3:0}；攻击 {4:0.#}；防御 {5:0.#}；暴击 {6:P0}；暴伤 {7:P0}；修炼增益 {8:P0}；神识 {9:0.#} |
| ui_character_breakthrough | 尝试突破 |
| ui_character_breakthrough_ready | 气机圆满，可尝试突破（成功率 {0:P0}） |
| ui_character_breakthrough_blocked | 突破条件尚未满足，点击按钮查看详情。 |
| npc_name_yaolao | 药老 |
| npc_name_zhanggui | 张掌柜 |
| npc_name_cangwu_guard | 山门弟子 |
| npc_name_blackwind_echo | 残魂 |
| npc_name_danding_guide | 丹鼎接引 |
| npc_name_trainer | 武教习 |
| npc_name_hermit | 洞府隐士 |
| ui_npc_interact | [E] 与{0}交谈 |
| ui_vendor_interact | [E] 与{0}交易 |
| dlg_shop_zhanggui_01 | 客官慢看，小店货真价实。 |
| quest_name_side_herb_01 | 清心十株 |
| quest_desc_side_herb_01 | 为丹鼎接引采回十株清心草。 |
| quest_obj_side_herb_01_collect | 收集清心草 |
| dlg_side_herb_01_start_01 | 丹炉近来缺一味清凉主材，劳你采十株清心草。 |
| dlg_side_herb_01_complete_01 | 叶脉清透，药性未散。宗门会记下这份心意。 |
| quest_name_side_bandit_01 | 散修之患 |
| quest_desc_side_bandit_01 | 清理青石原上劫掠行人的八名散修。 |
| quest_obj_side_bandit_01_kill | 击败劫掠散修 |
| dlg_side_bandit_01_start_01 | 镇外有散修拦路劫掠，除掉八人再回来。 |
| dlg_side_bandit_01_complete_01 | 出手有分寸，也护住了行路之人。这柄铁剑归你。 |
| quest_name_side_hermit_01 | 洞府手书 |
| quest_desc_side_hermit_01 | 将药老手书送至苍梧洞府隐士手中。 |
| quest_obj_side_hermit_01_deliver | 向洞府隐士送达药老手书 |
| dlg_side_hermit_01_start_01 | 这封手书须交给苍梧洞府的隐士，途中莫要拆封。 |
| dlg_hermit_side_01 | 故人字迹依旧。你替我送来此信，我便传你一式风刃。 |
| quest_name_daily_hunt | 今日猎妖 |
| quest_desc_daily_hunt | 在本轮日常中击败任意十五名敌人。 |
| quest_obj_daily_hunt | 击败任意敌人 |
| quest_name_daily_gather | 今日采撷 |
| quest_desc_daily_gather | 在本轮日常中完成五次采集。 |
| quest_obj_daily_gather | 采集任意材料 |
| quest_name_main_01_01 | 灵根初醒 |
| quest_desc_main_01_01 | 接受药老的灵根测试，正式踏上修行之路。 |
| quest_obj_main_01_01_talk_yaolao | 听药老讲解引气诀 |
| quest_name_main_01_02 | 东郊猎狼 |
| quest_desc_main_01_02 | 前往东郊，击杀三只灰狼，再回来向药老复命。 |
| quest_obj_main_01_02_kill_wolves | 击杀灰狼 |
| dlg_main_01_02_start_01 | 东郊近来狼患渐重，已有行商不敢出镇。 |
| dlg_main_01_02_start_02 | 你既已引气入体，可愿去除掉三只灰狼？ |
| dlg_main_01_02_start_03 | 莫要轻敌。事成之后，回来寻我。 |
| dlg_main_01_02_start_04 | 修行不争一时，准备妥当再来。 |
| dlg_main_01_02_complete_01 | 气息沉稳了几分。此行所得，才是修行的开端。 |
| dlg_choice_main_01_02_accept | 我这便去。 |
| dlg_choice_main_01_02_later | 容我再准备一番。 |
| quest_name_main_01_03 | 灵草涧 |
| quest_desc_main_01_03 | 采集五株清心草，并除掉盘踞灵草涧的灰爪。 |
| quest_obj_main_01_03_collect_qingxin | 收集清心草 |
| quest_obj_main_01_03_kill_elite | 击败灰爪 |
| quest_name_main_01_04 | 炉火初试 |
| quest_desc_main_01_04 | 在丹炉炼成一枚回气丹。 |
| quest_obj_main_01_04_craft_mana | 炼制回气丹 |
| quest_name_main_01_05 | 秘径北行 |
| quest_desc_main_01_05 | 前往青石镇西北的秘径入口，寻找通往苍梧的旧路。 |
| quest_obj_main_01_05_reach_path | 抵达秘径入口 |
| quest_name_main_01_06 | 苍梧试炼 |
| quest_desc_main_01_06 | 通过山门弟子的盘山道试炼。 |
| quest_obj_main_01_06_kill_trial | 清理盘山道妖兽 |
| quest_name_main_01_07 | 筑基机缘 |
| quest_desc_main_01_07 | 向山门弟子追问筑基机缘的去向。 |
| quest_obj_main_01_07_talk_guard | 询问筑基机缘 |
| quest_name_main_01_08 | 一朝筑基 |
| quest_desc_main_01_08 | 服下筑基丹，将练气修为推至筑基境。 |
| quest_obj_main_01_08_reach_foundation | 突破至筑基境 |
| quest_name_main_01_09 | 金丹叩关 |
| quest_desc_main_01_09 | 收好凝金丹，结成金丹后前往黑风秘境入口。 |
| quest_obj_main_01_09_collect_pill | 收下凝金丹 |
| quest_obj_main_01_09_reach_goldencore | 突破至金丹境 |
| quest_obj_main_01_09_reach_entrance | 抵达黑风秘境入口 |
| quest_name_main_01_10 | 黑风问道 |
| quest_desc_main_01_10 | 深入黑风秘境，击败镇守最深处的石将军。 |
| quest_obj_main_01_10_kill_boss | 击败黑风石将军 |
| dlg_main_01_01_start_01 | 灵根既醒，先让我看看你能否守住这一缕气。 |
| dlg_main_01_01_complete_01 | 气沉丹田，意守灵台。《引气诀》的门，你已推开了。 |
| dlg_main_01_03_start_01 | 灵草涧有清心草，也有一头成了气候的灰爪。 |
| dlg_main_01_03_complete_01 | 草叶无损，妖气已散。你的心比出发时更稳。 |
| dlg_main_01_04_start_01 | 修士不能只会争斗。用带回的灵草炼一枚回气丹。 |
| dlg_main_01_04_complete_01 | 火候尚嫩，药性却已收住。第一炉，算成了。 |
| dlg_main_01_05_start_01 | 镇西北有条封了多年的秘径，去认一认路。 |
| dlg_main_01_05_complete_01 | 旧禁已解。沿秘径北上，便是苍梧山门。 |
| dlg_main_01_06_start_01 | 苍梧不收空口求道之人。先清理盘山道的妖兽。 |
| dlg_main_01_06_complete_01 | 身手尚可。山中有一桩筑基机缘，也许与你有缘。 |
| dlg_main_01_07_start_01 | 药老曾托人留话：你的筑基之物，仍要回青石取。 |
| dlg_main_01_07_complete_01 | 回去吧。破境之前，先问清自己为何求道。 |
| dlg_main_01_08_start_01 | 这枚筑基丹只替你推门，门后的路仍要自己走。 |
| dlg_main_01_08_complete_01 | 道基已立。往后每一步，都比从前更重。 |
| dlg_main_01_09_start_01 | 凝金一关，先凝心，再凝丹。黑风入口会验证你的道行。 |
| dlg_main_01_09_complete_01 | 金丹气息……终于又有人走到这里。石将军在最深处等你。 |
| dlg_main_01_10_start_01 | 碑纹引来无数贪念。击败石将军，证明你不是下一个夺碑者。 |
| dlg_main_01_10_complete_01 | 石锁已断，碑纹却指向更远处。拓跋渊……他还活着。 |
| ui_dialogue_continue | [E] 继续 |
| ui_dialogue_finish | [E] 完成 |
| ui_dialogue_choose_first | [E] 选择第一项 |
| ui_quest_tracker_empty | 暂无追踪任务 |
| ui_quest_progress | {0} {1}/{2} |
| ui_quest_ready_turn_in | 目标已完成，可以交付 |
| ui_quest_guidance_accept | 前往{0}交谈，接取主线 |
| ui_quest_guidance_turn_in | 返回{0}交付任务 |
| ui_quest_marker_accept | 接取 · {0} |
| ui_quest_marker_objective | {0} |
| ui_quest_marker_turn_in | 交付 · {0} |
| ui_quest_marker_distance | {0} · {1:0}米 |
| ui_quest_panel_title | 任务 |
| ui_quest_active | 进行中 |
| ui_quest_daily | 每日委托 |
| ui_quest_panel_empty | 暂无任务。与带有任务标记的修士交谈可接取委托。 |
| ui_daily_ready | 委托完成，可领取奖励 |
| ui_daily_claimed | 今日奖励已领取 |
| ui_daily_claim | 领取奖励 |
| ui_map_title | 山河舆图 |
| ui_map_help | 选择已解锁传送阵前往对应地域。 |
| map_name_qingshi | 青石镇传送阵 |
| map_name_cangwu | 苍梧山门传送阵 |
| ui_map_locked | 尚未解锁 |
| ui_map_travel | 传送 |
| ui_pause_title | 静心凝神 |
| ui_pause_continue | 继续游历 |
| ui_pause_save | 保存进度 |
| ui_pause_settings | 游历设置 |
| ui_pause_exit | 返回主界面 |
| ui_pause_continue_hint | Esc 继续游历 |
| ui_save_success | 进度已保存 |
| ui_save_unavailable | 当前没有可用存档槽 |
| ui_settings_title | 游历设置 |
| ui_settings_master | 总音量 |
| ui_settings_bgm | 音乐 |
| ui_settings_sfx | 音效 |
| ui_settings_display | 显示模式 |
| ui_settings_windowed | 窗口模式 |
| ui_settings_fullscreen | 全屏模式 |
| ui_settings_reset | 恢复默认 |
| ui_settings_apply | 应用并返回 |
| ui_settings_saved | 设置已应用 |
| ui_settings_save_failed | 设置保存失败，请重试 |
| ui_settings_back_hint | Esc 返回 · 方向键 / 手柄可调节 |
| ui_settings_percent | {0}% |
| ui_exit_confirm | 保存进度并返回主界面？ |
| ui_common_confirm | 确认 |
| ui_common_cancel | 取消 |
| ui_runtime_message | 运行时消息（default 由调用方提供） |
| ui_player_default_name | 无名修士 |
| ui_player_hp | 气血 {0:0}/{1:0} |
| ui_player_currency | 灵石 {0} |
| ui_death_revive | 道消身陨——于最近传送阵苏醒。 |
| ui_death_xp_penalty | 当前层修为损失 {0:0}% |
| ui_death_respawn | 于最近传送阵复生 |
| ui_bt_success | 气机凝实，境界更进一步！ |
| ui_bt_fail | 心魔侵扰，破境未成。整理再战。 |
| ui_bt_fail_detail | 破境未成（本次成功率 {0:P0}）。{1} 心魔尚余 {2:0} 秒，修为距本层圆满尚差 {3:0}。 |
| ui_bt_block_no_next | 此境之后的破境之路尚未开放。 |
| ui_bt_block_stage | 需先修至{0}第{1}层，方可破境。 |
| ui_bt_block_item | 缺少{0}。{1} |
| ui_bt_block_combat | 气机未平，脱战五秒后方可破境。 |
| ui_bt_block_state | 此刻无法破境，请先结束当前交互。 |
| ui_blackwind_gate | 金丹方可踏入黑风秘境。 |
| ui_teleport_unlocked | 传送阵已解锁 |
| ui_teleport_locked | 此传送阵尚未解锁 |
| ui_cangwu_path_locked | 秘径尚未开启，先完成当前主线 |
| area_name_qingshi_town | 青石镇 |
| area_name_qingshi_training_ground | 习武坪 |
| area_name_qingshi_east_wilderness | 东郊荒野 |
| area_name_qingshi_herb_creek | 灵草涧 |
| area_name_qingshi_secret_path | 秘径入口 |
| area_name_cangwu_gate_platform | 山门平台 |
| area_name_cangwu_mountain_road | 盘山道 |
| area_name_cangwu_mist_valley | 云雾谷 |
| area_name_cangwu_cave | 洞府 |
| area_name_cangwu_thunder_terrace | 雷台远眺 |
| area_name_blackwind_b1 | 黑风地宫·一层 |
| area_name_blackwind_b2 | 黑风地宫·二层 |
| area_name_blackwind_b3 | 黑风地宫·三层 |
| area_name_blackwind_b4 | 黑风地宫·四层 |
| area_name_blackwind_b5 | 黑风地宫·五层 |
| ui_main_menu_title | 问道长生 |
| ui_main_menu_start_game | 踏入仙途 |
| ui_main_menu_subtitle | 一念入山海 · 一剑问长生 |
| ui_main_menu_start_hint | 鼠标点击 / Enter / 手柄 A 确认 |
| ui_main_menu_starting | 正在入境…… |
| ui_main_menu_start_unavailable | 暂时无法进入，请稍候重试。 |
| ui_nav_inventory | I 行囊 |
| ui_nav_character | C 角色 |
| ui_nav_skill | K 功法 |
| ui_nav_quest | J 任务 |
| ui_nav_map | M 地图 |
| ui_nav_pause | Esc |
| ui_loading_entering_world | 正在步入仙途… |
| ui_loading_progress_percent | 进度 {0}% |
| item_name_item_potion_heal_01 | 回血丹 |
| item_desc_item_potion_heal_01 | 服用后恢复八十点气血。 |
| item_name_item_potion_mana_01 | 回气丹 |
| item_desc_item_potion_mana_01 | 服用后恢复五十点灵力。 |
| item_name_item_potion_xp_01 | 聚气丹 |
| item_desc_item_potion_xp_01 | 服用后增加三百点修为。 |
| item_name_item_potion_body_01 | 锻体丹 |
| item_desc_item_potion_body_01 | 服用后增加二百点炼体经验。 |
| item_name_item_pill_foundation | 筑基丹 |
| item_desc_item_pill_foundation | 练气修士冲击筑基时所需的破境丹药。 |
| item_name_item_pill_goldencore | 凝金丹 |
| item_desc_item_pill_goldencore | 筑基修士凝结金丹时所需的破境丹药。 |
| item_name_item_mat_qingxin_grass | 清心草 |
| item_desc_item_mat_qingxin_grass | 叶脉清凉的低阶灵草，是炼制恢复丹药的常用主材。 |
| item_name_item_mat_spirit_dust | 灵尘 |
| item_desc_item_mat_spirit_dust | 细碎灵力凝成的粉尘，可稳定低阶丹炉中的药性。 |
| item_name_item_mat_beast_core_1 | 一品妖核 |
| item_desc_item_mat_beast_core_1 | 低阶妖兽体内凝结的精华，可用于炼制聚气丹。 |
| item_name_eq_weapon_wood_sword | 木剑 |
| item_desc_eq_weapon_wood_sword | 青石镇常见的练习木剑，攻击加八。 |
| item_name_eq_weapon_iron_sword | 铁剑 |
| item_desc_eq_weapon_iron_sword | 青石铁匠锻造的长剑，攻击加十八。 |
| item_name_item_mat_refine_stone | 精炼石 |
| item_desc_item_mat_refine_stone | 蕴含稳定灵力，可用于提升装备基础属性。 |
| item_name_item_skill_scroll | 功法残页 |
| item_desc_item_skill_scroll | 记载残缺运功法门，可用于提升已学功法。 |
| item_name_item_quest_hermit_letter | 药老手书 |
| item_desc_item_quest_hermit_letter | 药老封好的手书，需亲手交给苍梧洞府隐士。 |
| enemy_name_enemy_wolf_gray | 灰狼 |
| enemy_name_enemy_wolf_elite | 灰爪 |
| enemy_name_enemy_blackwind_spawn | 黑风小妖 |
| enemy_name_enemy_boss_stone_general | 黑风石将军 |
| enemy_name_enemy_bandit | 散修 |
| item_name_item_mat_wolf_hair | 狼毫 |
| item_desc_item_mat_wolf_hair | 灰狼颈背的硬毫，可作任务与炼丹材料。 |
| item_name_item_mat_black_stone | 黑风石屑 |
| item_desc_item_mat_black_stone | 被黑风侵蚀的石屑，仍残留微弱阴灵气息。 |
| recipe_name_recipe_heal_01 | 回血丹 |
| recipe_name_recipe_mana_01 | 回气丹 |
| recipe_name_recipe_body_01 | 锻体丹 |
| recipe_name_recipe_xp_01 | 聚气丹 |
| hint_serendipity_blackwind_echo_cache | 黑风秘境残响秘藏可得。 |
| serendipity_speaker_herb_spirit | 草木灵息 |
| serendipity_text_qingshi_herb_spirit | 溪畔灵气聚而不散，几株清心草在微光中自行舒展。 |
| serendipity_speaker_mist_stele | 雾中古碑 |
| serendipity_text_cangwu_mist_stele | 碑纹只显一瞬：长生非夺天地，而在不负此心。 |
| serendipity_speaker_cliff_box | 崖间旧匣 |
| serendipity_text_cangwu_cliff_box | 风蚀木匣卡在崖缝，匣中灵尘仍保持着微弱光泽。 |
| serendipity_speaker_blackwind_echo | 黑风残响 |
| serendipity_text_blackwind_echo_cache | 残响散去后，石缝里露出一包未被阴风侵蚀的旧物。 |
| ui_inventory_title | 背包 |
| ui_inventory_empty_slot | 空 |
| ui_inventory_no_selection | 请选择物品 |
| ui_inventory_empty | 行囊尚空，可从采集、战斗和任务中获得物品。 |
| ui_inventory_capacity | 行囊 {0}/{1} |
| ui_inventory_selected | 已选择：{0} |
| ui_inventory_stack_count | {0} ×{1} |
| ui_inventory_use | 使用 |
| ui_inventory_equip | 装备 |
| ui_common_close | 关闭 |
| ui_item_use_full_hp | 气血已满，无需服丹。 |
| ui_item_use_full_mana | 灵力充盈，无需服丹。 |
| ui_item_use_unavailable | 此物品当前无法使用。 |
| ui_alchemy_title | 炼丹 |
| world_name_alchemy_furnace | 青铜丹炉 |
| ui_alchemy_furnace_interact | [E] 使用{0} |
| ui_alchemy_help | 选择丹方，备齐材料后引动丹火。 |
| ui_alchemy_level | 丹道 {0} 级 · 熟练度 {1:0} |
| ui_alchemy_recipe_row | {0} · 成功率 {1:P0} |
| ui_alchemy_locked | {0} · 需丹道 {1} 级 |
| ui_alchemy_selection | {0}\n材料：{1}\n成功率：{2:P0} |
| ui_alchemy_missing_materials | 等级不足、材料欠缺或背包已满。 |
| ui_alchemy_craft | 开炉炼制 |
| ui_alchemy_success | 炼制成功：{0} ×{1} |
| ui_alchemy_failure | 丹火失衡，炼制失败。 |
| ui_gather_interact | [E] 采集{0} |
| ui_gather_progress | 采集中：{0} {1:P0} |
| ui_gather_success | 采得{0} ×{1} |
| ui_gather_interrupted | 受击打断了采集。 |
| ui_gather_unavailable | 此处暂不可采集，请检查等级、工具与背包。 |
| ui_shop_title | 张掌柜 · 杂货铺 |
| ui_shop_help | 购入补给，或将未绑定物品换作灵石。 |
| ui_shop_balance | 灵石：{0} |
| ui_shop_buy_heading | 购买 |
| ui_shop_sell_heading | 出售 |
| ui_shop_buy_row | {0}　{1}灵石 |
| ui_shop_sell_row | {0} ×{1}　卖价 {2} |
| ui_shop_no_selection | 选择背包物品后可出售一件。 |
| ui_shop_selected | 出售：{0} · 单价 {1} |
| ui_shop_sell | 出售一件 |
| ui_shop_buy_success | 购得{0} ×{1}，花费{2}灵石。 |
| ui_shop_sell_success | 售出{0} ×{1}，获得{2}灵石。 |
| ui_shop_insufficient_funds | 灵石不足。 |
| ui_shop_inventory_full | 背包空间不足。 |
| ui_shop_transaction_failed | 这笔交易无法完成。 |
| ui_shop_bound_item | 绑定物品不可出售。 |
| ui_equipment_realm_required | 当前境界不足，无法装备。 |
| ui_inventory_full | 背包已满，无法卸下装备。 |
| ui_equipment_unavailable | 此装备当前无法穿戴。 |
| ui_refine_no_equipment | 该槽位没有可精炼装备。 |
| ui_refine_max_level | 此装备已精炼至最高等级。 |
| ui_refine_material_missing | 精炼石不足，需要 {0} 个。 |
| ui_refine_success | 精炼成功，装备提升至 +{0}。 |
| ui_refine_fail | 精炼失败，装备等级保持不变。 |
| skill_name_skill_basic_qi_bolt | 引气弹 |
| skill_desc_skill_basic_qi_bolt | 凝聚灵气，向前射出一道引气弹。 |
| skill_name_skill_fire_ember | 星火诀 |
| skill_desc_skill_fire_ember | 弹指凝出星火，命中后灼烧敌人。 |
| skill_name_skill_ice_needle | 寒针 |
| skill_desc_skill_ice_needle | 凝寒成针，命中后令敌人身染寒意。 |
| skill_name_skill_lightning_chain | 引雷索 |
| skill_desc_skill_lightning_chain | 牵引雷光破敌，并短暂扰乱敌人行动。 |
| skill_name_skill_wind_slash | 风刃 |
| skill_desc_skill_wind_slash | 引风化刃，以迅疾灵压斩向前方。 |
| skill_name_skill_pass_iron_skin | 铁肤 |
| skill_desc_skill_pass_iron_skin | 运转气血淬炼皮肉，增强近身承伤能力。 |
| skill_name_skill_ult_fire_wave | 炎浪 |
| skill_desc_skill_ult_fire_wave | 聚拢烈焰化作炎浪，重创前方敌人。 |
| ui_skill_slot_one | 1  {0} |
| ui_skill_slot | {0}  {1} |
| ui_skill_cooldown | 冷却 {0:0.0}秒 |
| ui_player_mana | 灵力 {0:0}/{1:0} |
| ui_skill_empty | 空 |
| ui_skill_mana_insufficient | 灵力不足，无法施展功法。 |
| ui_skill_panel_title | 功法 |
| ui_skill_panel_help | 拖动已学功法到下方快捷栏；按 K 关闭。 |
| ui_skill_level | 第 {0} 重 |
| ui_skill_selection | 已选：{0} · 第 {1} 重 · 升级需 {2} 页功法残页 |
| ui_skill_no_selection | 尚未选择功法 |
| ui_skill_upgrade | 参悟升级 |
| ui_skill_upgrade_material_missing | 功法残页不足，需要 {0} 页。 |
| ui_skill_upgrade_max | 此功法已修至当前上限。 |
| ui_skill_upgrade_success | {0}提升至第 {1} 重。 |
| status_name_burn | 灼烧 |
| status_name_chill | 寒意 |
| status_name_freeze | 冻结 |
| status_name_shock_stun | 感电 |
| status_name_poison | 中毒 |
| status_name_wet | 潮湿 |
| status_name_wood_mark | 木灵印 |
| status_name_sever_def | 破甲 |
| status_name_heart_demon | 心魔 |
| reaction_name_melt | 融化 |
| reaction_name_burn_burst | 燃爆 |
| reaction_name_shock | 感电 |
| reaction_name_spread | 扩散 |
| reaction_name_sever | 断灵 |
| tutorial_move_move | 使用 WASD 或左摇杆移动。 |
| tutorial_move_look | 移动鼠标或右摇杆转动视角。 |
| tutorial_move_jump | 按空格键或手柄南键跳跃。 |
| tutorial_move_complete | 基础身法已掌握。 |
| tutorial_combat_light_attack | 按鼠标左键或手柄右扳机进行轻击。 |
| tutorial_combat_defeat_dummy | 击破前方木桩，完成战斗练习。 |
| tutorial_combat_complete | 基础战斗已掌握。 |
| tutorial_skill_overview | 按 K 打开功法面板，将已学功法拖入快捷栏后施展。 |
| tutorial_inventory_overview | 按 B 打开背包，可使用丹药或穿戴装备。 |
| tutorial_cultivation_overview | 气机圆满时按 C 打开角色面板，查看条件并尝试突破。 |
| tutorial_dungeon_overview | 黑风秘境共五层；完成当层目标会记录检查点。 |
| tutorial_flight_overview | 筑基后持有飞剑，可召剑起飞；禁飞区域需先落地。 |
| tutorial_alchemy_overview | 选择丹方并备齐材料；丹火失衡时主材返还、辅材消耗。 |
| tutorial_optional_complete | 指引已记录。 |
| ui_tutorial_dismiss | 知道了 |
| ui_mount_spirit_horse_mounted | 灵马已应召，移动速度提升。 |
| ui_mount_flying_sword_unlocked | 筑基功成，已解锁飞剑御空。 |
| ui_mount_realm_blocked | 需达到筑基境方可驾驭飞剑。 |
| ui_mount_flight_blocked | 此地灵机紊乱，无法御剑飞行。 |
| ui_mount_flight_started | 御剑升空：移动控制方向，空格上升，F 下降。 |
| ui_mount_dismounted | 已收起坐骑。 |
| ui_faction_danding_joined | 已记名丹鼎宗，可积累宗门声望。 |
| ui_faction_rank_up | 丹鼎宗声望提升至第 {0} 阶。 |
| ui_title_unlocked | 获得称号：{0} |
| ui_title_equipped | 已佩戴称号：{0} |
| ui_character_active_title | 称号：{0} |
| title_name_title_yaotu_apprentice | 斩妖学徒 |
| title_desc_title_yaotu_apprentice | 初经百战，已懂得在妖兽利爪下守住心神。 |
| title_name_title_yaotu | 斩妖人 |
| title_desc_title_yaotu | 千战留名，寻常妖物闻风退避。 |
| title_name_title_wendao | 问道者 |
| title_desc_title_wendao | 走过青石十问，初心仍在大道之上。 |
| title_name_title_poshi | 破石 |
| title_desc_title_poshi | 击破黑风石将军后留下的战名。 |
| title_name_title_tiegu | 铁骨 |
| title_desc_title_tiegu | 废脉炼体，骨血如铁，最大气血提高百分之五。 |
| achievement_name_ach_first_kill | 初入仙途 |
| achievement_desc_ach_first_kill | 首次击败敌人。 |
| achievement_name_ach_kill_100 | 小试牛刀 |
| achievement_desc_ach_kill_100 | 累计击败一百名敌人。 |
| achievement_name_ach_kill_1000 | 斩妖能手 |
| achievement_desc_ach_kill_1000 | 累计击败一千名敌人。 |
| achievement_name_ach_realm_found | 筑基有成 |
| achievement_desc_ach_realm_found | 成功踏入筑基境。 |
| achievement_name_ach_realm_core | 金丹初成 |
| achievement_desc_ach_realm_core | 成功凝结金丹。 |
| achievement_name_ach_quest_ch1 | 青石问道 |
| achievement_desc_ach_quest_ch1 | 完成青石主线十问。 |
| achievement_name_ach_alchemy_10 | 丹火初燃 |
| achievement_desc_ach_alchemy_10 | 成功炼丹十次。 |
| achievement_name_ach_secret_chest | 云雾藏宝 |
| achievement_desc_ach_secret_chest | 在苍梧断崖发现旧匣。 |
| achievement_name_ach_boss_stone | 石将军终结者 |
| achievement_desc_ach_boss_stone | 击败黑风石将军。 |
| achievement_name_ach_waste_body | 废脉锻体 |
| achievement_desc_ach_waste_body | 以废脉之身炼成铜皮铁骨。 |
| achievement_unlocked_ach_first_kill | 成就解锁：初入仙途 |
| achievement_unlocked_ach_kill_100 | 成就解锁：小试牛刀 |
| achievement_unlocked_ach_kill_1000 | 成就解锁：斩妖能手 |
| achievement_unlocked_ach_realm_found | 成就解锁：筑基有成 |
| achievement_unlocked_ach_realm_core | 成就解锁：金丹初成 |
| achievement_unlocked_ach_quest_ch1 | 成就解锁：青石问道 |
| achievement_unlocked_ach_alchemy_10 | 成就解锁：丹火初燃 |
| achievement_unlocked_ach_secret_chest | 成就解锁：云雾藏宝 |
| achievement_unlocked_ach_boss_stone | 成就解锁：石将军终结者 |
| achievement_unlocked_ach_waste_body | 成就解锁：废脉锻体 |
| ui_debug_console_title | 调试控制台（&#96; 开关 · Enter 执行 · Esc 关闭） |
| ui_debug_console_placeholder | 输入命令，例如 /help |
| ui_debug_console_ready | 输入 /help 查看可用命令。 |
| ui_debug_console_clear | 控制台记录已清空。 |
| debug_console_help | 可用命令：/god [on或off]、/killall、/setrealm <境界> <层>、/givexp <数值>、/give <物品ID> <数量>、/spawn <敌人ID> [数量]、/tp <地图ID> <出生点ID>、/save、/timescale <0-10>、/tutorial_skip。 |
| debug_console_success_god | 无敌模式已{0}。 |
| debug_console_success_killall | 已击败 {0} 个敌人。 |
| debug_console_success_setrealm | 境界已设为 {0} 第{1}层。 |
| debug_console_success_givexp | 已增加 {0} 点基础修为（灵根倍率照常生效）。 |
| debug_console_success_give | 已发放 {0} ×{1}。 |
| debug_console_success_spawn | 已生成 {0} ×{1}。 |
| debug_console_success_tp | 正在传送至 {0} / {1}。 |
| debug_console_success_save | 已保存至存档位 {0}。 |
| debug_console_success_timescale | 时间倍率已设为 {0}。 |
| debug_console_success_tutorial_skip | 移动与战斗教程已跳过，完成态已写入存档。 |
| debug_console_error_usage | 命令用法不正确：{0} |
| debug_console_error_unknown | 未知命令：{0}。使用 /help 查看列表。 |
| debug_console_error_invalid_argument | 命令参数无效：{0} |
| debug_console_error_service_unavailable | 调试依赖尚未就绪：{0} |
| debug_console_error_operation_failed | 调试操作失败：{0} |
| hint_quest_main_08_yaolao | 向药老请教筑基机缘。 |
| hint_quest_main_09_yaolao | 向药老求取凝金丹，再图金丹大道。 |

### 16.1 教程 ID（完成态统一写 `world.json tutorialsCompleted`）

| TutorialId | 首次触发 |
|------------|----------|
| tut_move | 首次进入青石 |
| tut_combat | 首次进入战斗 |
| tut_skill | 学会第一功法 |
| tut_inv | 首次获得物品 |
| tut_cult | 首次可突破 |
| tut_dungeon | 首次进入黑风 |
| tut_flight | 筑基且拥有飞剑 |
| tut_alchemy | 首次交互丹炉 |

### 16.2 坐骑 ID

| MountId | 解锁 | 地面倍率 | 飞行 |
|---------|------|----------|------|
| mount_spirit_horse | 新档默认 | 1.5× | 否 |
| mount_flying_sword | 达到筑基后自动解锁并设为当前选择 | 1.0× | 是，限高 40m |

---

## 17. Acceptance（内容）

- [x] 上表 ID 均有 SO 或明确占位  
- [x] 主线 10 环含金丹节拍可配置衔接  
- [x] 6+ 技能、3 图刷怪、10+ 成就、奇遇配额  
- [x] 配方材料闭环  
- [x] must-ship 文案 key 齐全  

G07-02 由 `MvpContentAudit` 逐项解析 Quest/NPC/Dialogue/Item/Recipe/Skill/
Enemy/Achievement/Title/Serendipity 与音画 ID 图，并由场景用例验证苍梧、黑风密度；
2026-07-15 回归为 EditMode 16/16、依赖 PlayMode 50/50。
