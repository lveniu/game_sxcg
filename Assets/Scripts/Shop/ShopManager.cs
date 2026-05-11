using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店管理器 — 战关间购买装备和卡牌
/// 数值从 BalanceProvider（economy.json / drop_tables.json）读取
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("商店配置（fallback 默认值，实际从 JSON 读取）")]
    public int baseCardPriceFallback = 40;
    public int discountChanceFallback = 20; // 20%概率折扣

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
    /// 从 JSON 获取基础卡牌价格，fallback 到 baseCardPriceFallback
    /// </summary>
    private int BaseCardPrice => BalanceProvider.GetCardPriceByRarity(0, baseCardPriceFallback) / 1; // common 品质基础价

    /// <summary>
    /// 从 JSON 获取折扣概率（百分比），fallback 到 discountChanceFallback
    /// </summary>
    private int DiscountChancePercent
    {
        get
        {
            float chance = BalanceProvider.GetShopDiscountChance();
            return Mathf.RoundToInt(chance * 100f);
        }
    }

    /// <summary>
    /// 从 JSON 获取折扣率（0~1），fallback 到 0.7
    /// </summary>
    private float DiscountRate => BalanceProvider.GetShopDiscountRate();

    /// <summary>
    /// 生成商店商品
    /// </summary>
    public void GenerateShop(int levelId)
    {
        CurrentItems.Clear();

        int discountPct = DiscountChancePercent;

        // 装备商品
        var equipments = EquipmentManager.GenerateShopItems(levelId);
        foreach (var equip in equipments)
        {
            CurrentItems.Add(new ShopItem
            {
                type = ShopItemType.Equipment,
                equipment = equip,
                price = equip.GetPrice(),
                isDiscounted = Random.Range(0, 100) < discountPct
            });
        }

        // 卡牌商品 — 价格从 economy.json price_by_rarity 读取
        int cardCount = 2 + (levelId / 5);
        var rewardCards = GameData.CreateRewardCards();
        for (int i = 0; i < cardCount && i < rewardCards.Count; i++)
        {
            int idx = Random.Range(0, rewardCards.Count);
            var card = rewardCards[idx];
            int rarityIdx = (int)(card.Data.rarity);
            int price = BalanceProvider.GetCardPriceByRarity(rarityIdx, baseCardPriceFallback);
            CurrentItems.Add(new ShopItem
            {
                type = ShopItemType.Card,
                card = card,
                price = price,
                isDiscounted = Random.Range(0, 100) < discountPct
            });
        }

        Debug.Log($"商店刷新，共{CurrentItems.Count}件商品（折扣率={DiscountRate}，折扣概率={discountPct}%）");
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    public bool BuyItem(int itemIndex, PlayerInventory inventory)
    {
        if (itemIndex < 0 || itemIndex >= CurrentItems.Count) return false;
        var item = CurrentItems[itemIndex];
        if (item.isSold) return false;

        int finalPrice = item.isDiscounted ? Mathf.RoundToInt(item.price * DiscountRate) : item.price;
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
