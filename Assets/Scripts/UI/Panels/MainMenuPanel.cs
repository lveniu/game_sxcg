using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 主菜单面板
    /// UI元素：
    /// - 标题Image/Text: 游戏Logo
    /// - 开始游戏Button
    /// - 设置Button（可选）
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        [Header("UI引用")]
        [Tooltip("开始游戏按钮")]
        public Button startButton;

        protected override void OnShow()
        {
            startButton?.onClick.RemoveAllListeners();
            startButton?.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            GameStateMachine.Instance.ChangeState(GameState.HeroSelect);
        }
    }
}
