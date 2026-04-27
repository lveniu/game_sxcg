using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 骰子组合评估器 — 扑克算法实现
/// MVP阶段仅支持3个骰子的组合识别
/// </summary>
public static class DiceCombinationEvaluator
{
    /// <summary>
    /// 评估一组骰子点数的组合类型
    /// </summary>
    public static DiceCombination Evaluate(int[] diceValues)
    {
        if (diceValues == null || diceValues.Length == 0)
            return new DiceCombination { Type = DiceCombinationType.None };

        var result = new DiceCombination();
        var sorted = diceValues.OrderBy(x => x).ToArray();
        var groups = diceValues.GroupBy(x => x).OrderByDescending(g => g.Count()).ToList();

        // 检查五条（5个骰子时）
        if (diceValues.Length >= 5)
        {
            var fiveGroup = groups.FirstOrDefault(g => g.Count() == 5);
            if (fiveGroup != null)
            {
                result.Type = DiceCombinationType.FiveOfAKind;
                result.ThreeValue = fiveGroup.Key;
                return result;
            }
        }

        // 检查四条（4个骰子时）
        if (diceValues.Length >= 4)
        {
            var fourGroup = groups.FirstOrDefault(g => g.Count() == 4);
            if (fourGroup != null)
            {
                result.Type = DiceCombinationType.FourOfAKind;
                result.ThreeValue = fourGroup.Key;
                return result;
            }
        }

        // 检查葫芦（3个骰子以上）
        if (diceValues.Length >= 5)
        {
            var threeGroup = groups.FirstOrDefault(g => g.Count() == 3);
            var pairGroup = groups.FirstOrDefault(g => g.Count() == 2);
            if (threeGroup != null && pairGroup != null)
            {
                result.Type = DiceCombinationType.FullHouse;
                result.ThreeValue = threeGroup.Key;
                result.PairValue = pairGroup.Key;
                return result;
            }
        }

        // 检查两对（4个骰子以上）
        if (diceValues.Length >= 4)
        {
            var pairGroups = groups.Where(g => g.Count() == 2).ToList();
            if (pairGroups.Count >= 2)
            {
                result.Type = DiceCombinationType.TwoPair;
                result.PairValue = pairGroups[0].Key;
                return result;
            }
        }

        // 检查顺子
        if (IsStraight(sorted))
        {
            result.Type = DiceCombinationType.Straight;
            result.StraightValues = sorted;
            return result;
        }

        // 检查三条
        var threeOfAKind = groups.FirstOrDefault(g => g.Count() == 3);
        if (threeOfAKind != null)
        {
            result.Type = DiceCombinationType.ThreeOfAKind;
            result.ThreeValue = threeOfAKind.Key;
            return result;
        }

        // 检查对子
        var pair = groups.FirstOrDefault(g => g.Count() == 2);
        if (pair != null)
        {
            result.Type = DiceCombinationType.Pair;
            result.PairValue = pair.Key;
            var single = groups.FirstOrDefault(g => g.Count() == 1);
            if (single != null)
                result.SingleValue = single.Key;
            return result;
        }

        result.Type = DiceCombinationType.None;
        return result;
    }

    /// <summary>
    /// 判断是否为顺子（所有点数连续）
    /// </summary>
    private static bool IsStraight(int[] sortedValues)
    {
        if (sortedValues.Length < 3) return false;
        for (int i = 1; i < sortedValues.Length; i++)
        {
            if (sortedValues[i] != sortedValues[i - 1] + 1)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 获取组合的战斗攻击加成倍率
    /// </summary>
    public static float GetAttackMultiplier(DiceCombinationType type)
    {
        switch (type)
        {
            case DiceCombinationType.ThreeOfAKind: return 1.2f;
            case DiceCombinationType.Straight: return 1.0f; // 顺子加攻速，不加攻击
            case DiceCombinationType.Pair: return 1.0f;     // 对子给单位Buff，不加全体攻击
            default: return 1.0f;
        }
    }

    /// <summary>
    /// 获取组合的战斗攻速加成倍率
    /// </summary>
    public static float GetAttackSpeedMultiplier(DiceCombinationType type)
    {
        switch (type)
        {
            case DiceCombinationType.Straight: return 1.2f;
            default: return 1.0f;
        }
    }
}
