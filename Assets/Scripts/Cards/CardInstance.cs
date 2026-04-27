using UnityEngine;

/// <summary>
/// 运行时卡牌实例 — 手牌中的具体卡牌
/// </summary>
public class CardInstance
{
    public CardData Data { get; private set; }
    public int StarLevel { get; set; } = 1;
    public bool IsEvolutionCard => Data.cardType == CardType.Evolution;

    public string CardName => Data.cardName;
    public CardType Type => Data.cardType;
    public int Cost => Data.cost;
    public Sprite Icon => Data.icon;

    public CardInstance(CardData data)
    {
        Data = data;
        StarLevel = 1;
    }

    /// <summary>
    /// 能否与另一张同名同星卡合成
    /// </summary>
    public bool CanMergeWith(CardInstance other)
    {
        if (other == null) return false;
        if (Data.cardType == CardType.Evolution) return false; // 进化卡不可合成
        return Data.cardName == other.Data.cardName && StarLevel == other.StarLevel && StarLevel < 3;
    }

    /// <summary>
    /// 合成升星
    /// </summary>
    public void Merge()
    {
        if (StarLevel >= 3) return;
        StarLevel++;
    }

    /// <summary>
    /// 检查是否能打出（简化：检查总点数是否足够）
    /// </summary>
    public bool CanPlay(int[] diceValues)
    {
        if (Data.cardType == CardType.Attribute) return true; // 属性卡免费

        int total = 0;
        foreach (var v in diceValues) total += v;
        return total >= Cost;
    }

    /// <summary>
    /// 检查是否满足骰子联动条件
    /// </summary>
    public bool HasComboBonus(DiceCombination combo)
    {
        return Data.requiredCombo != DiceCombinationType.None && combo.Type == Data.requiredCombo;
    }

    /// <summary>
    /// 获取效果描述
    /// </summary>
    public string GetEffectDescription()
    {
        if (Data.requiredCombo != DiceCombinationType.None)
        {
            return $"{Data.description}\n[联动] {Data.requiredCombo} 时效果×{Data.comboMultiplier}";
        }
        return Data.description;
    }
}
