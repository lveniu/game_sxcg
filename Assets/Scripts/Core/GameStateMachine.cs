using System;
using UnityEngine;

/// <summary>
/// 游戏核心状态 — 肉鸽循环流程
/// MainMenu → HeroSelect → DiceRoll → Battle → Settlement →
///   英雄存活 → RoguelikeReward → DiceRoll(下一关)
///   英雄阵亡 → GameOver
/// </summary>
public enum GameState
{
    MainMenu,           // 主菜单
    HeroSelect,         // 选择初始英雄（战/法/刺三选一）
    DiceRoll,           // 骰子阶段（投掷+免费重摇1次）
    Battle,             // 自动战斗
    Settlement,         // 结算（判断胜负）
    RoguelikeReward,    // 肉鸽三选一奖励
    GameOver            // 游戏结束（阵亡）
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
    /// MainMenu → HeroSelect → DiceRoll → Battle → Settlement →
    ///   英雄存活 → RoguelikeReward → DiceRoll(下一关)
    ///   英雄阵亡 → GameOver
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
                ChangeState(GameState.Battle);
                break;
            case GameState.Battle:
                ChangeState(GameState.Settlement);
                break;
            case GameState.Settlement:
                if (IsGameLost)
                {
                    ChangeState(GameState.GameOver);
                }
                else
                {
                    IsGameWon = true;
                    OnLevelEnded?.Invoke(CurrentLevel, true);
                    ChangeState(GameState.RoguelikeReward);
                }
                break;
            case GameState.RoguelikeReward:
                // 奖励选完，进入下一关
                CurrentLevel++;
                IsGameWon = false;
                ChangeState(GameState.DiceRoll);
                break;
            case GameState.GameOver:
                ChangeState(GameState.MainMenu);
                break;
        }
    }

    /// <summary>
    /// 宣告游戏胜利（本关通关）
    /// </summary>
    public void SetGameWon()
    {
        IsGameWon = true;
        IsGameLost = false;
    }

    /// <summary>
    /// 宣告游戏失败（阵亡）
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
                // UI由NewUIManager自动处理
                Debug.Log("[StateMachine] 进入主菜单");
                break;

            case GameState.HeroSelect:
                // UI由NewUIManager自动处理
                Debug.Log("[StateMachine] 进入英雄选择");
                break;

            case GameState.DiceRoll:
                OnLevelStarted?.Invoke(CurrentLevel);
                // 通知RoguelikeGameManager推进关卡（首次已在StartNewGame时设为0）
                RoguelikeGameManager.Instance?.EnterNextLevel();
                Debug.Log($"[StateMachine] 进入掷骰阶段 — 第{CurrentLevel}关");
                break;

            case GameState.Battle:
                // 启动战斗：由RoguelikeGameManager协调BattleManager
                RoguelikeGameManager.Instance?.StartBattle();
                Debug.Log($"[StateMachine] 战斗开始 — 第{CurrentLevel}关");
                break;

            case GameState.RoguelikeReward:
                // UI由NewUIManager自动显示RoguelikeRewardPanel
                Debug.Log($"[StateMachine] 进入奖励选择 — 第{CurrentLevel}关");
                break;

            case GameState.GameOver:
                Debug.Log($"[GameOver] 在第{CurrentLevel}关结束");
                break;

            case GameState.Settlement:
                // Settlement面板自行处理子面板流程
                Debug.Log($"[StateMachine] 进入结算 — 第{CurrentLevel}关");
                break;
        }
    }
}
