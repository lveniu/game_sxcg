using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 连携技系统 — 根据场上英雄职业组合触发团队Buff
/// 职业映射：Warrior(战士) / Mage(法师) / Assassin(刺客)
/// </summary>
public static class SynergySystem
{
    public static void ApplySynergies(List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return;

        int warriorCount = 0;
        int mageCount = 0;
        int assassinCount = 0;

        foreach (var hero in heroes)
        {
            if (hero == null || hero.Data == null) continue;
            switch (hero.Data.heroClass)
            {
                case HeroClass.Warrior:
                    warriorCount++;
                    break;
                case HeroClass.Mage:
                    mageCount++;
                    break;
                case HeroClass.Assassin:
                    assassinCount++;
                    break;
            }
        }

        // 前排铁壁：2+战士 → 所有战士防御+30%
        if (warriorCount >= 2)
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
        if (mageCount >= 2)
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
        bool hasWarrior = warriorCount > 0;
        bool hasMage = mageCount > 0;
        bool hasAssassin = assassinCount > 0;
        if (hasWarrior && hasMage && hasAssassin)
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
}
