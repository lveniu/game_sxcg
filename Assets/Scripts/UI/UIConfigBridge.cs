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
                _ => Color.white
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
}
