using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// FE-30 对象池管理器 — 管理所有高频 Instantiate/Destroy 场景
/// 使用 UnityEngine.Pool.ObjectPool<T> (Unity 2022.3 内置)
/// 
/// 优先级 (按 Instantiate 频率排序):
/// 高频（每次战斗调用多次）:
///   - DamagePopup → DamagePopupPool
///   - DamageNumber → DamageNumberPool
///   - AchievementToast → ToastPool
///   - BattleEffect → 已有 BattleEffectManager 内部池
/// 
/// 中频（面板创建/销毁时）:
///   - BattlePanel: unitBar, relicIcon, roguelikeCard
///   - CardPlayPanel: handCard
///   - RoguelikeRewardPanel: rewardCard
///   - ShopPanel: shopItem
///   - EquipPanel: heroItem, backpackItem
///   - EventPanel: optionButton
///   - DiceUpgradePanel: faceSlot, upgradeOption
///   - HeroSelectPanel: heroCard
///   - BattleGridPanel: gridCell
///   - SaveLoadPanel: saveSlot
///   - SettlementPanel: heroExpCard
/// 
/// 低频（单例/生命周期）:
///   - GameStateMachine, GameManager, AudioManager, etc. → 不需要池
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Header("DamagePopup 配置")]
    public GameObject damagePopupPrefab;
    public Transform damagePopupParent;
    [Range(10, 50)] public int damagePopupPoolSize = 20;

    [Header("DamageNumber 配置")]
    public GameObject damageNumberPrefab;
    public Transform damageNumberParent;
    [Range(10, 50)] public int damageNumberPoolSize = 30;

    [Header("Toast 配置")]
    [Range(5, 20)] public int toastPoolSize = 10;

    private ObjectPool<DamagePopup> _damagePopupPool;
    private ObjectPool<DamageNumber> _damageNumberPool;
    private ObjectPool<GameObject> _toastPool;

    // 全局唯一，复用 Awake 单例
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeDamagePopupPool();
        InitializeDamageNumberPool();
        InitializeToastPool();

        Debug.Log("[ObjectPoolManager] 对象池初始化完成:");
        Debug.Log($"  DamagePopup: {damagePopupPoolSize} 预创建");
        Debug.Log($"  DamageNumber: {damageNumberPoolSize} 预创建");
        Debug.Log($"  Toast: {toastPoolSize} 预创建");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // 回收所有池中的对象
        if (_damagePopupPool != null)
        {
            while (_damagePopupPool.Count > 0)
            {
                var item = _damagePopupPool.Get();
                Destroy(item.gameObject);
            }
        }
        if (_damageNumberPool != null)
        {
            while (_damageNumberPool.Count > 0)
            {
                var item = _damageNumberPool.Get();
                Destroy(item.gameObject);
            }
        }
        if (_toastPool != null)
        {
            while (_toastPool.Count > 0)
            {
                var item = _toastPool.Get();
                Destroy(item);
            }
        }
    }

    // ==================== DamagePopup 池 ====================

    void InitializeDamagePopupPool()
    {
        if (damagePopupPrefab == null) return;

        _damagePopupPool = new ObjectPool<DamagePopup>(
            createFunc: () =>
            {
                var go = GameObject.Instantiate(damagePopupPrefab, damagePopupParent);
                return go.GetComponent<DamagePopup>();
            },
            actionOnGet: popup => popup.gameObject.SetActive(true),
            actionOnRelease: popup => popup.gameObject.SetActive(false),
            actionOnDestroy: popup => Destroy(popup.gameObject),
            collectionCheck: popup => popup.gameObject.activeInHierarchy,
            defaultCapacity: damagePopupPoolSize
        );

        // 预创建
        for (int i = 0; i < damagePopupPoolSize; i++)
        {
            var popup = _damagePopupPool.Get();
            _damagePopupPool.Release(popup);
        }
    }

    public DamagePopup GetDamagePopup()
    {
        if (_damagePopupPool == null)
        {
            Debug.LogWarning("[ObjectPoolManager] DamagePopup 池未初始化，回退到 Instantiate");
            var go = GameObject.Instantiate(damagePopupPrefab, damagePopupParent);
            return go.GetComponent<DamagePopup>();
        }
        return _damagePopupPool.Get();
    }

    public void ReleaseDamagePopup(DamagePopup popup)
    {
        if (_damagePopupPool != null)
            _damagePopupPool.Release(popup);
        else
            Destroy(popup.gameObject);
    }

    // ==================== DamageNumber 池 ====================

    void InitializeDamageNumberPool()
    {
        _damageNumberPool = new ObjectPool<DamageNumber>(
            createFunc: () =>
            {
                var go = new GameObject("DamageNumber");
                go.transform.SetParent(damageNumberParent, false);
                var rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(200, 60);
                var text = go.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 32;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                go.AddComponent<Outline>();
                var dmgNum = go.AddComponent<DamageNumber>();
                var dmgNumComp = go.GetComponent<DamageNumber>();
                if (dmgNumComp != null)
                {
                    dmgNumComp.numberText = text;
                    dmgNumComp.outline = go.GetComponent<Outline>();
                }
                return dmgNum;
            },
            actionOnGet: dm =>
            {
                dm.gameObject.SetActive(true);
                dm.transform.localPosition = Vector3.zero;
                dm.KillAnimation(); // 清理旧动画
            },
            actionOnRelease: dm =>
            {
                dm.gameObject.SetActive(false);
                dm.KillAnimation();
            },
            actionOnDestroy: dm => Destroy(dm.gameObject),
            collectionCheck: dm => dm.gameObject.activeInHierarchy,
            defaultCapacity: damageNumberPoolSize
        );

        for (int i = 0; i < damageNumberPoolSize; i++)
        {
            var dm = _damageNumberPool.Get();
            _damageNumberPool.Release(dm);
        }
    }

    public DamageNumber GetDamageNumber()
    {
        if (_damageNumberPool == null) return null;
        return _damageNumberPool.Get();
    }

    public void ReleaseDamageNumber(DamageNumber dm)
    {
        if (_damageNumberPool != null)
            _damageNumberPool.Release(dm);
        else
            Destroy(dm.gameObject);
    }

    // ==================== Toast 池 ====================

    void InitializeToastPool()
    {
        _toastPool = new ObjectPool<GameObject>(
            createFunc: () => new GameObject("Toast"),
            actionOnGet: go => go.SetActive(true),
            actionOnRelease: go => go.SetActive(false),
            actionOnDestroy: go => Destroy(go),
            collectionCheck: go => go.activeInHierarchy,
            defaultCapacity: toastPoolSize
        );

        for (int i = 0; i < toastPoolSize; i++)
        {
            var go = _toastPool.Get();
            _toastPool.Release(go);
        }
    }

    public GameObject GetToast()
    {
        return _toastPool?.Get();
    }

    public void ReleaseToast(GameObject toast)
    {
        if (_toastPool != null)
            _toastPool.Release(toast);
        else
            Destroy(toast);
    }

    // ==================== 运行时统计 ====================

    /// <summary>获取运行时对象池统计信息</summary>
    public string GetPoolStats()
    {
        return $"[ObjectPoolStats]\n" +
               $"  DamagePopup: 池={_damagePopupPool?.Count} 已创建总数未知\n" +
               $"  DamageNumber: 池={_damageNumberPool?.Count}\n" +
               $"  Toast: 池={_toastPool?.Count}";
    }
}
