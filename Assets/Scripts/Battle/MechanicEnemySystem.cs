using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 机制类型枚举 — 定义机制怪的特殊能力
/// 配置于 mechanic_enemies.json
/// </summary>
public enum MechanicType
{
    None = 0,
    ShieldSwap = 1,       // 护盾互换
    DamageReflect = 2,    // 伤害反弹
    HealOnAttack = 3,     // 攻击回血
    SpawnMinions = 4,     // 召唤小怪
    Berserk = 5,          // 狂暴
    TimeBomb = 6,         // 定时炸弹
    StealthAssassin = 7,  // 隐身刺杀
    CurseSpread = 8,      // 诅咒扩散
    SplitOnDeath = 9,     // 分裂
    ElementalShift = 10   // 元素切换
}

/// <summary>
/// 机制怪运行时状态 — 挂载到Boss Hero上
/// </summary>
public class MechanicEnemyState
{
    public MechanicType mechanicType;
    public float mechanicStrength;
    public int mechanicTurnCounter;
    public int currentPhase = 1;
    public bool isActive = true;

    // 机制专属字段
    public int reflectPercent;
    public int spawnInterval;
    public int bombTimer;
    public int stealthCooldown;
    public bool isStealthed;
    public int curseDamagePerTurn;
    public string immuneElement;
    public bool hasSplit;
    public int berserkStacks;

    // 运行时引用（不序列化）
    public Hero owner;
    public List<int> cursedTargetInstanceIds = new List<int>();
    public MechanicEnemyEntry configEntry;
}

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
    public event System.Action<Hero, MechanicType, string> OnMechanicTriggered;
    public event System.Action<Hero, int, string> OnBossPhaseChanged;
    public event System.Action<Hero, string> OnMechanicWarning;
    public event System.Action<List<Hero>> OnMinionsSpawned;
    public event System.Action<Hero, int> OnBombExploded;

    // ========== 状态 ==========
    private Dictionary<int, MechanicEnemyState> _bossStates = new Dictionary<int, MechanicEnemyState>();
    private int _currentTurn = 0;
    private List<MechanicEnemyEntry> _config;

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
    public void RegisterBossMechanics(List<Hero> enemies, int levelId)
    {
        _bossStates.Clear();
        _currentTurn = 0;

        if (_config == null || _config.Count == 0) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsBoss) continue;

            var mechanicData = GetMechanicDataForLevel(levelId);
            if (mechanicData == null) continue;

            var state = CreateState(enemy, mechanicData);
            _bossStates[enemy.GetInstanceID()] = state;

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
                if (state != null && state.owner != null && state.owner.IsDead)
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

        switch (state.mechanicType)
        {
            case MechanicType.StealthAssassin:
                return HandleStealthAction(state, enemies, allies);

            case MechanicType.ElementalShift:
                HandleElementalShift(state);
                return false; // 行动仍然由AI执行

            case MechanicType.HealOnAttack:
                // 普通攻击但带吸血，不拦截
                return false;

            default:
                return false;
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
    /// 攻击后处理 — 用于HealOnAttack
    /// 由 AutoChessAI.NormalAttack 改造后调用
    /// </summary>
    public void OnBossAttacked(Hero boss, int damageDealt)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state))
            return;

        if (state.mechanicType == MechanicType.HealOnAttack)
        {
            float healPct = GetMechanicParamFloat(state, "heal_pct_per_phase", 0.2f);
            int phaseIndex = Mathf.Min(state.currentPhase - 1, 2);
            float[] pcts = GetMechanicParamFloatArray(state, "heal_pct_per_phase");
            if (pcts != null && phaseIndex < pcts.Length)
                healPct = pcts[phaseIndex];

            int heal = Mathf.RoundToInt(damageDealt * healPct);
            boss.Heal(heal);
            Debug.Log($"[MechanicEnemy] {boss.Data.heroName} 攻击回血 {heal}");
        }
    }

    /// <summary>
    /// 获取指定Boss的当前机制提示文本（供BossMechanicHUD使用）
    /// </summary>
    public string GetMechanicTip(Hero boss)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state))
            return null;

        if (state.configEntry?.phase_tips == null) return null;

        int tipIndex = Mathf.Min(state.currentPhase - 1, state.configEntry.phase_tips.Count - 1);
        return state.configEntry.phase_tips[tipIndex];
    }

    /// <summary>
    /// 清理战斗状态
    /// </summary>
    public void ClearBattleState()
    {
        _bossStates.Clear();
        _currentTurn = 0;
    }

    /// <summary>
    /// 移除指定英雄的诅咒（供 FaceEffectExecutor.Cleanse 调用）
    /// </summary>
    public void RemoveCurseFromHero(Hero hero)
    {
        if (hero == null) return;
        int instanceId = hero.GetInstanceID();

        foreach (var state in _bossStates.Values)
        {
            if (state.cursedTargetInstanceIds.Contains(instanceId))
            {
                state.cursedTargetInstanceIds.Remove(instanceId);
                Debug.Log($"[MechanicEnemy] 移除 {hero.Data.heroName} 的诅咒");
            }
        }
    }

    // ========== 私有方法 — 配置查找 ==========

    private MechanicEnemyEntry GetMechanicDataForLevel(int levelId)
    {
        if (_config == null) return null;

        // 按min_level筛选，优先匹配最接近的
        MechanicEnemyEntry best = null;
        foreach (var entry in _config)
        {
            if (levelId >= entry.min_level)
            {
                if (best == null || entry.min_level > best.min_level)
                    best = entry;
            }
        }
        return best;
    }

    private MechanicEnemyState CreateState(Hero boss, MechanicEnemyEntry data)
    {
        var state = new MechanicEnemyState
        {
            owner = boss,
            mechanicType = ParseMechanicType(data.mechanic_type),
            mechanicStrength = 1f,
            mechanicTurnCounter = 0,
            currentPhase = 1,
            isActive = true,
            configEntry = data,
            hasSplit = false,
            isStealthed = false,
            berserkStacks = 0
        };

        // 初始化机制专属参数
        if (state.mechanicType == MechanicType.TimeBomb)
        {
            int[] timers = GetMechanicParamIntArray(data, "bomb_timer_turns_per_phase");
            state.bombTimer = (timers != null && timers.Length > 0) ? timers[0] : 8;
        }

        return state;
    }

    private MechanicType ParseMechanicType(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr)) return MechanicType.None;
        return System.Enum.TryParse<MechanicType>(typeStr, out var result) ? result : MechanicType.None;
    }

    private void ApplyStatMultipliers(Hero boss, MechanicEnemyEntry data)
    {
        // 从configEntry的base_stats读取倍率
        // MechanicEnemyEntry继承自enemies.json的格式，base_stats是EnemyStatsEntry
        // 但mechanic_enemies.json中的base_stats包含multiplier字段
        // 使用Newtonsoft的mechanic_params解析
        var stats = data.mechanic_params;
        if (stats == null) return;

        float hpMult = GetParamFloat(stats, "health_multiplier", 1f);
        float atkMult = GetParamFloat(stats, "attack_multiplier", 1f);
        float defMult = GetParamFloat(stats, "defense_multiplier", 1f);

        // 如果mechanic_params中没有multiplier，跳过
        if (hpMult == 1f && atkMult == 1f && defMult == 1f) return;

        boss.MaxHealth = Mathf.RoundToInt(boss.MaxHealth * hpMult);
        boss.BoostMaxHealth(hpMult - 1f);
        boss.BoostAttack(atkMult - 1f);
        boss.BoostDefense(defMult - 1f);
    }

    // ========== 阶段控制 ==========

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

            string tip = null;
            if (state.configEntry?.phase_tips != null && newPhase - 1 < state.configEntry.phase_tips.Count)
                tip = state.configEntry.phase_tips[newPhase - 1];

            OnBossPhaseChanged?.Invoke(state.owner, newPhase, tip ?? $"Boss进入阶段{newPhase}");
            Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 阶段转换: {oldPhase}→{newPhase}");
        }
    }

    // ========== 机制处理器 ==========

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
                // 在OverrideEnemyAction中处理
                break;
        }
    }

    // --- ShieldSwap ---
    private void ProcessShieldSwap(MechanicEnemyState state, List<Hero> allies)
    {
        int interval = GetMechanicParamInt(state, "swap_interval_turns", 3);
        if (state.mechanicTurnCounter % interval != 0) return;

        Hero lowestAlly = FindLowestHealthAlly(state.owner, allies);
        if (lowestAlly == null || lowestAlly == state.owner) return;

        // 互换护盾（简化：互换当前生命值百分比差）
        float bossHpPct = (float)state.owner.CurrentHealth / state.owner.MaxHealth;
        float allyHpPct = (float)lowestAlly.CurrentHealth / lowestAlly.MaxHealth;

        // 给Boss加护盾
        float[] shieldPcts = GetMechanicParamFloatArray(state, "shield_pct_per_phase");
        if (shieldPcts != null && state.currentPhase - 1 < shieldPcts.Length)
        {
            int shield = Mathf.RoundToInt(state.owner.MaxHealth * shieldPcts[state.currentPhase - 1]);
            state.owner.AddShield(shield);
        }

        OnMechanicTriggered?.Invoke(state.owner, MechanicType.ShieldSwap, "护盾互换！");
        Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 执行护盾互换");
    }

    // --- DamageReflect ---
    private void HandleReflect(MechanicEnemyState state, int damage, Hero attacker)
    {
        if (attacker == null || attacker.IsDead) return;

        float[] reflectPcts = GetMechanicParamFloatArray(state, "reflect_pct_per_phase");
        float reflectPct = 0.2f;
        if (reflectPcts != null && state.currentPhase - 1 < reflectPcts.Length)
            reflectPct = reflectPcts[state.currentPhase - 1];

        int minDmg = GetMechanicParamInt(state, "min_damage_to_reflect", 5);
        int reflectDmg = Mathf.Max(minDmg, Mathf.RoundToInt(damage * reflectPct));

        attacker.TakeDamage(reflectDmg);
        OnMechanicTriggered?.Invoke(state.owner, MechanicType.DamageReflect, $"反弹{reflectDmg}伤害！");
        Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 反弹 {reflectDmg} 伤害给 {attacker.Data.heroName}");
    }

    // --- Berserk ---
    private void HandleBerserk(MechanicEnemyState state)
    {
        float bonusPct = GetMechanicParamFloat(state, "atk_bonus_per_threshold", 0.5f);
        int maxStacks = GetMechanicParamInt(state, "max_stacks", 3);

        int expectedStacks = state.currentPhase - 1;
        if (expectedStacks > state.berserkStacks && state.berserkStacks < maxStacks)
        {
            state.berserkStacks = expectedStacks;
            int atkBonus = Mathf.RoundToInt(state.owner.BattleAttack * bonusPct);
            state.owner.BattleAttack += atkBonus;

            OnMechanicTriggered?.Invoke(state.owner, MechanicType.Berserk, $"狂暴！攻击+{atkBonus}");
            Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 狂暴叠层 {state.berserkStacks}，攻击+{atkBonus}");
        }
    }

    // --- SpawnMinions ---
    private void ProcessSpawnMinions(MechanicEnemyState state, List<Hero> enemyUnits)
    {
        int[] intervals = GetMechanicParamIntArray(state, "spawn_interval_per_phase");
        int interval = 2;
        if (intervals != null && state.currentPhase - 1 < intervals.Length)
            interval = intervals[state.currentPhase - 1];

        if (state.mechanicTurnCounter % interval != 0) return;

        int maxMinions = GetMechanicParamInt(state, "max_minions", 3);
        int currentMinions = enemyUnits.Count(e => e != null && !e.IsBoss && !e.IsDead);

        if (currentMinions >= maxMinions) return;

        // 创建小怪（简化：克隆一个小怪模板）
        float minionPct = GetMechanicParamFloat(state, "minion_stat_pct", 0.4f);
        string template = GetMechanicParamString(state, "minion_template", "小怪");

        var minion = CreateMinion(template, minionPct);
        if (minion != null)
        {
            enemyUnits.Add(minion);
            var spawned = new List<Hero> { minion };
            OnMinionsSpawned?.Invoke(spawned);
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.SpawnMinions, "召唤了一个小怪！");
            Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 召唤小怪");
        }
    }

    // --- TimeBomb ---
    private void ProcessTimeBomb(MechanicEnemyState state, List<Hero> playerUnits)
    {
        state.bombTimer--;

        int warningAt = GetMechanicParamInt(state, "show_warning_at_turns_remaining", 3);
        if (state.bombTimer <= warningAt && state.bombTimer > 0)
        {
            OnMechanicWarning?.Invoke(state.owner, $"⚠ 炸弹倒计时 {state.bombTimer} 回合！");
        }

        if (state.bombTimer <= 0)
        {
            // 爆炸！
            float dmgPct = GetMechanicParamFloat(state, "bomb_damage_pct", 0.8f);
            int totalDmg = 0;

            foreach (var hero in playerUnits)
            {
                if (hero == null || hero.IsDead) continue;
                int dmg = Mathf.RoundToInt(hero.MaxHealth * dmgPct);
                hero.TakeDamage(dmg);
                totalDmg += dmg;
            }

            OnBombExploded?.Invoke(state.owner, totalDmg);
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.TimeBomb, $"💣 炸弹爆炸！全体{totalDmg}伤害");

            // 重置计时器（下一阶段）
            int[] timers = GetMechanicParamIntArray(state, "bomb_timer_turns_per_phase");
            if (timers != null && state.currentPhase - 1 < timers.Length)
                state.bombTimer = timers[state.currentPhase - 1];
            else
                state.bombTimer = 8;

            Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 炸弹爆炸！造成{totalDmg}伤害");
        }
    }

    // --- StealthAssassin ---
    private bool HandleStealthAction(MechanicEnemyState state, List<Hero> enemies, List<Hero> allies)
    {
        int[] intervals = GetMechanicParamIntArray(state, "stealth_interval_per_phase");
        int interval = 2;
        if (intervals != null && state.currentPhase - 1 < intervals.Length)
            interval = intervals[state.currentPhase - 1];

        if (state.mechanicTurnCounter % interval == 0)
        {
            // 隐身
            state.isStealthed = true;
            OnMechanicTriggered?.Invoke(state.owner, MechanicType.StealthAssassin, "隐身了！");
            Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 隐身");
            return true; // 本回合跳过行动
        }

        if (state.isStealthed)
        {
            // 解除隐身，攻击最高攻击单位
            state.isStealthed = false;

            float[] dmgMults = GetMechanicParamFloatArray(state, "stealth_damage_multiplier_per_phase");
            float dmgMult = 1f;
            if (dmgMults != null && state.currentPhase - 1 < dmgMults.Length)
                dmgMult = dmgMults[state.currentPhase - 1];

            Hero target = FindHighestAttackEnemy(enemies);
            if (target != null)
            {
                int dmg = Mathf.RoundToInt(state.owner.BattleAttack * dmgMult);
                target.TakeDamage(dmg, state.owner);
                OnMechanicTriggered?.Invoke(state.owner, MechanicType.StealthAssassin,
                    $"隐身刺杀！对{target.Data.heroName}造成{dmg}伤害");
                Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 隐身刺杀 {target.Data.heroName} {dmg}伤害");
            }
            return true; // 已处理行动
        }

        return false; // 正常行动
    }

    // --- CurseSpread ---
    private void ProcessCurseSpread(MechanicEnemyState state, List<Hero> playerUnits)
    {
        // 诅咒每回合造成伤害
        float[] dmgPcts = GetMechanicParamFloatArray(state, "curse_damage_pct_per_phase");
        float dmgPct = 0.03f;
        if (dmgPcts != null && state.currentPhase - 1 < dmgPcts.Length)
            dmgPct = dmgPcts[state.currentPhase - 1];

        foreach (int targetId in state.cursedTargetInstanceIds.ToList())
        {
            Hero cursed = FindHeroByInstanceId(playerUnits, targetId);
            if (cursed == null || cursed.IsDead)
            {
                state.cursedTargetInstanceIds.Remove(targetId);
                continue;
            }

            int dmg = Mathf.RoundToInt(cursed.MaxHealth * dmgPct);
            cursed.TakeDamage(dmg);
        }
    }

    /// <summary>
    /// Boss攻击时附加诅咒（由 AutoChessAI 改造后调用）
    /// </summary>
    public void OnBossAttackApplyCurse(Hero boss, Hero target)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state)) return;
        if (state.mechanicType != MechanicType.CurseSpread) return;
        if (target == null || target.IsDead) return;

        int targetId = target.GetInstanceID();
        if (!state.cursedTargetInstanceIds.Contains(targetId))
        {
            state.cursedTargetInstanceIds.Add(targetId);
            OnMechanicTriggered?.Invoke(boss, MechanicType.CurseSpread, $"诅咒了{target.Data.heroName}！");
        }
    }

    // --- ElementalShift ---
    private void HandleElementalShift(MechanicEnemyState state)
    {
        string[] cycle = GetMechanicParamStringArray(state, "immune_cycle");
        if (cycle == null || cycle.Length == 0) return;

        // 每回合切换免疫
        int index = _currentTurn % cycle.Length;
        state.immuneElement = cycle[index];
    }

    /// <summary>
    /// 检查Boss是否免疫某职业类型（由伤害计算前调用）
    /// </summary>
    public bool IsImmuneToClass(Hero boss, HeroClass heroClass)
    {
        if (!_bossStates.TryGetValue(boss.GetInstanceID(), out var state))
            return false;

        if (state.mechanicType != MechanicType.ElementalShift)
            return false;

        if (string.IsNullOrEmpty(state.immuneElement))
            return false;

        // 阶段3：全体免疫1回合
        if (state.currentPhase >= 3)
        {
            int immuneAllDuration = GetMechanicParamInt(state, "phase_3_immune_all_duration", 1);
            if (_currentTurn % (immuneAllDuration + 1) == 0)
                return true;
        }

        return state.immuneElement.Equals(heroClass.ToString(), System.StringComparison.OrdinalIgnoreCase);
    }

    // --- SplitOnDeath ---
    private void HandleBossDeath(MechanicEnemyState state, List<Hero> enemyUnits)
    {
        if (state.mechanicType != MechanicType.SplitOnDeath) return;
        if (state.hasSplit) return;

        state.hasSplit = true;

        int splitCount = GetMechanicParamInt(state, "split_count", 2);
        float splitPct = GetMechanicParamFloat(state, "split_stat_pct", 0.5f);
        bool canSplitAgain = GetMechanicParamBool(state, "split_can_split_again", false);

        for (int i = 0; i < splitCount; i++)
        {
            var split = CreateMinion(state.owner.Data.heroName + "_分裂体", splitPct);
            if (split != null)
            {
                split.IsBoss = false;
                enemyUnits.Add(split);
            }
        }

        OnMinionsSpawned?.Invoke(enemyUnits.FindAll(e => e != null && !e.IsDead && !e.IsBoss));
        OnMechanicTriggered?.Invoke(state.owner, MechanicType.SplitOnDeath, "分裂了！");
        Debug.Log($"[MechanicEnemy] {state.owner.Data.heroName} 死亡分裂为{splitCount}个副本");
    }

    // ========== 工具方法 ==========

    private Hero CreateMinion(string templateName, float statPct)
    {
        Debug.Log($"[MechanicEnemy] 创建小怪: {templateName} ({statPct * 100}%属性)");

        // 从GameBalance获取模板数据
        var template = GameBalance.GetHeroTemplate(templateName);
        if (template == null)
        {
            Debug.LogWarning($"[MechanicEnemy] 找不到模板: {templateName}，使用默认小怪模板");
            template = GameBalance.GetHeroTemplate("小怪");
        }
        if (template == null)
        {
            Debug.LogError($"[MechanicEnemy] 默认小怪模板也不存在，无法创建");
            return null;
        }

        // 创建GameObject + Hero组件
        var go = new GameObject($"Minion_{templateName}");
        go.transform.SetParent(transform);
        var hero = go.AddComponent<Hero>();

        // 构建HeroData
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = templateName;
        data.heroClass = template.HeroClass;
        data.baseHealth = Mathf.RoundToInt(template.Health * statPct);
        data.baseAttack = Mathf.RoundToInt(template.Attack * statPct);
        data.baseDefense = Mathf.RoundToInt(template.Defense * statPct);
        data.baseSpeed = Mathf.RoundToInt(template.Speed * statPct);
        data.baseCritRate = template.CritRate * statPct;
        data.summonCost = template.SummonCost;

        hero.Initialize(data, starLevel: 1);
        Debug.Log($"[MechanicEnemy] 小怪创建成功: {templateName} HP={hero.MaxHealth} ATK={hero.Attack}");
        return hero;
    }

    private Hero FindLowestHealthAlly(Hero exclude, List<Hero> allies)
    {
        Hero lowest = null;
        float lowestPct = 1f;
        foreach (var h in allies)
        {
            if (h == null || h.IsDead || h == exclude) continue;
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

    private Hero FindHeroByInstanceId(List<Hero> heroes, int instanceId)
    {
        foreach (var h in heroes)
        {
            if (h != null && h.GetInstanceID() == instanceId)
                return h;
        }
        return null;
    }

    // ========== 配置参数读取工具 ==========

    private int GetMechanicParamInt(MechanicEnemyState state, string key, int fallback)
    {
        return GetMechanicParamInt(state.configEntry, key, fallback);
    }

    private int GetMechanicParamInt(MechanicEnemyEntry entry, string key, int fallback)
    {
        if (entry?.mechanic_params == null) return fallback;
        return GetParamInt(entry.mechanic_params, key, fallback);
    }

    private float GetMechanicParamFloat(MechanicEnemyState state, string key, float fallback)
    {
        if (state.configEntry?.mechanic_params == null) return fallback;
        return GetParamFloat(state.configEntry.mechanic_params, key, fallback);
    }

    private int[] GetMechanicParamIntArray(MechanicEnemyState state, string key)
    {
        if (state.configEntry?.mechanic_params == null) return null;
        return GetParamIntArray(state.configEntry.mechanic_params, key);
    }

    private float[] GetMechanicParamFloatArray(MechanicEnemyState state, string key)
    {
        if (state.configEntry?.mechanic_params == null) return null;
        return GetParamFloatArray(state.configEntry.mechanic_params, key);
    }

    private string GetMechanicParamString(MechanicEnemyState state, string key, string fallback)
    {
        if (state.configEntry?.mechanic_params == null) return fallback;
        return GetParamString(state.configEntry.mechanic_params, key, fallback);
    }

    private string[] GetMechanicParamStringArray(MechanicEnemyState state, string key)
    {
        if (state.configEntry?.mechanic_params == null) return null;
        return GetParamStringArray(state.configEntry.mechanic_params, key);
    }

    private bool GetMechanicParamBool(MechanicEnemyState state, string key, bool fallback)
    {
        if (state.configEntry?.mechanic_params == null) return fallback;
        return GetParamBool(state.configEntry.mechanic_params, key, fallback);
    }

    // --- 通用 Dictionary&lt;string,object&gt; 参数读取 ---
    // 使用 Newtonsoft.Json.Linq 进行类型转换

    private static int GetParamInt(Dictionary<string, object> dict, string key, int fallback)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is double d) return (int)d;
            if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
        }
        return fallback;
    }

    private static float GetParamFloat(Dictionary<string, object> dict, string key, float fallback)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is float f) return f;
            if (val is double d) return (float)d;
            if (val is int i) return (float)i;
            if (float.TryParse(val?.ToString(), out float parsed)) return parsed;
        }
        return fallback;
    }

    private static string GetParamString(Dictionary<string, object> dict, string key, string fallback)
    {
        if (dict.TryGetValue(key, out object val) && val != null)
            return val.ToString();
        return fallback;
    }

    private static bool GetParamBool(Dictionary<string, object> dict, string key, bool fallback)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is bool b) return b;
            if (bool.TryParse(val?.ToString(), out bool parsed)) return parsed;
        }
        return fallback;
    }

    private static int[] GetParamIntArray(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is Newtonsoft.Json.Linq.JArray arr)
            {
                var result = new int[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                    result[i] = arr[i].Value<int>();
                return result;
            }
        }
        return null;
    }

    private static float[] GetParamFloatArray(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is Newtonsoft.Json.Linq.JArray arr)
            {
                var result = new float[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                    result[i] = arr[i].Value<float>();
                return result;
            }
        }
        return null;
    }

    private static string[] GetParamStringArray(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object val))
        {
            if (val is Newtonsoft.Json.Linq.JArray arr)
            {
                var result = new string[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                    result[i] = arr[i].Value<string>();
                return result;
            }
        }
        return null;
    }
}
