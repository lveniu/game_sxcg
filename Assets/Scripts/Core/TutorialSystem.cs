using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 新手引导步骤枚举
/// </summary>
public enum TutorialStep
{
    Welcome,       // 欢迎文字 + "开始冒险"按钮
    HeroSelect,    // 引导选择英雄，高亮英雄卡牌区域
    DiceRoll,      // 引导掷骰子，高亮掷骰按钮
    CardPlay,      // 引导出牌，高亮手牌区
    Positioning,   // 引导布阵，高亮棋盘
    Battle,        // 告知战斗自动进行
    Victory,       // 告知结算奖励
    MapSelect,     // 告知选择下一个节点
    Completed      // 引导全部完成（内部标记，不作为实际步骤）
}

/// <summary>
/// 引导步骤数据（可序列化，供UI层读取渲染）
/// </summary>
[System.Serializable]
public class TutorialStepData
{
    public TutorialStep step;
    public string title;
    public string description;
    public string targetPanelName;    // 要高亮/聚焦的面板名称
    public string highlightElement;   // 要高亮的UI元素路径
    public string confirmText;        // 确认按钮文字
}

/// <summary>
/// 新手引导系统 — 单例 MonoBehaviour
/// 
/// 职责：
/// - 管理引导步骤的推进与状态
/// - 订阅 GameStateMachine.OnStateChanged 自动推进
/// - 提供事件供 UI 层监听并渲染遮罩/高亮/气泡
/// - 不负责 UI 渲染（由前端根据事件自行实现）
/// 
/// 使用方式：
/// - TutorialSystem.Instance 获取单例
/// - 监听 OnTutorialStepChanged 获取当前步骤数据
/// - 调用 ConfirmCurrentStep() 确认当前步骤
/// - 调用 ResetTutorial() 手动重置引导
/// </summary>
public class TutorialSystem : MonoBehaviour
{
    public static TutorialSystem Instance { get; private set; }

    // ────────────────────────────────────────────
    // 事件
    // ────────────────────────────────────────────

    /// <summary>
    /// 引导步骤变化时触发，参数为当前步骤数据（null 表示引导结束）
    /// </summary>
    public event Action<TutorialStepData> OnTutorialStepChanged;

    /// <summary>
    /// 引导全部完成时触发
    /// </summary>
    public event Action OnTutorialCompleted;

    // ────────────────────────────────────────────
    // 公开属性
    // ────────────────────────────────────────────

    /// <summary>当前引导步骤</summary>
    public TutorialStep CurrentStep => currentStep;

    /// <summary>是否正在引导中</summary>
    public bool IsTutorialActive => currentStep != TutorialStep.Completed && !isCompleted;

    /// <summary>引导是否已完成（整个引导流程走完）</summary>
    public bool IsCompleted => isCompleted;

    // ────────────────────────────────────────────
    // 私有字段
    // ────────────────────────────────────────────

    [Header("调试")]
    [SerializeField] private TutorialStep currentStep = TutorialStep.Welcome;
    [SerializeField] private bool isCompleted = false;

    /// <summary>所有引导步骤数据（硬编码中文内容）</summary>
    private List<TutorialStepData> stepDataList;

    /// <summary>记录哪些步骤已被确认过（防止重复触发）</summary>
    private HashSet<TutorialStep> confirmedSteps = new HashSet<TutorialStep>();

    /// <summary>GameState → TutorialStep 的映射，用于状态机自动推进</summary>
    private Dictionary<GameState, TutorialStep> stateToStepMap;

    // ────────────────────────────────────────────
    // 常量
    // ────────────────────────────────────────────

    private const string PREFS_KEY_TUTORIAL_DONE = "tutorial_done";

    // ════════════════════════════════════════════
    // Unity 生命周期
    // ════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeStepData();
        InitializeStateMapping();

        // 读取 PlayerPrefs 判断是否已完成引导
        isCompleted = PlayerPrefs.GetInt(PREFS_KEY_TUTORIAL_DONE, 0) == 1;
        if (isCompleted)
        {
            currentStep = TutorialStep.Completed;
        }
    }

    private void Start()
    {
        // 订阅状态机事件
        if (GameStateMachine.Instance != null)
        {
            GameStateMachine.Instance.OnStateChanged += OnGameStateChanged;
        }
        else
        {
            Debug.LogWarning("[TutorialSystem] GameStateMachine 尚未初始化，引导将无法自动推进");
        }

        // 如果引导未完成，启动第一步
        if (!isCompleted)
        {
            SetStep(TutorialStep.Welcome);
        }
    }

    private void OnDestroy()
    {
        if (GameStateMachine.Instance != null)
        {
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ════════════════════════════════════════════
    // 公开方法
    // ════════════════════════════════════════════

    /// <summary>
    /// 确认当前步骤（由 UI 确认按钮调用）
    /// </summary>
    public void ConfirmCurrentStep()
    {
        if (currentStep == TutorialStep.Completed || isCompleted) return;

        confirmedSteps.Add(currentStep);
        Debug.Log($"[TutorialSystem] 确认步骤: {currentStep}");
    }

    /// <summary>
    /// 获取当前步骤的数据
    /// </summary>
    /// <returns>当前步骤数据，如果引导结束则返回 null</returns>
    public TutorialStepData GetCurrentStepData()
    {
        if (currentStep == TutorialStep.Completed) return null;
        return stepDataList.Find(d => d.step == currentStep);
    }

    /// <summary>
    /// 获取所有步骤数据（供 UI 一次性获取全部引导内容）
    /// </summary>
    public List<TutorialStepData> GetAllStepData()
    {
        return new List<TutorialStepData>(stepDataList);
    }

    /// <summary>
    /// 获取指定步骤的数据
    /// </summary>
    public TutorialStepData GetStepData(TutorialStep step)
    {
        return stepDataList.Find(d => d.step == step);
    }

    /// <summary>
    /// 检查指定步骤是否已被确认
    /// </summary>
    public bool IsStepConfirmed(TutorialStep step)
    {
        return confirmedSteps.Contains(step);
    }

    /// <summary>
    /// 手动重置引导（清除完成标记，重新开始）
    /// </summary>
    public static void ResetTutorial()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[TutorialSystem] 实例不存在，无法重置");
            return;
        }

        PlayerPrefs.DeleteKey(PREFS_KEY_TUTORIAL_DONE);
        PlayerPrefs.Save();

        Instance.isCompleted = false;
        Instance.confirmedSteps.Clear();
        Instance.SetStep(TutorialStep.Welcome);

        Debug.Log("[TutorialSystem] 引导已重置，将从 Welcome 重新开始");
    }

    /// <summary>
    /// 手动强制跳到指定步骤（调试用）
    /// </summary>
    /// <param name="step">目标步骤</param>
    public void ForceSetStep(TutorialStep step)
    {
        if (step == TutorialStep.Completed) return;

        Debug.Log($"[TutorialSystem] 强制跳转步骤: {currentStep} → {step}");
        isCompleted = false;
        SetStep(step);
    }

    /// <summary>
    /// 跳过整个引导流程
    /// </summary>
    public void SkipTutorial()
    {
        CompleteTutorial();
        Debug.Log("[TutorialSystem] 引导已跳过");
    }

    // ════════════════════════════════════════════
    // 内部逻辑
    // ════════════════════════════════════════════

    /// <summary>
    /// 设置当前步骤并触发事件
    /// </summary>
    private void SetStep(TutorialStep step)
    {
        if (step == TutorialStep.Completed)
        {
            CompleteTutorial();
            return;
        }

        currentStep = step;
        var data = GetCurrentStepData();

        Debug.Log($"[TutorialSystem] 步骤变更: {step} — {data?.title ?? "null"}");

        OnTutorialStepChanged?.Invoke(data);
    }

    /// <summary>
    /// 完成引导流程
    /// </summary>
    private void CompleteTutorial()
    {
        currentStep = TutorialStep.Completed;
        isCompleted = true;

        PlayerPrefs.SetInt(PREFS_KEY_TUTORIAL_DONE, 1);
        PlayerPrefs.Save();

        // 触发事件：步骤清空 + 引导完成
        OnTutorialStepChanged?.Invoke(null);
        OnTutorialCompleted?.Invoke();

        Debug.Log("[TutorialSystem] 新手引导全部完成！");
    }

    /// <summary>
    /// GameStateMachine 状态变化回调 — 自动推进引导步骤
    /// </summary>
    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (isCompleted || currentStep == TutorialStep.Completed) return;

        // 尝试从状态映射获取对应的引导步骤
        if (stateToStepMap.TryGetValue(newState, out TutorialStep targetStep))
        {
            // 只有目标步骤在当前步骤之后才推进（防止回退）
            if (targetStep > currentStep)
            {
                Debug.Log($"[TutorialSystem] 状态机 {oldState} → {newState}，自动推进引导 {currentStep} → {targetStep}");
                SetStep(targetStep);
            }
        }
    }

    // ════════════════════════════════════════════
    // 初始化
    // ════════════════════════════════════════════

    /// <summary>
    /// 初始化 GameState → TutorialStep 映射
    /// </summary>
    private void InitializeStateMapping()
    {
        stateToStepMap = new Dictionary<GameState, TutorialStep>
        {
            { GameState.MainMenu, TutorialStep.Welcome },
            { GameState.HeroSelect, TutorialStep.HeroSelect },
            { GameState.DiceRoll, TutorialStep.DiceRoll },
            { GameState.Battle, TutorialStep.Battle },
            { GameState.Settlement, TutorialStep.Victory },
            { GameState.MapSelect, TutorialStep.MapSelect }
        };
    }

    /// <summary>
    /// 初始化所有引导步骤数据（硬编码中文内容）
    /// </summary>
    private void InitializeStepData()
    {
        stepDataList = new List<TutorialStepData>
        {
            new TutorialStepData
            {
                step = TutorialStep.Welcome,
                title = "欢迎来到骰子传说！",
                description = "这是一款骰子 + 卡牌 + 自走棋的肉鸽冒险游戏。\n\n" +
                              "你将投掷骰子获得资源，用卡牌强化英雄，\n" +
                              "在棋盘上布阵迎战敌人，一路闯关！",
                targetPanelName = "MainMenuPanel",
                highlightElement = "",
                confirmText = "开始冒险"
            },
            new TutorialStepData
            {
                step = TutorialStep.HeroSelect,
                title = "选择你的英雄",
                description = "从三位英雄中选择一位作为你的初始角色。\n\n" +
                              "每位英雄拥有独特的技能和属性，\n" +
                              "请根据自己的策略风格进行选择！",
                targetPanelName = "HeroSelectPanel",
                highlightElement = "HeroCards",
                confirmText = "明白了"
            },
            new TutorialStepData
            {
                step = TutorialStep.DiceRoll,
                title = "掷出你的骰子！",
                description = "每关开始前，你可以投掷骰子获取本关资源。\n\n" +
                              "你拥有一次免费重摇的机会，\n" +
                              "尽量把骰面摇出你想要的属性吧！",
                targetPanelName = "DiceRollPanel",
                highlightElement = "RollButton",
                confirmText = "试试手气"
            },
            new TutorialStepData
            {
                step = TutorialStep.CardPlay,
                title = "打出你的卡牌",
                description = "掷骰结束后，你可以从手牌中选择卡牌打出。\n\n" +
                              "每张卡牌消耗一定的骰子点数，\n" +
                              "合理分配资源，强化你的战斗力！",
                targetPanelName = "CardPlayPanel",
                highlightElement = "HandArea",
                confirmText = "了解"
            },
            new TutorialStepData
            {
                step = TutorialStep.Positioning,
                title = "布置你的阵型",
                description = "在棋盘上拖动英雄，安排他们的站位。\n\n" +
                              "前排英雄会先承受伤害，\n" +
                              "把肉盾放前排、输出放后排是不错的选择！",
                targetPanelName = "BattleGridPanel",
                highlightElement = "ChessBoard",
                confirmText = "布阵完毕"
            },
            new TutorialStepData
            {
                step = TutorialStep.Battle,
                title = "战斗开始！",
                description = "布阵结束后，战斗将自动进行。\n\n" +
                              "你的英雄会根据站位和技能自动攻击，\n" +
                              "你只需要坐下来欣赏精彩的战斗！",
                targetPanelName = "BattlePanel",
                highlightElement = "",
                confirmText = "观看战斗"
            },
            new TutorialStepData
            {
                step = TutorialStep.Victory,
                title = "战斗胜利！",
                description = "恭喜你赢得了这场战斗！\n\n" +
                              "结算时你将获得金币、经验和可能掉落的遗物。\n" +
                              "合理利用奖励来强化你的队伍吧！",
                targetPanelName = "SettlementPanel",
                highlightElement = "RewardArea",
                confirmText = "领取奖励"
            },
            new TutorialStepData
            {
                step = TutorialStep.MapSelect,
                title = "选择你的道路",
                description = "在地图上选择下一个要前往的节点。\n\n" +
                              "不同节点会带来不同的遭遇：\n" +
                              "战斗、商店、随机事件、遗物……\n" +
                              "谨慎选择你的路线！",
                targetPanelName = "RoguelikeMapPanel",
                highlightElement = "MapNodes",
                confirmText = "踏上旅途"
            }
        };
    }
}
