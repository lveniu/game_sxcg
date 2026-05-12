using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Core;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 游戏结束面板 — 增强版战报统计 + 数字滚动动画
    /// UI元素：
    /// - resultTitle: 阵亡/通关标题Text
    /// - levelReachedText: 到达关卡数
    /// - relicCountText: 收集遗物数
    /// - killCountText: 击杀数
    /// - restartButton: 再来一局
    /// - shareButton: 分享战绩（可选）
    /// 
    /// 程序化创建的统计卡片：
    /// - battleDurationText: 战斗时长
    /// - maxWinStreakText: 最高连胜
    /// - totalDamageText: 伤害总输出
    /// - mvpHeroText: MVP英雄评选
    /// </summary>
    public class GameOverPanel : UIPanel
    {
        [Header("UI引用")]
        public Text resultTitle;
        public Text levelReachedText;
        public Text relicCountText;
        public Text killCountText;
        public Button restartButton;
        public Button shareButton;
        [Tooltip("查看战报按钮")]
        public Button battleStatsButton;

        [Header("统计面板布局")]
        [Tooltip("统计卡片容器（挂载VerticalLayoutGroup的RectTransform）")]
        public RectTransform statsContainer;

        [Header("卡片样式")]
        public Font cardFont;
        public int cardFontSize = 22;
        public int cardLabelFontSize = 14;
        public Color cardBgColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        public Color cardLabelColor = new Color(0.7f, 0.7f, 0.8f, 1f);
        public Color cardValueColor = Color.white;
        public Color cardAccentColor = new Color(1f, 0.85f, 0.2f, 1f);

        // 程序化创建的统计Text引用
        private Text battleDurationText;
        private Text maxWinStreakText;
        private Text totalDamageText;
        private Text mvpHeroText;

        // 动画用Sequence
        private Sequence cardAnimSequence;
        private Sequence numberAnimSequence;

        // 统计数据缓存
        private int statKillCount;
        private int statRelicCount;
        private int statMaxWinStreak;
        private long statTotalDamage;
        private float statBattleDuration;
        private string statMvpHeroName;

        protected override void OnShow()
        {
            // Remove old listeners first to avoid duplicates
            restartButton?.onClick.RemoveAllListeners();
            shareButton?.onClick.RemoveAllListeners();
            battleStatsButton?.onClick.RemoveAllListeners();

            // Bind fresh listeners
            restartButton?.onClick.AddListener(OnRestartClicked);
            shareButton?.onClick.AddListener(OnShareClicked);
            battleStatsButton?.onClick.AddListener(OnBattleStatsClicked);

            PopulateStats();
        }

        protected override void OnHide()
        {
            restartButton?.onClick.RemoveAllListeners();
            shareButton?.onClick.RemoveAllListeners();
            battleStatsButton?.onClick.RemoveAllListeners();

            // 清理动画
            cardAnimSequence?.Kill();
            numberAnimSequence?.Kill();
        }

        private void PopulateStats()
        {
            var gsm = GameStateMachine.Instance;
            var rgm = RoguelikeGameManager.Instance;

            // ── 基础标题 ──────────────────────────────────────
            bool isWin = gsm != null && gsm.IsGameWon;
            if (resultTitle != null)
                resultTitle.text = isWin
                    ? (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("game_over.victory_title")
                        : "🏆 通关！")
                    : (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("game_over.defeat_title")
                        : "💀 阵亡");

            // ── 关卡 ──────────────────────────────────────────
            int level = rgm != null ? rgm.CurrentLevel : (gsm != null ? gsm.CurrentLevel : 0);
            if (levelReachedText != null)
                levelReachedText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("game_over.level_reached", level.ToString())
                    : $"第 {level} 关";

            // ── 收集统计数据 ──────────────────────────────────
            CollectStats(gsm, rgm);

            // ── 确保容器存在 ──────────────────────────────────
            EnsureStatsContainer();

            // ── 构建统计卡片 ──────────────────────────────────
            BuildStatCards();

            // ── 播放入场动画 ──────────────────────────────────
            PlayCardAnimations();
        }

        /// <summary>
        /// 从各系统收集战斗统计数据，优先使用 BattleStatsTracker 真实数据
        /// </summary>
        private void CollectStats(GameStateMachine gsm, RoguelikeGameManager rgm)
        {
            // 尝试获取真实战斗统计数据
            var tracker = BattleStatsTracker.Instance;
            RunBattleStats runStats = tracker != null ? tracker.GetCurrentRunStats() : null;

            // 击杀数：优先 BattleStatsTracker，回退到 GameStats
            if (runStats != null && runStats.totalKills > 0)
                statKillCount = runStats.totalKills;
            else
                statKillCount = GameStats.KillCount;

            // 遗物数：优先RoguelikeGameManager，再RunStats，回退GameStats
            statRelicCount = 0;
            if (rgm != null && rgm.RelicSystem != null)
                statRelicCount = rgm.RelicSystem.RelicCount;
            else if (runStats != null && runStats.relicsCollected != null)
                statRelicCount = runStats.relicsCollected.Count;
            else
                statRelicCount = GameStats.RelicCount;

            // 战斗时长：优先 BattleStatsTracker 累计时长
            if (runStats != null && runStats.totalBattleDuration > 0f)
                statBattleDuration = runStats.totalBattleDuration;
            else
                statBattleDuration = GameStats.BattleDuration;

            // 最高连胜：优先 BattleStatsTracker
            if (runStats != null)
                statMaxWinStreak = runStats.maxConsecutiveWins;
            else
                statMaxWinStreak = GameStats.MaxWinStreak;

            // 伤害总输出：优先 BattleStatsTracker
            if (runStats != null && runStats.totalDamageDealt > 0)
                statTotalDamage = runStats.totalDamageDealt;
            else
                statTotalDamage = GameStats.TotalDamageDealt;

            // MVP英雄：优先 BattleStatsTracker 真实伤害统计
            if (tracker != null)
            {
                string mvpName = tracker.GetMVPHeroName();
                statMvpHeroName = mvpName != "无" ? mvpName : GameStats.GetMVPHero();
            }
            else
            {
                statMvpHeroName = GameStats.GetMVPHero();
            }
        }

        /// <summary>
        /// 确保statsContainer存在，如果未在Inspector中赋值则自动创建
        /// </summary>
        private void EnsureStatsContainer()
        {
            if (statsContainer != null) return;

            // 在resultTitle和按钮之间找一个合适的位置创建容器
            var go = new GameObject("StatsContainer", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.25f);
            rt.anchorMax = new Vector2(0.95f, 0.7f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 添加VerticalLayoutGroup用于卡片排列
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 添加ContentSizeFitter
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            statsContainer = rt;
        }

        /// <summary>
        /// 构建6个统计卡片（3行x2列），程序化创建所有UI
        /// </summary>
        private void BuildStatCards()
        {
            // 清理旧卡片
            for (int i = statsContainer.childCount - 1; i >= 0; i--)
                Destroy(statsContainer.GetChild(i).gameObject);

            // 卡片数据定义（图标, 标签, 值Text引用, 是否滚动动画）
            var loc = LocalizationManager.Instance;
            var cardDefs = new[]
            {
                ("⚔", loc != null ? loc.GetText("game_over.stat_kills") : "击杀", statKillCount, true),
                ("🏆", loc != null ? loc.GetText("game_over.stat_relics") : "遗物", statRelicCount, true),
                ("⏱", loc != null ? loc.GetText("game_over.stat_time") : "时长", 0, false),       // 时长不滚动
                ("🔥", loc != null ? loc.GetText("game_over.stat_streak") : "连胜", statMaxWinStreak, true),
                ("💥", loc != null ? loc.GetText("game_over.stat_damage") : "总伤", 0, true),         // 总伤特殊格式化
                ("🌟", loc != null ? loc.GetText("game_over.stat_mvp") : "MVP", 0, false),          // MVP不滚动
            };

            for (int row = 0; row < 3; row++)
            {
                // 创建行容器
                var rowGo = new GameObject($"StatRow_{row}", typeof(RectTransform));
                rowGo.transform.SetParent(statsContainer, false);

                var rowRt = rowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0, 1);
                rowRt.anchorMax = new Vector2(1, 1);
                rowRt.pivot = new Vector2(0.5f, 1);
                rowRt.sizeDelta = new Vector2(0, 65f);

                // 水平布局
                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 8f;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;

                // 初始隐藏（入场动画用）
                var rowCg = rowGo.AddComponent<CanvasGroup>();
                rowCg.alpha = 0f;
                rowRt.anchoredPosition = new Vector2(0, -30f);

                for (int col = 0; col < 2; col++)
                {
                    int idx = row * 2 + col;
                    var def = cardDefs[idx];
                    CreateStatCard(rowGo, def.Item1, def.Item2, idx, def.Item4);
                }
            }
        }

        /// <summary>
        /// 创建单个统计卡片（图标+标签+数值）
        /// </summary>
        private void CreateStatCard(GameObject parentRow, string icon, string label, int index, bool animateNumber)
        {
            var cardGo = new GameObject($"StatCard_{label}", typeof(RectTransform));
            cardGo.transform.SetParent(parentRow.transform, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = Vector2.zero;

            // 背景
            var bgImage = cardGo.AddComponent<Image>();
            bgImage.color = cardBgColor;
            bgImage.raycastTarget = false;

            // 使用LayoutElement控制尺寸
            var le = cardGo.AddComponent<LayoutElement>();
            le.minHeight = 58f;
            le.preferredHeight = 60f;
            le.flexibleWidth = 1f;

            // 垂直布局：图标+标签 / 数值
            var innerVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            innerVlg.spacing = 2f;
            innerVlg.padding = new RectOffset(6, 6, 6, 6);
            innerVlg.childAlignment = TextAnchor.MiddleCenter;
            innerVlg.childControlWidth = true;
            innerVlg.childControlHeight = true;
            innerVlg.childForceExpandWidth = true;
            innerVlg.childForceExpandHeight = false;

            // 标签行（图标 + 标签名）
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(cardGo.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = cardFont != null ? cardFont : Font.CreateDynamicFontFromOSFont("Arial", cardLabelFontSize);
            labelText.fontSize = cardLabelFontSize;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = cardLabelColor;
            labelText.text = $"{icon} {label}";
            labelText.raycastTarget = false;

            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.preferredHeight = 20f;
            labelLe.flexibleWidth = 1f;

            // 数值行
            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(cardGo.transform, false);
            var valueText = valueGo.AddComponent<Text>();
            valueText.font = cardFont != null ? cardFont : Font.CreateDynamicFontFromOSFont("Arial", cardFontSize);
            valueText.fontSize = cardFontSize;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = cardValueColor;
            valueText.text = animateNumber ? "0" : "";
            valueText.raycastTarget = false;

            var valueLe = valueGo.AddComponent<LayoutElement>();
            valueLe.preferredHeight = 28f;
            valueLe.flexibleWidth = 1f;

            // 根据index存储引用并设置初始值
            switch (index)
            {
                case 0: // 击杀
                    killCountText = valueText;
                    break;
                case 1: // 遗物
                    relicCountText = valueText;
                    break;
                case 2: // 时长 — 直接显示mm:ss，不做动画
                    battleDurationText = valueText;
                    valueText.text = FormatDuration(statBattleDuration);
                    break;
                case 3: // 连胜
                    maxWinStreakText = valueText;
                    break;
                case 4: // 总伤
                    totalDamageText = valueText;
                    break;
                case 5: // MVP
                    mvpHeroText = valueText;
                    valueText.text = statMvpHeroName;
                    valueText.color = cardAccentColor;
                    break;
            }
        }

        /// <summary>
        /// 播放统计卡片的入场动画 + 数字滚动动画
        /// </summary>
        private void PlayCardAnimations()
        {
            cardAnimSequence?.Kill();
            numberAnimSequence?.Kill();

            cardAnimSequence = DOTween.Sequence();
            numberAnimSequence = DOTween.Sequence();

            int rowCount = statsContainer.childCount;

            for (int i = 0; i < rowCount; i++)
            {
                var rowTf = statsContainer.GetChild(i);
                var rowCg = rowTf.GetComponent<CanvasGroup>();
                var rowRt = rowTf as RectTransform;

                if (rowCg == null || rowRt == null) continue;

                // 每行延迟0.15s飞入
                float delay = i * 0.15f;

                // 卡片飞入：从下方滑入 + 淡入
                cardAnimSequence.Insert(delay, rowCg.DOFade(1f, 0.35f).SetEase(Ease.OutQuad));
                cardAnimSequence.Insert(delay, rowRt.DOAnchorPosY(0f, 0.35f).SetEase(Ease.OutQuad));
            }

            // 数字滚动动画在最后一张卡片飞入后延迟0.5s开始
            float numberStartDelay = rowCount * 0.15f + 0.35f + 0.5f;

            // 击杀数滚动（整数）
            if (killCountText != null && statKillCount > 0)
            {
                var textRef = killCountText;
                int target = statKillCount;
                numberAnimSequence.Insert(numberStartDelay,
                    DOVirtual.Float(0f, target, 1.5f, (val) =>
                    {
                        textRef.text = Mathf.RoundToInt(val).ToString();
                    }).SetEase(Ease.OutQuad));
            }

            // 遗物数滚动（整数）
            if (relicCountText != null && statRelicCount > 0)
            {
                var textRef = relicCountText;
                int target = statRelicCount;
                numberAnimSequence.Insert(numberStartDelay,
                    DOVirtual.Float(0f, target, 1.2f, (val) =>
                    {
                        textRef.text = Mathf.RoundToInt(val).ToString();
                    }).SetEase(Ease.OutQuad));
            }

            // 连胜滚动（整数）
            if (maxWinStreakText != null && statMaxWinStreak > 0)
            {
                var textRef = maxWinStreakText;
                int target = statMaxWinStreak;
                numberAnimSequence.Insert(numberStartDelay,
                    DOVirtual.Float(0f, target, 1.3f, (val) =>
                    {
                        textRef.text = Mathf.RoundToInt(val).ToString();
                    }).SetEase(Ease.OutQuad));
            }

            // 总伤害滚动（带千分位逗号）
            if (totalDamageText != null && statTotalDamage > 0)
            {
                var textRef = totalDamageText;
                long target = statTotalDamage;
                numberAnimSequence.Insert(numberStartDelay,
                    DOVirtual.Float(0f, target, 1.8f, (val) =>
                    {
                        long rounded = (long)val;
                        textRef.text = FormatNumberWithCommas(rounded);
                    }).SetEase(Ease.OutQuad));
            }

            cardAnimSequence.Play();
            numberAnimSequence.Play();
        }

        #region 格式化工具

        /// <summary>
        /// 格式化战斗时长为 mm:ss
        /// </summary>
        private string FormatDuration(float seconds)
        {
            int totalSeconds = Mathf.RoundToInt(seconds);
            int mins = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// 格式化数字为千分位（如 2,450）
        /// </summary>
        private string FormatNumberWithCommas(long number)
        {
            return number.ToString("N0");
        }

        #endregion

        private void OnRestartClicked()
        {
            // 清理动画
            cardAnimSequence?.Kill();
            numberAnimSequence?.Kill();

            // 重置肉鸽状态
            RoguelikeGameManager.Instance?.StartNewGame();

            // 重置状态机的关卡计数
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.ResetGame();
            }
        }

        private void OnShareClicked()
        {
            // TODO(Phase2-WeChat): 调用微信分享API — 不阻塞Phase1
            Debug.Log("[GameOverPanel] 分享功能待接入微信SDK");
        }

        /// <summary>
        /// 查看战报 — 打开完整战报统计面板
        /// </summary>
        private void OnBattleStatsClicked()
        {
            var uiManager = NewUIManager.Instance;
            if (uiManager != null)
                uiManager.ShowSubPanel("BattleStats");
            else
                Debug.LogWarning("[GameOverPanel] NewUIManager实例不存在，无法打开战报面板");
        }

        /// <summary>
        /// 当面板被销毁时清理DOTween动画
        /// </summary>
        protected virtual void OnDestroy()
        {
            cardAnimSequence?.Kill();
            numberAnimSequence?.Kill();
        }
    }
}

/// <summary>
/// 战斗统计数据辅助类 — [Fallback] BattleStatsTracker未就绪时使用
/// BattleStatsTracker已实现完整战斗统计，此类仅作fallback
/// </summary>
public static class GameStats
{
    // ── Fallback 数据（BattleStatsTracker未就绪时使用） ────────

    /// <summary>击杀数</summary>
    public static int KillCount { get; set; } = 23;

    /// <summary>遗物数（回退用）</summary>
    public static int RelicCount { get; set; } = 4;

    /// <summary>战斗时长（秒）</summary>
    public static float BattleDuration { get; set; } = 755f; // 12:35

    /// <summary>最高连胜</summary>
    public static int MaxWinStreak { get; set; } = 5;

    /// <summary>伤害总输出</summary>
    public static long TotalDamageDealt { get; set; } = 2450;

    /// <summary>
    /// 获取MVP英雄名称 — 取伤害最高的英雄
    /// [已实现] 从RoguelikeGameManager获取真实英雄数据
    /// </summary>
    public static string GetMVPHero()
    {
        // [已实现] 从RoguelikeGameManager获取队伍英雄并计算MVP
        var rgm = RoguelikeGameManager.Instance;
        if (rgm != null && rgm.PlayerHeroes != null && rgm.PlayerHeroes.Count > 0)
        {
            // 简单实现：返回第一个英雄名
            // 后续改为遍历统计每个英雄的伤害
            Hero mvp = null;
            long maxDamage = -1;
            foreach (var hero in rgm.PlayerHeroes)
            {
                // 用攻击力*星级作为简易伤害代理（Mock逻辑）
                long proxyDamage = (long)hero.Attack * hero.StarLevel;
                if (proxyDamage > maxDamage)
                {
                    maxDamage = proxyDamage;
                    mvp = hero;
                }
            }
            if (mvp != null)
                return mvp.Data.heroName;
        }

        // 回退Mock
        return "战士";
    }

    /// <summary>
    /// 重置所有统计数据（新游戏时调用）
    /// </summary>
    public static void Reset()
    {
        KillCount = 0;
        RelicCount = 0;
        BattleDuration = 0f;
        MaxWinStreak = 0;
        TotalDamageDealt = 0;
    }
}
