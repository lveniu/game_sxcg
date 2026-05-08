using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 战斗观战面板 — 自动战斗观战UI
    /// 
    /// 竖屏720x1280布局（Inspector拖拽绑定）：
    /// ┌──────────────────────────────┐
    /// │  ⏱ 00:45  │  回合 3  │ x1▶  │  顶栏
    /// ├──────────────────────────────┤
    /// │  [骰子技能按钮]  [跳过战斗]  │  控制栏
    /// ├──────────────────────────────┤
    /// │ 敌方（右/红）                 │
    /// │  🗡骷髅兵 ████████░░ 80%     │
    /// │  🛡精英   ██████░░░░ 60%     │
    /// ├──────────────────────────────┤
    /// │        ⚔ VS ⚔               │  中间分隔
    /// ├──────────────────────────────┤
    /// │ 我方（左/蓝）                 │
    /// │  ⚔战士   █████████░ 90%     │
    /// │  🔮法师   ███████░░░ 70%     │
    /// │  🗡刺客   ██████████ 100%    │
    /// ├──────────────────────────────┤
    /// │ 战斗日志（滚动）              │
    /// │ > 战士 攻击 骷髅兵 -32       │
    /// │ > 骷髅兵 攻击 战士 -15       │
    /// │ > [骰子] 三条AOE！120伤害    │
    /// └──────────────────────────────┘
    /// 
    /// 结束弹窗（覆盖层）：
    /// ┌──────────────────────────────┐
    /// │      🏆 战斗胜利！           │
    /// │   或  💀 战斗失败...          │
    /// │   (自动3秒后进入结算)         │
    /// └──────────────────────────────┘
    /// </summary>
    public class BattlePanel : UIPanel
    {
        // ========== 顶栏信息 ==========
        [Header("顶栏")]
        public Text timerText;
        public Text roundText;
        public Text speedText;

        // ========== 控制按钮 ==========
        [Header("控制按钮")]
        public Button speedButton;
        public Button skipButton;
        public Button diceSkillButton;
        public Text diceSkillButtonText;
        public Image diceSkillButtonBg;

        // ========== 单位展示 ==========
        [Header("我方单位容器")]
        public RectTransform playerUnitsContainer;
        public GameObject unitBarPrefab;

        [Header("敌方单位容器")]
        public RectTransform enemyUnitsContainer;

        // ========== 战斗日志 ==========
        [Header("战斗日志")]
        public ScrollRect logScrollRect;
        public Text logText;
        public int maxLogLines = 50;

        // ========== VS分隔 ==========
        [Header("VS分隔")]
        public RectTransform vsDivider;

        // ========== 结束弹窗 ==========
        [Header("结束弹窗")]
        public RectTransform resultPopup;
        public Text resultTitleText;
        public Text resultSubText;
        public Image resultBg;
        public float autoCloseDelay = 3f;

        // ========== 内部状态 ==========
        private const float REFRESH_INTERVAL = 0.2f; // 血条刷新频率
        private float refreshTimer = 0f;

        // 单位血条缓存
        private class UnitBar
        {
            public RectTransform rect;
            public Text nameText;
            public Image healthBarFill;
            public Text healthText;
            public Image borderImage;
            public CanvasGroup canvasGroup;
            public Hero hero; // 绑定的Hero引用
        }

        private List<UnitBar> playerBars = new List<UnitBar>();
        private List<UnitBar> enemyBars = new List<UnitBar>();

        // 日志缓存
        private List<string> logLines = new List<string>();

        // 速度显示
        private static readonly string[] SPEED_LABELS = { "x1", "x2", "x4" };

        // 骰子技能颜色
        private static readonly Color DICE_SKILL_READY = new Color(1f, 0.85f, 0.2f);   // 金色
        private static readonly Color DICE_SKILL_USED = new Color(0.4f, 0.4f, 0.4f);    // 灰色

        // 我方/敌方颜色
        private static readonly Color PLAYER_COLOR = new Color(0.3f, 0.6f, 1f);     // 蓝色
        private static readonly Color ENEMY_COLOR = new Color(1f, 0.35f, 0.3f);     // 红色
        private static readonly Color HEALTH_HIGH = new Color(0.3f, 0.85f, 0.4f);   // 绿
        private static readonly Color HEALTH_MID = new Color(1f, 0.8f, 0.2f);       // 黄
        private static readonly Color HEALTH_LOW = new Color(1f, 0.3f, 0.3f);       // 红

        protected override void Awake()
        {
            base.Awake();
            panelId = "Battle";
        }

        protected override void OnShow()
        {
            // 清除旧监听器
            speedButton?.onClick.RemoveAllListeners();
            skipButton?.onClick.RemoveAllListeners();
            diceSkillButton?.onClick.RemoveAllListeners();

            // 绑定按钮事件
            speedButton?.onClick.AddListener(OnSpeedClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
            diceSkillButton?.onClick.AddListener(OnDiceSkillClicked);

            // 订阅后端事件
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.OnBattleStarted += OnBattleStarted;
                bm.OnBattleEnded += OnBattleEnded;
                bm.OnBattleSpeedChanged += OnBattleSpeedChanged;
                bm.OnDiceSkillTriggered += OnDiceSkillTriggered;
            }

            // 初始化日志
            logLines.Clear();
            if (logText != null) logText.text = "";
            AddLog("等待战斗开始...");

            // 隐藏结果弹窗
            if (resultPopup != null) resultPopup.gameObject.SetActive(false);

            // 初始化血条
            ClearUnitBars();
            RefreshUnitBars();

            // 更新骰子技能按钮
            RefreshDiceSkillButton();

            // 更新速度显示
            RefreshSpeedDisplay();

            refreshTimer = 0f;
        }

        protected override void OnHide()
        {
            // 取消后端事件订阅
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.OnBattleStarted -= OnBattleStarted;
                bm.OnBattleEnded -= OnBattleEnded;
                bm.OnBattleSpeedChanged -= OnBattleSpeedChanged;
                bm.OnDiceSkillTriggered -= OnDiceSkillTriggered;
            }

            speedButton?.onClick.RemoveAllListeners();
            skipButton?.onClick.RemoveAllListeners();
            diceSkillButton?.onClick.RemoveAllListeners();

            // 清理DOTween动画，防止面板隐藏后回调操作已销毁的对象
            resultPopup?.DOKill();
            if (resultPopup != null) resultPopup.gameObject.SetActive(false);
            vsDivider?.DOKill();

            // 清理血条
            ClearUnitBars();
        }

        /// <summary>
        /// 每帧刷新血条（轮询方式，因为Hero没有血量变化事件）
        /// </summary>
        private void Update()
        {
            if (!IsVisible) return;

            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsBattleActive) return;

            // 限制刷新频率
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= REFRESH_INTERVAL)
            {
                refreshTimer = 0f;
                RefreshUnitBars();
                RefreshTimer();
            }
        }

        // ========== 事件回调 ==========

        private void OnBattleStarted()
        {
            AddLog("⚔ 战斗开始！");
            ClearUnitBars();
            RefreshUnitBars();

            // VS分隔动画
            if (vsDivider != null)
            {
                vsDivider.localScale = Vector3.zero;
                vsDivider.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            }

            // 更新骰子技能按钮
            RefreshDiceSkillButton();
        }

        private void OnBattleEnded(bool won)
        {
            AddLog(won ? "🏆 战斗胜利！" : "💀 战斗失败...");

            // 禁用控制按钮
            if (speedButton != null) speedButton.interactable = false;
            if (skipButton != null) skipButton.interactable = false;
            if (diceSkillButton != null) diceSkillButton.interactable = false;

            // 显示结果弹窗
            ShowResultPopup(won);
        }

        private void OnBattleSpeedChanged(float newSpeed)
        {
            RefreshSpeedDisplay();
            string label = GetSpeedLabel(newSpeed);
            AddLog($"速度切换：{label}");
        }

        private void OnDiceSkillTriggered(string message)
        {
            AddLog($"🎲 {message}");

            // 按钮灰化动画
            if (diceSkillButtonBg != null)
            {
                diceSkillButtonBg.DOColor(DICE_SKILL_USED, 0.3f);
            }
        }

        // ========== 按钮事件 ==========

        private void OnSpeedClicked()
        {
            BattleManager.Instance?.CycleBattleSpeed();
        }

        private void OnSkipClicked()
        {
            BattleManager.Instance?.SkipBattle();
            AddLog("⏭ 跳过战斗");
        }

        private void OnDiceSkillClicked()
        {
            var bm = BattleManager.Instance;
            if (bm == null || bm.DiceSkillUsed) return;

            bm.TriggerDiceSkill();
            RefreshDiceSkillButton();
        }

        // ========== 血条刷新 ==========

        private void ClearUnitBars()
        {
            ClearBarsInContainer(playerUnitsContainer, playerBars);
            ClearBarsInContainer(enemyUnitsContainer, enemyBars);
            playerBars.Clear();
            enemyBars.Clear();
        }

        private void ClearBarsInContainer(RectTransform container, List<UnitBar> bars)
        {
            if (container == null) return;
            foreach (var bar in bars)
            {
                if (bar.rect != null) Destroy(bar.rect.gameObject);
            }
        }

        private void RefreshUnitBars()
        {
            var bm = BattleManager.Instance;
            if (bm == null) return;

            RefreshSideBars(bm.playerUnits, playerUnitsContainer, playerBars, true);
            RefreshSideBars(bm.enemyUnits, enemyUnitsContainer, enemyBars, false);
        }

        private void RefreshSideBars(List<Hero> units, RectTransform container, List<UnitBar> bars, bool isPlayer)
        {
            if (container == null) return;

            // 创建缺失的血条
            while (bars.Count < units.Count)
            {
                var bar = CreateUnitBar(container, isPlayer);
                bars.Add(bar);
            }

            // 隐藏多余的血条
            for (int i = units.Count; i < bars.Count; i++)
            {
                if (bars[i].rect != null)
                    bars[i].rect.gameObject.SetActive(false);
            }

            // 更新血条数据
            for (int i = 0; i < units.Count; i++)
            {
                var hero = units[i];
                var bar = bars[i];
                if (hero == null || bar.rect == null) continue;

                bar.rect.gameObject.SetActive(true);
                bar.hero = hero;

                // 名称
                string classIcon = GetClassIcon(hero.Data.heroClass);
                string starStr = GetStarString(hero.StarLevel);
                if (bar.nameText != null)
                {
                    bar.nameText.text = $"{classIcon}{hero.Data.heroName} {starStr}";
                    bar.nameText.color = isPlayer ? PLAYER_COLOR : ENEMY_COLOR;
                }

                // 血量百分比
                float healthPercent = hero.MaxHealth > 0 ? (float)hero.CurrentHealth / hero.MaxHealth : 0f;
                bool isDead = hero.IsDead;

                // 血条填充
                if (bar.healthBarFill != null)
                {
                    bar.healthBarFill.fillAmount = healthPercent;
                    bar.healthBarFill.color = isDead
                        ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                        : GetHealthColor(healthPercent);
                }

                // 血量文本
                if (bar.healthText != null)
                {
                    bar.healthText.text = isDead
                        ? "💀 阵亡"
                        : $"{hero.CurrentHealth}/{hero.MaxHealth}";
                }

                // 边框颜色
                if (bar.borderImage != null)
                {
                    bar.borderImage.color = isDead
                        ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                        : isPlayer ? PLAYER_COLOR : ENEMY_COLOR;
                }

                // 死亡灰化
                if (bar.canvasGroup != null)
                {
                    bar.canvasGroup.alpha = isDead ? 0.4f : 1f;
                }
            }
        }

        private UnitBar CreateUnitBar(RectTransform container, bool isPlayer)
        {
            var bar = new UnitBar();

            if (unitBarPrefab != null)
            {
                var go = Instantiate(unitBarPrefab, container);
                bar.rect = go.GetComponent<RectTransform>();

                // 查找子组件
                var nameObj = go.transform.Find("NameText");
                bar.nameText = nameObj?.GetComponent<Text>();

                var fillObj = go.transform.Find("HealthBar/Fill");
                bar.healthBarFill = fillObj?.GetComponent<Image>();

                var hpObj = go.transform.Find("HealthText");
                bar.healthText = hpObj?.GetComponent<Text>();

                var borderObj = go.transform.Find("Border");
                bar.borderImage = borderObj?.GetComponent<Image>();

                bar.canvasGroup = go.GetComponent<CanvasGroup>();
                if (bar.canvasGroup == null)
                    bar.canvasGroup = go.AddComponent<CanvasGroup>();
            }
            else
            {
                // 程序化创建简易血条
                var go = new GameObject(isPlayer ? "PlayerUnit" : "EnemyUnit");
                go.transform.SetParent(container, false);
                bar.rect = go.AddComponent<RectTransform>();
                bar.rect.sizeDelta = new Vector2(300, 40);

                // 背景条
                var bgGo = new GameObject("HealthBar");
                bgGo.transform.SetParent(go.transform, false);
                var bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0.15f, 0.2f);
                bgRect.anchorMax = new Vector2(0.85f, 0.5f);
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                // 填充条
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bgGo.transform, false);
                var fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                bar.healthBarFill = fillGo.AddComponent<Image>();
                bar.healthBarFill.color = HEALTH_HIGH;
                bar.healthBarFill.type = Image.Type.Filled;
                bar.healthBarFill.fillMethod = Image.FillMethod.Horizontal;

                // 名称文本
                var nameGo = new GameObject("NameText");
                nameGo.transform.SetParent(go.transform, false);
                var nameRect = nameGo.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.5f);
                nameRect.anchorMax = new Vector2(1, 1);
                nameRect.offsetMin = new Vector2(5, 0);
                nameRect.offsetMax = new Vector2(-5, 0);
                bar.nameText = nameGo.AddComponent<Text>();
                bar.nameText.fontSize = 16;
                bar.nameText.alignment = TextAnchor.MiddleLeft;
                bar.nameText.color = isPlayer ? PLAYER_COLOR : ENEMY_COLOR;

                // 血量文本
                var hpGo = new GameObject("HealthText");
                hpGo.transform.SetParent(go.transform, false);
                var hpRect = hpGo.AddComponent<RectTransform>();
                hpRect.anchorMin = Vector2.zero;
                hpRect.anchorMax = new Vector2(1, 0.5f);
                hpRect.offsetMin = new Vector2(5, 0);
                hpRect.offsetMax = new Vector2(-5, 0);
                bar.healthText = hpGo.AddComponent<Text>();
                bar.healthText.fontSize = 14;
                bar.healthText.alignment = TextAnchor.MiddleRight;
                bar.healthText.color = Color.white;

                bar.canvasGroup = go.AddComponent<CanvasGroup>();
            }

            // 入场动画
            if (bar.rect != null)
            {
                bar.rect.localScale = Vector3.one * 0.5f;
                bar.rect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }

            return bar;
        }

        // ========== 计时器 ==========

        private void RefreshTimer()
        {
            var bm = BattleManager.Instance;
            if (bm == null || timerText == null) return;

            float remaining = Mathf.Max(0f, bm.maxBattleTime - bm.BattleTimer);
            int seconds = Mathf.CeilToInt(remaining);
            timerText.text = $"⏱ {seconds}s";

            // 低于10秒变红
            if (remaining < 10f)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = Color.white;
            }
        }

        // ========== 速度显示 ==========

        private void RefreshSpeedDisplay()
        {
            var bm = BattleManager.Instance;
            if (bm == null || speedText == null) return;

            speedText.text = GetSpeedLabel(bm.battleSpeed);
        }

        private static string GetSpeedLabel(float speed)
        {
            if (speed >= 4f) return "x4";
            if (speed >= 2f) return "x2";
            return "x1";
        }

        // ========== 骰子技能按钮 ==========

        private void RefreshDiceSkillButton()
        {
            var bm = BattleManager.Instance;
            bool canUse = bm != null && !bm.DiceSkillUsed &&
                          bm.CurrentDiceCombo != null &&
                          bm.CurrentDiceCombo.Type != DiceCombinationType.None;

            if (diceSkillButton != null)
                diceSkillButton.interactable = canUse;

            if (diceSkillButtonBg != null)
                diceSkillButtonBg.color = canUse ? DICE_SKILL_READY : DICE_SKILL_USED;

            if (diceSkillButtonText != null)
            {
                if (bm == null || bm.CurrentDiceCombo == null || bm.CurrentDiceCombo.Type == DiceCombinationType.None)
                {
                    diceSkillButtonText.text = "🎲 无骰子技能";
                }
                else if (bm.DiceSkillUsed)
                {
                    diceSkillButtonText.text = "🎲 已释放";
                }
                else
                {
                    string comboName = GetComboName(bm.CurrentDiceCombo.Type);
                    diceSkillButtonText.text = $"🎲 {comboName}";
                }
            }
        }

        private static string GetComboName(DiceCombinationType type)
        {
            return type switch
            {
                DiceCombinationType.ThreeOfAKind => "三条·AOE",
                DiceCombinationType.Straight => "顺子·加速",
                DiceCombinationType.Pair => "对子·治疗",
                _ => "骰子技能"
            };
        }

        // ========== 战斗日志 ==========

        private void AddLog(string message)
        {
            logLines.Add(message);

            // 限制日志行数
            while (logLines.Count > maxLogLines)
                logLines.RemoveAt(0);

            if (logText != null)
            {
                logText.text = string.Join("\n", logLines);
            }

            // 自动滚动到底部
            if (logScrollRect != null)
            {
                DOVirtual.DelayedCall(0.05f, () =>
                {
                    if (logScrollRect != null)
                        logScrollRect.verticalNormalizedPosition = 0f;
                });
            }
        }

        // ========== 结果弹窗 ==========

        private void ShowResultPopup(bool won)
        {
            if (resultPopup == null)
            {
                // 没有弹窗组件时，用日志替代
                AddLog(won ? ">>> 自动进入结算..." : ">>> 返回主菜单...");
                DOVirtual.DelayedCall(autoCloseDelay, () =>
                {
                    // 结果弹窗关闭后由状态机自动跳转，无需手动操作
                });
                return;
            }

            resultPopup.gameObject.SetActive(true);

            // 标题
            if (resultTitleText != null)
            {
                resultTitleText.text = won ? "🏆 战斗胜利！" : "💀 战斗失败...";
                resultTitleText.color = won ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            }

            // 副标题
            if (resultSubText != null)
            {
                resultSubText.text = won ? "即将进入结算..." : "返回主菜单...";
            }

            // 背景色
            if (resultBg != null)
            {
                resultBg.color = won
                    ? new Color(0.1f, 0.3f, 0.1f, 0.9f)
                    : new Color(0.3f, 0.1f, 0.1f, 0.9f);
            }

            // 入场动画
            resultPopup.localScale = Vector3.zero;
            resultPopup.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

            // 自动关闭（状态机已由BattleManager.EndBattle跳转）
            DOVirtual.DelayedCall(autoCloseDelay, () =>
            {
                if (resultPopup != null)
                {
                    resultPopup.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (resultPopup != null)
                                resultPopup.gameObject.SetActive(false);
                        });
                }
            });
        }

        // ========== 工具方法 ==========

        private static Color GetHealthColor(float percent)
        {
            if (percent > 0.6f) return HEALTH_HIGH;
            if (percent > 0.3f) return HEALTH_MID;
            return HEALTH_LOW;
        }

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

        private static string GetStarString(int starLevel)
        {
            return starLevel switch
            {
                1 => "★",
                2 => "★★",
                3 => "★★★",
                _ => ""
            };
        }
    }
}
