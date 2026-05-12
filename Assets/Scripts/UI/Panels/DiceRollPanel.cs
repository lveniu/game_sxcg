using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using System.Collections;

namespace Game.UI
{
    /// <summary>
    /// 骰子掷骰面板
    /// 对接 DiceRoller: RollAll() / Reroll(keepMask) / CanReroll / RemainingRerolls
    /// 对接 DiceCombinationEvaluator: Evaluate(values) 返回 DiceCombination
    /// </summary>
    public class DiceRollPanel : UIPanel
    {
        [Header("骰子显示")]
        public Text diceText1;  // 显示1-6数字
        public Text diceText2;
        public Text diceText3;
        public Image diceBg1;   // 骰子背景（锁定变灰）
        public Image diceBg2;
        public Image diceBg3;

        [Header("锁定Toggle")]
        public Toggle keepToggle1;
        public Toggle keepToggle2;
        public Toggle keepToggle3;

        [Header("操作按钮")]
        public Button rollButton;       // 掷骰
        public Button rerollButton;     // 重摇
        public Button confirmButton;    // 确认进入战斗
        public Text rerollCountText;    // 剩余重摇次数

        [Header("组合结果")]
        public Text combinationText;        // 如 "三条(5)" "顺子(1,2,3)"
        public Text effectDescText;         // 组合效果描述
        public GameObject comboArea;        // 组合结果区域

        [Header("动画配置")]
        public float rollAnimDuration = 0.6f;
        public float rollAnimInterval = 0.05f;

        private Text[] diceTexts;
        private Image[] diceBgs;
        private Toggle[] keepToggles;

        private DiceRoller diceRoller;
        private DiceCombinationEvaluator evaluator;
        private int[] currentValues;
        private DiceCombination currentCombo;
        private bool isAnimating;
        private bool hasRolled;

        protected override void Awake()
        {
            base.Awake();
            diceTexts = new Text[] { diceText1, diceText2, diceText3 };
            diceBgs = new Image[] { diceBg1, diceBg2, diceBg3 };
            keepToggles = new Toggle[] { keepToggle1, keepToggle2, keepToggle3 };
        }

        protected override void OnShow()
        {
            // 使用RoguelikeGameManager的DiceRoller（含遗物加成的重摇次数）
            // 如果RoguelikeGameManager未初始化则创建临时的
            var rgm = RoguelikeGameManager.Instance;
            if (rgm != null && rgm.DiceRoller != null)
            {
                diceRoller = rgm.DiceRoller;
                // 每关重置骰子：恢复重摇次数（基础1 + 遗物额外）
                diceRoller.SetFreeRerolls(1 + (rgm.RelicSystem?.GetExtraRerolls() ?? 0));
            }
            else
            {
                diceRoller = new DiceRoller(diceCount: 3, sides: 6);
            }
            
            evaluator = new DiceCombinationEvaluator();
            currentValues = new int[] { 0, 0, 0 };
            currentCombo = null;
            hasRolled = false;
            isAnimating = false;

            // 重置UI
            foreach (var t in diceTexts) if (t) t.text = "?";
            foreach (var bg in diceBgs) if (bg) bg.color = Color.white;
            foreach (var tog in keepToggles) if (tog) tog.isOn = false;
            if (comboArea) comboArea.SetActive(false);
            if (rerollCountText) rerollCountText.text = $"重摇: {diceRoller.RemainingRerolls}";
            if (rerollButton) rerollButton.interactable = false;
            if (confirmButton) confirmButton.interactable = false;
            if (rollButton) rollButton.interactable = true;

            // 绑定事件
            rollButton?.onClick.RemoveAllListeners();
            rollButton?.onClick.AddListener(OnRollClicked);
            rerollButton?.onClick.RemoveAllListeners();
            rerollButton?.onClick.AddListener(OnRerollClicked);
            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirmClicked);

            // 骰子锁定Toggle音效
            foreach (var tog in keepToggles)
            {
                if (tog != null)
                {
                    tog.onValueChanged.RemoveAllListeners();
                    tog.onValueChanged.AddListener(OnKeepToggleChanged);
                }
            }

            // 订阅骰子事件
            diceRoller.OnDiceRolled += OnDiceRolled;
            diceRoller.OnRerollsExhausted += OnRerollsExhausted;
        }

        protected override void OnHide()
        {
            rollButton?.onClick.RemoveAllListeners();
            rerollButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();

            // Bug#1 fix: 始终取消订阅，避免重进时叠加监听器
            // 共享DiceRoller的OnDiceRolled/OnRerollsExhausted是multicast delegate，
            // 不取消会导致每次进入DiceRoll面板都叠加回调
            if (diceRoller != null)
            {
                diceRoller.OnDiceRolled -= OnDiceRolled;
                diceRoller.OnRerollsExhausted -= OnRerollsExhausted;
            }
            diceRoller = null;
        }

        #region 掷骰

        private void OnRollClicked()
        {
            if (isAnimating) return;
            // 骰子旋转音效
            PlayDiceAudio("spin");
            StartCoroutine(RollCoroutine(isReroll: false));
        }

        private void OnRerollClicked()
        {
            if (isAnimating || !diceRoller.CanReroll) return;

            bool[] keepMask = new bool[3];
            for (int i = 0; i < 3; i++)
                keepMask[i] = keepToggles[i] != null && keepToggles[i].isOn;

            // 骰子旋转音效
            PlayDiceAudio("spin");
            StartCoroutine(RollCoroutine(isReroll: true, keepMask));
        }

        private IEnumerator RollCoroutine(bool isReroll, bool[] keepMask = null)
        {
            isAnimating = true;
            if (rollButton) rollButton.interactable = false;
            if (rerollButton) rerollButton.interactable = false;
            if (confirmButton) confirmButton.interactable = false;

            // 掷骰
            int[] results;
            if (isReroll)
                results = diceRoller.Reroll(keepMask);
            else
                results = diceRoller.RollAll();

            // 数字滚动动画
            float elapsed = 0f;
            while (elapsed < rollAnimDuration)
            {
                for (int i = 0; i < 3; i++)
                {
                    // 被锁定的骰子不动画
                    if (isReroll && keepMask != null && keepMask[i])
                    {
                        if (diceTexts[i]) diceTexts[i].text = currentValues[i].ToString();
                        continue;
                    }
                    if (diceTexts[i]) diceTexts[i].text = Random.Range(1, 7).ToString();
                }
                elapsed += rollAnimInterval;
                yield return new WaitForSeconds(rollAnimInterval);
            }

            // 显示最终结果
            currentValues = results;
            for (int i = 0; i < 3; i++)
            {
                if (diceTexts[i]) diceTexts[i].text = results[i].ToString();
            }

            // 评估组合
            currentCombo = evaluator.Evaluate(results);
            ShowCombination(currentCombo);

            hasRolled = true;
            isAnimating = false;

            // 更新按钮状态
            if (rollButton) rollButton.interactable = true;
            if (rerollButton) rerollButton.interactable = diceRoller.CanReroll;
            if (confirmButton) confirmButton.interactable = true;
            UpdateRerollCount();
        }

        #endregion

        #region 组合显示

        private void ShowCombination(DiceCombination combo)
        {
            if (comboArea) comboArea.SetActive(true);

            // 通过UIConfigBridge获取配置化显示数据
            var comboDisplay = UIConfigBridge.GetComboDisplayData(combo.Type);

            if (combinationText)
            {
                if (combo.Type == DiceCombinationType.None)
                    combinationText.text = "无组合";
                else
                    combinationText.text = comboDisplay.nameCN;
            }

            if (effectDescText)
            {
                // 配置有描述则优先用配置，否则用模型自带
                if (comboDisplay != null && !string.IsNullOrEmpty(comboDisplay.description))
                    effectDescText.text = comboDisplay.description;
                else
                    effectDescText.text = combo.EffectDescription;
            }
        }

        #endregion

        #region UI更新

        private void OnDiceRolled(int[] values)
        {
            // 更新锁定骰子的背景色
            for (int i = 0; i < 3; i++)
            {
                if (diceBgs[i])
                    diceBgs[i].color = Color.white;
            }
        }

        private void OnRerollsExhausted()
        {
            if (rerollButton) rerollButton.interactable = false;
            if (rerollCountText) rerollCountText.text = "重摇: 0";
        }

        private void UpdateRerollCount()
        {
            if (rerollCountText)
                rerollCountText.text = $"重摇: {diceRoller.RemainingRerolls}";
        }

        #endregion

        #region 确认

        private void OnConfirmClicked()
        {
            if (!hasRolled) return;

            // 将骰子组合传给肉鸽管理器，供战斗系统使用
            RoguelikeGameManager.Instance?.SetDiceCombo(currentCombo);

            // Bug#4 fix: 使用NextState而非ChangeState，保持状态机流程一致
            // ChangeState(Battle)跳过了Settlement等中间状态判断
            GameStateMachine.Instance.NextState(); // DiceRoll → Battle
        }

        #endregion

        #region 音效辅助

        /// <summary> 锁定Toggle变化回调 </summary>
        private void OnKeepToggleChanged(bool isOn)
        {
            PlayDiceAudio("lock");
        }

        /// <summary>
        /// 播放骰子相关音效 — 通过AudioManager播放，无AudioManager时静默
        /// type: "spin"(旋转) | "lock"(锁定/解锁)
        /// </summary>
        private void PlayDiceAudio(string type)
        {
            if (AudioManager.Instance == null) return;

            switch (type)
            {
                case "spin":
                    AudioManager.PlaySFX("sfx_dice_spin");
                    break;
                case "lock":
                    AudioManager.PlaySFX("sfx_dice_lock");
                    break;
            }
        }

        #endregion
    }
}
