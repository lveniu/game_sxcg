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

            // 初始化
            RefreshHeroList();
            SelectHeroByDefault();
            RefreshBackpack();

            selectedEquipment = null;
            selectedBackpackIndex = -1;
        }

        protected override void OnHide()
        {
            closeButton?.onClick.RemoveAllListeners();
            weaponSlotButton?.onClick.RemoveAllListeners();
            armorSlotButton?.onClick.RemoveAllListeners();
            accessorySlotButton?.onClick.RemoveAllListeners();
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

            // 刷新套装信息
            RefreshSetBonuses();

            // 重置背包选中
            selectedEquipment = null;
            selectedBackpackIndex = -1;
            RefreshBackpackSelection();
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
                // 记录卸下前的套装件数
                var beforeCounts = GetCurrentSetPieceCounts();

                // 卸下装备
                var inventory = PlayerInventory.Instance;
                if (inventory != null)
                {
                    inventory.UnequipFromHero(slot, selectedHero);
                    Debug.Log($"[Equip] 卸下 {equipped.equipmentName}");
                }

                // 刷新
                RefreshSlots();
                RefreshStats();
                RefreshBackpack();

                // 刷新套装信息（含卸下检测动画）
                RefreshSetBonusesWithDeactivateCheck(beforeCounts);
            }
            else if (selectedEquipment != null && selectedEquipment.slot == slot)
            {
                // 记录穿戴前的套装件数
                var beforeCounts = GetCurrentSetPieceCounts();

                // 穿戴选中的装备
                var inventory = PlayerInventory.Instance;
                if (inventory != null && inventory.EquipToHero(selectedEquipment, selectedHero))
                {
                    Debug.Log($"[Equip] 穿戴 {selectedEquipment.equipmentName} → {selectedHero.Data.heroName}");

                    // 穿戴动画
                    PlayEquipAnimation(slot);

                    selectedEquipment = null;
                    selectedBackpackIndex = -1;
                    RefreshSlots();
                    RefreshStats();
                    RefreshBackpack();

                    // 刷新套装信息（含激活检测动画）
                    RefreshSetBonusesWithActivateCheck(beforeCounts);
                }
            }
            else if (selectedEquipment != null && selectedEquipment.slot != slot)
            {
                // 槽位不匹配提示
                Debug.Log($"[Equip] 该装备需要{selectedEquipment.slot}槽位，当前点击的是{slot}");
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
        /// </summary>
        private Dictionary<string, int> GetCurrentSetPieceCounts()
        {
            var counts = new Dictionary<string, int>();
            if (selectedHero == null) return counts;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var eq = GetEquippedItem(slot);
                if (eq == null) continue;
                string setId = GetSetIdForEquipment(eq.equipmentName);
                if (setId == null) continue;
                if (!counts.ContainsKey(setId))
                    counts[setId] = 0;
                counts[setId]++;
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

            // 为每个有件的套装创建UI（也处理已装备0件但有套装装备在背包的情况）
            // 这里只显示当前英雄有装备关联的套装
            foreach (var setData in MOCK_SET_DATABASE)
            {
                int pieceCount = counts.TryGetValue(setData.setId, out var c) ? c : 0;

                // 只显示有至少1件装备的套装（或者总是显示也可以，这里选择总是显示以演示）
                if (pieceCount > 0 || HasSetEquipmentInBackpack(setData.setId))
                {
                    var ui = CreateSetBonusUI(setData, pieceCount);
                    setBonusUIs.Add(ui);
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

            // 创建UI并检测激活
            foreach (var setData in MOCK_SET_DATABASE)
            {
                int before = beforeCounts.TryGetValue(setData.setId, out var b) ? b : 0;
                int after = afterCounts.TryGetValue(setData.setId, out var a) ? a : 0;

                if (after > 0 || HasSetEquipmentInBackpack(setData.setId))
                {
                    var ui = CreateSetBonusUI(setData, after);
                    setBonusUIs.Add(ui);

                    // 检测是否跨过阈值 → 播放激活动画
                    if (after >= setData.threshold2 && before < setData.threshold2)
                    {
                        PlaySetActivateAnimation(ui, setData, 2);
                    }
                    // 4件效果（如果有4件阈值）
                    if (after >= setData.totalPieces && before < setData.totalPieces)
                    {
                        PlaySetActivateAnimation(ui, setData, 4);
                    }
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

            // 创建UI并检测取消
            foreach (var setData in MOCK_SET_DATABASE)
            {
                int before = beforeCounts.TryGetValue(setData.setId, out var b) ? b : 0;
                int after = afterCounts.TryGetValue(setData.setId, out var a) ? a : 0;

                if (after > 0 || before > 0 || HasSetEquipmentInBackpack(setData.setId))
                {
                    var ui = CreateSetBonusUI(setData, after);
                    setBonusUIs.Add(ui);

                    // 检测是否掉过阈值 → 播放取消动画（红色闪烁）
                    if (before >= setData.threshold2 && after < setData.threshold2 && after > 0)
                    {
                        PlaySetDeactivateAnimation(ui, setData);
                    }
                }
            }
        }

        /// <summary>
        /// 检查背包装备中是否有属于指定套装的装备
        /// </summary>
        private bool HasSetEquipmentInBackpack(string setId)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || inventory.Equipments == null) return false;
            foreach (var eq in inventory.Equipments)
            {
                if (eq != null && GetSetIdForEquipment(eq.equipmentName) == setId)
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
    }
}
