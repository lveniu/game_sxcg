using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================
// BattleReport — 战斗回放数据模型 + 生成逻辑
// ============================================================

/// <summary>
/// 每回合快照 — 记录一个 battle tick 的双方状态
/// </summary>
[Serializable]
public class BattleSnapshot
{
    public int turnIndex;           // 回合序号（从0开始）
    public float timestamp;         // 战斗开始后的时间

    // 玩家方英雄快照
    public List<UnitSnapshot> playerUnits = new List<UnitSnapshot>();

    // 敌方单位快照
    public List<UnitSnapshot> enemyUnits = new List<UnitSnapshot>();

    // 本回合触发的事件标签（用于回放时间轴）
    public List<string> events = new List<string>();
}

/// <summary>
/// 单个单位在某回合的状态快照
/// </summary>
[Serializable]
public class UnitSnapshot
{
    public string unitName;
    public int hp;
    public int maxHp;
    public int shield;
    public bool isDead;

    // Buff 数量（简化）
    public int buffCount;
    public int debuffCount;
}

/// <summary>
/// 伤害构成分类统计
/// </summary>
[Serializable]
public class DamageBreakdown
{
    public int physicalDamage;      // 普通攻击伤害
    public int critDamage;          // 暴击伤害
    public int skillDamage;         // 技能伤害（骰子技能等）
    public int comboDamage;         // 连携加成伤害
    public int dotDamage;           // 持续伤害（Buff/Debuff）
    public int totalDamage => physicalDamage + critDamage + skillDamage + comboDamage + dotDamage;
}

/// <summary>
/// 机制触发统计
/// </summary>
[Serializable]
public class MechanicStats
{
    public int bossMechanicTriggers;    // Boss机制触发次数
    public int synergyActivations;      // 连携激活次数
    public int diceSkillUses;           // 骰子技能使用次数
    public int critCount;               // 暴击次数
    public int maxCombo;                // 最大连击数

    // 详细事件列表（用于时间轴展示）
    public List<TimelineEvent> timelineEvents = new List<TimelineEvent>();
}

/// <summary>
/// 时间轴事件（用于战报UI展示关键节点）
/// </summary>
[Serializable]
public class TimelineEvent
{
    public int turnIndex;           // 发生回合
    public float timestamp;         // 发生时间
    public string eventType;        // "crit" / "kill" / "synergy" / "boss_mechanic" / "dice_skill" / "combo"
    public string description;      // 可读描述
    public string sourceUnit;       // 触发单位
    public string targetUnit;       // 目标单位
    public int value;               // 数值（伤害/治疗量等）
}

/// <summary>
/// 战斗报告 — 一场战斗的完整回放数据
/// GenerateReport() 由 BattleStatsTracker 在战斗结束时调用
/// </summary>
[Serializable]
public class BattleReport
{
    // ─── 基本信息 ─────────────────────────────────
    public int battleIndex;             // 第N场战斗
    public bool isVictory;
    public int totalTurns;              // 总回合数
    public float duration;              // 战斗时长（秒）

    // ─── 汇总数据 ─────────────────────────────────
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int totalHealing;
    public int totalShield;
    public int totalKills;

    // ─── 伤害构成 ─────────────────────────────────
    public DamageBreakdown damageBreakdown = new DamageBreakdown();

    // ─── 机制统计 ─────────────────────────────────
    public MechanicStats mechanicStats = new MechanicStats();

    // ─── 英雄表现排名 ─────────────────────────────
    public List<HeroReportEntry> heroRankings = new List<HeroReportEntry>();

    // ─── 回合快照序列（用于回放） ─────────────────
    public List<BattleSnapshot> snapshots = new List<BattleSnapshot>();

    // ─── 骰子信息 ─────────────────────────────────
    public string diceComboType;
    public List<string> diceSkillsTriggered = new List<string>();

    // ─── MVP ──────────────────────────────────────
    public string mvpHeroName;
    public float mvpScore;

    /// <summary>序列化前清理 NonSerialized 数据</summary>
    public void BeforeSerialize()
    {
        // snapshots 可能很大，按需裁剪（保留最多100个关键回合）
        if (snapshots != null && snapshots.Count > 100)
        {
            // 保留第一个、最后一个、和有事件的回合
            var important = new List<BattleSnapshot>();
            important.Add(snapshots[0]);
            for (int i = 1; i < snapshots.Count - 1; i++)
            {
                if (snapshots[i].events != null && snapshots[i].events.Count > 0)
                    important.Add(snapshots[i]);
            }
            // 避免单元素时重复添加
            if (snapshots.Count > 1)
                important.Add(snapshots[snapshots.Count - 1]);
            snapshots = important;
        }

        // 清理空列表避免序列化问题
        if (diceSkillsTriggered == null)
            diceSkillsTriggered = new List<string>();
        if (heroRankings == null)
            heroRankings = new List<HeroReportEntry>();
    }
}

/// <summary>
/// 英雄在战报中的表现条目
/// </summary>
[Serializable]
public class HeroReportEntry
{
    public string heroName;
    public int damageDealt;
    public int damageTaken;
    public int healingDone;
    public int shieldGained;
    public int kills;
    public int critCount;
    public float score;                 // 综合评分
    public float damagePercent;         // 伤害占比（%）
}
