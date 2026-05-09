using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    // ═══════════════════════════════════════════════════════════
    // FE-09: 背包系统UI — 对接后端 PlayerInventory
    // ═══════════════════════════════════════════════════════════
    //
    // 数据源：PlayerInventory.Instance（后端单例）
    // 类型复用：EquipmentData / EquipmentSlot / CardRarity / Hero
    //
    // 三层动画安全：所有 tween 加 .SetLink(gameObject)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 背包面板 — 子面板模式，从多个入口打开
    /// 监听 PlayerInventory.OnInventoryChanged 自动刷新
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
        public RectTransform gridContent;

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
        public Text detailPowerText;
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
        private List<EquipmentData> displayItems = new List<EquipmentData>();
        private ItemCategory currentTab = ItemCategory.All;
        private EquipmentData selectedEquip;
        private readonly Dictionary<string, RectTransform> itemCells = new Dictionary<string, RectTransform>();

        // ══════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            slideInAnimation = false;

            tabAllButton?.onClick.AddListener(() => SwitchTab(ItemCategory.All));
            tabEquipButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Equipment));
            tabMaterialButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Material));
            tabConsumableButton?.onClick.AddListener(() => SwitchTab(ItemCategory.Consumable));

            closeButton?.onClick.AddListener(OnCloseClicked);
            equipButton?.onClick.AddListener(OnEquipClicked);
            discardButton?.onClick.AddListener(OnDiscardClicked);

            confirmDiscardYesButton?.onClick.AddListener(OnConfirmDiscard);
            confirmDiscardNoButton?.onClick.AddListener(OnCancelDiscard);
        }

        public override void Show()
        {
            base.Show();
            // 监听后端背包变更事件
            var inv = PlayerInventory.Instance;
            if (inv != null)
                inv.OnInventoryChanged += OnInventoryChanged;

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
            // 取消监听
            var inv = PlayerInventory.Instance;
            if (inv != null)
                inv.OnInventoryChanged -= OnInventoryChanged;

            ClearGrid();
        }

        /// <summary>后端背包变更回调 — 自动刷新</summary>
        private void OnInventoryChanged()
        {
            LoadInventoryAndRender();
        }

        // ══════════════════════════════════════
        // 数据加载 — 对接后端 PlayerInventory
        // ══════════════════════════════════════

        private void LoadInventoryAndRender()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[InventoryPanel] PlayerInventory 未就绪");
                displayItems = new List<EquipmentData>();
                return;
            }

            // 从后端读取装备列表（按战力排序）
            displayItems = inv.GetEquipmentsSortedByPower(true);
            UpdateGoldDisplay(inv.Gold);
            SwitchTab(currentTab);
        }

        // ══════════════════════════════════════
        // Tab 切换
        // ══════════════════════════════════════

        private void SwitchTab(ItemCategory tab)
        {
            currentTab = tab;

            SetTabHighlight(tabAllHighlight, tab == ItemCategory.All);
            SetTabHighlight(tabEquipHighlight, tab == ItemCategory.Equipment);
            SetTabHighlight(tabMaterialHighlight, tab == ItemCategory.Material);
            SetTabHighlight(tabConsumableHighlight, tab == ItemCategory.Consumable);

            var filtered = GetFilteredItems();

            ClearGrid();
            RenderGrid(filtered);
            ResetDetailPanel();
            selectedEquip = null;

            // Tab 切换淡入
            if (gridContent != null)
            {
                var cg = gridContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = gridContent.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.DOFade(1f, TAB_ANIM_DURATION).SetEase(Ease.OutQuad).SetLink(gameObject);
            }
        }

        private void SetTabHighlight(Image highlight, bool active)
        {
            if (highlight == null) return;
            highlight.gameObject.SetActive(active);
            if (active) highlight.color = COLOR_RARITY_GOLD;
        }

        /// <summary>
        /// 返回当前Tab过滤后的物品列表（副本，避免枚举期间修改崩溃）
        /// </summary>
        private List<EquipmentData> GetFilteredItems()
        {
            // MVP: 只有装备类，其他Tab返回空
            if (currentTab == ItemCategory.Material || currentTab == ItemCategory.Consumable)
                return new List<EquipmentData>();

            // 返回副本，防止调用方 foreach 枚举期间修改 displayItems 导致崩溃
            return new List<EquipmentData>(displayItems);
        }

        // ══════════════════════════════════════
        // 网格渲染
        // ══════════════════════════════════════

        private void RenderGrid(List<EquipmentData> items)
        {
            if (gridContent == null) return;

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

            int rows = Mathf.CeilToInt(items.Count / GRID_COLUMNS);
            float contentHeight = rows * (GRID_CELL_SIZE + GRID_SPACING) + GRID_SPACING;
            gridContent.sizeDelta = new Vector2(gridContent.sizeDelta.x, contentHeight);

            for (int i = 0; i < items.Count; i++)
            {
                RenderItemCell(items[i], i);
            }
        }

        private void RenderItemCell(EquipmentData equip, int index)
        {
            int col = index % (int)GRID_COLUMNS;
            int row = index / (int)GRID_COLUMNS;

            float x = GRID_SPACING + col * (GRID_CELL_SIZE + GRID_SPACING) + GRID_CELL_SIZE / 2f;
            float y = gridContent.sizeDelta.y - GRID_SPACING - row * (GRID_CELL_SIZE + GRID_SPACING) - GRID_CELL_SIZE / 2f;

            string cellId = equip.name + "_" + index; // 唯一标识

            var go = new GameObject($"Item_{equip.equipmentName}");
            go.transform.SetParent(gridContent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(GRID_CELL_SIZE, GRID_CELL_SIZE);
            rect.anchoredPosition = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 背景（稀有度边框色）
            var bg = go.AddComponent<Image>();
            bg.color = GetRarityColor(equip.rarity);

            // 内部区域
            var innerGo = new GameObject("Inner");
            innerGo.transform.SetParent(go.transform, false);
            var innerRect = innerGo.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(3f, 3f);
            innerRect.offsetMax = new Vector2(-3f, -3f);
            var innerBg = innerGo.AddComponent<Image>();
            innerBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // 物品图标
            CreateChildText(innerGo, "Icon",
                new Vector2(0, 0), Vector2.one,
                new Vector2(5f, 20f), new Vector2(-5f, -10f),
                GetSlotIcon(equip.slot), 32, Color.white, TextAnchor.MiddleCenter);

            // 物品名称
            CreateChildText(innerGo, "Name",
                new Vector2(0, 0), new Vector2(1, 0.3f),
                new Vector2(2f, 0f), new Vector2(-2f, 0f),
                equip.equipmentName, 11, Color.white, TextAnchor.MiddleCenter);

            // 点击按钮
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var capturedEquip = equip;
            var capturedCellId = cellId;
            button.onClick.AddListener(() => OnItemClicked(capturedEquip, capturedCellId));

            itemCells[cellId] = rect;
        }

        private void ClearGrid()
        {
            foreach (var kvp in itemCells)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            itemCells.Clear();
        }

        // ══════════════════════════════════════
        // 物品详情
        // ══════════════════════════════════════

        private string lastClickedCellId;

        private void OnItemClicked(EquipmentData equip, string cellId)
        {
            // 取消之前的选中
            if (selectedEquip != null && !string.IsNullOrEmpty(lastClickedCellId)
                && itemCells.TryGetValue(lastClickedCellId, out var prevRect))
            {
                prevRect.DOKill();
                prevRect.localScale = Vector3.one;
            }

            selectedEquip = equip;
            lastClickedCellId = cellId;

            if (itemCells.TryGetValue(cellId, out var rect))
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

            UpdateDetailPanel(equip);
        }

        private void UpdateDetailPanel(EquipmentData equip)
        {
            if (detailPanel == null) return;
            detailPanel.gameObject.SetActive(true);

            if (detailNameText != null)
            {
                detailNameText.text = equip.equipmentName;
                detailNameText.color = GetRarityColor(equip.rarity);
            }

            if (detailRarityText != null)
                detailRarityText.text = $"稀有度: {GetRarityName(equip.rarity)}";

            if (detailSlotText != null)
                detailSlotText.text = $"槽位: {GetSlotName(equip.slot)}";

            SetStatText(detailAttackText, "ATK", equip.attackBonus);
            SetStatText(detailDefenseText, "DEF", equip.defenseBonus);
            SetStatText(detailHealthText, "HP", equip.healthBonus);
            SetStatText(detailSpeedText, "SPD", equip.speedBonus);

            if (detailCritText != null)
            {
                if (equip.critRateBonus > 0)
                {
                    detailCritText.text = $"CRIT +{equip.critRateBonus:P0}";
                    detailCritText.color = Color.green;
                }
                else
                {
                    detailCritText.text = "CRIT +0%";
                    detailCritText.color = Color.gray;
                }
            }

            if (detailEffectText != null)
            {
                detailEffectText.text = !string.IsNullOrEmpty(equip.specialEffect)
                    ? $"⚡ 特效: {equip.specialEffect}"
                    : "";
                detailEffectText.color = COLOR_RARITY_GOLD;
            }

            if (detailDescText != null)
                detailDescText.text = equip.description;

            // 战力评分
            if (detailPowerText != null)
            {
                int power = PlayerInventory.GetEquipmentPower(equip);
                detailPowerText.text = $"💪 战力: {power}";
            }

            // 售价
            if (equipButton != null)
                equipButton.gameObject.SetActive(true);
            if (discardButton != null)
                discardButton.gameObject.SetActive(true);
        }

        private void ResetDetailPanel()
        {
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════
        // 装备到英雄
        // ══════════════════════════════════════

        private void OnEquipClicked()
        {
            if (selectedEquip == null) return;
            ShowHeroSelectPopup(selectedEquip);
        }

        private void ShowHeroSelectPopup(EquipmentData equip)
        {
            if (heroSelectPopup == null || heroListContainer == null) return;

            heroSelectPopup.gameObject.SetActive(true);

            // 清除旧按钮
            foreach (Transform child in heroListContainer)
                Destroy(child.gameObject);

            // 查找场上英雄（通过 GameStateMachine 或 HeroManager）
            var heroes = FindActiveHeroes();
            if (heroes.Count == 0)
            {
                var noHeroGo = new GameObject("NoHero");
                noHeroGo.transform.SetParent(heroListContainer, false);
                var t = noHeroGo.AddComponent<Text>();
                t.text = "当前没有英雄";
                t.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
                t.fontSize = 18;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.gray;
                return;
            }

            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                var btnGo = new GameObject($"Hero_{hero.Data.heroName}");
                btnGo.transform.SetParent(heroListContainer, false);

                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(250f, 50f);
                btnRect.anchoredPosition = new Vector2(0, -i * 60f);
                btnRect.pivot = new Vector2(0.5f, 1f);

                var bg = btnGo.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

                var btn = btnGo.AddComponent<Button>();
                var capturedEquip = equip;
                var capturedHero = hero;
                btn.onClick.AddListener(() => OnHeroSelected(capturedEquip, capturedHero));

                // 英雄名 + 当前装备
                var label = btnGo.AddComponent<Text>();
                string currentEquip = "";
                if (hero.EquippedItems.TryGetValue(equip.slot, out var equipped))
                    currentEquip = $" (当前: {equipped.equipmentName})";
                label.text = $"{GetClassIcon(hero.Data.heroClass)} {hero.Data.heroName}{currentEquip}";
                label.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
                label.fontSize = 18;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
            }

            heroSelectPopup.localScale = Vector3.zero;
            heroSelectPopup.DOScale(Vector3.one, 0.25f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void HideHeroSelectPopup()
        {
            if (heroSelectPopup != null) heroSelectPopup.gameObject.SetActive(false);
        }

        private void OnHeroSelected(EquipmentData equip, Hero hero)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            // 后端处理：卸旧装新 + 背包移除 + 触发 OnInventoryChanged
            inv.EquipToHero(equip, hero);

            Debug.Log($"[Inventory] {equip.equipmentName} 装备到 {hero.Data.heroName}");

            HideHeroSelectPopup();
            // OnInventoryChanged 会自动刷新
        }

        // ══════════════════════════════════════
        // 丢弃物品
        // ══════════════════════════════════════

        private void OnDiscardClicked()
        {
            if (selectedEquip == null) return;
            ShowConfirmDiscardPopup(selectedEquip);
        }

        private void ShowConfirmDiscardPopup(EquipmentData equip)
        {
            if (confirmDiscardPopup == null) return;

            confirmDiscardPopup.gameObject.SetActive(true);
            if (confirmDiscardText != null)
                confirmDiscardText.text = $"确定丢弃「{equip.equipmentName}」吗？";

            confirmDiscardPopup.localScale = Vector3.zero;
            confirmDiscardPopup.DOScale(Vector3.one, 0.2f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void HideConfirmDiscardPopup()
        {
            if (confirmDiscardPopup != null) confirmDiscardPopup.gameObject.SetActive(false);
        }

        private void OnConfirmDiscard()
        {
            if (selectedEquip == null) return;

            var inv = PlayerInventory.Instance;
            if (inv != null)
                inv.RemoveEquipment(selectedEquip);

            Debug.Log($"[Inventory] 丢弃: {selectedEquip.equipmentName}");

            HideConfirmDiscardPopup();
            selectedEquip = null;
            ResetDetailPanel();
            // OnInventoryChanged 自动刷新
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

        private void UpdateGoldDisplay(int gold)
        {
            if (goldText != null)
                goldText.text = $"💰 {gold}";
        }

        // ══════════════════════════════════════
        // 英雄查找
        // ══════════════════════════════════════

        /// <summary>查找场上活跃英雄（通过场景中的 Hero 组件）</summary>
        private static List<Hero> FindActiveHeroes()
        {
            var heroes = new List<Hero>();
            // 方案1: 场景中直接查找
            var allHeroes = Object.FindObjectsByType<Hero>(FindObjectsSortMode.None);
            foreach (var h in allHeroes)
            {
                if (!h.IsDead && h.Data != null)
                    heroes.Add(h);
            }
            return heroes;
        }

        // ══════════════════════════════════════
        // 辅助方法
        // ══════════════════════════════════════

        private static Color GetRarityColor(CardRarity rarity) => rarity switch
        {
            CardRarity.White  => COLOR_RARITY_WHITE,
            CardRarity.Blue   => COLOR_RARITY_BLUE,
            CardRarity.Purple => COLOR_RARITY_PURPLE,
            CardRarity.Gold   => COLOR_RARITY_GOLD,
            _ => Color.gray
        };

        private static string GetRarityName(CardRarity rarity) => rarity switch
        {
            CardRarity.White  => "⭐ 普通",
            CardRarity.Blue   => "⭐⭐ 稀有",
            CardRarity.Purple => "⭐⭐⭐ 史诗",
            CardRarity.Gold   => "⭐⭐⭐⭐ 传说",
            _ => "???"
        };

        private static string GetSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon    => "🗡 武器",
            EquipmentSlot.Armor     => "🛡 防具",
            EquipmentSlot.Accessory => "💍 饰品",
            _ => "???"
        };

        private static string GetSlotIcon(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon    => "🗡",
            EquipmentSlot.Armor     => "🛡",
            EquipmentSlot.Accessory => "💍",
            _ => "?"
        };

        private static string GetClassIcon(string className) => className switch
        {
            "Warrior"  => "⚔",
            "Mage"     => "🔮",
            "Assassin" => "🗡",
            _ => "?"
        };

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

        /// <summary>创建子物体 Text</summary>
        private static void CreateChildText(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax,
            string text, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
