using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 骰子掷骰面板
    /// UI元素：
    /// - dice1/2/3: 3个骰子Image（显示面值）
    /// - rollButton: 掷骰按钮
    /// - rerollButton: 重摇按钮（仅1次）
    /// - keepToggle1/2/3: 3个骰子锁定Toggle
    /// - combinationText: 组合结果显示（如"三条"/"顺子"）
    /// - combinationEffectText: 组合效果描述
    /// - confirmButton: 确认进入战斗
    /// </summary>
    public class DiceRollPanel : UIPanel
    {
        [Header("骰子显示")]
        public Image dice1;
        public Image dice2;
        public Image dice3;

        [Header("操作按钮")]
        public Button rollButton;
        public Button rerollButton;
        public Button confirmButton;

        [Header("锁定")]
        public Toggle keepToggle1;
        public Toggle keepToggle2;
        public Toggle keepToggle3;

        [Header("结果显示")]
        public Text combinationText;
        public Text combinationEffectText;

        protected override void OnShow()
        {
            rollButton?.onClick.AddListener(OnRollClicked);
            rerollButton?.onClick.AddListener(OnRerollClicked);
            confirmButton?.onClick.AddListener(OnConfirmClicked);
        }

        protected override void OnHide()
        {
            rollButton?.onClick.RemoveAllListeners();
            rerollButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
        }

        private void OnRollClicked()
        {
            // TODO: 调用 DiceRoller.RollAll()，播放掷骰动画，更新骰子Image
        }

        private void OnRerollClicked()
        {
            // TODO: 调用 DiceRoller.Reroll(keepMask)，更新显示
        }

        private void OnConfirmClicked()
        {
            GameStateMachine.Instance.ChangeState(GameState.Battle);
        }
    }
}
