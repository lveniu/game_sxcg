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
            // Remove old listeners first to avoid duplicates
            restartButton?.onClick.RemoveAllListeners();
            shareButton?.onClick.RemoveAllListeners();

            // Bind fresh listeners
            restartButton?.onClick.AddListener(OnRestartClicked);
            shareButton?.onClick.AddListener(OnShareClicked);

            PopulateStats();
        }

        protected override void OnHide()
        {
            restartButton?.onClick.RemoveAllListeners();
            shareButton?.onClick.RemoveAllListeners();
        }

        private void PopulateStats()
        {
            var gsm = GameStateMachine.Instance;
            var rgm = RoguelikeGameManager.Instance;

            // Determine win or loss
            bool isWin = gsm != null && gsm.IsGameWon;
            if (resultTitle != null)
                resultTitle.text = isWin ? "🏆 通关！" : "💀 阵亡";

            // Level reached — prefer RoguelikeGameManager's tracked level
            int level = rgm != null ? rgm.CurrentLevel : (gsm != null ? gsm.CurrentLevel : 0);
            if (levelReachedText != null)
                levelReachedText.text = $"到达关卡: {level}";

            // Relic count from RoguelikeGameManager
            int relicCount = 0;
            if (rgm != null && rgm.RelicSystem != null)
                relicCount = rgm.RelicSystem.RelicCount;
            if (relicCountText != null)
                relicCountText.text = $"收集遗物: {relicCount}";

            // Kill count placeholder
            if (killCountText != null)
                killCountText.text = "击杀数: --";
        }

        private void OnRestartClicked()
        {
            // 重置肉鸽状态
            RoguelikeGameManager.Instance?.StartNewGame();

            // 重置状态机的关卡计数
            if (GameStateMachine.Instance != null)
            {
                // ResetGame会重置CurrentLevel=1, IsGameWon/IsGameLost=false
                // 然后跳到HeroSelect — 但我们想回主菜单让玩家点击"开始游戏"
                // 所以先重置数值，再跳转
                GameStateMachine.Instance.ResetGame();
                // ResetGame会自动ChangeState(HeroSelect)
                // 如果想回主菜单，改用以下代码：
                // GameStateMachine.Instance.ChangeState(GameState.MainMenu);
            }
        }

        private void OnShareClicked()
        {
            // TODO: 调用微信分享API
        }
    }
}
