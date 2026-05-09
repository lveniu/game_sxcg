using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 骰子技能全屏演出控制器 — FE-04.3
    /// 
    /// 播放序列：
    /// 1. 暗化 overlay 渐入（0.2s）
    /// 2. 骰子组合居中放大展示（0.4s OutBack）
    /// 3. 组合名称文字弹出（如"三条·AOE"）
    /// 4. 闪光脉冲序列（按骰子数量，依次闪光）
    /// 5. 伤害飘字喷出
    /// 6. 整体消退（0.3s）
    /// 
    /// 时长控制：整个序列约 1.5s，可被后续战斗逻辑自动清除
    /// </summary>
    public class DiceSkillCinematic : MonoBehaviour
    {
        [Header("Overlay层")]
        public Image darkOverlay;

        [Header("骰子展示区")]
        public RectTransform diceDisplayContainer;
        public float diceDisplaySize = 80f;
        public float diceDisplaySpacing = 20f;

        [Header("组合名称")]
        public Text comboNameText;
        public RectTransform comboNameRect;

        [Header("闪光层")]
        public Image flashOverlay;

        [Header("伤害数字容器")]
        public RectTransform damageNumbersContainer;

        [Header("动画参数")]
        public float darkOverlayAlpha = 0.7f;
        public float fadeInDuration = 0.2f;
        public float diceScaleDuration = 0.4f;
        public float flashInterval = 0.15f;
        public float flashAlpha = 0.8f;
        public float fadeOutDuration = 0.3f;
        public float totalDuration = 1.8f;

        private Sequence mainSequence;
        private List<GameObject> tempObjects = new List<GameObject>();

        // 组合颜色映射
        private static readonly Color COLOR_THREE = new Color(1f, 0.2f, 0.2f);    // 红色-高伤害
        private static readonly Color COLOR_STRAIGHT = new Color(0.2f, 0.6f, 1f);  // 蓝色-速度
        private static readonly Color COLOR_PAIR = new Color(0.3f, 1f, 0.5f);      // 绿色-治疗
        private static readonly Color COLOR_NONE = new Color(0.8f, 0.8f, 0.8f);

        // 组合名称映射
        private static readonly string[] COMBO_NAMES = {
            "🎲 骰子技能",        // None
            "✨ 对子·治疗",        // Pair
            "⚡ 顺子·加速",        // Straight
            "💥 三条·AOE"          // ThreeOfAKind
        };

        /// <summary>
        /// 播放全屏骰子技能演出
        /// </summary>
        /// <param name="combo">骰子组合数据</param>
        /// <param name="diceValues">骰子点数数组（如 [3,3,3] 或 [1,2,3]）</param>
        public void Play(DiceCombination combo, int[] diceValues)
        {
            KillAndClear();

            var comboColor = GetComboColor(combo.Type);
            string comboName = GetComboNameText(combo.Type);

            mainSequence = DOTween.Sequence();
            mainSequence.SetUpdate(true); // 不受时间缩放影响

            // === 阶段1: 暗化 overlay ===
            EnsureDarkOverlay();
            darkOverlay.color = Color.clear;
            darkOverlay.gameObject.SetActive(true);
            mainSequence.Append(darkOverlay.DOFade(darkOverlayAlpha, fadeInDuration).SetEase(Ease.OutQuad));

            // === 阶段2: 骰子居中放大 ===
            CreateDiceDisplay(diceValues, comboColor);
            mainSequence.Append(diceDisplayContainer.DOScale(Vector3.one, diceScaleDuration).SetEase(Ease.OutBack));

            // === 阶段3: 组合名称弹出 ===
            EnsureComboNameText(comboColor);
            comboNameText.text = comboName;
            comboNameRect.localScale = Vector3.zero;
            mainSequence.Append(comboNameRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
            mainSequence.Join(comboNameRect.DOAnchorPosY(60f, 0.3f).SetEase(Ease.OutQuad));

            // === 阶段4: 闪光序列 ===
            EnsureFlashOverlay();
            flashOverlay.gameObject.SetActive(true);
            for (int i = 0; i < diceValues.Length; i++)
            {
                float flashTime = diceScaleDuration + 0.3f + i * flashInterval;
                Color flashC = new Color(comboColor.r, comboColor.g, comboColor.b, flashAlpha);
                mainSequence.Insert(flashTime, flashOverlay.DOColor(flashC, 0.08f));
                mainSequence.Insert(flashTime + 0.08f, flashOverlay.DOColor(Color.clear, 0.12f));
            }

            // === 阶段5: 整体消退 ===
            float fadeStart = diceScaleDuration + 0.3f + diceValues.Length * flashInterval + 0.3f;
            mainSequence.Insert(fadeStart, darkOverlay.DOFade(0f, fadeOutDuration));
            mainSequence.Insert(fadeStart, diceDisplayContainer.DOScale(Vector3.zero, fadeOutDuration).SetEase(Ease.InBack));
            mainSequence.Insert(fadeStart, comboNameRect.DOScale(Vector3.zero, fadeOutDuration * 0.5f).SetEase(Ease.InBack));

            // 清理
            mainSequence.OnComplete(() =>
            {
                SetAllInactive();
            });
        }

        /// <summary>
        /// 立即停止演出并清理
        /// </summary>
        public void Stop()
        {
            KillAndClear();
        }

        private void KillAndClear()
        {
            if (mainSequence != null)
            {
                mainSequence.Kill();
                mainSequence = null;
            }

            foreach (var go in tempObjects)
            {
                if (go != null) Destroy(go);
            }
            tempObjects.Clear();

            SetAllInactive();
        }

        private void SetAllInactive()
        {
            if (darkOverlay != null) darkOverlay.gameObject.SetActive(false);
            if (flashOverlay != null) flashOverlay.gameObject.SetActive(false);
            if (diceDisplayContainer != null) diceDisplayContainer.gameObject.SetActive(false);
            if (comboNameRect != null) comboNameRect.gameObject.SetActive(false);
        }

        // ========== 程序化创建辅助 ==========

        private void EnsureDarkOverlay()
        {
            if (darkOverlay != null) return;

            var go = new GameObject("DarkOverlay");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            darkOverlay = go.AddComponent<Image>();
            darkOverlay.color = Color.clear;
            darkOverlay.raycastTarget = false;
            tempObjects.Add(go);
        }

        private void EnsureFlashOverlay()
        {
            if (flashOverlay != null) return;

            var go = new GameObject("FlashOverlay");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            flashOverlay = go.AddComponent<Image>();
            flashOverlay.color = Color.clear;
            flashOverlay.raycastTarget = false;
            tempObjects.Add(go);
        }

        private void EnsureComboNameText(Color color)
        {
            if (comboNameText != null && comboNameRect != null) return;

            var go = new GameObject("ComboName");
            go.transform.SetParent(transform, false);
            comboNameRect = go.AddComponent<RectTransform>();
            comboNameRect.anchorMin = new Vector2(0.5f, 0.5f);
            comboNameRect.anchorMax = new Vector2(0.5f, 0.5f);
            comboNameRect.anchoredPosition = new Vector2(0f, 40f);
            comboNameRect.sizeDelta = new Vector2(400f, 60f);

            comboNameText = go.AddComponent<Text>();
            comboNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            comboNameText.fontSize = 36;
            comboNameText.fontStyle = FontStyle.Bold;
            comboNameText.alignment = TextAnchor.MiddleCenter;
            comboNameText.color = color;
            comboNameText.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            tempObjects.Add(go);
        }

        private void CreateDiceDisplay(int[] diceValues, Color comboColor)
        {
            if (diceDisplayContainer == null)
            {
                var go = new GameObject("DiceDisplay");
                go.transform.SetParent(transform, false);
                diceDisplayContainer = go.AddComponent<RectTransform>();
                diceDisplayContainer.anchorMin = new Vector2(0.5f, 0.5f);
                diceDisplayContainer.anchorMax = new Vector2(0.5f, 0.5f);
                diceDisplayContainer.anchoredPosition = new Vector2(0f, -30f);
                tempObjects.Add(go);
            }

            // 清除旧子对象
            foreach (Transform child in diceDisplayContainer)
            {
                Destroy(child.gameObject);
            }

            diceDisplayContainer.localScale = Vector3.zero;
            diceDisplayContainer.gameObject.SetActive(true);

            float totalWidth = diceValues.Length * diceDisplaySize + (diceValues.Length - 1) * diceDisplaySpacing;
            float startX = -totalWidth / 2f + diceDisplaySize / 2f;

            for (int i = 0; i < diceValues.Length; i++)
            {
                var diceGo = new GameObject($"Dice_{i}");
                diceGo.transform.SetParent(diceDisplayContainer, false);

                var diceRect = diceGo.AddComponent<RectTransform>();
                diceRect.sizeDelta = new Vector2(diceDisplaySize, diceDisplaySize);
                diceRect.anchoredPosition = new Vector2(startX + i * (diceDisplaySize + diceDisplaySpacing), 0f);

                // 骰子背景
                var bg = diceGo.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
                bg.raycastTarget = false;

                // 骰子边框
                var borderGo = new GameObject("Border");
                borderGo.transform.SetParent(diceGo.transform, false);
                var borderRect = borderGo.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = new Vector2(-2, -2);
                borderRect.offsetMax = new Vector2(2, 2);
                var borderImg = borderGo.AddComponent<Image>();
                borderImg.color = comboColor;
                borderImg.raycastTarget = false;

                // 骰子点数
                var textGo = new GameObject("ValueText");
                textGo.transform.SetParent(diceGo.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 36;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = diceValues[i].ToString();
                text.raycastTarget = false;
            }
        }

        // ========== 工具方法 ==========

        private static Color GetComboColor(DiceCombinationType type)
        {
            return type switch
            {
                DiceCombinationType.ThreeOfAKind => COLOR_THREE,
                DiceCombinationType.Straight => COLOR_STRAIGHT,
                DiceCombinationType.Pair => COLOR_PAIR,
                _ => COLOR_NONE
            };
        }

        private static string GetComboNameText(DiceCombinationType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < COMBO_NAMES.Length)
                return COMBO_NAMES[idx];
            return COMBO_NAMES[0];
        }

        private void OnDestroy()
        {
            KillAndClear();
        }
    }
}
