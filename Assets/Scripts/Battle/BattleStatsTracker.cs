using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// 数据模型
// ============================================================

/// <summary>
/// 单次战斗记录
/// </summary>
[Serializable]
public class BattleRecord
{
    public int battleIndex;          // 第N场战斗
    public bool isVictory;
    public float duration;           // 战斗时长（秒）
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int totalHealing;
    public int totalShield;
    public List<string> diceCombos = new List<string>(); // 触发的骰子组合
}

/// <summary>
/// 英雄成长轨迹
/// </summary>
[Serializable]
public class HeroGrowth
{
    public string heroName;
    public string heroClass;

    public int initialLevel;
    public int finalLevel;

    public int initialStar;
    public int finalStar;

    public int initialHP;
    public int finalHP;

    public int initialAtk;
    public int finalAtk;

    public int initialDef;
    public int finalDef;

    public int initialSpd;
    public int finalSpd;
}

/// <summary>
/// 整个Run的统计数据
/// JsonUtility不兼容Dictionary，comboCounts用平行List序列化
/// </summary>
[Serializable]
public class RunStats
{
    // 汇总统计
    public int totalBattles;
    public int victories;
    public int defeats;
    public float winRate;
    public int maxConsecutiveWins;

    public int totalDamageDealt;
    public int totalDamageTaken;
    public int totalHealing;
    public int totalShield;

    // 骰子组合统计 — JsonUtility兼容的平行List
    public List<string> comboNames = new List<string>();
    public List<int> comboValues = new List<int>();

    // 运行时Dictionary（[NonSerialized]不参与JsonUtility序列化）
    [NonSerialized] public Dictionary<string, int> comboCounts = new Dictionary<string, int>();

    // 遗物 & 成长
    public List<string> relicsCollected = new List<string>();
    public HeroGrowth heroGrowth = new HeroGrowth();

    // 战斗历史
    public List<BattleRecord> battleHistory = new List<BattleRecord>();

    /// <summary>序列化前：Dictionary → 平行List</summary>
    public void BeforeSerialize()
    {
        if (comboCounts != null)
        {
            comboNames = comboCounts.Keys.ToList();
            comboValues = comboCounts.Values.ToList();
        }
    }

    /// <summary>反序列化后：平行List → Dictionary</summary>
    public void AfterDeserialize()
    {
        comboCounts = new Dictionary<string, int>();
        if (comboNames != null && comboValues != null)
        {
            for (int i = 0; i < comboNames.Count && i < comboValues.Count; i++)
                comboCounts[comboNames[i]] = comboValues[i];
        }
    }
}

/// <summary>
/// 接口 — 后续可对接后端持久化
/// </summary>
public interface IBattleStatsData
{
    RunStats GetCurrentRunStats();
    List<RunStats> GetHistoryRunStats();
    void SaveRunStats(RunStats stats);
}

// ============================================================
// BattleStatsTracker — 单例数据采集层
// ============================================================

/// <summary>
/// 战斗统计追踪器 — 通过订阅BattleManager事件采集数据，不侵入业务逻辑
/// </summary>
public class BattleStatsTracker : MonoBehaviour, IBattleStatsData
{
    public static BattleStatsTracker Instance { get; private set; }

    private RunStats currentRun;
    private float battleStartTime;
    private Hero trackedHero;
    private int heroHPBeforeBattle;
    private int consecutiveWins;
    private bool isTracking;

    // 事件 — 面板可订阅
    public event Action<RunStats> OnRunStatsUpdated;
    public event Action<BattleRecord> OnBattleRecorded;

    // 场景加载后重新订阅（DontDestroyOnLoad会导致场景切换后BM引用失效）
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
    }

    // 场景切换后重新尝试订阅
    void OnEnable()
    {
        SubscribeEvents();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    // ========================================================
    // 事件订阅（不侵入BattleManager）
    // ========================================================

    private void SubscribeEvents()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;
        bm.OnBattleStarted += OnBattleStarted;
        bm.OnBattleEnded += OnBattleEnded;
        bm.OnDiceSkillTriggered += OnDiceSkillTriggered;
    }

    private void UnsubscribeEvents()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;
        bm.OnBattleStarted -= OnBattleStarted;
        bm.OnBattleEnded -= OnBattleEnded;
        bm.OnDiceSkillTriggered -= OnDiceSkillTriggered;
    }

    // ========================================================
    // Run 生命周期
    // ========================================================

    /// <summary>开始新一轮（重置所有统计）</summary>
    public void StartNewRun()
    {
        currentRun = new RunStats
        {
            comboCounts = new Dictionary<string, int>(),
            relicsCollected = new List<string>(),
            battleHistory = new List<BattleRecord>(),
            heroGrowth = new HeroGrowth()
        };
        consecutiveWins = 0;
        isTracking = true;
    }

    /// <summary>Run结束，序列化保存</summary>
    public void EndRun()
    {
        isTracking = false;
        SaveRunStats(currentRun);
    }

    // ========================================================
    // 战斗事件处理
    // ========================================================

    /// <summary>战斗开始 — 快照英雄状态</summary>
    private void OnBattleStarted()
    {
        battleStartTime = Time.time;

        // 从BattleManager获取玩家方第一个存活英雄作为追踪对象
        var bm = BattleManager.Instance;
        if (bm != null && bm.playerUnits != null && bm.playerUnits.Count > 0)
        {
            trackedHero = bm.playerUnits.FirstOrDefault(h => h != null && !h.IsDead);
        }
        if (trackedHero == null)
        {
            trackedHero = FindObjectOfType<Hero>();
        }

        if (trackedHero != null)
        {
            heroHPBeforeBattle = trackedHero.CurrentHealth;
        }
    }

    /// <summary>战斗结束 — 记录战斗数据并更新Run统计</summary>
    private void OnBattleEnded(bool victory)
    {
        float duration = Time.time - battleStartTime;

        var record = new BattleRecord
        {
            battleIndex = currentRun.totalBattles + 1,
            isVictory = victory,
            duration = duration,
            totalDamageDealt = 0,
            totalDamageTaken = 0,
            totalHealing = 0,
            totalShield = 0,
            diceCombos = new List<string>()
        };

        // 推算伤害（通过英雄HP变化）
        if (trackedHero != null)
        {
            int hpLost = heroHPBeforeBattle - trackedHero.CurrentHealth;
            record.totalDamageTaken = Mathf.Max(0, hpLost);
        }

        // 更新Run汇总
        currentRun.totalBattles++;
        if (victory)
        {
            currentRun.victories++;
            consecutiveWins++;
            if (consecutiveWins > currentRun.maxConsecutiveWins)
                currentRun.maxConsecutiveWins = consecutiveWins;
        }
        else
        {
            currentRun.defeats++;
            consecutiveWins = 0;
        }

        currentRun.winRate = currentRun.totalBattles > 0
            ? (float)currentRun.victories / currentRun.totalBattles * 100f
            : 0f;

        currentRun.totalDamageTaken += record.totalDamageTaken;

        currentRun.battleHistory.Add(record);

        OnBattleRecorded?.Invoke(record);
        OnRunStatsUpdated?.Invoke(currentRun);
    }

    /// <summary>骰子技能触发 — 记录组合使用次数</summary>
    private void OnDiceSkillTriggered(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        if (!isTracking || currentRun == null) return;

        if (currentRun.comboCounts == null)
            currentRun.comboCounts = new Dictionary<string, int>();

        if (currentRun.comboCounts.ContainsKey(skillName))
            currentRun.comboCounts[skillName]++;
        else
            currentRun.comboCounts[skillName] = 1;
    }

    // ========================================================
    // 外部调用接口
    // ========================================================

    /// <summary>遗物收集记录（由RoguelikeGameManager等外部调用）</summary>
    public void RecordRelicCollected(string relicName)
    {
        if (string.IsNullOrEmpty(relicName) || currentRun == null) return;
        if (!currentRun.relicsCollected.Contains(relicName))
            currentRun.relicsCollected.Add(relicName);
    }

    /// <summary>英雄成长轨迹（由SettlementPanel等外部调用）</summary>
    public void RecordHeroGrowth(Hero hero)
    {
        if (hero == null || hero.Data == null || currentRun == null) return;
        var g = currentRun.heroGrowth;
        if (g == null) currentRun.heroGrowth = g = new HeroGrowth();

        if (string.IsNullOrEmpty(g.heroName))
        {
            // 首次记录 = 初始值
            g.heroName = hero.Data.heroName;
            g.heroClass = hero.Data.heroClass.ToString();
            g.initialLevel = hero.HeroLevel;
            g.initialStar = hero.StarLevel;
            g.initialHP = hero.MaxHealth;
            g.initialAtk = hero.Attack;
            g.initialDef = hero.Defense;
            g.initialSpd = hero.Speed;
        }

        // 每次都更新最终值
        g.finalLevel = hero.HeroLevel;
        g.finalStar = hero.StarLevel;
        g.finalHP = hero.MaxHealth;
        g.finalAtk = hero.Attack;
        g.finalDef = hero.Defense;
        g.finalSpd = hero.Speed;
    }

    // ========================================================
    // IBattleStatsData 实现
    // ========================================================

    /// <summary>获取当前Run统计</summary>
    public RunStats GetCurrentRunStats() => currentRun;

    /// <summary>获取历史Run统计（预留，当前从PlayerPrefs读取上一局）</summary>
    public List<RunStats> GetHistoryRunStats()
    {
        var list = new List<RunStats>();
        var lastRun = LoadLastRunStats();
        if (lastRun != null)
            list.Add(lastRun);
        return list;
    }

    /// <summary>持久化RunStats（JSON → PlayerPrefs）</summary>
    public void SaveRunStats(RunStats stats)
    {
        if (stats == null) return;

        // 序列化前转换Dictionary → 平行List
        stats.BeforeSerialize();

        string json = JsonUtility.ToJson(stats);
        PlayerPrefs.SetString("LastRunStats", json);
        PlayerPrefs.Save();

        Debug.Log($"[BattleStatsTracker] RunStats已保存，共{stats.totalBattles}场战斗");
    }

    /// <summary>从PlayerPrefs加载上一局RunStats</summary>
    public RunStats LoadLastRunStats()
    {
        string json = PlayerPrefs.GetString("LastRunStats", "");
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var stats = JsonUtility.FromJson<RunStats>(json);
            if (stats != null)
                stats.AfterDeserialize(); // 平行List → Dictionary
            return stats;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BattleStatsTracker] 加载RunStats失败: {e.Message}");
            return null;
        }
    }
}
