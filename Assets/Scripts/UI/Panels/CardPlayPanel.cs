using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 卡牌系统面板 — 手牌区（上半屏）
    /// 
    /// 竖屏720x1280布局（Inspector拖拽绑定）：
    /// ┌──────────────────────────┐
    /// │ 顶部信息栏：人口/骰子点数/组合 │
    /// ├──────────────────────────┤
    /// │                          │
    /// │    手牌区（横向排列）      │
    /// │  区分：英雄/属性/战斗/进化 │
    /// │                          │
    /// ├──────────────────────────┤
    /// │ 操作按钮栏               │
    /// │ 召唤/打出属性/打出战斗/合成│
    /// └──────────────────────────┘
    /// 
    /// 交互流程：
    /// 1. OnShow → 刷新手牌、读取骰子点数
    /// 2. 点击手牌 → 选中高亮，根据类型启用不同操作按钮
    /// 3. 英雄卡 → 点击"召唤"（消耗骰子点数）
    /// 4. 属性卡 → 点击"打出"（免费，永久生效）
    /// 5. 战斗卡 → 点击"打出"（消耗点数，本场生效）
    /// 6. 两张同名同星 → 点击"合成"（2合1升星）
    /// </summary>
    public class CardPlayPanel : UIPanel
    {
        // ========== 顶部信息栏 ==========
        [Header("顶部信息")]
        public Text populationText;
        public Text dicePointsText;
        public Text comboText;

        // ========== 手牌区 ==========
        [Header("手牌容器")]
        public RectTransform handCardContainer;
        public GameObject cardItemPrefab;

        // ========== 卡牌详情 ==========
        [Header("卡牌详情区")]
        public RectTransform detailPanel;
        public Image detailIcon;
        public Text detailNameText;
        public Text detailTypeText;
        public Text detailCostText;
        public Text detailDescText;
        public Text detailStarText;
        public GameObject detailComboTag;
        public Text detailComboText;

        // ========== 操作按钮 ==========
        [Header("操作按钮")]
        public Button summonButton;
        public Button playAttributeButton;
        public Button playBattleButton;
        public Button mergeButton;
        public Button finishCardPlayButton; // 完成出牌 → 进入站位阶段
        public Text summonButtonText;
        public Text playAttributeButtonText;
        public Text TextplayBattleButtonText;
        public Text mergeButtonText;

        // ========== 合成区 ==========
        [Header("合成区")]
        public RectTransform mergeZone;
        public Image mergeSlot1;
        public Image mergeSlot2;
        public Image mergeResultSlot;
        public GameObject mergeEffectPrefab;

        // ========== 内部状态 ==========
        private List<CardInstance> handCards = new List<CardInstance>();
        private CardInstance selectedCard;
        private CardInstance mergeCard1;
        private CardInstance mergeCard2;
        private List<RectTransform> cardItems = new List<RectTransform>();
        private DiceCombination currentCombo;
        private int remainingPoints;

        // 卡牌颜色配置
        private static readonly Color HERO_COLOR = new Color(0.2f, 0.6f, 1f);      // 蓝色
        private static readonly Color ATTRIBUTE_COLOR = new Color(0.3f, 0.9f, 0.3f); // 绿色
        private static readonly Color BATTLE_COLOR = new Color(1f, 0.4f, 0.2f);      // 橙色
        private static readonly Color EVOLUTION_COLOR = new Color(0.8f, 0.3f, 1f);   // 紫色

        protected override void Awake()
        {
            base.Awake();
            panelId = "CardPlay";
        }

        protected override void OnShow()
        {
            // 清除旧监听器
            summonButton?.onClick.RemoveAllListeners();
            playAttributeButton?.onClick.RemoveAllListeners();
            playBattleButton?.onClick.RemoveAllListeners();
            mergeButton?.onClick.RemoveAllListeners();
            finishCardPlayButton?.onClick.RemoveAllListeners();

            // 绑定按钮事件
            summonButton?.onClick.AddListener(OnSummonClicked);
            playAttributeButton?.onClick.AddListener(OnPlayAttributeClicked);
            playBattleButton?.onClick.AddListener(OnPlayBattleClicked);
            mergeButton?.onClick.AddListener(OnMergeClicked);
            finishCardPlayButton?.onClick.AddListener(OnFinishCardPlayClicked);

            // 初始化状态
            selectedCard = null;
            mergeCard1 = null;
            mergeCard2 = null;

            // 禁用所有操作按钮
            SetAllButtonsInteractable(false);

            // 隐藏详情面板
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
            if (mergeZone != null) mergeZone.gameObject.SetActive(false);

            // 读取当前骰子组合和点数
            RefreshDiceInfo();

            // 刷新手牌
            RefreshHandCards();

            // 更新顶部信息
            UpdateTopBar();

            // 手牌入场动画
            PlayHandEnterAnimation();
        }

        protected override void OnHide()
        {
            summonButton?.onClick.RemoveAllListeners();
            playAttributeButton?.onClick.RemoveAllListeners();
            playBattleButton?.onClick.RemoveAllListeners();
            mergeButton?.onClick.RemoveAllListeners();

            // 清理卡牌动画
            foreach (var item in cardItems)
            {
                if (item != null) item.DOKill();
            }
            cardItems.Clear();
        }

        // ========== 刷新骰子信息 ==========
        private void RefreshDiceInfo()
        {
            // 从 GameManager 获取当前骰子组合
            var combo = GameManager.Instance?.GetCurrentDiceCombination();
            currentCombo = combo ?? new DiceCombination { Type = DiceCombinationType.None };

            // 从 DiceRoller 获取剩余点数
            var roller = GameManager.Instance?.GetDiceRoller();
            if (roller != null)
            {
                int[] values = roller.GetCurrentValues();
                remainingPoints = 0;
                foreach (var v in values) remainingPoints += v;
            }
            else
            {
                remainingPoints = 0;
            }
        }

        // ========== 手牌渲染 ==========
        private void RefreshHandCards()
        {
            // 清除旧卡牌
            foreach (Transform child in handCardContainer)
            {
                Destroy(child.gameObject);
            }
            cardItems.Clear();

            // 从后端获取手牌
            var deck = CardDeck.Instance;
            if (deck == null || handCardContainer == null || cardItemPrefab == null)
            {
                Debug.LogWarning("[CardPlayPanel] CardDeck或手牌容器未绑定");
                return;
            }

            handCards = new List<CardInstance>(deck.handCards);

            for (int i = 0; i < handCards.Count; i++)
            {
                var card = handCards[i];
                var go = Instantiate(cardItemPrefab, handCardContainer);
                var rect = go.GetComponent<RectTransform>();
                cardItems.Add(rect);

                // 卡牌名称
                var nameText = FindChildText(go, "NameText");
                if (nameText != null) nameText.text = card.CardName;

                // 卡牌类型标签
                var typeText = FindChildText(go, "TypeText");
                if (typeText != null) typeText.text = GetCardTypeLabel(card.Type);

                // 费用
                var costText = FindChildText(go, "CostText");
                if (costText != null)
                {
                    costText.text = card.Type == CardType.Attribute ? "免费" : $"{card.Cost}点";
                }

                // 星级
                var starText = FindChildText(go, "StarText");
                if (starText != null) starText.text = GetStarString(card.StarLevel);

                // 卡牌边框颜色（按类型区分）
                var borderImage = FindChildImage(go, "Border");
                if (borderImage != null) borderImage.color = GetCardColor(card.Type);

                // 稀有度底色
                var bgImage = FindChildImage(go, "Background");
                if (bgImage != null) bgImage.color = GetRarityColor(card.Data.rarity);

                // 持续性标签
                var durationTag = FindChildText(go, "DurationTag");
                if (durationTag != null)
                {
                    if (card.Type == CardType.Attribute)
                    {
                        durationTag.text = "本局永久";
                        durationTag.color = Color.green;
                    }
                    else if (card.Type == CardType.Battle)
                    {
                        durationTag.text = "本场";
                        durationTag.color = Color.yellow;
                    }
                    else
                    {
                        durationTag.text = "";
                    }
                }

                // 图标
                var iconImage = FindChildImage(go, "Icon");
                if (iconImage != null && card.Icon != null) iconImage.sprite = card.Icon;

                // 点击选中
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    var c = card;
                    btn.onClick.AddListener(() => OnCardClicked(c));
                }

                // 初始位置（入场动画用）
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -100f);
                rect.localScale = Vector3.one * 0.8f;

                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        // ========== 卡牌选中 ==========
        private void OnCardClicked(CardInstance card)
        {
            if (selectedCard == card)
            {
                // 再次点击取消选中
                DeselectCard();
                return;
            }

            selectedCard = card;
            UpdateCardHighlights();
            ShowCardDetail(card);
            UpdateButtonStates();
        }

        private void DeselectCard()
        {
            selectedCard = null;
            UpdateCardHighlights();
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
            SetAllButtonsInteractable(false);
        }

        /// <summary>
        /// 高亮选中的卡牌，其他半透明
        /// </summary>
        private void UpdateCardHighlights()
        {
            for (int i = 0; i < cardItems.Count && i < handCards.Count; i++)
            {
                var item = cardItems[i];
                if (item == null) continue;

                bool isSelected = (handCards[i] == selectedCard);
                var cg = item.GetComponent<CanvasGroup>();

                item.DOKill();

                if (isSelected)
                {
                    item.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutQuad);
                    item.DOAnchorPosY(30f, 0.2f).SetEase(Ease.OutQuad); // 上移突出
                    if (cg != null) cg.DOFade(1f, 0.15f);
                }
                else
                {
                    item.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutQuad);
                    item.DOAnchorPosY(0f, 0.2f).SetEase(Ease.OutQuad);
                    if (cg != null) cg.DOFade(selectedCard != null ? 0.5f : 1f, 0.15f);
                }
            }
        }

        /// <summary>
        /// 显示卡牌详情面板
        /// </summary>
        private void ShowCardDetail(CardInstance card)
        {
            if (detailPanel == null) return;
            detailPanel.gameObject.SetActive(true);

            if (detailNameText != null) detailNameText.text = card.CardName;
            if (detailTypeText != null) detailTypeText.text = GetCardTypeLabel(card.Type);
            if (detailCostText != null)
                detailCostText.text = card.Type == CardType.Attribute ? "免费" : $"消耗{card.Cost}点";
            if (detailDescText != null) detailDescText.text = card.GetEffectDescription();
            if (detailStarText != null) detailStarText.text = GetStarString(card.StarLevel);
            if (detailIcon != null && card.Icon != null) detailIcon.sprite = card.Icon;

            // 骰子联动标签
            if (detailComboTag != null)
            {
                bool hasCombo = card.Data.requiredCombo != DiceCombinationType.None;
                detailComboTag.SetActive(hasCombo);
                if (hasCombo && detailComboText != null)
                {
                    detailComboText.text = $"联动:{card.Data.requiredCombo} ×{card.Data.comboMultiplier}";
                }
            }

            // 详情面板入场动画
            detailPanel.anchoredPosition = new Vector2(0f, -50f);
            detailPanel.DOAnchorPosY(0f, 0.25f).SetEase(Ease.OutQuad);

            var cg = detailPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.DOFade(1f, 0.2f);
            }
        }

        // ========== 按钮状态联动 ==========
        private void UpdateButtonStates()
        {
            SetAllButtonsInteractable(false);

            if (selectedCard == null) return;

            var deck = CardDeck.Instance;
            if (deck == null) return;

            switch (selectedCard.Type)
            {
                case CardType.Hero:
                    // 英雄卡 → 召唤按钮
                    bool canSummon = deck.HasSpace && remainingPoints >= GetActualCost(selectedCard);
                    if (summonButton != null)
                    {
                        summonButton.interactable = canSummon;
                        if (summonButtonText != null)
                            summonButtonText.text = canSummon
                                ? $"召唤 ({GetActualCost(selectedCard)}点)"
                                : !deck.HasSpace ? "人口已满" : "点数不足";
                    }
                    break;

                case CardType.Attribute:
                    // 属性卡 → 免费打出
                    if (playAttributeButton != null)
                    {
                        playAttributeButton.interactable = true;
                        if (playAttributeButtonText != null)
                            playAttributeButtonText.text = $"打出（免费·永久）";
                    }
                    break;

                case CardType.Battle:
                    // 战斗卡 → 需要点数
                    bool canPlay = remainingPoints >= selectedCard.Cost;
                    if (playBattleButton != null)
                    {
                        playBattleButton.interactable = canPlay;
                        if (TextplayBattleButtonText != null)
                            TextplayBattleButtonText.text = canPlay
                                ? $"打出（{selectedCard.Cost}点·本场）"
                                : "点数不足";
                    }
                    break;

                case CardType.Evolution:
                    // 进化卡 → 暂不在此面板处理（需要选择场上英雄）
                    if (playAttributeButton != null)
                    {
                        playAttributeButton.interactable = true;
                        if (playAttributeButtonText != null)
                            playAttributeButtonText.text = "选择英雄进化";
                    }
                    break;
            }

            // 合成按钮：检查是否有同名同星可合成
            bool canMerge = false;
            if (selectedCard != null && selectedCard.StarLevel < 3)
            {
                var mergeable = deck.FindMergeableCards(selectedCard.CardName, selectedCard.StarLevel);
                canMerge = mergeable.Count >= 2;
            }
            if (mergeButton != null)
            {
                mergeButton.interactable = canMerge;
                if (mergeButtonText != null)
                    mergeButtonText.text = canMerge ? "合成 (2→升星)" : "无可合成卡牌";
            }
        }

        // ========== 操作逻辑 ==========

        /// <summary>召唤英雄</summary>
        private void OnSummonClicked()
        {
            if (selectedCard == null || selectedCard.Type != CardType.Hero) return;

            var deck = CardDeck.Instance;
            if (deck == null) return;

            // 扣除骰子点数（MVP：标记已消耗）
            int cost = GetActualCost(selectedCard);
            if (remainingPoints < cost) return;

            // 从手牌移除英雄卡
            deck.RemoveCard(selectedCard);

            // 根据卡牌名称映射到HeroData
            var heroData = ResolveHeroData(selectedCard.CardName);
            if (heroData != null)
            {
                var hero = deck.SummonHero(heroData);
                if (hero != null)
                {
                    Debug.Log($"[CardPlay] 召唤英雄：{heroData.heroName}，消耗{cost}点");
                    remainingPoints -= cost; // 本地扣除点数
                }
            }

            // 播放召唤动画
            PlayCardPlayAnimation(selectedCard, "召唤");

            selectedCard = null;
            RefreshAfterAction();
        }

        /// <summary>
        /// 根据卡牌名称解析对应的HeroData
        /// TODO: 后端应在CardData上增加heroData引用字段，MVP阶段用名称映射
        /// </summary>
        private HeroData ResolveHeroData(string cardName)
        {
            return cardName switch
            {
                "战士" => GameData.CreateWarriorHero(),
                "法师" => GameData.CreateMageHero(),
                "刺客" => GameData.CreateAssassinHero(),
                _ => GameData.CreateWarriorHero() // 默认战士
            };
        }

        /// <summary>打出属性卡</summary>
        private void OnPlayAttributeClicked()
        {
            if (selectedCard == null) return;

            var deck = CardDeck.Instance;
            if (deck == null) return;

            if (selectedCard.Type == CardType.Attribute)
            {
                deck.PlayAttributeCard(selectedCard);
                PlayCardPlayAnimation(selectedCard, "属性永久生效");
            }
            else if (selectedCard.Type == CardType.Evolution)
            {
                // 进化卡：选择场上第一个英雄进化
                // TODO: Hero.IsEvolved/Evolve() 方法待后端补充（CTO审查提到的编译错误）
                // MVP阶段先对场上第一个英雄使用
                var heroes = deck.fieldHeroes;
                if (heroes.Count > 0)
                {
                    var target = heroes[0];
                    deck.PlayEvolutionCard(selectedCard, target);
                    PlayCardPlayAnimation(selectedCard, "进化");
                }
                else
                {
                    Debug.Log("[CardPlay] 场上没有英雄可进化");
                    return; // 不消耗卡牌
                }
            }

            selectedCard = null;
            RefreshAfterAction();
        }

        /// <summary>打出战斗卡</summary>
        private void OnPlayBattleClicked()
        {
            if (selectedCard == null || selectedCard.Type != CardType.Battle) return;

            var deck = CardDeck.Instance;
            if (deck == null) return;

            if (remainingPoints < selectedCard.Cost) return;

            // 打出战斗卡（联动当前骰子组合）
            bool hasCombo = deck.PlayBattleCard(selectedCard, currentCombo);

            string label = hasCombo ? "打出·骰子联动！" : "打出";
            PlayCardPlayAnimation(selectedCard, label);

            selectedCard = null;
            RefreshAfterAction();
        }

        /// <summary>合成（2合1升星）</summary>
        private void OnMergeClicked()
        {
            if (selectedCard == null) return;

            var deck = CardDeck.Instance;
            if (deck == null) return;

            bool success = deck.TryMergeCards(selectedCard.CardName, selectedCard.StarLevel);
            if (success)
            {
                PlayMergeAnimation(selectedCard);
                Debug.Log($"[CardPlay] 合成成功：{selectedCard.CardName} → {selectedCard.StarLevel + 1}星");
            }

            selectedCard = null;
            mergeCard1 = null;
            mergeCard2 = null;
            RefreshAfterAction();
        }

        // ========== 动画 ==========

        /// <summary>手牌入场动画</summary>
        private void PlayHandEnterAnimation()
        {
            for (int i = 0; i < cardItems.Count; i++)
            {
                if (cardItems[i] == null) continue;
                float delay = i * 0.08f;

                cardItems[i].DOKill();
                cardItems[i].DOAnchorPosY(0f, 0.35f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack);
                cardItems[i].DOScale(Vector3.one, 0.3f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack);

                var cg = cardItems[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.DOFade(1f, 0.25f).SetDelay(delay);
            }
        }

        /// <summary>打出/召唤动画 — 卡牌飞出并消失</summary>
        private void PlayCardPlayAnimation(CardInstance card, string label)
        {
            int index = handCards.IndexOf(card);
            if (index < 0 || index >= cardItems.Count) return;

            var item = cardItems[index];
            if (item == null) return;

            item.DOKill();

            // 卡牌飞向上方消失
            item.DOAnchorPosY(300f, 0.4f).SetEase(Ease.InBack);
            item.DOScale(Vector3.one * 0.5f, 0.4f).SetEase(Ease.InBack);

            var cg = item.GetComponent<CanvasGroup>();
            if (cg != null) cg.DOFade(0f, 0.35f);
        }

        /// <summary>合成动画</summary>
        private void PlayMergeAnimation(CardInstance card)
        {
            if (mergeZone == null) return;
            mergeZone.gameObject.SetActive(true);

            // 两张卡飞向合成区
            var mergeable = CardDeck.Instance?.FindMergeableCards(card.CardName, card.StarLevel);
            if (mergeable == null || mergeable.Count < 2) return;

            // 简化：显示合成光效
            if (mergeEffectPrefab != null)
            {
                var effect = Instantiate(mergeEffectPrefab, mergeZone);
                Destroy(effect, 1.5f);
            }

            // 合成区动画
            mergeZone.DOScale(Vector3.one * 1.2f, 0.2f).SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (mergeZone != null)
                        mergeZone.DOScale(Vector3.one, 0.2f).SetEase(Ease.InOutQuad)
                            .SetLink(gameObject);
                    DOVirtual.DelayedCall(0.5f, () =>
                    {
                        if (mergeZone != null) mergeZone.gameObject.SetActive(false);
                    });
                });
        }

        // ========== 刷新 ==========

        /// <summary>完成出牌 → 进入站位阶段</summary>
        private void OnFinishCardPlayClicked()
        {
            Debug.Log("[CardPlay] 完成出牌，进入站位阶段");
            NewUIManager.Instance?.EnterPositioningPhase();
        }

        private void RefreshAfterAction()
        {
            // 刷新骰子信息（消耗后点数可能变化）
            RefreshDiceInfo();

            // 刷新手牌
            RefreshHandCards();
            PlayHandEnterAnimation();

            // 更新顶部信息
            UpdateTopBar();

            // 隐藏详情
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        }

        private void UpdateTopBar()
        {
            var deck = CardDeck.Instance;

            // 人口
            if (populationText != null && deck != null)
                populationText.text = $"人口: {deck.CurrentPopulation}/{deck.maxPopulation}";

            // 骰子点数
            if (dicePointsText != null)
                dicePointsText.text = $"骰子点数: {remainingPoints}";

            // 骰子组合
            if (comboText != null && currentCombo != null)
                comboText.text = currentCombo.Type != DiceCombinationType.None
                    ? $"组合: {currentCombo.Description}"
                    : "无组合";
        }

        // ========== 工具方法 ==========

        private void SetAllButtonsInteractable(bool interactable)
        {
            if (summonButton != null) summonButton.interactable = interactable;
            if (playAttributeButton != null) playAttributeButton.interactable = interactable;
            if (playBattleButton != null) playBattleButton.interactable = interactable;
            if (mergeButton != null) mergeButton.interactable = interactable;
        }

        private int GetActualCost(CardInstance card)
        {
            var deck = CardDeck.Instance;
            if (deck == null) return card.Cost;
            return Mathf.Max(1, card.Cost - deck.SummonCostReduction);
        }

        private static string GetCardTypeLabel(CardType type)
        {
            return type switch
            {
                CardType.Hero => "英雄",
                CardType.Attribute => "属性",
                CardType.Battle => "战斗",
                CardType.Evolution => "进化",
                _ => "未知"
            };
        }

        private static Color GetCardColor(CardType type)
        {
            return type switch
            {
                CardType.Hero => HERO_COLOR,
                CardType.Attribute => ATTRIBUTE_COLOR,
                CardType.Battle => BATTLE_COLOR,
                CardType.Evolution => EVOLUTION_COLOR,
                _ => Color.white
            };
        }

        private static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.White => new Color(0.85f, 0.85f, 0.85f),
                CardRarity.Blue => new Color(0.3f, 0.5f, 0.9f),
                CardRarity.Purple => new Color(0.6f, 0.2f, 0.9f),
                CardRarity.Gold => new Color(1f, 0.8f, 0.2f),
                _ => Color.gray
            };
        }

        private static string GetStarString(int starLevel)
        {
            return starLevel switch
            {
                1 => "★",
                2 => "★★",
                3 => "★★★",
                _ => ""
            };
        }

        private Text FindChildText(GameObject parent, string childName)
        {
            var t = parent.transform.Find(childName);
            return t?.GetComponent<Text>();
        }

        private Image FindChildImage(GameObject parent, string childName)
        {
            var t = parent.transform.Find(childName);
            return t?.GetComponent<Image>();
        }
    }
}
