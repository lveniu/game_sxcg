using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 骰子投掷界面
/// </summary>
public class DiceRollUI : MonoBehaviour
{
    [Header("骰子显示")]
    public List<Text> diceValueTexts;
    public List<Image> diceImages;

    [Header("按钮")]
    public Button rollButton;
    public Button rerollButton;
    public Button nextPhaseButton;

    [Header("组合显示")]
    public Text combinationText;
    public Text effectText;
    public Text rerollCountText;

    private DiceRoller diceRoller;

    void Start()
    {
        diceRoller = GameManager.Instance?.DiceRoller;

        if (rollButton != null)
            rollButton.onClick.AddListener(OnRollDice);
        if (rerollButton != null)
            rerollButton.onClick.AddListener(OnReroll);
        if (nextPhaseButton != null)
            nextPhaseButton.onClick.AddListener(OnNextPhase);

        UpdateRerollUI();
    }

    void OnDestroy()
    {
        if (rollButton != null) rollButton.onClick.RemoveListener(OnRollDice);
        if (rerollButton != null) rerollButton.onClick.RemoveListener(OnReroll);
        if (nextPhaseButton != null) nextPhaseButton.onClick.RemoveListener(OnNextPhase);
    }

    void OnEnable()
    {
        UpdateDiceDisplay();
        UpdateRerollUI();
    }

    private void OnRollDice()
    {
        if (diceRoller == null) return;

        int[] results = diceRoller.RollAll();
        UpdateDiceDisplay();
        UpdateCombinationDisplay();
        UpdateRerollUI();

        rollButton.interactable = false; // 投掷后禁用
    }

    private void OnReroll()
    {
        if (diceRoller == null || !diceRoller.CanReroll) return;

        int[] results = diceRoller.RerollAll();
        UpdateDiceDisplay();
        UpdateCombinationDisplay();
        UpdateRerollUI();
    }

    private void OnNextPhase()
    {
        GameStateMachine.Instance?.NextState(); // DiceRoll -> CardPlay
    }

    private void UpdateDiceDisplay()
    {
        if (diceRoller == null) return;
        int[] values = diceRoller.GetCurrentValues();

        for (int i = 0; i < diceValueTexts.Count && i < values.Length; i++)
        {
            diceValueTexts[i].text = values[i].ToString();
        }
    }

    private void UpdateCombinationDisplay()
    {
        if (diceRoller == null) return;
        var combo = diceRoller.GetCurrentCombination();

        if (combinationText != null)
            combinationText.text = combo.Description;
        if (effectText != null)
            effectText.text = combo.EffectDescription;
    }

    private void UpdateRerollUI()
    {
        if (diceRoller == null) return;

        if (rerollCountText != null)
            rerollCountText.text = $"剩余重摇: {diceRoller.RemainingRerolls}";

        if (rerollButton != null)
            rerollButton.interactable = diceRoller.CanReroll;
    }
}
