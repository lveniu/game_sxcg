using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 战斗特效管理器 — 管理战斗中的视觉特效系统
/// 使用对象池管理特效预制体，战斗中不允许 Instantiate/Destroy
/// 所有特效基于 Canvas(WorldSpace) + Image + DOTween 实现
/// </summary>
public class BattleEffectManager : MonoBehaviour
{
    public static BattleEffectManager Instance { get; private set; }

    #region 配置常量

    /// <summary>每种特效的初始池容量</summary>
    private const int POOL_INITIAL_CAPACITY = 5;

    /// <summary>特效类型列表</summary>
    private static readonly string[] EffectTypes = new[]
    {
        BattleEffectFactory.EFFECT_HIT,
        BattleEffectFactory.EFFECT_HEAL,
        BattleEffectFactory.EFFECT_SHIELD,
        BattleEffectFactory.EFFECT_CRIT,
        BattleEffectFactory.EFFECT_DEATH,
        BattleEffectFactory.EFFECT_LEVELUP
    };

    #endregion

    #region 内部状态

    /// <summary>对象池：每种特效类型的可用队列</summary>
    private Dictionary<string, Queue<GameObject>> effectPool = new Dictionary<string, Queue<GameObject>>();

    /// <summary>特效工厂（可替换的实现）</summary>
    private IBattleEffectProvider effectProvider;

    /// <summary>全屏闪烁覆盖层 Canvas</summary>
    private GameObject screenFlashCanvas;
    private Image screenFlashImage;

    /// <summary>特效容器（统一管理特效的父节点）</summary>
    private Transform effectsContainer;

    /// <summary>是否已初始化</summary>
    private bool isInitialized;

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
    }

    /// <summary>
    /// 初始化特效系统（在战斗开始前调用）
    /// 预创建对象池 + 初始化 ScreenFlash Canvas
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        // 创建特效容器
        var containerGo = new GameObject("[BattleEffects]");
        containerGo.transform.SetParent(transform);
        effectsContainer = containerGo.transform;

        // 初始化工厂
        effectProvider = new BattleEffectFactory();

        // 预创建每种特效的对象池
        foreach (var effectType in EffectTypes)
        {
            effectPool[effectType] = new Queue<GameObject>();
            for (int i = 0; i < POOL_INITIAL_CAPACITY; i++)
            {
                var go = CreatePooledObject(effectType);
                effectPool[effectType].Enqueue(go);
            }
        }

        // 创建全屏闪烁覆盖层
        CreateScreenFlashOverlay();

        isInitialized = true;
        Debug.Log("[BattleEffectManager] 特效系统初始化完成，对象池已预创建");
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // 清理所有特效
        ClearAllEffects();
    }

    #endregion

    #region 静态快捷方法

    /// <summary>
    /// 播放受击特效（红色缩放圆圈）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayHit(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_HIT);
    }

    /// <summary>
    /// 播放治疗特效（绿色上飘"+"号）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayHeal(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_HEAL);
    }

    /// <summary>
    /// 播放护盾特效（蓝色六边形轮廓）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayShield(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_SHIELD);
    }

    /// <summary>
    /// 播放暴击特效（金色爆炸 + 震屏）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayCrit(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_CRIT);
    }

    /// <summary>
    /// 播放死亡特效（灰色碎裂）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayDeath(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_DEATH);
    }

    /// <summary>
    /// 播放升级特效（金色光柱）
    /// </summary>
    /// <param name="targetPos">目标位置（世界坐标）</param>
    public static void PlayLevelUp(Vector3 targetPos)
    {
        Instance?.PlayEffectInternal(targetPos, BattleEffectFactory.EFFECT_LEVELUP);
    }

    #endregion

    #region 特效播放核心

    /// <summary>
    /// 内部特效播放方法：从对象池取出或创建新对象
    /// </summary>
    /// <param name="pos">世界坐标位置</param>
    /// <param name="effectType">特效类型</param>
    private void PlayEffectInternal(Vector3 pos, string effectType)
    {
        if (!isInitialized) Initialize();

        // 通过工厂播放特效（工厂直接创建，播放完自动回收）
        if (effectProvider != null)
            effectProvider.PlayEffect(pos, effectType);
    }

    #endregion

    #region 对象池管理

    /// <summary>
    /// 从对象池获取一个特效对象
    /// </summary>
    /// <param name="effectType">特效类型</param>
    /// <returns>可用的 GameObject，如果池为空则创建新的</returns>
    public GameObject GetFromPool(string effectType)
    {
        if (!isInitialized) Initialize();

        if (effectPool.TryGetValue(effectType, out var queue) && queue.Count > 0)
        {
            var go = queue.Dequeue();
            if (go != null)
            {
                go.SetActive(true);
                return go;
            }
        }

        // 池中无可用对象，创建新的
        return CreatePooledObject(effectType);
    }

    /// <summary>
    /// 回收特效对象到对象池
    /// </summary>
    /// <param name="effectType">特效类型</param>
    /// <param name="go">要回收的 GameObject</param>
    public void ReturnToPool(string effectType, GameObject go)
    {
        if (go == null) return;

        // 重置并隐藏
        go.SetActive(false);

        // 清理所有 DOTween 动画
        DOTween.Kill(go.transform);

        // 重置 Transform
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localPosition = Vector3.zero;

        // 放回池中
        if (!effectPool.ContainsKey(effectType))
            effectPool[effectType] = new Queue<GameObject>();

        effectPool[effectType].Enqueue(go);
    }

    /// <summary>
    /// 预创建一个池化特效对象
    /// </summary>
    /// <param name="effectType">特效类型</param>
    /// <returns>创建的 GameObject</returns>
    private GameObject CreatePooledObject(string effectType)
    {
        var go = new GameObject($"PooledEffect_{effectType}");
        go.transform.SetParent(effectsContainer);
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// 清理所有特效和对象池
    /// </summary>
    public void ClearAllEffects()
    {
        // 清理所有池化对象
        foreach (var kvp in effectPool)
        {
            if (kvp.Value != null)
            {
                foreach (var go in kvp.Value)
                {
                    if (go != null)
                        Destroy(go);
                }
            }
        }
        effectPool.Clear();

        // 清理特效容器下的所有活跃对象
        if (effectsContainer != null)
        {
            for (int i = effectsContainer.childCount - 1; i >= 0; i--)
            {
                var child = effectsContainer.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        // 清理闪烁覆盖层
        if (screenFlashCanvas != null)
        {
            DOTween.Kill(screenFlashImage);
            screenFlashImage.color = Color.clear;
        }

        isInitialized = false;
        Debug.Log("[BattleEffectManager] 所有特效已清理");
    }

    #endregion

    #region Camera Shake（震屏效果）

    /// <summary>
    /// 相机震动效果
    /// 使用 Camera.main.transform.DOShakePosition 实现
    /// </summary>
    /// <param name="duration">震动持续时间（秒）</param>
    /// <param name="intensity">震动强度</param>
    public void CameraShake(float duration, float intensity)
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.transform.DOShakePosition(duration, intensity, vibrato: 10, randomness: 90f,
            snapping: false, fadeOut: true);
    }

    #endregion

    #region Screen Flash（全屏闪烁）

    /// <summary>
    /// 全屏闪烁效果
    /// 使用全屏 Overlay Canvas + Image 实现
    /// </summary>
    /// <param name="color">闪烁颜色</param>
    /// <param name="duration">闪烁持续时间（秒）</param>
    public void ScreenFlash(Color color, float duration)
    {
        if (screenFlashImage == null) CreateScreenFlashOverlay();

        if (screenFlashImage == null) return;

        // 先停止之前的动画
        screenFlashImage.DOKill();

        // 设置初始颜色（不透明）
        color.a = 0.6f;
        screenFlashImage.color = color;

        // 渐隐到透明
        screenFlashImage.DOFade(0f, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => screenFlashImage.color = Color.clear);
    }

    /// <summary>
    /// 创建全屏闪烁覆盖层（Overlay Canvas + 全屏 Image）
    /// </summary>
    private void CreateScreenFlashOverlay()
    {
        if (screenFlashCanvas != null) return;

        screenFlashCanvas = new GameObject("[ScreenFlash]");
        screenFlashCanvas.transform.SetParent(transform);

        // 创建 Overlay Canvas
        var canvas = screenFlashCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 确保在最上层

        screenFlashCanvas.AddComponent<CanvasScaler>();
        screenFlashCanvas.AddComponent<GraphicRaycaster>();

        // 全屏 Image
        var imgGo = new GameObject("FlashImage");
        imgGo.transform.SetParent(screenFlashCanvas.transform, false);

        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        screenFlashImage = imgGo.AddComponent<Image>();
        screenFlashImage.color = Color.clear;
        screenFlashImage.raycastTarget = false; // 不拦截点击事件
    }

    #endregion

    #region 战斗生命周期

    /// <summary>
    /// 战斗开始时调用 — 初始化/重置特效系统
    /// </summary>
    public void OnBattleStart()
    {
        Initialize();
        Debug.Log("[BattleEffectManager] 战斗特效系统就绪");
    }

    /// <summary>
    /// 战斗结束时调用 — 清理所有活跃特效
    /// </summary>
    public void OnBattleEnd()
    {
        // 不清理对象池，只清理活跃特效
        if (effectsContainer != null)
        {
            DOTween.KillAll();
            for (int i = effectsContainer.childCount - 1; i >= 0; i--)
            {
                var child = effectsContainer.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    // 尝试回收到池中
                    var poolable = child.gameObject;
                    if (poolable.name.StartsWith("PooledEffect_"))
                    {
                        string type = poolable.name.Replace("PooledEffect_", "");
                        ReturnToPool(type, poolable);
                    }
                }
            }
        }

        // 重置闪烁
        if (screenFlashImage != null)
        {
            screenFlashImage.DOKill();
            screenFlashImage.color = Color.clear;
        }
    }

    #endregion

    #region Provider 管理

    /// <summary>
    /// 设置特效提供者（用于替换默认的纯代码特效）
    /// </summary>
    /// <param name="provider">自定义的特效提供者实现</param>
    public void SetEffectProvider(IBattleEffectProvider provider)
    {
        effectProvider = provider ?? new BattleEffectFactory();
        Debug.Log($"[BattleEffectManager] 特效提供者已替换: {provider?.GetType().Name ?? "Default"}");
    }

    /// <summary>
    /// 获取当前特效提供者
    /// </summary>
    public IBattleEffectProvider GetEffectProvider()
    {
        return effectProvider;
    }

    #endregion

    #region 调试

    /// <summary>
    /// 获取对象池状态信息（调试用）
    /// </summary>
    public string GetPoolStats()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[BattleEffectManager 对象池状态]");
        foreach (var kvp in effectPool)
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value?.Count ?? 0} 个可用");
        }
        return sb.ToString();
    }

    #endregion
}
