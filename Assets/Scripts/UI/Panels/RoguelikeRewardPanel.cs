using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 肉鸽三选一奖励面板
    /// UI元素：
    /// - rewardCard1/2/3: 3张奖励卡片Button
    /// - rewardName1/2/3: 奖励名称Text
    /// - rewardDesc1/2/3: 奖励描述Text
    /// - rewardIcon1/2/3: 奖励图标Image
    /// - rarityBorder1/2/3: 稀有度边框Image
    /// </summary>
    public class RoguelikeRewardPanel : UIPanel
    {
        [Header("奖励卡片")]
        public Button rewardCard1;
        public Button rewardCard2;
        public Button rewardCard3;

        [Header("奖励名称")]
        public Text rewardName1;
        public Text rewardName2;
        public Text rewardName3;

        [Header("奖励描述")]
        public Text rewardDesc1;
        public Text rewardDesc2;
        public Text rewardDesc3;

        [Header("奖励图标")]
        public Image rewardIcon1;
        public Image rewardIcon2;
        public Image rewardIcon3;

        [Header("稀有度边框")]
        public Image rarityBorder1;
        public Image rarityBorder2;
        public Image rarityBorder3;

        // 当前生成的奖励选项缓存
        private List<RewardOption> currentRewards = new List<RewardOption>();

        // 稀有度颜色: 1=普通(灰白), 2=稀有(蓝), 3=史诗(紫)
        private static readonly Color rarityColor1 = new Color(0.85f, 0.85f, 0.85f); // 灰白
        private static readonly Color rarityColor2 = new Color(0.26f, 0.53f, 0.96f); // 蓝
        private static readonly Color rarityColor3 = new Color(0.64f, 0.21f, 0.93f); // 紫

        private static Color GetRarityColor(int rarity)
        {
            switch (rarity)
            {
                case 2: return rarityColor2;
                case 3: return rarityColor3;
                default: return rarityColor1;
            }
        }

        protected override void OnShow()
        {
            // 绑定按钮点击
            rewardCard1?.onClick.AddListener(() => SelectReward(0));
            rewardCard2?.onClick.AddListener(() => SelectReward(1));
            rewardCard3?.onClick.AddListener(() => SelectReward(2));

            // 从RoguelikeGameManager获取3个奖励选项
            GenerateAndDisplayRewards();
        }

        protected override void OnHide()
        {
            // 移除按钮监听
            rewardCard1?.onClick.RemoveAllListeners();
            rewardCard2?.onClick.RemoveAllListeners();
            rewardCard3?.onClick.RemoveAllListeners();

            // 清空缓存
            currentRewards.Clear();
        }

        /// <summary>
        /// 调用RoguelikeGameManager生成奖励并显示到UI
        /// </summary>
        private void GenerateAndDisplayRewards()
        {
            currentRewards.Clear();

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

            // 显示最多3个奖励
            DisplayRewardSlot(0, rewardName1, rewardDesc1, rewardIcon1, rarityBorder1);
            DisplayRewardSlot(1, rewardName2, rewardDesc2, rewardIcon2, rarityBorder2);
            DisplayRewardSlot(2, rewardName3, rewardDesc3, rewardIcon3, rarityBorder3);
        }

        /// <summary>
        /// 将单个奖励选项显示到对应的UI槽位
        /// </summary>
        private void DisplayRewardSlot(int index, Text nameText, Text descText, Image icon, Image border)
        {
            if (index >= currentRewards.Count)
            {
                // 超出范围则隐藏
                if (nameText != null) nameText.gameObject.SetActive(false);
                if (descText != null) descText.gameObject.SetActive(false);
                if (icon != null) icon.gameObject.SetActive(false);
                if (border != null) border.gameObject.SetActive(false);
                return;
            }

            var reward = currentRewards[index];

            // 显示名称
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.text = reward.Name ?? string.Empty;
            }

            // 显示描述
            if (descText != null)
            {
                descText.gameObject.SetActive(true);
                descText.text = reward.Description ?? string.Empty;
            }

            // 显示图标（保持可见，具体sprite由外部设置）
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
            }

            // 设置稀有度边框颜色
            if (border != null)
            {
                border.gameObject.SetActive(true);
                border.color = GetRarityColor(reward.Rarity);
            }
        }

        /// <summary>
        /// 选择奖励：应用到GameManager，然后切换到骰子阶段
        /// </summary>
        private void SelectReward(int index)
        {
            if (currentRewards == null || index < 0 || index >= currentRewards.Count)
            {
                Debug.LogWarning($"[奖励面板] 无效的奖励索引: {index}");
                return;
            }

            var selectedReward = currentRewards[index];
            if (selectedReward == null)
            {
                Debug.LogWarning($"[奖励面板] 奖励选项为空: index={index}");
                return;
            }

            // 通过GameManager应用奖励（内部调用RewardSystem.ApplyReward）
            if (RoguelikeGameManager.Instance != null)
            {
                RoguelikeGameManager.Instance.ChooseReward(selectedReward);
                Debug.Log($"[奖励面板] 选择奖励: {selectedReward.GetDisplayText()}");
            }
            else
            {
                Debug.LogError("[奖励面板] RoguelikeGameManager实例不存在，无法应用奖励！");
            }

            // 进入下一关骰子阶段（NextState会递增CurrentLevel）
            GameStateMachine.Instance?.NextState();
        }
    }
}
