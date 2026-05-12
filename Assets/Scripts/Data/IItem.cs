using UnityEngine;

// ============================================================
// 物品通用接口 — 背包UI统一展示用
// EquipmentData / CardInstance 均实现此接口
// ============================================================

/// <summary>
/// 物品通用接口 — 背包UI可通过此接口统一展示所有物品
/// </summary>
public interface IItem
{
    string ItemId { get; }
    string DisplayName { get; }
    string Description { get; }
    ItemCategory Category { get; }
    int StackCount { get; set; }
    int MaxStack { get; }
    Sprite Icon { get; }
    bool IsStackable { get; }
    CardRarity Rarity { get; }
}

/// <summary>
/// 物品堆叠管理器 — 处理同类物品合并
/// </summary>
public static class ItemStackHelper
{
    /// <summary>
    /// 尝试将 incoming 堆叠到 target 上
    /// </summary>
    /// <param name="target">目标物品（已有堆叠）</param>
    /// <param name="incoming">新物品（尝试合并）</param>
    /// <param name="amount">要堆叠的数量</param>
    /// <returns>剩余未堆叠的数量</returns>
    public static int TryStack(IItem target, IItem incoming, int amount)
    {
        // 不同ID或任一方不可堆叠 → 直接返回原数量
        if (!target.IsStackable || !incoming.IsStackable || target.ItemId != incoming.ItemId)
            return amount;

        int space = target.MaxStack - target.StackCount;
        int toAdd = Mathf.Min(space, amount);
        target.StackCount += toAdd;
        return amount - toAdd;
    }

    /// <summary>
    /// 检查两个物品是否可以堆叠
    /// </summary>
    public static bool CanStack(IItem a, IItem b)
    {
        if (a == null || b == null) return false;
        if (!a.IsStackable || !b.IsStackable) return false;
        return a.ItemId == b.ItemId;
    }

    /// <summary>
    /// 获取物品剩余可堆叠空间
    /// </summary>
    public static int GetRemainingStackSpace(IItem item)
    {
        if (item == null || !item.IsStackable) return 0;
        return item.MaxStack - item.StackCount;
    }
}
