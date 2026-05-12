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
        bool isHealer = self.Data.heroName == "治疗者" || self.Data.heroName == "治疗兵";
        if (isHealer && allies != null && allies.Count > 0)
        {
            Hero weakest = FindWeakestAlly(allies);
            if (weakest != null && weakest.CurrentHealth < weakest.MaxHealth * 0.8f)
            {
                int heal = Mathf.RoundToInt(self.Data.activeSkill?.effectValue ?? 15);
                weakest.Heal(heal);
                Debug.Log($"{self.Data.heroName} 治疗 {weakest.Data.heroName} 恢复 {heal} 生命");
                // 战斗统计：通知治疗事件（healer=self, target=weakest）
                if (BattleManager.Instance != null)
                    BattleManager.Instance.NotifyHealDone(self, weakest, heal);
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
            NormalAttack(self, target, allies, enemies);
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
            var weakest = FindBest(enemies, (a, b) => a.CurrentHealth < b.CurrentHealth);
            if (weakest != null) return weakest;
        }

        // Boss优先攻击攻击最高的目标（威胁最大）
        if (self.Data.heroName == "Boss")
        {
            var highestAtk = FindBest(enemies, (a, b) => a.BattleAttack > b.BattleAttack);
            if (highestAtk != null) return highestAtk;
        }

        // 默认：找距离最近的活着的敌人
        return FindBest(enemies, (a, b) =>
            Vector2Int.Distance(self.GridPosition, a.GridPosition) <
            Vector2Int.Distance(self.GridPosition, b.GridPosition));
    }

    /// <summary>
    /// 通用查找：返回满足条件的最优目标
    /// </summary>
    static Hero FindBest(List<Hero> heroes, System.Func<Hero, Hero, bool> isBetter)
    {
        Hero best = null;
        foreach (var h in heroes)
        {
            if (h == null || h.IsDead) continue;
            if (best == null || isBetter(h, best))
                best = h;
        }
        return best;
    }

    /// <summary>
    /// 找血量最低的友方
    /// </summary>
    static Hero FindWeakestAlly(List<Hero> allies)
    {
        return FindBest(allies, (a, b) => (float)a.CurrentHealth / a.MaxHealth < (float)b.CurrentHealth / b.MaxHealth);
    }

    /// <summary>
    /// 普通攻击
    /// </summary>
    static void NormalAttack(Hero self, Hero target, List<Hero> allies = null, List<Hero> enemies = null)
    {
        // 破甲：忽略目标部分防御
        int effectiveDef = target.BattleDefense;
        if (self.HasArmorBreak)
            effectiveDef = Mathf.RoundToInt(target.BattleDefense * 0.5f);

        int damage = GameBalance.CalculateDamage(
            self.BattleAttack, self.BattleCritRate, self.BattleCritDamage, effectiveDef);
        target.TakeDamage(damage, self);

        // 面效果：OnAttack 触发
        if (FaceEffectExecutor.Instance != null)
            FaceEffectExecutor.Instance.ProcessOnAttackEffects(self, target, allies, enemies);

        // 机制怪：Boss受伤通知（target是被攻击者，self是攻击者）
        if (MechanicEnemySystem.Instance != null)
        {
            // Boss被攻击时：target是Boss → 触发DamageReflect/Berserk
            MechanicEnemySystem.Instance.OnBossDamaged(target, damage, self);

            // Boss攻击别人后回血：self是Boss → 触发HealOnAttack
            if (MechanicEnemySystem.Instance.IsRegisteredBoss(self))
                MechanicEnemySystem.Instance.OnBossAttacked(self, damage);
        }

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
                    // 战斗统计：通知自我治疗事件
                    if (BattleManager.Instance != null)
                        BattleManager.Instance.NotifyHealDone(self, self, heal);
                }
                break;
        }
    }
}
