using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌效果类型枚举
/// </summary>
public enum CardEffectType
{
    None,     // 无效果
    Heal,     // 治疗
    Damage,   // 伤害
    Buff,     // 增益
    Debuff,   // 减益
    Summon    // 召唤
}

/// <summary>
/// 效果定义 — 从JSON配置加载或在代码中定义
/// </summary>
[System.Serializable]
public class CardEffectDefinition
{
    public string effectId;           // 效果唯一ID
    public CardEffectType effectType; // 效果类型
    public string effectName;         // 效果名称（中文）
    public float baseValue;           // 基础数值（治疗量/伤害值/百分比等）
    public float levelScale = 0.15f;  // 每级增长比例
    public string targetScope;        // 目标范围：self / ally_single / ally_all / enemy_single / enemy_all
    public int duration = 0;          // 持续回合数（0=即时）
    public string description;        // 效果描述
}

/// <summary>
/// 卡牌效果引擎 — 单例，负责统一执行卡牌效果
/// 
/// 职责：
/// 1. 从JSON配置或内置默认值读取效果定义
/// 2. 根据卡牌的effectId查找效果定义并执行
/// 3. 支持效果类型：heal、damage、buff、debuff、summon
/// 4. 每种效果带数值（从配置读取，有fallback默认值）
/// </summary>
public class CardEffectEngine : MonoBehaviour
{
    public static CardEffectEngine Instance { get; private set; }

    /// <summary>效果定义缓存（effectId → definition）</summary>
    private Dictionary<string, CardEffectDefinition> effectDefinitions = new Dictionary<string, CardEffectDefinition>();

    /// <summary>内置fallback效果定义（当JSON未配置时使用）</summary>
    private Dictionary<string, CardEffectDefinition> fallbackDefinitions = new Dictionary<string, CardEffectDefinition>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeFallbackDefinitions();
        LoadEffectDefinitionsFromConfig();
    }

    /// <summary>
    /// 初始化内置fallback效果定义
    /// 当JSON配置文件未提供对应效果时，使用这些默认值
    /// </summary>
    private void InitializeFallbackDefinitions()
    {
        // === 属性卡效果 ===
        AddFallback("power_training", CardEffectType.Buff, "力量训练", 10f, "ally_all", 0, "攻击力+{value}");
        AddFallback("armor_training", CardEffectType.Buff, "坚固护甲", 10f, "ally_all", 0, "防御力+{value}");
        AddFallback("speed_training", CardEffectType.Buff, "灵敏训练", 10f, "ally_all", 0, "速度+{value}");
        AddFallback("holy_bless", CardEffectType.Buff, "神圣祝福", 50f, "ally_all", 0, "最大生命+{value}");

        // === 战斗卡效果 ===
        AddFallback("slash", CardEffectType.Damage, "斩击", 30f, "enemy_all", 0, "对敌方造成{value}%攻击力伤害");
        AddFallback("shield_bash", CardEffectType.Buff, "护盾冲击", 20f, "ally_all", 0, "获得{value}点护盾");
        AddFallback("find_weakness", CardEffectType.Debuff, "寻找弱点", 15f, "enemy_all", 2, "敌方暴击率-{value}%");
        AddFallback("flame_slash", CardEffectType.Damage, "火焰斩", 40f, "enemy_all", 0, "火焰伤害{value}%攻击力，附带灼烧");
        AddFallback("frost_armor", CardEffectType.Buff, "冰霜护甲", 25f, "ally_all", 3, "获得{value}护盾，附带减速");
        AddFallback("wind_step", CardEffectType.Buff, "疾风步", 30f, "ally_all", 0, "速度+{value}%");
        AddFallback("fatal_blow", CardEffectType.Damage, "致命一击", 50f, "enemy_single", 0, "暴击伤害+{value}%");
        AddFallback("summon_boost", CardEffectType.Buff, "召唤强化", 1f, "ally_all", 0, "召唤费用-{value}");
        AddFallback("fireball", CardEffectType.Damage, "火球术", 35f, "enemy_all", 0, "对全体造成{value}%攻击力火焰伤害");
        AddFallback("chain_strike", CardEffectType.Damage, "连环斩", 20f, "enemy_single", 0, "连击{value}次");
        AddFallback("life_steal", CardEffectType.Buff, "吸血攻击", 20f, "ally_all", 0, "攻击附带{value}%吸血");
        AddFallback("revive", CardEffectType.Heal, "复活术", 30f, "ally_single", 0, "复活一个英雄，恢复{value}%血量");
        AddFallback("poison_blade", CardEffectType.Damage, "毒刃", 15f, "enemy_single", 3, "每回合{value}点毒伤");
        AddFallback("energy_burst", CardEffectType.Buff, "能量爆发", 25f, "ally_all", 0, "全属性+{value}%");
        AddFallback("armor_break", CardEffectType.Debuff, "破甲攻击", 100f, "enemy_all", 2, "敌方护甲归零{value}%");
        AddFallback("group_heal", CardEffectType.Heal, "群体治疗", 25f, "ally_all", 0, "全体恢复{value}%最大生命");
        AddFallback("lightning_chain", CardEffectType.Damage, "闪电链", 15f, "enemy_single", 0, "闪电弹射{value}次");
        AddFallback("thorns", CardEffectType.Buff, "荆棘反伤", 20f, "ally_all", 0, "受击反伤{value}%");
        AddFallback("berserk_potion", CardEffectType.Buff, "狂暴药水", 40f, "ally_all", 0, "攻击+{value}%，防御-30%");
        AddFallback("shield_resonance", CardEffectType.Buff, "护盾共振", 20f, "ally_all", 0, "获得{value}%最大生命护盾");
        AddFallback("reroll", CardEffectType.None, "重摇", 0f, "self", 0, "重新投掷骰子");

        // === 进化卡效果 ===
        AddFallback("evolution_awaken", CardEffectType.Buff, "进化觉醒", 0f, "ally_single", 0, "使一个英雄进化");
    }

    /// <summary>
    /// 添加一个fallback效果定义
    /// </summary>
    private void AddFallback(string id, CardEffectType type, string name, float baseVal,
        string scope, int duration, string desc)
    {
        var def = new CardEffectDefinition
        {
            effectId = id,
            effectType = type,
            effectName = name,
            baseValue = baseVal,
            levelScale = 0.15f,
            targetScope = scope,
            duration = duration,
            description = desc
        };
        fallbackDefinitions[id] = def;
    }

    /// <summary>
    /// 从JSON配置文件加载效果定义
    /// 尝试从 face_effects.json 或 skills.json 读取，失败则跳过
    /// </summary>
    private void LoadEffectDefinitionsFromConfig()
    {
        // 尝试加载 face_effects.json
        var effectsFile = Resources.Load<TextAsset>("Data/face_effects");
        if (effectsFile != null)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<EffectDefinitionList>(effectsFile.text);
                if (wrapper?.effects != null)
                {
                    foreach (var def in wrapper.effects)
                    {
                        effectDefinitions[def.effectId] = def;
                    }
                    Debug.Log($"[CardEffectEngine] 从 face_effects.json 加载了 {wrapper.effects.Length} 个效果定义");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CardEffectEngine] 加载 face_effects.json 失败: {e.Message}，使用fallback");
            }
        }

        // 尝试从 skills.json 读取卡牌相关效果
        var skillsFile = Resources.Load<TextAsset>("Data/skills");
        if (skillsFile != null)
        {
            ParseSkillsJson(skillsFile.text);
        }
    }

    /// <summary>
    /// 解析 skills.json 中的效果定义
    /// </summary>
    private void ParseSkillsJson(string json)
    {
        try
        {
            var config = JsonUtility.FromJson<SkillsConfigWrapper>(json);
            if (config?.hero_skills != null)
            {
                foreach (var heroSkill in config.hero_skills)
                {
                    // 将英雄技能转化为卡牌效果定义
                    if (heroSkill.active_skill != null)
                    {
                        var def = new CardEffectDefinition
                        {
                            effectId = heroSkill.active_skill.id,
                            effectType = CardEffectType.Damage,
                            effectName = heroSkill.active_skill.name_cn,
                            baseValue = heroSkill.active_skill.skill_multiplier * 100f,
                            targetScope = heroSkill.active_skill.target_type == "aoe" ? "enemy_all" : "enemy_single",
                            description = heroSkill.active_skill.description_cn
                        };
                        if (!effectDefinitions.ContainsKey(def.effectId))
                        {
                            effectDefinitions[def.effectId] = def;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CardEffectEngine] 解析 skills.json 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 获取效果定义（优先JSON配置 → fallback默认值 → 动态生成）
    /// </summary>
    /// <param name="effectIdStr">效果ID字符串</param>
    /// <returns>效果定义，始终不为null</returns>
    public CardEffectDefinition GetEffectDefinition(string effectIdStr)
    {
        if (string.IsNullOrEmpty(effectIdStr)) return null;

        // 1. 优先从JSON配置查找
        if (effectDefinitions.TryGetValue(effectIdStr, out var def))
            return def;

        // 2. 从fallback查找
        if (fallbackDefinitions.TryGetValue(effectIdStr, out def))
            return def;

        // 3. 动态生成一个默认效果
        return new CardEffectDefinition
        {
            effectId = effectIdStr,
            effectType = CardEffectType.None,
            effectName = effectIdStr,
            baseValue = 10f,
            targetScope = "ally_all",
            description = $"效果: {effectIdStr}"
        };
    }

    /// <summary>
    /// 执行卡牌效果 — 核心方法
    /// </summary>
    /// <param name="card">要执行的卡牌实例</param>
    /// <param name="caster">施法者（打出此卡的玩家控制者）</param>
    /// <param name="allies">友方英雄列表</param>
    /// <param name="enemies">敌方英雄列表</param>
    /// <returns>执行是否成功</returns>
    public bool ExecuteCardEffect(CardInstance card, Hero caster, List<Hero> allies, List<Hero> enemies)
    {
        if (card == null) return false;

        // 确定效果ID：优先使用 effectIdStr，否则使用枚举名称转蛇形
        string effectIdStr = !string.IsNullOrEmpty(card.Data.effectIdStr)
            ? card.Data.effectIdStr
            : CardEffectIdToSnakeCase(card.Data.effectId);

        var def = GetEffectDefinition(effectIdStr);
        if (def == null || def.effectType == CardEffectType.None)
        {
            Debug.LogWarning($"[CardEffectEngine] 未找到效果定义: {effectIdStr}，跳过执行");
            return false;
        }

        // 计算实际效果值 = 基础值 × 等级倍率
        float actualValue = def.baseValue * card.LevelMultiplier;

        // 根据效果类型执行
        switch (def.effectType)
        {
            case CardEffectType.Heal:
                ExecuteHeal(def, actualValue, allies);
                break;
            case CardEffectType.Damage:
                ExecuteDamage(def, actualValue, allies, enemies);
                break;
            case CardEffectType.Buff:
                ExecuteBuff(def, actualValue, card, allies);
                break;
            case CardEffectType.Debuff:
                ExecuteDebuff(def, actualValue, enemies);
                break;
            case CardEffectType.Summon:
                ExecuteSummon(def, actualValue, caster, allies);
                break;
        }

        Debug.Log($"[CardEffectEngine] 执行卡牌效果: {card.CardName} " +
                  $"类型={def.effectType} 数值={actualValue:F1} 等级={card.Level}");
        return true;
    }

    /// <summary>
    /// 执行治疗效果
    /// </summary>
    /// <param name="def">效果定义</param>
    /// <param name="value">计算后的效果值</param>
    /// <param name="allies">友方英雄列表</param>
    private void ExecuteHeal(CardEffectDefinition def, float value, List<Hero> allies)
    {
        if (allies == null) return;

        switch (def.targetScope)
        {
            case "ally_all":
                foreach (var hero in allies)
                {
                    if (hero != null && !hero.IsDead)
                    {
                        int healAmount = Mathf.RoundToInt(hero.MaxHealth * value / 100f);
                        hero.Heal(healAmount);
                    }
                }
                break;
            case "ally_single":
                // 治疗血量最低的友方
                Hero lowestHp = FindLowestHpHero(allies);
                if (lowestHp != null)
                {
                    int healAmount = Mathf.RoundToInt(lowestHp.MaxHealth * value / 100f);
                    lowestHp.Heal(healAmount);
                }
                break;
        }
    }

    /// <summary>
    /// 执行伤害效果
    /// </summary>
    /// <param name="def">效果定义</param>
    /// <param name="value">计算后的效果值</param>
    /// <param name="allies">友方英雄列表（用于计算伤害）</param>
    /// <param name="enemies">敌方英雄列表（受伤目标）</param>
    private void ExecuteDamage(CardEffectDefinition def, float value, List<Hero> allies, List<Hero> enemies)
    {
        if (enemies == null || enemies.Count == 0) return;

        int damageBase = 0;
        // 从友方英雄获取攻击力作为伤害基数
        if (allies != null && allies.Count > 0)
        {
            foreach (var ally in allies)
            {
                if (ally != null) damageBase += ally.Attack;
            }
            damageBase = Mathf.RoundToInt(damageBase / (float)allies.Count);
        }
        else
        {
            damageBase = Mathf.RoundToInt(value); // 无友方时使用效果值作为直接伤害
        }

        int totalDamage = Mathf.RoundToInt(damageBase * value / 100f);

        switch (def.targetScope)
        {
            case "enemy_all":
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        enemy.TakeDamage(totalDamage);
                    }
                }
                break;
            case "enemy_single":
                // 攻击攻击力最高的敌人
                Hero target = FindHighestAttackHero(enemies);
                if (target != null)
                {
                    target.TakeDamage(totalDamage);
                }
                break;
            default:
                // 默认攻击全体
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        enemy.TakeDamage(totalDamage);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 执行增益效果
    /// </summary>
    /// <param name="def">效果定义</param>
    /// <param name="value">计算后的效果值</param>
    /// <param name="card">卡牌实例（用于获取效果枚举）</param>
    /// <param name="allies">友方英雄列表</param>
    private void ExecuteBuff(CardEffectDefinition def, float value, CardInstance card, List<Hero> allies)
    {
        if (allies == null) return;

        // 根据卡牌的effectId枚举执行具体的buff逻辑
        // 这里复用 CardDeck 中已有的逻辑，提供统一的入口
        float multiplier = value / 100f;

        switch (card.Data.effectId)
        {
            case CardEffectId.PowerTraining:
                foreach (var hero in allies)
                    if (hero != null) hero.BoostAttack(multiplier);
                break;
            case CardEffectId.ArmorTraining:
                foreach (var hero in allies)
                    if (hero != null) hero.BoostDefense(multiplier);
                break;
            case CardEffectId.SpeedTraining:
                foreach (var hero in allies)
                    if (hero != null) hero.BoostSpeed(multiplier);
                break;
            case CardEffectId.HolyBless:
                foreach (var hero in allies)
                    if (hero != null) hero.BoostMaxHealth(multiplier);
                break;
            case CardEffectId.ShieldBash:
            case CardEffectId.FrostArmor:
            case CardEffectId.ShieldResonance:
                foreach (var hero in allies)
                    if (hero != null) hero.AddShield(Mathf.RoundToInt(value));
                break;
            case CardEffectId.FatalBlow:
                foreach (var hero in allies)
                    if (hero != null) hero.BoostCritRate(multiplier);
                break;
            case CardEffectId.LifeSteal:
                foreach (var hero in allies)
                    if (hero != null) hero.LifeStealRate += multiplier;
                break;
            case CardEffectId.EnergyBurst:
                foreach (var hero in allies)
                {
                    if (hero != null)
                    {
                        hero.BoostAttack(multiplier);
                        hero.BoostDefense(multiplier);
                        hero.BoostSpeed(multiplier);
                    }
                }
                break;
            case CardEffectId.Thorns:
                foreach (var hero in allies)
                    if (hero != null) hero.BattleThornsRate += multiplier;
                break;
            default:
                // 通用buff：增加攻击力
                foreach (var hero in allies)
                    if (hero != null) hero.BoostAttack(multiplier);
                Debug.Log($"[CardEffectEngine] 使用通用Buff: {def.effectName}");
                break;
        }
    }

    /// <summary>
    /// 执行减益效果
    /// </summary>
    /// <param name="def">效果定义</param>
    /// <param name="value">计算后的效果值</param>
    /// <param name="enemies">敌方英雄列表</param>
    private void ExecuteDebuff(CardEffectDefinition def, float value, List<Hero> enemies)
    {
        if (enemies == null) return;

        float debuffPercent = value / 100f;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            switch (def.effectId)
            {
                case "armor_break":
                    enemy.HasArmorBreak = true;
                    break;
                case "find_weakness":
                    enemy.BattleDefense = Mathf.RoundToInt(enemy.BattleDefense * (1f - debuffPercent));
                    break;
                default:
                    // 通用debuff：降低攻击
                    enemy.BattleAttack = Mathf.RoundToInt(enemy.BattleAttack * (1f - debuffPercent));
                    break;
            }
        }
    }

    /// <summary>
    /// 执行召唤效果
    /// </summary>
    /// <param name="def">效果定义</param>
    /// <param name="value">计算后的效果值</param>
    /// <param name="caster">施法者</param>
    /// <param name="allies">友方英雄列表</param>
    private void ExecuteSummon(CardEffectDefinition def, float value, Hero caster, List<Hero> allies)
    {
        // 召唤效果通过 CardDeck.SummonHero 处理
        // 这里提供接口，实际逻辑由 CardDeck 管理
        var deck = CardDeck.Instance;
        if (deck == null || !deck.HasSpace) return;

        Debug.Log($"[CardEffectEngine] 召唤效果执行: {def.effectName}，数值={value}");
        // 具体召唤逻辑由上层调用者通过 CardDeck 处理
    }

    // ========== 工具方法 ==========

    /// <summary>
    /// 查找血量最低的存活英雄
    /// </summary>
    private Hero FindLowestHpHero(List<Hero> heroes)
    {
        Hero lowest = null;
        float lowestRatio = float.MaxValue;
        foreach (var hero in heroes)
        {
            if (hero == null || hero.IsDead) continue;
            float ratio = (float)hero.CurrentHealth / hero.MaxHealth;
            if (ratio < lowestRatio)
            {
                lowestRatio = ratio;
                lowest = hero;
            }
        }
        return lowest;
    }

    /// <summary>
    /// 查找攻击力最高的存活英雄
    /// </summary>
    private Hero FindHighestAttackHero(List<Hero> heroes)
    {
        Hero highest = null;
        int highestAtk = int.MinValue;
        foreach (var hero in heroes)
        {
            if (hero == null || hero.IsDead) continue;
            if (hero.Attack > highestAtk)
            {
                highestAtk = hero.Attack;
                highest = hero;
            }
        }
        return highest;
    }

    /// <summary>
    /// 将 CardEffectId 枚举名称转为蛇形命名（用于效果查找）
    /// 例如：PowerTraining → power_training
    /// </summary>
    private static string CardEffectIdToSnakeCase(CardEffectId effectId)
    {
        if (effectId == CardEffectId.None) return "none";

        string name = effectId.ToString();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLower(c));
        }
        return sb.ToString();
    }

    /// <summary>
    /// 注册自定义效果定义（供外部系统扩展）
    /// </summary>
    /// <param name="definition">效果定义</param>
    public void RegisterEffect(CardEffectDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.effectId)) return;
        effectDefinitions[definition.effectId] = definition;
    }

    /// <summary>
    /// 清除所有JSON加载的效果定义（保留fallback）
    /// </summary>
    public void ClearLoadedDefinitions()
    {
        effectDefinitions.Clear();
    }
}

// ========== JSON反序列化辅助类 ==========

/// <summary>
/// face_effects.json 的根结构
/// </summary>
[System.Serializable]
public class EffectDefinitionList
{
    public CardEffectDefinition[] effects;
}

/// <summary>
/// skills.json 的根结构（简化版，仅提取所需字段）
/// </summary>
[System.Serializable]
public class SkillsConfigWrapper
{
    public HeroSkillJsonEntry[] hero_skills;
}

/// <summary>
/// skills.json 中每个英雄技能条目
/// </summary>
[System.Serializable]
public class HeroSkillJsonEntry
{
    public string hero_class;
    public SkillJsonData normal_attack;
    public SkillJsonData active_skill;
    public SkillJsonData passive;
}

/// <summary>
/// skills.json 中技能数据
/// </summary>
[System.Serializable]
public class SkillJsonData
{
    public string id;
    public string name_cn;
    public float skill_multiplier;
    public string target_type;
    public string description_cn;
    public int cooldown_rounds;
}
