using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算界面
/// </summary>
public class SettlementUI : MonoBehaviour
{
    [Header("结果显示")]
    public Text resultText;
    public Text levelText;

    [Header("奖励选择")]
    public Transform rewardParent;
    public GameObject rewardButtonPrefab;

    [Header("操作按钮")]
    public Button nextLevelButton;
    public Button returnMenuButton;

    private CardDeck deck;

    void Start()
    {
        deck = CardDeck.Instance;

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(OnNextLevel);
        if (returnMenuButton != null)
            returnMenuButton.onClick.AddListener(OnReturnMenu);
    }

    void OnDestroy()
    {
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(OnNextLevel);
        if (returnMenuButton != null) returnMenuButton.onClick.RemoveListener(OnReturnMenu);
    }

    void OnEnable()
    {
        RefreshSettlement();
    }

    private void RefreshSettlement()
    {
        if (GameStateMachine.Instance == null) return;

        bool won = GameStateMachine.Instance.IsGameWon;
        int level = GameStateMachine.Instance.CurrentLevel;

        if (resultText != null)
            resultText.text = won ? "🎉 胜利！" : "💀 失败...";
        if (levelText != null)
            levelText.text = $"第{level}关";

        if (won)
        {
            ShowRewardChoices();
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(true);
        }
        else
        {
            ClearRewards();
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(false);
        }
    }

    private void ShowRewardChoices()
    {
        ClearRewards();
        if (rewardParent == null || rewardButtonPrefab == null) return;

        var rewards = GameData.CreateRewardCards();
        foreach (var card in rewards)
        {
            var go = Instantiate(rewardButtonPrefab, rewardParent);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = card.CardName;
            if (btn != null)
            {
                var c = card;
                btn.onClick.AddListener(() => OnSelectReward(c));
            }
        }
    }

    private void ClearRewards()
    {
        if (rewardParent == null) return;
        foreach (Transform child in rewardParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnSelectReward(CardInstance card)
    {
        deck?.AddCard(card);
        Debug.Log($"获得奖励: {card.CardName}");
        ClearRewards();
    }

    private void OnNextLevel()
    {
        GameStateMachine.Instance?.NextState(); // Settlement -> DiceRoll (下一关)
    }

    private void OnReturnMenu()
    {
        GameStateMachine.Instance?.ResetGame();
    }
}
