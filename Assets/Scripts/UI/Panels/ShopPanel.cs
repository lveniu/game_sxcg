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
    /// │  🏪 商店 Lv.2    💰 金币:150 │  顶栏（含商店等级）
    /// ├──────────────────────────────┤
    /// │ ┌──────┐ ┌──────┐ ┌──────┐  │
    /// │ │铁剑  │ │皮甲  │ │火球术│  │  商品区
    /// │ │⚔+10  │ │🛡+5  │ │🔥    │  │  (2列/3列网格)
    /// │ │40金  │ │60金  │ │40金  │  │
    /// │ │[购买]│ │🔒Lv.3│ │[购买]│  │
    /// │ │▓▓稀有▓│ │▓▓普通▓│ │▓▓稀有▓│  │
    /// │ └──────┘ └──────┘ └──────┘  │
    /// ├──────────────────────────────┤
    /// │  🔄 刷新 20💰    [关闭商店]  │  底栏（含刷新按钮）
    /// └──────────────────────────────┘
    /// 
    /// FE-13增强：商店等级/限购/等级锁定/刷新动画/购买飞行动画
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

        #region FE-13 新增字段：等级/刷新/飞行

        [Header("FE-13: 商店等级")]
        public Text shopLevelText;         // 商店等级文字 "🏪 商店 Lv.X"

        [Header("FE-13: 刷新按钮")]
        public Button refreshButton;       // 刷新按钮
        public Text refreshButtonText;     // 刷新按钮文字 "🔄 刷新 20💰"

        #endregion

        #region 后端对接新增：Tab切换 + 空状态

        [Header("商品Tab切换")]
        public Button tabAllButton;        // Tab: 全部
        public Button tabEquipButton;      // Tab: 装备
        public Button tabCardButton;       // Tab: 卡牌
        public Text tabAllText;            // Tab文字: "全部"
        public Text tabEquipText;          // Tab文字: "装备"
        public Text tabCardText;           // Tab文字: "卡牌"

        [Header("空状态")]
        public Text emptyStateText;        // 空商店提示文字

        #endregion

        // 商品卡片缓存（增强版）
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

            #region FE-13 新增卡片字段
            public Text lockText;           // 锁定文字 "🔒 Lv.X解锁"
            public Text limitText;          // 限购文字 "1/1"
            public Image rarityBar;         // 底部稀有度条
            public int mockIndex = -1;      // 对应Mock数据索引（-1=真实数据）
            #endregion
        }

        private List<ShopItemCard> itemCards = new List<ShopItemCard>();

        /// <summary>商品Tab筛选类型</summary>
        private enum ShopTabFilter { All, Equipment, Card }

        /// <summary>当前Tab筛选</summary>
        private ShopTabFilter currentTabFilter = ShopTabFilter.All;

        /// <summary>当前关卡ID（用于调用ShopManager.GenerateShop）</summary>
        private int currentLevelId = 1;

        /// <summary>筛选后的商品索引缓存（Tab过滤后的有效索引映射到CurrentItems的真实索引）</summary>
        private List<int> filteredItemIndices = new List<int>();

        /// <summary>空状态提示GameObject（程序化创建时缓存）</summary>
        private GameObject emptyStateGo;

        #region FE-13 Mock数据系统

        /// <summary>
        /// Mock商品数据 — 用于ShopManager未初始化时的展示
        /// </summary>
        private class MockShopItem
        {
            public string name;             // 商品名
            public string rarity;           // 稀有度："Common"/"Fine"/"Rare"/"Epic"
            public int price;               // 价格
            public bool isPurchased;        // 是否已购
            public int requiredLevel;       // 所需商店等级（0=无限制）
            public string statDesc;         // 属性描述
            public int purchaseLimit = 1;   // 限购数量
            public int purchasedCount = 0;  // 已购数量
        }

        /// <summary>
        /// Mock商店静态数据
        /// </summary>
        private static class MockShopData
        {
            public static int ShopLevel = 2;
            public static int RefreshCost = 20;
            public static int PlayerGold = 150;

            /// <summary>
            /// 获取Mock商品列表（每次刷新随机变化）
            /// </summary>
            public static List<MockShopItem> GetShopItems()
            {
                var items = new List<MockShopItem>
                {
                    new MockShopItem
                    {
                        name = "精铁长剑",
                        rarity = "Common",
                        price = 35,
                        requiredLevel = 0,
                        statDesc = "⚔+12 🛡+2",
                        purchaseLimit = 1,
                        purchasedCount = 0
                    },
                    new MockShopItem
                    {
                        name = "翡翠护甲",
                        rarity = "Fine",
                        price = 60,
                        requiredLevel = 1,
                        statDesc = "🛡+15 ❤+30",
                        purchaseLimit = 1,
                        purchasedCount = 0
                    },
                    new MockShopItem
                    {
                        name = "雷鸣之刃",
                        rarity = "Rare",
                        price = 120,
                        requiredLevel = 3,
                        statDesc = "⚔+25 💨+10",
                        purchaseLimit = 1,
                        purchasedCount = 0
                    },
                    new MockShopItem
                    {
                        name = "龙魂法杖",
                        rarity = "Epic",
                        price = 200,
                        requiredLevel = 4,
                        statDesc = "⚔+40 ❤+20 💨+15",
                        purchaseLimit = 1,
                        purchasedCount = 0
                    },
                    new MockShopItem
                    {
                        name = "治疗术卷轴",
                        rarity = "Common",
                        price = 30,
                        requiredLevel = 0,
                        statDesc = "🃏 恢复30%生命",
                        purchaseLimit = 2,
                        purchasedCount = 0
                    },
                    new MockShopItem
                    {
                        name = "冰霜护盾",
                        rarity = "Fine",
                        price = 55,
                        requiredLevel = 2,
                        statDesc = "🛡+12 ❤+15",
                        purchaseLimit = 1,
                        purchasedCount = 0
                    }
                };
                return items;
            }
        }

        // 当前使用的Mock商品数据缓存
        private List<MockShopItem> currentMockItems;

        #endregion

        #region FE-13 稀有度颜色系统（4档：白/绿/蓝/紫）

        // 原有稀有度颜色（保留兼容）
        private static readonly Color RARITY_WHITE = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color RARITY_BLUE = new Color(0.3f, 0.55f, 1f);
        private static readonly Color RARITY_PURPLE = new Color(0.7f, 0.3f, 1f);
        private static readonly Color RARITY_GOLD = new Color(1f, 0.85f, 0.2f);

        // FE-13新增：精良档绿色
        private static readonly Color RARITY_GREEN = new Color(0.3f, 0.9f, 0.4f);

        /// <summary>
        /// FE-13 稀有度显示映射（4档：白/绿/蓝/紫）
        /// 通过字符串匹配，独立于CardRarity枚举
        /// </summary>
        private static Color GetDisplayRarityColor(string rarity)
        {
            return rarity switch
            {
                "Common" => RARITY_WHITE,   // 普通 - 白框
                "Fine"   => RARITY_GREEN,   // 精良 - 绿框+发光
                "Rare"   => RARITY_BLUE,    // 稀有 - 蓝框+粒子
                "Epic"   => RARITY_PURPLE,  // 史诗 - 紫框+光环
                _        => RARITY_WHITE
            };
        }

        /// <summary>
        /// 获取稀有度显示名称（带图标）
        /// </summary>
        private static string GetRarityDisplayName(string rarity)
        {
            return rarity switch
            {
                "Common" => "普通",
                "Fine"   => "精良",
                "Rare"   => "稀有",
                "Epic"   => "史诗",
                _        => ""
            };
        }

        #endregion

        // 槽位图标
        private static readonly Dictionary<EquipmentSlot, string> SLOT_ICONS = new()
        {
            { EquipmentSlot.Weapon, "⚔" },
            { EquipmentSlot.Armor, "🛡" },
            { EquipmentSlot.Accessory, "💍" },
        };

        // 是否使用Mock数据
        private bool useMockData => ShopManager.Instance == null;

        // 当前商店等级
        private int currentShopLevel => useMockData ? MockShopData.ShopLevel : 1;

        // 当前玩家金币
        private int currentGold => useMockData ? MockShopData.PlayerGold :
            (PlayerInventory.Instance != null ? PlayerInventory.Instance.Gold : 0);

        // 当前刷新费用（真实模式从BalanceProvider读取，Mock模式用固定值）
        private int refreshCost => useMockData ? MockShopData.RefreshCost :
            BalanceProvider.GetRerollCostBase();

        /// <summary>
        /// 设置当前关卡ID（由外部调用，用于ShopManager.GenerateShop）
        /// </summary>
        public void SetCurrentLevel(int levelId)
        {
            currentLevelId = levelId;
        }

        protected override void Awake()
        {
            base.Awake();
            panelId = "Shop";
        }

        protected override void OnShow()
        {
            closeButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.AddListener(OnCloseClicked);

            #region FE-13 刷新按钮绑定
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveAllListeners();
                refreshButton.onClick.AddListener(OnRefreshClicked);
            }
            #endregion

            #region 后端对接：Tab按钮绑定
            if (tabAllButton != null)
            {
                tabAllButton.onClick.RemoveAllListeners();
                tabAllButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.All));
            }
            if (tabEquipButton != null)
            {
                tabEquipButton.onClick.RemoveAllListeners();
                tabEquipButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.Equipment));
            }
            if (tabCardButton != null)
            {
                tabCardButton.onClick.RemoveAllListeners();
                tabCardButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.Card));
            }
            #endregion

            #region FE-13 初始化Mock数据
            if (useMockData && currentMockItems == null)
            {
                currentMockItems = MockShopData.GetShopItems();
            }
            #endregion

            // 初始化商店等级显示
            RefreshGoldDisplay();
            RefreshShopLevelDisplay();
            RefreshRefreshButtonDisplay();

            // 加载商品（核心后端对接入口）
            LoadShopItems();

            // 更新Tab高亮
            UpdateTabHighlight();

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
            #region FE-13 清理刷新按钮
            if (refreshButton != null)
                refreshButton.onClick.RemoveAllListeners();
            #endregion
            #region 后端对接：清理Tab按钮
            if (tabAllButton != null) tabAllButton.onClick.RemoveAllListeners();
            if (tabEquipButton != null) tabEquipButton.onClick.RemoveAllListeners();
            if (tabCardButton != null) tabCardButton.onClick.RemoveAllListeners();
            #endregion
            ClearItemCards();
            HideEmptyState();
        }

        // ========== 商品刷新 ==========

        private void RefreshShopItems()
        {
            ClearItemCards();
            HideEmptyState();

            #region FE-13 Mock数据优先
            if (useMockData)
            {
                // 使用Mock数据
                if (currentMockItems == null)
                    currentMockItems = MockShopData.GetShopItems();

                for (int i = 0; i < currentMockItems.Count; i++)
                {
                    var card = CreateMockShopItemCard(currentMockItems[i], i);
                    itemCards.Add(card);
                }
            }
            else
            #endregion
            {
                // 使用真实ShopManager数据
                var shop = ShopManager.Instance;
                if (shop == null || shop.CurrentItems == null)
                {
                    Debug.LogWarning("[Shop] ShopManager未初始化");
                    ShowEmptyState("商店数据加载中...");
                    return;
                }

                var items = shop.CurrentItems;

                // 构建筛选后的索引映射
                filteredItemIndices.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].isSold) continue; // 跳过已售商品
                    if (currentTabFilter == ShopTabFilter.All)
                    {
                        filteredItemIndices.Add(i);
                    }
                    else if (currentTabFilter == ShopTabFilter.Equipment && items[i].type == ShopItemType.Equipment)
                    {
                        filteredItemIndices.Add(i);
                    }
                    else if (currentTabFilter == ShopTabFilter.Card && items[i].type == ShopItemType.Card)
                    {
                        filteredItemIndices.Add(i);
                    }
                }

                // 空状态处理
                if (filteredItemIndices.Count == 0)
                {
                    if (items.Count == 0 || items.TrueForAll(it => it.isSold))
                    {
                        ShowEmptyState("商店已售罄，点击刷新获取新商品");
                    }
                    else
                    {
                        ShowEmptyState("当前分类无可用商品");
                    }
                    return;
                }

                // 创建筛选后的商品卡片
                for (int displayIdx = 0; displayIdx < filteredItemIndices.Count; displayIdx++)
                {
                    int realIdx = filteredItemIndices[displayIdx];
                    var card = CreateShopItemCard(items[realIdx], realIdx);
                    itemCards.Add(card);
                }
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

        #region FE-13 Mock商品卡片创建

        /// <summary>
        /// 创建Mock数据商品卡片（程序化）
        /// </summary>
        private ShopItemCard CreateMockShopItemCard(MockShopItem mockItem, int index)
        {
            var card = new ShopItemCard();
            card.mockIndex = index;

            if (shopItemPrefab != null)
            {
                // 使用预制体
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

                // FE-13: 尝试查找锁定/限购文字
                var lockObj = go.transform.Find("LockText");
                card.lockText = lockObj?.GetComponent<Text>();
                var limitObj = go.transform.Find("LimitText");
                card.limitText = limitObj?.GetComponent<Text>();
                var barObj = go.transform.Find("RarityBar");
                card.rarityBar = barObj?.GetComponent<Image>();
            }
            else
            {
                // 程序化创建
                card = CreateProgrammaticMockCard(mockItem, index);
            }

            // 填充Mock数据
            PopulateMockCardData(card, mockItem, index);

            return card;
        }

        /// <summary>
        /// 程序化创建Mock商品卡片（含稀有度条、锁定文字、限购文字）
        /// </summary>
        private ShopItemCard CreateProgrammaticMockCard(MockShopItem mockItem, int index)
        {
            var card = new ShopItemCard();
            card.mockIndex = index;
            var go = new GameObject($"ShopItem_Mock_{index}");
            go.transform.SetParent(itemsContainer, false);
            card.rect = go.AddComponent<RectTransform>();
            card.rect.sizeDelta = new Vector2(200, 200); // 加高20px给稀有度条

            // 背景
            card.bgImage = go.AddComponent<Image>();
            card.bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            // FE-13: 稀有度边框（Outline）
            var outline = go.AddComponent<Outline>();
            var rarityColor = GetDisplayRarityColor(mockItem.rarity);
            outline.effectColor = rarityColor;
            outline.effectDistance = new Vector2(3, -3);

            card.canvasGroup = go.AddComponent<CanvasGroup>();

            // 名称
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.72f);
            nameRect.anchorMax = new Vector2(0.95f, 0.9f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
            card.nameText = nameGo.AddComponent<Text>();
            card.nameText.fontSize = 16;
            card.nameText.alignment = TextAnchor.MiddleCenter;
            card.nameText.color = Color.white;

            // FE-13: 限购文字（右上角）
            var limitGo = new GameObject("LimitText");
            limitGo.transform.SetParent(go.transform, false);
            var limitRect = limitGo.AddComponent<RectTransform>();
            limitRect.anchorMin = new Vector2(0.7f, 0.88f);
            limitRect.anchorMax = new Vector2(0.98f, 0.98f);
            limitRect.offsetMin = limitRect.offsetMax = Vector2.zero;
            card.limitText = limitGo.AddComponent<Text>();
            card.limitText.fontSize = 10;
            card.limitText.alignment = TextAnchor.MiddleRight;
            card.limitText.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);

            // 属性
            var statGo = new GameObject("StatText");
            statGo.transform.SetParent(go.transform, false);
            var statRect = statGo.AddComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0.05f, 0.48f);
            statRect.anchorMax = new Vector2(0.95f, 0.68f);
            statRect.offsetMin = statRect.offsetMax = Vector2.zero;
            card.statText = statGo.AddComponent<Text>();
            card.statText.fontSize = 13;
            card.statText.alignment = TextAnchor.MiddleCenter;
            card.statText.color = new Color(0.8f, 0.8f, 0.8f);

            // FE-13: 锁定文字
            var lockGo = new GameObject("LockText");
            lockGo.transform.SetParent(go.transform, false);
            var lockRect = lockGo.AddComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.1f, 0.28f);
            lockRect.anchorMax = new Vector2(0.9f, 0.42f);
            lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
            card.lockText = lockGo.AddComponent<Text>();
            card.lockText.fontSize = 12;
            card.lockText.alignment = TextAnchor.MiddleCenter;
            card.lockText.color = new Color(1f, 0.6f, 0.2f);

            // 价格
            var priceGo = new GameObject("PriceText");
            priceGo.transform.SetParent(go.transform, false);
            var priceRect = priceGo.AddComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.05f, 0.15f);
            priceRect.anchorMax = new Vector2(0.95f, 0.3f);
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
            buyRect.anchorMin = new Vector2(0.15f, 0.01f);
            buyRect.anchorMax = new Vector2(0.85f, 0.15f);
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

            #region FE-13: 底部稀有度条（高度10px）
            var barGo = new GameObject("RarityBar");
            barGo.transform.SetParent(go.transform, false);
            var barRect = barGo.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.sizeDelta = new Vector2(0, 10);
            barRect.anchoredPosition = Vector2.zero;
            card.rarityBar = barGo.AddComponent<Image>();
            card.rarityBar.color = rarityColor;
            #endregion

            #region FE-13: 精良/稀有/史诗视觉增强
            ApplyRarityVisualEffects(card, mockItem.rarity);
            #endregion

            return card;
        }

        /// <summary>
        /// FE-13: 稀有度视觉效果增强
        /// 精良=发光脉冲, 稀有=粒子感, 史诗=光环旋转
        /// </summary>
        private void ApplyRarityVisualEffects(ShopItemCard card, string rarity)
        {
            if (card.bgImage == null) return;

            switch (rarity)
            {
                case "Fine":
                    // 精良：边框发光脉冲
                    if (card.rect != null)
                    {
                        card.rect.DOScale(new Vector3(1.02f, 1.02f, 1f), 1f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine);
                    }
                    break;

                case "Rare":
                    // 稀有：背景色微闪
                    if (card.bgImage != null)
                    {
                        var origBg = card.bgImage.color;
                        card.bgImage.DOColor(
                            new Color(origBg.r + 0.05f, origBg.g + 0.05f, origBg.b + 0.15f, origBg.a),
                            0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }
                    break;

                case "Epic":
                    // 史诗：光环旋转效果（CanvasGroup alpha脉冲模拟）
                    if (card.canvasGroup != null)
                    {
                        // 不影响交互，只在稀有度条上加脉冲
                        if (card.rarityBar != null)
                        {
                            var barImg = card.rarityBar;
                            barImg.DOFade(0.5f, 0.6f)
                                .SetLoops(-1, LoopType.Yoyo)
                                .SetEase(Ease.InOutSine);
                        }
                    }
                    break;
            }
        }

        #endregion

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
            card.rect.sizeDelta = new Vector2(200, 200); // 加高给稀有度条

            // 背景
            card.bgImage = go.AddComponent<Image>();
            card.bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            // FE-13: 稀有度边框
            var outline = go.AddComponent<Outline>();
            outline.effectColor = GetRarityColor(shopItem);
            outline.effectDistance = new Vector2(3, -3);

            card.canvasGroup = go.AddComponent<CanvasGroup>();

            // 名称
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.72f);
            nameRect.anchorMax = new Vector2(0.95f, 0.9f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
            card.nameText = nameGo.AddComponent<Text>();
            card.nameText.fontSize = 16;
            card.nameText.alignment = TextAnchor.MiddleCenter;
            card.nameText.color = Color.white;

            // FE-13: 限购文字（右上角）
            var limitGo = new GameObject("LimitText");
            limitGo.transform.SetParent(go.transform, false);
            var limitRect = limitGo.AddComponent<RectTransform>();
            limitRect.anchorMin = new Vector2(0.7f, 0.88f);
            limitRect.anchorMax = new Vector2(0.98f, 0.98f);
            limitRect.offsetMin = limitRect.offsetMax = Vector2.zero;
            card.limitText = limitGo.AddComponent<Text>();
            card.limitText.fontSize = 10;
            card.limitText.alignment = TextAnchor.MiddleRight;
            card.limitText.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);

            // 属性
            var statGo = new GameObject("StatText");
            statGo.transform.SetParent(go.transform, false);
            var statRect = statGo.AddComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0.05f, 0.48f);
            statRect.anchorMax = new Vector2(0.95f, 0.68f);
            statRect.offsetMin = statRect.offsetMax = Vector2.zero;
            card.statText = statGo.AddComponent<Text>();
            card.statText.fontSize = 13;
            card.statText.alignment = TextAnchor.MiddleCenter;
            card.statText.color = new Color(0.8f, 0.8f, 0.8f);

            // FE-13: 锁定文字
            var lockGo = new GameObject("LockText");
            lockGo.transform.SetParent(go.transform, false);
            var lockRect = lockGo.AddComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.1f, 0.28f);
            lockRect.anchorMax = new Vector2(0.9f, 0.42f);
            lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
            card.lockText = lockGo.AddComponent<Text>();
            card.lockText.fontSize = 12;
            card.lockText.alignment = TextAnchor.MiddleCenter;
            card.lockText.color = new Color(1f, 0.6f, 0.2f);

            // 价格
            var priceGo = new GameObject("PriceText");
            priceGo.transform.SetParent(go.transform, false);
            var priceRect = priceGo.AddComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0.05f, 0.15f);
            priceRect.anchorMax = new Vector2(0.95f, 0.3f);
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
            buyRect.anchorMin = new Vector2(0.15f, 0.01f);
            buyRect.anchorMax = new Vector2(0.85f, 0.15f);
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

            #region FE-13: 底部稀有度条（高度10px）
            var barGo = new GameObject("RarityBar");
            barGo.transform.SetParent(go.transform, false);
            var barRect = barGo.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.sizeDelta = new Vector2(0, 10);
            barRect.anchoredPosition = Vector2.zero;
            card.rarityBar = barGo.AddComponent<Image>();
            card.rarityBar.color = GetRarityColor(shopItem);
            #endregion

            return card;
        }

        #region FE-13 Mock卡片数据填充

        /// <summary>
        /// 填充Mock商品卡片数据（含限购/等级锁定）
        /// </summary>
        private void PopulateMockCardData(ShopItemCard card, MockShopItem mockItem, int index)
        {
            var rarityColor = GetDisplayRarityColor(mockItem.rarity);
            bool isLocked = mockItem.requiredLevel > currentShopLevel;
            bool isPurchased = mockItem.isPurchased || mockItem.purchasedCount >= mockItem.purchaseLimit;

            // 名称（带稀有度颜色）
            if (card.nameText != null)
            {
                card.nameText.text = mockItem.name;
                card.nameText.color = isLocked ? new Color(0.5f, 0.5f, 0.5f) : rarityColor;
            }

            // 属性描述
            if (card.statText != null)
                card.statText.text = mockItem.statDesc;

            // 价格
            if (card.priceText != null)
                card.priceText.text = $"💰 {mockItem.price}";

            // FE-13: 限购文字（右上角 "1/1"）
            if (card.limitText != null)
            {
                if (mockItem.purchaseLimit > 1 || mockItem.purchasedCount > 0)
                {
                    card.limitText.text = $"{mockItem.purchasedCount}/{mockItem.purchaseLimit}";
                    card.limitText.gameObject.SetActive(true);
                }
                else
                {
                    card.limitText.gameObject.SetActive(false);
                }
            }

            // FE-13: 锁定文字
            if (card.lockText != null)
            {
                if (isLocked)
                {
                    card.lockText.text = $"🔒 Lv.{mockItem.requiredLevel}解锁";
                    card.lockText.gameObject.SetActive(true);
                }
                else
                {
                    card.lockText.gameObject.SetActive(false);
                }
            }

            // 折扣标签（Mock无折扣，隐藏）
            if (card.discountTag != null)
                card.discountTag.gameObject.SetActive(false);

            // FE-13: 稀有度条
            if (card.rarityBar != null)
            {
                card.rarityBar.color = isLocked
                    ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                    : rarityColor;
            }

            // 购买按钮
            if (card.buyButton != null)
            {
                card.buyButton.onClick.RemoveAllListeners();
                card.buyButton.onClick.AddListener(() => OnBuyClickedMock(index));

                if (card.buyButtonText != null)
                {
                    if (isPurchased)
                        card.buyButtonText.text = "✓ 已购";
                    else if (isLocked)
                        card.buyButtonText.text = "🔒 锁定";
                    else
                        card.buyButtonText.text = "购买";
                }

                // 等级锁定或已购买时禁用按钮
                card.buyButton.interactable = !isLocked && !isPurchased;

                // FE-13: 锁定按钮颜色变灰
                if (isLocked && card.buyButton.image != null)
                {
                    card.buyButton.image.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                }
            }

            // 已购灰化
            if (isPurchased)
            {
                if (card.canvasGroup != null) card.canvasGroup.alpha = 0.4f;
                if (card.bgImage != null) card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            // FE-13: 锁定灰化（轻微）
            if (isLocked && !isPurchased)
            {
                if (card.canvasGroup != null) card.canvasGroup.alpha = 0.6f;
                if (card.bgImage != null)
                    card.bgImage.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
            }
        }

        #endregion

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

            // 价格（含折扣显示）
            if (card.priceText != null)
            {
                if (shopItem.isDiscounted)
                {
                    int discountedPrice = CalculateDiscountedPrice(shopItem.price);
                    // 显示原价（删除线效果通过颜色变灰暗示）+ 折扣价
                    card.priceText.text = $"<s>💰{shopItem.price}</s>  💰{discountedPrice}";
                    card.priceText.color = new Color(1f, 0.5f, 0.2f); // 橙色强调折扣
                }
                else
                {
                    card.priceText.text = $"💰 {shopItem.price}";
                }
            }

            // 折扣标签（显示实际折扣百分比）
            if (card.discountTag != null)
            {
                card.discountTag.gameObject.SetActive(shopItem.isDiscounted);
                if (shopItem.isDiscounted)
                {
                    float discountRate = BalanceProvider.GetShopDiscountRate();
                    int discountPct = Mathf.RoundToInt((1f - discountRate) * 100f);
                    card.discountTag.text = $"-{discountPct}%";
                    card.discountTag.color = new Color(1f, 0.3f, 0.3f); // 红色醒目
                }
            }

            #region FE-13: 限购标记（默认1/1）
            if (card.limitText != null)
            {
                if (shopItem.isSold)
                {
                    card.limitText.text = "1/1";
                    card.limitText.gameObject.SetActive(true);
                }
                else
                {
                    card.limitText.gameObject.SetActive(false);
                }
            }
            #endregion

            #region FE-13: 等级锁定（真实数据模式暂不锁定）
            if (card.lockText != null)
                card.lockText.gameObject.SetActive(false);
            #endregion

            #region FE-13: 稀有度条
            if (card.rarityBar != null)
                card.rarityBar.color = GetRarityColor(shopItem);
            #endregion

            // 购买按钮
            if (card.buyButton != null)
            {
                card.buyButton.onClick.RemoveAllListeners();
                card.buyButton.onClick.AddListener(() => OnBuyClicked(index));

                if (card.buyButtonText != null)
                {
                    if (shopItem.isSold)
                    {
                        card.buyButtonText.text = "✓ 已购";
                    }
                    else
                    {
                        // 金币不足时显示灰色提示
                        int finalPrice = shopItem.isDiscounted
                            ? CalculateDiscountedPrice(shopItem.price)
                            : shopItem.price;
                        card.buyButtonText.text = currentGold < finalPrice ? "💰不足" : "购买";
                    }
                }

                card.buyButton.interactable = !shopItem.isSold;

                // 金币不足时按钮变暗（但仍可点击，点击后会有金币不足提示）
                if (!shopItem.isSold && card.buyButton.image != null)
                {
                    int finalPrice = shopItem.isDiscounted
                        ? CalculateDiscountedPrice(shopItem.price)
                        : shopItem.price;
                    if (currentGold < finalPrice)
                    {
                        card.buyButton.image.color = new Color(0.4f, 0.4f, 0.3f, 0.8f);
                    }
                }
            }

            // 已售灰化
            if (shopItem.isSold)
            {
                if (card.canvasGroup != null) card.canvasGroup.alpha = 0.4f;
                if (card.bgImage != null) card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        // ========== 购买逻辑（已移至后端对接区域 OnBuyClicked） ==========

        #region FE-13 Mock购买逻辑 + 飞行动画

        /// <summary>
        /// Mock模式购买点击处理（含飞行动画）
        /// </summary>
        private void OnBuyClickedMock(int index)
        {
            if (currentMockItems == null || index >= currentMockItems.Count) return;

            var mockItem = currentMockItems[index];

            // 检查等级锁定
            if (mockItem.requiredLevel > currentShopLevel)
            {
                Debug.Log("[Shop] 商店等级不足");
                ShowLevelLockedToast();
                return;
            }

            // 检查已购
            if (mockItem.isPurchased || mockItem.purchasedCount >= mockItem.purchaseLimit)
            {
                Debug.Log("[Shop] 商品已购买");
                return;
            }

            // 检查金币
            if (currentGold < mockItem.price)
            {
                Debug.Log("[Shop] 金币不足");
                // 金币不足闪烁红色
                if (goldText != null)
                {
                    goldText.DOColor(Color.red, 0.15f)
                        .SetLoops(3, LoopType.Yoyo).SetLink(gameObject)
                        .OnComplete(() => { if (goldText != null) goldText.color = new Color(1f, 0.85f, 0.2f); });
                }
                return;
            }

            // 执行购买
            mockItem.purchasedCount++;
            if (mockItem.purchasedCount >= mockItem.purchaseLimit)
                mockItem.isPurchased = true;
            MockShopData.PlayerGold -= mockItem.price;

            Debug.Log($"[Shop] Mock购买成功：{mockItem.name} 花费{mockItem.price}金币");

            // FE-13: 购买飞行动画
            if (index < itemCards.Count)
            {
                PlayPurchaseFlyAnimation(itemCards[index], mockItem.price, index);
            }
        }

        /// <summary>
        /// FE-13: 购买飞行动画
        /// 商品卡片飞向顶部金币区域 → 金币数字-N飘字 → 飞行完成灰化
        /// </summary>
        private void PlayPurchaseFlyAnimation(ShopItemCard card, int price, int index)
        {
            if (card.rect == null)
            {
                // 无卡片RectTransform，直接更新状态
                RefreshGoldDisplay();
                UpdateMockCardState(index);
                return;
            }

            // 记录原始位置
            var originalPos = card.rect.anchoredPosition;
            var originalScale = card.rect.localScale;

            // 目标位置：金币文字位置（顶栏右侧）
            Vector2 targetPos = goldText != null
                ? goldText.rectTransform.anchoredPosition
                : new Vector2(300f, 500f);

            // 计算世界坐标差
            if (goldText != null)
            {
                // 将金币文字的世界坐标转换到itemsContainer的本地坐标
                Vector3 goldWorldPos = goldText.rectTransform.position;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    itemsContainer, goldWorldPos, null, out localPos);
                targetPos = localPos;
            }

            // 第一步：卡片缩小到0.8（0.1s）
            var seq = DOTween.Sequence();
            seq.Append(card.rect.DOScale(new Vector3(0.8f, 0.8f, 1f), 0.1f).SetEase(Ease.InQuad));

            // 第二步：飞向金币区域（0.4s贝塞尔曲线模拟）
            seq.Append(card.rect.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.InBack));
            seq.Join(card.rect.DOScale(new Vector3(0.3f, 0.3f, 1f), 0.4f).SetEase(Ease.InQuad));
            seq.Join(card.canvasGroup != null
                ? card.canvasGroup.DOFade(0.3f, 0.4f)
                : null);

            // 第三步：到达后金币飘字
            seq.AppendCallback(() =>
            {
                // 金币 -N 飘字（红色向上消失）
                ShowGoldChangeFloat($"-{price}");
                RefreshGoldDisplay();
            });

            // 第四步：卡片弹回原位 + 灰化已购状态（0.2s）
            seq.Append(card.rect.DOAnchorPos(originalPos, 0.2f).SetEase(Ease.OutCubic));
            seq.Join(card.rect.DOScale(originalScale, 0.2f).SetEase(Ease.OutBack));

            seq.OnComplete(() =>
            {
                // 飞行完成后变为已购状态
                UpdateMockCardState(index);
            });

            seq.Play();
        }

        /// <summary>
        /// FE-13: 金币变化飘字（红色向上消失）
        /// </summary>
        private void ShowGoldChangeFloat(string text)
        {
            if (goldText == null) return;

            var floatGo = new GameObject("GoldFloat");
            floatGo.transform.SetParent(goldText.rectTransform.parent, false);
            var floatRect = floatGo.AddComponent<RectTransform>();

            // 在金币文字旁边
            floatRect.anchorMin = goldText.rectTransform.anchorMin;
            floatRect.anchorMax = goldText.rectTransform.anchorMax;
            floatRect.anchoredPosition = goldText.rectTransform.anchoredPosition + new Vector2(0, 30f);
            floatRect.sizeDelta = new Vector2(120, 30);

            var floatText = floatGo.AddComponent<Text>();
            floatText.fontSize = 18;
            floatText.alignment = TextAnchor.MiddleCenter;
            floatText.color = Color.red;
            floatText.text = text;

            // 向上飘出并消失（0.8s）
            floatRect.DOAnchorPosY(floatRect.anchoredPosition.y + 60f, 0.8f)
                .SetEase(Ease.OutCubic);
            floatText.DOFade(0f, 0.8f)
                .OnComplete(() => Destroy(floatGo));
        }

        /// <summary>
        /// 更新Mock卡片为已购状态（灰化+对勾）
        /// </summary>
        private void UpdateMockCardState(int index)
        {
            if (index >= itemCards.Count || index >= currentMockItems.Count) return;

            var card = itemCards[index];
            var mockItem = currentMockItems[index];

            // 已购灰化
            if (card.canvasGroup != null)
                card.canvasGroup.alpha = 0.4f;
            if (card.bgImage != null)
                card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            // 更新购买按钮
            if (card.buyButton != null)
            {
                card.buyButton.interactable = false;
                if (card.buyButtonText != null)
                    card.buyButtonText.text = "✓ 已购";
            }

            // 更新限购文字
            if (card.limitText != null && mockItem.purchaseLimit > 0)
            {
                card.limitText.text = $"{mockItem.purchasedCount}/{mockItem.purchaseLimit}";
                card.limitText.gameObject.SetActive(true);
            }

            // 刷新刷新按钮状态（金币可能不够刷新了）
            RefreshRefreshButtonDisplay();
        }

        /// <summary>
        /// FE-13: 显示等级不足提示
        /// </summary>
        private void ShowLevelLockedToast()
        {
            // 顶栏标题闪橙色提示
            if (shopLevelText != null)
            {
                var origColor = shopLevelText.color;
                shopLevelText.color = new Color(1f, 0.4f, 0.2f);
                shopLevelText.DOColor(origColor, 0.5f).SetDelay(0.3f);
            }
            else if (shopTitleText != null)
            {
                shopTitleText.text = "⚠ 商店等级不足！";
                shopTitleText.DOColor(Color.red, 0.15f)
                    .SetLoops(4, LoopType.Yoyo)
                    .OnComplete(() =>
                    {
                        if (shopTitleText != null)
                        {
                            shopTitleText.text = $"🏪 商店";
                            shopTitleText.color = Color.white;
                        }
                    });
            }
        }

        #endregion

        #region FE-13 商店等级显示

        /// <summary>
        /// 刷新商店等级显示 "🏪 商店 Lv.X"
        /// </summary>
        private void RefreshShopLevelDisplay()
        {
            int level = currentShopLevel;

            // FE-13: 更新商店等级文字
            if (shopLevelText != null)
            {
                shopLevelText.text = $"🏪 商店 Lv.{level}";
            }

            // 同时更新标题（兼容无shopLevelText的情况）
            if (shopTitleText != null)
            {
                shopTitleText.text = $"🏪 商店 Lv.{level}";
            }
        }

        #endregion

        #region FE-13 刷新按钮

        /// <summary>
        /// 刷新按钮显示（真实模式从BalanceProvider读取刷新费用）
        /// </summary>
        private void RefreshRefreshButtonDisplay()
        {
            int cost = refreshCost;

            if (refreshButtonText != null)
            {
                refreshButtonText.text = $"🔄 刷新 {cost}💰";
            }

            if (refreshButton != null)
            {
                // 金币不足时禁用
                refreshButton.interactable = currentGold >= cost;

                // 禁用时颜色变灰
                if (refreshButton.image != null)
                {
                    refreshButton.image.color = refreshButton.interactable
                        ? new Color(0.2f, 0.5f, 0.7f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                }
            }
        }

        /// <summary>
        /// 刷新按钮点击 — 旧卡片缩小消失 → 新卡片放大出现（0.3s交错）
        /// 真实模式调用ShopManager.GenerateShop重新生成商品
        /// </summary>
        private void OnRefreshClicked()
        {
            int cost = refreshCost;

            // 检查金币
            if (currentGold < cost)
            {
                Debug.Log("[Shop] 金币不足，无法刷新");
                // 金币不足闪烁
                if (goldText != null)
                {
                    goldText.DOColor(Color.red, 0.15f)
                        .SetLoops(3, LoopType.Yoyo).SetLink(gameObject)
                        .OnComplete(() => { if (goldText != null) goldText.color = new Color(1f, 0.85f, 0.2f); });
                }
                return;
            }

            if (useMockData)
            {
                // Mock模式：扣Mock金币
                MockShopData.PlayerGold -= cost;
            }
            else
            {
                // 真实模式：通过PlayerInventory扣金币
                var inventory = PlayerInventory.Instance;
                if (inventory != null)
                {
                    if (!inventory.SpendGold(cost))
                    {
                        Debug.Log("[Shop] 扣除刷新费用失败");
                        return;
                    }
                }
            }

            RefreshGoldDisplay();

            if (useMockData)
            {
                // 生成新Mock数据
                currentMockItems = MockShopData.GetShopItems();
            }
            else
            {
                // 真实模式：调用ShopManager重新生成商品
                var shop = ShopManager.Instance;
                if (shop != null)
                {
                    shop.GenerateShop(currentLevelId);
                    Debug.Log($"[Shop] 商店已刷新，共{shop.CurrentItems.Count}件商品");
                }
            }

            // 刷新动画：旧→缩小消失 → 新→放大出现
            PlayRefreshAnimation();
        }

        /// <summary>
        /// 刷新动画（0.3s交错：旧缩小消失→新放大出现）
        /// 支持Mock和真实模式
        /// </summary>
        private void PlayRefreshAnimation()
        {
            // 保存旧卡片引用
            var oldCards = new List<ShopItemCard>(itemCards);
            itemCards.Clear();

            var seq = DOTween.Sequence();

            // 阶段1：旧卡片依次缩小消失（0.3s交错）
            for (int i = 0; i < oldCards.Count; i++)
            {
                var card = oldCards[i];
                if (card.rect == null) continue;

                float delay = i * 0.05f;
                seq.Insert(delay, card.rect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
                if (card.canvasGroup != null)
                    seq.Insert(delay, card.canvasGroup.DOFade(0f, 0.2f));
            }

            // 阶段2：旧卡片消失后清理并创建新卡片
            float createDelay = oldCards.Count * 0.05f + 0.25f;
            seq.InsertCallback(createDelay, () =>
            {
                // 清理旧卡片
                foreach (var card in oldCards)
                {
                    if (card.rect != null) Destroy(card.rect.gameObject);
                }

                if (useMockData)
                {
                    // 创建新Mock卡片
                    if (currentMockItems != null)
                    {
                        for (int i = 0; i < currentMockItems.Count; i++)
                        {
                            var card = CreateMockShopItemCard(currentMockItems[i], i);
                            itemCards.Add(card);
                        }
                    }
                }
                else
                {
                    // 真实模式：重建筛选并创建卡片
                    var shop = ShopManager.Instance;
                    if (shop != null && shop.CurrentItems != null)
                    {
                        var items = shop.CurrentItems;
                        filteredItemIndices.Clear();
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (items[i].isSold) continue;
                            if (currentTabFilter == ShopTabFilter.All)
                                filteredItemIndices.Add(i);
                            else if (currentTabFilter == ShopTabFilter.Equipment && items[i].type == ShopItemType.Equipment)
                                filteredItemIndices.Add(i);
                            else if (currentTabFilter == ShopTabFilter.Card && items[i].type == ShopItemType.Card)
                                filteredItemIndices.Add(i);
                        }

                        // 空状态
                        if (filteredItemIndices.Count == 0)
                        {
                            ShowEmptyState("商店已售罄，点击刷新获取新商品");
                        }
                        else
                        {
                            HideEmptyState();
                            for (int displayIdx = 0; displayIdx < filteredItemIndices.Count; displayIdx++)
                            {
                                int realIdx = filteredItemIndices[displayIdx];
                                var card = CreateShopItemCard(items[realIdx], realIdx);
                                itemCards.Add(card);
                            }
                        }
                    }
                }

                // 新卡片依次放大出现
                for (int i = 0; i < itemCards.Count; i++)
                {
                    var c = itemCards[i];
                    if (c.rect == null) continue;
                    c.rect.localScale = Vector3.zero;
                    c.rect.DOScale(Vector3.one, 0.3f)
                        .SetDelay(i * 0.08f)
                        .SetEase(Ease.OutBack);
                }

                // 刷新按钮状态更新
                RefreshRefreshButtonDisplay();
            });

            seq.Play();
        }

        #endregion

        // ========== 金币显示 ==========

        private void RefreshGoldDisplay()
        {
            if (goldText == null) return;

            int gold = currentGold;
            goldText.text = $"💰 金币:{gold}";

            // 金币变化时脉冲动画
            goldText.rectTransform.DOKill();
            goldText.rectTransform.localScale = Vector3.one;
            goldText.rectTransform.DOScale(new Vector3(1.2f, 1.2f, 1f), 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutQuad);
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
                CardRarity.White => RARITY_WHITE,
                CardRarity.Blue => RARITY_BLUE,
                CardRarity.Purple => RARITY_PURPLE,
                CardRarity.Gold => RARITY_GOLD,
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

        // ================================================================
        // ========== 后端对接新增方法 ==========
        // ================================================================

        #region 后端对接：商品加载

        /// <summary>
        /// 从ShopManager获取商品列表并显示
        /// 核心入口：商店初始化时调用
        /// - 真实模式：调用ShopManager.GenerateShop生成商品
        /// - Mock模式：使用MockShopData
        /// </summary>
        public void LoadShopItems()
        {
            if (useMockData)
            {
                // Mock模式：初始化Mock数据
                if (currentMockItems == null)
                    currentMockItems = MockShopData.GetShopItems();
                Debug.Log($"[Shop] Mock模式加载商品，共{currentMockItems.Count}件");
            }
            else
            {
                // 真实模式：调用ShopManager生成商品
                var shop = ShopManager.Instance;
                if (shop == null)
                {
                    Debug.LogError("[Shop] ShopManager.Instance为空，无法加载商品");
                    ShowEmptyState("商店系统未就绪");
                    return;
                }

                // 如果当前没有商品，生成新商品
                if (shop.CurrentItems == null || shop.CurrentItems.Count == 0)
                {
                    shop.GenerateShop(currentLevelId);
                    Debug.Log($"[Shop] 生成新商品，关卡={currentLevelId}，共{shop.CurrentItems.Count}件");
                }
                else
                {
                    Debug.Log($"[Shop] 使用已有商品，共{shop.CurrentItems.Count}件");
                }
            }

            // 刷新商品显示
            RefreshShopItems();
        }

        #endregion

        #region 后端对接：购买流程

        /// <summary>
        /// 购买商品（真实模式）
        /// 流程：检查金币 → 调用ShopManager.BuyItem → 扣金币+添加物品 → 更新UI
        /// </summary>
        public void OnBuyClicked(int itemIndex)
        {
            var shop = ShopManager.Instance;
            var inventory = PlayerInventory.Instance;
            if (shop == null || inventory == null) return;

            // 验证索引在当前筛选中有效
            if (itemIndex < 0 || itemIndex >= shop.CurrentItems.Count)
            {
                Debug.LogWarning($"[Shop] 商品索引无效: {itemIndex}");
                return;
            }

            var shopItem = shop.CurrentItems[itemIndex];
            if (shopItem.isSold)
            {
                Debug.Log("[Shop] 商品已售出");
                return;
            }

            // 计算最终价格（含折扣）
            int finalPrice = shopItem.isDiscounted
                ? CalculateDiscountedPrice(shopItem.price)
                : shopItem.price;

            // 预检查金币是否足够（BuyItem内部也会检查，但提前给出提示）
            if (inventory.Gold < finalPrice)
            {
                Debug.Log($"[Shop] 金币不足：需要{finalPrice}，持有{inventory.Gold}");
                PlayInsufficientGoldAnimation();
                return;
            }

            // 调用后端购买接口
            bool success = shop.BuyItem(itemIndex, inventory);
            if (success)
            {
                Debug.Log($"[Shop] 购买成功：{shopItem.GetName()}，花费{finalPrice}金币");

                // 刷新金币显示
                RefreshGoldDisplay();

                // 播放购买飞行动画
                int displayIdx = filteredItemIndices.IndexOf(itemIndex);
                if (displayIdx >= 0 && displayIdx < itemCards.Count)
                {
                    PlayPurchaseFlyAnimationReal(itemCards[displayIdx], finalPrice, displayIdx);
                }
                else
                {
                    // 无法找到显示索引，直接刷新
                    UpdateItemDisplay();
                }
            }
            else
            {
                Debug.Log("[Shop] 购买失败：金币不足或商品已售");
                PlayInsufficientGoldAnimation();
            }
        }

        /// <summary>
        /// 真实模式购买飞行动画
        /// </summary>
        private void PlayPurchaseFlyAnimationReal(ShopItemCard card, int price, int displayIdx)
        {
            if (card.rect == null)
            {
                UpdateItemDisplay();
                return;
            }

            var originalPos = card.rect.anchoredPosition;
            var originalScale = card.rect.localScale;

            // 目标位置：金币文字
            Vector2 targetPos = new Vector2(300f, 500f);
            if (goldText != null)
            {
                Vector3 goldWorldPos = goldText.rectTransform.position;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    itemsContainer, goldWorldPos, null, out localPos);
                targetPos = localPos;
            }

            var seq = DOTween.Sequence();
            seq.Append(card.rect.DOScale(new Vector3(0.8f, 0.8f, 1f), 0.1f).SetEase(Ease.InQuad));
            seq.Append(card.rect.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.InBack));
            seq.Join(card.rect.DOScale(new Vector3(0.3f, 0.3f, 1f), 0.4f).SetEase(Ease.InQuad));
            if (card.canvasGroup != null)
                seq.Join(card.canvasGroup.DOFade(0.3f, 0.4f));

            seq.AppendCallback(() =>
            {
                ShowGoldChangeFloat($"-{price}");
                RefreshGoldDisplay();
            });

            seq.Append(card.rect.DOAnchorPos(originalPos, 0.2f).SetEase(Ease.OutCubic));
            seq.Join(card.rect.DOScale(originalScale, 0.2f).SetEase(Ease.OutBack));

            seq.OnComplete(() =>
            {
                // 更新为已购状态
                if (card.canvasGroup != null) card.canvasGroup.alpha = 0.4f;
                if (card.bgImage != null) card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                if (card.buyButton != null)
                {
                    card.buyButton.interactable = false;
                    if (card.buyButtonText != null)
                        card.buyButtonText.text = "✓ 已购";
                }

                // 闪烁效果
                if (card.bgImage != null)
                {
                    card.bgImage.DOColor(new Color(0.2f, 0.6f, 0.3f), 0.15f)
                        .SetLoops(2, LoopType.Yoyo);
                }

                RefreshRefreshButtonDisplay();
            });

            seq.Play();
        }

        /// <summary>
        /// 金币不足动画（金币文字闪红）
        /// </summary>
        private void PlayInsufficientGoldAnimation()
        {
            if (goldText != null)
            {
                goldText.DOColor(Color.red, 0.15f)
                    .SetLoops(3, LoopType.Yoyo).SetLink(gameObject)
                    .OnComplete(() => { if (goldText != null) goldText.color = new Color(1f, 0.85f, 0.2f); });
            }
        }

        #endregion

        #region 后端对接：刷新商店

        // OnRefreshClicked 已在上方更新，此处补充说明
        // 刷新逻辑已整合到 OnRefreshClicked() 中：
        // - Mock模式：重新生成MockShopData
        // - 真实模式：调用ShopManager.GenerateShop(currentLevelId)

        #endregion

        #region 后端对接：折扣计算

        /// <summary>
        /// 计算折扣价
        /// 从BalanceProvider获取折扣率（默认0.7），计算最终价格
        /// </summary>
        /// <param name="originalPrice">原价</param>
        /// <returns>折扣后价格</returns>
        public int CalculateDiscountedPrice(int originalPrice)
        {
            float discountRate = BalanceProvider.GetShopDiscountRate();
            return Mathf.RoundToInt(originalPrice * discountRate);
        }

        #endregion

        #region 后端对接：商品UI更新

        /// <summary>
        /// 更新所有商品卡片的显示状态
        /// 用于购买后刷新、金币变化后更新可购买状态
        /// </summary>
        public void UpdateItemDisplay()
        {
            if (useMockData) return; // Mock模式由自己的逻辑处理

            var shop = ShopManager.Instance;
            if (shop == null || shop.CurrentItems == null) return;

            var items = shop.CurrentItems;

            for (int displayIdx = 0; displayIdx < itemCards.Count && displayIdx < filteredItemIndices.Count; displayIdx++)
            {
                int realIdx = filteredItemIndices[displayIdx];
                if (realIdx >= items.Count) continue;

                var card = itemCards[displayIdx];
                var item = items[realIdx];

                // 更新购买按钮状态
                if (card.buyButton != null)
                {
                    if (item.isSold)
                    {
                        card.buyButton.interactable = false;
                        if (card.buyButtonText != null)
                            card.buyButtonText.text = "✓ 已购";
                        // 灰化
                        if (card.canvasGroup != null) card.canvasGroup.alpha = 0.4f;
                        if (card.bgImage != null) card.bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    }
                    else
                    {
                        int finalPrice = item.isDiscounted
                            ? CalculateDiscountedPrice(item.price)
                            : item.price;
                        bool canAfford = currentGold >= finalPrice;

                        if (card.buyButtonText != null)
                            card.buyButtonText.text = canAfford ? "购买" : "💰不足";

                        card.buyButton.interactable = true;

                        // 金币不足时按钮变暗
                        if (card.buyButton.image != null)
                        {
                            card.buyButton.image.color = canAfford
                                ? new Color(0.2f, 0.6f, 0.3f)
                                : new Color(0.4f, 0.4f, 0.3f, 0.8f);
                        }
                    }
                }
            }
        }

        #endregion

        #region 后端对接：Tab切换

        /// <summary>
        /// Tab切换点击处理
        /// </summary>
        private void OnTabClicked(ShopTabFilter filter)
        {
            if (currentTabFilter == filter) return; // 已经是当前Tab

            currentTabFilter = filter;
            Debug.Log($"[Shop] 切换Tab: {filter}");

            UpdateTabHighlight();
            RefreshShopItems();
        }

        /// <summary>
        /// 更新Tab按钮高亮状态
        /// </summary>
        private void UpdateTabHighlight()
        {
            // 高亮色
            Color activeColor = new Color(0.3f, 0.8f, 1f);
            Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);

            // 更新按钮文字颜色
            if (tabAllText != null)
                tabAllText.color = (currentTabFilter == ShopTabFilter.All) ? activeColor : inactiveColor;
            if (tabEquipText != null)
                tabEquipText.color = (currentTabFilter == ShopTabFilter.Equipment) ? activeColor : inactiveColor;
            if (tabCardText != null)
                tabCardText.color = (currentTabFilter == ShopTabFilter.Card) ? activeColor : inactiveColor;

            // 更新按钮图片颜色（如有）
            if (tabAllButton != null && tabAllButton.image != null)
                tabAllButton.image.color = (currentTabFilter == ShopTabFilter.All)
                    ? new Color(0.15f, 0.25f, 0.35f, 0.9f)
                    : new Color(0.1f, 0.1f, 0.15f, 0.7f);
            if (tabEquipButton != null && tabEquipButton.image != null)
                tabEquipButton.image.color = (currentTabFilter == ShopTabFilter.Equipment)
                    ? new Color(0.15f, 0.25f, 0.35f, 0.9f)
                    : new Color(0.1f, 0.1f, 0.15f, 0.7f);
            if (tabCardButton != null && tabCardButton.image != null)
                tabCardButton.image.color = (currentTabFilter == ShopTabFilter.Card)
                    ? new Color(0.15f, 0.25f, 0.35f, 0.9f)
                    : new Color(0.1f, 0.1f, 0.15f, 0.7f);

            // 程序化创建Tab（如果Inspector未赋值）
            EnsureTabButtonsCreated();
        }

        /// <summary>
        /// 程序化创建Tab按钮（如果Inspector中未手动赋值）
        /// </summary>
        private bool _tabsCreated = false;
        private void EnsureTabButtonsCreated()
        {
            if (_tabsCreated) return;
            if (tabAllButton != null || tabEquipButton != null || tabCardButton != null)
            {
                _tabsCreated = true; // Inspector中已有赋值，不需要程序化创建
                return;
            }

            // 找到itemsContainer的父级来放置Tab
            var parent = itemsContainer?.parent;
            if (parent == null) return;

            // 创建Tab容器
            var tabContainerGo = new GameObject("TabContainer");
            var tabContainerRect = tabContainerGo.AddComponent<RectTransform>();
            tabContainerRect.SetParent(parent, false);

            // 放在商品区上方
            tabContainerRect.anchorMin = new Vector2(0.05f, 0.82f);
            tabContainerRect.anchorMax = new Vector2(0.95f, 0.88f);
            tabContainerRect.offsetMin = tabContainerRect.offsetMax = Vector2.zero;

            var tabBg = tabContainerGo.AddComponent<Image>();
            tabBg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);

            // 创建三个Tab按钮
            CreateTabButton("TabAll", "全部", 0f, 0.33f, tabContainerRect, out tabAllButton, out tabAllText, ShopTabFilter.All);
            CreateTabButton("TabEquip", "⚔ 装备", 0.33f, 0.66f, tabContainerRect, out tabEquipButton, out tabEquipText, ShopTabFilter.Equipment);
            CreateTabButton("TabCard", "🃏 卡牌", 0.66f, 1f, tabContainerRect, out tabCardButton, out tabCardText, ShopTabFilter.Card);

            // 绑定事件
            if (tabAllButton != null)
            {
                tabAllButton.onClick.RemoveAllListeners();
                tabAllButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.All));
            }
            if (tabEquipButton != null)
            {
                tabEquipButton.onClick.RemoveAllListeners();
                tabEquipButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.Equipment));
            }
            if (tabCardButton != null)
            {
                tabCardButton.onClick.RemoveAllListeners();
                tabCardButton.onClick.AddListener(() => OnTabClicked(ShopTabFilter.Card));
            }

            _tabsCreated = true;
        }

        /// <summary>
        /// 创建单个Tab按钮
        /// </summary>
        private void CreateTabButton(string name, string label, float anchorMinX, float anchorMaxX,
            RectTransform parent, out Button outButton, out Text outText, ShopTabFilter filter)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(anchorMinX, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            outButton = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;

            outText = txtGo.AddComponent<Text>();
            outText.text = label;
            outText.fontSize = 14;
            outText.alignment = TextAnchor.MiddleCenter;
            outText.color = new Color(0.5f, 0.5f, 0.5f);
        }

        #endregion

        #region 后端对接：空状态处理

        /// <summary>
        /// 显示空商店状态
        /// </summary>
        private void ShowEmptyState(string message)
        {
            // 优先使用Inspector赋值的emptyStateText
            if (emptyStateText != null)
            {
                emptyStateText.text = message;
                emptyStateText.gameObject.SetActive(true);
                return;
            }

            // 程序化创建空状态提示
            if (emptyStateGo == null && itemsContainer != null)
            {
                emptyStateGo = new GameObject("EmptyState");
                var rect = emptyStateGo.AddComponent<RectTransform>();
                rect.SetParent(itemsContainer, false);
                rect.anchorMin = new Vector2(0.1f, 0.2f);
                rect.anchorMax = new Vector2(0.9f, 0.8f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;

                var txt = emptyStateGo.AddComponent<Text>();
                txt.fontSize = 18;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                emptyStateText = txt; // 缓存引用以便后续更新文字
            }

            if (emptyStateGo != null)
            {
                emptyStateGo.SetActive(true);
                if (emptyStateText != null)
                    emptyStateText.text = message;
            }
        }

        /// <summary>
        /// 隐藏空状态提示
        /// </summary>
        private void HideEmptyState()
        {
            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(false);
            if (emptyStateGo != null)
                emptyStateGo.SetActive(false);
        }

        #endregion
    }
}
