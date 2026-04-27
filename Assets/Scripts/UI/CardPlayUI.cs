using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出牌 + 棋盘站位界面
/// </summary>
public class CardPlayUI : MonoBehaviour
{
    [Header("手牌区")]
    public Transform handCardParent;
    public GameObject cardButtonPrefab;

    [Header("棋盘站位")]
    public List<Button> gridButtons; // 3x4=12个按钮
    public List<Text> gridTexts;

    [Header("操作按钮")]
    public Button summonButton;
    public Button startBattleButton;
    public Text populationText;

    [Header("组合显示")]
    public Text comboText;

    private CardDeck deck;
    private GridManager grid;
    private CardInstance selectedCard;
    private int selectedGridIndex = -1;

    void Start()
    {
        deck = CardDeck.Instance;
        grid = GridManager.Instance;

        if (summonButton != null)
            summonButton.onClick.AddListener(OnSummonHero);
        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(OnStartBattle);

        // 初始化棋盘按钮
        for (int i = 0; i < gridButtons.Count; i++)
        {
            int index = i;
            gridButtons[i].onClick.AddListener(() => OnSelectGrid(index));
        }
    }

    void OnDestroy()
    {
        if (summonButton != null) summonButton.onClick.RemoveListener(OnSummonHero);
        if (startBattleButton != null) startBattleButton.onClick.RemoveListener(OnStartBattle);
        foreach (var btn in gridButtons)
        {
            if (btn != null) btn.onClick.RemoveAllListeners();
        }
    }

    void OnEnable()
    {
        RefreshHandCards();
        RefreshGrid();
        UpdatePopulationText();
        UpdateComboText();
    }

    /// <summary>
    /// 刷新手牌显示
    /// </summary>
    private void RefreshHandCards()
    {
        if (deck == null || handCardParent == null) return;

        // 清除旧卡牌
        foreach (Transform child in handCardParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var card in deck.handCards)
        {
            var go = Instantiate(cardButtonPrefab, handCardParent);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = $"{card.CardName}\n({card.Type})";
            if (btn != null)
            {
                var c = card;
                btn.onClick.AddListener(() => OnSelectCard(c));
            }
        }
    }

    private void OnSelectCard(CardInstance card)
    {
        selectedCard = card;
        Debug.Log($"选中卡牌: {card.CardName}");
    }

    private void OnSelectGrid(int index)
    {
        selectedGridIndex = index;
        if (gridTexts != null && index < gridTexts.Count)
        {
            Debug.Log($"选中格子: {index}");
        }
    }

    private void OnSummonHero()
    {
        if (deck == null || selectedCard == null) return;

        // 简化：直接召唤一个新英雄
        var heroData = GameData.CreateTankHero(); // 默认召唤坦克
        var hero = deck.SummonHero(heroData);
        if (hero != null)
        {
            Debug.Log($"召唤英雄: {heroData.heroName}");
            RefreshHandCards();
            RefreshGrid();
            UpdatePopulationText();
        }
    }

    private void OnStartBattle()
    {
        // 应用站位效果
        deck?.ApplyPositioningToField(grid);
        GameStateMachine.Instance?.NextState(); // CardPlay/Positioning -> Battle
    }

    private void RefreshGrid()
    {
        if (grid == null || gridTexts == null) return;

        for (int i = 0; i < gridTexts.Count; i++)
        {
            int x = i % 3;
            int y = i / 3;
            var cell = grid.GetCell(x, y);
            if (cell != null && cell.IsOccupied)
            {
                gridTexts[i].text = cell.Occupant.Data.heroName;
            }
            else
            {
                gridTexts[i].text = "";
            }
        }
    }

    private void UpdatePopulationText()
    {
        if (populationText != null && deck != null)
            populationText.text = $"人口: {deck.CurrentPopulation}/{deck.maxPopulation}";
    }

    private void UpdateComboText()
    {
        if (comboText != null)
        {
            var combo = GameManager.Instance?.GetCurrentDiceCombination();
            comboText.text = combo != null ? $"骰子组合: {combo.Description}" : "";
        }
    }
}
