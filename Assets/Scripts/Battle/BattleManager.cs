using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 战斗管理器 — 管理自走棋自动战斗流程
/// 改造：加入骰子技能释放、加速(2x/4x)、跳过战斗
/// v2: 硬编码系数全部改为从 BalanceProvider (JSON配置) 读取，保留 fallback
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("战斗配置")]
    [Tooltip("战斗Tick间隔，实际值从 battle_formulas.json 读取")]
    public float battleTickInterval = 0.5f; // Inspector默认值，运行时由配置覆盖
    public float battleSpeed = 1f;
    [Tooltip("最大战斗时间，实际值从 battle_formulas.json 读取")]
    public float maxBattleTime = 60f; // Inspector默认值，运行时由配置覆盖

    [Header("单位")]
    public List<Hero> playerUnits = new List<Hero>();
    public List<Hero> enemyUnits = new List<Hero>();

    public bool IsBattleActive { get; private set; }
    public float BattleTimer { get; private set; }
    public bool PlayerWon { get; private set; }

    // 骰子组合（战斗开始时由外部设置）
    public DiceCombination CurrentDiceCombo { get; private set; }
    public bool DiceSkillUsed { get; private set; } // 骰子技能是否已释放
    public int[] LastDiceValues { get; private set; } // 投掷的原始骰子值（供UI显示）

    // 战斗速度档位（从 battle_formulas.json 的 speed_options 读取）
    public static float SPEED_1X = 1f;
    public static float SPEED_2X = 2f;
    public static float SPEED_4X = 4f;

    // 速度变化事件
    public event System.Action<float> OnBattleSpeedChanged;
    public event System.Action OnBattleStarted;
    public event System.Action<bool> OnBattleEnded; // true=胜利
    public event System.Action<string> OnDiceSkillTriggered;

    // 战斗统计精细事件（BattleStatsTracker订阅）
    public event System.Action<Hero, Hero> OnUnitKilled;       // (killer, victim)
    public event System.Action<Hero, Hero, int> OnDamageDealt; // (attacker, target, damage)
    public event System.Action<Hero, Hero, int> OnHealDone;    // (healer, target, healAmount)
    public event System.Action<Hero, int> OnShieldGained;      // (hero, shieldAmount)

    // BE-17: 回合结束事件（每tick触发，用于快照采集）
    public event System.Action<int> OnTurnEnded;               // (turnIndex)

    private Coroutine battleCoroutine;

    // 战斗音效桥接
    private AudioBattleBridge audioBridge;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化纯C#单例（非MonoBehaviour，需手动创建）
        if (MechanicEnemySystem.Instance == null)
            new MechanicEnemySystem();
        if (FaceEffectExecutor.Instance == null)
            new FaceEffectExecutor();

        // 初始化战斗特效系统
        if (BattleEffectManager.Instance == null)
        {
            var effectGo = new GameObject("[BattleEffectManager]");
            effectGo.transform.SetParent(transform);
            effectGo.AddComponent<BattleEffectManager>();
        }
        BattleEffectManager.Instance?.Initialize();
    }

    /// <summary>
    /// 从配置加载战斗参数（场景初始化时调用）
    /// </summary>
    public void LoadBattleConfig()
    {
        // 从 JSON 配置覆盖 Inspector 默认值
        battleTickInterval = BalanceProvider.GetBattleTickInterval();
        maxBattleTime = BalanceProvider.GetMaxBattleTime();

        // 加载速度档位
        var speedOpts = BalanceProvider.GetSpeedOptions();
        if (speedOpts != null && speedOpts.Count >= 3)
        {
            SPEED_1X = speedOpts[0];
            SPEED_2X = speedOpts[1];
            SPEED_4X = speedOpts[2];
        }

        Debug.Log($"[BattleManager] 配置加载完成: tick={battleTickInterval}s, maxTime={maxBattleTime}s, speeds=[{SPEED_1X},{SPEED_2X},{SPEED_4X}]");
    }

    /// <summary>
    /// 开始战斗（含骰子组合效果）
    /// </summary>
    public void StartBattle(List<Hero> players, List<Hero> enemies, DiceCombination diceCombo = null)
    {
        // 确保配置已加载
        LoadBattleConfig();

        playerUnits = new List<Hero>(players);
        enemyUnits = new List<Hero>(enemies);
        IsBattleActive = true;
        BattleTimer = 0f;
        PlayerWon = false;
        DiceSkillUsed = false;
        CurrentDiceCombo = diceCombo;

        // 从骰子组合推导原始值（供UI演出使用）
        if (diceCombo != null && diceCombo.Type != DiceCombinationType.None)
        {
            LastDiceValues = DeduceDiceValues(diceCombo);
        }
        else
        {
            LastDiceValues = null;
        }

        // 应用连携技Buff
        SynergySystem.ApplySynergies(playerUnits);

        // 注册机制怪（如果有Boss）
        if (MechanicEnemySystem.Instance != null)
        {
            int levelId = RoguelikeGameManager.Instance?.CurrentLevel ?? 0;
            MechanicEnemySystem.Instance.RegisterBossMechanics(enemyUnits, levelId);
        }

        // 面效果：激活 OnBattleStart 类型
        if (FaceEffectExecutor.Instance != null && RoguelikeGameManager.Instance != null)
        {
            var diceRoller = RoguelikeGameManager.Instance.DiceRoller;
            if (diceRoller != null && diceRoller.Dices != null && diceRoller.Dices.Length > 0)
            {
                var lastVals = LastDiceValues ?? new int[] { 1, 2, 3 };
                FaceEffectExecutor.Instance.ActivateBattleStartEffects(
                    diceRoller.Dices, lastVals, playerUnits, enemyUnits);
            }
        }

        // 战斗开始时根据骰子组合触发一次性效果
        if (diceCombo != null)
        {
            ApplyDiceComboEffects(diceCombo);
        }

        Debug.Log($"战斗开始！我方{playerUnits.Count}人 vs 敌方{enemyUnits.Count}人 | 骰子: {(diceCombo != null ? diceCombo.Description : "无")}");

        // 成就系统：记录战斗开始状态
        var achMgr2 = AchievementManager.Instance;
        if (achMgr2 != null)
        {
            int aliveCount = players.Count(h => h != null && h.CurrentHealth > 0);
            achMgr2.TrackBattleStart(aliveCount);
        }

        OnBattleStarted?.Invoke();
        battleCoroutine = StartCoroutine(BattleLoop());

        // 初始化战斗音效桥接
        InitAudioBridge();

        // 通知特效系统战斗开始
        BattleEffectManager.Instance?.OnBattleStart();
    }

    /// <summary>
    /// 应用骰子组合效果到所有玩家单位
    /// 系数全部从 skills.json 的 dice_combo_skills 读取
    /// </summary>
    void ApplyDiceComboEffects(DiceCombination combo)
    {
        if (combo == null || combo.Type == DiceCombinationType.None) return;

        switch (combo.Type)
        {
            case DiceCombinationType.ThreeOfAKind:
            {
                // 从 skills.json 读取: 三连轰击 attack_bonus_pct=0.5
                var skillData = BalanceProvider.GetDiceComboSkill("three_of_a_kind");
                float attackBonus = skillData?.attack_bonus_pct ?? 0.2f; // JSON:0.5, fallback:0.2
                int duration = skillData?.duration_rounds ?? 3;

                // 三条：开场全屏AOE，对所有敌人造成攻击力30%的伤害
                int aoeDamageBase = 0;
                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    aoeDamageBase += hero.Attack;
                    // 攻击加成
                    hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + attackBonus));
                }
                // AOE伤害比例（三条开场30%，即从JSON的attack_bonus_pct取一半作为AOE比例）
                float aoeRatio = Mathf.Min(attackBonus, 0.3f);
                int aoeDamage = Mathf.RoundToInt(aoeDamageBase * aoeRatio);
                foreach (var enemy in enemyUnits)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    enemy.TakeDamage(Mathf.Max(1, aoeDamage - enemy.BattleDefense));
                }
                Debug.Log($"[骰子技能] 三条AOE！全屏造成 {aoeDamage} 伤害，攻击+{attackBonus*100}%");
                OnDiceSkillTriggered?.Invoke($"三条全屏AOE！造成{aoeDamage}伤害");
                break;
            }

            case DiceCombinationType.Straight:
            {
                // 从 skills.json 读取: 急速冲锋 attack_speed_bonus_pct=0.2, dodge_bonus_pct=0.15
                var skillData = BalanceProvider.GetDiceComboSkill("straight");
                float speedBonus = skillData?.attack_speed_bonus_pct ?? 0.2f;
                int duration = skillData?.duration_rounds ?? 2;

                // 顺子：全体攻速加成
                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    hero.BattleAttackSpeed = 1f + speedBonus;
                }
                Debug.Log($"[骰子技能] 顺子！全体攻速+{speedBonus*100}%");
                OnDiceSkillTriggered?.Invoke($"顺子！全体攻速+{speedBonus*100}%");
                break;
            }

            case DiceCombinationType.Pair:
            {
                // 从 skills.json 读取: 精准打击
                var skillData = BalanceProvider.GetDiceComboSkill("pair");
                // 对子：护盾（从 battle_formulas.json 的 front.shield_bonus_pct 取，fallback 0.15）
                float shieldPct = BalanceProvider.GetPositionModifier("front")?.shield_bonus_pct ?? 0.15f;
                // 暴击率加成：优先从 skills.json 的 crit_rate_bonus 读取，fallback 0.1
                float critBonus = skillData?.crit_rate_bonus > 0 ? skillData.crit_rate_bonus : 0.1f;

                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    int shield = Mathf.RoundToInt(hero.MaxHealth * shieldPct);
                    hero.AddShield(shield);
                    hero.BattleCritRate += critBonus;
                }
                Debug.Log($"[骰子技能] 对子！全体获得护盾({shieldPct*100}%生命)+暴击率+{critBonus*100}%");
                OnDiceSkillTriggered?.Invoke($"对子！全体护盾+暴击+{critBonus*100}%");
                break;
            }
        }
    }

    /// <summary>
    /// 玩家手动释放骰子技能（点触释放）
    /// 系数从 skills.json 读取
    /// </summary>
    public void TriggerDiceSkill()
    {
        if (!IsBattleActive || DiceSkillUsed || CurrentDiceCombo == null) return;
        if (CurrentDiceCombo.Type == DiceCombinationType.None) return;

        // 检查使用次数限制
        int usageLimit = BalanceProvider.GetDiceComboSkillUsageLimit();
        if (usageLimit <= 0) return;

        DiceSkillUsed = true;

        switch (CurrentDiceCombo.Type)
        {
            case DiceCombinationType.ThreeOfAKind:
            {
                // 手动释放：全屏高额AOE
                // 从 skills.json 读取 damage_multiplier
                var skillData = BalanceProvider.GetDiceComboSkill("three_of_a_kind");
                float dmgMult = skillData?.damage_multiplier > 0 ? skillData.damage_multiplier : 0.5f;

                int dmg = 0;
                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    dmg += hero.BattleAttack;
                }
                int totalDmg = Mathf.RoundToInt(dmg * dmgMult);
                foreach (var enemy in enemyUnits)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    enemy.TakeDamage(Mathf.Max(1, totalDmg - enemy.BattleDefense / 2));
                }
                Debug.Log($"[手动骰子技能] 三条大招！全屏 {totalDmg} 伤害 (倍率{dmgMult})");
                OnDiceSkillTriggered?.Invoke($"三条大招！{totalDmg}伤害");
                break;
            }

            case DiceCombinationType.Straight:
            {
                // 手动释放：全体攻击加成
                var skillData = BalanceProvider.GetDiceComboSkill("straight");
                // fallback: JSON无直接攻击加成字段，用 attack_speed_bonus_pct * 1.5 作为手动加成
                float atkBonus = skillData?.attack_speed_bonus_pct > 0
                    ? skillData.attack_speed_bonus_pct * 1.5f
                    : 0.3f;

                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + atkBonus));
                }
                Debug.Log($"[手动骰子技能] 顺子加速！全体攻击+{atkBonus*100}%");
                OnDiceSkillTriggered?.Invoke($"顺子！全体攻击+{atkBonus*100}%");
                break;
            }

            case DiceCombinationType.Pair:
            {
                // 手动释放：全体治疗
                var skillData = BalanceProvider.GetDiceComboSkill("pair");
                float healPct = skillData?.damage_multiplier > 0 ? skillData.damage_multiplier : 0.3f;

                foreach (var hero in playerUnits)
                {
                    if (hero == null || hero.IsDead) continue;
                    int heal = Mathf.RoundToInt(hero.MaxHealth * healPct);
                    hero.Heal(heal);
                    // 战斗统计：骰子技能治疗（healer=null表示系统治疗）
                    NotifyHealDone(null, hero, heal);
                }
                Debug.Log($"[手动骰子技能] 对子治疗！全体恢复{healPct*100}%生命");
                OnDiceSkillTriggered?.Invoke($"对子治疗！全体恢复{healPct*100}%HP");
                break;
            }
        }
    }

    IEnumerator BattleLoop()
    {
        while (IsBattleActive)
        {
            yield return new WaitForSeconds(battleTickInterval / battleSpeed);

            // 机制怪：每tick触发
            if (MechanicEnemySystem.Instance != null)
                MechanicEnemySystem.Instance.OnBattleTick(playerUnits, enemyUnits);

            // 面效果：每回合触发
            if (FaceEffectExecutor.Instance != null)
                FaceEffectExecutor.Instance.ProcessPerTurnEffects(playerUnits, enemyUnits);

            // 我方单位行动
            foreach (var unit in playerUnits)
            {
                if (unit == null || unit.IsDead) continue;
                if (unit.IsStunned) { unit.SetStunned(false); continue; } // 眩晕跳过一回合
                AutoChessAI.TakeAction(unit, enemyUnits, playerUnits);
            }

            // 敌方单位行动
            foreach (var unit in enemyUnits)
            {
                if (unit == null || unit.IsDead) continue;
                if (unit.IsStunned) { unit.SetStunned(false); continue; }
                // 机制怪行为覆盖
                if (MechanicEnemySystem.Instance != null &&
                    MechanicEnemySystem.Instance.OverrideEnemyAction(unit, enemyUnits, playerUnits))
                    continue;
                AutoChessAI.TakeAction(unit, playerUnits, enemyUnits);
            }

            // 清理死亡单位
            RemoveDeadUnits();

            // 检查战斗结束
            if (CheckBattleEnd())
            {
                IsBattleActive = false;
                break;
            }

            BattleTimer += battleTickInterval;

            // BE-17: 通知回合结束（用于快照采集）
            OnTurnEnded?.Invoke((int)(BattleTimer / battleTickInterval) - 1);
        }

        EndBattle();
    }

    bool CheckBattleEnd()
    {
        bool allPlayerDead = playerUnits.TrueForAll(u => u == null || u.IsDead);
        bool allEnemyDead = enemyUnits.TrueForAll(u => u == null || u.IsDead);

        if (allPlayerDead || allEnemyDead)
            return true;

        // 超时判定（从配置读取最大时间）
        if (BattleTimer >= maxBattleTime)
            return true;

        return false;
    }

    void RemoveDeadUnits()
    {
        // 先尝试复活：检查玩家方死亡单位是否有复活遗物可用
        if (RelicSystem.Instance != null)
        {
            for (int i = playerUnits.Count - 1; i >= 0; i--)
            {
                var hero = playerUnits[i];
                if (hero != null && hero.IsDead)
                {
                    // 尝试复活，成功则跳过移除
                    if (RelicSystem.Instance.TryRevive(hero))
                        continue;
                }
            }
        }

        // 成就系统：追踪敌人击杀数（计算本tick死亡敌方单位）
        int enemiesKilledThisTick = enemyUnits.Count(u => u != null && u.IsDead);
        if (enemiesKilledThisTick > 0)
        {
            var achMgr = AchievementManager.Instance;
            if (achMgr != null)
            {
                for (int i = 0; i < enemiesKilledThisTick; i++)
                    achMgr.TrackEnemyKill();
                // 立即推送累计击杀数到成就进度
                achMgr.TrackProgress("total_enemies_killed", achMgr.GetTotalEnemiesKilled());
            }

            // 战斗统计：触发 OnUnitKilled 事件（玩家击杀敌人）
            // 注：精确killer未知（自走棋），传null表示系统击杀
            foreach (var enemy in enemyUnits)
            {
                if (enemy != null && enemy.IsDead)
                {
                    OnUnitKilled?.Invoke(null, enemy);
                }
            }
        }

        // 移除确认死亡的单位（复活失败的）
        // 先为死亡单位播放死亡特效
        foreach (var u in playerUnits)
        {
            if (u != null && u.IsDead)
                BattleEffectManager.PlayDeath(u.transform.position);
        }
        foreach (var u in enemyUnits)
        {
            if (u != null && u.IsDead)
                BattleEffectManager.PlayDeath(u.transform.position);
        }

        playerUnits.RemoveAll(u => u == null || u.IsDead);
        enemyUnits.RemoveAll(u => u == null || u.IsDead);
    }

    void EndBattle()
    {
        bool allEnemyDead = enemyUnits.TrueForAll(u => u == null || u.IsDead);
        bool allPlayerDead = playerUnits.TrueForAll(u => u == null || u.IsDead);

        PlayerWon = allEnemyDead && !allPlayerDead;

        // 通知特效系统战斗结束
        BattleEffectManager.Instance?.OnBattleEnd();

        // 清理战斗音效桥接
        CleanupAudioBridge();

        // 清理机制怪战斗状态
        if (MechanicEnemySystem.Instance != null)
            MechanicEnemySystem.Instance.ClearBattleState();

        // 清理面效果战斗状态
        if (FaceEffectExecutor.Instance != null)
            FaceEffectExecutor.Instance.ClearBattleEffects();

        if (PlayerWon)
        {
            Debug.Log("战斗胜利！");

            // 成就系统：追踪战斗开始英雄数 → 翻盘检测
            var achMgr = AchievementManager.Instance;
            if (achMgr != null)
            {
                // Boss击杀判定：通过关卡配置判断
                var levelCfg = BalanceProvider.GetLevel(GameStateMachine.Instance?.CurrentLevel ?? 1);
                if (levelCfg != null && levelCfg.allow_boss)
                    achMgr.TrackBossKill();
            }
        }
        else if (allPlayerDead)
            Debug.Log("战斗失败...");
        else
            Debug.Log("战斗超时！");

        OnBattleEnded?.Invoke(PlayerWon);

        // 通知状态机
        if (PlayerWon)
            GameStateMachine.Instance?.SetGameWon();
        else
            GameStateMachine.Instance?.SetGameLost();

        GameStateMachine.Instance?.NextState();
    }

    /// <summary>
    /// 设置战斗速度（1x/2x/4x）
    /// </summary>
    public void SetBattleSpeed(float speed)
    {
        battleSpeed = Mathf.Clamp(speed, SPEED_1X, SPEED_4X);
        OnBattleSpeedChanged?.Invoke(battleSpeed);
    }

    /// <summary>
    /// 切换到下一档速度 (1x→2x→4x→1x)
    /// </summary>
    public float CycleBattleSpeed()
    {
        if (battleSpeed < SPEED_2X)
            SetBattleSpeed(SPEED_2X);
        else if (battleSpeed < SPEED_4X)
            SetBattleSpeed(SPEED_4X);
        else
            SetBattleSpeed(SPEED_1X);
        return battleSpeed;
    }

    /// <summary>
    /// 跳过战斗 — 直接模拟计算结果
    /// </summary>
    public void SkipBattle()
    {
        if (!IsBattleActive) return;

        // 停止当前战斗协程
        StopBattle();

        // 模拟剩余战斗
        var result = SimulateBattle();
        PlayerWon = result;

        if (PlayerWon)
            Debug.Log("跳过战斗 — 胜利！");
        else
            Debug.Log("跳过战斗 — 失败...");

        OnBattleEnded?.Invoke(PlayerWon);

        if (PlayerWon)
            GameStateMachine.Instance?.SetGameWon();
        else
            GameStateMachine.Instance?.SetGameLost();

        GameStateMachine.Instance?.NextState();
    }

    /// <summary>
    /// 英雄战斗状态快照 — 用于 SimulateBattle 的保存/恢复
    /// </summary>
    private struct HeroSnapshot
    {
        public Hero hero;
        public int currentHealth;
        public int battleAttack;
        public float battleAttackSpeed;
        public float battleCritRate;
        public int battleDefense;
        public int battleSpeed;
        public float battleDodgeRate;
        public float battleCritDamage;
        public float lifeStealRate;
        public float battleThornsRate;
        public bool isStunned;
        public bool hasArmorBreak;
        public int lightningChainBounces;
    }

    /// <summary>
    /// 快速模拟战斗（无动画，纯数值计算）
    /// maxRounds 从 battle_formulas.json 读取
    /// 使用快照保存/恢复机制，避免浅拷贝污染原始状态
    /// </summary>
    public bool SimulateBattle()
    {
        int maxRounds = BalanceProvider.GetSimulateBattleMaxRounds();
        var simPlayers = new List<Hero>(playerUnits);
        var simEnemies = new List<Hero>(enemyUnits);

        // === 保存所有单位状态快照 ===
        var allUnits = new List<Hero>(simPlayers);
        allUnits.AddRange(simEnemies);
        var snapshots = new List<HeroSnapshot>(allUnits.Count);
        foreach (var unit in allUnits)
        {
            if (unit == null) continue;
            snapshots.Add(new HeroSnapshot
            {
                hero = unit,
                currentHealth = unit.CurrentHealth,
                battleAttack = unit.BattleAttack,
                battleAttackSpeed = unit.BattleAttackSpeed,
                battleCritRate = unit.BattleCritRate,
                battleDefense = unit.BattleDefense,
                battleSpeed = unit.BattleSpeed,
                battleDodgeRate = unit.BattleDodgeRate,
                battleCritDamage = unit.BattleCritDamage,
                lifeStealRate = unit.LifeStealRate,
                battleThornsRate = unit.BattleThornsRate,
                isStunned = unit.IsStunned,
                hasArmorBreak = unit.HasArmorBreak,
                lightningChainBounces = unit.LightningChainBounces,
            });
        }

        try
        {
            for (int round = 0; round < maxRounds; round++)
            {
                // 我方行动
                foreach (var unit in simPlayers)
                {
                    if (unit == null || unit.IsDead) continue;
                    AutoChessAI.TakeAction(unit, simEnemies, simPlayers);
                }

                // 敌方行动
                foreach (var unit in simEnemies)
                {
                    if (unit == null || unit.IsDead) continue;
                    AutoChessAI.TakeAction(unit, simPlayers, simEnemies);
                }

                // 清理死亡（先尝试复活）
                if (RelicSystem.Instance != null)
                {
                    for (int i = simPlayers.Count - 1; i >= 0; i--)
                    {
                        var h = simPlayers[i];
                        if (h != null && h.IsDead && RelicSystem.Instance.TryRevive(h))
                            continue;
                    }
                }
                simPlayers.RemoveAll(u => u == null || u.IsDead);
                simEnemies.RemoveAll(u => u == null || u.IsDead);

                // 检查结束
                if (simPlayers.Count == 0 || simEnemies.Count == 0)
                    break;
            }

            return simEnemies.Count == 0 && simPlayers.Count > 0;
        }
        finally
        {
            // === 恢复所有单位状态 ===
            foreach (var snap in snapshots)
            {
                if (snap.hero == null) continue;
                snap.hero.SetCurrentHealth(snap.currentHealth);
                snap.hero.BattleAttack = snap.battleAttack;
                snap.hero.BattleAttackSpeed = snap.battleAttackSpeed;
                snap.hero.BattleCritRate = snap.battleCritRate;
                snap.hero.BattleDefense = snap.battleDefense;
                snap.hero.BattleSpeed = snap.battleSpeed;
                snap.hero.BattleDodgeRate = snap.battleDodgeRate;
                snap.hero.BattleCritDamage = snap.battleCritDamage;
                snap.hero.LifeStealRate = snap.lifeStealRate;
                snap.hero.BattleThornsRate = snap.battleThornsRate;
                snap.hero.SetStunned(snap.isStunned);
                snap.hero.HasArmorBreak = snap.hasArmorBreak;
                snap.hero.LightningChainBounces = snap.lightningChainBounces;
            }
        }
    }

    public void StopBattle()
    {
        IsBattleActive = false;
        if (battleCoroutine != null)
            StopCoroutine(battleCoroutine);
    }

    /// <summary>
    /// 清空战斗状态
    /// </summary>
    public void ClearBattle()
    {
        StopBattle();
        playerUnits.Clear();
        enemyUnits.Clear();
        BattleTimer = 0f;
        CurrentDiceCombo = null;
        DiceSkillUsed = false;
        LastDiceValues = null;
    }

    /// <summary>
    /// 从 DiceCombination 推导原始骰子值数组（供UI演出使用）
    /// </summary>
    private int[] DeduceDiceValues(DiceCombination combo)
    {
        switch (combo.Type)
        {
            case DiceCombinationType.ThreeOfAKind:
                return new int[] { combo.ThreeValue, combo.ThreeValue, combo.ThreeValue };
            case DiceCombinationType.Straight:
                return combo.StraightValues ?? new int[] { 1, 2, 3 };
            case DiceCombinationType.Pair:
                return new int[] { combo.PairValue, combo.PairValue, combo.SingleValue };
            default:
                // 其他组合类型用组合内的值或默认
                if (combo.StraightValues != null) return combo.StraightValues;
                if (combo.ThreeValue > 0) return new int[] { combo.ThreeValue, combo.ThreeValue, combo.ThreeValue };
                if (combo.PairValue > 0) return new int[] { combo.PairValue, combo.PairValue, combo.SingleValue };
                return new int[] { 1, 2, 3 };
        }
    }

    // ========================================================
    // 战斗统计通知方法（由Hero/AutoChessAI调用）
    // ========================================================

    /// <summary>
    /// 通知伤害造成 — 由 Hero.TakeDamage 调用
    /// </summary>
    public void NotifyDamageDealt(Hero attacker, Hero target, int damage)
    {
        OnDamageDealt?.Invoke(attacker, target, damage);
    }

    /// <summary>
    /// 通知治疗完成 — 由 Hero.Heal / AutoChessAI 调用
    /// healer参数为施放治疗的英雄，target为被治疗者
    /// </summary>
    public void NotifyHealDone(Hero healer, Hero target, int healAmount)
    {
        OnHealDone?.Invoke(healer, target, healAmount);
    }

    /// <summary>
    /// 通知护盾获得 — 由 Hero.AddShield 调用
    /// </summary>
    public void NotifyShieldGained(Hero hero, int shieldAmount)
    {
        OnShieldGained?.Invoke(hero, shieldAmount);
    }

    // ========================================================
    // 战斗音效桥接
    // ========================================================

    /// <summary>
    /// 初始化音效桥接 — 战斗开始时调用
    /// </summary>
    private void InitAudioBridge()
    {
        if (audioBridge != null) return;

        var bridgeGo = new GameObject("AudioBattleBridge");
        bridgeGo.transform.SetParent(transform);
        audioBridge = bridgeGo.AddComponent<AudioBattleBridge>();

        // 获取当前骰子掷骰器
        DiceRoller roller = RoguelikeGameManager.Instance?.DiceRoller;
        audioBridge.Initialize(this, roller);
    }

    /// <summary>
    /// 清理音效桥接 — 战斗结束时调用
    /// </summary>
    private void CleanupAudioBridge()
    {
        if (audioBridge != null)
        {
            audioBridge.Cleanup();
            Destroy(audioBridge.gameObject);
            audioBridge = null;
        }
    }
}
