using System;
using UnityEngine;

/// <summary>
/// 随机事件系统 — 关卡间的随机遭遇
/// </summary>
public static class RandomEventSystem
{
    static readonly string[] eventNames = {
        "神秘宝箱", "陷阱", "神秘商人", "古老祭坛", "流浪医者", "竞技场"
    };

    /// <summary>
    /// 触发随机事件（30%概率）
    /// </summary>
    public static RandomEvent TriggerEvent(int levelId)
    {
        if (UnityEngine.Random.value > 0.3f) return null;

        int type = UnityEngine.Random.Range(0, 6);
        var evt = new RandomEvent { eventType = (RandomEventType)type };

        switch (evt.eventType)
        {
            case RandomEventType.Treasure:
                evt.goldReward = 20 + levelId * 5;
                evt.description = $"发现一个宝箱，获得 {evt.goldReward} 金币！";
                break;

            case RandomEventType.Trap:
                evt.healthLoss = 10 + levelId * 2;
                evt.description = $"触发陷阱！全体损失 {evt.healthLoss} 生命。";
                break;

            case RandomEventType.MysteryMerchant:
                evt.discountRate = 0.5f;
                evt.description = "遇到神秘商人，所有商品5折！";
                break;

            case RandomEventType.Altar:
                evt.buffAttack = 2;
                evt.description = "在古老祭坛祈祷，全体攻击+2（本局永久）。";
                break;

            case RandomEventType.WanderingHealer:
                evt.healAmount = 20 + levelId * 3;
                evt.description = $"流浪医者出现，全体恢复 {evt.healAmount} 生命。";
                break;

            case RandomEventType.Arena:
                evt.goldReward = 30 + levelId * 10;
                evt.description = $"参加竞技场，胜利可获得 {evt.goldReward} 金币！（直接进入战斗）";
                break;
        }

        Debug.Log($"[随机事件] {eventNames[type]}: {evt.description}");
        return evt;
    }

    /// <summary>
    /// 应用事件效果
    /// </summary>
    public static void ApplyEvent(RandomEvent evt, PlayerInventory inventory, System.Collections.Generic.List<Hero> heroes)
    {
        if (evt == null) return;

        switch (evt.eventType)
        {
            case RandomEventType.Treasure:
                inventory?.AddGold(evt.goldReward);
                break;

            case RandomEventType.Trap:
                foreach (var hero in heroes)
                {
                    if (hero == null || hero.IsDead) continue;
                    hero.CurrentHealth = Mathf.Max(1, hero.CurrentHealth - evt.healthLoss);
                }
                break;

            case RandomEventType.Altar:
                foreach (var hero in heroes)
                {
                    if (hero == null) continue;
                    hero.Data.baseAttack += evt.buffAttack;
                    hero.RecalculateStats();
                }
                break;

            case RandomEventType.WanderingHealer:
                foreach (var hero in heroes)
                {
                    if (hero == null || hero.IsDead) continue;
                    hero.Heal(evt.healAmount);
                }
                break;
        }
    }
}

public enum RandomEventType
{
    Treasure,         // 宝箱
    Trap,             // 陷阱
    MysteryMerchant,  // 神秘商人
    Altar,            // 古老祭坛
    WanderingHealer,  // 流浪医者
    Arena             // 竞技场
}

[Serializable]
public class RandomEvent
{
    public RandomEventType eventType;
    public string description;
    public int goldReward;
    public int healthLoss;
    public float discountRate;
    public int buffAttack;
    public int healAmount;
}
