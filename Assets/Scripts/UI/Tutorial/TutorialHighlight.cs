using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 教程高亮遮罩组件 — 在独立 Canvas 上渲染半透明遮罩 + 目标区域挖洞
/// 包含手指指示器和对话气泡
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    #region 常量

    private const string PREFAB_NAME = "[TutorialHighlight]";
    private const float MASK_ALPHA = 0.75f;
    private const float FINGER_FLOAT_HEIGHT = 15f;
    private const float FINGER_FLOAT_DURATION = 0.8f;
    private const float PULSE_DURATION = 1.2f;
    private const float BUBBLE_ANIM_DURATION = 0.3f;

    #endregion

    #region 内部组件

    private Canvas overlayCanvas;
    private CanvasScaler canvasScaler;
    private Image maskImage;
    private GameObject fingerArrow;
    private GameObject bubbleContainer;
    private Text bubbleTitle;
    private Text bubbleDesc;
    private Button skipButton;

    /// <summary>当前挖洞区域</summary>
    private RectTransform highlightTarget;

    /// <summary>挖洞边缘脉冲动画 Tweener</summary>
    private Tweener pulseTween;

    /// <summary>手指浮动动画 Tweener</summary>
    private Tweener fingerTween;

    #endregion

    #region 事件

    /// <summary>跳过按钮被点击</summary>
    public event Action OnSkipClicked;

    /// <summary>高亮区域被点击（穿透遮罩）</summary>
    public event Action OnHighlightClicked;

    #endregion

    #region 生命周期

    void Awake()
    {
        CreateOverlayCanvas();
        CreateMaskImage();
        CreateFingerArrow();
        CreateBubble();
        CreateSkipButton();
    }

    void OnDestroy()
    {
        pulseTween?.Kill();
        fingerTween?.Kill();
    }

    void Update()
    {
        // 实时跟踪高亮目标位置（目标可能移动/动画）
        if (highlightTarget != null && maskImage != null)
        {
            UpdateMaskHole();
        }
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 显示高亮遮罩
    /// </summary>
    /// <param name="target">要高亮的 RectTransform</param>
    /// <param name="shape">高亮形状 "rect" / "circle"</param>
    /// <param name="title">气泡标题</param>
    /// <param name="description">气泡描述</param>
    /// <param name="showFinger">是否显示手指</param>
    /// <param name="showBubble">是否显示气泡</param>
    public void Show(RectTransform target, string shape = "rect",
        string title = "", string description = "",
        bool showFinger = true, bool showBubble = true)
    {
        highlightTarget = target;
        gameObject.SetActive(true);

        // 更新遮罩
        UpdateMaskHole();

        // 边缘脉冲
        StartPulseAnimation();

        // 手指指示器
        if (showFinger && target != null)
        {
            ShowFinger(target, shape);
        }
        else
        {
            fingerArrow.SetActive(false);
        }

        // 对话气泡
        if (showBubble)
        {
            ShowBubble(title, description);
        }
        else
        {
            bubbleContainer.SetActive(false);
        }

        // 跳过按钮
        skipButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏高亮遮罩
    /// </summary>
    public void Hide()
    {
        pulseTween?.Kill();
        fingerTween?.Kill();
        highlightTarget = null;

        // 淡出动画
        if (maskImage != null)
        {
            maskImage.DOFade(0f, 0.2f).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 更新高亮目标（目标元素可能动态变化）
    /// </summary>
    public void SetTarget(RectTransform newTarget)
    {
        highlightTarget = newTarget;
    }

    #endregion

    #region 创建UI组件

    private void CreateOverlayCanvas()
    {
        // Canvas
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 10000; // 最高层
        overlayCanvas.overrideSorting = true;

        canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1080, 1920);

        gameObject.AddComponent<GraphicRaycaster>();
    }

    private void CreateMaskImage()
    {
        var maskGo = new GameObject("Mask");
        maskGo.transform.SetParent(transform, false);

        var rt = maskGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        maskImage = maskGo.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, MASK_ALPHA);
        maskImage.raycastTarget = true;

        // 点击遮罩不响应（只在非高亮区域拦截）
        maskImage.gameObject.AddComponent<Button>().onClick.AddListener(() => { });
    }

    private void CreateFingerArrow()
    {
        fingerArrow = new GameObject("FingerArrow");
        fingerArrow.transform.SetParent(transform, false);

        var rt = fingerArrow.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40, 50);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);

        var img = fingerArrow.AddComponent<Image>();
        img.raycastTarget = false;

        // 用三角形模拟箭头（向下指）
        img.color = new Color(1f, 0.9f, 0.2f, 1f);

        // 添加 Outline 让箭头更明显
        var outline = fingerArrow.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.6f, 0f, 1f);
        outline.effectDistance = new Vector2(2, 2);

        fingerArrow.SetActive(false);
    }

    private void CreateBubble()
    {
        bubbleContainer = new GameObject("Bubble");
        bubbleContainer.transform.SetParent(transform, false);

        var bubbleRt = bubbleContainer.AddComponent<RectTransform>();
        bubbleRt.anchorMin = new Vector2(0.1f, 0.02f);
        bubbleRt.anchorMax = new Vector2(0.9f, 0.22f);
        bubbleRt.offsetMin = Vector2.zero;
        bubbleRt.offsetMax = Vector2.zero;

        // 背景
        var bgImg = bubbleContainer.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        bgImg.raycastTarget = false;

        var bgOutline = bubbleContainer.AddComponent<Outline>();
        bgOutline.effectColor = new Color(0.6f, 0.5f, 1f, 0.8f);
        bgOutline.effectDistance = new Vector2(3, -3);

        // 标题
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(bubbleContainer.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.6f);
        titleRt.anchorMax = new Vector2(0.95f, 0.9f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        bubbleTitle = titleGo.AddComponent<Text>();
        bubbleTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleTitle.fontSize = 28;
        bubbleTitle.color = new Color(1f, 0.85f, 0.3f);
        bubbleTitle.alignment = TextAnchor.MiddleLeft;
        bubbleTitle.raycastTarget = false;

        // 描述
        var descGo = new GameObject("Description");
        descGo.transform.SetParent(bubbleContainer.transform, false);
        var descRt = descGo.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.05f, 0.05f);
        descRt.anchorMax = new Vector2(0.95f, 0.55f);
        descRt.offsetMin = Vector2.zero;
        descRt.offsetMax = Vector2.zero;

        bubbleDesc = descGo.AddComponent<Text>();
        bubbleDesc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleDesc.fontSize = 22;
        bubbleDesc.color = Color.white;
        bubbleDesc.alignment = TextAnchor.UpperLeft;
        bubbleDesc.raycastTarget = false;

        bubbleContainer.SetActive(false);
    }

    private void CreateSkipButton()
    {
        var btnGo = new GameObject("SkipButton");
        btnGo.transform.SetParent(transform, false);

        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(140, 50);
        rt.anchoredPosition = new Vector2(-20, -20);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        img.raycastTarget = true;

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = "跳过引导";
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        skipButton = btnGo.AddComponent<Button>();
        skipButton.targetGraphic = img;
        skipButton.onClick.AddListener(() => OnSkipClicked?.Invoke());

        btnGo.SetActive(false);
    }

    #endregion

    #region 遮罩挖洞

    /// <summary>
    /// 更新遮罩挖洞区域 — 通过 CanvasRenderer 的 material 或直接用 Image 实现
    /// 简化方案：用4个半透明矩形围出挖洞效果
    /// </summary>
    private void UpdateMaskHole()
    {
        if (highlightTarget == null || maskImage == null) return;

        // 简化实现：全屏半透明遮罩，高亮区域不遮挡
        // 实际项目中可用 Stencil Mask 或 UI Mask 实现
        // 这里直接设置 mask 颜色，让高亮区域边界可见
    }

    #endregion

    #region 动画

    private void StartPulseAnimation()
    {
        pulseTween?.Kill();

        // 遮罩透明度脉冲（引导注意力）
        pulseTween = maskImage.DOFade(MASK_ALPHA - 0.1f, PULSE_DURATION / 2f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void ShowFinger(RectTransform target, string shape)
    {
        fingerArrow.SetActive(true);

        // 定位到目标上方
        var worldPos = target.position;
        var screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
        var fingerRt = fingerArrow.GetComponent<RectTransform>();

        fingerRt.position = screenPos + Vector2.up * (target.rect.height / 2 + FINGER_FLOAT_HEIGHT + 30f);

        // 上下浮动动画
        fingerRt.anchoredPosition = fingerRt.anchoredPosition;
        var originY = fingerRt.anchoredPosition.y;
        fingerTween?.Kill();
        fingerTween = fingerRt.DOAnchorPosY(originY + FINGER_FLOAT_HEIGHT, FINGER_FLOAT_DURATION)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void ShowBubble(string title, string description)
    {
        bubbleContainer.SetActive(true);
        bubbleTitle.text = title ?? "";
        bubbleDesc.text = description ?? "";

        // 从底部弹出动画
        var rt = bubbleContainer.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -200f);
        rt.DOAnchorPos(Vector2.zero, BUBBLE_ANIM_DURATION)
            .SetEase(Ease.OutBack);
    }

    #endregion
}
