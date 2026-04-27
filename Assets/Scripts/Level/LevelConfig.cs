using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡敌人波次配置
/// </summary>
[System.Serializable]
public class EnemyWave
{
    public HeroData enemyData;
    public Vector2Int gridPosition = new Vector2Int(0, 3);
}

/// <summary>
/// 关卡配置数据
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Game/Level Config")]
public class LevelConfig : ScriptableObject
{
    public int levelId;
    public string levelName;
    [TextArea]
    public string description;

    [Header("敌人配置")]
    public List<EnemyWave> enemyWaves = new List<EnemyWave>();

    [Header("胜利奖励")]
    public List<CardData> rewardCards = new List<CardData>();
    public int goldReward = 10;
}
