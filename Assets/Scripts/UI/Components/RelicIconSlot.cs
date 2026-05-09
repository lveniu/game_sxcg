using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 遗物图标可复用组件 — FE-04.1
    /// 
    /// 用途：BattlePanel遗物栏中的单个遗物图标
    /// 功能：
    /// - 显示遗物emoji + 稀有度边框色
    /// - 悬停/长按显示Tooltip（名称+描述+稀有度星标）
    /// - 遗物触发时闪烁+缩放弹跳动画
    /// 
    /// 挂载方式：
    /// - 可挂到prefab上通过Inspector绑定
    /// - 也可通过 RelicIconSlot.Create() 程序化创建
    /// </summary>
    public class RelicIconSlot : MonoBehaviour
    {
        [Header("显示元素")]
        public Text emojiText;
        public Image borderImage;

        [Header("Tooltip")]
        public GameObject tooltip;
        public Text tooltipNameText;
        public Text tooltipDescText;

        [Header("动画参数")]
        public float punchScale = 1.3f;
        public float punchDuration = 0.2f;
        public float tooltipFadeDuration = 0.15f;

        // 内部数据
        private string relicId;
        private Tweener floatTween;

        /// <summary>
        /// 初始化遗物图标
        /// </summary>
        public void Setup(RelicDisplayData displayData)
        {
            if (displayData == null) return;

            relicId = displayData.relicId;

            // Emoji图标
            if (emojiText != null)
            {
                emojiText.text = displayData.iconEmoji;
            }

            // 稀有度边框颜色
            if (borderImage != null)
            {
                borderImage.color = displayData.rarityColor;
            }

            // Tooltip内容
            if (tooltipNameText != null)
            {
                tooltipNameText.text = $"{displayData.rarityName} {displayData.relicName}";
                tooltipNameText.color = displayData.rarityColor;
            }

            if (tooltipDescText != null)
            {
                tooltipDescText.text = displayData.effectDescription;
            }
        }

        /// <summary>
        /// 遗物触发效果时的弹跳闪烁动画
        /// </summary>
        public void PlayTriggerEffect()
        {
            // Punch scale弹跳
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOScale(Vector3.one * punchScale, punchDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    if (this != null)
                        transform.DOScale(Vector3.one, punchDuration).SetEase(Ease.OutQuad);
                });

            // 边框闪烁
            if (borderImage != null)
            {
                var origColor = borderImage.color;
                borderImage.DOColor(Color.white, 0.1f)
                    .OnComplete(() =>
                    {
                        if (borderImage != null)
                            borderImage.DOColor(origColor, 0.15f);
                    });
            }
        }

        /// <summary>
        /// 显示Tooltip
        /// </summary>
        public void ShowTooltip()
        {
            if (tooltip == null) return;
            tooltip.SetActive(true);
            var cg = tooltip.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.DOFade(1f, tooltipFadeDuration);
            }
        }

        /// <summary>
        /// 隐藏Tooltip
        /// </summary>
        public void HideTooltip()
        {
            if (tooltip == null) return;
            var cg = tooltip.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.DOFade(0f, tooltipFadeDuration)
                    .OnComplete(() =>
                    {
                        if (tooltip != null) tooltip.SetActive(false);
                    });
            }
            else
            {
                tooltip.SetActive(false);
            }
        }

        /// <summary>
        /// 添加持续浮动动画
        /// </summary>
        public void StartFloat(float amplitude = 3f, float speed = 2f, float phaseOffset = 0f)
        {
            StopFloat();
            var rect = transform as RectTransform;
            if (rect == null) return;

            float startY = rect.anchoredPosition.y;
            floatTween = DOTween.To(
                () => 0f,
                x =>
                {
                    if (rect != null)
                    {
                        var pos = rect.anchoredPosition;
                        pos.y = startY + Mathf.Sin(Time.time * speed + phaseOffset) * amplitude;
                        rect.anchoredPosition = pos;
                    }
                },
                1f, Mathf.Infinity
            ).SetLoops(-1);
        }

        /// <summary>
        /// 停止浮动
        /// </summary>
        public void StopFloat()
        {
            if (floatTween != null)
            {
                floatTween.Kill();
                floatTween = null;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
            StopFloat();
        }

        // ========== 程序化创建 ==========

        /// <summary>
        /// 程序化创建一个遗物图标实例
        /// </summary>
        public static RelicIconSlot Create(RectTransform parent, float size, RelicDisplayData displayData, int index, float spacing)
        {
            var go = new GameObject($"Relic_{displayData?.relicId ?? index.ToString()}");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(index * (size + spacing), 0f);

            // 背景
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            bg.raycastTarget = true;

            // 边框
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(go.transform, false);
            var borderRect = borderGo.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.raycastTarget = false;

            // Emoji文本
            var emojiGo = new GameObject("IconText");
            emojiGo.transform.SetParent(go.transform, false);
            var emojiRect = emojiGo.AddComponent<RectTransform>();
            emojiRect.anchorMin = Vector2.zero;
            emojiRect.anchorMax = Vector2.one;
            emojiRect.offsetMin = Vector2.zero;
            emojiRect.offsetMax = Vector2.zero;
            var emojiTxt = emojiGo.AddComponent<Text>();
            emojiTxt.fontSize = 22;
            emojiTxt.alignment = TextAnchor.MiddleCenter;
            emojiTxt.raycastTarget = false;

            // Tooltip
            var tooltipGo = new GameObject("Tooltip");
            tooltipGo.transform.SetParent(go.transform, false);
            tooltipGo.SetActive(false);
            var tooltipRect = tooltipGo.AddComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 0f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0f);
            tooltipRect.pivot = new Vector2(0.5f, 1f);
            tooltipRect.anchoredPosition = new Vector2(0f, -size / 2f - 4f);
            tooltipRect.sizeDelta = new Vector2(180, 70);
            var tooltipBg = tooltipGo.AddComponent<Image>();
            tooltipBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            var tooltipCg = tooltipGo.AddComponent<CanvasGroup>();

            // Tooltip名称
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(tooltipGo.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.55f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(8, 0);
            nameRect.offsetMax = new Vector2(-8, -4);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.raycastTarget = false;

            // Tooltip描述
            var descGo = new GameObject("DescText");
            descGo.transform.SetParent(tooltipGo.transform, false);
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.55f);
            descRect.offsetMin = new Vector2(8, 4);
            descRect.offsetMax = new Vector2(-8, -2);
            var descTxt = descGo.AddComponent<Text>();
            descTxt.fontSize = 11;
            descTxt.alignment = TextAnchor.UpperLeft;
            descTxt.color = new Color(0.85f, 0.85f, 0.85f);
            descTxt.raycastTarget = false;

            // 添加悬停事件
            var trigger = go.AddComponent<EventTrigger>();
            var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener(_ =>
            {
                var slot = go.GetComponent<RelicIconSlot>();
                slot?.ShowTooltip();
            });
            trigger.triggers.Add(pointerEnter);

            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener(_ =>
            {
                var slot = go.GetComponent<RelicIconSlot>();
                slot?.HideTooltip();
            });
            trigger.triggers.Add(pointerExit);

            // 组装组件
            var slot = go.AddComponent<RelicIconSlot>();
            slot.emojiText = emojiTxt;
            slot.borderImage = borderImg;
            slot.tooltip = tooltipGo;
            slot.tooltipNameText = nameTxt;
            slot.tooltipDescText = descTxt;

            // 填充数据
            if (displayData != null)
                slot.Setup(displayData);

            return slot;
        }
    }
}
