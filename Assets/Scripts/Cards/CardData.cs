using UnityEngine;

public enum CardType
{
    Hero,      // 英雄卡
    Attribute, // 属性卡
    Battle,    // 战斗卡
    Evolution  // 进化卡
}

public enum CardRarity
{
    White,  // 白（普通）
    Blue,   // 蓝（稀有）
    Purple, // 紫（史诗）
    Gold    // 金（传说）
}

public enum CardEffectId
{
    None,
    PowerTraining,    // 力量训练
    ArmorTraining,    // 坚固护甲
    SpeedTraining,    // 灵敏训练
    Slash,            // 斩击
    Reroll,           // 重摇
    ShieldBash,       // 护盾冲击
    FindWeakness,     // 寻找弱点
    EvolutionAwaken,  // 进化觉醒
    // 扩展
    FlameSlash,       // 火焰斩
    FrostArmor,       // 冰霜护甲
    WindStep,         // 疾风步
    HolyBless,        // 神圣祝福
    FatalBlow,        // 致命一击
    SummonBoost,      // 召唤强化
    // 再扩展
    Fireball,         // 火球术
    ChainStrike,      // 连环斩
    LifeSteal,        // 吸血攻击
    Revive,           // 复活术
    PoisonBlade,      // 毒刃
    EnergyBurst,      // 能量爆发
    // 第四轮
    ArmorBreak,       // 破甲攻击
    GroupHeal,        // 群体治疗
    LightningChain,   // 闪电链
    Thorns,           // 荊棘反伤
    BerserkPotion,    // 狂暴药水
    ShieldResonance   // 护盾共振
}

/// <summary>
/// 稀有度配置 — 定义每种稀有度的属性倍率、掉落权重、合成相关参数
/// 所有数值可被 cards.json 中的 rarity_config 段覆盖
/// </summary>
public static class CardRarityConfig
{
    /// <summary>稀有度属性倍率（影响 effectValue 的最终效果）</summary>
    public static readonly Dictionary<CardRarity, float> RarityMultipliers = new Dictionary<CardRarity, float>
    {
        { CardRarity.White,  1.0f },
        { CardRarity.Blue,   1.3f },
        { CardRarity.Purple, 1.6f },
        { CardRarity.Gold,   2.0f }
    };

    /// <summary>稀有度掉落权重（用于随机抽卡）</summary>
    public static readonly Dictionary<CardRarity, int> RarityWeights = new Dictionary<CardRarity, int>
    {
        { CardRarity.White,  60 },
        { CardRarity.Blue,   25 },
        { CardRarity.Purple, 12 },
        { CardRarity.Gold,   3  }
    };

    /// <summary>3合1合成所需的最低材料数量</summary>
    public const int MergeMaterialCount = 3;

    /// <summary>3合1合成基础金币消耗</summary>
    public static readonly Dictionary<CardRarity, int> MergeCostByRarity = new Dictionary<CardRarity, int>
    {
        { CardRarity.White,  30  },
        { CardRarity.Blue,   60  },
        { CardRarity.Purple, 120 },
        { CardRarity.Gold,   0   } // 金卡不可合成
    };

    /// <summary>3合1后目标稀有度</summary>
    public static readonly Dictionary<CardRarity, CardRarity> MergeUpgradeMap = new Dictionary<CardRarity, CardRarity>
    {
        { CardRarity.White,  CardRarity.Blue   },
        { CardRarity.Blue,   CardRarity.Purple },
        { CardRarity.Purple, CardRarity.Gold   }
    };

    /// <summary>获取稀有度属性倍率</summary>
    public static float GetMultiplier(CardRarity rarity)
    {
        return RarityMultipliers.TryGetValue(rarity, out var m) ? m : 1.0f;
    }

    /// <summary>获取稀有度掉落权重</summary>
    public static int GetWeight(CardRarity rarity)
    {
        return RarityWeights.TryGetValue(rarity, out var w) ? w : 1;
    }

    /// <summary>获取稀有度中文名</summary>
    public static string GetNameCn(CardRarity rarity) => rarity switch
    {
        CardRarity.White  => "普通(白)",
        CardRarity.Blue   => "稀有(蓝)",
        CardRarity.Purple => "史诗(紫)",
        CardRarity.Gold   => "传说(金)",
        _ => "未知"
    };
}

[CreateAssetMenu(fileName = "CardData", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("基础")]
    public string cardName;
    public CardType cardType;
    public CardRarity rarity;
    public CardEffectId effectId;

    [Header("消耗")]
    public int cost; // 消耗的骰子点数（MVP中战斗卡/进化卡有消耗，属性卡无消耗）

    [Header("效果")]
    public int effectValue; // 属性加成值 / 伤害倍率 * 100

    /// <summary>
    /// 稀有度属性倍率 — 最终效果值 = effectValue × RarityMultiplier
    /// 由 CardRarityConfig 提供默认值，可被 cards.json 覆盖
    /// </summary>
    public float RarityMultiplier => CardRarityConfig.GetMultiplier(rarity);

    /// <summary>
    /// 最终效果值（含稀有度倍率）
    /// </summary>
    public float FinalEffectValue => effectValue * RarityMultiplier;

    [Header("骰子联动")]
    public DiceCombinationType requiredCombo; // 需要的组合类型（None=不需要）
    public float comboMultiplier = 1.5f;      // 联动时的倍率

    [Header("合成")]
    public string upgradeFrom;     // 合成来源卡牌ID（null表示不可通过合成升级）
    public int upgradeCost = 0;    // 合成花费金币（0表示无金币消耗）

    [Header("效果引擎")]
    public string effectIdStr;     // 效果引擎用的字符串ID（与face_effects.json等配置关联）

    /// <summary>
    /// 获取效果引擎ID：优先使用 effectIdStr，否则自动从 effectId 枚举转为蛇形命名
    /// </summary>
    public string ResolvedEffectIdStr
    {
        get
        {
            if (!string.IsNullOrEmpty(effectIdStr)) return effectIdStr;
            // 自动从枚举名生成蛇形命名
            return CardEffectIdToSnakeCase(effectId);
        }
    }

    [Header("持有英雄（英雄卡专用）")]
    public HeroData ownerHero; // 英雄卡对应的目标HeroData，发到手中时自动关联

    [Header("外观")]
    public Sprite icon;

    [TextArea]
    public string description;

    /// <summary>
    /// 将 CardEffectId 枚举名称转为蛇形命名
    /// 例如：PowerTraining → power_training
    /// </summary>
    public static string CardEffectIdToSnakeCase(CardEffectId eid)
    {
        if (eid == CardEffectId.None) return "none";
        string name = eid.ToString();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLower(c));
        }
        return sb.ToString();
    }
}
