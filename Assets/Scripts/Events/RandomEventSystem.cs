using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件效果类型 — 每个选项可携带的具体效果
/// </summary>
public enum EventEffectType
{
    None,             // 无效果（纯叙事）
    AddGold,          // 加金币
    AddHealth,        // 加/减生命
    AddAttack,        // 加攻击
    AddRandomCard,    // 获得随机卡牌
    AddRandomRelic,   // 获得随机遗物
    HealPercent,      // 按百分比回血
    Discount,         // 商店折扣
    TriggerBattle     // 触发额外战斗
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

    /// <summary>事件选项列表（地图节点模式使用）</summary>
    public List<EventOption> options = new List<EventOption>();
}

/// <summary>
/// 随机事件系统 — 关卡间的随机遭遇 + 地图Event节点触发
/// </summary>
public static class RandomEventSystem
{
    static readonly string[] eventNames = {
        "神秘宝箱", "陷阱", "神秘商人", "古老祭坛", "流浪医者", "竞技场"
    };

    /// <summary>
    /// 触发随机事件（30%概率）— 旧流程兼容
    /// </summary>
    public static RandomEvent TriggerEvent(int levelId)
    {
        if (UnityEngine.Random.value > 0.3f) return null;

        int type = UnityEngine.Random.Range(0, 6);
        var evt = new RandomEvent { eventType = (RandomEventType)type };

        PopulateEvent(evt, levelId);

        Debug.Log($"[随机事件] {eventNames[type]}: {evt.description}");
        return evt;
    }

    /// <summary>
    /// 为地图Event节点强制生成事件（100%触发，带选项）
    /// </summary>
    public static RandomEvent TriggerEventForMapNode(int levelId)
    {
        int type = UnityEngine.Random.Range(0, 6);
        var evt = new RandomEvent { eventType = (RandomEventType)type };

        PopulateEvent(evt, levelId);
        GenerateOptionsForEvent(evt, levelId);

        Debug.Log($"[随机事件-地图] {eventNames[type]}: {evt.description} (选项: {evt.options.Count})");
        return evt;
    }

    /// <summary>
    /// 填充事件基础数据
    /// </summary>
    static void PopulateEvent(RandomEvent evt, int levelId)
    {
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
                evt.buffAttack = 2 + Mathf.RoundToInt(levelId * 0.3f);
                evt.description = $"在古老祭坛祈祷，全体攻击+{evt.buffAttack}（本局永久）。";
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
    }

    /// <summary>
    /// 为事件生成多选项（地图节点模式）
    /// </summary>
    static void GenerateOptionsForEvent(RandomEvent evt, int levelId)
    {
        evt.options.Clear();

        switch (evt.eventType)
        {
            case RandomEventType.Treasure:
                evt.options.Add(new EventOption
                {
                    optionText = "打开宝箱",
                    effectDescription = $"获得 {evt.goldReward} 金币",
                    effectType = EventEffectType.AddGold,
                    effectValue = evt.goldReward,
                    isRiskOption = false
                });
                evt.options.Add(new EventOption
                {
                    optionText = "小心检查再打开",
                    effectDescription = $"获得 {evt.goldReward / 2} 金币，全体恢复 {10 + levelId * 2} 生命",
                    effectType = EventEffectType.AddGold,
                    effectValue = evt.goldReward / 2,
                    secondaryEffect = EventEffectType.AddHealth,
                    secondaryValue = 10 + levelId * 2,
                    isRiskOption = false
                });
                evt.options.Add(new EventOption
                {
                    optionText = "用魔法探测（风险）",
                    effectDescription = levelId > 5 ? "获得大量金币或触发陷阱！" : "可能获得额外卡牌或一无所有",
                    effectType = EventEffectType.AddGold,
                    effectValue = Mathf.RoundToInt(evt.goldReward * 1.5f),
                    isRiskOption = true,
                    riskFailEffectType = EventEffectType.AddHealth,
                    riskFailValue = -(5 + levelId * 3)
                });
                break;

            case RandomEventType.Trap:
                evt.options.Add(new EventOption
                {
                    optionText = "硬抗",
                    effectDescription = $"全体损失 {evt.healthLoss} 生命",
                    effectType = EventEffectType.AddHealth,
                    effectValue = -evt.healthLoss,
                    isRiskOption = true
                });
                evt.options.Add(new EventOption
                {
                    optionText = "闪避翻滚",
                    effectDescription = "50%概率完全闪避，失败则损失更多生命",
                    effectType = EventEffectType.AddHealth,
                    effectValue = -Mathf.RoundToInt(evt.healthLoss * 0.5f),
                    isRiskOption = true,
                    riskFailEffectType = EventEffectType.AddHealth,
                    riskFailValue = -Mathf.RoundToInt(evt.healthLoss * 1.5f)
                });
                evt.options.Add(new EventOption
                {
                    optionText = "使用道具抵挡",
                    effectDescription = $"花费10金币抵消陷阱，仅损失少量生命",
                    effectType = EventEffectType.AddHealth,
                    effectValue = -Mathf.Max(1, evt.healthLoss / 4),
                    isRiskOption = false,
                    goldCost = 10
                });
                break;

            case RandomEventType.MysteryMerchant:
                evt.options.Add(new EventOption
                {
                    optionText = "购买神秘卡牌（30金币）",
                    effectDescription = "获得一张随机卡牌",
                    effectType = EventEffectType.AddRandomCard,
                    effectValue = 0,
                    isRiskOption = false,
                    goldCost = 30
                });
                evt.options.Add(new EventOption
                {
                    optionText = "购买遗物碎片（50金币）",
                    effectDescription = "获得一个随机遗物",
                    effectType = EventEffectType.AddRandomRelic,
                    effectValue = 0,
                    isRiskOption = false,
                    goldCost = 50
                });
                evt.options.Add(new EventOption
                {
                    optionText = "礼貌拒绝",
                    effectDescription = "无事发生",
                    effectType = EventEffectType.None,
                    isRiskOption = false
                });
                break;

            case RandomEventType.Altar:
                evt.options.Add(new EventOption
                {
                    optionText = "祈祷（献祭生命）",
                    effectDescription = $"全体攻击+{evt.buffAttack}，但损失 {10 + levelId} 生命",
                    effectType = EventEffectType.AddAttack,
                    effectValue = evt.buffAttack,
                    secondaryEffect = EventEffectType.AddHealth,
                    secondaryValue = -(10 + levelId),
                    isRiskOption = false
                });
                evt.options.Add(new EventOption
                {
                    optionText = "虔诚祭拜（献祭金币）",
                    effectDescription = $"全体攻击+{Mathf.Max(1, evt.buffAttack - 1)}，花费20金币",
                    effectType = EventEffectType.AddAttack,
                    effectValue = Mathf.Max(1, evt.buffAttack - 1),
                    isRiskOption = false,
                    goldCost = 20
                });
                evt.options.Add(new EventOption
                {
                    optionText = "亵渎祭坛（风险）",
                    effectDescription = "可能获得大量属性或被诅咒",
                    effectType = EventEffectType.AddAttack,
                    effectValue = evt.buffAttack * 2,
                    isRiskOption = true,
                    riskFailEffectType = EventEffectType.AddHealth,
                    riskFailValue = -(20 + levelId * 3)
                });
                break;

            case RandomEventType.WanderingHealer:
                evt.options.Add(new EventOption
                {
                    optionText = "请求治疗",
                    effectDescription = $"全体恢复 {evt.healAmount} 生命",
                    effectType = EventEffectType.AddHealth,
                    effectValue = evt.healAmount,
                    isRiskOption = false
                });
                evt.options.Add(new EventOption
                {
                    optionText = "请求强化药水",
                    effectDescription = $"恢复少量生命({Mathf.RoundToInt(evt.healAmount * 0.3f)})，攻击+1",
                    effectType = EventEffectType.AddHealth,
                    effectValue = Mathf.RoundToInt(evt.healAmount * 0.3f),
                    secondaryEffect = EventEffectType.AddAttack,
                    secondaryValue = 1,
                    isRiskOption = false
                });
                break;

            case RandomEventType.Arena:
                evt.options.Add(new EventOption
                {
                    optionText = "参加竞技场",
                    effectDescription = $"进入战斗，胜利获得 {evt.goldReward} 金币",
                    effectType = EventEffectType.TriggerBattle,
                    effectValue = evt.goldReward,
                    isRiskOption = false
                });
                evt.options.Add(new EventOption
                {
                    optionText = "放弃",
                    effectDescription = "安全离开",
                    effectType = EventEffectType.None,
                    isRiskOption = false
                });
                break;
        }
    }

    /// <summary>
    /// 应用事件效果（旧模式兼容）
    /// </summary>
    public static void ApplyEvent(RandomEvent evt, PlayerInventory inventory, List<Hero> heroes)
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

    /// <summary>
    /// 应用指定选项的效果（地图节点模式）
    /// </summary>
    public static string ApplyOptionEffect(EventOption option)
    {
        if (option == null) return "";

        // 处理金币花费
        if (option.goldCost > 0)
        {
            var inv = PlayerInventory.Instance;
            if (inv != null && inv.Gold < option.goldCost)
            {
                return "金币不足！";
            }
            inv?.SpendGold(option.goldCost);
        }

        bool isRiskSuccess = true;

        // 风险判定：50%概率失败
        if (option.isRiskOption && option.riskFailEffectType != EventEffectType.None)
        {
            isRiskSuccess = UnityEngine.Random.value >= 0.5f;
        }

        var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
        var inventory = PlayerInventory.Instance;
        string resultDesc = "";

        EventEffectType primaryEffect = isRiskSuccess ? option.effectType : option.riskFailEffectType;
        float primaryValue = isRiskSuccess ? option.effectValue : option.riskFailValue;

        resultDesc = ApplySingleEffect(primaryEffect, primaryValue, heroes, inventory);

        // 次要效果（成功时才触发）
        if (isRiskSuccess && option.secondaryEffect != EventEffectType.None)
        {
            string secondaryResult = ApplySingleEffect(option.secondaryEffect, option.secondaryValue, heroes, inventory);
            if (!string.IsNullOrEmpty(secondaryResult))
                resultDesc += "\n" + secondaryResult;
        }

        if (!isRiskSuccess && option.isRiskOption)
        {
            resultDesc = "【失败】" + resultDesc;
        }

        Debug.Log($"[随机事件] 应用选项效果: {resultDesc}");
        return resultDesc;
    }

    /// <summary>
    /// 应用单个效果，返回效果描述
    /// </summary>
    static string ApplySingleEffect(EventEffectType effectType, float value, List<Hero> heroes, PlayerInventory inventory)
    {
        switch (effectType)
        {
            case EventEffectType.AddGold:
                int gold = Mathf.RoundToInt(value);
                inventory?.AddGold(gold);
                return $"金币 {(gold >= 0 ? "+" : "")}{gold}";

            case EventEffectType.AddHealth:
                int hp = Mathf.RoundToInt(value);
                if (heroes != null)
                {
                    foreach (var hero in heroes)
                    {
                        if (hero == null || hero.IsDead) continue;
                        if (hp >= 0)
                            hero.Heal(hp);
                        else
                            hero.CurrentHealth = Mathf.Max(1, hero.CurrentHealth + hp); // hp is negative
                    }
                }
                return hp >= 0 ? $"生命 +{hp}" : $"生命 {hp}";

            case EventEffectType.AddAttack:
                int atk = Mathf.RoundToInt(value);
                if (heroes != null)
                {
                    foreach (var hero in heroes)
                    {
                        if (hero == null) continue;
                        hero.Data.baseAttack += atk;
                        hero.RecalculateStats();
                    }
                }
                return $"攻击 +{atk}";

            case EventEffectType.HealPercent:
                float pct = value / 100f;
                if (heroes != null)
                {
                    foreach (var hero in heroes)
                    {
                        if (hero == null || hero.IsDead) continue;
                        int healAmt = Mathf.RoundToInt(hero.MaxHealth * pct);
                        hero.Heal(healAmt);
                    }
                }
                return $"回复 {value}% 最大生命";

            case EventEffectType.AddRandomCard:
                return GrantRandomCard(inventory);

            case EventEffectType.AddRandomRelic:
                return GrantRandomRelic();

            case EventEffectType.Discount:
                // 折扣效果由商店面板读取
                return $"商店折扣 {value * 100:F0}%";

            case EventEffectType.TriggerBattle:
                // 竞技场战斗：直接进入骰子阶段
                var gsm = GameStateMachine.Instance;
                if (gsm != null)
                {
                    // 标记竞技场奖励金币
                    RoguelikeGameManager.Instance?.SetArenaGoldReward(Mathf.RoundToInt(value));
                    gsm.ChangeState(GameState.DiceRoll);
                }
                return $"进入竞技场战斗！胜利获得 {Mathf.RoundToInt(value)} 金币";

            default:
                return "";
        }
    }

    /// <summary>
    /// 给予随机卡牌
    /// </summary>
    static string GrantRandomCard(PlayerInventory inventory)
    {
        // 从卡牌数据库随机选一张
        var cardDatabase = GameData.GetAllCardData();
        if (cardDatabase == null || cardDatabase.Count == 0)
        {
            // fallback: 给金币替代
            inventory?.AddGold(25);
            return "没有可用卡牌，获得25金币";
        }

        var chosen = cardDatabase[UnityEngine.Random.Range(0, cardDatabase.Count)];
        var instance = new CardInstance(chosen);
        inventory?.AddCard(instance);
        return $"获得卡牌: {chosen.cardName}";
    }

    /// <summary>
    /// 给予随机遗物
    /// </summary>
    static string GrantRandomRelic()
    {
        var relicSys = RoguelikeGameManager.Instance?.RelicSystem;
        var rewardSys = RoguelikeGameManager.Instance?.RewardSystem;
        if (relicSys == null || rewardSys == null)
        {
            // fallback
            PlayerInventory.Instance?.AddGold(40);
            return "没有可用遗物，获得40金币";
        }

        var allRelics = rewardSys.GetAllRelics();
        var acquired = relicSys.OwnedRelics;
        var available = new List<RelicData>();

        foreach (var r in allRelics)
        {
            bool alreadyHas = false;
            foreach (var owned in acquired)
            {
                if (owned.Data != null && owned.Data.relicId == r.relicId)
                {
                    alreadyHas = true;
                    break;
                }
            }
            if (!alreadyHas) available.Add(r);
        }

        if (available.Count == 0)
        {
            PlayerInventory.Instance?.AddGold(40);
            return "遗物已全部收集，获得40金币";
        }

        var chosen = available[UnityEngine.Random.Range(0, available.Count)];
        relicSys.AcquireRelic(chosen);
        return $"获得遗物: {chosen.relicName}";
    }
}
