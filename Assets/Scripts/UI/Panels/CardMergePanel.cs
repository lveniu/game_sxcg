using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// FE-21 卡牌合成面板 — 两张同稀有度卡牌合成更高稀有度随机卡牌
    /// 
    /// 720x1280竖屏布局：
    /// ┌──────────────────────────┐
    /// │ 标题栏：卡牌合成 + 返回   │
    /// ├──────────────────────────┤
    /// │ 筛选栏：全部/白/蓝/紫    │
    /// ├────────┬────────┬────────┤
    /// │        │ 合成槽 │        │
    /// │ 卡牌   │ [A][B] │ 合成   │
    /// │ 列表   │        │ 预览   │
    /// │ (左)   │ 结果→  │ (右)   │
    /// │        │        │        │
    /// ├────────┴────────┴────────┤
    /// │ 合成按钮 + 金币消耗      │
    /// └──────────────────────────┘
    /// 
    /// 交互流程：
    /// 1. 左侧列表点选卡牌 → 加入合成槽A/B（同稀有度自动筛选）
    /// 2. 两张就绪 → 右侧预览池显示可能结果
    /// 3. 点击合成 → DOTween动画（飞入→旋转→爆发→新卡弹出）
    /// 4. 合成完成 → 刷新列表，新卡高亮
    /// </summary>
    public class CardMergePanel : UIPanel
    {
        // ── 常量 ──
        const float CW = 80f, CH = 105f, GAP = 8f;
        const float MERGE_ANIM_DUR = 0.8f;

        // ── 颜色 ──
        static readonly Color BG_DARK   = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        static readonly Color BG_PANEL  = new Color(0.12f, 0.12f, 0.18f, 0.9f);
        static readonly Color BG_SLOT   = new Color(0.15f, 0.15f, 0.22f, 0.85f);
        static readonly Color BG_SLOT_HOVER = new Color(0.2f, 0.2f, 0.3f, 0.9f);
        static readonly Color GOLD      = new Color(1f, 0.85f, 0.3f);
        static readonly Color SLOT_EMPTY_BG = new Color(0.1f, 0.1f, 0.15f, 0.6f);

        // ── 稀有度颜色（与 CardDeckEditorPanel / CardPlayPanel 统一） ──
        static Color RarityColor(CardRarity r) => r switch
        {
            CardRarity.White  => new Color(0.50f, 0.50f, 0.50f),
            CardRarity.Blue   => new Color(0.31f, 0.76f, 0.97f),
            CardRarity.Purple => new Color(0.67f, 0.28f, 0.74f),
            CardRarity.Gold   => new Color(1.00f, 0.84f, 0.25f),
            _ => Color.gray
        };
        static string RarityName(CardRarity r) => r switch
        {
            CardRarity.White  => "● 普通",
            CardRarity.Blue   => "● 精良",
            CardRarity.Purple => "● 史诗",
            CardRarity.Gold   => "● 传说",
            _ => r.ToString()
        };
        static string TypeEmoji(CardType t) => t switch
        {
            CardType.Battle => "⚔", CardType.Attribute => "📖",
            CardType.Evolution => "✨", CardType.Hero => "🦸",
            _ => "?"
        };

        // ── 状态 ──
        private List<CardInstance> allCards = new List<CardInstance>();
        private List<CardInstance> filteredCards = new List<CardInstance>();
        private CardInstance slotA, slotB;
        private CardRarity? filterRarity;
        private bool isAnimating;

        // ── UI引用 ──
        private RectTransform listZone, slotZone, previewZone;
        private RectTransform slotAHolder, slotBHolder, resultHolder;
        private Text costText, goldText, statusText;
        private Button mergeBtn;
        private List<RectTransform> listItemRts = new List<RectTransform>();
        private List<RectTransform> previewItemRts = new List<RectTransform>();

        // 筛选按钮
        private readonly List<Button> filterBtns = new List<Button>();
        private int activeFilterIdx;

        protected override void Awake()
        {
            base.Awake();
            panelId = "CardMerge";
            slideInAnimation = false; // 子面板，禁用滑入
            BuildUI();
        }

        protected override void OnShow()
        {
            LoadData();
            Refresh();
            // 监听合成完成事件
            var mergeSys = CardMergeSystem.Instance;
            if (mergeSys != null)
                mergeSys.OnMergeComplete += OnMergeComplete;
        }

        protected override void OnHide()
        {
            DOTween.Kill(gameObject);
            var mergeSys = CardMergeSystem.Instance;
            if (mergeSys != null)
                mergeSys.OnMergeComplete -= OnMergeComplete;
            slotA = null; slotB = null;
        }

        // ════════════════════════ UI构建 ════════════════════════

        private void BuildUI()
        {
            // 背景
            AddBg(rectTransform, BG_DARK);

            // ── 标题栏 ──
            var titleBar = MakeChild("Title", rectTransform, new Vector2(0, 0.92f), new Vector2(1, 1));
            AddBg(titleBar, BG_PANEL);
            MakeLabel("T", titleBar, new Vector2(0.05f, 0), new Vector2(0.45f, 1), "⚗ 卡牌合成", 20, Color.white, TextAnchor.MiddleLeft);
            var closeRt = MakeChild("Close", titleBar, new Vector2(0.85f, 0.15f), new Vector2(0.98f, 0.85f));
            AddBg(closeRt, new Color(0.6f, 0.2f, 0.2f));
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(Hide);
            MakeLabel("X", closeRt, Vector2.zero, Vector2.one, "✕", 18, Color.white, TextAnchor.MiddleCenter);

            // ── 筛选栏 ──
            BuildFilterBar();

            // ── 三列主体 ──
            // 左列：卡牌列表
            listZone = MakeChild("ListZone", rectTransform, new Vector2(0, 0.1f), new Vector2(0.33f, 0.87f));
            AddBg(listZone, new Color(0.1f, 0.1f, 0.14f, 0.7f));
            MakeLabel("LH", listZone, new Vector2(0, 0.93f), new Vector2(1, 1), "点选卡牌", 12, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleCenter);

            // 中列：合成槽
            slotZone = MakeChild("SlotZone", rectTransform, new Vector2(0.34f, 0.1f), new Vector2(0.66f, 0.87f));
            AddBg(slotZone, new Color(0.1f, 0.1f, 0.16f, 0.6f));
            BuildSlotArea();

            // 右列：预览
            previewZone = MakeChild("PreviewZone", rectTransform, new Vector2(0.67f, 0.1f), new Vector2(1f, 0.87f));
            AddBg(previewZone, new Color(0.1f, 0.1f, 0.14f, 0.7f));
            MakeLabel("PH", previewZone, new Vector2(0, 0.93f), new Vector2(1, 1), "可能获得", 12, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleCenter);

            // ── 底部操作栏 ──
            BuildBottomBar();
        }

        private void BuildFilterBar()
        {
            var bar = MakeChild("FilterBar", rectTransform, new Vector2(0, 0.87f), new Vector2(1, 0.91f));
            AddBg(bar, new Color(0.12f, 0.12f, 0.18f, 0.8f));

            string[] labels = { "全部", "●白", "●蓝", "●紫" };
            Color[] colors = {
                new Color(0.7f, 0.7f, 0.7f),
                new Color(0.50f, 0.50f, 0.50f),
                new Color(0.31f, 0.76f, 0.97f),
                new Color(0.67f, 0.28f, 0.74f)
            };

            float w = 1f / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var btnRt = MakeChild("F" + i, bar, new Vector2(i * w, 0.1f), new Vector2((i + 1) * w, 0.9f));
                AddBg(btnRt, i == 0 ? new Color(0.25f, 0.25f, 0.35f, 0.9f) : new Color(0.15f, 0.15f, 0.2f, 0.7f));
                var btn = btnRt.gameObject.AddComponent<Button>();
                var lbl = MakeLabel("L" + i, btnRt, Vector2.zero, Vector2.one, labels[i], 14, colors[i], TextAnchor.MiddleCenter);
                btn.onClick.AddListener(() => OnFilterClick(idx));
                filterBtns.Add(btn);
            }
            activeFilterIdx = 0;
        }

        private void BuildSlotArea()
        {
            // 合成槽A
            MakeLabel("SA_Label", slotZone, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.95f), "素材A", 11, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            slotAHolder = MakeChild("SlotA", slotZone, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.86f));
            AddBg(slotAHolder, SLOT_EMPTY_BG);
            AddOutline(slotAHolder, new Color(0.3f, 0.3f, 0.4f));
            MakeLabel("SA_Empty", slotAHolder, Vector2.zero, Vector2.one, "点选", 14, new Color(0.4f, 0.4f, 0.5f), TextAnchor.MiddleCenter);

            // 合成槽B
            MakeLabel("SB_Label", slotZone, new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.64f), "素材B", 11, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            slotBHolder = MakeChild("SlotB", slotZone, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.55f));
            AddBg(slotBHolder, SLOT_EMPTY_BG);
            AddOutline(slotBHolder, new Color(0.3f, 0.3f, 0.4f));
            MakeLabel("SB_Empty", slotBHolder, Vector2.zero, Vector2.one, "点选", 14, new Color(0.4f, 0.4f, 0.5f), TextAnchor.MiddleCenter);

            // 合成箭头
            MakeLabel("Arrow", slotZone, new Vector2(0.3f, 0.27f), new Vector2(0.7f, 0.34f), "▼", 20, GOLD, TextAnchor.MiddleCenter);

            // 结果槽
            resultHolder = MakeChild("ResultSlot", slotZone, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.25f));
            AddBg(resultHolder, new Color(0.12f, 0.12f, 0.2f, 0.8f));
            AddOutline(resultHolder, new Color(0.4f, 0.4f, 0.5f));
            MakeLabel("R_Empty", resultHolder, Vector2.zero, Vector2.one, "结果", 14, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
        }

        private void BuildBottomBar()
        {
            var bar = MakeChild("BottomBar", rectTransform, new Vector2(0, 0), new Vector2(1, 0.09f));
            AddBg(bar, BG_PANEL);

            // 金币消耗
            costText = MakeLabel("Cost", bar, new Vector2(0.03f, 0.1f), new Vector2(0.35f, 0.9f), "消耗：50🪙", 14, GOLD, TextAnchor.MiddleLeft);
            // 当前金币
            goldText = MakeLabel("Gold", bar, new Vector2(0.03f, 0.5f), new Vector2(0.35f, 0.95f), "", 11, new Color(0.8f, 0.7f, 0.3f), TextAnchor.MiddleLeft);

            // 合成按钮
            var mergeRt = MakeChild("MergeBtn", bar, new Vector2(0.4f, 0.1f), new Vector2(0.75f, 0.9f));
            AddBg(mergeRt, new Color(0.2f, 0.5f, 0.2f, 0.8f));
            mergeBtn = mergeRt.gameObject.AddComponent<Button>();
            mergeBtn.onClick.AddListener(OnMergeClick);
            mergeBtn.interactable = false;
            MakeLabel("MB", mergeRt, Vector2.zero, Vector2.one, "⚗ 合成", 18, Color.white, TextAnchor.MiddleCenter);

            // 状态提示
            statusText = MakeLabel("Status", bar, new Vector2(0.77f, 0.1f), new Vector2(0.98f, 0.9f), "", 11, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleRight);
        }

        // ════════════════════════ 数据加载 ════════════════════════

        private void LoadData()
        {
            allCards.Clear();
            slotA = null; slotB = null;
            filterRarity = null;
            activeFilterIdx = 0;

            var inventory = PlayerInventory.Instance;
            if (inventory == null) return;

            // 从背包获取所有卡牌
            foreach (var item in inventory.GetAllItems())
            {
                if (item is CardInstance ci && ci.Data != null)
                {
                    // 金卡不可作为素材
                    if (ci.Rarity != CardRarity.Gold)
                        allCards.Add(ci);
                }
            }
        }

        private void ApplyFilter()
        {
            filteredCards.Clear();
            foreach (var ci in allCards)
            {
                if (ci == null || ci.Data == null) continue;
                // 已在槽中的卡牌不再显示
                if (ci == slotA || ci == slotB) continue;
                // 稀有度筛选
                if (filterRarity.HasValue && ci.Rarity != filterRarity.Value) continue;
                filteredCards.Add(ci);
            }
        }

        // ════════════════════════ 刷新渲染 ════════════════════════

        private void Refresh()
        {
            ApplyFilter();
            RefreshList();
            RefreshSlots();
            RefreshPreview();
            RefreshBottomBar();
        }

        private void RefreshList()
        {
            ClearChildren(listZone, 1); // 保留标题label
            listItemRts.Clear();

            if (filteredCards.Count == 0)
            {
                MakeLabel("Empty", listZone, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f),
                    "无可用卡牌", 13, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
                return;
            }

            int cols = Mathf.Max(1, Mathf.FloorToInt((listZone.rect.width - GAP) / (CW + GAP)));
            for (int i = 0; i < filteredCards.Count; i++)
            {
                int col = i % cols, row = i / cols;
                var pos = new Vector2(GAP + col * (CW + GAP), -GAP - row * (CH + GAP));
                CreateListItem(filteredCards[i], listZone, pos, i);
            }
        }

        private void CreateListItem(CardInstance ci, RectTransform container, Vector2 pos, int idx)
        {
            var go = new GameObject("Card_" + ci.CardName);
            go.transform.SetParent(container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CW, CH);
            rt.anchoredPosition = pos;

            var bg = go.AddComponent<Image>();
            bg.color = BG_SLOT;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = RarityColor(ci.Rarity);
            outline.effectDistance = new Vector2(2, -2);

            // 内容
            MakeLabel("Type", rt, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.88f),
                TypeEmoji(ci.Type), 10, Color.white, TextAnchor.MiddleCenter);
            MakeLabel("Name", rt, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.7f),
                ci.CardName, 11, RarityColor(ci.Rarity), TextAnchor.MiddleCenter);
            MakeLabel("Cost", rt, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.4f),
                ci.Cost == 0 ? "免费" : $"{ci.Cost}点", 10, GOLD, TextAnchor.MiddleCenter);
            MakeLabel("Star", rt, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.2f),
                StarStr(ci.StarLevel), 10, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);

            // 点击加入合成槽
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCardSelect(ci));

            // Hover效果
            var trigger = go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (bg != null) bg.color = BG_SLOT_HOVER; });
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (bg != null) bg.color = BG_SLOT; });
            trigger.triggers.Add(exit);

            listItemRts.Add(rt);
        }

        private void RefreshSlots()
        {
            RenderSlot(slotAHolder, slotA, "素材A");
            RenderSlot(slotBHolder, slotB, "素材B");
            ClearSlot(resultHolder, "结果");
        }

        private void RenderSlot(RectTransform holder, CardInstance ci, string emptyLabel)
        {
            ClearChildren(holder, 0);
            if (ci == null || ci.Data == null)
            {
                MakeLabel("Empty", holder, Vector2.zero, Vector2.one, emptyLabel, 13,
                    new Color(0.4f, 0.4f, 0.5f), TextAnchor.MiddleCenter);
                var outline = holder.GetComponent<Outline>();
                if (outline != null) outline.effectColor = new Color(0.3f, 0.3f, 0.4f);
                return;
            }

            var bg = holder.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.15f, 0.15f, 0.22f, 0.9f);

            var outline2 = holder.GetComponent<Outline>();
            if (outline2 != null) outline2.effectColor = RarityColor(ci.Rarity);

            MakeLabel("Type", holder, new Vector2(0.05f, 0.65f), new Vector2(0.3f, 0.9f),
                TypeEmoji(ci.Type), 12, Color.white, TextAnchor.MiddleCenter);
            MakeLabel("Name", holder, new Vector2(0.3f, 0.65f), new Vector2(0.7f, 0.9f),
                ci.CardName, 14, RarityColor(ci.Rarity), TextAnchor.MiddleCenter);
            MakeLabel("Rarity", holder, new Vector2(0.7f, 0.65f), new Vector2(0.95f, 0.9f),
                RarityName(ci.Rarity), 10, RarityColor(ci.Rarity), TextAnchor.MiddleCenter);
            MakeLabel("Desc", holder, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.6f),
                ci.Data.description ?? "", 10, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);

            // 点击移出槽位
            var btn = holder.gameObject.GetComponent<Button>();
            if (btn == null) btn = holder.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => RemoveFromSlot(ci));
        }

        private void ClearSlot(RectTransform holder, string label)
        {
            ClearChildren(holder, 0);
            MakeLabel("Empty", holder, Vector2.zero, Vector2.one, label, 13,
                new Color(0.4f, 0.4f, 0.5f), TextAnchor.MiddleCenter);
        }

        private void RefreshPreview()
        {
            ClearChildren(previewZone, 1); // 保留标题
            previewItemRts.Clear();

            var mergeSys = CardMergeSystem.Instance;
            if (mergeSys == null || slotA == null || slotB == null)
            {
                MakeLabel("Empty", previewZone, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f),
                    "选两张卡牌\n查看预览", 12, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
                return;
            }

            var previews = mergeSys.GetMergePreview(slotA, slotB);
            if (previews == null || previews.Count == 0)
            {
                MakeLabel("None", previewZone, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f),
                    "无法合成", 12, new Color(0.8f, 0.3f, 0.3f), TextAnchor.MiddleCenter);
                return;
            }

            // 显示可能的卡牌（最多8张）
            int cols = Mathf.Max(1, Mathf.FloorToInt((previewZone.rect.width - GAP) / (CW + GAP)));
            int showCount = Mathf.Min(previews.Count, 8);
            for (int i = 0; i < showCount; i++)
            {
                int col = i % cols, row = i / cols;
                var pos = new Vector2(GAP + col * (CW + GAP), -GAP - row * (CH + GAP));
                CreatePreviewItem(previews[i], previewZone, pos);
            }

            if (previews.Count > 8)
            {
                MakeLabel("More", previewZone,
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.1f),
                    $"...等{previews.Count}张", 11, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleCenter);
            }
        }

        private void CreatePreviewItem(CardData data, RectTransform container, Vector2 pos)
        {
            var go = new GameObject("Prev_" + data.cardName);
            go.transform.SetParent(container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CW, CH);
            rt.anchoredPosition = pos;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.2f, 0.8f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = RarityColor(data.rarity);
            outline.effectDistance = new Vector2(1, -1);

            MakeLabel("T", rt, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.88f),
                TypeEmoji(data.cardType), 10, Color.white, TextAnchor.MiddleCenter);
            MakeLabel("N", rt, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.7f),
                data.cardName, 11, RarityColor(data.rarity), TextAnchor.MiddleCenter);

            // 半透明表示"可能"
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0.7f;

            previewItemRts.Add(rt);
        }

        private void RefreshBottomBar()
        {
            var mergeSys = CardMergeSystem.Instance;
            bool canMerge = mergeSys != null && mergeSys.CanMerge(slotA, slotB);

            if (mergeBtn != null)
            {
                mergeBtn.interactable = canMerge && !isAnimating;
                var btnImg = mergeBtn.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = canMerge && !isAnimating
                        ? new Color(0.2f, 0.6f, 0.2f, 0.9f)
                        : new Color(0.25f, 0.25f, 0.25f, 0.7f);
                }
            }

            if (costText != null)
            {
                int cost = mergeSys != null ? mergeSys.CurrentMergeCost : 50;
                costText.text = $"消耗：{cost}🪙";
            }

            if (goldText != null)
            {
                var inv = PlayerInventory.Instance;
                goldText.text = inv != null ? $"持有：{inv.Gold}🪙" : "";
            }

            if (statusText != null)
            {
                if (isAnimating)
                    statusText.text = "合成中...";
                else if (slotA == null && slotB == null)
                    statusText.text = "选择卡牌";
                else if (slotA == null || slotB == null)
                    statusText.text = "再选一张";
                else if (canMerge)
                    statusText.text = "✅ 可合成";
                else
                    statusText.text = "❌ 不匹配";
            }
        }

        // ════════════════════════ 交互逻辑 ════════════════════════

        private void OnFilterClick(int idx)
        {
            activeFilterIdx = idx;
            filterRarity = idx switch
            {
                1 => CardRarity.White,
                2 => CardRarity.Blue,
                3 => CardRarity.Purple,
                _ => null // 0=全部
            };

            // 更新筛选按钮样式
            for (int i = 0; i < filterBtns.Count; i++)
            {
                var img = filterBtns[i]?.GetComponent<Image>();
                if (img != null)
                    img.color = i == idx
                        ? new Color(0.25f, 0.25f, 0.35f, 0.9f)
                        : new Color(0.15f, 0.15f, 0.2f, 0.7f);
            }

            Refresh();
        }

        private void OnCardSelect(CardInstance ci)
        {
            if (isAnimating || ci == null) return;

            // 如果槽A空，放入A
            if (slotA == null)
            {
                slotA = ci;
                // 自动设置稀有度筛选为匹配的
                AutoSetFilter(ci.Rarity);
            }
            // 如果槽B空且稀有度匹配
            else if (slotB == null)
            {
                if (slotA.Rarity == ci.Rarity)
                    slotB = ci;
                else
                {
                    // 稀有度不匹配，替换A
                    slotA = ci;
                    slotB = null;
                    AutoSetFilter(ci.Rarity);
                }
            }
            // 都有卡，替换A
            else
            {
                slotA = ci;
                slotB = null;
                AutoSetFilter(ci.Rarity);
            }

            Refresh();
        }

        private void AutoSetFilter(CardRarity rarity)
        {
            // 自动跳到对应稀有度筛选
            int targetIdx = rarity switch
            {
                CardRarity.White => 1,
                CardRarity.Blue => 2,
                CardRarity.Purple => 3,
                _ => 0
            };
            if (targetIdx != activeFilterIdx)
                OnFilterClick(targetIdx);
        }

        private void RemoveFromSlot(CardInstance ci)
        {
            if (isAnimating) return;
            if (slotA == ci) slotA = null;
            else if (slotB == ci) slotB = null;
            Refresh();
        }

        private void OnMergeClick()
        {
            if (isAnimating) return;
            var mergeSys = CardMergeSystem.Instance;
            if (mergeSys == null || !mergeSys.CanMerge(slotA, slotB)) return;

            isAnimating = true;
            RefreshBottomBar();

            // ── 合成动画序列 ──
            var seq = DOTween.Sequence();
            seq.SetTarget(gameObject);

            // Phase 1: 两张卡飞入中央 → 缩小
            float halfW = slotZone.rect.width * 0.5f;
            float halfH = slotZone.rect.height * 0.5f;

            if (slotAHolder != null)
            {
                seq.Join(slotAHolder.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InQuad));
                seq.Join(slotAHolder.DOLocalRotate(new Vector3(0, 0, 15f), 0.2f));
            }
            if (slotBHolder != null)
            {
                seq.Join(slotBHolder.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.InQuad));
                seq.Join(slotBHolder.DOLocalRotate(new Vector3(0, 0, -15f), 0.2f));
            }

            // Phase 2: 旋转融合
            seq.AppendCallback(() =>
            {
                if (slotAHolder != null)
                {
                    slotAHolder.DOLocalRotate(new Vector3(0, 360, 0), 0.4f, RotateMode.FastBeyond360)
                        .SetEase(Ease.InOutQuad);
                }
                if (slotBHolder != null)
                {
                    slotBHolder.DOLocalRotate(new Vector3(0, -360, 0), 0.4f, RotateMode.FastBeyond360)
                        .SetEase(Ease.InOutQuad);
                }
            });

            // Phase 3: 执行合成
            seq.AppendInterval(0.4f);
            seq.AppendCallback(() =>
            {
                // 执行后端合成
                var result = mergeSys.MergeCards(slotA, slotB);
                if (result != null)
                {
                    RenderResult(result);
                    // 闪光效果
                    PlayFlashEffect();
                }
                else
                {
                    statusText.text = "合成失败";
                }
            });

            // Phase 4: 结果弹出
            seq.AppendInterval(0.3f);
            seq.AppendCallback(() =>
            {
                isAnimating = false;
                slotA = null;
                slotB = null;
                LoadData();
                Refresh();
            });

            seq.Play();
        }

        private void RenderResult(CardInstance result)
        {
            ClearChildren(resultHolder, 0);
            var bg = resultHolder.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);

            var outline = resultHolder.GetComponent<Outline>();
            if (outline != null) outline.effectColor = RarityColor(result.Rarity);

            MakeLabel("Type", resultHolder, new Vector2(0.05f, 0.65f), new Vector2(0.3f, 0.9f),
                TypeEmoji(result.Type), 12, Color.white, TextAnchor.MiddleCenter);
            MakeLabel("Name", resultHolder, new Vector2(0.3f, 0.65f), new Vector2(0.7f, 0.9f),
                result.CardName, 14, RarityColor(result.Rarity), TextAnchor.MiddleCenter);
            MakeLabel("Rarity", resultHolder, new Vector2(0.7f, 0.65f), new Vector2(0.95f, 0.9f),
                RarityName(result.Rarity), 10, RarityColor(result.Rarity), TextAnchor.MiddleCenter);
            MakeLabel("Desc", resultHolder, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.6f),
                result.Data?.description ?? "新卡牌!", 10, new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleLeft);

            // 弹出动画
            resultHolder.localScale = Vector3.zero;
            resultHolder.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        private void PlayFlashEffect()
        {
            var flash = MakeChild("Flash", rectTransform, Vector2.zero, Vector2.one);
            var img = flash.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.95f, 0.6f, 0.6f);
            img.raycastTarget = false;

            var cg = flash.gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (flash != null) Destroy(flash.gameObject);
            });
        }

        // ── 合成完成回调（事件监听） ──
        private void OnMergeComplete(CardInstance result)
        {
            Debug.Log($"[CardMergePanel] 合成完成：{result.CardName}({result.Rarity})");
        }

        // ════════════════════════ 工具方法 ════════════════════════

        private static string StarStr(int lv)
        {
            var s = "";
            for (int i = 0; i < lv; i++) s += "★";
            for (int i = lv; i < 3; i++) s += "☆";
            return s;
        }

        private static RectTransform MakeChild(string name, RectTransform p, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void AddBg(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
        }

        private static void AddOutline(RectTransform rt, Color c)
        {
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = c;
            ol.effectDistance = new Vector2(2, -2);
        }

        private static Text MakeLabel(string n, RectTransform p, Vector2 aMin, Vector2 aMax, string txt, int sz, Color c, TextAnchor a)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = txt;
            t.fontSize = sz;
            t.color = c;
            t.alignment = a;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return t;
        }

        private static void ClearChildren(RectTransform container, int skip = 0)
        {
            for (int i = container.childCount - 1; i >= skip; i--)
            {
                var ch = container.GetChild(i);
                if (ch != null) Destroy(ch.gameObject);
            }
        }
    }
}
