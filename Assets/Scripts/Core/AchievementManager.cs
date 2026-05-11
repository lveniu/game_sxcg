using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// =====================================================================
// 成就系统 — 数据模型
// =====================================================================

/// <summary>成就条件类型</summary>
public enum AchievementConditionType
{
    LevelReached,       // 达到关卡
    BossKills,          // Boss击杀数
    TotalBattleWins,    // 总胜场数
    PerfectVictories,   // 无伤胜利数
    RelicsInRun,        // 单局遗物数
    MaxGoldHeld,        // 单局最大金币持有
    HeroesInRun,        // 单局英雄数
    DicePairCount,      // 对子触发次数
    DiceStraightCount,  // 顺子触发次数
    DiceTripleCount,    // 三条触发次数
    DiceTotalCombos,    // 骰子组合总次数
    SpeedClear,         // 速通（N秒内）
    RerollsInBattle,    // 单场重摇次数
    ComebackWins,       // 翻盘胜利
    TotalEnemiesKilled  // 总击杀敌人数
}

/// <summary>成就稀有度</summary>
public enum AchievementRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>单条成就配置（从 JSON 加载）</summary>
[System.Serializable]
public class AchievementDef
{
    public string id;
    public string name_cn;
    public string description;
    public string category;
    public string condition_type;
    public int target_value;
    public string icon;
    public string rarity;
    public List<AchievementReward> rewards;
    public bool is_hidden;
}

/// <summary>成就奖励</summary>
[System.Serializable]
public class AchievementReward
{
    public string type;   // gold / relic / dice_face
    public int value;
    public string item_id; // 对于 relic/dice_face 类型
}

/// <summary>成就进度记录（运行时 + 存档）</summary>
[System.Serializable]
public class AchievementProgress
{
    public string achievement_id;
    public int current_value;
    public bool is_unlocked;
    public long unlock_timestamp; // Unix timestamp
    public bool rewards_claimed;
}

// =====================================================================
// 成就系统 — 管理器
// =====================================================================

/// <summary>
/// 成就管理器 — 单例，负责成就解锁、进度追踪、持久化、奖励发放
/// 监听游戏事件自动推进成就进度
/// 
/// 使用方式：
///   AchievementManager.Instance.TrackXxx(...) 触发进度
///   AchievementManager.Instance.GetProgress(...) 查询进度
///   AchievementManager.Instance.ClaimRewards(...) 领取奖励
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    // ========== 事件（给UI监听） ==========

    /// <summary>成就解锁时触发（参数：成就ID）</summary>
    public event System.Action<string> OnAchievementUnlocked;

    /// <summary>成就进度变化时触发（参数：成就ID, 当前值, 目标值）</summary>
    public event System.Action<string, int, int> OnProgressChanged;

    /// <summary>奖励已领取（参数：成就ID）</summary>
    public event System.Action<string> OnRewardsClaimed;

    // ========== 数据 ==========

    private Dictionary<string, AchievementDef> _defs = new Dictionary<string, AchievementDef>();
    private Dictionary<string, AchievementProgress> _progress = new Dictionary<string, AchievementProgress>();

    // 跨局累计统计
    private int _totalBossKills;
    private int _totalBattleWins;
    private int _totalPerfectVictories;
    private int _totalEnemiesKilled;
    private int _totalDiceCombos;

    // 当前局统计
    private int _runRelicCount;
    private int _runHeroCount;
    private int _runMaxGold;
    private int _battleRerollCount;
    private float _battleStartTime;
    private int _currentBattleAliveHeroesStart;

    // 运行时计数（跨局累计的骰子细分）
    private int _dicePairCount;
    private int _diceStraightCount;
    private int _diceTripleCount;

    // 存档key
    private const string ACHIEVEMENT_SAVE_KEY = "achievement_progress_v1";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAchievementDefs();
    }

    void Start()
    {
        LoadProgress();
        SubscribeEvents();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ========== 初始化 ==========

    void LoadAchievementDefs()
    {
        var config = ConfigLoader.LoadAchievements();
        if (config?.achievements == null)
        {
            Debug.LogWarning("[AchievementManager] achievements.json 加载失败，成就系统不可用");
            return;
        }

        foreach (var def in config.achievements)
        {
            _defs[def.id] = def;
            // 初始化进度（未解锁状态）
            if (!_progress.ContainsKey(def.id))
            {
                _progress[def.id] = new AchievementProgress
                {
                    achievement_id = def.id,
                    current_value = 0,
                    is_unlocked = false,
                    unlock_timestamp = 0,
                    rewards_claimed = false
                };
            }
        }

        Debug.Log($"[AchievementManager] 已加载 {_defs.Count} 个成就定义");
    }

    void SubscribeEvents()
    {
        // GameStateMachine: 关卡完成
        var gsm = GameStateMachine.Instance;
        if (gsm != null)
        {
            gsm.OnLevelEnded += OnLevelEnded;
        }

        // BattleManager: 战斗结束、骰子技能
        var bm = BattleManager.Instance;
        if (bm != null)
        {
            bm.OnBattleEnded += OnBattleEnded;
            bm.OnDiceSkillTriggered += OnDiceSkillTriggered;
        }

        // DiceRoller: 重摇
        var dr = FindFirstObjectByType<DiceRoller>();
        if (dr != null)
        {
            dr.OnRerollUsed += OnRerollUsed;
        }
    }

    void UnsubscribeEvents()
    {
        var gsm = GameStateMachine.Instance;
        if (gsm != null) gsm.OnLevelEnded -= OnLevelEnded;

        var bm = BattleManager.Instance;
        if (bm != null)
        {
            bm.OnBattleEnded -= OnBattleEnded;
            bm.OnDiceSkillTriggered -= OnDiceSkillTriggered;
        }
    }

    // ========== 事件处理 ==========

    void OnLevelEnded(int level, bool victory)
    {
        if (!victory) return;

        // 关卡推进
        TrackProgress("level_reached", level);
    }

    void OnBattleEnded(bool playerWon)
    {
        if (playerWon)
        {
            _totalBattleWins++;
            TrackProgress("total_battle_wins", _totalBattleWins);
            TrackProgress("total_enemies_killed", _totalEnemiesKilled);

            // 速通检测
            float battleDuration = Time.time - _battleStartTime;
            if (battleDuration <= 5f)
            {
                TrackProgress("speed_clear", 1);
            }

            // 无伤检测
            bool perfectVictory = CheckPerfectVictory();
            if (perfectVictory)
            {
                _totalPerfectVictories++;
                TrackProgress("perfect_victories", _totalPerfectVictories);
            }

            // 翻盘检测
            int aliveCount = GetAliveHeroCount();
            if (aliveCount == 1 && _currentBattleAliveHeroesStart > 1)
            {
                int comebacks = GetProgressValue("comeback_wins") + 1;
                TrackProgress("comeback_wins", comebacks);
            }
        }

        // 单局统计
        _runRelicCount = GetCurrentRelicCount();
        TrackProgress("relics_in_run", _runRelicCount);

        _runHeroCount = GetCurrentHeroCount();
        TrackProgress("heroes_in_run", _runHeroCount);

        _runMaxGold = Mathf.Max(_runMaxGold, GetCurrentGold());
        TrackProgress("max_gold_held", _runMaxGold);

        // 重摇统计
        TrackProgress("rerolls_in_battle", _battleRerollCount);

        AutoSave();
    }

    void OnDiceSkillTriggered(string skillDesc)
    {
        _totalDiceCombos++;

        // 根据描述判断组合类型
        if (skillDesc.Contains("三条"))
        {
            _diceTripleCount++;
            TrackProgress("dice_triple_count", _diceTripleCount);
        }
        else if (skillDesc.Contains("顺子"))
        {
            _diceStraightCount++;
            TrackProgress("dice_straight_count", _diceStraightCount);
        }
        else if (skillDesc.Contains("对子"))
        {
            _dicePairCount++;
            TrackProgress("dice_pair_count", _dicePairCount);
        }

        TrackProgress("dice_total_combos", _totalDiceCombos);
    }

    void OnRerollUsed(int usedCount)
    {
        _battleRerollCount = usedCount;
    }

    // ========== 公共追踪接口 ==========

    /// <summary>记录Boss击杀</summary>
    public void TrackBossKill()
    {
        _totalBossKills++;
        TrackProgress("boss_kills", _totalBossKills);
    }

    /// <summary>记录敌人击杀</summary>
    public void TrackEnemyKill()
    {
        _totalEnemiesKilled++;
    }

    /// <summary>记录战斗开始（用于速通/无伤检测）</summary>
    public void TrackBattleStart(int aliveHeroCount)
    {
        _battleStartTime = Time.time;
        _battleRerollCount = 0;
        _currentBattleAliveHeroesStart = aliveHeroCount;
    }

    /// <summary>重置单局统计（新肉鸽局开始时调用）</summary>
    public void ResetRunStats()
    {
        _runRelicCount = 0;
        _runHeroCount = 0;
        _runMaxGold = 0;
        _battleRerollCount = 0;
    }

    // ========== 核心进度推进 ==========

    /// <summary>
    /// 统一的进度推进方法
    /// conditionTypeStr 对应 JSON 中的 condition_type
    /// </summary>
    public void TrackProgress(string conditionType, int value)
    {
        // 找到所有匹配此条件类型的成就
        foreach (var kvp in _defs)
        {
            var def = kvp.Value;
            if (def.condition_type != conditionType) continue;

            var progress = _progress[def.id];
            if (progress.is_unlocked) continue;

            // 更新进度（取最大值，适用于 level_reached / max_gold 等递增型）
            int oldValue = progress.current_value;
            progress.current_value = Mathf.Max(progress.current_value, value);

            if (progress.current_value != oldValue)
            {
                OnProgressChanged?.Invoke(def.id, progress.current_value, def.target_value);
            }

            // 检查是否达成
            if (progress.current_value >= def.target_value)
            {
                UnlockAchievement(def.id);
            }
        }
    }

    void UnlockAchievement(string achievementId)
    {
        if (!_progress.TryGetValue(achievementId, out var progress)) return;
        if (progress.is_unlocked) return;

        progress.is_unlocked = true;
        progress.unlock_timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var def = _defs[achievementId];
        Debug.Log($"[AchievementManager] 🏆 成就解锁: {def.name_cn} - {def.description}");

        OnAchievementUnlocked?.Invoke(achievementId);
        AutoSave();
    }

    // ========== 查询接口 ==========

    /// <summary>获取成就定义</summary>
    public AchievementDef GetDef(string achievementId)
    {
        _defs.TryGetValue(achievementId, out var def);
        return def;
    }

    /// <summary>获取所有成就定义</summary>
    public List<AchievementDef> GetAllDefs()
    {
        return _defs.Values.ToList();
    }

    /// <summary>按分类获取成就</summary>
    public List<AchievementDef> GetDefsByCategory(string category)
    {
        return _defs.Values.Where(d => d.category == category).ToList();
    }

    /// <summary>获取进度</summary>
    public AchievementProgress GetProgress(string achievementId)
    {
        _progress.TryGetValue(achievementId, out var p);
        return p;
    }

    /// <summary>获取进度当前值</summary>
    public int GetProgressValue(string achievementId)
    {
        if (_progress.TryGetValue(achievementId, out var p))
            return p.current_value;
        return 0;
    }

    /// <summary>获取所有进度</summary>
    public Dictionary<string, AchievementProgress> GetAllProgress()
    {
        return _progress;
    }

    /// <summary>成就是否已解锁</summary>
    public bool IsUnlocked(string achievementId)
    {
        return _progress.TryGetValue(achievementId, out var p) && p.is_unlocked;
    }

    /// <summary>奖励是否已领取</summary>
    public bool IsRewardsClaimed(string achievementId)
    {
        return _progress.TryGetValue(achievementId, out var p) && p.rewards_claimed;
    }

    /// <summary>获取解锁成就数量</summary>
    public int GetUnlockedCount()
    {
        return _progress.Values.Count(p => p.is_unlocked);
    }

    /// <summary>获取总成就数量</summary>
    public int GetTotalCount()
    {
        return _defs.Count;
    }

    /// <summary>获取指定稀有度颜色</summary>
    public string GetRarityColor(string rarity)
    {
        var config = ConfigLoader.LoadAchievements();
        if (config?.rarity_display != null && config.rarity_display.TryGetValue(rarity, out var entry))
            return entry.color;
        return "#FFFFFF";
    }

    // ========== 奖励领取 ==========

    /// <summary>
    /// 领取成就奖励 — 返回奖励列表，null 表示已领取或未解锁
    /// </summary>
    public List<AchievementReward> ClaimRewards(string achievementId)
    {
        if (!_progress.TryGetValue(achievementId, out var progress)) return null;
        if (!progress.is_unlocked || progress.rewards_claimed) return null;

        var def = _defs[achievementId];
        progress.rewards_claimed = true;

        // 发放奖励
        if (def.rewards != null)
        {
            foreach (var reward in def.rewards)
            {
                GrantReward(reward);
            }
        }

        Debug.Log($"[AchievementManager] 领取奖励: {def.name_cn}");
        OnRewardsClaimed?.Invoke(achievementId);
        AutoSave();
        return def.rewards;
    }

    /// <summary>一键领取所有可领取奖励</summary>
    public int ClaimAllPendingRewards()
    {
        int count = 0;
        foreach (var kvp in _progress)
        {
            if (kvp.Value.is_unlocked && !kvp.Value.rewards_claimed)
            {
                ClaimRewards(kvp.Key);
                count++;
            }
        }
        return count;
    }

    void GrantReward(AchievementReward reward)
    {
        switch (reward.type)
        {
            case "gold":
                var inv = PlayerInventory.Instance;
                if (inv != null)
                {
                    inv.AddGold(reward.value);
                    Debug.Log($"[AchievementManager] 奖励金币 +{reward.value}");
                }
                break;

            case "relic":
                // TODO: 按 item_id 查找遗物数据并发放
                Debug.Log($"[AchievementManager] 奖励遗物: {reward.item_id}");
                break;

            case "dice_face":
                // TODO: 解锁骰子面
                Debug.Log($"[AchievementManager] 奖励骰子面: {reward.item_id}");
                break;

            default:
                Debug.LogWarning($"[AchievementManager] 未知奖励类型: {reward.type}");
                break;
        }
    }

    // ========== 持久化 ==========

    /// <summary>存档数据结构（用于序列化到 PlayerPrefs）</summary>
    [System.Serializable]
    private class AchievementSaveData
    {
        public List<AchievementProgress> achievements = new List<AchievementProgress>();
        public int totalBossKills;
        public int totalBattleWins;
        public int totalPerfectVictories;
        public int totalEnemiesKilled;
        public int totalDiceCombos;
        public int dicePairCount;
        public int diceStraightCount;
        public int diceTripleCount;
    }

    void AutoSave()
    {
        var saveData = new AchievementSaveData
        {
            achievements = _progress.Values.ToList(),
            totalBossKills = _totalBossKills,
            totalBattleWins = _totalBattleWins,
            totalPerfectVictories = _totalPerfectVictories,
            totalEnemiesKilled = _totalEnemiesKilled,
            totalDiceCombos = _totalDiceCombos,
            dicePairCount = _dicePairCount,
            diceStraightCount = _diceStraightCount,
            diceTripleCount = _diceTripleCount
        };

        string json = JsonUtility.ToJson(saveData, false);
        PlayerPrefs.SetString(ACHIEVEMENT_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(ACHIEVEMENT_SAVE_KEY)) return;

        string json = PlayerPrefs.GetString(ACHIEVEMENT_SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<AchievementSaveData>(json);
        if (saveData == null) return;

        // 恢复累计统计
        _totalBossKills = saveData.totalBossKills;
        _totalBattleWins = saveData.totalBattleWins;
        _totalPerfectVictories = saveData.totalPerfectVictories;
        _totalEnemiesKilled = saveData.totalEnemiesKilled;
        _totalDiceCombos = saveData.totalDiceCombos;
        _dicePairCount = saveData.dicePairCount;
        _diceStraightCount = saveData.diceStraightCount;
        _diceTripleCount = saveData.diceTripleCount;

        // 恢复各成就进度
        foreach (var ach in saveData.achievements)
        {
            if (_progress.ContainsKey(ach.achievement_id))
            {
                _progress[ach.achievement_id] = ach;
            }
        }

        Debug.Log($"[AchievementManager] 已加载进度: {_progress.Count(p => p.Value.is_unlocked)}/{_defs.Count} 已解锁");
    }

    /// <summary>重置所有成就（调试用）</summary>
    public void ResetAll()
    {
        _totalBossKills = 0;
        _totalBattleWins = 0;
        _totalPerfectVictories = 0;
        _totalEnemiesKilled = 0;
        _totalDiceCombos = 0;
        _dicePairCount = 0;
        _diceStraightCount = 0;
        _diceTripleCount = 0;

        foreach (var key in _defs.Keys.ToList())
        {
            _progress[key] = new AchievementProgress
            {
                achievement_id = key,
                current_value = 0,
                is_unlocked = false,
                unlock_timestamp = 0,
                rewards_claimed = false
            };
        }

        PlayerPrefs.DeleteKey(ACHIEVEMENT_SAVE_KEY);
        Debug.Log("[AchievementManager] 所有成就已重置");
    }

    // ========== 辅助方法 ==========

    bool CheckPerfectVictory()
    {
        var rgm = RoguelikeGameManager.Instance;
        if (rgm == null) return false;

        foreach (var hero in rgm.PlayerHeroes)
        {
            if (hero != null && hero.CurrentHealth < hero.MaxHealth)
                return false;
        }
        return true;
    }

    int GetAliveHeroCount()
    {
        var rgm = RoguelikeGameManager.Instance;
        if (rgm == null) return 0;
        return rgm.PlayerHeroes.Count(h => h != null && h.CurrentHealth > 0);
    }

    int GetCurrentRelicCount()
    {
        var rgm = RoguelikeGameManager.Instance;
        if (rgm?.RelicSystem == null) return 0;
        return rgm.RelicSystem.OwnedRelics.Count;
    }

    int GetCurrentHeroCount()
    {
        var rgm = RoguelikeGameManager.Instance;
        if (rgm == null) return 0;
        return rgm.PlayerHeroes.Count;
    }

    int GetCurrentGold()
    {
        var inv = PlayerInventory.Instance;
        return inv?.Gold ?? 0;
    }
}
