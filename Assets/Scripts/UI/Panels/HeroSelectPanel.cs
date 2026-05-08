using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Game.Core;
using System.Collections;

namespace Game.UI
{
    /// <summary>
    /// 英雄选择面板 - 战/法/刺三选一
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

        // 英雄数据配置（与面板显示一致，用于运行时创建HeroData）
        private static readonly string[] heroNames = { "铁壁战士", "奥术法师", "暗影刺客" };
        private static readonly HeroClass[] heroClasses = { HeroClass.Warrior, HeroClass.Mage, HeroClass.Assassin };
        private static readonly string[] heroDescs = {
            "高防高血，近战输出，队伍前排坦克",
            "远程AOE法术输出，群体伤害专家",
            "高速爆发，闪避背刺，单体秒杀"
        };
        private static readonly int[,] heroStats = {
            { 150, 12, 15, 8 },   // 战士: HP ATK DEF SPD
            { 100, 18, 5, 10 },   // 法师
            { 90, 15, 6, 18 }     // 刺客
        };

        protected override void Awake()
        {
            base.Awake();
            allCards = new HeroCard[] { warriorCard, mageCard, assassinCard };
        }

        protected override void OnShow()
        {
            selectedIndex = -1;
            SelectedClass = HeroClass.Warrior;
            confirmButton.interactable = false;
            tipText.text = "选择你的英雄";

            // 填充英雄数据
            for (int i = 0; i < allCards.Length; i++)
            {
                var card = allCards[i];
                if (card.nameText) card.nameText.text = heroNames[i];
                if (card.classText) card.classText.text = ((HeroClass)i).ToString();
                if (card.hpText) card.hpText.text = $"HP {heroStats[i, 0]}";
                if (card.atkText) card.atkText.text = $"ATK {heroStats[i, 1]}";
                if (card.defText) card.defText.text = $"DEF {heroStats[i, 2]}";
                if (card.spdText) card.spdText.text = $"SPD {heroStats[i, 3]}";
                if (card.skillDescText) card.skillDescText.text = heroDescs[i];
                if (card.selectedMark) card.selectedMark.SetActive(false);
                if (card.border) card.border.color = Color.white;

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
            SelectedClass = heroClasses[index];
            confirmButton.interactable = true;

            // 更新选中高亮
            for (int i = 0; i < allCards.Length; i++)
            {
                bool selected = (i == index);
                if (allCards[i].selectedMark) allCards[i].selectedMark.SetActive(selected);
                if (allCards[i].border) allCards[i].border.color = selected ? Color.yellow : Color.white;
            }

            tipText.text = $"已选择: {heroNames[index]}";
        }

        private void OnConfirm()
        {
            if (selectedIndex < 0) return;

            // 1. 创建运行时HeroData（ScriptableObject.CreateInstance）
            HeroData heroData = ScriptableObject.CreateInstance<HeroData>();
            heroData.heroName = heroNames[selectedIndex];
            heroData.heroClass = heroClasses[selectedIndex];
            heroData.baseHealth = heroStats[selectedIndex, 0];
            heroData.baseAttack = heroStats[selectedIndex, 1];
            heroData.baseDefense = heroStats[selectedIndex, 2];
            heroData.baseSpeed = heroStats[selectedIndex, 3];
            heroData.baseCritRate = 0.05f;
            heroData.summonCost = heroClasses[selectedIndex] == HeroClass.Assassin ? 1 : 2;

            // 2. 创建Hero GameObject并初始化
            GameObject heroObj = new GameObject($"Hero_{heroData.heroName}");
            Hero hero = heroObj.AddComponent<Hero>();
            hero.Initialize(heroData, starLevel: 1);

            // 3. 注册到RoguelikeGameManager
            RoguelikeGameManager.Instance.SelectHero(hero);

            Debug.Log($"[HeroSelect] 创建英雄: {heroData.heroName} ({heroData.heroClass}) " +
                      $"HP={hero.MaxHealth} ATK={hero.Attack} DEF={hero.Defense} SPD={hero.Speed}");

            // 4. 切换到骰子阶段
            GameStateMachine.Instance.ChangeState(GameState.DiceRoll);
        }
    }
}
