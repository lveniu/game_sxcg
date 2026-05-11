// FE-20: 成就面板 — 对接后端 AchievementManager 单例 | 竖屏720x1280
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    public class AchievementPanel : UIPanel
    {
        // ==================== 颜色 & 常量 ====================
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static readonly Color HDR_BG = Hex("#1a1a2e"), CARD_BG = new(.12f, .12f, .18f, .92f);
        static readonly Color BD_DONE = Hex("#4CAF50"), BD_CLAIM = Hex("#FFD700"), BD_PROG = Hex("#2196F3"), BD_HIDE = Hex("#555555");
        static readonly Color TAB_ACT = new(.25f, .55f, .95f), TAB_NRM = new(.2f, .2f, .25f, .9f);
        static readonly Color BAR_BG = new(.2f, .2f, .25f), BAR_FILL = new(.2f, .55f, .95f);
        static readonly Color RARITY_COMMON = Hex("#FFFFFF"), RARITY_RARE = Hex("#4A9EFF");
        static readonly Color RARITY_EPIC = Hex("#A855F7"), RARITY_LEGENDARY = Hex("#FFB800");

        static readonly Dictionary<string, Color> ICON_CLR = new()
        {
            ["sword"] = Hex("#E64A4A"), ["skull"] = Hex("#808080"), ["dice"] = Hex("#E6B82B"),
            ["gem"] = Hex("#66B3FF"), ["clock"] = Hex("#4DE680"), ["dragon"] = Hex("#CC33CC"),
            ["fire"] = Hex("#FF8019"), ["shield"] = Hex("#B3B3E6"), ["star"] = Hex("#FFD933"),
            ["coin"] = Hex("#FFCC19"), ["map"] = Hex("#996633"), ["trophy"] = Hex("#FFD700"),
            ["chest"] = Hex("#CD853F"), ["heart"] = Hex("#FF6B6B"), ["lightning"] = Hex("#FFD700"),
            ["reroll"] = Hex("#4DE680"), ["combo"] = Hex("#FF8019")
        };

        // 后端分类: progress / combat / collection / dice / special
        static readonly string[] CATS = { "all", "progress", "combat", "collection", "dice", "special" };
        static readonly string[] CAT_LABELS = { "全部", "冒险", "战斗", "收集", "骰子", "特殊" };

        static Color RarityColor(string rarity) => rarity?.ToLower() switch
        {
            "rare" => RARITY_RARE,
            "epic" => RARITY_EPIC,
            "legendary" => RARITY_LEGENDARY,
            _ => RARITY_COMMON
        };

        static string RarityEmoji(string rarity) => rarity?.ToLower() switch
        {
            "rare" => "🔵",
            "epic" => "🟣",
            "legendary" => "🟡",
            _ => "⚪"
        };

        static Font DefFont => Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ==================== 状态 ====================
        Text summaryText;
        readonly List<Button> tabButtons = new();
        RectTransform listContainer;
        string currentCategory = "all";

        // ==================== 生命周期 ====================
        protected override void Awake()
        {
            base.Awake();
            panelId = "Achievement";
            slideInAnimation = false;
            BuildUI();
        }

        protected override void OnShow()
        {
            // Tab 按钮
            tabButtons.ForEach(b => b.onClick.RemoveAllListeners());
            for (int i = 0; i < CATS.Length; i++)
            {
                string cat = CATS[i];
                tabButtons[i].onClick.AddListener(() => OnCategorySelected(cat));
            }

            // 订阅后端事件
            var am = AchievementManager.Instance;
            if (am != null)
            {
                am.OnAchievementUnlocked += OnAchievementUnlocked;
                am.OnProgressChanged += OnProgressChanged;
                am.OnRewardsClaimed += OnRewardsClaimed;
            }

            RefreshSummary();
            currentCategory = "all";
            HighlightTab("all");
            RefreshList("all");

            // 入场：从底部滑入
            rectTransform.anchoredPosition = new Vector2(0, -Screen.height);
            TrackTween(rectTransform.DOAnchorPosY(0, 0.4f).SetEase(Ease.OutBack));
        }

        protected override void OnHide()
        {
            // 取消事件订阅
            var am = AchievementManager.Instance;
            if (am != null)
            {
                am.OnAchievementUnlocked -= OnAchievementUnlocked;
                am.OnProgressChanged -= OnProgressChanged;
                am.OnRewardsClaimed -= OnRewardsClaimed;
            }

            tabButtons.ForEach(b => b.onClick.RemoveAllListeners());
            KillAllActiveTweens();
        }

        // ==================== UI构建 ====================
        void BuildUI()
        {
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(.08f, .08f, .12f, .97f);
            bg.raycastTarget = true;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;

            // 居中640宽内容区
            var content = CreateChild("Content", transform);
            var cr = content.Rect();
            cr.anchorMin = new(.5f, 0); cr.anchorMax = new(.5f, 1); cr.pivot = new(.5f, 1);
            cr.sizeDelta = new(640, 0); cr.offsetMin = cr.offsetMax = Vector2.zero;

            float y = 0;
            y = BuildHeader(content, y);
            y = BuildSummary(content, y);
            y = BuildTabs(content, y);
            BuildScrollView(content, y);
        }

        float BuildHeader(GameObject parent, float y)
        {
            var (go, rt) = MakeSection("Header", parent, 64, y);
            go.AddComponent<Image>().color = HDR_BG;
            MakeText("Title", go, (.05f, 0, .8f, 1), "🏆 成就", 26, Color.white, TextAnchor.MiddleLeft, bold: true);
            // 关闭按钮
            var close = CreateChild("CloseBtn", go.transform);
            var crt = close.Rect(); crt.anchorMin = new(.85f, .15f); crt.anchorMax = new(.95f, .85f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            close.AddComponent<Image>().color = new(.7f, .7f, .75f);
            var btn = close.AddComponent<Button>(); btn.targetGraphic = close.GetComponent<Image>();
            var txt = close.AddComponent<Text>(); txt.text = "✕"; txt.font = DefFont; txt.fontSize = 22;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            btn.onClick.AddListener(Hide);
            return y + 64;
        }

        float BuildSummary(GameObject parent, float y)
        {
            var (go, _) = MakeSection("Summary", parent, 40, y);
            summaryText = go.AddComponent<Text>();
            summaryText.font = DefFont; summaryText.fontSize = 16;
            summaryText.color = new(.85f, .85f, .9f);
            summaryText.alignment = TextAnchor.MiddleCenter;
            return y + 40;
        }

        float BuildTabs(GameObject parent, float y)
        {
            var (go, _) = MakeSection("Tabs", parent, 44, y);
            tabButtons.Clear();
            for (int i = 0; i < CATS.Length; i++)
            {
                var tab = CreateChild($"Tab_{CATS[i]}", go.transform);
                var trt = tab.Rect();
                float w = 1f / CATS.Length;
                trt.anchorMin = new(i * w + .01f, .1f); trt.anchorMax = new((i + 1) * w - .01f, .9f);
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                tab.AddComponent<Image>().color = TAB_NRM;
                var btn = tab.AddComponent<Button>(); btn.targetGraphic = tab.GetComponent<Image>();
                var t = tab.AddComponent<Text>(); t.text = CAT_LABELS[i]; t.font = DefFont;
                t.fontSize = 14; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
                tabButtons.Add(btn);
            }
            return y + 44;
        }

        void BuildScrollView(GameObject parent, float topOffset)
        {
            var sv = CreateChild("ScrollView", parent.transform);
            var srt = sv.Rect(); srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = new(0, -topOffset);
            sv.AddComponent<Image>().color = new(.06f, .06f, .1f);

            var vp = CreateChild("Viewport", sv.transform);
            var vrt = vp.Rect(); vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            vp.AddComponent<RectMask2D>();

            var lc = CreateChild("ListContent", vp.transform);
            listContainer = lc.Rect();
            listContainer.anchorMin = new(0, 1); listContainer.anchorMax = new(1, 1);
            listContainer.pivot = new(.5f, 1);
            listContainer.offsetMin = listContainer.offsetMax = Vector2.zero;

            var sr = sv.AddComponent<ScrollRect>();
            sr.content = listContainer; sr.viewport = vrt;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
        }

        // ==================== 刷新 ====================
        void RefreshSummary()
        {
            var am = AchievementManager.Instance;
            if (am == null || summaryText == null) return;

            int total = am.GetTotalCount();
            int unlocked = am.GetUnlockedCount();
            int claimable = 0;
            var allProg = am.GetAllProgress();
            foreach (var kv in allProg)
                if (kv.Value.is_unlocked && !kv.Value.rewards_claimed) claimable++;

            summaryText.text = $"{unlocked}/{total} 已解锁  ⭐ {claimable} 待领奖";
        }

        void RefreshList(string category)
        {
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);

            var am = AchievementManager.Instance;
            if (am == null) return;

            var allDefs = am.GetAllDefs();
            var allProg = am.GetAllProgress();
            var filtered = new List<(AchievementDef d, AchievementProgress p)>();

            foreach (var d in allDefs)
            {
                if (category != "all" && d.category != category) continue;
                allProg.TryGetValue(d.id, out var p);
                filtered.Add((d, p ?? new AchievementProgress { achievement_id = d.id }));
            }

            // 排序：待领奖(0) > 进行中(1) > 已领奖(2) > 隐藏(3)
            filtered.Sort((a, b) => SortScore(a).CompareTo(SortScore(b)));
            float y = 0;
            foreach (var (d, p) in filtered) { CreateCard(d, p, y); y += 108; }
            listContainer.sizeDelta = new(0, Mathf.Max(y, 0));
        }

        static int SortScore((AchievementDef d, AchievementProgress p) x)
        {
            bool hidden = x.d.is_hidden && !x.p.is_unlocked;
            bool done = x.p.is_unlocked && x.p.rewards_claimed;
            bool claimable = x.p.is_unlocked && !x.p.rewards_claimed;

            if (claimable) return 0;
            if (!x.p.is_unlocked && !hidden) return 1;
            if (done) return 2;
            return 3; // 隐藏
        }

        void OnCategorySelected(string cat) { currentCategory = cat; HighlightTab(cat); RefreshList(cat); }

        void HighlightTab(string cat)
        {
            for (int i = 0; i < CATS.Length; i++)
            {
                bool active = CATS[i] == cat;
                var img = tabButtons[i].GetComponent<Image>();
                var txt = tabButtons[i].GetComponent<Text>();
                if (img) img.color = active ? TAB_ACT : TAB_NRM;
                if (txt) txt.color = active ? Color.white : new(.7f, .7f, .75f);
            }
        }

        // ==================== 成就卡片 ====================
        void CreateCard(AchievementDef d, AchievementProgress p, float yPos)
        {
            bool hidden = d.is_hidden && !p.is_unlocked;
            bool done = p.is_unlocked && p.rewards_claimed;
            bool claimable = p.is_unlocked && !p.rewards_claimed;
            bool inProgress = !p.is_unlocked && !hidden;

            var go = CreateChild($"Card_{d.id}", listContainer);
            var rt = go.Rect();
            rt.anchorMin = new(.02f, 1); rt.anchorMax = new(.98f, 1); rt.pivot = new(.5f, 1);
            rt.sizeDelta = new(0, 100); rt.anchoredPosition = new(0, -yPos);

            go.AddComponent<Image>().color = CARD_BG;

            // 边框颜色
            var outline = go.AddComponent<Outline>();
            outline.effectColor = claimable ? BD_CLAIM : done ? BD_DONE : inProgress ? BD_PROG : BD_HIDE;
            outline.effectDistance = new(2, -2);

            // 金色脉冲（待领奖）
            if (claimable)
            {
                var seq = DOTween.Sequence();
                seq.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c, new Color(1f, .84f, 0f), .5f));
                seq.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c, new Color(.7f, .55f, 0f), .5f));
                seq.SetLoops(-1, LoopType.Yoyo).SetTarget(go);
            }

            // 左侧图标色块 60x60
            var icon = CreateChild("Icon", go.transform);
            var irt = icon.Rect(); irt.anchorMin = irt.anchorMax = new(.03f, .2f);
            irt.pivot = Vector2.zero; irt.sizeDelta = new(60, 60);
            icon.AddComponent<Image>().color = hidden ? new(.3f, .3f, .35f) :
                ICON_CLR.GetValueOrDefault(d.icon ?? "star", Color.gray);
            var iconTxt = icon.AddComponent<Text>(); iconTxt.font = DefFont; iconTxt.fontSize = 24;
            iconTxt.color = Color.white; iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.text = hidden ? "?" : (d.icon ?? "star") switch
            {
                "sword" => "⚔", "skull" => "💀", "dice" => "🎲", "gem" => "💎", "clock" => "⏱",
                "dragon" => "🐉", "fire" => "🔥", "shield" => "🛡", "star" => "⭐", "coin" => "💰",
                "map" => "🗺", "trophy" => "🏆", "chest" => "📦", "heart" => "❤", "lightning" => "⚡",
                "reroll" => "🔄", "combo" => "🎯", _ => "?"
            };

            const float L = .17f;
            // 名称
            MakeText("Name", go, (L, .6f, .72f, .88f), hidden ? "???" : (d.name_cn ?? d.id), 17,
                hidden ? new(.5f, .5f, .55f) : Color.white, TextAnchor.MiddleLeft, bold: true);
            // 描述
            MakeText("Desc", go, (L, .35f, .95f, .58f), hidden ? "继续探索以解锁此成就" : (d.description ?? ""), 13,
                hidden ? new(.45f, .45f, .5f) : new(.7f, .7f, .75f), TextAnchor.MiddleLeft);

            // 进度条
            var bar = CreateChild("Bar", go.transform);
            var brt = bar.Rect(); brt.anchorMin = new(L, .08f); brt.anchorMax = new(.72f, .3f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bar.AddComponent<Image>().color = BAR_BG;

            var fill = CreateChild("Fill", bar.transform);
            var frt = fill.Rect(); frt.anchorMin = Vector2.zero; frt.pivot = new(0, .5f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            float fillAmt = p.is_unlocked ? 1f : hidden ? 0f : Mathf.Clamp01((float)p.current_value / d.target_value);
            frt.anchorMax = new(fillAmt, 1f);
            fill.AddComponent<Image>().color = done ? BD_DONE : claimable ? BD_CLAIM : BAR_FILL;

            // 进度条填充动画
            if (inProgress && fillAmt > 0)
            {
                frt.anchorMax = new(0, 1f);
                TrackTween(frt.DOAnchorMax(new(fillAmt, 1f), .6f).SetEase(Ease.OutQuad).SetTarget(go));
            }

            // 进度文字
            string progStr = hidden ? "" : p.is_unlocked ? "✅ 已完成" :
                $"{Mathf.Min(p.current_value, d.target_value)}/{d.target_value}";
            MakeText("ProgTxt", go, (L, .08f, .72f, .3f), progStr, 12, Color.white, TextAnchor.MiddleCenter);

            // 稀有度徽章（右上角）
            var tier = CreateChild("Tier", go.transform);
            var trt = tier.Rect(); trt.anchorMin = new(.8f, .65f); trt.anchorMax = new(.97f, .95f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            tier.AddComponent<Image>().color = hidden ? new(.3f, .3f, .35f) : RarityColor(d.rarity);
            var tierTxt = tier.AddComponent<Text>(); tierTxt.font = DefFont; tierTxt.fontSize = 18;
            tierTxt.color = Color.white; tierTxt.alignment = TextAnchor.MiddleCenter;
            tierTxt.text = hidden ? "?" : RarityEmoji(d.rarity);

            // 已完成标记
            if (done)
                MakeText("Done", go, (.75f, .08f, .97f, .55f), "✅ 已完成", 13, Color.white,
                    TextAnchor.MiddleCenter, bg: new(.3f, .7f, .4f, .9f));

            // 领取按钮
            if (claimable)
            {
                string capturedId = d.id;
                var claimBtn = CreateChild("ClaimBtn", go.transform);
                var clrt = claimBtn.Rect(); clrt.anchorMin = new(.74f, .08f); clrt.anchorMax = new(.98f, .55f);
                clrt.offsetMin = clrt.offsetMax = Vector2.zero;
                claimBtn.AddComponent<Image>().color = new(.85f, .65f, .1f);
                var cb = claimBtn.AddComponent<Button>(); cb.targetGraphic = claimBtn.GetComponent<Image>();

                // 奖励描述
                string rewardText = GetRewardDescription(d);
                var ct = claimBtn.AddComponent<Text>();
                ct.text = rewardText; ct.font = DefFont; ct.fontSize = 12;
                ct.color = new(.2f, .1f, 0); ct.alignment = TextAnchor.MiddleCenter;
                cb.onClick.AddListener(() => OnClaimReward(capturedId));
            }

            // 稀有度色标（进行中）
            if (inProgress)
            {
                var dot = CreateChild("Dot", go.transform);
                var drt = dot.Rect(); drt.anchorMin = drt.anchorMax = new(.74f, .15f);
                drt.pivot = new(.5f, .5f); drt.sizeDelta = new(12, 12);
                dot.AddComponent<Image>().color = RarityColor(d.rarity);
            }

            // 入场弹入动画
            rt.localScale = Vector3.one * .9f;
            TrackTween(rt.DOScale(Vector3.one, .25f).SetEase(Ease.OutBack).SetTarget(go));
        }

        /// <summary>生成奖励描述文本</summary>
        static string GetRewardDescription(AchievementDef d)
        {
            if (d.rewards == null || d.rewards.Count == 0) return "领取 🎁";
            var parts = new List<string>();
            foreach (var r in d.rewards)
            {
                switch (r.type?.ToLower())
                {
                    case "gold": parts.Add($"{r.value}💰"); break;
                    case "relic": parts.Add("🎁遗物"); break;
                    case "dice_face": parts.Add("🎲面"); break;
                    default: parts.Add($"🎁x{r.value}"); break;
                }
            }
            return "领取 " + string.Join("+", parts);
        }

        // ==================== 领取奖励 ====================
        void OnClaimReward(string id)
        {
            var am = AchievementManager.Instance;
            if (am == null) return;

            var rewards = am.ClaimRewards(id);
            if (rewards == null) return;

            // 播放卡片动画 + 金币飞出
            for (int i = 0; i < listContainer.childCount; i++)
            {
                var child = listContainer.GetChild(i);
                if (child.name != $"Card_{id}") continue;

                var cardRt = child as RectTransform;
                if (cardRt != null)
                    TrackTween(cardRt.DOScale(.92f, .15f).SetEase(Ease.InQuad)
                        .OnComplete(() => cardRt.DOScale(Vector3.one, .2f).SetEase(Ease.OutBack)));

                // 金币飞出特效
                var fly = CreateChild("GoldFly", child);
                var frt = fly.Rect(); frt.anchorMin = frt.anchorMax = new(.5f, .5f);
                frt.pivot = new(.5f, .5f); frt.sizeDelta = new(32, 32);
                var fImg = fly.AddComponent<Image>(); fImg.color = BD_CLAIM;
                var fTxt = fly.AddComponent<Text>(); fTxt.text = "💰"; fTxt.font = DefFont;
                fTxt.fontSize = 24; fTxt.alignment = TextAnchor.MiddleCenter; fTxt.raycastTarget = false;
                frt.anchoredPosition = Vector2.zero;
                frt.DOAnchorPos(new(0, 80), .6f).SetEase(Ease.OutQuad).OnComplete(() => Destroy(fly));
                frt.DOScale(1.5f, .6f);
                fImg.DOFade(0, .6f);
                break;
            }

            DOVirtual.DelayedCall(.7f, () => { RefreshSummary(); RefreshList(currentCategory); });
        }

        // ==================== 事件回调 ====================

        void OnAchievementUnlocked(string achievementId)
        {
            Debug.Log($"[AchievementPanel] 成就解锁通知: {achievementId}");
            RefreshSummary();
            RefreshList(currentCategory);
        }

        void OnProgressChanged(string achievementId, int current, int target)
        {
            // 进度变化时刷新当前列表（轻量：仅刷新进度文字和进度条）
            RefreshSummary();
        }

        void OnRewardsClaimed(string achievementId)
        {
            RefreshSummary();
            RefreshList(currentCategory);
        }

        // ==================== DOTween 追踪（参考 BattlePanel） ====================

        readonly List<Tween> _activeTweens = new();

        void TrackTween(Tween t)
        {
            if (t == null) return;
            _activeTweens.Add(t);
            t.OnKill(() => _activeTweens.Remove(t));
        }

        void KillAllActiveTweens()
        {
            foreach (var t in _activeTweens)
                if (t != null && t.IsActive()) t.Kill();
            _activeTweens.Clear();
            DOTween.Kill(gameObject);
        }

        // ==================== UI辅助方法 ====================
        static RectTransform Rect(this GameObject go) => go.GetComponent<RectTransform>();

        static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new(0, 1); rt.anchorMax = new(1, 1); rt.pivot = new(.5f, 1);
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            return go;
        }

        static (GameObject go, RectTransform rt) MakeSection(string name, GameObject parent, float h, float y)
        {
            var go = CreateChild(name, parent.transform);
            var rt = go.Rect(); rt.sizeDelta = new(0, h); rt.anchoredPosition = new(0, -y);
            return (go, rt);
        }

        static Text MakeText(string name, GameObject parent, (float, float, float, float) anchors,
            string text, int size, Color color, TextAnchor align, bool bold = false, Color? bg = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new(anchors.Item1, anchors.Item2);
            rt.anchorMax = new(anchors.Item3, anchors.Item4);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (bg.HasValue) { var img = go.AddComponent<Image>(); img.color = bg.Value; }
            var t = go.AddComponent<Text>();
            t.text = text; t.font = DefFont; t.fontSize = size; t.color = color; t.alignment = align;
            if (bold) t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
