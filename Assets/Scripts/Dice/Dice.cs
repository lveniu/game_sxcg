using UnityEngine;

/// <summary>
/// 单个骰子，支持多面和特殊面效果
/// </summary>
public class Dice
{
    public int[] Faces { get; private set; }
    public int CurrentValue { get; private set; }
    public bool IsLocked { get; set; }

    /// <summary>
    /// 特殊面效果，索引对应 Faces 数组下标
    /// </summary>
    public string[] FaceEffects { get; private set; }

    public Dice(int sides = 6)
    {
        Faces = new int[sides];
        FaceEffects = new string[sides];
        for (int i = 0; i < sides; i++)
        {
            Faces[i] = i + 1;
        }
    }

    /// <summary>
    /// 投掷骰子，如果被锁定则保持原值
    /// </summary>
    public int Roll()
    {
        if (IsLocked) return CurrentValue;
        CurrentValue = Random.Range(1, Faces.Length + 1);
        return CurrentValue;
    }

    public void SetValue(int value)
    {
        if (value >= 1 && value <= Faces.Length)
            CurrentValue = value;
    }

    /// <summary>
    /// 替换某一面为特殊效果
    /// </summary>
    public void UpgradeFace(int faceIndex, string effect)
    {
        if (faceIndex >= 0 && faceIndex < Faces.Length)
        {
            FaceEffects[faceIndex] = effect;
        }
    }
}
