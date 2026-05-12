using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 伤害飘字系统 — 战斗中显示伤害/治疗/闪避等浮动文字
/// FE-30: 使用 ObjectPool 管理，避免运行时 Instantiate/Destroy
/// </summary>
public class DamagePopup : MonoBehaviour
{
    public static DamagePopup Instance { get; private set; }

    [Header("预制体")]
    public GameObject popupPrefab;
    public Transform popupParent;

    [Header("飘字颜色")]
    public Color damageColor = Color.red;
    public Color critColor = new Color(1f, 0.5f, 0f);
    public Color healColor = Color.green;
    public Color missColor = Color.gray;
    public Color shieldColor = Color.cyan;

    [Header("动画参数")]
    public float moveSpeed = 60f;
    public float fadeDuration = 1f;
    public float spreadRange = 30f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 显示伤害飘字（池化版本）
    /// </summary>
    public void ShowDamage(Vector3 worldPos, int amount, bool isCrit = false)
    {
        string text = amount.ToString();
        if (isCrit) text += "!";
        SpawnPopup(worldPos, text, isCrit ? critColor : damageColor, 1.2f);
    }

    /// <summary>
    /// 显示治疗飘字
    /// </summary>
    public void ShowHeal(Vector3 worldPos, int amount)
    {
        SpawnPopup(worldPos, $"+{amount}", healColor, 1f);
    }

    /// <summary>
    /// 显示闪避/护盾
    /// </summary>
    public void ShowText(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        SpawnPopup(worldPos, text, color, scale);
    }

    void SpawnPopup(Vector3 worldPos, string text, Color color, float scale)
    {
        // FE-30: 使用对象池
        var pool = ObjectPoolManager.Instance;
        DamagePopup popup;
        GameObject go;

        if (pool != null)
        {
            popup = pool.GetDamagePopup();
            if (popup != null && popup != this)
            {
                popup.SetContent(text, color, worldPos, scale);
                popup.StartCoroutine(popup.AnimatePopupCoroutine());
                return;
            }
        }

        // 回退：使用 prefab（如果 ObjectPool 不可用）
        if (popupPrefab == null)
        {
            CreateSimplePopup(worldPos, text, color, scale);
            return;
        }

        go = Instantiate(popupPrefab, popupParent);
        var txt = go.GetComponent<Text>();
        if (txt != null)
        {
            txt.text = text;
            txt.color = color;
        }

        var rt = go.GetComponent<RectTransform>();
        Vector2 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;
        rt.position = screenPos;

        StartCoroutine(AnimatePopup(rt, go, false));
    }

    /// <summary>
    /// 设置池化对象的内容（复用模式）
    /// </summary>
    public void SetContent(string text, Color color, Vector3 worldPos, float scale)
    {
        // 设置文本
        var txt = GetComponent<Text>();
        if (txt != null)
        {
            txt.text = text;
            txt.color = color;
        }

        // 设置位置
        var rt = GetComponent<RectTransform>();
        Vector2 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;
        rt.position = screenPos;

        _scale = scale;
        _isPoolBacked = true;
    }

    void CreateSimplePopup(Vector3 worldPos, string text, Color color, float scale)
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("DamagePopup", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = Mathf.RoundToInt(24 * scale);
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var rt = go.GetComponent<RectTransform>();
        Vector2 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;
        rt.position = screenPos;
        rt.sizeDelta = new Vector2(100, 40);

        StartCoroutine(AnimatePopup(rt, go, true));
    }

    // ===== 动画（原始模式，销毁） =====
    void AnimatePopup(RectTransform rt, GameObject go, bool isSimple)
    {
        StartCoroutine(_Animate(rt, go, isSimple));
    }

    IEnumerator _Animate(RectTransform rt, GameObject go, bool isSimple)
    {
        float timer = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 randomOffset = new Vector2(Random.Range(-spreadRange, spreadRange), 0);
        Text txt = go.GetComponent<Text>();
        Color startColor = txt != null ? txt.color : Color.white;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            if (rt != null)
                rt.anchoredPosition = startPos + randomOffset + Vector2.up * moveSpeed * t;

            if (txt != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                txt.color = c;
            }

            yield return null;
        }

        if (isSimple)
        {
            // 原始模式：直接销毁
            if (go != null) Destroy(go);
        }
        else
        {
            // 池化模式：回收到池
            var pool = ObjectPoolManager.Instance;
            if (pool != null)
                pool.ReleaseDamagePopup(this);
            else
                Destroy(gameObject);
        }
    }

    // ===== 动画（协程版本，供池化使用） =====
    public IEnumerator AnimatePopupCoroutine()
    {
        float timer = 0f;
        var rt = GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        Vector2 randomOffset = new Vector2(Random.Range(-spreadRange, spreadRange), 0);
        Text txt = GetComponent<Text>();
        Color startColor = txt != null ? txt.color : Color.white;
        float s = _scale;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            if (rt != null)
                rt.anchoredPosition = startPos + randomOffset + Vector2.up * moveSpeed * t;

            if (txt != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                txt.color = c;
            }

            yield return null;
        }

        // 回收
        var pool = ObjectPoolManager.Instance;
        if (pool != null)
            pool.ReleaseDamagePopup(this);
        else
            Destroy(gameObject);
    }

    private float _scale = 1f;
    private bool _isPoolBacked = false;
}
