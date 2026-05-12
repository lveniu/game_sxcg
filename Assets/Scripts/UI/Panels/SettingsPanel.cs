using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 设置面板 — 音量/画质/震动/语言，PlayerPrefs存储
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  ⚙ 设置              [✕关闭] │
    /// ├──────────────────────────────┤
    /// │  🔊 音乐音量                  │
    /// │  [━━━━━━━━━━░░░░░░░░] 80%    │
    /// │                               │
    /// │  🔉 音效音量                  │
    /// │  [━━━━━━━░░░░░░░░░░] 60%     │
    /// │                               │
    /// │  📱 画质                      │
    /// │  [低] [中✓] [高]              │
    /// │                               │
    /// │  📳 震动                      │
    /// │  [✓] 开启                     │
    /// │                               │
    /// │  🌐 语言                      │
    /// │  [中文✓] [English]            │
    /// │                               │
    /// ├──────────────────────────────┤
    /// │  版本 v0.1.0                  │
    /// │  [重置所有设置]                │
    /// └──────────────────────────────┘
    /// 
    /// 所有UI元素在代码中动态创建，不需要预制体。
    /// 继承UIPanel，OnShow读取PlayerPrefs → 刷新UI，OnHide保存到PlayerPrefs。
    /// </summary>
    public class SettingsPanel : UIPanel
    {
        // ============================================================
        // PlayerPrefs Keys
        // ============================================================
        private const string KEY_BGM_VOLUME  = "Settings_BGM_Volume";
        private const string KEY_SFX_VOLUME  = "Settings_SFX_Volume";
        private const string KEY_QUALITY     = "Settings_Quality";
        private const string KEY_VIBRATION   = "Settings_Vibration";
        private const string KEY_LANGUAGE    = "Settings_Language";

        // ============================================================
        // 默认值
        // ============================================================
        private const float DEFAULT_BGM_VOLUME = 0.8f;
        private const float DEFAULT_SFX_VOLUME = 0.6f;
        private const int   DEFAULT_QUALITY    = 1;      // 0=低, 1=中, 2=高
        private const int   DEFAULT_VIBRATION  = 1;      // 1=开, 0=关
        private const string DEFAULT_LANGUAGE  = "zh";

        // ============================================================
        // 颜色常量
        // ============================================================
        private static readonly Color BG_COLOR          = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        private static readonly Color HEADER_BG         = new Color(0.1f, 0.1f, 0.14f, 1f);
        private static readonly Color SECTION_LABEL_CLR = new Color(0.85f, 0.85f, 0.9f);
        private static readonly Color SLIDER_BG_CLR     = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color SLIDER_FILL_CLR   = new Color(0.2f, 0.55f, 0.95f, 1f);
        private static readonly Color BTN_SELECTED_BG   = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color BTN_SELECTED_TXT  = new Color(0.15f, 0.4f, 0.85f);
        private static readonly Color BTN_NORMAL_BG     = new Color(0.25f, 0.25f, 0.3f, 0.9f);
        private static readonly Color BTN_NORMAL_TXT    = new Color(0.7f, 0.7f, 0.75f);
        private static readonly Color TOGGLE_ON_CLR     = new Color(0.2f, 0.7f, 0.4f);
        private static readonly Color TOGGLE_OFF_CLR    = new Color(0.4f, 0.4f, 0.45f);
        private static readonly Color RESET_BTN_BG      = new Color(0.75f, 0.2f, 0.2f, 0.9f);
        private static readonly Color CLOSE_BTN_CLR     = new Color(0.7f, 0.7f, 0.75f);

        // ============================================================
        // 运行时状态
        // ============================================================
        private float bgmVolume;
        private float sfxVolume;
        private int   qualityLevel;
        private bool  vibrationOn;
        private string language;

        // ============================================================
        // UI引用 — 运行时创建
        // ============================================================
        private Slider bgmSlider;
        private Text   bgmValueText;
        private Slider sfxSlider;
        private Text   sfxValueText;
        private Button[] qualityBtns = new Button[3];
        private Text[]  qualityBtnTexts = new Text[3];
        private Button  vibrationBtn;
        private Text    vibrationBtnText;
        private Image   vibrationBtnBg;
        private Button[] languageBtns = new Button[2];
        private Text[]  languageBtnTexts = new Text[2];
        private Text    versionText;
        private Button  resetButton;

        private RectTransform contentRoot;

        // ============================================================
        // 生命周期
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            panelId = "Settings";
            slideInAnimation = false; // 自定义滑入动画

            BuildUI();
        }

        protected override void OnShow()
        {
            LoadSettings();
            RefreshUI();
            PlaySlideInAnimation();
        }

        protected override void OnHide()
        {
            SaveSettings();
        }

        // ============================================================
        // UI构建 — 程序化创建全部元素
        // ============================================================

        private void BuildUI()
        {
            // --- 全屏背景 ---
            var bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            bg.color = BG_COLOR;
            bg.raycastTarget = true;

            var selfRect = rectTransform;
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = selfRect.offsetMax = Vector2.zero;

            // --- 内容区域（居中640宽） ---
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(transform, false);
            contentRoot = contentGo.AddComponent<RectTransform>();
            contentRoot.anchorMin = new Vector2(0.5f, 0f);
            contentRoot.anchorMax = new Vector2(0.5f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.sizeDelta = new Vector2(640f, 0f);
            contentRoot.offsetMin = new Vector2(contentRoot.offsetMin.x, 60f);
            contentRoot.offsetMax = new Vector2(contentRoot.offsetMax.x, -60f);

            float y = 0f;

            // --- 标题栏 ---
            y = BuildHeader(contentRoot, y);

            // --- 音乐音量 ---
            y = BuildSliderSection(contentRoot, y, "🔊 音乐音量",
                out bgmSlider, out bgmValueText, DEFAULT_BGM_VOLUME, OnBGMChanged);

            // --- 音效音量 ---
            y = BuildSliderSection(contentRoot, y, "🔉 音效音量",
                out sfxSlider, out sfxValueText, DEFAULT_SFX_VOLUME, OnSFXChanged);

            // --- 画质 ---
            y = BuildQualitySection(contentRoot, y);

            // --- 震动 ---
            y = BuildVibrationSection(contentRoot, y);

            // --- 语言 ---
            y = BuildLanguageSection(contentRoot, y);

            // --- 版本号 ---
            y = BuildVersionSection(contentRoot, y);

            // --- 重置按钮 ---
            BuildResetButton(contentRoot, y);
        }

        // ==================== 标题栏 ====================

        private float BuildHeader(RectTransform parent, float startY)
        {
            // 标题背景
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(parent, false);
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 70f);
            headerRect.anchoredPosition = new Vector2(0f, -startY);
            var headerBg = headerGo.AddComponent<Image>();
            headerBg.color = HEADER_BG;

            // 标题文字
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0f);
            titleRect.anchorMax = new Vector2(0.8f, 1f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "⚙ 设置";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;

            // 关闭按钮
            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(headerGo.transform, false);
            var closeRect = closeGo.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.85f, 0.15f);
            closeRect.anchorMax = new Vector2(0.95f, 0.85f);
            closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = CLOSE_BTN_CLR;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var closeTxt = closeGo.AddComponent<Text>();
            closeTxt.text = "✕";
            closeTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            closeTxt.fontSize = 24;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeBtn.onClick.AddListener(OnCloseClicked);

            return startY + 70f;
        }

        // ==================== 滑块区域 ====================

        private float BuildSliderSection(RectTransform parent, float startY, string label,
            out Slider slider, out Text valueText, float defaultValue, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            float sectionHeight = 90f;

            // 区域容器
            var sectionGo = new GameObject(label.Replace(" ", "_"));
            sectionGo.transform.SetParent(parent, false);
            var sectionRect = sectionGo.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0f, 1f);
            sectionRect.anchorMax = new Vector2(1f, 1f);
            sectionRect.pivot = new Vector2(0.5f, 1f);
            sectionRect.sizeDelta = new Vector2(0f, sectionHeight);
            sectionRect.anchoredPosition = new Vector2(0f, -startY);

            // 标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(sectionGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0.6f);
            labelRect.anchorMax = new Vector2(1f, 0.9f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 20;
            labelText.color = SECTION_LABEL_CLR;
            labelText.alignment = TextAnchor.MiddleLeft;

            // 滑块背景
            var sliderBgGo = new GameObject("SliderBg");
            sliderBgGo.transform.SetParent(sectionGo.transform, false);
            var sliderBgRect = sliderBgGo.AddComponent<RectTransform>();
            sliderBgRect.anchorMin = new Vector2(0.05f, 0.15f);
            sliderBgRect.anchorMax = new Vector2(0.75f, 0.5f);
            sliderBgRect.offsetMin = sliderBgRect.offsetMax = Vector2.zero;
            var sliderBgImg = sliderBgGo.AddComponent<Image>();
            sliderBgImg.color = SLIDER_BG_CLR;

            // 滑块填充
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(sliderBgGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = SLIDER_FILL_CLR;

            // 滑块手柄
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(sliderBgGo.transform, false);
            var handleRect = handleGo.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(30f, 0f);
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.offsetMin = new Vector2(-15f, -4f);
            handleRect.offsetMax = new Vector2(15f, 4f);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;

            // Slider组件
            slider = sliderBgGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = defaultValue;
            slider.onValueChanged.AddListener(onValueChanged);

            // 百分比文字
            var valGo = new GameObject("ValueText");
            valGo.transform.SetParent(sectionGo.transform, false);
            var valRect = valGo.AddComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0.78f, 0.15f);
            valRect.anchorMax = new Vector2(0.95f, 0.5f);
            valRect.offsetMin = valRect.offsetMax = Vector2.zero;
            valueText = valGo.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            valueText.fontSize = 18;
            valueText.color = Color.white;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.text = Mathf.RoundToInt(defaultValue * 100) + "%";

            return startY + sectionHeight;
        }

        // ==================== 画质区域 ====================

        private float BuildQualitySection(RectTransform parent, float startY)
        {
            float sectionHeight = 85f;

            var sectionGo = new GameObject("QualitySection");
            sectionGo.transform.SetParent(parent, false);
            var sectionRect = sectionGo.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0f, 1f);
            sectionRect.anchorMax = new Vector2(1f, 1f);
            sectionRect.pivot = new Vector2(0.5f, 1f);
            sectionRect.sizeDelta = new Vector2(0f, sectionHeight);
            sectionRect.anchoredPosition = new Vector2(0f, -startY);

            // 标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(sectionGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0.6f);
            labelRect.anchorMax = new Vector2(1f, 0.9f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "📱 画质";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 20;
            labelText.color = SECTION_LABEL_CLR;
            labelText.alignment = TextAnchor.MiddleLeft;

            // 三个按钮：低 / 中 / 高
            string[] labels = { "低", "中", "高" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var btnGo = new GameObject($"QualityBtn_{i}");
                btnGo.transform.SetParent(sectionGo.transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.05f + i * 0.31f, 0.1f);
                btnRect.anchorMax = new Vector2(0.3f + i * 0.31f, 0.5f);
                btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

                var bg = btnGo.AddComponent<Image>();
                bg.color = BTN_NORMAL_BG;

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnQualitySelected(captured));

                // 文字（带✓占位）
                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txtRect = txtGo.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.fontSize = 18;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = BTN_NORMAL_TXT;
                txt.text = labels[i];

                qualityBtns[i] = btn;
                qualityBtnTexts[i] = txt;
            }

            return startY + sectionHeight;
        }

        // ==================== 震动区域 ====================

        private float BuildVibrationSection(RectTransform parent, float startY)
        {
            float sectionHeight = 70f;

            var sectionGo = new GameObject("VibrationSection");
            sectionGo.transform.SetParent(parent, false);
            var sectionRect = sectionGo.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0f, 1f);
            sectionRect.anchorMax = new Vector2(1f, 1f);
            sectionRect.pivot = new Vector2(0.5f, 1f);
            sectionRect.sizeDelta = new Vector2(0f, sectionHeight);
            sectionRect.anchoredPosition = new Vector2(0f, -startY);

            // 标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(sectionGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0.15f);
            labelRect.anchorMax = new Vector2(0.45f, 0.85f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "📳 震动";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 20;
            labelText.color = SECTION_LABEL_CLR;
            labelText.alignment = TextAnchor.MiddleLeft;

            // 切换按钮
            var btnGo = new GameObject("VibrationToggle");
            btnGo.transform.SetParent(sectionGo.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.15f);
            btnRect.anchorMax = new Vector2(0.75f, 0.85f);
            btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

            vibrationBtnBg = btnGo.AddComponent<Image>();
            vibrationBtnBg.color = TOGGLE_ON_CLR;

            vibrationBtn = btnGo.AddComponent<Button>();
            vibrationBtn.targetGraphic = vibrationBtnBg;
            vibrationBtn.onClick.AddListener(OnVibrationToggled);

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
            vibrationBtnText = txtGo.AddComponent<Text>();
            vibrationBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            vibrationBtnText.fontSize = 18;
            vibrationBtnText.alignment = TextAnchor.MiddleCenter;
            vibrationBtnText.color = Color.white;

            return startY + sectionHeight;
        }

        // ==================== 语言区域 ====================

        private float BuildLanguageSection(RectTransform parent, float startY)
        {
            float sectionHeight = 70f;

            var sectionGo = new GameObject("LanguageSection");
            sectionGo.transform.SetParent(parent, false);
            var sectionRect = sectionGo.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0f, 1f);
            sectionRect.anchorMax = new Vector2(1f, 1f);
            sectionRect.pivot = new Vector2(0.5f, 1f);
            sectionRect.sizeDelta = new Vector2(0f, sectionHeight);
            sectionRect.anchoredPosition = new Vector2(0f, -startY);

            // 标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(sectionGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0.15f);
            labelRect.anchorMax = new Vector2(0.45f, 0.85f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "🌐 语言";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 20;
            labelText.color = SECTION_LABEL_CLR;
            labelText.alignment = TextAnchor.MiddleLeft;

            // 两个按钮：中文 / English
            string[] labels = { "中文", "English" };
            for (int i = 0; i < 2; i++)
            {
                int captured = i;
                var btnGo = new GameObject($"LangBtn_{i}");
                btnGo.transform.SetParent(sectionGo.transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f + i * 0.24f, 0.15f);
                btnRect.anchorMax = new Vector2(0.7f + i * 0.24f, 0.85f);
                btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

                var bg = btnGo.AddComponent<Image>();
                bg.color = BTN_NORMAL_BG;

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => OnLanguageSelected(captured));

                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txtRect = txtGo.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.fontSize = 18;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = BTN_NORMAL_TXT;
                txt.text = labels[i];

                languageBtns[i] = btn;
                languageBtnTexts[i] = txt;
            }

            return startY + sectionHeight;
        }

        // ==================== 版本号 ====================

        private float BuildVersionSection(RectTransform parent, float startY)
        {
            var go = new GameObject("VersionText");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 1f);
            rt.anchorMax = new Vector2(0.95f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 30f);
            rt.anchoredPosition = new Vector2(0f, -(startY + 15f));

            versionText = go.AddComponent<Text>();
            versionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            versionText.fontSize = 14;
            versionText.color = new Color(0.5f, 0.5f, 0.55f);
            versionText.alignment = TextAnchor.MiddleCenter;

            string ver = Application.version;
            if (string.IsNullOrEmpty(ver) || ver == "0.0" || ver == "0.0.0")
                ver = "v0.1.0";
            else
                ver = $"v{ver}";
            versionText.text = ver;

            return startY + 55f;
        }

        // ==================== 重置按钮 ====================

        private void BuildResetButton(RectTransform parent, float startY)
        {
            var go = new GameObject("ResetBtn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 1f);
            rt.anchorMax = new Vector2(0.85f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 50f);
            rt.anchoredPosition = new Vector2(0f, -(startY + 5f));

            var bg = go.AddComponent<Image>();
            bg.color = RESET_BTN_BG;

            resetButton = go.AddComponent<Button>();
            resetButton.targetGraphic = bg;
            resetButton.onClick.AddListener(OnResetClicked);

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = "重置所有设置";
        }

        // ============================================================
        // 设置读写
        // ============================================================

        private void LoadSettings()
        {
            bgmVolume   = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, DEFAULT_BGM_VOLUME);
            sfxVolume   = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);
            qualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, DEFAULT_QUALITY);
            vibrationOn = PlayerPrefs.GetInt(KEY_VIBRATION, DEFAULT_VIBRATION) == 1;
            language    = PlayerPrefs.GetString(KEY_LANGUAGE, DEFAULT_LANGUAGE);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_BGM_VOLUME, bgmVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
            PlayerPrefs.SetInt(KEY_QUALITY, qualityLevel);
            PlayerPrefs.SetInt(KEY_VIBRATION, vibrationOn ? 1 : 0);
            PlayerPrefs.SetString(KEY_LANGUAGE, language);
            PlayerPrefs.Save();
        }

        // ============================================================
        // 刷新UI
        // ============================================================

        private void RefreshUI()
        {
            // 音量滑块
            if (bgmSlider != null) bgmSlider.value = bgmVolume;
            if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(bgmVolume * 100) + "%";

            if (sfxSlider != null) sfxSlider.value = sfxVolume;
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(sfxVolume * 100) + "%";

            // 画质按钮高亮
            RefreshQualityButtons();

            // 震动按钮
            RefreshVibrationButton();

            // 语言按钮高亮
            RefreshLanguageButtons();
        }

        private void RefreshQualityButtons()
        {
            string[] labels = { "低", "中", "高" };
            for (int i = 0; i < 3; i++)
            {
                if (qualityBtns[i] == null) continue;
                var img = qualityBtns[i].GetComponent<Image>();
                bool selected = (i == qualityLevel);
                if (img != null) img.color = selected ? BTN_SELECTED_BG : BTN_NORMAL_BG;
                if (qualityBtnTexts[i] != null)
                {
                    qualityBtnTexts[i].color = selected ? BTN_SELECTED_TXT : BTN_NORMAL_TXT;
                    qualityBtnTexts[i].text = selected ? $"{labels[i]} ✓" : labels[i];
                }
            }
        }

        private void RefreshVibrationButton()
        {
            if (vibrationBtnBg != null)
                vibrationBtnBg.color = vibrationOn ? TOGGLE_ON_CLR : TOGGLE_OFF_CLR;
            if (vibrationBtnText != null)
                vibrationBtnText.text = vibrationOn ? "✓ 开启" : "关闭";
        }

        private void RefreshLanguageButtons()
        {
            string[] labels = { "中文", "English" };
            string[] keys   = { "zh",   "en" };
            for (int i = 0; i < 2; i++)
            {
                if (languageBtns[i] == null) continue;
                var img = languageBtns[i].GetComponent<Image>();
                bool selected = (language == keys[i]);
                if (img != null) img.color = selected ? BTN_SELECTED_BG : BTN_NORMAL_BG;
                if (languageBtnTexts[i] != null)
                {
                    languageBtnTexts[i].color = selected ? BTN_SELECTED_TXT : BTN_NORMAL_TXT;
                    languageBtnTexts[i].text = selected ? $"{labels[i]} ✓" : labels[i];
                }
            }
        }

        // ============================================================
        // 回调
        // ============================================================

        private void OnBGMChanged(float value)
        {
            // 步长 0.05 取整
            bgmVolume = Mathf.Round(value * 20f) / 20f;
            if (bgmSlider != null) bgmSlider.value = bgmVolume;
            if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(bgmVolume * 100) + "%";
            AudioListener.volume = bgmVolume;
        }

        private void OnSFXChanged(float value)
        {
            sfxVolume = Mathf.Round(value * 20f) / 20f;
            if (sfxSlider != null) sfxSlider.value = sfxVolume;
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(sfxVolume * 100) + "%";
            if (AudioManager.Instance != null)
                AudioManager.Instance.SFXVolume = sfxVolume;
        }

        private void OnQualitySelected(int level)
        {
            qualityLevel = Mathf.Clamp(level, 0, 2);
            QualitySettings.SetQualityLevel(qualityLevel, true);
            RefreshQualityButtons();
            Debug.Log($"[Settings] 画质切换为: {new[] { "低", "中", "高" }[qualityLevel]}");
        }

        private void OnVibrationToggled()
        {
            vibrationOn = !vibrationOn;
            RefreshVibrationButton();
            Debug.Log($"[Settings] 震动: {(vibrationOn ? "开启" : "关闭")}");
            if (vibrationOn)
                Handheld.Vibrate();
        }

        private void OnLanguageSelected(int index)
        {
            language = index == 0 ? "zh" : "en";
            RefreshLanguageButtons();
            Debug.Log($"[Settings] 语言切换为: {language}");
            // 本地化系统对接占位：待 LocalizationManager 实现后取消注释
            // if (LocalizationManager.Instance != null)
            //     LocalizationManager.Instance.SetLanguage(language);
        }

        private void OnResetClicked()
        {
            bgmVolume   = DEFAULT_BGM_VOLUME;
            sfxVolume   = DEFAULT_SFX_VOLUME;
            qualityLevel = DEFAULT_QUALITY;
            vibrationOn = DEFAULT_VIBRATION == 1;
            language    = DEFAULT_LANGUAGE;

            // 应用
            AudioListener.volume = bgmVolume;
            QualitySettings.SetQualityLevel(qualityLevel, true);

            RefreshUI();
            SaveSettings();

            Debug.Log("[Settings] 所有设置已重置为默认值");

            // 重置按钮缩放反馈
            if (resetButton != null)
            {
                var rt = resetButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.DOKill();
                    rt.DOScale(0.92f, 0.08f).SetEase(Ease.InQuad)
                        .OnComplete(() => rt.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
                }
            }
        }

        private void OnCloseClicked()
        {
            Hide();
        }

        // ============================================================
        // 入场/出场动画 — 底部滑入/滑出
        // ============================================================

        private void PlaySlideInAnimation()
        {
            if (contentRoot == null) return;

            contentRoot.DOKill();

            // 从底部滑入
            float targetY = contentRoot.anchoredPosition.y;
            contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, -1280f);
            contentRoot.DOAnchorPosY(targetY, 0.35f)
                .SetEase(Ease.OutCubic)
                .SetTarget(gameObject);
        }
    }
}
