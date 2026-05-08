using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 奖励类型
/// </summary>
public enum RewardType
{
    NewUnit,          // 新单位（加入战斗AI控制）
    DiceFaceUpgrade,  // 骰子面升级
    StatBoost,        // 属性强化（全体或单体）
    Relic             // 遗物（被动效果）
}

/// <summary>
/// 属性强化目标
/// </summary>
public enum StatBoostTarget
{
    AllHeroes,    // 全体
    RandomHero    // 随机单体
}

/// <summary>
/// 属性类型
/// </summary>
public enum StatType
{
    Health,
    Attack,
    Defense,
    Speed,
    CritRate
}

/// <summary>
/// 单个奖励选项
/// </summary>
public class RewardOption
{
    public RewardType Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Rarity { get; set; } // 1=普通, 2=稀有, 3=史诗

    // NewUnit
    public string HeroTemplateName { get; set; }

    // DiceFaceUpgrade
    public int DiceIndex { get; set; }     // 哪个骰子
    public int FaceIndex { get; set; }     // 哪一面
    public int NewFaceValue { get; set; }  // 新面值

    // StatBoost
    public StatType BoostStat { get; set; }
    public float BoostAmount { get; set; }  // 百分比加成 (0.1 = 10%)
    public StatBoostTarget BoostTarget { get; set; }

    // Relic
    public string RelicId { get; set; }

    public string GetDisplayText()
    {
        return $"[{Type}] {Name}: {Description}";
    }
}

/// <summary>
/// 肉鸽奖励系统 — 三选一
/// 通关后生成3个奖励供玩家选择
/// </summary>
public class RoguelikeRewardSystem
{
    // 已获得的遗物ID列表
    public List<string> AcquiredRelicIds { get; private set; } = new List<string>();

    // 奖励池权重随关卡变化
    private int currentLevel = 1;

    // 遗物数据库
    private Dictionary<string, RelicData> relicDatabase;

    // 随机数生成器
    private System.Random rng;

    // 事件
    public event System.Action<RewardOption> OnRewardSelected;

    public RoguelikeRewardSystem()
    {
        rng = new System.Random();
        InitializeRelicDatabase();
    }

    /// <summary>
    /// 设置当前关卡（影响奖励品质）
    /// </summary>
    public void SetCurrentLevel(int level)
    {
        currentLevel = level;
    }

    /// <summary>
    /// 生成3个奖励选项供玩家选择
    /// </summary>
    public List<RewardOption> GenerateRewards(int level, List<Hero> currentHeroes, DiceRoller diceRoller)
    {
        currentLevel = level;
        var rewards = new List<RewardOption>();
        var usedTypes = new HashSet<RewardType>();

        // 生成3个不同类型的奖励
        for (int i = 0; i < 3; i++)
        {
            var reward = GenerateSingleReward(level, currentHeroes, diceRoller, usedTypes);
            if (reward != null)
            {
                rewards.Add(reward);
                usedTypes.Add(reward.Type);
            }
        }

        // 如果生成不足3个，补齐
        while (rewards.Count < 3)
        {
            var fallback = GenerateFallbackReward(level);
            if (fallback != null)
                rewards.Add(fallback);
            else
                break;
        }

        return rewards;
    }

    RewardOption GenerateSingleReward(int level, List<Hero> currentHeroes, DiceRoller diceRoller, HashSet<RewardType> excludeTypes)
    {
        // 权重：随关卡变化
        float unitWeight = currentHeroes.Count < 5 ? 30f : 10f;  // 队伍未满时更容易出新单位
        float diceWeight = 25f;
        float statWeight = 25f;
        float relicWeight = level >= 3 ? 20f : 5f; // 3关后才容易出遗物

        // 排除已有类型
        if (excludeTypes.Contains(RewardType.NewUnit)) unitWeight = 0;
        if (excludeTypes.Contains(RewardType.DiceFaceUpgrade)) diceWeight = 0;
        if (excludeTypes.Contains(RewardType.StatBoost)) statWeight = 0;
        if (excludeTypes.Contains(RewardType.Relic)) relicWeight = 0;

        float total = unitWeight + diceWeight + statWeight + relicWeight;
        if (total <= 0) return null;

        float roll = (float)rng.NextDouble() * total;

        if (roll < unitWeight)
            return GenerateNewUnitReward(level, currentHeroes);
        roll -= unitWeight;

        if (roll < diceWeight)
            return GenerateDiceUpgradeReward(level, diceRoller);
        roll -= diceWeight;

        if (roll < statWeight)
            return GenerateStatBoostReward(level, currentHeroes);
        roll -= statWeight;

        if (roll < relicWeight)
            return GenerateRelicReward(level);

        return GenerateStatBoostReward(level, currentHeroes);
    }

    RewardOption GenerateNewUnitReward(int level, List<Hero> currentHeroes)
    {
        // 随机选择英雄模板
        string[] templates = { "坦克", "射手", "刺客", "法师", "战士" };
        string template = templates[rng.Next(templates.Length)];

        // 稀有度随关卡提升
        int rarity = 1;
        if (level >= 5) rarity = 2;
        if (level >= 10 && rng.NextDouble() < 0.3f) rarity = 3;

        var heroStats = GameBalance.GetHeroTemplate(template);

        return new RewardOption
        {
            Type = RewardType.NewUnit,
            Name = $"招募 {template}",
            Description = $"获得一个{rarity}星{template}（HP:{heroStats.Health} ATK:{heroStats.Attack}）",
            Rarity = rarity,
            HeroTemplateName = template
        };
    }

    RewardOption GenerateDiceUpgradeReward(int level, DiceRoller diceRoller)
    {
        // 骰子面升级：随机选一个骰子的一面，增加点数
        int diceCount = diceRoller?.Dices?.Length ?? 3;
        int diceIndex = rng.Next(diceCount);
        int faceCount = 6; // 默认6面
        int faceIndex = rng.Next(faceCount);
        int bonus = level >= 5 ? 2 : 1; // 5关后升级幅度+1
        int newValue = Mathf.Min(faceIndex + 1 + bonus, 9); // 最高9面

        return new RewardOption
        {
            Type = RewardType.DiceFaceUpgrade,
            Name = $"骰子强化",
            Description = $"骰子{diceIndex + 1}的第{faceIndex + 1}面强化为{newValue}点",
            Rarity = level >= 7 ? 2 : 1,
            DiceIndex = diceIndex,
            FaceIndex = faceIndex,
            NewFaceValue = newValue
        };
    }

    RewardOption GenerateStatBoostReward(int level, List<Hero> currentHeroes)
    {
        // 属性强化：随机属性，全体或单体
        StatType[] stats = { StatType.Health, StatType.Attack, StatType.Defense, StatType.Speed, StatType.CritRate };
        StatType stat = stats[rng.Next(stats.Length)];

        bool isAll = rng.NextDouble() < 0.6f; // 60%概率全体
        float amount = 0.05f + level * 0.01f + (float)rng.NextDouble() * 0.05f; // 5~20%

        string targetText = isAll ? "全体" : "随机一名";
        string statText = stat switch
        {
            StatType.Health => "生命",
            StatType.Attack => "攻击",
            StatType.Defense => "防御",
            StatType.Speed => "速度",
            StatType.CritRate => "暴击率",
            _ => "属性"
        };

        return new RewardOption
        {
            Type = RewardType.StatBoost,
            Name = $"{statText}强化",
            Description = $"{targetText}{statText}+{Mathf.RoundToInt(amount * 100)}%",
            Rarity = isAll ? 2 : 1,
            BoostStat = stat,
            BoostAmount = amount,
            BoostTarget = isAll ? StatBoostTarget.AllHeroes : StatBoostTarget.RandomHero
        };
    }

    RewardOption GenerateRelicReward(int level)
    {
        // 从遗物数据库中随机选一个未获得的遗物
        var available = new List<RelicData>();
        foreach (var relic in relicDatabase.Values)
        {
            if (!AcquiredRelicIds.Contains(relic.relicId))
                available.Add(relic);
        }

        if (available.Count == 0)
        {
            // 所有遗物都已获得，改为属性强化
            return null;
        }

        // 关卡越高，稀有遗物概率越高
        var filtered = available.FindAll(r =>
        {
            if (r.rarity == 3 && level < 8) return false; // 8关后才出史诗遗物
            return true;
        });

        if (filtered.Count == 0) filtered = available;
        var chosen = filtered[rng.Next(filtered.Count)];

        return new RewardOption
        {
            Type = RewardType.Relic,
            Name = chosen.relicName,
            Description = chosen.description,
            Rarity = chosen.rarity,
            RelicId = chosen.relicId
        };
    }

    RewardOption GenerateFallbackReward(int level)
    {
        // 保底奖励：金币/经验
        return new RewardOption
        {
            Type = RewardType.StatBoost,
            Name = "属性微调",
            Description = "全体攻击+5%",
            Rarity = 1,
            BoostStat = StatType.Attack,
            BoostAmount = 0.05f,
            BoostTarget = StatBoostTarget.AllHeroes
        };
    }

    /// <summary>
    /// 应用选中的奖励
    /// </summary>
    public void ApplyReward(RewardOption reward, List<Hero> currentHeroes, DiceRoller diceRoller, RelicSystem relicSystem)
    {
        switch (reward.Type)
        {
            case RewardType.NewUnit:
                // 新单位通过事件通知外部创建Hero对象
                Debug.Log($"[奖励] 获得新单位: {reward.HeroTemplateName}");
                break;

            case RewardType.DiceFaceUpgrade:
                if (diceRoller != null && reward.DiceIndex < diceRoller.Dices.Length)
                {
                    var dice = diceRoller.Dices[reward.DiceIndex];
                    dice.UpgradeFace(reward.FaceIndex, $"升级+{reward.NewFaceValue}");
                    Debug.Log($"[奖励] 骰子{reward.DiceIndex + 1}面{reward.FaceIndex + 1}升级为{reward.NewFaceValue}");
                }
                break;

            case RewardType.StatBoost:
                ApplyStatBoost(reward, currentHeroes);
                break;

            case RewardType.Relic:
                if (relicSystem != null)
                {
                    relicSystem.AcquireRelic(reward.RelicId);
                    AcquiredRelicIds.Add(reward.RelicId);
                    Debug.Log($"[奖励] 获得遗物: {reward.Name}");
                }
                break;
        }

        OnRewardSelected?.Invoke(reward);
    }

    void ApplyStatBoost(RewardOption reward, List<Hero> heroes)
    {
        if (heroes == null || heroes.Count == 0) return;

        var targets = new List<Hero>();
        if (reward.BoostTarget == StatBoostTarget.AllHeroes)
        {
            targets = heroes;
        }
        else
        {
            // 随机选一个
            targets.Add(heroes[rng.Next(heroes.Count)]);
        }

        foreach (var hero in targets)
        {
            if (hero == null) continue;
            switch (reward.BoostStat)
            {
                case StatType.Health:
                    hero.BoostMaxHealth(reward.BoostAmount);
                    break;
                case StatType.Attack:
                    hero.BoostAttack(reward.BoostAmount);
                    break;
                case StatType.Defense:
                    hero.BoostDefense(reward.BoostAmount);
                    break;
                case StatType.Speed:
                    hero.BoostSpeed(reward.BoostAmount);
                    break;
                case StatType.CritRate:
                    hero.BoostCritRate(reward.BoostAmount);
                    break;
            }
        }
    }

    void InitializeRelicDatabase()
    {
        relicDatabase = new Dictionary<string, RelicData>
        {
            // 普通遗物
            { "relic_iron_shield", new RelicData("relic_iron_shield", "铁盾", 1, RelicRarity.Common,
                "全体防御+10%", RelicEffectType.DefenseBoost, 0.1f) },
            { "relic_sharp_blade", new RelicData("relic_sharp_blade", "锋利之刃", 1, RelicRarity.Common,
                "全体攻击+10%", RelicEffectType.AttackBoost, 0.1f) },
            { "relic_health_stone", new RelicData("relic_health_stone", "生命之石", 1, RelicRarity.Common,
                "全体生命+15%", RelicEffectType.HealthBoost, 0.15f) },
            { "relic_speed_boots", new RelicData("relic_speed_boots", "疾风之靴", 1, RelicRarity.Common,
                "全体速度+10%", RelicEffectType.SpeedBoost, 0.1f) },
            { "relic_lucky_coin", new RelicData("relic_lucky_coin", "幸运币", 1, RelicRarity.Common,
                "全体暴击率+5%", RelicEffectType.CritBoost, 0.05f) },

            // 稀有遗物
            { "relic_dragon_heart", new RelicData("relic_dragon_heart", "龙心", 2, RelicRarity.Rare,
                "每场战斗开始时，全体获得最大生命20%的护盾", RelicEffectType.BattleStartShield, 0.2f) },
            { "relic_vampire_fang", new RelicData("relic_vampire_fang", "吸血鬼之牙", 2, RelicRarity.Rare,
                "全体获得10%吸血", RelicEffectType.LifeSteal, 0.1f) },
            { "relic_reroll_crystal", new RelicData("relic_reroll_crystal", "重摇水晶", 2, RelicRarity.Rare,
                "每关额外获得1次重摇机会", RelicEffectType.ExtraReroll, 1f) },
            { "relic_poison_dagger", new RelicData("relic_poison_dagger", "淬毒匕首", 2, RelicRarity.Rare,
                "攻击附带5%最大生命中毒", RelicEffectType.PoisonAttack, 0.05f) },
            { "relic_thorns_armor", new RelicData("relic_thorns_armor", "荆棘之甲", 2, RelicRarity.Rare,
                "全体获得10%反伤", RelicEffectType.Thorns, 0.1f) },

            // 史诗遗物
            { "relic_phoenix_feather", new RelicData("relic_phoenix_feather", "凤凰羽毛", 3, RelicRarity.Epic,
                "首次阵亡时自动复活，恢复50%生命（每关一次）", RelicEffectType.Revive, 0.5f) },
            { "relic_giant_slayer", new RelicData("relic_giant_slayer", "巨人杀手", 3, RelicRarity.Epic,
                "对生命高于自身的敌人伤害+30%", RelicEffectType.GiantSlayer, 0.3f) },
            { "relic_dice_lords_eye", new RelicData("relic_dice_lords_eye", "骰子领主之眼", 3, RelicRarity.Epic,
                "骰子组合效果增强50%", RelicEffectType.ComboBoost, 0.5f) },
            { "relic_double_reward", new RelicData("relic_double_reward", "贪婪之冠", 3, RelicRarity.Epic,
                "每3关额外获得一次三选一奖励", RelicEffectType.DoubleReward, 3f) },
        };
    }

    /// <summary>
    /// 获取遗物数据
    /// </summary>
    public RelicData GetRelicData(string relicId)
    {
        if (relicDatabase != null && relicDatabase.TryGetValue(relicId, out var data))
            return data;
        return null;
    }

    /// <summary>
    /// 获取所有可用遗物
    /// </summary>
    public List<RelicData> GetAllRelics()
    {
        return new List<RelicData>(relicDatabase.Values);
    }
}
