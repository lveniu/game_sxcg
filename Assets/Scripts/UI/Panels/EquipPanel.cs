using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 装备管理面板 — 英雄装备穿戴/卸下
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  ⚙ 装备管理                  │  标题
    /// ├──────────┬───────────────────┤
    /// │ 英雄列表 │   装备槽位         │
    /// │ ⚔战士 ★ │ ┌───────────────┐ │
    /// │ 🔮法师 ★ │ │ 武器：铁剑    │ │
    /// │ 🗡刺客★★│ │ ⚔+10 💨+2    │ │
    /// │          │ │ [点击卸下]    │ │
    /// │          │ ├───────────────┤ │
    /// │          │ │ 防具：空      │ │
    /// │          │ │ [拖入装备]    │ │
    /// │          │ ├───────────────┤ │
    /// │          │ │ 饰品：空      │ │
    /// │          │ │ [拖入装备]    │ │
    /// │          │ └───────────────┘ │
    /// │          │                   │
    /// │          │ 【套装信息】       │
    /// │          │ 铁壁套装 ████░ 2/4│
    /// │          │ ✅ 2件: 防御+20   │
    /// │          │ 🔒 4件: 防御+50   │
    /// │          │                   │
    /// │          │ 属性对比：         │
    /// │          │ ⚔ 45 → 55 (+10)  │
    /// │          │ 🛡 20 → 20       │
    /// │          │ ❤ 100 → 100      │
    /// ├──────────┴───────────────────┤
    /// │  背包装备                     │
    /// │ ┌──────┐ ┌──────┐ ┌──────┐ │
    /// │ │铁盾🔵│ │疾风戒│ │力量护│ │
    /// │ │🛡+8  │ │💨+5  │ │⚔+15 │ │
    /// │ └──────┘ └──────┘ └──────┘ │
    /// ├──────────────────────────────┤
    /// │          [关闭]              │
    /// └──────────────────────────────┘
    /// 
    /// 交互方式（MVP简化：点击选择，不用拖拽）：
    /// 1. 点背包装备 → 高亮选中
    /// 2. 点空槽位 → 装备到该槽位
    /// 3. 点已装备的槽位 → 卸下回背包
    /// </summary>
    public class EquipPanel : UIPanel
    {
        [Header("英雄列表")]
        public RectTransform heroListContainer;
        public GameObject heroItemPrefab;

        [Header("装备槽位")]
        public RectTransform slotsContainer;
        public Image weaponSlotBg;
        public Text weaponSlotText;
        public Button weaponSlotButton;
        public Image armorSlotBg;
        public Text armorSlotText;
        public Button armorSlotButton;
        public Image accessorySlotBg;
        public Text accessorySlotText;
        public Button accessorySlotButton;

        [Header("属性面板")]
        public RectTransform statsPanel;
        public Text attackText;
        public Text defenseText;
        public Text healthText;
        public Text speedText;

        [Header("背包装备")]
        public RectTransform backpackContainer;
        public GameObject backpackItemPrefab;
        public GridLayoutGroup backpackGrid;

        [Header("按钮")]
        public Button closeButton;

        #region 套装UI容器
        [Header("套装信息区域（可留空，程序化创建）")]
        public RectTransform setBonusContainer;
        #endregion

        #region 强化系统UI
        [Header("强化系统（可留空，程序化创建）")]
        public Button enhanceButton;
        public Text enhancePreviewText;      // 强化预览信息
        public RectTransform enhancePanel;   // 强化区域容器
        #endregion

        #region 装备对比UI
        [Header("装备对比（可留空，程序化创建）")]
        public RectTransform comparePanel;   // 对比区域容器
        public Text compareText;             // 对比文字
        #endregion

        // 内部缓存
        private class HeroItemUI
        {
            public RectTransform rect;
            public Text nameText;
            public Text starText;
            public Image bgImage;
            public Button button;
            public Hero hero;
        }

        private class BackpackItemUI
        {
            public RectTransform rect;
            public Text nameText;
            public Text statText;
            public Image bgImage;
            public Button button;
            public EquipmentData equipment;
            public Image setColorDot; // 套装色标点
        }

        private List<HeroItemUI> heroItems = new List<HeroItemUI>();
        private List<BackpackItemUI> backpackItems = new List<BackpackItemUI>();

        private Hero selectedHero;
        private EquipmentData selectedEquipment;
        private int selectedBackpackIndex = -1;

        // 当前查看/操作的装备（用于强化预览），可以是背包选中或已装备的
        private EquipmentData enhanceTarget;

        #region 套装UI内部缓存
        // 套装进度条/效果行的UI引用
        private class SetBonusUI
        {
            public RectTransform rect;              // 整个套装区域根节点
            public Text setNameText;                // 套装名
            public Image progressBarFill;           // 进度条填充
            public Text progressText;               // "X/Y" 文字
            public Text bonusLine2;                 // 2件效果行
            public Text bonusLine4;                 // 4件效果行
            public string setId;                    // 对应套装ID
        }
        private List<SetBonusUI> setBonusUIs = new List<SetBonusUI>();

        // 上一次套装件数快照，用于判断是否达到阈值
        private Dictionary<string, int> lastSetPieceCount = new Dictionary<string, int>();
        #endregion

        // 颜色
        private static readonly Color SELECTED_HERO_BG = new Color(0.2f, 0.4f, 0.7f, 0.8f);
        private static readonly Color UNSELECTED_HERO_BG = new Color(0.15f, 0.15f, 0.2f, 0.6f);
        private static readonly Color EMPTY_SLOT_COLOR = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        private static readonly Color FILLED_SLOT_COLOR = new Color(0.2f, 0.35f, 0.2f, 0.8f);
        private static readonly Color STAT_UP = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color STAT_DOWN = new Color(1f, 0.3f, 0.3f);
        private static readonly Color STAT_SAME = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color SELECTED_EQUIP_BG = new Color(0.6f, 0.5f, 0.1f, 0.6f);
        private static readonly Color UNSELECTED_EQUIP_BG = new Color(0.15f, 0.15f, 0.2f, 0.6f);

        private static readonly Dictionary<EquipmentSlot, string> SLOT_NAMES = new()
        {
            { EquipmentSlot.Weapon, "⚔ 武器" },
            { EquipmentSlot.Armor, "🛡 防具" },
            { EquipmentSlot.Accessory, "💍 饰品" },
        };

        #region 套装Mock数据
        // ========== 套装数据定义（Mock） ==========

        /// <summary>
        /// 套装数据定义
        /// </summary>
        public class MockSetData
        {
            public string setId;         // 套装唯一ID
            public string name;          // 套装显示名
            public int totalPieces;      // 总件数
            public int threshold2;       // 2件效果阈值
            public string bonus2;        // 2件效果描述
            public string bonus4;        // 4件效果描述
            public Color setColor;       // 套装标识颜色
        }

        /// <summary>
        /// 套装Mock数据库
        /// </summary>
        private static readonly List<MockSetData> MOCK_SET_DATABASE = new()
        {
            new MockSetData
            {
                setId = "iron_wall",
                name = "🛡 铁壁套装",
                totalPieces = 4,
                threshold2 = 2,
                bonus2 = "防御+20",
                bonus4 = "防御+50, 受击反伤10%",
                setColor = new Color(0.3f, 0.55f, 1f) // 蓝色
            },
            new MockSetData
            {
                setId = "berserker",
                name = "🔥 狂战套装",
                totalPieces = 4,
                threshold2 = 2,
                bonus2 = "攻击+25",
                bonus4 = "攻击+60, 暴击率+15%",
                setColor = new Color(1f, 0.3f, 0.2f) // 红色
            }
        };

        /// <summary>
        /// 装备的套装归属映射
        /// </summary>
        public static class MockEquipmentSetLink
        {
            // equipmentName -> setId
            public static readonly Dictionary<string, string> Links = new()
            {
                { "铁盾", "iron_wall" },
                { "铁壁铠甲", "iron_wall" },
                { "铁壁护腕", "iron_wall" },
                { "铁壁战靴", "iron_wall" },
                { "烈焰之剑", "berserker" },
                { "狂战斧", "berserker" },
                { "狂战护甲", "berserker" },
                { "狂战戒指", "berserker" },
            };
        }

        /// <summary>
        /// 根据装备名获取所属套装ID，不属于任何套装返回null
        /// </summary>
        private static string GetSetIdForEquipment(string equipmentName)
        {
            if (MockEquipmentSetLink.Links.TryGetValue(equipmentName, out var setId))
                return setId;
            return null;
        }

        /// <summary>
        /// 根据套装ID获取套装数据
        /// </summary>
        private static MockSetData GetSetDataById(string setId)
        {
            foreach (var sd in MOCK_SET_DATABASE)
                if (sd.setId == setId) return sd;
            return null;
        }
        #endregion

        protected override void Awake()
        {
            base.Awake();
            panelId = "Equip";
        }

        protected override void OnShow()
        {
            closeButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.AddListener(OnCloseClicked);

            // 绑定槽位按钮
            weaponSlotButton?.onClick.RemoveAllListeners();
            weaponSlotButton?.onClick.AddListener(() => OnSlotClicked(EquipmentSlot.Weapon));
            armorSlotButton?.onClick.RemoveAllListeners();
            armorSlotButton?.onClick.AddListener(() => OnSlotClicked(EquipmentSlot.Armor));
            accessorySlotButton?.onClick.RemoveAllListeners();
            accessorySlotButton?.onClick.AddListener(() => OnSlotClicked(EquipmentSlot.Accessory));

            // 绑定强化按钮
            enhanceButton?.onClick.RemoveAllListeners();
            enhanceButton?.onClick.AddListener(OnEnhanceClicked);

            // 初始化
            RefreshHeroList();
            SelectHeroByDefault();
            RefreshBackpack();

            selectedEquipment = null;
            selectedBackpackIndex = -1;
            enhanceTarget = null;

            // 确保强化区域容器存在
            EnsureEnhancePanel();
            // 确保对比区域容器存在
            EnsureComparePanel();

            // 初始化强化预览为空
            UpdateEnhancePreview(null);
        }

        protected override void OnHide()
        {
            closeButton?.onClick.RemoveAllListeners();
            weaponSlotButton?.onClick.RemoveAllListeners();
            armorSlotButton?.onClick.RemoveAllListeners();
            accessorySlotButton?.onClick.RemoveAllListeners();
            enhanceButton?.onClick.RemoveAllListeners();
            ClearHeroItems();
            ClearBackpackItems();
            ClearSetBonusUIs();
        }

        // ========== 英雄列表 ==========

        private void RefreshHeroList()
        {
            ClearHeroItems();

            var deck = CardDeck.Instance;
            if (deck == null || deck.fieldHeroes == null) return;

            var heroes = deck.fieldHeroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] == null) continue;
                var item = CreateHeroItem(heroes[i], i);
                heroItems.Add(item);
            }

            // 入场动画
            for (int i = 0; i < heroItems.Count; i++)
            {
                if (heroItems[i].rect == null) continue;
                heroItems[i].rect.anchoredPosition = new Vector2(-200f, heroItems[i].rect.anchoredPosition.y);
                heroItems[i].rect.DOAnchorPosX(0f, 0.3f).SetDelay(i * 0.08f).SetEase(Ease.OutCubic);
            }
        }

        private HeroItemUI CreateHeroItem(Hero hero, int index)
        {
            var item = new HeroItemUI { hero = hero };

            if (heroItemPrefab != null)
            {
                var go = Instantiate(heroItemPrefab, heroListContainer);
                item.rect = go.GetComponent<RectTransform>();
                item.nameText = go.transform.Find("NameText")?.GetComponent<Text>();
                item.starText = go.transform.Find("StarText")?.GetComponent<Text>();
                item.bgImage = go.GetComponent<Image>();
                item.button = go.GetComponent<Button>();
            }
            else
            {
                // 程序化创建
                var go = new GameObject($"Hero_{index}");
                go.transform.SetParent(heroListContainer, false);
                item.rect = go.AddComponent<RectTransform>();
                item.rect.sizeDelta = new Vector2(160, 60);

                item.bgImage = go.AddComponent<Image>();
                item.bgImage.color = UNSELECTED_HERO_BG;

                item.button = go.AddComponent<Button>();

                // 名称
                var nameGo = new GameObject("NameText");
                nameGo.transform.SetParent(go.transform, false);
                var nameRect = nameGo.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0.05f, 0.35f);
                nameRect.anchorMax = new Vector2(0.65f, 0.85f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                item.nameText = nameGo.AddComponent<Text>();
                item.nameText.fontSize = 14;
                item.nameText.alignment = TextAnchor.MiddleLeft;
                item.nameText.color = Color.white;

                // 星级
                var starGo = new GameObject("StarText");
                starGo.transform.SetParent(go.transform, false);
                var starRect = starGo.AddComponent<RectTransform>();
                starRect.anchorMin = new Vector2(0.65f, 0.35f);
                starRect.anchorMax = new Vector2(0.95f, 0.85f);
                starRect.offsetMin = starRect.offsetMax = Vector2.zero;
                item.starText = starGo.AddComponent<Text>();
                item.starText.fontSize = 12;
                item.starText.alignment = TextAnchor.MiddleRight;
                item.starText.color = new Color(1f, 0.85f, 0.2f);
            }

            // 填充数据
            string icon = GetClassIcon(hero.Data.heroClass);
            if (item.nameText != null)
                item.nameText.text = $"{icon} {hero.Data.heroName}";

            if (item.starText != null)
            {
                string stars = "";
                for (int s = 0; s < hero.StarLevel; s++) stars += "★";
                item.starText.text = stars;
            }

            // 点击选中
            if (item.button != null)
            {
                int capturedIndex = index;
                item.button.onClick.AddListener(() => OnHeroSelected(capturedIndex));
            }

            return item;
        }

        private void OnHeroSelected(int index)
        {
            if (index < 0 || index >= heroItems.Count) return;

            // 取消旧选中
            foreach (var h in heroItems)
            {
                if (h.bgImage != null) h.bgImage.color = UNSELECTED_HERO_BG;
            }

            // 高亮新选中
            selectedHero = heroItems[index].hero;
            if (heroItems[index].bgImage != null)
                heroItems[index].bgImage.color = SELECTED_HERO_BG;

            // 刷新装备槽和属性
            RefreshSlots();
            RefreshStats();

            // 刷新套装信息 — 对接 SetBonusSystem
            ShowSetBonusInfo(selectedHero);

            // 重置背包选中
            selectedEquipment = null;
            selectedBackpackIndex = -1;
            enhanceTarget = null;
            RefreshBackpackSelection();

            // 重置强化预览和装备对比
            UpdateEnhancePreview(null);
            UpdateCompareDisplay(null);
        }

        private void SelectHeroByDefault()
        {
            if (heroItems.Count > 0)
                OnHeroSelected(0);
        }

        // ========== 装备槽位 ==========

        private void RefreshSlots()
        {
            RefreshSlot(EquipmentSlot.Weapon, weaponSlotBg, weaponSlotText);
            RefreshSlot(EquipmentSlot.Armor, armorSlotBg, armorSlotText);
            RefreshSlot(EquipmentSlot.Accessory, accessorySlotBg, accessorySlotText);
        }

        private void RefreshSlot(EquipmentSlot slot, Image bg, Text text)
        {
            string slotName = SLOT_NAMES.TryGetValue(slot, out var n) ? n : slot.ToString();
            var equipped = GetEquippedItem(slot);

            if (text != null)
            {
                if (equipped != null)
                {
                    string stats = GetEquipStats(equipped);
                    text.text = $"{slotName}\n{equipped.equipmentName}\n{stats}";
                    text.color = Color.white;
                }
                else
                {
                    text.text = $"{slotName}\n空\n点击选择后穿戴";
                    text.color = new Color(0.5f, 0.5f, 0.5f);
                }
            }

            if (bg != null)
                bg.color = equipped != null ? FILLED_SLOT_COLOR : EMPTY_SLOT_COLOR;
        }

        private void OnSlotClicked(EquipmentSlot slot)
        {
            if (selectedHero == null) return;

            var equipped = GetEquippedItem(slot);

            if (equipped != null)
            {
                // 设置强化目标为当前已装备的装备（用于查看强化信息）
                enhanceTarget = equipped;
                UpdateEnhancePreview(equipped);

                // 记录卸下前的套装件数
                var beforeCounts = GetCurrentSetPieceCounts();

                // 卸下装备 — 对接 OnUnequipFromHero
                OnUnequipFromHero(selectedHero, slot);

                // 刷新
                RefreshSlots();
                RefreshStats();
                RefreshBackpack();

                // 刷新套装信息（含卸下检测动画）
                RefreshSetBonusesWithDeactivateCheck(beforeCounts);
                // 同时更新 SetBonusSystem 的显示
                ShowSetBonusInfo(selectedHero);

                // 重置强化目标
                enhanceTarget = null;
                UpdateEnhancePreview(null);
            }
            else if (selectedEquipment != null && selectedEquipment.slot == slot)
            {
                // 记录穿戴前的套装件数
                var beforeCounts = GetCurrentSetPieceCounts();

                // 获取当前槽位装备（用于对比），如果为空则为null
                EquipmentData currentEquip = GetEquippedItem(slot);

                // 穿戴选中的装备 — 对接 OnEquipToHero
                OnEquipToHero(selectedHero, selectedEquipment);

                // 穿戴动画
                PlayEquipAnimation(slot);

                selectedEquipment = null;
                selectedBackpackIndex = -1;
                enhanceTarget = null;
                RefreshSlots();
                RefreshStats();
                RefreshBackpack();

                // 刷新套装信息（含激活检测动画）
                RefreshSetBonusesWithActivateCheck(beforeCounts);
                // 同时更新 SetBonusSystem 的显示
                ShowSetBonusInfo(selectedHero);

                // 重置对比
                UpdateCompareDisplay(null);
            }
            else if (selectedEquipment != null && selectedEquipment.slot != slot)
            {
                // 槽位不匹配提示
                Debug.Log($"[Equip] 该装备需要{selectedEquipment.slot}槽位，当前点击的是{slot}");
            }
            else
            {
                // 空槽位且无选中装备 — 如果有已装备项则设为强化目标
                var slotEquip = GetEquippedItem(slot);
                if (slotEquip != null)
                {
                    enhanceTarget = slotEquip;
                    UpdateEnhancePreview(slotEquip);
                }
            }
        }

        // ========== 属性面板 ==========

        private void RefreshStats()
        {
            if (selectedHero == null) return;

            if (attackText != null)
            {
                int baseAtk = selectedHero.Attack;
                int bonus = GetTotalEquipBonus(e => e.attackBonus);
                attackText.text = FormatStatLine("⚔ 攻击", baseAtk, bonus);
                attackText.color = bonus > 0 ? STAT_UP : bonus < 0 ? STAT_DOWN : STAT_SAME;
            }

            if (defenseText != null)
            {
                int baseDef = selectedHero.Defense;
                int bonus = GetTotalEquipBonus(e => e.defenseBonus);
                defenseText.text = FormatStatLine("🛡 防御", baseDef, bonus);
                defenseText.color = bonus > 0 ? STAT_UP : bonus < 0 ? STAT_DOWN : STAT_SAME;
            }

            if (healthText != null)
            {
                int baseHp = selectedHero.MaxHealth;
                int bonus = GetTotalEquipBonus(e => e.healthBonus);
                healthText.text = FormatStatLine("❤ 生命", baseHp, bonus);
                healthText.color = bonus > 0 ? STAT_UP : bonus < 0 ? STAT_DOWN : STAT_SAME;
            }

            if (speedText != null)
            {
                int baseSpd = selectedHero.Speed;
                int bonus = GetTotalEquipBonus(e => e.speedBonus);
                speedText.text = FormatStatLine("💨 速度", baseSpd, bonus);
                speedText.color = bonus > 0 ? STAT_UP : bonus < 0 ? STAT_DOWN : STAT_SAME;
            }
        }

        private int GetTotalEquipBonus(System.Func<EquipmentData, int> selector)
        {
            int total = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var eq = GetEquippedItem(slot);
                if (eq != null) total += selector(eq);
            }
            return total;
        }

        private static string FormatStatLine(string label, int baseVal, int bonus)
        {
            if (bonus > 0)
                return $"{label}: {baseVal} ({baseVal - bonus}+{bonus})";
            if (bonus < 0)
                return $"{label}: {baseVal} ({baseVal - bonus}{bonus})";
            return $"{label}: {baseVal}";
        }

        // ========== 背包装备 ==========

        private void RefreshBackpack()
        {
            ClearBackpackItems();

            var inventory = PlayerInventory.Instance;
            if (inventory == null || inventory.Equipments == null) return;

            for (int i = 0; i < inventory.Equipments.Count; i++)
            {
                var eq = inventory.Equipments[i];
                if (eq == null) continue;
                var item = CreateBackpackItem(eq, i);
                backpackItems.Add(item);
            }
        }

        private BackpackItemUI CreateBackpackItem(EquipmentData equipment, int index)
        {
            var item = new BackpackItemUI { equipment = equipment };

            if (backpackItemPrefab != null)
            {
                var go = Instantiate(backpackItemPrefab, backpackContainer);
                item.rect = go.GetComponent<RectTransform>();
                item.nameText = go.transform.Find("NameText")?.GetComponent<Text>();
                item.statText = go.transform.Find("StatText")?.GetComponent<Text>();
                item.bgImage = go.GetComponent<Image>();
                item.button = go.GetComponent<Button>();
            }
            else
            {
                var go = new GameObject($"Backpack_{index}");
                go.transform.SetParent(backpackContainer, false);
                item.rect = go.AddComponent<RectTransform>();
                item.rect.sizeDelta = new Vector2(140, 100);

                item.bgImage = go.AddComponent<Image>();
                item.bgImage.color = UNSELECTED_EQUIP_BG;

                var outline = go.AddComponent<Outline>();
                outline.effectColor = GetRarityColor(equipment.rarity);
                outline.effectDistance = new Vector2(1, -1);

                item.button = go.AddComponent<Button>();

                // 名称
                var nameGo = new GameObject("NameText");
                nameGo.transform.SetParent(go.transform, false);
                var nameRect = nameGo.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0.05f, 0.55f);
                nameRect.anchorMax = new Vector2(0.95f, 0.9f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                item.nameText = nameGo.AddComponent<Text>();
                item.nameText.fontSize = 13;
                item.nameText.alignment = TextAnchor.MiddleCenter;
                item.nameText.color = GetRarityColor(equipment.rarity);

                // 属性
                var statGo = new GameObject("StatText");
                statGo.transform.SetParent(go.transform, false);
                var statRect = statGo.AddComponent<RectTransform>();
                statRect.anchorMin = new Vector2(0.05f, 0.1f);
                statRect.anchorMax = new Vector2(0.95f, 0.5f);
                statRect.offsetMin = statRect.offsetMax = Vector2.zero;
                item.statText = statGo.AddComponent<Text>();
                item.statText.fontSize = 11;
                item.statText.alignment = TextAnchor.MiddleCenter;
                item.statText.color = new Color(0.8f, 0.8f, 0.8f);

                // 【需求4】背包装备列表标识 — 套装色标点
                string setId = GetSetIdForEquipment(equipment.equipmentName);
                if (setId != null)
                {
                    MockSetData setData = GetSetDataById(setId);
                    if (setData != null)
                    {
                        var dotGo = new GameObject("SetDot");
                        dotGo.transform.SetParent(go.transform, false);
                        var dotRect = dotGo.AddComponent<RectTransform>();
                        dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 1f);
                        dotRect.sizeDelta = new Vector2(10, 10);
                        dotRect.pivot = new Vector2(1f, 1f);
                        dotRect.anchoredPosition = new Vector2(-4f, -4f);
                        var dotImage = dotGo.AddComponent<Image>();
                        dotImage.color = setData.setColor;
                        dotImage.raycastTarget = false;
                        item.setColorDot = dotImage;
                    }
                }
            }

            // 数据
            if (item.nameText != null)
            {
                string slotIcon = SLOT_NAMES.TryGetValue(equipment.slot, out var icon) ? icon : "";
                item.nameText.text = $"{slotIcon}\n{equipment.equipmentName}";
            }

            if (item.statText != null)
                item.statText.text = GetEquipStats(equipment);

            // 点击选择
            if (item.button != null)
            {
                int capturedIndex = index;
                item.button.onClick.AddListener(() => OnBackpackItemClicked(capturedIndex));
            }

            return item;
        }

        private void OnBackpackItemClicked(int index)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || index >= inventory.Equipments.Count) return;

            // 取消旧选中
            selectedBackpackIndex = -1;
            selectedEquipment = null;
            RefreshBackpackSelection();

            // 高亮新选中
            selectedBackpackIndex = index;
            selectedEquipment = inventory.Equipments[index];

            if (index < backpackItems.Count && backpackItems[index].bgImage != null)
            {
                backpackItems[index].bgImage.color = SELECTED_EQUIP_BG;
                backpackItems[index].rect?.DOScale(Vector3.one * 1.05f, 0.2f).SetEase(Ease.OutBack);
            }

            // 自动高亮对应槽位
            HighlightMatchingSlot(selectedEquipment.slot);

            // 设置强化目标为选中的背包装备
            enhanceTarget = selectedEquipment;
            UpdateEnhancePreview(selectedEquipment);

            // 如果有选中英雄，显示装备对比
            if (selectedHero != null)
            {
                EquipmentData currentEquipped = GetEquippedItem(selectedEquipment.slot);
                UpdateCompareDisplay(CompareEquipments(currentEquipped, selectedEquipment));
            }
        }

        private void RefreshBackpackSelection()
        {
            foreach (var item in backpackItems)
            {
                if (item.bgImage != null)
                    item.bgImage.color = UNSELECTED_EQUIP_BG;
                if (item.rect != null)
                    item.rect.localScale = Vector3.one;
            }
        }

        private void HighlightMatchingSlot(EquipmentSlot slot)
        {
            // 重置所有槽位颜色
            if (weaponSlotBg != null && slot != EquipmentSlot.Weapon)
                weaponSlotBg.color = GetEquippedItem(EquipmentSlot.Weapon) != null ? FILLED_SLOT_COLOR : EMPTY_SLOT_COLOR;
            if (armorSlotBg != null && slot != EquipmentSlot.Armor)
                armorSlotBg.color = GetEquippedItem(EquipmentSlot.Armor) != null ? FILLED_SLOT_COLOR : EMPTY_SLOT_COLOR;
            if (accessorySlotBg != null && slot != EquipmentSlot.Accessory)
                accessorySlotBg.color = GetEquippedItem(EquipmentSlot.Accessory) != null ? FILLED_SLOT_COLOR : EMPTY_SLOT_COLOR;

            // 高亮匹配槽位
            var targetBg = slot switch
            {
                EquipmentSlot.Weapon => weaponSlotBg,
                EquipmentSlot.Armor => armorSlotBg,
                EquipmentSlot.Accessory => accessorySlotBg,
                _ => null
            };

            if (targetBg != null)
            {
                targetBg.color = GetEquippedItem(slot) != null
                    ? new Color(0.5f, 0.4f, 0.1f, 0.8f) // 已有装备：换装色
                    : new Color(0.2f, 0.5f, 0.3f, 0.8f); // 空槽位：可穿戴色

                // 脉冲动画
                targetBg.rectTransform.localScale = Vector3.one;
                targetBg.rectTransform.DOScale(Vector3.one * 1.05f, 0.3f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        // ========== 关闭 ==========

        private void OnCloseClicked()
        {
            Hide();
        }

        // ========== 清理 ==========

        private void ClearHeroItems()
        {
            foreach (var item in heroItems)
                if (item.rect != null) Destroy(item.rect.gameObject);
            heroItems.Clear();
        }

        private void ClearBackpackItems()
        {
            foreach (var item in backpackItems)
                if (item.rect != null) Destroy(item.rect.gameObject);
            backpackItems.Clear();
        }

        #region 套装UI清理
        /// <summary>
        /// 清理套装信息区域的所有UI元素
        /// </summary>
        private void ClearSetBonusUIs()
        {
            foreach (var sui in setBonusUIs)
                if (sui.rect != null) Destroy(sui.rect.gameObject);
            setBonusUIs.Clear();
            lastSetPieceCount.Clear();
        }
        #endregion

        // ========== 装备查询（MVP直接查，等后端补Hero.Equip/Unequip后对接）==========

        private EquipmentData GetEquippedItem(EquipmentSlot slot)
        {
            if (selectedHero == null) return null;
            return selectedHero.GetEquippedItem(slot);
        }

        #region 套装件数统计
        /// <summary>
        /// 获取当前英雄所有已装备装备的套装件数统计
        /// 返回 Dictionary<setId, pieceCount>
        /// 使用 EquipmentData.setId 真实套装归属（不再依赖Mock映射）
        /// </summary>
        private Dictionary<string, int> GetCurrentSetPieceCounts()
        {
            var counts = new Dictionary<string, int>();
            if (selectedHero == null) return counts;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var eq = GetEquippedItem(slot);
                if (eq == null || !eq.BelongsToSet) continue;
                if (!counts.ContainsKey(eq.setId))
                    counts[eq.setId] = 0;
                counts[eq.setId]++;
            }
            return counts;
        }
        #endregion

        #region 套装进度条与效果展示
        /// <summary>
        /// 刷新套装信息区域（无动画版本，用于初始加载和英雄切换）
        /// </summary>
        private void RefreshSetBonuses()
        {
            if (selectedHero == null) return;

            // 清理旧UI
            ClearSetBonusUIs();

            // 确保容器存在
            EnsureSetBonusContainer();

            // 获取当前套装件数
            var counts = GetCurrentSetPieceCounts();

            // 记录快照
            lastSetPieceCount = new Dictionary<string, int>(counts);

            // 尝试使用 SetBonusSystem 的真实数据
            var setBonusSystem = SetBonusSystem.Instance;
            if (setBonusSystem != null)
            {
                // 使用真实套装系统数据
                var allSetIds = setBonusSystem.GetAllSetIds();
                foreach (var setId in allSetIds)
                {
                    int pieceCount = counts.TryGetValue(setId, out var c) ? c : 0;
                    if (pieceCount > 0 || HasSetEquipmentInBackpack(setId))
                    {
                        var defs = setBonusSystem.GetSetDefinitions(setId);
                        if (defs.Count > 0)
                        {
                            var ui = CreateSetBonusUIFromSystem(setId, defs, pieceCount);
                            setBonusUIs.Add(ui);
                        }
                    }
                }
            }
            else
            {
                // 降级：使用Mock数据
                foreach (var setData in MOCK_SET_DATABASE)
                {
                    int pieceCount = counts.TryGetValue(setData.setId, out var c) ? c : 0;
                    if (pieceCount > 0 || HasSetEquipmentInBackpack(setData.setId))
                    {
                        var ui = CreateSetBonusUI(setData, pieceCount);
                        setBonusUIs.Add(ui);
                    }
                }
            }
        }

        /// <summary>
        /// 刷新套装信息（穿戴后调用，检测是否有新激活的套装效果）
        /// </summary>
        private void RefreshSetBonusesWithActivateCheck(Dictionary<string, int> beforeCounts)
        {
            if (selectedHero == null) return;

            // 清理旧UI
            ClearSetBonusUIs();
            EnsureSetBonusContainer();

            // 获取当前件数
            var afterCounts = GetCurrentSetPieceCounts();
            lastSetPieceCount = new Dictionary<string, int>(afterCounts);

            var setBonusSystem = SetBonusSystem.Instance;

            // 创建UI并检测激活
            void ProcessSet(string setId, int before, int after)
            {
                if (setBonusSystem != null)
                {
                    var defs = setBonusSystem.GetSetDefinitions(setId);
                    if (defs.Count > 0)
                    {
                        var ui = CreateSetBonusUIFromSystem(setId, defs, after);
                        setBonusUIs.Add(ui);

                        // 检测是否跨过任意阈值 → 播放激活动画
                        defs.Sort((a, b) => a.requiredCount.CompareTo(b.requiredCount));
                        foreach (var def in defs)
                        {
                            if (after >= def.requiredCount && before < def.requiredCount)
                            {
                                PlaySetActivateAnimationFromSystem(ui, def);
                            }
                        }
                    }
                }
                else
                {
                    // Mock降级
                    var mockData = GetSetDataById(setId);
                    if (mockData != null)
                    {
                        var ui = CreateSetBonusUI(mockData, after);
                        setBonusUIs.Add(ui);
                        if (after >= mockData.threshold2 && before < mockData.threshold2)
                            PlaySetActivateAnimation(ui, mockData, 2);
                        if (after >= mockData.totalPieces && before < mockData.totalPieces)
                            PlaySetActivateAnimation(ui, mockData, 4);
                    }
                }
            }

            if (setBonusSystem != null)
            {
                var allSetIds = setBonusSystem.GetAllSetIds();
                foreach (var setId in allSetIds)
                {
                    int before = beforeCounts.TryGetValue(setId, out var b) ? b : 0;
                    int after = afterCounts.TryGetValue(setId, out var a) ? a : 0;
                    if (after > 0 || HasSetEquipmentInBackpack(setId))
                        ProcessSet(setId, before, after);
                }
            }
            else
            {
                foreach (var setData in MOCK_SET_DATABASE)
                {
                    int before = beforeCounts.TryGetValue(setData.setId, out var b) ? b : 0;
                    int after = afterCounts.TryGetValue(setData.setId, out var a) ? a : 0;
                    if (after > 0 || HasSetEquipmentInBackpack(setData.setId))
                        ProcessSet(setData.setId, before, after);
                }
            }
        }

        /// <summary>
        /// 刷新套装信息（卸下后调用，检测是否有效果被取消）
        /// </summary>
        private void RefreshSetBonusesWithDeactivateCheck(Dictionary<string, int> beforeCounts)
        {
            if (selectedHero == null) return;

            // 清理旧UI
            ClearSetBonusUIs();
            EnsureSetBonusContainer();

            // 获取当前件数
            var afterCounts = GetCurrentSetPieceCounts();
            lastSetPieceCount = new Dictionary<string, int>(afterCounts);

            var setBonusSystem = SetBonusSystem.Instance;

            // 创建UI并检测取消
            void ProcessSetDeactivate(string setId, int before, int after)
            {
                if (setBonusSystem != null)
                {
                    var defs = setBonusSystem.GetSetDefinitions(setId);
                    if (defs.Count > 0)
                    {
                        var ui = CreateSetBonusUIFromSystem(setId, defs, after);
                        setBonusUIs.Add(ui);

                        // 检测是否掉过任意阈值 → 播放取消动画
                        defs.Sort((a, b) => a.requiredCount.CompareTo(b.requiredCount));
                        foreach (var def in defs)
                        {
                            if (before >= def.requiredCount && after < def.requiredCount && after > 0)
                            {
                                PlaySetDeactivateAnimationFromSystem(ui, def);
                            }
                        }
                    }
                }
                else
                {
                    // Mock降级
                    var mockData = GetSetDataById(setId);
                    if (mockData != null)
                    {
                        var ui = CreateSetBonusUI(mockData, after);
                        setBonusUIs.Add(ui);
                        if (before >= mockData.threshold2 && after < mockData.threshold2 && after > 0)
                            PlaySetDeactivateAnimation(ui, mockData);
                    }
                }
            }

            if (setBonusSystem != null)
            {
                var allSetIds = setBonusSystem.GetAllSetIds();
                foreach (var setId in allSetIds)
                {
                    int before = beforeCounts.TryGetValue(setId, out var b) ? b : 0;
                    int after = afterCounts.TryGetValue(setId, out var a) ? a : 0;
                    if (after > 0 || before > 0 || HasSetEquipmentInBackpack(setId))
                        ProcessSetDeactivate(setId, before, after);
                }
            }
            else
            {
                foreach (var setData in MOCK_SET_DATABASE)
                {
                    int before = beforeCounts.TryGetValue(setData.setId, out var b) ? b : 0;
                    int after = afterCounts.TryGetValue(setData.setId, out var a) ? a : 0;
                    if (after > 0 || before > 0 || HasSetEquipmentInBackpack(setData.setId))
                        ProcessSetDeactivate(setData.setId, before, after);
                }
            }
        }

        /// <summary>
        /// 检查背包装备中是否有属于指定套装的装备
        /// 使用 EquipmentData.setId 真实套装归属
        /// </summary>
        private bool HasSetEquipmentInBackpack(string setId)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || inventory.Equipments == null) return false;
            foreach (var eq in inventory.Equipments)
            {
                if (eq != null && eq.BelongsToSet && eq.setId == setId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 确保套装信息容器存在（如果Inspector中未指定，则程序化创建）
        /// </summary>
        private void EnsureSetBonusContainer()
        {
            if (setBonusContainer != null) return;

            // 在slotsContainer下方创建套装信息容器
            // 查找statsPanel作为参考位置，如果不存在则在slotsContainer后面
            Transform parent = statsPanel != null ? statsPanel.parent : (slotsContainer != null ? slotsContainer.parent : transform);
            GameObject containerGo = new GameObject("SetBonusContainer");
            containerGo.transform.SetParent(parent, false);
            setBonusContainer = containerGo.AddComponent<RectTransform>();

            // 定位在槽位下方、属性面板上方
            var rect = setBonusContainer;
            rect.anchorMin = new Vector2(0.22f, 0.28f);
            rect.anchorMax = new Vector2(0.98f, 0.48f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // 添加垂直布局
            var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 背景图
            var bg = containerGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            bg.raycastTarget = false;
        }

        /// <summary>
        /// 为一个套装创建完整的UI：套装名 + 进度条 + 效果行
        /// </summary>
        private SetBonusUI CreateSetBonusUI(MockSetData setData, int pieceCount)
        {
            var ui = new SetBonusUI { setId = setData.setId };

            // 根节点
            var rootGo = new GameObject($"SetBonus_{setData.setId}");
            rootGo.transform.SetParent(setBonusContainer, false);
            ui.rect = rootGo.AddComponent<RectTransform>();

            // 背景图（用于动画脉冲）
            var rootBg = rootGo.AddComponent<Image>();
            rootBg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            rootBg.raycastTarget = false;

            // 垂直布局
            var vlg = rootGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // ---- 第一行：套装名 + 进度条 + "X/Y" ----
            var row1Go = new GameObject("Row1_Progress");
            row1Go.transform.SetParent(rootGo.transform, false);
            var row1Rect = row1Go.AddComponent<RectTransform>();
            row1Rect.sizeDelta = new Vector2(0, 28);

            var row1Layout = row1Go.AddComponent<HorizontalLayoutGroup>();
            row1Layout.spacing = 6f;
            row1Layout.padding = new RectOffset(0, 0, 2, 2);
            row1Layout.childAlignment = TextAnchor.MiddleLeft;
            row1Layout.childControlWidth = true;
            row1Layout.childControlHeight = true;
            row1Layout.childForceExpandWidth = true;
            row1Layout.childForceExpandHeight = false;

            // 套装名
            var nameGo = new GameObject("SetName");
            nameGo.transform.SetParent(row1Go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            ui.setNameText = nameGo.AddComponent<Text>();
            ui.setNameText.text = setData.name;
            ui.setNameText.fontSize = 13;
            ui.setNameText.fontStyle = FontStyle.Bold;
            ui.setNameText.color = setData.setColor;
            ui.setNameText.alignment = TextAnchor.MiddleLeft;
            // 设置preferredWidth
            var nameLayoutEl = nameGo.AddComponent<LayoutElement>();
            nameLayoutEl.minWidth = 100;
            nameLayoutEl.flexibleWidth = 0;

            // 进度条背景
            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(row1Go.transform, false);
            var barBgRect = barBgGo.AddComponent<RectTransform>();
            var barBgImage = barBgGo.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            barBgImage.raycastTarget = false;
            var barBgLayoutEl = barBgGo.AddComponent<LayoutElement>();
            barBgLayoutEl.flexibleWidth = 1;
            barBgLayoutEl.minHeight = 16;

            // 进度条填充（使用Image.Fill方式 — 通过调整anchorMax模拟）
            var barFillGo = new GameObject("BarFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRect = barFillGo.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(pieceCount / (float)setData.totalPieces, 1f);
            barFillRect.offsetMin = barFillRect.offsetMax = Vector2.zero;
            ui.progressBarFill = barFillGo.AddComponent<Image>();
            // 进度条颜色：达到2件阈值后变金色，否则用套装色
            ui.progressBarFill.color = pieceCount >= setData.threshold2
                ? new Color(1f, 0.85f, 0.2f)
                : setData.setColor;
            ui.progressBarFill.raycastTarget = false;

            // "X/Y" 文字
            var progTextGo = new GameObject("ProgressText");
            progTextGo.transform.SetParent(row1Go.transform, false);
            var progTextRect = progTextGo.AddComponent<RectTransform>();
            ui.progressText = progTextGo.AddComponent<Text>();
            ui.progressText.text = $"{pieceCount}/{setData.totalPieces}";
            ui.progressText.fontSize = 12;
            ui.progressText.fontStyle = FontStyle.Bold;
            ui.progressText.color = pieceCount >= setData.threshold2
                ? new Color(1f, 0.85f, 0.2f)
                : new Color(0.7f, 0.7f, 0.7f);
            ui.progressText.alignment = TextAnchor.MiddleCenter;
            var progLayoutEl = progTextGo.AddComponent<LayoutElement>();
            progLayoutEl.minWidth = 36;
            progLayoutEl.flexibleWidth = 0;

            // ---- 第二行：2件效果 ----
            bool bonus2Active = pieceCount >= setData.threshold2;
            ui.bonusLine2 = CreateBonusLine(rootGo.transform, "BonusLine2",
                $"✅ {setData.threshold2}件: {setData.bonus2}",
                bonus2Active);

            // ---- 第三行：4件效果 ----
            bool bonus4Active = pieceCount >= setData.totalPieces;
            ui.bonusLine4 = CreateBonusLine(rootGo.transform, "BonusLine4",
                $"🔒 4件: {setData.bonus4}",
                !bonus4Active); // 未激活时显示灰色+锁

            // 如果4件已激活，覆盖文字
            if (bonus4Active)
            {
                ui.bonusLine4.text = $"✅ 4件: {setData.bonus4}";
            }

            return ui;
        }

        /// <summary>
        /// 创建一个套装效果行
        /// 已激活：金色文字 + ✅ + 绿色背景
        /// 未激活：灰色文字 + 🔒
        /// </summary>
        private Text CreateBonusLine(Transform parent, string objName, string text, bool isActive)
        {
            var lineGo = new GameObject(objName);
            lineGo.transform.SetParent(parent, false);
            var lineRect = lineGo.AddComponent<RectTransform>();
            lineRect.sizeDelta = new Vector2(0, 22);

            // 背景色
            var lineBg = lineGo.AddComponent<Image>();
            lineBg.color = isActive
                ? new Color(0.15f, 0.35f, 0.15f, 0.6f)   // 绿色背景（已激活）
                : new Color(0.1f, 0.1f, 0.12f, 0.4f);     // 暗色背景（未激活）
            lineBg.raycastTarget = false;

            // 文字
            var lineText = lineGo.AddComponent<Text>();
            lineText.text = text;
            lineText.fontSize = 11;
            lineText.alignment = TextAnchor.MiddleLeft;
            lineText.color = isActive
                ? new Color(1f, 0.85f, 0.2f)    // 金色（已激活）
                : new Color(0.5f, 0.5f, 0.5f);  // 灰色（未激活）

            return lineText;
        }
        #endregion

        #region 套装激活动画
        /// <summary>
        /// 套装效果激活动画：金色脉冲 + 闪光 + 飘字
        /// 使用DOTween：scale 1→1.1→1 + 颜色脉冲 + 飘字向上消失
        /// </summary>
        private void PlaySetActivateAnimation(SetBonusUI ui, MockSetData setData, int threshold)
        {
            if (ui.rect == null) return;

            // 1. Scale脉冲：1 → 1.1 → 1
            ui.rect.localScale = Vector3.one;
            ui.rect.DOScale(Vector3.one * 1.1f, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ui.rect.DOScale(Vector3.one, 0.3f).SetEase(Ease.InOutSine);
                });

            // 2. 颜色脉冲（背景金色闪烁）
            var bgImage = ui.rect.GetComponent<Image>();
            if (bgImage != null)
            {
                Color originalBg = bgImage.color;
                bgImage.DOColor(new Color(1f, 0.85f, 0.2f, 0.9f), 0.15f)
                    .OnComplete(() =>
                    {
                        bgImage.DOColor(originalBg, 0.5f).SetEase(Ease.InOutSine);
                    });
            }

            // 3. 进度条闪烁
            if (ui.progressBarFill != null)
            {
                ui.progressBarFill.DOColor(new Color(1f, 1f, 0.6f), 0.15f)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            // 4. 飘字动画 "XX套装激活！"
            string flyText = threshold == 2
                ? $"{setData.name.Replace("🛡 ", "").Replace("🔥 ", "")} 2件效果激活！"
                : $"{setData.name.Replace("🛡 ", "").Replace("🔥 ", "")} 4件效果激活！";
            PlayFlyingText(flyText, ui.rect, setData.setColor);
        }

        /// <summary>
        /// 套装效果取消动画：红色闪烁提示
        /// </summary>
        private void PlaySetDeactivateAnimation(SetBonusUI ui, MockSetData setData)
        {
            if (ui.rect == null) return;

            // 红色闪烁
            var bgImage = ui.rect.GetComponent<Image>();
            if (bgImage != null)
            {
                Color originalBg = bgImage.color;
                bgImage.DOColor(new Color(0.8f, 0.15f, 0.15f, 0.9f), 0.15f)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            // 飘字 "XX套装效果失效！"
            string flyText = $"{setData.name.Replace("🛡 ", "").Replace("🔥 ", "")} 效果失效！";
            PlayFlyingText(flyText, ui.rect, new Color(1f, 0.3f, 0.2f));
        }

        /// <summary>
        /// 套装效果激活动画（使用 SetDefinition 真实数据）
        /// 金色脉冲 + 闪光 + 飘字
        /// </summary>
        private void PlaySetActivateAnimationFromSystem(SetBonusUI ui, SetDefinition def)
        {
            if (ui.rect == null) return;

            // 1. Scale脉冲：1 → 1.1 → 1
            ui.rect.localScale = Vector3.one;
            ui.rect.DOScale(Vector3.one * 1.1f, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ui.rect.DOScale(Vector3.one, 0.3f).SetEase(Ease.InOutSine);
                });

            // 2. 颜色脉冲（背景金色闪烁）
            var bgImage = ui.rect.GetComponent<Image>();
            if (bgImage != null)
            {
                Color originalBg = bgImage.color;
                bgImage.DOColor(new Color(1f, 0.85f, 0.2f, 0.9f), 0.15f)
                    .OnComplete(() =>
                    {
                        bgImage.DOColor(originalBg, 0.5f).SetEase(Ease.InOutSine);
                    });
            }

            // 3. 进度条闪烁
            if (ui.progressBarFill != null)
            {
                ui.progressBarFill.DOColor(new Color(1f, 1f, 0.6f), 0.15f)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            // 4. 飘字动画
            string flyText = $"{def.setName} {def.requiredCount}件效果激活！";
            Color flyColor = GetSetColor(def.setId);
            PlayFlyingText(flyText, ui.rect, flyColor);
        }

        /// <summary>
        /// 套装效果取消动画（使用 SetDefinition 真实数据）
        /// 红色闪烁提示
        /// </summary>
        private void PlaySetDeactivateAnimationFromSystem(SetBonusUI ui, SetDefinition def)
        {
            if (ui.rect == null) return;

            // 红色闪烁
            var bgImage = ui.rect.GetComponent<Image>();
            if (bgImage != null)
            {
                Color originalBg = bgImage.color;
                bgImage.DOColor(new Color(0.8f, 0.15f, 0.15f, 0.9f), 0.15f)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            // 飘字
            string flyText = $"{def.setName} 效果失效！";
            PlayFlyingText(flyText, ui.rect, new Color(1f, 0.3f, 0.2f));
        }

        /// <summary>
        /// 飘字动画：在指定位置创建文字，向上飘动后消失
        /// </summary>
        private void PlayFlyingText(string text, RectTransform origin, Color textColor)
        {
            if (origin == null || setBonusContainer == null) return;

            // 在套装容器上创建飘字
            var flyGo = new GameObject("FlyText");
            flyGo.transform.SetParent(setBonusContainer, false);
            var flyRect = flyGo.AddComponent<RectTransform>();

            // 定位在origin上方
            flyRect.anchorMin = flyRect.anchorMax = new Vector2(0.5f, 0.5f);
            flyRect.pivot = new Vector2(0.5f, 0.5f);
            flyRect.anchoredPosition = new Vector2(0f, origin.anchoredPosition.y + 30f);
            flyRect.sizeDelta = new Vector2(300, 30);

            var flyText = flyGo.AddComponent<Text>();
            flyText.text = text;
            flyText.fontSize = 16;
            flyText.fontStyle = FontStyle.Bold;
            flyText.alignment = TextAnchor.MiddleCenter;
            flyText.color = textColor;

            // 描边让文字更清晰
            var outline = flyGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);

            // 向上飘动 + 淡出
            flyRect.DOAnchorPosY(flyRect.anchoredPosition.y + 60f, 1.5f)
                .SetEase(Ease.OutCubic);
            flyText.DOFade(0f, 1.5f)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    Destroy(flyGo);
                });
        }
        #endregion

        // ========== 动画 ==========

        private void PlayEquipAnimation(EquipmentSlot slot)
        {
            var targetBg = slot switch
            {
                EquipmentSlot.Weapon => weaponSlotBg,
                EquipmentSlot.Armor => armorSlotBg,
                EquipmentSlot.Accessory => accessorySlotBg,
                _ => null
            };

            if (targetBg != null)
            {
                targetBg.rectTransform.localScale = Vector3.one * 0.8f;
                targetBg.rectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }

        // ========== 工具方法 ==========

        private static string GetClassIcon(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "⚔",
                HeroClass.Mage => "🔮",
                HeroClass.Assassin => "🗡",
                _ => "●"
            };
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => new Color(0.85f, 0.85f, 0.85f),
                CardRarity.Rare => new Color(0.3f, 0.55f, 1f),
                CardRarity.Epic => new Color(0.7f, 0.3f, 1f),
                CardRarity.Legendary => new Color(1f, 0.85f, 0.2f),
                _ => Color.white
            };
        }

        private static string GetEquipStats(EquipmentData eq)
        {
            if (eq == null) return "";
            var parts = new System.Collections.Generic.List<string>();
            if (eq.attackBonus != 0) parts.Add($"⚔{eq.attackBonus:+#;-#;0}");
            if (eq.defenseBonus != 0) parts.Add($"🛡{eq.defenseBonus:+#;-#;0}");
            if (eq.healthBonus != 0) parts.Add($"❤{eq.healthBonus:+#;-#;0}");
            if (eq.speedBonus != 0) parts.Add($"💨{eq.speedBonus:+#;-#;0}");
            return string.Join(" ", parts);
        }

        // ================================================================
        // 强化系统对接 — EquipmentEnhancer
        // ================================================================

        #region 强化系统

        /// <summary>
        /// 强化按钮点击回调 — 对接 EquipmentEnhancer.Instance.Enhance(equip)
        /// 消耗金币进行强化，5级以上有失败概率，失败不掉级
        /// </summary>
        public void OnEnhanceClicked()
        {
            if (enhanceTarget == null)
            {
                Debug.Log("[EquipPanel] 没有选中可强化的装备");
                return;
            }

            var enhancer = EquipmentEnhancer.Instance;
            if (enhancer == null)
            {
                Debug.LogError("[EquipPanel] EquipmentEnhancer 实例不存在！");
                return;
            }

            // 再次检查是否可以强化（金币/等级）
            if (!enhancer.CanEnhance(enhanceTarget))
            {
                Debug.Log($"[EquipPanel] 无法强化 {enhanceTarget.equipmentName}（金币不足或已满级）");
                UpdateEnhancePreview(enhanceTarget);
                return;
            }

            // 执行强化
            EnhanceResult result = enhancer.Enhance(enhanceTarget);

            // 根据结果显示反馈
            switch (result)
            {
                case EnhanceResult.Success:
                    Debug.Log($"[EquipPanel] 强化成功！{enhanceTarget.equipmentName} → +{enhanceTarget.enhanceLevel}");
                    PlayEnhanceSuccessAnimation();
                    break;
                case EnhanceResult.Failed:
                    Debug.Log($"[EquipPanel] 强化失败！{enhanceTarget.equipmentName} 保持 +{enhanceTarget.enhanceLevel}");
                    PlayEnhanceFailAnimation();
                    break;
                case EnhanceResult.MaxLevel:
                    Debug.Log($"[EquipPanel] {enhanceTarget.equipmentName} 已达最大强化等级");
                    break;
                case EnhanceResult.NotEnoughGold:
                    Debug.Log($"[EquipPanel] 金币不足，强化 {enhanceTarget.equipmentName} 需要 {enhancer.GetEnhanceCost(enhanceTarget)} 金币");
                    break;
                case EnhanceResult.InvalidEquipment:
                    Debug.Log("[EquipPanel] 无效装备");
                    break;
            }

            // 更新强化预览
            UpdateEnhancePreview(enhanceTarget);

            // 刷新属性面板（强化可能改变装备属性）
            RefreshStats();
            RefreshSlots();
        }

        /// <summary>
        /// 更新强化预览显示
        /// 显示：当前等级 → 下一等级、费用、成功率、属性增量
        /// 5级以上标红成功率，提醒玩家有失败风险
        /// </summary>
        public void UpdateEnhancePreview(EquipmentData equip)
        {
            // 确保强化区域容器存在
            EnsureEnhancePanel();

            if (enhancePreviewText == null) return;

            if (equip == null)
            {
                enhancePreviewText.text = "选择装备查看强化信息";
                enhancePreviewText.color = new Color(0.5f, 0.5f, 0.5f);
                if (enhanceButton != null) enhanceButton.interactable = false;
                return;
            }

            var enhancer = EquipmentEnhancer.Instance;
            if (enhancer == null)
            {
                enhancePreviewText.text = "强化系统未就绪";
                enhancePreviewText.color = STAT_DOWN;
                if (enhanceButton != null) enhanceButton.interactable = false;
                return;
            }

            bool canEnhance = enhancer.CanEnhance(equip);
            bool isMaxLevel = equip.enhanceLevel >= equip.maxEnhanceLevel;
            int cost = enhancer.GetEnhanceCost(equip);
            float successRate = enhancer.GetSuccessRate(equip);

            // 构建预览文本
            string preview = $"<b>{equip.equipmentName}</b>\n";

            if (isMaxLevel)
            {
                preview += $"<color=#FFD700>已满级 +{equip.enhanceLevel}/{equip.maxEnhanceLevel}</color>";
                enhancePreviewText.color = new Color(1f, 0.85f, 0.2f);
                if (enhanceButton != null) enhanceButton.interactable = false;
            }
            else
            {
                // 等级进度
                preview += $"强化等级：+{equip.enhanceLevel} → +{equip.enhanceLevel + 1}\n";

                // 属性增量预览
                float delta = 0.1f; // 每级+10%
                int atkDelta = Mathf.RoundToInt(equip.attackBonus * delta);
                int defDelta = Mathf.RoundToInt(equip.defenseBonus * delta);
                int hpDelta = Mathf.RoundToInt(equip.healthBonus * delta);
                int spdDelta = Mathf.RoundToInt(equip.speedBonus * delta);

                if (atkDelta > 0) preview += $"⚔攻击 +{atkDelta} ";
                if (defDelta > 0) preview += $"🛡防御 +{defDelta} ";
                if (hpDelta > 0) preview += $"❤生命 +{hpDelta} ";
                if (spdDelta > 0) preview += $"💨速度 +{spdDelta} ";
                preview += "\n";

                // 费用
                var inventory = PlayerInventory.Instance;
                bool goldEnough = inventory != null && inventory.Gold >= cost;
                string costColor = goldEnough ? "#FFFFFF" : "#FF4444";
                preview += $"<color={costColor}>费用：{cost} 金币</color>\n";

                // 成功率（5级以上标红警告）
                if (equip.enhanceLevel >= 5)
                {
                    // 5级以上有失败概率，用红色显示成功率
                    preview += $"<color=#FF6644>成功率：{successRate * 100:F0}%（有失败风险）</color>";
                }
                else
                {
                    preview += $"<color=#44FF44>成功率：100%</color>";
                }

                enhancePreviewText.color = Color.white;
                if (enhanceButton != null) enhanceButton.interactable = canEnhance;
            }

            enhancePreviewText.text = preview;
        }

        /// <summary>
        /// 确保强化区域容器存在（程序化创建）
        /// </summary>
        private void EnsureEnhancePanel()
        {
            if (enhancePanel != null) return;

            Transform parent = statsPanel != null ? statsPanel.parent : transform;
            GameObject go = new GameObject("EnhancePanel");
            go.transform.SetParent(parent, false);
            enhancePanel = go.AddComponent<RectTransform>();

            // 定位在套装信息下方
            var rect = enhancePanel;
            rect.anchorMin = new Vector2(0.22f, 0.18f);
            rect.anchorMax = new Vector2(0.98f, 0.28f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // 背景图
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            bg.raycastTarget = false;

            // 垂直布局
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 预览文字
            var textGo = new GameObject("EnhancePreviewText");
            textGo.transform.SetParent(go.transform, false);
            enhancePreviewText = textGo.AddComponent<Text>();
            enhancePreviewText.fontSize = 11;
            enhancePreviewText.alignment = TextAnchor.UpperLeft;
            enhancePreviewText.color = new Color(0.5f, 0.5f, 0.5f);
            enhancePreviewText.text = "选择装备查看强化信息";
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;

            // 强化按钮（程序化创建，如果Inspector未指定）
            if (enhanceButton == null)
            {
                var btnGo = new GameObject("EnhanceButton");
                btnGo.transform.SetParent(go.transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(120, 30);
                var btnBg = btnGo.AddComponent<Image>();
                btnBg.color = new Color(0.2f, 0.6f, 0.3f, 0.9f);
                enhanceButton = btnGo.AddComponent<Button>();

                var btnTextGo = new GameObject("Text");
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRect = btnTextGo.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = btnTextRect.offsetMax = Vector2.zero;
                var btnText = btnTextGo.AddComponent<Text>();
                btnText.text = "⚡ 强化";
                btnText.fontSize = 14;
                btnText.fontStyle = FontStyle.Bold;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
            }
        }

        /// <summary>
        /// 强化成功动画 — 绿色脉冲 + 缩放弹跳
        /// </summary>
        private void PlayEnhanceSuccessAnimation()
        {
            if (enhancePanel == null) return;

            var bg = enhancePanel.GetComponent<Image>();
            if (bg != null)
            {
                Color orig = bg.color;
                bg.DOColor(new Color(0.15f, 0.5f, 0.2f, 0.95f), 0.15f)
                    .OnComplete(() => bg.DOColor(orig, 0.4f).SetEase(Ease.InOutSine));
            }

            enhancePanel.localScale = Vector3.one * 0.95f;
            enhancePanel.DOScale(Vector3.one * 1.05f, 0.15f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => enhancePanel.DOScale(Vector3.one, 0.2f).SetEase(Ease.InOutSine));

            // 飘字
            PlayFlyingText("强化成功！", enhancePanel, new Color(0.3f, 1f, 0.4f));
        }

        /// <summary>
        /// 强化失败动画 — 红色闪烁 + 抖动
        /// </summary>
        private void PlayEnhanceFailAnimation()
        {
            if (enhancePanel == null) return;

            var bg = enhancePanel.GetComponent<Image>();
            if (bg != null)
            {
                Color orig = bg.color;
                bg.DOColor(new Color(0.6f, 0.1f, 0.1f, 0.95f), 0.1f)
                    .SetLoops(4, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(() => bg.color = orig);
            }

            // 抖动效果
            enhancePanel.DOShakeAnchorPos(0.4f, 8f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic);

            // 飘字
            PlayFlyingText("强化失败！", enhancePanel, new Color(1f, 0.3f, 0.2f));
        }

        #endregion

        // ================================================================
        // 套装效果展示对接 — SetBonusSystem
        // ================================================================

        #region 套装效果展示

        /// <summary>
        /// 显示当前英雄的套装激活状态 — 对接 SetBonusSystem.Instance.GetActiveSetBonuses
        /// 同时结合 EquipmentManager.CountSetPieces 获取件数统计
        /// </summary>
        public void ShowSetBonusInfo(Hero hero)
        {
            if (hero == null) return;

            var setBonusSystem = SetBonusSystem.Instance;

            // 获取当前英雄各套装件数统计
            var pieceCounts = EquipmentManager.CountSetPieces(hero);

            // 获取已激活的套装效果（来自 SetBonusSystem）
            var activeBonuses = setBonusSystem != null
                ? setBonusSystem.GetActiveSetBonuses(hero)
                : new List<ActiveSetBonus>();

            // 清理旧UI
            ClearSetBonusUIs();
            EnsureSetBonusContainer();

            // 记录快照
            lastSetPieceCount = new Dictionary<string, int>(pieceCounts);

            // 获取所有套装ID
            var allSetIds = setBonusSystem != null
                ? setBonusSystem.GetAllSetIds()
                : new List<string>(pieceCounts.Keys);

            // 为每个有件的套装创建UI
            foreach (var setId in allSetIds)
            {
                int pieceCount = pieceCounts.TryGetValue(setId, out var c) ? c : 0;

                // 只显示有至少1件装备关联的套装
                if (pieceCount > 0 || HasSetEquipmentInBackpack(setId))
                {
                    // 使用真实套装系统数据替代Mock数据
                    var defs = setBonusSystem != null
                        ? setBonusSystem.GetSetDefinitions(setId)
                        : new List<SetDefinition>();

                    if (defs.Count > 0)
                    {
                        var ui = CreateSetBonusUIFromSystem(setId, defs, pieceCount);
                        setBonusUIs.Add(ui);
                    }
                    else
                    {
                        // 降级：如果 SetBonusSystem 没有定义，尝试用 Mock 数据
                        var mockData = GetSetDataById(setId);
                        if (mockData != null)
                        {
                            var ui = CreateSetBonusUI(mockData, pieceCount);
                            setBonusUIs.Add(ui);
                        }
                    }
                }
            }

            // 打印激活的套装效果到日志
            foreach (var bonus in activeBonuses)
            {
                Debug.Log($"[套装] {hero.Data.heroName} 激活 {bonus.setName}（{bonus.equippedCount}件/{bonus.requiredCount}件需求）");
                foreach (var effect in bonus.bonuses)
                {
                    Debug.Log($"  效果：{effect.description}");
                }
            }
        }

        /// <summary>
        /// 使用 SetBonusSystem 的 SetDefinition 数据创建套装UI
        /// 支持任意数量的阈值（2件、4件等）
        /// </summary>
        private SetBonusUI CreateSetBonusUIFromSystem(string setId, List<SetDefinition> defs, int pieceCount)
        {
            var ui = new SetBonusUI { setId = setId };

            // 获取套装名和颜色
            string setName = defs.Count > 0 ? defs[0].setName : setId;
            Color setColor = GetSetColor(setId);
            int maxPieces = 4; // 默认总件数

            // 找出最高阈值作为总件数
            foreach (var def in defs)
            {
                if (def.requiredCount > maxPieces)
                    maxPieces = def.requiredCount;
            }

            // 根节点
            var rootGo = new GameObject($"SetBonus_{setId}");
            rootGo.transform.SetParent(setBonusContainer, false);
            ui.rect = rootGo.AddComponent<RectTransform>();

            // 背景图（用于动画脉冲）
            var rootBg = rootGo.AddComponent<Image>();
            rootBg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            rootBg.raycastTarget = false;

            // 垂直布局
            var vlg = rootGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // ---- 第一行：套装名 + 进度条 + "X/Y" ----
            var row1Go = new GameObject("Row1_Progress");
            row1Go.transform.SetParent(rootGo.transform, false);
            var row1Rect = row1Go.AddComponent<RectTransform>();
            row1Rect.sizeDelta = new Vector2(0, 28);

            var row1Layout = row1Go.AddComponent<HorizontalLayoutGroup>();
            row1Layout.spacing = 6f;
            row1Layout.padding = new RectOffset(0, 0, 2, 2);
            row1Layout.childAlignment = TextAnchor.MiddleLeft;
            row1Layout.childControlWidth = true;
            row1Layout.childControlHeight = true;
            row1Layout.childForceExpandWidth = true;
            row1Layout.childForceExpandHeight = false;

            // 套装名
            var nameGo = new GameObject("SetName");
            nameGo.transform.SetParent(row1Go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            ui.setNameText = nameGo.AddComponent<Text>();
            ui.setNameText.text = setName;
            ui.setNameText.fontSize = 13;
            ui.setNameText.fontStyle = FontStyle.Bold;
            ui.setNameText.color = setColor;
            ui.setNameText.alignment = TextAnchor.MiddleLeft;
            var nameLayoutEl = nameGo.AddComponent<LayoutElement>();
            nameLayoutEl.minWidth = 100;
            nameLayoutEl.flexibleWidth = 0;

            // 进度条背景
            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(row1Go.transform, false);
            var barBgRect = barBgGo.AddComponent<RectTransform>();
            var barBgImage = barBgGo.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            barBgImage.raycastTarget = false;
            var barBgLayoutEl = barBgGo.AddComponent<LayoutElement>();
            barBgLayoutEl.flexibleWidth = 1;
            barBgLayoutEl.minHeight = 16;

            // 进度条填充
            var barFillGo = new GameObject("BarFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRect = barFillGo.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(Mathf.Clamp01(pieceCount / (float)maxPieces), 1f);
            barFillRect.offsetMin = barFillRect.offsetMax = Vector2.zero;
            ui.progressBarFill = barFillGo.AddComponent<Image>();

            // 检查是否达到任意阈值
            bool anyActive = false;
            foreach (var def in defs)
            {
                if (pieceCount >= def.requiredCount) { anyActive = true; break; }
            }
            ui.progressBarFill.color = anyActive ? new Color(1f, 0.85f, 0.2f) : setColor;
            ui.progressBarFill.raycastTarget = false;

            // "X/Y" 文字
            var progTextGo = new GameObject("ProgressText");
            progTextGo.transform.SetParent(row1Go.transform, false);
            var progTextRect = progTextGo.AddComponent<RectTransform>();
            ui.progressText = progTextGo.AddComponent<Text>();
            ui.progressText.text = $"{pieceCount}/{maxPieces}";
            ui.progressText.fontSize = 12;
            ui.progressText.fontStyle = FontStyle.Bold;
            ui.progressText.color = anyActive ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
            ui.progressText.alignment = TextAnchor.MiddleCenter;
            var progLayoutEl = progTextGo.AddComponent<LayoutElement>();
            progLayoutEl.minWidth = 36;
            progLayoutEl.flexibleWidth = 0;

            // ---- 效果行：按阈值从小到大显示 ----
            defs.Sort((a, b) => a.requiredCount.CompareTo(b.requiredCount));

            // 为2件和4件效果分别创建行
            if (defs.Count >= 1)
                ui.bonusLine2 = CreateBonusLineFromSystem(rootGo.transform, defs[0], pieceCount);
            if (defs.Count >= 2)
                ui.bonusLine4 = CreateBonusLineFromSystem(rootGo.transform, defs[1], pieceCount);

            return ui;
        }

        /// <summary>
        /// 从 SetDefinition 创建一个套装效果行
        /// </summary>
        private Text CreateBonusLineFromSystem(Transform parent, SetDefinition def, int pieceCount)
        {
            bool isActive = pieceCount >= def.requiredCount;
            string effectDesc = string.Join(", ", def.bonuses.ConvertAll(b => b.description));
            string icon = isActive ? "✅" : "🔒";
            string text = $"{icon} {def.requiredCount}件: {effectDesc}";

            return CreateBonusLine(parent, $"BonusLine_{def.requiredCount}", text, isActive);
        }

        /// <summary>
        /// 根据套装ID返回对应的标识颜色
        /// </summary>
        private static Color GetSetColor(string setId)
        {
            return setId switch
            {
                "set_flame" => new Color(1f, 0.3f, 0.2f),    // 烈焰-红色
                "set_rock" => new Color(0.3f, 0.55f, 1f),    // 磐石-蓝色
                "set_wind" => new Color(0.3f, 0.9f, 0.5f),   // 疾风-绿色
                "set_fate" => new Color(0.7f, 0.3f, 1f),     // 命运-紫色
                "iron_wall" => new Color(0.3f, 0.55f, 1f),   // 铁壁-蓝色（兼容旧Mock）
                "berserker" => new Color(1f, 0.3f, 0.2f),    // 狂战-红色（兼容旧Mock）
                _ => new Color(0.7f, 0.7f, 0.7f)
            };
        }

        #endregion

        // ================================================================
        // 装备对比系统
        // ================================================================

        #region 装备对比

        /// <summary>
        /// 对比两件装备的属性差异
        /// 返回对比描述字符串，用于UI显示
        /// current: 当前穿戴的装备（可能为null表示空槽位）
        /// candidate: 准备穿戴的候选装备
        /// </summary>
        public string CompareEquipments(EquipmentData current, EquipmentData candidate)
        {
            if (candidate == null) return "";
            if (current == null)
            {
                // 空槽位 → 只显示候选装备属性（全部为增益）
                string stats = GetEquipStats(candidate);
                return $"<color=#44FF44>新装备（空槽位）</color>\n{stats}";
            }

            // 计算各属性差值（含强化加成）
            int atkDiff = candidate.EnhancedAttackBonus - current.EnhancedAttackBonus;
            int defDiff = candidate.EnhancedDefenseBonus - current.EnhancedDefenseBonus;
            int hpDiff = candidate.EnhancedHealthBonus - current.EnhancedHealthBonus;
            int spdDiff = candidate.EnhancedSpeedBonus - current.EnhancedSpeedBonus;
            float critDiff = candidate.EnhancedCritRateBonus - current.EnhancedCritRateBonus;

            // 战力评分对比
            int currentPower = PlayerInventory.GetEquipmentPower(current);
            int candidatePower = PlayerInventory.GetEquipmentPower(candidate);
            int powerDiff = candidatePower - currentPower;

            string result = $"<b>装备对比</b>\n";
            result += FormatCompareLine("⚔攻击", current.EnhancedAttackBonus, candidate.EnhancedAttackBonus, atkDiff);
            result += FormatCompareLine("🛡防御", current.EnhancedDefenseBonus, candidate.EnhancedDefenseBonus, defDiff);
            result += FormatCompareLine("❤生命", current.EnhancedHealthBonus, candidate.EnhancedHealthBonus, hpDiff);
            result += FormatCompareLine("💨速度", current.EnhancedSpeedBonus, candidate.EnhancedSpeedBonus, spdDiff);

            if (critDiff != 0f)
            {
                string critColor = critDiff > 0 ? "#44FF44" : "#FF4444";
                string critSign = critDiff > 0 ? "+" : "";
                result += $"暴击: {critColor}>{critSign}{critDiff * 100:F1}%</color>\n";
            }

            // 战力评分
            string powerColor = powerDiff > 0 ? "#44FF44" : powerDiff < 0 ? "#FF4444" : "#CCCCCC";
            string powerSign = powerDiff > 0 ? "+" : "";
            result += $"<b>战力: {powerColor}>{currentPower} → {candidatePower} ({powerSign}{powerDiff})</color></b>";

            return result;
        }

        /// <summary>
        /// 格式化单行对比文本
        /// </summary>
        private static string FormatCompareLine(string label, int currentVal, int candidateVal, int diff)
        {
            if (diff == 0)
                return $"{label}: {currentVal} → {candidateVal}\n";
            string color = diff > 0 ? "#44FF44" : "#FF4444";
            string sign = diff > 0 ? "+" : "";
            return $"{label}: {currentVal} → <color={color}>{candidateVal} ({sign}{diff})</color>\n";
        }

        /// <summary>
        /// 更新对比区域显示
        /// </summary>
        private void UpdateCompareDisplay(string compareResult)
        {
            EnsureComparePanel();

            if (compareText == null) return;

            if (string.IsNullOrEmpty(compareResult))
            {
                compareText.text = "选中背包装备查看对比";
                compareText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                compareText.text = compareResult;
                compareText.color = Color.white;
            }
        }

        /// <summary>
        /// 确保对比区域容器存在（程序化创建）
        /// </summary>
        private void EnsureComparePanel()
        {
            if (comparePanel != null) return;

            Transform parent = statsPanel != null ? statsPanel.parent : transform;
            GameObject go = new GameObject("ComparePanel");
            go.transform.SetParent(parent, false);
            comparePanel = go.AddComponent<RectTransform>();

            var rect = comparePanel;
            rect.anchorMin = new Vector2(0.22f, 0.10f);
            rect.anchorMax = new Vector2(0.98f, 0.18f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.1f, 0.9f);
            bg.raycastTarget = false;

            var textGo = new GameObject("CompareText");
            textGo.transform.SetParent(go.transform, false);
            compareText = textGo.AddComponent<Text>();
            compareText.fontSize = 11;
            compareText.alignment = TextAnchor.UpperLeft;
            compareText.color = new Color(0.5f, 0.5f, 0.5f);
            compareText.text = "选中背包装备查看对比";
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector4(8, 4, 8, 4);
            textRect.offsetMax = new Vector4(-8, -4, -8, -4);
        }

        #endregion

        // ================================================================
        // 装备穿脱对接 — Hero.Equip/Unequip + PlayerInventory
        // ================================================================

        #region 装备穿脱

        /// <summary>
        /// 将装备穿戴到英雄身上
        /// 对接 Hero.Equip(equipment) + PlayerInventory.EquipToHero
        /// 如果目标槽位已有装备，自动替换（旧装备回到背包）
        /// </summary>
        public void OnEquipToHero(Hero hero, EquipmentData equip)
        {
            if (hero == null || equip == null)
            {
                Debug.LogWarning("[EquipPanel] 穿戴失败：英雄或装备为空");
                return;
            }

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[EquipPanel] PlayerInventory 实例不存在！");
                return;
            }

            // 检查装备是否在背包中
            if (!inventory.Equipments.Contains(equip))
            {
                Debug.LogWarning($"[EquipPanel] 装备 {equip.equipmentName} 不在背包中，无法穿戴");
                return;
            }

            // 检查目标槽位是否已有装备 → 自动替换
            EquipmentData existingEquip = hero.GetEquippedItem(equip.slot);
            if (existingEquip != null)
            {
                // 卸下旧装备回到背包
                var unequipped = hero.Unequip(equip.slot);
                if (unequipped != null)
                {
                    inventory.AddEquipment(unequipped);
                    Debug.Log($"[EquipPanel] 替换：{unequipped.equipmentName} 卸下回到背包");
                }
            }

            // 穿戴新装备（从背包移除 → 英雄装备）
            if (inventory.EquipToHero(equip, hero))
            {
                Debug.Log($"[EquipPanel] 穿戴 {equip.equipmentName} → {hero.Data.heroName}（{equip.slot}槽位）");
            }
        }

        /// <summary>
        /// 从英雄身上卸下装备
        /// 对接 Hero.Unequip(slot) + PlayerInventory.AddEquipment
        /// 卸下的装备自动回到背包
        /// </summary>
        public void OnUnequipFromHero(Hero hero, EquipmentSlot slot)
        {
            if (hero == null)
            {
                Debug.LogWarning("[EquipPanel] 卸下失败：英雄为空");
                return;
            }

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogError("[EquipPanel] PlayerInventory 实例不存在！");
                return;
            }

            // 检查目标槽位是否有装备
            EquipmentData equipped = hero.GetEquippedItem(slot);
            if (equipped == null)
            {
                Debug.LogWarning($"[EquipPanel] {hero.Data.heroName} 的 {slot} 槽位没有装备");
                return;
            }

            // 使用 PlayerInventory.UnequipFromHero 统一处理
            inventory.UnequipFromHero(slot, hero);
            Debug.Log($"[EquipPanel] 卸下 {equipped.equipmentName} ← {hero.Data.heroName}（{slot}槽位）");
        }

        #endregion
    }
}
