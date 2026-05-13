using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 连携技系统 — 根据场上英雄职业组合触发团队Buff
/// v2: 所有硬编码数值改为从 BalanceProvider (battle_formulas.json) 读取
/// 职业映射：Warrior(战士) / Mage(法师) / Assassin(刺客)
/// </summary>
public static class SynergySystem
{
    /// <summary>
    /// 应用连携技 — 遍历JSON配置中的所有连携规则，满足条件则触发
    /// v3: 增加阵营连携检测（BE-19）
    /// </summary>
    public static void ApplySynergies(List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return;

        // 1. 职业连携（原有逻辑）
        var classCounts = CountHeroClasses(heroes);
        ApplyClassSynergies(heroes, classCounts);

        // 2. 阵营连携（BE-19 新增）
        var factionCounts = CountHeroFactions(heroes);
        ApplyFactionSynergies(heroes, factionCounts);
    }

    /// <summary>
    /// 职业连携逻辑（从 ApplySynergies 拆出）
    /// </summary>
    static void ApplyClassSynergies(List<Hero> heroes, Dictionary<HeroClass, int> classCounts)
    {
        // 从 JSON 配置读取职业连携规则
        var synergies = BalanceProvider.GetSynergies();
        if (synergies == null || synergies.Count == 0)
        {
            ApplyFallbackSynergies(heroes, classCounts);
            return;
        }

        foreach (var syn in synergies)
        {
            if (syn == null) continue;
            if (IsConditionMet(syn.condition, classCounts))
                ApplySynergyEffect(syn, heroes);
        }
    }

    /// <summary>
    /// 统计各职业数量
    /// </summary>
    static Dictionary<HeroClass, int> CountHeroClasses(List<Hero> heroes)
    {
        var counts = new Dictionary<HeroClass, int>
        {
            { HeroClass.Warrior, 0 },
            { HeroClass.Mage, 0 },
            { HeroClass.Assassin, 0 }
        };

        foreach (var hero in heroes)
        {
            if (hero == null || hero.Data == null) continue;
            if (counts.ContainsKey(hero.Data.heroClass))
                counts[hero.Data.heroClass]++;
        }

        return counts;
    }

    /// <summary>
    /// 解析条件字符串，判断是否满足
    /// 支持: "2+ warrior" / "2+ mage" / "2+ assassin" / "at least 1 of each class"
    /// </summary>
    static bool IsConditionMet(string condition, Dictionary<HeroClass, int> classCounts)
    {
        if (string.IsNullOrEmpty(condition)) return false;
        string cond = condition.ToLower().Trim();

        // "at least 1 of each class" — 均衡阵容
        if (cond.Contains("at least 1 of each class") || cond.Contains("each class"))
        {
            return classCounts[HeroClass.Warrior] >= 1
                && classCounts[HeroClass.Mage] >= 1
                && classCounts[HeroClass.Assassin] >= 1;
        }

        // "N+ classname" 模式 — 如 "2+ warrior"
        var match = Regex.Match(cond, @"(\d+)\+\s*(warrior|mage|assassin|战士|法师|刺客)");
        if (match.Success)
        {
            int required = int.Parse(match.Groups[1].Value);
            HeroClass cls = ParseClassName(match.Groups[2].Value);
            return classCounts.ContainsKey(cls) && classCounts[cls] >= required;
        }

        Debug.LogWarning($"[SynergySystem] 未识别的连携条件: {condition}");
        return false;
    }

    /// <summary>
    /// 应用单条连携效果
    /// </summary>
    static void ApplySynergyEffect(SynergyEntry syn, List<Hero> heroes)
    {
        if (syn.target == "all_allies")
        {
            // 全体效果
            foreach (var hero in heroes)
            {
                if (hero == null) continue;
                ApplyStatBonuses(syn, hero);
            }
        }
        else
        {
            // self_class — 只对特定职业生效，从 condition 推断职业
            HeroClass targetClass = ExtractTargetClass(syn.condition);
            foreach (var hero in heroes)
            {
                if (hero == null || hero.Data == null) continue;
                if (hero.Data.heroClass == targetClass)
                {
                    ApplyStatBonuses(syn, hero);
                }
            }
        }

        Debug.Log($"[连携技] {syn.name_cn}({syn.condition}) 已触发 — {syn.effect}");
    }

    /// <summary>
    /// 对单个英雄应用属性加成
    /// </summary>
    static void ApplyStatBonuses(SynergyEntry syn, Hero hero)
    {
        bool applied = false;

        // 防御加成
        if (syn.defense_bonus_pct > 0)
        {
            hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * (1f + syn.defense_bonus_pct));
            applied = true;
        }

        // 攻击加成
        if (syn.attack_bonus_pct > 0)
        {
            hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + syn.attack_bonus_pct));
            applied = true;
        }

        // 暴击率加成
        if (syn.crit_rate_bonus_pct > 0)
        {
            hero.BattleCritRate += syn.crit_rate_bonus_pct;
            applied = true;
        }

        // 全属性加成
        if (syn.all_stats_bonus_pct > 0)
        {
            hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + syn.all_stats_bonus_pct));
            hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * (1f + syn.all_stats_bonus_pct));
            hero.BattleSpeed = Mathf.RoundToInt(hero.BattleSpeed * (1f + syn.all_stats_bonus_pct));
            applied = true;
        }

        if (applied && hero.Data != null)
        {
            Debug.Log($"  → {hero.Data.heroName} 获得连携加成");
        }
    }

    /// <summary>
    /// 从 condition 字符串提取目标职业
    /// </summary>
    static HeroClass ExtractTargetClass(string condition)
    {
        if (string.IsNullOrEmpty(condition)) return HeroClass.Warrior;
        string cond = condition.ToLower();

        if (cond.Contains("warrior") || cond.Contains("战士")) return HeroClass.Warrior;
        if (cond.Contains("mage") || cond.Contains("法师")) return HeroClass.Mage;
        if (cond.Contains("assassin") || cond.Contains("刺客")) return HeroClass.Assassin;

        return HeroClass.Warrior;
    }

    /// <summary>
    /// 字符串 → HeroClass 枚举
    /// </summary>
    static HeroClass ParseClassName(string name)
    {
        string n = name.ToLower().Trim();
        if (n == "warrior" || n == "战士") return HeroClass.Warrior;
        if (n == "mage" || n == "法师") return HeroClass.Mage;
        if (n == "assassin" || n == "刺客") return HeroClass.Assassin;
        return HeroClass.Warrior;
    }

    /// <summary>
    /// Fallback: JSON无配置时使用硬编码默认值（与原始逻辑一致）
    /// </summary>
    static void ApplyFallbackSynergies(List<Hero> heroes, Dictionary<HeroClass, int> classCounts)
    {
        // 前排铁壁：2+战士 → 所有战士防御+30%
        if (classCounts[HeroClass.Warrior] >= 2)
        {
            foreach (var hero in heroes)
            {
                if (hero?.Data?.heroClass == HeroClass.Warrior)
                {
                    hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * 1.3f);
                    Debug.Log($"[连携技] 前排铁壁：{hero.Data.heroName} 防御+30%");
                }
            }
        }

        // 远程火力：2+法师 → 法师攻击+20%
        if (classCounts[HeroClass.Mage] >= 2)
        {
            foreach (var hero in heroes)
            {
                if (hero?.Data?.heroClass == HeroClass.Mage)
                {
                    hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * 1.2f);
                    Debug.Log($"[连携技] 远程火力：{hero.Data.heroName} 攻击+20%");
                }
            }
        }

        // 暗影突袭：2+刺客 → 刺客暴击率+15%
        if (classCounts[HeroClass.Assassin] >= 2)
        {
            foreach (var hero in heroes)
            {
                if (hero?.Data?.heroClass == HeroClass.Assassin)
                {
                    hero.BattleCritRate += 0.15f;
                    Debug.Log($"[连携技] 暗影突袭：{hero.Data.heroName} 暴击率+15%");
                }
            }
        }

        // 均衡阵容：每职业至少1个 → 全体全属性+10%
        if (classCounts[HeroClass.Warrior] > 0 && classCounts[HeroClass.Mage] > 0 && classCounts[HeroClass.Assassin] > 0)
        {
            foreach (var hero in heroes)
            {
                if (hero == null) continue;
                hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * 1.1f);
                hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * 1.1f);
                hero.BattleSpeed = Mathf.RoundToInt(hero.BattleSpeed * 1.1f);
                Debug.Log($"[连携技] 均衡阵容：{hero.Data.heroName} 全属性+10%");
            }
        }
    }

    // ====================================================
    // BE-19: 阵营连携系统
    // ====================================================

    /// <summary>
    /// 统计各阵营英雄数量（排除 None）
    /// </summary>
    static Dictionary<HeroFaction, int> CountHeroFactions(List<Hero> heroes)
    {
        var counts = new Dictionary<HeroFaction, int>();
        foreach (var hero in heroes)
        {
            if (hero == null || hero.Data == null) continue;
            var f = hero.Data.faction;
            if (f == HeroFaction.None) continue;
            if (!counts.ContainsKey(f)) counts[f] = 0;
            counts[f]++;
        }
        return counts;
    }

    /// <summary>
    /// 应用阵营连携效果
    /// </summary>
    static void ApplyFactionSynergies(List<Hero> heroes, Dictionary<HeroFaction, int> factionCounts)
    {
        var configs = BalanceProvider.GetFactionSynergies();
        if (configs == null || configs.Count == 0)
        {
            ApplyFallbackFactionSynergies(heroes, factionCounts);
            return;
        }

        foreach (var kvp in factionCounts)
        {
            var factionName = kvp.Key.ToString();
            int count = kvp.Value;

            // 获取该阵营所有满足人数条件的连携
            var activated = BalanceProvider.GetActivatedFactionSynergies(factionName, count);
            foreach (var entry in activated)
            {
                ApplyFactionSynergyEffect(entry, heroes, kvp.Key);
            }
        }
    }

    /// <summary>
    /// 应用单条阵营连携效果
    /// </summary>
    static void ApplyFactionSynergyEffect(FactionSynergyEntry entry, List<Hero> heroes, HeroFaction faction)
    {
        List<Hero> targets;

        if (entry.target == "all_allies")
        {
            targets = heroes;
        }
        else // self_faction
        {
            targets = heroes.FindAll(h => h != null && h.Data != null && h.Data.faction == faction);
        }

        foreach (var hero in targets)
        {
            if (hero == null) continue;

            if (entry.attack_bonus_pct > 0)
                hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + entry.attack_bonus_pct));
            if (entry.defense_bonus_pct > 0)
                hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * (1f + entry.defense_bonus_pct));
            if (entry.speed_bonus_pct > 0)
                hero.BattleSpeed = Mathf.RoundToInt(hero.BattleSpeed * (1f + entry.speed_bonus_pct));
            if (entry.crit_rate_bonus_pct > 0)
                hero.BattleCritRate += entry.crit_rate_bonus_pct;
            if (entry.hp_regen_pct > 0)
            {
                // 每回合回血 — 存储在临时buff中（BattleManager tick 时检查）
                // 简单实现：直接加一次
                int heal = Mathf.RoundToInt(hero.MaxHealth * entry.hp_regen_pct);
                hero.Heal(heal);
            }
            if (entry.all_stats_bonus_pct > 0)
            {
                hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * (1f + entry.all_stats_bonus_pct));
                hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * (1f + entry.all_stats_bonus_pct));
                hero.BattleSpeed = Mathf.RoundToInt(hero.BattleSpeed * (1f + entry.all_stats_bonus_pct));
            }
        }

        Debug.Log($"[阵营连携] {entry.name_cn}({entry.faction} x{entry.required_count}) 已触发 — {entry.effect}");
    }

    /// <summary>
    /// Fallback: JSON无阵营配置时的默认值
    /// </summary>
    static void ApplyFallbackFactionSynergies(List<Hero> heroes, Dictionary<HeroFaction, int> factionCounts)
    {
        foreach (var kvp in factionCounts)
        {
            if (kvp.Value < 2) continue;

            switch (kvp.Key)
            {
                case HeroFaction.Human:
                    // 人类联盟：2+人类 → 全体攻击+10%
                    foreach (var h in heroes)
                    {
                        if (h == null) continue;
                        h.BattleAttack = Mathf.RoundToInt(h.BattleAttack * 1.1f);
                    }
                    Debug.Log("[阵营连携] 人类联盟：全体攻击+10%");
                    break;

                case HeroFaction.Elf:
                    // 精灵共鸣：2+精灵 → 精灵速度+15%
                    foreach (var h in heroes)
                    {
                        if (h?.Data?.faction == HeroFaction.Elf)
                            h.BattleSpeed = Mathf.RoundToInt(h.BattleSpeed * 1.15f);
                    }
                    Debug.Log("[阵营连携] 精灵共鸣：精灵速度+15%");
                    break;

                case HeroFaction.Orc:
                    // 兽人狂暴：2+兽人 → 兽人攻击+20%
                    foreach (var h in heroes)
                    {
                        if (h?.Data?.faction == HeroFaction.Orc)
                            h.BattleAttack = Mathf.RoundToInt(h.BattleAttack * 1.2f);
                    }
                    Debug.Log("[阵营连携] 兽人狂暴：兽人攻击+20%");
                    break;

                case HeroFaction.Undead:
                    // 亡灵侵蚀：2+亡灵 → 亡灵暴击+15%
                    foreach (var h in heroes)
                    {
                        if (h?.Data?.faction == HeroFaction.Undead)
                            h.BattleCritRate += 0.15f;
                    }
                    Debug.Log("[阵营连携] 亡灵侵蚀：亡灵暴击+15%");
                    break;

                case HeroFaction.Mech:
                    // 机械过载：2+机械 → 机械防御+25%
                    foreach (var h in heroes)
                    {
                        if (h?.Data?.faction == HeroFaction.Mech)
                            h.BattleDefense = Mathf.RoundToInt(h.BattleDefense * 1.25f);
                    }
                    Debug.Log("[阵营连携] 机械过载：机械防御+25%");
                    break;
            }
        }
    }
}
