using UnityEngine;

/// <summary>
/// 游戏管理器 — 全局单例，整合骰子系统与状态机
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("核心系统")]
    public GameStateMachine StateMachine;
    public DiceRoller DiceRoller { get; private set; }

    [Header("游戏配置")]
    [Tooltip("每局骰子数量")]
    public int diceCount = 3;
    [Tooltip("每个骰子面数")]
    public int diceSides = 6;
    [Tooltip("免费重摇次数")]
    public int freeRerolls = 2;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSystems();
    }

    void Start()
    {
        Debug.Log("[GameManager] 游戏系统初始化完成，进入主菜单...");
    }

    /// <summary>
    /// 初始化核心系统
    /// </summary>
    private void InitializeSystems()
    {
        // 创建骰子投掷器
        DiceRoller = new DiceRoller(diceCount, diceSides);
        DiceRoller.SetFreeRerolls(freeRerolls);

        // 获取或创建状态机
        if (StateMachine == null)
        {
            StateMachine = FindObjectOfType<GameStateMachine>();
            if (StateMachine == null)
            {
                var go = new GameObject("GameStateMachine");
                StateMachine = go.AddComponent<GameStateMachine>();
            }
        }
    }

    /// <summary>
    /// 开始新游戏
    /// </summary>
    public void StartNewGame()
    {
        DiceRoller.Reset();
        StateMachine.ResetGame();
    }

    /// <summary>
    /// 跳转到下一个状态
    /// </summary>
    public void NextPhase()
    {
        StateMachine.NextState();
    }

    /// <summary>
    /// 获取当前骰子的组合信息
    /// </summary>
    public DiceCombination GetCurrentDiceCombination()
    {
        return DiceRoller.GetCurrentCombination();
    }
}
