using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏默认数据 — MVP所有英雄、技能、卡牌的基础数据
/// 以代码形式存在，不依赖ScriptableObject资源文件
/// </summary>
public static class GameData
{
    static HeroData CreateHeroFromTemplate(string displayName, string templateKey, SkillData normalAtk, SkillData activeSkill = null, HeroData evoForm = null, string desc = "")
    {
        var stats = GameBalance.GetHeroTemplate(templateKey);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = displayName;
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = normalAtk;
        data.activeSkill = activeSkill;
        data.evolutionForm = evoForm;
        data.description = desc;
        return data;
    }

    static HeroData CreateEnemyFromTemplate(string displayName, string templateKey, int levelId, SkillData activeSkill = null, string desc = "")
    {
        var stats = GameBalance.GetEnemyTemplate(templateKey, levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = displayName;
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = activeSkill;
        data.description = desc;
        return data;
    }

    /// <summary>
    /// JSON id → 中文显示名映射（enemies.json 里的 id → GameData 中文名）
    /// </summary>
    static readonly Dictionary<string, string> EnemyIdToNameCn = new Dictionary<string, string>
    {
        {"minion", "小怪"},
        {"ranger", "弓手"},
        {"brute", "重装兵"},
        {"elite", "精英"},
        {"bomber", "自爆怪"},
        {"healer", "治疗兵"},           // 对齐 enemies.json name_cn
        {"shielder", "护盾怪"},
        {"splitter", "分裂怪"},
        {"stealth", "隐身怪"},
        {"curse_mage", "诅咒巫师"},
        {"heavy_knight", "重装骑士"},
        {"venom_spider", "毒液蜘蛛"},
        {"boss_standard", "普通Boss"},   // 对齐 enemies.json name_cn
        {"boss_mega", "巨型Boss"},
    };

    /// <summary>
    /// 根据 JSON id 创建敌人（属性从 enemies.json 读取，技能根据类型匹配）
    /// LevelManager 的新入口，替代各个 CreateEnemy* 硬编码方法
    /// </summary>
    public static HeroData CreateEnemyByJsonId(string jsonId, int levelId = 1)
    {
        if (string.IsNullOrEmpty(jsonId)) return CreateEnemyGrunt(levelId);

        // 1. 获取中文名
        string nameCn = EnemyIdToNameCn.GetValueOrDefault(jsonId, jsonId);

        // 2. 尝试从 enemies.json 获取属性（通过 GameBalance 已实现的 JSON 优先 + fallback）
        var stats = GameBalance.GetEnemyTemplate(nameCn, levelId);

        // 3. 根据 jsonId 匹配技能
        SkillData activeSkill = GetSkillForEnemyType(jsonId);
        string desc = GetDescForEnemyType(jsonId);

        // 4. 创建 HeroData（复用 CreateEnemyFromTemplate 的逻辑）
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = nameCn;
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = activeSkill;
        data.description = desc;
        return data;
    }

    /// <summary>
    /// 根据敌人 JSON id 匹配对应的技能
    /// </summary>
    static SkillData GetSkillForEnemyType(string jsonId)
    {
        return jsonId switch
        {
            "elite" => CreatePierceShotSkill(),
            "boss_standard" => CreateAOESmashSkill(),
            "boss_mega" => CreateAOESmashSkill(),
            "healer" => CreateHealSkill(),
            "curse_mage" => CreateCurseSkill(),
            _ => null // 大部分敌人没有特殊技能，用普攻
        };
    }

    /// <summary>
    /// 根据敌人 JSON id 返回描述
    /// </summary>
    static string GetDescForEnemyType(string jsonId)
    {
        return jsonId switch
        {
            "minion" => "普通小怪",
            "ranger" => "远程弓手",
            "brute" => "高防重装兵",
            "elite" => "精英敌人",
            "bomber" => "死亡时对周围造成高额伤害",
            "healer" => "每回合给友方回血",
            "shielder" => "开场自带护盾",
            "splitter" => "死亡时分裂成2个小怪",
            "stealth" => "每3回合隐身1回合",
            "curse_mage" => "攻击降低目标攻击力，持续2回合",
            "heavy_knight" => "极高防御，每次受击只造成1点伤害",
            "venom_spider" => "攻击附带剧毒，每回合扣血",
            "boss_standard" => "Boss战",
            "boss_mega" => "巨型Boss战",
            _ => ""
        };
    }

    /// <summary>
    /// 随机获取一张奖励卡牌（从奖励池中随机选一张）
    /// </summary>
    public static CardData GetRandomRewardCard()
    {
        var pool = CreateRewardCards();
        if (pool == null || pool.Count == 0) return CreatePowerTrainingCard();

        int idx = UnityEngine.Random.Range(0, pool.Count);
        var instance = pool[idx];
        return instance != null ? instance.Data : CreatePowerTrainingCard();
    }

    static CardData CreateCard(string name, CardType type, CardRarity rarity, CardEffectId effectId, int cost, string desc,
        int effectValue = 0, DiceCombinationType combo = DiceCombinationType.None, float comboMultiplier = 1f)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = name;
        card.cardType = type;
        card.rarity = rarity;
        card.effectId = effectId;
        card.cost = cost;
        card.effectValue = effectValue;
        card.requiredCombo = combo;
        card.comboMultiplier = comboMultiplier;
        card.description = desc;
        return card;
    }

    // ========== 英雄数据（JSON驱动 + fallback） ==========

    /// <summary>
    /// 根据 JSON classId 创建英雄（属性从 hero_classes.json 读取）
    /// 替代各个 CreateXxxHero() 硬编码方法
    /// </summary>
    public static HeroData CreateHeroByJsonId(string classId, int level = 1)
    {
        if (string.IsNullOrEmpty(classId)) return CreateWarriorHero();

        // 1. nameCn映射
        string nameCn = classId switch
        {
            "warrior" => "战士",
            "mage" => "法师", 
            "assassin" => "刺客",
            "chain_knight" => "链甲使者",
            "berserker" => "狂战士",
            "arch_mage" => "大法师",
            "wandering_mage" => "巡游法师",
            "shadow_dancer" => "影舞者",
            _ => classId // fallback直接用传入名
        };

        // 2. 尝试从 BalanceProvider.GetHeroStats() 读取JSON属性
        var stats = BalanceProvider.GetHeroStats(nameCn);

        // 3. 按classId匹配技能
        SkillData activeSkill = classId switch
        {
            "warrior" => CreateWhirlwindSkill(),
            "mage" => CreateFireballSkill(),
            "assassin" => CreateBackstabSkill(),
            "chain_knight" => CreateShieldReflectSkill(),
            "berserker" => CreateBerserkSkill(),
            "arch_mage" => CreateMeteorSkill(),
            "wandering_mage" => CreateFrostNovaSkill(),
            "shadow_dancer" => CreateBackstabSkill(),
            _ => null
        };

        // 4. 查找进化形态
        HeroData evoForm = classId switch
        {
            "warrior" => CreateWarriorEvolved(),
            "mage" => CreateMageEvolved(),
            "assassin" => CreateAssassinEvolved(),
            _ => null
        };

        // 5. 如果JSON读到了属性，用JSON的；否则fallback到旧方法
        if (stats != null && stats.Health > 0)
        {
            var data = ScriptableObject.CreateInstance<HeroData>();
            data.heroName = nameCn;
            data.heroClass = stats.HeroClass;
            data.baseHealth = stats.Health;
            data.baseAttack = stats.Attack;
            data.baseDefense = stats.Defense;
            data.baseSpeed = stats.Speed;
            data.baseCritRate = stats.CritRate;
            data.summonCost = stats.SummonCost;
            data.normalAttack = CreateNormalAttack();
            data.activeSkill = activeSkill;
            data.evolutionForm = evoForm;
            data.description = GetHeroDesc(classId);
            return data;
        }

        // fallback到旧硬编码（直接调用CreateHeroFromTemplate，避免循环调用）
        return classId switch
        {
            "warrior" => CreateHeroFromTemplate("战士", "战士", CreateNormalAttack(), CreateWhirlwindSkill(), CreateWarriorEvolved(), "近战连击型输出"),
            "mage" => CreateHeroFromTemplate("法师", "法师", CreateNormalAttack(), CreateFireballSkill(), CreateMageEvolved(), "远程AOE法术输出"),
            "assassin" => CreateHeroFromTemplate("刺客", "刺客", CreateNormalAttack(), CreateBackstabSkill(), CreateAssassinEvolved(), "高速爆发，闪避背刺"),
            "chain_knight" => CreateHeroFromTemplate("链甲使者", "链甲使者", CreateNormalAttack(), CreateShieldReflectSkill(), desc: "铁壁防御，反弹伤害"),
            "berserker" => CreateHeroFromTemplate("狂战士", "狂战士", CreateNormalAttack(), CreateBerserkSkill(), desc: "鲜血与屠杀的化身，越战越勇"),
            "arch_mage" => CreateHeroFromTemplate("大法师", "大法师", CreateNormalAttack(), CreateMeteorSkill(), desc: "元素的终极代言，陨石毁天灭地"),
            "wandering_mage" => CreateHeroFromTemplate("巡游法师", "巡游法师", CreateNormalAttack(), CreateFrostNovaSkill(), desc: "冰霜新星控场法师"),
            "shadow_dancer" => CreateHeroFromTemplate("影舞者", "影舞者", CreateNormalAttack(), CreateBackstabSkill(), desc: "极速闪避，背刺必暴"),
            _ => CreateHeroFromTemplate("战士", "战士", CreateNormalAttack(), CreateWhirlwindSkill(), CreateWarriorEvolved(), "近战连击型输出")
        };
    }

    static string GetHeroDesc(string classId) => classId switch
    {
        "warrior" => "近战连击型输出",
        "mage" => "远程AOE法术输出",
        "assassin" => "高速爆发，闪避背刺",
        "chain_knight" => "铁壁防御，反弹伤害",
        "berserker" => "鲜血与屠杀的化身，越战越勇",
        "arch_mage" => "元素的终极代言，陨石毁天灭地",
        "wandering_mage" => "冰霜新星控场法师",
        "shadow_dancer" => "极速闪避，背刺必暴",
        _ => ""
    };

    public static HeroData CreateAssassinHero()
    {
        return CreateHeroByJsonId("assassin");
    }

    // 兼容旧调用 — 转发到三职业版本
    public static HeroData CreateTankHero() => CreateWarriorHero();
    public static HeroData CreateArcherHero() => CreateMageHero();

    // ========== 敌人数据模板 ==========

    public static HeroData CreateEnemyGrunt(int levelId = 1)
    {
        return CreateEnemyFromTemplate("小怪", "小怪", levelId, desc: "普通小怪");
    }

    public static HeroData CreateEnemyElite(int levelId = 1)
    {
        return CreateEnemyFromTemplate("精英", "精英", levelId, CreatePierceShotSkill(), "精英敌人");
    }

    public static HeroData CreateEnemyBoss(int levelId = 1)
    {
        return CreateEnemyFromTemplate("Boss", "Boss", levelId, CreateAOESmashSkill(), "Boss战");
    }

    // ========== 技能数据 ==========

    public static SkillData CreateNormalAttack()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "普攻";
        skill.damageMultiplier = 1f;
        skill.cooldown = 0f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "普通攻击";
        return skill;
    }

    public static SkillData CreateShieldBashSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "护盾冲击";
        skill.damageMultiplier = 1.5f;
        skill.cooldown = 4f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.effectValue = 20;
        skill.description = "用护盾冲撞敌人，造成伤害并获得护盾";
        return skill;
    }

    public static SkillData CreatePierceShotSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "穿甲射击";
        skill.damageMultiplier = 2f;
        skill.cooldown = 5f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "射出穿透敌人护甲的一箭";
        return skill;
    }

    public static SkillData CreateBackstabSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "背刺";
        skill.damageMultiplier = 2.5f;
        skill.cooldown = 4f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "潜行到敌人背后造成致命伤害";
        return skill;
    }

    public static SkillData CreateAOESmashSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "震地锤";
        skill.damageMultiplier = 1.2f;
        skill.cooldown = 6f;
        skill.targetType = SkillTargetType.AOE;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "砸向地面，对所有敌人造成伤害";
        return skill;
    }

    // ========== 卡牌数据 ==========

    public static CardData CreatePowerTrainingCard()
    {
        return CreateCard("力量训练", CardType.Attribute, CardRarity.White, CardEffectId.PowerTraining, 0, "本局永久+3攻击", 3);
    }

    public static CardData CreateArmorTrainingCard()
    {
        return CreateCard("坚固护甲", CardType.Attribute, CardRarity.White, CardEffectId.ArmorTraining, 0, "本局永久+3防御", 3);
    }

    public static CardData CreateSpeedTrainingCard()
    {
        return CreateCard("灵敏训练", CardType.Attribute, CardRarity.White, CardEffectId.SpeedTraining, 0, "本局永久+2速度", 2);
    }

    public static CardData CreateSlashCard()
    {
        return CreateCard("斩击", CardType.Battle, CardRarity.White, CardEffectId.Slash, 1, "本场攻击+50%，对子时伤害翻倍", 50, DiceCombinationType.Pair, 2f);
    }

    public static CardData CreateRerollCard()
    {
        return CreateCard("重摇", CardType.Battle, CardRarity.White, CardEffectId.Reroll, 1, "消耗1点，重新掷该骰子");
    }

    public static CardData CreateShieldBashCard()
    {
        return CreateCard("护盾冲击", CardType.Battle, CardRarity.Blue, CardEffectId.ShieldBash, 2, "消耗2点，获得护盾并冲撞，三条时护盾+50%", 30, DiceCombinationType.ThreeOfAKind, 1.5f);
    }

    public static CardData CreateFindWeaknessCard()
    {
        return CreateCard("寻找弱点", CardType.Battle, CardRarity.Blue, CardEffectId.FindWeakness, 1, "本场暴击率+30%，顺子时额外+20%", 30, DiceCombinationType.Straight, 1.67f);
    }

    public static CardData CreateEvolutionAwakenCard()
    {
        return CreateCard("进化觉醒", CardType.Evolution, CardRarity.Purple, CardEffectId.EvolutionAwaken, 3, "消耗3个相同点数，解锁进化形态");
    }

    // ========== 初始卡组 ==========

    public static List<CardInstance> CreateStartingDeck()
    {
        var deck = new List<CardInstance>();
        // 4张属性卡 + 2张战斗卡
        deck.Add(new CardInstance(CreatePowerTrainingCard()));
        deck.Add(new CardInstance(CreateArmorTrainingCard()));
        deck.Add(new CardInstance(CreateSpeedTrainingCard()));
        deck.Add(new CardInstance(CreatePowerTrainingCard()));
        deck.Add(new CardInstance(CreateSlashCard()));
        deck.Add(new CardInstance(CreateRerollCard()));
        return deck;
    }

    public static List<CardInstance> CreateRewardCards()
    {
        var cards = new List<CardInstance>();
        cards.Add(new CardInstance(CreatePowerTrainingCard()));
        cards.Add(new CardInstance(CreateSlashCard()));
        cards.Add(new CardInstance(CreateShieldBashCard()));
        cards.Add(new CardInstance(CreateFindWeaknessCard()));
        cards.Add(new CardInstance(CreateFlameSlashCard()));
        cards.Add(new CardInstance(CreateFrostArmorCard()));
        cards.Add(new CardInstance(CreateWindStepCard()));
        cards.Add(new CardInstance(CreateHolyBlessCard()));
        cards.Add(new CardInstance(CreateFatalBlowCard()));
        cards.Add(new CardInstance(CreateSummonBoostCard()));
        cards.Add(new CardInstance(CreateFireballCard()));
        cards.Add(new CardInstance(CreateChainStrikeCard()));
        cards.Add(new CardInstance(CreateLifeStealCard()));
        cards.Add(new CardInstance(CreatePoisonBladeCard()));
        cards.Add(new CardInstance(CreateEnergyBurstCard()));
        cards.Add(new CardInstance(CreateArmorBreakCard()));
        cards.Add(new CardInstance(CreateGroupHealCard()));
        cards.Add(new CardInstance(CreateLightningChainCard()));
        cards.Add(new CardInstance(CreateThornsCard()));
        cards.Add(new CardInstance(CreateBerserkPotionCard()));
        cards.Add(new CardInstance(CreateShieldResonanceCard()));
        return cards;
    }

    // ========== 扩展卡牌 ==========

    public static CardData CreateFlameSlashCard()
    {
        return CreateCard("火焰斩", CardType.Battle, CardRarity.Blue, CardEffectId.FlameSlash, 2, "本场攻击附加20%火焰伤害，三条时变成范围AOE", 20, DiceCombinationType.ThreeOfAKind, 2f);
    }

    public static CardData CreateFrostArmorCard()
    {
        return CreateCard("冰霜护甲", CardType.Battle, CardRarity.Blue, CardEffectId.FrostArmor, 1, "获得护盾并减速敌人，顺子时护盾翻倍", 25, DiceCombinationType.Straight, 2f);
    }

    public static CardData CreateWindStepCard()
    {
        return CreateCard("疾风步", CardType.Battle, CardRarity.Blue, CardEffectId.WindStep, 1, "本场速度+50%，对子时闪避+20%", 50, DiceCombinationType.Pair, 1.5f);
    }

    public static CardData CreateHolyBlessCard()
    {
        return CreateCard("神圣祝福", CardType.Attribute, CardRarity.Gold, CardEffectId.HolyBless, 0, "本局永久+5生命上限", 5);
    }

    public static CardData CreateFatalBlowCard()
    {
        return CreateCard("致命一击", CardType.Battle, CardRarity.Purple, CardEffectId.FatalBlow, 2, "本场暴击伤害+50%，三条时必暴", 50, DiceCombinationType.ThreeOfAKind, 2f);
    }

    public static CardData CreateSummonBoostCard()
    {
        return CreateCard("召唤强化", CardType.Attribute, CardRarity.Blue, CardEffectId.SummonBoost, 0, "本局永久-1召唤消耗（最低1）", 1);
    }

    // ========== 扩展敌人 ==========

    public static HeroData CreateEnemyBomber(int levelId = 1)
    {
        return CreateEnemyFromTemplate("自爆怪", "自爆怪", levelId, desc: "死亡时对周围造成高额伤害");
    }

    public static HeroData CreateEnemyHealer(int levelId = 1)
    {
        return CreateEnemyFromTemplate("治疗者", "治疗者", levelId, CreateHealSkill(), "每回合给友方回血");
    }

    static SkillData CreateHealSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "治疗之光";
        skill.damageMultiplier = 0f;
        skill.cooldown = 3f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Heal;
        skill.effectValue = 15;
        skill.description = "为友方单位回复生命";
        return skill;
    }

    // ========== 英雄进化形态 ==========

    public static HeroData CreateTankEvolved() => CreateWarriorEvolved();

    public static HeroData CreateArcherEvolved() => CreateMageEvolved();

    public static HeroData CreateAssassinEvolved()
    {
        return CreateHeroFromTemplate("影舞者", "影舞者", CreateNormalAttack(), CreateBackstabSkill(), desc: "极速闪避，背刺必暴");
    }

    static SkillData CreateShieldReflectSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "护盾反弹";
        skill.damageMultiplier = 1f;
        skill.cooldown = 4f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.effectValue = 30; // 反弹30%伤害
        skill.description = "获得护盾，受击时反弹伤害";
        return skill;
    }

    // ========== 再扩展英雄 ==========

    public static HeroData CreateMageHero()
    {
        return CreateHeroByJsonId("mage");
    }

    public static HeroData CreateWarriorHero()
    {
        return CreateHeroByJsonId("warrior");
    }

    public static HeroData CreateMageEvolved()
    {
        return CreateHeroFromTemplate("大法师", "大法师", CreateNormalAttack(), CreateMeteorSkill(), desc: "元素的终极代言，陨石毁天灭地");
    }

    public static HeroData CreateWarriorEvolved()
    {
        return CreateHeroFromTemplate("狂战士", "狂战士", CreateNormalAttack(), CreateBerserkSkill(), desc: "鲜血与屠杀的化身，越战越勇");
    }

    public static HeroData CreateChainKnightHero()
    {
        return CreateHeroFromTemplate("链甲使者", "链甲使者", CreateNormalAttack(), CreateShieldReflectSkill(), desc: "铁壁防御，反弹伤害");
    }

    public static HeroData CreateWanderingMageHero()
    {
        return CreateHeroFromTemplate("巡游法师", "巡游法师", CreateNormalAttack(), CreateFrostNovaSkill(), desc: "冰霜新星控场法师");
    }

    /// <summary>
    /// 根据模板名称创建英雄HeroData（兼容入口）
    /// 所有英雄统一走 CreateHeroByJsonId → BalanceProvider.GetHeroStats
    /// </summary>
    public static HeroData CreateHeroDataByTemplateName(string templateName)
    {
        string jsonId = templateName switch
        {
            "战士" => "warrior",
            "法师" => "mage",
            "刺客" => "assassin",
            "链甲使者" => "chain_knight",
            "狂战士" => "berserker",
            "大法师" => "arch_mage",
            "巡游法师" => "wandering_mage",
            "影舞者" => "shadow_dancer",
            _ => "warrior"
        };
        var hero = CreateHeroByJsonId(jsonId);
        return hero ?? CreateWarriorHero(); // 最终fallback
    }

    static SkillData CreateFireballSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "火球术";
        skill.damageMultiplier = 1.8f;
        skill.cooldown = 5f;
        skill.targetType = SkillTargetType.AOE;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "发射火球对所有敌人造成伤害";
        return skill;
    }

    static SkillData CreateWhirlwindSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "旋风斩";
        skill.damageMultiplier = 1.3f;
        skill.cooldown = 4f;
        skill.targetType = SkillTargetType.AOE;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "旋转攻击周围所有敌人";
        return skill;
    }

    static SkillData CreateMeteorSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "陨石术";
        skill.damageMultiplier = 2.5f;
        skill.cooldown = 6f;
        skill.targetType = SkillTargetType.AOE;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "召唤陨石砸向敌阵，造成巨大AOE伤害";
        return skill;
    }

    static SkillData CreateBerserkSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "狂暴斩";
        skill.damageMultiplier = 2f;
        skill.cooldown = 5f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Damage;
        skill.effectValue = 20; // 吸血20%伤害
        skill.description = "猛烈攻击并吸收伤害的20%为生命";
        return skill;
    }

    static SkillData CreateFrostNovaSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "冰霜新星";
        skill.damageMultiplier = 1.5f;
        skill.cooldown = 5f;
        skill.targetType = SkillTargetType.AOE;
        skill.effectType = SkillEffectType.Damage;
        skill.description = "释放冰霜新星，对所有敌人造成伤害并减速";
        return skill;
    }

    // ========== 再扩展敌人 ==========

    public static HeroData CreateEnemyShielder(int levelId = 1)
    {
        return CreateEnemyFromTemplate("护盾怪", "护盾怪", levelId, desc: "开场自带护盾");
    }

    public static HeroData CreateEnemySplitter(int levelId = 1)
    {
        return CreateEnemyFromTemplate("分裂怪", "分裂怪", levelId, desc: "死亡时分裂成2个小怪");
    }

    public static HeroData CreateEnemyStealth(int levelId = 1)
    {
        return CreateEnemyFromTemplate("隐身怪", "隐身怪", levelId, desc: "每3回合隐身1回合");
    }

    public static HeroData CreateEnemyCurseMage(int levelId = 1)
    {
        return CreateEnemyFromTemplate("诅咒巫师", "诅咒巫师", levelId, CreateCurseSkill(), "攻击降低目标攻击力，持续2回合");
    }

    public static HeroData CreateEnemyHeavyKnight(int levelId = 1)
    {
        return CreateEnemyFromTemplate("重装骑士", "重装骑士", levelId, desc: "极高防御，每次受击只造成1点伤害");
    }

    public static HeroData CreateEnemyVenomSpider(int levelId = 1)
    {
        return CreateEnemyFromTemplate("毒液蜘蛛", "毒液蜘蛛", levelId, desc: "攻击附带剧毒，每回合扣血");
    }

    static SkillData CreateCurseSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        skill.skillName = "减益诅咒";
        skill.damageMultiplier = 0.8f;
        skill.cooldown = 4f;
        skill.targetType = SkillTargetType.Single;
        skill.effectType = SkillEffectType.Debuff;
        skill.effectValue = 20; // 降低20%攻击
        skill.description = "诅咒目标，降低其攻击力";
        return skill;
    }

    // ========== 再扩展卡牌 ==========

    public static CardData CreateFireballCard()
    {
        return CreateCard("火球术", CardType.Battle, CardRarity.Purple, CardEffectId.Fireball, 2, "本场攻击变AOE，三条时伤害+50%", 30, DiceCombinationType.ThreeOfAKind, 1.5f);
    }

    public static CardData CreateChainStrikeCard()
    {
        return CreateCard("连环斩", CardType.Battle, CardRarity.Blue, CardEffectId.ChainStrike, 2, "本场攻击2次，对子时3次", 2, DiceCombinationType.Pair, 2f);
    }

    public static CardData CreateLifeStealCard()
    {
        return CreateCard("吸血攻击", CardType.Battle, CardRarity.Blue, CardEffectId.LifeSteal, 1, "本场攻击造成伤害的30%转化为生命，顺子时50%", 30, DiceCombinationType.Straight, 1.67f);
    }

    public static CardData CreateReviveCard()
    {
        return CreateCard("复活术", CardType.Attribute, CardRarity.Gold, CardEffectId.Revive, 0, "本局永久+1复活次数", 1);
    }

    public static CardData CreatePoisonBladeCard()
    {
        return CreateCard("毒刃", CardType.Battle, CardRarity.Blue, CardEffectId.PoisonBlade, 1, "本场攻击附加中毒，对子时毒害翻倍", 5, DiceCombinationType.Pair, 2f);
    }

    public static CardData CreateEnergyBurstCard()
    {
        return CreateCard("能量爆发", CardType.Battle, CardRarity.Purple, CardEffectId.EnergyBurst, 3, "本场攻击、防御、速度+20%，三条时+30%", 20, DiceCombinationType.ThreeOfAKind, 1.5f);
    }

    // ========== 第四轮新卡牌 ==========

    public static CardData CreateArmorBreakCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "破甲攻击";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.ArmorBreak;
        card.cost = 1;
        card.effectValue = 50; // 降低50%防御
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 2f;
        card.description = "本场攻击降低目标50%防御，对时降为0";
        return card;
    }

    public static CardData CreateGroupHealCard()
    {
        return CreateCard("群体治疗", CardType.Battle, CardRarity.Purple, CardEffectId.GroupHeal, 2, "立即恢复全体友方20%生命，三条时30%", 20, DiceCombinationType.ThreeOfAKind, 1.5f);
    }

    public static CardData CreateLightningChainCard()
    {
        return CreateCard("闪电链", CardType.Battle, CardRarity.Purple, CardEffectId.LightningChain, 2, "攻击弹射到3个目标，顺子时5次", 3, DiceCombinationType.Straight, 2f);
    }

    public static CardData CreateThornsCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "荊棘反伤";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.Thorns;
        card.cost = 1;
        card.effectValue = 30; // 反弩30%
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 2f;
        card.description = "本场受击时反弩30%伤害，对时反弩60%";
        return card;
    }

    public static CardData CreateBerserkPotionCard()
    {
        return CreateCard("狂暴药水", CardType.Battle, CardRarity.Purple, CardEffectId.BerserkPotion, 2, "本场攻击+80%，但防御-30%，三条时攻击+120%", 80, DiceCombinationType.ThreeOfAKind, 1.5f);
    }

    public static CardData CreateShieldResonanceCard()
    {
        return CreateCard("护盾共振", CardType.Battle, CardRarity.Gold, CardEffectId.ShieldResonance, 2, "给全体友方施加30%生命值护盾，三条时60%", 30, DiceCombinationType.ThreeOfAKind, 2f);
    }

    // ========== 存档支持：按名称/ID查找数据 ==========

    /// <summary>根据装备名创建EquipmentData（用于存档恢复）</summary>
    public static EquipmentData CreateEquipmentByName(string equipName)
    {
        // 尝试从Resources加载
        var allEquips = Resources.LoadAll<EquipmentData>("Equipment");
        foreach (var eq in allEquips)
        {
            if (eq.equipmentName == equipName) return Object.Instantiate(eq);
        }
        // fallback: 创建基础装备
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.equipmentName = equipName;
        return data;
    }

    /// <summary>根据卡牌名查找CardData</summary>
    public static CardData GetCardDataByName(string cardName)
    {
        // 先尝试从Resources加载
        var allCards = Resources.LoadAll<CardData>("Cards");
        foreach (var c in allCards)
        {
            if (c.cardName == cardName) return c;
        }
        // fallback: 通过已知卡牌工厂方法匹配
        var candidates = CreateRewardCards();
        foreach (var c in candidates)
        {
            if (c?.Data != null && c.Data.cardName == cardName) return c.Data;
        }
        // 再检查初始卡组
        var starterDeck = CreateStartingDeck();
        foreach (var c in starterDeck)
        {
            if (c?.Data != null && c.Data.cardName == cardName) return c.Data;
        }
        return null;
    }

    /// <summary>根据遗物ID查找RelicData</summary>
    public static RelicData GetRelicDataById(string relicId)
    {
        // 尝试从Resources加载
        var allRelics = Resources.LoadAll<RelicData>("Relics");
        foreach (var r in allRelics)
        {
            if (r.relicId == relicId) return r;
        }
        // fallback: 通过奖励系统查找
        var rewardSystem = RoguelikeGameManager.Instance?.RewardSystem;
        if (rewardSystem != null)
        {
            var data = rewardSystem.GetRelicData(relicId);
            if (data != null) return data;
        }
        return null;
    }
}


