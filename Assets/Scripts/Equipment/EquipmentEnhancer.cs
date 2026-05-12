using UnityEngine;

/// <summary>
/// 装备强化结果
/// </summary>
public enum EnhanceResult
{
    /// <summary>强化成功</summary>
    Success,
    /// <summary>强化失败（不掉级）</summary>
    Failed,
    /// <summary>已达最大等级</summary>
    MaxLevel,
    /// <summary>金币不足</summary>
    NotEnoughGold,
    /// <summary>装备为空</summary>
    InvalidEquipment
}

/// <summary>
/// 装备强化系统 — 管理装备强化升级
/// 每级 +10% 基础属性，5级以上有失败概率
/// 单例模式
/// </summary>
public class EquipmentEnhancer : MonoBehaviour
{
    public static EquipmentEnhancer Instance { get; private set; }

    /// <summary>强化成功率事件回调（参数：装备、结果）</summary>
    public event System.Action<EquipmentData, EnhanceResult> OnEnhanceComplete;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 强化装备
    /// 消耗金币，5级以上有失败概率（10%-30%），失败不掉级
    /// </summary>
    /// <param name="equip">要强化的装备</param>
    /// <returns>强化结果</returns>
    public EnhanceResult Enhance(EquipmentData equip)
    {
        // 验证装备有效性
        if (equip == null)
            return EnhanceResult.InvalidEquipment;

        // 检查是否已达最大等级
        if (equip.enhanceLevel >= equip.maxEnhanceLevel)
            return EnhanceResult.MaxLevel;

        // 计算强化费用
        int cost = GetEnhanceCost(equip);

        // 检查金币是否足够
        var inventory = PlayerInventory.Instance;
        if (inventory == null || inventory.Gold < cost)
            return EnhanceResult.NotEnoughGold;

        // 扣除金币
        inventory.SpendGold(cost);

        // 5级以上有失败概率：10% + (当前等级 - 5) * 4%，最高30%
        if (equip.enhanceLevel >= 5)
        {
            float failChance = Mathf.Clamp(0.10f + (equip.enhanceLevel - 5) * 0.04f, 0.10f, 0.30f);
            if (Random.value < failChance)
            {
                Debug.Log($"强化失败！{equip.equipmentName} +{equip.enhanceLevel} → 不掉级");
                OnEnhanceComplete?.Invoke(equip, EnhanceResult.Failed);
                return EnhanceResult.Failed;
            }
        }

        // 强化成功
        equip.enhanceLevel++;
        equip.ApplyEnhance();

        Debug.Log($"强化成功！{equip.equipmentName} → +{equip.enhanceLevel}");
        OnEnhanceComplete?.Invoke(equip, EnhanceResult.Success);
        return EnhanceResult.Success;
    }

    /// <summary>
    /// 计算强化费用
    /// 公式：基础费用 × 费用倍率^当前等级
    /// </summary>
    /// <param name="equip">目标装备</param>
    /// <returns>强化所需金币数</returns>
    public int GetEnhanceCost(EquipmentData equip)
    {
        if (equip == null) return 0;
        float cost = equip.enhanceCostBase * Mathf.Pow(equip.enhanceCostMultiplier, equip.enhanceLevel);
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// 获取强化成功率（百分比，0-1）
    /// 5级以下100%，5级以上 90%-70%
    /// </summary>
    /// <param name="equip">目标装备</param>
    /// <returns>成功率</returns>
    public float GetSuccessRate(EquipmentData equip)
    {
        if (equip == null) return 0f;
        if (equip.enhanceLevel < 5) return 1f;
        float failChance = Mathf.Clamp(0.10f + (equip.enhanceLevel - 5) * 0.04f, 0.10f, 0.30f);
        return 1f - failChance;
    }

    /// <summary>
    /// 预览强化后的属性增量（不实际强化）
    /// </summary>
    /// <param name="equip">目标装备</param>
    /// <returns>下一级各属性增量描述</returns>
    public string GetEnhancePreview(EquipmentData equip)
    {
        if (equip == null || equip.enhanceLevel >= equip.maxEnhanceLevel)
            return "已达最大等级";

        float currentMult = 1f + equip.enhanceLevel * 0.1f;
        float nextMult = 1f + (equip.enhanceLevel + 1) * 0.1f;
        float delta = nextMult - currentMult; // 0.1

        int atkDelta = Mathf.RoundToInt(equip.attackBonus * delta);
        int defDelta = Mathf.RoundToInt(equip.defenseBonus * delta);
        int hpDelta = Mathf.RoundToInt(equip.healthBonus * delta);
        int spdDelta = Mathf.RoundToInt(equip.speedBonus * delta);

        string preview = $"强化+{equip.enhanceLevel} → +{equip.enhanceLevel + 1}\n";
        if (atkDelta > 0) preview += $"攻击+{atkDelta} ";
        if (defDelta > 0) preview += $"防御+{defDelta} ";
        if (hpDelta > 0) preview += $"生命+{hpDelta} ";
        if (spdDelta > 0) preview += $"速度+{spdDelta} ";
        preview += $"\n费用: {GetEnhanceCost(equip)} 金币";
        preview += $"\n成功率: {GetSuccessRate(equip) * 100:F0}%";

        return preview;
    }

    /// <summary>
    /// 检查装备是否可以强化
    /// </summary>
    /// <param name="equip">目标装备</param>
    /// <returns>是否可以强化</returns>
    public bool CanEnhance(EquipmentData equip)
    {
        if (equip == null) return false;
        if (equip.enhanceLevel >= equip.maxEnhanceLevel) return false;
        int cost = GetEnhanceCost(equip);
        var inventory = PlayerInventory.Instance;
        return inventory != null && inventory.Gold >= cost;
    }
}
