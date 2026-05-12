using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 事件效果引擎 — 执行 EventEffect 定义的各类效果
/// 支持 gold/heal/damage/card/relic/dice/enhance/exp/buff_atk/buff_def 等效果类型
/// 每个效果执行前先进行概率判定，未通过则跳过并返回失败文字
/// </summary>
public static class EventEffectEngine
{
    // ========== 公共接口 ==========

    /// <summary>
    /// 执行单个效果，返回结果描述文字
    /// 先检查 probability，通过后才执行具体效果
    /// </summary>
    /// <param name="effect">要执行的效果</param>
    /// <returns>执行结果描述（成功或失败文字）</returns>
    public static string ExecuteEffect(EventEffect effect)
    {
        if (effect == null) return "";

        // 概率判定
        if (effect.probability < 1f && UnityEngine.Random.value > effect.probability)
        {
            string failMsg = !string.IsNullOrEmpty(effect.failText)
                ? effect.failText
                : "运气不佳，什么都没发生。";
            Debug.Log($"[事件效果] 概率判定失败({effect.probability:P0}): {failMsg}");
            return failMsg;
        }

        // 按类型分发执行
        string result = effect.type switch
        {
            "gold"      => ExecuteGold(effect),
            "heal"      => ExecuteHeal(effect),
            "damage"    => ExecuteDamage(effect),
            "card"      => ExecuteCard(effect),
            "relic"     => ExecuteRelic(effect),
            "dice"      => ExecuteDice(effect),
            "enhance"   => ExecuteEnhance(effect),
            "exp"       => ExecuteExp(effect),
            "buff_atk"  => ExecuteBuffAttack(effect),
            "buff_def"  => ExecuteBuffDefense(effect),
            _ => $"未知效果类型: {effect.type}"
        };

        Debug.Log($"[事件效果] {effect.type}(值={effect.value}, 目标={effect.target}): {result}");
        return result;
    }

    /// <summary>
    /// 批量执行效果列表，返回所有结果的拼接文字
    /// </summary>
    /// <param name="effects">效果列表</param>
    /// <returns>所有效果的执行结果，以换行分隔</returns>
    public static string ExecuteEffects(List<EventEffect> effects)
    {
        if (effects == null || effects.Count == 0) return "";

        var results = new List<string>();
        foreach (var effect in effects)
        {
            string result = ExecuteEffect(effect);
            if (!string.IsNullOrEmpty(result))
                results.Add(result);
        }
        return string.Join("\n", results);
    }

    // ========== 效果类型实现 ==========

    /// <summary>
    /// 金币效果 — 正值增加金币，负值扣除金币
    /// 使用 PlayerInventory.AddGold / SpendGold
    /// </summary>
    static string ExecuteGold(EventEffect effect)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null) return "背包系统不可用，金币效果未执行";

        int amount = effect.value;
        if (amount >= 0)
        {
            inventory.AddGold(amount);
            return $"获得 {amount} 金币";
        }
        else
        {
            int cost = Mathf.Abs(amount);
            if (inventory.Gold < cost)
                return $"金币不足（需要{cost}，持有{inventory.Gold}），效果未执行";
            inventory.SpendGold(cost);
            return $"花费 {cost} 金币";
        }
    }

    /// <summary>
    /// 治疗效果 — 恢复英雄生命值
    /// 目标 all_heroes: 全体英雄治疗; random_hero: 随机一个英雄; self: 同 random_hero
    /// 使用 hero.Heal
    /// </summary>
    static string ExecuteHeal(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可治疗的英雄";

        int amount = Mathf.Max(0, effect.value);
        var targets = ResolveTargets(effect.target, heroes);

        foreach (var hero in targets)
        {
            if (hero != null && !hero.IsDead)
                hero.Heal(amount);
        }

        int count = targets.Count;
        return count > 1
            ? $"全体英雄恢复 {amount} 生命"
            : $"恢复 {amount} 生命";
    }

    /// <summary>
    /// 伤害效果 — 对英雄造成伤害
    /// 使用 hero.TakeDamage
    /// </summary>
    static string ExecuteDamage(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可受伤的英雄";

        int amount = Mathf.Max(0, effect.value);
        var targets = ResolveTargets(effect.target, heroes);

        foreach (var hero in targets)
        {
            if (hero != null && !hero.IsDead)
                hero.TakeDamage(amount);
        }

        int count = targets.Count;
        return count > 1
            ? $"全体英雄受到 {amount} 点伤害"
            : $"受到 {amount} 点伤害";
    }

    /// <summary>
    /// 卡牌效果 — 获得随机卡牌
    /// 使用 PlayerInventory.AddCard
    /// </summary>
    static string ExecuteCard(EventEffect effect)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null) return "背包系统不可用，卡牌效果未执行";

        var cardDatabase = GameData.GetAllCardData();
        if (cardDatabase == null || cardDatabase.Count == 0)
        {
            // fallback: 给金币替代
            inventory.AddGold(25);
            return "没有可用卡牌，获得25金币";
        }

        int count = Mathf.Max(1, effect.value);
        var names = new List<string>();

        for (int i = 0; i < count; i++)
        {
            var chosen = cardDatabase[UnityEngine.Random.Range(0, cardDatabase.Count)];
            var instance = new CardInstance(chosen);
            inventory.AddCard(instance);
            names.Add(chosen.cardName);
        }

        return count == 1
            ? $"获得卡牌: {names[0]}"
            : $"获得 {count} 张卡牌: {string.Join("、", names)}";
    }

    /// <summary>
    /// 遗物效果 — 获得随机遗物
    /// 使用 RelicSystem.AcquireRelic
    /// </summary>
    static string ExecuteRelic(EventEffect effect)
    {
        var relicSys = RoguelikeGameManager.Instance?.RelicSystem;
        var rewardSys = RoguelikeGameManager.Instance?.RewardSystem;
        if (relicSys == null || rewardSys == null)
        {
            // fallback
            PlayerInventory.Instance?.AddGold(40);
            return "遗物系统不可用，获得40金币";
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

    /// <summary>
    /// 骰子面升级效果 — 随机升级一个骰子的面效果
    /// 使用 Dice.UpgradeFace
    /// </summary>
    static string ExecuteDice(EventEffect effect)
    {
        // 尝试获取 DiceRoller（通过 BattleManager 或 DiceRollPanel）
        var battleMgr = BattleManager.Instance;
        if (battleMgr == null)
        {
            // 非战斗状态，给替代奖励
            PlayerInventory.Instance?.AddGold(20);
            return "当前无法升级骰子面，获得20金币";
        }

        // 尝试获取骰子
        var diceRoller = battleMgr.GetType().GetProperty("DiceRoller")?.GetValue(battleMgr) as DiceRoller;
        if (diceRoller == null || diceRoller.Dices == null || diceRoller.Dices.Length == 0)
        {
            PlayerInventory.Instance?.AddGold(20);
            return "骰子系统不可用，获得20金币";
        }

        // 随机选一个骰子，随机升级一个面
        int diceIdx = UnityEngine.Random.Range(0, diceRoller.Dices.Length);
        var dice = diceRoller.Dices[diceIdx];
        int faceIdx = UnityEngine.Random.Range(0, dice.Faces.Length);

        string[] possibleEffects = { "double", "critical", "shield", "heal", "extra_roll" };
        string chosenEffect = possibleEffects[UnityEngine.Random.Range(0, possibleEffects.Length)];

        dice.UpgradeFace(faceIdx, chosenEffect);

        return $"骰子{diceIdx + 1}的第{faceIdx + 1}面升级为「{chosenEffect}」效果";
    }

    /// <summary>
    /// 装备强化效果 — 随机强化一件已装备的装备
    /// 使用 EquipmentEnhancer.Enhance（免费强化，忽略金币）
    /// </summary>
    static string ExecuteEnhance(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可强化装备的英雄";

        // 收集所有已装备的装备
        var equippedItems = new List<EquipmentData>();
        foreach (var hero in heroes)
        {
            if (hero == null) continue;
            foreach (var item in hero.EquippedItems.Values)
            {
                if (item != null && item.enhanceLevel < item.maxEnhanceLevel)
                    equippedItems.Add(item);
            }
        }

        if (equippedItems.Count == 0)
            return "没有可强化的装备";

        // 随机选一件强化
        var chosen = equippedItems[UnityEngine.Random.Range(0, equippedItems.Count)];

        // 使用强化系统（免费强化：直接提升等级）
        chosen.enhanceLevel++;
        var enhancer = EquipmentEnhancer.Instance;
        enhancer?.OnEnhanceComplete?.Invoke(chosen, EnhanceResult.Success);

        return $"装备「{chosen.equipmentName}」强化至 +{chosen.enhanceLevel}";
    }

    /// <summary>
    /// 经验效果 — 给英雄添加经验值
    /// 使用 HeroExpSystem.GainExp
    /// </summary>
    static string ExecuteExp(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可获得经验的英雄";

        int amount = Mathf.Max(0, effect.value);
        var expSystem = HeroExpSystem.Instance;

        if (expSystem != null)
        {
            var targets = ResolveTargets(effect.target, heroes);
            foreach (var hero in targets)
            {
                if (hero != null && !hero.IsDead)
                    expSystem.GainExp(hero, amount, ExpSource.EventBonus);
            }
            int count = targets.Count;
            return count > 1
                ? $"全体英雄获得 {amount} 经验"
                : $"获得 {amount} 经验";
        }
        else
        {
            // fallback: 直接加经验
            var targets = ResolveTargets(effect.target, heroes);
            foreach (var hero in targets)
            {
                if (hero != null && !hero.IsDead)
                    hero.AddExp(amount);
            }
            return $"获得 {amount} 经验";
        }
    }

    /// <summary>
    /// 攻击力增益效果 — 正值增加攻击力，负值降低攻击力
    /// 使用 hero.Data.baseAttack + RecalculateStats
    /// </summary>
    static string ExecuteBuffAttack(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可增益的英雄";

        int amount = effect.value;
        var targets = ResolveTargets(effect.target, heroes);

        foreach (var hero in targets)
        {
            if (hero != null)
            {
                hero.Data.baseAttack += amount;
                hero.RecalculateStats();
            }
        }

        int count = targets.Count;
        return count > 1
            ? (amount >= 0 ? $"全体英雄攻击力 +{amount}" : $"全体英雄攻击力 {amount}")
            : (amount >= 0 ? $"攻击力 +{amount}" : $"攻击力 {amount}");
    }

    /// <summary>
    /// 防御力增益效果 — 正值增加防御力，负值降低防御力
    /// 使用 hero.Data.baseDefense + RecalculateStats
    /// </summary>
    static string ExecuteBuffDefense(EventEffect effect)
    {
        var heroes = GetHeroes();
        if (heroes == null || heroes.Count == 0) return "没有可增益的英雄";

        int amount = effect.value;
        var targets = ResolveTargets(effect.target, heroes);

        foreach (var hero in targets)
        {
            if (hero != null)
            {
                hero.Data.baseDefense += amount;
                hero.RecalculateStats();
            }
        }

        int count = targets.Count;
        return count > 1
            ? (amount >= 0 ? $"全体英雄防御力 +{amount}" : $"全体英雄防御力 {amount}")
            : (amount >= 0 ? $"防御力 +{amount}" : $"防御力 {amount}");
    }

    // ========== 辅助方法 ==========

    /// <summary>
    /// 获取当前玩家英雄列表
    /// 优先从 RoguelikeGameManager 获取，fallback 从 CardDeck 获取
    /// </summary>
    static List<Hero> GetHeroes()
    {
        var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
        if (heroes != null && heroes.Count > 0) return heroes;

        // fallback
        var deck = CardDeck.Instance;
        return deck?.fieldHeroes;
    }

    /// <summary>
    /// 根据目标字符串解析实际目标英雄列表
    /// all_heroes → 全体; random_hero → 随机一个; self/其他 → 随机一个
    /// </summary>
    static List<Hero> ResolveTargets(string target, List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return new List<Hero>();

        switch (target)
        {
            case "all_heroes":
                return heroes;

            case "random_hero":
            case "self":
            default:
                // 从存活英雄中随机选一个
                var alive = heroes.FindAll(h => h != null && !h.IsDead);
                if (alive.Count == 0) return new List<Hero>();
                return new List<Hero> { alive[UnityEngine.Random.Range(0, alive.Count)] };
        }
    }
}
