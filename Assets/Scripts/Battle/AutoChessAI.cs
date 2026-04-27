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
