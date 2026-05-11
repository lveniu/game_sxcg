using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 主菜单面板 — 游戏入口界面
    /// 
    /// UI元素：
    /// - gameTitleText: 游戏标题文本
    /// - startButton:   开始游戏按钮
    /// - settingsButton:设置按钮（可选，预留扩展）
    /// - exitButton:    退出游戏按钮
    /// - versionText:   版本号文本（可选，显示在底部）
    /// 
    /// 动画流程：
    /// - 标题从顶部掉落（Ease.OutBounce）
    /// - 按钮从底部依次滑入（0.1s交错延迟）
    /// - 版本号淡入
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        // ============================================================
        // Inspector 字段
        // ============================================================

        [Header("UI引用")]
        [Tooltip("游戏标题文本")]
        public Text gameTitleText;

        [Tooltip("开始游戏按钮")]
        public Button startButton;

        [Tooltip("设置按钮（可选）")]
        public Button settingsButton;

        [Tooltip("成就按钮（可选）")]
        public Button achievementButton;

        [Tooltip("退出游戏按钮")]
        public Button exitButton;

        [Tooltip("版本号文本（可选，底部显示）")]
        public Text versionText;

        [Header("动画配置")]
        [Tooltip("标题初始Y偏移（屏幕上方）")]
        public float titleStartOffsetY = 800f;

        [Tooltip("标题掉落动画时长")]
        public float titleAnimDuration = 0.8f;

        [Tooltip("按钮初始Y偏移（屏幕下方）")]
        public float buttonStartOffsetY = -400f;

        [Tooltip("按钮滑入动画时长")]
        public float buttonAnimDuration = 0.5f;

        [Tooltip("按钮之间交错延迟（秒）")]
        public float buttonStaggerDelay = 0.1f;

        [Tooltip("版本号淡入时长")]
        public float versionFadeDuration = 0.5f;

        [Header("标题配置")]
        [Tooltip("游戏标题文字")]
        public string gameTitle = "骰子勇者";

        // ============================================================
        // 内部缓存
        // ============================================================

        /// <summary>标题RectTransform缓存</summary>
        private RectTransform titleRect;

        /// <summary>所有按钮的RectTransform，用于交错动画</summary>
        private RectTransform[] buttonRects;

        /// <summary>所有活跃的DOTween动画，用于清理</summary>
        private Sequence animSequence;

        // ============================================================
        // 生命周期
        // ============================================================

        protected override void Awake()
        {
            base.Awake();

            // 缓存RectTransform引用
            if (gameTitleText != null)
                titleRect = gameTitleText.GetComponent<RectTransform>();

            // 收集所有按钮RectTransform（动画用）
            var buttonList = new System.Collections.Generic.List<RectTransform>();
            if (startButton != null) buttonList.Add(startButton.GetComponent<RectTransform>());
            if (settingsButton != null) buttonList.Add(settingsButton.GetComponent<RectTransform>());
            if (achievementButton != null) buttonList.Add(achievementButton.GetComponent<RectTransform>());
            if (exitButton != null) buttonList.Add(exitButton.GetComponent<RectTransform>());
            buttonRects = buttonList.ToArray();
        }

        // ============================================================
        // 显示 / 隐藏
        // ============================================================

        protected override void OnShow()
        {
            // --- 清理旧的监听器并重新绑定 ---
            BindButtons();

            // --- 设置标题文字 ---
            if (gameTitleText != null)
                gameTitleText.text = gameTitle;

            // --- 设置版本号 ---
            SetupVersionText();

            // --- 播放入场动画 ---
            PlayEntryAnimations();
        }

        protected override void OnHide()
        {
            // 清理所有按钮监听器
            ClearButtonListeners();

            // 终止未完成的动画
            KillAnimations();
        }

        /// <summary>
        /// 面板禁用时确保动画清理，防止内存泄漏
        /// </summary>
        protected virtual void OnDestroy()
        {
            KillAnimations();
        }

        // ============================================================
        // 按钮绑定
        // ============================================================

        /// <summary>清除所有按钮监听器并重新绑定</summary>
        private void BindButtons()
        {
            startButton?.onClick.RemoveAllListeners();
            settingsButton?.onClick.RemoveAllListeners();
            achievementButton?.onClick.RemoveAllListeners();
            exitButton?.onClick.RemoveAllListeners();

            startButton?.onClick.AddListener(OnStartClicked);
            settingsButton?.onClick.AddListener(OnSettingsClicked);
            achievementButton?.onClick.AddListener(OnAchievementClicked);
            exitButton?.onClick.AddListener(OnExitClicked);
        }

        /// <summary>清除所有按钮监听器</summary>
        private void ClearButtonListeners()
        {
            startButton?.onClick.RemoveAllListeners();
            settingsButton?.onClick.RemoveAllListeners();
            achievementButton?.onClick.RemoveAllListeners();
            exitButton?.onClick.RemoveAllListeners();
        }

        // ============================================================
        // 按钮回调
        // ============================================================

        /// <summary>
        /// 开始游戏 — 初始化肉鸽流程并切换到英雄选择
        /// </summary>
        private void OnStartClicked()
        {
            // 确保RoguelikeGameManager已初始化新游戏
            if (RoguelikeGameManager.Instance != null)
            {
                RoguelikeGameManager.Instance.StartNewGame();
            }
            else
            {
                Debug.LogWarning("[MainMenuPanel] RoguelikeGameManager实例不存在，跳过初始化");
            }

            // 重置状态机的关卡计数和胜负标记
            var gsm = GameStateMachine.Instance;
            if (gsm != null)
            {
                // 直接重置内部状态并跳转（不用ResetGame，因为它会跳到HeroSelect，我们也是要跳HeroSelect）
                gsm.ResetGame();
            }
        }

        /// <summary>
        /// 设置按钮 — 打开设置子面板
        /// </summary>
        private void OnSettingsClicked()
        {
            var uiManager = NewUIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowSubPanel("Settings");
            }
        }

        /// <summary>
        /// 成就按钮 — 打开成就面板
        /// </summary>
        private void OnAchievementClicked()
        {
            var uiManager = NewUIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowSubPanel("Achievement");
            }
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        private void OnExitClicked()
        {
#if UNITY_EDITOR
            // 编辑器模式下停止运行
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // 真机构建下退出应用
            Application.Quit();
#endif
        }

        // ============================================================
        // 版本号
        // ============================================================

        /// <summary>
        /// 初始化版本号文本
        /// 优先使用Application.version，未设置时显示默认值
        /// </summary>
        private void SetupVersionText()
        {
            if (versionText == null) return;

            string ver = Application.version;
            // Application.version 未设置时可能为空或默认值
            if (string.IsNullOrEmpty(ver) || ver == "0.0" || ver == "0.0.0")
                ver = "1.0.0";

            versionText.text = $"v{ver}";
            versionText.color = new Color(versionText.color.r, versionText.color.g, versionText.color.b, 0f);
        }

        // ============================================================
        // DOTween 入场动画
        // ============================================================

        /// <summary>
        /// 播放完整的入场动画序列：
        /// 1. 标题从顶部掉落（带弹跳缓动）
        /// 2. 按钮从底部依次滑入（交错延迟）
        /// 3. 版本号淡入
        /// </summary>
        private void PlayEntryAnimations()
        {
            // 终止上一次未完成的动画
            KillAnimations();

            animSequence = DOTween.Sequence();
            animSequence.SetTarget(this);
            animSequence.SetUpdate(true); // 即使Time.timeScale=0也能播放

            // --- 标题动画：从顶部掉落 ---
            if (titleRect != null)
            {
                float targetY = titleRect.anchoredPosition.y;
                titleRect.anchoredPosition = new Vector2(
                    titleRect.anchoredPosition.x,
                    targetY + titleStartOffsetY
                );

                animSequence.Append(
                    titleRect.DOAnchorPosY(targetY, titleAnimDuration)
                        .SetEase(Ease.OutBounce)
                );
            }

            // --- 按钮动画：从底部依次滑入 ---
            for (int i = 0; i < buttonRects.Length; i++)
            {
                var btnRect = buttonRects[i];
                if (btnRect == null) continue;

                float targetY = btnRect.anchoredPosition.y;

                // 初始位置偏移到底部外
                btnRect.anchoredPosition = new Vector2(
                    btnRect.anchoredPosition.x,
                    targetY + buttonStartOffsetY
                );

                // 第一个按钮用Join（与标题动画并行也可以，或者Append接在后面）
                // 这里用Insert实现交错：在标题动画完成后依次出现
                float delay = titleAnimDuration + buttonStaggerDelay * i;

                animSequence.Insert(delay,
                    btnRect.DOAnchorPosY(targetY, buttonAnimDuration)
                        .SetEase(Ease.OutCubic)
                );
            }

            // --- 版本号淡入 ---
            if (versionText != null)
            {
                float versionDelay = titleAnimDuration + buttonStaggerDelay * buttonRects.Length;
                animSequence.Insert(versionDelay,
                    versionText.DOFade(1f, versionFadeDuration)
                        .SetEase(Ease.OutQuad)
                );
            }

            animSequence.Play();
        }

        /// <summary>
        /// 终止所有进行中的动画，防止状态泄漏
        /// </summary>
        private void KillAnimations()
        {
            if (animSequence != null && animSequence.IsActive())
            {
                animSequence.Kill();
                animSequence = null;
            }

            // 单独清理各元素的残留Tween（安全兜底）
            if (titleRect != null) titleRect.DOKill();
            foreach (var btnRect in buttonRects)
            {
                if (btnRect != null) btnRect.DOKill();
            }
            if (versionText != null) versionText.DOKill();
        }
    }
}
