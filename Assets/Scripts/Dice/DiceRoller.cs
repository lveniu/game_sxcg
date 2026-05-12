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

    /// <summary>
    /// 增加免费重摇次数（叠加式，用于面效果/遗物等）
    /// </summary>
    /// <param name="count">增加的次数</param>
    public void AddFreeRerolls(int count)
    {
        FreeRerolls += count;
        Debug.Log($"[DiceRoller] 免费重摇次数 +{count}，当前: {FreeRerolls}");
    }

    /// <summary>
    /// 升级指定骰子的面值
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="newValue">新的面值</param>
    /// <returns>是否升级成功</returns>
    public bool UpgradeDice(int diceIndex, int faceIndex, int newValue)
    {
        if (diceIndex < 0 || diceIndex >= Dices.Length)
        {
            Debug.LogWarning($"[DiceRoller] UpgradeDice 失败：diceIndex {diceIndex} 越界（共 {Dices.Length} 颗骰子）");
            return false;
        }
        return Dices[diceIndex].UpgradeFace(faceIndex, newValue);
    }

    /// <summary>
    /// 给指定骰子的面添加特殊效果
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="effectId">效果ID（如 "lightning", "shield" 等）</param>
    /// <returns>是否添加成功</returns>
    public bool AddEffectToFace(int diceIndex, int faceIndex, string effectId)
    {
        if (diceIndex < 0 || diceIndex >= Dices.Length)
        {
            Debug.LogWarning($"[DiceRoller] AddEffectToFace 失败：diceIndex {diceIndex} 越界");
            return false;
        }
        return Dices[diceIndex].AddSpecialEffect(faceIndex, effectId);
    }

    /// <summary>
    /// 获取指定骰子面从当前值升级到目标值的费用
    /// 从 dice_system.json 的 face_upgrade 配置计算
    /// 费用公式：基础费用 × (目标值 - 当前面值)，基础费用从 BalanceProvider 获取
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="targetValue">目标面值</param>
    /// <returns>升级费用（金币）；如果无法升级返回 -1</returns>
    public int GetUpgradeCost(int diceIndex, int faceIndex, int targetValue)
    {
        if (diceIndex < 0 || diceIndex >= Dices.Length) return -1;
        var dice = Dices[diceIndex];
        if (faceIndex < 0 || faceIndex >= dice.Faces.Length) return -1;

        int currentValue = dice.Faces[faceIndex];
        if (targetValue <= currentValue) return -1; // 目标值必须大于当前值

        // 通过 DiceUpgradeEngine 计算费用（如果存在），否则用默认公式
        var engine = DiceUpgradeEngine.Instance;
        if (engine != null)
        {
            return engine.CalculateCost(currentValue, targetValue);
        }

        // 默认费用公式：每级 10 金币
        return (targetValue - currentValue) * 10;
    }
}
