using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 肉鸽三选一奖励面板
    /// UI元素：
    /// - rewardCard1/2/3: 3张奖励卡片Button
    /// - rewardName1/2/3: 奖励名称Text
    /// - rewardDesc1/2/3: 奖励描述Text
    /// - rewardIcon1/2/3: 奖励图标Image
    /// - rarityBorder1/2/3: 稀有度边框Image
    /// </summary>
    public class RoguelikeRewardPanel : UIPanel
    {
        [Header("奖励卡片")]
        public Button rewardCard1;
        public Button rewardCard2;
        public Button rewardCard3;

        [Header("奖励名称")]
        public Text rewardName1;
        public Text rewardName2;
        public Text rewardName3;

        [Header("奖励描述")]
        public Text rewardDesc1;
        public Text rewardDesc2;
        public Text rewardDesc3;

        [Header("奖励图标")]
        public Image rewardIcon1;
        public Image rewardIcon2;
        public Image rewardIcon3;

        protected override void OnShow()
        {
            rewardCard1?.onClick.AddListener(() => SelectReward(0));
            rewardCard2?.onClick.AddListener(() => SelectReward(1));
            rewardCard3?.onClick.AddListener(() => SelectReward(2));
        }

        protected override void OnHide()
        {
            rewardCard1?.onClick.RemoveAllListeners();
            rewardCard2?.onClick.RemoveAllListeners();
            rewardCard3?.onClick.RemoveAllListeners();
        }

        private void SelectReward(int index)
        {
            // TODO: 调用 RoguelikeRewardSystem.ApplyReward(index)
            // 进入下一关骰子阶段
            GameStateMachine.Instance.ChangeState(GameState.DiceRoll);
        }
    }
}
