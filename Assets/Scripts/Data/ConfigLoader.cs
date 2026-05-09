using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// JSON配置加载器 — 从 Resources/Data/ 加载所有游戏配置
/// 使用 Newtonsoft.Json，JSON放 Resources/Data/
/// 数据模型严格匹配JSON文件结构
/// </summary>
public static class ConfigLoader
{
    private static Dictionary<string, JObject> _cache = new Dictionary<string, JObject>();
    private static Dictionary<string, object> _typedCache = new Dictionary<string, object>();

    /// <summary>
    /// 从 Resources/Data/ 加载JSON文件并缓存
    /// </summary>
    public static JObject LoadJson(string fileName)
    {
        string key = fileName.Replace(".json", "");
        if (_cache.TryGetValue(key, out JObject cached))
            return cached;

        var textAsset = Resources.Load<TextAsset>($"Data/{key}");
        if (textAsset == null)
        {
            Debug.LogError($"[ConfigLoader] 未找到配置文件: Data/{key}");
            return null;
        }

        var obj = JObject.Parse(textAsset.text);
        _cache[key] = obj;
        return obj;
    }

    /// <summary>
    /// 反序列化为指定类型（带缓存）
    /// </summary>
    public static T Load<T>(string fileName) where T : class
    {
        string key = typeof(T).Name;
        if (_typedCache.TryGetValue(key, out object cached))
            return cached as T;

        var json = LoadJson(fileName);
        if (json == null) return null;
        var result = json.ToObject<T>();
        _typedCache[key] = result;
        return result;
    }

    /// <summary>
    /// 清除缓存（场景切换时调用）
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _typedCache.Clear();
    }

    // ========== 便捷加载方法 ==========

    /// <summary>加载英雄职业配置</summary>
    public static HeroClassesConfig LoadHeroClasses() => Load<HeroClassesConfig>("hero_classes");

    /// <summary>加载遗物配置</summary>
    public static RelicsConfig LoadRelics() => Load<RelicsConfig>("relics");

    /// <summary>加载骰子系统配置</summary>
    public static DiceSystemConfig LoadDiceSystem() => Load<DiceSystemConfig>("dice_system");

    /// <summary>加载敌人配置</summary>
    public static EnemiesConfig LoadEnemies() => Load<EnemiesConfig>("enemies");

    /// <summary>加载关卡配置</summary>
    public static LevelsConfig LoadLevels() => Load<LevelsConfig>("levels");

    /// <summary>加载经济配置</summary>
    public static EconomyConfig LoadEconomy() => Load<EconomyConfig>("economy");

    /// <summary>加载战斗公式配置</summary>
    public static BattleFormulasConfig LoadBattleFormulas() => Load<BattleFormulasConfig>("battle_formulas");

    /// <summary>加载技能配置</summary>
    public static SkillsConfig LoadSkills() => Load<SkillsConfig>("skills");

    /// <summary>加载掉落表配置</summary>
    public static DropTablesConfig LoadDropTables() => Load<DropTablesConfig>("drop_tables");
}

// =====================================================================
// 数据模型类 — 严格匹配JSON文件结构
// =====================================================================

// ---------- hero_classes.json ----------

/// <summary>英雄职业配置</summary>
public class HeroClassesConfig
{
    public string _version;
    public string _description;
    public List<HeroClassEntry> classes;
    public Dictionary<string, StarRatingEntry> star_rating;
    public RoguelikeBoostConfig roguelike_boost;
}

public class HeroClassEntry
{
    public string id;
    public string name_cn;
    public string role;
    public string description;
    public HeroStatsEntry base_stats;
    public HeroGrowthEntry growth_per_level;
    public int summon_cost;
    public Dictionary<string, Dictionary<string, float>> position_bonus;
    public string color;
    public string icon_ref;
}

public class HeroStatsEntry
{
    public int max_health;
    public int attack;
    public int defense;
    public int speed;
    public float crit_rate;
    public float crit_damage;
}

public class HeroGrowthEntry
{
    public float max_health;
    public float attack;
    public float defense;
    public float speed;
    public float crit_rate;
    public float crit_damage;
}

public class StarRatingEntry
{
    public float multiplier;
}

public class RoguelikeBoostConfig
{
    public float max_health_pct;
    public float attack_pct;
    public float defense_pct;
    public float speed_pct;
    public float crit_rate_pct;
}

// ---------- battle_formulas.json ----------

/// <summary>战斗公式配置</summary>
public class BattleFormulasConfig
{
    public string _version;
    public string _description;
    public DamageFormulaConfig damage_formula;
    public CritFormulaConfig crit_formula;
    public HealFormulaConfig heal_formula;
    public DodgeFormulaConfig dodge_formula;
    public ThornsFormulaConfig thorns_formula;
    public PositionModifiersConfig position_modifiers;
    public SynergySystemConfig synergy_system;
    public BattleTimingConfig battle_timing;
}

public class DamageFormulaConfig
{
    public string base_formula;
    public int min_damage;
}

public class CritFormulaConfig
{
    public string check;
    public float crit_multiplier_base;
    public string crit_damage_additive;
    public string final_crit_multiplier;
}

public class HealFormulaConfig
{
    public string base_formula;
    public float heal_multiplier_default;
}

public class DodgeFormulaConfig
{
    public string check;
    public List<string> dodge_rate_sources;
    public string speed_dodge_formula;
}

public class ThornsFormulaConfig
{
    public string base_formula;
    public float thorns_rate_default;
}

public class PositionModifiersConfig
{
    public string _description;
    public PositionModEntry front;
    public PositionModEntry middle;
    public PositionModEntry back;
}

public class PositionModEntry
{
    public float shield_bonus_pct;
    public float damage_taken_modifier;
    public float damage_bonus_pct;
    public List<string> suitable_roles;
}

public class SynergySystemConfig
{
    public string _description;
    public List<SynergyEntry> synergies;
}

public class SynergyEntry
{
    public string id;
    public string name_cn;
    public string condition;
    public string effect;
    public float defense_bonus_pct;
    public float attack_bonus_pct;
    public float crit_rate_bonus_pct;
    public float all_stats_bonus_pct;
    public string target; // self_class / all_allies
}

public class BattleTimingConfig
{
    public float max_battle_time_sec;
    public float battle_tick_interval_sec;
    public List<int> speed_options;
    public int simulate_battle_max_rounds;
    public string overtime_rule;
}

// ---------- enemies.json ----------

/// <summary>敌人配置</summary>
public class EnemiesConfig
{
    public string _version;
    public string _description;
    public EnemyScalingConfig scaling;
    public List<EnemyEntry> enemy_types;
    public List<BossEntry> boss_types;
    public MechanicEnemiesConfig mechanic_enemies;
    public EnemyCountFormulaConfig enemy_count_formula;
}

public class EnemyScalingConfig
{
    public string _description;
    public string formula;
    public string difficulty_multiplier;
    public string note;
}

public class EnemyEntry
{
    public string id;
    public string name_cn;
    public string role;
    public EnemyStatsEntry base_stats;
    public List<string> skills;
    public int spawn_weight;
    public int min_level;
    public float death_explosion_damage_pct;
    public float heal_amount_pct;
    public string heal_target;
}

public class EnemyStatsEntry
{
    public int max_health;
    public int attack;
    public int defense;
    public int speed;
    public float crit_rate;
}

public class BossEntry
{
    public string id;
    public string name_cn;
    public string role;
    public EnemyStatsEntry base_stats;
    public List<string> skills;
    public string enrage_trigger;
    public float enrage_attack_bonus_pct;
    public int spawn_level;
    public int summon_count;
    public int summon_interval_rounds;
}

public class MechanicEnemiesConfig
{
    public string _description;
    public List<MechanicEnemyEntry> enemies;
}

public class MechanicEnemyEntry
{
    public string id;
    public string name_cn;
    public string mechanic;
    public string mechanic_desc_cn;
    public EnemyStatsEntry base_stats;
    public Dictionary<string, object> mechanic_params;
    public string counter_strategy;
    public int min_level;
}

public class EnemyCountFormulaConfig
{
    public string _description;
    public string formula;
    public int level_1_to_2;
    public int level_3_to_5;
    public int level_6_to_8;
    public int level_9_plus;
    public List<int> boss_levels;
    public int boss_adds_minion_count;
}

// ---------- levels.json ----------

/// <summary>关卡配置</summary>
public class LevelsConfig
{
    public string _version;
    public string _description;
    public DifficultyCurveConfig difficulty_curve;
    public Dictionary<string, LevelTierEntry> level_templates;
    public List<int> boss_levels;
    public Dictionary<string, BossConfigEntry> boss_config;
    public InfiniteModeConfig infinite_mode;
    public RandomEventConfig random_event;
}

public class DifficultyCurveConfig
{
    public DifficultyPhaseEntry phase_1;
    public DifficultyPhaseEntry phase_2;
}

public class DifficultyPhaseEntry
{
    public string _description;
    public List<int> range;
    public string formula;
    public Dictionary<string, float> examples;
    public string _note;
}

public class LevelTierEntry
{
    public string _description;
    public List<int> range;
    public List<string> enemy_pool;
    public int max_enemies;
    public bool allow_elite;
    public float elite_chance;
    public bool allow_boss;
    public List<int> boss_levels;
    public bool allow_mechanic;
    public float mechanic_enemy_chance;
}

public class BossConfigEntry
{
    public string boss_type;
    public int adds;
    public object difficulty_scale_override; // float or "dynamic"
    public bool mechanic_combo;
}

public class InfiniteModeConfig
{
    public string _description;
    public int max_level;
    public InfiniteScalingCapConfig scaling_cap;
    public MechanicCombinationConfig mechanic_combination;
}

public class InfiniteScalingCapConfig
{
    public string _description;
    public float max_difficulty_multiplier;
    public int cap_at_level;
}

public class MechanicCombinationConfig
{
    public string level_16_plus;
    public string level_30_plus;
}

public class RandomEventConfig
{
    public float trigger_chance;
    public List<int> trigger_level_range;
    public List<string> event_types;
}

// ---------- skills.json ----------

/// <summary>技能配置</summary>
public class SkillsConfig
{
    public string _version;
    public string _description;
    public List<HeroSkillEntry> hero_skills;
    public DiceComboSkillConfig dice_combo_skills;
}

public class HeroSkillEntry
{
    public string hero_class;
    public SkillDetailEntry normal_attack;
    public SkillDetailEntry active_skill;
    public PassiveSkillEntry passive;
}

public class SkillDetailEntry
{
    public string id;
    public string name_cn;
    public float skill_multiplier;
    public string target_type; // single / aoe
    public int cooldown_rounds;
    public string description_cn;
    public string additional_effect;
    public int stun_rounds;
    public string aoe_range;
    public float burn_dot_pct;
    public int burn_duration_rounds;
    public string effect;
    public float shield_pct;
    public string trigger;
    public bool guaranteed_crit;
}

public class PassiveSkillEntry
{
    public string id;
    public string name_cn;
    public string effect;
    public float defense_multiplier;
    public float crit_rate_bonus;
    public string trigger;
    public string description_cn;
    public float shield_pct;
    public int cooldown_rounds;
}

public class DiceComboSkillConfig
{
    public string _description;
    public string trigger_timing;
    public int usage_limit_per_battle;
    public List<DiceComboSkillEntry> skills;
}

public class DiceComboSkillEntry
{
    public string combo_id;
    public string skill_name_cn;
    public string effect_type; // team_buff / single_target / fallback
    public float attack_bonus_pct;
    public int duration_rounds;
    public string animation;
    public float attack_speed_bonus_pct;
    public float dodge_bonus_pct;
    public float damage_multiplier;
    public string target;
    public float sum_attack_bonus_pct;
    public string description_cn;
}

// ---------- relics.json ----------

/// <summary>遗物配置</summary>
public class RelicsConfig
{
    public string _version;
    public Dictionary<string, RelicRarityWeight> rarity_weights;
    public List<RelicEntry> relics;
    public int max_relics_per_run;
    public int max_relic_choices;
}

public class RelicRarityWeight
{
    public int base_weight;
    public int min_level;
}

public class RelicEntry
{
    public string id;
    public string name_cn;
    public string rarity;
    public string description_cn;
    public string effect_type;
    public JObject effect;
    public string trigger;
    public bool stackable;
    public int max_stacks;
}

// ---------- dice_system.json ----------

/// <summary>骰子系统配置</summary>
public class DiceSystemConfig
{
    public string _version;
    public string _description;
    public DiceConfigSection dice_config;
    public List<DiceCombinationEntry> combinations;
}

public class DiceConfigSection
{
    public int dice_count;
    public int faces;
    public List<int> face_values;
    public int total_outcomes;
    public int free_rerolls;
    public int max_rerolls_from_relics;
    public float roll_animation_duration_sec;
    public float reroll_animation_duration_sec;
}

public class DiceCombinationEntry
{
    public string id;
    public string name_cn;
    public string name_en;
    public string condition;
    public float probability;
    public int occurrences_out_of_216;
    public int sort_priority;
    public JObject effects;
    public JObject visual;
}

// ---------- economy.json ----------

/// <summary>经济配置</summary>
public class EconomyConfig
{
    public string _version;
    public EconomySettings settings;
}

public class EconomySettings
{
    public int starting_gold;
    public int interest_rate_pct;
    public int max_interest;
    public int reroll_cost_base;
    public int reroll_cost_increment;
}

// ---------- drop_tables.json ----------

/// <summary>掉落表配置</summary>
public class DropTablesConfig
{
    public string _version;
    public string _description;
    public RoguelikeRewardsConfig roguelike_rewards;
    public GoldRewardsConfig gold_rewards;
    public EquipmentDropConfig equipment_drop;
}

public class RoguelikeRewardsConfig
{
    public string _description;
    public int max_choices;
    public List<RewardTypeEntry> reward_types;
    public GuaranteeSystemConfig guarantee_system;
    public JObject dynamic_adjustment;
}

public class RewardTypeEntry
{
    public string id;
    public string name_cn;
    public string description_cn;
    public Dictionary<string, int> weight_by_phase;
    public int max_units_per_run;
    public int max_upgrades_per_run;
    public int max_relics_per_run;
}

public class GuaranteeSystemConfig
{
    public string _description;
    public int trigger_missing_count;
    public float weight_multiplier_on_guarantee;
}

public class GoldRewardsConfig
{
    public string _description;
    public string formula;
    public int base_gold;
    public int level_bonus;
    public Dictionary<string, int> milestone_bonus;
}

public class EquipmentDropConfig
{
    public string _description;
    public int guaranteed_every_n_levels;
    public float random_drop_chance;
    public bool no_drop_in_first_2_levels;
}
