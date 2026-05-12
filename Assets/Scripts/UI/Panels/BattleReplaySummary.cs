using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 战斗回放摘要面板 — 跳过战斗后展示关键帧回放
    /// 竖屏720x1280布局，所有UI程序化创建
    ///
    /// 布局：
    /// ┌──────────────────────────────────────┐
    /// │  ⏯ 战斗回放摘要              [跳过▶] │  标题栏
    /// ├──────────────────────────────────────┤
    /// │  ⚔ 总输出 1,250 伤害               │  帧1 从右滑入
    /// │  💔 承受 480 伤害                    │  帧2
    /// │  💚 关键治疗 320 点                  │  帧3
    /// │  🎲 触发 三条                       │  帧4
    /// │  🏆 最后一击！胜利！                 │  帧N
    /// ├──────────────────────────────────────┤
    /// │  战斗时长: 12.3s  [进入结算 →]       │  底部
    /// └──────────────────────────────────────┘
    ///
    /// 只在跳过/加速战斗时弹出，正常打完不弹
    /// </summary>
    public class BattleReplaySummary : UIPanel
    {
        // ── 关键帧数据 ──
        public class ReplayFrame
        {
            public string icon;
            public string text;
            public Color color;
        }

        // ── 配置 ──
        [Header("动画配置")]
        public float frameSlideDuration = 0.2f;
        public float frameSlideInterval = 0.1f;
        public float panelEnterDuration = 0.3f;

        [Header("颜色配置")]
        public Color titleBgColor = new Color(0.1f, 0.1f, 0.18f, 0.95f);
        public Color contentBgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
        public Color bottomBgColor = new Color(0.1f, 0.1f, 0.18f, 0.95f);

        // ── 回调 ──
        /// <summary>回放完成或跳过时的回调，由 BattlePanel 设置</summary>
        public System.Action onReplayComplete;

        // ── 内部引用 ──
        private RectTransform contentContainer;
        private List<Tweener> activeTweens = new List<Tweener>();
        private List<RectTransform> frameElements = new List<RectTransform>();
        private Font uiFont;
        private Text durationText;
        private float lastBattleDuration;

        protected override void OnShow()
        {
            slideInAnimation = false;
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildUI();
            PlayEnterAnimation();
        }

        protected override void OnHide()
        {
            KillAllTweens();
            onReplayComplete = null;
        }

        protected override void OnDestroy()
        {
            KillAllTweens();
        }

        // ══════════════════════════════════════
        //  UI 构建
        // ══════════════════════════════════════

        private void BuildUI()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 背景
            var bgImg = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);
            bgImg.raycastTarget = true;

            // 内容根（居中卡片）
            var card = CreateChild("Card", rect);
            card.anchorMin = new Vector2(0.05f, 0.15f);
            card.anchorMax = new Vector2(0.95f, 0.85f);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.color = contentBgColor;

            // ── 标题栏 ──
            var titleBar = CreateChild("TitleBar", card);
            titleBar.anchorMin = new Vector2(0, 0.92f);
            titleBar.anchorMax = new Vector2(1, 1);
            titleBar.offsetMin = Vector2.zero;
            titleBar.offsetMax = Vector2.zero;
            var titleBg = titleBar.gameObject.AddComponent<Image>();
            titleBg.color = titleBgColor;
            var titleText = CreateText("TitleText", titleBar, "⏯ 战斗回放摘要", 20, FontStyle.Bold, Color.white);
            SetStretchFull(titleText);

            // 跳过按钮（右上角）
            var skipBtnObj = CreateChild("SkipBtn", titleBar);
            skipBtnObj.anchorMin = new Vector2(0.75f, 0.15f);
            skipBtnObj.anchorMax = new Vector2(0.97f, 0.85f);
            skipBtnObj.offsetMin = Vector2.zero;
            skipBtnObj.offsetMax = Vector2.zero;
            var skipBg = skipBtnObj.gameObject.AddComponent<Image>();
            skipBg.color = new Color(0.3f, 0.3f, 0.4f, 1f);
            var skipBtn = skipBtnObj.gameObject.AddComponent<Button>();
            var skipLabel = CreateText("SkipLabel", skipBtnObj, "跳过 ▶", 14, FontStyle.Normal, Color.white);
            SetStretchFull(skipLabel);
            skipLabel.alignment = TextAnchor.MiddleCenter;
            skipBtn.onClick.AddListener(CompleteReplay);

            // ── 内容区域（带滚动） ──
            contentContainer = CreateChild("Content", card);
            contentContainer.anchorMin = new Vector2(0.03f, 0.12f);
            contentContainer.anchorMax = new Vector2(0.97f, 0.9f);
            contentContainer.offsetMin = Vector2.zero;
            contentContainer.offsetMax = Vector2.zero;

            // ── 底部栏 ──
            var bottomBar = CreateChild("BottomBar", card);
            bottomBar.anchorMin = new Vector2(0, 0);
            bottomBar.anchorMax = new Vector2(1, 0.12f);
            bottomBar.offsetMin = Vector2.zero;
            bottomBar.offsetMax = Vector2.zero;
            var bottomBg = bottomBar.gameObject.AddComponent<Image>();
            bottomBg.color = bottomBgColor;

            // 时长文本
            durationText = CreateText("Duration", bottomBar, "战斗时长: 0.0s", 14, FontStyle.Normal,
                new Color(0.7f, 0.7f, 0.8f, 1f));
            durationText.rectTransform.anchorMin = new Vector2(0.03f, 0.1f);
            durationText.rectTransform.anchorMax = new Vector2(0.55f, 0.9f);
            durationText.rectTransform.offsetMin = Vector2.zero;
            durationText.rectTransform.offsetMax = Vector2.zero;
            durationText.alignment = TextAnchor.MiddleLeft;

            // 进入结算按钮
            var nextBtnObj = CreateChild("NextBtn", bottomBar);
            nextBtnObj.anchorMin = new Vector2(0.57f, 0.15f);
            nextBtnObj.anchorMax = new Vector2(0.97f, 0.85f);
            nextBtnObj.offsetMin = Vector2.zero;
            nextBtnObj.offsetMax = Vector2.zero;
            var nextBg = nextBtnObj.gameObject.AddComponent<Image>();
            nextBg.color = new Color(0.2f, 0.6f, 0.3f, 1f);
            var nextBtn = nextBtnObj.gameObject.AddComponent<Button>();
            var nextLabel = CreateText("NextLabel", nextBtnObj, "进入结算 →", 15, FontStyle.Bold, Color.white);
            SetStretchFull(nextLabel);
            nextLabel.alignment = TextAnchor.MiddleCenter;
            nextBtn.onClick.AddListener(CompleteReplay);

            // ── 填充关键帧 ──
            PopulateFrames();
        }

        private void PopulateFrames()
        {
            var record = GetLastBattleRecord();
            var frames = GenerateFrames(record);

            lastBattleDuration = record != null ? record.duration : 5.2f;
            if (durationText != null)
                durationText.text = $"战斗时长: {lastBattleDuration:F1}s";

            float yPos = 0f;
            float frameHeight = 50f;

            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                var frameRect = CreateChild($"Frame_{i}", contentContainer);
                frameRect.anchorMin = new Vector2(0, 1);
                frameRect.anchorMax = new Vector2(1, 1);
                frameRect.pivot = new Vector2(0.5f, 1);
                frameRect.sizeDelta = new Vector2(0, frameHeight);
                frameRect.anchoredPosition = new Vector2(0, -yPos);

                // 背景
                var frameBg = frameRect.gameObject.AddComponent<Image>();
                frameBg.color = new Color(0.15f, 0.15f, 0.22f, 0.6f + i * 0.03f);

                // 左侧色条
                var accent = CreateChild("Accent", frameRect);
                accent.anchorMin = new Vector2(0, 0.1f);
                accent.anchorMax = new Vector2(0, 0.9f);
                accent.offsetMin = new Vector2(0, 0);
                accent.offsetMax = new Vector2(4, 0);
                var accentBg = accent.gameObject.AddComponent<Image>();
                accentBg.color = f.color;

                // Emoji
                var iconText = CreateText("Icon", frameRect, f.icon, 22, FontStyle.Normal, Color.white);
                iconText.rectTransform.anchorMin = new Vector2(0, 0);
                iconText.rectTransform.anchorMax = new Vector2(0, 1);
                iconText.rectTransform.offsetMin = new Vector2(12, 0);
                iconText.rectTransform.offsetMax = new Vector2(48, 0);
                iconText.alignment = TextAnchor.MiddleCenter;

                // 描述
                var descText = CreateText("Desc", frameRect, f.text, 16, FontStyle.Normal, f.color);
                descText.rectTransform.anchorMin = new Vector2(0, 0);
                descText.rectTransform.anchorMax = new Vector2(1, 1);
                descText.rectTransform.offsetMin = new Vector2(52, 0);
                descText.rectTransform.offsetMax = new Vector2(-12, 0);
                descText.alignment = TextAnchor.MiddleLeft;

                // 初始位置在屏幕右侧（动画用）
                frameRect.anchoredPosition = new Vector2(800, -yPos);
                frameElements.Add(frameRect);

                yPos += frameHeight + 6f;
            }
        }

        // ══════════════════════════════════════
        //  关键帧生成
        // ══════════════════════════════════════

        private BattleRecord GetLastBattleRecord()
        {
            var tracker = BattleStatsTracker.Instance;
            if (tracker != null)
            {
                var stats = tracker.GetCurrentRunStats();
                if (stats != null && stats.battleHistory != null && stats.battleHistory.Count > 0)
                    return stats.battleHistory[stats.battleHistory.Count - 1];
            }
            return null;
        }

        private List<ReplayFrame> GenerateFrames(BattleRecord record)
        {
            var frames = new List<ReplayFrame>();

            if (record == null)
            {
                // Mock 数据（编辑器测试）
                frames.Add(new ReplayFrame { icon = "⚔", text = "总输出 1,250 伤害", color = new Color(1f, 0.35f, 0.35f) });
                frames.Add(new ReplayFrame { icon = "💔", text = "承受 480 伤害", color = new Color(1f, 0.55f, 0.15f) });
                frames.Add(new ReplayFrame { icon = "💚", text = "关键治疗 320 点", color = new Color(0.3f, 1f, 0.5f) });
                frames.Add(new ReplayFrame { icon = "🛡", text = "护盾抵挡 180 伤害", color = new Color(0.3f, 0.7f, 1f) });
                frames.Add(new ReplayFrame { icon = "🎲", text = "触发 三条", color = new Color(0.65f, 0.45f, 1f) });
                frames.Add(new ReplayFrame { icon = "🎲", text = "触发 对子", color = new Color(0.65f, 0.45f, 1f) });
                frames.Add(new ReplayFrame { icon = "🏆", text = "最后一击！胜利！", color = new Color(1f, 0.85f, 0.2f) });
                return frames;
            }

            // 伤害输出
            if (record.totalDamageDealt > 0)
                frames.Add(new ReplayFrame
                {
                    icon = "⚔",
                    text = $"总输出 {FormatNumber(record.totalDamageDealt)} 伤害",
                    color = new Color(1f, 0.35f, 0.35f)
                });

            // 承伤
            if (record.totalDamageTaken > 0)
                frames.Add(new ReplayFrame
                {
                    icon = "💔",
                    text = $"承受 {FormatNumber(record.totalDamageTaken)} 伤害",
                    color = new Color(1f, 0.55f, 0.15f)
                });

            // 治疗
            if (record.totalHealing > 0)
                frames.Add(new ReplayFrame
                {
                    icon = "💚",
                    text = $"关键治疗 {FormatNumber(record.totalHealing)} 点",
                    color = new Color(0.3f, 1f, 0.5f)
                });

            // 护盾
            if (record.totalShield > 0)
                frames.Add(new ReplayFrame
                {
                    icon = "🛡",
                    text = $"护盾抵挡 {FormatNumber(record.totalShield)} 伤害",
                    color = new Color(0.3f, 0.7f, 1f)
                });

            // 骰子组合
            if (record.diceCombos != null)
            {
                foreach (var combo in record.diceCombos)
                    frames.Add(new ReplayFrame
                    {
                        icon = "🎲",
                        text = $"触发 {combo}",
                        color = new Color(0.65f, 0.45f, 1f)
                    });
            }

            // 战斗结果（最后一帧）
            if (record.isVictory)
                frames.Add(new ReplayFrame
                {
                    icon = "🏆",
                    text = "最后一击！胜利！",
                    color = new Color(1f, 0.85f, 0.2f)
                });
            else
                frames.Add(new ReplayFrame
                {
                    icon = "💀",
                    text = "英雄阵亡...",
                    color = new Color(0.6f, 0.6f, 0.6f)
                });

            return frames;
        }

        // ══════════════════════════════════════
        //  动画
        // ══════════════════════════════════════

        private void PlayEnterAnimation()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;

            // 面板从底部滑入
            var startPos = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(startPos.x, -Screen.height);
            var tw = rect.DOAnchorPos(startPos, panelEnterDuration)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
            activeTweens.Add(tw);

            // 每帧依次从右侧滑入
            for (int i = 0; i < frameElements.Count; i++)
            {
                var frame = frameElements[i];
                var targetY = frame.anchoredPosition.y;
                var tw2 = frame.DOAnchorPos(new Vector2(0, targetY), frameSlideDuration)
                    .SetDelay(panelEnterDuration + i * frameSlideInterval)
                    .SetEase(Ease.OutQuart)
                    .SetLink(gameObject);
                activeTweens.Add(tw2);
            }
        }

        // ══════════════════════════════════════
        //  回放完成 / 跳过
        // ══════════════════════════════════════

        private void CompleteReplay()
        {
            KillAllTweens();

            // 通知外部（BattlePanel）回放结束
            var callback = onReplayComplete;
            onReplayComplete = null;
            callback?.Invoke();

            // 隐藏自己 — 直接用基类Hide()，不依赖NewUIManager
            Hide();
        }

        // ══════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════

        private void KillAllTweens()
        {
            foreach (var tw in activeTweens)
            {
                if (tw != null && tw.IsActive())
                    tw.Kill();
            }
            activeTweens.Clear();
        }

        private RectTransform CreateChild(string name, RectTransform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        private Text CreateText(string name, RectTransform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var txt = obj.AddComponent<Text>();
            txt.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            return txt;
        }

        private void SetStretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string FormatNumber(int n)
        {
            return n.ToString("N0");
        }
    }
}
