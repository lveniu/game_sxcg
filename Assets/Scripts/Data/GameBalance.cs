using UnityEngine;

/// <summary>
/// 数值总表 — 集中管理所有游戏数值、公式、难度曲线
/// 职业统一：Warrior(战士) / Mage(法师) / Assassin(刺客)
/// </summary>
public static class GameBalance
{
    // ========== 难度曲线 ==========

    /// <summary>
    /// 关卡难度系数：phase_1(1-10关)线性+15%，phase_2(11关+)增速放缓至+5%
    /// </summary>
    public static float GetLevelDifficulty(int levelId)
    {
        if (levelId <= 10)
            return 1f + (levelId - 1) * 0.15f;
        else
            return 2.35f + (levelId - 10) * 0.05f; // 10关时2.35，之后每关+5%
    }

    /// <summary>
    /// 金币奖励基数
    /// </summary>
    public static int GetBaseGoldReward(int levelId)
    {
        return 20 + levelId * 10 + (levelId / 5) * 20;
    }

    // ========== 英雄数值模板 ==========

    public static HeroStatTemplate GetHeroTemplate(string heroName)
    {
        return heroName switch
        {
            // 三大基础职业
            "战士" => new HeroStatTemplate(150, 8, 10, 6, 0.02f, 2, HeroClass.Warrior),
            "法师" => new HeroStatTemplate(70, 12, 3, 8, 0.05f, 2, HeroClass.Mage),
            "刺客" => new HeroStatTemplate(70, 16, 3, 14, 0.12f, 1, HeroClass.Assassin),
            // 进化形态
            "链甲使者" => new HeroStatTemplate(200, 10, 15, 5, 0.03f, 2, HeroClass.Warrior),
            "狂战士" => new HeroStatTemplate(130, 14, 10, 8, 0.08f, 2, HeroClass.Warrior),
            "大法师" => new HeroStatTemplate(85, 18, 4, 10, 0.08f, 2, HeroClass.Mage),
            "巡游法师" => new HeroStatTemplate(100, 18, 5, 12, 0.12f, 2, HeroClass.Mage),
            "影舞者" => new HeroStatTemplate(85, 22, 4, 18, 0.20f, 1, HeroClass.Assassin),
            _ => new HeroStatTemplate(100, 10, 5, 8, 0.05f, 1, HeroClass.Warrior)
        };
    }

    // ========== 敌人数值模板（含难度缩放） ==========

    public static HeroStatTemplate GetEnemyTemplate(string enemyName, int levelId = 1)
    {
        float diff = GetLevelDifficulty(levelId);
        var baseTemplate = enemyName switch
        {
            "小怪" => new HeroStatTemplate(60, 6, 3, 5, 0f, 0, HeroClass.Warrior),
            "精英" => new HeroStatTemplate(120, 12, 6, 8, 0.05f, 0, HeroClass.Mage),
            "Boss" => new HeroStatTemplate(300, 15, 10, 5, 0.1f, 0, HeroClass.Warrior),
            "自爆怪" => new HeroStatTemplate(40, 4, 1, 8, 0f, 0, HeroClass.Warrior),
            "治疗者" => new HeroStatTemplate(50, 5, 2, 4, 0f, 0, HeroClass.Mage),
            "护盾怪" => new HeroStatTemplate(80, 5, 8, 4, 0f, 0, HeroClass.Warrior),
            "分裂怪" => new HeroStatTemplate(100, 6, 2, 5, 0f, 0, HeroClass.Warrior),
            "隐身怪" => new HeroStatTemplate(60, 10, 2, 12, 0.1f, 0, HeroClass.Assassin),
            "诅咒巫师" => new HeroStatTemplate(70, 8, 4, 6, 0.05f, 0, HeroClass.Mage),
            "重装骑士" => new HeroStatTemplate(180, 10, 12, 4, 0.03f, 0, HeroClass.Warrior),
            "毒液蜘蛛" => new HeroStatTemplate(90, 7, 5, 7, 0.08f, 0, HeroClass.Assassin),
            _ => new HeroStatTemplate(60, 6, 3, 5, 0f, 0, HeroClass.Warrior)
        };

        return baseTemplate.Scale(diff);
    }

    // ========== 星级成长公式 ==========

    /// <summary>
    /// 星级倍率：1星=1x, 2星=1.5x, 3星=2.2x
    /// </summary>
    public static float GetStarMultiplier(int starLevel)
    {
        return starLevel switch
        {
            1 => 1f,
            2 => 1.5f,
            3 => 2.2f,
            _ => 1f + (starLevel - 1) * 0.6f
        };
    }

    // ========== 伤害公式 ==========

    /// <summary>
    /// 计算伤害：攻 - 防，最低1点，可暴击
    /// </summary>
    public static int CalculateDamage(
        int attackerAtk,
        float critRate,
        float critDamage,
        int targetDef,
        float damageMultiplier = 1f)
    {
        bool isCrit = Random.value < critRate;
        float critMult = isCrit ? (1.5f + critDamage) : 1f;
        float rawDamage = attackerAtk * damageMultiplier * critMult;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage) - targetDef);
        return finalDamage;
    }

    /// <summary>
    /// 计算治疗量
    /// </summary>
    public static int CalculateHeal(int baseHeal, float healMultiplier = 1f)
    {
        return Mathf.RoundToInt(baseHeal * healMultiplier);
    }

    // ========== 卡牌数值 ==========

    public static CardStatTemplate GetCardTemplate(string cardName)
    {
        return cardName switch
        {
            "力量训练" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 3),
            "坚固护甲" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 3),
            "灵敏训练" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 2),
            "斩击" => new CardStatTemplate(CardType.Battle, CardRarity.White, 1, 50),
            "重摇" => new CardStatTemplate(CardType.Battle, CardRarity.White, 1, 0),
            "护盾冲击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 30),
            "寻找弱点" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "进化觉醒" => new CardStatTemplate(CardType.Evolution, CardRarity.Purple, 3, 0),
            "火焰斩" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 20),
            "冰霜护甲" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 25),
            "疾风步" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 50),
            "神圣祝福" => new CardStatTemplate(CardType.Attribute, CardRarity.Gold, 0, 5),
            "致命一击" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 50),
            "召唤强化" => new CardStatTemplate(CardType.Attribute, CardRarity.Blue, 0, 1),
            "火球术" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 30),
            "连环斩" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 2),
            "吸血攻击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "复活术" => new CardStatTemplate(CardType.Attribute, CardRarity.Gold, 0, 1),
            "毒刃" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 5),
            "能量爆发" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 3, 20),
            "破甲攻击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 50),
            "群体治疗" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 20),
            "闪电链" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 3),
            "荊棘反伤" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "狂暴药水" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 80),
            "护盾共振" => new CardStatTemplate(CardType.Battle, CardRarity.Gold, 2, 30),
            _ => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 0)
        };
    }

    // ========== 骰子联动倍率 ==========

    /// <summary>
    /// 获取骰子组合的基础倍率
    /// </summary>
    public static float GetComboMultiplier(DiceCombinationType combo)
    {
        return combo switch
        {
            DiceCombinationType.ThreeOfAKind => 1.5f,
            DiceCombinationType.Straight => 1.67f,
            DiceCombinationType.Pair => 2f,
            _ => 1f
        };
    }
}

/// <summary>
/// 英雄数值模板结构
/// </summary>
public struct HeroStatTemplate
{
    public int Health;
    public int Attack;
    public int Defense;
    public int Speed;
    public float CritRate;
    public int SummonCost;
    public HeroClass HeroClass;

    public HeroStatTemplate(int h, int a, int d, int s, float c, int cost, HeroClass cls)
    {
        Health = h; Attack = a; Defense = d; Speed = s;
        CritRate = c; SummonCost = cost; HeroClass = cls;
    }

    public HeroStatTemplate Scale(float multiplier)
    {
        return new HeroStatTemplate(
            Mathf.RoundToInt(Health * multiplier),
            Mathf.RoundToInt(Attack * multiplier),
            Mathf.RoundToInt(Defense * multiplier),
            Mathf.RoundToInt(Speed * multiplier),
            CritRate,
            SummonCost,
            HeroClass
        );
    }
}

/// <summary>
/// 卡牌数值模板结构
/// </summary>
public struct CardStatTemplate
{
    public CardType CardType;
    public CardRarity Rarity;
    public int Cost;
    public int EffectValue;

    public CardStatTemplate(CardType type, CardRarity rarity, int cost, int value)
    {
        CardType = type; Rarity = rarity; Cost = cost; EffectValue = value;
    }
}
