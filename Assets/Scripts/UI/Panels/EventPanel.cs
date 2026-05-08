using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 随机事件弹窗 — 战斗胜利后30%概率触发
    /// 
    /// 布局（居中弹窗）：
    /// ┌────────────────────────────┐
    /// │   🎲 随机事件              │
    /// │                            │
    /// │   [事件图标]               │
    /// │   宝箱！你发现了一个宝箱   │
    /// │                            │
    /// │   📜 效果：                │
    /// │   金币 +50                 │
    /// │   攻击 +5                  │
    /// │                            │
    /// │   [确认]                   │
    /// └────────────────────────────┘
    /// </summary>
    public class EventPanel : UIPanel
    {
        [Header("事件显示")]
        public Image eventIcon;
        public Text eventTitleText;
        public Text eventDescText;
        public RectTransform effectsPanel;
        public Text effectsText;

        [Header("按钮")]
        public Button confirmButton;
        public Text confirmButtonText;

        // 事件类型图标映射
        private static readonly Dictionary<RandomEventType, string> EVENT_ICONS = new()
        {
            { RandomEventType.Treasure, "📦" },
            { RandomEventType.Trap, "⚠️" },
            { RandomEventType.MysteryMerchant, "🧙" },
            { RandomEventType.Altar, "⛪" },
            { RandomEventType.WanderingHealer, "💊" },
            { RandomEventType.Arena, "⚔️" },
        };

        private static readonly Dictionary<RandomEventType, string> EVENT_TITLES = new()
        {
            { RandomEventType.Treasure, "宝箱！" },
            { RandomEventType.Trap, "陷阱！" },
            { RandomEventType.MysteryMerchant, "神秘商人" },
            { RandomEventType.Altar, "祭坛" },
            { RandomEventType.WanderingHealer, "流浪医师" },
            { RandomEventType.Arena, "竞技场" },
        };

        private RandomEvent currentEvent;

        protected override void Awake()
        {
            base.Awake();
            panelId = "Event";
        }

        protected override void OnShow()
        {
            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirmClicked);

            // 默认隐藏
            if (effectsPanel != null) effectsPanel.gameObject.SetActive(false);
        }

        protected override void OnHide()
        {
            confirmButton?.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 触发并显示随机事件
        /// </summary>
        public void TriggerAndShow(int levelId)
        {
            currentEvent = RandomEventSystem.TriggerEvent(levelId);

            if (currentEvent == null)
            {
                // 30%没触发，直接跳过
                Debug.Log("[Event] 未触发随机事件，跳过");
                Hide();
                return;
            }

            // 显示事件内容
            ShowEventContent(currentEvent);
        }

        /// <summary>
        /// 显示指定事件内容
        /// </summary>
        public void ShowEventContent(RandomEvent evt)
        {
            currentEvent = evt;

            // 标题
            string title = EVENT_TITLES.TryGetValue(evt.eventType, out var t) ? t : "随机事件";
            if (eventTitleText != null)
                eventTitleText.text = title;

            // 描述
            if (eventDescText != null)
                eventDescText.text = evt.description;

            // 图标颜色
            if (eventIcon != null)
            {
                string iconStr = EVENT_ICONS.TryGetValue(evt.eventType, out var icon) ? icon : "❓";
                // 图标用颜色区分类型
                eventIcon.color = evt.eventType switch
                {
                    RandomEventType.Treasure => new Color(1f, 0.85f, 0.2f),    // 金色
                    RandomEventType.Trap => new Color(1f, 0.3f, 0.3f),          // 红色
                    RandomEventType.MysteryMerchant => new Color(0.6f, 0.3f, 1f), // 紫色
                    RandomEventType.Altar => new Color(0.9f, 0.9f, 0.9f),       // 白色
                    RandomEventType.WanderingHealer => new Color(0.3f, 0.9f, 0.4f), // 绿色
                    RandomEventType.Arena => new Color(0.9f, 0.5f, 0.2f),       // 橙色
                    _ => Color.gray
                };
            }

            // 效果面板
            string effects = BuildEffectsString(evt);
            if (effectsText != null)
                effectsText.text = effects;

            if (effectsPanel != null)
                effectsPanel.gameObject.SetActive(!string.IsNullOrEmpty(effects));

            // 确认按钮文字
            if (confirmButtonText != null)
                confirmButtonText.text = evt.eventType == RandomEventType.Trap ? "接受" : "确认";

            // 入场动画
            PlayEnterAnimation();
        }

        private string BuildEffectsString(RandomEvent evt)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (evt.goldReward != 0)
                parts.Add($"金币 {(evt.goldReward > 0 ? "+" : "")}{evt.goldReward}");

            if (evt.healthLoss != 0)
                parts.Add($"生命 {(evt.healthLoss > 0 ? "-" : "+")}{Mathf.Abs(evt.healthLoss)}");

            if (evt.buffAttack != 0)
                parts.Add($"攻击 {(evt.buffAttack > 0 ? "+" : "")}{evt.buffAttack}");

            if (evt.healAmount != 0)
                parts.Add($"治疗 +{evt.healAmount}");

            if (evt.discountRate > 0)
                parts.Add($"商店折扣 {(1f - evt.discountRate) * 100:F0}%");

            return parts.Count > 0 ? string.Join("\n", parts) : "";
        }

        private void OnConfirmClicked()
        {
            if (currentEvent == null)
            {
                Hide();
                return;
            }

            // 应用事件效果
            var inventory = PlayerInventory.Instance;
            var deck = CardDeck.Instance;
            var heroes = deck?.fieldHeroes;

            if (inventory != null)
            {
                RandomEventSystem.ApplyEvent(currentEvent, inventory, heroes);
                Debug.Log($"[Event] 应用事件效果：{currentEvent.description}");
            }

            // 关闭动画
            if (rectTransform != null)
            {
                rectTransform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
                    .OnComplete(() => Hide());
            }
            else
            {
                Hide();
            }
        }

        private void PlayEnterAnimation()
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.zero;
                rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }
        }
    }
}
