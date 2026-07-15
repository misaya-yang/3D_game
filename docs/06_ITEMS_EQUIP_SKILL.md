# 06 · 物品、背包、装备、功法、生活技能

> **版本**: v3.1

---

## 0. 物品获取提示

- `ItemData.AcquisitionHintKeys`：突破丹等关键消耗品必填（如 `hint_quest_main_08_yaolao`）  
- 杂货店 **不卖** 筑基丹/凝金丹（主线 AcceptRewards 发放）  
- 失败突破 **不扣** 突破丹（见 `05`）

---

## 1. 背包 InventoryManager

### 1.1 Schema（运行时）

```csharp
public class InventorySlot
{
    public string ItemId;     // 空=空槽
    public int Count;
    public bool Bound;
    public string InstanceId; // 装备实例；堆叠物可空
    public string ExtraJson;  // 精炼等级等，或指向 EquipmentInstance
}

public class EquipmentInstance
{
    public string InstanceId;
    public string EquipmentDataId;
    public int RefineLevel;
    public int Durability;
    public string[] GemIds;   // MVP 可空数组
}
```

### 1.2 API

```csharp
public class InventoryManager : SafeBehaviour
{
    public const int Capacity = 50;
    public IReadOnlyList<InventorySlot> Slots { get; }
    public int SpiritStones { get; private set; }

    public bool CanAdd(string itemId, int count);
    public bool AddItem(string itemId, int count, AcquireSource source, string instanceId = null);
    public bool RemoveItem(string itemId, int count);
    public bool RemoveAt(int slotIndex, int count);
    public int CountItem(string itemId);
    public void Sort();                       // 按 Type, Rarity, Id
    public bool AddSpiritStones(int delta);   // 不足返回 false
    public EquipmentInstance GetEquipmentInstance(string instanceId);
    public EquipmentInstance CreateEquipmentInstance(string equipmentDataId);
}
```

### 1.3 获取管道

```
1. CanAdd? 否 → 世界掉落物 180s，提示背包满
2. 堆叠：同 ItemId 非装备填满 MaxStack
3. 装备：每件新 InstanceId，占 1 格
4. Publish OnItemAcquired
5. Quest 监听计数
```

### 1.4 使用物品 ItemUseSystem

```csharp
public class ItemUseSystem : SafeBehaviour
{
    public bool CanUse(int slotIndex);
    public bool Use(int slotIndex); // 战斗中可用的 Consumable 白名单
}
```

管道：查 ItemData → 检查境界 → 应用 UseEffects → 扣数量 → OnItemUsed。

**EdgeCases**：HP 已满用血瓶仍消耗（MVP 简化）或提示不用——**采用：已满则不消耗并提示**。

---

## 2. 装备 EquipmentManager

### 2.1 API

```csharp
public class EquipmentManager : SafeBehaviour
{
    public IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> Worn { get; }

    public bool EquipFromInventory(int inventorySlot);
    public bool Unequip(EquipmentSlot slot);      // 回背包，满则失败
    public int GetRefineLevel(EquipmentSlot slot);
    public StatBlock GetEquipmentStats();         // 含精炼
}
```

### 2.2 精炼 RefineSystem

```csharp
public class RefineSystem : SafeBehaviour
{
    public float GetSuccessRate(int currentLevel);
    public bool TryRefine(EquipmentSlot slot); // 消耗 item_mat_refine_stone
}
```

- 成功：RefineLevel++，属性 `* (1 + 0.05 * level)` 相对 Base  
- 失败：MVP **不掉级不毁装**，只消耗材料  
- 成功率：`max(0.4, 0.95 - 0.03 * currentLevel)`  
- 材料：每级 1 + level/2 个精炼石（向下取整）
- `TryRefine` 返回本次是否成功；合法尝试无论成败都消耗材料，并发布
  `OnEquipmentUpgraded`。未穿戴、材料不足、已满级不构成尝试且不消耗。
- 精炼直接修改穿戴实例并立即保存 `inventory`（材料）与成功时的
  `equipment`（等级）；实例仍遵守背包/穿戴所有权互斥。

### 2.3 套装 / 镶嵌

- 数据结构保留 `SetId`、`MaxGemSockets`  
- MVP **不实现**套装效果结算与镶嵌 UI（Goal 后置）  
- `GetEquipmentStats` 仅 Base+Refine  

### 2.4 EdgeCases

| 条件 | 行为 |
|------|------|
| 境界不足 | 不可装备，Toast |
| 卸下背包满 | false + 提示 |
| 精炼满级 | false |
| 材料不足 | false |

---

## 3. 功法 SkillManager

### 3.1 运行时

```csharp
public class SkillRuntime
{
    public string SkillId;
    public int Level;
    public float Exp;           // MVP 可用击杀次数代替升级材料
    public float CooldownRemaining;
}
```

快捷栏：4 个 Active 槽；Ultimate 占 Skill4 或独立——**MVP：槽 1–3 Active，槽 4 Ultimate 或 Active**。  
`SkillData.PreferredRoots`（`03`）：可选，用于创角推荐对应元素功法。

### 3.2 API

```csharp
public class SkillManager : SafeBehaviour
{
    public IReadOnlyList<SkillRuntime> Learned { get; }
    public string[] EquippedIds { get; } // length 4

    public bool Learn(string skillId);
    public bool Equip(string skillId, int barIndex);
    public bool Unequip(int barIndex);
    public bool CanCast(int barIndex);
    public bool TryCast(int barIndex, Vector3 targetPoint, GameObject targetActor);
    public void TickCooldowns(float dt);
    public bool TryUpgrade(string skillId); // 消耗 item_skill_scroll × 当前等级
}
```

### 3.3 释放管道 SkillCastPipeline

```
1. CanCast: 已学、已装备、CD=0、Mana 够、非异常 Silence、非 Dead/Dialogue
2. 进入 PlayerState.SkillCast，播动画
3. CastTime 后：生成投射物或范围判定
4. CombatSystem.DealDamage / 上状态
5. 扣 Mana、设 CD、OnSkillCast
6. RecoveryTime 后回 Idle
7. 闪避可取消后摇（配置 Cancelable=true 默认 true）
```

### 3.4 升级

- MaxLevel 默认 5  
- 伤害：`BaseDamage + DamagePerLevel * (Level-1)`  
- 升级消耗：`item_skill_scroll` × Level（见内容表）  

### 3.5 G-VS-04 基线与 G03-02 扩展

- 首发仅注册 `skill_basic_qi_bolt`，默认已学会并装备在快捷栏槽 1；其余 3 槽为空。
- 权威数值：BaseDamage `30`、ManaCost `10`、Cooldown `1.5s`、CastTime `0.2s`、RecoveryTime `0.3s`、Range `12m`、Radius `0`、Projectile `true`。
- 伤害类型为 `Physical`，元素为 `None`；投射物/命中占位 ID 为 `VFX_Skill_QiBolt_Projectile` / `VFX_Skill_QiBolt_Impact`。
- 初始 MaxMana `50`；扣蓝发生在前摇结束、投射物生成之前。前摇中或 CD 中再次输入均失败；灵力不足不进入 `SkillCast`，发布 `ui_skill_mana_insufficient`。
- G03-02 注册 `09_CONTENT§4` 的 7 门功法；6 门 Active/Ultimate 可装入任意
  4 槽，Passive 只可学习。PlayerInputReader/PlayerActionBuffer 消费 Skill1–4。
- `panel_skill` 用 K 开关并锁定玩法输入；已学条目实现 drag source，HUD 四槽实现
  drop target。重复装备同一功法时从旧槽移除，装备变化即时保存 `skills.json`。
- `TryUpgrade` 消耗 `item_skill_scroll × 当前等级`；材料不足或满级不消耗，成功
  Level++、发布 `OnSkillUpgraded`，并即时保存 `inventory.json` 与 `skills.json`。

---

## 4. 生活技能

### 4.1 炼丹 AlchemySystem（MVP 必做）

```csharp
public class AlchemySystem : SafeBehaviour
{
    public int Level { get; }
    public float Xp { get; }

    public bool CanCraft(string recipeId);
    public float GetSuccessRate(string recipeId);
    public bool Craft(string recipeId); // 同步或读条 CraftTime
}
```

管道：

```
1. 等级 ≥ RequiredCraftLevel
2. 材料足够
3. 扣材料
4. roll ≤ BaseSuccessRate + successBonus(level)
5. 成功：给产物 + 炼丹 XP + OnCraftCompleted
6. 失败：按 Ingredient.ConsumedOnFail 处理返还 + OnCraftFailed
7. XP 够则升级
```

G03-03 的 MVP 实现采用瞬时结算，注册 `09_CONTENT§7` 全部 4 张丹方。
成功熟练度为 `100 × RequiredCraftLevel`，`CraftLevelConfig.xpRequired` 继续表示
累计阈值，升级不扣 XP；成功率为
`Clamp01(BaseSuccessRate + 当前等级 SuccessBonus)`。失败时灵尘作为催化剂消耗，
清心草、狼毫与一品妖核按 `ConsumedOnFail=false` 静默退回（不发布新的
`OnItemAcquired`）。每次有效尝试结束后同步保存
`inventory.json` / `alchemy.json`；材料、等级或背包条件不满足时不扣物品、不发成功/
失败事件。

### 4.2 采集 GatheringSystem（MVP 必做）

```csharp
public class GatheringSystem : SafeBehaviour
{
    public int Level { get; }
    public bool CanGather(GatherableObject obj);
    public bool Gather(GatherableObject obj); // 读条 1.5s，可被伤害打断
}
```

`GatherableObject`：itemId、count 范围、requiredLevel、respawnSeconds、tool 可选。

G03-04 固定基础读条 `1.5s`：开始后临时锁定玩法输入，完成时以
`OnItemAcquired(AcquireSource.Gather)` 发放随机区间产物并即时保存
`inventory.json`。`OnPlayerDamaged` 且实伤 `>0` 时立即打断，不给物品、不进入
刷新冷却；成功后节点隐藏并按自身 `respawnSeconds` 原地恢复。青石占位节点均为
采集等级 1、无工具门槛；等级/工具/背包条件不满足时不进入读条。

### 4.3 炼器 / 制符 / 阵法

- **API 与 SO 预留**（见 03）  
- MVP **不实现 UI 与完整逻辑**  
- 禁止在 MVP Goal 中展开  

---

## 5. 商店（NPC Vendor 简化）

```csharp
public class ShopSystem : SafeBehaviour
{
    public bool Buy(string npcId, string itemId, int count);
    public bool Sell(int inventorySlot, int count); // 卖价 = SellPrice
}
```

MVP 张掌柜货单固定为：回血丹 10、精炼石 25、铁剑 80 灵石；物品
`BuyPrice` / `SellPrice` 是交易权威，具体内容表见 `09§13`。绑定、任务、
货币物品或 `SellPrice<=0` 不可出售。声望折扣见 `08_UI_META` Faction；
G03-05 不提前实现折扣。

交易必须是原子的：买入前同时校验 NPC 货单、灵石和背包容量；卖出前同时
校验格子、数量、绑定状态、卖价与货币溢出。任一步失败都不得改变灵石或
背包；成功后同步写 `inventory.json`（含灵石）与 `profile.json`，并发布
`OnShopTransactionCompleted`。张掌柜交互通过 `OnShopOpened` 打开
`panel_shop`，关闭时归还玩法输入。

---

## 6. Acceptance

- [ ] 50 格增删堆叠排序  
- [ ] 背包满掉地 180s  
- [ ] 丹药使用改 HP/Mana/修为  
- [ ] 装备穿脱即时改攻击力  
- [ ] 精炼成功属性 +5%/级  
- [ ] 学技能、拖快捷栏、放技能扣蓝进 CD  
- [ ] 炼丹成功失败分支  
- [ ] 采集读条与刷新  
- [ ] 张掌柜可以买卖；灵石不足/背包满时交易不产生部分写入  
- [ ] 相关数据全部可存档  
