using System;
using System.Collections.Generic;

/// <summary>
/// 肉鸽单次运行数据 — 支持序列化以实现中途退出恢复
/// </summary>
[System.Serializable]
public class RoguelikeRunData
{
    public int currentFloor;
    public List<string> selectedHeroes = new List<string>();
    public List<string> ownedRelics = new List<string>();
    public int currentGold;
    public int shopLevel;
    public int seed;
    public List<int> visitedNodes = new List<int>();

    /// <summary>英雄ID → 当前血量（JsonUtility不支持Dictionary，使用辅助列表序列化）</summary>
    public Dictionary<string, int> currentPlayerHP = new Dictionary<string, int>();

    // --- 序列化辅助：JsonUtility 不支持 Dictionary，需要用 List 模拟 ---
    [Serializable]
    public struct StringIntPair
    {
        public string key;
        public int value;
        public StringIntPair(string k, int v) { key = k; value = v; }
    }

    public List<StringIntPair> _hpPairs = new List<StringIntPair>();

    /// <summary>序列化前调用：将 Dictionary 转为 List</summary>
    public void BeforeSerialize()
    {
        _hpPairs.Clear();
        foreach (var kv in currentPlayerHP)
            _hpPairs.Add(new StringIntPair(kv.Key, kv.Value));
    }

    /// <summary>反序列化后调用：将 List 还原为 Dictionary</summary>
    public void AfterDeserialize()
    {
        currentPlayerHP.Clear();
        foreach (var pair in _hpPairs)
            currentPlayerHP[pair.key] = pair.value;
    }
}
