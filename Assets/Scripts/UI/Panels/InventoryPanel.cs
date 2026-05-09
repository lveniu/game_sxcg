using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    // ═══════════════════════════════════════════════════════════
    // FE-09: 背包系统UI重构 — 分类Tab + 网格展示 + 详情面板 + 快捷穿戴
    // ═══════════════════════════════════════════════════════════
    //
    // 布局（竖屏 720×1280）：
    // ┌──────────────────────────────────────┐
    // │  [关闭] 背包    💰 1234金币          │  顶部栏
    // ├──────────────────────────────────────┤
    // │ [全部] [装备] [材料] [消耗品]        │  Tab栏
    // ├──────────────┬───────────────────────┤
    // │  ┌──┐ ┌──┐  │  📋 物品详情          │
    // │  │🗡│ │🛡│  │  名称/稀有度/属性      │
    // │  └──┘ └──┘  │  [装备到英雄▼]        │
    // │  ┌──┐ ┌──┐  │  [丢弃]              │
    // │  │💍│ │🗡│  │                       │
    // │  └──┘ └──┘  │                       │
    // └──────────────┴───────────────────────┘
    //
    // 交互：
    // 1. Tab切换分类（全部/装备/材料/消耗品）
    // 2. 网格物品点击 → 右侧详情更新
    // 3. "装备到英雄" → 英雄选择下拉 → EquipToHero
    // 4. "丢弃" → 二次确认弹窗
    //
    // 三层动画安全（同FE-08规范）
    // ═══════════════════════════════════════════════════════════

    /// <summary>物品分类枚举（与后端 ItemCategory 对齐）</summary>
    public enum ItemCategory
    {
        All,         // 全部
        Equipment,   // 装备
        Material,    // 材料（预留）
        Consumable   // 消耗品（预留）
    }

    /// <summary>装备稀有度（复用后端 CardRarity）</summary>
    public enum ItemRarity
    {
        White,  // 普通
        Blue,   // 稀有
        Purple, // 史诗
        Gold    // 传说
    }

    /// <summary>装备槽位（复用后端 EquipmentSlot）</summary>
    public enum EquipSlot
    {
        Weapon,    // 武器
        Armor,     // 防具
        Accessory  // 饰品
    }

    /// <summary>装备数据（Mock版，后端用 EquipmentData）</summary>
    public class InventoryItem
    {
        public string itemId;
        public string itemName;
        public string description;
        public ItemCategory category;
        public EquipSlot slot;
        public ItemRarity rarity;
        public int attackBonus;
        public int defenseBonus;
        public int healthBonus;
        public int speedBonus;
        public float critRateBonus;
        public string specialEffect;
    }

    /// <summary>英雄数据（Mock版，用于装备穿戴下拉）</summary>
    public class HeroInfo
    {
        public string heroId;
        public string heroName;
        public string className;
        public Dictionary<EquipSlot, InventoryItem> equippedItems = new Dictionary<EquipSlot, InventoryItem>();
    }

    /// <summary>
    /// FE-09 背包系统UI面板
    /// 子面板模式：从多个入口打开（主菜单/战斗结算/商店购买后）
    /// </summary>
    public class InventoryPanel : UIPanel
    {
        // ──────── 布局常量 ────────
        private const float GRID_CELL_SIZE = 120f;
        private const float GRID_SPACING = 10f;
        private const float GRID_COLUMNS = 3f;
        private const float TAB_ANIM_DURATION = 0.2f;

        // ──────── 稀有度颜色 ────────
        private static readonly Color COLOR_RARITY_WHITE  = HexColor("#CCCCCC");
        private static readonly Color COLOR_RARITY_BLUE   = HexColor("#4A90D9");
        private static readonly Color COLOR_RARITY_PURPLE = HexColor("#9B59B6");
        private static readonly Color COLOR_RARITY_GOLD   = HexColor("#FFD700");

        // ──────── Inspector 引用 ────────
        [Header("顶部栏")]
        public Text titleText;
        public Text goldText;
        public Button closeButton;

        [Header("Tab栏")]
        public Button tabAllButton;
        public Button tabEquipButton;
        public Button tabMaterialButton;
        public Button tabConsumableButton;
        public Image tabAllHighlight;
        public Image tabEquipHighlight;
        public Image tabMaterialHighlight;
        public Image tabConsumableHighlight;

        [Header("物品网格区")]
        public ScrollRect itemScrollRect;
        public RectTransform gridContent;        // 网格容器

        [Header("详情面板")]
        public RectTransform detailPanel;
        public Text detailNameText;
        public Text detailRarityText;
        public Text detailSlotText;
        public Text detailAttackText;
        public Text detailDefenseText;
        public Text detailHealthText;
        public Text detailSpeedText;
        public Text detailCritText;
        public Text detailEffectText;
        public Text detailDescText;
        public Button equipButton;
        public Button discardButton;

        [Header("英雄选择弹窗")]
        public RectTransform heroSelectPopup;
        public RectTransform heroListContainer;

        [Header("确认丢弃弹窗")]
        public RectTransform confirmDiscardPopup;
        public Text confirmDiscardText;
        public Button confirmDiscardYesButton;
        public Button confirmDiscardNoButton;

        // ──────── 运行时状态 ────────
        private List<InventoryItem> allItems = new List<InventoryItem>();
        private List<HeroInfo> heroes = new List<HeroInfo>();
        private ItemCategory currentTab = ItemCategory.All;
        private InventoryItem selectedItem;
        private readonly Dictionary<string, RectTransform> itemCells = new Dictionary<string, RectTransform>();
        private int playerGold;

        // ══════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            slideInAnimation = false;

            // Tab按钮
            tabAllButton?.onClick.AddListener(() => SwitchTab(ItemCategory.All));
            tabEquipButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Equipment));
            tabMaterialButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Material));
            tabConsumableButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Consumable));

            // 操作按钮
            closeButton?.onClick.AddListener(OnCloseClicked);
            equipButton?.onClick.AddListener(OnEquipClicked);
            discardButton?.onClick.AddListener(OnDiscardClicked);

            // 确认丢弃弹窗
            confirmDiscardYesButton?.onClick.AddListener(OnConfirmDiscard);
            confirmDiscardNoButton?.onClick.AddListener(OnCancelDiscard);
        }

        public override void Show()
        {
            base.Show();
            LoadInventoryAndRender();
        }

        public override void OnShow()
        {
            ResetDetailPanel();
            HideHeroSelectPopup();
            HideConfirmDiscardPopup();
        }

        public override void OnHide()
        {
            ClearGrid();
        }

        // ══════════════════════════════════════
        // 数据加载（Mock先行）
        // ══════════════════════════════════════

        private void LoadInventoryAndRender()
        {
            // TODO: 后端就绪后替换为 PlayerInventory.Instance 读取
            allItems = GenerateMockItems();
            heroes = GenerateMockHeroes();
            playerGold = 1234;

            UpdateGoldDisplay();
            SwitchTab(ItemCategory.All);
        }

        private List<InventoryItem> GenerateMockItems()
        {
            return new List<InventoryItem>
            {
                new InventoryItem
                {
                    itemId = "equip_001", itemName = "铁剑", category = ItemCategory.Equipment,
                    slot = EquipSlot.Weapon, rarity = ItemRarity.White,
                    attackBonus = 5, defenseBonus = 0, healthBonus = 0, speedBonus = 2,
                    critRateBonus = 0.03f, specialEffect = "", description = "一把普通的铁剑"
                },
                new InventoryItem
                {
                    itemId = "equip_002", itemName = "铁盾", category = ItemCategory.Equipment,
                    slot = EquipSlot.Armor, rarity = ItemRarity.White,
                    attackBonus = 0, defenseBonus = 8, healthBonus = 20, speedBonus = -1,
                    critRateBonus = 0, specialEffect = "", description = "厚重的铁盾"
                },
                new InventoryItem
                {
                    itemId = "equip_003", itemName = "力量戒指", category = ItemCategory.Equipment,
                    slot = EquipSlot.Accessory, rarity = ItemRarity.Blue,
                    attackBonus = 3, defenseBonus = 0, healthBonus = 10, speedBonus = 0,
                    critRateBonus = 0.05f, specialEffect = "暴击时回复5%生命", description = "蕴含力量的蓝色戒指"
                },
                new InventoryItem
                {
                    itemId = "equip_004", itemName = "暗影匕首", category = ItemCategory.Equipment,
                    slot = EquipSlot.Weapon, rarity = ItemRarity.Purple,
                    attackBonus = 12, defenseBonus = 0, healthBonus = 0, speedBonus = 5,
                    critRateBonus = 0.15f, specialEffect = "攻击附带流血效果", description = "刺客专用的暗影武器"
                },
                new InventoryItem
                {
                    itemId = "equip_005", itemName = "圣光铠甲", category = ItemCategory.Equipment,
                    slot = EquipSlot.Armor, rarity = ItemRarity.Gold,
                    attackBonus = 2, defenseBonus = 15, healthBonus = 50, speedBonus = -3,
                    critRateBonus = 0, specialEffect = "受击时10%概率回复20%生命", description = "传说级圣光铠甲"
                },
                new InventoryItem
                {
                    itemId = "equip_006", itemName = "法师长袍", category = ItemCategory.Equipment,
                    slot = EquipSlot.Armor, rarity = ItemRarity.Blue,
                    attackBonus = 6, defenseBonus = 3, healthBonus = 15, speedBonus = 1,
                    critRateBonus = 0.08f, specialEffect = "法术伤害+15%", description = "为法师定制的蓝色长袍"
                },
                new InventoryItem
                {
                    itemId = "equip_007", itemName = "疾风之靴", category = ItemCategory.Equipment,
                    slot = EquipSlot.Accessory, rarity = ItemRarity.Purple,
                    attackBonus = 0, defenseBonus = 2, healthBonus = 0, speedBonus = 8,
                    critRateBonus = 0.1f, specialEffect = "闪避率+10%", description = "轻盈的紫色靴子"
                },
                new InventoryItem
                {
                    itemId = "equip_008", itemName = "短弓", category = ItemCategory.Equipment,
                    slot = EquipSlot.Weapon, rarity = ItemRarity.White,
                    attackBonus = 4, defenseBonus = 0, healthBonus = 0, speedBonus = 3,
                    critRateBonus = 0.05f, specialEffect = "", description = "普通的短弓"
                }
            };
        }

        private List<HeroInfo> GenerateMockHeroes()
        {
            return new List<HeroInfo>
            {
                new HeroInfo { heroId = "warrior", heroName = "战士", className = "Warrior" },
                new HeroInfo { heroId = "mage", heroName = "法师", className = "Mage" },
                new HeroInfo { heroId = "assassin", heroName = "刺客", className = "Assassin" }
            };
        }

        // ══════════════════════════════════════
        // Tab 切换
        // ══════════════════════════════════════

        private void SwitchTab(ItemCategory tab)
        {
            currentTab = tab;

            // 更新Tab高亮
            SetTabHighlight(tabAllHighlight, tab == ItemCategory.All);
            SetTabHighlight(tabEquipHighlight, tab == ItemCategory.Equipment);
            SetTabHighlight(tabMaterialHighlight, tab == ItemCategory.Material);
            SetTabHighlight(tabConsumableHighlight, tab == ItemCategory.Consumable);

            // 过滤物品
            var filtered = GetFilteredItems();

            // 重新渲染网格
            ClearGrid();
            RenderGrid(filtered);

            // 重置详情
            ResetDetailPanel();
            selectedItem = null;

            // Tab切换淡入动画
            if (gridContent != null)
            {
                var cg = gridContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = gridContent.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.DOFade(1f, TAB_ANIM_DURATION).SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            }
        }

        private void SetTabHighlight(Image highlight, bool active)
        {
            if (highlight == null) return;
            highlight.gameObject.SetActive(active);
            if (active)
            {
                highlight.color = COLOR_RARITY_GOLD;
            }
        }

        private List<InventoryItem> GetFilteredItems()
        {
            if (currentTab == ItemCategory.All) return allItems;

            var filtered = new List<InventoryItem>();
            foreach (var item in allItems)
            {
                if (item.category == currentTab) filtered.Add(item);
            }

            // 材料/消耗品 Tab 暂无数据时返回空列表
            return filtered;
        }

        // ══════════════════════════════════════
        // 网格渲染
        // ══════════════════════════════════════

        private void RenderGrid(List<InventoryItem> items)
        {
            if (gridContent == null) return;

            // 空状态
            if (items.Count == 0)
            {
                var emptyGo = new GameObject("EmptyState");
                emptyGo.transform.SetParent(gridContent, false);
                var emptyRect = emptyGo.AddComponent<RectTransform>();
                emptyRect.anchorMin = Vector2.zero;
                emptyRect.anchorMax = Vector2.one;
                emptyRect.offsetMin = Vector2.zero;
                emptyRect.offsetMax = Vector2.zero;
                var emptyText = emptyGo.AddComponent<Text>();
                emptyText.text = currentTab == ItemCategory.Material ? "暂无材料" :
                                 currentTab == ItemCategory.Consumable ? "暂无消耗品" : "背包空空如也~";
                emptyText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
                emptyText.fontSize = 20;
                emptyText.alignment = TextAnchor.MiddleCenter;
                emptyText.color = new Color(0.6f, 0.6f, 0.6f);
                return;
            }

            // 计算网格尺寸
            int rows = Mathf.CeilToInt(items.Count / GRID_COLUMNS);
            float contentHeight = rows * (GRID_CELL_SIZE + GRID_SPACING) + GRID_SPACING;
            gridContent.sizeDelta = new Vector2(gridContent.sizeDelta.x, contentHeight);

            for (int i = 0; i < items.Count; i++)
            {
                RenderItemCell(items[i], i);
            }
        }

        private void RenderItemCell(InventoryItem item, int index)
        {
            int col = index % (int)GRID_COLUMNS;
            int row = index / (int)GRID_COLUMNS;

            // 计算位置
            float x = GRID_SPACING + col * (GRID_CELL_SIZE + GRID_SPACING) + GRID_CELL_SIZE / 2f;
            float y = contentHeight_grid - GRID_SPACING - row * (GRID_CELL_SIZE + GRID_SPACING) - GRID_CELL_SIZE / 2f;

            // 创建格子
            var go = new GameObject($"Item_{item.itemId}");
            go.transform.SetParent(gridContent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(GRID_CELL_SIZE, GRID_CELL_SIZE);
            rect.anchoredPosition = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 背景（稀有度边框色）
            var bg = go.AddComponent<Image>();
            bg.color = GetRarityColor(item.rarity);

            // 内部区域（暗色背景）
            var innerGo = new GameObject("Inner");
            innerGo.transform.SetParent(go.transform, false);
            var innerRect = innerGo.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(3f, 3f);
            innerRect.offsetMax = new Vector2(-3f, -3f);
            var innerBg = innerGo.AddComponent<Image>();
            innerBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // 物品图标（用文字模拟）
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(innerGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5f, 20f);
            iconRect.offsetMax = new Vector2(-5f, -10f);
            var iconText = iconGo.AddComponent<Text>();
            iconText.text = GetSlotIcon(item.slot);
            iconText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 32;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.raycastTarget = false;

            // 物品名称（底部小字）
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(innerGo.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0.3f);
            nameRect.offsetMin = new Vector2(2f, 0f);
            nameRect.offsetMax = new Vector2(-2f, 0f);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = item.itemName;
            nameText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 11;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.raycastTarget = false;

            // 点击按钮
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnItemClicked(item.itemId));

            itemCells[item.itemId] = rect;
        }

        private float contentHeight_grid => gridContent != null ? gridContent.sizeDelta.y : 1000f;

        private void ClearGrid()
        {
            foreach (var kvp in itemCells)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            itemCells.Clear();
        }

        // ══════════════════════════════════════
        // 物品详情
        // ══════════════════════════════════════

        private void OnItemClicked(string itemId)
        {
            var item = allItems.Find(i => i.itemId == itemId);
            if (item == null) return;

            // 取消之前的选中
            if (selectedItem != null && itemCells.TryGetValue(selectedItem.itemId, out var prevRect))
            {
                prevRect.DOKill();
                prevRect.localScale = Vector3.one;
            }

            // 选中新物品
            selectedItem = item;
            if (itemCells.TryGetValue(itemId, out var rect))
            {
                rect.DOScale(new Vector3(1.1f, 1.1f, 1f), 0.1f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (rect == null) return;
                        rect.DOScale(new Vector3(1.05f, 1.05f, 1f), 0.1f)
                            .SetEase(Ease.OutQuad)
                            .SetLink(gameObject);
                    });
            }

            // 更新详情面板
            UpdateDetailPanel(item);
        }

        private void UpdateDetailPanel(InventoryItem item)
        {
            if (detailPanel == null) return;
            detailPanel.gameObject.SetActive(true);

            // 基本信息
            if (detailNameText != null)
                detailNameText.text = item.itemName;
            if (detailNameText != null)
                detailNameText.color = GetRarityColor(item.rarity);

            if (detailRarityText != null)
                detailRarityText.text = $"稀有度: {GetRarityName(item.rarity)}";

            if (detailSlotText != null)
                detailSlotText.text = $"槽位: {GetSlotName(item.slot)}";

            // 属性加成（绿色正数，红色负数）
            SetStatText(detailAttackText, "ATK", item.attackBonus);
            SetStatText(detailDefenseText, "DEF", item.defenseBonus);
            SetStatText(detailHealthText, "HP", item.healthBonus);
            SetStatText(detailSpeedText, "SPD", item.speedBonus);

            if (detailCritText != null)
            {
                if (item.critRateBonus > 0)
                {
                    detailCritText.text = $"CRIT +{item.critRateBonus:P0}";
                    detailCritText.color = Color.green;
                }
                else
                {
                    detailCritText.text = "CRIT +0%";
                    detailCritText.color = Color.gray;
                }
            }

            // 特效
            if (detailEffectText != null)
            {
                if (!string.IsNullOrEmpty(item.specialEffect))
                {
                    detailEffectText.text = $"⚡ 特效: {item.specialEffect}";
                    detailEffectText.color = COLOR_RARITY_GOLD;
                }
                else
                {
                    detailEffectText.text = "";
                }
            }

            // 描述
            if (detailDescText != null)
                detailDescText.text = item.description;

            // 按钮可见性：只有装备才能穿戴
            if (equipButton != null)
                equipButton.gameObject.SetActive(item.category == ItemCategory.Equipment);
            if (discardButton != null)
                discardButton.gameObject.SetActive(true);
        }

        private void ResetDetailPanel()
        {
            if (detailPanel == null) return;
            detailPanel.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════
        // 装备到英雄
        // ══════════════════════════════════════

        private void OnEquipClicked()
        {
            if (selectedItem == null) return;
            ShowHeroSelectPopup(selectedItem);
        }

        private void ShowHeroSelectPopup(InventoryItem item)
        {
            if (heroSelectPopup == null || heroListContainer == null) return;

            heroSelectPopup.gameObject.SetActive(true);

            // 清除旧英雄按钮
            foreach (Transform child in heroListContainer)
            {
                Destroy(child.gameObject);
            }

            // 生成英雄列表
            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                var btnGo = new GameObject($"Hero_{hero.heroId}");
                btnGo.transform.SetParent(heroListContainer, false);

                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(250f, 50f);
                btnRect.anchoredPosition = new Vector2(0, -i * 60f);
                btnRect.pivot = new Vector2(0.5f, 1f);

                var bg = btnGo.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

                var btn = btnGo.AddComponent<Button>();
                var capturedItem = item;
                var capturedHero = hero;
                btn.onClick.AddListener(() => OnHeroSelected(capturedItem, capturedHero));

                // 英雄名+当前装备信息
                var label = btnGo.AddComponent<Text>();
                string currentEquip = "";
                if (hero.equippedItems.TryGetValue(item.slot, out var equipped))
                    currentEquip = $" (当前: {equipped.itemName})";
                label.text = $"{GetClassIcon(hero.className)} {hero.heroName}{currentEquip}";
                label.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
                label.fontSize = 18;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
            }

            // 弹窗缩放入场
            heroSelectPopup.localScale = Vector3.zero;
            heroSelectPopup.DOScale(Vector3.one, 0.25f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void HideHeroSelectPopup()
        {
            if (heroSelectPopup != null)
                heroSelectPopup.gameObject.SetActive(false);
        }

        private void OnHeroSelected(InventoryItem item, HeroInfo hero)
        {
            // TODO: 后端替换为 PlayerInventory.Instance.EquipToHero(item, hero)

            // 如果该槽位已有装备，自动卸下回背包
            if (hero.equippedItems.ContainsKey(item.slot))
            {
                var oldEquip = hero.equippedItems[item.slot];
                allItems.Add(oldEquip);
                hero.equippedItems.Remove(item.slot);
            }

            // 从背包移除并装备到英雄
            allItems.Remove(item);
            hero.equippedItems[item.slot] = item;

            Debug.Log($"[Inventory] {item.itemName} 装备到 {hero.heroName}");

            // 装备成功动画：格子飞出
            if (itemCells.TryGetValue(item.itemId, out var rect))
            {
                rect.DOScale(Vector3.zero, 0.3f)
                    .SetEase(Ease.InBack)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        HideHeroSelectPopup();
                        // 刷新网格
                        SwitchTab(currentTab);
                    });
            }
            else
            {
                HideHeroSelectPopup();
                SwitchTab(currentTab);
            }
        }

        // ══════════════════════════════════════
        // 丢弃物品
        // ══════════════════════════════════════

        private void OnDiscardClicked()
        {
            if (selectedItem == null) return;
            ShowConfirmDiscardPopup(selectedItem);
        }

        private void ShowConfirmDiscardPopup(InventoryItem item)
        {
            if (confirmDiscardPopup == null) return;

            confirmDiscardPopup.gameObject.SetActive(true);
            if (confirmDiscardText != null)
                confirmDiscardText.text = $"确定丢弃「{item.itemName}」吗？";

            confirmDiscardPopup.localScale = Vector3.zero;
            confirmDiscardPopup.DOScale(Vector3.one, 0.2f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void HideConfirmDiscardPopup()
        {
            if (confirmDiscardPopup != null)
                confirmDiscardPopup.gameObject.SetActive(false);
        }

        private void OnConfirmDiscard()
        {
            if (selectedItem == null) return;

            // TODO: 后端替换为 PlayerInventory.Instance.RemoveEquipment(item)
            allItems.Remove(selectedItem);

            // 丢弃动画
            if (itemCells.TryGetValue(selectedItem.itemId, out var rect))
            {
                var captured = rect;
                rect.DOScale(Vector3.zero, 0.3f)
                    .SetEase(Ease.InBack)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (captured != null) Destroy(captured.gameObject);
                    });
            }

            Debug.Log($"[Inventory] 丢弃: {selectedItem.itemName}");

            HideConfirmDiscardPopup();
            selectedItem = null;
            ResetDetailPanel();

            // 延迟刷新
            DOVirtual.DelayedCall(0.35f, () => SwitchTab(currentTab));
        }

        private void OnCancelDiscard()
        {
            HideConfirmDiscardPopup();
        }

        // ══════════════════════════════════════
        // 关闭
        // ══════════════════════════════════════

        private void OnCloseClicked()
        {
            Hide();
        }

        // ══════════════════════════════════════
        // 金币显示
        // ══════════════════════════════════════

        private void UpdateGoldDisplay()
        {
            if (goldText != null)
                goldText.text = $"💰 {playerGold}";
        }

        // ══════════════════════════════════════
        // 辅助方法
        // ══════════════════════════════════════

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.White  => COLOR_RARITY_WHITE,
                ItemRarity.Blue   => COLOR_RARITY_BLUE,
                ItemRarity.Purple => COLOR_RARITY_PURPLE,
                ItemRarity.Gold   => COLOR_RARITY_GOLD,
                _ => Color.gray
            };
        }

        private static string GetRarityName(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.White  => "⭐ 普通",
                ItemRarity.Blue   => "⭐⭐ 稀有",
                ItemRarity.Purple => "⭐⭐⭐ 史诗",
                ItemRarity.Gold   => "⭐⭐⭐⭐ 传说",
                _ => "???"
            };
        }

        private static string GetSlotName(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Weapon    => "🗡 武器",
                EquipSlot.Armor     => "🛡 防具",
                EquipSlot.Accessory => "💍 饰品",
                _ => "???"
            };
        }

        private static string GetSlotIcon(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Weapon    => "🗡",
                EquipSlot.Armor     => "🛡",
                EquipSlot.Accessory => "💍",
                _ => "?"
            };
        }

        private static string GetClassIcon(string className)
        {
            return className switch
            {
                "Warrior"  => "⚔",
                "Mage"     => "🔮",
                "Assassin" => "🗡",
                _ => "?"
            };
        }

        private static void SetStatText(Text text, string label, int value)
        {
            if (text == null) return;
            if (value > 0)
            {
                text.text = $"{label} +{value}";
                text.color = Color.green;
            }
            else if (value < 0)
            {
                text.text = $"{label} {value}";
                text.color = Color.red;
            }
            else
            {
                text.text = $"{label} +0";
                text.color = Color.gray;
            }
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
