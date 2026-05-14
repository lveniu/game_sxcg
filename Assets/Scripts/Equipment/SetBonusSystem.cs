using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 套装效果类型
/// </summary>
public enum SetBonusType
{
    AttackPercent,      // 攻击力百分比加成
    DefensePercent,     // 防御力百分比加成
    HealthPercent,      // 生命值百分比加成
    SpeedPercent,       // 速度百分比加成
    CritRateFlat,       // 暴击率固定加成
    CritDamageFlat,     // 暴击伤害固定加成
    LifeStealFlat,      // 吸血率固定加成
    DodgeFlat,          // 闪避率固定加成
    ThornsFlat          // 荆棘反伤率固定加成
}

/// <summary>
/// 单条套装效果定义
/// </summary>
[System.Serializable]
public class SetBonusEffect
{
    /// <summary>效果类型</summary>
    public SetBonusType type;
    /// <summary>效果数值（百分比用小数，如0.15=15%）</summary>
    public float value;
    /// <summary>效果描述</summary>
    public string description;

    public SetBonusEffect(SetBonusType type, float value, string description)
    {
        this.type = type;
        this.value = value;
        this.description = description;
    }
}

/// <summary>
/// 套装定义 — 包含2件套和4件套效果
/// </summary>
[System.Serializable]
public class SetDefinition
{
    /// <summary>套装ID</summary>
    public string setId;
    /// <summary>套装名称</summary>
    public string setName;
    /// <summary>需要激活的件数阈值</summary>
    public int requiredCount;
    /// <summary>该阈值下的效果列表</summary>
    public List<SetBonusEffect> bonuses = new();

    public SetDefinition(string setId, string setName, int requiredCount, List<SetBonusEffect> bonuses)
    {
        this.setId = setId;
        this.setName = setName;
        this.requiredCount = requiredCount;
        this.bonuses = bonuses;
    }
}

/// <summary>
/// 激活的套装效果实例（用于返回给调用方）
/// </summary>
public class ActiveSetBonus
{
    /// <summary>套装名称</summary>
    public string setName;
    /// <summary>已装备件数</summary>
    public int equippedCount;
    /// <summary>效果阈值（2件或4件）</summary>
    public int requiredCount;
    /// <summary>激活的效果列表</summary>
    public List<SetBonusEffect> bonuses = new();
}

/// <summary>
/// 套装效果系统 — 管理套装定义、检测套装激活状态、应用套装加成
/// 单例模式
/// </summary>
public class SetBonusSystem : MonoBehaviour
{
    public static SetBonusSystem Instance { get; private set; }

    /// <summary>所有套装定义列表</summary>
    private Dictionary<string, List<SetDefinition>> setDefinitions = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSets();
    }

    /// <summary>
    /// 从 equipment_sets.json 加载套装定义
    /// 通过 BalanceProvider 统一入口读取，支持热重载
    /// </summary>
    private void InitializeSets()
    {
        setDefinitions.Clear();

        var config = BalanceProvider.EquipmentSets;
        if (config?.sets == null || config.sets.Count == 0)
        {
            Debug.LogError("[SetBonusSystem] equipment_sets.json 加载失败或为空，套装系统不可用");
            return;
        }

        foreach (var setEntry in config.sets)
        {
            if (string.IsNullOrEmpty(setEntry.setId)) continue;

            foreach (var tier in setEntry.tiers)
            {
                var bonuses = new List<SetBonusEffect>();
                foreach (var b in tier.bonuses)
                {
                    if (System.Enum.TryParse<SetBonusType>(b.type, out var bonusType))
                    {
                        bonuses.Add(new SetBonusEffect(bonusType, b.value, b.description));
                    }
                    else
                    {
                        Debug.LogWarning($"[SetBonusSystem] 未知套装效果类型: {b.type} (套装: {setEntry.setId})");
                    }
                }

                var def = new SetDefinition(setEntry.setId, setEntry.setName, tier.requiredCount, bonuses);
                AddSetDefinition(def);
            }
        }

        Debug.Log($"[SetBonusSystem] 从 JSON 加载完成，共 {setDefinitions.Count} 个套装定义");
    }

    /// <summary>
    /// 添加套装定义到系统
    /// </summary>
    private void AddSetDefinition(SetDefinition def)
    {
        if (!setDefinitions.ContainsKey(def.setId))
            setDefinitions[def.setId] = new List<SetDefinition>();
        setDefinitions[def.setId].Add(def);
    }

    /// <summary>
    /// 获取所有套装ID列表
    /// </summary>
    public List<string> GetAllSetIds()
    {
        return new List<string>(setDefinitions.Keys);
    }

    /// <summary>
    /// 获取指定套装ID的所有定义（含不同阈值的2件/4件效果）
    /// </summary>
    public List<SetDefinition> GetSetDefinitions(string setId)
    {
        if (setDefinitions.TryGetValue(setId, out var defs))
            return defs;
        return new List<SetDefinition>();
    }

    /// <summary>
    /// 检查英雄装备的套装件数，应用对应加成
    /// 在 Hero.RecalculateStats() 中调用
    /// </summary>
    /// <param name="hero">目标英雄</param>
    public void CheckSetBonus(Hero hero)
    {
        if (hero == null) return;

        var activeBonuses = GetActiveSetBonuses(hero);
        foreach (var bonus in activeBonuses)
        {
            foreach (var effect in bonus.bonuses)
            {
                ApplySetBonusEffect(hero, effect);
            }
        }
    }

    /// <summary>
    /// 获取英雄当前激活的所有套装效果列表
    /// </summary>
    /// <param name="hero">目标英雄</param>
    /// <returns>激活的套装效果列表</returns>
    public List<ActiveSetBonus> GetActiveSetBonuses(Hero hero)
    {
        var result = new List<ActiveSetBonus>();
        if (hero == null) return result;

        // 统计各套装件数
        var setCounts = new Dictionary<string, int>();
        var setNames = new Dictionary<string, string>();

        foreach (var item in hero.EquippedItems.Values)
        {
            if (item == null || !item.BelongsToSet) continue;
            if (!setCounts.ContainsKey(item.setId))
            {
                setCounts[item.setId] = 0;
                setNames[item.setId] = item.setName;
            }
            setCounts[item.setId]++;
        }

        // 检查每个套装的激活情况
        foreach (var kvp in setCounts)
        {
            string setId = kvp.Key;
            int count = kvp.Value;

            if (!setDefinitions.ContainsKey(setId)) continue;

            // 按阈值从大到小检查，只取最高激活效果
            var defs = setDefinitions[setId];
            defs.Sort((a, b) => b.requiredCount.CompareTo(a.requiredCount));

            foreach (var def in defs)
            {
                if (count >= def.requiredCount)
                {
                    var active = new ActiveSetBonus
                    {
                        setName = setNames[setId],
                        equippedCount = count,
                        requiredCount = def.requiredCount,
                        bonuses = def.bonuses
                    };
                    result.Add(active);
                    break; // 同一套装只取最高激活档位
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 将单个套装效果应用到英雄属性上
    /// </summary>
    /// <param name="hero">目标英雄</param>
    /// <param name="effect">套装效果</param>
    private void ApplySetBonusEffect(Hero hero, SetBonusEffect effect)
    {
        switch (effect.type)
        {
            case SetBonusType.AttackPercent:
                hero.BoostAttack(effect.value);
                break;
            case SetBonusType.DefensePercent:
                hero.BoostDefense(effect.value);
                break;
            case SetBonusType.HealthPercent:
                hero.BoostMaxHealth(effect.value);
                break;
            case SetBonusType.SpeedPercent:
                hero.BoostSpeed(effect.value);
                break;
            case SetBonusType.CritRateFlat:
                hero.BoostCritRate(effect.value);
                break;
            case SetBonusType.CritDamageFlat:
                hero.BoostCritDamage(effect.value);
                break;
            case SetBonusType.LifeStealFlat:
                hero.LifeStealRate += effect.value;
                break;
            case SetBonusType.DodgeFlat:
                hero.BattleDodgeRate += effect.value;
                break;
            case SetBonusType.ThornsFlat:
                hero.BattleThornsRate += effect.value;
                break;
        }
    }
}
