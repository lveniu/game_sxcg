using UnityEngine;

/// <summary>
/// 装备数据 — 武器、防具、饰品的基础数据
/// 含套装归属和强化升级系统
/// </summary>
[CreateAssetMenu(fileName = "Equipment", menuName = "Game/Equipment")]
public class EquipmentData : ScriptableObject, IItem
{
    public string equipmentName;
    public EquipmentSlot slot;
    public CardRarity rarity; // 复用卡牌稀有度

    // ===== IItem 接口实现 =====

    /// <summary>物品唯一ID（使用 equipmentName 作为标识）</summary>
    public string ItemId => string.IsNullOrEmpty(equipmentName) ? name : equipmentName;

    /// <summary>显示名称</summary>
    public string DisplayName => equipmentName;

    /// <summary>物品描述</summary>
    public string Description => description;

    /// <summary>物品大类（装备）</summary>
    public ItemCategory Category => ItemCategory.Equipment;

    /// <summary>堆叠数量（装备不可堆叠，始终为1）</summary>
    public int StackCount { get; set; } = 1;

    /// <summary>最大堆叠（装备不可堆叠，始终为1）</summary>
    public int MaxStack => 1;

    /// <summary>图标（装备暂无独立图标，返回null）</summary>
    public Sprite Icon => null;

    /// <summary>是否可堆叠（装备不可堆叠）</summary>
    public bool IsStackable => false;

    /// <summary>稀有度（直接映射）</summary>
    CardRarity IItem.Rarity => rarity;

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
        // 此处仅更新描述（去掉旧前缀，加新前缀，避免重复拼接）
        if (enhanceLevel > 0)
        {
            // 移除可能存在的旧强化前缀
            string desc = description;
            int prefixIdx = desc.IndexOf("强化+");
            if (prefixIdx >= 0)
            {
                // 跳过 "强化+N " 前缀部分
                int spaceIdx = desc.IndexOf(' ', prefixIdx);
                if (spaceIdx >= 0)
                    desc = desc.Substring(spaceIdx + 1);
                else
                    desc = desc.Substring(0, prefixIdx);
            }
            description = $"强化+{enhanceLevel} {desc}";
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
