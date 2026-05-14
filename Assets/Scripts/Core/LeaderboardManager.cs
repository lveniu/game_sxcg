using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// LeaderboardManager — 排行榜管理器（单例）
// 微信小游戏使用 WX.getUserCloudStorage / getFriendCloudStorage
// 非微信环境使用本地模拟数据（PlayerPrefs）
// ============================================================

/// <summary>
/// 排行榜条目
/// </summary>
[Serializable]
public class LeaderboardEntry
{
    public string playerName;       // 玩家昵称
    public string openId;           // 微信 openId（或本地生成的唯一ID）
    public long score;              // 综合分数（用于排序）
    public int levelReached;        // 到达层数
    public int totalKills;          // 击杀数
    public long totalDamage;        // 伤害总量
    public int relicCount;          // 遗物数
    public int maxWinStreak;        // 最高连胜
    public long timestamp;          // 提交时间戳

    /// <summary>格式化显示文本</summary>
    public string FormatDetail()
    {
        return $"层数{levelReached} | 击杀{totalKills} | 伤害{totalDamage:N0}";
    }
}

/// <summary>
/// 排行榜数据容器（JsonUtility序列化用）
/// </summary>
[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}

/// <summary>
/// 排行榜类型
/// </summary>
public enum LeaderboardType
{
    Friends,    // 好友排行
    Global      // 全局排行
}

/// <summary>
/// 排行榜管理器 — 单例
/// 职责：
/// 1. 接收 RunStats 提交分数
/// 2. 拉取好友/全局排行数据
/// 3. 微信环境调用云存储API，非微信环境本地模拟
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    // ========== 配置 ==========

    /// <summary>排行榜云存储key</summary>
    private const string LEADERBOARD_KEY = "leaderboard";
    private const string BEST_SCORE_KEY = "best_score";

    /// <summary>排行榜最大条目数</summary>
    private const int MAX_ENTRIES = 10;

    /// <summary>本地模拟玩家名称池</summary>
    private static readonly string[] MOCK_NAMES = new[]
    {
        "勇者小明", "骰子大师", "战棋之王", "无尽之刃",
        "幸运星", "肉鸽达人", "棋逢对手", "命运之手",
        "暴击之王", "连胜将军"
    };

    // ========== 本地缓存 ==========

    /// <summary>本地缓存的好友排行</summary>
    private List<LeaderboardEntry> cachedFriendLeaderboard = new List<LeaderboardEntry>();

    /// <summary>本地缓存的全局排行</summary>
    private List<LeaderboardEntry> cachedGlobalLeaderboard = new List<LeaderboardEntry>();

    /// <summary>当前玩家ID（首次运行生成，持久化到PlayerPrefs）</summary>
    private string localPlayerId;

    /// <summary>当前玩家昵称</summary>
    private string localPlayerName = "玩家";

    // ========== 事件 ==========

    /// <summary>排行榜数据刷新完成</summary>
    public event Action<LeaderboardType, List<LeaderboardEntry>> OnLeaderboardLoaded;

    /// <summary>分数提交完成</summary>
    public event Action<bool, long> OnScoreSubmitted;

    // ========== 生命周期 ==========

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitLocalIdentity();
        LoadLocalLeaderboard();
    }

    // ========== 公共接口 ==========

    /// <summary>
    /// 提交本局分数（传入 RunBattleStats 数据）
    /// 综合评分 = 层数×1000 + 击杀×50 + 伤害/100 + 连胜×200 + 遗物×150
    /// </summary>
    /// <param name="runStats">本局战斗统计</param>
    /// <param name="levelReached">到达层数</param>
    /// <param name="relicCount">收集遗物数</param>
    public void SubmitScore(RunBattleStats runStats, int levelReached, int relicCount)
    {
        if (runStats == null)
        {
            Debug.LogWarning("[LeaderboardManager] runStats为空，跳过提交");
            OnScoreSubmitted?.Invoke(false, 0);
            return;
        }

        // 计算综合分数
        long score = CalculateScore(runStats, levelReached, relicCount);

        // 构建排行榜条目
        var entry = new LeaderboardEntry
        {
            playerName = localPlayerName,
            openId = localPlayerId,
            score = score,
            levelReached = levelReached,
            totalKills = runStats.totalKills,
            totalDamage = runStats.totalDamageDealt,
            relicCount = relicCount,
            maxWinStreak = runStats.maxConsecutiveWins,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        Debug.Log($"[LeaderboardManager] 提交分数: {score} (层数={levelReached}, 击杀={runStats.totalKills}, 伤害={runStats.totalDamageDealt})");

        // 更新最高分
        long bestScore = GetBestScore();
        if (score > bestScore)
        {
            SetBestScore(score);
            Debug.Log($"[LeaderboardManager] 新最高分! {bestScore} → {score}");
        }

        // 微信环境：上传到云存储
        if (WechatMiniGameAdapter.IsWechatEnvironment)
        {
            SubmitToWechatCloud(entry);
        }
        else
        {
            // 非微信环境：更新本地模拟排行榜
            SubmitToLocal(entry);
        }

        OnScoreSubmitted?.Invoke(true, score);
    }

    /// <summary>
    /// 获取排行榜数据
    /// </summary>
    /// <param name="type">排行榜类型（好友/全局）</param>
    /// <param name="callback">回调返回排行榜列表</param>
    public void GetLeaderboard(LeaderboardType type, Action<List<LeaderboardEntry>> callback)
    {
        if (WechatMiniGameAdapter.IsWechatEnvironment)
        {
            GetWechatLeaderboard(type, callback);
        }
        else
        {
            // 非微信环境：返回本地模拟数据
            var cached = type == LeaderboardType.Friends
                ? cachedFriendLeaderboard
                : cachedGlobalLeaderboard;

            // 模拟异步延迟
            StartCoroutine(GetOrCreateRunner().SimulateAsync(cached, callback, 0.3f));
        }
    }

    /// <summary>
    /// 获取历史最高分
    /// </summary>
    public long GetBestScore()
    {
        return PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);
    }

    /// <summary>
    /// 获取当前玩家ID
    /// </summary>
    public string GetPlayerId() => localPlayerId;

    /// <summary>
    /// 获取当前玩家昵称
    /// </summary>
    public string GetPlayerName() => localPlayerName;

    /// <summary>
    /// 设置玩家昵称（登录后由外部调用）
    /// </summary>
    public void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        localPlayerName = name;
        PlayerPrefs.SetString("lb_player_name", name);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 设置玩家OpenId（微信登录后由外部调用）
    /// </summary>
    public void SetPlayerOpenId(string openId)
    {
        if (string.IsNullOrEmpty(openId)) return;
        localPlayerId = openId;
        PlayerPrefs.SetString("lb_player_id", openId);
        PlayerPrefs.Save();
    }

    // ========== 分数计算 ==========

    /// <summary>
    /// 综合评分公式
    /// 层数权重最高，击杀和伤害次之
    /// </summary>
    private long CalculateScore(RunBattleStats runStats, int levelReached, int relicCount)
    {
        long score = 0;
        score += (long)levelReached * 1000;
        score += runStats.totalKills * 50L;
        score += runStats.totalDamageDealt / 100;
        score += runStats.maxConsecutiveWins * 200L;
        score += relicCount * 150L;
        return score;
    }

    // ========== 本地排行榜逻辑 ==========

    /// <summary>
    /// 初始化本地身份（首次运行生成随机ID和昵称）
    /// </summary>
    private void InitLocalIdentity()
    {
        localPlayerId = PlayerPrefs.GetString("lb_player_id", "");
        if (string.IsNullOrEmpty(localPlayerId))
        {
            localPlayerId = "local_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString("lb_player_id", localPlayerId);
        }

        localPlayerName = PlayerPrefs.GetString("lb_player_name", "");
        if (string.IsNullOrEmpty(localPlayerName))
        {
            localPlayerName = "勇者" + UnityEngine.Random.Range(1000, 9999);
            PlayerPrefs.SetString("lb_player_name", localPlayerName);
        }
    }

    /// <summary>
    /// 加载本地排行榜（PlayerPrefs → JsonUtility）
    /// </summary>
    private void LoadLocalLeaderboard()
    {
        // 加载好友排行（本地只有自己）
        string friendJson = PlayerPrefs.GetString("lb_friends", "");
        if (!string.IsNullOrEmpty(friendJson))
        {
            var friendData = JsonUtility.FromJson<LeaderboardData>(friendJson);
            if (friendData != null)
                cachedFriendLeaderboard = friendData.entries;
        }

        // 加载全局排行
        string globalJson = PlayerPrefs.GetString("lb_global", "");
        if (!string.IsNullOrEmpty(globalJson))
        {
            var globalData = JsonUtility.FromJson<LeaderboardData>(globalJson);
            if (globalData != null)
                cachedGlobalLeaderboard = globalData.entries;
        }

        // 如果全局排行是空的，生成模拟数据
        if (cachedGlobalLeaderboard.Count == 0)
        {
            GenerateMockGlobalLeaderboard();
        }
    }

    /// <summary>
    /// 保存本地排行榜到 PlayerPrefs
    /// </summary>
    private void SaveLocalLeaderboard()
    {
        var friendData = new LeaderboardData { entries = cachedFriendLeaderboard };
        PlayerPrefs.SetString("lb_friends", JsonUtility.ToJson(friendData));

        var globalData = new LeaderboardData { entries = cachedGlobalLeaderboard };
        PlayerPrefs.SetString("lb_global", JsonUtility.ToJson(globalData));

        PlayerPrefs.Save();
    }

    /// <summary>
    /// 提交到本地排行榜
    /// </summary>
    private void SubmitToLocal(LeaderboardEntry entry)
    {
        // 更新好友排行（本地只有自己，但保留接口一致性）
        UpdateLeaderboardList(cachedFriendLeaderboard, entry);
        UpdateLeaderboardList(cachedGlobalLeaderboard, entry);

        SaveLocalLeaderboard();
    }

    /// <summary>
    /// 更新排行榜列表：替换同ID记录或插入，截取Top N
    /// </summary>
    private void UpdateLeaderboardList(List<LeaderboardEntry> list, LeaderboardEntry entry)
    {
        // 查找是否已有该玩家记录
        int existingIdx = list.FindIndex(e => e.openId == entry.openId);
        if (existingIdx >= 0)
        {
            // 只保留更高分
            if (entry.score > list[existingIdx].score)
            {
                list[existingIdx] = entry;
            }
        }
        else
        {
            list.Add(entry);
        }

        // 按分数降序排序
        list.Sort((a, b) => b.score.CompareTo(a.score));

        // 截取Top N
        if (list.Count > MAX_ENTRIES)
            list.RemoveRange(MAX_ENTRIES, list.Count - MAX_ENTRIES);
    }

    /// <summary>
    /// 生成模拟全局排行榜数据（首次运行时）
    /// </summary>
    private void GenerateMockGlobalLeaderboard()
    {
        cachedGlobalLeaderboard.Clear();

        for (int i = 0; i < MAX_ENTRIES; i++)
        {
            var entry = new LeaderboardEntry
            {
                playerName = MOCK_NAMES[i % MOCK_NAMES.Length],
                openId = "mock_" + i,
                score = (MAX_ENTRIES - i) * 1500L + UnityEngine.Random.Range(-200, 200),
                levelReached = UnityEngine.Random.Range(5, 30),
                totalKills = UnityEngine.Random.Range(20, 100),
                totalDamage = UnityEngine.Random.Range(5000L, 50000L),
                relicCount = UnityEngine.Random.Range(1, 10),
                maxWinStreak = UnityEngine.Random.Range(2, 10),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - UnityEngine.Random.Range(0, 86400 * 7)
            };
            cachedGlobalLeaderboard.Add(entry);
        }

        // 按分数降序排序
        cachedGlobalLeaderboard.Sort((a, b) => b.score.CompareTo(a.score));

        SaveLocalLeaderboard();
        Debug.Log($"[LeaderboardManager] 已生成{MAX_ENTRIES}条模拟全局排行数据");
    }

    /// <summary>
    /// 保存/读取最高分
    /// </summary>
    private void SetBestScore(long score)
    {
        PlayerPrefs.SetInt(BEST_SCORE_KEY, (int)Math.Min(score, int.MaxValue));
        PlayerPrefs.Save();
    }

    // ========== 微信云存储逻辑 ==========

    /// <summary>
    /// 提交分数到微信云存储
    /// 使用 WX.setUserCloudStorage
    /// </summary>
    private void SubmitToWechatCloud(LeaderboardEntry entry)
    {
        // 序列化排行榜数据
        string entryJson = JsonUtility.ToJson(entry);

        // 微信云存储使用KV模式，key=leaderboard，value=JSON数据
        WechatMiniGameAdapter.SetCloudData(LEADERBOARD_KEY, entryJson);

        Debug.Log($"[LeaderboardManager] 已提交到微信云存储: score={entry.score}");
    }

    /// <summary>
    /// 从微信获取排行榜数据
    /// 好友排行：getFriendCloudStorage
    /// 全局排行：getGroupCloudStorage（需要开放数据域）
    /// </summary>
    private void GetWechatLeaderboard(LeaderboardType type, Action<List<LeaderboardEntry>> callback)
    {
        Debug.Log($"[LeaderboardManager] 获取微信排行榜: {type}");

        // 微信开放数据域需要通过jslib调用
        // 这里先尝试从云存储读取，回退到本地缓存
        string cloudKey = type == LeaderboardType.Friends ? "lb_friends_cloud" : "lb_global_cloud";

        WechatMiniGameAdapter.GetCloudData(cloudKey, (json) =>
        {
            List<LeaderboardEntry> result = new List<LeaderboardEntry>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<LeaderboardData>(json);
                    if (data != null && data.entries != null)
                        result = data.entries;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LeaderboardManager] 解析微信排行榜数据失败: {e.Message}");
                }
            }

            // 回退到本地缓存
            if (result.Count == 0)
            {
                result = type == LeaderboardType.Friends
                    ? cachedFriendLeaderboard
                    : cachedGlobalLeaderboard;
            }

            OnLeaderboardLoaded?.Invoke(type, result);
            callback?.Invoke(result);
        });
    }

    // ========== 辅助类 ==========

    /// <summary>
    /// 协程运行器（用于模拟异步操作）
    /// </summary>
    private LeaderboardRunner _runner;

    private LeaderboardRunner GetOrCreateRunner()
    {
        if (_runner != null) return _runner;

        var go = GameObject.Find("[LeaderboardManager]");
        if (go == null)
        {
            // 使用当前GameObject
            go = gameObject;
        }

        _runner = GetComponent<LeaderboardRunner>();
        if (_runner == null)
            _runner = gameObject.AddComponent<LeaderboardRunner>();

        return _runner;
    }

    /// <summary>
    /// 协程运行器（内部类）
    /// </summary>
    private class LeaderboardRunner : MonoBehaviour
    {
        public System.Collections.IEnumerator SimulateAsync<T>(T data, Action<T> callback, float delay)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke(data);
        }
    }
}
