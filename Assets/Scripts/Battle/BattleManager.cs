using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // 战斗速度档位（从 battle_formulas.json 的 speed_options 读取）
    public static float SPEED_1X = 1f;
    public static float SPEED_2X = 2f;
    public static float SPEED_4X = 4f;

    // 速度变化事件
    public event System.Action<float> OnBattleSpeedChanged;
    public event System.Action OnBattleStarted;
    public event System.Action<bool> OnBattleEnded; // true=胜利
    public event System.Action<string> OnDiceSkillTriggered;

    private Coroutine battleCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        // 应用连携技Buff
        SynergySystem.ApplySynergies(playerUnits);

        // 战斗开始时根据骰子组合触发一次性效果
        if (diceCombo != null)
        {
            ApplyDiceComboEffects(diceCombo);
        }

        Debug.Log($"战斗开始！我方{playerUnits.Count}人 vs 敌方{enemyUnits.Count}人 | 骰子: {(diceCombo != null ? diceCombo.Description : "无")}");

        OnBattleStarted?.Invoke();
        battleCoroutine = StartCoroutine(BattleLoop());
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
        playerUnits.RemoveAll(u => u == null || u.IsDead);
        enemyUnits.RemoveAll(u => u == null || u.IsDead);
    }

    void EndBattle()
    {
        bool allEnemyDead = enemyUnits.TrueForAll(u => u == null || u.IsDead);
        bool allPlayerDead = playerUnits.TrueForAll(u => u == null || u.IsDead);

        PlayerWon = allEnemyDead && !allPlayerDead;

        if (PlayerWon)
            Debug.Log("战斗胜利！");
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
    /// 快速模拟战斗（无动画，纯数值计算）
    /// maxRounds 从 battle_formulas.json 读取
    /// </summary>
    public bool SimulateBattle()
    {
        int maxRounds = BalanceProvider.GetSimulateBattleMaxRounds();
        var simPlayers = new List<Hero>(playerUnits);
        var simEnemies = new List<Hero>(enemyUnits);

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

            // 清理死亡
            simPlayers.RemoveAll(u => u == null || u.IsDead);
            simEnemies.RemoveAll(u => u == null || u.IsDead);

            // 检查结束
            if (simPlayers.Count == 0 || simEnemies.Count == 0)
                break;
        }

        return simEnemies.Count == 0 && simPlayers.Count > 0;
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
    }
}
