using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using System.Collections;

namespace Game.UI
{
    /// <summary>
    /// 英雄选择面板 - 战/法/刺三选一
    /// 通过 UIConfigBridge 获取数值显示数据
    /// </summary>
    public class HeroSelectPanel : UIPanel
    {
        [System.Serializable]
        public class HeroCard
        {
            public Button cardButton;
            public Image border;          // 选中高亮边框
            public Image icon;
            public Text nameText;
            public Text classText;        // 职业标签
            public Text hpText;
            public Text atkText;
            public Text defText;
            public Text spdText;
            public Text critText;         // 暴击率（新增）
            public Text costText;         // 召唤消耗（新增）
            public Text skillNameText;
            public Text skillDescText;
            public GameObject selectedMark; // 选中勾
        }

        [Header("英雄卡片")]
        public HeroCard warriorCard;
        public HeroCard mageCard;
        public HeroCard assassinCard;

        [Header("公共UI")]
        public Button confirmButton;
        public Text tipText;

        private HeroCard[] allCards;
        private int selectedIndex = -1;

        /// <summary>选中的英雄职业</summary>
        public HeroClass SelectedClass { get; private set; }

        // 缓存从UIConfigBridge获取的数据
        private HeroDisplayData[] heroDisplayData;

        protected override void Awake()
        {
            base.Awake();
            allCards = new HeroCard[] { warriorCard, mageCard, assassinCard };
            heroDisplayData = UIConfigBridge.GetAllHeroDisplayData();
        }

        protected override void OnShow()
        {
            selectedIndex = -1;
            SelectedClass = HeroClass.Warrior;
            confirmButton.interactable = false;
            tipText.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("hero_select.title")
                : "选择你的英雄";

            // 通过UIConfigBridge填充英雄数据
            for (int i = 0; i < allCards.Length && i < heroDisplayData.Length; i++)
            {
                var card = allCards[i];
                var data = heroDisplayData[i];

                if (card.nameText) card.nameText.text = data.displayName;
                if (card.classText) card.classText.text = data.className;
                if (card.hpText) card.hpText.text = $"HP {data.stats.maxHealth}";
                if (card.atkText) card.atkText.text = $"ATK {data.stats.attack}";
                if (card.defText) card.defText.text = $"DEF {data.stats.defense}";
                if (card.spdText) card.spdText.text = $"SPD {data.stats.speed}";
                if (card.critText) card.critText.text = $"CRT {Mathf.RoundToInt(data.stats.critRate * 100)}%";
                if (card.costText) card.costText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("hero_select.cost", data.summonCost.ToString())
                    : $"消耗 {data.summonCost}";
                if (card.skillDescText) card.skillDescText.text = data.description;
                if (card.selectedMark) card.selectedMark.SetActive(false);
                if (card.border) card.border.color = Color.white;

                // 设置职业主题色到边框（未选中时半透明）
                if (card.icon) card.icon.color = data.color;

                int idx = i;
                card.cardButton?.onClick.RemoveAllListeners();
                card.cardButton?.onClick.AddListener(() => SelectHero(idx));
            }

            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirm);
        }

        protected override void OnHide()
        {
            foreach (var card in allCards)
                card.cardButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.RemoveAllListeners();
        }

        private void SelectHero(int index)
        {
            selectedIndex = index;
            SelectedClass = heroDisplayData[index].heroClass;
            confirmButton.interactable = true;

            // 更新选中高亮
            for (int i = 0; i < allCards.Length; i++)
            {
                bool selected = (i == index);
                if (allCards[i].selectedMark) allCards[i].selectedMark.SetActive(selected);
                if (allCards[i].border)
                    allCards[i].border.color = selected ? heroDisplayData[i].color : Color.white;
            }

            tipText.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("hero_select.selected", heroDisplayData[index].displayName)
                : $"已选择: {heroDisplayData[index].displayName}";
        }

        private void OnConfirm()
        {
            if (selectedIndex < 0) return;

            var data = heroDisplayData[selectedIndex];

            // 1. 创建运行时HeroData
            HeroData heroData = ScriptableObject.CreateInstance<HeroData>();
            heroData.heroName = data.displayName;
            heroData.heroClass = data.heroClass;
            heroData.baseHealth = data.stats.maxHealth;
            heroData.baseAttack = data.stats.attack;
            heroData.baseDefense = data.stats.defense;
            heroData.baseSpeed = data.stats.speed;
            heroData.baseCritRate = data.stats.critRate;
            heroData.summonCost = data.summonCost;

            // 2. 创建Hero GameObject并初始化
            GameObject heroObj = new GameObject($"Hero_{heroData.heroName}");
            Hero hero = heroObj.AddComponent<Hero>();
            hero.Initialize(heroData, starLevel: 1);

            // Bug#3 fix: 确保RoguelikeGameManager已初始化再调用SelectHero
            var rgm = RoguelikeGameManager.Instance;
            if (rgm == null)
            {
                // 场景中可能没有RoguelikeGameManager，创建一个
                var go = new GameObject("RoguelikeGameManager");
                rgm = go.AddComponent<RoguelikeGameManager>();
                Debug.Log("[HeroSelect] 自动创建RoguelikeGameManager");
            }
            rgm.SelectHero(hero);

            Debug.Log($"[HeroSelect] 创建英雄: {heroData.heroName} ({heroData.heroClass}) " +
                      $"HP={hero.MaxHealth} ATK={hero.Attack} DEF={hero.Defense} SPD={hero.Speed}");

            // 3. 切换到骰子阶段（通过NextState保持流程一致）
            GameStateMachine.Instance.ChangeState(GameState.DiceRoll);
        }
    }
}
