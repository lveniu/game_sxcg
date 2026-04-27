using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选择初始英雄界面
/// </summary>
public class HeroSelectUI : MonoBehaviour
{
    [Header("英雄按钮")]
    public List<Button> heroButtons;
    public List<Text> heroNameTexts;
    public List<Text> heroDescTexts;

    [Header("数据")]
    public List<HeroData> heroChoices;

    void Start()
    {
        // 如果没有预设数据，用代码创建
        if (heroChoices == null || heroChoices.Count == 0)
        {
            heroChoices = new List<HeroData>
            {
                GameData.CreateTankHero(),
                GameData.CreateArcherHero(),
                GameData.CreateAssassinHero()
            };
        }

        for (int i = 0; i < heroButtons.Count && i < heroChoices.Count; i++)
        {
            int index = i;
            heroButtons[i].onClick.AddListener(() => OnSelectHero(index));

            if (heroNameTexts != null && i < heroNameTexts.Count)
                heroNameTexts[i].text = heroChoices[i].heroName;
            if (heroDescTexts != null && i < heroDescTexts.Count)
                heroDescTexts[i].text = heroChoices[i].description;
        }
    }

    void OnDestroy()
    {
        foreach (var btn in heroButtons)
        {
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
    }

    private void OnSelectHero(int index)
    {
        if (index < 0 || index >= heroChoices.Count) return;

        var heroData = heroChoices[index];
        var hero = CardDeck.Instance?.SummonHero(heroData);
        if (hero != null)
        {
            Debug.Log($"选择了英雄: {heroData.heroName}");
        }

        // 给予初始卡组
        var startingDeck = GameData.CreateStartingDeck();
        foreach (var card in startingDeck)
        {
            CardDeck.Instance?.AddCard(card);
        }

        GameStateMachine.Instance?.NextState(); // HeroSelect -> DiceRoll
    }
}
