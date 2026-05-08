using UnityEngine;

/// <summary>
/// 遗物稀有度
/// </summary>
public enum RelicRarity
{
    Common,  // 普通
    Rare,    // 稀有
    Epic     // 史诗
}

/// <summary>
/// 遗物效果类型
/// </summary>
public enum RelicEffectType
{
    // 属性加成类
    AttackBoost,        // 攻击加成
    DefenseBoost,       // 防御加成
    HealthBoost,        // 生命加成
    SpeedBoost,         // 速度加成
    CritBoost,          // 暴击加成

    // 战斗机制类
    BattleStartShield,  // 战斗开始护盾
    LifeSteal,          // 吸血
    Thorns,             // 反伤
    PoisonAttack,       // 中毒攻击
    GiantSlayer,        // 巨人杀手

    // 骰子/系统类
    ExtraReroll,        // 额外重摇
    ComboBoost,         // 组合增强
    DoubleReward,       // 双倍奖励

    // 复活类
    Revive              // 复活
}

/// <summary>
/// 遗物数据 — 定义遗物的属性和效果
/// </summary>
public class RelicData
{
    public string relicId { get; private set; }
    public string relicName { get; private set; }
    public int rarity { get; private set; }
    public RelicRarity rarityLevel { get; private set; }
    public string description { get; private set; }
    public RelicEffectType effectType { get; private set; }
    public float effectValue { get; private set; }

    public RelicData(string id, string name, int rarity, RelicRarity rarityLevel,
        string desc, RelicEffectType effectType, float effectValue)
    {
        this.relicId = id;
        this.relicName = name;
        this.rarity = rarity;
        this.rarityLevel = rarityLevel;
        this.description = desc;
        this.effectType = effectType;
        this.effectValue = effectValue;
    }

    /// <summary>
    /// 获取稀有度对应的颜色标记
    /// </summary>
    public string GetRarityTag()
    {
        return rarityLevel switch
        {
            RelicRarity.Common => "白",
            RelicRarity.Rare => "蓝",
            RelicRarity.Epic => "紫",
            _ => "白"
        };
    }

    public override string ToString()
    {
        return $"[{GetRarityTag()}] {relicName}: {description}";
    }
}

/// <summary>
/// 遗物实例 — 运行时状态
/// </summary>
public class RelicInstance
{
    public RelicData Data { get; private set; }
    public bool IsActive { get; private set; } = true;

    // 运行时状态
    public int TriggerCount { get; private set; }
    public bool HasBeenUsedThisLevel { get; private set; }

    public RelicInstance(RelicData data)
    {
        Data = data;
    }

    public void Trigger()
    {
        TriggerCount++;
        HasBeenUsedThisLevel = true;
    }

    public void ResetForNewLevel()
    {
        HasBeenUsedThisLevel = false;
    }
}
