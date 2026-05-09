using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡生成器 — 为肉鸽模式生成敌人、提供关卡配置
/// 包装 LevelManager 的功能，适配 RoguelikeGameManager 的接口
/// CTO: RoguelikeGameManager 改用已有的 LevelManager（不用不存在的LevelGenerator）
/// </summary>
public class LevelGenerator
{
    /// <summary>
    /// 根据关卡编号生成敌人列表
    /// </summary>
    public List<Hero> GenerateEnemies(int levelId)
    {
        var levelMgr = LevelManager.Instance;
        if (levelMgr == null)
        {
            Debug.LogError("[LevelGenerator] LevelManager.Instance 为空，无法生成敌人");
            return new List<Hero>();
        }

        levelMgr.LoadLevel(levelId);
        return levelMgr.SpawnEnemies();
    }

    /// <summary>
    /// 获取关卡配置信息
    /// </summary>
    public LevelConfig GetLevelConfig(int levelId)
    {
        var levelMgr = LevelManager.Instance;
        if (levelMgr == null) return null;

        var config = levelMgr.GetLevelConfig(levelId);
        if (config == null)
        {
            // 触发 LevelManager 的默认关卡创建
            levelMgr.LoadLevel(levelId);
            config = levelMgr.CurrentLevel;
        }
        return config;
    }
}
