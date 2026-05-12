using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// BattleStatsData — 战斗统计数据结构（每场 + 累计）
// ============================================================

/// <summary>
/// 单个英雄在某场战斗中的表现统计
/// </summary>
[Serializable]
public class HeroBattleStats
{
    public string heroName;
    public int heroInstanceId;   // GetInstanceID() 用于区分同名英雄

    // 伤害
    public int damageDealt;
    public int damageTaken;
    public int critCount;

    // 治疗 & 护盾
    public int healingDone;
    public int shieldGained;

    // 击杀
    public int kills;

    // 伤害明细（可选调试用）
    [NonSerialized] public List<int> damageLog = new List<int>();

    /// <summary>综合评分 = 伤害权重×伤害 + 治疗权重×治疗 + 击杀权重×击杀 - 受伤权重×受伤</summary>
    public float Score => damageDealt * 1f + healingDone * 0.8f + kills * 200f - damageTaken * 0.3f;
}

/// <summary>
/// 单场战斗统计记录
/// </summary>
[Serializable]
public class BattleStatsRecord
{
    public int battleIndex;            // 第N场战斗
    public bool isVictory;
    public float duration;             // 战斗时长（秒）

    // 汇总数据
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int totalHealing;
    public int totalShield;
    public int totalKills;             // 本场击杀敌方数

    // 骰子组合
    public string diceComboType;       // DiceCombinationType 名称
    public string diceComboDesc;       // 可读描述
    public List<string> diceSkillsTriggered = new List<string>(); // 触发的骰子技能

    // 每英雄统计
    [NonSerialized] public Dictionary<int, HeroBattleStats> heroStatsMap = new Dictionary<int, HeroBattleStats>();
    public List<HeroBattleStats> heroStatsList = new List<HeroBattleStats>(); // 序列化用

    /// <summary>序列化前</summary>
    public void BeforeSerialize()
    {
        heroStatsList = new List<HeroBattleStats>();
        if (heroStatsMap != null)
        {
            foreach (var kv in heroStatsMap)
                heroStatsList.Add(kv.Value);
        }
    }

    /// <summary>反序列化后</summary>
    public void AfterDeserialize()
    {
        heroStatsMap = new Dictionary<int, HeroBattleStats>();
        if (heroStatsList != null)
        {
            foreach (var hs in heroStatsList)
                heroStatsMap[hs.heroInstanceId] = hs;
        }
    }
}

/// <summary>
/// 骰子组合统计项
/// </summary>
[Serializable]
public class DiceComboStat
{
    public string comboType;           // DiceCombinationType 名称
    public string displayName;         // 可读名称（三条/顺子/对子）
    public int triggerCount;           // 触发次数
    public int totalDamageBonus;       // 由该组合带来的额外伤害
}

/// <summary>
/// 整个Run的累计统计（跨所有战斗汇总）
/// JsonUtility兼容：Dictionary用平行List序列化
/// </summary>
[Serializable]
public class RunBattleStats
{
    // ─── 总体汇总 ──────────────────────────────────
    public int totalBattles;
    public int victories;
    public int defeats;
    public float winRate;
    public int maxConsecutiveWins;      // 最高连胜
    public int currentConsecutiveWins;  // 当前连胜

    // ─── 伤害 / 治疗 / 护盾 ───────────────────────
    public long totalDamageDealt;       // 全Run总输出
    public long totalDamageTaken;       // 全Run总承伤
    public long totalHealing;           // 全Run总治疗
    public long totalShield;            // 全Run总护盾

    // ─── 击杀 ─────────────────────────────────────
    public int totalKills;              // 全Run总击杀

    // ─── 战斗时长 ──────────────────────────────────
    public float totalBattleDuration;   // 全Run总战斗时长
    public float longestBattle;         // 最长单场战斗

    // ─── 骰子组合统计 — 平行List序列化 ─────────────
    public List<string> comboNames = new List<string>();
    public List<int> comboCounts = new List<int>();
    [NonSerialized] public Dictionary<string, int> comboCountMap = new Dictionary<string, int>();

    // ─── 遗物收集 ──────────────────────────────────
    public List<string> relicsCollected = new List<string>();

    // ─── 英雄累计统计 — 平行List序列化 ─────────────
    [NonSerialized] public Dictionary<int, HeroBattleStats> heroCumulativeMap = new Dictionary<int, HeroBattleStats>();
    public List<HeroBattleStats> heroCumulativeList = new List<HeroBattleStats>();

    // ─── 每场战斗历史 ──────────────────────────────
    public List<BattleStatsRecord> battleHistory = new List<BattleStatsRecord>();

    // ─── 序列化辅助 ────────────────────────────────

    public void BeforeSerialize()
    {
        // 骰子组合 Dictionary → 平行List
        if (comboCountMap != null)
        {
            comboNames = new List<string>(comboCountMap.Keys);
            comboCounts = new List<int>(comboCountMap.Values);
        }

        // 英雄累计 Dictionary → List
        heroCumulativeList = new List<HeroBattleStats>();
        if (heroCumulativeMap != null)
        {
            foreach (var kv in heroCumulativeMap)
                heroCumulativeList.Add(kv.Value);
        }

        // 每场记录序列化
        foreach (var rec in battleHistory)
            rec.BeforeSerialize();
    }

    public void AfterDeserialize()
    {
        // 骰子组合 平行List → Dictionary
        comboCountMap = new Dictionary<string, int>();
        if (comboNames != null && comboCounts != null)
        {
            for (int i = 0; i < comboNames.Count && i < comboCounts.Count; i++)
                comboCountMap[comboNames[i]] = comboCounts[i];
        }

        // 英雄累计 List → Dictionary
        heroCumulativeMap = new Dictionary<int, HeroBattleStats>();
        if (heroCumulativeList != null)
        {
            foreach (var hs in heroCumulativeList)
                heroCumulativeMap[hs.heroInstanceId] = hs;
        }

        // 每场记录反序列化
        foreach (var rec in battleHistory)
            rec.AfterDeserialize();
    }

    // ─── 快捷查询方法 ──────────────────────────────

    /// <summary>获取MVP英雄（累计伤害最高）</summary>
    public HeroBattleStats GetMVPHero()
    {
        HeroBattleStats mvp = null;
        float maxScore = -1f;
        if (heroCumulativeMap != null)
        {
            foreach (var kv in heroCumulativeMap)
            {
                if (kv.Value.Score > maxScore)
                {
                    maxScore = kv.Value.Score;
                    mvp = kv.Value;
                }
            }
        }
        return mvp;
    }

    /// <summary>获取骰子组合使用最多的类型</summary>
    public string GetMostUsedCombo()
    {
        string best = "无";
        int bestCount = 0;
        if (comboCountMap != null)
        {
            foreach (var kv in comboCountMap)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    best = kv.Key;
                }
            }
        }
        return best;
    }
}
