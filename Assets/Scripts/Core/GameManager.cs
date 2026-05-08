using UnityEngine;

/// <summary>
/// 游戏总管理器 — 协调各个子系统
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameStateMachine StateMachine { get; private set; }
    public DiceRoller DiceRoller { get; private set; }

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
        StateMachine = GameStateMachine.Instance;
        if (StateMachine == null)
        {
            var go = new GameObject("GameStateMachine");
            StateMachine = go.AddComponent<GameStateMachine>();
        }

        // DiceRoller 是纯C#类（非MonoBehaviour），直接实例化
        DiceRoller = new DiceRoller(diceCount: 3, sides: 6);
    }

    /// <summary>
    /// 开始新游戏
    /// </summary>
    public void StartNewGame()
    {
        Debug.Log("[GameManager] 开始新游戏");
        StateMachine.ResetGame();
    }

    /// <summary>
    /// 开始新关卡
    /// </summary>
    public void StartLevel(int level)
    {
        Debug.Log($"[GameManager] 开始第{level}关");
    }
}
