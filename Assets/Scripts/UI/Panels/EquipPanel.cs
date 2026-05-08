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
    /// │          │ 属性对比：         │
    /// │          │ ⚔ 45 → 55 (+10)  │
    /// │          │ 🛡 20 → 20       │
    /// │          │ ❤ 100 → 100      │
    /// ├──────────┴───────────────────┤
    /// │  背包装备                     │
    /// │ ┌──────┐ ┌──────┐ ┌──────┐ │
    /// │ │铁盾  │ │疾风戒│ │力量护│ │
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
        }

        private List<HeroItemUI> heroItems = new List<HeroItemUI>();
        private List<BackpackItemUI> backpackItems = new List<BackpackItemUI>();

        private Hero selectedHero;
        private EquipmentData selectedEquipment;
        private int selectedBackpackIndex = -1;

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
            }
            else if (selectedEquipment != null && selectedEquipment.slot == slot)
            {
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

        // ========== 装备查询（MVP直接查，等后端补Hero.Equip/Unequip后对接）==========

        private EquipmentData GetEquippedItem(EquipmentSlot slot)
        {
            // TODO: 等后端实现 Hero.GetEquippedItem(slot) 后对接
            // 目前返回null（MVP无装备状态持久化）
            return null;
        }

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
