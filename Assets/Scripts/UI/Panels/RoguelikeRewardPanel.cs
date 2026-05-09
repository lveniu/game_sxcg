using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 肉鸽三选一奖励面板增强版 — FE-05
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │      🎁 选择一项奖励          │  标题
    /// ├──────────────────────────────┤
    /// │  ┌──────┐ ┌──────┐ ┌──────┐ │
    /// │  │ 👤   │ │ 🎲   │ │ 🏺   │ │  卡片正面
    /// │  │新单位 │ │骰子  │ │遗物  │ │  (翻转后显示)
    /// │  │★★★   │ │强化  │ │★★   │ │
    /// │  │描述.. │ │描述..│ │描述..│ │
    /// │  └──────┘ └──────┘ └──────┘ │
    /// │                              │
    /// │  [确认选择]                   │  确认按钮
    └──────────────────────────────┘
    /// 
    /// 卡片翻转动画：
    /// - 入场：从背面翻入（Y轴180°→0°）
    /// - 选中：翻转放大（scale 1→1.2），其余灰化缩小
    /// - 确认：选中卡片飞出
    /// 
    /// 遗物详情弹窗（点击遗物卡片弹出）：
    /// ┌──────────────────────────────┐
    /// │    🏺 铁壁盾牌 ★★★           │
    /// │    [史诗] 防御力+15%          │
    /// │    坚固的铁盾，大幅提升防御    │
    /// │                              │
    /// │    [选择此奖励]               │
    /// └──────────────────────────────┘
    /// </summary>
    public class RoguelikeRewardPanel : UIPanel
    {
        [Header("卡片容器")]
        public RectTransform cardsContainer;
        public GameObject rewardCardPrefab;

        // 3个卡片的根节点
        [Header("卡片槽位（Inspector可选绑定）")]
        public RectTransform cardSlot0;
        public RectTransform cardSlot1;
        public RectTransform cardSlot2;

        [Header("标题")]
        public Text titleText;

        [Header("确认按钮")]
        public Button confirmButton;
        public Text confirmButtonText;
        public RectTransform confirmButtonRect;

        [Header("遗物详情弹窗")]
        public RectTransform relicDetailPopup;
        public Text relicDetailNameText;
        public Text relicDetailRarityText;
        public Text relicDetailDescText;
        public Text relicDetailEffectText;
        public Image relicDetailIcon;
        public Image relicDetailBorder;
        public Button relicDetailSelectButton;
        public Button relicDetailCloseButton;

        [Header("动画参数")]
        public float flipDuration = 0.5f;
        public float cardSpacing = 20f;
        public float cardWidth = 200f;
        public float cardHeight = 300f;
        public float entryDelay = 0.15f;
        public float selectedScale = 1.15f;
        public float dimmedScale = 0.85f;
        public float dimmedAlpha = 0.4f;

        // FE-05: 浮动动画参数
        [Header("浮动动画")]
        public float floatAmplitude = 8f;       // 浮动幅度(px)
        public float floatDuration = 1.5f;      // 一个浮动周期(秒)
        public Ease floatEase = Ease.InOutSine; // 浮动缓动

        // 当前奖励数据
        private List<RewardOption> currentRewards = new List<RewardOption>();

        // 卡片运行时数据
        private class RewardCard
        {
            public RectTransform rect;
            public RectTransform cardFront;
            public RectTransform cardBack;
            public Text nameText;
            public Text descText;
            public Text typeLabelText;
            public Text rarityText;
            public Image typeIcon;
            public Image borderImage;
            public CanvasGroup canvasGroup;
            public Button button;
            public int index;
            public bool isFlipped;
            public Vector2 originalPosition;  // 卡片初始位置（浮动基准）
            public Tweener floatTween;         // 浮动动画引用（用于停止）
        }
        private List<RewardCard> cards = new List<RewardCard>();

        // 选择状态
        private int selectedIndex = -1;
        private bool isConfirmed = false;

        // 遗物详情弹窗关联的卡片索引
        private int relicDetailCardIndex = -1;

        // 颜色常量
        private static readonly Color CARD_BG_NORMAL = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        private static readonly Color CARD_BG_SELECTED = new Color(0.15f, 0.2f, 0.3f, 0.98f);
        private static readonly Color CARD_BACK_COLOR = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        private static readonly Color CONFIRM_ENABLED = new Color(0.2f, 0.7f, 0.4f);
        private static readonly Color CONFIRM_DISABLED = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        protected override void Awake()
        {
            base.Awake();
            panelId = "RoguelikeReward";
        }

        protected override void OnShow()
        {
            // 绑定确认按钮
            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirmClicked);

            // 绑定遗物弹窗按钮
            relicDetailSelectButton?.onClick.RemoveAllListeners();
            relicDetailSelectButton?.onClick.AddListener(OnRelicDetailSelectClicked);
            relicDetailCloseButton?.onClick.RemoveAllListeners();
            relicDetailCloseButton?.onClick.AddListener(OnRelicDetailCloseClicked);

            // 初始化确认按钮
            if (confirmButton != null) confirmButton.interactable = false;
            if (confirmButtonRect != null) confirmButtonRect.localScale = Vector3.zero;
            if (confirmButtonText != null) confirmButtonText.text = "确认选择";

            // 标题
            if (titleText != null)
            {
                titleText.text = "🎁 选择一项奖励";
                titleText.color = Color.white;
            }

            // 隐藏遗物弹窗
            if (relicDetailPopup != null) relicDetailPopup.gameObject.SetActive(false);

            // 重置选择状态
            selectedIndex = -1;
            isConfirmed = false;

            // 生成奖励
            GenerateAndDisplayRewards();
        }

        protected override void OnHide()
        {
            confirmButton?.onClick.RemoveAllListeners();
            relicDetailSelectButton?.onClick.RemoveAllListeners();
            relicDetailCloseButton?.onClick.RemoveAllListeners();

            // 清理卡片
            ClearCards();
            currentRewards.Clear();
        }

        // ========== 奖励生成与显示 ==========

        private void GenerateAndDisplayRewards()
        {
            currentRewards.Clear();
            ClearCards();

            if (RoguelikeGameManager.Instance == null)
            {
                Debug.LogError("[奖励面板] RoguelikeGameManager实例不存在！");
                return;
            }

            currentRewards = RoguelikeGameManager.Instance.GenerateRewards();

            if (currentRewards == null || currentRewards.Count == 0)
            {
                Debug.LogWarning("[奖励面板] 未生成任何奖励选项");
                return;
            }

            // 创建并显示卡片
            StartCoroutine(AnimateCardEntry());
        }

        /// <summary>
        /// FE-05.1: 卡片入场翻转动画（依次翻入）+ 浮动循环
        /// </summary>
        private IEnumerator AnimateCardEntry()
        {
            RectTransform[] slots = { cardSlot0, cardSlot1, cardSlot2 };

            for (int i = 0; i < currentRewards.Count && i < 3; i++)
            {
                var reward = currentRewards[i];
                var slot = slots[i];

                // 创建卡片
                var card = CreateRewardCard(reward, i, slot);
                cards.Add(card);

                // 初始状态：背面朝上（Y旋转180度）
                card.rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
                card.rect.localScale = Vector3.one * 0.3f;
                card.rect.anchoredPosition = new Vector2(0f, 200f);

                // 等待一下形成依次翻入效果
                yield return new WaitForSeconds(entryDelay);

                // 翻入动画：从背面翻到正面
                Sequence flipSeq = DOTween.Sequence();
                flipSeq.Append(card.rect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
                flipSeq.Join(card.rect.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutCubic));
                // 翻转：先到90度（背面消失），再到0度（正面出现）
                flipSeq.Insert(0.15f, card.rect.DORotate(new Vector3(0f, 90f, 0f), 0.15f).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        // 切换到正面显示
                        if (card.cardBack != null) card.cardBack.gameObject.SetActive(false);
                        if (card.cardFront != null) card.cardFront.gameObject.SetActive(true);
                    }));
                flipSeq.Insert(0.3f, card.rect.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutQuad));

                card.isFlipped = true;

                // 记录初始位置
                card.originalPosition = Vector2.zero;

                // 翻入完成后启动浮动动画（延迟按index错开）
                int capturedI = i;
                flipSeq.OnComplete(() => StartFloatAnimation(cards[capturedI]));
            }
        }

        /// <summary>
        /// FE-05: 启动卡片浮动循环动画
        /// </summary>
        private void StartFloatAnimation(RewardCard card)
        {
            if (card.rect == null) return;

            // 用相位偏移让每张卡片浮动节奏错开
            float phaseOffset = card.index * 0.4f;
            float yBase = card.originalPosition.y;

            card.floatTween = card.rect.DOAnchorPosY(yBase + floatAmplitude, floatDuration)
                .SetEase(floatEase)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(phaseOffset);
        }

        /// <summary>
        /// FE-05: 停止单张卡片浮动
        /// </summary>
        private void StopFloatAnimation(RewardCard card)
        {
            if (card.floatTween != null && card.floatTween.IsActive())
            {
                card.floatTween.Kill();
                card.floatTween = null;
            }
        }

        /// <summary>
        /// FE-05: 停止所有卡片浮动
        /// </summary>
        private void StopAllFloatAnimations()
        {
            foreach (var card in cards)
                StopFloatAnimation(card);
        }

        /// <summary>
        /// 创建单张奖励卡片
        /// </summary>
        private RewardCard CreateRewardCard(RewardOption reward, int index, RectTransform slot)
        {
            var card = new RewardCard { index = index };

            RectTransform parentRect = slot ?? cardsContainer;
            if (parentRect == null) parentRect = transform as RectTransform;

            // 使用prefab或程序化创建
            if (rewardCardPrefab != null)
            {
                var go = Instantiate(rewardCardPrefab, parentRect);
                card.rect = go.GetComponent<RectTransform>();
                card.button = go.GetComponent<Button>();

                // 查找子节点
                card.cardFront = go.transform.Find("Front") as RectTransform;
                card.cardBack = go.transform.Find("Back") as RectTransform;
                card.nameText = go.transform.Find("Front/NameText")?.GetComponent<Text>();
                card.descText = go.transform.Find("Front/DescText")?.GetComponent<Text>();
                card.typeLabelText = go.transform.Find("Front/TypeLabel")?.GetComponent<Text>();
                card.rarityText = go.transform.Find("Front/RarityText")?.GetComponent<Text>();
                card.typeIcon = go.transform.Find("Front/TypeIcon")?.GetComponent<Image>();
                card.borderImage = go.transform.Find("Border")?.GetComponent<Image>();
                card.canvasGroup = go.GetComponent<CanvasGroup>();
            }
            else
            {
                // 程序化创建卡片
                var go = new GameObject($"RewardCard_{index}");
                go.transform.SetParent(parentRect, false);
                card.rect = go.AddComponent<RectTransform>();
                card.rect.sizeDelta = new Vector2(cardWidth, cardHeight);
                card.rect.anchorMin = new Vector2(0.5f, 0.5f);
                card.rect.anchorMax = new Vector2(0.5f, 0.5f);

                card.canvasGroup = go.AddComponent<CanvasGroup>();
                card.button = go.AddComponent<Button>();

                // 卡片背景
                var bgImg = go.AddComponent<Image>();
                bgImg.color = CARD_BG_NORMAL;
                bgImg.raycastTarget = true;

                // 稀有度边框
                var borderGo = new GameObject("Border");
                borderGo.transform.SetParent(go.transform, false);
                var borderRect = borderGo.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = new Vector2(2, 2);
                borderRect.offsetMax = new Vector2(-2, -2);
                card.borderImage = borderGo.AddComponent<Image>();
                card.borderImage.color = UIConfigBridge.GetRarityColor(reward.Rarity);
                card.borderImage.raycastTarget = false;

                // ===== 背面 =====
                var backGo = new GameObject("Back");
                backGo.transform.SetParent(go.transform, false);
                card.cardBack = backGo.AddComponent<RectTransform>();
                card.cardBack.anchorMin = Vector2.zero;
                card.cardBack.anchorMax = Vector2.one;
                card.cardBack.offsetMin = Vector2.zero;
                card.cardBack.offsetMax = Vector2.zero;
                var backImg = backGo.AddComponent<Image>();
                backImg.color = CARD_BACK_COLOR;
                var backText = backGo.AddComponent<Text>();
                backText.text = "🎁";
                backText.fontSize = 48;
                backText.alignment = TextAnchor.MiddleCenter;
                backText.color = new Color(0.5f, 0.5f, 0.5f);

                // ===== 正面 =====
                var frontGo = new GameObject("Front");
                frontGo.transform.SetParent(go.transform, false);
                card.cardFront = frontGo.AddComponent<RectTransform>();
                card.cardFront.anchorMin = Vector2.zero;
                card.cardFront.anchorMax = Vector2.one;
                card.cardFront.offsetMin = Vector2.zero;
                card.cardFront.offsetMax = Vector2.zero;
                frontGo.SetActive(false); // 先隐藏正面

                // 类型标签
                var typeGo = new GameObject("TypeLabel");
                typeGo.transform.SetParent(frontGo.transform, false);
                var typeRect = typeGo.AddComponent<RectTransform>();
                typeRect.anchorMin = new Vector2(0, 0.85f);
                typeRect.anchorMax = new Vector2(1, 1);
                typeRect.offsetMin = new Vector2(8, 2);
                typeRect.offsetMax = new Vector2(-8, -2);
                card.typeLabelText = typeGo.AddComponent<Text>();
                card.typeLabelText.fontSize = 12;
                card.typeLabelText.alignment = TextAnchor.MiddleCenter;
                card.typeLabelText.color = RewardTypeIcons.GetColor(reward.Type);
                card.typeLabelText.raycastTarget = false;

                // 类型图标 (emoji)
                var iconGo = new GameObject("TypeIcon");
                iconGo.transform.SetParent(frontGo.transform, false);
                var iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.3f, 0.55f);
                iconRect.anchorMax = new Vector2(0.7f, 0.85f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                card.typeIcon = iconGo.AddComponent<Image>();
                card.typeIcon.color = RewardTypeIcons.GetColor(reward.Type);
                card.typeIcon.raycastTarget = false;
                // emoji作为text覆盖在image上
                var iconText = iconGo.AddComponent<Text>();
                iconText.text = RewardTypeIcons.GetIcon(reward.Type);
                iconText.fontSize = 40;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = Color.white;

                // 名称
                var nameGo = new GameObject("NameText");
                nameGo.transform.SetParent(frontGo.transform, false);
                var nameRect = nameGo.AddComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.4f);
                nameRect.anchorMax = new Vector2(1, 0.55f);
                nameRect.offsetMin = new Vector2(8, 0);
                nameRect.offsetMax = new Vector2(-8, 0);
                card.nameText = nameGo.AddComponent<Text>();
                card.nameText.fontSize = 16;
                card.nameText.fontStyle = FontStyle.Bold;
                card.nameText.alignment = TextAnchor.MiddleCenter;
                card.nameText.color = Color.white;
                card.nameText.raycastTarget = false;

                // 稀有度
                var rarGo = new GameObject("RarityText");
                rarGo.transform.SetParent(frontGo.transform, false);
                var rarRect = rarGo.AddComponent<RectTransform>();
                rarRect.anchorMin = new Vector2(0, 0.3f);
                rarRect.anchorMax = new Vector2(1, 0.4f);
                rarRect.offsetMin = new Vector2(8, 0);
                rarRect.offsetMax = new Vector2(-8, 0);
                card.rarityText = rarGo.AddComponent<Text>();
                card.rarityText.fontSize = 14;
                card.rarityText.alignment = TextAnchor.MiddleCenter;
                card.rarityText.color = UIConfigBridge.GetRarityColor(reward.Rarity);
                card.rarityText.raycastTarget = false;

                // 描述
                var descGo = new GameObject("DescText");
                descGo.transform.SetParent(frontGo.transform, false);
                var descRect = descGo.AddComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0, 0.02f);
                descRect.anchorMax = new Vector2(1, 0.3f);
                descRect.offsetMin = new Vector2(10, 2);
                descRect.offsetMax = new Vector2(-10, -2);
                card.descText = descGo.AddComponent<Text>();
                card.descText.fontSize = 12;
                card.descText.alignment = TextAnchor.UpperLeft;
                card.descText.color = new Color(0.8f, 0.8f, 0.8f);
                card.descText.raycastTarget = false;
            }

            // 填充内容
            PopulateCardContent(card, reward);

            // 点击事件
            if (card.button != null)
            {
                int capturedIndex = index;
                card.button.onClick.AddListener(() => OnCardClicked(capturedIndex));
            }

            return card;
        }

        /// <summary>
        /// FE-05.2: 填充卡片内容（含4种奖励类型图标区分）
        /// </summary>
        private void PopulateCardContent(RewardCard card, RewardOption reward)
        {
            // 类型标签 + 图标
            string typeLabel = RewardTypeIcons.GetTypeLabel(reward.Type);
            string typeIcon = RewardTypeIcons.GetIcon(reward.Type);
            Color typeColor = RewardTypeIcons.GetColor(reward.Type);

            if (card.typeLabelText != null)
                card.typeLabelText.text = typeLabel;

            if (card.nameText != null)
                card.nameText.text = reward.Name ?? typeLabel;

            if (card.rarityText != null)
                card.rarityText.text = $"{UIConfigBridge.GetRarityStars(reward.Rarity)} {UIConfigBridge.GetRarityNameCN(reward.Rarity)}";

            // FE-05.2: 根据奖励类型生成不同描述
            string richDesc = UIConfigBridge.GetRewardRichDescription(reward);
            if (card.descText != null)
                card.descText.text = richDesc;

            // 稀有度边框颜色
            if (card.borderImage != null)
                card.borderImage.color = UIConfigBridge.GetRarityColor(reward.Rarity);
        }

        // ========== 卡片交互 ==========

        private void OnCardClicked(int index)
        {
            if (isConfirmed) return;

            var reward = currentRewards[index];

            // FE-05.3: 遗物类型 → 弹出详情弹窗
            if (reward.Type == RewardType.Relic && !string.IsNullOrEmpty(reward.RelicId))
            {
                ShowRelicDetailPopup(index);
                return;
            }

            // 其他类型直接选中
            SelectCard(index);
        }

        /// <summary>
        /// FE-05.4: 选中卡片 → 翻转到居中 + 其余灰化缩小
        /// </summary>
        private void SelectCard(int index)
        {
            selectedIndex = index;

            // 先停止所有浮动
            StopAllFloatAnimations();

            // 居中位置（cardsContainer的中心）
            Vector2 centerPos = Vector2.zero;
            if (cardsContainer != null)
                centerPos = cardsContainer.rect.center - cardsContainer.rect.min;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.rect == null) continue;

                if (i == index)
                {
                    // ===== 选中卡片：翻转到居中 =====
                    Sequence selectSeq = DOTween.Sequence();

                    // Step1: 翻转到侧面（Y 90°）
                    selectSeq.Append(card.rect.DORotate(new Vector3(0f, 90f, 0f), 0.15f).SetEase(Ease.InQuad));

                    // Step2: 同时移动到居中 + 放大
                    selectSeq.Join(card.rect.DOAnchorPos(centerPos, 0.25f).SetEase(Ease.OutCubic));
                    selectSeq.Join(card.rect.DOScale(Vector3.one * selectedScale, 0.25f).SetEase(Ease.OutBack));

                    // Step3: 翻回正面
                    selectSeq.Append(card.rect.DORotate(Vector3.zero, 0.15f).SetEase(Ease.OutQuad));

                    // 高亮边框
                    if (card.borderImage != null)
                    {
                        card.borderImage.DOColor(
                            UIConfigBridge.GetRarityColor(currentRewards[i].Rarity) * 1.5f,
                            0.3f
                        );
                    }

                    if (card.canvasGroup != null) card.canvasGroup.alpha = 1f;
                }
                else
                {
                    // 未选中：灰化 + 缩小 + 淡出
                    card.rect.DOScale(Vector3.one * dimmedScale, 0.3f).SetEase(Ease.OutQuad);
                    if (card.canvasGroup != null)
                        card.canvasGroup.DOFade(dimmedAlpha, 0.3f);
                    card.rect.DOAnchorPosY(-10f, 0.3f).SetEase(Ease.OutCubic);
                }
            }

            // 显示确认按钮
            ShowConfirmButton();
        }

        /// <summary>
        /// 显示确认按钮
        /// </summary>
        private void ShowConfirmButton()
        {
            if (confirmButton == null || confirmButtonRect == null) return;

            confirmButton.interactable = true;
            confirmButton.gameObject.SetActive(true);
            confirmButtonRect.localScale = Vector3.zero;

            // 更新按钮文字
            if (confirmButtonText != null && selectedIndex >= 0 && selectedIndex < currentRewards.Count)
            {
                var reward = currentRewards[selectedIndex];
                confirmButtonText.text = $"选择: {reward.Name}";
            }

            confirmButtonRect.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        private void OnConfirmClicked()
        {
            if (selectedIndex < 0 || isConfirmed) return;

            isConfirmed = true;
            confirmButton.interactable = false;

            // 选中卡片飞出动画
            var selectedCard = cards.Find(c => c.index == selectedIndex);
            if (selectedCard?.rect != null)
            {
                selectedCard.rect.DOScale(Vector3.one * 1.5f, 0.4f).SetEase(Ease.InBack);
                selectedCard.rect.DOLocalMoveY(500f, 0.4f).SetEase(Ease.InCubic);
                if (selectedCard.canvasGroup != null)
                    selectedCard.canvasGroup.DOFade(0f, 0.4f);
            }

            // 其余卡片淡出
            foreach (var card in cards)
            {
                if (card.index != selectedIndex && card.rect != null)
                {
                    if (card.canvasGroup != null)
                        card.canvasGroup.DOFade(0f, 0.3f);
                }
            }

            // 延迟执行实际选择逻辑
            DOVirtual.DelayedCall(0.6f, () =>
            {
                ApplySelectedReward();
            });
        }

        private void ApplySelectedReward()
        {
            if (selectedIndex < 0 || selectedIndex >= currentRewards.Count) return;

            var selectedReward = currentRewards[selectedIndex];

            if (RoguelikeGameManager.Instance != null)
            {
                RoguelikeGameManager.Instance.ChooseReward(selectedReward);
                Debug.Log($"[奖励面板] 选择奖励: {selectedReward.GetDisplayText()}");
            }

            // 进入下一关骰子阶段
            GameStateMachine.Instance?.NextState();
        }

        // ========== FE-05.3: 遗物详情弹窗 ==========

        private void ShowRelicDetailPopup(int cardIndex)
        {
            var reward = currentRewards[cardIndex];
            if (reward?.RelicId == null) return;

            // 从RoguelikeRewardSystem获取遗物数据
            var relicData = GetRelicDataById(reward.RelicId);
            if (relicData == null)
            {
                // 降级：直接选中
                SelectCard(cardIndex);
                return;
            }

            relicDetailCardIndex = cardIndex;

            // 填充弹窗
            var displayData = UIConfigBridge.GetRelicDisplayData(relicData);

            if (relicDetailPopup == null)
            {
                // 没有弹窗prefab → 直接选中
                SelectCard(cardIndex);
                return;
            }

            relicDetailPopup.gameObject.SetActive(true);

            if (relicDetailNameText != null)
            {
                relicDetailNameText.text = $"{displayData.iconEmoji} {displayData.relicName}";
                relicDetailNameText.color = displayData.rarityColor;
            }

            if (relicDetailRarityText != null)
            {
                relicDetailRarityText.text =
                    $"{UIConfigBridge.GetRarityStars(displayData.rarity)} {displayData.rarityName}";
                relicDetailRarityText.color = displayData.rarityColor;
            }

            if (relicDetailDescText != null)
                relicDetailDescText.text = displayData.description;

            if (relicDetailEffectText != null)
                relicDetailEffectText.text = displayData.effectDescription;

            if (relicDetailBorder != null)
                relicDetailBorder.color = displayData.rarityColor;

            // 弹窗入场动画
            relicDetailPopup.localScale = Vector3.zero;
            relicDetailPopup.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        private void OnRelicDetailSelectClicked()
        {
            // 关闭弹窗
            if (relicDetailPopup != null)
            {
                relicDetailPopup.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (relicDetailPopup != null)
                            relicDetailPopup.gameObject.SetActive(false);
                    });
            }

            // 选中对应的卡片
            if (relicDetailCardIndex >= 0)
            {
                SelectCard(relicDetailCardIndex);
            }
        }

        private void OnRelicDetailCloseClicked()
        {
            if (relicDetailPopup != null)
            {
                relicDetailPopup.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (relicDetailPopup != null)
                            relicDetailPopup.gameObject.SetActive(false);
                    });
            }
            relicDetailCardIndex = -1;
        }

        /// <summary>
        /// 从RoguelikeRewardSystem获取遗物数据
        /// 优先从RewardOption携带的RelicData直接取，避免反射
        /// </summary>
        private RelicData GetRelicDataById(string relicId)
        {
            // 方案1: 从当前奖励列表中直接取（RewardOption已携带RelicData）
            foreach (var reward in currentRewards)
            {
                if (reward.Type == RewardType.Relic && reward.RelicId == relicId && reward.RelicData != null)
                    return reward.RelicData;
            }

            // 方案2: 兜底从后端公共方法获取
            var rgm = RoguelikeGameManager.Instance;
            if (rgm?.RewardSystem != null)
            {
                var data = rgm.RewardSystem.GetRelicData(relicId);
                if (data != null) return data;
            }

            Debug.LogWarning($"[RoguelikeRewardPanel] 未找到遗物数据: {relicId}");
            return null;
        }

        // ========== 清理 ==========

        private void ClearCards()
        {
            StopAllFloatAnimations();
            foreach (var card in cards)
            {
                if (card.rect != null)
                {
                    card.rect.DOKill();
                    Destroy(card.rect.gameObject);
                }
            }
            cards.Clear();
        }
    }
}
