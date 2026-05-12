using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI文本自动本地化组件
/// 挂在Text/TextMeshProUGUI上，Inspector填localizationKey
/// 订阅OnLanguageChanged自动刷新
/// </summary>
[RequireComponent(typeof(Text))]
public class LocalizeText : MonoBehaviour
{
    [Tooltip("本地化Key，如 main_menu.start_button")]
    public string localizationKey;

    [Tooltip("带参数模板时的参数（可选，逗号分隔）")]
    public string templateArgs = "";

    [Tooltip("Editor模式下自动预览")]
    public bool previewInEditor = true;

    private Text textComponent;
    private bool isSubscribed = false;

    void Awake()
    {
        textComponent = GetComponent<Text>();
    }

    void OnEnable()
    {
        Subscribe();
        RefreshText();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!previewInEditor || string.IsNullOrEmpty(localizationKey)) return;

        // Editor预览
        textComponent = GetComponent<Text>();
        if (textComponent != null)
        {
            RefreshText();
        }
    }
#endif

    /// <summary>
    /// 运行时设置key并刷新（代码调用方式）
    /// </summary>
    public void SetKey(string key, params object[] args)
    {
        localizationKey = key;
        if (args != null && args.Length > 0)
            templateArgs = string.Join(",", System.Array.ConvertAll(args, a => a?.ToString() ?? ""));
        else
            templateArgs = "";

        RefreshText();
    }

    /// <summary>
    /// 手动刷新文本
    /// </summary>
    public void RefreshText()
    {
        if (textComponent == null) return;
        if (string.IsNullOrEmpty(localizationKey))
        {
            textComponent.text = "";
            return;
        }

        if (LocalizationManager.Instance == null) return;

        if (!string.IsNullOrEmpty(templateArgs))
        {
            var args = templateArgs.Split(',');
            textComponent.text = LocalizationManager.Instance.GetText(localizationKey, args);
        }
        else
        {
            textComponent.text = LocalizationManager.Instance.GetText(localizationKey);
        }
    }

    // ============================================================
    // 事件订阅
    // ============================================================

    private void Subscribe()
    {
        if (isSubscribed) return;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChangedHandler;
            isSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChangedHandler;
        }
        isSubscribed = false;
    }

    private void OnLanguageChangedHandler()
    {
        RefreshText();
    }

    // ============================================================
    // 防止多次订阅（场景切换时LocalizationManager可能重建）
    // ============================================================

    void OnDestroy()
    {
        Unsubscribe();
    }
}
