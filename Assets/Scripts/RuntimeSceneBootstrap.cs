using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时场景启动器 — 挂载到任意GameObject，点Play自动创建完整游戏场景
/// 使用方法: 创建空物体→挂载此脚本→点Play
/// 
/// 创建流程：
/// 1. 核心单例管理器（GameManager, GameStateMachine, RoguelikeGameManager, BattleManager）
/// 2. Canvas + EventSystem
/// 3. NewUIManager + 所有Panel子对象
/// 4. 初始化游戏进入MainMenu
/// </summary>
public class RuntimeSceneBootstrap : MonoBehaviour
{
    [Header("是否自动运行")]
    public bool autoBootstrap = true;

    void Start()
    {
        if (autoBootstrap)
        {
            Bootstrap();
        }
    }

    public void Bootstrap()
    {
        Debug.Log("[Bootstrap] 开始初始化场景...");

        // 1. 核心管理器（DontDestroyOnLoad的单例）
        EnsureManager<GameManager>("GameManager");
        EnsureManager<GameStateMachine>("GameStateMachine");
        EnsureManager<RoguelikeGameManager>("RoguelikeGameManager");
        EnsureManager<BattleManager>("BattleManager");

        // 2. Canvas + EventSystem
        var canvas = SetupCanvas();

        // 3. NewUIManager — 挂载到Canvas上
        var uiMgr = canvas.gameObject.GetComponent<Game.UI.NewUIManager>();
        if (uiMgr == null)
            uiMgr = canvas.gameObject.AddComponent<Game.UI.NewUIManager>();

        // 4. 创建所有Panel子对象并绑定到NewUIManager
        // 注意：NewUIManager.Awake()在AddComponent时已调用，InitPanelMap()此时引用为null
        // 所以在绑定完所有面板后，需要重新初始化panelMap
        uiMgr.mainMenuPanel = CreatePanelWithComponent<Game.UI.MainMenuPanel>(canvas, "MainMenuPanel");
        uiMgr.heroSelectPanel = CreatePanelWithComponent<Game.UI.HeroSelectPanel>(canvas, "HeroSelectPanel");
        uiMgr.diceRollPanel = CreatePanelWithComponent<Game.UI.DiceRollPanel>(canvas, "DiceRollPanel");
        uiMgr.battlePanel = CreatePanelWithComponent<Game.UI.BattlePanel>(canvas, "BattlePanel");
        uiMgr.settlementPanel = CreatePanelWithComponent<Game.UI.SettlementPanel>(canvas, "SettlementPanel");
        uiMgr.roguelikeRewardPanel = CreatePanelWithComponent<Game.UI.RoguelikeRewardPanel>(canvas, "RoguelikeRewardPanel");
        uiMgr.gameOverPanel = CreatePanelWithComponent<Game.UI.GameOverPanel>(canvas, "GameOverPanel");

        // 子面板
        uiMgr.eventPanel = CreatePanelWithComponent<Game.UI.EventPanel>(canvas, "EventPanel");
        uiMgr.shopPanel = CreatePanelWithComponent<Game.UI.ShopPanel>(canvas, "ShopPanel");
        uiMgr.equipPanel = CreatePanelWithComponent<Game.UI.EquipPanel>(canvas, "EquipPanel");
        uiMgr.cardPlayPanel = CreatePanelWithComponent<Game.UI.CardPlayPanel>(canvas, "CardPlayPanel");
        uiMgr.battleGridPanel = CreatePanelWithComponent<Game.UI.BattleGridPanel>(canvas, "BattleGridPanel");

        // 面板引用已绑定，重新初始化panelMap
        uiMgr.RebuildPanelMap();

        // 5. NewUIManager会通过Awake自动收集所有子Panel
        // 然后订阅GameStateMachine.OnStateChanged来驱动面板切换

        // 6. 初始化游戏（会触发MainMenu状态）
        // RoguelikeGameManager.StartNewGame() 在 MainMenuPanel 点击开始时调用
        // GameStateMachine.Start() 会自动进入 MainMenu 状态

        // 初始状态：隐藏所有Panel（NewUIManager的ShowPanel会控制）
        foreach (Transform child in canvas.transform)
        {
            if (child.GetComponent<Game.UI.UIPanel>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        // 激活MainMenu面板（因为GameStateMachine.Start会ChangeState(MainMenu)）
        // NewUIManager.OnGameStateChanged会处理面板切换

        Debug.Log("[Bootstrap] 场景初始化完成！等待GameStateMachine进入MainMenu...");
    }

    T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        var existing = Object.FindObjectOfType<T>();
        if (existing != null) return existing;
        var go = new GameObject(name);
        return go.AddComponent<T>();
    }

    Canvas SetupCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        return canvas;
    }

    /// <summary>
    /// 创建全屏Panel并挂载UIPanel组件
    /// </summary>
    T CreatePanelWithComponent<T>(Canvas canvas, string name) where T : Game.UI.UIPanel
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 背景遮罩
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        bg.raycastTarget = true;

        // 添加CanvasGroup用于淡入淡出
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;

        var panel = go.AddComponent<T>();
        return panel;
    }
}
