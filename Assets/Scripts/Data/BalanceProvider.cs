using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 数值配置统一入口 — 前端和后端都通过此入口获取配置数据
/// 不直接操作 GameBalance 的硬编码，而是从 JSON 配置读取
/// 
/// 设计原则：
/// 1. 所有配置读取的唯一入口
/// 2. JSON 优先，fallback 到 GameBalance 硬编码默认值
/// 3. 缓存配置实例，避免重复反序列化
/// 4. 提供热重载接口（调试/策划调数值用）
/// </summary>
public static class BalanceProvider
{
    // ========== 配置缓存 ==========
    private static HeroClassesConfig _heroClasses;
    private static EnemiesConfig _enemies;
    private static LevelsConfig _levels;
    private static BattleFormulasConfig _battleFormulas;
    private static SkillsConfig _skills;
    private static DropTablesConfig _dropTables;
    private static EconomyConfig _economy;
    private static RelicsConfig _relics;
    private static DiceSystemConfig _diceSystem;
    private static MechanicEnemiesFileConfig _mechanicEnemies;
    private static FaceEffectsFileConfig _faceEffects;
    private static HeroExpFileConfig _heroExpConfig;
    private static RoguelikeMapFileConfig _roguelikeMap;

    // 懒加载属性
    public static HeroClassesConfig HeroClasses => _heroClasses ?? (_heroClasses = ConfigLoader.LoadHeroClasses());
    public static EnemiesConfig Enemies => _enemies ?? (_enemies = ConfigLoader.LoadEnemies());
    public static LevelsConfig Levels => _levels ?? (_levels = ConfigLoader.LoadLevels());
    public static BattleFormulasConfig BattleFormulas => _battleFormulas ?? (_battleFormulas = ConfigLoader.LoadBattleFormulas());
    public static SkillsConfig Skills => _skills ?? (_skills = ConfigLoader.LoadSkills());
    public static DropTablesConfig DropTables => _dropTables ?? (_dropTables = ConfigLoader.LoadDropTables());
    public static EconomyConfig Economy => _economy ?? (_economy = ConfigLoader.LoadEconomy());
    public static RelicsConfig Relics => _relics ?? (_relics = ConfigLoader.LoadRelics());
    public static DiceSystemConfig DiceSystem => _diceSystem ?? (_diceSystem = ConfigLoader.LoadDiceSystem());
    public static MechanicEnemiesFileConfig MechanicEnemies => _mechanicEnemies ?? (_mechanicEnemies = ConfigLoader.LoadMechanicEnemies());
    public static FaceEffectsFileConfig FaceEffects => _faceEffects ?? (_faceEffects = ConfigLoader.LoadFaceEffects());
    public static HeroExpFileConfig HeroExpConfig => _heroExpConfig ?? (_heroExpConfig = ConfigLoader.LoadHeroExpConfig());
    public static RoguelikeMapFileConfig RoguelikeMapConfig => _roguelikeMap ?? (_roguelikeMap = ConfigLoader.LoadRoguelikeMap());

    /// <summary>
    /// 热重载所有配置（策划调数值后调用）
    /// </summary>
    public static void ReloadAll()
    {
        ConfigLoader.ClearCache();
        _heroClasses = null;
        _enemies = null;
        _levels = null;
        _battleFormulas = null;
        _skills = null;
        _dropTables = null;
        _economy = null;
        _relics = null;
        _diceSystem = null;
        _mechanicEnemies = null;
        _faceEffects = null;
        _heroExpConfig = null;
        _roguelikeMap = null;
        GameBalance.ReloadConfigs();
        Debug.Log("[BalanceProvider] 所有配置已重新加载");
    }

    // ========== 英雄相关 ==========

    /// <summary>
    /// 获取所有英雄职业配置列表
    /// </summary>
    public static List<HeroClassEntry> GetAllHeroClasses()
    {
        return HeroClasses?.classes ?? new List<HeroClassEntry>();
    }

    /// <summary>
    /// 按角色类型获取英雄配置
    /// </summary>
    public static HeroClassEntry GetHeroClass(string heroId)
    {
        var classes = HeroClasses?.classes;
        if (classes == null) return null;
        return classes.Find(c => c.id == heroId || c.name_cn == heroId);
    }

    /// <summary>
    /// 按角色枚举获取英雄配置
    /// </summary>
    public static HeroClassEntry GetHeroClass(HeroClass heroClass)
    {
        string roleId = heroClass.ToString().ToLower();
        return GetHeroClass(roleId);
    }

    /// <summary>
    /// 获取英雄基础属性（JSON优先，fallback到GameBalance）
    /// </summary>
    public static HeroStatTemplate GetHeroStats(string heroId)
    {
        var entry = GetHeroClass(heroId);
        if (entry != null)
        {
            HeroClass cls = ParseHeroClass(entry.role ?? entry.id);
            return new HeroStatTemplate(
                entry.base_stats.max_health,
                entry.base_stats.attack,
                entry.base_stats.defense,
                entry.base_stats.speed,
                entry.base_stats.crit_rate,
                entry.summon_cost,
                cls
            );
        }
        // fallback
        return GameBalance.GetHeroTemplate(heroId);
    }

    // ========== 敌人相关 ==========

    /// <summary>
    /// 获取所有敌人配置
    /// </summary>
    public static List<EnemyEntry> GetAllEnemies()
    {
        return Enemies?.enemy_types ?? new List<EnemyEntry>();
    }

    /// <summary>
    /// 按ID/中文名获取敌人配置
    /// </summary>
    public static EnemyEntry GetEnemy(string enemyId)
    {
        var enemies = Enemies?.enemy_types;
        if (enemies == null) return null;
        return enemies.Find(e => e.id == enemyId || e.name_cn == enemyId);
    }

    /// <summary>
    /// 获取敌人属性模板（含难度缩放）
    /// </summary>
    public static HeroStatTemplate GetEnemyStats(string enemyId, int levelId = 1)
    {
        return GameBalance.GetEnemyTemplate(enemyId, levelId);
    }

    // ========== 关卡相关 ==========

    /// <summary>
    /// 获取关卡配置
    /// </summary>
    public static LevelTierEntry GetLevel(int levelId)
    {
        var templates = Levels?.level_templates;
        if (templates == null) return null;
        foreach (var kvp in templates)
        {
            if (kvp.Value?.range != null && levelId >= kvp.Value.range[0] && levelId <= kvp.Value.range[1])
                return kvp.Value;
        }
        return null;
    }

    /// <summary>
    /// 获取所有关卡配置
    /// </summary>
    public static List<LevelTierEntry> GetAllLevels()
    {
        var templates = Levels?.level_templates;
        if (templates == null) return new List<LevelTierEntry>();
        return templates.Values.ToList();
    }

    // ========== 战斗公式相关 ==========

    /// <summary>
    /// 获取暴击基础倍率（默认1.5）
    /// </summary>
    public static float GetCritMultiplierBase()
    {
        return BattleFormulas?.crit_formula?.crit_multiplier_base ?? 1.5f;
    }

    /// <summary>
    /// 获取最低伤害（默认1）
    /// </summary>
    public static int GetMinDamage()
    {
        return BattleFormulas?.damage_formula?.min_damage ?? 1;
    }

    /// <summary>
    /// 获取默认治疗倍率（默认0.5）
    /// </summary>
    public static float GetHealMultiplierDefault()
    {
        return BattleFormulas?.heal_formula?.heal_multiplier_default ?? 0.5f;
    }

    /// <summary>
    /// 获取最大战斗时间（秒）
    /// </summary>
    public static float GetMaxBattleTime()
    {
        return BattleFormulas?.battle_timing?.max_battle_time_sec ?? 20f;
    }

    /// <summary>
    /// 获取战斗Tick间隔（秒）
    /// </summary>
    public static float GetBattleTickInterval()
    {
        return BattleFormulas?.battle_timing?.battle_tick_interval_sec ?? 0.3f;
    }

    /// <summary>
    /// 获取模拟战斗最大轮数
    /// </summary>
    public static int GetSimulateBattleMaxRounds()
    {
        return BattleFormulas?.battle_timing?.simulate_battle_max_rounds ?? 100;
    }

    /// <summary>
    /// 获取战斗速度档位
    /// </summary>
    public static List<int> GetSpeedOptions()
    {
        return BattleFormulas?.battle_timing?.speed_options ?? new List<int> { 1, 2, 4 };
    }

    /// <summary>
    /// 获取站位修正系数
    /// </summary>
    public static PositionModEntry GetPositionModifier(string position)
    {
        var pos = BattleFormulas?.position_modifiers;
        if (pos == null) return null;
        return position switch
        {
            "front" => pos.front,
            "middle" => pos.middle,
            "back" => pos.back,
            _ => null
        };
    }

    /// <summary>
    /// 获取连携技列表
    /// </summary>
    public static List<SynergyEntry> GetSynergies()
    {
        return BattleFormulas?.synergy_system?.synergies ?? new List<SynergyEntry>();
    }

    // ========== 技能相关 ==========

    /// <summary>
    /// 获取职业的普通攻击技能
    /// </summary>
    public static SkillDetailEntry GetNormalAttack(string heroClass)
    {
        string id = heroClass.ToLower();
        var entry = Skills?.hero_skills?.Find(s => s.hero_class == id);
        return entry?.normal_attack;
    }

    /// <summary>
    /// 获取职业的主动技能
    /// </summary>
    public static SkillDetailEntry GetActiveSkill(string heroClass)
    {
        string id = heroClass.ToLower();
        var entry = Skills?.hero_skills?.Find(s => s.hero_class == id);
        return entry?.active_skill;
    }

    /// <summary>
    /// 获取职业的被动技能
    /// </summary>
    public static PassiveSkillEntry GetPassive(string heroClass)
    {
        string id = heroClass.ToLower();
        var entry = Skills?.hero_skills?.Find(s => s.hero_class == id);
        return entry?.passive;
    }

    /// <summary>
    /// 获取骰子组合技能
    /// </summary>
    public static DiceComboSkillEntry GetDiceComboSkill(string comboId)
    {
        var skills = Skills?.dice_combo_skills?.skills;
        if (skills == null) return null;
        return skills.Find(s => s.combo_id == comboId);
    }

    /// <summary>
    /// 获取骰子组合技能使用次数限制
    /// </summary>
    public static int GetDiceComboSkillUsageLimit()
    {
        return Skills?.dice_combo_skills?.usage_limit_per_battle ?? 1;
    }

    // ========== 掉落/奖励相关 ==========

    /// <summary>
    /// 获取肉鸽奖励类型权重（按关卡阶段）
    /// </summary>
    public static int GetRewardWeight(string rewardTypeId, int levelId)
    {
        var rewardTypes = DropTables?.roguelike_rewards?.reward_types;
        if (rewardTypes == null) return 0;

        var entry = rewardTypes.Find(r => r.id == rewardTypeId);
        if (entry?.weight_by_phase == null) return 0;

        string phase = GetPhaseKey(levelId);
        if (entry.weight_by_phase.TryGetValue(phase, out int weight))
            return weight;

        return 0;
    }

    /// <summary>
    /// 获取奖励选项数量（默认3）
    /// </summary>
    public static int GetRewardChoiceCount()
    {
        return DropTables?.roguelike_rewards?.max_choices ?? 3;
    }

    /// <summary>
    /// 获取金币奖励
    /// </summary>
    public static int GetGoldReward(int levelId)
    {
        return GameBalance.GetBaseGoldReward(levelId);
    }

    /// <summary>
    /// 获取保底触发次数
    /// </summary>
    public static int GetGuaranteeTriggerCount()
    {
        return DropTables?.roguelike_rewards?.guarantee_system?.trigger_missing_count ?? 3;
    }

    /// <summary>
    /// 获取保底权重倍率
    /// </summary>
    public static float GetGuaranteeWeightMultiplier()
    {
        return DropTables?.roguelike_rewards?.guarantee_system?.weight_multiplier_on_guarantee ?? 2f;
    }

    // ========== 经济相关 ==========

    /// <summary>初始金币</summary>
    public static int GetStartingGold() => Economy?.settings?.starting_gold ?? 10;
    /// <summary>利息率(%)</summary>
    public static int GetInterestRate() => Economy?.settings?.interest_rate_pct ?? 10;
    /// <summary>最大利息</summary>
    public static int GetMaxInterest() => Economy?.settings?.max_interest ?? 5;
    /// <summary>重摇基础费用</summary>
    public static int GetRerollCostBase() => Economy?.settings?.reroll_cost_base ?? 1;
    /// <summary>重摇费用递增</summary>
    public static int GetRerollCostIncrement() => Economy?.settings?.reroll_cost_increment ?? 1;

    // ========== 商店相关 ==========

    /// <summary>装备商品配置（economy.json shop.items 中 category=="equipment" 的条目）</summary>
    public static EconomyShopItemConfig GetShopEquipmentConfig()
    {
        var items = Economy?.shop?.items;
        if (items == null) return null;
        return items.Find(i => i.category == "equipment");
    }

    /// <summary>商店折扣概率（0~1），fallback 0.2</summary>
    public static float GetShopDiscountChance()
    {
        var equipConfig = GetShopEquipmentConfig();
        if (equipConfig != null && equipConfig.discount_chance > 0)
            return equipConfig.discount_chance;
        return 0.2f;
    }

    /// <summary>商店折扣率（0~1），fallback 0.7</summary>
    public static float GetShopDiscountRate()
    {
        var equipConfig = GetShopEquipmentConfig();
        if (equipConfig != null && equipConfig.discount_rate > 0)
            return equipConfig.discount_rate;
        return 0.7f;
    }

    /// <summary>按品质获取卡牌价格（取 price_by_rarity 的中间值），fallback basePrice * (rarity+1)</summary>
    public static int GetCardPriceByRarity(int rarityIndex, int fallbackBasePrice)
    {
        var equipConfig = GetShopEquipmentConfig();
        var priceByRarity = equipConfig?.price_by_rarity;
        if (priceByRarity != null)
        {
            string[] rarityKeys = { "common", "uncommon", "rare", "legendary" };
            if (rarityIndex >= 0 && rarityIndex < rarityKeys.Length)
            {
                string key = rarityKeys[rarityIndex];
                if (priceByRarity.TryGetValue(key, out int[] range) && range != null && range.Length >= 2)
                {
                    return (range[0] + range[1]) / 2;
                }
            }
        }
        return fallbackBasePrice * (rarityIndex + 1);
    }

    /// <summary>获取装备品质掉落权重列表，fallback null</summary>
    public static List<EconomyEquipRarityConfig> GetEquipmentRarities()
    {
        return Economy?.equipment?.rarities ?? new List<EconomyEquipRarityConfig>();
    }

    // ========== 遗物相关 ==========

    /// <summary>
    /// 获取所有遗物配置
    /// </summary>
    public static List<RelicEntry> GetAllRelics()
    {
        return Relics?.relics ?? new List<RelicEntry>();
    }

    /// <summary>
    /// 获取遗物稀有度权重
    /// </summary>
    public static int GetRelicRarityWeight(string rarity, int currentLevel)
    {
        var weights = Relics?.rarity_weights;
        if (weights == null) return 0;
        if (weights.TryGetValue(rarity, out RelicRarityWeight weight))
        {
            if (currentLevel >= weight.min_level)
                return weight.base_weight;
        }
        return 0;
    }

    /// <summary>最大遗物数量</summary>
    public static int GetMaxRelicsPerRun() => Relics?.max_relics_per_run ?? 10;
    /// <summary>遗物最大选择数</summary>
    public static int GetMaxRelicChoices() => Relics?.max_relic_choices ?? 3;

    // ========== 骰子相关 ==========

    /// <summary>骰子数量</summary>
    public static int GetDiceCount() => DiceSystem?.dice_config?.dice_count ?? 3;
    /// <summary>骰子面数</summary>
    public static int GetDiceFaces() => DiceSystem?.dice_config?.faces ?? 6;
    /// <summary>免费重摇次数</summary>
    public static int GetFreeRerolls() => DiceSystem?.dice_config?.free_rerolls ?? 1;
    /// <summary>遗物增加的最大重摇次数</summary>
    public static int GetMaxRerollsFromRelics() => DiceSystem?.dice_config?.max_rerolls_from_relics ?? 3;
    /// <summary>骰子组合列表</summary>
    public static List<DiceCombinationEntry> GetDiceCombinations() => DiceSystem?.combinations ?? new List<DiceCombinationEntry>();

    // ========== 机制敌人相关 ==========

    /// <summary>
    /// 获取所有机制敌人配置列表
    /// </summary>
    public static List<MechanicEnemyEntry> GetMechanicEnemies()
    {
        return MechanicEnemies?.mechanic_enemies ?? new List<MechanicEnemyEntry>();
    }

    /// <summary>
    /// 按ID获取机制敌人配置
    /// </summary>
    public static MechanicEnemyEntry GetMechanicEnemy(string id)
    {
        var enemies = MechanicEnemies?.mechanic_enemies;
        if (enemies == null) return null;
        return enemies.Find(e => e.id == id);
    }

    /// <summary>
    /// 根据关卡ID获取匹配的机制怪配置
    /// </summary>
    public static MechanicEnemyEntry GetMechanicEnemyForLevel(int levelId)
    {
        var entries = MechanicEnemies?.mechanic_enemies;
        if (entries == null || entries.Count == 0) return null;

        if (levelId >= 16)
        {
            // 16+关：从所有 min_level <= levelId 的条目中随机选
            var candidates = entries.FindAll(e => e.min_level <= levelId);
            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        // 11-15关：精确匹配 min_level
        return entries.Find(e => e.min_level == levelId)
            ?? entries.Find(e => e.min_level <= levelId);
    }

    /// <summary>
    /// 获取组合机制配置
    /// </summary>
    public static CombinedMechanicsConfig GetCombinedMechanics()
    {
        return MechanicEnemies?.combined_mechanics;
    }

    /// <summary>
    /// 获取难度缩放配置
    /// </summary>
    public static MechanicDifficultyScalingConfig GetMechanicDifficultyScaling()
    {
        return MechanicEnemies?.difficulty_scaling;
    }

    // ========== 骰子面效果相关 ==========

    /// <summary>
    /// 获取骰子面效果配置
    /// </summary>
    public static FaceEffectsFileConfig GetFaceEffectsConfig()
    {
        return FaceEffects;
    }

    /// <summary>
    /// 获取面效果列表（供 FaceEffectExecutor 使用）
    /// </summary>
    public static List<FaceEffectEntry> GetFaceEffects()
    {
        return FaceEffects?.face_effects ?? new List<FaceEffectEntry>();
    }

    /// <summary>
    /// 按效果ID查找面效果定义
    /// </summary>
    public static FaceEffectEntry GetFaceEffectDef(string effectId)
    {
        var effects = FaceEffects?.face_effects;
        if (effects == null) return null;
        return effects.Find(e => e.id == effectId);
    }

    /// <summary>
    /// 获取所有面效果定义
    /// </summary>
    public static List<FaceEffectEntry> GetAllFaceEffects()
    {
        return FaceEffects?.face_effects ?? new List<FaceEffectEntry>();
    }

    // ========== 英雄经验相关 ==========

    /// <summary>每级属性加成百分比（默认5%）</summary>
    public static float GetLevelStatBonusPct() => HeroExpConfig?.level_stat_bonus_pct ?? 5f;

    /// <summary>被动技能强化阈值列表</summary>
    public static List<int> GetPassiveSkillThresholds() => HeroExpConfig?.passive_skill_auto_level?.level_thresholds ?? new List<int> {3, 6, 9, 12};

    /// <summary>每个阈值的被动技能加成</summary>
    public static float GetPassiveSkillBonusPerThreshold() => HeroExpConfig?.passive_skill_auto_level?.bonus_per_threshold ?? 0.1f;

    // ========== 星级相关 ==========

    /// <summary>星级倍率</summary>
    public static float GetStarMultiplier(int starLevel)
    {
        return GameBalance.GetStarMultiplier(starLevel);
    }

    // ========== 难度相关 ==========

    /// <summary>关卡难度系数</summary>
    public static float GetLevelDifficulty(int levelId)
    {
        return GameBalance.GetLevelDifficulty(levelId);
    }

    // ========== 工具方法 ==========

    // ========== 肉鸽地图路径相关 ==========

    /// <summary>
    /// 获取地图生成配置（带fallback默认值）
    /// </summary>
    public static MapGenerationConfig GetMapGenerationConfig()
    {
        if (RoguelikeMapConfig?.map_generation != null)
            return RoguelikeMapConfig.map_generation;

        // Fallback: 返回硬编码默认值
        return new MapGenerationConfig
        {
            total_layers = 15,
            min_nodes_per_layer = 2,
            max_nodes_per_layer = 4,
            boss_interval = 5,
            max_connections_per_node = 3,
            start_layer_node_count = 1,
            fork_layers = new List<int> { 2, 4, 7, 9, 12 },
            fork_min_paths = 2,
            fork_max_paths = 3,
            convergence_layers = new List<int> { 4, 9, 14 },
            convergence_description = "在Boss前一层收敛路径"
        };
    }

    /// <summary>
    /// 获取指定层的阶段权重配置
    /// </summary>
    public static RoguelikeMapPhaseWeights GetMapNodeWeights(int layer)
    {
        var weights = RoguelikeMapConfig?.node_weights;
        if (weights == null) return null;

        // layer is 0-based, level_range is 1-based, so compare layer+1
        int level = layer + 1;
        if (weights.phase_1?.level_range != null
            && level >= weights.phase_1.level_range[0]
            && level <= weights.phase_1.level_range[1])
            return weights.phase_1;
        if (weights.phase_2?.level_range != null
            && level >= weights.phase_2.level_range[0]
            && level <= weights.phase_2.level_range[1])
            return weights.phase_2;
        if (weights.phase_3?.level_range != null
            && level >= weights.phase_3.level_range[0]
            && level <= weights.phase_3.level_range[1])
            return weights.phase_3;

        return weights.phase_1; // fallback
    }

    /// <summary>
    /// 获取指定层的节点类型权重字典
    /// </summary>
    public static Dictionary<string, int> GetNodeWeightsForLayer(int layer)
    {
        var phaseWeights = GetMapNodeWeights(layer);
        if (phaseWeights?.weights != null)
            return phaseWeights.weights;

        // Fallback defaults
        return new Dictionary<string, int>
        {
            { "Battle", 40 },
            { "Elite", 10 },
            { "Event", 20 },
            { "Shop", 15 },
            { "Rest", 10 },
            { "Treasure", 5 }
        };
    }

    /// <summary>
    /// 获取敌人HP倍率（按层）
    /// 公式: 1.0 + layer * 0.15
    /// </summary>
    public static float GetEnemyHpMultiplier(int layer)
    {
        var scaling = RoguelikeMapConfig?.difficulty_scaling;
        if (scaling != null && !string.IsNullOrEmpty(scaling.enemy_hp_multiplier_formula))
            return ParseLayerFormula(scaling.enemy_hp_multiplier_formula, layer);
        return 1.0f + layer * 0.15f;
    }

    /// <summary>
    /// 获取敌人ATK倍率（按层）
    /// 公式: 1.0 + layer * 0.12
    /// </summary>
    public static float GetEnemyAtkMultiplier(int layer)
    {
        var scaling = RoguelikeMapConfig?.difficulty_scaling;
        if (scaling != null && !string.IsNullOrEmpty(scaling.enemy_atk_multiplier_formula))
            return ParseLayerFormula(scaling.enemy_atk_multiplier_formula, layer);
        return 1.0f + layer * 0.12f;
    }

    /// <summary>
    /// 获取特殊规则配置
    /// </summary>
    public static RoguelikeMapSpecialRulesConfig GetMapSpecialRules()
    {
        return RoguelikeMapConfig?.special_rules;
    }

    /// <summary>
    /// 解析 "1.0 + layer * 0.15" 类型的公式
    /// </summary>
    private static float ParseLayerFormula(string formula, int layer)
    {
        // 支持格式: "1.0 + layer * 0.15"
        try
        {
            var parts = formula.Split('+');
            float baseVal = float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (parts.Length < 2) return baseVal;

            var multPart = parts[1].Trim();
            // 期望 "layer * X" 格式
            if (multPart.StartsWith("layer"))
            {
                var tokens = multPart.Split('*');
                if (tokens.Length >= 2)
                {
                    float coefficient = float.Parse(tokens[tokens.Length - 1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    return baseVal + layer * coefficient;
                }
            }
            return baseVal;
        }
        catch
        {
            Debug.LogWarning($"[BalanceProvider] 无法解析公式: {formula}, 使用默认值");
            return 1.0f;
        }
    }

    // ========== 工具方法（通用） ==========

    /// <summary>关卡ID → 阶段Key</summary>
    private static string GetPhaseKey(int levelId)
    {
        if (levelId <= 5) return "phase_1_level_1_5";
        if (levelId <= 10) return "phase_2_level_6_10";
        return "phase_3_level_11_plus";
    }

    /// <summary>字符串 → HeroClass 枚举</summary>
    private static HeroClass ParseHeroClass(string role)
    {
        if (string.IsNullOrEmpty(role)) return HeroClass.Warrior;
        string r = role.ToLower().Trim();
        if (r == "warrior" || r == "战士") return HeroClass.Warrior;
        if (r == "mage" || r == "法师") return HeroClass.Mage;
        if (r == "assassin" || r == "刺客") return HeroClass.Assassin;
        return HeroClass.Warrior;
    }

    // ========== 成就系统相关 ==========

    private static AchievementsConfig _achievements;
    public static AchievementsConfig Achievements => _achievements ?? (_achievements = ConfigLoader.LoadAchievements());

    /// <summary>获取所有成就定义</summary>
    public static List<AchievementDef> GetAllAchievements()
    {
        return Achievements?.achievements ?? new List<AchievementDef>();
    }

    /// <summary>获取成就分类列表</summary>
    public static List<AchievementCategoryEntry> GetAchievementCategories()
    {
        return Achievements?.categories ?? new List<AchievementCategoryEntry>();
    }

    /// <summary>按分类获取成就</summary>
    public static List<AchievementDef> GetAchievementsByCategory(string categoryId)
    {
        var all = Achievements?.achievements;
        if (all == null) return new List<AchievementDef>();
        return all.FindAll(a => a.category == categoryId);
    }

    /// <summary>按ID获取成就定义</summary>
    public static AchievementDef GetAchievementDef(string achievementId)
    {
        var all = Achievements?.achievements;
        if (all == null) return null;
        return all.Find(a => a.id == achievementId);
    }

    /// <summary>获取成就稀有度颜色</summary>
    public static string GetAchievementRarityColor(string rarity)
    {
        if (Achievements?.rarity_display != null && Achievements.rarity_display.TryGetValue(rarity, out var entry))
            return entry.color;
        return "#FFFFFF";
    }

    /// <summary>获取成就稀有度中文名</summary>
    public static string GetAchievementRarityName(string rarity)
    {
        if (Achievements?.rarity_display != null && Achievements.rarity_display.TryGetValue(rarity, out var entry))
            return entry.name_cn;
        return "普通";
    }
}
