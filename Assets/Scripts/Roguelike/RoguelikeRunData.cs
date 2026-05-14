using System;
using System.Collections.Generic;

/// <summary>
/// 肉鸽单次运行数据 — 支持序列化以实现中途退出恢复
/// </summary>
[System.Serializable]
public class RoguelikeRunData
{
    // === 基础字段 ===
    public int currentFloor;
    public List<string> selectedHeroes = new List<string>();
    public List<string> ownedRelics = new List<string>();
    public int currentGold;
    public int shopLevel;
    public int seed;
    public List<int> visitedNodes = new List<int>();

    /// <summary>英雄ID → 当前血量（JsonUtility不支持Dictionary，使用辅助列表序列化）</summary>
    public Dictionary<string, int> currentPlayerHP = new Dictionary<string, int>();

    // === P0 补全：卡牌/装备/骰子/英雄星级 ===

    /// <summary>卡牌ID列表（cardName_starLevel 格式）</summary>
    public List<string> ownedCards = new List<string>();

    /// <summary>装备名称列表</summary>
    public List<string> ownedEquipments = new List<string>();

    /// <summary>骰子面值数据（骰子索引 → 面值数组，扁平化存储）</summary>
    public List<int> diceFaces = new List<int>();

    /// <summary>骰子面效果数据（骰子索引 × 面索引 → 效果ID，空串表示无效果）</summary>
    public List<string> diceFaceEffects = new List<string>();

    /// <summary>骰子数量</summary>
    public int diceCount;

    /// <summary>每个骰子的面数</summary>
    public int diceSides;

    /// <summary>英雄星级（英雄ID → 星级）</summary>
    public Dictionary<string, int> heroStarLevels = new Dictionary<string, int>();

    /// <summary>英雄等级（英雄ID → 等级）</summary>
    public Dictionary<string, int> heroLevels = new Dictionary<string, int>();

    // --- 序列化辅助：JsonUtility 不支持 Dictionary，需要用 List 模拟 ---
    [Serializable]
    public struct StringIntPair
    {
        public string key;
        public int value;
        public StringIntPair(string k, int v) { key = k; value = v; }
    }

    public List<StringIntPair> _hpPairs = new List<StringIntPair>();
    public List<StringIntPair> _starLevelPairs = new List<StringIntPair>();
    public List<StringIntPair> _heroLevelPairs = new List<StringIntPair>();

    /// <summary>序列化前调用：将所有 Dictionary 转为 List</summary>
    public void BeforeSerialize()
    {
        _hpPairs.Clear();
        foreach (var kv in currentPlayerHP)
            _hpPairs.Add(new StringIntPair(kv.Key, kv.Value));

        _starLevelPairs.Clear();
        foreach (var kv in heroStarLevels)
            _starLevelPairs.Add(new StringIntPair(kv.Key, kv.Value));

        _heroLevelPairs.Clear();
        foreach (var kv in heroLevels)
            _heroLevelPairs.Add(new StringIntPair(kv.Key, kv.Value));
    }

    /// <summary>反序列化后调用：将所有 List 还原为 Dictionary</summary>
    public void AfterDeserialize()
    {
        currentPlayerHP.Clear();
        foreach (var pair in _hpPairs)
            currentPlayerHP[pair.key] = pair.value;

        heroStarLevels.Clear();
        foreach (var pair in _starLevelPairs)
            heroStarLevels[pair.key] = pair.value;

        heroLevels.Clear();
        foreach (var pair in _heroLevelPairs)
            heroLevels[pair.key] = pair.value;
    }
}
