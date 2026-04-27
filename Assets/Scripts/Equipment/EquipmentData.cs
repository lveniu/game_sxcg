using UnityEngine;

/// <summary>
/// 装备数据 — 武器、防具、饰品的基础数据
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
}
