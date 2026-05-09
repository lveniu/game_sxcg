using UnityEngine;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// UI数值显示桥接层 — 前端面板通过此类获取所有显示用数值
    /// 
    /// 设计原则：
    /// 1. 前端面板不直接读JSON，也不直接依赖GameBalance
    /// 2. 所有显示数值统一从这里获取
    /// 3. 当后端ConfigLoader就绪后，只需修改本类的数据源
    /// 
    /// 当前状态：桥接GameBalance硬编码数据 → 后续切换为ConfigLoader
    /// </summary>
    public static class UIConfigBridge
    {
        // ========== 英雄选择面板数据 ==========

        /// <summary>
        /// 获取英雄卡片显示数据
        /// </summary>
        public static HeroDisplayData GetHeroDisplayData(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => new HeroDisplayData
                {
                    heroClass = HeroClass.Warrior,
                    displayName = "铁壁战士",
                    className = "战士",
                    description = "高防高血，近战输出，队伍前排坦克",
                    icon = "warrior_icon",
                    color = HexToColor("#4A90D9"),
                    stats = new HeroStats
                    {
                        maxHealth = 150,
                        attack = 8,
                        defense = 10,
                        speed = 6,
                        critRate = 0.02f
                    },
                    summonCost = 2
                },
                HeroClass.Mage => new HeroDisplayData
                {
                    heroClass = HeroClass.Mage,
                    displayName = "奥术法师",
                    className = "法师",
                    description = "远程AOE法术输出，群体伤害专家",
                    icon = "mage_icon",
                    color = HexToColor("#9B59B6"),
                    stats = new HeroStats
                    {
                        maxHealth = 70,
                        attack = 12,
                        defense = 3,
                        speed = 8,
                        critRate = 0.05f
                    },
                    summonCost = 2
                },
                HeroClass.Assassin => new HeroDisplayData
                {
                    heroClass = HeroClass.Assassin,
                    displayName = "暗影刺客",
                    className = "刺客",
                    description = "高速爆发，闪避背刺，单体秒杀",
                    icon = "assassin_icon",
                    color = HexToColor("#E74C3C"),
                    stats = new HeroStats
                    {
                        maxHealth = 70,
                        attack = 16,
                        defense = 3,
                        speed = 14,
                        critRate = 0.12f
                    },
                    summonCost = 1
                },
                _ => null
            };
        }

        /// <summary>
        /// 获取所有可选英雄数据
        /// </summary>
        public static HeroDisplayData[] GetAllHeroDisplayData()
        {
            return new HeroDisplayData[]
            {
                GetHeroDisplayData(HeroClass.Warrior),
                GetHeroDisplayData(HeroClass.Mage),
                GetHeroDisplayData(HeroClass.Assassin)
            };
        }

        // ========== 骰子组合显示数据 ==========

        /// <summary>
        /// 获取骰子组合的显示信息
        /// </summary>
        public static DiceComboDisplayData GetComboDisplayData(DiceCombinationType comboType)
        {
            return comboType switch
            {
                DiceCombinationType.ThreeOfAKind => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.ThreeOfAKind,
                    nameCN = "三条",
                    description = "全体攻击+50%，持续3回合",
                    borderColor = HexToColor("#FFD700"),
                    glowIntensity = 1.0f,
                    sortPriority = 1
                },
                DiceCombinationType.Straight => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.Straight,
                    nameCN = "顺子",
                    description = "全体攻速+20%，闪避+15%，持续2回合",
                    borderColor = HexToColor("#3498DB"),
                    glowIntensity = 0.8f,
                    sortPriority = 2
                },
                DiceCombinationType.Pair => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.Pair,
                    nameCN = "对子",
                    description = "对敌方攻击最高单位造成2倍伤害",
                    borderColor = HexToColor("#2ECC71"),
                    glowIntensity = 0.5f,
                    sortPriority = 3
                },
                _ => new DiceComboDisplayData
                {
                    comboType = DiceCombinationType.Scattered,
                    nameCN = "散牌",
                    description = "无加成，骰子点数之和提供微量攻击加成",
                    borderColor = HexToColor("#95A5A6"),
                    glowIntensity = 0.0f,
                    sortPriority = 4
                }
            };
        }

        // ========== 骰子特殊面显示数据 ==========

        /// <summary>
        /// 获取骰子特殊面的显示信息
        /// </summary>
        public static DiceFaceDisplayData GetSpecialFaceDisplay(string faceId)
        {
            return faceId switch
            {
                "lightning" => new DiceFaceDisplayData { nameCN = "⚡闪电", color = HexToColor("#F1C40F"), description = "连锁闪电x3" },
                "shield" => new DiceFaceDisplayData { nameCN = "🛡护盾", color = HexToColor("#3498DB"), description = "全体护盾+15%" },
                "heal" => new DiceFaceDisplayData { nameCN = "💚治疗", color = HexToColor("#2ECC71"), description = "全体回复10%" },
                "poison" => new DiceFaceDisplayData { nameCN = "☠毒素", color = HexToColor("#9B59B6"), description = "中毒5%/回合" },
                "critical" => new DiceFaceDisplayData { nameCN = "💥暴击", color = HexToColor("#E74C3C"), description = "必暴+50%爆伤" },
                _ => new DiceFaceDisplayData { nameCN = $"骰子{faceId}", color = Color.white, description = "" }
            };
        }

        // ========== 遗物稀有度显示 ==========

        /// <summary>
        /// 获取遗物稀有度显示信息
        /// </summary>
        public static string GetRarityNameCN(int rarity)
        {
            return rarity switch
            {
                1 => "普通",
                2 => "稀有",
                3 => "史诗",
                4 => "传说",
                _ => "未知"
            };
        }

        public static Color GetRarityColor(int rarity)
        {
            return rarity switch
            {
                1 => new Color(0.85f, 0.85f, 0.85f), // 灰白
                2 => new Color(0.26f, 0.53f, 0.96f),  // 蓝
                3 => new Color(0.64f, 0.21f, 0.93f),  // 紫
                4 => new Color(1f, 0.75f, 0f),        // 金（传说）
                _ => Color.white
            };
        }

        public static string GetRarityStars(int rarity)
        {
            return rarity switch
            {
                1 => "★",
                2 => "★★",
                3 => "★★★",
                4 => "★★★★",
                _ => ""
            };
        }

        // ========== 战斗显示工具 ==========

        /// <summary>
        /// 获取职业图标Emoji
        /// </summary>
        public static string GetClassIcon(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "⚔",
                HeroClass.Mage => "🔮",
                HeroClass.Assassin => "🗡",
                _ => "❓"
            };
        }

        /// <summary>
        /// 获取职业主题色
        /// </summary>
        public static Color GetClassColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => new Color(0.9f, 0.4f, 0.3f),   // 红
                HeroClass.Mage => new Color(0.3f, 0.5f, 0.9f),      // 蓝
                HeroClass.Assassin => new Color(0.7f, 0.3f, 0.9f),  // 紫
                _ => Color.white
            };
        }

        /// <summary>
        /// 格式化属性值显示（百分比/整数自动选择）
        /// </summary>
        public static string FormatStat(StatType statType, float value)
        {
            return statType switch
            {
                StatType.CritRate => $"{Mathf.RoundToInt(value * 100)}%",
                _ => Mathf.RoundToInt(value).ToString()
            };
        }

        /// <summary>
        /// 格式化属性名称中文
        /// </summary>
        public static string GetStatNameCN(StatType statType)
        {
            return statType switch
            {
                StatType.Health => "生命",
                StatType.Attack => "攻击",
                StatType.Defense => "防御",
                StatType.Speed => "速度",
                StatType.CritRate => "暴击率",
                _ => "属性"
            };
        }

        // ========== 遗物显示桥接 ==========

        /// <summary>
        /// 从RelicData转为前端显示数据
        /// </summary>
        public static RelicDisplayData GetRelicDisplayData(RelicData data)
        {
            if (data == null) return null;
            return new RelicDisplayData
            {
                relicId = data.relicId,
                relicName = data.relicName,
                description = data.description,
                iconEmoji = GetRelicEmoji(data.effectType),
                rarity = data.rarity,
                rarityName = GetRarityNameCN(data.rarity),
                rarityColor = GetRarityColor(data.rarity),
                effectDescription = GetEffectDescription(data.effectType, data.effectValue)
            };
        }

        /// <summary>
        /// 遗物效果类型 → Emoji图标
        /// </summary>
        public static string GetRelicEmoji(RelicEffectType type)
        {
            return type switch
            {
                RelicEffectType.AttackBoost => "⚔",
                RelicEffectType.DefenseBoost => "🛡",
                RelicEffectType.HealthBoost => "❤",
                RelicEffectType.SpeedBoost => "💨",
                RelicEffectType.CritBoost => "💥",
                RelicEffectType.BattleStartShield => "🔰",
                RelicEffectType.LifeSteal => "🧛",
                RelicEffectType.Thorns => "🌵",
                RelicEffectType.PoisonAttack => "☠",
                RelicEffectType.GiantSlayer => "🗡",
                RelicEffectType.ExtraReroll => "🎲",
                RelicEffectType.ComboBoost => "✨",
                RelicEffectType.DoubleReward => "💰",
                RelicEffectType.Revive => "🕊",
                _ => "🏺"
            };
        }

        /// <summary>
        /// 遗物效果类型 + 数值 → 可读描述
        /// </summary>
        public static string GetEffectDescription(RelicEffectType type, float value)
        {
            return type switch
            {
                RelicEffectType.AttackBoost => $"攻击力+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.DefenseBoost => $"防御力+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.HealthBoost => $"生命值+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.SpeedBoost => $"速度+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.CritBoost => $"暴击率+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.BattleStartShield => $"开局护盾+{Mathf.RoundToInt(value * 100)}%生命",
                RelicEffectType.LifeSteal => $"吸血{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.Thorns => $"反伤{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.PoisonAttack => $"攻击附带中毒{Mathf.RoundToInt(value)}%/回合",
                RelicEffectType.GiantSlayer => $"对高血量敌人额外伤害+{Mathf.RoundToInt(value * 100)}%",
                RelicEffectType.ExtraReroll => $"重摇次数+{Mathf.RoundToInt(value)}",
                RelicEffectType.ComboBoost => $"散牌升级为对子",
                RelicEffectType.DoubleReward => $"{Mathf.RoundToInt(value * 100)}%概率双倍奖励",
                RelicEffectType.Revive => $"每关复活{Mathf.RoundToInt(value)}次",
                _ => $"效果值: {value}"
            };
        }

        // ========== 奖励选项描述 ==========

        /// <summary>
        /// 生成奖励选项的富文本描述（用于卡片和弹窗）
        /// </summary>
        public static string GetRewardRichDescription(RewardOption reward)
        {
            if (reward == null) return "";
            return reward.Type switch
            {
                RewardType.NewUnit => $"招募新英雄加入队伍\n星级: {reward.Rarity}★",
                RewardType.DiceFaceUpgrade => $"骰子{reward.DiceIndex + 1} #{reward.FaceIndex + 1}面 → {reward.NewFaceValue}",
                RewardType.StatBoost => $"{GetStatNameCN(reward.BoostStat)}+{Mathf.RoundToInt(reward.BoostAmount * 100)}%\n({(reward.BoostTarget == StatBoostTarget.AllHeroes ? "全体" : "随机单体")})",
                RewardType.Relic => $"获得遗物\n{reward.Description}",
                _ => reward.Description ?? ""
            };
        }

        // ========== 工具方法 ==========

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.white;
        }
    }

    // ========== 数据结构 ==========

    /// <summary>
    /// 英雄卡片显示数据（英雄选择面板使用）
    /// </summary>
    public class HeroDisplayData
    {
        public HeroClass heroClass;
        public string displayName;     // "铁壁战士"
        public string className;       // "战士"
        public string description;     // 角色描述
        public string icon;            // 图标引用名
        public Color color;            // 主题色
        public HeroStats stats;
        public int summonCost;
    }

    /// <summary>
    /// 英雄属性显示数据
    /// </summary>
    public struct HeroStats
    {
        public int maxHealth;
        public int attack;
        public int defense;
        public int speed;
        public float critRate;
    }

    /// <summary>
    /// 骰子组合显示数据
    /// </summary>
    public class DiceComboDisplayData
    {
        public DiceCombinationType comboType;
        public string nameCN;
        public string description;
        public Color borderColor;
        public float glowIntensity;
        public int sortPriority;
    }

    /// <summary>
    /// 骰子特殊面显示数据
    /// </summary>
    public class DiceFaceDisplayData
    {
        public string nameCN;
        public Color color;
        public string description;
    }

    // ========== 奖励类型图标 ==========
    // (放在这里让 BattlePanel 和 RoguelikeRewardPanel 都能用)

    /// <summary>
    /// 获取奖励类型的图标emoji和主题色
    /// </summary>
    public static class RewardTypeIcons
    {
        public static string GetIcon(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => "👤",
                RewardType.DiceFaceUpgrade => "🎲",
                RewardType.StatBoost => "📈",
                RewardType.Relic => "🏺",
                _ => "❓"
            };
        }

        public static Color GetColor(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => new Color(0.3f, 0.8f, 0.4f),       // 绿
                RewardType.DiceFaceUpgrade => new Color(0.2f, 0.6f, 1f), // 蓝
                RewardType.StatBoost => new Color(1f, 0.6f, 0.2f),      // 橙
                RewardType.Relic => new Color(0.8f, 0.4f, 1f),          // 紫
                _ => Color.white
            };
        }

        public static string GetTypeLabel(RewardType type)
        {
            return type switch
            {
                RewardType.NewUnit => "新单位",
                RewardType.DiceFaceUpgrade => "骰子强化",
                RewardType.StatBoost => "属性强化",
                RewardType.Relic => "遗物",
                _ => "未知"
            };
        }
    }

    /// <summary>
    /// 遗物图标显示数据（BattlePanel遗物栏 / RewardPanel详情弹窗使用）
    /// </summary>
    public class RelicDisplayData
    {
        public string relicId;
        public string relicName;
        public string description;
        public string iconEmoji;
        public int rarity;
        public string rarityName;
        public Color rarityColor;
        public string effectDescription;
    }
}
