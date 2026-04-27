using UnityEngine;

/// <summary>
/// 骰子系统测试脚本 — 用于验证核心赌狗机制
/// 挂载在场景中的任意GameObject上即可测试
/// </summary>
public class DiceTest : MonoBehaviour
{
    [Header("测试配置")]
    public int diceCount = 3;
    public int diceSides = 6;
    public int freeRerolls = 2;

    private DiceRoller roller;

    void Start()
    {
        Debug.Log("========== 骰子系统测试开始 ==========");
        roller = new DiceRoller(diceCount, diceSides);
        roller.SetFreeRerolls(freeRerolls);

        // 订阅事件
        roller.OnDiceRolled += OnDiceRolled;
        roller.OnRerollUsed += OnRerollUsed;
        roller.OnRerollsExhausted += OnRerollsExhausted;

        // 测试流程
        TestRollSequence();
    }

    void TestRollSequence()
    {
        Debug.Log("\n--- 第一次投掷 ---");
        int[] results = roller.RollAll();
        PrintResults(results);

        // 模拟重摇：保留第一个骰子，重摇其他
        if (roller.CanReroll)
        {
            Debug.Log("\n--- 重摇（保留第1个骰子）---");
            bool[] keepMask = new bool[diceCount];
            keepMask[0] = true;
            int[] rerollResults = roller.Reroll(keepMask);
            PrintResults(rerollResults);
        }

        // 再重摇一次
        if (roller.CanReroll)
        {
            Debug.Log("\n--- 再次重摇（全部重投）---");
            int[] rerollResults = roller.RerollAll();
            PrintResults(rerollResults);
        }

        // 尝试第三次重摇（应该失败）
        if (!roller.CanReroll)
        {
            Debug.Log("\n--- 尝试第三次重摇 ---");
            Debug.Log($"剩余重摇次数: {roller.RemainingRerolls}，无法重摇");
        }

        Debug.Log("\n========== 骰子系统测试结束 ==========");
    }

    void PrintResults(int[] results)
    {
        string resultStr = string.Join(", ", results);
        var combo = DiceCombinationEvaluator.Evaluate(results);

        Debug.Log($"骰子点数: [{resultStr}]");
        Debug.Log($"组合: {combo.Description}");
        Debug.Log($"组合效果: {combo.EffectDescription}");
        Debug.Log($"已用重摇: {roller.UsedRerolls} / {roller.FreeRerolls}");
    }

    void OnDiceRolled(int[] results)
    {
        // 测试事件回调
    }

    void OnRerollUsed(int usedCount)
    {
        Debug.Log($"[事件] 已使用第{usedCount}次重摇");
    }

    void OnRerollsExhausted()
    {
        Debug.Log("[事件] 重摇次数已耗尽");
    }
}
