using UnityEngine;
using System.Collections.Generic;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// UI管理器 - 微信小游戏竖屏版
    /// 
    /// Canvas配置参考（在Unity Editor中设置）：
    /// - Canvas Scaler: Scale With Screen Size
    /// - Reference Resolution: 720 x 1280（竖屏）
    /// - Match: 0.5（宽高各半）
    /// - Screen Match Mode: MatchWidthOrHeight
    /// - 安全区适配：使用 SafeArea 组件处理刘海屏
    /// </summary>
    public class NewUIManager : MonoBehaviour
    {
        public static NewUIManager Instance { get; private set; }

        [Header("面板引用 - 在Inspector中拖拽绑定")]
        public MainMenuPanel mainMenuPanel;
        public HeroSelectPanel heroSelectPanel;
        public DiceRollPanel diceRollPanel;
        public BattlePanel battlePanel;
        public SettlementPanel settlementPanel;
        public RoguelikeRewardPanel roguelikeRewardPanel;
        public GameOverPanel gameOverPanel;

        // 子面板（非状态驱动，手动显示/隐藏）
        [Header("子面板 - 卡牌&站位阶段")]
        public CardPlayPanel cardPlayPanel;
        public BattleGridPanel battleGridPanel;

        [Header("子面板 - 结算子流程")]
        public EventPanel eventPanel;
        public ShopPanel shopPanel;
        public EquipPanel equipPanel;

        [Header("子面板 - 肉鸽地图&背包")]
        public RoguelikeMapPanel roguelikeMapPanel;
        public InventoryPanel inventoryPanel;

        private Dictionary<GameState, UIPanel> panelMap;
        private UIPanel currentPanel;

        private void Awake()
        {
            Instance = this;
            InitPanelMap();
        }

        private void Start()
        {
            // 订阅状态机事件
            GameStateMachine.Instance.OnStateChanged += OnGameStateChanged;
            
            // 初始隐藏所有面板
            HideAllPanels();
            
            // 如果状态机已经在某个状态（比如Start()先于本函数执行），
            // 立即同步到当前状态
            if (GameStateMachine.Instance != null && 
                GameStateMachine.Instance.CurrentState != GameState.MainMenu)
            {
                SwitchPanel(GameStateMachine.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;
        }

        /// <summary>初始化面板映射</summary>
        private void InitPanelMap()
        {
            panelMap = new Dictionary<GameState, UIPanel>
            {
                { GameState.MainMenu, mainMenuPanel },
                { GameState.HeroSelect, heroSelectPanel },
                { GameState.DiceRoll, diceRollPanel },
                { GameState.Battle, battlePanel },
                { GameState.Settlement, settlementPanel },
                { GameState.RoguelikeReward, roguelikeRewardPanel },
                { GameState.GameOver, gameOverPanel },
                { GameState.MapSelect, roguelikeMapPanel }
            };
        }

        /// <summary>
        /// 面板引用绑定完成后重建映射（供RuntimeSceneBootstrap调用）
        /// Awake时面板可能还未创建，需要在绑定后重新构建
        /// </summary>
        public void RebuildPanelMap()
        {
            InitPanelMap();
            HideAllPanels();
        }

        /// <summary>状态切换回调（签名匹配 GameStateMachine.OnStateChanged: Action&lt;GameState, GameState&gt;）</summary>
        private void OnGameStateChanged(GameState oldState, GameState newState)
        {
            SwitchPanel(newState);
        }

        /// <summary>切换面板</summary>
        public void SwitchPanel(GameState state)
        {
            if (!panelMap.TryGetValue(state, out var panel))
            {
                Debug.LogWarning($"[UIManager] 未找到状态 {state} 对应的面板");
                return;
            }

            // 隐藏当前面板
            if (currentPanel != null)
                currentPanel.Hide();

            // 显示新面板
            currentPanel = panel;
            currentPanel.Show();
        }

        /// <summary>隐藏所有面板</summary>
        private void HideAllPanels()
        {
            foreach (var kvp in panelMap)
            {
                if (kvp.Value != null)
                    kvp.Value.HideImmediate();
            }
        }

        /// <summary>获取当前面板</summary>
        public T GetCurrentPanel<T>() where T : UIPanel
        {
            return currentPanel as T;
        }

        /// <summary>显示子面板（非状态驱动，如卡牌面板、站位面板）</summary>
        public void ShowSubPanel(UIPanel panel)
        {
            if (panel != null) panel.Show();
        }

        /// <summary>显示子面板（按名称查找）</summary>
        public void ShowSubPanel(string panelId)
        {
            var panel = panelId switch
            {
                "CardPlay" => (UIPanel)cardPlayPanel,
                "BattleGrid" => battleGridPanel,
                "Event" => eventPanel,
                "Shop" => shopPanel,
                "Equip" => equipPanel,
                "RoguelikeMap" => roguelikeMapPanel,
                "Inventory" => inventoryPanel,
                _ => null
            };
            if (panel != null) panel.Show();
            else Debug.LogWarning($"[UIManager] 未找到子面板：{panelId}");
        }

        /// <summary>隐藏子面板</summary>
        public void HideSubPanel(UIPanel panel)
        {
            if (panel != null) panel.Hide();
        }

        /// <summary>进入卡牌+站位阶段（DiceRoll结束后调用）</summary>
        public void EnterCardPlayPhase()
        {
            ShowSubPanel(cardPlayPanel);
        }

        /// <summary>从卡牌阶段进入站位阶段</summary>
        public void EnterPositioningPhase()
        {
            HideSubPanel(cardPlayPanel);
            ShowSubPanel(battleGridPanel);
        }
    }
}
