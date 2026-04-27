using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家背包 — 管理金币、装备、卡牌
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public int Gold { get; private set; } = 100;
    public List<EquipmentData> Equipments { get; private set; } = new();
    public List<CardInstance> Cards { get; private set; } = new();

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
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    public void AddEquipment(EquipmentData equipment)
    {
        if (equipment == null) return;
        Equipments.Add(equipment);
        Debug.Log($"获得装备: {equipment.equipmentName}");
    }

    public void RemoveEquipment(EquipmentData equipment)
    {
        Equipments.Remove(equipment);
    }

    public void AddCard(CardInstance card)
    {
        if (card == null) return;
        Cards.Add(card);
        Debug.Log($"获得卡牌: {card.Data.cardName}");
    }

    public void RemoveCard(CardInstance card)
    {
        Cards.Remove(card);
    }

    /// <summary>
    /// 装备给英雄（从背包移除）
    /// </summary>
    public bool EquipToHero(EquipmentData equipment, Hero hero)
    {
        if (!Equipments.Contains(equipment)) return false;
        hero.Equip(equipment);
        Equipments.Remove(equipment);
        return true;
    }

    /// <summary>
    /// 从英雄卸下装备（回到背包）
    /// </summary>
    public void UnequipFromHero(EquipmentSlot slot, Hero hero)
    {
        var equipment = hero.Unequip(slot);
        if (equipment != null)
            Equipments.Add(equipment);
    }
}
