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

    // ===== BE-12 IItem 统一查询 =====

    /// <summary>
    /// 获取所有物品的 IItem 视图（统一遍历装备+卡牌）
    /// </summary>
    public List<IItem> GetAllItems()
    {
        var items = new List<IItem>(Equipments.Count + Cards.Count);
        foreach (var eq in Equipments)
            if (eq != null) items.Add(eq);
        foreach (var card in Cards)
            if (card != null) items.Add(card);
        return items;
    }

    /// <summary>
    /// 按物品大类筛选（前端Tab切换用）
    /// </summary>
    public List<IItem> GetItemsByCategory(ItemCategory category)
    {
        if (category == ItemCategory.All)
            return GetAllItems();

        var items = new List<IItem>();
        foreach (var eq in Equipments)
            if (eq != null && eq.Category == category) items.Add(eq);
        foreach (var card in Cards)
            if (card != null && card.Category == category) items.Add(card);
        return items;
    }

    /// <summary>
    /// 按稀有度筛选所有物品
    /// </summary>
    public List<IItem> GetItemsByRarity(CardRarity rarity)
    {
        var items = new List<IItem>();
        foreach (var eq in Equipments)
            if (eq != null && eq.rarity == rarity) items.Add(eq);
        foreach (var card in Cards)
            if (card != null && card.Rarity == rarity) items.Add(card);
        return items;
    }

    /// <summary>
    /// 获取物品总数（装备+卡牌）
    /// </summary>
    public int GetTotalItemCount() => Equipments.Count + Cards.Count;

    /// <summary>
    /// 通过 ItemId 查找物品
    /// </summary>
    public IItem FindItemById(string itemId)
    {
        foreach (var eq in Equipments)
            if (eq != null && eq.ItemId == itemId) return eq;
        foreach (var card in Cards)
            if (card != null && card.ItemId == itemId) return card;
        return null;
    }

    // ===== 存档支持 =====
    public void ForceSetGold(int amount) { Gold = amount; OnInventoryChanged?.Invoke(); }
    public void ClearEquipmentsForLoad() { Equipments.Clear(); OnInventoryChanged?.Invoke(); }
    public void ClearCardsForLoad() { Cards.Clear(); OnInventoryChanged?.Invoke(); }

    // ===== BE-18 堆叠 + 分类 + 排序 =====

    /// <summary>
    /// 通用添加物品 — 自动识别类型，可堆叠物品尝试合并
    /// </summary>
    /// <returns>剩余未添加的数量（0=全部添加成功）</returns>
    public int AddItem(IItem item, int amount = 1)
    {
        if (item == null || amount <= 0) return amount;

        // 装备类
        if (item is EquipmentData eq)
        {
            // 装备不可堆叠（同名装备是独立实例）
            for (int i = 0; i < amount; i++)
                AddEquipment(eq);
            return 0;
        }

        // 卡牌类
        if (item is CardInstance card)
        {
            // 卡牌尝试堆叠：查找已有同ID卡牌
            if (card.IsStackable && card.MaxStack > 1)
            {
                int remaining = amount;
                foreach (var existing in Cards)
                {
                    if (existing == null || !ItemStackHelper.CanStack(existing, card)) continue;
                    remaining = ItemStackHelper.TryStack(existing, card, remaining);
                    if (remaining <= 0) break;
                }
                if (remaining > 0)
                {
                    card.StackCount = remaining;
                    AddCard(card);
                }
                return 0;
            }
            // 不可堆叠：直接添加
            AddCard(card);
            return 0;
        }

        return amount;
    }

    /// <summary>
    /// 按物品类型筛选（细粒度类型，如武器/护甲/法术卡等）
    /// </summary>
    public List<IItem> GetItemsByType(string itemType)
    {
        if (string.IsNullOrEmpty(itemType) || itemType == "All")
            return GetAllItems();

        var result = new List<IItem>();

        // 装备类型匹配（按槽位/子类名）
        foreach (var eq in Equipments)
        {
            if (eq == null) continue;
            if (eq.slot.ToString().Equals(itemType, System.StringComparison.OrdinalIgnoreCase) ||
                eq.equipmentName.Contains(itemType))
                result.Add(eq);
        }

        // 卡牌类型匹配
        foreach (var card in Cards)
        {
            if (card == null || card.Data == null) continue;
            if (card.Data.cardType.ToString().Equals(itemType, System.StringComparison.OrdinalIgnoreCase) ||
                card.Data.cardName.Contains(itemType))
                result.Add(card);
        }

        return result;
    }

    /// <summary>
    /// 排序接口 — 支持多种排序标准
    /// </summary>
    public enum SortCriteria
    {
        Name,           // 按名称字典序
        Rarity,         // 按稀有度降序
        Type,           // 按类型分组
        Power,          // 按战力/效果降序
        AcquireTime     // 按获取时间（列表顺序）
    }

    /// <summary>
    /// 对物品列表按指定标准排序
    /// </summary>
    public List<IItem> SortBy(List<IItem> items, SortCriteria criteria, bool descending = true)
    {
        if (items == null || items.Count <= 1) return items ?? new List<IItem>();

        var sorted = new List<IItem>(items);
        switch (criteria)
        {
            case SortCriteria.Name:
                sorted.Sort((a, b) => descending
                    ? string.Compare(b.DisplayName, a.DisplayName, System.StringComparison.Ordinal)
                    : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
                break;

            case SortCriteria.Rarity:
                sorted.Sort((a, b) => descending
                    ? ((int)b.Rarity).CompareTo((int)a.Rarity)
                    : ((int)a.Rarity).CompareTo((int)b.Rarity));
                break;

            case SortCriteria.Type:
                sorted.Sort((a, b) => descending
                    ? b.Category.ToString().CompareTo(a.Category.ToString())
                    : a.Category.ToString().CompareTo(b.Category.ToString()));
                break;

            case SortCriteria.Power:
                sorted.Sort((a, b) =>
                {
                    int powerA = GetItemPower(a);
                    int powerB = GetItemPower(b);
                    return descending ? powerB.CompareTo(powerA) : powerA.CompareTo(powerB);
                });
                break;

            case SortCriteria.AcquireTime:
                // 保持原始顺序（descending则反转）
                if (descending) sorted.Reverse();
                break;
        }
        return sorted;
    }

    /// <summary>获取物品战力评分（通用版本）</summary>
    public static int GetItemPower(IItem item)
    {
        if (item is EquipmentData eq) return GetEquipmentPower(eq);
        if (item is CardInstance card && card.Data != null)
            return card.Data.manaCost > 0 ? card.Data.damage * 3 : card.Data.damage;
        return 0;
    }

    /// <summary>背包容量限制 — 默认装备20+卡牌20</summary>
    public int MaxEquipSlots = 20;
    public int MaxCardSlots = 20;

    /// <summary>检查是否可以添加装备</summary>
    public bool CanAddEquipment() => Equipments.Count < MaxEquipSlots;

    /// <summary>检查是否可以添加卡牌</summary>
    public bool CanAddCard() => Cards.Count < MaxCardSlots;

    /// <summary>获取装备槽占用数（堆叠物品按1个slot计）</summary>
    public int EquipmentSlotUsage => Equipments.Count;

    /// <summary>获取卡牌槽占用数（堆叠物品按1个slot计）</summary>
    public int CardSlotUsage => Cards.Count;
}
