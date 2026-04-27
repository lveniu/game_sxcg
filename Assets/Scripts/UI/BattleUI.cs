using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自动战斗界面
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("我方单位")]
    public Transform playerPanel;
    public GameObject unitInfoPrefab;

    [Header("敌方单位")]
    public Transform enemyPanel;

    [Header("战斗控制")]
    public Button speedButton;
    public Button skipButton;
    public Text timerText;

    [Header("战斗日志")]
    public Text battleLogText;

    private BattleManager battle;
    private List<GameObject> unitInfoItems = new List<GameObject>();

    void Start()
    {
        battle = BattleManager.Instance;

        if (speedButton != null)
            speedButton.onClick.AddListener(OnToggleSpeed);
        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipBattle);
    }

    void OnDestroy()
    {
        if (speedButton != null) speedButton.onClick.RemoveListener(OnToggleSpeed);
        if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipBattle);
    }

    void OnEnable()
    {
        ClearUnitInfo();
        if (battle != null)
        {
            ShowUnits(battle.playerUnits, playerPanel);
            ShowUnits(battle.enemyUnits, enemyPanel);
        }
    }

    void Update()
    {
        if (battle != null && battle.IsBattleActive)
        {
            if (timerText != null)
                timerText.text = $"时间: {battle.BattleTimer:F1}s";
        }
    }

    private void ShowUnits(List<Hero> units, Transform parent)
    {
        if (parent == null || unitInfoPrefab == null) return;

        foreach (var unit in units)
        {
            if (unit == null) continue;
            var go = Instantiate(unitInfoPrefab, parent);
            var texts = go.GetComponentsInChildren<Text>();
            if (texts.Length >= 2)
            {
                texts[0].text = unit.Data.heroName;
                texts[1].text = $"HP: {unit.CurrentHealth}/{unit.MaxHealth}";
            }
            unitInfoItems.Add(go);
        }
    }

    private void ClearUnitInfo()
    {
        foreach (var go in unitInfoItems)
        {
            if (go != null) Destroy(go);
        }
        unitInfoItems.Clear();

        if (playerPanel != null)
            foreach (Transform child in playerPanel) Destroy(child.gameObject);
        if (enemyPanel != null)
            foreach (Transform child in enemyPanel) Destroy(child.gameObject);
    }

    private void OnToggleSpeed()
    {
        if (battle == null) return;
        float newSpeed = battle.battleSpeed >= 2f ? 1f : battle.battleSpeed * 2f;
        battle.SetBattleSpeed(newSpeed);
        if (speedButton != null)
        {
            var txt = speedButton.GetComponentInChildren<Text>();
            if (txt != null) txt.text = $"x{newSpeed}";
        }
    }

    private void OnSkipBattle()
    {
        if (battle != null)
        {
            battle.StopBattle();
        }
    }

    /// <summary>
    /// 添加一条战斗日志（由BattleManager调用）
    /// </summary>
    public void AppendLog(string message)
    {
        if (battleLogText != null)
        {
            battleLogText.text += message + "\n";
        }
    }
}
