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
    /// 触发随机事件（概率由JSON配置）— 旧流程兼容
    /// </summary>
    public static RandomEvent TriggerEvent(int levelId)
    {
        float triggerChance = BalanceProvider.GetRandomEventTriggerChance();
        if (UnityEngine.Random.value > triggerChance) return null;

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
    /// 填充事件基础数据 — JSON优先，fallback到硬编码
    /// </summary>
    static void PopulateEvent(RandomEvent evt, int levelId)
    {
        // BE-11: 尝试从JSON配置读取
        var jsonEntry = BalanceProvider.GetRandomEvent(evt.eventType.ToString());
        if (jsonEntry != null)
        {
            PopulateEventFromJson(evt, jsonEntry, levelId);
            return;
        }

        // Fallback: 原有硬编码
        PopulateEventHardcoded(evt, levelId);
    }

    /// <summary>
    /// 从JSON配置填充事件数据
    /// </summary>
    static void PopulateEventFromJson(RandomEvent evt, RandomEventEntry entry, int levelId)
    {
        // 解析公式变量
        float goldReward = EvaluateFormula(entry.gold_formula, levelId);
        float healthLoss = EvaluateFormula(entry.health_loss_formula, levelId);
        float buffAttack = EvaluateFormula(entry.buff_attack_formula, levelId);
        float healAmount = EvaluateFormula(entry.heal_formula, levelId);

        evt.goldReward = Mathf.RoundToInt(goldReward);
        evt.healthLoss = Mathf.RoundToInt(healthLoss);
        evt.buffAttack = Mathf.RoundToInt(buffAttack);
        evt.healAmount = Mathf.RoundToInt(healAmount);
        evt.discountRate = entry.discount_rate > 0 ? entry.discount_rate : 0.5f;

        // 替换描述模板中的变量
        string desc = entry.description_template ?? "";
        desc = desc.Replace("{goldReward}", evt.goldReward.ToString());
        desc = desc.Replace("{healthLoss}", evt.healthLoss.ToString());
        desc = desc.Replace("{buffAttack}", evt.buffAttack.ToString());
        desc = desc.Replace("{healAmount}", evt.healAmount.ToString());
        evt.description = desc;
    }

    /// <summary>
    /// 硬编码fallback（原PopulateEvent逻辑）
    /// </summary>
    static void PopulateEventHardcoded(RandomEvent evt, int levelId)
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
    /// 为事件生成多选项（地图节点模式）— JSON优先，fallback到硬编码
    /// </summary>
    static void GenerateOptionsForEvent(RandomEvent evt, int levelId)
    {
        evt.options.Clear();

        // BE-11: 尝试从JSON配置读取选项
        var jsonEntry = BalanceProvider.GetRandomEvent(evt.eventType.ToString());
        if (jsonEntry?.options != null && jsonEntry.options.Count > 0)
        {
            GenerateOptionsFromJson(evt, jsonEntry, levelId);
            return;
        }

        // Fallback: 原有硬编码选项
        GenerateOptionsHardcoded(evt, levelId);
    }

    /// <summary>
    /// 从JSON配置生成选项
    /// </summary>
    static void GenerateOptionsFromJson(RandomEvent evt, RandomEventEntry entry, int levelId)
    {
        // 预计算公式变量
        float goldReward = EvaluateFormula(entry.gold_formula, levelId);
        float healthLoss = EvaluateFormula(entry.health_loss_formula, levelId);
        float buffAttack = EvaluateFormula(entry.buff_attack_formula, levelId);
        float healAmount = EvaluateFormula(entry.heal_formula, levelId);

        foreach (var opt in entry.options)
        {
            var option = new EventOption
            {
                optionText = opt.optionText,
                effectType = ParseEffectType(opt.effectType),
                effectValue = EvaluateFormulaWithVars(opt.effectFormula, levelId, goldReward, healthLoss, buffAttack, healAmount),
                isRiskOption = opt.isRiskOption,
                goldCost = opt.goldCost
            };

            // 次要效果
            if (!string.IsNullOrEmpty(opt.secondaryEffect))
            {
                option.secondaryEffect = ParseEffectType(opt.secondaryEffect);
                option.secondaryValue = EvaluateFormulaWithVars(opt.secondaryFormula, levelId, goldReward, healthLoss, buffAttack, healAmount);
            }

            // 风险失败效果
            if (!string.IsNullOrEmpty(opt.riskFailEffectType))
            {
                option.riskFailEffectType = ParseEffectType(opt.riskFailEffectType);
                option.riskFailValue = EvaluateFormulaWithVars(opt.riskFailFormula, levelId, goldReward, healthLoss, buffAttack, healAmount);
            }

            // 生成效果描述
            option.effectDescription = BuildOptionDescription(option);

            evt.options.Add(option);
        }
    }

    /// <summary>
    /// 硬编码fallback选项（原GenerateOptionsForEvent逻辑）
    /// </summary>
    static void GenerateOptionsHardcoded(RandomEvent evt, int levelId)
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

    // ========== BE-11: JSON 配置辅助方法 ==========

    /// <summary>
    /// 解析简单公式 — 支持 "20 + level * 5" 格式
    /// 变量: level, goldReward, healthLoss, buffAttack, healAmount
    /// </summary>
    static float EvaluateFormula(string formula, int level)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;
        return EvaluateFormulaWithVars(formula, level, 0, 0, 0, 0);
    }

    /// <summary>
    /// 解析公式，支持预计算变量替换
    /// </summary>
    static float EvaluateFormulaWithVars(string formula, int level, float goldReward, float healthLoss, float buffAttack, float healAmount)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;

        try
        {
            // 替换变量名
            string expr = formula
                .Replace("goldReward", goldReward.ToString("F2"))
                .Replace("healthLoss", healthLoss.ToString("F2"))
                .Replace("buffAttack", buffAttack.ToString("F2"))
                .Replace("healAmount", healAmount.ToString("F2"))
                .Replace("level", level.ToString());

            // 处理 max(a, b) 函数
            if (expr.Contains("max("))
            {
                var match = System.Text.RegularExpressions.Regex.Match(expr, @"max\(\s*([^,]+)\s*,\s*([^)]+)\s*\)");
                if (match.Success)
                {
                    float a = EvaluateSimpleExpr(match.Groups[1].Value.Trim());
                    float b = EvaluateSimpleExpr(match.Groups[2].Value.Trim());
                    return Mathf.Max(a, b);
                }
            }

            return EvaluateSimpleExpr(expr);
        }
        catch
        {
            Debug.LogWarning($"[RandomEventSystem] 公式解析失败: {formula}");
            return 0f;
        }
    }

    /// <summary>
    /// 解析简单数学表达式（支持 +, -, *, / 和括号）
    /// </summary>
    static float EvaluateSimpleExpr(string expr)
    {
        if (string.IsNullOrEmpty(expr)) return 0f;
        expr = expr.Trim();

        // 递归处理括号
        while (expr.Contains("("))
        {
            int open = expr.LastIndexOf('(');
            int close = expr.IndexOf(')', open);
            if (close < 0) break;
            string inner = expr.Substring(open + 1, close - open - 1);
            float val = EvaluateSimpleExpr(inner);
            expr = expr.Substring(0, open) + val.ToString("F6") + expr.Substring(close + 1);
        }

        // 处理加减法（从左到右）
        // 先找最外层的 + 和 -
        int addIdx = -1, subIdx = -1;
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            if (expr[i] == '+') { addIdx = i; break; }
            if (expr[i] == '-' && i > 0 && !"+-*/".Contains(expr[i - 1].ToString())) { subIdx = i; break; }
        }
        if (addIdx > 0)
            return EvaluateSimpleExpr(expr.Substring(0, addIdx)) + EvaluateSimpleExpr(expr.Substring(addIdx + 1));
        if (subIdx > 0)
            return EvaluateSimpleExpr(expr.Substring(0, subIdx)) - EvaluateSimpleExpr(expr.Substring(subIdx + 1));

        // 处理乘除法
        int mulIdx = expr.LastIndexOf('*');
        int divIdx = expr.LastIndexOf('/');
        if (mulIdx >= 0)
            return EvaluateSimpleExpr(expr.Substring(0, mulIdx)) * EvaluateSimpleExpr(expr.Substring(mulIdx + 1));
        if (divIdx >= 0)
        {
            float divisor = EvaluateSimpleExpr(expr.Substring(divIdx + 1));
            return divisor != 0 ? EvaluateSimpleExpr(expr.Substring(0, divIdx)) / divisor : 0f;
        }

        // 最终数值
        expr = expr.Trim();
        if (expr.StartsWith("-"))
        {
            float v;
            if (float.TryParse(expr.Substring(1), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return -v;
        }
        else
        {
            float v;
            if (float.TryParse(expr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
        }
        return 0f;
    }

    /// <summary>
    /// 字符串 → EventEffectType 枚举
    /// </summary>
    static EventEffectType ParseEffectType(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr)) return EventEffectType.None;
        if (System.Enum.TryParse(typeStr, out EventEffectType result))
            return result;
        return EventEffectType.None;
    }

    /// <summary>
    /// 根据选项效果生成描述文本
    /// </summary>
    static string BuildOptionDescription(EventOption opt)
    {
        var parts = new List<string>();

        if (opt.effectType != EventEffectType.None)
            parts.Add(DescribeEffect(opt.effectType, opt.effectValue));

        if (opt.secondaryEffect != EventEffectType.None)
            parts.Add(DescribeEffect(opt.secondaryEffect, opt.secondaryValue));

        if (opt.goldCost > 0)
            parts.Insert(0, $"花费{opt.goldCost}金币");

        if (opt.isRiskOption)
            parts.Add("（风险）");

        return string.Join("，", parts);
    }

    /// <summary>
    /// 描述单个效果
    /// </summary>
    static string DescribeEffect(EventEffectType type, float value)
    {
        int v = Mathf.RoundToInt(value);
        return type switch
        {
            EventEffectType.AddGold => $"金币 {(v >= 0 ? "+" : "")}{v}",
            EventEffectType.AddHealth => v >= 0 ? $"生命 +{v}" : $"生命 {v}",
            EventEffectType.AddAttack => $"攻击 +{v}",
            EventEffectType.AddRandomCard => "获得随机卡牌",
            EventEffectType.AddRandomRelic => "获得随机遗物",
            EventEffectType.HealPercent => $"回复{v}%最大生命",
            EventEffectType.Discount => $"商店折扣{v}%",
            EventEffectType.TriggerBattle => $"进入战斗，胜利获得{v}金币",
            _ => ""
        };
    }
}
