using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地化管理器 — 单例，支持多语言切换
/// 语言包路径：Resources/Localization/{lang}.json
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // ============================================================
    // 语言枚举
    // ============================================================

    public enum Language
    {
        ZhCN = 0,
        EnUS = 1
    }

    // ============================================================
    // 事件
    // ============================================================

    /// <summary> 语言切换后触发，所有LocalizeText自动订阅刷新 </summary>
    public event Action OnLanguageChanged;

    // ============================================================
    // 状态
    // ============================================================

    [Header("当前语言")]
    [SerializeField] private Language currentLanguage = Language.ZhCN;

    private Dictionary<string, string> currentTexts = new Dictionary<string, string>();
    private Dictionary<string, string> fallbackTexts = new Dictionary<string, string>(); // 中文回退

    private const string PREFS_KEY = "Localization_Language";
    private const string FALLBACK_LANG = "zh-CN";
    private static readonly string[] LANG_CODES = { "zh-CN", "en-US" };

    // ============================================================
    // 生命周期
    // ============================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 加载保存的语言偏好
        currentLanguage = (Language)PlayerPrefs.GetInt(PREFS_KEY, (int)Language.ZhCN);

        // 始终加载中文作为回退
        fallbackTexts = LoadLanguageFile(FALLBACK_LANG);

        // 加载当前语言
        ReloadCurrentLanguage();
    }

    // ============================================================
    // 公开接口
    // ============================================================

    /// <summary> 当前语言 </summary>
    public Language CurrentLanguage => currentLanguage;

    /// <summary> 当前语言代码（如 "zh-CN"） </summary>
    public string CurrentLanguageCode => LANG_CODES[(int)currentLanguage];

    /// <summary>
    /// 获取本地化文本
    /// 回退链：当前语言 → 中文 → 返回key本身
    /// </summary>
    public string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        // 当前语言
        if (currentTexts.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
            return text;

        // 回退中文
        if (fallbackTexts.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
            return text;

        // 兜底返回key
        Debug.LogWarning($"[Localization] Key not found: {key}");
        return key;
    }

    /// <summary>
    /// 获取本地化文本（带参数模板）
    /// 模板格式：{0} {1} {2}...
    /// </summary>
    public string GetText(string key, params object[] args)
    {
        string template = GetText(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Localization] Format error for key '{key}' with template '{template}'");
            return template;
        }
    }

    /// <summary>
    /// 切换语言 — 触发OnLanguageChanged事件
    /// </summary>
    public void SetLanguage(Language lang)
    {
        if (currentLanguage == lang) return;

        currentLanguage = lang;
        PlayerPrefs.SetInt(PREFS_KEY, (int)lang);
        PlayerPrefs.Save();

        ReloadCurrentLanguage();
        OnLanguageChanged?.Invoke();

        Debug.Log($"[Localization] Language changed to {CurrentLanguageCode}");
    }

    /// <summary>
    /// 支持语言数量
    /// </summary>
    public int LanguageCount => LANG_CODES.Length;

    /// <summary>
    /// 获取语言代码 by index
    /// </summary>
    public string GetLanguageCode(int index)
    {
        if (index < 0 || index >= LANG_CODES.Length) return FALLBACK_LANG;
        return LANG_CODES[index];
    }

    /// <summary>
    /// 获取语言显示名称（用于UI下拉框）
    /// </summary>
    public string GetLanguageDisplayName(int index)
    {
        switch (index)
        {
            case 0: return "中文";
            case 1: return "English";
            default: return LANG_CODES[index];
        }
    }

    // ============================================================
    // 内部
    // ============================================================

    private void ReloadCurrentLanguage()
    {
        string langCode = CurrentLanguageCode;

        if (langCode == FALLBACK_LANG)
        {
            // 中文就是回退包，直接引用
            currentTexts = fallbackTexts;
        }
        else
        {
            currentTexts = LoadLanguageFile(langCode);
        }
    }

    /// <summary>
    /// 从 Resources/Localization/{langCode}.json 加载语言包
    /// </summary>
    private Dictionary<string, string> LoadLanguageFile(string langCode)
    {
        var result = new Dictionary<string, string>();

        var jsonFile = Resources.Load<TextAsset>($"Localization/{langCode}");
        if (jsonFile == null)
        {
            Debug.LogWarning($"[Localization] Language file not found: Localization/{langCode}");
            return result;
        }

        try
        {
            var parsed = MiniJson.Deserialize(jsonFile.text) as Dictionary<string, object>;
            if (parsed != null)
            {
                FlattenDictionary(parsed, "", result);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Localization] Failed to parse {langCode}.json: {e.Message}");
        }

        Debug.Log($"[Localization] Loaded {langCode}: {result.Count} keys");
        return result;
    }

    /// <summary>
    /// 扁平化嵌套JSON → key用.连接
    /// 如 { "main_menu": { "start": "开始" } } → "main_menu.start" = "开始"
    /// </summary>
    private void FlattenDictionary(Dictionary<string, object> source, string prefix, Dictionary<string, string> target)
    {
        foreach (var kvp in source)
        {
            string fullKey = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object> nested)
            {
                FlattenDictionary(nested, fullKey, target);
            }
            else
            {
                target[fullKey] = kvp.Value?.ToString() ?? "";
            }
        }
    }

    // ============================================================
    // MiniJson — 轻量JSON解析，不依赖第三方库
    // ============================================================

    private static class MiniJson
    {
        public static object Deserialize(string json)
        {
            var parser = new JsonParser(json);
            return parser.ParseValue();
        }

        private class JsonParser
        {
            private string json;
            private int index;

            public JsonParser(string json) { this.json = json; index = 0; }

            public object ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length) return null;

                char c = json[index];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n') return ParseNull();
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();

                return null;
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                index++; // skip {
                SkipWhitespace();

                while (index < json.Length && json[index] != '}')
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    index++; // skip :
                    SkipWhitespace();
                    object val = ParseValue();
                    dict[key] = val;
                    SkipWhitespace();
                    if (index < json.Length && json[index] == ',') index++;
                }
                if (index < json.Length) index++; // skip }
                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                index++; // skip [
                SkipWhitespace();

                while (index < json.Length && json[index] != ']')
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    if (index < json.Length && json[index] == ',') index++;
                    SkipWhitespace();
                }
                if (index < json.Length) index++;
                return list;
            }

            private string ParseString()
            {
                index++; // skip "
                int start = index;
                var sb = new System.Text.StringBuilder();

                while (index < json.Length && json[index] != '"')
                {
                    if (json[index] == '\\')
                    {
                        sb.Append(json.Substring(start, index - start));
                        index++;
                        if (index < json.Length)
                        {
                            char esc = json[index];
                            switch (esc)
                            {
                                case '"': sb.Append('"'); break;
                                case '\\': sb.Append('\\'); break;
                                case '/': sb.Append('/'); break;
                                case 'n': sb.Append('\n'); break;
                                case 't': sb.Append('\t'); break;
                                case 'r': sb.Append('\r'); break;
                                default: sb.Append(esc); break;
                            }
                        }
                        index++;
                        start = index;
                    }
                    else
                    {
                        index++;
                    }
                }
                sb.Append(json.Substring(start, index - start));
                if (index < json.Length) index++; // skip "
                return sb.ToString();
            }

            private object ParseNumber()
            {
                int start = index;
                if (json[index] == '-') index++;
                while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
                if (index < json.Length && json[index] == '.')
                {
                    index++;
                    while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
                }
                string numStr = json.Substring(start, index - start);
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
                return 0;
            }

            private bool ParseBool()
            {
                if (json[index] == 't') { index += 4; return true; }
                index += 5; return false;
            }

            private object ParseNull() { index += 4; return null; }

            private void SkipWhitespace()
            {
                while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
            }
        }
    }
}
