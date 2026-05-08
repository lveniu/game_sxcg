using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 战斗结算面板
    /// UI元素：
    /// - resultText: 胜利/失败
    /// - levelText: 当前关卡数
    /// - rewardSummaryText: 获得奖励摘要
    /// - nextButton: 继续按钮（→肉鸽奖励或GameOver）
    /// </summary>
    public class SettlementPanel : UIPanel
    {
        [Header("UI引用")]
        public Text resultText;
        public Text levelText;
        public Text rewardSummaryText;
        public Button nextButton;

        protected override void OnShow()
        {
            nextButton?.onClick.AddListener(OnNextClicked);
        }

        protected override void OnHide()
        {
            nextButton?.onClick.RemoveAllListeners();
        }

        private void OnNextClicked()
        {
            // 状态机会在Settlement中判断存活/阵亡，自动跳转
            // 这里只是触发下一步
            GameStateMachine.Instance.NextState();
        }
    }
}
