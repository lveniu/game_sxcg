using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 结算面板 — 战斗结束后展示结果+金币+英雄经验，然后跳转到 RoguelikeReward 或 GameOver
    /// 
    /// 流程：战斗胜利 → Settlement(结果+金币+英雄经验) → RoguelikeReward(三选一奖励) → 下一关
    ///       战斗失败 → Settlement(结果) → GameOver
    /// 
    /// 注意：商店/事件子流程由 RoguelikeRewardPanel 处理，不在此面板中
    /// 
    /// 竖屏720x1280布局（增强后）：
    /// ┌──────────────────────────────┐
    /// │  🏆 战斗胜利！ / 💀 战败...  │  结果标题
    /// │  第 3 关                      │
    /// ├──────────────────────────────┤
    /// │  [英雄经验卡片区域]           │  胜利时展示
    /// │  🗡 铁壁战士 Lv.3            │
    /// │  ████████░░ 80/120 EXP       │
    /// │  +45 EXP                     │
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
        [Tooltip("查看战报按钮")]
        public Button battleStatsButton;

        [Header("英雄经验区")]
        [Tooltip("英雄经验卡片容器，用于排列经验卡片")]
        public RectTransform heroExpArea;
        [Tooltip("经验卡片预制体（可选，为null时代码动态创建）")]
        public GameObject heroExpCardPrefab;
        [Tooltip("英雄间动画间隔（秒）")]
        public float expAnimDelay = 0.5f;

        // 运行时引用：跟踪所有动画完成
        private int pendingExpAnimations = 0;

        protected override void Awake()
        {
            base.Awake();
            panelId = "Settlement";
        }

        protected override void OnShow()
        {
            nextButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            battleStatsButton?.onClick.RemoveAllListeners();

            nextButton?.onClick.AddListener(OnNextClicked);
            backButton?.onClick.AddListener(OnBackClicked);
            battleStatsButton?.onClick.AddListener(OnBattleStatsClicked);

            ShowSettlement();
        }

        protected override void OnHide()
        {
            nextButton?.onClick.RemoveAllListeners();
            backButton?.onClick.RemoveAllListeners();
            battleStatsButton?.onClick.RemoveAllListeners();

            // 清理经验卡片区域
            ClearHeroExpArea();
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
                resultTitleText.text = won
                    ? (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("settlement.victory_title")
                        : "🏆 战斗胜利！")
                    : (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("settlement.defeat_title")
                        : "💀 战斗失败...");
                resultTitleText.color = won ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);

                resultTitleText.rectTransform.localScale = Vector3.zero;
                resultTitleText.rectTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            // 关卡进度显示
            var rgm = RoguelikeGameManager.Instance;
            int totalLevels = (BalanceProvider.Levels != null && BalanceProvider.Levels.level_templates != null)
                ? BalanceProvider.Levels.level_templates.Count : 10;
            int currentLevelProgress = rgm != null ? rgm.CurrentLevel : level;

            if (levelText != null)
            {
                string levelTitle = UIConfigBridge.GetLevelTitle(level);
                string progress = $"({currentLevelProgress}/{totalLevels})";
                levelText.text = $"{levelTitle} {progress}";
            }

            // 当前金币显示
            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                Debug.Log($"[SettlementPanel] 当前金币: {inventory.Gold}");
            }

            // 装备掉落（胜利时）
            ShowEquipmentDrop(won);

            // 金币（胜利时）
            if (won)
                ShowGoldReward(level);

            // 英雄经验（胜利时）
            if (won)
            {
                ShowHeroExpRewards(level);
            }
            else
            {
                ClearHeroExpArea();
            }

            // 按钮 — 胜利时先隐藏"继续"，等经验动画完成后显示
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(won);
                if (won)
                {
                    // 如果有经验动画，禁用按钮直到动画完成
                    nextButton.interactable = (pendingExpAnimations == 0);
                }
                else
                {
                    nextButton.interactable = true;
                }
                if (nextButtonText != null)
                    nextButtonText.text = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("settlement.next_button")
                        : "继续 →";
            }

            if (backButton != null)
            {
                backButton.gameObject.SetActive(!won);
            }

            // 自动存档（胜利时保存进度）
            if (won && SaveSystem.Instance != null)
            {
                SaveSystem.Instance.Save();
                Debug.Log("[SettlementPanel] 胜利结算 → 自动存档完成");
            }
        }

        private void ShowEquipmentDrop(bool won)
        {
            if (equipmentArea != null)
                equipmentArea.gameObject.SetActive(false);

            if (!won) return;

            var gsm = GameStateMachine.Instance;
            if (gsm == null) return;
            int level = gsm.CurrentLevel;

            var lootSystem = LootSystem.Instance;
            if (lootSystem == null)
            {
                Debug.LogWarning("[SettlementPanel] LootSystem实例不存在，跳过装备掉落");
                return;
            }

            // 检查本关是否掉落装备
            if (!lootSystem.ShouldDropEquipment(level)) return;

            // 生成掉落列表（3选1）
            var drops = lootSystem.GenerateLootDrops(level, 3);
            if (drops == null || drops.Count == 0) return;

            // 展示装备掉落区域
            if (equipmentArea != null)
            {
                equipmentArea.gameObject.SetActive(true);

                // 构建掉落装备展示文本
                if (equipmentText != null)
                {
                    var lines = new System.Text.StringBuilder();
                    lines.AppendLine("🎁 获得装备（选择一件）:");
                    for (int i = 0; i < drops.Count; i++)
                    {
                        lines.AppendLine($"  {i + 1}. {drops[i].GetDisplayText()}");
                    }
                    equipmentText.text = lines.ToString();

                    // 入场动画
                    equipmentText.rectTransform.localScale = Vector3.zero;
                    equipmentText.rectTransform.DOScale(Vector3.one, 0.4f)
                        .SetEase(Ease.OutBack).SetLink(gameObject);
                }

                // 创建选择按钮
                CreateLootSelectionButtons(drops);
            }
        }

        /// <summary>
        /// 为每件掉落装备创建选择按钮
        /// </summary>
        private void CreateLootSelectionButtons(List<LootDrop> drops)
        {
            if (equipmentArea == null) return;

            for (int i = 0; i < drops.Count; i++)
            {
                int index = i; // 闭包捕获
                var drop = drops[i];

                var btnGo = new GameObject($"LootBtn_{i}");
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.SetParent(equipmentArea, false);
                btnRt.sizeDelta = new Vector2(equipmentArea.rect.width - 40f, 36f);

                var btnImage = btnGo.AddComponent<Image>();
                btnImage.color = GetRarityBgColor(drop.Rarity);
                btnImage.raycastTarget = true;

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnImage;
                btn.onClick.AddListener(() => OnLootSelected(drop, btnGo));

                var btnText = btnGo.AddComponent<Text>();
                btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                btnText.fontSize = 14;
                btnText.color = Color.white;
                btnText.text = drop.GetDisplayText();
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.raycastTarget = false;

                // 按钮入场动画（依次弹入）
                btnRt.localScale = Vector3.zero;
                btnRt.DOScale(Vector3.one, 0.3f)
                    .SetDelay(0.2f + i * 0.15f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 玩家选中一件掉落装备
        /// </summary>
        private void OnLootSelected(LootDrop drop, GameObject btnObj)
        {
            if (drop == null || drop.IsSelected) return;

            var lootSystem = LootSystem.Instance;
            if (lootSystem == null) return;

            // 加入玩家背包
            bool claimed = lootSystem.ClaimLoot(drop);
            if (!claimed)
            {
                Debug.LogWarning("[SettlementPanel] 装备领取失败");
                return;
            }

            // 更新UI：显示已领取
            if (equipmentText != null)
            {
                equipmentText.text = $"✅ 已获得装备:\n  {drop.GetDisplayText()}";
            }

            // 移除其他选择按钮（已做出选择）
            if (equipmentArea != null)
            {
                for (int c = equipmentArea.childCount - 1; c >= 0; c--)
                {
                    var child = equipmentArea.GetChild(c);
                    if (child.gameObject != equipmentText?.gameObject && child.gameObject != btnObj)
                        Destroy(child.gameObject);
                }
            }

            // 选中按钮高亮效果
            if (btnObj != null)
            {
                var img = btnObj.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
                    btnObj.transform.DOScale(1.05f, 0.2f).SetEase(Ease.OutQuad).SetLink(gameObject)
                        .OnComplete(() => btnObj.transform.DOScale(1f, 0.1f).SetLink(gameObject));
                }
            }

            Debug.Log($"[SettlementPanel] 玩家选择了装备: {drop.DisplayName}");
        }

        /// <summary>
        /// 获取稀有度对应的背景颜色
        /// </summary>
        private Color GetRarityBgColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.White => new Color(0.4f, 0.4f, 0.4f, 0.85f),
                CardRarity.Blue => new Color(0.2f, 0.35f, 0.6f, 0.85f),
                CardRarity.Purple => new Color(0.45f, 0.2f, 0.6f, 0.85f),
                CardRarity.Gold => new Color(0.6f, 0.5f, 0.15f, 0.9f),
                _ => new Color(0.4f, 0.4f, 0.4f, 0.85f)
            };
        }

        private void ShowGoldReward(int level)
        {
            if (goldRewardText == null) return;

            // 从 ConfigLoader → UIConfigBridge 获取金币奖励（JSON配置优先）
            int goldReward = UIConfigBridge.GetGoldReward(level);

            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                inventory.AddGold(goldReward);
                goldRewardText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("settlement.gold_reward_balance", goldReward.ToString(), inventory.Gold.ToString())
                    : $"💰 金币 +{goldReward}（余额：{inventory.Gold}）";
            }
            else
                goldRewardText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("settlement.gold_reward", goldReward.ToString())
                    : $"💰 金币 +{goldReward}";

            goldRewardText.rectTransform.localScale = Vector3.zero;
            goldRewardText.rectTransform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        // ========== 英雄经验系统 ==========

        /// <summary>
        /// 清理英雄经验区域
        /// </summary>
        private void ClearHeroExpArea()
        {
            if (heroExpArea == null) return;
            for (int i = heroExpArea.childCount - 1; i >= 0; i--)
            {
                Destroy(heroExpArea.GetChild(i).gameObject);
            }
            heroExpArea.gameObject.SetActive(false);
            pendingExpAnimations = 0;
        }

        /// <summary>
        /// 展示所有参战英雄的经验获取
        /// </summary>
        private void ShowHeroExpRewards(int level)
        {
            if (heroExpArea == null) return;
            ClearHeroExpArea();
            heroExpArea.gameObject.SetActive(true);

            var deck = CardDeck.Instance;
            if (deck == null || deck.fieldHeroes == null || deck.fieldHeroes.Count == 0) return;

            // 获取参战英雄列表（过滤null）
            var heroes = deck.fieldHeroes.FindAll(h => h != null);
            if (heroes.Count == 0) return;

            // Mock经验奖励：基础20 + 关卡等级*10
            int baseExpReward = 20 + level * 10;

            pendingExpAnimations = heroes.Count;

            // 按顺序为每个英雄创建经验卡片并播放动画
            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                int expReward = baseExpReward;
                float delay = i * expAnimDelay;

                RectTransform card = CreateHeroExpCard(hero, expReward);

                // 延迟播放每个英雄的经验动画
                DOVirtual.DelayedCall(delay, () =>
                {
                    PlayExpAnimation(card, hero, expReward);
                }).SetLink(gameObject);
            }
        }

        /// <summary>
        /// 创建英雄经验卡片UI（动态创建）
        /// </summary>
        private RectTransform CreateHeroExpCard(Hero hero, int expReward)
        {
            RectTransform card;

            if (heroExpCardPrefab != null)
            {
                // 使用预制体
                var go = Instantiate(heroExpCardPrefab, heroExpArea);
                card = go.GetComponent<RectTransform>();
                if (card == null) card = go.AddComponent<RectTransform>();
            }
            else
            {
                // 代码动态创建卡片
                var go = new GameObject($"ExpCard_{hero.Data.heroName}");
                card = go.AddComponent<RectTransform>();
                card.SetParent(heroExpArea, false);

                // --- 卡片背景 ---
                var bgImage = go.AddComponent<Image>();
                bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
                bgImage.raycastTarget = false;

                // 卡片尺寸
                card.sizeDelta = new Vector2(heroExpArea.rect.width - 20f, 130f);

                // --- 布局：垂直排列 ---
                var vLayout = go.AddComponent<VerticalLayoutGroup>();
                vLayout.padding = new RectOffset(15, 15, 10, 10);
                vLayout.spacing = 6f;
                vLayout.childAlignment = TextAnchor.UpperLeft;
                vLayout.childControlWidth = true;
                vLayout.childControlHeight = false;
                vLayout.childForceExpandWidth = true;
                vLayout.childForceExpandHeight = false;

                // --- 第一行：英雄图标 + 名称 + 等级 ---
                var row1 = CreateChildObject("Row1_Info", card);
                var row1Layout = row1.gameObject.AddComponent<HorizontalLayoutGroup>();
                row1Layout.spacing = 8f;
                row1Layout.childAlignment = TextAnchor.MiddleLeft;
                row1Layout.childControlWidth = false;
                row1Layout.childControlHeight = false;
                row1Layout.childForceExpandWidth = false;
                row1Layout.childForceExpandHeight = false;

                // 英雄图标
                var iconGo = CreateChildObject("HeroIcon", row1);
                var iconImage = iconGo.gameObject.AddComponent<Image>();
                iconImage.color = GetClassColor(hero.Data.heroClass);
                iconImage.raycastTarget = false;
                var iconRt = iconGo;
                iconRt.sizeDelta = new Vector2(30f, 30f);

                // 英雄名称
                var nameGo = CreateChildObject("HeroName", row1);
                var nameText = nameGo.gameObject.AddComponent<Text>();
                nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                nameText.fontSize = 16;
                nameText.color = Color.white;
                nameText.text = $"{GetClassIcon(hero.Data.heroClass)} {hero.Data.heroName}";
                nameText.raycastTarget = false;
                nameText.alignment = TextAnchor.MiddleLeft;
                var nameRt = nameGo;
                nameRt.sizeDelta = new Vector2(130f, 24f);

                // 等级
                var levelGo = CreateChildObject("HeroLevel", row1);
                var levelTextComp = levelGo.gameObject.AddComponent<Text>();
                levelTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                levelTextComp.fontSize = 14;
                levelTextComp.color = new Color(1f, 0.85f, 0.2f);
                int mockLevel = MockHeroExpData.GetLevel(hero);
                levelTextComp.text = $"Lv.{mockLevel}";
                levelTextComp.raycastTarget = false;
                levelTextComp.alignment = TextAnchor.MiddleLeft;
                levelTextComp.name = "LevelText";
                var levelRt = levelGo;
                levelRt.sizeDelta = new Vector2(60f, 22f);

                // 星级区域
                var starGo = CreateChildObject("StarArea", row1);
                var starRt = starGo;
                starRt.sizeDelta = new Vector2(60f, 22f);
                starGo.gameObject.name = "StarArea";
                // 星星用子对象，后续在PlayStarUpAnimation中使用
                var starImages = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    var starChild = CreateChildObject($"Star_{s}", starRt);
                    starChild.sizeDelta = new Vector2(16f, 16f);
                    var starChildImg = starChild.gameObject.AddComponent<Image>();
                    starChildImg.color = s < hero.StarLevel
                        ? new Color(1f, 0.85f, 0.1f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.5f);
                    starChildImg.raycastTarget = false;
                    starImages[s] = starChildImg;
                    // 水平排列：手动设置位置
                    starChild.anchoredPosition = new Vector2(s * 18f, 0f);
                }

                // --- 第二行：经验条 + 经验数字 ---
                var row2 = CreateChildObject("Row2_ExpBar", card);
                var row2Rt = row2;
                row2Rt.sizeDelta = new Vector2(0f, 20f);

                var expBarBg = row2.gameObject.AddComponent<Image>();
                expBarBg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                expBarBg.raycastTarget = false;

                // 填充条
                var fillGo = CreateChildObject("ExpFill", row2Rt);
                fillGo.anchorMin = Vector2.zero;
                fillGo.anchorMax = Vector2.one;
                fillGo.offsetMin = new Vector2(2f, 2f);
                fillGo.offsetMax = new Vector2(-2f, -2f);
                var fillImage = fillGo.gameObject.AddComponent<Image>();
                fillImage.color = new Color(0.2f, 0.8f, 1f);
                fillImage.raycastTarget = false;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillGo.gameObject.name = "ExpFill";

                // 经验数字
                var expTextGo = CreateChildObject("ExpText", row2Rt);
                expTextGo.anchorMin = Vector2.zero;
                expTextGo.anchorMax = Vector2.one;
                expTextGo.offsetMin = Vector2.zero;
                expTextGo.offsetMax = Vector2.zero;
                var expText = expTextGo.gameObject.AddComponent<Text>();
                expText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                expText.fontSize = 11;
                expText.color = Color.white;
                expText.alignment = TextAnchor.MiddleCenter;
                expText.raycastTarget = false;
                int curExp = MockHeroExpData.GetExp(hero);
                int expToNext = MockHeroExpData.GetExpToNext(hero);
                expText.text = $"{curExp}/{expToNext} EXP";
                expTextGo.gameObject.name = "ExpText";

                // --- 第三行：获得经验数字 ---
                var row3 = CreateChildObject("Row3_ExpGain", card);
                var row3Rt = row3;
                row3Rt.sizeDelta = new Vector2(0f, 20f);
                var expGainText = row3.gameObject.AddComponent<Text>();
                expGainText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                expGainText.fontSize = 14;
                expGainText.color = new Color(0.4f, 1f, 0.4f);
                expGainText.text = $"+{expReward} EXP";
                expGainText.alignment = TextAnchor.MiddleLeft;
                expGainText.raycastTarget = false;
                row3.gameObject.name = "ExpGainText";

                // --- 第四行：升级标记（隐藏） ---
                var row4 = CreateChildObject("Row4_LevelUp", card);
                var row4Rt = row4;
                row4Rt.sizeDelta = new Vector2(0f, 24f);
                var levelUpText = row4.gameObject.AddComponent<Text>();
                levelUpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                levelUpText.fontSize = 18;
                levelUpText.fontStyle = FontStyle.Bold;
                levelUpText.color = new Color(1f, 0.85f, 0.1f);
                levelUpText.text = "LEVEL UP!";
                levelUpText.alignment = TextAnchor.MiddleCenter;
                levelUpText.raycastTarget = false;
                row4.gameObject.SetActive(false);
                row4.gameObject.name = "LevelUpText";

                // --- 第五行：星级进化标记（隐藏） ---
                var row5 = CreateChildObject("Row5_StarUp", card);
                var row5Rt = row5;
                row5Rt.sizeDelta = new Vector2(0f, 20f);
                var starUpText = row5.gameObject.AddComponent<Text>();
                starUpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                starUpText.fontSize = 14;
                starUpText.fontStyle = FontStyle.Bold;
                starUpText.color = new Color(1f, 0.6f, 0.1f);
                starUpText.text = "★ 进化!";
                starUpText.alignment = TextAnchor.MiddleCenter;
                starUpText.raycastTarget = false;
                row5.gameObject.SetActive(false);
                row5.gameObject.name = "StarUpText";

                // --- 第六行：属性变化区域（隐藏） ---
                var row6 = CreateChildObject("Row6_Stats", card);
                var row6Rt = row6;
                row6Rt.sizeDelta = new Vector2(0f, 18f);
                var statsText = row6.gameObject.AddComponent<Text>();
                statsText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                statsText.fontSize = 12;
                statsText.color = new Color(0.8f, 0.8f, 0.8f);
                statsText.text = "";
                statsText.alignment = TextAnchor.MiddleLeft;
                statsText.raycastTarget = false;
                row6.gameObject.SetActive(false);
                row6.gameObject.name = "StatChangesText";
            }

            // 卡片初始隐藏（用于入场动画）
            card.localScale = Vector3.one;
            card.anchoredPosition = new Vector2(0f, 0f);

            return card;
        }

        /// <summary>
        /// 创建子对象（带RectTransform）
        /// </summary>
        private RectTransform CreateChildObject(string name, RectTransform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        /// <summary>
        /// 播放经验获取动画（含升级和星级进化）
        /// </summary>
        private void PlayExpAnimation(RectTransform card, Hero hero, int expReward)
        {
            // 模拟经验获取
            var (didLevelUp, didStarUp, levelsGained) = MockHeroExpData.SimulateExpGain(hero, expReward);

            // 查找卡片内的UI元素
            var expFill = card.Find("Row2_ExpBar/ExpFill")?.GetComponent<Image>();
            var expText = card.Find("Row2_ExpBar/ExpText")?.GetComponent<Text>();
            var expGainText = card.Find("Row3_ExpGain")?.GetComponent<Text>();
            var levelUpTextGo = card.Find("Row4_LevelUp")?.gameObject;
            var starUpTextGo = card.Find("Row5_StarUp")?.gameObject;
            var statsTextGo = card.Find("Row6_Stats")?.GetComponent<Text>();
            var levelText = card.Find("Row1_Info/HeroLevel")?.GetComponent<Text>();
            var starArea = card.Find("Row1_Info/StarArea");

            int curExp = MockHeroExpData.GetExp(hero);
            int expToNext = MockHeroExpData.GetExpToNext(hero);
            int prevLevel = MockHeroExpData.GetLevel(hero) - levelsGained;
            int prevExpToNext = 50 + prevLevel * 20;
            int prevExp = curExp + expToNext * levelsGained - expReward;

            // 计算初始经验条比例
            float startFill = prevExpToNext > 0 ? Mathf.Clamp01((float)prevExp / prevExpToNext) : 0f;

            // 获得经验数字飞入效果
            if (expGainText != null)
            {
                var gainRt = expGainText.rectTransform;
                gainRt.anchoredPosition = new Vector2(150f, 0f);
                gainRt.DOKill();
                gainRt.DOAnchorPos(new Vector2(0f, 0f), 0.3f).SetEase(Ease.OutCubic).SetLink(gameObject);
                // 闪烁效果
                expGainText.color = new Color(0.4f, 1f, 0.4f);
                expGainText.DOKill();
                expGainText.DOFade(1f, 0.1f).SetLink(gameObject);
            }

            // 计算动画序列
            float totalAnimDuration;

            if (didLevelUp)
            {
                // 升级动画：先填满 → 闪光 → 重置 → 显示新等级
                if (expFill != null)
                {
                    expFill.fillAmount = startFill;
                    var fillSeq = DOTween.Sequence();
                    fillSeq.SetLink(gameObject);

                    // 先填满当前级
                    fillSeq.Append(expFill.DOFillAmount(1f, 0.3f).SetEase(Ease.OutQuad));

                    // 如果有多级升级，快速循环
                    for (int lv = 1; lv < levelsGained; lv++)
                    {
                        fillSeq.AppendCallback(() =>
                        {
                            expFill.fillAmount = 0f;
                        });
                        fillSeq.Append(expFill.DOFillAmount(1f, 0.15f).SetEase(Ease.OutQuad));
                    }

                    // 最后一级：填到当前经验比例
                    float finalFill = expToNext > 0 ? Mathf.Clamp01((float)curExp / expToNext) : 0f;
                    fillSeq.AppendCallback(() =>
                    {
                        expFill.fillAmount = 0f;
                        // 闪光效果：卡片背景闪白
                        var bg = card.GetComponent<Image>();
                        if (bg != null)
                        {
                            var origColor = bg.color;
                            bg.color = Color.white;
                            bg.DOKill();
                            bg.DOColor(origColor, 0.3f).SetEase(Ease.OutQuad).SetLink(gameObject);
                        }
                    });
                    fillSeq.Append(expFill.DOFillAmount(finalFill, 0.25f).SetEase(Ease.OutQuad));

                    fillSeq.Play();
                }

                // 更新等级文字
                if (levelText != null)
                {
                    DOVirtual.DelayedCall(0.5f, () =>
                    {
                        levelText.text = $"Lv.{MockHeroExpData.GetLevel(hero)}";
                    }).SetLink(gameObject);
                }

                // 播放升级动画
                DOVirtual.DelayedCall(0.6f, () =>
                {
                    PlayLevelUpAnimation(card, hero, levelsGained);
                }).SetLink(gameObject);

                // 播放星级进化动画
                if (didStarUp)
                {
                    DOVirtual.DelayedCall(1.2f, () =>
                    {
                        PlayStarUpAnimation(card, hero);
                    }).SetLink(gameObject);
                }

                // 显示属性变化
                if (statsTextGo != null)
                {
                    DOVirtual.DelayedCall(didStarUp ? 1.8f : 1.2f, () =>
                    {
                        var statChanges = MockHeroExpData.GetLevelUpStatChanges(hero, levelsGained);
                        if (statChanges != null && statChanges.Count > 0)
                        {
                            var statParent = statsTextGo.rectTransform;
                            statsTextGo.gameObject.SetActive(true);
                            string statStr = string.Join("  ", statChanges.ConvertAll(s => $"{s.stat}+{s.delta}"));
                            statsTextGo.text = statStr;

                            // 从左滑入效果
                            statParent.anchoredPosition = new Vector2(-200f, 0f);
                            statParent.DOKill();
                            statParent.DOAnchorPos(new Vector2(0f, 0f), 0.4f)
                                .SetEase(Ease.OutCubic).SetLink(gameObject);
                        }
                    }).SetLink(gameObject);
                }

                // 更新经验数字
                if (expText != null)
                {
                    DOVirtual.DelayedCall(0.8f, () =>
                    {
                        expText.text = $"{curExp}/{expToNext} EXP";
                    }).SetLink(gameObject);
                }

                totalAnimDuration = didStarUp ? 2.5f : 1.8f;
            }
            else
            {
                // 无升级：经验条平滑增长
                if (expFill != null)
                {
                    float targetFill = expToNext > 0 ? Mathf.Clamp01((float)curExp / expToNext) : 0f;
                    expFill.fillAmount = startFill;
                    expFill.DOKill();
                    expFill.DOFillAmount(targetFill, 0.5f).SetEase(Ease.OutQuad).SetLink(gameObject);
                }

                // 更新经验数字
                if (expText != null)
                {
                    expText.text = $"{curExp}/{expToNext} EXP";
                }

                totalAnimDuration = 0.7f;
            }

            // 动画完成后，通知继续按钮
            DOVirtual.DelayedCall(totalAnimDuration, () =>
            {
                OnExpAnimationComplete();
            }).SetLink(gameObject);
        }

        /// <summary>
        /// 播放升级动画
        /// </summary>
        private void PlayLevelUpAnimation(RectTransform card, Hero hero, int levelsGained)
        {
            var levelUpTextGo = card.Find("Row4_LevelUp")?.gameObject;
            if (levelUpTextGo == null) return;

            levelUpTextGo.SetActive(true);

            // "LEVEL UP!" 从中心放大弹出（0→1.5→1.0，OutBack）
            var rt = levelUpTextGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;

            var seq = DOTween.Sequence();
            seq.SetLink(gameObject);
            seq.Append(rt.DOScale(1.5f, 0.2f).SetEase(Ease.OutQuad));
            seq.Append(rt.DOScale(1.0f, 0.15f).SetEase(Ease.OutBack));
            seq.Play();

            // 卡片边框金色脉冲（scale 1.0↔1.05 × 3次）
            var cardSeq = DOTween.Sequence();
            cardSeq.SetLink(gameObject);
            for (int i = 0; i < 3; i++)
            {
                cardSeq.Append(card.DOScale(1.05f, 0.1f));
                cardSeq.Append(card.DOScale(1.0f, 0.1f));
            }
            cardSeq.Play();
        }

        /// <summary>
        /// 播放星级进化动画
        /// </summary>
        private void PlayStarUpAnimation(RectTransform card, Hero hero)
        {
            var starArea = card.Find("Row1_Info/StarArea");
            var starUpTextGo = card.Find("Row5_StarUp")?.gameObject;

            int newStarLevel = hero.StarLevel;

            // 星星图标逐个亮起
            if (starArea != null)
            {
                for (int s = 0; s < 3; s++)
                {
                    var starChild = starArea.Find($"Star_{s}");
                    if (starChild == null) continue;
                    var starImg = starChild.GetComponent<Image>();
                    if (starImg == null) continue;

                    if (s < newStarLevel)
                    {
                        int idx = s;
                        DOVirtual.DelayedCall(idx * 0.3f, () =>
                        {
                            // 亮起：DOFade + DOScale
                            starImg.color = new Color(1f, 0.85f, 0.1f, 0f);
                            starImg.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject);
                            starChild.localScale = Vector3.zero;
                            starChild.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad).SetLink(gameObject)
                                .OnComplete(() =>
                                {
                                    starChild.DOScale(1.0f, 0.1f).SetEase(Ease.OutQuad).SetLink(gameObject);
                                });

                            // 简化粒子效果：金色缩放+透明度脉冲
                            ShowFloatingText(
                                card,
                                "✦",
                                new Color(1f, 0.85f, 0.1f),
                                new Vector2(Random.Range(-60f, 60f), Random.Range(-30f, 30f))
                            );
                        }).SetLink(gameObject);
                    }
                }
            }

            // 卡片背景色闪白（简化光柱效果）
            var bg = card.GetComponent<Image>();
            if (bg != null)
            {
                var origColor = bg.color;
                var goldFlash = new Color(1f, 0.9f, 0.5f, 1f);
                bg.DOKill();
                bg.DOColor(goldFlash, 0.15f).SetEase(Ease.OutQuad).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        bg.DOColor(origColor, 0.3f).SetEase(Ease.OutQuad).SetLink(gameObject);
                    });
            }

            // 显示 "★ 进化!" 文字
            if (starUpTextGo != null)
            {
                starUpTextGo.SetActive(true);
                var starUpText = starUpTextGo.GetComponent<Text>();
                if (starUpText != null)
                {
                    starUpText.text = $"★→{new string('★', newStarLevel)} 进化!";
                }

                var starUpRt = starUpTextGo.GetComponent<RectTransform>();
                starUpRt.localScale = Vector3.zero;
                starUpRt.DOKill();
                starUpRt.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        starUpRt.DOScale(1.0f, 0.1f).SetEase(Ease.OutQuad).SetLink(gameObject);
                    });
            }
        }

        /// <summary>
        /// 飘字效果
        /// </summary>
        private void ShowFloatingText(RectTransform parent, string text, Color color, Vector2 offset)
        {
            var floatGo = new GameObject("FloatingText");
            var floatRt = floatGo.AddComponent<RectTransform>();
            floatRt.SetParent(parent, false);
            floatRt.anchorMin = new Vector2(0.5f, 0.5f);
            floatRt.anchorMax = new Vector2(0.5f, 0.5f);
            floatRt.pivot = new Vector2(0.5f, 0.5f);
            floatRt.anchoredPosition = offset;
            floatRt.sizeDelta = new Vector2(40f, 20f);

            var floatText = floatGo.AddComponent<Text>();
            floatText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            floatText.fontSize = 16;
            floatText.color = color;
            floatText.text = text;
            floatText.alignment = TextAnchor.MiddleCenter;
            floatText.raycastTarget = false;

            // 飘字动画：向上飘 + 淡出
            floatRt.DOKill();
            floatText.DOKill();

            var seq = DOTween.Sequence();
            seq.SetLink(gameObject);
            seq.Append(floatRt.DOAnchorPosY(offset.y + 30f, 0.8f).SetEase(Ease.OutCubic));
            seq.Join(floatText.DOFade(0f, 0.8f).SetEase(Ease.InQuad));
            seq.Join(floatRt.DOScale(1.5f, 0.8f).SetEase(Ease.OutQuad));
            seq.OnComplete(() => Destroy(floatGo));
            seq.Play();
        }

        /// <summary>
        /// 单个英雄经验动画完成回调
        /// </summary>
        private void OnExpAnimationComplete()
        {
            pendingExpAnimations--;
            if (pendingExpAnimations <= 0)
            {
                pendingExpAnimations = 0;
                // 所有动画完成，启用"继续"按钮
                if (nextButton != null && nextButton.gameObject.activeSelf)
                {
                    nextButton.interactable = true;
                    // 按钮弹入效果
                    var btnRt = nextButton.GetComponent<RectTransform>();
                    if (btnRt != null)
                    {
                        btnRt.localScale = Vector3.zero;
                        btnRt.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 获取英雄职业对应的图标
        /// </summary>
        private string GetClassIcon(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Warrior: return "🗡";
                case HeroClass.Mage: return "🔮";
                case HeroClass.Assassin: return "🏹";
                default: return "⭐";
            }
        }

        /// <summary>
        /// 获取英雄职业对应的颜色
        /// </summary>
        private Color GetClassColor(HeroClass heroClass)
        {
            switch (heroClass)
            {
                case HeroClass.Warrior: return new Color(0.9f, 0.3f, 0.3f);
                case HeroClass.Mage: return new Color(0.4f, 0.4f, 0.9f);
                case HeroClass.Assassin: return new Color(0.3f, 0.8f, 0.3f);
                default: return Color.gray;
            }
        }

        // ========== 按钮事件 ==========

        private void OnNextClicked()
        {
            // 通知肉鸽管理器进入下一关
            var rgm = RoguelikeGameManager.Instance;
            if (rgm != null)
            {
                rgm.EnterNextLevel();
                Debug.Log($"[SettlementPanel] 进入下一关，当前关卡: {rgm.CurrentLevel}");
            }

            // 跳转肉鸽奖励（NextState在Settlement状态下会跳到RoguelikeReward）
            GameStateMachine.Instance?.NextState();
        }

        private void OnBackClicked()
        {
            // 失败：跳转游戏结束
            GameStateMachine.Instance?.NextState();
        }

        /// <summary>
        /// 查看战报 — 打开战报统计面板
        /// </summary>
        private void OnBattleStatsClicked()
        {
            var uiManager = NewUIManager.Instance;
            if (uiManager != null)
                uiManager.ShowSubPanel("BattleStats");
            else
                Debug.LogWarning("[SettlementPanel] NewUIManager实例不存在，无法打开战报面板");
        }
    }
}

// Mock 数据 — 后端 BE-10 完成后删除，替换为 HeroExpSystem
public static class MockHeroExpData
{
    private static readonly Dictionary<Hero, MockExpState> expMap = new Dictionary<Hero, MockExpState>();

    public struct MockExpState
    {
        public int level;
        public int currentExp;
        public int expToNext;
    }

    /// <summary>
    /// 确保英雄有Mock数据
    /// </summary>
    private static void EnsureHeroData(Hero hero)
    {
        if (hero == null) return;

        // 清理已销毁 Hero 的脏 key（防内存泄漏）
        var keysToRemove = new List<Hero>();
        foreach (var kvp in expMap)
        {
            if (kvp.Key == null)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var k in keysToRemove)
            expMap.Remove(k);

        if (!expMap.ContainsKey(hero))
        {
            expMap[hero] = new MockExpState
            {
                level = 1,
                currentExp = 0,
                expToNext = 50 + 1 * 20  // 70
            };
        }
    }

    /// <summary>
    /// 获取英雄 Mock 等级
    /// </summary>
    public static int GetLevel(Hero hero)
    {
        EnsureHeroData(hero);
        return expMap[hero].level;
    }

    /// <summary>
    /// 获取英雄 Mock 经验
    /// </summary>
    public static int GetExp(Hero hero)
    {
        EnsureHeroData(hero);
        return expMap[hero].currentExp;
    }

    /// <summary>
    /// 获取升级所需经验
    /// </summary>
    public static int GetExpToNext(Hero hero)
    {
        EnsureHeroData(hero);
        return expMap[hero].expToNext;
    }

    /// <summary>
    /// 模拟经验获取，返回 (didLevelUp, didStarUp, levelsGained)
    /// </summary>
    public static (bool didLevelUp, bool didStarUp, int levelsGained) SimulateExpGain(Hero hero, int amount)
    {
        EnsureHeroData(hero);
        var state = expMap[hero];

        int oldLevel = state.level;
        int oldStarLevel = hero.StarLevel;
        int remaining = amount;
        int levelsGained = 0;

        while (remaining > 0)
        {
            int needed = state.expToNext - state.currentExp;
            if (remaining >= needed)
            {
                // 升级
                remaining -= needed;
                state.currentExp = 0;
                state.level++;
                levelsGained++;
                state.expToNext = 50 + state.level * 20;
            }
            else
            {
                state.currentExp += remaining;
                remaining = 0;
            }
        }

        // 检查星级进化：每3级触发一次（3→2星, 6→3星）
        bool didStarUp = false;
        int newStarThreshold = oldStarLevel * 3;
        if (state.level >= newStarThreshold && oldStarLevel < 3)
        {
            // 注意：不直接修改hero.StarLevel（后端BE-10负责），
            // 但为了Mock UI展示，模拟升级
            didStarUp = true;
        }

        expMap[hero] = state;

        bool didLevelUp = levelsGained > 0;
        return (didLevelUp, didStarUp, levelsGained);
    }

    /// <summary>
    /// 获取升级后的属性变化（Mock）
    /// </summary>
    public static List<(string stat, int delta)> GetLevelUpStatChanges(Hero hero, int levelsGained)
    {
        var changes = new List<(string, int)>();
        if (levelsGained <= 0) return changes;

        // ATK+2/级, DEF+1/级, HP+10/级
        changes.Add(("ATK", 2 * levelsGained));
        changes.Add(("DEF", 1 * levelsGained));
        changes.Add(("HP", 10 * levelsGained));

        return changes;
    }

    /// <summary>
    /// 重置指定英雄的Mock数据
    /// </summary>
    public static void ResetHero(Hero hero)
    {
        if (hero != null && expMap.ContainsKey(hero))
        {
            expMap.Remove(hero);
        }
    }

    /// <summary>
    /// 重置所有Mock数据
    /// </summary>
    public static void ResetAll()
    {
        expMap.Clear();
    }
}
