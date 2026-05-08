using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

namespace Game.UI
{
    /// <summary>
    /// 骰子掷骰面板 — 核心赌狗体验
    /// 
    /// UI布局（竖屏720x1280）：
    /// - 顶部：关卡信息 + 英雄头像
    /// - 中部：3个骰子（大号显示，支持点击锁定）
    /// - 骰子下方：组合结果显示（如"三条"、"顺子"）
    /// - 底部：掷骰按钮 / 重摇按钮 / 确认按钮
    /// 
    /// 交互流程：
    /// 1. 进入面板 → 自动掷骰（骰子旋转动画）
    /// 2. 显示结果 → 自动评估组合 → 显示组合名+效果
    /// 3. 玩家可以点击骰子锁定/解锁（锁定的不参与重摇）
    /// 4. 点重摇按钮（仅1次免费）→ 锁定的保留，其他重摇
    /// 5. 点确认 → 进入 Battle 状态
    /// </summary>
    public class DiceRollPanel : UIPanel
    {
        [Header("骰子显示 - 3个骰子Image")]
        public Image dice1;
        public Image dice2;
        public Image dice3;

        [Header("骰子容器（用于旋转动画）")]
        public RectTransform dice1Container;
        public RectTransform dice2Container;
        public RectTransform dice3Container;

        [Header("骰子锁定遮罩")]
        public GameObject dice1Lock;
        public GameObject dice2Lock;
        public GameObject dice3Lock;

        [Header("骰子点数Text（调试用，也可用于UI显示）")]
        public Text dice1ValueText;
        public Text dice2ValueText;
        public Text dice3ValueText;

        [Header("操作按钮")]
        public Button rollButton;
        public Button rerollButton;
        public Button confirmButton;

        [Header("按钮Text")]
        public Text rollButtonText;
        public Text rerollButtonText;
        public Text confirmButtonText;

        [Header("组合结果显示")]
        public Text combinationText;
        public Text combinationEffectText;

        [Header("关卡信息")]
        public Text levelText;

        [Header("提示Text")]
        public Text tipText;

        // 内部状态
        private DiceRoller diceRoller;
        private bool[] lockedDice = new bool[3];
        private int[] currentValues = new int[3];
        private bool isRolling = false;
        private bool hasRerolled = false;

        // 骰子精灵（1-6面的Sprite，在Inspector中绑定）
        // 如果没有精灵图，用数字Text代替
        [Header("骰子面精灵（可选，没有则用数字）")]
        public Sprite[] diceFaceSprites; // 长度6，索引0=面值1

        private Image[] diceImages;
        private RectTransform[] diceContainers;
        private GameObject[] diceLocks;
        private Text[] diceValueTexts;
        private Button[] diceClickAreas; // 骰子本身的Button用于点击锁定

        protected override void Awake()
        {
            base.Awake();
            panelId = "DiceRoll";

            diceImages = new Image[] { dice1, dice2, dice3 };
            diceContainers = new RectTransform[] { dice1Container, dice2Container, dice3Container };
            diceLocks = new GameObject[] { dice1Lock, dice2Lock, dice3Lock };
            diceValueTexts = new Text[] { dice1ValueText, dice2ValueText, dice3ValueText };
        }

        protected override void OnShow()
        {
            // 初始化骰子投掷器
            diceRoller = new DiceRoller(3, 6);

            // 订阅骰子事件
            diceRoller.OnDiceRolled += OnDiceRolled;
            diceRoller.OnRerollUsed += OnRerollUsed;
            diceRoller.OnRerollsExhausted += OnRerollsExhausted;

            // 绑定按钮
            if (rollButton != null) rollButton.onClick.AddListener(OnRollClicked);
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);

            // 初始状态
            hasRerolled = false;
            lockedDice = new bool[3];
            currentValues = new int[3];

            // 重置UI
            ResetDiceVisuals();
            UpdateButtonStates();
            HideCombinationResult();

            // 显示关卡信息
            if (levelText != null)
            {
                var sm = Game.Core.GameStateMachine.Instance;
                levelText.text = sm != null ? $"第 {sm.CurrentLevel} 关" : "骰子阶段";
            }

            // 骰子入场动画后自动掷骰
            PlayDiceEnterAnimation(() =>
            {
                // 入场完成，自动掷第一次骰
                DOVirtual.DelayedCall(0.2f, () => PerformRoll());
            });
        }

        protected override void OnHide()
        {
            // 取消骰子事件
            if (diceRoller != null)
            {
                diceRoller.OnDiceRolled -= OnDiceRolled;
                diceRoller.OnRerollUsed -= OnRerollUsed;
                diceRoller.OnRerollsExhausted -= OnRerollsExhausted;
            }

            // 清理按钮
            if (rollButton != null) rollButton.onClick.RemoveAllListeners();
            if (rerollButton != null) rerollButton.onClick.RemoveAllListeners();
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();

            // 清理DOTween
            foreach (var container in diceContainers)
            {
                if (container != null) container.DOKill();
            }
        }

        // ========== 骰子交互 ==========

        /// <summary>
        /// 点击骰子 → 锁定/解锁
        /// </summary>
        public void OnDiceClicked(int diceIndex)
        {
            if (isRolling) return;
            if (diceIndex < 0 || diceIndex >= 3) return;

            // 还没掷过骰不允许锁定
            if (currentValues[0] == 0 && currentValues[1] == 0 && currentValues[2] == 0) return;

            lockedDice[diceIndex] = !lockedDice[diceIndex];
            UpdateDiceLockVisual(diceIndex);
        }

        /// <summary>
        /// 掷骰按钮
        /// </summary>
        private void OnRollClicked()
        {
            if (isRolling) return;
            PerformRoll();
        }

        /// <summary>
        /// 重摇按钮
        /// </summary>
        private void OnRerollClicked()
        {
            if (isRolling) return;
            if (!diceRoller.CanReroll) return;

            hasRerolled = true;
            PerformReroll();
        }

        /// <summary>
        /// 确认按钮 → 进入战斗
        /// </summary>
        private void OnConfirmClicked()
        {
            if (isRolling) return;

            // 确认动画
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                var rt = confirmButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.DOKill();
                    rt.DOScale(Vector3.one * 1.1f, 0.15f).SetLoops(2, LoopType.Yoyo);
                }
            }

            // 保存当前骰子组合到GameManager
            var combo = diceRoller.GetCurrentCombination();
            Debug.Log($"[DiceRoll] 确认骰子: {string.Join(",", currentValues)} 组合: {combo.Description}");

            // 存储结果供战斗阶段使用
            PlayerPrefs.SetString("LastDiceValues", $"{currentValues[0]},{currentValues[1]},{currentValues[2]}");
            PlayerPrefs.SetInt("LastDiceCombo", (int)combo.Type);
            PlayerPrefs.Save();

            // 延迟切换到战斗
            DOVirtual.DelayedCall(0.4f, () =>
            {
                Game.Core.GameStateMachine.Instance.ChangeState(Game.Core.GameState.Battle);
            });
        }

        // ========== 核心骰子逻辑 ==========

        /// <summary>
        /// 执行掷骰（全部重投）
        /// </summary>
        private void PerformRoll()
        {
            isRolling = true;
            DisableAllButtons();
            HideCombinationResult();

            // 解锁所有骰子
            lockedDice = new bool[3];
            for (int i = 0; i < 3; i++) UpdateDiceLockVisual(i);

            // 隐藏骰子值
            foreach (var txt in diceValueTexts)
            {
                if (txt != null) txt.text = "?";
            }

            // 播放旋转动画
            PlayDiceRollAnimation(() =>
            {
                // 动画结束，获取结果
                currentValues = diceRoller.RollAll();
                UpdateDiceDisplay();
                ShowCombinationResult();

                isRolling = false;
                UpdateButtonStates();
            });
        }

        /// <summary>
        /// 执行重摇（保留锁定的骰子）
        /// </summary>
        private void PerformReroll()
        {
            isRolling = true;
            DisableAllButtons();
            HideCombinationResult();

            // 对未锁定的骰子播放旋转动画
            PlayDiceRerollAnimation(lockedDice, () =>
            {
                currentValues = diceRoller.Reroll(lockedDice);
                UpdateDiceDisplay();
                ShowCombinationResult();

                isRolling = false;
                UpdateButtonStates();
            });
        }

        // ========== 骰子事件回调 ==========

        private void OnDiceRolled(int[] values)
        {
            // 掷骰完成回调（结果已在PerformRoll中处理）
        }

        private void OnRerollUsed(int usedCount)
        {
            if (tipText != null)
                tipText.text = $"已使用重摇 {usedCount} 次";
        }

        private void OnRerollsExhausted()
        {
            if (tipText != null)
                tipText.text = "重摇次数已用完";
        }

        // ========== UI更新 ==========

        /// <summary>
        /// 更新骰子面值显示
        /// </summary>
        private void UpdateDiceDisplay()
        {
            for (int i = 0; i < 3; i++)
            {
                // 更新点数Text
                if (diceValueTexts[i] != null)
                    diceValueTexts[i].text = currentValues[i].ToString();

                // 更新骰子精灵（如果有）
                if (diceImages[i] != null && diceFaceSprites != null && diceFaceSprites.Length >= 6)
                {
                    int spriteIndex = Mathf.Clamp(currentValues[i] - 1, 0, 5);
                    diceImages[i].sprite = diceFaceSprites[spriteIndex];
                }
            }
        }

        /// <summary>
        /// 更新单个骰子的锁定视觉
        /// </summary>
        private void UpdateDiceLockVisual(int index)
        {
            if (diceLocks[index] == null) return;

            if (lockedDice[index])
            {
                diceLocks[index].SetActive(true);
                // 锁定：骰子缩小+灰化
                if (diceContainers[index] != null)
                {
                    diceContainers[index].DOKill();
                    diceContainers[index].DOScale(Vector3.one * 0.85f, 0.2f).SetEase(Ease.OutQuad);
                }
                var cg = diceContainers[index]?.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0.6f;
            }
            else
            {
                diceLocks[index].SetActive(false);
                // 解锁：恢复
                if (diceContainers[index] != null)
                {
                    diceContainers[index].DOKill();
                    diceContainers[index].DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
                }
                var cg = diceContainers[index]?.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        /// <summary>
        /// 显示组合结果
        /// </summary>
        private void ShowCombinationResult()
        {
            var combo = diceRoller.GetCurrentCombination();

            if (combinationText != null)
            {
                combinationText.text = combo.Description;
                combinationText.gameObject.SetActive(true);

                // 组合文字动画：缩放弹跳
                combinationText.rectTransform.DOKill();
                combinationText.rectTransform.localScale = Vector3.one * 0.5f;
                combinationText.rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);

                // 根据组合稀有度改变颜色
                switch (combo.Type)
                {
                    case DiceCombinationType.ThreeOfAKind:
                        combinationText.color = new Color(1f, 0.2f, 0.2f); // 红色 - 稀有
                        break;
                    case DiceCombinationType.Straight:
                        combinationText.color = new Color(0.2f, 0.6f, 1f); // 蓝色 - 稀有
                        break;
                    case DiceCombinationType.Pair:
                        combinationText.color = new Color(0.3f, 1f, 0.3f); // 绿色 - 普通
                        break;
                    default:
                        combinationText.color = Color.white;
                        break;
                }
            }

            if (combinationEffectText != null)
            {
                combinationEffectText.text = combo.EffectDescription;
                combinationEffectText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏组合结果
        /// </summary>
        private void HideCombinationResult()
        {
            if (combinationText != null) combinationText.gameObject.SetActive(false);
            if (combinationEffectText != null) combinationEffectText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasValues = currentValues[0] > 0;

            // 掷骰按钮：始终可用（重新掷）
            if (rollButton != null) rollButton.interactable = true;
            if (rollButtonText != null) rollButtonText.text = "掷骰";

            // 重摇按钮：还有次数 & 已经掷过
            bool canReroll = diceRoller != null && diceRoller.CanReroll && hasValues;
            if (rerollButton != null) rerollButton.interactable = canReroll;
            if (rerollButtonText != null)
            {
                if (canReroll)
                    rerollButtonText.text = $"重摇 (剩余{diceRoller.RemainingRerolls}次)";
                else if (hasRerolled)
                    rerollButtonText.text = "已用完";
                else
                    rerollButtonText.text = "重摇 (1次)";
            }

            // 确认按钮：已掷骰才可确认
            if (confirmButton != null) confirmButton.interactable = hasValues;
            if (confirmButtonText != null)
                confirmButtonText.text = hasValues ? "确认 → 战斗" : "请先掷骰";
        }

        /// <summary>
        /// 禁用所有按钮
        /// </summary>
        private void DisableAllButtons()
        {
            if (rollButton != null) rollButton.interactable = false;
            if (rerollButton != null) rerollButton.interactable = false;
            if (confirmButton != null) confirmButton.interactable = false;
        }

        /// <summary>
        /// 重置骰子视觉
        /// </summary>
        private void ResetDiceVisuals()
        {
            for (int i = 0; i < 3; i++)
            {
                if (diceValueTexts[i] != null) diceValueTexts[i].text = "?";
                if (diceLocks[i] != null) diceLocks[i].SetActive(false);
                if (diceContainers[i] != null)
                {
                    diceContainers[i].localScale = Vector3.one;
                    var cg = diceContainers[i].GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
            }
        }

        // ========== 动画 ==========

        /// <summary>
        /// 骰子入场动画 — 从上方掉落
        /// </summary>
        private void PlayDiceEnterAnimation(System.Action onComplete)
        {
            int completed = 0;
            int total = 0;

            for (int i = 0; i < 3; i++)
            {
                if (diceContainers[i] == null) continue;
                total++;

                var container = diceContainers[i];
                container.DOKill();

                // 初始在上方
                float targetY = container.anchoredPosition.y;
                container.anchoredPosition = new Vector2(container.anchoredPosition.x, targetY + 300f);
                container.localScale = Vector3.one * 0.5f;

                float delay = i * 0.12f;
                container.DOAnchorPosY(targetY, 0.5f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBounce)
                    .OnComplete(() =>
                    {
                        completed++;
                        if (completed >= total && onComplete != null)
                            onComplete();
                    });

                container.DOScale(Vector3.one, 0.4f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack);
            }

            if (total == 0 && onComplete != null) onComplete();
        }

        /// <summary>
        /// 掷骰旋转动画 — 3个骰子同时旋转
        /// </summary>
        private void PlayDiceRollAnimation(System.Action onComplete)
        {
            int completed = 0;

            for (int i = 0; i < 3; i++)
            {
                if (diceContainers[i] == null)
                {
                    completed++;
                    continue;
                }

                var container = diceContainers[i];
                container.DOKill();

                // 快速旋转动画
                var seq = DOTween.Sequence();
                seq.Append(container.DORotate(new Vector3(0, 0, 360f), 0.15f, RotateMode.FastBeyond360))
                   .SetLoops(3, LoopType.Restart);
                seq.OnComplete(() =>
                {
                    container.localRotation = Quaternion.identity;
                    // 结果弹出效果
                    container.DOScale(Vector3.one * 1.15f, 0.1f).SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                        {
                            container.DOScale(Vector3.one, 0.1f).SetEase(Ease.InQuad)
                                .OnComplete(() =>
                                {
                                    completed++;
                                    if (completed >= 3 && onComplete != null)
                                        onComplete();
                                });
                        });
                });
            }
        }

        /// <summary>
        /// 重摇旋转动画 — 只旋转未锁定的骰子
        /// </summary>
        private void PlayDiceRerollAnimation(bool[] lockMask, System.Action onComplete)
        {
            int toRoll = 0;
            int completed = 0;

            // 计算需要重摇的骰子数量
            for (int i = 0; i < 3; i++)
            {
                if (!lockMask[i]) toRoll++;
            }

            if (toRoll == 0)
            {
                if (onComplete != null) onComplete();
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                if (lockMask[i])
                {
                    // 锁定的骰子不参与动画，但给一个小抖动表示"保留"
                    if (diceContainers[i] != null)
                    {
                        diceContainers[i].DOKill();
                        diceContainers[i].DOShakeScale(0.3f, 0.1f, 10, 90f);
                    }
                    continue;
                }

                if (diceContainers[i] == null)
                {
                    completed++;
                    continue;
                }

                var container = diceContainers[i];
                container.DOKill();

                // 旋转动画（比首次短）
                var seq = DOTween.Sequence();
                seq.Append(container.DORotate(new Vector3(0, 0, 360f), 0.12f, RotateMode.FastBeyond360))
                   .SetLoops(2, LoopType.Restart);
                seq.OnComplete(() =>
                {
                    container.localRotation = Quaternion.identity;
                    container.DOScale(Vector3.one * 1.1f, 0.08f).SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                        {
                            container.DOScale(Vector3.one, 0.08f).SetEase(Ease.InQuad)
                                .OnComplete(() =>
                                {
                                    completed++;
                                    if (completed >= toRoll && onComplete != null)
                                        onComplete();
                                });
                        });
                });
            }
        }
    }
}
