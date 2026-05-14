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
    MapSelect,          // BE-08 地图路径选择
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
                ChangeState(GameState.MapSelect);
                break;
            case GameState.MapSelect:
                // MapSelect → DiceRoll 由 RoguelikeMapSystem.SelectNode() 驱动
                // 这里不自动推进，等玩家选择节点
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

                    // 检查是否击败最终Boss → 胜利结算
                    var mapSys = RoguelikeMapSystem.Instance;
                    if (mapSys != null && mapSys.IsFinalBossDefeated())
                    {
                        Debug.Log($"[GameStateMachine] 最终Boss已击败！通关结算");
                        // 标记为胜利结算（GameOverPanel 会显示通关）
                        var rgm = RoguelikeGameManager.Instance;
                        if (rgm != null)
                        {
                            rgm.GameOver(); // 触发 OnGameOver 事件和统计
                        }
                        ChangeState(GameState.GameOver);
                    }
                    else
                    {
                        ChangeState(GameState.RoguelikeReward);
                    }
                }
                break;
            case GameState.RoguelikeReward:
                // 奖励选完，回到地图选择（而非直接骰子）
                CurrentLevel++;
                IsGameWon = false;
                ChangeState(GameState.MapSelect);
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

    /// <summary>
    /// 从肉鸽运行存档恢复游戏 — 由 SaveLoadPanel "继续游戏" 按钮调用
    /// 流程：加载 RoguelikeRunData → RoguelikeGameManager.ResumeRun() → 跳转 MapSelect
    /// </summary>
    public void ResumeSavedRun()
    {
        var saveSys = SaveSystem.Instance;
        if (saveSys == null || !saveSys.HasSavedRun())
        {
            Debug.LogWarning("[StateMachine] 没有肉鸽运行存档可恢复");
            return;
        }

        var runData = saveSys.LoadRoguelikeRun();
        if (runData == null)
        {
            Debug.LogError("[StateMachine] 肉鸽运行存档加载失败");
            return;
        }

        // 确保RoguelikeGameManager存在
        var rgm = RoguelikeGameManager.Instance;
        if (rgm == null)
        {
            Debug.LogError("[StateMachine] RoguelikeGameManager不存在，无法恢复");
            return;
        }

        // 恢复肉鸽运行状态
        rgm.ResumeRun(runData);

        // 同步关卡进度到状态机
        CurrentLevel = runData.currentFloor;
        IsGameWon = false;
        IsGameLost = false;

        // 跳转到地图选择，让玩家从上次位置继续
        ChangeState(GameState.MapSelect);

        Debug.Log($"[StateMachine] 肉鸽存档恢复完成 → Floor {runData.currentFloor}, 跳转MapSelect");
    }

    /// <summary>
    /// BE-10: 战斗结算 — 给存活英雄分配经验
    /// </summary>
    private void GrantBattleExp()
    {
        var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
        if (heroes == null || heroes.Count == 0) return;

        int level = CurrentLevel;
        bool isBoss = (level % 5 == 0); // 每5关Boss
        int baseExp = 10; // 通关基础经验

        HeroExpSystem.Instance?.GainExpForTeam(heroes, baseExp, level, isBoss);
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

            case GameState.MapSelect:
                // 首次进入时生成地图，后续进入时刷新可选节点
                if (RoguelikeMapSystem.Instance == null)
                    new RoguelikeMapSystem();
                if (RoguelikeMapSystem.Instance.CurrentMap == null)
                    RoguelikeMapSystem.Instance.GenerateMap(15);
                Debug.Log($"[StateMachine] 进入地图选择 — {RoguelikeMapSystem.Instance.GetMapStats()}");
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
                // BE-10: 战斗结算经验
                GrantBattleExp();
                // Settlement面板自行处理子面板流程
                Debug.Log($"[StateMachine] 进入结算 — 第{CurrentLevel}关");
                break;
        }
    }
}
