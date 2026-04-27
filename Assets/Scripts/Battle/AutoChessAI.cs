using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 自走棋 AI — 控制单位自动寻敌、攻击、释放技能
/// </summary>
public static class AutoChessAI
{
    /// <summary>
    /// 单位执行一次行动
    /// </summary>
    public static void TakeAction(Hero self, List<Hero> enemies, List<Hero> allies)
    {
        if (self == null || self.IsDead) return;

        // 治疗者AI：优先治疗血量最低的友方
        if (self.Data.heroName == "治疗者" && allies != null && allies.Count > 0)
        {
            Hero weakest = FindWeakestAlly(allies);
            if (weakest != null && weakest.CurrentHealth < weakest.MaxHealth * 0.8f)
            {
                int heal = Mathf.RoundToInt(self.Data.activeSkill?.effectValue ?? 15);
                weakest.Heal(heal);
                Debug.Log($"{self.Data.heroName} 治疗 {weakest.Data.heroName} 恢复 {heal} 生命");
                return;
            }
        }

        if (enemies == null || enemies.Count == 0) return;

        // 寻找目标
        Hero target = FindTarget(self, enemies);
        if (target == null) return;

        // 检查是否释放技能
        if (self.Data.activeSkill != null && Random.value < 0.3f)
        {
            UseSkill(self, target, enemies);
        }
        else
        {
            NormalAttack(self, target);
        }
    }

    /// <summary>
    /// 寻找目标 — 根据职业选择优先级
    /// </summary>
    static Hero FindTarget(Hero self, List<Hero> enemies)
    {
        // 刺客优先攻击血量最低/后排脆皮
        if (self.Data.heroClass == HeroClass.Assassin)
        {
            Hero weakest = null;
            int minHealth = int.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.CurrentHealth < minHealth)
                {
                    minHealth = enemy.CurrentHealth;
                    weakest = enemy;
                }
            }
            if (weakest != null) return weakest;
        }

        // Boss优先攻击攻击最高的目标（威胁最大）
        if (self.Data.heroName == "Boss")
        {
            Hero highestAtk = null;
            int maxAtk = -1;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.BattleAttack > maxAtk)
                {
                    maxAtk = enemy.BattleAttack;
                    highestAtk = enemy;
                }
            }
            if (highestAtk != null) return highestAtk;
        }

        // 默认：找距离最近的活着的敌人
        Hero nearest = null;
        float minDist = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float dist = Vector2Int.Distance(self.GridPosition, enemy.GridPosition);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    /// <summary>
    /// 找血量最低的友方
    /// </summary>
    static Hero FindWeakestAlly(List<Hero> allies)
    {
        Hero weakest = null;
        float minHealthRatio = float.MaxValue;
        foreach (var ally in allies)
        {
            if (ally == null || ally.IsDead) continue;
            float ratio = (float)ally.CurrentHealth / ally.MaxHealth;
            if (ratio < minHealthRatio)
            {
                minHealthRatio = ratio;
                weakest = ally;
            }
        }
        return weakest;
    }

    /// <summary>
    /// 普通攻击
    /// </summary>
    static void NormalAttack(Hero self, Hero target)
    {
        // 破甲：忽略目标部分防御
        int effectiveDef = target.BattleDefense;
        if (self.HasArmorBreak)
            effectiveDef = Mathf.RoundToInt(target.BattleDefense * 0.5f);

        int damage = GameBalance.CalculateDamage(
            self.BattleAttack, self.BattleCritRate, self.BattleCritDamage, effectiveDef);
        target.TakeDamage(damage, self);

        // 闪电链：弹射到附近敌人
        if (self.LightningChainBounces > 0)
        {
            // 简化实现：随机弹射到其他敌人
            // 这里需要传入敌人列表，暂时不实现完整逻辑
        }

        bool isCrit = damage > self.BattleAttack; // 简化判断
        string critTag = isCrit ? " 暴击!" : "";
        Debug.Log($"{self.Data.heroName} 攻击 {target.Data.heroName}，造成 {damage} 伤害{critTag}");
    }

    /// <summary>
    /// 释放技能
    /// </summary>
    static void UseSkill(Hero self, Hero target, List<Hero> enemies)
    {
        var skill = self.Data.activeSkill;
        if (skill == null) return;

        switch (skill.targetType)
        {
            case SkillTargetType.Single:
                {
                    int dmg = GameBalance.CalculateDamage(self.BattleAttack, self.BattleCritRate, self.BattleCritDamage, target.BattleDefense, skill.damageMultiplier);
                    target.TakeDamage(dmg, self);
                    Debug.Log($"{self.Data.heroName} 释放 [{skill.skillName}] → {target.Data.heroName} 造成 {dmg} 伤害");
                }
                break;

            case SkillTargetType.AOE:
                {
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        int dmg = GameBalance.CalculateDamage(self.BattleAttack, self.BattleCritRate, self.BattleCritDamage, enemy.BattleDefense, skill.damageMultiplier * 0.6f);
                        enemy.TakeDamage(dmg, self);
                    }
                    Debug.Log($"{self.Data.heroName} 释放 [{skill.skillName}] AOE 伤害");
                }
                break;

            case SkillTargetType.Self:
                {
                    int heal = Mathf.RoundToInt(skill.effectValue);
                    self.Heal(heal);
                    Debug.Log($"{self.Data.heroName} 释放 [{skill.skillName}] 恢复 {heal} 生命");
                }
                break;
        }
    }

    /// <summary>
    /// 伤害计算 — 使用统一公式
    /// </summary>
    static int CalculateDamage(Hero attacker, Hero defender)
    {
        return GameBalance.CalculateDamage(
            attacker.BattleAttack,
            attacker.BattleCritRate,
            attacker.BattleCritDamage,
            defender.BattleDefense
        );
    }
}
