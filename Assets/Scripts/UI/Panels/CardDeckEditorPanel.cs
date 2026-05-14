using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    // ── 卡牌UI条目 ────────────────────────────────────────
    /// <summary>
    /// 卡组编辑面板中的卡牌条目，持有对 CardInstance 的引用
    /// </summary>
    public class DeckCardEntry
    {
        public string name, description, rarity, cardType;
        public int cost, starLevel;
        public bool isInDeck;
        public RectTransform rect;
        public CanvasGroup canvasGroup;
        public Image bgImage;
        public Outline outline;

        /// <summary>关联的真实卡牌实例（用于与 CardDeck 交互）</summary>
        public CardInstance cardInstance;
    }

    // ── 卡组编辑面板 ──────────────────────────────────────
    /// <summary>
    /// FE-15 卡组编辑面板 — 拖拽排列/加入移除/Tooltip/数量限制/稀有度视觉
    /// 720x1280竖屏：上方=当前卡组(已选) 下方=卡池(未选) 中间分隔线
    /// </summary>
    public class CardDeckEditorPanel : UIPanel, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const int MAX_DECK = 12;
        const float CW = 90f, CH = 120f, GAP = 10f, DRAG_SCALE = 1.2f;

        private readonly List<DeckCardEntry> deckCards = new List<DeckCardEntry>();
        private readonly List<DeckCardEntry> poolCards = new List<DeckCardEntry>();

        private RectTransform deckZone, poolZone;
        private Text countText;
        private RectTransform tooltipPanel;
        private Text ttName, ttDesc, ttRarity, ttType, ttCost;

        // 拖拽状态
        private DeckCardEntry dragEntry;
        private Vector2 dragStartPos;
        private bool isDragging;

        protected override void Awake()
        {
            base.Awake();
            panelId = "CardDeckEditor";
            BuildUI();
        }

        protected override void OnShow() { LoadData(); Refresh(); }
        protected override void OnHide() { HideTooltip(); DOTween.Kill(gameObject); }

        // ════════════════════════ UI构建 ════════════════════════

        private void BuildUI()
        {
            // 标题栏
            var bar = MakeChild("TitleBar", rectTransform, new Vector2(0,0.92f), new Vector2(1,1));
            AddBg(bar, new Color(0.12f,0.12f,0.18f,0.9f));
            MakeLabel("Title", bar, new Vector2(0.05f,0), new Vector2(0.55f,1), "🃏 卡组编辑", 20, Color.white, TextAnchor.MiddleLeft);
            countText = MakeLabel("Count", bar, new Vector2(0.55f,0), new Vector2(0.78f,1), "0/12", 16, new Color(1f,0.85f,0.3f), TextAnchor.MiddleCenter);
            var closeRt = MakeChild("CloseBtn", bar, new Vector2(0.85f,0.15f), new Vector2(0.98f,0.85f));
            AddBg(closeRt, new Color(0.6f,0.2f,0.2f));
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(Hide);
            MakeLabel("X", closeRt, Vector2.zero, Vector2.one, "✕", 18, Color.white, TextAnchor.MiddleCenter);

            // 卡组区（上方）
            deckZone = MakeChild("DeckZone", rectTransform, new Vector2(0,0.48f), new Vector2(1,0.91f));
            AddBg(deckZone, new Color(0.1f,0.1f,0.15f,0.6f));
            MakeLabel("DL", deckZone, new Vector2(0,0.9f), new Vector2(1,1), "已选卡牌（点击移出·长按拖拽重排）", 12, new Color(0.7f,0.7f,0.8f), TextAnchor.MiddleCenter);

            // 分隔线
            var div = MakeChild("Div", rectTransform, new Vector2(0.05f,0.45f), new Vector2(0.95f,0.48f));
            AddBg(div, new Color(0.3f,0.3f,0.4f,0.6f));
            MakeLabel("DT", div, Vector2.zero, Vector2.one, "─── 卡池（可加入的卡牌）───", 13, new Color(0.6f,0.6f,0.7f), TextAnchor.MiddleCenter);

            // 卡池区（下方）
            poolZone = MakeChild("PoolZone", rectTransform, new Vector2(0,0), new Vector2(1,0.44f));
            AddBg(poolZone, new Color(0.08f,0.08f,0.12f,0.6f));
            MakeLabel("PL", poolZone, new Vector2(0,0.9f), new Vector2(1,1), "点击卡牌加入卡组", 12, new Color(0.6f,0.6f,0.7f), TextAnchor.MiddleCenter);

            // Tooltip
            BuildTooltip();
        }

        private void BuildTooltip()
        {
            tooltipPanel = MakeChild("TT", rectTransform, Vector2.zero, Vector2.zero);
            tooltipPanel.pivot = tooltipPanel.anchorMin = tooltipPanel.anchorMax = new Vector2(0.5f,0.5f);
            tooltipPanel.sizeDelta = new Vector2(260,170);
            tooltipPanel.gameObject.SetActive(false);
            AddBg(tooltipPanel, new Color(0.12f,0.12f,0.2f,0.95f));
            var ol = tooltipPanel.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0.4f,0.4f,0.6f);
            ol.effectDistance = new Vector2(2,-2);
            tooltipPanel.gameObject.AddComponent<CanvasGroup>();

            ttName  = MakeLabel("N", tooltipPanel, new Vector2(0.05f,0.78f), new Vector2(0.95f,0.95f), "", 16, Color.white, TextAnchor.MiddleCenter);
            ttRarity= MakeLabel("R", tooltipPanel, new Vector2(0.05f,0.62f), new Vector2(0.95f,0.76f), "", 12, Color.yellow, TextAnchor.MiddleCenter);
            ttType  = MakeLabel("T", tooltipPanel, new Vector2(0.05f,0.48f), new Vector2(0.95f,0.6f), "", 12, new Color(0.7f,0.8f,1f), TextAnchor.MiddleCenter);
            ttCost  = MakeLabel("C", tooltipPanel, new Vector2(0.05f,0.34f), new Vector2(0.95f,0.46f), "", 12, new Color(1f,0.85f,0.3f), TextAnchor.MiddleCenter);
            ttDesc  = MakeLabel("D", tooltipPanel, new Vector2(0.05f,0.02f), new Vector2(0.95f,0.32f), "", 12, new Color(0.85f,0.85f,0.85f), TextAnchor.MiddleLeft);
        }

        // ════════════════════════ 数据加载 ════════════════════════

        private void LoadData()
        {
            deckCards.Clear(); poolCards.Clear();
            ClearChildren(deckZone, 1); ClearChildren(poolZone, 1);

            var deck = CardDeck.Instance;
            if (deck == null)
            {
                Debug.LogWarning("[CardDeckEditorPanel] CardDeck 未就绪，无法加载卡组数据");
                return;
            }

            // 确保卡池已从 PlayerInventory 同步
            deck.SyncFromInventory();

            // 加载已加入卡组的卡牌
            foreach (var ci in deck.handCards)
            {
                if (ci == null) continue;
                var e = CreateEntryFromInstance(ci, isInDeck: true);
                deckCards.Add(e);
            }

            // 加载卡池（未加入卡组的卡牌）
            foreach (var ci in deck.cardPool)
            {
                if (ci == null) continue;
                var e = CreateEntryFromInstance(ci, isInDeck: false);
                poolCards.Add(e);
            }
        }

        /// <summary>
        /// 从 CardInstance 创建 DeckCardEntry
        /// </summary>
        private static DeckCardEntry CreateEntryFromInstance(CardInstance ci, bool isInDeck)
        {
            return new DeckCardEntry
            {
                name = ci.CardName,
                description = ci.Data.description ?? "",
                rarity = ci.Data.rarity.ToString(),
                cardType = ci.Data.cardType.ToString(),
                cost = ci.Cost,
                starLevel = ci.StarLevel,
                isInDeck = isInDeck,
                cardInstance = ci
            };
        }

        // ════════════════════════ 刷新渲染 ════════════════════════

        private void Refresh()
        {
            ClearChildren(deckZone, 1); ClearChildren(poolZone, 1);
            LayoutCards(deckCards, deckZone);
            LayoutCards(poolCards, poolZone);
            UpdateCount();
        }

        private void LayoutCards(List<DeckCardEntry> cards, RectTransform container)
        {
            int cols = Mathf.Max(1, Mathf.FloorToInt((container.rect.width - GAP) / (CW + GAP)));
            for (int i = 0; i < cards.Count; i++)
            {
                int col = i % cols, row = i / cols;
                var pos = new Vector2(GAP + col*(CW+GAP), -GAP - row*(CH+GAP));
                CreateCardSlot(cards[i], container, pos);
            }
        }

        private void CreateCardSlot(DeckCardEntry e, RectTransform container, Vector2 pos)
        {
            var go = new GameObject("Card_"+e.name);
            go.transform.SetParent(container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0,1);
            rt.anchorMin = rt.anchorMax = new Vector2(0,1);
            rt.sizeDelta = new Vector2(CW, CH);
            rt.anchoredPosition = pos;

            e.bgImage = go.AddComponent<Image>();
            e.bgImage.color = e.isInDeck ? new Color(0.15f,0.15f,0.22f,0.9f) : new Color(0.12f,0.12f,0.18f,0.85f);

            e.outline = go.AddComponent<Outline>();
            e.outline.effectColor = RarityColor(e.rarity);
            e.outline.effectDistance = new Vector2(2,-2);

            e.canvasGroup = go.AddComponent<CanvasGroup>();
            e.rect = rt;

            // 卡牌内容
            MakeLabel("Type", rt, new Vector2(0.05f,0.72f), new Vector2(0.95f,0.88f), TypeEmoji(e.cardType), 10, TypeColor(e.cardType), TextAnchor.MiddleCenter);
            MakeLabel("Name", rt, new Vector2(0.05f,0.45f), new Vector2(0.95f,0.7f), e.name, 12, RarityColor(e.rarity), TextAnchor.MiddleCenter);
            MakeLabel("Cost", rt, new Vector2(0.05f,0.28f), new Vector2(0.95f,0.43f), e.cost==0?"免费":$"{e.cost}点", 10, new Color(1f,0.85f,0.3f), TextAnchor.MiddleCenter);
            MakeLabel("Star", rt, new Vector2(0.05f,0.12f), new Vector2(0.95f,0.26f), StarStr(e.starLevel), 10, new Color(1f,0.8f,0.2f), TextAnchor.MiddleCenter);

            // 点击
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCardClick(e));

            // 卡池满时灰化
            if (!e.isInDeck && deckCards.Count >= MAX_DECK)
            {
                e.canvasGroup.alpha = 0.4f;
                btn.interactable = false;
            }

            // 右键/长按显示Tooltip
            var trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry{ eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => { tooltipTarget = e; tooltipTimer = 0; });
            trigger.triggers.Add(entry);
            var entryUp = new EventTrigger.Entry{ eventID = EventTriggerType.PointerUp };
            entryUp.callback.AddListener(_ => { tooltipTarget = null; if(tooltipShown) HideTooltip(); });
            trigger.triggers.Add(entryUp);

            ApplyRarityFx(e);
        }

        private void UpdateCount()
        {
            if (countText == null) return;
            countText.text = $"{deckCards.Count}/{MAX_DECK}";
            countText.color = deckCards.Count >= MAX_DECK ? new Color(1f,0.3f,0.3f) : new Color(1f,0.85f,0.3f);
        }

        // ════════════════════════ 点击交互 ════════════════════════

        private void OnCardClick(DeckCardEntry e)
        {
            if (isDragging) return;
            var deck = CardDeck.Instance;
            if (deck == null || e.cardInstance == null) return;

            if (e.isInDeck)
            {
                deck.RemoveFromDeck(e.cardInstance);
                e.isInDeck = false;
                deckCards.Remove(e);
                poolCards.Add(e);
            }
            else
            {
                if (deckCards.Count >= MAX_DECK) return;
                deck.AddToDeck(e.cardInstance);
                e.isInDeck = true;
                poolCards.Remove(e);
                deckCards.Add(e);
            }
            Refresh();
        }

        // ════════════════════════ 拖拽系统 ════════════════════════

        public void OnBeginDrag(PointerEventData ev)
        {
            var e = FindEntryAt(ev); if (e==null||!e.isInDeck) return;
            dragEntry = e;
            dragStartPos = e.rect.anchoredPosition;
            isDragging = true;
            e.rect.SetAsLastSibling();
            e.rect.DOScale(Vector3.one*DRAG_SCALE, 0.1f).SetEase(Ease.OutQuad);
            e.canvasGroup.DOFade(0.7f, 0.1f);
            e.canvasGroup.blocksRaycasts = false;
            HighlightZone(deckZone, true);
        }

        public void OnDrag(PointerEventData ev)
        {
            if (dragEntry==null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(deckZone, ev.position, ev.pressEventCamera, out var lp);
            dragEntry.rect.anchoredPosition = lp;
        }

        public void OnEndDrag(PointerEventData ev)
        {
            if (dragEntry==null) return;
            HighlightZone(deckZone, false);

            if (RectTransformUtility.RectangleContainsScreenPoint(deckZone, ev.position, ev.pressEventCamera))
            {
                int idx = FindInsertIdx(ev.position);
                deckCards.Remove(dragEntry);
                if (idx>=0&&idx<deckCards.Count) deckCards.Insert(idx, dragEntry);
                else deckCards.Add(dragEntry);
                dragEntry.rect.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                dragEntry.rect.DOAnchorPos(dragStartPos, 0.2f).SetEase(Ease.OutBack);
                dragEntry.rect.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            dragEntry.canvasGroup.DOFade(1f, 0.2f);
            dragEntry.canvasGroup.blocksRaycasts = true;
            dragEntry = null; isDragging = false;
            Refresh();
        }

        private int FindInsertIdx(Vector2 screenPos)
        {
            for (int i=0; i<deckCards.Count; i++)
            {
                if (deckCards[i]==dragEntry||deckCards[i].rect==null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(deckCards[i].rect, screenPos, null)) return i;
            }
            return -1;
        }

        private DeckCardEntry FindEntryAt(PointerEventData ev)
        {
            foreach (var e in deckCards)
                if (e.rect!=null && RectTransformUtility.RectangleContainsScreenPoint(e.rect, ev.position, ev.pressEventCamera)) return e;
            return null;
        }

        private void HighlightZone(RectTransform zone, bool on)
        {
            var img = zone?.GetComponent<Image>(); if (img==null) return;
            img.DOKill();
            img.DOColor(on ? new Color(0.2f,0.25f,0.35f,0.8f) : new Color(0.1f,0.1f,0.15f,0.6f), 0.15f);
        }

        // ════════════════════════ Tooltip ════════════════════════

        private DeckCardEntry tooltipTarget;
        private bool tooltipShown;
        private float tooltipTimer;
        private const float TOOLTIP_DELAY = 0.5f;

        private void Update()
        {
            // 长按检测
            if (tooltipTarget != null && !tooltipShown)
            {
                tooltipTimer += Time.unscaledDeltaTime;
                if (tooltipTimer >= TOOLTIP_DELAY) { ShowTooltip(tooltipTarget); tooltipShown = true; }
            }
        }

        private void ShowTooltip(DeckCardEntry e)
        {
            if (tooltipPanel==null) return;
            tooltipPanel.gameObject.SetActive(true);
            ttName.text = e.name;
            ttRarity.text = RarityLabel(e.rarity);
            ttRarity.color = RarityColor(e.rarity);
            ttType.text = "类型："+TypeLabel(e.cardType);
            ttCost.text = e.cost==0 ? "消耗：免费" : $"消耗：{e.cost}点";
            ttDesc.text = e.description;
            tooltipPanel.anchoredPosition = Vector2.zero;
            var cg = tooltipPanel.GetComponent<CanvasGroup>();
            if (cg!=null) { cg.alpha=0f; cg.DOFade(1f,0.15f); }
            tooltipPanel.localScale = Vector3.one*0.9f;
            tooltipPanel.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack);
        }

        private void HideTooltip()
        {
            if (tooltipPanel!=null) tooltipPanel.gameObject.SetActive(false);
            tooltipShown = false; tooltipTarget = null; tooltipTimer = 0;
        }

        // ════════════════════════ 稀有度视觉 ════════════════════════

        private void ApplyRarityFx(DeckCardEntry e)
        {
            if (e.rect==null) return;
            switch (e.rarity)
            {
                case "Rare": // 精良=绿边+微光
                    if (e.bgImage!=null)
                    { var c=e.bgImage.color; e.bgImage.DOColor(new Color(c.r+0.03f,c.g+0.06f,c.b+0.03f,c.a),1.2f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine); }
                    break;
                case "Epic": // 史诗=紫边+流光
                    if (e.outline!=null)
                    { var c=e.outline.effectColor; DOTween.To(()=>e.outline.effectColor,v=>e.outline.effectColor=v,new Color(c.r+0.3f,c.g+0.1f,c.b+0.3f),0.8f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine).SetTarget(e.rect); }
                    e.rect.DOScale(Vector3.one*1.03f,1.5f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine);
                    break;
                case "Legendary": // 传说=金边+脉冲
                    if (e.bgImage!=null)
                    { var c=e.bgImage.color; e.bgImage.DOColor(new Color(c.r+0.1f,c.g+0.08f,c.b,c.a),0.6f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine); }
                    e.rect.DOScale(Vector3.one*1.05f,0.8f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine);
                    break;
            }
        }

        // ════════════════════════ 工具方法 ════════════════════════

        // FE-21: 统一稀有度色标 Common=#808080 Rare=#4FC3F7 Epic=#AB47BC Legendary=#FFD740
        private static Color RarityColor(string r) => r switch
        {
            "Common"=>new Color(0.50f,0.50f,0.50f), "Rare"=>new Color(0.31f,0.76f,0.97f),
            "Epic"=>new Color(0.67f,0.28f,0.74f), "Legendary"=>new Color(1.00f,0.84f,0.25f), _=>Color.gray
        };
        private static string RarityLabel(string r) => r switch
        {
            "Common"=>"● 普通","Rare"=>"● 精良","Epic"=>"● 史诗","Legendary"=>"● 传说",_=>r
        };
        private static Color TypeColor(string t) => t switch
        {
            "Battle"=>new Color(1f,0.4f,0.2f),"Attribute"=>new Color(0.3f,0.9f,0.3f),
            "Evolution"=>new Color(0.8f,0.3f,1f),"Hero"=>new Color(0.2f,0.6f,1f),_=>Color.white
        };
        private static string TypeEmoji(string t) => t switch
        {
            "Battle"=>"⚔战斗","Attribute"=>"📖属性","Evolution"=>"✨进化","Hero"=>"🦸英雄",_=>t
        };
        private static string TypeLabel(string t) => t switch
        {
            "Battle"=>"战斗卡","Attribute"=>"属性卡","Evolution"=>"进化卡","Hero"=>"英雄卡",_=>t
        };
        private static string StarStr(int lv) { var s=""; for(int i=0;i<lv;i++) s+="★"; for(int i=lv;i<3;i++) s+="☆"; return s; }

        private static RectTransform MakeChild(string name, RectTransform p, Vector2 aMin, Vector2 aMax)
        { var go=new GameObject(name); go.transform.SetParent(p,false); var rt=go.AddComponent<RectTransform>(); rt.anchorMin=aMin; rt.anchorMax=aMax; rt.offsetMin=rt.offsetMax=Vector2.zero; return rt; }

        private static void AddBg(RectTransform rt, Color c) { var img=rt.gameObject.AddComponent<Image>(); img.color=c; }

        private static Text MakeLabel(string n, RectTransform p, Vector2 aMin, Vector2 aMax, string txt, int sz, Color c, TextAnchor a)
        { var go=new GameObject(n); go.transform.SetParent(p,false); var rt=go.AddComponent<RectTransform>(); rt.anchorMin=aMin; rt.anchorMax=aMax; rt.offsetMin=rt.offsetMax=Vector2.zero;
          var t=go.AddComponent<Text>(); t.text=txt; t.fontSize=sz; t.color=c; t.alignment=a; t.font=Resources.GetBuiltinResource<Font>("Arial.ttf"); return t; }

        private static void ClearChildren(RectTransform container, int skip=0)
        { for(int i=container.childCount-1;i>=skip;i--) { var ch=container.GetChild(i); if(ch!=null) Object.Destroy(ch.gameObject); } }
    }
}
