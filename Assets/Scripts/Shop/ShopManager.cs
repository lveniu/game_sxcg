using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 — 战关间购买装备和卡牌
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("商店配置")]
    public int baseCardPrice = 40;
    public int discountChance = 20; // 20%概率折扣

    public List<ShopItem> CurrentItems { get; private set; } = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 生成商店商品
    /// </summary>
    public void GenerateShop(int levelId)
    {
        CurrentItems.Clear();

        // 装备商品
        var equipments = EquipmentManager.GenerateShopItems(levelId);
        foreach (var equip in equipments)
        {
            CurrentItems.Add(new ShopItem
            {
                type = ShopItemType.Equipment,
                equipment = equip,
                price = equip.GetPrice(),
                isDiscounted = Random.Range(0, 100) < discountChance
            });
        }

        // 卡牌商品
        int cardCount = 2 + (levelId / 5);
        var rewardCards = GameData.CreateRewardCards();
        for (int i = 0; i < cardCount && i < rewardCards.Count; i++)
        {
            int idx = Random.Range(0, rewardCards.Count);
            var card = rewardCards[idx];
            int price = baseCardPrice * (int)(card.Data.rarity + 1);
            CurrentItems.Add(new ShopItem
            {
                type = ShopItemType.Card,
                card = card,
                price = price,
                isDiscounted = Random.Range(0, 100) < discountChance
            });
        }

        Debug.Log($"商店刷新，共{CurrentItems.Count}件商品");
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    public bool BuyItem(int itemIndex, PlayerInventory inventory)
    {
        if (itemIndex < 0 || itemIndex >= CurrentItems.Count) return false;
        var item = CurrentItems[itemIndex];
        if (item.isSold) return false;

        int finalPrice = item.isDiscounted ? Mathf.RoundToInt(item.price * 0.7f) : item.price;
        if (inventory.Gold < finalPrice) return false;

        inventory.SpendGold(finalPrice);

        switch (item.type)
        {
            case ShopItemType.Equipment:
                inventory.AddEquipment(item.equipment);
                break;
            case ShopItemType.Card:
                inventory.AddCard(item.card);
                break;
        }

        item.isSold = true;
        Debug.Log($"购买了 {item.GetName()} 花费 {finalPrice} 金币");
        return true;
    }
}

public enum ShopItemType { Equipment, Card }

public class ShopItem
{
    public ShopItemType type;
    public EquipmentData equipment;
    public CardInstance card;
    public int price;
    public bool isDiscounted;
    public bool isSold;

    public string GetName()
    {
        return type switch
        {
            ShopItemType.Equipment => equipment?.equipmentName ?? "装备",
            ShopItemType.Card => card?.Data?.cardName ?? "卡牌",
            _ => "???"
        };
    }
}
