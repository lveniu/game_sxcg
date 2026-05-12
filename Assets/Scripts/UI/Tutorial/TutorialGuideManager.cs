using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using System;

/// <summary>
/// 教程引导管理器 — 编排引导步骤流程
/// 管理步骤队列：高亮目标 → 等待操作 → 检测完成 → 下一步
/// 引导数据可 JSON 配置化，状态持久化到 PlayerPrefs
/// </summary>
public class TutorialGuideManager : MonoBehaviour
{
    public static TutorialGuideManager Instance { get; private set; }

    #region 配置

    /// <summary>引导步骤数据（JSON配置或硬编码）</summary>
    private List<TutorialGuideStep> steps = new List<TutorialGuideStep>();

    /// <summary>当前步骤索引</summary>
    private int currentStepIndex = -1;

    /// <summary>高亮遮罩实例</summary>
    private TutorialHighlight highlight;

    /// <summary>超时计时器</summary>
    private float timeoutTimer;

    /// <summary>是否引导中</summary>
    private bool isGuideActive;

    /// <summary>是否启用引导（设置项）</summary>
    private bool guideEnabled = true;

    #endregion

    #region 常量

    private const string PREFS_GUIDE_DONE = "tutorial_guide_done";
    private const string PREFS_GUIDE_ENABLED = "tutorial_guide_enabled";
    private const string PREFS_GUIDE_STEP = "tutorial_guide_step";

    #endregion

    #region 事件

    /// <summary>引导步骤开始</summary>
    public event Action<TutorialGuideStep> OnStepStarted;

    /// <summary>引导步骤完成</summary>
    public event Action<TutorialGuideStep> OnStepCompleted;

    /// <summary>全部引导完成</summary>
    public event Action OnGuideCompleted;

    #endregion

    #region 属性

    /// <summary>当前步骤数据</summary>
    public TutorialGuideStep CurrentStep =>
        (currentStepIndex >= 0 && currentStepIndex < steps.Count) ? steps[currentStepIndex] : null;

    /// <summary>是否引导中</summary>
    public bool IsGuideActive => isGuideActive;

    /// <summary>当前步骤索引</summary>
    public int CurrentStepIndex => currentStepIndex;

    /// <summary>总步骤数</summary>
    public int TotalSteps => steps.Count;

    #endregion

    #region 生命周期

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 读取设置
        guideEnabled = PlayerPrefs.GetInt(PREFS_GUIDE_ENABLED, 1) == 1;

        // 初始化步骤
        InitializeSteps();
    }

    void Start()
    {
        // 创建高亮遮罩
        CreateHighlightOverlay();

        // 检查是否已完成引导
        bool done = PlayerPrefs.GetInt(PREFS_GUIDE_DONE, 0) == 1;
        if (!done && guideEnabled)
        {
            // 从上次的步骤继续
            int savedStep = PlayerPrefs.GetInt(PREFS_GUIDE_STEP, 0);
            currentStepIndex = savedStep - 1; // StartGuide 会 +1
            StartGuide();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // 订阅状态机
        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;
    }

    void Update()
    {
        if (!isGuideActive || currentStepIndex < 0) return;

        // 超时检测
        var step = CurrentStep;
        if (step != null && step.timeout > 0)
        {
            timeoutTimer += Time.deltaTime;
            if (timeoutTimer >= step.timeout)
            {
                Debug.Log($"[TutorialGuideManager] 步骤 {step.stepID} 超时自动完成");
                CompleteCurrentStep();
            }
        }
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 启动引导流程
    /// </summary>
    public void StartGuide()
    {
        if (steps.Count == 0) return;

        isGuideActive = true;

        // 订阅状态机事件
        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged += OnGameStateChanged;

        AdvanceToNextStep();
        Debug.Log("[TutorialGuideManager] 引导流程启动");
    }

    /// <summary>
    /// 完成当前步骤，推进到下一步
    /// </summary>
    public void CompleteCurrentStep()
    {
        if (!isGuideActive || currentStepIndex < 0) return;

        var step = CurrentStep;
        if (step == null) return;

        timeoutTimer = 0f;

        OnStepCompleted?.Invoke(step);
        Debug.Log($"[TutorialGuideManager] 步骤完成: {step.stepID}");

        // 隐藏高亮
        if (highlight != null) highlight.Hide();

        // 保存进度
        PlayerPrefs.SetInt(PREFS_GUIDE_STEP, currentStepIndex + 1);
        PlayerPrefs.Save();

        // 推进下一步
        AdvanceToNextStep();
    }

    /// <summary>
    /// 外部触发自定义事件（用于 waitForEvent="custom" 类型）
    /// </summary>
    public void TriggerCustomEvent(string eventID)
    {
        if (!isGuideActive || currentStepIndex < 0) return;

        var step = CurrentStep;
        if (step != null && step.waitForEvent == "custom" && step.stepID == eventID)
        {
            CompleteCurrentStep();
        }
    }

    /// <summary>
    /// 跳过整个引导
    /// </summary>
    public void SkipGuide()
    {
        if (highlight != null) highlight.Hide();
        FinishGuide();
        Debug.Log("[TutorialGuideManager] 引导已跳过");
    }

    /// <summary>
    /// 设置引导开关（设置界面调用）
    /// </summary>
    public void SetGuideEnabled(bool enabled)
    {
        guideEnabled = enabled;
        PlayerPrefs.SetInt(PREFS_GUIDE_ENABLED, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled && isGuideActive)
        {
            SkipGuide();
        }
        else if (enabled && !isGuideActive)
        {
            bool done = PlayerPrefs.GetInt(PREFS_GUIDE_DONE, 0) == 1;
            if (!done)
            {
                currentStepIndex = -1;
                StartGuide();
            }
        }
    }

    /// <summary>
    /// 重置引导（重新开始）
    /// </summary>
    public void ResetGuide()
    {
        PlayerPrefs.DeleteKey(PREFS_GUIDE_DONE);
        PlayerPrefs.DeleteKey(PREFS_GUIDE_STEP);
        PlayerPrefs.Save();

        currentStepIndex = -1;
        isGuideActive = false;

        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;

        Debug.Log("[TutorialGuideManager] 引导已重置");
    }

    /// <summary>
    /// 是否启用引导
    /// </summary>
    public bool IsGuideEnabled() => guideEnabled;

    #endregion

    #region 内部逻辑

    /// <summary>
    /// 推进到下一步
    /// </summary>
    private void AdvanceToNextStep()
    {
        currentStepIndex++;

        if (currentStepIndex >= steps.Count)
        {
            FinishGuide();
            return;
        }

        var step = CurrentStep;
        if (step == null)
        {
            FinishGuide();
            return;
        }

        timeoutTimer = 0f;
        OnStepStarted?.Invoke(step);
        Debug.Log($"[TutorialGuideManager] 步骤开始: {step.stepID} — {step.title}");

        // 查找高亮目标并显示
        var target = FindHighlightTarget(step.highlightPath);
        if (target != null)
        {
            highlight?.Show(target, step.highlightShape,
                step.title, step.description,
                step.showFinger, step.showBubble);
        }
        else
        {
            // 没有高亮目标，只显示气泡
            highlight?.Show(null, "rect",
                step.title, step.description,
                false, step.showBubble);
        }

        // 如果等待点击，在高亮目标上添加点击监听
        if (step.waitForEvent == "click" && target != null)
        {
            AddClickListener(target);
        }
    }

    /// <summary>
    /// 完成引导流程
    /// </summary>
    private void FinishGuide()
    {
        isGuideActive = false;
        currentStepIndex = steps.Count;

        PlayerPrefs.SetInt(PREFS_GUIDE_DONE, 1);
        PlayerPrefs.SetInt(PREFS_GUIDE_STEP, steps.Count);
        PlayerPrefs.Save();

        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;

        OnGuideCompleted?.Invoke();
        Debug.Log("[TutorialGuideManager] 引导流程全部完成！");
    }

    /// <summary>
    /// 查找高亮目标 RectTransform
    /// </summary>
    private RectTransform FindHighlightTarget(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // 查找方式1：通过 GameObject 名称查找
        var go = GameObject.Find(path);
        if (go != null) return go.GetComponent<RectTransform>();

        // 查找方式2：通过路径在所有面板中查找
        var panels = FindObjectsOfType<UIPanel>();
        foreach (var panel in panels)
        {
            var t = panel.transform.Find(path);
            if (t != null) return t as RectTransform;
        }

        Debug.LogWarning($"[TutorialGuideManager] 未找到高亮目标: {path}");
        return null;
    }

    /// <summary>
    /// 在高亮目标上添加点击监听（完成后自动移除）
    /// </summary>
    private void AddClickListener(RectTransform target)
    {
        var button = target.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnTargetClicked);
            return;
        }

        // 没有 Button 组件则添加 EventTrigger
        var trigger = target.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick
        };
        entry.callback.AddListener(_ => OnTargetClicked());
        trigger.triggers.Add(entry);
    }

    /// <summary>
    /// 高亮目标被点击回调
    /// </summary>
    private void OnTargetClicked()
    {
        var step = CurrentStep;
        if (step != null && step.waitForEvent == "click")
        {
            CompleteCurrentStep();
        }
    }

    /// <summary>
    /// 游戏状态变化回调 — 用于 state_change 类型等待
    /// </summary>
    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        if (!isGuideActive || currentStepIndex < 0) return;

        var step = CurrentStep;
        if (step != null && step.waitForEvent == "state_change")
        {
            if (newState.ToString() == step.triggerState)
            {
                CompleteCurrentStep();
            }
        }
    }

    #endregion

    #region 创建高亮遮罩

    private void CreateHighlightOverlay()
    {
        var go = new GameObject("[TutorialHighlight]");
        go.transform.SetParent(transform);
        highlight = go.AddComponent<TutorialHighlight>();
        highlight.OnSkipClicked += SkipGuide;
    }

    #endregion

    #region 步骤初始化

    /// <summary>
    /// 初始化新手前5步引导（配置化，后续可替换为 JSON 加载）
    /// </summary>
    private void InitializeSteps()
    {
        steps = new List<TutorialGuideStep>
        {
            // Step1: 选择英雄
            new TutorialGuideStep
            {
                stepID = "hero_select",
                title = GetLocText("tutorial.hero_select_title", "选择你的英雄"),
                description = GetLocText("tutorial.hero_select_desc", "从三位英雄中选择一位作为你的初始角色。\n每位英雄拥有独特的技能和属性！"),
                highlightPath = "HeroCards",
                highlightShape = "rect",
                waitForEvent = "click",
                timeout = 30f,
                showFinger = true,
                showBubble = true
            },
            // Step2: 掷骰子
            new TutorialGuideStep
            {
                stepID = "dice_roll",
                title = GetLocText("tutorial.dice_roll_title", "掷出命运之骰"),
                description = GetLocText("tutorial.dice_roll_desc", "点击掷骰按钮，投掷骰子获取本关资源。\n骰面结果决定你的战斗加成！"),
                highlightPath = "RollButton",
                highlightShape = "rect",
                waitForEvent = "click",
                timeout = 30f,
                showFinger = true,
                showBubble = true
            },
            // Step3: 锁定骰子
            new TutorialGuideStep
            {
                stepID = "dice_lock",
                title = GetLocText("tutorial.dice_lock_title", "锁定有利骰子"),
                description = GetLocText("tutorial.dice_lock_desc", "点击骰子可以锁定它，锁定的骰子不会被重摇。\n保留好结果，重摇不满意的面！"),
                highlightPath = "DiceContainer",
                highlightShape = "rect",
                waitForEvent = "click",
                timeout = 20f,
                showFinger = true,
                showBubble = true
            },
            // Step4: 确认组合
            new TutorialGuideStep
            {
                stepID = "dice_confirm",
                title = GetLocText("tutorial.dice_confirm_title", "确认骰子组合"),
                description = GetLocText("tutorial.dice_confirm_desc", "确认你的骰子结果，进入战斗阶段。\n三条、顺子、对子各有不同加成！"),
                highlightPath = "ConfirmButton",
                highlightShape = "rect",
                waitForEvent = "click",
                timeout = 20f,
                showFinger = true,
                showBubble = true
            },
            // Step5: 战斗
            new TutorialGuideStep
            {
                stepID = "battle",
                title = GetLocText("tutorial.battle_start_title", "战斗开始！"),
                description = GetLocText("tutorial.battle_start_desc", "战斗自动进行，你可以加速或跳过。\n胜利后获得金币和经验奖励！"),
                highlightPath = "SpeedButton",
                highlightShape = "rect",
                waitForEvent = "state_change",
                triggerState = "Settlement",
                timeout = 0f,
                showFinger = true,
                showBubble = true
            }
        };
    }

    /// <summary>
    /// 本地化辅助方法 — 优先从语言包读取，回退到fallback
    /// </summary>
    private static string GetLocText(string key, string fallback)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.GetText(key);
        return fallback;
    }

    #endregion
}
