# BE-08 机制怪系统 & BE-09 骰子面效果执行器 — 详细技术方案

> **版本**: v1.0  
> **日期**: 2026-05-09  
> **状态**: 待评审  
> **依赖**: BalanceProvider ✅ | BattleManager ✅ | AutoChessAI ✅ | DiceRoller ✅ | BossMechanicHUD ✅  

---

## 目录

1. [代码结构分析摘要](#1-代码结构分析摘要)
2. [BE-08 机制怪系统（MechanicEnemySystem）](#2-be-08-机制怪系统)
3. [BE-09 骰子面效果执行器（FaceEffectExecutor）](#3-be-09-骰子面效果执行器)
4. [集成点与改造清单](#4-集成点与改造清单)
5. [JSON配置Schema](#5-json配置schema)
6. [测试用例](#6-测试用例)
7. [工时估算](#7-工时估算)

---

## 1. 代码结构分析摘要

### 1.1 BattleManager 战斗Tick流程

```
StartBattle(players, enemies, diceCombo)
  → LoadBattleConfig()                    // 从BalanceProvider读取tick/maxTime/speed
  → 设置 playerUnits / enemyUnits
  → ApplyDiceComboEffects(diceCombo)      // 骰子组合开场效果
  → StartCoroutine(BattleLoop())

BattleLoop() 协程每 tick:
  → WaitForSeconds(tickInterval / battleSpeed)
  → 遍历 playerUnits → AutoChessAI.TakeAction(unit, enemies, allies)
  → 遍历 enemyUnits → AutoChessAI.TakeAction(unit, enemies, allies)
  → RemoveDeadUnits()
  → CheckBattleEnd()                      // 全灭 或 超时
  → BattleTimer += tickInterval
  → EndBattle() → OnBattleEnded → GameStateMachine.NextState()
```

**关键集成点**:
- 敌方行动在 BattleLoop 中调用 `AutoChessAI.TakeAction(unit, playerUnits, enemyUnits)`（L292-296）
- **机制怪需要在敌方行动前后插入机制处理逻辑**
- `CheckBattleEnd()` 只检查全灭和超时，无阶段逻辑

### 1.2 BossMechanicHUD 已有结构

```csharp
// 已有字段：
private Hero currentBoss;
private int currentPhase = 0;
private int totalPhases = 3;
private static readonly float[] PHASE_THRESHOLDS = { 0.66f, 0.33f };

// 已有方法：
void DetectBoss()           // 遍历 enemyUnits 找 Hero.IsBoss == true
void CheckPhaseTransition() // 按血量阈值自动切换阶段
void OnPhaseChanged()       // 显示阶段横幅动画
void ShowMechanicTip(tip)   // 显示机制提示条
void ShowSkillWarning(msg)  // 全屏技能预警

// 已有硬编码的机制提示：
// Phase 1: "🎯 Boss处于第一阶段，攻击力较低"
// Phase 2: "⚠ Boss进入狂暴状态！攻击力+50%"
// Phase 3: "🔥 Boss释放终极技能！全屏AOE"
```

**结论**: BossMechanicHUD 已有完整的阶段UI和动画框架，但机制提示是硬编码的。BE-08需要让 MechanicEnemySystem 驱动 HUD 的提示内容。

### 1.3 Dice.FaceEffects 数据结构

```csharp
// Dice.cs
public int[] Faces { get; private set; }         // 默认 [1,2,3,4,5,6]
public string[] FaceEffects { get; private set; } // 索引对应Faces下标
public int CurrentValue { get; private set; }     // 投掷结果 (1-6)

// 已有方法：
void UpgradeFace(int faceIndex, string effect)   // 替换某面的效果字符串
```

**关键发现**: `FaceEffects` 是 `string[]`，目前只是标记字符串，没有解析和执行逻辑。**BE-09 需要建立 string → 执行器的映射机制**。

### 1.4 AutoChessAI 决策架构

```csharp
public static void TakeAction(Hero self, List<Hero> enemies, List<Hero> allies)
{
    // 1. 治疗者AI（heroName == "治疗者"）
    // 2. FindTarget(self, enemies):
    //    - 刺客 → 血量最低
    //    - Boss(heroName == "Boss") → 攻击最高
    //    - 默认 → 距离最近
    // 3. 30%概率 UseSkill，否则 NormalAttack
}
```

**关键问题**: 当前Boss AI 仅通过 `heroName == "Boss"` 做简单的"攻击最高"寻敌，**没有机制行为**。BE-08需要：
- 将 `heroName` 判断改为 `IsBoss` 属性判断
- 新增 `MechanicType` 驱动的专属AI逻辑

### 1.5 LevelManager Boss关识别

```csharp
// LevelManager.CreateDefaultLevel() 硬编码了1-15关+default
// Boss关: 3, 5, 9, 10, 14, 15, default
// 机制怪关 (10+): 11=CurseMage, 12=HeavyKnight, 13=VenomSpider, 14=Boss+CurseMage+HeavyKnight, 15=Boss+Boss+VenomSpider+Healer

// 关卡是否是Boss关：enemyWaves中存在IsBoss==true的单位
// 当前没有显式的 isBossLevel 标记
```

### 1.6 BalanceProvider 配置读取模式

```csharp
// 统一入口：静态类，懒加载 JSON → fallback 到 GameBalance 硬编码
// 使用方式：
float tickInterval = BalanceProvider.GetBattleTickInterval(); // JSON优先，fallback 0.3f
var skillData = BalanceProvider.GetDiceComboSkill("three_of_a_kind"); // 返回 DiceComboSkillEntry
int maxRounds = BalanceProvider.GetSimulateBattleMaxRounds(); // JSON优先，fallback 100

// 新增配置的固定模式：
// 1. JSON文件定义新config节点
// 2. ConfigLoader.cs 新增 LoadXxx() 方法
// 3. BalanceProvider 新增缓存字段 + 懒加载属性 + Get方法
// 4. 提供合理的 fallback 值
```

---

## 2. BE-08 机制怪系统

### 2.1 系统概述

机制怪系统为10关后出现的Boss增加特殊战斗机制，使战斗不再是纯数值对拼，而是需要玩家利用骰子组合和遗物来应对的策略性挑战。

### 2.2 枚举定义

```csharp
/// <summary>
/// 机制类型枚举 — 定义机制怪的特殊能力
/// 配置于 mechanic_enemies.json
/// </summary>
public enum MechanicType
{
    None = 0,
    
    /// <summary>护盾互换 — 每3回合与最低血量友方互换护盾</summary>
    ShieldSwap = 1,
    
    /// <summary>伤害反弹 — 反弹X%受到的伤害</summary>
    DamageReflect = 2,
    
    /// <summary>攻击回血 — 每次攻击回复造成伤害的X%</summary>
    HealOnAttack = 3,
    
    /// <summary>召唤小怪 — 每2回合召唤1个小怪</summary>
    SpawnMinions = 4,
    
    /// <summary>狂暴 — 血量每降低25%，攻击+50%</summary>
    Berserk = 5,
    
    /// <summary>定时炸弹 — N回合后全体高额伤害</summary>
    TimeBomb = 6,
    
    /// <summary>隐身刺杀 — 每2回合隐身+攻击最高攻击单位</summary>
    StealthAssassin = 7,
    
    /// <summary>诅咒扩散 — 攻击附带诅咒，被诅咒单位每回合掉血</summary>
    CurseSpread = 8,
    
    /// <summary>分裂 — 死亡时分裂为2个50%属性的副本</summary>
    SplitOnDeath = 9,
    
    /// <summary>元素切换 — 每回合切换免疫的元素/职业类型</summary>
    ElementalShift = 10
}
```

### 2.3 数据结构

```csharp
/// <summary>
/// 机制怪运行时状态 — 挂载到Boss Hero上
/// </summary>
public class MechanicEnemyState
{
    public MechanicType mechanicType;
    public float mechanicStrength;     // 机制强度（随关卡递增）
    public int mechanicTurnCounter;    // 机制触发回合计数
    public int currentPhase;           // 当前阶段 (1-3)
    public bool isActive;              // 机制是否激活
    
    // 机制专属字段
    public int reflectPercent;         // DamageReflect: 反弹百分比
    public int spawnInterval;          // SpawnMinions: 召唤间隔(回合)
    public int bombTimer;              // TimeBomb: 炸弹倒计时
    public int stealthCooldown;        // StealthAssassin: 隐身CD
    public bool isStealthed;           // StealthAssassin: 当前是否隐身
    public int curseDamagePerTurn;     // CurseSpread: 诅咒每回合伤害
    public string immuneElement;       // ElementalShift: 当前免疫元素
    public bool hasSplit;              // SplitOnDeath: 是否已分裂过
    
    // 运行时引用（不序列化）
    [System.NonSerialized] public Hero owner;
    [System.NonSerialized] public List<int> cursedTargetInstanceIds = new List<int>();
}
```

### 2.4 JSON配置Schema

**文件路径**: `Assets/Resources/Data/mechanic_enemies.json`

```json
{
    "mechanic_enemies": [
        {
            "id": "shield_guard",
            "name_cn": "护盾守卫",
            "mechanic_type": "ShieldSwap",
            "description": "每3回合与最低血量友方互换护盾",
            "phase_tips": [
                "🎯 护盾守卫正在积蓄护盾",
                "⚠ 护盾守卫开始互换护盾！",
                "🔥 护盾守卫进入极限防御！"
            ],
            "base_stats": {
                "health_multiplier": 1.5,
                "attack_multiplier": 0.8,
                "defense_multiplier": 2.0,
                "speed_multiplier": 0.6
            },
            "mechanic_params": {
                "swap_interval_turns": 3,
                "shield_pct_per_phase": [0.1, 0.15, 0.2]
            },
            "reward_bonus": {
                "relic_drop_chance_bonus": 0.1
            },
            "min_level": 11,
            "max_level": -1
        },
        {
            "id": "reflect_puppet",
            "name_cn": "反伤傀儡",
            "mechanic_type": "DamageReflect",
            "description": "反弹受到伤害的30%",
            "phase_tips": [
                "🎯 反伤傀儡的荆棘护甲较弱",
                "⚠ 反伤傀儡反伤率提升！吸血/低伤应对",
                "🔥 反傀儡伤害反弹50%！"
            ],
            "base_stats": {
                "health_multiplier": 1.2,
                "attack_multiplier": 1.0,
                "defense_multiplier": 1.5,
                "speed_multiplier": 0.8
            },
            "mechanic_params": {
                "reflect_pct_per_phase": [0.2, 0.3, 0.5],
                "min_damage_to_reflect": 5
            },
            "min_level": 12
        },
        {
            "id": "split_mother",
            "name_cn": "分裂母体",
            "mechanic_type": "SplitOnDeath",
            "description": "死亡时分裂为2个50%属性的副本",
            "phase_tips": [
                "🎯 分裂母体血量较高",
                "⚠ 分裂母体即将分裂！AOE清场准备",
                "🔥 分裂后的副本仍有战斗力！"
            ],
            "base_stats": {
                "health_multiplier": 2.0,
                "attack_multiplier": 1.0,
                "defense_multiplier": 0.8,
                "speed_multiplier": 0.5
            },
            "mechanic_params": {
                "split_count": 2,
                "split_stat_pct": 0.5,
                "split_can_split_again": false
            },
            "min_level": 13
        },
        {
            "id": "berserk_behemoth",
            "name_cn": "狂暴巨兽",
            "mechanic_type": "Berserk",
            "description": "血量每降低25%，攻击+50%",
            "phase_tips": [
                "🎯 狂暴巨兽攻击力较低",
                "⚠ 狂暴巨兽攻击力提升！",
                "🔥 狂暴巨兽进入狂暴！攻击力极高！"
            ],
            "base_stats": {
                "health_multiplier": 2.5,
                "attack_multiplier": 0.6,
                "defense_multiplier": 1.2,
                "speed_multiplier": 0.7
            },
            "mechanic_params": {
                "atk_bonus_per_threshold": 0.5,
                "hp_thresholds": [0.75, 0.5, 0.25],
                "max_stacks": 3
            },
            "min_level": 14
        },
        {
            "id": "summon_lord",
            "name_cn": "召唤领主",
            "mechanic_type": "SpawnMinions",
            "description": "每2回合召唤1个小怪",
            "phase_tips": [
                "🎯 召唤领主正在积蓄力量",
                "⚠ 召唤速度加快！优先击杀Boss",
                "🔥 召唤领主每回合召唤！"
            ],
            "base_stats": {
                "health_multiplier": 1.8,
                "attack_multiplier": 0.7,
                "defense_multiplier": 1.0,
                "speed_multiplier": 0.8
            },
            "mechanic_params": {
                "spawn_interval_per_phase": [3, 2, 1],
                "minion_stat_pct": 0.4,
                "max_minions": 3,
                "minion_template": "小怪"
            },
            "min_level": 15
        },
        {
            "id": "bomb_timer",
            "name_cn": "定时炸弹",
            "mechanic_type": "TimeBomb",
            "description": "5回合后全体高额伤害",
            "phase_tips": [
                "🎯 定时炸弹开始倒计时！5回合内击杀",
                "⚠ 倒计时加速！剩余3回合！",
                "🔥 最后警告！立即击杀否则团灭！"
            ],
            "base_stats": {
                "health_multiplier": 1.0,
                "attack_multiplier": 1.2,
                "defense_multiplier": 0.6,
                "speed_multiplier": 1.5
            },
            "mechanic_params": {
                "bomb_timer_turns_per_phase": [8, 6, 4],
                "bomb_damage_pct": 0.8,
                "show_warning_at_turns_remaining": 3
            },
            "min_level": 16
        },
        {
            "id": "stealth_assassin",
            "name_cn": "隐身刺客",
            "mechanic_type": "StealthAssassin",
            "description": "每2回合隐身+攻击最高攻击单位",
            "phase_tips": [
                "🎯 隐身刺客会定期消失",
                "⚠ 隐身刺客伤害提高！",
                "🔥 隐身刺客每次隐身后伤害翻倍！"
            ],
            "base_stats": {
                "health_multiplier": 0.8,
                "attack_multiplier": 2.0,
                "defense_multiplier": 0.4,
                "speed_multiplier": 2.0
            },
            "mechanic_params": {
                "stealth_interval_per_phase": [3, 2, 2],
                "stealth_damage_multiplier_per_phase": [1.0, 1.5, 2.0],
                "target_preference": "highest_attack"
            },
            "min_level": 17
        },
        {
            "id": "curse_spreader",
            "name_cn": "诅咒法师",
            "mechanic_type": "CurseSpread",
            "description": "攻击附带诅咒，被诅咒单位每回合掉血",
            "phase_tips": [
                "🎯 诅咒法师的诅咒可被净化",
                "⚠ 诅咒伤害翻倍！",
                "🔥 诅咒法师施加不可净化的诅咒！"
            ],
            "base_stats": {
                "health_multiplier": 1.0,
                "attack_multiplier": 1.5,
                "defense_multiplier": 0.8,
                "speed_multiplier": 1.2
            },
            "mechanic_params": {
                "curse_damage_pct_per_phase": [0.03, 0.05, 0.08],
                "curse_duration": 3,
                "curse_spread_on_death": true
            },
            "min_level": 18
        },
        {
            "id": "elemental_shifter",
            "name_cn": "元素使者",
            "mechanic_type": "ElementalShift",
            "description": "每回合切换免疫的职业类型",
            "phase_tips": [
                "🎯 元素使者每回合切换免疫",
                "⚠ 免疫范围扩大！",
                "🔥 元素使者免疫所有伤害1回合！"
            ],
            "base_stats": {
                "health_multiplier": 1.3,
                "attack_multiplier": 1.3,
                "defense_multiplier": 1.0,
                "speed_multiplier": 1.0
            },
            "mechanic_params": {
                "immune_cycle": ["warrior", "mage", "assassin"],
                "phase_3_immune_all_duration": 1,
                "shift_warning_turns_before": 1
            },
            "min_level": 19
        }
    ],
    
    "combined_mechanics": {
        "description": "16+关随机组合机制",
        "min_level_for_combined": 16,
        "max_mechanics_per_boss": 2,
        "combination_weights": {
            "high_damage": ["Berserk", "StealthAssassin"],
            "defensive": ["ShieldSwap", "DamageReflect"],
            "swarm": ["SpawnMinions", "CurseSpread"]
        }
    },
    
    "difficulty_scaling": {
        "stat_multiplier_per_level_above_10": 0.1,
        "mechanic_strength_per_level_above_10": 0.05
    }
}
```

### 2.5 MechanicEnemySystem 核心接口

```csharp
/// <summary>
/// 机制怪系统 — 管理机制怪的生成、机制触发、阶段控制
/// 文件位置: Assets/Scripts/Battle/MechanicEnemySystem.cs
/// 
/// 设计原则:
/// 1. 不修改 BattleManager 核心循环，通过事件/委托注入
/// 2. 机制逻辑与数值分离，从 mechanic_enemies.json 读取配置
/// 3. 每个机制是独立的处理器，可自由组合
/// 4. 与 BossMechanicHUD 通过事件通信
/// </summary>
public class MechanicEnemySystem
{
    // ========== 单例 ==========
    public static MechanicEnemySystem Instance { get; private set; }
    
    // ========== 事件（驱动UI） ==========
    /// <summary>机制触发时（用于HUD显示提示）</summary>
    public event System.Action<Hero, MechanicType, string> OnMechanicTriggered;
    
    /// <summary>Boss阶段切换时（用于HUD阶段横幅）</summary>
    public event System.Action<Hero, int, string> OnBossPhaseChanged;
    
    /// <summary>技能预警（用于HUD全屏预警）</summary>
    public event System.Action<Hero, string> OnMechanicWarning;
    
    /// <summary>新敌人被召唤（用于BattleManager注册新单位）</summary>
    public event System.Action<List<Hero>> OnMinionsSpawned;
    
    /// <summary>定时炸弹爆炸（全屏伤害触发）</summary>
    public event System.Action<Hero, int> OnBombExploded;
    
    // ========== 状态 ==========
    private Dictionary<int, MechanicEnemyState> _bossStates = new();
    // key = Hero.GetInstanceID()
    
    private int _currentTurn = 0;
    private MechanicEnemiesConfig _config;
    
    // ========== 初始化 ==========
    
    public MechanicEnemySystem()
    {
        Instance = this;
        _config = BalanceProvider.GetMechanicEnemies();
    }
    
    /// <summary>
    /// 战斗开始时注册所有Boss机制
    /// 由 BattleManager.StartBattle() 在 ApplyDiceComboEffects 之后调用
    /// </summary>
    /// <param name="enemies">敌方单位列表</param>
    /// <param name="levelId">当前关卡ID</param>
    public void RegisterBossMechanics(List<Hero> enemies, int levelId)
    {
        _bossStates.Clear();
        _currentTurn = 0;
        
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsBoss) continue;
            
            // 从配置查找机制类型
            var mechanicData = GetMechanicDataForLevel(levelId);
            if (mechanicData == null) continue;
            
            var state = CreateState(enemy, mechanicData, levelId);
            _bossStates[enemy.GetInstanceID()] = state;
            
            // 标记Boss
            enemy.IsBoss = true;
            
            // 应用机制怪的属性倍率
            ApplyStatMultipliers(enemy, mechanicData);
            
            Debug.Log($"[MechanicEnemy] 注册Boss机制: {enemy.Data.heroName} " +
                     $"→ {mechanicData.mechanic_type} 阶段1/{state.currentPhase}");
        }
    }
    
    /// <summary>
    /// 每个战斗Tick调用 — 在敌方行动之前
    /// 由 BattleManager.BattleLoop() 在遍历enemyUnits前调用
    /// </summary>
    public void OnBattleTick(List<Hero> playerUnits, List<Hero> enemyUnits)
    {
        _currentTurn++;
        
        foreach (var kvp in _bossStates.ToList())
        {
            var state = kvp.Value;
            if (state == null || state.owner == null || state.owner.IsDead)
            {
                // Boss死亡时处理（如分裂）
                if (state?.owner != null && state.owner.IsDead)
                    HandleBossDeath(state, enemyUnits);
                
                _bossStates.Remove(kvp.Key);
                continue;
            }
            
            // 检查阶段转换
            CheckPhaseTransition(state);
            
            // 执行回合制机制
            ProcessMechanic(state, playerUnits, enemyUnits);
        }
    }
    
    /// <summary>
    /// 敌方单位行动前的拦截 — 替代AutoChessAI的默认Boss逻辑
    /// 返回 true 表示已处理该单位的行动，跳过默认AI
    /// </summary>
    public bool OverrideEnemyAction(Hero enemy, List<Hero> enemies, List<Hero> allies)
    {
        if (!_bossStates.TryGetValue(enemy.GetInstanceID(), out var state))
            return false;
        
        // 机制专属行动逻辑
        switch (state.mechanicType)
        {
            case MechanicType.StealthAssassin:
                return HandleStealthAction(state, enemies, allies);
            
            case MechanicType.HealOnAttack:
                // 普通攻击但带吸血，不拦截
                return false;
            
            case MechanicType.ElementalShift:
                HandleElementalShift(state);
                return false; // 行动仍然由AI执行
            
            default:
                return false; // 大部分机制不拦截行动
        }
    }
    
    /// <summary>
    /// 伤害后处理 — 用于DamageReflect、Berserk等
    /// 由 Hero.TakeDamage() 改造后调用
    /// </summary>
    public void OnBossDamaged(Hero boss, int damage, Hero attacker)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state))
            return;
        
        switch (state.mechanicType)
        {
            case MechanicType.DamageReflect:
                HandleReflect(state, damage, attacker);
                break;
            case MechanicType.Berserk:
                HandleBerserk(state);
                break;
        }
    }
    
    /// <summary>
    /// 获取指定Boss的当前机制提示文本（供BossMechanicHUD使用）
    /// </summary>
    public string GetMechanicTip(Hero boss)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state))
            return null;
        
        var data = GetMechanicData(state.mechanicType);
        if (data?.phase_tips == null) return null;
        
        int tipIndex = Mathf.Min(state.currentPhase - 1, data.phase_tips.Count - 1);
        return data.phase_tips[tipIndex];
    }
    
    /// <summary>
    /// 清理战斗状态
    /// </summary>
    public void ClearBattleState()
    {
        _bossStates.Clear();
        _currentTurn = 0;
    }
    
    // ========== 私有方法 — 机制处理器 ==========
    
    private void ProcessMechanic(MechanicEnemyState state, 
        List<Hero> playerUnits, List<Hero> enemyUnits)
    {
        state.mechanicTurnCounter++;
        
        switch (state.mechanicType)
        {
            case MechanicType.ShieldSwap:
                ProcessShieldSwap(state, enemyUnits);
                break;
            case MechanicType.SpawnMinions:
                ProcessSpawnMinions(state, enemyUnits);
                break;
            case MechanicType.TimeBomb:
                ProcessTimeBomb(state, playerUnits);
                break;
            case MechanicType.CurseSpread:
                ProcessCurseSpread(state, playerUnits);
                break;
            case MechanicType.ElementalShift:
                ProcessElementalShift(state);
                break;
        }
    }
    
    // --- ShieldSwap ---
    private void ProcessShieldSwap(MechanicEnemyState state, List<Hero> allies)
    {
        int interval = GetMechanicParamInt(state, "swap_interval_turns", 3);
        if (state.mechanicTurnCounter % interval != 0) return;
        
        Hero lowestAlly = FindLowestHealthAlly(state.owner, allies);
        if (lowestAlly == null) return;
        
        // 互换护盾（MVP: 互换CurrentHealth的超出MaxHealth部分）
        // 详见实现指引 2.7
        Debug.Log($"[Mechanic] 护盾互换: {state.owner.Data.heroName} ↔ {lowestAlly.Data.heroName}");
        OnMechanicTriggered?.Invoke(state.owner, MechanicType.ShieldSwap, 
            "护盾互换！");
    }
    
    // --- SpawnMinions ---
    private void ProcessSpawnMinions(MechanicEnemyState state, List<Hero> enemyUnits)
    {
        var intervals = GetMechanicParamList(state, "spawn_interval_per_phase");
        int interval = intervals != null && state.currentPhase <= intervals.Count
            ? intervals[state.currentPhase - 1]
            : 2;
        int maxMinions = GetMechanicParamInt(state, "max_minions", 3);
        
        // 计算当前小怪数量
        int currentMinions = enemyUnits.Count(u => u != null && !u.IsBoss && !u.IsDead);
        if (currentMinions >= maxMinions) return;
        
        if (state.mechanicTurnCounter % interval != 0) return;
        
        // 召唤小怪
        float minionPct = GetMechanicParamFloat(state, "minion_stat_pct", 0.4f);
        var minion = SpawnMinion(state.owner, minionPct);
        if (minion != null)
        {
            var newMinions = new List<Hero> { minion };
            OnMinionsSpawned?.Invoke(newMinions);
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.SpawnMinions,
                $"召唤了小怪！当前{currentMinions + 1}/{maxMinions}");
        }
    }
    
    // --- TimeBomb ---
    private void ProcessTimeBomb(MechanicEnemyState state, List<Hero> playerUnits)
    {
        var timers = GetMechanicParamList(state, "bomb_timer_turns_per_phase");
        int maxTimer = timers != null && state.currentPhase <= timers.Count
            ? timers[state.currentPhase - 1]
            : 5;
        
        int remaining = maxTimer - state.mechanicTurnCounter;
        int warningAt = GetMechanicParamInt(state, "show_warning_at_turns_remaining", 3);
        
        if (remaining <= warningAt && remaining > 0)
        {
            OnMechanicWarning?.Invoke(state.owner, 
                $"⚠ 定时炸弹！剩余 {remaining} 回合！");
        }
        
        if (remaining <= 0)
        {
            // 爆炸！
            float dmgPct = GetMechanicParamFloat(state, "bomb_damage_pct", 0.8f);
            foreach (var hero in playerUnits)
            {
                if (hero == null || hero.IsDead) continue;
                int dmg = Mathf.RoundToInt(hero.MaxHealth * dmgPct);
                hero.TakeDamage(dmg, state.owner);
            }
            OnBombExploded?.Invoke(state.owner, Mathf.RoundToInt(
                playerUnits.FirstOrDefault()?.MaxHealth * dmgPct ?? 0));
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.TimeBomb,
                "💣 定时炸弹爆炸！全体受到大量伤害！");
        }
    }
    
    // --- DamageReflect ---
    private void HandleReflect(MechanicEnemyState state, int damage, Hero attacker)
    {
        var pcts = GetMechanicParamList(state, "reflect_pct_per_phase");
        float reflectPct = pcts != null && state.currentPhase <= pcts.Count
            ? pcts[state.currentPhase - 1] / 100f
            : 0.3f;
        
        int minDmg = GetMechanicParamInt(state, "min_damage_to_reflect", 5);
        int reflectDmg = Mathf.Max(minDmg, Mathf.RoundToInt(damage * reflectPct));
        
        if (attacker != null && !attacker.IsDead)
        {
            attacker.TakeDamage(reflectDmg);
            Debug.Log($"[Mechanic] 反伤: {state.owner.Data.heroName} 反弹 {reflectDmg} 给 {attacker.Data.heroName}");
        }
    }
    
    // --- Berserk ---
    private void HandleBerserk(MechanicEnemyState state)
    {
        float hpPct = (float)state.owner.CurrentHealth / state.owner.MaxHealth;
        var thresholds = GetMechanicParamList(state, "hp_thresholds");
        if (thresholds == null) return;
        
        float atkBonus = GetMechanicParamFloat(state, "atk_bonus_per_threshold", 0.5f);
        int stackCount = 0;
        foreach (var threshold in thresholds)
        {
            if (hpPct <= threshold / 100f)
                stackCount++;
        }
        
        // 应用攻击加成
        int baseAtk = state.owner.Attack; // 使用基础攻击
        int newAtk = Mathf.RoundToInt(baseAtk * (1f + atkBonus * stackCount));
        state.owner.BattleAttack = newAtk;
    }
    
    // --- StealthAssassin ---
    private bool HandleStealthAction(MechanicEnemyState state, 
        List<Hero> enemies, List<Hero> allies)
    {
        var intervals = GetMechanicParamList(state, "stealth_interval_per_phase");
        int interval = intervals != null && state.currentPhase <= intervals.Count
            ? intervals[state.currentPhase - 1]
            : 2;
        
        if (state.mechanicTurnCounter % interval == 0)
        {
            // 进入隐身
            state.isStealthed = true;
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.StealthAssassin,
                "隐身刺客消失了！");
            return true; // 跳过本轮行动
        }
        
        if (state.isStealthed)
        {
            // 隐身攻击 — 打最高攻击
            var target = FindHighestAttackHero(enemies);
            if (target != null)
            {
                var multipliers = GetMechanicParamList(state, "stealth_damage_multiplier_per_phase");
                float mult = multipliers != null && state.currentPhase <= multipliers.Count
                    ? multipliers[state.currentPhase - 1] / 100f
                    : 1.5f;
                
                int dmg = Mathf.RoundToInt(state.owner.BattleAttack * mult);
                target.TakeDamage(dmg, state.owner);
                
                state.isStealthed = false;
                OnMechanicTriggered?.Invoke(state.owner, MechanicType.StealthAssassin,
                    $"隐身刺客突袭 {target.Data.heroName}！造成{dmg}伤害！");
            }
            return true; // 已处理
        }
        
        return false; // 非隐身回合，走正常AI
    }
    
    // --- CurseSpread ---
    private void ProcessCurseSpread(MechanicEnemyState state, List<Hero> playerUnits)
    {
        // 每回合处理已诅咒单位的掉血
        var pcts = GetMechanicParamList(state, "curse_damage_pct_per_phase");
        float dmgPct = pcts != null && state.currentPhase <= pcts.Count
            ? pcts[state.currentPhase - 1] / 100f
            : 0.03f;
        
        foreach (var hero in playerUnits)
        {
            if (hero == null || hero.IsDead) continue;
            if (!state.cursedTargetInstanceIds.Contains(hero.GetInstanceID())) continue;
            
            int curseDmg = Mathf.RoundToInt(hero.MaxHealth * dmgPct);
            hero.TakeDamage(curseDmg);
        }
    }
    
    // --- ElementalShift ---
    private void HandleElementalShift(MechanicEnemyState state)
    {
        var cycle = GetMechanicParamStringList(state, "immune_cycle");
        if (cycle == null || cycle.Count == 0) return;
        
        int idx = _currentTurn % cycle.Count;
        state.immuneElement = cycle[idx];
        
        Debug.Log($"[Mechanic] 元素切换: 免疫 {state.immuneElement}");
        OnMechanicTriggered?.Invoke(state.owner, MechanicType.ElementalShift,
            $"元素使者现在免疫 {state.immuneElement} 类攻击！");
    }
    
    // --- SplitOnDeath ---
    private void HandleBossDeath(MechanicEnemyState state, List<Hero> enemyUnits)
    {
        if (state.mechanicType != MechanicType.SplitOnDeath || state.hasSplit) return;
        
        int splitCount = GetMechanicParamInt(state, "split_count", 2);
        float statPct = GetMechanicParamFloat(state, "split_stat_pct", 0.5f);
        bool canSplitAgain = GetMechanicParamBool(state, "split_can_split_again", false);
        
        var splits = new List<Hero>();
        for (int i = 0; i < splitCount; i++)
        {
            var split = CreateSplitCopy(state.owner, statPct);
            if (split != null)
            {
                splits.Add(split);
                if (!canSplitAgain)
                    split.IsBoss = false; // 副本不再触发分裂
            }
        }
        
        state.hasSplit = true;
        if (splits.Count > 0)
        {
            OnMinionsSpawned?.Invoke(splits);
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.SplitOnDeath,
                $"分裂母体分裂为 {splits.Count} 个副本！");
        }
    }
    
    // --- HealOnAttack (通过事件钩子) ---
    /// <summary>
    /// 攻击后回血 — 由 AutoChessAI.NormalAttack 改造后调用
    /// </summary>
    public void OnBossAttacked(Hero boss, int damageDealt)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state)) return;
        if (state.mechanicType != MechanicType.HealOnAttack) return;
        
        float healPct = GetMechanicParamFloat(state, "heal_pct", 0.5f);
        int heal = Mathf.RoundToInt(damageDealt * healPct);
        boss.Heal(heal);
    }
    
    // ========== 工具方法 ==========
    
    private MechanicEnemyEntry GetMechanicDataForLevel(int levelId)
    {
        // 从配置查找适合当前关卡的机制怪
        var entries = _config?.mechanic_enemies;
        if (entries == null) return null;
        
        // 16+关使用组合机制
        if (levelId >= 16)
        {
            // 随机选择一个基础机制
            var candidates = entries.FindAll(e => e.min_level <= levelId);
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }
        
        // 11-15关按配置的min_level匹配
        return entries.Find(e => e.min_level == levelId) 
            ?? entries.Find(e => e.min_level <= levelId);
    }
    
    private MechanicEnemyEntry GetMechanicData(MechanicType type)
    {
        return _config?.mechanic_enemies?.Find(
            e => ParseMechanicType(e.mechanic_type) == type);
    }
    
    private MechanicEnemyState CreateState(Hero boss, MechanicEnemyEntry data, int levelId)
    {
        float scaling = 1f + (levelId - 10) * 
            (_config?.difficulty_scaling?.mechanic_strength_per_level_above_10 ?? 0.05f);
        
        return new MechanicEnemyState
        {
            mechanicType = ParseMechanicType(data.mechanic_type),
            mechanicStrength = scaling,
            mechanicTurnCounter = 0,
            currentPhase = 1,
            isActive = true,
            owner = boss
        };
    }
    
    private void ApplyStatMultipliers(Hero boss, MechanicEnemyEntry data)
    {
        var stats = data.base_stats;
        if (stats == null) return;
        
        boss.BoostMaxHealth(stats.health_multiplier - 1f);
        boss.BoostAttack(stats.attack_multiplier - 1f);
        boss.BoostDefense(stats.defense_multiplier - 1f);
    }
    
    private void CheckPhaseTransition(MechanicEnemyState state)
    {
        if (state.owner == null) return;
        float hpPct = (float)state.owner.CurrentHealth / state.owner.MaxHealth;
        
        int newPhase = 1;
        if (hpPct <= 0.33f) newPhase = 3;
        else if (hpPct <= 0.66f) newPhase = 2;
        
        if (newPhase > state.currentPhase)
        {
            int oldPhase = state.currentPhase;
            state.currentPhase = newPhase;
            
            var data = GetMechanicData(state.mechanicType);
            string tip = data?.phase_tips != null && newPhase <= data.phase_tips.Count
                ? data.phase_tips[newPhase - 1]
                : $"Boss进入第{newPhase}阶段！";
            
            OnBossPhaseChanged?.Invoke(state.owner, newPhase, tip);
        }
    }
    
    private Hero SpawnMinion(Hero parent, float statPct)
    {
        var go = new GameObject($"Minion_{parent.GetInstanceID()}");
        var minion = go.AddComponent<Hero>();
        
        var template = GameData.CreateEnemyGrunt();
        minion.Initialize(template);
        
        // 缩小属性
        minion.BoostMaxHealth(-(1f - statPct));
        minion.BoostAttack(-(1f - statPct));
        
        return minion;
    }
    
    private Hero CreateSplitCopy(Hero original, float statPct)
    {
        var go = new GameObject($"Split_{original.GetInstanceID()}");
        var copy = go.AddComponent<Hero>();
        copy.Initialize(original.Data, original.StarLevel);
        
        // 设置为原始属性的statPct%
        copy.BoostMaxHealth(-(1f - statPct));
        copy.BoostAttack(-(1f - statPct));
        copy.BoostDefense(-(1f - statPct));
        
        return copy;
    }
    
    private Hero FindLowestHealthAlly(Hero self, List<Hero> allies)
    {
        Hero lowest = null;
        float lowestPct = 1f;
        foreach (var ally in allies)
        {
            if (ally == null || ally.IsDead || ally == self) continue;
            float pct = (float)ally.CurrentHealth / ally.MaxHealth;
            if (pct < lowestPct)
            {
                lowestPct = pct;
                lowest = ally;
            }
        }
        return lowest;
    }
    
    private Hero FindHighestAttackHero(List<Hero> heroes)
    {
        Hero best = null;
        foreach (var h in heroes)
        {
            if (h == null || h.IsDead) continue;
            if (best == null || h.BattleAttack > best.BattleAttack)
                best = h;
        }
        return best;
    }
    
    // 配置参数读取工具
    private int GetMechanicParamInt(MechanicEnemyState state, string key, int fallback)
    {
        var data = GetMechanicData(state.mechanicType);
        if (data?.mechanic_params == null) return fallback;
        if (data.mechanic_params.TryGetValue(key, out var val))
            return val is int i ? i : fallback;
        return fallback;
    }
    
    private float GetMechanicParamFloat(MechanicEnemyState state, string key, float fallback)
    {
        var data = GetMechanicData(state.mechanicType);
        if (data?.mechanic_params == null) return fallback;
        if (data.mechanic_params.TryGetValue(key, out var val))
            return val is float f ? f : (val is int i ? (float)i : fallback);
        return fallback;
    }
    
    private bool GetMechanicParamBool(MechanicEnemyState state, string key, bool fallback)
    {
        var data = GetMechanicData(state.mechanicType);
        if (data?.mechanic_params == null) return fallback;
        if (data.mechanic_params.TryGetValue(key, out var val))
            return val is bool b ? b : fallback;
        return fallback;
    }
    
    private List<int> GetMechanicParamList(MechanicEnemyState state, string key)
    {
        // 从JSON的mechanic_params中读取int数组
        // 需要在MechanicEnemyEntry中定义
        return null; // 简化，实际从配置读取
    }
    
    private List<string> GetMechanicParamStringList(MechanicEnemyState state, string key)
    {
        return null; // 简化
    }
    
    private MechanicType ParseMechanicType(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr)) return MechanicType.None;
        return System.Enum.TryParse<MechanicType>(typeStr, out var t) ? t : MechanicType.None;
    }
}
```

### 2.6 BalanceProvider 扩展

在 `BalanceProvider.cs` 中新增:

```csharp
// ========== 新增字段 ==========
private static MechanicEnemiesConfig _mechanicEnemies;

// ========== 新增懒加载属性 ==========
public static MechanicEnemiesConfig MechanicEnemiesConfig => 
    _mechanicEnemies ?? (_mechanicEnemies = ConfigLoader.LoadMechanicEnemies());

// ========== 新增公共方法 ==========
/// <summary>
/// 获取机制怪配置
/// </summary>
public static MechanicEnemiesConfig GetMechanicEnemies()
{
    return MechanicEnemiesConfig;
}

/// <summary>
/// 根据关卡ID获取匹配的机制怪配置
/// </summary>
public static MechanicEnemyEntry GetMechanicEnemyForLevel(int levelId)
{
    var entries = MechanicEnemiesConfig?.mechanic_enemies;
    if (entries == null) return null;
    
    if (levelId >= 16)
    {
        var candidates = entries.FindAll(e => e.min_level <= levelId);
        return candidates.Count > 0 ? candidates[0] : null; // 外部负责随机
    }
    
    return entries.Find(e => e.min_level == levelId)
        ?? entries.Find(e => e.min_level <= levelId);
}

/// <summary>
/// 获取骰子面效果配置
/// </summary>
public static FaceEffectsConfig GetFaceEffectsConfig()
{
    return FaceEffects;
}

// ========== ReloadAll 中新增 ==========
// 在 ReloadAll() 方法中添加:
_mechanicEnemies = null;
```

### 2.7 BattleManager 集成改动

```csharp
// ===== BattleManager.cs 需要的改动 =====

// 1. 新增字段
public MechanicEnemySystem MechanicSystem { get; private set; }
public int CurrentLevelId { get; private set; }

// 2. StartBattle 改造
public void StartBattle(List<Hero> players, List<Hero> enemies, 
    DiceCombination diceCombo = null, int levelId = 0)
{
    // ... 现有代码 ...
    CurrentLevelId = levelId;
    
    // 现有: 应用骰子组合
    if (diceCombo != null) ApplyDiceComboEffects(diceCombo);
    
    // >>> 新增: 注册机制怪 <<<
    MechanicSystem = new MechanicEnemySystem();
    MechanicSystem.RegisterBossMechanics(enemyUnits, levelId);
    
    // >>> 新增: 订阅机制怪事件 <<<
    MechanicSystem.OnMinionsSpawned += HandleMinionsSpawned;
    MechanicSystem.OnMechanicWarning += (boss, msg) => {
        BossMechanicHUD.Instance?.ShowSkillWarning(msg);
    };
    
    // ... 继续现有代码 ...
}

// 3. BattleLoop 改造
IEnumerator BattleLoop()
{
    while (IsBattleActive)
    {
        yield return new WaitForSeconds(battleTickInterval / battleSpeed);
        
        // >>> 新增: 机制怪回合开始处理 <<<
        MechanicSystem?.OnBattleTick(playerUnits, enemyUnits);
        
        // 我方单位行动
        foreach (var unit in playerUnits)
        {
            if (unit == null || unit.IsDead) continue;
            AutoChessAI.TakeAction(unit, enemyUnits, playerUnits);
        }
        
        // 敌方单位行动
        foreach (var unit in enemyUnits)
        {
            if (unit == null || unit.IsDead) continue;
            
            // >>> 新增: 机制怪行动拦截 <<<
            if (MechanicSystem?.OverrideEnemyAction(unit, playerUnits, enemyUnits) == true)
                continue;
            
            AutoChessAI.TakeAction(unit, playerUnits, enemyUnits);
        }
        
        // ... 现有代码 ...
    }
}

// 4. 新增: 召唤小怪处理
private void HandleMinionsSpawned(List<Hero> newMinions)
{
    foreach (var minion in newMinions)
    {
        if (minion != null && !enemyUnits.Contains(minion))
            enemyUnits.Add(minion);
    }
}

// 5. ClearBattle 改造
public void ClearBattle()
{
    MechanicSystem?.ClearBattleState();
    // ... 现有代码 ...
}
```

### 2.8 AutoChessAI 改造

```csharp
// AutoChessAI.cs 的改动:

// 1. TakeAction 中的Boss判断改为使用IsBoss属性
public static void TakeAction(Hero self, List<Hero> enemies, List<Hero> allies)
{
    // ... 现有代码 ...
    
    // 改前: if (self.Data.heroName == "Boss")
    // 改后:
    if (self.IsBoss)
    {
        // Boss寻敌策略保持"攻击最高"
        var highestAtk = FindBest(enemies, (a, b) => a.BattleAttack > b.BattleAttack);
        if (highestAtk != null) target = highestAtk;
    }
    
    // ... 现有代码 ...
}

// 2. NormalAttack 新增Boss攻击回调
static void NormalAttack(Hero self, Hero target)
{
    // ... 现有伤害计算 ...
    target.TakeDamage(damage, self);
    
    // >>> 新增: 攻击回血机制 <<<
    MechanicEnemySystem.Instance?.OnBossAttacked(self, damage);
    
    // ... 现有代码 ...
}

// 3. 治疗者AI同样改为不依赖heroName（使用heroClass）
```

### 2.9 BossMechanicHUD 改造

```csharp
// BossMechanicHUD.cs 的改动:

// 1. RefreshDisplay 改造 — 从MechanicEnemySystem获取提示
private void RefreshDisplay()
{
    if (!isBossActive || currentBoss == null) { /* ... 现有隐藏逻辑 ... */ return; }
    
    // Boss名称
    if (bossNameText != null)
        bossNameText.text = $"🗡 {currentBoss.Data?.heroName ?? "Boss"}";
    
    CreatePhaseIndicators();
    RefreshHealthBar();
    UpdatePhaseText();
    
    // >>> 改造: 从MechanicEnemySystem获取机制提示 <<<
    var mechanicSystem = BattleManager.Instance?.MechanicSystem;
    string tip = mechanicSystem?.GetMechanicTip(currentBoss);
    ShowMechanicTip(tip ?? GetPhaseMechanicTip(currentPhase));
}

// 2. 订阅MechanicEnemySystem事件
protected override void OnShow()
{
    // ... 现有代码 ...
    
    var mechanicSystem = BattleManager.Instance?.MechanicSystem;
    if (mechanicSystem != null)
    {
        mechanicSystem.OnMechanicTriggered += OnMechanicTriggered;
        mechanicSystem.OnBossPhaseChanged += OnBossPhaseChangedFromSystem;
        mechanicSystem.OnMechanicWarning += OnMechanicWarning;
    }
}

private void OnMechanicTriggered(Hero boss, MechanicType type, string message)
{
    if (boss == currentBoss)
        ShowMechanicTip(message);
}

private void OnBossPhaseChangedFromSystem(Hero boss, int newPhase, string tip)
{
    if (boss != currentBoss) return;
    currentPhase = newPhase;
    UpdatePhaseText();
    CreatePhaseIndicators();
    ShowMechanicTip(tip);
}

private void OnMechanicWarning(Hero boss, string message)
{
    if (boss == currentBoss)
        ShowSkillWarning(message);
}
```

### 2.10 LevelManager 改造

```csharp
// LevelManager.cs — 在CreateDefaultLevel中标记Boss关并关联机制

// 10+关的Boss需要标记IsBoss=true并关联MechanicType
// 改造 SpawnEnemies()：
public List<Hero> SpawnEnemies()
{
    var enemies = new List<Hero>();
    if (CurrentLevel == null) return enemies;
    
    int index = 0;
    foreach (var wave in CurrentLevel.enemyWaves)
    {
        if (wave.enemyData == null) continue;
        
        var go = new GameObject($"Enemy_{index}_{wave.enemyData.heroName}");
        var enemy = go.AddComponent<Hero>();
        enemy.Initialize(wave.enemyData);
        enemy.GridPosition = wave.gridPosition;
        
        // >>> 新增: Boss标记 + 10关后设置机制 <<<
        if (wave.isBoss)
        {
            enemy.IsBoss = true;
        }
        
        enemies.Add(enemy);
        index++;
    }
    
    return enemies;
}

// EnemyWave 新增字段:
// public bool isBoss = false;
// 在CreateDefaultLevel中为Boss关的wave设置 isBoss = true
```

---

## 3. BE-09 骰子面效果执行器

### 3.1 系统概述

骰子面效果执行器负责在战斗中根据投掷的骰子面值触发对应的效果。每个骰子面可以通过肉鸽奖励升级，获得特殊效果（治疗/护盾/暴击/闪避等）。

### 3.2 枚举定义

```csharp
/// <summary>
/// 骰子面效果类型
/// </summary>
public enum FaceEffectType
{
    None = 0,
    
    /// <summary>治疗 — 治疗血量最低的友方X%生命</summary>
    Heal = 1,
    
    /// <summary>护盾 — 全体获得X点护盾</summary>
    Shield = 2,
    
    /// <summary>额外伤害 — 对攻击最高敌人造成X%额外伤害</summary>
    ExtraDamage = 3,
    
    /// <summary>攻速加成 — 全体攻速+X%</summary>
    AttackSpeed = 4,
    
    /// <summary>眩晕 — 随机敌人眩晕1回合</summary>
    Stun = 5,
    
    /// <summary>暴击加成 — 本场战斗暴击率+X%</summary>
    CritBoost = 6,
    
    /// <summary>破甲 — 本次攻击忽略敌人X%防御</summary>
    ArmorBreak = 7,
    
    /// <summary>吸血 — 全体获得X%吸血（本场战斗）</summary>
    LifeSteal = 8,
    
    /// <summary>反伤 — 全体获得X%荆棘反伤</summary>
    Thorns = 9,
    
    /// <summary>净化 — 移除全体友方的debuff（含诅咒）</summary>
    Cleanse = 10
}
```

### 3.3 骰子面效果数据结构

```csharp
/// <summary>
/// 骰子面效果定义 — 可序列化，对应 face_effects.json
/// </summary>
[System.Serializable]
public class FaceEffectDef
{
    /// <summary>效果ID（唯一标识，如 "heal_lowest_ally"）</summary>
    public string effectId;
    
    /// <summary>效果类型</summary>
    public FaceEffectType effectType;
    
    /// <summary>效果名称</summary>
    public string effectName;
    
    /// <summary>效果描述模板（{value}为占位符）</summary>
    public string descriptionTemplate;
    
    /// <summary>目标类型</summary>
    public FaceEffectTarget targetScope;
    
    /// <summary>触发时机</summary>
    public FaceEffectTrigger triggerTiming;
    
    /// <summary>基础效果值（百分比 or 固定值）</summary>
    public float baseValue;
    
    /// <summary>每级成长值</summary>
    public float growthPerLevel;
    
    /// <summary>最大等级</summary>
    public int maxLevel = 3;
    
    /// <summary>适用的骰子面值（null=任意面，[1]=仅面1）</summary>
    public List<int> applicableFaces;
    
    /// <summary>图标Sprite名</summary>
    public string iconRef;
    
    /// <summary>稀有度（影响出现权重）</summary>
    public string rarity = "common";
}

/// <summary>
/// 目标范围
/// </summary>
public enum FaceEffectTarget
{
    Self,               // 自身
    LowestHealthAlly,   // 血量最低友方
    AllAllies,          // 全体友方
    HighestAtkEnemy,    // 攻击最高敌人
    RandomEnemy,        // 随机敌人
    AllEnemies,         // 全体敌人
    AllUnits            // 全体单位
}

/// <summary>
/// 触发时机
/// </summary>
public enum FaceEffectTrigger
{
    OnDiceResult,       // 骰子投出该面时立即触发
    OnBattleStart,      // 战斗开始时（根据骰子结果决定是否激活）
    OnAttack,           // 攻击时触发
    OnDamaged,          // 受伤时触发
    OnKill,             // 击杀时触发
    PerTurn             // 每回合触发
}
```

### 3.4 FaceEffectExecutor 核心接口

```csharp
/// <summary>
/// 骰子面效果执行器 — 解析和执行骰子面效果
/// 文件位置: Assets/Scripts/Dice/FaceEffectExecutor.cs
/// 
/// 设计原则:
/// 1. Dice.FaceEffects 仍为 string[]，存储 effectId
/// 2. 本系统负责 string(effectId) → FaceEffectDef → 执行逻辑 的完整链路
/// 3. 效果配置从 face_effects.json 读取
/// 4. 每种 FaceEffectType 对应一个独立的处理函数
/// 5. 支持效果等级（肉鸽奖励升级）
/// </summary>
public class FaceEffectExecutor
{
    // ========== 单例 ==========
    public static FaceEffectExecutor Instance { get; private set; }
    
    // ========== 事件 ==========
    /// <summary>面效果触发时（用于UI显示）</summary>
    public event System.Action<FaceEffectDef, int, string> OnFaceEffectTriggered;
    // 参数: 效果定义, 骰子面值, 描述文本
    
    /// <summary>面效果升级时</summary>
    public event System.Action<int, FaceEffectDef, int> OnFaceEffectUpgraded;
    // 参数: 骰子索引, 效果定义, 新等级
    
    // ========== 状态 ==========
    private FaceEffectsConfig _config;
    
    /// <summary>
    /// 运行时效果等级表
    /// Key: "diceIndex_faceIndex" (如 "0_3" 表示第0个骰子的第3面)
    /// Value: 效果等级 (1-based)
    /// </summary>
    private Dictionary<string, int> _effectLevels = new Dictionary<string, int>();
    
    /// <summary>
    /// 激活的持续效果（本场战斗有效）
    /// Key: effectId
    /// Value: 效果等级
    /// </summary>
    private Dictionary<string, int> _activeBattleEffects = new Dictionary<string, int>();
    
    public FaceEffectExecutor()
    {
        Instance = this;
        _config = BalanceProvider.GetFaceEffectsConfig();
    }
    
    // ========== 骰子投掷后触发 ==========
    
    /// <summary>
    /// 处理骰子投掷结果 — 检查每个骰子是否有面效果
    /// 由 DiceRoller.OnDiceRolled 事件或 RoguelikeGameManager 在骰子阶段结束后调用
    /// </summary>
    /// <param name="dices">骰子数组</param>
    /// <param name="values">投掷结果</param>
    /// <param name="playerHeroes">我方英雄列表</param>
    /// <param name="enemyHeroes">敌方英雄列表</param>
    public void ProcessDiceResults(Dice[] dices, int[] values, 
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        for (int i = 0; i < dices.Length; i++)
        {
            if (i >= values.Length) break;
            
            int faceValue = values[i];
            var dice = dices[i];
            
            // 获取该面对应的效果ID
            // FaceEffects 数组下标 = faceValue - 1 (面值1 → 下标0)
            int effectIndex = faceValue - 1;
            if (effectIndex < 0 || effectIndex >= dice.FaceEffects.Length) continue;
            
            string effectId = dice.FaceEffects[effectIndex];
            if (string.IsNullOrEmpty(effectId)) continue;
            
            // 查找效果定义
            var effectDef = FindEffectDef(effectId);
            if (effectDef == null) continue;
            
            // 仅处理 OnDiceResult 触发的效果
            if (effectDef.triggerTiming != FaceEffectTrigger.OnDiceResult) continue;
            
            // 获取效果等级
            string levelKey = $"{i}_{effectIndex}";
            int level = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
            
            // 计算效果值
            float effectValue = CalculateEffectValue(effectDef, level);
            
            // 执行效果
            ExecuteEffect(effectDef, effectValue, playerHeroes, enemyHeroes);
            
            // 触发事件
            string desc = FormatDescription(effectDef, effectValue);
            OnFaceEffectTriggered?.Invoke(effectDef, faceValue, desc);
            
            Debug.Log($"[FaceEffect] 骰子{i} 面{faceValue} → {effectDef.effectName}: {desc}");
        }
    }
    
    // ========== 战斗开始时激活持续效果 ==========
    
    /// <summary>
    /// 战斗开始时激活所有 OnBattleStart 触发的面效果
    /// 由 BattleManager.StartBattle() 调用
    /// </summary>
    public void ActivateBattleStartEffects(Dice[] dices, int[] lastValues,
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        _activeBattleEffects.Clear();
        
        for (int i = 0; i < dices.Length; i++)
        {
            if (i >= lastValues.Length) break;
            
            var dice = dices[i];
            for (int faceIdx = 0; faceIdx < dice.FaceEffects.Length; faceIdx++)
            {
                string effectId = dice.FaceEffects[faceIdx];
                if (string.IsNullOrEmpty(effectId)) continue;
                
                var effectDef = FindEffectDef(effectId);
                if (effectDef == null) continue;
                if (effectDef.triggerTiming != FaceEffectTrigger.OnBattleStart) continue;
                
                string levelKey = $"{i}_{faceIdx}";
                int level = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
                float effectValue = CalculateEffectValue(effectDef, level);
                
                ExecuteEffect(effectDef, effectValue, playerHeroes, enemyHeroes);
                _activeBattleEffects[effectId] = level;
                
                string desc = FormatDescription(effectDef, effectValue);
                OnFaceEffectTriggered?.Invoke(effectDef, faceIdx + 1, desc);
            }
        }
    }
    
    // ========== 回合触发 ==========
    
    /// <summary>
    /// 每回合检查 PerTurn 类型的效果
    /// 由 BattleManager.BattleLoop() 中每tick调用
    /// </summary>
    public void ProcessPerTurnEffects(List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        foreach (var kvp in _activeBattleEffects)
        {
            var effectDef = FindEffectDef(kvp.Key);
            if (effectDef == null || effectDef.triggerTiming != FaceEffectTrigger.PerTurn) continue;
            
            float value = CalculateEffectValue(effectDef, kvp.Value);
            ExecuteEffect(effectDef, value, playerHeroes, enemyHeroes);
        }
    }
    
    // ========== 攻击时触发 ==========
    
    /// <summary>
    /// 英雄攻击时检查 OnAttack 类型的面效果
    /// 由 AutoChessAI.NormalAttack() 调用
    /// </summary>
    public void ProcessOnAttackEffects(Hero attacker, Hero target, 
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        foreach (var kvp in _activeBattleEffects)
        {
            var effectDef = FindEffectDef(kvp.Key);
            if (effectDef == null || effectDef.triggerTiming != FaceEffectTrigger.OnAttack) continue;
            
            // 仅我方英雄触发
            if (!playerHeroes.Contains(attacker)) continue;
            
            float value = CalculateEffectValue(effectDef, kvp.Value);
            ExecuteEffect(effectDef, value, playerHeroes, enemyHeroes, attacker, target);
        }
    }
    
    // ========== 升级接口 ==========
    
    /// <summary>
    /// 升级骰子面效果（肉鸽奖励 DiceFaceUpgrade 调用）
    /// </summary>
    /// <param name="diceIndex">骰子索引 (0-2)</param>
    /// <param name="faceIndex">面索引 (0-5)</param>
    /// <param name="effectId">要安装的效果ID</param>
    /// <param name="targetDice">目标骰子对象</param>
    /// <returns>升级后的等级，0=失败</returns>
    public int UpgradeFaceEffect(int diceIndex, int faceIndex, string effectId, Dice targetDice)
    {
        if (targetDice == null) return 0;
        if (faceIndex < 0 || faceIndex >= targetDice.FaceEffects.Length) return 0;
        
        var effectDef = FindEffectDef(effectId);
        if (effectDef == null) return 0;
        
        string levelKey = $"{diceIndex}_{faceIndex}";
        
        // 检查是否已有该效果
        if (targetDice.FaceEffects[faceIndex] == effectId)
        {
            // 同效果 → 升级
            int currentLevel = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
            if (currentLevel >= effectDef.maxLevel) return 0; // 已满级
            
            int newLevel = currentLevel + 1;
            _effectLevels[levelKey] = newLevel;
            OnFaceEffectUpgraded?.Invoke(diceIndex, effectDef, newLevel);
            return newLevel;
        }
        else
        {
            // 不同效果 → 替换并设为1级
            targetDice.UpgradeFace(faceIndex, effectId);
            _effectLevels[levelKey] = 1;
            OnFaceEffectUpgraded?.Invoke(diceIndex, effectDef, 1);
            return 1;
        }
    }
    
    /// <summary>
    /// 获取可用的升级选项（供 RoguelikeRewardSystem 调用）
    /// </summary>
    public List<FaceUpgradeOption> GetAvailableUpgrades(Dice[] dices, int levelId)
    {
        var options = new List<FaceUpgradeOption>();
        var allEffects = _config?.effects ?? new List<FaceEffectDef>();
        
        for (int diceIdx = 0; diceIdx < dices.Length; diceIdx++)
        {
            for (int faceIdx = 0; faceIdx < dices[diceIdx].FaceEffects.Length; faceIdx++)
            {
                string currentEffectId = dices[diceIdx].FaceEffects[faceIdx];
                
                if (string.IsNullOrEmpty(currentEffectId))
                {
                    // 空面 → 可以安装任意适用效果
                    foreach (var effectDef in allEffects)
                    {
                        if (effectDef.applicableFaces == null || 
                            effectDef.applicableFaces.Contains(faceIdx + 1))
                        {
                            options.Add(new FaceUpgradeOption
                            {
                                diceIndex = diceIdx,
                                faceIndex = faceIdx,
                                effectDef = effectDef,
                                isNew = true
                            });
                        }
                    }
                }
                else
                {
                    // 已有效果 → 可以升级
                    var currentDef = FindEffectDef(currentEffectId);
                    if (currentDef == null) continue;
                    
                    string levelKey = $"{diceIdx}_{faceIdx}";
                    int currentLevel = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
                    
                    if (currentLevel < currentDef.maxLevel)
                    {
                        options.Add(new FaceUpgradeOption
                        {
                            diceIndex = diceIdx,
                            faceIndex = faceIdx,
                            effectDef = currentDef,
                            isNew = false,
                            currentLevel = currentLevel
                        });
                    }
                }
            }
        }
        
        return options;
    }
    
    // ========== 核心执行逻辑 ==========
    
    private void ExecuteEffect(FaceEffectDef def, float value,
        List<Hero> playerHeroes, List<Hero> enemyHeroes,
        Hero specificTarget = null, Hero specificAttacker = null)
    {
        switch (def.effectType)
        {
            case FaceEffectType.Heal:
                ExecuteHeal(def, value, playerHeroes);
                break;
            case FaceEffectType.Shield:
                ExecuteShield(def, value, playerHeroes);
                break;
            case FaceEffectType.ExtraDamage:
                ExecuteExtraDamage(def, value, enemyHeroes);
                break;
            case FaceEffectType.AttackSpeed:
                ExecuteAttackSpeed(def, value, playerHeroes);
                break;
            case FaceEffectType.Stun:
                ExecuteStun(def, value, enemyHeroes);
                break;
            case FaceEffectType.CritBoost:
                ExecuteCritBoost(def, value, playerHeroes);
                break;
            case FaceEffectType.ArmorBreak:
                ExecuteArmorBreak(def, value, specificAttacker);
                break;
            case FaceEffectType.LifeSteal:
                ExecuteLifeSteal(def, value, playerHeroes);
                break;
            case FaceEffectType.Thorns:
                ExecuteThorns(def, value, playerHeroes);
                break;
            case FaceEffectType.Cleanse:
                ExecuteCleanse(def, playerHeroes);
                break;
        }
    }
    
    // --- 效果执行器 ---
    
    private void ExecuteHeal(FaceEffectDef def, float value, List<Hero> allies)
    {
        Hero target = def.targetScope switch
        {
            FaceEffectTarget.LowestHealthAlly => FindLowestHealthAlly(allies),
            FaceEffectTarget.AllAllies => null, // 全体治疗
            _ => null
        };
        
        float healPct = value / 100f;
        
        if (target != null)
        {
            int heal = Mathf.RoundToInt(target.MaxHealth * healPct);
            target.Heal(heal);
        }
        else
        {
            // 全体治疗
            foreach (var hero in allies)
            {
                if (hero == null || hero.IsDead) continue;
                int heal = Mathf.RoundToInt(hero.MaxHealth * healPct);
                hero.Heal(heal);
            }
        }
    }
    
    private void ExecuteShield(FaceEffectDef def, float value, List<Hero> allies)
    {
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddShield(Mathf.RoundToInt(value));
        }
    }
    
    private void ExecuteExtraDamage(FaceEffectDef def, float value, List<Hero> enemies)
    {
        Hero target = def.targetScope switch
        {
            FaceEffectTarget.HighestAtkEnemy => FindHighestAttackEnemy(enemies),
            FaceEffectTarget.RandomEnemy => FindRandomEnemy(enemies),
            _ => enemies?.FirstOrDefault(h => h != null && !h.IsDead)
        };
        
        if (target == null) return;
        
        float dmgPct = value / 100f;
        int dmg = Mathf.RoundToInt(target.MaxHealth * dmgPct);
        target.TakeDamage(Mathf.Max(1, dmg));
    }
    
    private void ExecuteAttackSpeed(FaceEffectDef def, float value, List<Hero> allies)
    {
        float speedBonus = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.BattleAttackSpeed += speedBonus;
        }
    }
    
    private void ExecuteStun(FaceEffectDef def, float value, List<Hero> enemies)
    {
        var target = FindRandomEnemy(enemies);
        if (target != null)
        {
            // Hero 没有Stun字段 — 需要新增或使用标记
            // MVP方案: stun通过跳过行动实现
            target.SetStunned(true); // 需要在Hero中新增
        }
    }
    
    private void ExecuteCritBoost(FaceEffectDef def, float value, List<Hero> allies)
    {
        float critBonus = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.BattleCritRate = Mathf.Clamp01(hero.BattleCritRate + critBonus);
        }
    }
    
    private void ExecuteArmorBreak(FaceEffectDef def, float value, Hero attacker)
    {
        if (attacker == null) return;
        // 设置攻击者的破甲标记
        // value = 忽略防御的百分比
        attacker.HasArmorBreak = true; // 已有此字段
    }
    
    private void ExecuteLifeSteal(FaceEffectDef def, float value, List<Hero> allies)
    {
        float lifestealPct = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddRelicBuff(new RelicBuff(RelicBuffType.LifeSteal, lifestealPct, "骰子面效果"));
        }
    }
    
    private void ExecuteThorns(FaceEffectDef def, float value, List<Hero> allies)
    {
        float thornsPct = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddRelicBuff(new RelicBuff(RelicBuffType.Thorns, thornsPct, "骰子面效果"));
        }
    }
    
    private void ExecuteCleanse(FaceEffectDef def, List<Hero> allies)
    {
        // 移除诅咒 — 需要与MechanicEnemySystem的CurseSpread联动
        var mechanicSystem = MechanicEnemySystem.Instance;
        if (mechanicSystem != null)
        {
            foreach (var hero in allies)
            {
                if (hero != null && !hero.IsDead)
                    mechanicSystem.RemoveCurseFromHero(hero);
            }
        }
    }
    
    // ========== 工具方法 ==========
    
    private FaceEffectDef FindEffectDef(string effectId)
    {
        return _config?.effects?.Find(e => e.effectId == effectId);
    }
    
    private float CalculateEffectValue(FaceEffectDef def, int level)
    {
        return def.baseValue + def.growthPerLevel * (level - 1);
    }
    
    private string FormatDescription(FaceEffectDef def, float value)
    {
        return def.descriptionTemplate?.Replace("{value}", Mathf.RoundToInt(value).ToString())
            ?? $"{def.effectName}: {value}";
    }
    
    private Hero FindLowestHealthAlly(List<Hero> allies)
    {
        Hero lowest = null;
        float lowestPct = 1f;
        foreach (var h in allies)
        {
            if (h == null || h.IsDead) continue;
            float pct = (float)h.CurrentHealth / h.MaxHealth;
            if (pct < lowestPct) { lowestPct = pct; lowest = h; }
        }
        return lowest;
    }
    
    private Hero FindHighestAttackEnemy(List<Hero> enemies)
    {
        Hero best = null;
        foreach (var h in enemies)
        {
            if (h == null || h.IsDead) continue;
            if (best == null || h.BattleAttack > best.BattleAttack) best = h;
        }
        return best;
    }
    
    private Hero FindRandomEnemy(List<Hero> enemies)
    {
        var alive = enemies?.FindAll(h => h != null && !h.IsDead);
        if (alive == null || alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }
    
    // ========== 清理 ==========
    
    public void ClearBattleEffects()
    {
        _activeBattleEffects.Clear();
    }
    
    public void ClearAll()
    {
        _effectLevels.Clear();
        _activeBattleEffects.Clear();
    }
}

/// <summary>
/// 升级选项（供UI展示）
/// </summary>
public class FaceUpgradeOption
{
    public int diceIndex;
    public int faceIndex;
    public FaceEffectDef effectDef;
    public bool isNew;
    public int currentLevel;
    
    public string GetDisplayText()
    {
        if (isNew)
            return $"骰子{diceIndex + 1} 面{faceIndex + 1} → {effectDef.effectName}";
        else
            return $"骰子{diceIndex + 1} 面{faceIndex + 1} {effectDef.effectName} Lv{currentLevel}→{currentLevel + 1}";
    }
}
```

### 3.5 JSON配置Schema

**文件路径**: `Assets/Resources/Data/face_effects.json`

```json
{
    "effects": [
        {
            "effectId": "heal_lowest_ally",
            "effectType": "Heal",
            "effectName": "治愈之光",
            "descriptionTemplate": "治疗血量最低友方{value}%生命",
            "targetScope": "LowestHealthAlly",
            "triggerTiming": "OnDiceResult",
            "baseValue": 15,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": [1],
            "iconRef": "icon_heal",
            "rarity": "common"
        },
        {
            "effectId": "shield_all",
            "effectType": "Shield",
            "effectName": "守护之盾",
            "descriptionTemplate": "全体获得{value}点护盾",
            "targetScope": "AllAllies",
            "triggerTiming": "OnDiceResult",
            "baseValue": 20,
            "growthPerLevel": 10,
            "maxLevel": 3,
            "applicableFaces": [2],
            "iconRef": "icon_shield",
            "rarity": "common"
        },
        {
            "effectId": "extra_damage_highest",
            "effectType": "ExtraDamage",
            "effectName": "精准打击",
            "descriptionTemplate": "对攻击最高敌人造成{value}%额外伤害",
            "targetScope": "HighestAtkEnemy",
            "triggerTiming": "OnDiceResult",
            "baseValue": 10,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": [3],
            "iconRef": "icon_damage",
            "rarity": "common"
        },
        {
            "effectId": "attack_speed_all",
            "effectType": "AttackSpeed",
            "effectName": "疾风之力",
            "descriptionTemplate": "全体攻速+{value}%",
            "targetScope": "AllAllies",
            "triggerTiming": "OnDiceResult",
            "baseValue": 15,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": [4],
            "iconRef": "icon_speed",
            "rarity": "common"
        },
        {
            "effectId": "stun_random",
            "effectType": "Stun",
            "effectName": "雷击",
            "descriptionTemplate": "随机敌人眩晕1回合",
            "targetScope": "RandomEnemy",
            "triggerTiming": "OnDiceResult",
            "baseValue": 1,
            "growthPerLevel": 0,
            "maxLevel": 1,
            "applicableFaces": [5],
            "iconRef": "icon_stun",
            "rarity": "rare"
        },
        {
            "effectId": "crit_boost_all",
            "effectType": "CritBoost",
            "effectName": "锐利之眼",
            "descriptionTemplate": "本场战斗暴击率+{value}%",
            "targetScope": "AllAllies",
            "triggerTiming": "OnBattleStart",
            "baseValue": 10,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": [6],
            "iconRef": "icon_crit",
            "rarity": "common"
        },
        {
            "effectId": "armor_break_self",
            "effectType": "ArmorBreak",
            "effectName": "破甲之击",
            "descriptionTemplate": "攻击忽略{value}%防御",
            "targetScope": "Self",
            "triggerTiming": "OnBattleStart",
            "baseValue": 50,
            "growthPerLevel": 10,
            "maxLevel": 3,
            "applicableFaces": null,
            "iconRef": "icon_armor_break",
            "rarity": "rare"
        },
        {
            "effectId": "lifesteal_all",
            "effectType": "LifeSteal",
            "effectName": "吸血光环",
            "descriptionTemplate": "全体获得{value}%吸血",
            "targetScope": "AllAllies",
            "triggerTiming": "OnBattleStart",
            "baseValue": 10,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": null,
            "iconRef": "icon_lifesteal",
            "rarity": "rare"
        },
        {
            "effectId": "thorns_all",
            "effectType": "Thorns",
            "effectName": "荆棘护甲",
            "descriptionTemplate": "全体获得{value}%反伤",
            "targetScope": "AllAllies",
            "triggerTiming": "OnBattleStart",
            "baseValue": 10,
            "growthPerLevel": 5,
            "maxLevel": 3,
            "applicableFaces": null,
            "iconRef": "icon_thorns",
            "rarity": "rare"
        },
        {
            "effectId": "cleanse_all",
            "effectType": "Cleanse",
            "effectName": "净化之光",
            "descriptionTemplate": "移除全体友方的debuff",
            "targetScope": "AllAllies",
            "triggerTiming": "OnDiceResult",
            "baseValue": 1,
            "growthPerLevel": 0,
            "maxLevel": 1,
            "applicableFaces": null,
            "iconRef": "icon_cleanse",
            "rarity": "epic"
        }
    ],
    
    "upgrade_config": {
        "max_upgrades_per_run": 6,
        "cost_free_levels": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        "rare_min_level": 8,
        "epic_min_level": 12
    }
}
```

### 3.6 RoguelikeGameManager 集成

```csharp
// RoguelikeGameManager.cs 新增:

public FaceEffectExecutor FaceEffectExecutor { get; private set; }

// StartNewGame 中新增:
FaceEffectExecutor = new FaceEffectExecutor();

// StartBattle 中新增:
public void StartBattle()
{
    var enemies = LevelGenerator.GenerateEnemies(CurrentLevel);
    RelicSystem.ApplyRelicEffects(PlayerHeroes);
    
    // >>> 新增: 面效果激活 <<<
    if (FaceEffectExecutor != null && DiceRoller != null)
    {
        FaceEffectExecutor.ActivateBattleStartEffects(
            DiceRoller.Dices, 
            DiceRoller.GetCurrentValues(),
            PlayerHeroes, 
            enemies
        );
    }
    
    // >>> 新增: 骰子面即时效果 <<<
    if (FaceEffectExecutor != null && DiceRoller != null)
    {
        FaceEffectExecutor.ProcessDiceResults(
            DiceRoller.Dices,
            DiceRoller.GetCurrentValues(),
            PlayerHeroes,
            enemies
        );
    }
    
    BattleManager.Instance?.StartBattle(PlayerHeroes, enemies, LastDiceCombo, CurrentLevel);
}
```

### 3.7 Hero 需要新增的字段

```csharp
// Hero.cs 新增:

/// <summary>是否被眩晕（本回合跳过行动）</summary>
public bool IsStunned { get; set; } = false;

/// <summary>
/// 设置眩晕状态
/// </summary>
public void SetStunned(bool stunned)
{
    IsStunned = stunned;
}

// BattleManager.BattleLoop 中增加眩晕检查:
// 在遍历单位行动时:
if (unit.IsStunned) { unit.IsStunned = false; continue; }
```

---

## 4. 集成点与改造清单

### 4.1 BE-08 改造文件清单

| 文件 | 改动类型 | 改动描述 |
|------|----------|----------|
| `Battle/MechanicEnemySystem.cs` | **新增** | 核心系统文件，~400行 |
| `Battle/BattleManager.cs` | 改造 | 新增 MechanicSystem 字段、StartBattle 增加levelId参数、BattleLoop增加机制Tick |
| `Battle/AutoChessAI.cs` | 改造 | heroName=="Boss" → IsBoss、新增攻击回调 |
| `UI/Panels/BossMechanicHUD.cs` | 改造 | 订阅MechanicEnemySystem事件，提示内容改为从系统获取 |
| `Level/LevelManager.cs` | 改造 | SpawnEnemies中设置IsBoss标记 |
| `Data/BalanceProvider.cs` | 改造 | 新增 MechanicEnemiesConfig 缓存和Get方法 |
| `Data/ConfigLoader.cs` | 改造 | 新增 LoadMechanicEnemies() |
| `Data/mechanic_enemies.json` | **新增** | 机制怪配置JSON |
| `Level/EnemyWave` | 改造 | 新增 isBoss 字段 |

### 4.2 BE-09 改造文件清单

| 文件 | 改动类型 | 改动描述 |
|------|----------|----------|
| `Dice/FaceEffectExecutor.cs` | **新增** | 核心执行器文件，~350行 |
| `Dice/Dice.cs` | 无改动 | FaceEffects string[] 保持不变 |
| `Roguelike/RoguelikeGameManager.cs` | 改造 | 新增 FaceEffectExecutor 实例、StartBattle中调用激活 |
| `Heroes/Hero.cs` | 改造 | 新增 IsStunned 字段和 SetStunned 方法 |
| `Battle/BattleManager.cs` | 改造 | BattleLoop中增加眩晕检查 |
| `Data/BalanceProvider.cs` | 改造 | 新增 FaceEffectsConfig 缓存和Get方法 |
| `Data/ConfigLoader.cs` | 改造 | 新增 LoadFaceEffects() |
| `Data/face_effects.json` | **新增** | 面效果配置JSON |

### 4.3 调用时序图

```
┌─────────────── 骰子阶段 ───────────────┐
│ DiceRoller.RollAll()                    │
│   → FaceEffectExecutor.ProcessDiceResults()  │  ← BE-09
│     → ExecuteEffect (Heal/Shield/...)   │
│     → OnFaceEffectTriggered (UI)        │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────── 战斗阶段 ───────────────┐
│ BattleManager.StartBattle()             │
│   → MechanicEnemySystem.RegisterBoss()  │  ← BE-08
│   → FaceEffectExecutor.ActivateBattle() │  ← BE-09
│   → ApplyDiceComboEffects()             │
│                                         │
│ BattleLoop (每tick):                    │
│   → MechanicSystem.OnBattleTick()       │  ← BE-08 机制处理
│   → FaceEffect.ProcessPerTurnEffects()  │  ← BE-09 持续效果
│   → 遍历我方行动:                       │
│     if (unit.IsStunned) skip            │  ← BE-09 眩晕
│     AutoChessAI.TakeAction()            │
│   → 遍历敌方行动:                       │
│     if MechanicSystem.OverrideAction()  │  ← BE-08 机制行动
│       continue                          │
│     AutoChessAI.TakeAction()            │
│                                         │
│ BossMechanicHUD.Update():               │
│   → 订阅 MechanicSystem 事件            │  ← BE-08
│   → 显示机制提示/预警                   │
└─────────────────────────────────────────┘
```

---

## 5. JSON配置数据结构定义

需要在 ConfigLoader.cs 中新增的配置类：

```csharp
// ========== 机制怪配置 ==========

[System.Serializable]
public class MechanicEnemiesConfig
{
    public List<MechanicEnemyEntry> mechanic_enemies;
    public CombinedMechanicsConfig combined_mechanics;
    public MechanicDifficultyScaling difficulty_scaling;
}

[System.Serializable]
public class MechanicEnemyEntry
{
    public string id;
    public string name_cn;
    public string mechanic_type;              // MechanicType枚举字符串
    public string description;
    public List<string> phase_tips;
    public MechanicStatMultipliers base_stats;
    public Dictionary<string, object> mechanic_params;
    public int min_level;
    public int max_level;
}

[System.Serializable]
public class MechanicStatMultipliers
{
    public float health_multiplier = 1f;
    public float attack_multiplier = 1f;
    public float defense_multiplier = 1f;
    public float speed_multiplier = 1f;
}

[System.Serializable]
public class CombinedMechanicsConfig
{
    public string description;
    public int min_level_for_combined;
    public int max_mechanics_per_boss;
}

[System.Serializable]
public class MechanicDifficultyScaling
{
    public float stat_multiplier_per_level_above_10;
    public float mechanic_strength_per_level_above_10;
}

// ========== 骰子面效果配置 ==========

[System.Serializable]
public class FaceEffectsConfig
{
    public List<FaceEffectDef> effects;
    public FaceEffectUpgradeConfig upgrade_config;
}

[System.Serializable]
public class FaceEffectUpgradeConfig
{
    public int max_upgrades_per_run;
    public List<int> cost_free_levels;
    public int rare_min_level;
    public int epic_min_level;
}
```

---

## 6. 测试用例

### 6.1 BE-08 测试

| 编号 | 测试场景 | 预期结果 |
|------|----------|----------|
| T08-01 | 第11关出现护盾守卫 | Boss使用ShieldSwap机制，每3回合互换护盾 |
| T08-02 | 第12关出现反伤傀儡 | 攻击Boss时反弹30%伤害给攻击者 |
| T08-03 | 第13关出现分裂母体 | Boss死亡后分裂为2个50%属性副本 |
| T08-04 | 第14关出现狂暴巨兽 | 血量每降25%攻击+50%，HUD显示阶段变化 |
| T08-05 | 第15关出现召唤领主 | 每2回合召唤小怪，max_minions=3 |
| T08-06 | Boss阶段切换 | 66%→阶段2, 33%→阶段3，HUD显示横幅 |
| T08-07 | 定时炸弹超时 | 5回合后全体受到80%生命伤害 |
| T08-08 | 隐身刺客攻击 | 每2回合隐身，下次攻击打最高攻击单位 |
| T08-09 | 诅咒法师诅咒扩散 | 攻击附加诅咒，每回合掉血3% |
| T08-10 | 元素使者切换 | 每回合切换免疫职业，被免疫职业攻击0伤害 |
| T08-11 | 16+关组合机制 | Boss拥有2种机制同时生效 |
| T08-12 | 非Boss关 | MechanicSystem不注册任何机制 |
| T08-13 | SimulateBattle跳过 | 机制在模拟战斗中仍然生效 |

### 6.2 BE-09 测试

| 编号 | 测试场景 | 预期结果 |
|------|----------|----------|
| T09-01 | 面1有治愈之光 | 投出1时，治疗血量最低友方15%生命 |
| T09-02 | 面2有守护之盾 | 投出2时，全体获得20点护盾 |
| T09-03 | 面3有精准打击 | 投出3时，对攻击最高敌人造成10%额外伤害 |
| T09-04 | 面4有疾风之力 | 投出4时，全体攻速+15% |
| T09-05 | 面5有雷击 | 投出5时，随机敌人眩晕1回合 |
| T09-06 | 面6有锐利之眼 | 战斗开始时暴击率+10%（OnBattleStart） |
| T09-07 | 升级面效果 | 同效果升级：Lv1→Lv2，baseValue+growthPerLevel |
| T09-08 | 替换面效果 | 不同效果替换：旧效果清除，新效果Lv1 |
| T09-09 | 满级效果 | Lv3效果无法再升级，GetAvailableUpgrades不返回 |
| T09-10 | 空面无效果 | FaceEffects[i]="" 时跳过，不执行任何效果 |
| T09-11 | 净化 vs 诅咒 | Cleanse效果移除CurseSpread的诅咒标记 |
| T09-12 | 吸血光环 | LifeSteal OnBattleStart → 全体获得10%吸血遗物Buff |

---

## 7. 工时估算

### BE-08 机制怪系统

| 任务 | 工时 | 说明 |
|------|------|------|
| MechanicEnemySystem.cs 核心实现 | 3天 | 9种机制处理器 + 阶段控制 |
| mechanic_enemies.json 配置 | 0.5天 | 9个机制怪配置 |
| BattleManager集成 | 0.5天 | 注册/卸载/事件订阅 |
| AutoChessAI改造 | 0.5天 | IsBoss判断 + 回调 |
| BossMechanicHUD联动 | 0.5天 | 事件订阅改造 |
| LevelManager改造 | 0.5天 | Boss标记 + 机制怪生成 |
| BalanceProvider/ConfigLoader扩展 | 0.5天 | 新配置加载 |
| 联调测试 | 1天 | 13个测试用例 |
| **合计** | **7天** | |

### BE-09 骰子面效果执行器

| 任务 | 工时 | 说明 |
|------|------|------|
| FaceEffectExecutor.cs 核心实现 | 2天 | 10种效果执行器 + 升级逻辑 |
| face_effects.json 配置 | 0.5天 | 10个效果配置 |
| RoguelikeGameManager集成 | 0.5天 | 骰子阶段/战斗阶段接入 |
| Hero眩晕支持 | 0.5天 | IsStunned + BattleLoop检查 |
| BalanceProvider/ConfigLoader扩展 | 0.5天 | 新配置加载 |
| 联调测试 | 1天 | 12个测试用例 |
| **合计** | **5天** | |

### 总计: 12人天

**建议**: BE-08 和 BE-09 可并行开发，无硬依赖关系。集成联调需串行（BE-09 的 Cleanse 效果依赖 BE-08 的 CurseSpread）。

---

## 附录A: MechanicEnemySystem 公共接口速查

```csharp
// 初始化（BattleManager.StartBattle 调用）
void RegisterBossMechanics(List<Hero> enemies, int levelId)

// 战斗Tick（BattleManager.BattleLoop 每tick调用）
void OnBattleTick(List<Hero> playerUnits, List<Hero> enemyUnits)

// 行动拦截（BattleManager.BattleLoop 敌方遍历时调用）
bool OverrideEnemyAction(Hero enemy, List<Hero> enemies, List<Hero> allies)

// 伤害后处理（Hero.TakeDamage 改造后调用）
void OnBossDamaged(Hero boss, int damage, Hero attacker)

// 攻击后处理（AutoChessAI.NormalAttack 改造后调用）
void OnBossAttacked(Hero boss, int damageDealt)

// UI查询（BossMechanicHUD 调用）
string GetMechanicTip(Hero boss)

// 清理
void ClearBattleState()

// 诅咒移除（FaceEffectExecutor.Cleanse调用）
void RemoveCurseFromHero(Hero hero)

// 事件
event Action<Hero, MechanicType, string> OnMechanicTriggered
event Action<Hero, int, string> OnBossPhaseChanged
event Action<Hero, string> OnMechanicWarning
event Action<List<Hero>> OnMinionsSpawned
event Action<Hero, int> OnBombExploded
```

## 附录B: FaceEffectExecutor 公共接口速查

```csharp
// 骰子投掷后触发（RoguelikeGameManager/DiceRoller事件调用）
void ProcessDiceResults(Dice[] dices, int[] values, 
    List<Hero> playerHeroes, List<Hero> enemyHeroes)

// 战斗开始激活（RoguelikeGameManager.StartBattle调用）
void ActivateBattleStartEffects(Dice[] dices, int[] lastValues,
    List<Hero> playerHeroes, List<Hero> enemyHeroes)

// 每回合触发（BattleManager.BattleLoop调用）
void ProcessPerTurnEffects(List<Hero> playerHeroes, List<Hero> enemyHeroes)

// 攻击时触发（AutoChessAI.NormalAttack调用）
void ProcessOnAttackEffects(Hero attacker, Hero target,
    List<Hero> playerHeroes, List<Hero> enemyHeroes)

// 升级面效果（RoguelikeRewardSystem调用）
int UpgradeFaceEffect(int diceIndex, int faceIndex, string effectId, Dice targetDice)

// 获取可用升级选项（RoguelikeRewardSystem调用）
List<FaceUpgradeOption> GetAvailableUpgrades(Dice[] dices, int levelId)

// 清理
void ClearBattleEffects()
void ClearAll()

// 事件
event Action<FaceEffectDef, int, string> OnFaceEffectTriggered
event Action<int, FaceEffectDef, int> OnFaceEffectUpgraded
```
