using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 英雄选择面板 — 战/法/刺三选一
    /// 
    /// UI元素（竖屏720x1280布局，Inspector拖拽绑定）：
    /// - 顶部标题：选择你的英雄
    /// - 中间区域：3张英雄卡片并排（战士/法师/刺客）
    ///   每张卡片包含：头像Image、名称Text、职业Text、属性面板Text、技能描述Text
    /// - 底部：确认按钮（选中后启用）
    /// 
    /// 交互流程：
    /// 1. 面板打开，3张卡片从底部依次飞入（间隔0.1s）
    /// 2. 点击卡片 → 选中高亮（放大+描边），其他卡片半透明
    /// 3. 点击确认 → 进入 DiceRoll 状态
    /// </summary>
    public class HeroSelectPanel : UIPanel
    {
        [Header("英雄卡片容器")]
        public RectTransform warriorCard;
        public RectTransform mageCard;
        public RectTransform assassinCard;

        [Header("卡片按钮（点击选中）")]
        public Button warriorButton;
        public Button mageButton;
        public Button assassinButton;

        [Header("选中高亮Image")]
        public Image warriorHighlight;
        public Image mageHighlight;
        public Image assassinHighlight;

        [Header("英雄头像")]
        public Image warriorIcon;
        public Image mageIcon;
        public Image assassinIcon;

        [Header("英雄名称")]
        public Text warriorNameText;
        public Text mageNameText;
        public Text assassinNameText;

        [Header("英雄职业标签")]
        public Text warriorClassText;
        public Text mageClassText;
        public Text assassinClassText;

        [Header("英雄属性Text")]
        public Text warriorStatsText;
        public Text mageStatsText;
        public Text assassinStatsText;

        [Header("技能名称")]
        public Text warriorSkillNameText;
        public Text mageSkillNameText;
        public Text assassinSkillNameText;

        [Header("技能描述")]
        public Text warriorSkillDescText;
        public Text mageSkillDescText;
        public Text assassinSkillDescText;

        [Header("操作按钮")]
        public Button confirmButton;
        public Text confirmButtonText;

        [Header("面板标题")]
        public Text titleText;

        // 内部状态
        private int selectedIndex = -1;
        private RectTransform[] cardRects;
        private Image[] highlightImages;
        private RectTransform selectedCard = null;

        // 英雄预设数据（MVP硬编码，后续走ScriptableObject配置表）
        private static readonly HeroPreviewData[] heroPreviews = new HeroPreviewData[]
        {
            new HeroPreviewData
            {
                heroClass = HeroClass.Warrior,
                displayName = "战士",
                classLabel = "近战 / 坦克",
                hp = 110, atk = 10, def = 8, spd = 6,
                skillName = "旋风斩",
                skillDesc = "对周围敌人造成攻击力150%的范围伤害",
                summonCost = 2
            },
            new HeroPreviewData
            {
                heroClass = HeroClass.Mage,
                displayName = "法师",
                classLabel = "远程 / AOE",
                hp = 70, atk = 12, def = 3, spd = 8,
                skillName = "火球术",
                skillDesc = "对目标区域造成攻击力200%的范围伤害",
                summonCost = 2
            },
            new HeroPreviewData
            {
                heroClass = HeroClass.Assassin,
                displayName = "刺客",
                classLabel = "近战 / 爆发",
                hp = 70, atk = 16, def = 3, spd = 14,
                skillName = "背刺",
                skillDesc = "从背后攻击造成攻击力250%的暴击伤害",
                summonCost = 1
            }
        };

        protected override void Awake()
        {
            base.Awake();
            panelId = "HeroSelect";

            cardRects = new RectTransform[] { warriorCard, mageCard, assassinCard };
            highlightImages = new Image[] { warriorHighlight, mageHighlight, assassinHighlight };
        }

        protected override void OnShow()
        {
            selectedIndex = -1;
            selectedCard = null;

            // 绑定按钮事件
            warriorButton?.onClick.AddListener(() => SelectHero(0));
            mageButton?.onClick.AddListener(() => SelectHero(1));
            assassinButton?.onClick.AddListener(() => SelectHero(2));
            confirmButton?.onClick.AddListener(OnConfirmClicked);

            // 初始化确认按钮为禁用
            if (confirmButton != null)
                confirmButton.interactable = false;
            if (confirmButtonText != null)
                confirmButtonText.text = "请选择英雄";

            // 填充英雄数据
            PopulateHeroData();

            // 重置卡片状态
            ResetCardStates();

            // 播放入场动画
            PlayCardEnterAnimation();
        }

        protected override void OnHide()
        {
            warriorButton?.onClick.RemoveAllListeners();
            mageButton?.onClick.RemoveAllListeners();
            assassinButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();

            // 清理所有DOTween动画
            foreach (var card in cardRects)
            {
                if (card != null) card.DOKill();
            }
        }

        /// <summary>
        /// 填充3张英雄卡片的数据
        /// </summary>
        private void PopulateHeroData()
        {
            // 战士
            SetHeroDisplay(0, warriorNameText, warriorClassText, warriorStatsText,
                warriorSkillNameText, warriorSkillDescText);
            // 法师
            SetHeroDisplay(1, mageNameText, mageClassText, mageStatsText,
                mageSkillNameText, mageSkillDescText);
            // 刺客
            SetHeroDisplay(2, assassinNameText, assassinClassText, assassinStatsText,
                assassinSkillNameText, assassinSkillDescText);
        }

        private void SetHeroDisplay(int index, Text nameText, Text classText, Text statsText,
            Text skillNameText, Text skillDescText)
        {
            var data = heroPreviews[index];
            if (nameText != null) nameText.text = data.displayName;
            if (classText != null) classText.text = data.classLabel;
            if (statsText != null)
            {
                statsText.text = $"HP:{data.hp}  ATK:{data.atk}\nDEF:{data.def}  SPD:{data.spd}\n召唤消耗:{data.summonCost}";
            }
            if (skillNameText != null) skillNameText.text = data.skillName;
            if (skillDescText != null) skillDescText.text = data.skillDesc;
        }

        /// <summary>
        /// 重置所有卡片到初始状态
        /// </summary>
        private void ResetCardStates()
        {
            for (int i = 0; i < cardRects.Length; i++)
            {
                if (cardRects[i] == null) continue;

                // 缩放重置
                cardRects[i].localScale = Vector3.one;
                cardRects[i].DOKill();

                // CanvasGroup重置（用于半透明效果）
                var cg = cardRects[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;

                // 高亮隐藏
                if (highlightImages[i] != null)
                {
                    highlightImages[i].gameObject.SetActive(false);
                    highlightImages[i].color = new Color(1f, 0.85f, 0f, 0f); // 金色高亮
                }
            }
        }

        /// <summary>
        /// 卡片入场动画 — 从底部依次飞入
        /// </summary>
        private void PlayCardEnterAnimation()
        {
            for (int i = 0; i < cardRects.Length; i++)
            {
                if (cardRects[i] == null) continue;

                // 初始位置在屏幕下方
                var startY = cardRects[i].anchoredPosition.y - 200f;
                cardRects[i].anchoredPosition = new Vector2(
                    cardRects[i].anchoredPosition.x, startY);
                cardRects[i].localScale = Vector3.one * 0.8f;

                var cg = cardRects[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;

                // 延迟飞入
                float delay = i * 0.1f;
                cardRects[i].DOAnchorPosY(startY + 200f, 0.4f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack);

                cardRects[i].DOScale(Vector3.one, 0.4f)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack);

                if (cg != null)
                {
                    cg.DOFade(1f, 0.3f).SetDelay(delay);
                }
            }
        }

        /// <summary>
        /// 选中英雄
        /// </summary>
        private void SelectHero(int index)
        {
            if (selectedIndex == index) return;

            selectedIndex = index;
            selectedCard = cardRects[index];

            // 更新选中/未选中卡片的视觉状态
            for (int i = 0; i < cardRects.Length; i++)
            {
                if (cardRects[i] == null) continue;

                bool isSelected = (i == index);
                var cg = cardRects[i].GetComponent<CanvasGroup>();

                if (isSelected)
                {
                    // 选中卡片：放大 + 显示高亮
                    cardRects[i].DOKill();
                    cardRects[i].DOScale(Vector3.one * 1.08f, 0.25f).SetEase(Ease.OutQuad);

                    if (highlightImages[i] != null)
                    {
                        highlightImages[i].gameObject.SetActive(true);
                        highlightImages[i].DOFade(0.6f, 0.25f);
                    }

                    if (cg != null) cg.DOFade(1f, 0.2f);
                }
                else
                {
                    // 未选中卡片：缩小 + 半透明
                    cardRects[i].DOKill();
                    cardRects[i].DOScale(Vector3.one * 0.92f, 0.25f).SetEase(Ease.OutQuad);

                    if (highlightImages[i] != null)
                    {
                        highlightImages[i].DOFade(0f, 0.2f).OnComplete(() =>
                            highlightImages[i].gameObject.SetActive(false));
                    }

                    if (cg != null) cg.DOFade(0.5f, 0.2f);
                }
            }

            // 启用确认按钮
            if (confirmButton != null)
                confirmButton.interactable = true;
            if (confirmButtonText != null)
                confirmButtonText.text = $"确认选择: {heroPreviews[index].displayName}";
        }

        /// <summary>
        /// 确认选择 — 进入骰子阶段
        /// </summary>
        private void OnConfirmClicked()
        {
            if (selectedIndex < 0) return;

            // 选中动画 — 卡片放大消失
            if (selectedCard != null)
            {
                selectedCard.DOKill();
                selectedCard.DOScale(Vector3.one * 1.3f, 0.3f).SetEase(Ease.InBack);
                var cg = selectedCard.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOFade(0f, 0.3f);
            }

            // 记录选择，供后续阶段使用
            PlayerPrefs.SetInt("SelectedHeroClass", (int)heroPreviews[selectedIndex].heroClass);
            PlayerPrefs.Save();

            Debug.Log($"[HeroSelect] 选择了 {heroPreviews[selectedIndex].displayName}");

            // 延迟切换状态，等动画播完
            DOVirtual.DelayedCall(0.35f, () =>
            {
                GameStateMachine.Instance.ChangeState(GameState.DiceRoll);
            });
        }

        /// <summary>
        /// 英雄预览数据（UI展示用，MVP硬编码）
        /// </summary>
        private struct HeroPreviewData
        {
            public HeroClass heroClass;
            public string displayName;
            public string classLabel;
            public int hp, atk, def, spd;
            public string skillName;
            public string skillDesc;
            public int summonCost;
        }
    }
}
