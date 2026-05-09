using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 随机事件弹窗 — 战斗胜利后30%概率触发 / 肉鸽地图Event节点触发
    /// 
    /// 布局（居中弹窗）：
    /// ┌────────────────────────────┐
    /// │   🎲 随机事件              │
    /// │                            │
    /// │   [事件图标区 - 渐变背景]   │
    /// │   宝箱！你发现了一个宝箱   │
    /// │                            │
    /// │   📜 效果：                │
    /// │   金币 +50                 │
    /// │   攻击 +5                  │
    /// │                            │
    /// │   [选项1] [选项2] [选项3]  │  ← 多选项模式
    /// │   或 [确认]                │  ← 兼容旧单按钮模式
    /// └────────────────────────────┘
    /// </summary>
    public class EventPanel : UIPanel
    {
        [Header("事件显示")]
        public Image eventIcon;
        public Text eventTitleText;
        public Text eventDescText;
        public RectTransform effectsPanel;
        public Text effectsText;

        [Header("图标区背景")]
        public Image iconBackground;

        [Header("按钮")]
        public Button confirmButton;
        public Text confirmButtonText;

        [Header("选项容器（动态生成选项按钮的父节点）")]
        public RectTransform optionsContainer;

        [Header("选项按钮预制体引用（可选，无则纯代码生成）")]
        public GameObject optionButtonPrefab;

        [Header("结果文字区")]
        public Text resultText;

        // 事件类型图标映射
        private static readonly Dictionary<RandomEventType, string> EVENT_ICONS = new()
        {
            { RandomEventType.Treasure, "📦" },
            { RandomEventType.Trap, "⚠️" },
            { RandomEventType.MysteryMerchant, "🧙" },
            { RandomEventType.Altar, "⛪" },
            { RandomEventType.WanderingHealer, "💊" },
            { RandomEventType.Arena, "⚔️" },
        };

        private static readonly Dictionary<RandomEventType, string> EVENT_TITLES = new()
        {
            { RandomEventType.Treasure, "宝箱！" },
            { RandomEventType.Trap, "陷阱！" },
            { RandomEventType.MysteryMerchant, "神秘商人" },
            { RandomEventType.Altar, "祭坛" },
            { RandomEventType.WanderingHealer, "流浪医师" },
            { RandomEventType.Arena, "竞技场" },
        };

        // 事件类型 → 图标区渐变背景色 (color1→color2)
        private static readonly Dictionary<RandomEventType, (Color c1, Color c2)> EVENT_BG_GRADIENTS = new()
        {
            { RandomEventType.Treasure,        (new Color(1f, 0.85f, 0.1f),  new Color(1f, 0.6f, 0.15f)) },   // 金→橙
            { RandomEventType.Trap,            (new Color(0.9f, 0.2f, 0.2f), new Color(0.5f, 0.1f, 0.1f)) },  // 红→暗红
            { RandomEventType.MysteryMerchant, (new Color(0.55f, 0.25f, 0.9f), new Color(0.35f, 0.15f, 0.6f)) }, // 紫→深紫
            { RandomEventType.Altar,           (new Color(0.95f, 0.95f, 1f),  new Color(0.7f, 0.8f, 1f)) },    // 白→蓝白
            { RandomEventType.WanderingHealer, (new Color(0.3f, 0.9f, 0.4f),  new Color(0.4f, 0.85f, 0.55f)) }, // 绿→浅绿
            { RandomEventType.Arena,           (new Color(0.95f, 0.55f, 0.15f), new Color(0.85f, 0.35f, 0.15f)) }, // 橙→红橙
        };

        private RandomEvent currentEvent;
        private List<EventOption> currentOptions;
        private List<GameObject> dynamicOptionButtons = new List<GameObject>();
        private bool isShowingResult = false;
        private Tweener typewriterTween;
        private int typewriterIndex = 0;
        private string typewriterFullText = "";

        protected override void Awake()
        {
            base.Awake();
            panelId = "Event";
        }

        protected override void OnShow()
        {
            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirmClicked);

            // 默认隐藏效果面板和结果区
            if (effectsPanel != null) effectsPanel.gameObject.SetActive(false);
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
                resultText.text = "";
            }

            // 清理旧的动态按钮
            ClearDynamicOptions();

            isShowingResult = false;
        }

        protected override void OnHide()
        {
            confirmButton?.onClick.RemoveAllListeners();
            KillAllTweens();

            // 清理动态按钮
            ClearDynamicOptions();

            // 回到地图选择状态（肉鸽地图集成）
            ReturnToMapSelect();
        }

        /// <summary>
        /// 触发并显示随机事件（兼容旧模式：30%随机触发）
        /// </summary>
        public void TriggerAndShow(int levelId)
        {
            TriggerAndShow(levelId, null);
        }

        /// <summary>
        /// 触发并显示随机事件（支持传入自定义选项）
        /// 当 options 不为空时，显示多选项模式；为 null 时走旧的单按钮确认流程
        /// </summary>
        public void TriggerAndShow(int levelId, List<EventOption> options)
        {
            currentEvent = RandomEventSystem.TriggerEvent(levelId);
            currentOptions = options;

            if (currentEvent == null)
            {
                // 没触发随机事件
                if (options != null && options.Count > 0)
                {
                    // 从地图节点触发，事件为空但有选项 → 创建一个默认事件
                    Debug.Log("[Event] 地图节点事件，但后端返回null，跳过");
                    Hide();
                    return;
                }
                // 30%没触发（旧流程），直接跳过
                Debug.Log("[Event] 未触发随机事件，跳过");
                Hide();
                return;
            }

            ShowEventContent(currentEvent, options);
        }

        /// <summary>
        /// 从肉鸽地图Event节点触发（100%触发，不依赖30%概率）
        /// </summary>
        public void ShowFromMapNode(int levelId, List<EventOption> options = null)
        {
            // 地图节点强制触发，不走30%概率
            currentEvent = RandomEventSystem.TriggerEvent(levelId);

            // 地图节点100%触发，如果后端30%返回null，则直接构造一个事件
            if (currentEvent == null)
            {
                // 强制生成一个随机事件（跳过概率检查）
                int type = UnityEngine.Random.Range(0, 6);
                currentEvent = new RandomEvent { eventType = (RandomEventType)type };
                // 简单描述
                string title = EVENT_TITLES.TryGetValue(currentEvent.eventType, out var t) ? t : "随机事件";
                currentEvent.description = $"你遇到了{title}！";
            }

            currentOptions = options;
            ShowEventContent(currentEvent, options);
        }

        /// <summary>
        /// 显示指定事件内容（兼容旧接口）
        /// </summary>
        public void ShowEventContent(RandomEvent evt)
        {
            ShowEventContent(evt, null);
        }

        /// <summary>
        /// 显示指定事件内容（带选项支持）
        /// </summary>
        public void ShowEventContent(RandomEvent evt, List<EventOption> options)
        {
            currentEvent = evt;
            currentOptions = options;

            // 标题
            string title = EVENT_TITLES.TryGetValue(evt.eventType, out var t) ? t : "随机事件";
            if (eventTitleText != null)
                eventTitleText.text = title;

            // 描述
            if (eventDescText != null)
                eventDescText.text = evt.description;

            // 图标
            if (eventIcon != null)
            {
                eventIcon.color = evt.eventType switch
                {
                    RandomEventType.Treasure => new Color(1f, 0.85f, 0.2f),      // 金色
                    RandomEventType.Trap => new Color(1f, 0.3f, 0.3f),            // 红色
                    RandomEventType.MysteryMerchant => new Color(0.6f, 0.3f, 1f), // 紫色
                    RandomEventType.Altar => new Color(0.9f, 0.9f, 0.9f),         // 白色
                    RandomEventType.WanderingHealer => new Color(0.3f, 0.9f, 0.4f), // 绿色
                    RandomEventType.Arena => new Color(0.9f, 0.5f, 0.2f),         // 橙色
                    _ => Color.gray
                };
            }

            // 图标区背景渐变色
            ApplyIconBackground(evt.eventType);

            // 效果面板（旧模式显示）
            string effects = BuildEffectsString(evt);
            if (effectsText != null)
                effectsText.text = effects;

            if (effectsPanel != null)
                effectsPanel.gameObject.SetActive(!string.IsNullOrEmpty(effects));

            // 判断多选项 vs 旧单按钮
            bool hasOptions = options != null && options.Count > 0;

            if (hasOptions)
            {
                // 多选项模式：隐藏旧确认按钮，动态生成选项
                if (confirmButton != null)
                    confirmButton.gameObject.SetActive(false);

                // 生成选项按钮
                GenerateOptionButtons(options);
            }
            else
            {
                // 旧模式：显示确认按钮
                if (confirmButton != null)
                    confirmButton.gameObject.SetActive(true);

                if (confirmButtonText != null)
                    confirmButtonText.text = evt.eventType == RandomEventType.Trap ? "接受" : "确认";
            }

            // 入场动画
            PlayEnterAnimation();
        }

        #region 图标区背景

        /// <summary>
        /// 根据事件类型设置图标区背景渐变色
        /// </summary>
        private void ApplyIconBackground(RandomEventType eventType)
        {
            if (iconBackground == null) return;

            if (EVENT_BG_GRADIENTS.TryGetValue(eventType, out var colors))
            {
                iconBackground.color = colors.c1;
                // 如果背景支持渐变（Graphic双侧），用双色插值
                // 这里简单设置主色，如需真渐变可在子对象加第二层Image
                iconBackground.color = Color.Lerp(colors.c1, colors.c2, 0.3f);
            }
            else
            {
                iconBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }
        }

        #endregion

        #region 选项按钮动态生成

        /// <summary>
        /// 动态生成选项按钮（最多3个）
        /// </summary>
        private void GenerateOptionButtons(List<EventOption> options)
        {
            ClearDynamicOptions();

            if (optionsContainer == null)
            {
                Debug.LogWarning("[EventPanel] optionsContainer 未赋值，无法生成选项按钮");
                return;
            }

            int count = Mathf.Min(options.Count, 3);

            for (int i = 0; i < count; i++)
            {
                var option = options[i];
                GameObject btnObj = CreateOptionButton(option, i);
                dynamicOptionButtons.Add(btnObj);
            }
        }

        /// <summary>
        /// 创建单个选项按钮
        /// </summary>
        private GameObject CreateOptionButton(EventOption option, int index)
        {
            // 创建按钮对象
            GameObject btnObj;
            if (optionButtonPrefab != null)
            {
                btnObj = Object.Instantiate(optionButtonPrefab, optionsContainer);
            }
            else
            {
                // 纯代码生成
                btnObj = new GameObject($"Option_{index}");
                btnObj.transform.SetParent(optionsContainer, false);
            }

            var btnRect = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(200f, 50f);
            btnRect.localScale = Vector3.one;

            // 添加 Image 背景
            var bgImage = btnObj.GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = btnObj.AddComponent<Image>();
                bgImage.sprite = null; // 使用默认白色方块
            }

            // 风险选项红色背景
            if (option.isRiskOption)
            {
                bgImage.color = new Color(0.85f, 0.2f, 0.2f, 0.9f);
            }
            else
            {
                bgImage.color = new Color(0.2f, 0.45f, 0.8f, 0.9f);
            }

            // 添加 Button 组件
            var button = btnObj.GetComponent<Button>();
            if (button == null)
                button = btnObj.AddComponent<Button>();

            button.targetGraphic = bgImage;

            // 添加文字
            var textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            var txt = textObj.AddComponent<Text>();
            txt.text = option.optionText;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 18;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            var txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            // 保存 EventOption 引用
            var eventData = btnObj.AddComponent<EventOptionData>();
            eventData.option = option;

            // 添加 EventTrigger 处理悬停效果
            AddHoverEffects(btnObj, button, option.isRiskOption);

            // 点击事件
            int capturedIndex = index;
            button.onClick.AddListener(() => OnOptionClicked(option, capturedIndex));

            // 入场动画：从下方淡入滑入
            var cg = btnObj.GetComponent<CanvasGroup>() ?? btnObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            var btnRt = btnObj.GetComponent<RectTransform>();
            float originalY = btnRt.anchoredPosition.y;
            btnRt.anchoredPosition = new Vector2(btnRt.anchoredPosition.x, originalY - 30f);

            cg.DOFade(1f, 0.3f)
                .SetDelay(0.5f + index * 0.1f)
                .SetLink(gameObject);
            btnRt.DOAnchorPosY(originalY, 0.3f)
                .SetDelay(0.5f + index * 0.1f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);

            return btnObj;
        }

        /// <summary>
        /// 为选项按钮添加悬停效果
        /// </summary>
        private void AddHoverEffects(GameObject btnObj, Button button, bool isRisk)
        {
            var trigger = btnObj.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                ?? btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var btnTransform = btnObj.GetComponent<RectTransform>();

            // PointerEnter: scale 1.0→1.03 + 亮度提升
            var entryEnter = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            entryEnter.callback.AddListener((data) =>
            {
                if (isShowingResult) return;
                btnTransform.DOScale(1.03f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);

                // 亮度提升
                var img = btnObj.GetComponent<Image>();
                if (img != null)
                {
                    var targetColor = img.color;
                    targetColor.r = Mathf.Min(1f, targetColor.r + 0.15f);
                    targetColor.g = Mathf.Min(1f, targetColor.g + 0.15f);
                    targetColor.b = Mathf.Min(1f, targetColor.b + 0.15f);
                    img.DOColor(targetColor, 0.12f).SetLink(gameObject);
                }

                // 风险选项：悬停时抖动
                if (isRisk)
                {
                    btnTransform.DOShakeAnchorPos(0.4f, 4f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic)
                        .SetLink(gameObject);
                }
            });
            trigger.triggers.Add(entryEnter);

            // PointerExit: scale 1.03→1.0 + 亮度恢复
            var entryExit = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            entryExit.callback.AddListener((data) =>
            {
                if (isShowingResult) return;
                btnTransform.DOScale(1f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);

                var img = btnObj.GetComponent<Image>();
                if (img != null)
                {
                    var originalColor = isRisk
                        ? new Color(0.85f, 0.2f, 0.2f, 0.9f)
                        : new Color(0.2f, 0.45f, 0.8f, 0.9f);
                    img.DOColor(originalColor, 0.12f).SetLink(gameObject);
                }
            });
            trigger.triggers.Add(entryExit);
        }

        /// <summary>
        /// 清理所有动态生成的选项按钮
        /// </summary>
        private void ClearDynamicOptions()
        {
            foreach (var btn in dynamicOptionButtons)
            {
                if (btn != null)
                    Object.Destroy(btn);
            }
            dynamicOptionButtons.Clear();
        }

        #endregion

        #region 选项点击处理

        /// <summary>
        /// 选项按钮点击回调
        /// </summary>
        private void OnOptionClicked(EventOption option, int index)
        {
            if (isShowingResult || currentEvent == null) return;
            isShowingResult = true;

            // 按钮脉冲动画：缩小→放大
            if (index < dynamicOptionButtons.Count && dynamicOptionButtons[index] != null)
            {
                var btnTransform = dynamicOptionButtons[index].GetComponent<RectTransform>();
                if (btnTransform != null)
                {
                    btnTransform.DOKill();
                    btnTransform.DOScale(0.9f, 0.08f).SetEase(Ease.InQuad).SetLink(gameObject)
                        .OnComplete(() =>
                        {
                            btnTransform.DOScale(1.08f, 0.15f).SetEase(Ease.OutBack).SetLink(gameObject)
                                .OnComplete(() =>
                                {
                                    btnTransform.DOScale(1f, 0.1f).SetLink(gameObject);
                                });
                        });
                }
            }

            // 禁用所有按钮（防止重复点击）
            foreach (var btn in dynamicOptionButtons)
            {
                if (btn != null)
                {
                    var button = btn.GetComponent<Button>();
                    if (button != null) button.interactable = false;
                }
            }

            // 隐藏旧确认按钮
            if (confirmButton != null) confirmButton.interactable = false;

            // 应用事件效果（后端处理）
            var inventory = PlayerInventory.Instance;
            var deck = CardDeck.Instance;
            var heroes = deck?.fieldHeroes;
            if (inventory != null)
            {
                RandomEventSystem.ApplyEvent(currentEvent, inventory, heroes);
                Debug.Log($"[Event] 应用事件效果：{currentEvent.description}");
            }

            // 显示结果（打字机效果 + 效果反馈）
            ShowResult(option);
        }

        /// <summary>
        /// 旧模式确认按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            if (currentEvent == null)
            {
                Hide();
                return;
            }

            if (isShowingResult) return;
            isShowingResult = true;

            // 应用事件效果
            var inventory = PlayerInventory.Instance;
            var deck = CardDeck.Instance;
            var heroes = deck?.fieldHeroes;

            if (inventory != null)
            {
                RandomEventSystem.ApplyEvent(currentEvent, inventory, heroes);
                Debug.Log($"[Event] 应用事件效果：{currentEvent.description}");
            }

            // 确认按钮脉冲
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                var btnRect = confirmButton.GetComponent<RectTransform>();
                if (btnRect != null)
                {
                    btnRect.DOScale(0.9f, 0.08f).SetEase(Ease.InQuad).SetLink(gameObject)
                        .OnComplete(() =>
                        {
                            btnRect.DOScale(1.08f, 0.15f).SetEase(Ease.OutBack).SetLink(gameObject)
                                .OnComplete(() =>
                                {
                                    btnRect.DOScale(1f, 0.1f).SetLink(gameObject);
                                });
                        });
                }
            }

            // 旧模式：显示效果反馈后关闭
            ShowLegacyResult();
        }

        #endregion

        #region 结果显示

        /// <summary>
        /// 显示选项结果（打字机效果 + 效果反馈动画）
        /// </summary>
        private void ShowResult(EventOption option)
        {
            string resultContent = option.effectDescription ?? "";
            if (string.IsNullOrEmpty(resultContent))
                resultContent = BuildEffectsString(currentEvent);

            // 显示结果文字区
            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = "";
                StartTypewriter(resultText, resultContent, () =>
                {
                    // 打字完成后的效果反馈
                    PlayEffectFeedback(option.effectDescription);

                    // 打字完成后0.5s自动关闭
                    DOVirtual.DelayedCall(0.5f, () => CloseWithAnimation()).SetLink(gameObject);
                });
            }
            else
            {
                // 没有结果文字区，直接播反馈并关闭
                PlayEffectFeedback(option.effectDescription);
                DOVirtual.DelayedCall(0.8f, () => CloseWithAnimation()).SetLink(gameObject);
            }
        }

        /// <summary>
        /// 旧模式结果显示
        /// </summary>
        private void ShowLegacyResult()
        {
            string effects = BuildEffectsString(currentEvent);

            if (resultText != null && !string.IsNullOrEmpty(effects))
            {
                resultText.gameObject.SetActive(true);
                resultText.text = "";
                StartTypewriter(resultText, effects, () =>
                {
                    PlayEffectFeedback(effects);
                    DOVirtual.DelayedCall(0.5f, () => CloseWithAnimation()).SetLink(gameObject);
                });
            }
            else
            {
                // 没有效果文字，直接关闭
                CloseWithAnimation();
            }
        }

        /// <summary>
        /// 打字机效果：逐字显示文字
        /// </summary>
        private void StartTypewriter(Text target, string fullText, System.Action onComplete)
        {
            if (target == null || string.IsNullOrEmpty(fullText))
            {
                onComplete?.Invoke();
                return;
            }

            KillTypewriter();
            typewriterFullText = fullText;
            typewriterIndex = 0;
            target.text = "";

            typewriterTween = DOVirtual.DelayedCall(0.03f, () =>
            {
                if (target == null)
                {
                    KillTypewriter();
                    return;
                }

                typewriterIndex++;
                if (typewriterIndex >= fullText.Length)
                {
                    target.text = fullText;
                    KillTypewriter();
                    onComplete?.Invoke();
                }
                else
                {
                    target.text = fullText.Substring(0, typewriterIndex);
                }
            }).SetLoops(fullText.Length + 1).SetLink(gameObject);
        }

        private void KillTypewriter()
        {
            if (typewriterTween != null && typewriterTween.IsActive())
            {
                typewriterTween.Kill();
                typewriterTween = null;
            }
        }

        /// <summary>
        /// 效果反馈动画（飘字 + 屏幕震动）
        /// </summary>
        private void PlayEffectFeedback(string effectDesc)
        {
            if (string.IsNullOrEmpty(effectDesc)) return;

            // 判断正面/负面效果
            bool isPositive = IsPositiveEffect(effectDesc);

            // 飘字效果
            SpawnFloatingText(effectDesc, isPositive);

            // 负面效果：轻微屏幕震动
            if (!isPositive && rectTransform != null)
            {
                rectTransform.DOShakeAnchorPos(0.3f, 8f, 15, 90f, false, true, ShakeRandomnessMode.Harmonic)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 判断效果是否为正面
        /// </summary>
        private bool IsPositiveEffect(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            // 检查是否包含负面关键词
            if (text.Contains("损失") || text.Contains("减少") || text.Contains("生命 -"))
                return false;
            // 检查是否包含正面关键词
            if (text.Contains("+") || text.Contains("获得") || text.Contains("恢复") || text.Contains("增加"))
                return true;
            return true; // 默认为正面
        }

        /// <summary>
        /// 生成飘字效果
        /// </summary>
        private void SpawnFloatingText(string text, bool isPositive)
        {
            // 在面板上创建飘字对象
            var floatObj = new GameObject("FloatingText");
            floatObj.transform.SetParent(rectTransform, false);

            var floatRect = floatObj.AddComponent<RectTransform>();
            floatRect.anchoredPosition = new Vector2(0f, -50f);
            floatRect.sizeDelta = new Vector2(300f, 40f);

            var floatText = floatObj.AddComponent<Text>();
            floatText.text = text;
            floatText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            floatText.fontSize = 20;
            floatText.alignment = TextAnchor.MiddleCenter;
            floatText.color = isPositive ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.3f, 0.2f);
            floatText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 向上飘出动画
            floatRect.DOAnchorPosY(80f, 1.2f).SetEase(Ease.OutQuad).SetLink(gameObject);
            var cg = floatObj.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 1.2f).SetEase(Ease.InQuad).SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (floatObj != null) Object.Destroy(floatObj);
                });
        }

        #endregion

        #region 动画

        /// <summary>
        /// 入场动画：从屏幕底部滑入 + scale缩放 + 图标区延迟淡入
        /// </summary>
        private void PlayEnterAnimation()
        {
            if (rectTransform != null)
            {
                // 初始状态：底部位置 + 缩小
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -500f);
                rectTransform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                // 滑入到中心
                rectTransform.DOAnchorPosY(0f, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);

                // 缩放 0.5→1.0
                rectTransform.DOScale(Vector3.one, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);
            }

            // 图标区单独延迟0.2s淡入
            if (eventIcon != null)
            {
                var iconCg = eventIcon.GetComponent<CanvasGroup>()
                    ?? eventIcon.gameObject.AddComponent<CanvasGroup>();
                iconCg.alpha = 0f;
                iconCg.DOFade(1f, 0.3f)
                    .SetDelay(0.2f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            }

            // 图标区背景也延迟淡入
            if (iconBackground != null)
            {
                var bgCg = iconBackground.GetComponent<CanvasGroup>()
                    ?? iconBackground.gameObject.AddComponent<CanvasGroup>();
                bgCg.alpha = 0f;
                bgCg.DOFade(1f, 0.3f)
                    .SetDelay(0.2f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 关闭动画（缩小→隐藏）
        /// </summary>
        private void CloseWithAnimation()
        {
            if (rectTransform != null)
            {
                rectTransform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
                    .SetLink(gameObject)
                    .OnComplete(() => Hide());
            }
            else
            {
                Hide();
            }
        }

        #endregion

        #region 工具方法

        private string BuildEffectsString(RandomEvent evt)
        {
            var parts = new List<string>();

            if (evt.goldReward != 0)
                parts.Add($"金币 {(evt.goldReward > 0 ? "+" : "")}{evt.goldReward}");

            if (evt.healthLoss != 0)
                parts.Add($"生命 {(evt.healthLoss > 0 ? "-" : "+")}{Mathf.Abs(evt.healthLoss)}");

            if (evt.buffAttack != 0)
                parts.Add($"攻击 {(evt.buffAttack > 0 ? "+" : "")}{evt.buffAttack}");

            if (evt.healAmount != 0)
                parts.Add($"治疗 +{evt.healAmount}");

            if (evt.discountRate > 0)
                parts.Add($"商店折扣 {(1f - evt.discountRate) * 100:F0}%");

            return parts.Count > 0 ? string.Join("\n", parts) : "";
        }

        private void KillAllTweens()
        {
            KillTypewriter();
            // DOTween.Kill(gameObject) 已在 UIPanel.Hide() 中调用
        }

        /// <summary>
        /// 关闭后回到地图选择状态（肉鸽地图集成）
        /// </summary>
        private void ReturnToMapSelect()
        {
            var gsm = GameStateMachine.Instance;
            if (gsm != null && gsm.CurrentState != GameState.MapSelect)
            {
                gsm.ChangeState(GameState.MapSelect);
            }
        }

        #endregion

        #region 辅助组件

        /// <summary>
        /// 内部类：挂载在动态生成的选项按钮上，存储选项数据
        /// </summary>
        private class EventOptionData : MonoBehaviour
        {
            public EventOption option;
        }

        #endregion
    }
}

/// <summary>
/// 随机事件选项（前端定义，后端补上后删除此类）
/// </summary>
[System.Serializable]
public class EventOption
{
    public string optionText;        // "打开宝箱" / "小心绕过"
    public string effectDescription; // "获得50金币" / "安全通过，无事发生"
    public bool isRiskOption;        // 是否风险选项（红色高亮）
}
