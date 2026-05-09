using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡管理器 — 加载关卡配置、生成敌人、发放奖励
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("关卡配置列表")]
    public List<LevelConfig> levelConfigs = new List<LevelConfig>();

    [Header("当前关卡")]
    public LevelConfig CurrentLevel { get; private set; }

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
    /// 根据关卡ID获取配置
    /// </summary>
    public LevelConfig GetLevelConfig(int levelId)
    {
        foreach (var config in levelConfigs)
        {
            if (config.levelId == levelId)
                return config;
        }
        return null;
    }

    /// <summary>
    /// 加载当前关卡
    /// </summary>
    public void LoadLevel(int levelId)
    {
        CurrentLevel = GetLevelConfig(levelId);
        if (CurrentLevel == null)
        {
            Debug.LogWarning($"未找到关卡 {levelId} 配置，使用默认数据");
            CurrentLevel = CreateDefaultLevel(levelId);
        }
        Debug.Log($"加载关卡: {CurrentLevel.levelName}");
    }

    /// <summary>
    /// 根据当前关卡配置生成敌人
    /// </summary>
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
            enemies.Add(enemy);
            index++;
        }

        Debug.Log($"生成敌人: {enemies.Count}");
        return enemies;
    }

    /// <summary>
    /// 发放通关奖励
    /// </summary>
    public void GrantRewards()
    {
        if (CurrentLevel == null) return;

        var deck = CardDeck.Instance;
        if (deck == null) return;

        foreach (var cardData in CurrentLevel.rewardCards)
        {
            if (cardData == null) continue;
            deck.AddCard(new CardInstance(cardData));
        }

        Debug.Log($"获得奖励: {CurrentLevel.goldReward} 金币 + {CurrentLevel.rewardCards.Count} 张卡牌");
    }

    /// <summary>
    /// 创建默认关卡（从 levels.json + enemies.json 动态生成，去硬编码）
    /// 逻辑：根据关卡ID匹配tier → 从敌人池按权重随机选敌 → boss关特殊处理
    /// </summary>
    private LevelConfig CreateDefaultLevel(int levelId)
    {
        var config = ScriptableObject.CreateInstance<LevelConfig>();
        config.levelId = levelId;
        config.levelName = $"第{levelId}关";

        // 1. 从 levels.json 获取当前关卡的 tier 配置
        LevelTierEntry tier = BalanceProvider.GetLevel(levelId);

        if (tier != null && tier.enemy_pool != null && tier.enemy_pool.Count > 0)
        {
            // 2. 确定敌人数量
            int enemyCount = CalculateEnemyCount(levelId, tier);

            // 3. boss 关卡特殊处理
            bool isBossLevel = IsBossLevel(levelId);
            if (isBossLevel)
            {
                SpawnBossEnemies(config, levelId, tier);
            }
            else
            {
                // 4. 从敌人池按权重随机选取
                SpawnEnemiesFromPool(config, tier.enemy_pool, enemyCount, levelId);
            }
        }
        else
        {
            // fallback: 至少1个基础敌人
            config.enemyWaves.Add(new EnemyWave
            {
                enemyData = GameData.CreateEnemyGrunt(levelId),
                gridPosition = new Vector2Int(1, 3)
            });
        }

        // 5. 金币奖励（JSON驱动）
        config.goldReward = BalanceProvider.GetGoldReward(levelId);

        // 6. 奖励卡牌（随机1张，从奖励池选取）
        var rewardCard = GameData.GetRandomRewardCard();
        if (rewardCard != null)
            config.rewardCards.Add(rewardCard);

        return config;
    }

    /// <summary>
    /// 计算敌人数量：从 levels.json 的 enemy_count_formula 或 tier.max_enemies 读取
    /// </summary>
    private int CalculateEnemyCount(int levelId, LevelTierEntry tier)
    {
        // 先从 tier 的 max_enemies 获取上限
        int maxEnemies = tier.max_enemies > 0 ? tier.max_enemies : 1;

        // 使用 enemies.json 的 enemy_count_formula 规则
        var enemiesConfig = BalanceProvider.Enemies;
        if (enemiesConfig?.enemy_count_formula != null)
        {
            var formula = enemiesConfig.enemy_count_formula;
            int count;
            if (levelId <= 2) count = formula.level_1_to_2;
            else if (levelId <= 5) count = formula.level_3_to_5;
            else if (levelId <= 8) count = formula.level_6_to_8;
            else count = formula.level_9_plus;

            return Mathf.Min(count, maxEnemies);
        }

        // fallback
        return Mathf.Min(1 + (levelId - 1) / 3, maxEnemies);
    }

    /// <summary>
    /// 判断是否为 Boss 关卡：从 levels.json 的 boss_levels 读取
    /// </summary>
    private bool IsBossLevel(int levelId)
    {
        var levels = BalanceProvider.Levels;
        if (levels?.boss_levels != null)
            return levels.boss_levels.Contains(levelId);

        // fallback
        return levelId == 5 || levelId == 10 || levelId == 15;
    }

    /// <summary>
    /// Boss 关卡敌人生成：从 levels.json 的 boss_config 读取 boss 类型 + 随从
    /// </summary>
    private void SpawnBossEnemies(LevelConfig config, int levelId, LevelTierEntry tier)
    {
        var levels = BalanceProvider.Levels;
        BossConfigEntry bossCfg = null;

        // 匹配 boss_config（先精确匹配 level_N，再匹配 level_N_plus）
        if (levels?.boss_config != null)
        {
            string key = $"level_{levelId}";
            if (levels.boss_config.TryGetValue(key, out BossConfigEntry entry))
            {
                bossCfg = entry;
            }
            else
            {
                // 查找 level_N_plus 通配
                foreach (var kvp in levels.boss_config)
                {
                    if (kvp.Key.EndsWith("_plus"))
                    {
                        string prefix = kvp.Key.Replace("_plus", "");
                        if (key.StartsWith(prefix))
                        {
                            bossCfg = kvp.Value;
                            break;
                        }
                    }
                }
            }
        }

        string bossTypeId = bossCfg?.boss_type ?? "boss_standard";
        int addsCount = bossCfg?.adds ?? 1;

        // 生成 Boss
        var bossData = GameData.CreateEnemyByJsonId(bossTypeId, levelId);
        if (bossData == null) bossData = GameData.CreateEnemyBoss(levelId);
        config.enemyWaves.Add(new EnemyWave
        {
            enemyData = bossData,
            gridPosition = new Vector2Int(1, 3)
        });

        // 生成 Boss 随从（从 tier 的敌人池随机选）
        if (tier.enemy_pool != null && tier.enemy_pool.Count > 0)
        {
            for (int i = 0; i < addsCount; i++)
            {
                string minionId = PickRandomFromPool(tier.enemy_pool);
                var minionData = GameData.CreateEnemyByJsonId(minionId, levelId);
                if (minionData == null) minionData = GameData.CreateEnemyGrunt(levelId);
                config.enemyWaves.Add(new EnemyWave
                {
                    enemyData = minionData,
                    gridPosition = new Vector2Int(i % 2 == 0 ? 0 : 2, 3)
                });
            }
        }
    }

    /// <summary>
    /// 从敌人池按权重随机选取并生成敌人
    /// </summary>
    private void SpawnEnemiesFromPool(LevelConfig config, List<string> pool, int count, int levelId)
    {
        var enemiesConfig = BalanceProvider.Enemies;
        int[] xPositions = { 0, 2, 1, 0 }; // 敌人散开站位

        for (int i = 0; i < count; i++)
        {
            // 按 spawn_weight 加权随机
            string enemyId = PickWeightedEnemy(pool, enemiesConfig, levelId);
            if (enemyId == null) enemyId = pool[0];

            // 尝试是否生成精英（从 tier 配置读取）
            bool makeElite = false;
            var tier = BalanceProvider.GetLevel(levelId);
            if (tier != null && tier.allow_elite && UnityEngine.Random.value < tier.elite_chance)
            {
                // 用精英替换当前敌人
                enemyId = "elite";
                makeElite = true;
            }

            var enemyData = GameData.CreateEnemyByJsonId(enemyId, levelId);
            if (enemyData == null) enemyData = GameData.CreateEnemyGrunt(levelId);

            config.enemyWaves.Add(new EnemyWave
            {
                enemyData = enemyData,
                gridPosition = new Vector2Int(xPositions[i % xPositions.Length], 3)
            });
        }
    }

    /// <summary>
    /// 按 spawn_weight 加权随机从敌人池中选取敌人ID
    /// </summary>
    private string PickWeightedEnemy(List<string> pool, EnemiesConfig config, int levelId)
    {
        if (config?.enemy_types == null || pool.Count == 0)
            return pool.Count > 0 ? pool[0] : "minion";

        // 收集池中有效敌人的权重
        var candidates = new List<(string id, int weight)>();
        int totalWeight = 0;

        foreach (string poolId in pool)
        {
            var entry = config.enemy_types.Find(e => e.id == poolId);
            if (entry != null && levelId >= entry.min_level)
            {
                candidates.Add((entry.id, entry.spawn_weight));
                totalWeight += entry.spawn_weight;
            }
        }

        if (candidates.Count == 0)
            return pool[0];

        // 加权随机
        int roll = UnityEngine.Random.Range(0, totalWeight);
        int accumulated = 0;
        foreach (var (id, weight) in candidates)
        {
            accumulated += weight;
            if (roll < accumulated)
                return id;
        }

        return candidates[candidates.Count - 1].id;
    }

    /// <summary>
    /// 从池中随机选一个（等概率）
    /// </summary>
    private string PickRandomFromPool(List<string> pool)
    {
        if (pool == null || pool.Count == 0) return "minion";
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }
}
