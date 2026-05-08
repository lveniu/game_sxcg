using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 结算面板 — 战斗结束后展示奖励，驱动 EventPanel/ShopPanel 子流程
    /// 
    /// 流程：战斗胜利 → EventPanel(30%) → ShopPanel(每3关) → SettlementPanel → 下一关
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  🏆 战斗胜利！ / 💀 战败...  │  结果标题
    /// │  第 3 关                      │
    /// ├──────────────────────────────┤
    /// │  奖励卡牌（3选1）             │
    /// │ ┌─────┐ ┌─────┐ ┌─────┐    │
    /// │ │ 火球 │ │ 治疗 │ │ 护盾 │    │
    /// │ │ 术  │ │ 术  │ │ 术  │    │
    /// │ └─────┘ └─────┘ └─────┘    │
    /// │   ✓选中                       │
    /// ├──────────────────────────────┤
    /// │  获得装备                     │
    /// │  🛡 铁盾 (自动掉落)          │
    /// ├──────────────────────────────┤
    /// │  💰 金币 +50                  │
    /// ├──────────────────────────────┤
    /// │   [下一关] / [返回主菜单]     │
    /// └──────────────────────────────┘
    /// </summary>
    public class SettlementPanel : UIPanel
    {
        [Header("结果标题")]
        public Text resultTitleText;
        public Text levelText;

        [Header("奖励卡牌区")]
        public RectTransform rewardCardsContainer;
        public GameObject rewardCardPrefab;
        public Text rewardLabel;

        [Header("装备展示区")]
        public RectTransform equipmentArea;
        public Text equipmentText;

        [Header("金币")]
        public Text goldRewardText;

        [Header("按钮")]
        public Button nextButton;
        public Text nextButtonText;
        public Button backButton;

        // 奖励卡牌缓存
        private class RewardCardUI
        {
            public RectTransform rect;
            public Text nameText;
            public Text descText;
            public Text rarityText;
            public Image bgImage;
            public CanvasGroup canvasGroup;
            public Button clickButton;
            public CardInstance cardData;
            public int index;
        }

        private List<RewardCardUI> rewardCards = new List<RewardCardUI>();
        private int selectedCardIndex = -1;

        // 稀有度颜色
        private static readonly Color RARITY_WHITE = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color RARITY_BLUE = new Color(0.3f, 0.55f, 1f);
        private static readonly Color RARITY_PURPLE = new Color(0.7f, 0.3f, 1f);
        private static readonly Color RARITY_GOLD = new Color(1f, 0.85f, 0.2f);
        private static readonly Color SELECTED_COLOR = new Color(1f, 0.85f, 0.2f, 0.3f);
        private static readonly Color UNSELECTED_COLOR = new Color(0, 0, 0, 0);

        // 子流程状态
        private enum SettlementPhase
        {
            WaitingEvent,   // 等待随机事件
            WaitingShop,    // 等待商店
            ShowingReward,  // 显示奖励
            Done            // 完成
        }

        private SettlementPhase phase = SettlementPhase.ShowingReward;

        protected override void Awake()
        {
            base.Awake();
            panelId = "Settlement";
        }

        protected override void OnShow()
        {
            nextButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();

            nextButton?.onClick.AddListener(OnNextClicked);
            backButton?.onClick.AddListener(OnBackClicked);

            // 启动结算流程
            StartSettlementFlow();
        }

        protected override void OnHide()
        {
            nextButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            ClearRewardCards();
        }

        // ========== 结算流程 ==========

        private void StartSettlementFlow()
        {
            var gsm = GameStateMachine.Instance;
            if (gsm == null) return;

            bool won = gsm.IsGameWon;
            int level = gsm.CurrentLevel;

            // 显示结果
            ShowBattleResult(won, level);

            if (!won)
            {
                // 失败：显示返回按钮
                phase = SettlementPhase.Done;
                return;
            }

            // 胜利：启动子流程
            // 阶段1：随机事件（30%概率）
            phase = SettlementPhase.WaitingEvent;
            TryShowRandomEvent(level);
        }

        private void TryShowRandomEvent(int level)
        {
            var evt = RandomEventSystem.TriggerEvent(level);
            if (evt != null)
            {
                Debug.Log($"[Settlement] 触发随机事件：{evt.description}");
                ShowSubPanel("Event");
                var eventPanel = NewUIManager.Instance?.GetCurrentPanel<EventPanel>();
                if (eventPanel != null)
                {
                    eventPanel.ShowEventContent(evt);
                    // 事件面板关闭后继续流程
                    eventPanel.OnHidden += OnEventPanelClosed;
                }
                else
                {
                    // 无面板则直接跳过
                    OnEventPanelClosed();
                }
            }
            else
            {
                // 没触发事件，继续商店
                OnEventPanelClosed();
            }
        }

        private void OnEventPanelClosed()
        {
            // 阶段2：商店（每3关出现）
            phase = SettlementPhase.WaitingShop;
            int level = GameStateMachine.Instance?.CurrentLevel ?? 1;

            if (level % 3 == 0)
            {
                Debug.Log($"[Settlement] 第{level}关，显示商店");
                ShowSubPanel("Shop");
                var shopPanel = NewUIManager.Instance?.GetCurrentPanel<ShopPanel>();
                // 商店面板由玩家手动关闭，关闭后继续
                // 通过监听 Hide 事件
                if (shopPanel != null)
                {
                    shopPanel.OnHidden += OnShopPanelClosed;
                }
                else
                {
                    OnShopPanelClosed();
                }
            }
            else
            {
                OnShopPanelClosed();
            }
        }

        private void OnShopPanelClosed()
        {
            // 阶段3：显示奖励
            phase = SettlementPhase.ShowingReward;
            ShowRewards();
        }

        // ========== 结果展示 ==========

        private void ShowBattleResult(bool won, int level)
        {
            // 标题
            if (resultTitleText != null)
            {
                resultTitleText.text = won ? "🏆 战斗胜利！" : "💀 战斗失败...";
                resultTitleText.color = won ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);

                // 标题动画
                resultTitleText.rectTransform.localScale = Vector3.zero;
                resultTitleText.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            }

            // 关卡
            if (levelText != null)
                levelText.text = $"第 {level} 关";

            // 按钮
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(won);
                if (nextButtonText != null)
                    nextButtonText.text = "下一关 →";
            }

            if (backButton != null)
            {
                backButton.gameObject.SetActive(!won);
            }
        }

        // ========== 奖励展示 ==========

        private void ShowRewards()
        {
            ClearRewardCards();
            selectedCardIndex = -1;

            var gsm = GameStateMachine.Instance;
            if (gsm == null) return;

            // 1. 奖励卡牌（3选1）
            ShowRewardCards();

            // 2. 装备掉落
            ShowEquipmentDrop();

            // 3. 金币奖励
            ShowGoldReward();

            // 启用下一关按钮
            if (nextButton != null)
                nextButton.interactable = true;
        }

        private void ShowRewardCards()
        {
            var rewardList = GameData.CreateRewardCards();
            if (rewardList == null || rewardList.Count == 0)
            {
                if (rewardLabel != null) rewardLabel.text = "无奖励卡牌";
                return;
            }

            // 取前3张
            int count = Mathf.Min(3, rewardList.Count);
            if (rewardLabel != null) rewardLabel.text = "选择一张奖励卡牌：";

            for (int i = 0; i < count; i++)
            {
                var card = CreateRewardCard(rewardList[i], i);
                rewardCards.Add(card);
            }

            // 依次入场动画
            for (int i = 0; i < rewardCards.Count; i++)
            {
                var c = rewardCards[i];
                if (c.rect == null) continue;
                c.rect.localScale = Vector3.zero;
                c.rect.DOScale(Vector3.one, 0.35f)
                    .SetDelay(i * 0.12f)
                    .SetEase(Ease.OutBack);
            }
        }

        private RewardCardUI CreateRewardCard(CardInstance cardData, int index)
        {
            var card = new RewardCardUI
            {
                cardData = cardData,
                index = index
            };

            if (rewardCardPrefab != null)
            {
                var go = Instantiate(rewardCardPrefab, rewardCardsContainer);
                card.rect = go.GetComponent<RectTransform>();

                var nameObj = go.transform.Find("NameText");
                card.nameText = nameObj?.GetComponent<Text>();

                var descObj = go.transform.Find("DescText");
                card.descText = descObj?.GetComponent<Text>();

                var rarityObj = go.transform.Find("RarityText");
                card.rarityText = rarityObj?.GetComponent<Text>();

                card.bgImage = go.GetComponent<Image>();
                card.clickButton = go.GetComponent<Button>();
                card.canvasGroup = go.GetComponent<CanvasGroup>();
            }
            else
            {
                // 程序化创建
                var go = new GameObject($"RewardCard_{index}");
                go.transform.SetParent(rewardCardsContainer, false);
                card.rect = go.AddComponent<RectTransform>();
                card.rect.sizeDelta = new Vector2(180, 220);

                card.bgImage = go.AddComponent<Image>();
                card.bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

                var outline = go.AddComponent<Outline>();
                outline.effectColor = GetRarityColor(cardData.Data.rarity);
                outline.effectDistance = new Vector2(2, -2);

                card.clickButton = go.AddComponent<Button>();
                card.canvasGroup = go.AddComponent<CanvasGroup>();

                // 名称
                var nameGo = new GameObject("NameText");
                nameGo.transform.SetParent(go.transform, false);
                var nameRect = nameGo.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0.05f, 0.6f);
                nameRect.anchorMax = new Vector2(0.95f, 0.85f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                card.nameText = nameGo.AddComponent<Text>();
                card.nameText.fontSize = 16;
                card.nameText.alignment = TextAnchor.MiddleCenter;
                card.nameText.color = GetRarityColor(cardData.Data.rarity);

                // 描述
                var descGo = new GameObject("DescText");
                descGo.transform.SetParent(go.transform, false);
                var descRect = descGo.AddComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0.05f, 0.15f);
                descRect.anchorMax = new Vector2(0.95f, 0.55f);
                descRect.offsetMin = descRect.offsetMax = Vector2.zero;
                card.descText = descGo.AddComponent<Text>();
                card.descText.fontSize = 12;
                card.descText.alignment = TextAnchor.MiddleCenter;
                card.descText.color = new Color(0.8f, 0.8f, 0.8f);
                card.descText.supportRichText = true;

                // 稀有度
                var rarGo = new GameObject("RarityText");
                rarGo.transform.SetParent(go.transform, false);
                var rarRect = rarGo.AddComponent<RectTransform>();
                rarRect.anchorMin = new Vector2(0.05f, 0.02f);
                rarRect.anchorMax = new Vector2(0.95f, 0.14f);
                rarRect.offsetMin = rarRect.offsetMax = Vector2.zero;
                card.rarityText = rarGo.AddComponent<Text>();
                card.rarityText.fontSize = 11;
                card.rarityText.alignment = TextAnchor.MiddleCenter;
            }

            // 填充数据
            if (card.nameText != null)
                card.nameText.text = cardData.Data.cardName;

            if (card.descText != null)
                card.descText.text = cardData.Data.description;

            if (card.rarityText != null)
            {
                card.rarityText.text = GetRarityLabel(cardData.Data.rarity);
                card.rarityText.color = GetRarityColor(cardData.Data.rarity);
            }

            // 点击选择
            if (card.clickButton != null)
            {
                card.clickButton.onClick.RemoveAllListeners();
                card.clickButton.onClick.AddListener(() => OnRewardCardClicked(index));
            }

            return card;
        }

        private void OnRewardCardClicked(int index)
        {
            // 取消旧选择
            if (selectedCardIndex >= 0 && selectedCardIndex < rewardCards.Count)
            {
                var old = rewardCards[selectedCardIndex];
                if (old.bgImage != null)
                    old.bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                if (old.rect != null)
                    old.rect.DOScale(Vector3.one, 0.2f);
            }

            selectedCardIndex = index;

            // 高亮新选择
            if (index >= 0 && index < rewardCards.Count)
            {
                var selected = rewardCards[index];
                if (selected.bgImage != null)
                    selected.bgImage.color = SELECTED_COLOR;
                if (selected.rect != null)
                    selected.rect.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutBack);
            }
        }

        private void ShowEquipmentDrop()
        {
            // MVP：显示自动掉落的装备信息
            if (equipmentArea != null)
                equipmentArea.gameObject.SetActive(false);

            // TODO: 对接后端装备掉落系统
            // 预留：var drop = LootSystem.GetLevelDrop(level);
            // equipmentText.text = $"🛡 {drop.equipmentName}";
        }

        private void ShowGoldReward()
        {
            if (goldRewardText == null) return;

            int level = GameStateMachine.Instance?.CurrentLevel ?? 1;
            // 基础金币奖励 = 20 + level * 10
            int goldReward = 20 + level * 10;

            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                inventory.AddGold(goldReward);
                goldRewardText.text = $"💰 金币 +{goldReward}（余额：{inventory.Gold}）";
            }
            else
            {
                goldRewardText.text = $"💰 金币 +{goldReward}";
            }

            // 金币动画
            goldRewardText.rectTransform.localScale = Vector3.zero;
            goldRewardText.rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        // ========== 按钮事件 ==========

        private void OnNextClicked()
        {
            // 如果选中了奖励卡牌，添加到牌组
            if (selectedCardIndex >= 0 && selectedCardIndex < rewardCards.Count)
            {
                var selectedCard = rewardCards[selectedCardIndex];
                if (selectedCard.cardData != null)
                {
                    CardDeck.Instance?.AddCard(selectedCard.cardData);
                    Debug.Log($"[Settlement] 获得卡牌：{selectedCard.cardData.Data.cardName}");
                }
            }

            // 跳转下一关（由状态机处理）
            GameStateMachine.Instance?.NextState();
        }

        private void OnBackClicked()
        {
            // 返回主菜单
            GameStateMachine.Instance?.ChangeState(GameState.MainMenu);
        }

        // ========== 子面板辅助 ==========

        private void ShowSubPanel(string panelId)
        {
            NewUIManager.Instance?.ShowSubPanel(panelId);
        }

        // ========== 清理 ==========

        private void ClearRewardCards()
        {
            foreach (var card in rewardCards)
            {
                if (card.rect != null) Destroy(card.rect.gameObject);
            }
            rewardCards.Clear();
        }

        // ========== 工具方法 ==========

        private static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => RARITY_WHITE,
                CardRarity.Rare => RARITY_BLUE,
                CardRarity.Epic => RARITY_PURPLE,
                CardRarity.Legendary => RARITY_GOLD,
                _ => Color.white
            };
        }

        private static string GetRarityLabel(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => "普通",
                CardRarity.Rare => "稀有",
                CardRarity.Epic => "史诗",
                CardRarity.Legendary => "传说",
                _ => ""
            };
        }
    }
}
