using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 骰子升级引擎 — 管理骰子升级的经济和规则
/// 负责：费用计算、金币扣除、升级执行、效果应用
/// 配置来源：dice_system.json 的 face_upgrade 段
/// </summary>
public class DiceUpgradeEngine
{
    // ========== 单例 ==========
    private static DiceUpgradeEngine _instance;

    /// <summary>
    /// 获取升级引擎实例（懒加载）
    /// </summary>
    public static DiceUpgradeEngine Instance
    {
        get
        {
            if (_instance == null)
                _instance = new DiceUpgradeEngine();
            return _instance;
        }
    }

    // ========== 事件 ==========
    /// <summary>升级完成事件（diceIndex, faceIndex, newValue）</summary>
    public event System.Action<int, int, int> OnUpgradeCompleted;

    /// <summary>效果应用完成事件（diceIndex, faceIndex, effectId）</summary>
    public event System.Action<int, int, string> OnEffectApplied;

    /// <summary>升级失败事件（diceIndex, faceIndex, reason）</summary>
    public event System.Action<int, int, string> OnUpgradeFailed;

    // ========== 配置 ==========
    private FaceUpgradeConfig _upgradeConfig;
    private List<SpecialFaceEntry> _specialFaces;

    // ========== 升级费用表 ==========
    /// <summary>
    /// 每级升级费用表（索引=面值-1，值为升级到该面值的费用）
    /// 默认: [0, 0, 10, 20, 35, 55, 80] → 面1→面2=10金, 面2→面3=20金...
    /// </summary>
    private readonly int[] _levelCosts = { 0, 0, 10, 20, 35, 55, 80, 110, 150, 200 };

    /// <summary>
    /// 构造函数 — 从配置加载升级规则
    /// </summary>
    public DiceUpgradeEngine()
    {
        LoadConfig();
    }

    /// <summary>
    /// 从 BalanceProvider 加载配置
    /// </summary>
    private void LoadConfig()
    {
        var diceConfig = BalanceProvider.DiceSystem;
        if (diceConfig?.face_upgrade != null)
        {
            _upgradeConfig = diceConfig.face_upgrade;
            _specialFaces = _upgradeConfig.special_faces ?? new List<SpecialFaceEntry>();
        }
        else
        {
            _specialFaces = new List<SpecialFaceEntry>();
            Debug.LogWarning("[DiceUpgradeEngine] dice_system.json 中缺少 face_upgrade 配置，使用默认值");
        }
    }

    /// <summary>
    /// 重新加载配置（热重载用）
    /// </summary>
    public void ReloadConfig()
    {
        LoadConfig();
        Debug.Log("[DiceUpgradeEngine] 配置已重新加载");
    }

    // ========== 费用计算 ==========

    /// <summary>
    /// 计算从当前等级升级到目标等级的总费用
    /// 费用公式：累加每一级的升级费用
    /// </summary>
    /// <param name="currentLevel">当前面值</param>
    /// <param name="targetLevel">目标面值</param>
    /// <returns>总升级费用（金币）</returns>
    public int CalculateCost(int currentLevel, int targetLevel)
    {
        if (targetLevel <= currentLevel) return 0;

        int totalCost = 0;
        for (int level = currentLevel + 1; level <= targetLevel; level++)
        {
            totalCost += GetSingleLevelCost(level);
        }
        return totalCost;
    }

    /// <summary>
    /// 获取升到指定等级的单级费用
    /// </summary>
    /// <param name="level">目标等级（面值）</param>
    /// <returns>该级费用</returns>
    private int GetSingleLevelCost(int level)
    {
        if (level >= 0 && level < _levelCosts.Length)
            return _levelCosts[level];
        // 超出预定义表的按递推公式: 前一级 + 50
        return 200 + (level - _levelCosts.Length + 1) * 50;
    }

    /// <summary>
    /// 检查玩家金币是否足够支付升级
    /// </summary>
    /// <param name="cost">需要的金币数</param>
    /// <returns>是否负担得起</returns>
    public bool CanAfford(int cost)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[DiceUpgradeEngine] PlayerInventory 实例不存在");
            return false;
        }
        return inventory.Gold >= cost;
    }

    // ========== 升级执行 ==========

    /// <summary>
    /// 执行升级操作：扣金币 + 更新骰子面值
    /// 完整流程：验证 → 扣费 → 升级 → 通知
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="targetValue">目标面值</param>
    /// <returns>是否升级成功</returns>
    public bool ExecuteUpgrade(int diceIndex, int faceIndex, int targetValue)
    {
        // 1. 获取骰子引用
        var roller = GetDiceRoller();
        if (roller == null)
        {
            NotifyFailed(diceIndex, faceIndex, "DiceRoller 不可用");
            return false;
        }
        if (diceIndex < 0 || diceIndex >= roller.Dices.Length)
        {
            NotifyFailed(diceIndex, faceIndex, $"骰子索引 {diceIndex} 越界");
            return false;
        }

        var dice = roller.Dices[diceIndex];
        if (faceIndex < 0 || faceIndex >= dice.Faces.Length)
        {
            NotifyFailed(diceIndex, faceIndex, $"面索引 {faceIndex} 越界");
            return false;
        }

        int currentValue = dice.Faces[faceIndex];
        if (targetValue <= currentValue)
        {
            NotifyFailed(diceIndex, faceIndex, $"目标值 {targetValue} 不大于当前值 {currentValue}");
            return false;
        }

        // 2. 计算费用
        int cost = CalculateCost(currentValue, targetValue);

        // 3. 检查金币
        if (!CanAfford(cost))
        {
            NotifyFailed(diceIndex, faceIndex, $"金币不足：需要 {cost}，当前 {PlayerInventory.Instance?.Gold ?? 0}");
            return false;
        }

        // 4. 扣除金币
        var inventory = PlayerInventory.Instance;
        if (!inventory.SpendGold(cost))
        {
            NotifyFailed(diceIndex, faceIndex, "扣款失败");
            return false;
        }

        // 5. 执行升级
        bool upgraded = dice.UpgradeFace(faceIndex, targetValue);
        if (!upgraded)
        {
            // 回滚：退还金币
            inventory.AddGold(cost);
            NotifyFailed(diceIndex, faceIndex, "UpgradeFace 调用失败");
            return false;
        }

        // 6. 通知
        Debug.Log($"[DiceUpgradeEngine] 升级成功：骰子{diceIndex + 1} 面{faceIndex + 1} " +
                  $"值 {currentValue}→{targetValue}，花费 {cost} 金币");
        OnUpgradeCompleted?.Invoke(diceIndex, faceIndex, targetValue);
        return true;
    }

    // ========== 效果应用 ==========

    /// <summary>
    /// 获取可用的骰面特殊效果列表（从 dice_system.json 读取）
    /// </summary>
    /// <returns>特殊面效果列表</returns>
    public List<SpecialFaceEntry> GetAvailableEffects()
    {
        return _specialFaces ?? new List<SpecialFaceEntry>();
    }

    /// <summary>
    /// 按效果ID获取特殊面效果配置
    /// </summary>
    /// <param name="effectId">效果ID</param>
    /// <returns>特殊面配置，不存在则返回 null</returns>
    public SpecialFaceEntry GetEffectById(string effectId)
    {
        if (string.IsNullOrEmpty(effectId) || _specialFaces == null) return null;
        return _specialFaces.Find(e => e.id == effectId);
    }

    /// <summary>
    /// 给指定骰子面应用特殊效果
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <param name="effectId">效果ID</param>
    /// <returns>是否应用成功</returns>
    /// <summary>
    /// 计算骰面效果应用的费用
    /// 基于效果配置中的 cost 字段，未配置则免费
    /// </summary>
    private int CalculateEffectCost(string effectId)
    {
        var effectEntry = GetEffectById(effectId);
        if (effectEntry == null) return 0;
        // 效果配置中的 cost 字段（如有）
        return effectEntry.cost;
    }

    public bool ApplyEffect(int diceIndex, int faceIndex, string effectId)
    {
        // 1. 验证效果ID是否存在
        var effectEntry = GetEffectById(effectId);
        if (effectEntry == null)
        {
            Debug.LogWarning($"[DiceUpgradeEngine] 效果不存在: {effectId}");
            NotifyFailed(diceIndex, faceIndex, $"效果ID不存在: {effectId}");
            return false;
        }

        // 2. 计算效果应用费用
        int cost = CalculateEffectCost(effectId);
        if (cost > 0)
        {
            if (!CanAfford(cost))
            {
                NotifyFailed(diceIndex, faceIndex, $"金币不足：需要 {cost}，当前 {PlayerInventory.Instance?.Gold ?? 0}");
                return false;
            }
            var inventory = PlayerInventory.Instance;
            if (inventory != null && !inventory.SpendGold(cost))
            {
                NotifyFailed(diceIndex, faceIndex, "扣款失败");
                return false;
            }
        }

        // 3. 获取骰子引用
        var roller = GetDiceRoller();
        if (roller == null)
        {
            // 回滚金币
            if (cost > 0) PlayerInventory.Instance?.AddGold(cost);
            NotifyFailed(diceIndex, faceIndex, "DiceRoller 不可用");
            return false;
        }

        // 4. 通过 DiceRoller 添加效果
        bool applied = roller.AddEffectToFace(diceIndex, faceIndex, effectId);
        if (!applied)
        {
            // 回滚金币
            if (cost > 0) PlayerInventory.Instance?.AddGold(cost);
            NotifyFailed(diceIndex, faceIndex, "AddEffectToFace 调用失败");
            return false;
        }

        // 5. 通知
        Debug.Log($"[DiceUpgradeEngine] 效果已应用：骰子{diceIndex + 1} 面{faceIndex + 1} → {effectEntry.name_cn}({effectId})，花费 {cost} 金币");
        OnEffectApplied?.Invoke(diceIndex, faceIndex, effectId);
        return true;
    }

    // ========== 升级预览 ==========

    /// <summary>
    /// 升级预览数据结构
    /// </summary>
    public class UpgradePreview
    {
        /// <summary>骰子索引</summary>
        public int DiceIndex;
        /// <summary>面索引</summary>
        public int FaceIndex;
        /// <summary>当前面值</summary>
        public int CurrentValue;
        /// <summary>当前面效果（可能为空）</summary>
        public string CurrentEffect;
        /// <summary>下一级面值</summary>
        public int NextValue;
        /// <summary>升级到下一级的费用</summary>
        public int NextLevelCost;
        /// <summary>是否可以升级</summary>
        public bool CanUpgrade;
        /// <summary>是否可以负担</summary>
        public bool CanAfford;
        /// <summary>当前金币</summary>
        public int CurrentGold;
        /// <summary>可用的特殊效果列表</summary>
        public List<SpecialFaceEntry> AvailableEffects;
    }

    /// <summary>
    /// 获取指定骰子面的升级预览信息
    /// 包含：当前值、下一级费用、是否可负担、可用效果列表
    /// </summary>
    /// <param name="diceIndex">骰子索引（0-based）</param>
    /// <param name="faceIndex">面索引（0-based）</param>
    /// <returns>升级预览数据</returns>
    public UpgradePreview GetUpgradePreview(int diceIndex, int faceIndex)
    {
        var preview = new UpgradePreview
        {
            DiceIndex = diceIndex,
            FaceIndex = faceIndex,
            AvailableEffects = GetAvailableEffects()
        };

        var roller = GetDiceRoller();
        if (roller == null || diceIndex < 0 || diceIndex >= roller.Dices.Length)
        {
            preview.CanUpgrade = false;
            return preview;
        }

        var dice = roller.Dices[diceIndex];
        if (faceIndex < 0 || faceIndex >= dice.Faces.Length)
        {
            preview.CanUpgrade = false;
            return preview;
        }

        // 填充当前状态
        preview.CurrentValue = dice.Faces[faceIndex];
        preview.CurrentEffect = dice.FaceEffects[faceIndex];

        // 计算下一级
        preview.NextValue = preview.CurrentValue + 1;
        preview.NextLevelCost = GetSingleLevelCost(preview.NextValue);

        // 面值上限检查（6面骰最大不超过 9）
        preview.CanUpgrade = preview.NextValue <= 9;

        // 金币检查
        var inventory = PlayerInventory.Instance;
        preview.CurrentGold = inventory?.Gold ?? 0;
        preview.CanAfford = preview.CurrentGold >= preview.NextLevelCost;

        return preview;
    }

    // ========== 私有工具 ==========

    /// <summary>
    /// 获取 DiceRoller 引用（优先从 RoguelikeGameManager，其次从 GameManager）
    /// </summary>
    private DiceRoller GetDiceRoller()
    {
        // 优先肉鸽模式
        if (RoguelikeGameManager.Instance != null && RoguelikeGameManager.Instance.DiceRoller != null)
            return RoguelikeGameManager.Instance.DiceRoller;

        // fallback 普通模式
        if (GameManager.Instance != null && GameManager.Instance.DiceRoller != null)
            return GameManager.Instance.DiceRoller;

        return null;
    }

    /// <summary>
    /// 通知升级失败
    /// </summary>
    private void NotifyFailed(int diceIndex, int faceIndex, string reason)
    {
        Debug.LogWarning($"[DiceUpgradeEngine] 升级失败：骰子{diceIndex + 1} 面{faceIndex + 1} — {reason}");
        OnUpgradeFailed?.Invoke(diceIndex, faceIndex, reason);
    }
}
