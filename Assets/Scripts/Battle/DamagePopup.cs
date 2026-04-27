using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 伤害飘字系统 — 战斗中显示伤害/治疗/闪避等浮动文字
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
    /// 显示伤害飘字
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
        if (popupPrefab == null)
        {
            // 如果没有预制体，创建简单文字
            CreateSimplePopup(worldPos, text, color, scale);
            return;
        }

        var go = Instantiate(popupPrefab, popupParent);
        var txt = go.GetComponent<Text>();
        if (txt != null)
        {
            txt.text = text;
            txt.color = color;
        }

        // 转换世界坐标到屏幕坐标
        var rt = go.GetComponent<RectTransform>();
        Vector2 screenPos = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPos) : Vector2.zero;
        rt.position = screenPos;

        StartCoroutine(AnimatePopup(rt, go));
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

        StartCoroutine(AnimatePopup(rt, go));
    }

    IEnumerator AnimatePopup(RectTransform rt, GameObject go)
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

        if (go != null) Destroy(go);
    }
}
