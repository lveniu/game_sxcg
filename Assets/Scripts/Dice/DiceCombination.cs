/// <summary>
/// 骰子组合类型
/// MVP阶段仅支持：None、Pair、ThreeOfAKind、Straight
/// Alpha阶段扩展：TwoPair、FullHouse、FourOfAKind、StraightFlush
/// </summary>
public enum DiceCombinationType
{
    None,           // 无组合
    Pair,           // 对子（2个相同）
    ThreeOfAKind,   // 三条（3个相同）
    Straight,       // 顺子（连续）
    TwoPair,        // 两对
    FullHouse,      // 葫芦（三条+对子）
    FourOfAKind,    // 四条
    FiveOfAKind,    // 五条
    StraightFlush   // 同花顺
}

/// <summary>
/// 骰子组合评估结果
/// </summary>
public class DiceCombination
{
    public DiceCombinationType Type { get; set; }

    // Pair 相关
    public int PairValue { get; set; }      // 对子的点数
    public int SingleValue { get; set; }    // 剩余单点的点数（用于给对应英雄专属Buff）

    // ThreeOfAKind 相关
    public int ThreeValue { get; set; }     // 三条的点数

    // Straight 相关
    public int[] StraightValues { get; set; } // 顺子的点数数组

    public string Description => GetDescription();

    /// <summary>
    /// 组合战斗效果描述
    /// </summary>
    public string EffectDescription => GetEffectDescription();

    private string GetDescription()
    {
        switch (Type)
        {
            case DiceCombinationType.ThreeOfAKind:
                return $"三条 ({ThreeValue})";
            case DiceCombinationType.Straight:
                return $"顺子 ({string.Join(",", StraightValues)})";
            case DiceCombinationType.Pair:
                return $"对子 ({PairValue}) + 单点 ({SingleValue})";
            case DiceCombinationType.TwoPair:
                return $"两对";
            case DiceCombinationType.FullHouse:
                return $"葫芦";
            case DiceCombinationType.FourOfAKind:
                return $"四条 ({ThreeValue})";
            case DiceCombinationType.StraightFlush:
                return $"同花顺";
            default:
                return "无组合";
        }
    }

    private string GetEffectDescription()
    {
        switch (Type)
        {
            case DiceCombinationType.ThreeOfAKind:
                return "全体攻击+20%";
            case DiceCombinationType.Straight:
                return "全体攻速+20%";
            case DiceCombinationType.Pair:
                return $"点数{SingleValue}的英雄获得专属Buff";
            default:
                return "无额外效果";
        }
    }
}
