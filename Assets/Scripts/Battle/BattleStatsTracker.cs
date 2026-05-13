using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// BattleStatsTracker — 单例，事件驱动采集战斗数据
// ============================================================

/// <summary>
/// 战斗统计追踪器 — 订阅 BattleManager 事件采集数据，不侵入核心业务逻辑
/// 功能：击杀数、伤害、治疗、护盾、骰子组合统计
/// </summary>
public class BattleStatsTracker : MonoBehaviour
{
    public static BattleStatsTracker Instance { get; private set; }

    // ─── 当前Run统计 ───────────────────────────────
    private RunBattleStats currentRun;
    private RunBattleStats lastRun;   // 上一局（持久化后缓存）

    // ─── 当前战斗临时数据 ──────────────────────────
    private BattleStatsRecord currentBattle;
    private BattleReport currentReport;     // BE-17: 当前战斗报告
    private List<BattleSnapshot> turnSnapshots; // BE-17: 回合快照序列
    private int turnCounter;                    // BE-17: 回合计数器
    private float battleStartTime;
    private Dictionary<int, int> heroHPBeforeBattle = new Dictionary<int, int>();
    private bool isTracking;
    private bool battleActive;
    private bool subscribed;

    // ─── 事件 — 面板/其他系统可订阅 ─────────────────
    public event Action<RunBattleStats> OnRunStatsUpdated;
    public event Action<BattleStatsRecord> OnBattleRecorded;
    public event Action<RunBattleStats> OnRunEnded;

    // ====================================================
    // 生命周期
    // ====================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        StartNewRun();
    }

    void Start()
    {
        SubscribeEvents();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
        subscribed = false;
    }

    void OnEnable()
    {
        SubscribeEvents();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    // ====================================================
    // 事件订阅（不侵入 BattleManager）
    // ====================================================

    private void SubscribeEvents()
    {
        if (subscribed) return;  // 防重复订阅
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted += HandleBattleStarted;
        bm.OnBattleEnded += HandleBattleEnded;
        bm.OnDiceSkillTriggered += HandleDiceSkillTriggered;

        // 新增的精细事件（可能为null，安全订阅）
        bm.OnUnitKilled += HandleUnitKilled;
        bm.OnDamageDealt += HandleDamageDealt;
        bm.OnHealDone += HandleHealDone;
        bm.OnShieldGained += HandleShieldGained;

        // BE-17: 回合快照采集
        bm.OnTurnEnded += HandleTurnEnded;

        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!subscribed) return;  // 防重复取消
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.OnBattleStarted -= HandleBattleStarted;
        bm.OnBattleEnded -= HandleBattleEnded;
        bm.OnDiceSkillTriggered -= HandleDiceSkillTriggered;

        bm.OnUnitKilled -= HandleUnitKilled;
        bm.OnDamageDealt -= HandleDamageDealt;
        bm.OnHealDone -= HandleHealDone;
        bm.OnShieldGained -= HandleShieldGained;

        bm.OnTurnEnded -= HandleTurnEnded;

        subscribed = false;
    }

    // ====================================================
    // Run 生命周期
    // ====================================================

    /// <summary>开始新一轮（重置所有统计）</summary>
    public void StartNewRun()
    {
        currentRun = new RunBattleStats
        {
            comboCountMap = new Dictionary<string, int>(),
            relicsCollected = new List<string>(),
            heroCumulativeMap = new Dictionary<int, HeroBattleStats>(),
            heroCumulativeList = new List<HeroBattleStats>(),
            battleHistory = new List<BattleStatsRecord>()
        };
        isTracking = true;
        battleActive = false;
        Debug.Log("[BattleStatsTracker] 新Run开始，统计数据已重置");
    }

    /// <summary>Run结束，持久化保存</summary>
    public void EndRun()
    {
        isTracking = false;
        lastRun = currentRun;
        SaveRunStats(currentRun);
        OnRunEnded?.Invoke(currentRun);
        Debug.Log($"[BattleStatsTracker] Run结束，共{currentRun.totalBattles}场战斗，{currentRun.victories}胜");
    }

    // ====================================================
    // 战斗事件处理
    // ====================================================

    /// <summary>战斗开始 — 快照英雄状态</summary>
    private void HandleBattleStarted()
    {
        battleStartTime = Time.time;
        battleActive = true;
        turnCounter = 0;

        // BE-17: 初始化快照序列
        turnSnapshots = new List<BattleSnapshot>();
        currentReport = new BattleReport
        {
            battleIndex = currentRun.totalBattles + 1,
            snapshots = new List<BattleSnapshot>(),
            damageBreakdown = new DamageBreakdown(),
            mechanicStats = new MechanicStats()
        };

        var bm = BattleManager.Instance;
        if (bm == null) return;

        currentBattle = new BattleStatsRecord
        {
            battleIndex = currentRun.totalBattles + 1,
            heroStatsMap = new Dictionary<int, HeroBattleStats>(),
            diceSkillsTriggered = new List<string>()
        };

        // 记录骰子组合信息
        if (bm.CurrentDiceCombo != null && bm.CurrentDiceCombo.Type != DiceCombinationType.None)
        {
            currentBattle.diceComboType = bm.CurrentDiceCombo.Type.ToString();
            currentBattle.diceComboDesc = bm.CurrentDiceCombo.Description;

            // Run级骰子组合计数
            if (currentRun.comboCountMap == null)
                currentRun.comboCountMap = new Dictionary<string, int>();

            string comboName = bm.CurrentDiceCombo.Type.ToString();
            if (currentRun.comboCountMap.ContainsKey(comboName))
                currentRun.comboCountMap[comboName]++;
            else
                currentRun.comboCountMap[comboName] = 1;
        }
        else
        {
            currentBattle.diceComboType = "None";
            currentBattle.diceComboDesc = "无";
        }

        // 快照所有玩家英雄的HP
        heroHPBeforeBattle.Clear();
        if (bm.playerUnits != null)
        {
            foreach (var hero in bm.playerUnits)
            {
                if (hero == null) continue;

                int id = hero.GetInstanceID();
                heroHPBeforeBattle[id] = hero.CurrentHealth;

                // 初始化英雄统计
                if (!currentBattle.heroStatsMap.ContainsKey(id))
                {
                    currentBattle.heroStatsMap[id] = new HeroBattleStats
                    {
                        heroName = hero.Data != null ? hero.Data.heroName : "未知",
                        heroInstanceId = id
                    };
                }
            }
        }
    }

    /// <summary>战斗结束 — 汇总统计</summary>
    private void HandleBattleEnded(bool victory)
    {
        if (!battleActive || currentBattle == null) return;
        battleActive = false;

        float duration = Time.time - battleStartTime;
        currentBattle.isVictory = victory;
        currentBattle.duration = duration;

        // 从BM获取最终击杀数（基于已清除的敌人）
        var bm = BattleManager.Instance;

        // 计算伤害承受（通过英雄HP差值推算 + 累计追踪）
        if (bm != null && bm.playerUnits != null)
        {
            foreach (var hero in bm.playerUnits)
            {
                if (hero == null) continue;
                int id = hero.GetInstanceID();

                if (heroHPBeforeBattle.TryGetValue(id, out int hpBefore))
                {
                    int hpLost = hpBefore - hero.CurrentHealth;
                    int damageTaken = Mathf.Max(0, hpLost);

                    // 护盾转化为临时生命会导致 CurrentHealth > MaxHealth
                    // 但承受伤害 = hpBefore - CurrentHealth + 已算作伤害的部分
                    if (!currentBattle.heroStatsMap.ContainsKey(id))
                    {
                        currentBattle.heroStatsMap[id] = new HeroBattleStats
                        {
                            heroName = hero.Data != null ? hero.Data.heroName : "未知",
                            heroInstanceId = id
                        };
                    }
                    currentBattle.heroStatsMap[id].damageTaken += damageTaken;
                }
            }
        }

        // 汇总本场数据
        currentBattle.totalDamageDealt = 0;
        currentBattle.totalDamageTaken = 0;
        currentBattle.totalHealing = 0;
        currentBattle.totalShield = 0;
        currentBattle.totalKills = 0;

        foreach (var kv in currentBattle.heroStatsMap)
        {
            var hs = kv.Value;
            currentBattle.totalDamageDealt += hs.damageDealt;
            currentBattle.totalDamageTaken += hs.damageTaken;
            currentBattle.totalHealing += hs.healingDone;
            currentBattle.totalShield += hs.shieldGained;
            currentBattle.totalKills += hs.kills;
        }

        // ─── 更新Run累计统计 ──────────────────────────
        currentRun.totalBattles++;
        if (victory)
        {
            currentRun.victories++;
            currentRun.currentConsecutiveWins++;
            if (currentRun.currentConsecutiveWins > currentRun.maxConsecutiveWins)
                currentRun.maxConsecutiveWins = currentRun.currentConsecutiveWins;
        }
        else
        {
            currentRun.defeats++;
            currentRun.currentConsecutiveWins = 0;
        }

        currentRun.winRate = currentRun.totalBattles > 0
            ? (float)currentRun.victories / currentRun.totalBattles * 100f
            : 0f;

        currentRun.totalDamageDealt += currentBattle.totalDamageDealt;
        currentRun.totalDamageTaken += currentBattle.totalDamageTaken;
        currentRun.totalHealing += currentBattle.totalHealing;
        currentRun.totalShield += currentBattle.totalShield;
        currentRun.totalKills += currentBattle.totalKills;
        currentRun.totalBattleDuration += duration;

        if (duration > currentRun.longestBattle)
            currentRun.longestBattle = duration;

        // 合并英雄累计统计
        foreach (var kv in currentBattle.heroStatsMap)
        {
            int id = kv.Key;
            var battleHS = kv.Value;
            if (!currentRun.heroCumulativeMap.ContainsKey(id))
            {
                currentRun.heroCumulativeMap[id] = new HeroBattleStats
                {
                    heroName = battleHS.heroName,
                    heroInstanceId = battleHS.heroInstanceId
                };
            }
            var runHS = currentRun.heroCumulativeMap[id];
            runHS.damageDealt += battleHS.damageDealt;
            runHS.damageTaken += battleHS.damageTaken;
            runHS.healingDone += battleHS.healingDone;
            runHS.shieldGained += battleHS.shieldGained;
            runHS.kills += battleHS.kills;
            runHS.critCount += battleHS.critCount;
        }

        currentRun.battleHistory.Add(currentBattle);

        // BE-17: 生成战斗报告
        FinalizeReport(victory, duration);

        // 通知
        OnBattleRecorded?.Invoke(currentBattle);
        OnRunStatsUpdated?.Invoke(currentRun);

        Debug.Log($"[BattleStatsTracker] 第{currentBattle.battleIndex}场结束 " +
                  $"胜={victory} 时长={duration:F1}s " +
                  $"伤害={currentBattle.totalDamageDealt} 击杀={currentBattle.totalKills} " +
                  $"治疗={currentBattle.totalHealing} 护盾={currentBattle.totalShield}");
    }

    /// <summary>骰子技能触发 — 记录</summary>
    private void HandleDiceSkillTriggered(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        if (!isTracking || currentRun == null) return;

        // 本场记录
        if (currentBattle != null && battleActive)
        {
            currentBattle.diceSkillsTriggered.Add(skillName);
        }

        // BE-17: 记录骰子技能时间轴事件
        if (currentReport != null)
        {
            currentReport.mechanicStats.diceSkillUses++;
            currentReport.mechanicStats.timelineEvents.Add(new TimelineEvent
            {
                turnIndex = turnCounter,
                timestamp = Time.time - battleStartTime,
                eventType = "dice_skill",
                description = $"骰子技能: {skillName}",
                sourceUnit = "",
                targetUnit = "",
                value = 0
            });
        }
    }

    /// <summary>单位被击杀</summary>
    private void HandleUnitKilled(Hero killer, Hero victim)
    {
        if (!battleActive || currentBattle == null) return;

        if (killer != null)
        {
            int killerId = killer.GetInstanceID();
            if (currentBattle.heroStatsMap.TryGetValue(killerId, out var hs))
            {
                hs.kills++;
            }
        }

        // BE-17: 记录击杀时间轴事件
        if (currentReport != null)
        {
            currentReport.mechanicStats.timelineEvents.Add(new TimelineEvent
            {
                turnIndex = turnCounter,
                timestamp = Time.time - battleStartTime,
                eventType = "kill",
                description = $"{killer?.Data?.heroName ?? "??"} 击杀 {victim?.Data?.heroName ?? "??"}",
                sourceUnit = killer?.Data?.heroName ?? "??",
                targetUnit = victim?.Data?.heroName ?? "??",
                value = 0
            });
        }
    }

    /// <summary>伤害造成</summary>
    private void HandleDamageDealt(Hero attacker, Hero target, int damage)
    {
        if (!battleActive || currentBattle == null) return;
        if (attacker == null) return;

        // 统计玩家方英雄的伤害
        int attackerId = attacker.GetInstanceID();
        EnsureHeroStats(attacker);
        if (currentBattle.heroStatsMap.TryGetValue(attackerId, out var hs))
        {
            hs.damageDealt += damage;
            // 暴击判定（伤害 > 攻击力视为暴击）
            bool isCrit = damage > attacker.BattleAttack;
            if (isCrit)
            {
                hs.critCount++;
                // BE-17: 记录暴击时间轴事件
                if (currentReport != null)
                {
                    currentReport.damageBreakdown.critDamage += damage;
                    currentReport.mechanicStats.critCount++;
                    currentReport.mechanicStats.timelineEvents.Add(new TimelineEvent
                    {
                        turnIndex = turnCounter,
                        timestamp = Time.time - battleStartTime,
                        eventType = "crit",
                        description = $"{attacker.Data?.heroName ?? "??"} 暴击 {target?.Data?.heroName ?? "??"} {damage}点",
                        sourceUnit = attacker.Data?.heroName ?? "??",
                        targetUnit = target?.Data?.heroName ?? "??",
                        value = damage
                    });
                }
            }
            else
            {
                if (currentReport != null)
                    currentReport.damageBreakdown.physicalDamage += damage;
            }
        }
    }

    /// <summary>治疗完成</summary>
    private void HandleHealDone(Hero healer, Hero target, int healAmount)
    {
        if (!battleActive || currentBattle == null) return;

        // 治疗者统计（healer可能为null，如骰子技能治疗）
        if (healer != null)
        {
            int healerId = healer.GetInstanceID();
            EnsureHeroStats(healer);
            if (currentBattle.heroStatsMap.TryGetValue(healerId, out var hs))
            {
                hs.healingDone += healAmount;
            }
        }
        // 如果healer为null，则归到target自身的治疗统计
        else if (target != null)
        {
            int targetId = target.GetInstanceID();
            EnsureHeroStats(target);
            if (currentBattle.heroStatsMap.TryGetValue(targetId, out var hs))
            {
                hs.healingDone += healAmount;
            }
        }
    }

    /// <summary>护盾获得</summary>
    private void HandleShieldGained(Hero hero, int shieldAmount)
    {
        if (!battleActive || currentBattle == null) return;
        if (hero == null) return;

        int heroId = hero.GetInstanceID();
        EnsureHeroStats(hero);
        if (currentBattle.heroStatsMap.TryGetValue(heroId, out var hs))
        {
            hs.shieldGained += shieldAmount;
        }
    }

    // ====================================================
    // 外部调用接口（供其他系统使用）
    // ====================================================

    /// <summary>获取当前Run统计</summary>
    public RunBattleStats GetCurrentRunStats() => currentRun;

    /// <summary>获取上一局Run统计</summary>
    public RunBattleStats GetLastRunStats() => lastRun;

    /// <summary>遗物收集记录</summary>
    public void RecordRelicCollected(string relicName)
    {
        if (string.IsNullOrEmpty(relicName) || currentRun == null) return;
        if (!currentRun.relicsCollected.Contains(relicName))
            currentRun.relicsCollected.Add(relicName);
    }

    /// <summary>击杀数手动累加（当 OnUnitKilled 事件未触发时作为兜底）</summary>
    public void RecordKill(int count = 1)
    {
        if (currentRun == null) return;
        currentRun.totalKills += count;
    }

    /// <summary>获取当前Run的MVP英雄名</summary>
    public string GetMVPHeroName()
    {
        if (currentRun == null) return "无";
        var mvp = currentRun.GetMVPHero();
        return mvp != null ? mvp.heroName : "无";
    }

    // ====================================================
    // 内部辅助
    // ====================================================

    /// <summary>确保英雄在当前战斗统计中已注册</summary>
    private void EnsureHeroStats(Hero hero)
    {
        if (hero == null || currentBattle == null) return;
        int id = hero.GetInstanceID();
        if (!currentBattle.heroStatsMap.ContainsKey(id))
        {
            currentBattle.heroStatsMap[id] = new HeroBattleStats
            {
                heroName = hero.Data != null ? hero.Data.heroName : "未知",
                heroInstanceId = id
            };
        }
    }

    // ====================================================
    // BE-17: 战斗回放 — 回合快照 + 报告生成
    // ====================================================

    /// <summary>每回合结束 — 采集双方状态快照</summary>
    private void HandleTurnEnded(int turnIdx)
    {
        if (!battleActive || currentReport == null) return;

        turnCounter = turnIdx;
        var bm = BattleManager.Instance;
        if (bm == null) return;

        var snapshot = new BattleSnapshot
        {
            turnIndex = turnIdx,
            timestamp = Time.time - battleStartTime,
            playerUnits = CaptureUnitSnapshots(bm.playerUnits),
            enemyUnits = CaptureUnitSnapshots(bm.enemyUnits)
        };

        turnSnapshots.Add(snapshot);
    }

    /// <summary>采集一组单位的快照</summary>
    private List<UnitSnapshot> CaptureUnitSnapshots(List<Hero> units)
    {
        var result = new List<UnitSnapshot>();
        if (units == null) return result;

        foreach (var unit in units)
        {
            if (unit == null) continue;
            result.Add(new UnitSnapshot
            {
                unitName = unit.Data != null ? unit.Data.heroName : "??",
                hp = unit.CurrentHealth,
                maxHp = unit.MaxHealth,
                shield = 0,  // 护盾直接加到HP，无法区分
                isDead = unit.IsDead,
                buffCount = unit.RelicBuffs?.Count ?? 0,
                debuffCount = 0
            });
        }
        return result;
    }

    /// <summary>战斗结束 — 生成完整 BattleReport</summary>
    private void FinalizeReport(bool victory, float duration)
    {
        if (currentReport == null) return;

        currentReport.isVictory = victory;
        currentReport.totalTurns = turnCounter + 1;
        currentReport.duration = duration;

        // 填入汇总数据（来自 currentBattle，已由 HandleBattleEnded 计算）
        currentReport.totalDamageDealt = currentBattle?.totalDamageDealt ?? 0;
        currentReport.totalDamageTaken = currentBattle?.totalDamageTaken ?? 0;
        currentReport.totalHealing = currentBattle?.totalHealing ?? 0;
        currentReport.totalShield = currentBattle?.totalShield ?? 0;
        currentReport.totalKills = currentBattle?.totalKills ?? 0;

        // 骰子信息
        currentReport.diceComboType = currentBattle?.diceComboType ?? "None";
        currentReport.diceSkillsTriggered = currentBattle?.diceSkillsTriggered ?? new List<string>();

        // 回合快照（采样：最多保存60个关键帧）
        currentReport.snapshots = SampleSnapshots(turnSnapshots, 60);

        // 英雄排名
        BuildHeroRankings();

        // 时间轴按时间排序
        if (currentReport.mechanicStats.timelineEvents != null)
        {
            currentReport.mechanicStats.timelineEvents =
                currentReport.mechanicStats.timelineEvents
                    .OrderBy(e => e.timestamp)
                    .ToList();
        }

        Debug.Log($"[BattleStatsTracker] BattleReport生成 回合={currentReport.totalTurns} " +
                  $"伤害构成=[物理:{currentReport.damageBreakdown.physicalDamage} 暴击:{currentReport.damageBreakdown.critDamage}] " +
                  $"MVP={currentReport.mvpHeroName}");
    }

    /// <summary>采样快照（保留关键帧，控制内存）</summary>
    private List<BattleSnapshot> SampleSnapshots(List<BattleSnapshot> all, int maxCount)
    {
        if (all == null || all.Count <= maxCount) return all ?? new List<BattleSnapshot>();

        // 保留第一个、最后一个、有事件的、和均匀采样的
        var result = new List<BattleSnapshot>();
        result.Add(all[0]);

        // 有事件的快照优先
        var eventSnapshots = all.Where(s => s.events != null && s.events.Count > 0).ToList();

        // 均匀采样填充
        int remaining = maxCount - 2; // 减去首尾
        float step = (float)(all.Count - 2) / remaining;
        for (int i = 0; i < remaining; i++)
        {
            int idx = 1 + (int)(i * step);
            if (idx < all.Count - 1)
                result.Add(all[idx]);
        }

        result.Add(all[all.Count - 1]);
        return result.OrderBy(s => s.turnIndex).ToList();
    }

    /// <summary>构建英雄表现排名</summary>
    private void BuildHeroRankings()
    {
        if (currentBattle == null || currentReport == null) return;

        currentReport.heroRankings = new List<HeroReportEntry>();
        int totalDmg = Mathf.Max(1, currentBattle.totalDamageDealt);

        foreach (var kv in currentBattle.heroStatsMap)
        {
            var hs = kv.Value;
            currentReport.heroRankings.Add(new HeroReportEntry
            {
                heroName = hs.heroName,
                damageDealt = hs.damageDealt,
                damageTaken = hs.damageTaken,
                healingDone = hs.healingDone,
                shieldGained = hs.shieldGained,
                kills = hs.kills,
                critCount = hs.critCount,
                score = hs.Score,
                damagePercent = (float)hs.damageDealt / totalDmg * 100f
            });
        }

        // 按评分降序排列
        currentReport.heroRankings = currentReport.heroRankings
            .OrderByDescending(h => h.score)
            .ToList();

        // 设置 MVP
        if (currentReport.heroRankings.Count > 0)
        {
            currentReport.mvpHeroName = currentReport.heroRankings[0].heroName;
            currentReport.mvpScore = currentReport.heroRankings[0].score;
        }
    }

    // ====================================================
    // 外部接口
    // ====================================================

    /// <summary>获取最近一场战斗的 BattleReport（BE-17 核心接口）</summary>
    public BattleReport GenerateReport()
    {
        return currentReport;
    }

    /// <summary>获取当前Run所有已完成的战斗报告列表</summary>
    public List<BattleReport> GetAllBattleReports()
    {
        // 从 battleHistory 重建（暂不持久化 BattleReport）
        return runReports ?? new List<BattleReport>();
    }

    private List<BattleReport> runReports = new List<BattleReport>();

    // ====================================================
    // 持久化
    // ====================================================

    /// <summary>持久化RunStats（JSON → PlayerPrefs）</summary>
    public void SaveRunStats(RunBattleStats stats)
    {
        if (stats == null) return;
        stats.BeforeSerialize();
        string json = JsonUtility.ToJson(stats);
        PlayerPrefs.SetString("LastRunBattleStats", json);
        PlayerPrefs.Save();
        Debug.Log($"[BattleStatsTracker] RunStats已保存，共{stats.totalBattles}场战斗");
    }

    /// <summary>从PlayerPrefs加载上一局RunStats</summary>
    public RunBattleStats LoadLastRunStats()
    {
        // 优先从新key读取
        string json = PlayerPrefs.GetString("LastRunBattleStats", "");
        if (string.IsNullOrEmpty(json))
        {
            // 兼容旧key
            json = PlayerPrefs.GetString("LastRunStats", "");
        }
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var stats = JsonUtility.FromJson<RunBattleStats>(json);
            if (stats != null)
                stats.AfterDeserialize();
            return stats;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BattleStatsTracker] 加载RunStats失败: {e.Message}");
            return null;
        }
    }

    // ====================================================
    // 调试辅助
    // ====================================================

    /// <summary>打印当前Run统计摘要</summary>
    public void DebugPrintSummary()
    {
        if (currentRun == null) { Debug.Log("[BattleStats] 无Run数据"); return; }
        Debug.Log($"[BattleStats] 场次={currentRun.totalBattles} 胜={currentRun.victories} " +
                  $"连胜={currentRun.maxConsecutiveWins} 击杀={currentRun.totalKills} " +
                  $"伤害={currentRun.totalDamageDealt} 治疗={currentRun.totalHealing} " +
                  $"护盾={currentRun.totalShield} 时长={currentRun.totalBattleDuration:F1}s");
    }
}
