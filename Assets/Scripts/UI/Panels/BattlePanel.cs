using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 战斗观战面板增强版 — FE-04
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  ⏱ 00:45  │  回合 3  │ x1▶  │  顶栏
    /// ├──────────────────────────────┤
    /// │  [骰子技能按钮]  [跳过战斗]  │  控制栏
    /// │  遗物图标栏: 🛡🎲✨         │  遗物栏(新增)
    /// ├──────────────────────────────┤
    /// │ 敌方（右/红）                 │
    /// │  🗡骷髅兵 ████████░░ 80%     │
    /// │  🛡精英   ██████░░░░ 60%     │
    /// ├──────────────────────────────┤
    /// │        ⚔ VS ⚔               │  中间分隔
    /// ├──────────────────────────────┤
    /// │ 我方（左/蓝）— 属性面板      │
    /// │  ⚔战士 ★★★ HP150 ATK8 DEF10 │  显示ConfigLoader数值
    /// │  🔮法师 ★★  HP70  ATK12 DEF3│
    /// │  🗡刺客 ★   HP70  ATK16 DEF3 │
    /// ├──────────────────────────────┤
    /// │ 战斗日志（滚动）              │
    /// └──────────────────────────────┘
    /// 
    /// 骰子技能特效层（全屏覆盖）：
    /// ┌──────────────────────────────┐
    /// │   ✨ 全屏闪光 + 伤害飘字 ✨  │
    /// │   -32  -45  CRIT! -80       │
    /// └──────────────────────────────┘
    /// 
    /// 结束弹窗（覆盖层）：
    /// ┌──────────────────────────────┐
    /// │    🏆 战斗胜利！/ 💀 失败    │
    /// │    奖励预览: xxx              │
    /// │    [确认进入结算]             │
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

        // ========== 遗物图标栏 (FE-04.3 新增) ==========
        [Header("遗物图标栏")]
        public RectTransform relicBarContainer;
        public GameObject relicIconPrefab;
        public float relicIconSize = 40f;
        public float relicIconSpacing = 8f;

        // ========== 单位展示 ==========
        [Header("我方单位容器")]
        public RectTransform playerUnitsContainer;
        public GameObject unitBarPrefab;

        [Header("敌方单位容器")]
        public RectTransform enemyUnitsContainer;

        // ========== 骰子技能特效层 (FE-04.2 + FE-04.3 增强) ==========
        [Header("骰子技能特效")]
        public DiceSkillCinematic diceSkillCinematic; // 全屏演出控制器(FE-04.3)
        public Image flashOverlay;               // 全屏闪光遮罩(降级兼容)
        public RectTransform damageNumbersContainer;  // 伤害数字容器
        public GameObject damageNumberPrefab;     // 伤害数字预制体
        public float flashDuration = 0.4f;
        public float flashMaxAlpha = 0.6f;
        public float damageNumberDuration = 1.2f;

        // ========== 战斗日志 ==========
        [Header("战斗日志")]
        public ScrollRect logScrollRect;
        public Text logText;
        public int maxLogLines = 50;

        // ========== VS分隔 ==========
        [Header("VS分隔")]
        public RectTransform vsDivider;

        // ========== 结束弹窗 (FE-04.4 增强) ==========
        [Header("结束弹窗")]
        public RectTransform resultPopup;
        public Text resultTitleText;
        public Text resultSubText;
        public Image resultBg;
        public RectTransform resultIcon;         // 胜利/失败大图标
        public RectTransform rewardPreview;      // 奖励预览区
        public Text rewardPreviewText;
        public Button resultConfirmButton;       // 确认按钮（替代自动关闭）
        public float autoCloseDelay = 3f;

        // ========== 内部状态 ==========
        private const float REFRESH_INTERVAL = 0.2f;
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
            // FE-04.1: 属性面板子元素
            public Text statsText;    // 属性数值
            public Hero hero;
        }

        private List<UnitBar> playerBars = new List<UnitBar>();
        private List<UnitBar> enemyBars = new List<UnitBar>();

        // 遗物图标缓存
        private class RelicIconEntry
        {
            public RectTransform rect;
            public Text emojiText;
            public GameObject tooltip;
            public Text tooltipNameText;
            public Text tooltipDescText;
            public string relicId;
        }
        private List<RelicIconEntry> relicIconEntries = new List<RelicIconEntry>();

        // 日志缓存
        private List<string> logLines = new List<string>();

        // Bug#4 fix: 追踪所有活跃的DOTween，防止切场景泄漏
        private List<Tween> activeTweens = new List<Tween>();

        // 速度显示
        private static readonly string[] SPEED_LABELS = { "x1", "x2", "x4" };

        // 骰子技能颜色
        private static readonly Color DICE_SKILL_READY = new Color(1f, 0.85f, 0.2f);
        private static readonly Color DICE_SKILL_USED = new Color(0.4f, 0.4f, 0.4f);

        // 我方/敌方颜色
        private static readonly Color PLAYER_COLOR = new Color(0.3f, 0.6f, 1f);
        private static readonly Color ENEMY_COLOR = new Color(1f, 0.35f, 0.3f);
        private static readonly Color HEALTH_HIGH = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color HEALTH_MID = new Color(1f, 0.8f, 0.2f);
        private static readonly Color HEALTH_LOW = new Color(1f, 0.3f, 0.3f);

        // 骰子技能闪光颜色
        private static readonly Color FLASH_COLOR_THREE = new Color(1f, 0.85f, 0.2f, 0.6f);  // 金
        private static readonly Color FLASH_COLOR_STRAIGHT = new Color(0.2f, 0.6f, 1f, 0.6f); // 蓝
        private static readonly Color FLASH_COLOR_PAIR = new Color(0.2f, 0.85f, 0.4f, 0.6f); // 绿

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
            resultConfirmButton?.onClick.RemoveAllListeners();

            // 绑定按钮事件
            speedButton?.onClick.AddListener(OnSpeedClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
            diceSkillButton?.onClick.AddListener(OnDiceSkillClicked);
            resultConfirmButton?.onClick.AddListener(OnResultConfirmClicked);
            if (resultConfirmButton != null) resultConfirmButton.gameObject.SetActive(false);

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

            // 隐藏闪光层
            if (flashOverlay != null)
            {
                flashOverlay.color = Color.clear;
                flashOverlay.gameObject.SetActive(false);
            }

            // FE-04.3: 初始化遗物图标栏
            RefreshRelicBar();

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
            resultConfirmButton?.onClick.RemoveAllListeners();

            // Bug#4 fix: 统一Kill所有追踪的DOTween
            KillAllActiveTweens();

            // 清理特定组件上的DOTween（兜底）
            resultPopup?.DOKill();
            if (resultPopup != null) resultPopup.gameObject.SetActive(false);
            vsDivider?.DOKill();
            flashOverlay?.DOKill();
            damageNumbersContainer?.DOKill();
            relicBarContainer?.DOKill();
            resultIcon?.DOKill();
            rewardPreview?.DOKill();
            resultConfirmButton?.transform.DOKill();

            // FE-04.3: 停止全屏演出
            if (diceSkillCinematic != null) diceSkillCinematic.Stop();

            // 清理血条
            ClearUnitBars();

            // 清理遗物图标
            ClearRelicBar();

            // 清理伤害数字
            ClearDamageNumbers();
        }

        private void Update()
        {
            if (!IsVisible) return;

            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsBattleActive) return;

            refreshTimer += Time.deltaTime;
            if (refreshTimer >= REFRESH_INTERVAL)
            {
                refreshTimer = 0f;
                RefreshUnitBars();
                RefreshTimer();
            }
        }

        // ========== FE-04.1: ConfigLoader数值对接 ==========

        /// <summary>
        /// 获取英雄的属性面板显示文本（来自UIConfigBridge/ConfigLoader）
        /// </summary>
        private string GetHeroStatsText(Hero hero)
        {
            if (hero?.Data == null) return "";

            // 优先从UIConfigBridge获取（已对齐JSON）
            var displayData = UIConfigBridge.GetHeroDisplayData(hero.Data.heroClass);
            if (displayData != null)
            {
                return $"HP{displayData.stats.maxHealth} ATK{displayData.stats.attack} DEF{displayData.stats.defense} SPD{displayData.stats.speed}";
            }

            // 降级：直接读Hero属性
            return $"HP{hero.MaxHealth} ATK{hero.Attack} DEF{hero.Defense} SPD{hero.Speed}";
        }

        // ========== FE-04.2: 骰子技能释放动画 ==========

        private void OnDiceSkillTriggered(string message)
        {
            AddLog($"🎲 {message}");

            // 按钮灰化
            if (diceSkillButtonBg != null)
            {
                diceSkillButtonBg.DOColor(DICE_SKILL_USED, 0.3f);
            }

            // 获取当前骰子组合类型来决定特效颜色
            var bm = BattleManager.Instance;
            var combo = bm?.CurrentDiceCombo;
            if (combo == null) return;

            // FE-04.3: 优先使用全屏演出控制器
            if (diceSkillCinematic != null)
            {
                int[] diceValues = bm.LastDiceValues ?? new int[] { 1, 2, 3 };
                diceSkillCinematic.Play(combo, diceValues);
            }
            else
            {
                // 降级：简单闪光
                PlayFlashEffect(combo.Type);
            }

            // 伤害数字飘出
            PlayDamageNumbers(combo);
        }

        /// <summary>
        /// 全屏闪光特效
        /// </summary>
        private void PlayFlashEffect(DiceCombinationType comboType)
        {
            if (flashOverlay == null) return;

            flashOverlay.gameObject.SetActive(true);

            // 根据组合类型选颜色
            Color flashColor = comboType switch
            {
                DiceCombinationType.ThreeOfAKind => FLASH_COLOR_THREE,
                DiceCombinationType.Straight => FLASH_COLOR_STRAIGHT,
                DiceCombinationType.Pair => FLASH_COLOR_PAIR,
                _ => new Color(1f, 1f, 1f, 0.4f)
            };

            // Bug#4 fix: 闪光序列统一追踪，切场景时可被Kill
            flashOverlay.color = Color.clear;
            var fadeIn = flashOverlay.DOColor(flashColor, flashDuration * 0.3f).SetEase(Ease.OutQuad);
            TrackTween(fadeIn);
            fadeIn.OnComplete(() =>
            {
                if (flashOverlay != null)
                {
                    var fadeOut = flashOverlay.DOColor(Color.clear, flashDuration * 0.7f).SetEase(Ease.InQuad);
                    TrackTween(fadeOut);
                    fadeOut.OnComplete(() =>
                    {
                        if (flashOverlay != null)
                            flashOverlay.gameObject.SetActive(false);
                    });
                }
            });
        }

        /// <summary>
        /// 伤害数字飘出动画
        /// </summary>
        private void PlayDamageNumbers(DiceCombination combo)
        {
            if (damageNumbersContainer == null) return;

            var bm = BattleManager.Instance;
            if (bm?.enemyUnits == null) return;

            // 根据组合类型估算伤害（用于UI展示）
            int baseDamage = 0;
            var diceValues = bm.LastDiceValues;
            if (diceValues != null)
            {
                foreach (var v in diceValues) baseDamage += v;
                baseDamage *= 5;
            }
            else
            {
                baseDamage = 30; // 降级默认值
            }
            bool isCrit = combo.Type == DiceCombinationType.ThreeOfAKind;
            bool isAOE = combo.Type == DiceCombinationType.ThreeOfAKind;

            int enemyCount = bm.enemyUnits.Count;
            if (enemyCount == 0) return;

            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = bm.enemyUnits[i];
                if (enemy == null || enemy.IsDead) continue;

                int dmg = isAOE ? baseDamage : (i == 0 ? baseDamage : 0);
                if (dmg <= 0) continue;

                // 随机偏移，避免重叠
                float xOffset = Random.Range(-200f, 200f);
                float yOffset = Random.Range(-50f, 50f);

                SpawnDamageNumber(
                    new Vector2(xOffset, yOffset),
                    dmg,
                    isCrit && i == 0,
                    combo.Type
                );
            }
        }

        /// <summary>
        /// 生成单个伤害数字
        /// </summary>
        private void SpawnDamageNumber(Vector2 offset, int damage, bool isCrit, DiceCombinationType comboType)
        {
            var go = new GameObject("DmgNum");
            go.transform.SetParent(damageNumbersContainer, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(200, 60);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = isCrit ? 42 : 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GetDamageNumberColor(comboType);
            text.text = isCrit ? $"暴击 -{damage}" : $"-{damage}";

            // 描边效果
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            // 飘出动画：上浮 + 缩放 + 淡出
            Sequence seq = DOTween.Sequence();
            seq.Append(rect.DOScale(isCrit ? 1.5f : 1.2f, 0.2f).SetEase(Ease.OutBack));
            seq.Join(rect.DOAnchorPosY(offset.y + 80f, damageNumberDuration).SetEase(Ease.OutCubic));
            seq.Insert(0.4f, text.DOFade(0f, damageNumberDuration - 0.4f));
            seq.OnComplete(() =>
            {
                // Bug#4 fix: 从追踪列表移除已完成的Tween
                activeTweens.Remove(seq);
                if (go != null) Destroy(go);
            });
            // Bug#4 fix: 追踪Sequence防止泄漏
            TrackTween(seq);
        }

        private static Color GetDamageNumberColor(DiceCombinationType type)
        {
            return type switch
            {
                DiceCombinationType.ThreeOfAKind => new Color(1f, 0.2f, 0.2f),   // 红色(高伤害)
                DiceCombinationType.Straight => new Color(0.3f, 0.7f, 1f),       // 蓝色(速度)
                DiceCombinationType.Pair => new Color(0.3f, 1f, 0.5f),           // 绿色(治疗)
                _ => Color.white
            };
        }

        private void ClearDamageNumbers()
        {
            if (damageNumbersContainer == null) return;
            foreach (Transform child in damageNumbersContainer)
            {
                child.DOKill();
                Destroy(child.gameObject);
            }
        }

        // ========== FE-04.3: 遗物效果图标栏 ==========

        private void RefreshRelicBar()
        {
            ClearRelicBar();

            var relicSys = RoguelikeGameManager.Instance?.RelicSystem;
            if (relicSys == null || relicBarContainer == null) return;

            var ownedRelics = relicSys.OwnedRelics;
            if (ownedRelics == null || ownedRelics.Count == 0) return;

            for (int i = 0; i < ownedRelics.Count; i++)
            {
                var relic = ownedRelics[i];
                if (relic?.Data == null) continue;

                CreateRelicIcon(relic.Data, i);
            }

            // 遗物栏入场动画
            if (relicBarContainer != null)
            {
                relicBarContainer.localScale = Vector3.one * 0.5f;
                relicBarContainer.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }
        }

        private void CreateRelicIcon(RelicData data, int index)
        {
            var displayData = UIConfigBridge.GetRelicDisplayData(data);

            GameObject go;
            RectTransform rect;
            Text emojiText;

            if (relicIconPrefab != null)
            {
                go = Instantiate(relicIconPrefab, relicBarContainer);
                rect = go.GetComponent<RectTransform>();
                var child = go.transform.Find("IconText");
                emojiText = child?.GetComponent<Text>();
            }
            else
            {
                // 程序化创建
                go = new GameObject($"Relic_{data.relicId}");
                go.transform.SetParent(relicBarContainer, false);
                rect = go.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(relicIconSize, relicIconSize);

                // 背景圆
                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                bg.raycastTarget = true;

                // 边框
                var borderGo = new GameObject("Border");
                borderGo.transform.SetParent(go.transform, false);
                var borderRect = borderGo.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                var borderImg = borderGo.AddComponent<Image>();
                borderImg.color = displayData.rarityColor;
                borderImg.raycastTarget = false;

                // Emoji图标
                var emojiGo = new GameObject("IconText");
                emojiGo.transform.SetParent(go.transform, false);
                var emojiRect = emojiGo.AddComponent<RectTransform>();
                emojiRect.anchorMin = Vector2.zero;
                emojiRect.anchorMax = Vector2.one;
                emojiRect.offsetMin = Vector2.zero;
                emojiRect.offsetMax = Vector2.zero;
                emojiText = emojiGo.AddComponent<Text>();
                emojiText.fontSize = 22;
                emojiText.alignment = TextAnchor.MiddleCenter;
                emojiText.raycastTarget = false;
            }

            // 设置内容
            if (emojiText != null)
            {
                emojiText.text = displayData.iconEmoji;
            }

            // 位置（水平排列）
            rect.anchoredPosition = new Vector2(
                index * (relicIconSize + relicIconSpacing),
                0f
            );

            // 创建Tooltip
            var entry = new RelicIconEntry
            {
                rect = rect,
                emojiText = emojiText,
                relicId = data.relicId
            };
            CreateRelicTooltip(entry, displayData, go);
            relicIconEntries.Add(entry);
        }

        private void CreateRelicTooltip(RelicIconEntry entry, RelicDisplayData displayData, GameObject parent)
        {
            // Tooltip（默认隐藏，鼠标悬停显示）
            var tooltipGo = new GameObject("Tooltip");
            tooltipGo.transform.SetParent(parent.transform, false);
            tooltipGo.SetActive(false);

            var tooltipRect = tooltipGo.AddComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 0f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0f);
            tooltipRect.pivot = new Vector2(0.5f, 1f);
            tooltipRect.anchoredPosition = new Vector2(0f, -relicIconSize / 2f - 4f);
            tooltipRect.sizeDelta = new Vector2(180, 70);

            var bg = tooltipGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // 名称 + 稀有度
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(tooltipGo.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.55f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(8, 0);
            nameRect.offsetMax = new Vector2(-8, -4);
            var nameText = nameGo.AddComponent<Text>();
            nameText.fontSize = 13;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = displayData.rarityColor;
            nameText.text = $"{displayData.rarityName} {displayData.relicName}";
            nameText.raycastTarget = false;

            // 效果描述
            var descGo = new GameObject("DescText");
            descGo.transform.SetParent(tooltipGo.transform, false);
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.55f);
            descRect.offsetMin = new Vector2(8, 4);
            descRect.offsetMax = new Vector2(-8, -2);
            var descText = descGo.AddComponent<Text>();
            descText.fontSize = 11;
            descText.alignment = TextAnchor.UpperLeft;
            descText.color = new Color(0.85f, 0.85f, 0.85f);
            descText.text = displayData.effectDescription;
            descText.raycastTarget = false;

            entry.tooltip = tooltipGo;
            entry.tooltipNameText = nameText;
            entry.tooltipDescText = descText;

            // 添加悬停事件触发器
            var trigger = parent.AddComponent<EventTrigger>();
            var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ => tooltipGo.SetActive(true));
            trigger.triggers.Add(pointerEnter);

            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ => tooltipGo.SetActive(false));
            trigger.triggers.Add(pointerExit);
        }

        private void ClearRelicBar()
        {
            if (relicBarContainer == null) return;
            foreach (var entry in relicIconEntries)
            {
                if (entry.rect != null)
                {
                    entry.rect.DOKill();
                    Destroy(entry.rect.gameObject);
                }
            }
            relicIconEntries.Clear();
        }

        // ========== FE-04.4: 战斗结束弹窗美化 ==========

        private void ShowResultPopup(bool won)
        {
            if (resultPopup == null)
            {
                AddLog(won ? ">>> 自动进入结算..." : ">>> 返回主菜单...");
                return;
            }

            resultPopup.gameObject.SetActive(true);

            // 标题 + 颜色区分
            if (resultTitleText != null)
            {
                string fullTitle = won ? "🏆 战斗胜利！" : "💀 战斗失败...";
                resultTitleText.color = won
                    ? new Color(1f, 0.85f, 0.2f)
                    : new Color(0.8f, 0.2f, 0.2f);

                if (won)
                {
                    // FE-04.4: 胜利打字机效果
                    resultTitleText.text = "";
                    TypewriterEffect(resultTitleText, fullTitle, 0.08f, 0.3f);
                }
                else
                {
                    resultTitleText.text = fullTitle;
                }
            }

            // 副标题
            if (resultSubText != null)
            {
                var bm = BattleManager.Instance;
                resultSubText.text = won
                    ? $"用时 {bm?.BattleTimer:F0}s"
                    : "英雄已阵亡...";
                resultSubText.color = won
                    ? new Color(0.8f, 0.9f, 0.8f)
                    : new Color(0.6f, 0.3f, 0.3f);
            }

            // 背景色区分
            if (resultBg != null)
            {
                resultBg.color = won
                    ? new Color(0.05f, 0.2f, 0.1f, 0.92f)   // 深绿
                    : new Color(0.2f, 0.05f, 0.05f, 0.92f);  // 深红
            }

            // 大图标动画
            if (resultIcon != null)
            {
                resultIcon.gameObject.SetActive(true);
                resultIcon.localScale = Vector3.zero;

                if (won)
                {
                    // 胜利：从上方滑入 + 金色光晕脉冲
                    resultIcon.anchoredPosition = new Vector2(0, 300f);
                    resultIcon.DOAnchorPosY(0f, 0.6f).SetEase(Ease.OutBack);
                    resultIcon.DOScale(Vector3.one * 1.2f, 0.5f).SetEase(Ease.OutBack)
                        .OnComplete(() =>
                        {
                            resultIcon.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutQuad);
                            // 持续脉冲
                            resultIcon.DOScale(new Vector3(1.05f, 1.05f, 1f), 0.8f)
                                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                        });
                }
                else
                {
                    // FE-04.4: 失败红闪特效
                    resultIcon.anchoredPosition = new Vector2(0, 0f);
                    resultIcon.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutQuad);
                    // 红闪：背景快速闪烁3次
                    PlayRedFlashEffect(3, 0.15f);
                }
            }

            // FE-04.4: 奖励预览（仅胜利时显示）
            if (rewardPreview != null)
            {
                if (won)
                {
                    rewardPreview.gameObject.SetActive(true);
                    if (rewardPreviewText != null)
                    {
                        var rgm = RoguelikeGameManager.Instance;
                        if (rgm != null)
                        {
                            int level = rgm.CurrentLevel;
                            rewardPreviewText.text = $"通关奖励即将揭晓...\n第{level}关完成！";
                        }
                        else
                        {
                            rewardPreviewText.text = "通关奖励即将揭晓...";
                        }
                    }
                    // 奖励区入场
                    rewardPreview.localScale = Vector3.zero;
                    rewardPreview.DOScale(Vector3.one, 0.5f).SetDelay(0.5f).SetEase(Ease.OutBack);
                }
                else
                {
                    rewardPreview.gameObject.SetActive(false);
                }
            }

            // FE-04.4: 入场动画区分
            if (won)
            {
                // 胜利：从底部滑入
                resultPopup.anchoredPosition = new Vector2(0, -600f);
                resultPopup.localScale = Vector3.one;
                resultPopup.DOAnchorPosY(0f, 0.6f).SetEase(Ease.OutBack);
            }
            else
            {
                // 失败：缩放弹出（带抖动）
                resultPopup.anchoredPosition = Vector2.zero;
                resultPopup.localScale = Vector3.zero;
                resultPopup.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        // 抖动效果
                        resultPopup.DOShakeAnchorPos(0.3f, 10f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic);
                    });
            }

            // 显示确认按钮
            if (resultConfirmButton != null)
            {
                resultConfirmButton.gameObject.SetActive(true);
                resultConfirmButton.interactable = true;
                resultConfirmButton.transform.localScale = Vector3.zero;
                resultConfirmButton.transform.DOScale(Vector3.one, 0.4f)
                    .SetDelay(won ? 0.8f : 0.5f).SetEase(Ease.OutBack);
            }
        }

        /// <summary>
        /// 打字机效果 — 逐字显示文本
        /// </summary>
        private void TypewriterEffect(Text text, string fullText, float charDelay, float startDelay)
        {
            if (text == null || string.IsNullOrEmpty(fullText)) return;

            text.text = "";
            float delay = startDelay;
            for (int i = 1; i <= fullText.Length; i++)
            {
                int len = i;
                DOVirtual.DelayedCall(delay, () =>
                {
                    if (text != null) text.text = fullText.Substring(0, len);
                });
                delay += charDelay;
            }
        }

        /// <summary>
        /// 失败红闪特效 — 背景快速闪烁
        /// </summary>
        private void PlayRedFlashEffect(int flashCount, float flashDuration)
        {
            if (resultBg == null) return;

            Color baseColor = new Color(0.2f, 0.05f, 0.05f, 0.92f);
            Color flashColor = new Color(0.5f, 0.1f, 0.1f, 0.95f);

            Sequence seq = DOTween.Sequence();
            for (int i = 0; i < flashCount; i++)
            {
                seq.Append(resultBg.DOColor(flashColor, flashDuration * 0.5f));
                seq.Append(resultBg.DOColor(baseColor, flashDuration * 0.5f));
            }
        }

        private void OnResultConfirmClicked()
        {
            // 点击确认后 → 状态机跳转
            resultConfirmButton?.interactable = false;

            if (resultPopup != null)
            {
                resultPopup.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (resultPopup != null)
                            resultPopup.gameObject.SetActive(false);
                        // 由状态机自动处理下一阶段跳转
                    });
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

            RefreshDiceSkillButton();
            RefreshRelicBar();
        }

        private void OnBattleEnded(bool won)
        {
            AddLog(won ? "🏆 战斗胜利！" : "💀 战斗失败...");

            if (speedButton != null) speedButton.interactable = false;
            if (skipButton != null) skipButton.interactable = false;
            if (diceSkillButton != null) diceSkillButton.interactable = false;

            ShowResultPopup(won);
        }

        private void OnBattleSpeedChanged(float newSpeed)
        {
            RefreshSpeedDisplay();
            AddLog($"速度切换：{GetSpeedLabel(newSpeed)}");
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

            while (bars.Count < units.Count)
            {
                var bar = CreateUnitBar(container, isPlayer);
                bars.Add(bar);
            }

            for (int i = units.Count; i < bars.Count; i++)
            {
                if (bars[i].rect != null)
                    bars[i].rect.gameObject.SetActive(false);
            }

            for (int i = 0; i < units.Count; i++)
            {
                var hero = units[i];
                var bar = bars[i];
                if (hero == null || bar.rect == null) continue;

                bar.rect.gameObject.SetActive(true);
                bar.hero = hero;

                // 名称 + 职业图标 + 星级
                string classIcon = UIConfigBridge.GetClassIcon(hero.Data.heroClass);
                string starStr = GetStarString(hero.StarLevel);
                if (bar.nameText != null)
                {
                    bar.nameText.text = $"{classIcon}{hero.Data.heroName} {starStr}";
                    bar.nameText.color = isPlayer ? PLAYER_COLOR : ENEMY_COLOR;
                }

                // FE-04.1: 属性面板（仅我方显示ConfigLoader数值）
                if (bar.statsText != null)
                {
                    bar.statsText.gameObject.SetActive(isPlayer);
                    if (isPlayer)
                    {
                        bar.statsText.text = GetHeroStatsText(hero);
                    }
                }

                // 血量
                float healthPercent = hero.MaxHealth > 0 ? (float)hero.CurrentHealth / hero.MaxHealth : 0f;
                bool isDead = hero.IsDead;

                if (bar.healthBarFill != null)
                {
                    bar.healthBarFill.fillAmount = healthPercent;
                    bar.healthBarFill.color = isDead
                        ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                        : GetHealthColor(healthPercent);
                }

                if (bar.healthText != null)
                {
                    bar.healthText.text = isDead
                        ? "💀 阵亡"
                        : $"{hero.CurrentHealth}/{hero.MaxHealth}";
                }

                if (bar.borderImage != null)
                {
                    bar.borderImage.color = isDead
                        ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                        : isPlayer ? PLAYER_COLOR : ENEMY_COLOR;
                }

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

                var nameObj = go.transform.Find("NameText");
                bar.nameText = nameObj?.GetComponent<Text>();

                var fillObj = go.transform.Find("HealthBar/Fill");
                bar.healthBarFill = fillObj?.GetComponent<Image>();

                var hpObj = go.transform.Find("HealthText");
                bar.healthText = hpObj?.GetComponent<Text>();

                var borderObj = go.transform.Find("Border");
                bar.borderImage = borderObj?.GetComponent<Image>();

                // FE-04.1: 查找属性文本（可能在prefab中已存在）
                var statsObj = go.transform.Find("StatsText");
                bar.statsText = statsObj?.GetComponent<Text>();
                // Bug#5 fix: prefab分支也需要初始化StatsText可见性
                if (bar.statsText != null)
                    bar.statsText.gameObject.SetActive(isPlayer);

                bar.canvasGroup = go.GetComponent<CanvasGroup>();
                if (bar.canvasGroup == null)
                    bar.canvasGroup = go.AddComponent<CanvasGroup>();
            }
            else
            {
                // 程序化创建（含属性面板）
                var go = new GameObject(isPlayer ? "PlayerUnit" : "EnemyUnit");
                go.transform.SetParent(container, false);
                bar.rect = go.AddComponent<RectTransform>();
                bar.rect.sizeDelta = new Vector2(300, 55);

                // 背景条
                var bgGo = new GameObject("HealthBar");
                bgGo.transform.SetParent(go.transform, false);
                var bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0.15f, 0.1f);
                bgRect.anchorMax = new Vector2(0.85f, 0.35f);
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
                nameRect.anchorMin = new Vector2(0, 0.6f);
                nameRect.anchorMax = new Vector2(1, 1);
                nameRect.offsetMin = new Vector2(5, 0);
                nameRect.offsetMax = new Vector2(-5, 0);
                bar.nameText = nameGo.AddComponent<Text>();
                bar.nameText.fontSize = 14;
                bar.nameText.alignment = TextAnchor.MiddleLeft;
                bar.nameText.color = isPlayer ? PLAYER_COLOR : ENEMY_COLOR;

                // 血量文本
                var hpGo = new GameObject("HealthText");
                hpGo.transform.SetParent(go.transform, false);
                var hpRect = hpGo.AddComponent<RectTransform>();
                hpRect.anchorMin = new Vector2(0, 0.1f);
                hpRect.anchorMax = new Vector2(1, 0.35f);
                hpRect.offsetMin = new Vector2(5, 0);
                hpRect.offsetMax = new Vector2(-5, 0);
                bar.healthText = hpGo.AddComponent<Text>();
                bar.healthText.fontSize = 12;
                bar.healthText.alignment = TextAnchor.MiddleRight;
                bar.healthText.color = Color.white;

                // FE-04.1: 属性面板文本（我方专属）
                var statsGo = new GameObject("StatsText");
                statsGo.transform.SetParent(go.transform, false);
                var statsRect = statsGo.AddComponent<RectTransform>();
                statsRect.anchorMin = new Vector2(0.4f, 0.6f);
                statsRect.anchorMax = new Vector2(1, 1);
                statsRect.offsetMin = new Vector2(0, 0);
                statsRect.offsetMax = new Vector2(-5, -2);
                bar.statsText = statsGo.AddComponent<Text>();
                bar.statsText.fontSize = 10;
                bar.statsText.alignment = TextAnchor.MiddleRight;
                bar.statsText.color = new Color(0.7f, 0.7f, 0.7f);
                bar.statsText.gameObject.SetActive(isPlayer);

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

            timerText.color = remaining < 10f ? Color.red : Color.white;
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
            while (logLines.Count > maxLogLines)
                logLines.RemoveAt(0);

            if (logText != null)
            {
                logText.text = string.Join("\n", logLines);
            }

            if (logScrollRect != null)
            {
                DOVirtual.DelayedCall(0.05f, () =>
                {
                    if (logScrollRect != null)
                        logScrollRect.verticalNormalizedPosition = 0f;
                });
            }
        }

        // ========== 工具方法 ==========

        private static Color GetHealthColor(float percent)
        {
            if (percent > 0.6f) return HEALTH_HIGH;
            if (percent > 0.3f) return HEALTH_MID;
            return HEALTH_LOW;
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

        // ========== 清理 ==========

        /// <summary>
        /// Bug#4 fix: 切场景时清理DOTween和事件订阅，防止泄漏
        /// </summary>
        protected override void OnDestroy()
        {
            // Bug#4 fix: 清理unitBar上的DOTween动画，防止切场景泄漏
            KillAllUnitBarTweens(playerBars);
            KillAllUnitBarTweens(enemyBars);
            resultPopup?.DOKill();
            vsDivider?.DOKill();
            flashOverlay?.DOKill();
            damageNumbersContainer?.DOKill();

            // 取消后端事件订阅（OnHide可能已执行，但双重退订安全）
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.OnBattleStarted -= OnBattleStarted;
                bm.OnBattleEnded -= OnBattleEnded;
                bm.OnBattleSpeedChanged -= OnBattleSpeedChanged;
                bm.OnDiceSkillTriggered -= OnDiceSkillTriggered;
            }

            base.OnDestroy();
        }

        private void KillAllUnitBarTweens(List<UnitBar> bars)
        {
            if (bars == null) return;
            foreach (var bar in bars)
            {
                if (bar?.rect != null) bar.rect.DOKill();
            }
        }
    }
}
