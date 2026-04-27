using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连携技系统 — 根据场上英雄职业组合触发团队Buff
/// </summary>
public static class SynergySystem
{
    public static void ApplySynergies(List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return;

        int tankCount = 0;
        int rangedCount = 0; // 射手+法师
        int assassinCount = 0;
        bool hasWarrior = false;

        foreach (var hero in heroes)
        {
            if (hero == null || hero.Data == null) continue;
            switch (hero.Data.heroClass)
            {
                case HeroClass.Tank:
                    tankCount++;
                    break;
                case HeroClass.Archer:
                    rangedCount++;
                    break;
                case HeroClass.Assassin:
                    assassinCount++;
                    break;
            }
            // 战士作为特殊职业，在MVP中用Tank占位
            if (hero.Data.heroName == "战士" || hero.Data.heroName == "狂战士")
                hasWarrior = true;
        }

        // 前排铁壁：2+坦克 → 所有坦克防御+30%
        if (tankCount >= 2)
        {
            foreach (var hero in heroes)
            {
                if (hero?.Data?.heroClass == HeroClass.Tank)
                {
                    hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * 1.3f);
                    Debug.Log($"[连携技] 前排铁壁：{hero.Data.heroName} 防御+30%");
                }
            }
        }

        // 远程火力：2+射手/法师 → 远程攻击+20%
        if (rangedCount >= 2)
        {
            foreach (var hero in heroes)
            {
                if (hero?.Data?.heroClass == HeroClass.Archer)
                {
                    hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * 1.2f);
                    Debug.Log($"[连携技] 远程火力：{hero.Data.heroName} 攻击+20%");
                }
            }
        }

        // 暗影突袭：2+刺客 → 刺客暴击率+15%
        if (assassinCount >= 2)
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
        bool hasTank = tankCount > 0;
        bool hasRanged = rangedCount > 0;
        bool hasAssassin = assassinCount > 0;
        if (hasTank && hasRanged && hasAssassin)
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

        // 狂战之心：场上有狂战士 → 全体攻击+10%
        if (hasWarrior)
        {
            foreach (var hero in heroes)
            {
                if (hero == null) continue;
                hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * 1.1f);
            }
            Debug.Log("[连携技] 狂战之心：全体攻击+10%");
        }
    }
}
