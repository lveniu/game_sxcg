using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 游戏结束面板
    /// UI元素：
    /// - resultTitle: 阵亡/通关标题Text
    /// - levelReachedText: 到达关卡数
    /// - relicCountText: 收集遗物数
    /// - killCountText: 击杀数
    /// - restartButton: 再来一局
    /// - shareButton: 分享战绩（可选）
    /// </summary>
    public class GameOverPanel : UIPanel
    {
        [Header("UI引用")]
        public Text resultTitle;
        public Text levelReachedText;
        public Text relicCountText;
        public Text killCountText;
        public Button restartButton;
        public Button shareButton;

        protected override void OnShow()
        {
            restartButton?.onClick.AddListener(OnRestartClicked);
            shareButton?.onClick.AddListener(OnShareClicked);
        }

        protected override void OnHide()
        {
            restartButton?.onClick.RemoveAllListeners();
            shareButton?.onClick.RemoveAllListeners();
        }

        private void OnRestartClicked()
        {
            // TODO: 重置游戏状态
            GameStateMachine.Instance.ChangeState(GameState.MainMenu);
        }

        private void OnShareClicked()
        {
            // TODO: 调用微信分享API
        }
    }
}
