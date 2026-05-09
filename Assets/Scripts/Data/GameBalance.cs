using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 数值总表 — JSON驱动的游戏数值管理
/// 所有数值从 Resources/Data/ 下的JSON文件读取
/// 保留硬编码默认值作为fallback
/// </summary>
public static class GameBalance
{
    // ========== 缓存 ==========

    private static HeroClassesConfig _heroConfig;
    private static EnemiesConfig _enemiesConfig;
    private static LevelsConfig _levelsConfig;
    private static BattleFormulasConfig _formulasConfig;
    private static SkillsConfig _skillsConfig;
    private static DiceSystemConfig _diceConfig;

    private static HeroClassesConfig HeroConfig
    {
        get
        {
            if (_heroConfig == null) _heroConfig = ConfigLoader.LoadHeroClasses();
            return _heroConfig;
        }
    }

    private static EnemiesConfig EnemiesCfg
    {
        get
        {
            if (_enemiesConfig == null) _enemiesConfig = ConfigLoader.LoadEnemies();
            return _enemiesConfig;
        }
    }

    private static LevelsConfig LevelsCfg
    {
        get
        {
            if (_levelsConfig == null) _levelsConfig = ConfigLoader.LoadLevels();
            return _levelsConfig;
        }
    }

    private static BattleFormulasConfig FormulasCfg
    {
        get
        {
            if (_formulasConfig == null) _formulasConfig = ConfigLoader.LoadBattleFormulas();
            return _formulasConfig;
        }
    }

    private static SkillsConfig SkillsCfg
    {
        get
        {
            if (_skillsConfig == null) _skillsConfig = ConfigLoader.LoadSkills();
            return _skillsConfig;
        }
    }

    private static DiceSystemConfig DiceCfg
    {
        get
        {
            if (_diceConfig == null) _diceConfig = ConfigLoader.LoadDiceSystem();
            return _diceConfig;
        }
    }

    // ========== 难度曲线（JSON驱动） ==========

    /// <summary>
    /// 关卡难度系数：从 levels.json 的 difficulty_curve 读取
    /// phase_1: 1 + (level-1)*0.15, phase_2: 2.35 + (level-10)*0.08
    /// </summary>
    public static float GetLevelDifficulty(int levelId)
    {
        if (LevelsCfg?.difficulty_curve == null)
        {
            // fallback 硬编码
            return levelId <= 10
                ? 1f + (levelId - 1) * 0.15f
                : 2.35f + (levelId - 10) * 0.08f;
        }

        var phase1 = LevelsCfg.difficulty_curve.phase_1;
        var phase2 = LevelsCfg.difficulty_curve.phase_2;

        if (levelId <= phase1.range[1])
        {
            // phase_1: 1 + (level-1)*0.15
            float startVal = 1f;
            float multiplier = 0.15f;
            if (phase1.examples != null && phase1.examples.Count >= 2)
            {
                // 从example推算参数
                // level_1 = 1.0, level_3 = 1.3 → per_level = 0.15
                multiplier = (phase1.examples["level_3"] - phase1.examples["level_1"]) / 2f;
            }
            return startVal + (levelId - phase1.range[0]) * multiplier;
        }
        else
        {
            // phase_2: 2.35 + (level-10)*0.08
            float phase1End = 1f + (phase1.range[1] - phase1.range[0]) * 0.15f;
            float multiplier2 = 0.08f;
            if (phase2.examples != null && phase2.examples.Count >= 2)
            {
                multiplier2 = (phase2.examples["level_20"] - phase2.examples["level_15"]) / 5f;
            }
            return phase1End + (levelId - phase1.range[1]) * multiplier2;
        }
    }

    /// <summary>
    /// 金币奖励基数
    /// </summary>
    public static int GetBaseGoldReward(int levelId)
    {
        return 20 + levelId * 10 + (levelId / 5) * 20;
    }

    // ========== 英雄数值模板（JSON驱动） ==========

    /// <summary>
    /// 从 hero_classes.json 读取英雄基础属性
    /// </summary>
    public static HeroStatTemplate GetHeroTemplate(string heroName)
    {
        if (HeroConfig?.classes != null)
        {
            // 先尝试按 name_cn 匹配
            var entry = HeroConfig.classes.Find(c => c.name_cn == heroName);
            // 再尝试按 id 匹配
            if (entry == null) entry = HeroConfig.classes.Find(c => c.id == heroName);

            if (entry != null)
            {
                HeroClass cls = entry.id switch
                {
                    "warrior" => HeroClass.Warrior,
                    "mage" => HeroClass.Mage,
                    "assassin" => HeroClass.Assassin,
                    _ => HeroClass.Warrior
                };
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
        }

        // fallback 硬编码
        return heroName switch
        {
            "战士" => new HeroStatTemplate(150, 8, 10, 6, 0.02f, 2, HeroClass.Warrior),
            "法师" => new HeroStatTemplate(70, 12, 3, 8, 0.05f, 2, HeroClass.Mage),
            "刺客" => new HeroStatTemplate(70, 16, 3, 14, 0.12f, 1, HeroClass.Assassin),
            "链甲使者" => new HeroStatTemplate(200, 10, 15, 5, 0.03f, 2, HeroClass.Warrior),
            "狂战士" => new HeroStatTemplate(130, 14, 10, 8, 0.08f, 2, HeroClass.Warrior),
            "大法师" => new HeroStatTemplate(85, 18, 4, 10, 0.08f, 2, HeroClass.Mage),
            "巡游法师" => new HeroStatTemplate(100, 18, 5, 12, 0.12f, 2, HeroClass.Mage),
            "影舞者" => new HeroStatTemplate(85, 22, 4, 18, 0.20f, 1, HeroClass.Assassin),
            _ => new HeroStatTemplate(100, 10, 5, 8, 0.05f, 1, HeroClass.Warrior)
        };
    }

    // ========== 敌人数值模板（JSON驱动 + 难度缩放） ==========

    /// <summary>
    /// 从 enemies.json 读取敌人属性，按关卡难度缩放
    /// </summary>
    public static HeroStatTemplate GetEnemyTemplate(string enemyName, int levelId = 1)
    {
        float diff = GetLevelDifficulty(levelId);

        if (EnemiesCfg?.enemy_types != null)
        {
            // 按name_cn匹配普通敌人
            var entry = EnemiesCfg.enemy_types.Find(e => e.name_cn == enemyName);
            if (entry != null)
            {
                HeroClass cls = entry.role switch
                {
                    "fodder" => HeroClass.Warrior,
                    "ranged" => HeroClass.Assassin,
                    "tank" => HeroClass.Warrior,
                    "elite" => HeroClass.Mage,
                    "suicide" => HeroClass.Warrior,
                    "support" => HeroClass.Mage,
                    _ => HeroClass.Warrior
                };
                var baseTemplate = new HeroStatTemplate(
                    entry.base_stats.max_health,
                    entry.base_stats.attack,
                    entry.base_stats.defense,
                    entry.base_stats.speed,
                    entry.base_stats.crit_rate,
                    0, cls
                );
                return baseTemplate.Scale(diff);
            }
        }

        // 尝试boss_types
        if (EnemiesCfg?.boss_types != null)
        {
            var boss = EnemiesCfg.boss_types.Find(b => b.name_cn == enemyName);
            if (boss != null)
            {
                var baseTemplate = new HeroStatTemplate(
                    boss.base_stats.max_health,
                    boss.base_stats.attack,
                    boss.base_stats.defense,
                    boss.base_stats.speed,
                    boss.base_stats.crit_rate,
                    0, HeroClass.Warrior
                );
                return baseTemplate.Scale(diff);
            }
        }

        // fallback 硬编码
        var fbTemplate = enemyName switch
        {
            "小怪" => new HeroStatTemplate(60, 6, 3, 5, 0f, 0, HeroClass.Warrior),
            "弓手" => new HeroStatTemplate(50, 8, 2, 7, 0.05f, 0, HeroClass.Assassin),
            "重装兵" => new HeroStatTemplate(80, 7, 8, 3, 0f, 0, HeroClass.Warrior),
            "精英" => new HeroStatTemplate(120, 12, 6, 8, 0.05f, 0, HeroClass.Mage),
            "Boss" => new HeroStatTemplate(300, 15, 10, 5, 0.1f, 0, HeroClass.Warrior),
            "巨型Boss" => new HeroStatTemplate(500, 20, 15, 4, 0.15f, 0, HeroClass.Warrior),
            "自爆怪" => new HeroStatTemplate(40, 4, 1, 8, 0f, 0, HeroClass.Warrior),
            "治疗者" => new HeroStatTemplate(50, 5, 2, 4, 0f, 0, HeroClass.Mage),
            "治疗兵" => new HeroStatTemplate(50, 5, 2, 4, 0f, 0, HeroClass.Mage),
            "护盾怪" => new HeroStatTemplate(80, 5, 8, 4, 0f, 0, HeroClass.Warrior),
            "分裂怪" => new HeroStatTemplate(100, 6, 2, 5, 0f, 0, HeroClass.Warrior),
            "隐身怪" => new HeroStatTemplate(60, 10, 2, 12, 0.1f, 0, HeroClass.Assassin),
            "诅咒巫师" => new HeroStatTemplate(70, 8, 4, 6, 0.05f, 0, HeroClass.Mage),
            "重装骑士" => new HeroStatTemplate(180, 10, 12, 4, 0.03f, 0, HeroClass.Warrior),
            "毒液蜘蛛" => new HeroStatTemplate(90, 7, 5, 7, 0.08f, 0, HeroClass.Assassin),
            _ => new HeroStatTemplate(60, 6, 3, 5, 0f, 0, HeroClass.Warrior)
        };
        return fbTemplate.Scale(diff);
    }

    // ========== 星级成长公式（JSON驱动） ==========

    /// <summary>
    /// 星级倍率：从 hero_classes.json 的 star_rating 读取
    /// </summary>
    public static float GetStarMultiplier(int starLevel)
    {
        if (HeroConfig?.star_rating != null)
        {
            string key = starLevel.ToString();
            if (HeroConfig.star_rating.TryGetValue(key, out StarRatingEntry entry))
            {
                return entry.multiplier;
            }
        }
        // fallback
        return starLevel switch
        {
            1 => 1f,
            2 => 1.5f,
            3 => 2.2f,
            _ => 1f + (starLevel - 1) * 0.6f
        };
    }

    // ========== 伤害公式（JSON驱动） ==========

    /// <summary>
    /// 计算伤害：暴击基础倍率从 battle_formulas.json 读取
    /// </summary>
    public static int CalculateDamage(
        int attackerAtk,
        float critRate,
        float critDamage,
        int targetDef,
        float damageMultiplier = 1f)
    {
        bool isCrit = Random.value < critRate;

        // 暴击基础倍率从JSON读取
        float critBase = 1.5f;
        if (FormulasCfg?.crit_formula != null)
        {
            critBase = FormulasCfg.crit_formula.crit_multiplier_base;
        }

        float critMult = isCrit ? (critBase + critDamage) : 1f;
        float rawDamage = attackerAtk * damageMultiplier * critMult;

        int minDmg = 1;
        if (FormulasCfg?.damage_formula != null)
        {
            minDmg = FormulasCfg.damage_formula.min_damage;
        }

        int finalDamage = Mathf.Max(minDmg, Mathf.RoundToInt(rawDamage) - targetDef);
        return finalDamage;
    }

    /// <summary>
    /// 计算治疗量：治疗默认倍率从 battle_formulas.json 读取
    /// </summary>
    public static int CalculateHeal(int baseHeal, float healMultiplier = -1f)
    {
        if (healMultiplier < 0f)
        {
            // 从JSON读取默认治疗倍率
            healMultiplier = 0.5f;
            if (FormulasCfg?.heal_formula != null)
            {
                healMultiplier = FormulasCfg.heal_formula.heal_multiplier_default;
            }
        }
        return Mathf.RoundToInt(baseHeal * healMultiplier);
    }

    // ========== 卡牌数值（保留硬编码，JSON无对应配置） ==========

    public static CardStatTemplate GetCardTemplate(string cardName)
    {
        return cardName switch
        {
            "力量训练" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 3),
            "坚固护甲" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 3),
            "灵敏训练" => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 2),
            "斩击" => new CardStatTemplate(CardType.Battle, CardRarity.White, 1, 50),
            "重摇" => new CardStatTemplate(CardType.Battle, CardRarity.White, 1, 0),
            "护盾冲击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 30),
            "寻找弱点" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "进化觉醒" => new CardStatTemplate(CardType.Evolution, CardRarity.Purple, 3, 0),
            "火焰斩" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 20),
            "冰霜护甲" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 25),
            "疾风步" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 50),
            "神圣祝福" => new CardStatTemplate(CardType.Attribute, CardRarity.Gold, 0, 5),
            "致命一击" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 50),
            "召唤强化" => new CardStatTemplate(CardType.Attribute, CardRarity.Blue, 0, 1),
            "火球术" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 30),
            "连环斩" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 2, 2),
            "吸血攻击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "复活术" => new CardStatTemplate(CardType.Attribute, CardRarity.Gold, 0, 1),
            "毒刃" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 5),
            "能量爆发" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 3, 20),
            "破甲攻击" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 50),
            "群体治疗" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 20),
            "闪电链" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 3),
            "荊棘反伤" => new CardStatTemplate(CardType.Battle, CardRarity.Blue, 1, 30),
            "狂暴药水" => new CardStatTemplate(CardType.Battle, CardRarity.Purple, 2, 80),
            "护盾共振" => new CardStatTemplate(CardType.Battle, CardRarity.Gold, 2, 30),
            _ => new CardStatTemplate(CardType.Attribute, CardRarity.White, 0, 0)
        };
    }

    // ========== 骰子联动倍率（JSON驱动） ==========

    /// <summary>
    /// 获取骰子组合的基础倍率 — 从 skills.json 的 dice_combo_skills 读取
    /// </summary>
    public static float GetComboMultiplier(DiceCombinationType combo)
    {
        if (SkillsCfg?.dice_combo_skills?.skills != null)
        {
            string comboId = combo switch
            {
                DiceCombinationType.ThreeOfAKind => "three_of_a_kind",
                DiceCombinationType.Straight => "straight",
                DiceCombinationType.Pair => "pair",
                _ => null
            };

            if (comboId != null)
            {
                var skill = SkillsCfg.dice_combo_skills.skills.Find(s => s.combo_id == comboId);
                if (skill != null)
                {
                    // 三条用attack_bonus_pct, 对子用damage_multiplier
                    if (skill.attack_bonus_pct > 0)
                        return 1f + skill.attack_bonus_pct;
                    if (skill.damage_multiplier > 0)
                        return skill.damage_multiplier;
                }
            }
        }

        // fallback
        return combo switch
        {
            DiceCombinationType.ThreeOfAKind => 1.5f,
            DiceCombinationType.Straight => 1.67f,
            DiceCombinationType.Pair => 2f,
            _ => 1f
        };
    }

    // ========== 辅助方法 ==========

    /// <summary>
    /// 根据敌人name_cn在enemies.json中查找完整数据
    /// </summary>
    public static EnemyEntry FindEnemyEntry(string enemyName)
    {
        if (EnemiesCfg?.enemy_types == null) return null;
        return EnemiesCfg.enemy_types.Find(e => e.name_cn == enemyName || e.id == enemyName);
    }

    /// <summary>
    /// 根据Boss name_cn在enemies.json中查找Boss数据
    /// </summary>
    public static BossEntry FindBossEntry(string bossName)
    {
        if (EnemiesCfg?.boss_types == null) return null;
        return EnemiesCfg.boss_types.Find(b => b.name_cn == bossName || b.id == bossName);
    }

    /// <summary>
    /// 获取战斗timing配置
    /// </summary>
    public static BattleTimingConfig GetBattleTiming()
    {
        return FormulasCfg?.battle_timing;
    }

    /// <summary>
    /// 获取连携技配置
    /// </summary>
    public static SynergySystemConfig GetSynergyConfig()
    {
        return FormulasCfg?.synergy_system;
    }

    /// <summary>
    /// 获取站位修正配置
    /// </summary>
    public static PositionModifiersConfig GetPositionModifiers()
    {
        return FormulasCfg?.position_modifiers;
    }

    /// <summary>
    /// 获取骰子组合技能配置
    /// </summary>
    public static DiceComboSkillConfig GetDiceComboSkills()
    {
        return SkillsCfg?.dice_combo_skills;
    }

    /// <summary>
    /// 获取英雄技能配置
    /// </summary>
    public static HeroSkillEntry GetHeroSkills(string heroClassId)
    {
        if (SkillsCfg?.hero_skills == null) return null;
        return SkillsCfg.hero_skills.Find(s => s.hero_class == heroClassId);
    }

    /// <summary>
    /// 重载所有配置缓存（BalanceProvider.ReloadAll 调用）
    /// </summary>
    public static void ReloadConfigs()
    {
        _heroConfig = null;
        _enemiesConfig = null;
        _levelsConfig = null;
        _formulasConfig = null;
        _skillsConfig = null;
        _diceConfig = null;
    }
}

/// <summary>
/// 英雄数值模板结构
/// </summary>
public struct HeroStatTemplate
{
    public int Health;
    public int Attack;
    public int Defense;
    public int Speed;
    public float CritRate;
    public int SummonCost;
    public HeroClass HeroClass;

    public HeroStatTemplate(int h, int a, int d, int s, float c, int cost, HeroClass cls)
    {
        Health = h; Attack = a; Defense = d; Speed = s;
        CritRate = c; SummonCost = cost; HeroClass = cls;
    }

    public HeroStatTemplate Scale(float multiplier)
    {
        return new HeroStatTemplate(
            Mathf.RoundToInt(Health * multiplier),
            Mathf.RoundToInt(Attack * multiplier),
            Mathf.RoundToInt(Defense * multiplier),
            Mathf.RoundToInt(Speed * multiplier),
            CritRate, // 暴击率不随难度缩放
            SummonCost,
            HeroClass
        );
    }
}

/// <summary>
/// 卡牌数值模板结构
/// </summary>
public struct CardStatTemplate
{
    public CardType CardType;
    public CardRarity Rarity;
    public int Cost;
    public int EffectValue;

    public CardStatTemplate(CardType type, CardRarity rarity, int cost, int value)
    {
        CardType = type; Rarity = rarity; Cost = cost; EffectValue = value;
    }
}
