using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 战斗面板 - 自动战斗
    /// UI元素：
    /// - playerUnitSlots: 我方单位展示区（最多3个）
    /// - enemyUnitSlots: 敌方单位展示区
    /// - speedButton: 加速按钮（1x/2x/4x切换）
    /// - skipButton: 跳过战斗按钮
    /// - diceSkillButton: 骰子技能释放按钮
    /// - roundText: 回合数
    /// - timerText: 倒计时
    /// </summary>
    public class BattlePanel : UIPanel
    {
        [Header("战斗控制")]
        public Button speedButton;
        public Button skipButton;
        public Button diceSkillButton;

        [Header("战斗信息")]
        public Text roundText;
        public Text timerText;
        public Text speedText;

        private float[] speedOptions = { 1f, 2f, 4f };
        private int currentSpeedIndex = 0;

        protected override void OnShow()
        {
            currentSpeedIndex = 0;
            speedButton?.onClick.AddListener(OnSpeedClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
            diceSkillButton?.onClick.AddListener(OnDiceSkillClicked);
        }

        protected override void OnHide()
        {
            speedButton?.onClick.RemoveAllListeners();
            skipButton?.onClick.RemoveAllListeners();
            diceSkillButton?.onClick.RemoveAllListeners();
        }

        private void OnSpeedClicked()
        {
            currentSpeedIndex = (currentSpeedIndex + 1) % speedOptions.Length;
            float speed = speedOptions[currentSpeedIndex];
            // TODO: BattleManager.SetBattleSpeed(speed)
            speedText.text = $"{speed}x";
        }

        private void OnSkipClicked()
        {
            // TODO: BattleManager.SkipBattle()
        }

        private void OnDiceSkillClicked()
        {
            // TODO: BattleManager.UseDiceSkill()
        }
    }
}
