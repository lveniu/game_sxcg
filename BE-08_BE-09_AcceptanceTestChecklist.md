# BE-08 (机制怪系统) & BE-09 (面效果系统) 验收测试关注点清单

> 审查基于 dev 分支代码，审查日期: 2026-05-09
> 审查人: AI Code Reviewer

---

## 一、发现的编译级 BUG (必须在验收前修复)

### BUG-1: BalanceProvider 类型不匹配 — 编译失败

**文件**: `BalanceProvider.cs` 第27行 / 第40行 / 第462行

```
private static MechanicEnemiesConfig _mechanicEnemies;          // ← 类型: MechanicEnemiesConfig
= ConfigLoader.LoadMechanicEnemies();                            // ← 返回: MechanicEnemiesFileConfig (不兼容!)
```

- `ConfigLoader.LoadMechanicEnemies()` 返回 `MechanicEnemiesFileConfig`，但 `BalanceProvider._mechanicEnemies` 声明为 `MechanicEnemiesConfig`
- 这两个是**不同类型**（虽然结构类似），赋值会导致编译错误
- **影响范围**: 整个机制怪系统的配置读取链路
- **修复方案**: 将 `BalanceProvider` 中 `_mechanicEnemies` 类型和 `MechanicEnemies` 属性改为 `MechanicEnemiesFileConfig`

### BUG-2: BalanceProvider.GetMechanicDifficultyScaling() 返回不存在的类型

**文件**: `BalanceProvider.cs` 第462行

```csharp
public static MechanicDifficultyScaling GetMechanicDifficultyScaling()
    => MechanicEnemies?.difficulty_scaling;  // difficulty_scaling 类型是 MechanicDifficultyScalingConfig
```

- 返回类型写成了 `MechanicDifficultyScaling`（不存在），实际应为 `MechanicDifficultyScalingConfig`
- **影响**: 编译失败，如果该方法被调用的话

---

## 二、BE-08 机制怪系统 — 验收测试关注点

### 2.1 关键方法清单

| 方法 | 入参 | 出参 | 触发时机 |
|------|------|------|----------|
| `RegisterBossMechanics(enemies, levelId)` | `List<Hero>`, `int` | `void` | `BattleManager.StartBattle()` |
| `OnBattleTick(playerUnits, enemyUnits)` | `List<Hero>`, `List<Hero>` | `void` | `BattleManager.BattleLoop()` 每tick |
| `OverrideEnemyAction(enemy, enemies, allies)` | `Hero`, `List<Hero>`, `List<Hero>` | `bool` | BattleLoop 敌方行动前 |
| `OnBossDamaged(boss, damage, attacker)` | `Hero`, `int`, `Hero` | `void` | `AutoChessAI.NormalAttack()` 伤害后 |
| `OnBossAttacked(boss, damageDealt)` | `Hero`, `int` | `void` | `AutoChessAI.NormalAttack()` 攻击后 |
| `OnBossAttackApplyCurse(boss, target)` | `Hero`, `Hero` | `void` | `FaceEffectExecutor.ProcessOnAttackEffects()` |
| `RemoveCurseFromHero(hero)` | `Hero` | `void` | `FaceEffectExecutor.ApplyCleanseEffect()` |
| `IsImmuneToClass(boss, heroClass)` | `Hero`, `HeroClass` | `bool` | 伤害计算前(需确认是否已集成) |
| `GetMechanicTip(boss)` | `Hero` | `string` | BossMechanicHUD 查询 |

### 2.2 9种机制怪 — 逐一验收关注点

#### (1) ShieldSwap — 护盾互换
- **触发条件**: `mechanicTurnCounter % swap_interval_turns == 0` (默认每3回合)
- **逻辑**: 找最低血量友方 → Boss获得护盾(maxHp × shield_pct_per_phase[phase])
- **边界条件**:
  - ⚠ 只有Boss一个敌方单位时 → `FindLowestHealthAlly` 排除自身后返回null → 提前return，**不触发**（符合预期）
  - ⚠ 没有配置 `shield_pct_per_phase` 或数组长度<3 → 数组越界保护（代码有phase-1 < length检查）
  - ⚠ `mechanicTurnCounter` 在 `ProcessMechanic` 中++，但注册时未重置 → 第一轮counter=1，interval=3 → 第3轮才触发（正确）
- **配置一致性**: JSON中 `shield_pct_per_phase: [0.1, 0.15, 0.2]`，代码用 `state.currentPhase - 1` 索引 → Phase1=0.1, Phase2=0.15, Phase3=0.2 ✓

#### (2) DamageReflect — 伤害反弹
- **触发条件**: `OnBossDamaged` → HandleReflect
- **逻辑**: 按Phase读取reflect_pct → 计算 `max(minDmg, damage × reflectPct)` → attacker.TakeDamage
- **边界条件**:
  - ⚠ 反弹伤害对已死亡的attacker → 代码有 `attacker == null || attacker.IsDead` 保护
  - ⚠ 反弹伤害能否触发二次反弹（A攻击Boss → 反弹伤害给A → A再反弹给Boss）？→ 当前代码中 `TakeDamage` 不触发 `OnBossDamaged`（只有 `AutoChessAI.NormalAttack` 调用），**不会无限循环** ✓
  - ⚠ damage=0 时反射 → `reflectDmg = max(5, 0) = 5` → 仍会造成最低5点反弹
- **配置一致性**: `reflect_pct_per_phase: [0.2, 0.3, 0.5]` ✓

#### (3) HealOnAttack — 攻击回血
- **触发条件**: `OnBossAttacked` → mechanictype == HealOnAttack
- **逻辑**: 按 `heal_pct_per_phase[phase]` 比例回复
- **⚠ 关键BUG**: `OnBossAttacked` 被错误调用在 `AutoChessAI.NormalAttack` 中：
  ```csharp
  MechanicEnemySystem.Instance.OnBossDamaged(target, damage, self);  // target是被攻击的Boss ✓
  MechanicEnemySystem.Instance.OnBossAttacked(self, damage);          // self是攻击者，不是Boss! ✗
  ```
  - `self` 是**玩家英雄**，`OnBossAttacked` 内部用 `boss.GetInstanceID()` 查找状态 → 找不到 → **HealOnAttack永远不触发**
  - **正确调用**: 应该在Boss作为攻击者攻击时调用 `OnBossAttacked(boss, damageDealt)`
- **配置**: JSON中无 HealOnAttack 条目（缺少 `heal_pct_per_phase` 字段），会用fallback 0.2

#### (4) SpawnMinions — 召唤小怪
- **触发条件**: `mechanicTurnCounter % interval == 0` 且 当前小怪数 < max_minions
- **⚠ 关键缺陷**: `CreateMinion()` 返回 null (MVP简化)
  ```csharp
  // MVP: 返回null，实际需要由BattleManager的工厂方法创建
  return null;
  ```
  - **所有召唤逻辑不会生效** → SummonLord 的 SpawnMinions 机制完全不可用
  - 如果后续实现，需测试：
    - 小怪是否正确加入 enemyUnits 列表
    - 小怪是否影响 RemoveDeadUnits 的计数
    - max_minions 计数是否正确（当前数非Boss且活着的敌人）
- **配置**: `spawn_interval_per_phase: [3, 2, 1]` → Phase1每3回合, Phase2每2回合, Phase3每1回合 ✓

#### (5) Berserk — 狂暴
- **触发条件**: `OnBossDamaged` → HandleBerserk
- **逻辑**: 每次受伤时检查 `currentPhase - 1 > berserkStacks` → 叠加攻击力
- **⚠ 数值累积BUG**:
  - `berserkStacks` 由 `expectedStacks = currentPhase - 1` 决定
  - Phase1时 `expectedStacks = 0`，不会触发
  - Phase2时 `expectedStacks = 1`，触发一次 → `BattleAttack += Attack * 0.5`
  - **但是**: 如果Boss从Phase2被打到Phase3，下一次受伤 `expectedStacks = 2`
  - `state.berserkStacks = 2`，攻击力只加一次（phase2时加的），phase3不再额外加
  - ⚠ **更严重**: `atkBonus = RoundToInt(BattleAttack * 0.5)`，此时 BattleAttack 已经被加过一次了 → 第二次加成基数变大
- **配置**: `atk_bonus_per_threshold: 0.5`, `max_stacks: 3` ✓

#### (6) TimeBomb — 定时炸弹
- **触发条件**: `ProcessMechanic` 每回合 → bombTimer-- → 到0爆炸
- **逻辑**: 爆炸时对全体玩家造成 `maxHp × bomb_damage_pct` 伤害
- **边界条件**:
  - ⚠ 炸弹重置逻辑: 爆炸后读取 `bomb_timer_turns_per_phase[phase-1]` 重置 → **炸弹爆炸后下一阶段还会再次爆炸**
  - ⚠ 炸弹伤害不经过防御计算（直接 `hero.TakeDamage(dmg)`），可能导致过高伤害
  - ⚠ 如果玩家全被炸死，战斗是否正确结束 → RemoveDeadUnits + CheckBattleEnd 应该处理 ✓
- **配置**: `bomb_timer_turns_per_phase: [8, 6, 4]`, `bomb_damage_pct: 0.8` ✓

#### (7) StealthAssassin — 隐身刺杀
- **触发条件**: `OverrideEnemyAction` 拦截
- **逻辑**: 每N回合隐身 → 下一回合解除隐身 + 攻击最高攻击单位
- **⚠ 隐身状态不影响被攻击**: `isStealthed` 标记只用于Boss行动决策，**不影响敌人对Boss的攻击**。隐身时Boss仍可被攻击
- **⚠ 时序问题**: 
  - Turn1: `mechanicTurnCounter=1`, interval=3 → `1%3 != 0` → 不隐身
  - 如果 `isStealthed=false` → return false → AI正常行动
  - Turn3: `3%3 == 0` → 隐身，return true → 跳过行动
  - Turn4: `isStealthed=true` → 解除隐身+攻击，return true
  - **问题**: `FindHighestAttackEnemy(enemies)` 参数名是enemies但传的是玩家列表（BattleManager传入 `OverrideEnemyAction(unit, enemyUnits, playerUnits)`，其中 allies 参数是 playerUnits）。看代码: `HandleStealthAction(state, enemies, allies)` → `FindHighestAttackEnemy(enemies)` → **这里enemies实际是enemyUnits（Boss的友军），不是玩家**
  - **⚠ 严重BUG**: 隐身刺客的目标查找 `FindHighestAttackEnemy(enemies)` 中，`enemies` 参数在 BattleManager 中传入的是 `enemyUnits`（Boss自己的队伍），而不是 `playerUnits`（玩家队伍）→ **隐身刺客会攻击自己的队友而非玩家**
  - BattleManager第332行: `OverrideEnemyAction(unit, enemyUnits, playerUnits)` → `OverrideEnemyAction(Hero enemy, List<Hero> enemies, List<Hero> allies)` → enemies=enemyUnits, allies=playerUnits
  - 但 HandleStealthAction 中 `FindHighestAttackEnemy(enemies)` 用的是 enemies(=enemyUnits) → **打自己人**

#### (8) CurseSpread — 诅咒扩散
- **触发条件**: `ProcessMechanic` 每回合 DOT + `OnBossAttackApplyCurse` 攻击附加
- **⚠ 调用链混乱**:
  - `OnBossAttackApplyCurse` 在 `FaceEffectExecutor.ProcessOnAttackEffects` 中被调用
  - 参数 `(attacker, target)` → 内部 `boss = attacker`
  - **但**: ProcessOnAttackEffects 是在 NormalAttack 中由**任何攻击者**触发的
  - 如果玩家攻击Boss → ProcessOnAttackEffects(playerHero, boss, ...) → OnBossAttackApplyCurse(playerHero, boss)
  - 内部检查 `_bossStates.TryGetValue(boss.GetInstanceID())` → playerHero 不是Boss → 找不到 → 正确跳过
  - 如果Boss攻击玩家 → 但 `ProcessOnAttackEffects` 只在 NormalAttack 后调用，Boss通过 OverrideEnemyAction 行动时**不经过 NormalAttack**（除了隐身刺客的直接 TakeDamage）
  - **结论**: CurseSpread 的 `OnBossAttackApplyCurse` 实际上**永远不会被正确触发**，因为Boss通过机制怪系统行动时不走 NormalAttack → ProcessOnAttackEffects 路径
- **诅咒DOT**: `ProcessCurseSpread` 在 OnBattleTick 中触发，对已诅咒单位造成 `maxHp × dmgPct` 伤害 → 这部分可以正常工作（如果有诅咒目标的话）
- **配置**: `curse_damage_pct_per_phase: [0.03, 0.05, 0.08]` ✓
- **注意**: JSON中的 `curse_duration` 和 `curse_spread_on_death` 参数在代码中**完全未使用**

#### (9) ElementalShift — 元素切换
- **触发条件**: `OverrideEnemyAction` → HandleElementalShift (每tick)
- **逻辑**: 按 `_currentTurn % cycle.Length` 切换免疫元素
- **⚠ IsImmuneToClass 未被集成**: 
  - 代码中定义了 `IsImmuneToClass(Hero, HeroClass)` 方法
  - **但在任何伤害计算流程中都没有调用此方法** → 元素免疫机制完全无效
  - 需要在 `AutoChessAI.NormalAttack` 或 `GameBalance.CalculateDamage` 中集成
- **配置**: `immune_cycle: ["warrior", "mage", "assassin"]` → 与 HeroClass 枚举对应 ✓

#### (10) SplitOnDeath — 分裂
- **触发条件**: `HandleBossDeath` → Boss死亡时
- **⚠ 同样受 CreateMinion 返回 null 影响 → 分裂完全不可用**
- **⚠ 死亡时序问题**: 
  - `OnBattleTick` 中先检查 `state.owner.IsDead` → 然后 `HandleBossDeath`
  - 但 `RemoveDeadUnits()` 在 BattleLoop 中于 `OnBattleTick` 之后执行
  - Boss在当前tick被打死后，下一个tick的OnBattleTick才能检测到IsDead → **时序上没问题**（不会遗漏）
- **配置**: `split_count: 2`, `split_stat_pct: 0.5` ✓

### 2.3 通用边界条件

| 测试场景 | 预期结果 | 风险等级 |
|----------|----------|----------|
| 多个Boss同时存在 | 各自独立维护 _bossStates | ⚠ 中 |
| Boss 在 Phase 转换边界（66.01% → 66.00%）| Phase 不应该频繁抖动 | ✅ 低（只升不降）|
| _currentTurn 溢出（int.MaxValue） | 极长时间战斗 | ✅ 低 |
| MechanicEnemySystem 构造时 _config 为空 | RegisterBossMechanics 提前return | ✅ 低 |
| GetMechanicDataForLevel 多条目 min_level 相同 | 选最后遍历到的 | ⚠ 中（不确定行为）|
| levelId = 0（无RoguelikeGameManager）| `CurrentLevel ?? 0` → levelId=0，低于所有min_level → 不注册Boss | ✅ 低 |
| BattleManager.SkipBattle → SimulateBattle | **不触发** MechanicEnemySystem → 跳过战斗时Boss机制无效 | ⚠ 高 |

### 2.4 集成点风险

| 集成点 | 风险描述 |
|--------|----------|
| BattleManager.Awake 初始化 | MechanicEnemySystem 使用 `new` 创建纯C#单例，不是MonoBehaviour → 场景重载不会重新创建（Instance残留） |
| BattleManager.StartBattle 注册 | 先调 RegisterBossMechanics 再调 ActivateBattleStartEffects → 面效果中 Cleanse 不会误清初始状态 |
| BattleManager.BattleLoop 顺序 | OnBattleTick → 玩家行动 → 敌方行动 → 清理死亡 → 检查结束 → 机制怪OnBattleTick中counter++在玩家行动前 |
| AutoChessAI.NormalAttack 回调 | OnBossDamaged(target) 参数正确，但 **OnBossAttacked(self) 参数错误** (BUG-3) |
| FaceEffectExecutor ↔ MechanicEnemySystem | ProcessOnAttackEffects 中调用 OnBossAttackApplyCurse → **调用链不完整** |
| BattleManager.SimulateBattle | 跳过战斗时不触发机制怪系统 → **Boss机制在跳过战斗时无效** |
| ClearBattleState vs EndBattle | EndBattle 中调用 ClearBattleState，但如果 SkipBattle 后走 SimulateBattle → 不调用 ClearBattleState → **状态泄漏** |

---

## 三、BE-09 面效果系统 — 验收测试关注点

### 3.1 关键方法清单

| 方法 | 入参 | 出参 | 触发时机 |
|------|------|------|----------|
| `ProcessRollResults(diceValues, dice)` | `int[]`, `Dice[]` | `void` | 战斗开始 / 骰子投掷后 |
| `ApplyFaceEffect(effectId)` | `string` | `void` | 外部直接调用 |
| `ActivateBattleStartEffects(dices, lastValues, playerHeroes, enemyHeroes)` | `Dice[]`, `int[]`, `List<Hero>`, `List<Hero>` | `void` | BattleManager.StartBattle |
| `ProcessPerTurnEffects(playerHeroes, enemyHeroes)` | `List<Hero>`, `List<Hero>` | `void` | BattleLoop 每tick（当前空实现）|
| `ProcessOnAttackEffects(attacker, target, allies, enemies)` | `Hero`, `Hero`, `List<Hero>`, `List<Hero>` | `void` | AutoChessAI.NormalAttack |
| `ClearBattleEffects()` | 无 | `void` | BattleManager.EndBattle |

### 3.2 11种面效果 — 逐一验收关注点

#### (1) double_attack — 连击 (on_roll_value: 6)
- **效果类型**: Buff → BattleAttack × 1.5
- **⚠ 永久性问题**: `multiply` 操作直接修改 `BattleAttack`，**没有回合/次数限制**
- JSON中有 `duration_type: "next_attack"` 但代码**完全忽略**此字段
- 结果: 投出6后攻击力永久 ×1.5 直到战斗结束
- **验证点**: 多次投出6是否叠加？→ 是的，1.5 × 1.5 × ... → **指数增长**

#### (2) crit_boost — 暴击强化 (on_roll_value: 5)
- **效果类型**: Buff → BattleCritRate + 0.3
- **⚠ 同样忽略 duration_type**: 永久 +30% 暴击率
- **验证点**: 多次投出5 → BattleCritRate 可超过 1.0（但有 `Mathf.Clamp01` 保护）✓

#### (3) shield — 护盾 (on_roll_value: 4)
- **效果类型**: Shield → 全体友方 15% maxHp 护盾
- **⚠ Hero.AddShield 实现为临时生命**: `CurrentHealth = Min(MaxHealth + shield, CurrentHealth + shield)` → 超过 MaxHealth 的部分就是"护盾"
- **验证点**: 护盾不能叠加到无限 → 受 `Min(MaxHealth+shield, ...)` 限制

#### (4) heal — 治疗 (on_roll_value: 3)
- **效果类型**: Heal → 自身 10% maxHp
- **target: "self"** → `_playerHeroes[0]` → 只治疗第一个玩家英雄
- **⚠ 谁是"self"?**: 取 `_playerHeroes[0]` 而非骰子持有者 → 语义不明确

#### (5) coin_bonus — 金币 (on_roll_value: 2)
- **效果类型**: Economy → 金币+2, 利息+5%
- **⚠ 空实现**: 代码中只打Log，**没有实际经济系统调用**
- `var rgm = RoguelikeGameManager.Instance;` → 只检查不为null，不执行任何操作

#### (6) reroll_refund — 重摇返还 (on_roll_value: 1)
- **效果类型**: Economy → 重摇返还+1
- **调用**: `roller.AddFreeRerolls(rerollRefund)` → 依赖 DiceRoller 实现

#### (7) lightning_chain — 闪电链 (on_face_effect)
- **效果类型**: ChainAttack
- **⚠ 双重实现问题**:
  - `ApplyChainAttackEffect` 中直接执行闪电链伤害 + 设置 `attacker.LightningChainBounces = bounces`
  - `ProcessOnAttackEffects` 中又有一套独立的闪电链逻辑（消耗 LightningChainBounces）
  - **执行流程**: ChainAttack 效果触发 → 立即造成弹射伤害 → 设置 LightningChainBounces=2 → 后续每次 NormalAttack → ProcessOnAttackEffects → 再弹射一次 → LightningChainBounces=0
  - **结果**: 闪电链在触发时弹射一轮，之后每次攻击再弹射 → **伤害超出预期**
- **⚠ ApplyChainAttackEffect 第368行**: `_playerHeroes == null || _playerHeroes == null` → 重复判断（应为 `_playerHeroes == null || _enemyHeroes == null`）

#### (8) armor_break — 破甲 (on_face_effect)
- **效果类型**: Debuff → 目标防御 × 0.5
- **⚠ Hero.HasArmorBreak 也有独立逻辑**: ProcessOnAttackEffects 中再次检查 `attacker.HasArmorBreak` → 防御再次减半
- **双重减防风险**: 如果面效果Debuff + HasArmorBreak同时生效 → 防御被减半两次 = 原防御 × 0.25

#### (9) stun — 眩晕 (on_face_effect)
- **效果类型**: CC → 目标眩晕1回合
- **⚠ duration_rounds 被忽略**: 代码中读取了 duration 但 `SetStunned(true)` 没有传入持续时间
- BattleManager.BattleLoop 中眩晕解除是 `if (unit.IsStunned) { unit.SetStunned(false); continue; }` → **只眩晕1个tick**
- 如果 duration > 1，代码不支持多回合眩晕

#### (10) cleanse — 净化 (on_face_effect)
- **效果类型**: Cleanse → 移除眩晕 + 移除诅咒
- **集成**: 正确调用 `MechanicEnemySystem.Instance?.RemoveCurseFromHero(hero)` ✓
- **验证点**: 净化后诅咒列表是否正确清理

#### (11) aoe_damage — 范围伤害 (on_face_effect)
- **效果类型**: AOE → 全体敌人 20% 攻击力伤害
- **⚠ TakeDamage 不传 attacker**: `enemy.TakeDamage(Mathf.Max(1, dmg))` → 无攻击者 → 不会触发荆棘/反弹等
- **验证点**: 确认是否为预期行为

### 3.3 面效果触发模式对比

| 触发方式 | 触发时机 | 支持的效果类型 |
|----------|----------|---------------|
| `on_roll_value` | ProcessRollResults → ApplyValueTriggeredEffect | Buff, Shield, Heal, Economy（4种）|
| `on_face_effect` | ProcessRollResults → ApplyFaceEffect | Buff, Debuff, Shield, Heal, CC, ChainAttack, AOE, Cleanse, Economy（9种）|

**⚠ 不一致**: `ApplyValueTriggeredEffect` 的 switch 不处理 Debuff/CC/ChainAttack/AOE/Cleanse → 如果JSON中配置了 `trigger: "on_roll_value"` + `effect_type: "CC"` → 走 default 分支 → 只打Log，**效果不生效**

### 3.4 边界条件

| 测试场景 | 预期结果 | 风险 |
|----------|----------|------|
| diceValues=null | ProcessRollResults 提前return | ✅ 低 |
| dice=null | 跳过面效果触发检查，只走值触发 | ✅ 低 |
| diceValues[i] 越界 (值>6或<1) | FindEffectByValue 找不到 → 不触发 | ✅ 低 |
| _playerHeroes 为空 | 大部分效果有null检查 | ✅ 低 |
| _enemyHeroes 为空 | Debuff/CC/AOE 不生效 | ✅ 低 |
| effect_params=null | 各方法开头有null检查 → return | ✅ 低 |
| 重复触发同一面效果 | Buff叠乘/叠加（无防重） | ⚠ 中 |
| 3个骰子都投6 | double_attack 触发3次 → BattleAttack ×1.5³ = 3.375倍 | ⚠ 高 |

### 3.5 集成点风险

| 集成点 | 风险描述 |
|--------|----------|
| BattleManager.StartBattle 调用时序 | 先 RegisterBossMechanics → 再 ActivateBattleStartEffects → 最后 ApplyDiceComboEffects → 面效果Buff可能与骰子组合Buff叠加（均为multiply）|
| ProcessRollResults 的 Dice 索引 | `dice[i].FaceEffects[diceValues[i] - 1]` → 如果 diceValues[i]=0 → 索引-1 → **ArrayIndexOutOfBoundsException** |
| ProcessOnAttackEffects 中的破甲 | 修改 `target.BattleDefense` 为永久值（本战斗内），不恢复 |
| FaceEffectExecutor 单例生命周期 | 在 BattleManager.Awake 中创建，场景切换后可能残留旧 Instance |
| Economy 效果空实现 | coin_bonus 的金币和利息加成没有实际效果 → **玩家看不到收益** |

---

## 四、配置数据与代码逻辑一致性检查

### 4.1 MechanicType 枚举 vs JSON mechanic_type 字段

| 枚举值 | JSON中是否存在 | 匹配 |
|--------|---------------|------|
| ShieldSwap (1) | ✅ shield_guard | ✓ |
| DamageReflect (2) | ✅ reflect_puppet | ✓ |
| HealOnAttack (3) | ❌ **JSON中无此类型** | ⚠ |
| SpawnMinions (4) | ✅ summon_lord | ✓ |
| Berserk (5) | ✅ berserk_behemoth | ✓ |
| TimeBomb (6) | ✅ bomb_timer | ✓ |
| StealthAssassin (7) | ✅ stealth_assassin | ✓ |
| CurseSpread (8) | ✅ curse_spreader | ✓ |
| SplitOnDeath (9) | ✅ split_mother | ✓ |
| ElementalShift (10) | ✅ elemental_shifter | ✓ |

**注意**: HealOnAttack (3) 在枚举中定义但JSON中无对应配置条目

### 4.2 FaceEffect effect_type vs JSON

| 代码处理的类型 | JSON中是否存在 |
|---------------|---------------|
| Buff | ✅ (double_attack, crit_boost) |
| Debuff | ✅ (armor_break) |
| Shield | ✅ (shield) |
| Heal | ✅ (heal) |
| CC | ✅ (stun) |
| ChainAttack | ✅ (lightning_chain) |
| AOE | ✅ (aoe_damage) |
| Cleanse | ✅ (cleanse) |
| Economy | ✅ (coin_bonus, reroll_refund) |

**共11种面效果配置，代码处理9种effect_type** ✓

### 4.3 未使用的配置字段

| JSON字段 | 所在配置 | 代码中是否使用 |
|----------|----------|---------------|
| `hp_thresholds: [0.75, 0.5, 0.25]` | berserk_behemoth | ❌ 代码用固定 0.33/0.66 阈值 |
| `curse_duration: 3` | curse_spreader | ❌ 诅咒无持续时间 |
| `curse_spread_on_death: true` | curse_spreader | ❌ 死亡扩散未实现 |
| `shift_warning_turns_before: 1` | elemental_shifter | ❌ 预警未实现 |
| `target_preference: "highest_attack"` | stealth_assassin | ❌ 硬编码在代码中 |
| `duration_type: "next_attack"` | double_attack | ❌ 永久生效 |
| `face_upgrade_costs` | face_effects.json | ❌ 未在代码中引用 |

### 4.4 MechanicEnemiesConfig vs MechanicEnemiesFileConfig

两个类型结构几乎相同但名称不同:
- `MechanicEnemiesConfig` → 嵌套在 enemies.json 中的字段类型
- `MechanicEnemiesFileConfig` → 独立 mechanic_enemies.json 的顶层类型

**BalanceProvider 使用 `MechanicEnemiesConfig` 但 `ConfigLoader.LoadMechanicEnemies()` 返回 `MechanicEnemiesFileConfig`** → **编译错误**

---

## 五、验收测试用例建议 (按优先级排序)

### P0 — 阻塞级 (必须修复后才能验收)

| ID | 用例 | 预期 | 关联BUG |
|----|------|------|---------|
| P0-1 | 编译项目 | 无编译错误 | BUG-1, BUG-2 |
| P0-2 | 修复后启动战斗含Boss | MechanicEnemySystem 正确加载配置 | BUG-1 |
| P0-3 | 创建小怪 (SpawnMinions/SplitOnDeath) | 小怪正确出现在战场 | CreateMinion返回null |
| P0-4 | AutoChessAI.OnBossAttacked 参数 | Boss攻击后正确回血 | BUG-3 参数错误 |
| P0-5 | SkipBattle 时机制怪状态清理 | 无状态泄漏 | ClearBattleState未调用 |

### P1 — 机制怪功能验证

| ID | 用例 | 预期 |
|----|------|------|
| P1-1 | ShieldSwap Boss + 1个小怪 → 3回合后触发 | Boss获得护盾 |
| P1-2 | DamageReflect Boss → 受伤后反弹 | 攻击者受到反弹伤害 |
| P1-3 | Berserk Boss → 66%HP阶段转换 | 攻击力提升 |
| P1-4 | TimeBomb Boss → 倒计时结束 | 全体玩家受80%HP伤害 |
| P1-5 | StealthAssassin Boss → 隐身后攻击 | **攻击玩家而非队友** (需修复目标参数) |
| P1-6 | CurseSpread Boss → 攻击附带诅咒 | 被攻击者受到每回合DOT |
| P1-7 | ElementalShift Boss → 切换免疫 | 对应职业攻击无效 |
| P1-8 | SplitOnDeath Boss → 死亡后分裂 | 生成2个50%属性副本 |
| P1-9 | 各机制怪 Phase 1→2→3 转换 | 机制强度随阶段提升 |
| P1-10 | Boss击杀后战斗正常结束 | 不卡死/不崩溃 |

### P2 — 面效果功能验证

| ID | 用例 | 预期 |
|----|------|------|
| P2-1 | 投出6 → double_attack | 全体攻击力 ×1.5 |
| P2-2 | 连续投出6三次 | 攻击力 ×1.5³ (需确认是否为预期) |
| P2-3 | 投出4 → shield | 全体获得15%HP护盾 |
| P2-4 | 闪电链面效果触发 | 弹射2个目标，每次衰减30% |
| P2-5 | 破甲面效果 + HasArmorBreak | 防御不重复减半 (或确认预期) |
| P2-6 | 眩晕面效果 → 下回合跳过行动 | 敌方被眩晕1tick |
| P2-7 | 净化面效果 → 移除诅咒 | 诅咒DOT停止 |
| P2-8 | AOE面效果 → 全体敌人受伤 | 伤害 = 攻击力 × 20% |
| P2-9 | 金币面效果 (投出2) | **实际获得金币** (当前空实现) |
| P2-10 | 重摇返还面效果 (投出1) | 获得1次免费重摇 |

### P3 — 集成/边界/异常

| ID | 用例 | 预期 |
|----|------|------|
| P3-1 | 无Boss关卡 → 机制怪系统 | 不注册、不报错 |
| P3-2 | 空配置文件 → 启动战斗 | 使用fallback值 |
| P3-3 | 配置文件缺失 → 启动战斗 | 不崩溃 |
| P3-4 | 面效果 + 骰子组合Buff叠加 | 叠加而非覆盖 |
| P3-5 | 战斗速度2x/4x → 机制怪触发频率 | 不变（基于tick而非时间）|
| P3-6 | 多场连续战斗 → 状态隔离 | 无状态泄漏 |
| P3-7 | 所有9种机制怪 × 3阶段 → 全覆盖 | 无遗漏 |
| P3-8 | diceValues 包含0 → 面效果 | 不越界崩溃 |

---

## 六、总结 — 关键发现

### 必须修复的BUG (4个编译/逻辑级)
1. **BUG-1**: `BalanceProvider._mechanicEnemies` 类型与 `ConfigLoader.LoadMechanicEnemies()` 返回类型不匹配 → **编译失败**
2. **BUG-2**: `GetMechanicDifficultyScaling()` 返回不存在的类型 → **编译失败**
3. **BUG-3**: `AutoChessAI.NormalAttack` 中 `OnBossAttacked(self, damage)` 的 `self` 是攻击者而非Boss → **HealOnAttack机制永远不触发**
4. **BUG-4**: `HandleStealthAction` 中 `FindHighestAttackEnemy(enemies)` 的 `enemies` 是友军 → **隐身刺客攻击自己队友**

### 功能缺失 (3个)
1. `CreateMinion()` 返回 null → SpawnMinions 和 SplitOnDeath **完全不可用**
2. `IsImmuneToClass()` 未在任何伤害流程中调用 → ElementalShift **免疫机制不生效**
3. `ApplyEconomyEffect` 中金币/利息无实际调用 → coin_bonus **无实际效果**

### 设计隐患 (3个)
1. 面效果 Buff 无回合/次数限制 → `duration_type` 字段被忽略 → 可能导致数值膨胀
2. `ProcessOnAttackEffects` 中的破甲与 Debuff 面效果可能**双重减防**
3. `BattleManager.SimulateBattle`（跳过战斗）不触发机制怪 → **跳过战斗结果可能不准确**

### 配置数据问题 (2个)
1. JSON中多个配置字段在代码中未使用（`hp_thresholds`, `curse_duration`, `curse_spread_on_death` 等）
2. HealOnAttack 枚举值存在但JSON中无对应配置条目
