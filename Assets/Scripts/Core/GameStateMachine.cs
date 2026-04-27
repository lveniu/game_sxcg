using System;
using UnityEngine;

/// <summary>
/// 游戏核心状态 — 管理赌狗流循环流程
/// </summary>
public enum GameState
{
    MainMenu,       // 主菜单
    HeroSelect,     // 选择初始英雄
    DiceRoll,       // 骰子阶段（投掷+重摇）
    CardPlay,       // 出牌阶段（召唤/打卡）
    Positioning,    // 站位阶段
    Battle,         // 自动战斗
    Settlement,     // 结算阶段
    GameOver        // 游戏结束
}

/// <summary>
/// 游戏状态机 — 单例，管理核心循环的状态转换
/// </summary>
public class GameStateMachine : MonoBehaviour
{
    public static GameStateMachine Instance { get; private set; }

    [SerializeField]
    private GameState currentState;
    public GameState CurrentState => currentState;

    public int CurrentLevel { get; private set; } = 1;
    public bool IsGameWon { get; private set; }
    public bool IsGameLost { get; private set; }

    // 状态变化事件
    public event Action<GameState, GameState> OnStateChanged; // (oldState, newState)
    public event Action<GameState> OnStateEntered;
    public event Action<GameState> OnStateExited;

    // 关卡事件
    public event Action<int> OnLevelStarted;  // 关卡编号
    public event Action<int, bool> OnLevelEnded; // (关卡编号, 是否胜利)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 游戏启动后进入主菜单
        ChangeState(GameState.MainMenu);
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        var oldState = currentState;
        OnStateExited?.Invoke(oldState);

        currentState = newState;
        Debug.Log($"[StateMachine] {oldState} -> {newState}");

        OnStateChanged?.Invoke(oldState, newState);
        OnStateEntered?.Invoke(newState);

        HandleStateEntered(newState);
    }

    /// <summary>
    /// 按核心循环顺序推进到下一个状态
    /// </summary>
    public void NextState()
    {
        switch (currentState)
        {
            case GameState.MainMenu:
                ChangeState(GameState.HeroSelect);
                break;
            case GameState.HeroSelect:
                ChangeState(GameState.DiceRoll);
                break;
            case GameState.DiceRoll:
                ChangeState(GameState.CardPlay);
                break;
            case GameState.CardPlay:
                ChangeState(GameState.Positioning);
                break;
            case GameState.Positioning:
                ChangeState(GameState.Battle);
                break;
            case GameState.Battle:
                ChangeState(GameState.Settlement);
                break;
            case GameState.Settlement:
                if (IsGameWon)
                    ChangeState(GameState.GameOver);
                else if (IsGameLost)
                    ChangeState(GameState.GameOver);
                else
                {
                    CurrentLevel++;
                    ChangeState(GameState.DiceRoll); // 进入下一关
                }
                break;
            case GameState.GameOver:
                ChangeState(GameState.MainMenu);
                break;
        }
    }

    /// <summary>
    /// 立即进入战斗（跳过出牌和站位，用于快速测试）
    /// </summary>
    public void SkipToBattle()
    {
        if (currentState == GameState.CardPlay || currentState == GameState.Positioning)
        {
            ChangeState(GameState.Battle);
        }
    }

    /// <summary>
    /// 宣告游戏胜利
    /// </summary>
    public void SetGameWon()
    {
        IsGameWon = true;
        IsGameLost = false;
    }

    /// <summary>
    /// 宣告游戏失败
    /// </summary>
    public void SetGameLost()
    {
        IsGameWon = false;
        IsGameLost = true;
    }

    /// <summary>
    /// 重置游戏状态（新开一局）
    /// </summary>
    public void ResetGame()
    {
        CurrentLevel = 1;
        IsGameWon = false;
        IsGameLost = false;
        ChangeState(GameState.HeroSelect);
    }

    private void HandleStateEntered(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:
                // TODO: 显示主菜单UI
                break;

            case GameState.HeroSelect:
                // TODO: 显示英雄选择界面
                break;

            case GameState.DiceRoll:
                OnLevelStarted?.Invoke(CurrentLevel);
                // TODO: 初始化骰子投掷器，显示骰子UI
                break;

            case GameState.CardPlay:
                // TODO: 显示卡牌手牌，允许打出/召唤
                break;

            case GameState.Positioning:
                // TODO: 显示棋盘，允许拖拽站位
                break;

            case GameState.Battle:
                // TODO: 启动BattleManager开始自动战斗
                break;

            case GameState.Settlement:
                // TODO: 显示战斗结果和奖励选择
                break;

            case GameState.GameOver:
                // TODO: 显示通关/失败界面
                break;
        }
    }
}
