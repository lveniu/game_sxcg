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

    /// <summary>
    /// 初始面值备份，用于 Reset()
    /// </summary>
    private int[] _initialFaces;

    public Dice(int sides = 6)
    {
        Faces = new int[sides];
        FaceEffects = new string[sides];
        for (int i = 0; i < sides; i++)
        {
            Faces[i] = i + 1;
        }
        // 备份初始面值
        _initialFaces = new int[sides];
        System.Array.Copy(Faces, _initialFaces, sides);
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

    /// <summary>
    /// 升级指定面的数值
    /// </summary>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="newValue">新的面值</param>
    /// <returns>是否升级成功</returns>
    public bool UpgradeFace(int faceIndex, int newValue)
    {
        if (faceIndex < 0 || faceIndex >= Faces.Length)
        {
            Debug.LogWarning($"[Dice] UpgradeFace 失败：faceIndex {faceIndex} 越界");
            return false;
        }
        if (newValue < 1)
        {
            Debug.LogWarning($"[Dice] UpgradeFace 失败：newValue {newValue} 不能小于1");
            return false;
        }
        int oldValue = Faces[faceIndex];
        Faces[faceIndex] = newValue;
        Debug.Log($"[Dice] 面升级：面{faceIndex + 1} 值 {oldValue} → {newValue}");
        return true;
    }

    /// <summary>
    /// 给指定面添加特殊效果
    /// </summary>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="effectId">效果ID（如 "lightning", "shield", "heal" 等）</param>
    /// <returns>是否添加成功</returns>
    public bool AddSpecialEffect(int faceIndex, string effectId)
    {
        if (faceIndex < 0 || faceIndex >= Faces.Length)
        {
            Debug.LogWarning($"[Dice] AddSpecialEffect 失败：faceIndex {faceIndex} 越界");
            return false;
        }
        if (string.IsNullOrEmpty(effectId))
        {
            Debug.LogWarning("[Dice] AddSpecialEffect 失败：effectId 为空");
            return false;
        }
        string oldEffect = FaceEffects[faceIndex];
        FaceEffects[faceIndex] = effectId;
        Debug.Log($"[Dice] 面效果变更：面{faceIndex + 1} [{oldEffect ?? "无"}] → [{effectId}]");
        return true;
    }

    /// <summary>
    /// 移除指定面的特殊效果
    /// </summary>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <returns>是否移除成功（面原本没有效果也算成功）</returns>
    public bool RemoveSpecialEffect(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= Faces.Length)
        {
            Debug.LogWarning($"[Dice] RemoveSpecialEffect 失败：faceIndex {faceIndex} 越界");
            return false;
        }
        string oldEffect = FaceEffects[faceIndex];
        FaceEffects[faceIndex] = null;
        if (!string.IsNullOrEmpty(oldEffect))
        {
            Debug.Log($"[Dice] 面效果移除：面{faceIndex + 1} [{oldEffect}] 已清除");
        }
        return true;
    }

    /// <summary>
    /// 重置骰子到初始状态（面值+效果全部还原）
    /// </summary>
    public void Reset()
    {
        // 恢复初始面值
        if (_initialFaces != null)
        {
            System.Array.Copy(_initialFaces, Faces, Faces.Length);
        }
        // 清除所有面效果
        for (int i = 0; i < FaceEffects.Length; i++)
        {
            FaceEffects[i] = null;
        }
        // 重置运行时状态
        CurrentValue = 0;
        IsLocked = false;
        Debug.Log("[Dice] 骰子已重置到初始状态");
    }
}
