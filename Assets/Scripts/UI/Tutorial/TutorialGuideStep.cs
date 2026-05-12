using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 教程步骤定义 — 描述一个引导步骤的完整配置
/// 支持 JSON 序列化，可通过外部配置扩展
/// </summary>
[Serializable]
public class TutorialGuideStep
{
    /// <summary>步骤唯一标识</summary>
    public string stepID;

    /// <summary>步骤标题</summary>
    public string title;

    /// <summary>步骤描述（引导文字）</summary>
    public string description;

    /// <summary>要高亮的UI元素路径（从面板根节点开始的 Transform 路径）</summary>
    public string highlightPath;

    /// <summary>高亮形状: "rect" 或 "circle"</summary>
    public string highlightShape = "rect";

    /// <summary>等待的事件类型: "click" / "state_change" / "custom"</summary>
    public string waitForEvent = "click";

    /// <summary>超时自动完成时间（秒），0 = 不超时</summary>
    public float timeout = 0f;

    /// <summary>是否显示手指指示器</summary>
    public bool showFinger = true;

    /// <summary>是否显示对话气泡</summary>
    public bool showBubble = true;

    /// <summary>关联的游戏状态（用于 state_change 类型等待）</summary>
    public string triggerState = "";
}
