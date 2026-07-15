# 05 · 修炼、灵根、境界、炼体、属性

> **版本**: v3.1

---

## 1. PlayerStats（属性聚合）

所有来源加算后写入缓存；**任何来源变化必须 `Recalculate()`**。

```csharp
public class PlayerStats : SafeBehaviour
{
    public StatBlock BaseFromRealm { get; }
    public StatBlock FromEquipment { get; }
    public StatBlock FromTitle { get; }
    public StatBlock FromBuffs { get; }
    public StatBlock Final { get; }

    public float CurrentHp { get; private set; }
    public float CurrentMana { get; private set; }

    public void Recalculate();
    public void SetHp(float v);
    public void SetMana(float v);
    public void ApplyHpDelta(float delta);
    public void ApplyManaDelta(float delta);

    /// 基线格挡物理减伤 0.60；废灵根 +blockPhysDrBonus 后 clamp 0.85 → 0.70
    public float GetBlockPhysDr();
}
```

**合成顺序**：

```
Final = (RealmBase + Equipment + Title + 其它固定) * (1 + 百分比加成) + Buff 平坦值
炼体: MaxHp *= (1+hpBonus); 物理受伤 *= (1-physDR)
灵根: 元素伤害加成、修炼速度、炼体效率、废被动见 §2
```

```csharp
public class StatModifier
{
    public string Id;
    public StatModType Type; // Flat, Percent
    public string StatName;
    public float Value;
    public string SourceId;
}
```

---

## 2. 灵根 SpiritRootSystem

```csharp
public class SpiritRootSystem : SafeBehaviour
{
    public SpiritRootType Root { get; private set; }
    public void ChooseRoot(SpiritRootType type); // 仅五系
    public void RandomizeRoot();                 // 按 weight，可出天/废
    public float GetCultivationMultiplier();
    public float GetBodyMultiplier();
    public float GetElementBonus(ElementType e);
    public float GetBodyPotionMul();             // 废 1.25，其它 1.0
    public float GetPhysicalDamageBonus();       // 废 0.10
    public float GetBlockPhysDrBonus();          // 废 0.10
}
```

**规则**：

- 五灵根自选；天/废 **仅随机**（不可点选）  
- 创角后不可更改（MVP）  
- 文案 key：`root_intro_five` / `root_intro_waste` / `root_intro_heaven`  
- 天灵根：创角 UI 文案「修炼速度显著提升」；**角色/进阶面板显示 1.35×**  

### 2.1 乘数与被动（权威摘要；JSON 见 `03§4.2`）

| Root | cultivationMul | bodyMul | elementBonus | 额外被动 |
|------|----------------|---------|--------------|----------|
| Metal/Wood/Fire | 1.1 | 1.0 | 对应 0.15 | 无 |
| Water | 1.1 | 1.0 | Water 0.15 **+ Ice 0.10** | 无 |
| Earth | 1.1 | **1.15** | Earth 0.15 | 无（文案厚土，非废级） |
| Heaven | 1.35 | 1.0 | All 0.10 | 无战斗额外被动 |
| Waste | 0.55 | **1.5** | 无 | 见下 |

**废脉 `passive_waste_iron_vein`**：

| 字段 | 值 | 施加 |
|------|-----|------|
| blockPhysDrBonus | 0.10 | 格挡：`final = min(0.85, 0.60+0.10)=0.70`，管道 step 8 |
| physicalDamageBonus | 0.10 | 普攻/重击/无元素技能 |
| bodyPotionMul | 1.25 | 锻体丹 AddBodyXp |
| 铁肤 | 不白送 | 仍炼体 1 解锁（因 body 快而更早获得） |

**Acceptance**：

| 路线 | 验收 |
|------|------|
| Fire | cultivationMul=1.1；火技能 bonus 0.15 |
| Waste | body XP ≥ 对照×1.5；锻体丹×1.25；格挡 DR=0.70；木桩实伤约为对照 0.75 倍 |
| Heaven | cultivationMul=1.35；面板显示 1.35× |

---

## 3. 修炼 CultivationManager

### 3.1 状态机（逻辑 3 态）

| 状态 | 时长 | 说明 |
|------|------|------|
| Idle | — | 正常 |
| BreakingThrough | **3.0 s** 无敌 | 演出拍 1–3 |
| BreakthroughResult | **1.5–2.0 s** | 演出拍 4–5 + 结算 |

| 转换 | 条件 |
|------|------|
| Idle→BreakingThrough | `TryBreakthrough` 且 Blockers 空 |
| BreakingThrough→Result | 3.0s 到点 + 掷骰 |
| Result→Idle | 演出结束 |

**GameState**：保持 `Playing` + 输入锁；**不**进 `Cutscene`（Esc 仍可 Pause，Pause 暂停协程）。

### 3.2 五拍演出时间线（纯表现）

| 拍 | 时间轴 | 表现 |
|----|--------|------|
| 1 准备 | 0–0.3s | 锁战斗输入 |
| 2 聚气 | 0.3–1.8s | 法阵 VFX、镜头缓推 |
| 3 天象 | 1.8–3.0s | 粒子、震屏渐进；**3.0s 掷骰** |
| 4 结果 | Result 0–1.2s | 成功金光 / 失败黑气；事件 |
| 5 余韵 | Result 1.2–2.0s | 属性对比面板；Toast |

**不发布** `OnBreakthroughPhase`。UI 读 `CeremonyBeat`（0=none，1–5）。

### 3.3 API

```csharp
public struct BreakthroughBlocker {
    public string Code; // NotMaxSubStage, MissingItem, InCombat, WrongState, ...
    public string MessageKey;
    public string RelatedItemId;
    public string[] AcquisitionHintKeys;
}

public class CultivationManager : SafeBehaviour
{
    public RealmType Realm { get; }
    public int SubStage { get; }
    public float CurrentXp { get; }
    public float XpToNext { get; }
    public bool IsBreakingThrough { get; }
    public int CeremonyBeat { get; }

    public void AddXp(float amount, XpSourceType source);
    public float ApplyDeathXpPenalty(float percent); // 仅扣当前层 CurrentXp，不降层
    public bool CanLevelSubStage();
    public bool TryAdvanceSubStage();
    public bool CanBreakthrough();
    public IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers();
    public float GetBreakthroughSuccessRate(); // 含 pity → 可 1.0
    public bool TryBreakthrough();
}
```

### 3.4 修为获取管道

```
1. 原始 amount
2. * SpiritRoot.cultivationMul
3. * (1 + Stat.CultivationSpeed)
4. * 其它 Buff
5. 写入 CurrentXp → OnXpGained
6. 层内升级逻辑
```

**G-VS-05 代码优先落地边界**：`AddXp` 完成灵根倍率与 `CultivationSpeed` 乘算后，自动消耗每层阈值、保留溢出修为并刷新境界基础属性；练功木桩击杀提供 `09§5` 的原始修为 `25`。本 Goal 只处理同一境界内的子阶，最大层修为封顶到当前阈值；大境界突破状态机、材料、pity 与五拍仪式仍由后续 Goal 实现。

### 3.5 突破检查与材料

`CanBreakthrough` / Blockers 须全部通过：

1. 存在 `breakthroughToNext`（金丹→元婴为 **null** → 不可破）  
2. SubStage ≥ minSubStage  
3. 若有 `requiredItemId`，背包 ≥1（**成功才消耗；失败不消耗**）  
4. 脱战 ≥5s；非 Dialogue/Dead  

**RealmConfig 材料（`03` 权威）**：

| 从 → 到 | requiredItemId |
|---------|----------------|
| 练气 → 筑基 | `item_pill_foundation` |
| 筑基 → 金丹 | `item_pill_goldencore` |
| 金丹 → 元婴 | **null**（不做） |

**辅助丹成功率加成：MVP = 0**（无配方）。

### 3.6 筑基 pity（KD-17）

| 事件 | 行为 |
|------|------|
| `quest_main_01_08` Accept | 若 Realm &lt; Foundation：置 `questFlag_main_foundation_pity=true`；若已 Foundation+：不置 Flag，境界目标直接满足 |
| `GetBreakthroughSuccessRate` | Flag 且目标为筑基：返回 **1.0**（绕过 0.95 clamp） |
| 进入 BreakingThrough 且 CanBreakthrough | **先消费 Flag**，再演出；必成功 |
| 场景卸载且已消费 Flag、未完成掷骰 | **恢复 Flag**；不扣材料/XP |
| 金丹突破 | **永不**读 pity |

### 3.7 突破结算

```
rate = GetBreakthroughSuccessRate() // 含 pity
成功: Realm++, SubStage=1, XP=0, 消耗材料, Recalculate, OnRealmBreakthrough
失败: XP *= (1-failPenalty), HeartDemon, 不耗材料, OnRealmBreakthroughFailed
失败 Toast: SuccessRate、下次建议（Blockers/Hint）、心魔剩余、距再破 XP 估计
```

### 3.8 EdgeCases

| 条件 | 行为 |
|------|------|
| 突破中传送请求 | 排队，结果后再传 |
| 突破中场景卸载 | 强制中断：不扣 XP/材料；pity 恢复 |
| AddXp 负数 | 忽略 |
| 无 breakthroughToNext | CanBreakthrough=false |

---

## 4. 炼体 BodyRefinementManager

```csharp
public class BodyRefinementManager : SafeBehaviour
{
    public BodyLevel Level { get; }
    public float Xp { get; }
    public float HpBonus { get; }
    public float PhysicalDR { get; }
    public float ControlResist { get; }
    public bool HasCombatRevive { get; }

    public void AddBodyXp(float amount); // 丹药 * bodyPotionMul
    public bool TryLevelUp();
    public bool TryConsumeRevive();
}
```

| 来源 | 公式 |
|------|------|
| 受伤 | damageTaken * 0.1 * bodyMul |
| 炼体丹 | Value * bodyPotionMul |
| 雷劫 | 预留 |

`BodyRefinementConfig.levels[n].xpToNext` 表示**解锁该目标等级所需的累计炼体 XP**；
凡躯阈值为 0，`TryLevelUp()` 检查下一等级阈值且不扣除累计 XP。

---

## 5. 打坐

MVP **不做**独立打坐；修为来自战斗/任务/丹药。

---

## 6. Acceptance

- [ ] 五灵根自选倍率正确；天/废仅随机  
- [ ] 废：body×1.5、丹×1.25、格挡 DR=0.70  
- [ ] Water 保留 Ice 0.10  
- [ ] 练气 9 层数值与 RealmConfig 一致  
- [ ] 突破成功/失败、材料成功才扣、心魔  
- [ ] 五拍演出 + CeremonyBeat；3s 无敌  
- [ ] pity 边界（含中断恢复）  
- [ ] 金丹可破；元婴 CanBreakthrough=false  
- [ ] GetBreakthroughBlockers 可驱动失败 Toast  
- [ ] 存档境界/XP/炼体/灵根/pity Flag 一致  
