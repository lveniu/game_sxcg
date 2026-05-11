using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    // ═══════════════════════════════════════════════════════════
    // FE-21: 图鉴面板 — 收集进度展示（英雄/卡牌/遗物）
    // 数据接口预留: ICollectionData, 先用 MockCollectionData
    // ═══════════════════════════════════════════════════════════

    public interface ICollectionData
    {
        List<CollectionHeroEntry> GetCollectedHeroes();
        List<CollectionCardEntry> GetCollectedCards();
        List<CollectionRelicEntry> GetCollectedRelics();
        (int collected, int total) GetHeroProgress();
        (int collected, int total) GetCardProgress();
        (int collected, int total) GetRelicProgress();
    }

    public class CollectionHeroEntry
    {
        public string id, name, heroClass, rarity, skillDesc;
        public bool isCollected;
        public int starLevel, heroLevel, maxHealth, attack, defense, speed;
    }

    public class CollectionCardEntry
    {
        public string id, name, cardType, rarity, description;
        public bool isCollected; public int cost;
    }

    public class CollectionRelicEntry
    {
        public string id, name, rarity, effectType, description;
        public bool isCollected; public float effectValue;
    }

    #region Mock — 后端完成后替换
    public class MockCollectionData : ICollectionData
    {
        public List<CollectionHeroEntry> GetCollectedHeroes() => new List<CollectionHeroEntry>
        {
            new CollectionHeroEntry { id="h1", name="铁甲战士", heroClass="Warrior", rarity="Rare",
                isCollected=true, starLevel=3, heroLevel=15, maxHealth=120, attack=35, defense=28, speed=12,
                skillDesc="钢铁意志 — 受到攻击时30%概率减少50%伤害" },
            new CollectionHeroEntry { id="h2", name="奥术法师", heroClass="Mage", rarity="Epic",
                isCollected=true, starLevel=2, heroLevel=8, maxHealth=80, attack=55, defense=10, speed=18,
                skillDesc="奥术风暴 — 释放范围魔法，对所有敌人造成120%攻击力伤害" },
            new CollectionHeroEntry { id="h3", name="暗影刺客", heroClass="Assassin", rarity="Legendary",
                isCollected=false, starLevel=0, heroLevel=0, maxHealth=0, attack=0, defense=0, speed=0, skillDesc="" }
        };
        public List<CollectionCardEntry> GetCollectedCards() => new List<CollectionCardEntry>
        {
            new CollectionCardEntry { id="c1", name="力量强化", cardType="Attribute", rarity="Common", isCollected=true, cost=2, description="提升目标英雄攻击力+15%，持续3回合" },
            new CollectionCardEntry { id="c2", name="火球术", cardType="Skill", rarity="Rare", isCollected=true, cost=3, description="对单个敌人造成80点火焰伤害，附加灼烧效果" },
            new CollectionCardEntry { id="c3", name="进化之心", cardType="Evolution", rarity="Epic", isCollected=true, cost=4, description="使目标英雄立即进化，所有属性+20%" },
            new CollectionCardEntry { id="c4", name="守护祝福", cardType="Support", rarity="Rare", isCollected=true, cost=2, description="为全体队友施加护盾，吸收30点伤害" },
            new CollectionCardEntry { id="c5", name="暗影突袭", cardType="Skill", rarity="Epic", isCollected=false, cost=0, description="" },
            new CollectionCardEntry { id="c6", name="终极进化", cardType="Evolution", rarity="Legendary", isCollected=false, cost=0, description="" }
        };
        public List<CollectionRelicEntry> GetCollectedRelics() => new List<CollectionRelicEntry>
        {
            new CollectionRelicEntry { id="r1", name="守护符文", rarity="Common", isCollected=true, effectType="防御提升", effectValue=10f, description="所有英雄防御力+10" },
            new CollectionRelicEntry { id="r2", name="暴风之眼", rarity="Epic", isCollected=true, effectType="攻击提升", effectValue=25f, description="所有英雄攻击力+25%" },
            new CollectionRelicEntry { id="r3", name="龙心护符", rarity="Legendary", isCollected=true, effectType="生命加成", effectValue=50f, description="所有英雄最大生命值+50，每回合恢复5%生命" },
            new CollectionRelicEntry { id="r4", name="疾风靴", rarity="Rare", isCollected=false, effectType="", effectValue=0f, description="" },
            new CollectionRelicEntry { id="r5", name="命运之刃", rarity="Epic", isCollected=false, effectType="", effectValue=0f, description="" }
        };
        public (int, int) GetHeroProgress() => (2, 3);
        public (int, int) GetCardProgress() => (4, 6);
        public (int, int) GetRelicProgress() => (3, 5);
    }
    #endregion

    /// <summary>图鉴面板 — 三子页Tab（英雄/卡牌/遗物），程序化创建</summary>
    public class CollectionPanel : UIPanel
    {
        const float TITLE_H = 80f, PROG_H = 50f, TAB_H = 60f;
        const float CONTENT_TOP = TITLE_H + PROG_H + TAB_H;
        const float SP = 15f, PAD = 20f;

        static readonly Color C_COMMON = Hex("#808080"), C_RARE = Hex("#4CAF50");
        static readonly Color C_EPIC = Hex("#2196F3"), C_LEG = Hex("#FFD700");
        static readonly Color C_TITLE = Hex("#1a1a2e"), C_TAB_ON = Hex("#2a2a4e"), C_TAB_OFF = Hex("#0f0f1e");
        static readonly Color C_CARD = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        static readonly Color C_SILH = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        static readonly Color C_OVER = new Color(0, 0, 0, 0.7f);

        ICollectionData data;
        RectTransform contentRt;
        readonly List<Image> tabBgs = new List<Image>();
        readonly List<Tweener> tweens = new List<Tweener>();
        readonly List<RectTransform> cards = new List<RectTransform>();
        RectTransform detailOv, detailCard;
        int curTab;

        protected override void Awake() { base.Awake(); slideInAnimation = false; data = new MockCollectionData(); }

        protected override void OnShow()
        {
            KillTweens(); ClearAll();
            BuildTitleBar(); BuildProgressBar(); BuildTabs(); BuildContent();
            curTab = 0; SwitchTab(0);
            rectTransform.anchoredPosition = new Vector2(0f, -1280f);
            tweens.Add(rectTransform.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject));
        }

        protected override void OnHide()
        {
            KillTweens();
            rectTransform.DOAnchorPosY(-1280f, 0.3f).SetEase(Ease.InBack).SetLink(gameObject);
        }

        // ──── 标题栏 ────
        void BuildTitleBar()
        {
            var rt = Mk("TitleBar", rectTransform); AnchTop(rt, TITLE_H, 0);
            Img(rt).color = C_TITLE;
            var t = Mk("T", rt); Fill(t, new Vector2(20, 0), new Vector2(-60, 0));
            var txt = Txt(t, "📖 图鉴", 28, Color.white, TextAnchor.MiddleLeft); txt.fontStyle = FontStyle.Bold;
            var c = Mk("X", rt); c.anchorMin = c.anchorMax = new Vector2(1, 0.5f);
            c.pivot = new Vector2(1, 0.5f); c.sizeDelta = new Vector2(60, 60); c.anchoredPosition = new Vector2(-15, 0);
            Img(c).color = new Color(0.4f, 0.2f, 0.2f, 0.8f);
            c.gameObject.AddComponent<Button>().onClick.AddListener(Hide);
            var lb = Mk("L", c); Fill(lb); Txt(lb, "X", 24, Color.white);
        }

        // ──── 进度条 ────
        void BuildProgressBar()
        {
            var hp = data.GetHeroProgress(); var cp = data.GetCardProgress(); var rp = data.GetRelicProgress();
            int col = hp.collected + cp.collected + rp.collected, tot = hp.total + cp.total + rp.total;
            var rt = Mk("Prog", rectTransform); AnchTop(rt, PROG_H, -TITLE_H);
            Img(rt).color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            var lb = Mk("L", rt); lb.anchorMin = Vector2.zero; lb.anchorMax = Vector2.one;
            lb.offsetMin = new Vector2(20, 15); lb.offsetMax = new Vector2(-20, 0);
            Txt(lb, $"收集进度: {col}/{tot}", 18, Color.white, TextAnchor.MiddleLeft);
            var bg = Mk("Bg", rt); bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = new Vector2(20, 5); bg.offsetMax = new Vector2(-20, -25); Img(bg).color = new Color(0.2f, 0.2f, 0.3f);
            var fl = Mk("F", bg); fl.anchorMin = Vector2.zero; fl.anchorMax = new Vector2(0, 1);
            fl.offsetMin = Vector2.zero; fl.offsetMax = Vector2.zero; Img(fl).color = C_LEG;
            fl.DOAnchorMax(new Vector2(tot > 0 ? (float)col / tot : 0f, 1f), 0.6f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        // ──── Tab栏 ────
        void BuildTabs()
        {
            tabBgs.Clear();
            var hp = data.GetHeroProgress(); var cp = data.GetCardProgress(); var rp = data.GetRelicProgress();
            string[] labels = { $"英雄({hp.collected}/{hp.total})", $"卡牌({cp.collected}/{cp.total})", $"遗物({rp.collected}/{rp.total})" };
            var rt = Mk("Tabs", rectTransform); AnchTop(rt, TAB_H, -TITLE_H - PROG_H);
            Img(rt).color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            float tw = (720f - PAD * 2 - 10) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var t = Mk($"T{i}", rt); t.anchorMin = t.anchorMax = new Vector2(0, 0.5f);
                t.pivot = new Vector2(0.5f, 0.5f); t.sizeDelta = new Vector2(tw, TAB_H - 10);
                t.anchoredPosition = new Vector2(PAD + i * (tw + 5f) + tw / 2f, 0);
                tabBgs.Add(Img(t)); tabBgs[i].color = C_TAB_OFF;
                int idx = i; t.gameObject.AddComponent<Button>().onClick.AddListener(() => SwitchTab(idx));
                var l = Mk("L", t); Fill(l); Txt(l, labels[i], 16, Color.white);
            }
        }

        void SwitchTab(int i) { curTab = i; for (int j = 0; j < tabBgs.Count; j++) tabBgs[j].color = j == i ? C_TAB_ON : C_TAB_OFF; ClearCards();
            if (i == 0) RenderHeroes(); else if (i == 1) RenderCards(); else RenderRelics(); }

        // ──── 内容容器 ────
        void BuildContent()
        {
            var rt = Mk("Ct", rectTransform); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0, -CONTENT_TOP);
            rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = new Vector2(0, -CONTENT_TOP);
            contentRt = rt;
        }

        // ──── 英雄子页 (3列 200×240) ────
        void RenderHeroes()
        {
            var list = data.GetCollectedHeroes();
            GridSize(list.Count, 3, 200f, 240f);
            for (int i = 0; i < list.Count; i++) { var p = GPos(i, 3, 200f, 240f); if (list[i].isCollected) MkHero(list[i], p, i); else MkSil(200f, 240f, p, i); }
        }
        void MkHero(CollectionHeroEntry h, Vector2 p, int idx)
        {
            var rt = Card("H_" + h.id, 200f, 240f, p); Border(rt, h.rarity);
            Lbl(rt, "N", 0, .55f, 1, 1, $"{CI(h.heroClass)} {h.name}", 15, Color.white);
            Lbl(rt, "S", 0, .42f, 1, .6f, new string('★', h.starLevel), 14, C_LEG);
            Lbl(rt, "L", 0, .3f, 1, .48f, $"Lv.{h.heroLevel}", 13, new Color(0.7f, .9f, 1f));
            Lbl(rt, "A", 0, .02f, 1, .35f, $"HP:{h.maxHealth} ATK:{h.attack}\nDEF:{h.defense} SPD:{h.speed}", 11, new Color(.75f, .75f, .8f));
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => DetHero(h));
            AnimIn(rt, idx);
        }
        void DetHero(CollectionHeroEntry h)
        {
            var c = GetDetailCard(); var inn = DetFill(c); float y = 1f;
            DL(inn, ref y, $"{CI(h.heroClass)} {h.name}", 24, Color.white);
            DL(inn, ref y, $"[{h.rarity}] {new string('★', h.starLevel)}", 16, RC(h.rarity));
            DL(inn, ref y, $"职业: {CN(h.heroClass)}  Lv.{h.heroLevel}", 14, new Color(.8f, .8f, .85f));
            DL(inn, ref y, $"HP:{h.maxHealth} ATK:{h.attack}\nDEF:{h.defense} SPD:{h.speed}", 14, new Color(.7f, .9f, 1f));
            DL(inn, ref y, $"技能: {h.skillDesc}", 13, new Color(1f, .9f, .6f));
            Pop(c);
        }

        // ──── 卡牌子页 (3列 200×180) ────
        void RenderCards()
        {
            var list = data.GetCollectedCards();
            GridSize(list.Count, 3, 200f, 180f);
            for (int i = 0; i < list.Count; i++) { var p = GPos(i, 3, 200f, 180f); if (list[i].isCollected) MkCard(list[i], p, i); else MkSil(200f, 180f, p, i); }
        }
        void MkCard(CollectionCardEntry d, Vector2 p, int idx)
        {
            var rt = Card("C_" + d.id, 200f, 180f, p); Border(rt, d.rarity);
            Lbl(rt, "N", 0, .55f, 1, 1, d.name, 16, Color.white);
            Lbl(rt, "C", 0, .38f, 1, .6f, $"费用: {d.cost}", 13, new Color(1f, .85f, .4f));
            Lbl(rt, "T", 0, .18f, 1, .42f, $"类型: {d.cardType}", 12, new Color(.7f, .8f, .9f));
            Lbl(rt, "R", 0, 0, 1, .22f, $"[{d.rarity}]", 12, RC(d.rarity));
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => DetCard(d));
            AnimIn(rt, idx);
        }
        void DetCard(CollectionCardEntry d)
        {
            var c = GetDetailCard(); var inn = DetFill(c); float y = 1f;
            DL(inn, ref y, d.name, 24, Color.white);
            DL(inn, ref y, $"[{d.rarity}]  类型: {d.cardType}  费用: {d.cost}", 14, RC(d.rarity));
            DL(inn, ref y, d.description, 14, new Color(.85f, .85f, .9f));
            Pop(c);
        }

        // ──── 遗物子页 (2列 320×150) ────
        void RenderRelics()
        {
            var list = data.GetCollectedRelics();
            GridSize(list.Count, 2, 320f, 150f);
            for (int i = 0; i < list.Count; i++) { var p = GPos(i, 2, 320f, 150f); if (list[i].isCollected) MkRelic(list[i], p, i); else MkSil(320f, 150f, p, i); }
        }
        void MkRelic(CollectionRelicEntry r, Vector2 p, int idx)
        {
            var rt = Card("R_" + r.id, 320f, 150f, p); Border(rt, r.rarity);
            Lbl(rt, "N", 0, .5f, 1, 1, r.name, 16, Color.white, 10f);
            Lbl(rt, "E", 0, .15f, 1, .5f, $"效果: {r.effectType} +{r.effectValue}", 12, new Color(.7f, .9f, 1f), 10f);
            Lbl(rt, "R", 0, 0, 1, .2f, $"[{r.rarity}]", 12, RC(r.rarity), 10f);
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => DetRelic(r));
            AnimIn(rt, idx);
        }
        void DetRelic(CollectionRelicEntry r)
        {
            var c = GetDetailCard(); var inn = DetFill(c); float y = 1f;
            DL(inn, ref y, r.name, 24, Color.white);
            DL(inn, ref y, $"[{r.rarity}]", 16, RC(r.rarity));
            DL(inn, ref y, $"效果: {r.effectType} +{r.effectValue}", 14, new Color(.7f, .9f, 1f));
            DL(inn, ref y, r.description, 14, new Color(.85f, .85f, .9f));
            Pop(c);
        }

        // ──── 剪影 ────
        void MkSil(float w, float h, Vector2 p, int idx)
        {
            var rt = Card("Sil", w, h, p); rt.GetComponent<Image>().color = C_SILH;
            var ov = Mk("O", rt); Fill(ov); Img(ov).color = C_OVER;
            Lbl(rt, "Q", 0, .4f, 1, .75f, "???", 28, new Color(.5f, .5f, .55f));
            Lbl(rt, "H", 0, .1f, 1, .4f, "继续探索以解锁", 12, new Color(.4f, .4f, .45f));
            AnimIn(rt, idx);
        }

        // ──── 详情弹窗 ────
        RectTransform GetDetailCard()
        {
            if (detailCard != null) return detailCard;
            var rt = Mk("DC", rectTransform); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, 500f); Img(rt).color = new Color(0.1f, 0.1f, 0.18f, 0.98f);
            detailCard = rt; return rt;
        }
        RectTransform DetFill(RectTransform card)
        {
            var old = card.Find("I"); if (old != null) Destroy(old.gameObject);
            var rt = Mk("I", card); Fill(rt, new Vector2(15, 15), new Vector2(-15, -15));
            Img(rt).color = C_CARD; return rt;
        }
        void DL(Transform p, ref float y, string text, int sz, Color c)
        {
            y -= 0.12f; var rt = Mk("L", p); rt.anchorMin = new Vector2(0, y); rt.anchorMax = new Vector2(1, y + 0.12f);
            rt.offsetMin = new Vector2(10, -12); rt.offsetMax = new Vector2(-10, 0);
            var t = Txt(rt, text, sz, c, sz >= 24 ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);
            if (sz >= 24) t.fontStyle = FontStyle.Bold;
        }
        void Pop(RectTransform card)
        {
            if (detailOv == null) { var o = Mk("DO", rectTransform); Fill(o); Img(o).color = new Color(0, 0, 0, 0.6f);
                o.gameObject.AddComponent<Button>().onClick.AddListener(ClosePop); detailOv = o; }
            detailOv.gameObject.SetActive(true); detailOv.SetAsLastSibling();
            card.SetAsLastSibling(); card.gameObject.SetActive(true); card.localScale = Vector3.zero;
            tweens.Add(card.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject));
        }
        void ClosePop()
        {
            if (detailCard != null && detailCard.gameObject.activeSelf)
                tweens.Add(detailCard.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetLink(gameObject)
                    .OnComplete(() => { if (detailCard != null) detailCard.gameObject.SetActive(false); }));
            if (detailOv != null) detailOv.gameObject.SetActive(false);
        }

        // ──── 网格 ────
        void GridSize(int n, int cols, float cw, float ch) { contentRt.sizeDelta = new Vector2(720f, Mathf.CeilToInt(n / (float)cols) * (ch + SP) + PAD * 2); }
        Vector2 GPos(int i, int cols, float cw, float ch)
        {
            float x = PAD + (i % cols) * (cw + SP) + cw / 2f; int r = i / cols;
            return new Vector2(x, -PAD - r * (ch + SP) - ch / 2f);
        }
        RectTransform Card(string name, float w, float h, Vector2 p)
        {
            var rt = Mk(name, contentRt); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = p;
            Img(rt).color = C_CARD; cards.Add(rt); return rt;
        }
        void Border(RectTransform c, string r) { var rt = Mk("B", c); Fill(rt, new Vector2(-2, -2), new Vector2(2, 2)); Img(rt).color = RC(r); rt.SetAsFirstSibling(); }
        void AnimIn(RectTransform rt, int i) { rt.localScale = Vector3.zero; tweens.Add(rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(i * 0.05f).SetLink(gameObject)); }

        // ──── 清理 ────
        void ClearCards() { foreach (var c in cards) if (c != null) Destroy(c.gameObject); cards.Clear();
            if (detailOv != null) { Destroy(detailOv.gameObject); detailOv = null; }
            if (detailCard != null) { Destroy(detailCard.gameObject); detailCard = null; } }
        void ClearAll() { ClearCards(); for (int i = rectTransform.childCount - 1; i >= 0; i--) Destroy(rectTransform.GetChild(i).gameObject);
            tabBgs.Clear(); contentRt = null; }
        void KillTweens() { foreach (var t in tweens) if (t != null && t.IsActive()) t.Kill(); tweens.Clear(); }

        // ──── UI工具 ────
        static RectTransform Mk(string n, Transform p) { var g = new GameObject(n); g.transform.SetParent(p, false); return g.AddComponent<RectTransform>(); }
        static void Fill(RectTransform r, Vector2? mn = null, Vector2? mx = null) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = mn ?? Vector2.zero; r.offsetMax = mx ?? Vector2.zero; }
        static void AnchTop(RectTransform r, float h, float y) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1f); r.sizeDelta = new Vector2(0, h); r.anchoredPosition = new Vector2(0, y); }
        static Image Img(RectTransform r) { return r.gameObject.AddComponent<Image>(); }
        static Text Txt(RectTransform r, string t, int s, Color c, TextAnchor a = TextAnchor.MiddleCenter)
        { var x = r.gameObject.AddComponent<Text>(); x.text = t; x.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); x.fontSize = s; x.color = c; x.alignment = a; return x; }
        void Lbl(RectTransform p, string n, float a0, float a1, float a2, float a3, string t, int s, Color c, float pad = 5f)
        { var r = Mk(n, p); r.anchorMin = new Vector2(a0, a1); r.anchorMax = new Vector2(a2, a3); r.offsetMin = new Vector2(pad, 0); r.offsetMax = new Vector2(-pad, 0); Txt(r, t, s, c); }

        // ──── 辅助 ────
        static Color RC(string r) { return r == "Rare" ? C_RARE : r == "Epic" ? C_EPIC : r == "Legendary" ? C_LEG : C_COMMON; }
        static string CI(string c) { return c == "Warrior" ? "⚔" : c == "Mage" ? "🔮" : c == "Assassin" ? "🗡" : "?"; }
        static string CN(string c) { return c == "Warrior" ? "战士" : c == "Mage" ? "法师" : c == "Assassin" ? "刺客" : c; }
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }
    }
}
