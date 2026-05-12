using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 掉落装备信息 — 描述一次掉落产生的装备实例
/// </summary>
public class LootDrop
{
    /// <summary>装备数据（ScriptableObject实例或运行时创建）</summary>
    public EquipmentData Equipment { get; set; }

    /// <summary>装备显示名称</summary>
    public string DisplayName => Equipment != null ? Equipment.equipmentName : "未知装备";

    /// <summary>稀有度</summary>
    public CardRarity Rarity => Equipment != null ? Equipment.rarity : CardRarity.White;

    /// <summary>装备槽位</summary>
    public EquipmentSlot Slot => Equipment != null ? Equipment.slot : EquipmentSlot.Weapon;

    /// <summary>是否已被玩家选中</summary>
    public bool IsSelected { get; set; }

    /// <summary>生成显示文本</summary>
    public string GetDisplayText()
    {
        string rarityIcon = GetRarityIcon(Rarity);
        string slotName = GetSlotName(Slot);
        string statInfo = "";
        if (Equipment != null)
        {
            var parts = new List<string>();
            if (Equipment.attackBonus > 0) parts.Add($"ATK+{Equipment.attackBonus}");
            if (Equipment.defenseBonus > 0) parts.Add($"DEF+{Equipment.defenseBonus}");
            if (Equipment.healthBonus > 0) parts.Add($"HP+{Equipment.healthBonus}");
            if (Equipment.speedBonus > 0) parts.Add($"SPD+{Equipment.speedBonus}");
            if (Equipment.critRateBonus > 0) parts.Add($"CRT+{Equipment.critRateBonus:P0}");
            statInfo = string.Join(" ", parts);
        }
        return $"{rarityIcon} [{slotName}] {DisplayName}  {statInfo}";
    }

    private static string GetRarityIcon(CardRarity r) => r switch
    {
        CardRarity.White => "⚪",
        CardRarity.Blue => "🔵",
        CardRarity.Purple => "🟣",
        CardRarity.Gold => "🟡",
        _ => "⚪"
    };

    private static string GetSlotName(EquipmentSlot s) => s switch
    {
        EquipmentSlot.Weapon => "武器",
        EquipmentSlot.Armor => "防具",
        EquipmentSlot.Accessory => "饰品",
        _ => "装备"
    };
}

/// <summary>
/// 装备掉落系统 — 单例
/// 根据 drop_tables.json 配置掉落装备
/// 
/// 掉落规则：
/// - 每 guaranteed_every_n_levels 关必掉（默认每3关）
/// - 其余关卡有 random_drop_chance 概率掉落（默认30%）
/// - 前 no_drop_in_first_2_levels 关不掉落（默认前2关）
/// - 关卡难度越高，稀有度概率越大（加权随机）
/// - 稀有度权重来自 economy.json equipment.rarities.drop_weight，并根据关卡难度动态调整
/// - 装备属性根据槽位（weapon/armor/accessory）和稀有度倍率随机生成
/// </summary>
public class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }

    // 随机数生成器
    private System.Random rng;

    // 配置缓存
    private EquipmentDropConfig dropConfig;
    private List<EconomyEquipRarityConfig> rarityConfigs;
    private List<EconomyEquipSlotConfig> slotConfigs;

    // 装备名称库（按槽位分类）
    private static readonly Dictionary<string, string[]> EquipmentNamePool = new Dictionary<string, string[]>
    {
        {
            "weapon", new[]
            {
                "铁剑", "铜刀", "精钢长剑", "赤焰刃", "寒冰戟",
                "破甲锥", "风暴之锤", "暗影匕首", "龙牙剑", "雷鸣战斧",
                "碎星弓", "烈焰法杖", "圣光之刃", "虚空之牙"
            }
        },
        {
            "armor", new[]
            {
                "皮甲", "链甲", "板甲", "秘银战甲", "龙鳞护胸",
                "荆棘之壁", "暗影斗篷", "圣骑铠甲", "铁壁护心镜", "冰霜胸甲",
                "凤凰羽衣", "不屈战铠"
            }
        },
        {
            "accessory", new[]
            {
                "铜戒指", "银项链", "速度之靴", "暴击护符", "生命吊坠",
                "闪避披风", "吸血鬼之牙", "荆棘指环", "圣光坠饰", "暗影面纱",
                "命运之轮", "守护之翼"
            }
        }
    };

    // 稀有度前缀
    private static readonly Dictionary<CardRarity, string> RarityPrefix = new Dictionary<CardRarity, string>
    {
        { CardRarity.White, "" },
        { CardRarity.Blue, "精制" },
        { CardRarity.Purple, "稀有·" },
        { CardRarity.Gold, "传说·" }
    };

    // 特效池
    private static readonly string[] SpecialEffects = {
        "", "", "", // 大概率无特效
        "攻击时有10%概率双倍伤害",
        "受击时反弹10%伤害",
        "击杀回复5%最大血量",
        "暴击时额外造成20%伤害",
        "每回合回复3%最大血量",
        "免疫首次控制效果"
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        rng = new System.Random();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ========== 公开接口 ==========

    /// <summary>
    /// 判断指定关卡是否掉落装备
    /// </summary>
    /// <param name="level">当前关卡</param>
    /// <returns>是否掉落</returns>
    public bool ShouldDropEquipment(int level)
    {
        EnsureConfigLoaded();

        // 前2关不掉落
        if (dropConfig != null && dropConfig.no_drop_in_first_2_levels && level <= 2)
            return false;

        // 保底掉落：每N关必掉
        if (dropConfig != null && dropConfig.guaranteed_every_n_levels > 0)
        {
            if (level % dropConfig.guaranteed_every_n_levels == 0)
                return true;
        }

        // 随机掉落
        float dropChance = dropConfig?.random_drop_chance ?? 0.3f;
        return (float)rng.NextDouble() < dropChance;
    }

    /// <summary>
    /// 生成本关的掉落装备列表（供玩家选择）
    /// </summary>
    /// <param name="level">当前关卡</param>
    /// <param name="count">生成数量（默认3个供三选一）</param>
    /// <returns>掉落装备列表</returns>
    public List<LootDrop> GenerateLootDrops(int level, int count = 3)
    {
        EnsureConfigLoaded();
        var drops = new List<LootDrop>();

        for (int i = 0; i < count; i++)
        {
            var drop = GenerateSingleDrop(level);
            if (drop != null)
                drops.Add(drop);
        }

        Debug.Log($"[LootSystem] 关卡{level}生成{drops.Count}件掉落装备");
        return drops;
    }

    /// <summary>
    /// 生成本关的掉落装备（单件，用于自动掉落展示）
    /// </summary>
    /// <param name="level">当前关卡</param>
    /// <returns>单件掉落装备，或null</returns>
    public LootDrop GetLevelDrop(int level)
    {
        if (!ShouldDropEquipment(level)) return null;
        return GenerateSingleDrop(level);
    }

    /// <summary>
    /// 将选中的装备加入玩家背包
    /// </summary>
    /// <param name="drop">选中的掉落装备</param>
    /// <returns>是否成功加入</returns>
    public bool ClaimLoot(LootDrop drop)
    {
        if (drop == null || drop.Equipment == null)
        {
            Debug.LogWarning("[LootSystem] 尝试领取空掉落");
            return false;
        }

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[LootSystem] PlayerInventory不存在，无法添加装备");
            return false;
        }

        inventory.AddEquipment(drop.Equipment);
        drop.IsSelected = true;
        Debug.Log($"[LootSystem] 玩家领取装备: {drop.DisplayName} ({drop.Rarity})");
        return true;
    }

    /// <summary>
    /// 批量领取掉落装备
    /// </summary>
    /// <param name="drops">选中的掉落列表</param>
    public void ClaimLootBatch(List<LootDrop> drops)
    {
        if (drops == null) return;
        foreach (var drop in drops)
        {
            ClaimLoot(drop);
        }
    }

    // ========== 内部实现 ==========

    /// <summary>
    /// 生成单件掉落装备
    /// </summary>
    private LootDrop GenerateSingleDrop(int level)
    {
        // 1. 决定稀有度（加权随机，关卡越高稀有度概率越大）
        CardRarity rarity = RollRarity(level);

        // 2. 决定装备槽位
        string slotId = RollSlot();

        // 3. 生成装备数据
        var equip = CreateEquipmentData(slotId, rarity, level);

        return new LootDrop
        {
            Equipment = equip,
            IsSelected = false
        };
    }

    /// <summary>
    /// 加权随机决定稀有度
    /// 关卡难度越高，高稀有度的权重越大
    /// </summary>
    private CardRarity RollRarity(int level)
    {
        // 基础权重从 economy.json equipment.rarities.drop_weight 获取
        // common=50, uncommon=30, rare=15, legendary=5
        float[] weights = GetRarityWeights(level);

        // 加权随机选择
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
            totalWeight += weights[i];

        if (totalWeight <= 0f) return CardRarity.White;

        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;

        CardRarity[] rarities = { CardRarity.White, CardRarity.Blue, CardRarity.Purple, CardRarity.Gold };
        for (int i = 0; i < rarities.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return rarities[i];
        }

        return CardRarity.White;
    }

    /// <summary>
    /// 获取稀有度权重（根据关卡难度动态调整）
    /// 难度越高，高稀有度权重越大：
    /// - 关卡1-5：接近原始权重
    /// - 关卡6-10：uncommon/rare权重提升，common降低
    /// - 关卡11+：rare/legendary大幅提升，common大幅降低
    /// </summary>
    private float[] GetRarityWeights(int level)
    {
        // 基础权重（fallback，优先从配置读取）
        float commonW = 50f;
        float uncommonW = 30f;
        float rareW = 15f;
        float legendaryW = 5f;

        // 尝试从 economy.json 配置读取
        if (rarityConfigs != null && rarityConfigs.Count >= 4)
        {
            // 顺序: common, uncommon, rare, legendary
            commonW = rarityConfigs[0].drop_weight;
            uncommonW = rarityConfigs[1].drop_weight;
            rareW = rarityConfigs[2].drop_weight;
            legendaryW = rarityConfigs[3].drop_weight;
        }

        // 根据关卡难度调整权重
        // 每个高阶段，低稀有度权重下降，高稀有度权重上升
        float difficultyScale = Mathf.Min(level / 15f, 1f); // 0~1，关卡15时满

        // 难度修正：线性插值调整权重
        // common 随难度降低：50 → 20
        commonW = Mathf.Lerp(commonW, commonW * 0.4f, difficultyScale);
        // uncommon 基本不变
        uncommonW = Mathf.Lerp(uncommonW, uncommonW * 1.2f, difficultyScale);
        // rare 随难度提升：15 → 35
        rareW = Mathf.Lerp(rareW, rareW * 2.3f, difficultyScale);
        // legendary 随难度大幅提升：5 → 20
        legendaryW = Mathf.Lerp(legendaryW, legendaryW * 4.0f, difficultyScale);

        // 关卡10+解锁传说装备保底权重
        if (level >= 10)
            legendaryW = Mathf.Max(legendaryW, 10f);

        // 关卡6+解锁紫装保底权重
        if (level >= 6)
            rareW = Mathf.Max(rareW, 20f);

        return new float[] { commonW, uncommonW, rareW, legendaryW };
    }

    /// <summary>
    /// 随机决定装备槽位
    /// </summary>
    private string RollSlot()
    {
        string[] slots = { "weapon", "armor", "accessory" };
        return slots[rng.Next(slots.Length)];
    }

    /// <summary>
    /// 创建装备数据（运行时动态创建 ScriptableObject 实例）
    /// 根据槽位配置和稀有度倍率生成属性
    /// </summary>
    private EquipmentData CreateEquipmentData(string slotId, CardRarity rarity, int level)
    {
        var equip = ScriptableObject.CreateInstance<EquipmentData>();

        // 设置槽位
        equip.slot = slotId switch
        {
            "weapon" => EquipmentSlot.Weapon,
            "armor" => EquipmentSlot.Armor,
            _ => EquipmentSlot.Accessory
        };

        // 设置稀有度
        equip.rarity = rarity;

        // 获取稀有度倍率
        float statMultiplier = GetStatMultiplier(rarity);

        // 根据槽位生成属性
        var slotConfig = GetSlotConfig(slotId);
        if (slotConfig != null)
        {
            ApplySlotBasedStats(equip, slotConfig, statMultiplier, level);
        }
        else
        {
            // fallback：使用默认属性生成
            ApplyDefaultStats(equip, slotId, statMultiplier, level);
        }

        // 生成装备名称
        equip.equipmentName = GenerateEquipmentName(slotId, rarity);

        // 随机特效（高稀有度更容易出特效）
        equip.specialEffect = RollSpecialEffect(rarity);

        // 生成描述
        equip.description = GenerateDescription(equip);

        // 初始化强化系统默认值
        equip.enhanceLevel = 0;
        equip.maxEnhanceLevel = 10;
        equip.enhanceCostBase = 50;
        equip.enhanceCostMultiplier = 1.5f;

        return equip;
    }

    /// <summary>
    /// 获取稀有度属性倍率
    /// </summary>
    private float GetStatMultiplier(CardRarity rarity)
    {
        if (rarityConfigs != null)
        {
            int idx = rarity switch
            {
                CardRarity.White => 0,
                CardRarity.Blue => 1,
                CardRarity.Purple => 2,
                CardRarity.Gold => 3,
                _ => 0
            };
            if (idx < rarityConfigs.Count)
                return rarityConfigs[idx].stat_multiplier;
        }

        return rarity switch
        {
            CardRarity.White => 1.0f,
            CardRarity.Blue => 1.3f,
            CardRarity.Purple => 1.7f,
            CardRarity.Gold => 2.2f,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 获取槽位配置
    /// </summary>
    private EconomyEquipSlotConfig GetSlotConfig(string slotId)
    {
        if (slotConfigs == null) return null;
        return slotConfigs.Find(s => s.id == slotId);
    }

    /// <summary>
    /// 根据槽位配置生成属性
    /// </summary>
    private void ApplySlotBasedStats(EquipmentData equip, EconomyEquipSlotConfig slotConfig,
        float rarityMultiplier, int level)
    {
        // 关卡缩放因子（关卡越高属性越高）
        float levelScale = 1f + (level - 1) * 0.08f;

        int[] statRange = slotConfig.stat_range;
        int statMin = statRange != null && statRange.Length >= 1 ? statRange[0] : 2;
        int statMax = statRange != null && statRange.Length >= 2 ? statRange[1] : 10;

        // 生成基础属性值
        int baseStat = rng.Next(statMin, statMax + 1);
        int finalStat = Mathf.RoundToInt(baseStat * rarityMultiplier * levelScale);

        // 根据槽位分配主属性
        switch (slotConfig.id)
        {
            case "weapon":
                equip.attackBonus = finalStat;
                // 武器可能附带速度
                if (rng.NextDouble() < 0.3f)
                    equip.speedBonus = Mathf.RoundToInt(rng.Next(1, 4) * rarityMultiplier);
                break;

            case "armor":
                equip.defenseBonus = Mathf.RoundToInt(finalStat * 0.7f);
                equip.healthBonus = Mathf.RoundToInt(finalStat * 1.5f);
                break;

            case "accessory":
                // 饰品随机分配属性
                int accType = rng.Next(4);
                switch (accType)
                {
                    case 0:
                        equip.critRateBonus = Mathf.RoundToInt(rng.Next(3, 9) * rarityMultiplier) / 100f;
                        break;
                    case 1:
                        equip.speedBonus = Mathf.RoundToInt(rng.Next(2, 6) * rarityMultiplier);
                        break;
                    case 2:
                        equip.attackBonus = Mathf.RoundToInt(finalStat * 0.5f);
                        equip.defenseBonus = Mathf.RoundToInt(finalStat * 0.3f);
                        break;
                    case 3:
                        equip.healthBonus = Mathf.RoundToInt(finalStat * 0.8f);
                        equip.speedBonus = Mathf.RoundToInt(rng.Next(1, 3) * rarityMultiplier);
                        break;
                }
                break;
        }
    }

    /// <summary>
    /// Fallback：默认属性生成（无配置时使用）
    /// </summary>
    private void ApplyDefaultStats(EquipmentData equip, string slotId,
        float rarityMultiplier, int level)
    {
        float levelScale = 1f + (level - 1) * 0.08f;

        switch (slotId)
        {
            case "weapon":
                equip.attackBonus = Mathf.RoundToInt(rng.Next(3, 12) * rarityMultiplier * levelScale);
                if (rng.NextDouble() < 0.3f)
                    equip.speedBonus = Mathf.RoundToInt(rng.Next(1, 4) * rarityMultiplier);
                break;

            case "armor":
                equip.defenseBonus = Mathf.RoundToInt(rng.Next(2, 8) * rarityMultiplier * levelScale);
                equip.healthBonus = Mathf.RoundToInt(rng.Next(5, 15) * rarityMultiplier * levelScale);
                break;

            default: // accessory
                int accType = rng.Next(3);
                if (accType == 0)
                    equip.critRateBonus = Mathf.RoundToInt(rng.Next(3, 8) * rarityMultiplier) / 100f;
                else if (accType == 1)
                    equip.speedBonus = Mathf.RoundToInt(rng.Next(2, 5) * rarityMultiplier * levelScale);
                else
                    equip.attackBonus = Mathf.RoundToInt(rng.Next(2, 6) * rarityMultiplier * levelScale);
                break;
        }
    }

    /// <summary>
    /// 生成装备名称
    /// </summary>
    private string GenerateEquipmentName(string slotId, CardRarity rarity)
    {
        if (EquipmentNamePool.TryGetValue(slotId, out var names) && names.Length > 0)
        {
            string baseName = names[rng.Next(names.Length)];
            string prefix = RarityPrefix.GetValueOrDefault(rarity, "");
            return $"{prefix}{baseName}";
        }
        return $"{rarity}级{slotId}";
    }

    /// <summary>
    /// 随机特效（高稀有度更容易出特效）
    /// </summary>
    private string RollSpecialEffect(CardRarity rarity)
    {
        float effectChance = rarity switch
        {
            CardRarity.White => 0.1f,
            CardRarity.Blue => 0.3f,
            CardRarity.Purple => 0.6f,
            CardRarity.Gold => 0.9f,
            _ => 0.1f
        };

        if ((float)rng.NextDouble() < effectChance)
        {
            return SpecialEffects[rng.Next(3, SpecialEffects.Length)]; // 跳过空特效
        }
        return "";
    }

    /// <summary>
    /// 生成装备描述
    /// </summary>
    private string GenerateDescription(EquipmentData equip)
    {
        var parts = new List<string>();

        if (equip.attackBonus > 0) parts.Add($"攻击+{equip.attackBonus}");
        if (equip.defenseBonus > 0) parts.Add($"防御+{equip.defenseBonus}");
        if (equip.healthBonus > 0) parts.Add($"生命+{equip.healthBonus}");
        if (equip.speedBonus > 0) parts.Add($"速度+{equip.speedBonus}");
        if (equip.critRateBonus > 0) parts.Add($"暴击率+{equip.critRateBonus:P0}");

        string desc = string.Join("  ", parts);
        if (!string.IsNullOrEmpty(equip.specialEffect))
            desc += $"\n★ {equip.specialEffect}";

        return desc;
    }

    /// <summary>
    /// 确保配置已加载
    /// </summary>
    private void EnsureConfigLoaded()
    {
        if (dropConfig == null)
        {
            dropConfig = BalanceProvider.DropTables?.equipment_drop;
        }
        if (rarityConfigs == null)
        {
            rarityConfigs = BalanceProvider.GetEquipmentRarities();
        }
        if (slotConfigs == null)
        {
            var economy = BalanceProvider.Economy;
            slotConfigs = economy?.equipment?.slots;
        }
    }

    // ========== 调试/工具接口 ==========

    /// <summary>
    /// 重置配置缓存（配置热重载时使用）
    /// </summary>
    public void ReloadConfig()
    {
        dropConfig = null;
        rarityConfigs = null;
        slotConfigs = null;
        EnsureConfigLoaded();
        Debug.Log("[LootSystem] 配置已重新加载");
    }

    /// <summary>
    /// 设置随机种子（用于测试/重现）
    /// </summary>
    public void SetSeed(int seed)
    {
        rng = new System.Random(seed);
    }
}
