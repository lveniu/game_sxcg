using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 结算面板 — 战斗结束后展示结果+金币，然后跳转到 RoguelikeReward 或 GameOver
    /// 
    /// 流程：战斗胜利 → Settlement(结果+金币) → RoguelikeReward(三选一奖励) → 下一关
    ///       战斗失败 → Settlement(结果) → GameOver
    /// 
    /// 注意：商店/事件子流程由 RoguelikeRewardPanel 处理，不在此面板中
    /// 
    /// 竖屏720x1280布局：
    /// ┌──────────────────────────────┐
    /// │  🏆 战斗胜利！ / 💀 战败...  │  结果标题
    /// │  第 3 关                      │
    /// ├──────────────────────────────┤
    /// │  获得装备                     │
    /// │  🛡 铁盾 (自动掉落)          │
    /// ├──────────────────────────────┤
    /// │  💰 金币 +50                  │
    /// ├──────────────────────────────┤
    /// │   [继续] / [返回主菜单]       │
    /// └──────────────────────────────┘
    /// </summary>
    public class SettlementPanel : UIPanel
    {
        [Header("结果标题")]
        public Text resultTitleText;
        public Text levelText;

        [Header("装备展示区")]
        public RectTransform equipmentArea;
        public Text equipmentText;

        [Header("金币")]
        public Text goldRewardText;

        [Header("按钮")]
        public Button nextButton;
        public Text nextButtonText;
        public Button backButton;

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

            ShowSettlement();
        }

        protected override void OnHide()
        {
            nextButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
        }

        // ========== 结算展示 ==========

        private void ShowSettlement()
        {
            var gsm = GameStateMachine.Instance;
            if (gsm == null) return;

            bool won = gsm.IsGameWon;
            int level = gsm.CurrentLevel;

            // 标题
            if (resultTitleText != null)
            {
                resultTitleText.text = won ? "🏆 战斗胜利！" : "💀 战斗失败...";
                resultTitleText.color = won ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);

                resultTitleText.rectTransform.localScale = Vector3.zero;
                resultTitleText.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
            }

            // 关卡
            if (levelText != null)
                levelText.text = $"第 {level} 关";

            // 装备掉落（胜利时）
            ShowEquipmentDrop(won);

            // 金币（胜利时）
            if (won)
                ShowGoldReward(level);

            // 按钮
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(won);
                nextButton.interactable = true;
                if (nextButtonText != null)
                    nextButtonText.text = "继续 →";
            }

            if (backButton != null)
            {
                backButton.gameObject.SetActive(!won);
            }
        }

        private void ShowEquipmentDrop(bool won)
        {
            if (equipmentArea != null)
                equipmentArea.gameObject.SetActive(false);

            // TODO: 对接后端装备掉落系统
            // if (won) { var drop = LootSystem.GetLevelDrop(level); ... }
        }

        private void ShowGoldReward(int level)
        {
            if (goldRewardText == null) return;

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

            goldRewardText.rectTransform.localScale = Vector3.zero;
            goldRewardText.rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        // ========== 按钮事件 ==========

        private void OnNextClicked()
        {
            // 胜利：跳转肉鸽奖励（NextState在Settlement状态下会跳到RoguelikeReward）
            GameStateMachine.Instance?.NextState();
        }

        private void OnBackClicked()
        {
            // 失败：跳转游戏结束
            GameStateMachine.Instance?.NextState();
        }
    }
}
