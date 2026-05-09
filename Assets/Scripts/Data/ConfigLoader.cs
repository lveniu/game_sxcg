using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

/// <summary>
/// JSON配置加载器 — 从 Resources/Data/ 加载所有游戏配置
/// CTO要求：使用 Newtonsoft.Json，JSON放 Resources/Data/
/// </summary>
public static class ConfigLoader
{
    private static Dictionary<string, JObject> _cache = new Dictionary<string, JObject>();

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
    /// 反序列化为指定类型
    /// </summary>
    public static T Load<T>(string fileName) where T : class
    {
        var json = LoadJson(fileName);
        if (json == null) return null;
        return json.ToObject<T>();
    }

    /// <summary>
    /// 清除缓存（场景切换时调用）
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    // ========== 便捷加载方法 ==========

    /// <summary>加载英雄职业配置</summary>
    public static HeroClassesConfig LoadHeroClasses() => Load<HeroClassesConfig>("hero_classes");

    /// <summary>加载遗物配置</summary>
    public static RelicsConfig LoadRelics() => Load<RelicsConfig>("relics");

    /// <summary>加载骰子配置</summary>
    public static DiceSystemConfig LoadDiceSystem() => Load<DiceSystemConfig>("dice_system");

    /// <summary>加载敌人配置</summary>
    public static EnemiesConfig LoadEnemies() => Load<EnemiesConfig>("enemies");

    /// <summary>加载关卡配置</summary>
    public static LevelsConfig LoadLevels() => Load<LevelsConfig>("levels");

    /// <summary>加载经济配置</summary>
    public static EconomyConfig LoadEconomy() => Load<EconomyConfig>("economy");
}

// ========== 数据模型类 ==========

/// <summary>英雄职业配置</summary>
public class HeroClassesConfig
{
    public string _version;
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
    public string rarity; // common / rare / epic / legendary
    public string description_cn;
    public string effect_type;
    public JObject effect;
    public string trigger;
    public bool stackable;
    public int max_stacks;
}

/// <summary>骰子系统配置</summary>
public class DiceSystemConfig
{
    public string _version;
    public DiceConfigSection dice_config;
    public List<DiceCombinationEntry> combinations;
}

public class DiceConfigSection
{
    public int dice_count;
    public int faces;
    public int free_rerolls;
    public int max_rerolls_from_relics;
    public float roll_animation_duration_sec;
    public float reroll_animation_duration_sec;
}

public class DiceCombinationEntry
{
    public string id;
    public string name_cn;
    public float probability;
    public int sort_priority;
    public JObject effects;
    public JObject visual;
}

/// <summary>敌人配置</summary>
public class EnemiesConfig
{
    public string _version;
    public List<EnemyEntry> enemies;
}

public class EnemyEntry
{
    public string id;
    public string name_cn;
    public string role;
    public string description_cn;
    public HeroStatsEntry base_stats;
    public List<string> skills;
}

/// <summary>关卡配置</summary>
public class LevelsConfig
{
    public string _version;
    public List<LevelEntry> levels;
}

public class LevelEntry
{
    public int id;
    public string name_cn;
    public string type; // normal / elite / boss / event
    public List<WaveEntry> waves;
    public RewardEntry reward;
}

public class WaveEntry
{
    public List<string> enemies;
}

public class RewardEntry
{
    public int gold_base;
    public float relic_chance;
}

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
