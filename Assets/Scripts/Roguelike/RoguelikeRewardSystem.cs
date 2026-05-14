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
    public RelicData RelicData { get; set; }  // 直接携带完整遗物数据，前端无需反查

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
        // 随机选择英雄模板（3职业：战士/法师/刺客 + 进化形态）
        string[] templates = { "战士", "法师", "刺客", "链甲使者", "狂战士", "大法师", "巡游法师", "影舞者" };
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
            RelicId = chosen.relicId,
            RelicData = chosen  // 直接携带完整遗物数据
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
                    // 使用 int 重载真正改变面值，string 重载只改效果文本
                    bool upgraded = dice.UpgradeFace(reward.FaceIndex, reward.NewFaceValue);
                    if (upgraded)
                    {
                        Debug.Log($"[奖励] 骰子{reward.DiceIndex + 1}面{reward.FaceIndex + 1}面值升级为{reward.NewFaceValue}");
                    }
                    else
                    {
                        Debug.LogWarning($"[奖励] 骰子面值升级失败：骰子{reward.DiceIndex + 1}面{reward.FaceIndex + 1}，目标值{reward.NewFaceValue}");
                    }
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
            // ===== 普通遗物 (Common) =====
            { "iron_shield", new RelicData("iron_shield", "铁壁盾牌", 1, RelicRarity.Common,
                "全体防御+15%", RelicEffectType.DefenseBoost, 0.15f) },
            { "sharp_blade", new RelicData("sharp_blade", "锋利之刃", 1, RelicRarity.Common,
                "全体攻击+10%", RelicEffectType.AttackBoost, 0.1f) },
            { "lucky_coin", new RelicData("lucky_coin", "幸运硬币", 1, RelicRarity.Common,
                "重摇后骰子结果+1（最大6）", RelicEffectType.ExtraReroll, 1f) },
            { "speed_boots", new RelicData("speed_boots", "疾风之靴", 1, RelicRarity.Common,
                "全体速度+10%", RelicEffectType.SpeedBoost, 0.1f) },
            { "health_amulet", new RelicData("health_amulet", "生命护符", 1, RelicRarity.Common,
                "全体最大血量+15%", RelicEffectType.HealthBoost, 0.15f) },

            // ===== 稀有遗物 (Rare) =====
            { "vampire_fang", new RelicData("vampire_fang", "吸血鬼之牙", 2, RelicRarity.Rare,
                "全体吸血10%（造成的伤害10%回血）", RelicEffectType.LifeSteal, 0.1f) },
            { "dice_master", new RelicData("dice_master", "骰子大师", 2, RelicRarity.Rare,
                "每关额外+1次重摇", RelicEffectType.ExtraReroll, 1f) },
            { "crit_glasses", new RelicData("crit_glasses", "暴击眼镜", 2, RelicRarity.Rare,
                "全体暴击率+10%", RelicEffectType.CritBoost, 0.1f) },
            { "thorns_armor", new RelicData("thorns_armor", "荆棘铠甲", 2, RelicRarity.Rare,
                "受击时反弹15%伤害给攻击者", RelicEffectType.Thorns, 0.15f) },
            { "dragon_heart", new RelicData("dragon_heart", "龙心", 2, RelicRarity.Rare,
                "每回合结束时回复5%最大血量", RelicEffectType.HealthBoost, 0.05f) },

            // ===== 史诗遗物 (Epic) =====
            { "phoenix_feather", new RelicData("phoenix_feather", "凤凰羽毛", 3, RelicRarity.Epic,
                "首次死亡时复活，恢复50%血量", RelicEffectType.Revive, 0.5f) },
            { "dice_lord_eye", new RelicData("dice_lord_eye", "骰子领主之眼", 3, RelicRarity.Epic,
                "散牌也视为对子效果（最低保底对子）", RelicEffectType.ComboBoost, 1f) },

            // ===== 传说遗物 (Legendary) =====
            { "mechanic_breaker", new RelicData("mechanic_breaker", "机制破解器", 4, RelicRarity.Legendary,
                "对机制怪额外伤害30%", RelicEffectType.GiantSlayer, 0.3f) },
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
