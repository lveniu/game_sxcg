using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品大类（前端Tab用）
/// </summary>
public enum ItemCategory
{
    All,         // 全部
    Equipment,   // 装备
    Material,    // 材料（预留，MVP不实现）
    Consumable   // 消耗品（预留，MVP不实现）
}

/// <summary>
/// 玩家背包 — 管理金币、装备、卡牌
/// BE-09 扩展：分类查询、事件系统、战力评分+排序
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public int Gold { get; private set; } = 100;
    public List<EquipmentData> Equipments { get; private set; } = new();
    public List<CardInstance> Cards { get; private set; } = new();

    // ===== 事件系统（前端监听刷新UI）=====
    
    /// <summary>背包内容变更（增删物品时触发）</summary>
    public event System.Action OnInventoryChanged;
    
    /// <summary>装备变更（添加或移除时触发）</summary>
    public event System.Action<EquipmentData, bool> OnEquipmentChanged; // (装备, 是否添加)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        Debug.Log($"金币 +{amount} 当前: {Gold}");

        // 成就系统：追踪金币获取 → max_gold_held 类成就
        var achMgr = AchievementManager.Instance;
        if (achMgr != null && amount > 0)
        {
            achMgr.TrackProgress("max_gold_held", Gold);
        }
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void AddEquipment(EquipmentData equipment)
    {
        if (equipment == null) return;
        Equipments.Add(equipment);
        Debug.Log($"获得装备: {equipment.equipmentName}");
        OnEquipmentChanged?.Invoke(equipment, true);
        OnInventoryChanged?.Invoke();
    }

    public void RemoveEquipment(EquipmentData equipment)
    {
        if (Equipments.Remove(equipment))
        {
            OnEquipmentChanged?.Invoke(equipment, false);
            OnInventoryChanged?.Invoke();
        }
    }

    public void AddCard(CardInstance card)
    {
        if (card == null) return;
        Cards.Add(card);
        Debug.Log($"获得卡牌: {card.Data.cardName}");
        OnInventoryChanged?.Invoke();
    }

    public void RemoveCard(CardInstance card)
    {
        Cards.Remove(card);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 装备给英雄（从背包移除）
    /// </summary>
    public bool EquipToHero(EquipmentData equipment, Hero hero)
    {
        if (!Equipments.Contains(equipment)) return false;
        hero.Equip(equipment);
        Equipments.Remove(equipment);
        OnEquipmentChanged?.Invoke(equipment, false);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 从英雄卸下装备（回到背包）
    /// </summary>
    public void UnequipFromHero(EquipmentSlot slot, Hero hero)
    {
        var equipment = hero.Unequip(slot);
        if (equipment != null)
        {
            Equipments.Add(equipment);
            OnEquipmentChanged?.Invoke(equipment, true);
            OnInventoryChanged?.Invoke();
        }
    }

    // ===== BE-09 分类查询 =====

    /// <summary>按槽位筛选装备</summary>
    public List<EquipmentData> GetEquipmentsBySlot(EquipmentSlot slot)
    {
        return Equipments.FindAll(e => e != null && e.slot == slot);
    }

    /// <summary>按稀有度筛选装备</summary>
    public List<EquipmentData> GetEquipmentsByRarity(CardRarity rarity)
    {
        return Equipments.FindAll(e => e != null && e.rarity == rarity);
    }

    /// <summary>获取装备总数</summary>
    public int GetEquipmentCount() => Equipments.Count;

    /// <summary>获取卡牌总数</summary>
    public int GetCardCount() => Cards.Count;

    // ===== BE-09 战力评分 =====

    /// <summary>
    /// 获取装备的战力评分（用于排序和比较）
    /// 权重：攻击×3 + 防御×2 + 生命×1 + 速度×2 + 暴击×100
    /// 含强化加成
    /// </summary>
    public static int GetEquipmentPower(EquipmentData equip)
    {
        if (equip == null) return 0;
        return equip.EnhancedAttackBonus * 3 + equip.EnhancedDefenseBonus * 2
             + equip.EnhancedHealthBonus + equip.EnhancedSpeedBonus * 2
             + Mathf.RoundToInt(equip.EnhancedCritRateBonus * 100);
    }

    /// <summary>按战力排序装备（默认降序，最强在前）</summary>
    public List<EquipmentData> GetEquipmentsSortedByPower(bool descending = true)
    {
        var sorted = new List<EquipmentData>(Equipments);
        if (descending)
            sorted.Sort((a, b) => GetEquipmentPower(b) - GetEquipmentPower(a));
        else
            sorted.Sort((a, b) => GetEquipmentPower(a) - GetEquipmentPower(b));
        return sorted;
    }

    // ===== 存档支持 =====
    public void ForceSetGold(int amount) { Gold = amount; OnInventoryChanged?.Invoke(); }
    public void ClearEquipmentsForLoad() { Equipments.Clear(); OnInventoryChanged?.Invoke(); }
    public void ClearCardsForLoad() { Cards.Clear(); OnInventoryChanged?.Invoke(); }
}
