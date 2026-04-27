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
    public static void TakeAction(Hero self, List<Hero> enemies)
    {
        if (self == null || self.IsDead) return;
        if (enemies == null || enemies.Count == 0) return;

        // 寻找目标
        Hero target = FindTarget(self, enemies);
        if (target == null) return;

        // 检查是否释放技能（简化：每次行动有30%概率放技能）
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
    /// 寻找目标 — 简化为找距离最近的活着的敌人
    /// </summary>
    static Hero FindTarget(Hero self, List<Hero> enemies)
    {
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
    /// 普通攻击
    /// </summary>
    static void NormalAttack(Hero self, Hero target)
    {
        int damage = CalculateDamage(self, target);
        target.TakeDamage(damage);

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
                    int dmg = Mathf.RoundToInt(self.BattleAttack * skill.damageMultiplier);
                    target.TakeDamage(dmg);
                    Debug.Log($"{self.Data.heroName} 释放 [{skill.skillName}] → {target.Data.heroName} 造成 {dmg} 伤害");
                }
                break;

            case SkillTargetType.AOE:
                {
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || enemy.IsDead) continue;
                        int dmg = Mathf.RoundToInt(self.BattleAttack * skill.damageMultiplier * 0.6f);
                        enemy.TakeDamage(dmg);
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
    /// 伤害计算 — 攻击 × 暴击 - 防御
    /// </summary>
    static int CalculateDamage(Hero attacker, Hero defender)
    {
        float attack = attacker.BattleAttack;
        float critChance = attacker.BattleCritRate;
        bool isCrit = Random.value < critChance;

        float damage = attack;
        if (isCrit) damage *= 2f;

        damage = Mathf.Max(1, damage - defender.BattleDefense);
        return Mathf.RoundToInt(damage);
    }
}
