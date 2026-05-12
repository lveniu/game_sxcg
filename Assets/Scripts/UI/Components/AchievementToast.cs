// FE-20: 成就解锁Toast通知 — 全局组件，订阅 AchievementManager.OnAchievementUnlocked
// 从屏幕顶部滑入，金色闪光边框，停留2.5s，自动滑出，多成就队列间隔1s
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class AchievementToast : MonoBehaviour
{
    // ========== 单例 ==========
    public static AchievementToast Instance { get; private set; }

    // ========== 配置 ==========
    const float SLIDE_IN_DURATION = 0.5f;
    const float DISPLAY_DURATION = 2.5f;
    const float SLIDE_OUT_DURATION = 0.3f;
    const float QUEUE_INTERVAL = 1f;
    const float TOAST_HEIGHT = 80f;

    static readonly Color GOLD_BORDER = new(1f, 0.84f, 0f);
    static readonly Color BG_COLOR = new(.1f, .1f, .15f, .95f);

    // 稀有度颜色
    static readonly Dictionary<string, Color> RARITY_COLORS = new()
    {
        ["common"] = new(.75f, .75f, .75f),
        ["rare"] = new(.29f, .62f, 1f),
        ["epic"] = new(.66f, .33f, .97f),
        ["legendary"] = new(1f, .72f, 0f)
    };

    static Font DefFont => Resources.GetBuiltinResource<Font>("Arial.ttf");

    // ========== 状态 ==========
    readonly Queue<string> _pendingQueue = new();
    bool _isDisplaying;
    RectTransform _canvasRt;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 确保有 Canvas
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最顶层

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);

        _canvasRt = GetComponent<RectTransform>();
    }

    void Start()
    {
        var am = AchievementManager.Instance;
        if (am != null)
        {
            am.OnAchievementUnlocked += OnAchievementUnlocked;
        }
    }

    void OnDestroy()
    {
        var am = AchievementManager.Instance;
        if (am != null)
        {
            am.OnAchievementUnlocked -= OnAchievementUnlocked;
        }
        DOTween.Kill(this);
    }

    // ========== 事件回调 ==========
    void OnAchievementUnlocked(string achievementId)
    {
        _pendingQueue.Enqueue(achievementId);
        if (!_isDisplaying)
            ShowNext();
    }

    // ========== 显示逻辑 ==========
    void ShowNext()
    {
        if (_pendingQueue.Count == 0)
        {
            _isDisplaying = false;
            return;
        }

        _isDisplaying = true;
        var achievementId = _pendingQueue.Dequeue();
        var am = AchievementManager.Instance;
        if (am == null) { _isDisplaying = false; return; }

        var def = am.GetDef(achievementId);
        if (def == null) { ShowNext(); return; }

        var toast = BuildToastUI(def);
        var rt = toast.Rect();

        // 从屏幕顶部外侧滑入
        rt.anchoredPosition = new Vector2(0, TOAST_HEIGHT);
        rt.DOAnchorPos(Vector2.zero, SLIDE_IN_DURATION)
            .SetEase(Ease.OutBack)
            .SetTarget(toast)
            .OnComplete(() =>
            {
                // 停留后滑出
                rt.DOAnchorPos(new Vector2(0, TOAST_HEIGHT), SLIDE_OUT_DURATION)
                    .SetEase(Ease.InBack)
                    .SetTarget(toast)
                    .SetDelay(DISPLAY_DURATION)
                    .OnComplete(() =>
                    {
                        Destroy(toast);
                        DOVirtual.DelayedCall(QUEUE_INTERVAL, ShowNext);
                    });
            });
    }

    // ========== UI构建 ==========
    GameObject BuildToastUI(AchievementDef def)
    {
        var go = new GameObject("AchievementToast");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new(.1f, 1f);
        rt.anchorMax = new(.9f, 1f);
        rt.pivot = new(.5f, 1f);
        rt.sizeDelta = new(0, TOAST_HEIGHT);
        rt.anchoredPosition = Vector2.zero;

        // 背景
        var bg = go.AddComponent<Image>();
        bg.color = BG_COLOR;
        bg.raycastTarget = false;

        // 金色边框
        var outline = go.AddComponent<Outline>();
        outline.effectColor = GOLD_BORDER;
        outline.effectDistance = new(3, -3);

        // 金色脉冲边框动画
        var seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c,
            new Color(1f, .7f, 0f), .4f));
        seq.Append(DOTween.To(() => outline.effectColor, c => outline.effectColor = c,
            GOLD_BORDER, .4f));
        seq.SetLoops(-1, LoopType.Yoyo).SetTarget(go);

        // 内容区域
        // 左侧图标
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = new(.02f, .15f);
        irt.anchorMax = new(.12f, .85f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.color = new(1f, .84f, 0f, .3f);
        iconImg.raycastTarget = false;
        var iconTxt = iconGo.AddComponent<Text>();
        iconTxt.text = "🏆";
        iconTxt.font = DefFont;
        iconTxt.fontSize = 28;
        iconTxt.color = Color.white;
        iconTxt.alignment = TextAnchor.MiddleCenter;

        // 标题
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(go.transform, false);
        var trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new(.15f, .55f);
        trt.anchorMax = new(.85f, .9f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var titleTxt = titleGo.AddComponent<Text>();
        titleTxt.text = $"🏆 成就解锁: {def.name_cn}";
        titleTxt.font = DefFont;
        titleTxt.fontSize = 16;
        titleTxt.color = new(1f, .84f, 0f);
        titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.fontStyle = FontStyle.Bold;

        // 描述
        var descGo = new GameObject("Desc");
        descGo.transform.SetParent(go.transform, false);
        var drt = descGo.AddComponent<RectTransform>();
        drt.anchorMin = new(.15f, .1f);
        drt.anchorMax = new(.85f, .5f);
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        var descTxt = descGo.AddComponent<Text>();
        descTxt.text = def.description ?? "";
        descTxt.font = DefFont;
        descTxt.fontSize = 13;
        descTxt.color = new(.85f, .85f, .9f);
        descTxt.alignment = TextAnchor.MiddleLeft;

        return go;
    }
}
