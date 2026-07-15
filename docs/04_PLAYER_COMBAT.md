# 04 · 角色控制、摄像机与战斗

---

## 1. 输入映射（Input System）

| Action | 键鼠 | 手柄 | 说明 |
|--------|------|------|------|
| Move | WASD | 左摇杆 | Vector2 |
| Look | 鼠标 | 右摇杆 | Vector2 |
| Jump | Space | A/South | |
| Sprint | LeftShift 按住 | 左摇杆按下 | |
| LightAttack | 鼠标左 | RT/R2 | 连段 |
| HeavyAttack | 鼠标右 | Y/North | |
| Dodge | LeftCtrl / Q | B/East | 带无敌帧 |
| Block | Mouse4 / F 按住 | LT/L2 | |
| LockOn | 中键 / Tab | 右摇杆按下 | 切换锁定 |
| Skill1–4 | 1 2 3 4 | 方向键/LB+XYAB | |
| Interact | E | X/West | NPC/采集 |
| OpenInventory | B | View | |
| OpenCharacter | C | — | 属性/修炼 |
| OpenSkill | K | — | |
| OpenMap | M | — | |
| OpenQuest | J | — | |
| Pause | Esc | Start | |
| Mount | V | — | 召唤/下马；筑基后默认飞剑并起飞 |

改键：`settings.json` 存 Input System rebind 覆盖。

代码优先阶段的权威资产为 `Resources/Input/PlayerInputActions.inputactions`；`PlayerInputReader` 在运行时克隆该资产并以 `IPlayerInputSource` 注册到 `ServiceLocator`。G-VS-01 消费 Move / Look / Jump / Sprint，G-VS-02 新增消费 LightAttack，G-VS-04 新增消费 Skill1，G01-01 新增消费 HeavyAttack / Dodge / Block，G01-02 新增消费 LockOn，G03-02 新增消费 Skill2–4 与 OpenSkill，G06-03 消费 Mount 并为飞行暴露 JumpHeld。飞行时按住 Space 上升、按住 F 下降，水平速度复用既有 Sprint，垂直速度复用既有 Walk。

---

## 2. 玩家状态机

| 状态 | 可转换到 | 条件 |
|------|----------|------|
| Idle | Move, Attack, Skill, Dodge, Block, Jump, Interact, Mount, Dead | 输入 |
| Move | Idle, Sprint, Attack, Skill, Dodge, Jump, Dead | |
| Sprint | Move, Idle, Dodge, Jump, Dead | 有体力/无硬直 |
| Jump | Fall, Attack(空中弱), Dead | |
| Fall | Idle, Move, Dead | 落地 |
| LightAttack | LightAttack(连段窗), Idle, Dodge, Dead | 连段 1–4 |
| HeavyAttack | Idle, Dodge, Dead | 后摇结束 |
| Dodge | Idle, Move | 动画结束 |
| Block | Idle, BlockHit, Dead | 松键→Idle |
| BlockHit | Block, Idle | |
| SkillCast | Idle, Dead | 后摇结束；可被 Dodge 取消若配置允许 |
| Stagger | Idle | 硬直结束 |
| Dead | — | 外部复活 |
| Mounted | MountedMove, Dismount | |
| Flying | FlyMove, Land | 需筑基+飞剑 |

**硬规则**：

- 攻击/技能前摇中：**不可**移动转向（可配置极小转向）  
- 闪避：**取消**当前攻击后摇（取消窗从后摇开始）  
- 对话/Paused：输入仅 UI  

### 2.1 API

```csharp
public class PlayerController : SafeBehaviour
{
    public PlayerState State { get; }
    public bool IsInvincible { get; }
    public Transform LockTarget { get; }

    public void SetInputEnabled(bool enabled);
    public void ForceState(PlayerState state); // 过场/死亡
    public void TeleportTo(Vector3 pos, Quaternion rot);
}
```

### 2.2 移动数值（默认）

| 参数 | 值 |
|------|-----|
| Walk/Run 速度 | 5 m/s |
| Sprint | 8 m/s |
| 加速度 | 40 |
| 转向速度 | 720 °/s |
| 跳跃初速度 | 7 |
| 重力 | -20 |
| 闪避距离 | 5 m / 0.35 s |
| 闪避无敌 | 0.2 s |
| 闪避 CD | 0.8 s |
| 格挡减伤 | 60% 物理 / 30% 元素 |
| 格挡时移速 | ×0.4 |

---

## 3. 摄像机

### 3.1 API

```csharp
public class ThirdPersonCamera : MonoBehaviour
{
    public void SetTarget(Transform player);
    public void SetLockOnTarget(Transform target);
    public void ClearLockOn();
    public void Shake(float intensity, float duration);
    public void SetCombatMode(bool inCombat);
    public void SetDialogueFocus(Transform npcFace, bool enable);
    public void SetMounted(bool mounted);
    public void SetFlying(bool flying);
}
```

### 3.2 行为表

| 情境 | Offset | FOV | 其它 |
|------|--------|-----|------|
| 探索 | (0, 1.8, -5) | 55 | 自由旋转 |
| 战斗 | (0, 1.8, -4.5) | 65 | |
| 锁定 | 看向 玩家↔目标中点 | 60 | 自动 yaw |
| 对话 | 面向 NPC | 45 | 禁旋转 |
| 骑乘 | (0, 2.8, -6) | 60 | |
| 飞行 | (0, 5, -10) | 65 | |
| 贴墙 | 距离 ≥1.5 | — | 遮挡半透明 α=0.3 |

震屏：闪避 0.3/0.1s；暴击 0.5/0.15s；BOSS 0.8/0.3–0.6s；突破 1.0/1.5s。

### 3.3 锁定规则

- 神识范围内最近敌人；再按切换下一目标  
- 目标死亡/超距 `DisengageRange` → ClearLockOn  
- 锁定时轻攻击略微转向目标  

代码基线：基础神识锁定范围为 **14m**，`StatBlock.DivineSense` 作为米数增量；候选同时受目标自身 `DisengageRange` 限制，避免锁上后立即超距。`ILockOnTarget` 提供存活态、镜头锚点与清锁距离；`PlayerTargetingController` 仅在 LockOn 按键时收集并按距离排序候选，常帧只校验当前目标。锁定变化发布 `OnLockOnChanged(LockOnInfo)`，供 Camera 与后续锁定血条共用。

`ThirdPersonCamera` 监听锁定、对话与闪避事件：锁定时以玩家焦点和目标锚点的中点构图；`OnDialogueStarted` 通过 `IDialogueFocusTarget` 解析 NPC 头部焦点，结束后恢复；`OnPlayerDodged` 使用本节震屏参数。贴墙距离下限为 1.5m，遮挡 Renderer 通过可恢复的 `MaterialPropertyBlock` 写入 α=0.3，占位/正式材质需支持透明显示。

---

## 4. 战斗系统

### 4.1 伤害管道（严格顺序）

```
1. 收集基础值: BaseDamage（武器/技能）
2. 攻击力系数: Base * (1 + Attack/100)
3. 技能/连段倍率
4. 暴击判定: CritRate（含装备）→ * CritDamage
5. 防御减伤: * (100/(100+Def))；True 跳过
6. 元素加成/抗性
7. 元素反应倍率（若触发）
8. 格挡/护盾（`ICombatDefenseProvider.GetBlockDamageReduction()`；物理基线 **0.60**，废灵根最终 **0.70**，见 `05`）
9. 炼体物理 DR（仅 Physical）
10. 无敌帧为 0；其余正伤害 clamp ≥ 1（治疗除外）
11. 扣 HP；写 DamageInfo（含 SkillId）；Publish 事件；命中触发 hitstop（§4.8）
12. 死亡检查（写 DeathInfo.LastHitSkillId）
```

### 4.2 IDamageable

```csharp
public interface IDamageable
{
    float CurrentHp { get; }
    float MaxHp { get; }
    bool IsDead { get; }
    void ApplyDamage(DamageInfo info);
    void ApplyHeal(float amount, string sourceId);
}
```

目标侧无敌与格挡统一通过 `ICombatDefenseProvider` 查询，避免 `CombatSystem` 直接耦合玩家实现。`True` 伤害跳过格挡；格挡只改变已经过防御减伤后的伤害。

```csharp
public interface ICombatDefenseProvider
{
    bool IsInvincible { get; }
    bool IsBlocking { get; }
    float GetBlockDamageReduction(DamageType damageType);
}
```

### 4.3 CombatSystem API

```csharp
public class CombatSystem : SafeBehaviour
{
    public DamageInfo ComputeDamage(DamageRequest req);
    public DamageInfo ComputeDamage(DamageRequest req, IDamageable target);
    public void DealDamage(IDamageable target, DamageRequest req);
    public bool TryMeleeHit(Transform attacker, float range, float angle, DamageRequest req);
    public void RegisterActor(IDamageable actor);
    public void UnregisterActor(IDamageable actor);
}

public struct DamageRequest
{
    public GameObject Source;
    public float BaseDamage;
    public DamageType Type;
    public ElementType Element;
    public float Multiplier;      // 连段/技能
    public bool CanCrit;
    public string SkillId;
    public bool IgnoreAttackScaling; // DoT 等已结算基础值
    public string StatusOnHitId;
    public float StatusChance;
}
```

`DamageRequest` 按既有契约不重复携带 Target；因此带 `IDamageable target` 的 overload 是含目标 Defense 的最终计算入口，单参数 overload 用于无目标预览（Defense=0）。`DealDamage` 与 `TryMeleeHit` 必须走带目标入口。

需要发布死亡语义的 `IDamageable` 同时实现 `ICombatDeathHandler`。`CombatSystem` 的固定顺序为：扣 HP / 目标受伤事件 → `OnDamageApplied` → 反应副作用与 `OnElementReactionTriggered` → 命中状态 → `ICombatDeathHandler.HandleDeath`，确保死亡、任务和掉落监听不会先于本次伤害反馈。`DamageInfo.Reaction/ReactionMultiplier` 让伤害监听无需依赖事件先后即可识别本击反应。

### 4.4 普攻连段

| 段 | 倍率 | 前摇 | 后摇 | 取消闪避 |
|----|------|------|------|----------|
| L1 | 1.0 | 0.1 | 0.25 | 后摇可 |
| L2 | 1.1 | 0.1 | 0.25 | 后摇可 |
| L3 | 1.25 | 0.12 | 0.3 | 后摇可 |
| L4 | 1.5 | 0.15 | 0.45 | 后摇可 |

连段窗：后摇开始后 0.35s 内再按接下一段，否则回 Idle。  
重击：倍率 2.0，前摇 0.35，后摇 0.5，可破部分敌人霸体（精英以下）。

G-VS-02 只实现 L1：前摇 0.1s、后摇 0.25s、倍率 1.0；不实现连段窗口、重击、闪避、格挡或完整 buffer。每次有效结算发布 `OnDamageApplied(DamageInfo)` 供伤害飘字消费。

G-VS-02 的最小伤害管道实现步骤 1–5 与 10–12；步骤 6–9 在本 Goal 中为 identity（只透传 Type/Element），分别由后续元素、格挡与炼体 Goal 接入，禁止在当前卡提前实现。

G01-01 在该最小管道上接入四段轻击、重击、闪避和格挡。连段输入只在本节规定的 0.35s 连段窗内消费；这不是 G01-05 的通用 120ms 预输入缓冲。攻击前摇不能闪避，命中结算后的后摇可以通过专用状态转换取消。

### 4.5 元素反应

| 已有异常 \ 攻击 | Fire | Ice | Lightning | Wind | Metal |
|-----------------|------|-----|-----------|------|-------|
| Ice | Melt 1.5x | — | Shock* | Spread | — |
| Poison | BurnBurst 1.3x 清毒 | — | — | Spread | — |
| Wet(Water) | — | — | Shock 1.4x+0.5s晕 | Spread | — |
| Wood 标记 | — | — | — | Spread | Sever 1.25x+破防 |

\* Ice+Lightning 亦走 Shock。  
Wind+Any：将目标当前异常复制到半径 4m 敌人（伤害不二次反应）。

同一目标同时带多种可反应异常时，**单次命中只结算一个反应**：Fire 优先 Ice 后 Poison；Lightning 优先 Wet 后 Ice；Wind 按 Ice → Poison → Wet → Wood → 其余元素顺序选展示主异常，但会复制目标全部 `AuraElement != None` 的异常。此顺序消除原矩阵在多异常并存时的歧义，避免乘区连乘失控。BurnBurst 结算后清毒；Shock/Sever 的控制或破防在本击伤害后生效。

G01-03 代码基线：`CombatSystem` 在防御后、格挡前应用反应倍率；`StatusEffectData.AuraElement` 是查询源；Spread 只复制给 `ICombatTeamProvider` 判定为受击方阵营、且在 4m 内的已注册存活 Actor。状态 DoT 的 `Element=None`，因此不会递归触发第二次反应。

**反应可见性（强制挂钩）**：触发反应时 FOV +2 回弹 0.2s + 彩色飘字（G01-05）。

### 4.8 战斗手感包（Feel Package · 数字权威源）

> 归属 Goal **G01-05**。VS 最小集见 G-VS-02（伤害字必做；hitstop 30ms 可选占位）。  
> **闪避取消后摇**已在 §2/§4.4 规定，本包不重复立项。完美闪避 **MVP 不做**。

| G-VS-02 灰盒 L1 参数 | 值 |
|----------------------|----|
| BaseDamage | 10 |
| 判定距离 | 2.5 m |
| 判定扇形 | 100° |

| 参数 | MVP 默认 | 说明 |
|------|----------|------|
| Input Buffer | **120 ms** | 攻击/闪避/技能预输入 |
| Hitstop L1/L2 | **30 ms** | |
| Hitstop L3/L4/重击 | **50–70 ms** | 默认 60ms |
| Hitstun 普通敌 | 0.12–0.25 s（默认 **0.18s**） | 精英 ×0.5 |
| 暴击震屏 | intensity 0.5 / 0.15s | **Crit 必调用** §3.2 既有表 |
| 元素反应镜头 | FOV+2 / 0.2s | 见上 |
| 闪避取消 | — | **已有**，非本包新增 |
| 完美闪避 | — | 不做 |

```csharp
public class CombatFeelSettings // 常量或 SO，非 FeatureFlags 文件
{
    public const float InputBufferSeconds = 0.12f;
    public const float HitstopLight12Seconds = 0.03f;
    public const float HitstopLight34HeavySeconds = 0.06f;
    public const float NormalEnemyHitstunSeconds = 0.18f;
    public const float EliteHitstunMultiplier = 0.5f;
    public const float CriticalShakeIntensity = 0.5f;
    public const float CriticalShakeDuration = 0.15f;
    public const float ElementReactionFovKick = 2f;
    public const float ElementReactionFovDuration = 0.2f;
}

// PlayerController / Combat
void EnqueueBufferedAction(BufferedActionType type);
void PlayHitstop(float seconds);
// Death/Dialogue：清空缓冲；Pause 时协程暂停优先于 hitstop 时间缩放
```

G01-05 代码基线：`PlayerActionBuffer` 以 unscaled time 统一捕获 Light/Heavy/Dodge/Skill1；G03-02 将同一缓冲契约扩展至 Skill2–4。只有动作真正进入状态机时才消费；Pause 冻结窗口，Death/Dialogue/Cutscene/Loading/MainMenu 清空。`PlayerCombatController` 将段数对应的 `HitstopSeconds` 与普通敌 `HitstunSeconds` 写入 `DamageRequest`，`CombatSystem` 仅在正伤害命中后发布并施加，`CombatFeelController` 从 `OnDamageApplied` 驱动全局 hitstop 且保留调用前 `Time.timeScale`。暴击复用 `ThirdPersonCamera.Shake(0.5, 0.15)`；元素反应同时触发 FOV+2/0.2s 回弹与既有 `reaction_name_*` localization key 的彩色飘字。

**Acceptance（G01-05）**：缓冲期内预输入可出招；Editor 可观察 L3/L4 hitstop；暴击必 Shake；元素反应镜头或彩色字可见。

### 4.6 状态效果管理

```csharp
public class StatusEffectManager : SafeBehaviour, IStatusEffectService
{
    public void Apply(string statusId, GameObject target, GameObject source, int stacks = 1);
    public bool TryApply(string statusId, GameObject target, GameObject source, int stacks, float sourceBaseDamage);
    public void Remove(string statusId, GameObject target);
    public void Tick(float dt);
    public bool Has(string statusId, GameObject target);
    public void ClearAll(GameObject target);
    public int GetStacks(string statusId, GameObject target);
    public float GetRemainingDuration(string statusId, GameObject target);
    public bool IsStunned(GameObject target);
}
```

**MVP 状态库**（`09_CONTENT` 可扩展）：

| Id | 效果 |
|----|------|
| status_burn | 火 DoT，3s，每秒 5% 技能基础 |
| status_chill | 移速 -30%，3s；再叠冻结 |
| status_freeze | 昏迷 1.5s，CD 8s 内不重复 |
| status_shock_stun | 0.5s 晕 |
| status_poison | DoT 4s |
| status_wet | 标记，4s |
| status_wood_mark | 木标记，4s（供 Sever） |
| status_sever_def | 防御 -10%，3s |
| status_heart_demon | 伤害 -10%，30s（突破失败） |

叠层规则：重复施加时层数 clamp 到 `MaxStacks` 并刷新持续时间；到期发布 `OnStatusEffectChanged(Expired)` 后移除。`status_chill` 达 2 层时提升为 `status_freeze`；冻结从首次生效起计算 8s 重复免疫，免疫期内寒意仍可叠层但不提升。DoT 每层独立贡献同一跳伤害，统一经 `CombatSystem` 结算且不重复套攻击力。

状态变化统一发布 `OnStatusEffectChanged(StatusEffectInfo)`；跨系统只通过 `IStatusEffectService` 查询控制、移速、攻击、造成伤害与防御倍率。Actor 死亡或注销时清空临时状态；MVP 状态不写入存档。

### 4.7 玩家资源

| 资源 | 上限来源 | 回复 |
|------|----------|------|
| HP | Stats | 脱战 5s 后每秒 2% MaxHp；丹药 |
| Mana | Stats | 脱战每秒 3% MaxMana；丹药 |
| 闪避无额外条 | — | CD |

脱战定义：`IsInCombat==false` 持续 5s（最后一次造成/受到伤害起算）。

G01-04 代码契约：`PlayerStats` 在造成或受到正伤害时将
`GameManager.IsInCombat` 置 true 并重置计时；满 5s 前不回复，跨过边界后
只按超出 5s 的时间片，再依 `FormulaLibrary` 的 MaxHp/MaxMana 比例回复。
Dead、Paused、Dialogue、Cutscene 与 Loading 均不推进计时或回复。回复来源
ID 为 `recovery_out_of_combat`。

---

## 5. EdgeCases

| 条件 | 行为 |
|------|------|
| 对已死亡目标伤害 | 忽略 |
| 无敌帧中 | 伤害 0，可播放「免疫」飘字 |
| 同时多段伤害 | 逐次结算，允许同帧多次 |
| 锁定目标销毁 | ClearLockOn |
| 技能中被击晕 | 打断技能，进 Stagger |
| 突破吟唱中 | 短暂无敌（见修炼文档） |
| Mana 不足 | 不进 SkillCast，UI 红闪 + SFX |
| CD 中 | 忽略输入，快捷栏闪烁 |

---

## 6. Acceptance

- [ ] WASD+鼠标完整移动与镜头  
- [ ] 四连击倍率正确，连段窗可接可断  
- [ ] 闪避 0.2s 无敌可测（/god 关时木桩）  
- [ ] 格挡减伤数值符合表  
- [ ] 锁定切换与死亡清锁定  
- [ ] 火打冰触发 Melt，伤害日志正确  
- [ ] 摄像机遮挡、震屏、对话 FOV  
- [ ] 死亡→复活点流程  
- [ ] 键鼠与手柄均可完成一次击杀  
