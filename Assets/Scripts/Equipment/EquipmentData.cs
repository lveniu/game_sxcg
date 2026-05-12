using UnityEngine;

/// <summary>
/// 装备数据 — 武器、防具、饰品的基础数据
/// 含套装归属和强化升级系统
/// </summary>
[CreateAssetMenu(fileName = "Equipment", menuName = "Game/Equipment")]
public class EquipmentData : ScriptableObject
{
    public string equipmentName;
    public EquipmentSlot slot;
    public CardRarity rarity; // 复用卡牌稀有度

    [Header("基础属性")]
    public int attackBonus;
    public int defenseBonus;
    public int healthBonus;
    public int speedBonus;
    public float critRateBonus;

    [Header("特殊效果")]
    public string specialEffect; // 如"吸血""反弹"等，MVP中用文本描述
    public string description;

    [Header("套装信息")]
    /// <summary>套装ID，空字符串表示不属于任何套装</summary>
    public string setId = "";
    /// <summary>套装名称（如"烈焰套装"）</summary>
    public string setName = "";

    [Header("强化系统")]
    /// <summary>当前强化等级，默认0</summary>
    public int enhanceLevel = 0;
    /// <summary>最大强化等级，默认10</summary>
    public int maxEnhanceLevel = 10;
    /// <summary>基础强化费用</summary>
    public int enhanceCostBase = 50;
    /// <summary>每级费用倍率，默认1.5</summary>
    public float enhanceCostMultiplier = 1.5f;

    /// <summary>
    /// 装备售价（基于稀有度）
    /// </summary>
    public int GetPrice()
    {
        return rarity switch
        {
            CardRarity.White => 30,
            CardRarity.Blue => 60,
            CardRarity.Purple => 120,
            CardRarity.Gold => 200,
            _ => 30
        };
    }

    /// <summary>
    /// 根据强化等级计算实际属性值
    /// 每级 +10% 基础属性
    /// </summary>
    /// <param name="baseStat">装备的基础属性值</param>
    /// <returns>强化后的实际属性值</returns>
    public float GetEnhancedStat(float baseStat)
    {
        float multiplier = 1f + enhanceLevel * 0.1f;
        return baseStat * multiplier;
    }

    /// <summary>
    /// 强化时调用，更新属性值
    /// 由 EquipmentEnhancer.Enhance() 在强化成功后调用
    /// </summary>
    public void ApplyEnhance()
    {
        // 属性由 GetEnhancedStat 动态计算，
        // 此处仅提升强化等级（实际提升由调用方负责）
        // 这里可以做额外的副作用处理，如更新描述等
        if (enhanceLevel > 0)
        {
            description = $"强化+{enhanceLevel} " + description;
        }
    }

    /// <summary>
    /// 获取强化后的攻击力加成
    /// </summary>
    public int EnhancedAttackBonus => Mathf.RoundToInt(GetEnhancedStat(attackBonus));

    /// <summary>
    /// 获取强化后的防御力加成
    /// </summary>
    public int EnhancedDefenseBonus => Mathf.RoundToInt(GetEnhancedStat(defenseBonus));

    /// <summary>
    /// 获取强化后的生命值加成
    /// </summary>
    public int EnhancedHealthBonus => Mathf.RoundToInt(GetEnhancedStat(healthBonus));

    /// <summary>
    /// 获取强化后的速度加成
    /// </summary>
    public int EnhancedSpeedBonus => Mathf.RoundToInt(GetEnhancedStat(speedBonus));

    /// <summary>
    /// 获取强化后的暴击率加成
    /// </summary>
    public float EnhancedCritRateBonus => GetEnhancedStat(critRateBonus);

    /// <summary>
    /// 是否属于某个套装
    /// </summary>
    public bool BelongsToSet => !string.IsNullOrEmpty(setId);
}
