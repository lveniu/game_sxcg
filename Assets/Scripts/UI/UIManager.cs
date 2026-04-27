using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI管理器 — 单例，管理所有界面面板的显示/隐藏，并与GameStateMachine联动
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("各界面面板")]
    public GameObject mainMenuPanel;
    public GameObject heroSelectPanel;
    public GameObject diceRollPanel;
    public GameObject cardPlayPanel;
    public GameObject battlePanel;
    public GameObject settlementPanel;

    private Dictionary<GameState, GameObject> statePanelMap;
    private GameObject currentPanel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        statePanelMap = new Dictionary<GameState, GameObject>
        {
            { GameState.MainMenu, mainMenuPanel },
            { GameState.HeroSelect, heroSelectPanel },
            { GameState.DiceRoll, diceRollPanel },
            { GameState.CardPlay, cardPlayPanel },
            { GameState.Positioning, cardPlayPanel }, // 出牌和站位同一个界面
            { GameState.Battle, battlePanel },
            { GameState.Settlement, settlementPanel },
            { GameState.GameOver, settlementPanel }
        };
    }

    void Start()
    {
        // 注册状态机事件
        if (GameStateMachine.Instance != null)
        {
            GameStateMachine.Instance.OnStateChanged += OnGameStateChanged;
        }

        // 初始隐藏所有面板
        HideAllPanels();
    }

    void OnDestroy()
    {
        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (statePanelMap.TryGetValue(newState, out var panel))
        {
            ShowPanel(panel);
        }
    }

    /// <summary>
    /// 显示指定面板
    /// </summary>
    public void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        HideAllPanels();
        panel.SetActive(true);
        currentPanel = panel;
    }

    /// <summary>
    /// 隐藏当前面板
    /// </summary>
    public void HideCurrentPanel()
    {
        if (currentPanel != null)
            currentPanel.SetActive(false);
    }

    /// <summary>
    /// 隐藏所有面板
    /// </summary>
    public void HideAllPanels()
    {
        mainMenuPanel?.SetActive(false);
        heroSelectPanel?.SetActive(false);
        diceRollPanel?.SetActive(false);
        cardPlayPanel?.SetActive(false);
        battlePanel?.SetActive(false);
        settlementPanel?.SetActive(false);
    }

    /// <summary>
    /// 获取当前显示的面板
    /// </summary>
    public GameObject GetCurrentPanel() => currentPanel;
}
