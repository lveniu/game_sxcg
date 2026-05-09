using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 商店面板 — 每3关出现，购买装备和卡牌
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  🏪 商店         💰 金币:150  │  顶栏
    /// ├──────────────────────────────┤
    /// │ ┌──────┐ ┌──────┐ ┌──────┐  │
    /// │ │铁剑  │ │皮甲  │ │火球术│  │  商品区
    /// │ │⚔+10  │ │🛡+5  │ │🔥    │  │  (2列/3列网格)
    /// │ │40金  │ │60金  │ │40金  │  │
    /// │ │[购买]│ │[购买]│ │-20%  │  │
    /// │ └──────┘ └──────┘ └──────┘  │
    /// │ ┌──────┐ ┌──────┐          │
    /// │ │短弓  │ │治疗术│          │
    /// │ │...   │ │...   │          │
    /// │ └──────┘ └──────┘          │
    /// ├──────────────────────────────┤
    /// │      [关闭商店]              │  底栏
    /// └──────────────────────────────┘
    /// </summary>
    public class ShopPanel : UIPanel
    {
        [Header("顶栏")]
        public Text goldText;
        public Text shopTitleText;

        [Header("商品区")]
        public GridLayoutGroup itemGrid;
        public RectTransform itemsContainer;
        public GameObject shopItemPrefab;

        [Header("底栏")]
        public Button closeButton;

        // 商品卡片缓存
        private class ShopItemCard
        {
            public RectTransform rect;
            public Text nameText;
            public Text statText;
            public Text priceText;
            public Text discountTag;
            public Button buyButton;
            public Text buyButtonText;
            public CanvasGroup canvasGroup;
            public Image bgImage;
        }

        private List<ShopItemCard> itemCards = new List<ShopItemCard>();

        // 稀有度颜色
        private static readonly Color RARITY_WHITE = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color RARITY_BLUE = new Color(0.3f, 0.55f, 1f);
        private static readonly Color RARITY_PURPLE = new Color(0.7f, 0.3f, 1f);
        private static readonly Color RARITY_GOLD = new Color(1f, 0.85f, 0.2f);

        // 槽位图标
        private static readonly Dictionary<EquipmentSlot, string> SLOT_ICONS = new()
        {
            { EquipmentSlot.Weapon, "⚔" },
            { EquipmentSlot.Armor, "🛡" },
            { EquipmentSlot.Accessory, "💍" },
        };

        protected override void Awake()
        {
            base.Awake();
            panelId = "Shop";
        }

        protected override void OnShow()
        {
            closeButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.AddListener(OnCloseClicked);

            RefreshGoldDisplay();
            RefreshShopItems();

            // 入场动画
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(0, -100f);
                rectTransform.DOAnchorPos(Vector2.zero, 0.4f).SetEase(Ease.OutCubic);
            }
        }

        protected override void OnHide()
        {
            closeButton?.onClick.RemoveAllListeners();
            ClearItemCards();
        }

        // ========== 商品刷新 ==========

        private void RefreshShopItems()
        {
            ClearItemCards();

            var shop = ShopManager.Instance;
            if (shop == null || shop.CurrentItems == null)
            {
                Debug.LogWarning("[Shop] ShopManager未初始化");
                return;
            }

            var items = shop.CurrentItems;
            for (int i = 0; i < items.Count; i++)
            {
                var card = CreateShopItemCard(items[i], i);
                itemCards.Add(card);
            }

            // 商品入场动画（依次弹入）
            for (int i = 0; i < itemCards.Count; i++)
            {
                var c = itemCards[i];
                if (c.rect == null) continue;
                c.rect.localScale = Vector3.zero;
                c.rect.DOScale(Vector3.one, 0.3f)
                    .SetDelay(i * 0.08f)
                    .SetEase(Ease.OutBack);
            }
        }

        private ShopItemCard CreateShopItemCard(ShopItem shopItem, int index)
        {
            var card = new ShopItemCard();

            if (shopItemPrefab != null)
            {
                var go = Instantiate(shopItemPrefab, itemsContainer);
                card.rect = go.GetComponent<RectTransform>();

                var nameObj = go.transform.Find("NameText");
                card.nameText = nameObj?.GetComponent<Text>();

                var statObj = go.transform.Find("StatText");
                card.statText = statObj?.GetComponent<Text>();

                var priceObj = go.transform.Find("PriceText");
                card.priceText = priceObj?.GetComponent<Text>();

                var discObj = go.transform.Find("DiscountTag");
                card.discountTag = discObj?.GetComponent<Text>();

                var buyObj = go.transform.Find("BuyButton");
                card.buyButton = buyObj?.GetComponent<Button>();
                card.buyButtonText = buyObj?.Find("Text")?.GetComponent<Text>();

                card.canvasGroup = go.GetComponent<CanvasGroup>();
                card.bgImage = go.GetComponent<Image>();
            }
            else
            {
                // 程序化创建商品卡片
                card = CreateProgrammaticCard(shopItem, index);
            }

            // 填充数据
            PopulateCardData(card, shopItem, index);

            return card;
        }

        private ShopItemCard CreateProgrammaticCard(ShopItem shopItem, int index)
        {
            var card = new ShopItemCard();
            var go = new GameObject($"ShopItem_{index}");
            go.transform.SetParent(itemsContainer, false);
            card.rect = go.AddComponent<RectTransform>();
            card.rect.sizeDelta = new Vector2(200, 180);

            // 背景
            card.bgImage = go.AddComponent<Image>();
            card.bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            // 边框
            var outline = go.AddComponent<Outline>();
            outline.effectColor = GetRarityColor(shopItem);
            outline.effectDistance = new Vector2(2, -2);

            card.canvasGroup = go.AddComponent<CanvasGroup>();

            // 名称
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.7f);
            nameRect.anchorMax = new Vector2(0.95f, 0.9f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
            card.nameText = nameGo.AddComponent<Text>();
            card.nameText.fontSize = 16;
            card.nameText.alignment = TextAnchor.MiddleCenter;
            card.nameText.color = Color.white;

            // 属性
            var statGo = new GameObject("StatText");
            statGo.transform.SetParent(go.transform, false);
            var statRect = statGo.AddComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0.05f, 0.45f);
            statRect.anchorMax = new Vector2(0.95f, 0.68f);
            statRect.offsetMin = statRect.offsetMax = Vector2.zero;
            card.statText = statGo.AddComponent<Text>();
            card.statText.fontSize = 13;
            card.statText.alignment = TextAnchor.MiddleCenter;
            card.statText.color = new Color(0.8f, 0.8f, 0.8f);

            // 价格
            var priceGo = new GameObject("PriceText");
            priceGo.transform.SetParent(go.transform, false);
            var priceRect = priceGo.AddComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.05f, 0.25f);
            priceRect.anchorMax = new Vector2(0.95f, 0.42f);
            priceRect.offsetMin = priceRect.offsetMax = Vector2.zero;
            card.priceText = priceGo.AddComponent<Text>();
            card.priceText.fontSize = 14;
            card.priceText.alignment = TextAnchor.MiddleCenter;
            card.priceText.color = new Color(1f, 0.85f, 0.2f);

            // 折扣标签
            var discGo = new GameObject("DiscountTag");
            discGo.transform.SetParent(go.transform, false);
            var discRect = discGo.AddComponent<RectTransform>();
            discRect.anchorMin = new Vector2(0.7f, 0.85f);
            discRect.anchorMax = new Vector2(0.98f, 0.98f);
            discRect.offsetMin = discRect.offsetMax = Vector2.zero;
            card.discountTag = discGo.AddComponent<Text>();
            card.discountTag.fontSize = 11;
            card.discountTag.alignment = TextAnchor.MiddleRight;
            card.discountTag.color = new Color(1f, 0.3f, 0.3f);

            // 购买按钮
            var buyGo = new GameObject("BuyButton");
            buyGo.transform.SetParent(go.transform, false);
            var buyRect = buyGo.AddComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(0.15f, 0.05f);
            buyRect.anchorMax = new Vector2(0.85f, 0.22f);
            buyRect.offsetMin = buyRect.offsetMax = Vector2.zero;
            card.buyButton = buyGo.AddComponent<Button>();
            var buyBg = buyGo.AddComponent<Image>();
            buyBg.color = new Color(0.2f, 0.6f, 0.3f);
            var buyTxtGo = new GameObject("Text");
            buyTxtGo.transform.SetParent(buyGo.transform, false);
            var buyTxtRect = buyTxtGo.AddComponent<RectTransform>();
            buyTxtRect.anchorMin = Vector2.zero;
            buyTxtRect.anchorMax = Vector2.one;
            buyTxtRect.offsetMin = buyTxtRect.offsetMax = Vector2.zero;
            card.buyButtonText = buyTxtGo.AddComponent<Text>();
            card.buyButtonText.fontSize = 14;
            card.buyButtonText.alignment = TextAnchor.MiddleCenter;
            card.buyButtonText.color = Color.white;
            card.buyButtonText.text = "购买";

            return card;
        }

        private void PopulateCardData(ShopItemCard card, ShopItem shopItem, int index)
        {
            // 名称
            string itemName = shopItem.GetName();
            if (card.nameText != null)
            {
                card.nameText.text = itemName;
                card.nameText.color = GetRarityColor(shopItem);
            }

            // 属性描述
            string stats = GetItemStats(shopItem);
            if (card.statText != null)
                card.statText.text = stats;

            // 价格
            if (card.priceText != null)
            {
                if (shopItem.isDiscounted)
                    card.priceText.text = $"💰 {shopItem.price} (折扣!)";
                else
                    card.priceText.text = $"💰 {shopItem.price}";
            }

            // 折扣标签
            if (card.discountTag != null)
            {
                card.discountTag.gameObject.SetActive(shopItem.isDiscounted);
                if (shopItem.isDiscounted)
                    card.discountTag.text = "-30%";
            }

            // 购买按钮
            if (card.buyButton != null)
            {
                card.buyButton.onClick.RemoveAllListeners();
                card.buyButton.onClick.AddListener(() => OnBuyClicked(index));

                if (card.buyButtonText != null)
                    card.buyButtonText.text = shopItem.isSold ? "已购" : "购买";

                card.buyButton.interactable = !shopItem.isSold;
            }

            // 已售灰化
            if (shopItem.isSold)
            {
                if (card.canvasGroup != null) card.canvasGroup.alpha = 0.4f;
                if (card.bgImage != null) card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        // ========== 购买逻辑 ==========

        private void OnBuyClicked(int index)
        {
            var shop = ShopManager.Instance;
            var inventory = PlayerInventory.Instance;
            if (shop == null || inventory == null) return;

            bool success = shop.BuyItem(index, inventory);
            if (success)
            {
                Debug.Log($"[Shop] 购买成功：商品#{index}");

                // 刷新金币
                RefreshGoldDisplay();

                // 刷新商品卡片状态
                var items = shop.CurrentItems;
                if (index < itemCards.Count && index < items.Count)
                {
                    var card = itemCards[index];
                    var item = items[index];

                    // 已购灰化动画
                    if (card.canvasGroup != null)
                        card.canvasGroup.DOFade(0.4f, 0.3f);

                    if (card.buyButton != null)
                    {
                        card.buyButton.interactable = false;
                        if (card.buyButtonText != null)
                            card.buyButtonText.text = "已购";
                    }

                    // 闪烁效果
                    if (card.bgImage != null)
                    {
                        card.bgImage.DOColor(new Color(0.2f, 0.6f, 0.3f), 0.15f)
                            .SetLoops(2, LoopType.Yoyo);
                    }
                }
            }
            else
            {
                Debug.Log($"[Shop] 购买失败：金币不足或商品已售");

                // 金币不足闪烁红色
                if (goldText != null)
                {
                    goldText.DOColor(Color.red, 0.15f)
                        .SetLoops(3, LoopType.Yoyo).SetLink(gameObject)
                        .OnComplete(() => { if (goldText != null) goldText.color = new Color(1f, 0.85f, 0.2f); });
                }
            }
        }

        // ========== 金币显示 ==========

        private void RefreshGoldDisplay()
        {
            var inventory = PlayerInventory.Instance;
            if (goldText != null && inventory != null)
                goldText.text = $"💰 金币: {inventory.Gold}";
        }

        // ========== 关闭 ==========

        private void OnCloseClicked()
        {
            if (rectTransform != null)
            {
                rectTransform.DOAnchorPos(new Vector2(0, -100f), 0.3f).SetEase(Ease.InCubic)
                    .SetLink(gameObject)
                    .OnComplete(() => Hide());
            }
            else
            {
                Hide();
            }
        }

        // ========== 清理 ==========

        private void ClearItemCards()
        {
            foreach (var card in itemCards)
            {
                if (card.rect != null) Destroy(card.rect.gameObject);
            }
            itemCards.Clear();
        }

        // ========== 工具方法 ==========

        private static Color GetRarityColor(ShopItem item)
        {
            if (item.type == ShopItemType.Equipment && item.equipment != null)
                return GetRarityColor(item.equipment.rarity);
            if (item.type == ShopItemType.Card && item.card?.Data != null)
                return GetRarityColor(item.card.Data.rarity);
            return Color.white;
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => RARITY_WHITE,
                CardRarity.Rare => RARITY_BLUE,
                CardRarity.Epic => RARITY_PURPLE,
                CardRarity.Legendary => RARITY_GOLD,
                _ => Color.white
            };
        }

        private static string GetItemStats(ShopItem item)
        {
            if (item.type == ShopItemType.Equipment && item.equipment != null)
            {
                var eq = item.equipment;
                var parts = new System.Collections.Generic.List<string>();
                string slotIcon = SLOT_ICONS.TryGetValue(eq.slot, out var icon) ? icon : "";
                if (eq.attackBonus != 0) parts.Add($"⚔{eq.attackBonus:+#;-#;0}");
                if (eq.defenseBonus != 0) parts.Add($"🛡{eq.defenseBonus:+#;-#;0}");
                if (eq.healthBonus != 0) parts.Add($"❤{eq.healthBonus:+#;-#;0}");
                if (eq.speedBonus != 0) parts.Add($"💨{eq.speedBonus:+#;-#;0}");
                return $"{slotIcon} {string.Join(" ", parts)}";
            }
            if (item.type == ShopItemType.Card && item.card?.Data != null)
            {
                return $"🃏 {item.card.Data.cardName}";
            }
            return "";
        }
    }
}
