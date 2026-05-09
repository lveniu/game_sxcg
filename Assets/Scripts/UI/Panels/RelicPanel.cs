using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using Game.Roguelike;

namespace Game.UI
{
    /// <summary>
    /// FE-06 遗物列表面板 — 展示当前已获得的所有遗物
    /// 数据源: RelicSystem.Instance.OwnedRelics
    /// 桥接层: UIConfigBridge.GetRelicDisplayData()
    /// </summary>
    public class RelicPanel : UIPanel
    {
        [System.Serializable]
        public class RelicSlot
        {
            public RectTransform rect;
            public Image iconImage;        // 遗物图标（emoji 渲染）
            public Image borderImage;      // 稀有度边框
            public Text nameText;          // 遗物名称
            public Text descText;          // 简短描述
            public Text rarityText;        // 稀有度+星标
            public Text effectText;        // 效果描述
            public Text triggerCountText;  // 触发次数
            public GameObject activeGlow;  // 激活发光效果
        }

        [Header("遗物列表")]
        public RectTransform listContainer;     // 遗物槽位容器（ScrollView Content）
        public GameObject relicSlotPrefab;      // 遗物槽位预制体（可选，为空则程序化创建）
        public int maxDisplaySlots = 10;

        [Header("空状态")]
        public GameObject emptyState;
        public Text emptyText;

        [Header("详情弹窗")]
        public RectTransform detailPopup;
        public Text detailNameText;
        public Text detailRarityText;
        public Text detailDescText;
        public Text detailEffectText;
        public Text detailTriggerText;
        public Button detailCloseButton;

        [Header("统计")]
        public Text totalCountText;
        public Text totalEffectSummaryText;

        // 程序化创建的槽位
        private readonly List<RelicSlot> activeSlots = new List<RelicSlot>();
        private readonly List<Tweener> activeTweens = new List<Tweener>();

        protected override void Awake()
        {
            base.Awake();

            if (detailCloseButton != null)
                detailCloseButton.onClick.AddListener(HideDetailPopup);

            if (emptyText != null)
                emptyText.text = "暂无遗物\n通关后选择遗物奖励来获得";
        }

        protected override void OnShow()
        {
            RefreshList();

            // 监听遗物变化
            if (RelicSystem.Instance != null)
            {
                RelicSystem.Instance.OnRelicAcquired += OnRelicAcquired;
                RelicSystem.Instance.OnRelicTriggered += OnRelicTriggered;
            }
        }

        protected override void OnHide()
        {
            if (RelicSystem.Instance != null)
            {
                RelicSystem.Instance.OnRelicAcquired -= OnRelicAcquired;
                RelicSystem.Instance.OnRelicTriggered -= OnRelicTriggered;
            }
            KillAllTweens();
        }

        #region 列表刷新

        /// <summary>
        /// 刷新遗物列表（完整重绘）
        /// </summary>
        public void RefreshList()
        {
            ClearSlots();
            KillAllTweens();

            var relics = RelicSystem.Instance?.OwnedRelics;
            bool hasRelics = relics != null && relics.Count > 0;

            // 空状态切换
            if (emptyState != null)
                emptyState.SetActive(!hasRelics);

            if (!hasRelics)
            {
                UpdateStats(0, "");
                return;
            }

            // 创建槽位
            for (int i = 0; i < relics.Count && i < maxDisplaySlots; i++)
            {
                var instance = relics[i];
                var displayData = UIConfigBridge.GetRelicDisplayData(instance.Data);
                var slot = CreateSlot(i, displayData, instance);
                activeSlots.Add(slot);

                // 入场动画：依次弹入
                if (slot.rect != null)
                {
                    slot.rect.localScale = Vector3.zero;
                    var t = slot.rect.DOScale(1f, 0.3f)
                        .SetEase(Ease.OutBack)
                        .SetDelay(i * 0.08f);
                    activeTweens.Add(t);
                }
            }

            UpdateStats(relics.Count, BuildEffectSummary(relics));
        }

        /// <summary>
        /// 创建单个遗物槽位
        /// </summary>
        private RelicSlot CreateSlot(int index, RelicDisplayData data, RelicInstance instance)
        {
            RelicSlot slot;

            if (relicSlotPrefab != null)
            {
                // 使用预制体创建
                var go = Instantiate(relicSlotPrefab, listContainer);
                slot = CreateSlotFromTransform(go.transform);
            }
            else
            {
                // 程序化创建
                slot = CreateSlotProcedurally(index, data, instance);
            }

            // 填充数据
            PopulateSlot(slot, data, instance);

            return slot;
        }

        /// <summary>
        /// 填充槽位数据
        /// </summary>
        private void PopulateSlot(RelicSlot slot, RelicDisplayData data, RelicInstance instance)
        {
            if (slot.nameText != null)
                slot.nameText.text = data.relicName;

            if (slot.descText != null)
                slot.descText.text = data.description;

            if (slot.rarityText != null)
            {
                slot.rarityText.text = $"{data.rarityName} {UIConfigBridge.GetRarityStars(data.rarity)}";
                slot.rarityText.color = data.rarityColor;
            }

            if (slot.effectText != null)
                slot.effectText.text = data.effectDescription;

            if (slot.borderImage != null)
                slot.borderImage.color = data.rarityColor;

            if (slot.iconImage != null)
            {
                slot.iconImage.GetComponent<Text>().text = data.iconEmoji;
            }

            if (slot.triggerCountText != null)
                slot.triggerCountText.text = $"触发 {instance.TriggerCount} 次";

            if (slot.activeGlow != null)
                slot.activeGlow.SetActive(instance.IsActive);

            // 点击查看详情
            if (slot.rect != null)
            {
                slot.rect.GetComponent<Button>()?.onClick.AddListener(() =>
                    ShowDetailPopup(data, instance));
            }
        }

        #endregion

        #region 程序化创建 UI

        /// <summary>
        /// 程序化创建完整槽位 UI
        /// </summary>
        private RelicSlot CreateSlotProcedurally(int index, RelicDisplayData data, RelicInstance instance)
        {
            var slot = new RelicSlot();

            // 根节点
            var rootGO = new GameObject($"RelicSlot_{index}");
            var rootRT = rootGO.AddComponent<RectTransform>();
            rootRT.SetParent(listContainer, false);
            rootRT.sizeDelta = new Vector2(320f, 72f);
            rootRT.anchorMin = new Vector2(0.5f, 1f);
            rootRT.anchorMax = new Vector2(0.5f, 1f);
            rootRT.pivot = new Vector2(0.5f, 1f);
            rootRT.anchoredPosition = new Vector2(0f, -index * 80f - 10f);

            // 背景按钮
            var bgBtn = rootGO.AddComponent<Button>();
            var bgImg = rootGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            bgBtn.targetGraphic = bgImg;

            // 稀有度边框
            var borderGO = new GameObject("Border");
            var borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.SetParent(rootRT, false);
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = new Vector2(-4f, -4f);
            slot.borderImage = borderGO.AddComponent<Image>();
            slot.borderImage.color = data.rarityColor;
            slot.borderImage.type = Image.Type.Sliced;

            // Emoji 图标
            var iconGO = new GameObject("Icon");
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.SetParent(rootRT, false);
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.sizeDelta = new Vector2(50f, 50f);
            iconRT.anchoredPosition = new Vector2(30f, 0f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            var iconTxt = iconGO.AddComponent<Text>();
            iconTxt.text = data.iconEmoji;
            iconTxt.fontSize = 28;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.iconImage = iconGO.AddComponent<Image>();
            slot.iconImage.color = Color.clear;

            // 名称
            var nameGO = new GameObject("Name");
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.SetParent(rootRT, false);
            nameRT.anchorMin = new Vector2(0f, 0.5f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = new Vector2(65f, -2f);
            nameRT.offsetMax = new Vector2(-10f, -2f);
            var nameTxt = nameGO.AddComponent<Text>();
            nameTxt.text = data.relicName;
            nameTxt.fontSize = 16;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = Color.white;
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.nameText = nameTxt;

            // 稀有度标签
            var rarityGO = new GameObject("Rarity");
            var rarityRT = rarityGO.AddComponent<RectTransform>();
            rarityRT.SetParent(rootRT, false);
            rarityRT.anchorMin = new Vector2(1f, 0.5f);
            rarityRT.anchorMax = new Vector2(1f, 1f);
            rarityRT.offsetMin = new Vector2(-100f, 0f);
            rarityRT.offsetMax = new Vector2(-10f, -2f);
            rarityRT.pivot = new Vector2(1f, 0.5f);
            var rarityTxt = rarityGO.AddComponent<Text>();
            rarityTxt.text = $"{data.rarityName} {UIConfigBridge.GetRarityStars(data.rarity)}";
            rarityTxt.fontSize = 11;
            rarityTxt.color = data.rarityColor;
            rarityTxt.alignment = TextAnchor.MiddleRight;
            rarityTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.rarityText = rarityTxt;

            // 效果描述
            var effectGO = new GameObject("Effect");
            var effectRT = effectGO.AddComponent<RectTransform>();
            effectRT.SetParent(rootRT, false);
            effectRT.anchorMin = new Vector2(0f, 0f);
            effectRT.anchorMax = new Vector2(1f, 0.5f);
            effectRT.offsetMin = new Vector2(65f, 2f);
            effectRT.offsetMax = new Vector2(-10f, 2f);
            var effectTxt = effectGO.AddComponent<Text>();
            effectTxt.text = data.effectDescription;
            effectTxt.fontSize = 12;
            effectTxt.color = new Color(0.8f, 0.8f, 0.8f);
            effectTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.effectText = effectTxt;

            // 触发次数
            var triggerGO = new GameObject("TriggerCount");
            var triggerRT = triggerGO.AddComponent<RectTransform>();
            triggerRT.SetParent(rootRT, false);
            triggerRT.anchorMin = new Vector2(1f, 0f);
            triggerRT.anchorMax = new Vector2(1f, 0.5f);
            triggerRT.offsetMin = new Vector2(-100f, 2f);
            triggerRT.offsetMax = new Vector2(-10f, 2f);
            triggerRT.pivot = new Vector2(1f, 0.5f);
            var triggerTxt = triggerGO.AddComponent<Text>();
            triggerTxt.text = $"触发 {instance.TriggerCount} 次";
            triggerTxt.fontSize = 10;
            triggerTxt.color = new Color(0.6f, 0.6f, 0.6f);
            triggerTxt.alignment = TextAnchor.MiddleRight;
            triggerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.triggerCountText = triggerTxt;

            slot.rect = rootRT;

            return slot;
        }

        /// <summary>
        /// 从预制体实例的 Transform 提取组件引用
        /// </summary>
        private RelicSlot CreateSlotFromTransform(Transform t)
        {
            return new RelicSlot
            {
                rect = t as RectTransform,
                iconImage = t.Find("Icon")?.GetComponent<Image>(),
                borderImage = t.Find("Border")?.GetComponent<Image>(),
                nameText = t.Find("Name")?.GetComponent<Text>(),
                descText = t.Find("Desc")?.GetComponent<Text>(),
                rarityText = t.Find("Rarity")?.GetComponent<Text>(),
                effectText = t.Find("Effect")?.GetComponent<Text>(),
                triggerCountText = t.Find("TriggerCount")?.GetComponent<Text>(),
                activeGlow = t.Find("ActiveGlow")?.gameObject
            };
        }

        #endregion

        #region 详情弹窗

        private void ShowDetailPopup(RelicDisplayData data, RelicInstance instance)
        {
            if (detailPopup == null) return;

            if (detailNameText != null)
                detailNameText.text = $"{data.iconEmoji} {data.relicName}";

            if (detailRarityText != null)
            {
                detailRarityText.text = $"{data.rarityName} {UIConfigBridge.GetRarityStars(data.rarity)}";
                detailRarityText.color = data.rarityColor;
            }

            if (detailDescText != null)
                detailDescText.text = data.description;

            if (detailEffectText != null)
                detailEffectText.text = $"效果：{data.effectDescription}";

            if (detailTriggerText != null)
                detailTriggerText.text = $"已触发 {instance.TriggerCount} 次 | {(instance.IsActive ? "✅ 激活中" : "⏸ 未激活")}";

            detailPopup.gameObject.SetActive(true);
            detailPopup.localScale = Vector3.zero;
            activeTweens.Add(
                detailPopup.DOScale(1f, 0.3f).SetEase(Ease.OutBack)
            );
        }

        private void HideDetailPopup()
        {
            if (detailPopup == null) return;
            activeTweens.Add(
                detailPopup.DOScale(0f, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    detailPopup.gameObject.SetActive(false);
                })
            );
        }

        #endregion

        #region 统计

        private void UpdateStats(int count, string summary)
        {
            if (totalCountText != null)
                totalCountText.text = $"遗物: {count}/{maxDisplaySlots}";

            if (totalEffectSummaryText != null)
                totalEffectSummaryText.text = summary;
        }

        /// <summary>
        /// 生成效果摘要（如 "攻击+10% | 防御+5 | 额外重摇×1"）
        /// </summary>
        private string BuildEffectSummary(List<RelicInstance> relics)
        {
            var parts = new List<string>();
            var effectTypes = System.Enum.GetValues(typeof(RelicEffectType));

            foreach (RelicEffectType et in effectTypes)
            {
                if (RelicSystem.Instance != null && RelicSystem.Instance.HasRelicEffect(et))
                {
                    float val = RelicSystem.Instance.GetTotalEffectValue(et);
                    string desc = UIConfigBridge.GetEffectDescription(et, val);
                    if (!string.IsNullOrEmpty(desc))
                        parts.Add(desc);
                }
            }

            // 额外重摇单独显示
            int extraRerolls = RelicSystem.Instance?.GetExtraRerolls() ?? 0;
            if (extraRerolls > 0)
                parts.Add($"额外重摇×{extraRerolls}");

            return parts.Count > 0 ? string.Join(" | ", parts) : "";
        }

        #endregion

        #region 事件回调

        private void OnRelicAcquired(RelicInstance instance)
        {
            RefreshList();
        }

        private void OnRelicTriggered(RelicInstance instance)
        {
            // 找到对应槽位，播放触发动画
            int idx = RelicSystem.Instance.OwnedRelics.IndexOf(instance);
            if (idx >= 0 && idx < activeSlots.Count)
            {
                var slot = activeSlots[idx];

                // 更新触发次数
                if (slot.triggerCountText != null)
                    slot.triggerCountText.text = $"触发 {instance.TriggerCount} 次";

                // 弹跳动画
                if (slot.rect != null)
                {
                    var seq = DOTween.Sequence();
                    seq.Append(slot.rect.DOScale(1.15f, 0.1f));
                    seq.Append(slot.rect.DOScale(1f, 0.15f));
                    seq.SetEase(Ease.OutQuad);
                    activeTweens.Add(seq);
                }

                // 边框闪白
                if (slot.borderImage != null)
                {
                    var origColor = slot.borderImage.color;
                    var t = DOTween.Sequence();
                    t.Append(slot.borderImage.DOColor(Color.white, 0.1f));
                    t.Append(slot.borderImage.DOColor(origColor, 0.2f));
                    activeTweens.Add(t);
                }
            }
        }

        #endregion

        #region 清理

        private void ClearSlots()
        {
            foreach (var slot in activeSlots)
            {
                if (slot.rect != null)
                    Destroy(slot.rect.gameObject);
            }
            activeSlots.Clear();
        }

        private void KillAllTweens()
        {
            foreach (var t in activeTweens)
            {
                if (t != null && t.IsActive())
                    t.Kill();
            }
            activeTweens.Clear();
        }

        protected override void OnDestroy()
        {
            KillAllTweens();
            base.OnDestroy();
        }

        #endregion
    }
}
