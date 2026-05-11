using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 遗物系统 — 管理遗物的获取、装备和效果触发
/// </summary>
public class RelicSystem
{
    public static RelicSystem Instance { get; private set; }

    // 已拥有的遗物实例
    public List<RelicInstance> OwnedRelics { get; private set; } = new List<RelicInstance>();

    // 遗物效果事件
    public event System.Action<RelicInstance> OnRelicAcquired;
    public event System.Action<RelicInstance> OnRelicTriggered;

    public RelicSystem()
    {
        Instance = this;
    }

    /// <summary>
    /// 获取遗物
    /// </summary>
    public void AcquireRelic(string relicId)
    {
        var rewardSystem = RoguelikeGameManager.Instance?.RewardSystem;
        var data = rewardSystem?.GetRelicData(relicId);
        if (data == null)
        {
            Debug.LogWarning($"遗物 {relicId} 不存在于数据库中");
            return;
        }

        var instance = new RelicInstance(data);
        OwnedRelics.Add(instance);
        OnRelicAcquired?.Invoke(instance);
        Debug.Log($"[遗物系统] 获得遗物: {data.relicName} - {data.description}");
    }

    /// <summary>
    /// 获取遗物
    /// </summary>
    public void AcquireRelic(RelicData data)
    {
        if (data == null) return;
        var instance = new RelicInstance(data);
        OwnedRelics.Add(instance);
        OnRelicAcquired?.Invoke(instance);
    }

    /// <summary>
    /// 应用所有遗物的属性加成效果到英雄
    /// </summary>
    public void ApplyRelicEffects(List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return;

        foreach (var relic in OwnedRelics)
        {
            if (!relic.IsActive || relic.Data == null) continue;

            switch (relic.Data.effectType)
            {
                case RelicEffectType.AttackBoost:
                    foreach (var hero in heroes)
                        hero.BoostAttack(relic.Data.effectValue);
                    break;

                case RelicEffectType.DefenseBoost:
                    foreach (var hero in heroes)
                        hero.BoostDefense(relic.Data.effectValue);
                    break;

                case RelicEffectType.HealthBoost:
                    foreach (var hero in heroes)
                        hero.BoostMaxHealth(relic.Data.effectValue);
                    break;

                case RelicEffectType.SpeedBoost:
                    foreach (var hero in heroes)
                        hero.BoostSpeed(relic.Data.effectValue);
                    break;

                case RelicEffectType.CritBoost:
                    foreach (var hero in heroes)
                        hero.BoostCritRate(relic.Data.effectValue);
                    break;

                case RelicEffectType.BattleStartShield:
                    foreach (var hero in heroes)
                    {
                        int shield = Mathf.RoundToInt(hero.MaxHealth * relic.Data.effectValue);
                        hero.AddShield(shield);
                    }
                    break;

                case RelicEffectType.LifeSteal:
                    foreach (var hero in heroes)
                        hero.LifeStealRate += relic.Data.effectValue;
                    break;

                case RelicEffectType.Thorns:
                    foreach (var hero in heroes)
                        hero.BattleThornsRate += relic.Data.effectValue;
                    break;
            }
        }
    }

    /// <summary>
    /// 检查是否有指定效果的遗物
    /// </summary>
    public bool HasRelicEffect(RelicEffectType effectType)
    {
        foreach (var relic in OwnedRelics)
        {
            if (relic.IsActive && relic.Data != null && relic.Data.effectType == effectType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 获取指定效果遗物的总数值
    /// </summary>
    public float GetTotalEffectValue(RelicEffectType effectType)
    {
        float total = 0f;
        foreach (var relic in OwnedRelics)
        {
            if (relic.IsActive && relic.Data != null && relic.Data.effectType == effectType)
                total += relic.Data.effectValue;
        }
        return total;
    }

    /// <summary>
    /// 获取额外重摇次数
    /// </summary>
    public int GetExtraRerolls()
    {
        return Mathf.RoundToInt(GetTotalEffectValue(RelicEffectType.ExtraReroll));
    }

    /// <summary>
    /// 获取骰子组合效果增强倍率
    /// </summary>
    public float GetComboBoostMultiplier()
    {
        return 1f + GetTotalEffectValue(RelicEffectType.ComboBoost);
    }

    /// <summary>
    /// 尝试触发复活效果
    /// </summary>
    public bool TryRevive(Hero hero)
    {
        if (hero == null || !hero.IsDead) return false;

        foreach (var relic in OwnedRelics)
        {
            if (!relic.IsActive || relic.Data == null) continue;
            if (relic.Data.effectType == RelicEffectType.Revive && !relic.HasBeenUsedThisLevel)
            {
                // 标记遗物本关已使用
                relic.Trigger();

                // 计算恢复血量（effectValue 为百分比，凤凰羽毛默认 0.5 = 50%）
                int healAmount = Mathf.RoundToInt(hero.MaxHealth * relic.Data.effectValue);

                // 执行复活：恢复血量，清除死亡状态
                hero.Revive(healAmount);

                Debug.Log($"[遗物] {relic.Data.relicName} 触发复活！{hero.Data.heroName} 恢复 {healAmount} 生命 ({hero.CurrentHealth}/{hero.MaxHealth})");
                OnRelicTriggered?.Invoke(relic);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查是否应该获得双倍奖励
    /// </summary>
    public bool ShouldGetDoubleReward(int currentLevel)
    {
        if (!HasRelicEffect(RelicEffectType.DoubleReward)) return false;
        float interval = GetTotalEffectValue(RelicEffectType.DoubleReward);
        return currentLevel > 0 && currentLevel % Mathf.RoundToInt(interval) == 0;
    }

    /// <summary>
    /// 新关卡开始时重置所有遗物的关卡状态
    /// </summary>
    public void ResetForNewLevel()
    {
        foreach (var relic in OwnedRelics)
        {
            relic.ResetForNewLevel();
        }
    }

    /// <summary>
    /// 清除所有遗物
    /// </summary>
    public void ClearAll()
    {
        OwnedRelics.Clear();
    }

    /// <summary>
    /// 清除所有遗物（存档恢复用）
    /// </summary>
    public void ClearRelicsForLoad() { OwnedRelics.Clear(); }

    /// <summary>
    /// 获取遗物数量
    /// </summary>
    public int RelicCount => OwnedRelics.Count;
}
