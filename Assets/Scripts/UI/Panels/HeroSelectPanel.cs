using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 英雄选择面板 - 战/法/刺三选一
    /// UI元素：
    /// - heroCard1/2/3: 3张英雄卡片Button
    /// - 英雄名称Text x3
    /// - 英雄属性Text x3（HP/ATK/DEF/SPD）
    /// - 英雄技能描述Text x3
    /// - 确认选择Button
    /// </summary>
    public class HeroSelectPanel : UIPanel
    {
        [Header("UI引用")]
        public Button warriorCard;   // 战士卡片
        public Button mageCard;      // 法师卡片
        public Button assassinCard;  // 刺客卡片
        public Text warriorName;
        public Text mageName;
        public Text assassinName;

        private int selectedHeroIndex = -1;

        protected override void OnShow()
        {
            selectedHeroIndex = -1;
            warriorCard?.onClick.AddListener(() => SelectHero(0));
            mageCard?.onClick.AddListener(() => SelectHero(1));
            assassinCard?.onClick.AddListener(() => SelectHero(2));
        }

        protected override void OnHide()
        {
            warriorCard?.onClick.RemoveAllListeners();
            mageCard?.onClick.RemoveAllListeners();
            assassinCard?.onClick.RemoveAllListeners();
        }

        private void SelectHero(int index)
        {
            selectedHeroIndex = index;
            // TODO: 高亮选中卡片
            // 选择后直接进入骰子阶段
            GameStateMachine.Instance.ChangeState(GameState.DiceRoll);
        }
    }
}
