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
    /// 创建默认关卡（缺少配置时使用代码数据）
    /// </summary>
    private LevelConfig CreateDefaultLevel(int levelId)
    {
        var config = ScriptableObject.CreateInstance<LevelConfig>();
        config.levelId = levelId;
        config.levelName = $"第{levelId}关";

        switch (levelId)
        {
            case 1:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyGrunt(), gridPosition = new Vector2Int(0, 3) });
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyGrunt(), gridPosition = new Vector2Int(2, 3) });
                config.goldReward = 20;
                config.rewardCards.Add(GameData.CreatePowerTrainingCard());
                break;
            case 2:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyElite(), gridPosition = new Vector2Int(1, 3) });
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyGrunt(), gridPosition = new Vector2Int(0, 3) });
                config.goldReward = 30;
                config.rewardCards.Add(GameData.CreateSlashCard());
                break;
            case 3:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyBoss(), gridPosition = new Vector2Int(1, 3) });
                config.goldReward = 50;
                config.rewardCards.Add(GameData.CreateShieldBashCard());
                break;
            case 4:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyBomber(), gridPosition = new Vector2Int(0, 3) });
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyElite(), gridPosition = new Vector2Int(2, 3) });
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyGrunt(), gridPosition = new Vector2Int(1, 3) });
                config.goldReward = 40;
                config.rewardCards.Add(GameData.CreateFlameSlashCard());
                break;
            case 5:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyBoss(), gridPosition = new Vector2Int(0, 3) });
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyHealer(), gridPosition = new Vector2Int(2, 3) });
                config.goldReward = 80;
                config.rewardCards.Add(GameData.CreateEvolutionAwakenCard());
                break;
            default:
                config.enemyWaves.Add(new EnemyWave { enemyData = GameData.CreateEnemyBoss(), gridPosition = new Vector2Int(1, 3) });
                config.goldReward = 50 + levelId * 10;
                config.rewardCards.Add(GameData.CreatePowerTrainingCard());
                break;
        }

        return config;
    }
}
