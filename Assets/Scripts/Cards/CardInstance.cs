using UnityEngine;

/// <summary>
/// 运行时卡牌实例 — 手牌中的具体卡牌
/// </summary>
public class CardInstance : IItem
{
    public CardData Data { get; private set; }
    public int StarLevel { get; set; } = 1;

    // ===== IItem 接口实现 =====

    /// <summary>物品唯一ID（卡牌名 + 星级，保证唯一性）</summary>
    public string ItemId => $"{Data.cardName}_star{StarLevel}";

    /// <summary>显示名称</summary>
    public string DisplayName => CardName;

    /// <summary>物品描述</summary>
    public string Description => GetEffectDescription();

    /// <summary>物品大类（卡牌归为消耗品Tab）</summary>
    public ItemCategory Category => ItemCategory.Consumable;

    /// <summary>堆叠数量（卡牌不可堆叠）</summary>
    public int StackCount { get; set; } = 1;

    /// <summary>最大堆叠</summary>
    public int MaxStack => 1;

    /// <summary>图标</summary>
    public Sprite Icon => Data.icon;

    /// <summary>是否可堆叠（卡牌不可堆叠）</summary>
    public bool IsStackable => false;

    /// <summary>稀有度</summary>
    public CardRarity Rarity => Data.rarity;

    /// <summary>卡牌等级（1~5），影响效果数值</summary>
    public int Level { get; private set; } = 1;

    /// <summary>最大等级上限</summary>
    public const int MaxLevel = 5;

    /// <summary>每级效果倍率增量</summary>
    private const float LevelBonusPerLevel = 0.15f;

    public bool IsEvolutionCard => Data.cardType == CardType.Evolution;

    public string CardName => Data.cardName;
    public CardType Type => Data.cardType;
    public int Cost => Data.cost;
    public Sprite Icon => Data.icon;

    /// <summary>根据等级获取效果倍率（等级1=1.0，每级+15%）</summary>
    public float LevelMultiplier => 1f + (Level - 1) * LevelBonusPerLevel;

    public CardInstance(CardData data)
    {
        Data = data;
        StarLevel = 1;
        Level = 1;
    }

    /// <summary>
    /// 检查此卡牌是否可以通过消耗材料卡进行升级
    /// </summary>
    /// <param name="availableCards">当前手牌中可作为材料的卡牌列表</param>
    /// <returns>是否满足升级条件</returns>
    public bool CanUpgrade(System.Collections.Generic.List<CardInstance> availableCards)
    {
        // 已满级不可升级
        if (Level >= MaxLevel) return false;

        // 没有合成来源配置，说明不可通过消耗材料升级
        if (string.IsNullOrEmpty(Data.upgradeFrom)) return false;

        // 检查手牌中是否有足够的材料卡
        int materialCount = 0;
        foreach (var card in availableCards)
        {
            if (card != this && card.Data.cardName == Data.upgradeFrom)
            {
                materialCount++;
            }
        }

        // 需要至少2张同名材料卡
        return materialCount >= 2;
    }

    /// <summary>
    /// 升级卡牌等级（需先通过CanUpgrade检查）
    /// </summary>
    /// <returns>是否升级成功</returns>
    public bool Upgrade()
    {
        if (Level >= MaxLevel) return false;
        Level++;
        UnityEngine.Debug.Log($"卡牌升级：{CardName} → 等级{Level}，效果倍率{LevelMultiplier:F2}");
        return true;
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
        string desc = Data.description;

        // 附加等级信息（等级>1时显示）
        if (Level > 1)
        {
            desc += $"\n[等级{Level}] 效果倍率×{LevelMultiplier:F2}";
        }

        if (Data.requiredCombo != DiceCombinationType.None)
        {
            return $"{desc}\n[联动] {Data.requiredCombo} 时效果×{Data.comboMultiplier}";
        }
        return desc;
    }
}
