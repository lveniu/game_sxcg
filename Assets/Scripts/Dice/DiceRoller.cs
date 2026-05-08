using System;
using UnityEngine;

/// <summary>
/// 骰子投掷管理器 — 核心赌狗机制
/// 管理投掷、重摇、组合评估
/// </summary>
public class DiceRoller
{
    public Dice[] Dices { get; private set; }

    /// <summary>
    /// 免费重摇次数（肉鸽模式默认1次）
    /// </summary>
    public int FreeRerolls { get; private set; } = 1;

    /// <summary>
    /// 已使用的重摇次数
    /// </summary>
    public int UsedRerolls { get; private set; } = 0;

    /// <summary>
    /// 是否还能重摇
    /// </summary>
    public bool CanReroll => UsedRerolls < FreeRerolls;

    /// <summary>
    /// 剩余重摇次数
    /// </summary>
    public int RemainingRerolls => FreeRerolls - UsedRerolls;

    // 事件
    public event Action<int[]> OnDiceRolled;
    public event Action<int> OnRerollUsed; // 参数为已使用次数
    public event Action OnRerollsExhausted;

    public DiceRoller(int diceCount = 3, int sides = 6)
    {
        Dices = new Dice[diceCount];
        for (int i = 0; i < diceCount; i++)
        {
            Dices[i] = new Dice(sides);
        }
    }

    /// <summary>
    /// 投掷所有骰子（忽略锁定状态）
    /// </summary>
    public int[] RollAll()
    {
        int[] results = new int[Dices.Length];
        for (int i = 0; i < Dices.Length; i++)
        {
            results[i] = Dices[i].Roll();
        }
        OnDiceRolled?.Invoke(results);
        return results;
    }

    /// <summary>
    /// 重摇 — 核心赌狗机制
    /// </summary>
    /// <param name="keepMask">保留面具，true=保留不重投</param>
    /// <returns>新的骰子点数数组</returns>
    public int[] Reroll(bool[] keepMask)
    {
        if (!CanReroll)
        {
            OnRerollsExhausted?.Invoke();
            return GetCurrentValues();
        }

        if (keepMask == null || keepMask.Length != Dices.Length)
        {
            keepMask = new bool[Dices.Length];
        }

        UsedRerolls++;
        int[] results = new int[Dices.Length];
        for (int i = 0; i < Dices.Length; i++)
        {
            if (!keepMask[i])
                results[i] = Dices[i].Roll();
            else
                results[i] = Dices[i].CurrentValue;
        }

        OnRerollUsed?.Invoke(UsedRerolls);
        if (!CanReroll)
            OnRerollsExhausted?.Invoke();

        return results;
    }

    /// <summary>
    /// 全部重摇（不保留任何骰子）
    /// </summary>
    public int[] RerollAll()
    {
        return Reroll(new bool[Dices.Length]);
    }

    /// <summary>
    /// 保留指定索引的骰子，重摇其他
    /// </summary>
    public int[] RerollExcept(int keepIndex)
    {
        bool[] keepMask = new bool[Dices.Length];
        if (keepIndex >= 0 && keepIndex < Dices.Length)
            keepMask[keepIndex] = true;
        return Reroll(keepMask);
    }

    /// <summary>
    /// 获取当前所有骰子的点数
    /// </summary>
    public int[] GetCurrentValues()
    {
        int[] values = new int[Dices.Length];
        for (int i = 0; i < Dices.Length; i++)
        {
            values[i] = Dices[i].CurrentValue;
        }
        return values;
    }

    /// <summary>
    /// 获取当前骰子的组合
    /// </summary>
    public DiceCombination GetCurrentCombination()
    {
        return DiceCombinationEvaluator.Evaluate(GetCurrentValues());
    }

    /// <summary>
    /// 重置状态（进入新一关时调用）
    /// </summary>
    public void Reset()
    {
        UsedRerolls = 0;
        foreach (var dice in Dices)
        {
            dice.IsLocked = false;
        }
    }

    /// <summary>
    /// 设置免费重摇次数（局外成长可改变）
    /// </summary>
    public void SetFreeRerolls(int count)
    {
        FreeRerolls = count;
    }
}
