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

    [Header("商店等级")]
    public int ShopLevel { get; private set; } = 1;
    public int MaxShopLevel => 5;

    /// <summary>限购追踪：itemIndex → 剩余可购次数</summary>
    private Dictionary<int, int> purchaseLimits = new();

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
    /// 升级商店（每关结束后可升一次，消耗金币）
    /// 费用：50 * shopLevel，最高5级
    /// </summary>
    public bool UpgradeShop(PlayerInventory inventory)
    {
        if (ShopLevel >= MaxShopLevel) return false;
        int cost = 50 * ShopLevel;
        if (inventory.Gold < cost) return false;
        inventory.SpendGold(cost);
        ShopLevel++;
        Debug.Log($"[商店] 升级到 Lv.{ShopLevel}（高级商品概率={GetHighRarityChance():P0}）");
        return true;
    }

    /// <summary>商店等级对应的高级商品概率</summary>
    public float GetHighRarityChance()
    {
        return 0.1f + ShopLevel * 0.08f; // Lv1=18%, Lv5=50%
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
    /// 生成商店商品（含等级影响 + 限购）
    /// 商店等级影响高级商品出现概率
    /// </summary>
    public void GenerateShop(int levelId)
    {
        CurrentItems.Clear();
        purchaseLimits.Clear();

        int discountPct = DiscountChancePercent;
        float highRarityChance = GetHighRarityChance(); // 商店等级影响

        // 装备商品 — 商店等级越高，越容易出高品质
        var equipments = EquipmentManager.GenerateShopItems(levelId);
        foreach (var equip in equipments)
        {
            // 高等级商店有概率将白装/蓝装提升品质
            if (Random.value < highRarityChance * 0.3f)
            {
                if (equip.rarity == CardRarity.White)
                    equip.rarity = CardRarity.Blue;
                else if (equip.rarity == CardRarity.Blue)
                    equip.rarity = CardRarity.Purple;
            }

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

        // 设置限购：紫/金色限购1次，其他限购2次
        for (int i = 0; i < CurrentItems.Count; i++)
        {
            int limit = CurrentItems[i].GetHighRarity() >= CardRarity.Purple ? 1 : 2;
            purchaseLimits[i] = limit;
        }

        Debug.Log($"商店刷新，共{CurrentItems.Count}件商品（折扣率={DiscountRate}，折扣概率={discountPct}%，高级概率={highRarityChance:P0}）");
    }

    /// <summary>
    /// 购买商品（含限购检查）
    /// </summary>
    public bool BuyItem(int itemIndex, PlayerInventory inventory)
    {
        if (itemIndex < 0 || itemIndex >= CurrentItems.Count) return false;
        var item = CurrentItems[itemIndex];
        if (item.isSold) return false;

        // 限购检查
        if (purchaseLimits.TryGetValue(itemIndex, out int remaining) && remaining <= 0)
        {
            Debug.Log($"[商店] {item.GetName()} 已达限购次数");
            return false;
        }

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

        // 扣减限购次数
        if (purchaseLimits.ContainsKey(itemIndex))
            purchaseLimits[itemIndex]--;

        Debug.Log($"购买了 {item.GetName()} 花费 {finalPrice} 金币（限购剩余{purchaseLimits.GetValueOrDefault(itemIndex, -1)}）");
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

    /// <summary>获取商品对应的高品质稀有度（用于限购判定）</summary>
    public CardRarity GetHighRarity()
    {
        return type switch
        {
            ShopItemType.Equipment => equipment?.rarity ?? CardRarity.White,
            ShopItemType.Card => card?.Data?.rarity ?? CardRarity.White,
            _ => CardRarity.White
        };
    }
}
