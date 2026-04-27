using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗管理器 — 管理自走棋自动战斗流程
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("战斗配置")]
    public float battleTickInterval = 0.5f; // 每次行动间隔（秒）
    public float battleSpeed = 1f;
    public float maxBattleTime = 30f; // 最大战斗时间

    [Header("单位")]
    public List<Hero> playerUnits = new List<Hero>();
    public List<Hero> enemyUnits = new List<Hero>();

    public bool IsBattleActive { get; private set; }
    public float BattleTimer { get; private set; }
    public bool PlayerWon { get; private set; }

    private Coroutine battleCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 开始战斗
    /// </summary>
    public void StartBattle(List<Hero> players, List<Hero> enemies)
    {
        playerUnits = new List<Hero>(players);
        enemyUnits = new List<Hero>(enemies);
        IsBattleActive = true;
        BattleTimer = 0f;
        PlayerWon = false;

        Debug.Log($"战斗开始！我方{playerUnits.Count}人 vs 敌方{enemyUnits.Count}人");

        battleCoroutine = StartCoroutine(BattleLoop());
    }

    IEnumerator BattleLoop()
    {
        while (IsBattleActive)
        {
            yield return new WaitForSeconds(battleTickInterval / battleSpeed);

            // 我方单位行动
            foreach (var unit in playerUnits)
            {
                if (unit == null || unit.IsDead) continue;
                AutoChessAI.TakeAction(unit, enemyUnits);
            }

            // 敌方单位行动
            foreach (var unit in enemyUnits)
            {
                if (unit == null || unit.IsDead) continue;
                AutoChessAI.TakeAction(unit, playerUnits);
            }

            // 清理死亡单位
            RemoveDeadUnits();

            // 检查战斗结束
            if (CheckBattleEnd())
            {
                IsBattleActive = false;
                break;
            }

            BattleTimer += battleTickInterval;
        }

        EndBattle();
    }

    bool CheckBattleEnd()
    {
        bool allPlayerDead = playerUnits.TrueForAll(u => u == null || u.IsDead);
        bool allEnemyDead = enemyUnits.TrueForAll(u => u == null || u.IsDead);

        if (allPlayerDead || allEnemyDead)
            return true;

        // 超时判定：敌方胜利
        if (BattleTimer >= maxBattleTime)
            return true;

        return false;
    }

    void RemoveDeadUnits()
    {
        playerUnits.RemoveAll(u => u == null || u.IsDead);
        enemyUnits.RemoveAll(u => u == null || u.IsDead);
    }

    void EndBattle()
    {
        bool allEnemyDead = enemyUnits.TrueForAll(u => u == null || u.IsDead);
        bool allPlayerDead = playerUnits.TrueForAll(u => u == null || u.IsDead);

        PlayerWon = allEnemyDead && !allPlayerDead;

        if (PlayerWon)
            Debug.Log("战斗胜利！");
        else if (allPlayerDead)
            Debug.Log("战斗失败...");
        else
            Debug.Log("战斗超时！");

        // 通知状态机进入结算
        if (PlayerWon)
            GameStateMachine.Instance?.SetGameWon();
        else
            GameStateMachine.Instance?.SetGameLost();

        GameStateMachine.Instance?.NextState();
    }

    /// <summary>
    /// 加速战斗
    /// </summary>
    public void SetBattleSpeed(float speed)
    {
        battleSpeed = Mathf.Clamp(speed, 0.5f, 4f);
    }

    public void StopBattle()
    {
        IsBattleActive = false;
        if (battleCoroutine != null)
            StopCoroutine(battleCoroutine);
    }

    /// <summary>
    /// 清空战斗状态
    /// </summary>
    public void ClearBattle()
    {
        StopBattle();
        playerUnits.Clear();
        enemyUnits.Clear();
        BattleTimer = 0f;
    }
}
