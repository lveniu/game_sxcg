using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备管理器 — 生成装备、管理掉落池
/// </summary>
public static class EquipmentManager
{
    static readonly string[] weaponNames = { "短剑", "长枪", "弩弓", "法杖", "巨剑", "精灵弓", "魔法杖", "龙之剑" };
    static readonly string[] armorNames = { "皮甲", "锁子甲", "板甲", "链甲", "魔法盔", "龙鳞甲" };
    static readonly string[] accessoryNames = { "力量戒指", "防御护符", "敏捷靴", "暴击水晶", "生命宝珠", "先攻之眼" };

    /// <summary>
    /// 随关卡获得装备掉落
    /// </summary>
    public static List<EquipmentData> GetLevelDrops(int levelId)
    {
        var drops = new List<EquipmentData>();

        // 每3关必掉一件，其他关卡有30%概率
        bool shouldDrop = (levelId % 3 == 0) || Random.value < 0.3f;
        if (!shouldDrop) return drops;

        int count = (levelId % 3 == 0) ? 1 + (levelId / 10) : 1;
        for (int i = 0; i < count; i++)
        {
            drops.Add(GenerateRandomEquipment(levelId));
        }
        return drops;
    }

    /// <summary>
    /// 生成商店货品
    /// </summary>
    public static List<EquipmentData> GenerateShopItems(int levelId)
    {
        var items = new List<EquipmentData>();
        int count = 2 + (levelId / 5);
        for (int i = 0; i < count; i++)
        {
            items.Add(GenerateRandomEquipment(levelId, true));
        }
        return items;
    }

    /// <summary>
    /// 生成单件随机装备
    /// </summary>
    public static EquipmentData GenerateRandomEquipment(int levelId, bool isShop = false)
    {
        var slot = (EquipmentSlot)Random.Range(0, 3);
        var rarity = RollRarity(levelId, isShop);
        var equip = ScriptableObject.CreateInstance<EquipmentData>();

        equip.slot = slot;
        equip.rarity = rarity;
        equip.equipmentName = GenerateName(slot, rarity);
        equip.description = GenerateDescription(slot, rarity);

        // 根据槽位和稀有度分配属性
        ApplyStats(equip, slot, rarity, levelId);
        return equip;
    }

    static CardRarity RollRarity(int levelId, bool isShop)
    {
        float roll = Random.value;
        float goldChance = Mathf.Clamp01(0.02f + levelId * 0.005f);
        float purpleChance = Mathf.Clamp01(0.08f + levelId * 0.01f);
        float blueChance = Mathf.Clamp01(0.25f + levelId * 0.01f);

        if (isShop) // 商店稀有度略高
        {
            goldChance *= 1.5f;
            purpleChance *= 1.3f;
            blueChance *= 1.2f;
        }

        if (roll < goldChance) return CardRarity.Gold;
        if (roll < goldChance + purpleChance) return CardRarity.Purple;
        if (roll < goldChance + purpleChance + blueChance) return CardRarity.Blue;
        return CardRarity.White;
    }

    static string GenerateName(EquipmentSlot slot, CardRarity rarity)
    {
        string[] names = slot switch
        {
            EquipmentSlot.Weapon => weaponNames,
            EquipmentSlot.Armor => armorNames,
            EquipmentSlot.Accessory => accessoryNames,
            _ => weaponNames
        };
        string name = names[Random.Range(0, names.Length)];
        string prefix = rarity switch
        {
            CardRarity.White => "普通",
            CardRarity.Blue => "稀有",
            CardRarity.Purple => "精英",
            CardRarity.Gold => "传说",
            _ => ""
        };
        return $"{prefix}{name}";
    }

    static string GenerateDescription(EquipmentSlot slot, CardRarity rarity)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => "提升攻击力",
            EquipmentSlot.Armor => "提升防御和生命",
            EquipmentSlot.Accessory => "提升特殊属性",
            _ => ""
        };
    }

    static void ApplyStats(EquipmentData equip, EquipmentSlot slot, CardRarity rarity, int levelId)
    {
        int baseValue = 2 + levelId;
        int multiplier = rarity switch
        {
            CardRarity.White => 1,
            CardRarity.Blue => 2,
            CardRarity.Purple => 3,
            CardRarity.Gold => 5,
            _ => 1
        };

        switch (slot)
        {
            case EquipmentSlot.Weapon:
                equip.attackBonus = baseValue * multiplier;
                if (rarity >= CardRarity.Purple) equip.critRateBonus = 0.03f * (int)rarity;
                break;

            case EquipmentSlot.Armor:
                equip.defenseBonus = baseValue * multiplier;
                equip.healthBonus = baseValue * multiplier * 2;
                break;

            case EquipmentSlot.Accessory:
                // 饰品随机分配
                int statType = Random.Range(0, 4);
                switch (statType)
                {
                    case 0: equip.attackBonus = baseValue * multiplier; break;
                    case 1: equip.defenseBonus = baseValue * multiplier; break;
                    case 2: equip.speedBonus = baseValue * multiplier; break;
                    case 3: equip.critRateBonus = 0.02f * multiplier; break;
                }
                break;
        }
    }
}
