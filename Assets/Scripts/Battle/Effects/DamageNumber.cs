using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// FE-33: 伤害数字组件 — 挂载到每个飘字 GameObject 上
/// 支持类型化显示：暴击(放大红色)、治疗(绿色)、护盾(蓝色)、普通伤害(白色)
/// 通过 DamageNumberPool 对象池管理，避免运行时 Instantiate/Destroy
/// </summary>
public enum DamageNumberType
{
    Normal,   // 普通伤害 — 白色
    Critical, // 暴击 — 放大红色 + 震动
    Heal,     // 治疗 — 绿色
    Shield,   // 护盾 — 蓝色
    Miss,     // 闪避 — 灰色
    Poison    // 中毒 — 紫色
}

public class DamageNumber : MonoBehaviour
{
    // ── 组件引用 ──
    private Text textComponent;
    private RectTransform rectTransform;
    private Canvas canvas;

    // ── 动画参数 ──
    private float _duration = 1f;
    private float _moveSpeed = 80f;
    private float _spreadRange = 25f;
    private Vector2 _startPos;
    private Vector2 _randomOffset;
    private Color _startColor;
    private float _scale;

    // ── 池化标记 ──
    private bool _isAnimating;
    private System.Action<DamageNumber> _onComplete;

    /// <summary>当前数字类型（用于回池分类）</summary>
    public DamageNumberType NumberType { get; private set; }

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        textComponent = GetComponent<Text>();
        canvas = GetComponent<Canvas>();

        if (textComponent == null)
        {
            textComponent = gameObject.AddComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.raycastTarget = false;
        }
    }

    /// <summary>
    /// 初始化飘字内容并开始动画
    /// </summary>
    /// <param name="worldPos">世界坐标位置</param>
    /// <param name="text">显示文本</param>
    /// <param name="type">伤害类型</param>
    /// <param name="onComplete">动画完成回调（回池）</param>
    public void Setup(Vector3 worldPos, string text, DamageNumberType type, System.Action<DamageNumber> onComplete = null)
    {
        NumberType = type;
        _onComplete = onComplete;
        _isAnimating = true;

        // 设置类型化样式
        ApplyTypeStyle(type);

        // 设置文本
        if (textComponent != null)
            textComponent.text = text;

        // 设置位置（世界坐标 → 屏幕坐标）
        Vector2 screenPos = Camera.main != null
            ? Camera.main.WorldToScreenPoint(worldPos)
            : Vector2.zero;
        rectTransform.position = screenPos;
        _startPos = rectTransform.anchoredPosition;
        _randomOffset = new Vector2(Random.Range(-_spreadRange, _spreadRange), 0);

        // 应用缩放
        rectTransform.localScale = Vector3.one * _scale;

        // 启动 DOTween 动画序列
        PlayAnimation();
    }

    /// <summary>
    /// 根据类型设置颜色、字号、缩放
    /// </summary>
    private void ApplyTypeStyle(DamageNumberType type)
    {
        switch (type)
        {
            case DamageNumberType.Critical:
                // FE-33: 暴击放大红色
                _startColor = new Color(1f, 0.15f, 0.1f, 1f); // 亮红色
                _scale = 1.6f; // 放大
                _duration = 1.2f;
                _moveSpeed = 100f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 36;
                    textComponent.fontStyle = FontStyle.Bold;
                }
                break;

            case DamageNumberType.Heal:
                // FE-33: 治疗绿色
                _startColor = new Color(0.1f, 1f, 0.35f, 1f);
                _scale = 1.1f;
                _duration = 1f;
                _moveSpeed = 60f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 24;
                    textComponent.fontStyle = FontStyle.Normal;
                }
                break;

            case DamageNumberType.Shield:
                // FE-33: 护盾蓝色
                _startColor = new Color(0.2f, 0.55f, 1f, 1f);
                _scale = 1.1f;
                _duration = 1f;
                _moveSpeed = 50f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 24;
                    textComponent.fontStyle = FontStyle.Normal;
                }
                break;

            case DamageNumberType.Miss:
                _startColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                _scale = 0.9f;
                _duration = 0.8f;
                _moveSpeed = 40f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 20;
                    textComponent.fontStyle = FontStyle.Italic;
                }
                break;

            case DamageNumberType.Poison:
                _startColor = new Color(0.7f, 0.2f, 1f, 1f);
                _scale = 1f;
                _duration = 1f;
                _moveSpeed = 50f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 22;
                    textComponent.fontStyle = FontStyle.Normal;
                }
                break;

            default: // Normal
                _startColor = Color.white;
                _scale = 1f;
                _duration = 1f;
                _moveSpeed = 70f;
                if (textComponent != null)
                {
                    textComponent.fontSize = 24;
                    textComponent.fontStyle = FontStyle.Normal;
                }
                break;
        }

        if (textComponent != null)
            textComponent.color = _startColor;
    }

    /// <summary>
    /// DOTween 动画序列：上飘 + 渐隐 + 暴击特殊效果
    /// </summary>
    private void PlayAnimation()
    {
        // 先 Kill 所有已有的 Tween
        rectTransform.DOKill();
        if (textComponent != null)
            textComponent.DOKill();

        var seq = DOTween.Sequence();
        seq.SetTarget(gameObject);

        // 上飘动画
        float endY = _startPos.y + _moveSpeed;
        seq.Append(
            rectTransform.DOAnchorPosY(endY, _duration)
                .SetEase(Ease.OutQuad)
        );

        // 水平随机偏移
        float targetX = _startPos.x + _randomOffset.x;
        seq.Join(
            rectTransform.DOAnchorPosX(targetX, _duration * 0.6f)
                .SetEase(Ease.OutQuad)
        );

        // 渐隐
        if (textComponent != null)
        {
            seq.Join(
                textComponent.DOFade(0f, _duration * 0.6f)
                    .SetDelay(_duration * 0.4f)
                    .SetEase(Ease.InQuad)
            );
        }

        // 暴击特殊效果：弹跳缩放
        if (NumberType == DamageNumberType.Critical)
        {
            seq.Insert(0f, rectTransform.DOScale(_scale * 1.3f, 0.1f).SetEase(Ease.OutQuad));
            seq.Insert(0.1f, rectTransform.DOScale(_scale, 0.15f).SetEase(Ease.InOutQuad));
            // 暴击微震
            seq.Insert(0f, rectTransform.DOShakeAnchorPos(0.2f, 8f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic));
        }

        // 完成回调
        seq.OnComplete(() =>
        {
            _isAnimating = false;
            _onComplete?.Invoke(this);
        });
    }

    /// <summary>
    /// 停止动画（池回收前调用）
    /// </summary>
    public void StopAnimation()
    {
        if (!_isAnimating) return;
        _isAnimating = false;

        rectTransform.DOKill();
        if (textComponent != null)
            textComponent.DOKill();

        _onComplete = null;
    }

    /// <summary>
    /// 重置到初始状态（池回收时调用）
    /// </summary>
    public void ResetState()
    {
        StopAnimation();

        if (textComponent != null)
        {
            textComponent.text = "";
            textComponent.color = Color.white;
            textComponent.fontSize = 24;
            textComponent.fontStyle = FontStyle.Normal;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
        }

        NumberType = DamageNumberType.Normal;
        _scale = 1f;
        _onComplete = null;
    }
}

/// <summary>
/// FE-33: DamageNumber 对象池管理器
/// 内嵌于 DamageNumber.cs，统一管理所有飘字的创建/回收
/// 避免战斗中频繁 Instantiate/Destroy 导致 GC Spike
/// </summary>
public class DamageNumberPool
{
    private const int INITIAL_POOL_SIZE = 15;
    private const int MAX_POOL_SIZE = 50;

    private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
    private readonly List<DamageNumber> _active = new List<DamageNumber>();
    private readonly Transform _container;
    private readonly Canvas _parentCanvas;

    /// <summary>当前活跃的飘字数量</summary>
    public int ActiveCount => _active.Count;

    /// <summary>池中可用的飘字数量</summary>
    public int AvailableCount => _pool.Count;

    public DamageNumberPool(Canvas parentCanvas)
    {
        _parentCanvas = parentCanvas;

        // 创建容器
        var containerObj = new GameObject("[DamageNumberPool]");
        containerObj.transform.SetParent(parentCanvas.transform, false);
        _container = containerObj.transform;

        // 预创建对象
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            var dn = CreateNewNumber();
            dn.gameObject.SetActive(false);
            _pool.Enqueue(dn);
        }
    }

    /// <summary>
    /// 显示一个飘字（从池中获取或创建新对象）
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <param name="value">数值（自动添加前缀）</param>
    /// <param name="type">伤害类型</param>
    public void Show(Vector3 worldPos, int value, DamageNumberType type)
    {
        string text = FormatText(value, type);
        Show(worldPos, text, type);
    }

    /// <summary>
    /// 显示一个自定义文本飘字
    /// </summary>
    public void Show(Vector3 worldPos, string text, DamageNumberType type)
    {
        var number = GetFromPool();
        if (number == null) return;

        number.gameObject.SetActive(true);
        number.Setup(worldPos, text, type, ReturnToPool);
        _active.Add(number);
    }

    /// <summary>
    /// 每帧清理已完成动画的飘字（可选，也可完全依赖回调）
    /// </summary>
    public void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] == null || !_active[i].gameObject.activeSelf)
            {
                if (_active[i] != null)
                    ForceReturn(_active[i]);
                _active.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 立即回收所有活跃飘字
    /// </summary>
    public void ClearAll()
    {
        foreach (var number in _active)
        {
            if (number != null)
                ForceReturn(number);
        }
        _active.Clear();
    }

    // ── 内部方法 ──

    private DamageNumber GetFromPool()
    {
        DamageNumber number;

        if (_pool.Count > 0)
        {
            number = _pool.Dequeue();
            if (number != null && number.gameObject != null)
                return number;
        }

        // 池空，创建新的（如果未达到上限）
        if (_active.Count + _pool.Count < MAX_POOL_SIZE)
            return CreateNewNumber();

        // 贪婪回收：强制回收最早的活跃对象
        if (_active.Count > 0)
        {
            var oldest = _active[0];
            _active.RemoveAt(0);
            if (oldest != null)
            {
                oldest.StopAnimation();
                oldest.ResetState();
                return oldest;
            }
        }

        Debug.LogWarning("[DamageNumberPool] 池已满且无可用对象");
        return null;
    }

    private void ReturnToPool(DamageNumber number)
    {
        if (number == null) return;

        _active.Remove(number);
        ForceReturn(number);
    }

    private void ForceReturn(DamageNumber number)
    {
        if (number == null) return;

        number.StopAnimation();
        number.ResetState();
        number.gameObject.SetActive(false);

        if (_pool.Count < MAX_POOL_SIZE)
            _pool.Enqueue(number);
        else
            Object.Destroy(number.gameObject);
    }

    private DamageNumber CreateNewNumber()
    {
        var go = new GameObject("DamageNumber");
        go.transform.SetParent(_container, false);

        // 必要组件
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150, 50);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100; // 在其他UI之上

        go.AddComponent<CanvasRenderer>();

        var dn = go.AddComponent<DamageNumber>();
        return dn;
    }

    /// <summary>
    /// 根据类型格式化文本
    /// </summary>
    private static string FormatText(int value, DamageNumberType type)
    {
        return type switch
        {
            DamageNumberType.Critical => $"{value}!",
            DamageNumberType.Heal => $"+{value}",
            DamageNumberType.Shield => $"🛡{value}",
            DamageNumberType.Miss => "MISS",
            DamageNumberType.Poison => $"{value}",
            _ => value.ToString()
        };
    }
}
