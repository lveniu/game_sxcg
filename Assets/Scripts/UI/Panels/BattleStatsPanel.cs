using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 战报统计面板 — 展示本次Run的完整战斗统计数据
    /// 竖屏720x1280布局，所有UI程序化创建
    ///
    /// 布局：
    /// ┌──────────────────────────────────────┐
    /// │  📊 战报统计                  [X]    │  标题栏(#1a1a2e)
    /// ├──────────────────────────────────────┤
    /// │  总场次: 12  胜率: 75%  最高连胜: 5  │  概览区
    /// ├──────────────────────────────────────┤
    /// │  累计数据（横向柱状图）               │
    /// │  伤害 ████████████ 12,450            │  红色柱
    /// │  治疗 ██████ 6,200                   │  绿色柱
    /// │  护盾 ███ 3,100                      │  蓝色柱
    /// │  承伤 █████████ 9,800                │  橙色柱
    /// ├──────────────────────────────────────┤
    /// │  骰子组合统计                        │
    /// │  三条 ████████ 8次                   │
    /// │  对子 ████████████ 15次              │
    /// │  顺子 ███ 3次                        │
    /// │  散牌 ██████████████ 18次            │
    /// ├──────────────────────────────────────┤
    /// │  遗物收集 (本次run)                   │
    /// │  [图标] 烈焰之刃  [图标] 冰霜护盾   │
    /// ├──────────────────────────────────────┤
    /// │  英雄成长                            │
    /// │  ⚔ 战士  Lv.1→15  ⭐1→3             │
    /// │  HP  80→120  ATK 30→65  DEF 20→45   │
    /// │  ████████████ (+40)                  │
    /// ├──────────────────────────────────────┤
    /// │  [查看详细战报 ▼]                    │
    /// └──────────────────────────────────────┘
    /// </summary>
    public class BattleStatsPanel : UIPanel
    {
        // ── 配色常量 ──
        private static readonly Color ColorDamage   = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ColorHeal      = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color ColorShield    = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color ColorTaken     = new Color(0.9f, 0.6f, 0.1f);
        private static readonly Color ColorComboTri  = new Color(0.6f, 0.3f, 0.9f);
        private static readonly Color ColorComboPair = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color ColorComboStr  = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color ColorComboMisc = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color ColorGrowth    = new Color(0.3f, 1f, 0.4f);
        private static readonly Color ColorBg        = new Color(0.08f, 0.08f, 0.14f, 0.95f);
        private static readonly Color ColorCardBg    = new Color(0.12f, 0.12f, 0.2f, 0.9f);
        private static readonly Color ColorSectionBg = new Color(0.1f, 0.1f, 0.16f, 0.85f);
        private static readonly Color ColorTitleBar  = new Color(0.1f, 0.1f, 0.18f);

        // ── 内部引用 ──
        private RectTransform contentRoot;
        private RectTransform battleHistoryContainer;
        private bool historyExpanded;
        private Font defaultFont;
        private readonly List<Tween> activeTweens = new List<Tween>();

        // 面板宽度常量（竖屏720）
        private const float PanelWidth = 680f;
        private const float BarMaxWidth = 360f;
        private const float Padding = 16f;
        private const float BarHeight = 20f;
        private const float SectionSpacing = 10f;

        protected override void Awake()
        {
            base.Awake();
            panelId = "BattleStats";
            slideInAnimation = false; // 自定义入场动画
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ================================================================
        // 生命周期
        // ================================================================

        protected override void OnShow()
        {
            RemoveAllListeners();
            KillAllActiveTweens();

            // 获取数据 — null时使用Mock
            RunBattleStats stats = BattleStatsTracker.Instance != null
                ? BattleStatsTracker.Instance.GetCurrentRunStats()
                : MockRunStats();

            BuildUI(stats);
            PlayEntryAnimation();
        }

        protected override void OnHide()
        {
            RemoveAllListeners();
            KillAllActiveTweens();
        }

        // ================================================================
        // UI构建
        // ================================================================

        private void BuildUI(RunBattleStats stats)
        {
            // 清理旧内容
            if (contentRoot != null)
                Destroy(contentRoot.gameObject);

            // 全屏背景
            var bgGo = new GameObject("BattleStatsPanel_BG");
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.SetParent(rectTransform, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = ColorBg;
            bgImg.raycastTarget = true;

            // 内容根节点（带ScrollRect支持）
            contentRoot = CreateScrollContent(bgRt, "ContentRoot", out ScrollRect scrollRect);

            float y = 0f;

            // ── 标题栏 ──
            y = BuildTitleBar(contentRoot, y);

            // ── 概览区 ──
            y = BuildOverviewSection(contentRoot, y, stats);

            // ── 累计数据柱状图 ──
            y = BuildCumulativeDataSection(contentRoot, y, stats);

            // ── 骰子组合统计 ──
            y = BuildComboStatsSection(contentRoot, y, stats);

            // ── 遗物收集 ──
            y = BuildRelicsSection(contentRoot, y, stats);

            // ── 英雄成长 ──
            y = BuildHeroGrowthSection(contentRoot, y, stats);

            // ── 战斗历史（折叠） ──
            y = BuildBattleHistorySection(contentRoot, y, stats);

            // 更新内容高度
            contentRoot.sizeDelta = new Vector2(PanelWidth, y + Padding);
        }

        // ── 标题栏 ──

        private float BuildTitleBar(RectTransform parent, float startY)
        {
            float height = 50f;
            var rt = CreateSection(parent, "TitleBar", startY, PanelWidth, height);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ColorTitleBar;
            img.raycastTarget = false;

            // 标题文字
            var titleRt = CreateChild("Title", rt, new Vector2(Padding, 0), new Vector2(300f, height));
            titleRt.anchorMin = new Vector2(0, 0);
            titleRt.anchorMax = new Vector2(0, 1);
            var titleText = titleRt.gameObject.AddComponent<Text>();
            titleText.font = defaultFont;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.text = "📊 战报统计";
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;

            // 关闭按钮
            var closeRt = CreateChild("CloseBtn", rt, new Vector2(-Padding, 0), new Vector2(40f, 36f));
            closeRt.anchorMin = new Vector2(1, 0.5f);
            closeRt.anchorMax = new Vector2(1, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            var closeBtnImg = closeRt.gameObject.AddComponent<Image>();
            closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => Hide());
            var closeLabel = CreateChild("X", closeRt, Vector2.zero, new Vector2(40f, 36f));
            closeLabel.anchorMin = Vector2.zero;
            closeLabel.anchorMax = Vector2.one;
            closeLabel.offsetMin = Vector2.zero;
            closeLabel.offsetMax = Vector2.zero;
            var closeTxt = closeLabel.gameObject.AddComponent<Text>();
            closeTxt.font = defaultFont;
            closeTxt.fontSize = 20;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = Color.white;
            closeTxt.text = "✕";
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.raycastTarget = false;

            return startY + height + SectionSpacing;
        }

        // ── 概览区 ──

        private float BuildOverviewSection(RectTransform parent, float startY, RunBattleStats stats)
        {
            float height = 70f;
            var rt = CreateSection(parent, "Overview", startY, PanelWidth, height);
            AddBackground(rt, ColorCardBg);

            // 三个数据卡片并排
            float cardW = (PanelWidth - Padding * 4) / 3f;
            string[] labels = { "总场次", "胜率", "最高连胜" };
            string[] values = {
                stats.totalBattles.ToString(),
                stats.winRate.ToString("F0") + "%",
                stats.maxConsecutiveWins.ToString()
            };

            for (int i = 0; i < 3; i++)
            {
                float x = Padding + i * (cardW + Padding);
                var cardRt = CreateChild($"Card_{labels[i]}", rt, new Vector2(x, 8f), new Vector2(cardW, height - 16f));
                cardRt.anchorMin = new Vector2(0, 0);
                cardRt.anchorMax = new Vector2(0, 1);

                // 数值
                var valRt = CreateChild("Value", cardRt, new Vector2(0, 10f), new Vector2(cardW, 30f));
                valRt.anchorMin = new Vector2(0, 0.5f);
                valRt.anchorMax = new Vector2(1, 1);
                valRt.offsetMin = Vector2.zero;
                valRt.offsetMax = Vector2.zero;
                var valTxt = valRt.gameObject.AddComponent<Text>();
                valTxt.font = defaultFont;
                valTxt.fontSize = 26;
                valTxt.fontStyle = FontStyle.Bold;
                valTxt.color = Color.white;
                valTxt.text = values[i];
                valTxt.alignment = TextAnchor.MiddleCenter;
                valTxt.raycastTarget = false;

                // 标签
                var lblRt = CreateChild("Label", cardRt, Vector2.zero, new Vector2(cardW, 20f));
                lblRt.anchorMin = new Vector2(0, 0);
                lblRt.anchorMax = new Vector2(1, 0.5f);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                var lblTxt = lblRt.gameObject.AddComponent<Text>();
                lblTxt.font = defaultFont;
                lblTxt.fontSize = 13;
                lblTxt.color = new Color(0.7f, 0.7f, 0.8f);
                lblTxt.text = labels[i];
                lblTxt.alignment = TextAnchor.MiddleCenter;
                lblTxt.raycastTarget = false;
            }

            return startY + height + SectionSpacing;
        }

        // ── 累计数据柱状图 ──

        private float BuildCumulativeDataSection(RectTransform parent, float startY, RunBattleStats stats)
        {
            var entries = new[]
            {
                ("伤害", stats.totalDamageDealt, ColorDamage),
                ("治疗", stats.totalHealing,      ColorHeal),
                ("护盾", stats.totalShield,       ColorShield),
                ("承伤", stats.totalDamageTaken,   ColorTaken)
            };

            long maxVal = 1;
            foreach (var e in entries)
                if (e.Item2 > maxVal) maxVal = e.Item2;

            float rowH = BarHeight + 8f;
            float height = 30f + entries.Length * rowH + Padding;

            var rt = CreateSection(parent, "CumulativeData", startY, PanelWidth, height);
            AddSectionTitle(rt, "累计数据");
            AddBackground(rt, ColorSectionBg);

            for (int i = 0; i < entries.Length; i++)
            {
                float yPos = height - 30f - Padding - i * rowH;
                CreateBarRow(rt, entries[i].Item1, entries[i].Item2, maxVal, entries[i].Item3,
                    yPos, BarMaxWidth, BarHeight, false);
            }

            return startY + height + SectionSpacing;
        }

        // ── 骰子组合统计 ──

        private float BuildComboStatsSection(RectTransform parent, float startY, RunBattleStats stats)
        {
            // 默认组合及颜色映射
            var defaultCombos = new[]
            {
                ("三条", "三條", "Triple", ColorComboTri),
                ("对子", "對子", "Pair",   ColorComboPair),
                ("顺子", "順子", "Straight", ColorComboStr),
                ("散牌", "散牌", "Misc",   ColorComboMisc)
            };

            // 从RunStats中获取组合数据
            var comboList = new List<(string label, int count, Color color)>();
            foreach (var dc in defaultCombos)
            {
                int count = GetComboCount(stats, dc.Item1, dc.Item2, dc.Item3);
                comboList.Add((dc.Item1, count, dc.Item4));
            }

            int maxVal = 1;
            foreach (var c in comboList)
                if (c.count > maxVal) maxVal = c.count;

            float rowH = BarHeight + 8f;
            float height = 30f + comboList.Count * rowH + Padding;

            var rt = CreateSection(parent, "ComboStats", startY, PanelWidth, height);
            AddSectionTitle(rt, "骰子组合统计");
            AddBackground(rt, ColorSectionBg);

            for (int i = 0; i < comboList.Count; i++)
            {
                float yPos = height - 30f - Padding - i * rowH;
                CreateBarRow(rt, comboList[i].label, comboList[i].count, maxVal, comboList[i].color,
                    yPos, BarMaxWidth, BarHeight, true);
            }

            return startY + height + SectionSpacing;
        }

        // ── 遗物收集 ──

        private float BuildRelicsSection(RectTransform parent, float startY, RunBattleStats stats)
        {
            var relics = stats.relicsCollected ?? new List<string>();
            bool hasRelics = relics.Count > 0;

            float itemH = 36f;
            float itemW = 150f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((PanelWidth - Padding * 2) / (itemW + 8f)));
            int rows = hasRelics ? Mathf.CeilToInt((float)relics.Count / cols) : 1;
            float height = 30f + rows * (itemH + 6f) + Padding;

            var rt = CreateSection(parent, "Relics", startY, PanelWidth, height);
            AddSectionTitle(rt, "遗物收集 (本次run)");
            AddBackground(rt, ColorSectionBg);

            if (!hasRelics)
            {
                var emptyRt = CreateChild("Empty", rt, new Vector2(0, 10f), new Vector2(PanelWidth, itemH));
                emptyRt.anchorMin = new Vector2(0, 0);
                emptyRt.anchorMax = new Vector2(1, 0);
                var emptyTxt = emptyRt.gameObject.AddComponent<Text>();
                emptyTxt.font = defaultFont;
                emptyTxt.fontSize = 14;
                emptyTxt.color = new Color(0.5f, 0.5f, 0.5f);
                emptyTxt.text = "暂无遗物";
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                emptyTxt.raycastTarget = false;
            }
            else
            {
                // 稀有度颜色映射
                var rarityColors = new[] { new Color(0.5f, 0.5f, 0.5f), new Color(0.2f, 0.8f, 0.3f),
                    new Color(0.2f, 0.5f, 0.9f), new Color(1f, 0.8f, 0.1f) };

                for (int i = 0; i < relics.Count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;
                    float x = Padding + col * (itemW + 8f);
                    float yPos = height - 30f - Padding - row * (itemH + 6f);

                    var itemRt = CreateChild($"Relic_{i}", rt, new Vector2(x, yPos), new Vector2(itemW, itemH));
                    itemRt.anchorMin = new Vector2(0, 1);
                    itemRt.anchorMax = new Vector2(0, 1);

                    // 图标占位色块
                    var iconRt = CreateChild("Icon", itemRt, new Vector2(4f, 3f), new Vector2(30f, 30f));
                    var iconImg = iconRt.gameObject.AddComponent<Image>();
                    iconImg.color = rarityColors[i % rarityColors.Length];
                    iconImg.raycastTarget = false;

                    // 名称
                    var nameRt = CreateChild("Name", itemRt, new Vector2(38f, 0f), new Vector2(itemW - 42f, itemH));
                    var nameTxt = nameRt.gameObject.AddComponent<Text>();
                    nameTxt.font = defaultFont;
                    nameTxt.fontSize = 13;
                    nameTxt.color = Color.white;
                    nameTxt.text = relics[i];
                    nameTxt.alignment = TextAnchor.MiddleLeft;
                    nameTxt.raycastTarget = false;
                }
            }

            return startY + height + SectionSpacing;
        }

        // ── 英雄成长 ──

        private float BuildHeroGrowthSection(RectTransform parent, float startY, RunBattleStats stats)
        {
            // Bug#fix: 使用 heroCumulativeList 替代不存在的 heroGrowth 字段
            var heroes = stats.heroCumulativeList;
            bool hasData = heroes != null && heroes.Count > 0;

            float barH = 14f;
            float rowH = 30f;
            // 每个英雄显示: 名称行 + 伤害/击杀/治疗 三行柱
            float perHero = rowH + 3 * (barH + 22f) + Padding;
            float height = hasData
                ? 30f + heroes.Count * perHero + Padding
                : 30f + 40f + Padding;

            var rt = CreateSection(parent, "HeroGrowth", startY, PanelWidth, height);
            AddSectionTitle(rt, "英雄成长");
            AddBackground(rt, ColorSectionBg);

            if (!hasData)
            {
                var emptyRt = CreateChild("Empty", rt, new Vector2(0, 10f), new Vector2(PanelWidth, 40f));
                emptyRt.anchorMin = new Vector2(0, 0);
                emptyRt.anchorMax = new Vector2(1, 0);
                var emptyTxt = emptyRt.gameObject.AddComponent<Text>();
                emptyTxt.font = defaultFont;
                emptyTxt.fontSize = 14;
                emptyTxt.color = new Color(0.5f, 0.5f, 0.5f);
                emptyTxt.text = "暂无数据";
                emptyTxt.alignment = TextAnchor.MiddleCenter;
                emptyTxt.raycastTarget = false;
                return startY + height + SectionSpacing;
            }

            // 找最大值做柱状图归一化
            long maxStat = 1;
            foreach (var h in heroes)
                maxStat = Math.Max(maxStat, Math.Max(h.damageDealt, Math.Max(h.healingDone, h.kills * 200L)));

            float yPos = height - 30f - Padding;
            for (int hi = 0; hi < heroes.Count; hi++)
            {
                var hero = heroes[hi];
                // 英雄信息行
                var infoRt = CreateChild($"HeroInfo_{hi}", rt, new Vector2(Padding, yPos - rowH), new Vector2(PanelWidth - Padding * 2, rowH));
                infoRt.anchorMin = new Vector2(0, 1);
                infoRt.anchorMax = new Vector2(1, 1);
                var infoTxt = infoRt.gameObject.AddComponent<Text>();
                infoTxt.font = defaultFont;
                infoTxt.fontSize = 15;
                infoTxt.fontStyle = FontStyle.Bold;
                infoTxt.color = Color.white;
                infoTxt.text = $"⚔ {hero.heroName}  击杀:{hero.kills}  暴击:{hero.critCount}";
                infoTxt.alignment = TextAnchor.MiddleLeft;
                infoTxt.raycastTarget = false;

                // 伤害/治疗/护盾 柱状行
                var statLines = new[]
                {
                    ("伤害", (long)hero.damageDealt, ColorDamage),
                    ("治疗", (long)hero.healingDone, ColorHeal),
                    ("护盾", (long)hero.shieldGained, ColorShield)
                };

                for (int i = 0; i < statLines.Length; i++)
                {
                    float lineY = yPos - rowH - Padding - i * (barH + 22f);
                    CreateStatGrowthRow(rt, statLines[i].Item1, statLines[i].Item2, maxStat,
                        lineY, barH, statLines[i].Item3);
                }

                yPos -= perHero;
            }

            return startY + height + SectionSpacing;
        }

        // ── 战斗历史 ──

        private float BuildBattleHistorySection(RectTransform parent, float startY, RunBattleStats stats)
        {
            var history = stats.battleHistory ?? new List<BattleStatsRecord>();
            float collapsedH = 50f;
            float itemH = 36f;
            float expandedH = collapsedH + history.Count * itemH + Padding;

            float height = collapsedH;
            var rt = CreateSection(parent, "BattleHistory", startY, PanelWidth, height);
            AddBackground(rt, ColorSectionBg);

            // 展开/折叠按钮
            var toggleRt = CreateChild("ToggleBtn", rt, new Vector2(Padding, 4f), new Vector2(PanelWidth - Padding * 2, 40f));
            toggleRt.anchorMin = new Vector2(0, 0);
            toggleRt.anchorMax = new Vector2(1, 1);
            toggleRt.offsetMin = new Vector2(Padding, 4f);
            toggleRt.offsetMax = new Vector2(-Padding, -4f);
            var toggleImg = toggleRt.gameObject.AddComponent<Image>();
            toggleImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            var toggleBtn = toggleRt.gameObject.AddComponent<Button>();
            var toggleLabel = CreateChild("ToggleLabel", toggleRt, Vector2.zero, Vector2.zero);
            toggleLabel.anchorMin = Vector2.zero;
            toggleLabel.anchorMax = Vector2.one;
            toggleLabel.offsetMin = Vector2.zero;
            toggleLabel.offsetMax = Vector2.zero;
            var toggleTxt = toggleLabel.gameObject.AddComponent<Text>();
            toggleTxt.font = defaultFont;
            toggleTxt.fontSize = 15;
            toggleTxt.color = Color.white;
            toggleTxt.text = history.Count > 0 ? "查看详细战报 ▼" : "暂无战斗记录";
            toggleTxt.alignment = TextAnchor.MiddleCenter;
            toggleTxt.raycastTarget = false;

            // 存储折叠状态数据用于展开
            historyExpanded = false;
            battleHistoryContainer = null;

            if (history.Count > 0)
            {
                toggleBtn.onClick.AddListener(() =>
                {
                    historyExpanded = !historyExpanded;
                    if (historyExpanded)
                    {
                        rt.sizeDelta = new Vector2(PanelWidth, expandedH);
                        toggleTxt.text = "收起战报 ▲";
                        if (battleHistoryContainer == null)
                            CreateBattleHistoryItems(rt, stats);
                    }
                    else
                    {
                        rt.sizeDelta = new Vector2(PanelWidth, collapsedH);
                        toggleTxt.text = "查看详细战报 ▼";
                        if (battleHistoryContainer != null)
                            Destroy(battleHistoryContainer.gameObject);
                        battleHistoryContainer = null;
                    }
                });
            }

            return startY + expandedH + SectionSpacing;
        }

        // ── 战斗历史列表项 ──

        private void CreateBattleHistoryItems(RectTransform section, RunBattleStats stats)
        {
            var history = stats.battleHistory;
            if (history == null) return;

            float itemH = 36f;
            var containerGo = new GameObject("HistoryItems");
            battleHistoryContainer = containerGo.AddComponent<RectTransform>();
            battleHistoryContainer.SetParent(section, false);
            battleHistoryContainer.anchorMin = new Vector2(0, 0);
            battleHistoryContainer.anchorMax = new Vector2(1, 1);
            battleHistoryContainer.offsetMin = new Vector2(Padding, 4f);
            battleHistoryContainer.offsetMax = new Vector2(-Padding, -54f);
            battleHistoryContainer.pivot = new Vector2(0.5f, 1f);

            for (int i = 0; i < history.Count; i++)
            {
                var rec = history[i];
                float yPos = -i * itemH;
                var itemRt = CreateChild($"Battle_{i}", battleHistoryContainer,
                    new Vector2(0, yPos), new Vector2(battleHistoryContainer.rect.width, itemH - 4f));
                itemRt.anchorMin = new Vector2(0, 1);
                itemRt.anchorMax = new Vector2(1, 1);

                var itemBg = itemRt.gameObject.AddComponent<Image>();
                itemBg.color = rec.isVictory
                    ? new Color(0.15f, 0.35f, 0.2f, 0.8f)
                    : new Color(0.4f, 0.15f, 0.15f, 0.8f);
                itemBg.raycastTarget = false;

                var labelRt = CreateChild("Label", itemRt, Vector2.zero, Vector2.zero);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(8f, 0f);
                labelRt.offsetMax = new Vector2(-8f, 0f);
                var labelTxt = labelRt.gameObject.AddComponent<Text>();
                labelTxt.font = defaultFont;
                labelTxt.fontSize = 13;
                labelTxt.color = Color.white;
                string resultIcon = rec.isVictory ? "✅" : "❌";
                labelTxt.text = $"#{rec.battleIndex}  {resultIcon}  {rec.duration:F1}s";
                labelTxt.alignment = TextAnchor.MiddleLeft;
                labelTxt.raycastTarget = false;
            }
        }

        // ================================================================
        // 通用UI构建辅助
        // ================================================================

        private RectTransform CreateScrollContent(RectTransform parent, string name, out ScrollRect scrollRect)
        {
            // ScrollRect容器
            var scrollGo = new GameObject("ScrollView");
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = Color.clear;

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;

            // Viewport
            var vpGo = new GameObject("Viewport");
            var vpRt = vpGo.AddComponent<RectTransform>();
            vpRt.SetParent(scrollRt, false);
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            var vpMask = vpGo.AddComponent<RectMask2D>();

            // Content
            var contentGo = new GameObject(name);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.SetParent(vpRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(PanelWidth, 2000f);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            scrollRect.viewport = vpRt;
            scrollRect.content = contentRt;

            return contentRt;
        }

        private RectTransform CreateSection(RectTransform parent, string name, float yPos, float width, float height)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((parent.rect.width - width) * 0.5f, -yPos);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        private RectTransform CreateChild(string name, RectTransform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private void AddBackground(RectTransform rt, Color color)
        {
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void AddSectionTitle(RectTransform parent, string title)
        {
            float height = 30f;
            var rt = CreateChild("SectionTitle", parent,
                new Vector2(Padding, parent.sizeDelta.y - height), new Vector2(PanelWidth - Padding * 2, height));
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = defaultFont;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(0.9f, 0.85f, 0.7f);
            txt.text = title;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;
        }

        /// <summary>
        /// 创建横向柱状图行（带DOSizeDelta动画）
        /// </summary>
        private void CreateBarRow(RectTransform parent, string label, long value, long maxVal,
            Color barColor, float yPos, float maxWidth, float barH, bool isCount)
        {
            float labelW = 50f;
            float valueW = 80f;

            // 标签
            var lblRt = CreateChild($"Label_{label}", parent,
                new Vector2(Padding, yPos - barH), new Vector2(labelW, barH));
            var lblTxt = lblRt.gameObject.AddComponent<Text>();
            lblTxt.font = defaultFont;
            lblTxt.fontSize = 13;
            lblTxt.color = new Color(0.75f, 0.75f, 0.8f);
            lblTxt.text = label;
            lblTxt.alignment = TextAnchor.MiddleRight;
            lblTxt.raycastTarget = false;

            // 柱子背景（暗色轨道）
            float barStartX = Padding + labelW + 8f;
            float barAreaW = maxWidth;
            var trackRt = CreateChild($"Track_{label}", parent,
                new Vector2(barStartX, yPos - barH), new Vector2(barAreaW, barH));
            var trackImg = trackRt.gameObject.AddComponent<Image>();
            trackImg.color = new Color(0.15f, 0.15f, 0.2f, 0.7f);
            trackImg.raycastTarget = false;

            // 柱子填充（从0宽度动画到目标宽度）
            var fillRt = CreateChild($"Fill_{label}", trackRt, Vector2.zero, new Vector2(0f, barH));
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = new Vector2(0f, 0f);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = barColor;
            fillImg.raycastTarget = false;

            // 计算目标宽度并设置DOSizeDelta动画
            float targetW = maxVal > 0 ? (float)value / maxVal * barAreaW : 0f;
            targetW = Mathf.Clamp(targetW, 0f, barAreaW);
            var tween = fillRt.DOSizeDelta(new Vector2(targetW, barH), 0.6f)
                .SetEase(Ease.OutQuart)
                .SetDelay(0.3f)
                .SetLink(gameObject);
            activeTweens.Add(tween);

            // 数值文字
            var valRt = CreateChild($"Value_{label}", parent,
                new Vector2(barStartX + barAreaW + 8f, yPos - barH), new Vector2(valueW, barH));
            var valTxt = valRt.gameObject.AddComponent<Text>();
            valTxt.font = defaultFont;
            valTxt.fontSize = 13;
            valTxt.fontStyle = FontStyle.Bold;
            valTxt.color = Color.white;
            valTxt.text = isCount ? $"{value}次" : value.ToString("N0");
            valTxt.alignment = TextAnchor.MiddleLeft;
            valTxt.raycastTarget = false;
        }

        /// <summary>
        /// 创建英雄属性柱状行（累计统计值）
        /// </summary>
        private void CreateStatGrowthRow(RectTransform parent, string statName,
            long value, long maxStat, float yPos, float barH, Color barColor)
        {
            // 文字行：伤害 5,400
            float textY = yPos - barH - 4f;
            var textRt = CreateChild($"StatText_{statName}", parent,
                new Vector2(Padding, textY - 18f), new Vector2(PanelWidth - Padding * 2, 18f));
            textRt.anchorMin = new Vector2(0, 1);
            textRt.anchorMax = new Vector2(1, 1);

            var statTxt = textRt.gameObject.AddComponent<Text>();
            statTxt.font = defaultFont;
            statTxt.fontSize = 13;
            statTxt.color = new Color(0.8f, 0.8f, 0.85f);
            statTxt.text = $"{statName}  {value:N0}";
            statTxt.alignment = TextAnchor.MiddleLeft;
            statTxt.raycastTarget = false;

            // 柱状条
            float barStartX = Padding;
            float barAreaW = PanelWidth - Padding * 2;
            var barRt = CreateChild($"GrowthBar_{statName}", parent,
                new Vector2(barStartX, yPos - barH), new Vector2(barAreaW, barH));
            var barBg = barRt.gameObject.AddComponent<Image>();
            barBg.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);
            barBg.raycastTarget = false;

            // 填充条
            var fillRt = CreateChild("Fill", barRt, Vector2.zero, new Vector2(0f, barH));
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = new Vector2(0f, 0f);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = barColor;
            fillImg.raycastTarget = false;

            float targetW = maxStat > 0 ? (float)value / maxStat * barAreaW : 0f;
            targetW = Mathf.Clamp(targetW, 0f, barAreaW);
            var tween = fillRt.DOSizeDelta(new Vector2(targetW, barH), 0.6f)
                .SetEase(Ease.OutQuart)
                .SetDelay(0.4f)
                .SetLink(gameObject);
            activeTweens.Add(tween);
        }

        // ================================================================
        // 数据辅助
        // ================================================================

        /// <summary>
        /// 从RunBattleStats.comboCountMap中查找组合计数（支持多种名称匹配）
        /// </summary>
        private int GetComboCount(RunBattleStats stats, params string[] possibleNames)
        {
            if (stats?.comboCountMap == null) return 0;
            foreach (var name in possibleNames)
            {
                if (stats.comboCountMap.TryGetValue(name, out int count))
                    return count;
            }
            return 0;
        }

        /// <summary>
        /// Mock数据 — 编辑器内测试用
        /// </summary>
        private RunBattleStats MockRunStats()
        {
            var stats = new RunBattleStats
            {
                totalBattles = 12,
                victories = 9,
                defeats = 3,
                winRate = 75f,
                maxConsecutiveWins = 5,
                totalDamageDealt = 12450,
                totalDamageTaken = 9800,
                totalHealing = 6200,
                totalShield = 3100,
                comboCountMap = new Dictionary<string, int>
                {
                    { "三条", 8 },
                    { "对子", 15 },
                    { "顺子", 3 },
                    { "散牌", 18 }
                },
                relicsCollected = new List<string> { "烈焰之刃", "冰霜护盾", "生命之环" },
                heroCumulativeList = new List<HeroBattleStats>
                {
                    new HeroBattleStats
                    {
                        heroName = "战士",
                        heroInstanceId = 1,
                        damageDealt = 5400,
                        damageTaken = 3200,
                        healingDone = 1200,
                        shieldGained = 800,
                        kills = 18,
                        critCount = 5
                    }
                },
                battleHistory = new List<BattleStatsRecord>
                {
                    new BattleStatsRecord { battleIndex = 1, isVictory = true,  duration = 12.3f },
                    new BattleStatsRecord { battleIndex = 2, isVictory = true,  duration = 8.7f },
                    new BattleStatsRecord { battleIndex = 3, isVictory = false, duration = 15.2f },
                    new BattleStatsRecord { battleIndex = 4, isVictory = true,  duration = 10.1f },
                    new BattleStatsRecord { battleIndex = 5, isVictory = true,  duration = 7.8f },
                    new BattleStatsRecord { battleIndex = 6, isVictory = true,  duration = 11.5f },
                    new BattleStatsRecord { battleIndex = 7, isVictory = false, duration = 13.9f },
                    new BattleStatsRecord { battleIndex = 8, isVictory = true,  duration = 9.2f },
                    new BattleStatsRecord { battleIndex = 9, isVictory = true,  duration = 6.5f },
                    new BattleStatsRecord { battleIndex = 10, isVictory = true, duration = 14.0f },
                    new BattleStatsRecord { battleIndex = 11, isVictory = false, duration = 16.8f },
                    new BattleStatsRecord { battleIndex = 12, isVictory = true,  duration = 8.3f }
                }
            };
            return stats;
        }

        // ================================================================
        // 动画
        // ================================================================

        private void PlayEntryAnimation()
        {
            // 面板从底部滑入（OutBack, 0.4s）
            rectTransform.anchoredPosition = new Vector2(0f, -1280f);
            var tween = rectTransform.DOAnchorPosY(0f, 0.4f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
            activeTweens.Add(tween);
        }

        // ================================================================
        // 清理
        // ================================================================

        private void RemoveAllListeners()
        {
            // 遍历所有子按钮并移除监听器
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
                btn.onClick.RemoveAllListeners();
        }

        private void KillAllActiveTweens()
        {
            foreach (var tw in activeTweens)
            {
                if (tw != null && tw.IsActive())
                    tw.Kill();
            }
            activeTweens.Clear();
            DOTween.Kill(gameObject);
        }

        protected override void OnDestroy()
        {
            KillAllActiveTweens();
        }
    }
}
