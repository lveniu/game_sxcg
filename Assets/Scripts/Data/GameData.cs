using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏默认数据 — MVP所有英雄、技能、卡牌的基础数据
/// 以代码形式存在，不依赖ScriptableObject资源文件
/// </summary>
public static class GameData
{
    // ========== 英雄数据 ==========

    public static HeroData CreateTankHero()
    {
        var stats = GameBalance.GetHeroTemplate("坦克");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "坦克";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateShieldBashSkill();
        data.evolutionForm = CreateTankEvolved();
        data.description = "高防高血，吸收伤害，护盾反弹";
        return data;
    }

    public static HeroData CreateArcherHero()
    {
        var stats = GameBalance.GetHeroTemplate("射手");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "射手";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreatePierceShotSkill();
        data.evolutionForm = CreateArcherEvolved();
        data.description = "远程输出，越远越痛";
        return data;
    }

    public static HeroData CreateAssassinHero()
    {
        var stats = GameBalance.GetHeroTemplate("刺客");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "刺客";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateBackstabSkill();
        data.evolutionForm = CreateAssassinEvolved();
        data.description = "高速爆发，闪避背刺";
        return data;
    }

    // ========== 敌人数据模板 ==========

    public static HeroData CreateEnemyGrunt(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("小怪", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "小怪";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "普通小怪";
        return data;
    }

    public static HeroData CreateEnemyElite(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("精英", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "精英";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreatePierceShotSkill();
        data.description = "精英敌人";
        return data;
    }

    public static HeroData CreateEnemyBoss(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("Boss", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "Boss";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateAOESmashSkill();
        data.description = "Boss战";
        return data;
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
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "力量训练";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.White;
        card.effectId = CardEffectId.PowerTraining;
        card.cost = 0;
        card.effectValue = 3;
        card.description = "本局永久+3攻击";
        return card;
    }

    public static CardData CreateArmorTrainingCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "坚固护甲";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.White;
        card.effectId = CardEffectId.ArmorTraining;
        card.cost = 0;
        card.effectValue = 3;
        card.description = "本局永久+3防御";
        return card;
    }

    public static CardData CreateSpeedTrainingCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "灵敏训练";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.White;
        card.effectId = CardEffectId.SpeedTraining;
        card.cost = 0;
        card.effectValue = 2;
        card.description = "本局永久+2速度";
        return card;
    }

    public static CardData CreateSlashCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "斩击";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.White;
        card.effectId = CardEffectId.Slash;
        card.cost = 1;
        card.effectValue = 50; // +50%攻击
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 2f; // 对子时翻倍
        card.description = "本场攻击+50%，对子时伤害翻倍";
        return card;
    }

    public static CardData CreateRerollCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "重摇";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.White;
        card.effectId = CardEffectId.Reroll;
        card.cost = 1;
        card.description = "消耗1点，重新掷该骰子";
        return card;
    }

    public static CardData CreateShieldBashCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "护盾冲击";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.ShieldBash;
        card.cost = 2;
        card.effectValue = 30;
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 1.5f;
        card.description = "消耗2点，获得护盾并冲撞，三条时护盾+50%";
        return card;
    }

    public static CardData CreateFindWeaknessCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "寻找弱点";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.FindWeakness;
        card.cost = 1;
        card.effectValue = 30;
        card.requiredCombo = DiceCombinationType.Straight;
        card.comboMultiplier = 1.67f; // 顺子时额外+20%，总共+50%
        card.description = "本场暴击率+30%，顺子时额外+20%";
        return card;
    }

    public static CardData CreateEvolutionAwakenCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "进化觉醒";
        card.cardType = CardType.Evolution;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.EvolutionAwaken;
        card.cost = 3;
        card.description = "消耗3个相同点数，解锁进化形态";
        return card;
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
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "火焰斩";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.FlameSlash;
        card.cost = 2;
        card.effectValue = 20; // +20%火焰伤害
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 2f; // 三条时变AOE
        card.description = "本场攻击附加20%火焰伤害，三条时变成范围AOE";
        return card;
    }

    public static CardData CreateFrostArmorCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "冰霜护甲";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.FrostArmor;
        card.cost = 1;
        card.effectValue = 25;
        card.requiredCombo = DiceCombinationType.Straight;
        card.comboMultiplier = 2f;
        card.description = "获得护盾并减速敌人，顺子时护盾翻倍";
        return card;
    }

    public static CardData CreateWindStepCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "疾风步";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.WindStep;
        card.cost = 1;
        card.effectValue = 50;
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 1.5f;
        card.description = "本场速度+50%，对子时闪避+20%";
        return card;
    }

    public static CardData CreateHolyBlessCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "神圣祝福";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.Gold;
        card.effectId = CardEffectId.HolyBless;
        card.cost = 0;
        card.effectValue = 5;
        card.description = "本局永久+5生命上限";
        return card;
    }

    public static CardData CreateFatalBlowCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "致命一击";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.FatalBlow;
        card.cost = 2;
        card.effectValue = 50;
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 2f; // 三条时必暴
        card.description = "本场暴击伤害+50%，三条时必暴";
        return card;
    }

    public static CardData CreateSummonBoostCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "召唤强化";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.SummonBoost;
        card.cost = 0;
        card.effectValue = 1;
        card.description = "本局永久-1召唤消耗（最低1）";
        return card;
    }

    // ========== 扩展敌人 ==========

    public static HeroData CreateEnemyBomber(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("自爆怪", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "自爆怪";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "死亡时对周围造成高额伤害";
        return data;
    }

    public static HeroData CreateEnemyHealer(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("治疗者", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "治疗者";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateHealSkill();
        data.description = "每回合给友方回血";
        return data;
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

    public static HeroData CreateTankEvolved()
    {
        var stats = GameBalance.GetHeroTemplate("链甲使者");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "链甲使者";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateShieldReflectSkill();
        data.description = "极致防御，护盾反弹伤害";
        return data;
    }

    public static HeroData CreateArcherEvolved()
    {
        var stats = GameBalance.GetHeroTemplate("巡游射手");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "巡游射手";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreatePierceShotSkill();
        data.description = "超远射程，穿透敌阵";
        return data;
    }

    public static HeroData CreateAssassinEvolved()
    {
        var stats = GameBalance.GetHeroTemplate("影舞者");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "影舞者";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateBackstabSkill();
        data.description = "极速闪避，背刺必暴";
        return data;
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
        var stats = GameBalance.GetHeroTemplate("法师");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "法师";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateFireballSkill();
        data.evolutionForm = CreateMageEvolved();
        data.description = "远程AOE法术输出";
        return data;
    }

    public static HeroData CreateWarriorHero()
    {
        var stats = GameBalance.GetHeroTemplate("战士");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "战士";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateWhirlwindSkill();
        data.evolutionForm = CreateWarriorEvolved();
        data.description = "近战连击型输出";
        return data;
    }

    public static HeroData CreateMageEvolved()
    {
        var stats = GameBalance.GetHeroTemplate("大法师");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "大法师";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateMeteorSkill();
        data.description = "元素的终极代言，陨石毁天灭地";
        return data;
    }

    public static HeroData CreateWarriorEvolved()
    {
        var stats = GameBalance.GetHeroTemplate("狂战士");
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "狂战士";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = stats.SummonCost;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateBerserkSkill();
        data.description = "鲜血与屠杀的化身，越战越勇";
        return data;
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

    // ========== 再扩展敌人 ==========

    public static HeroData CreateEnemyShielder(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("护盾怪", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "护盾怪";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "开场自带护盾";
        return data;
    }

    public static HeroData CreateEnemySplitter(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("分裂怪", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "分裂怪";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "死亡时分裂成2个小怪";
        return data;
    }

    public static HeroData CreateEnemyStealth(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("隐身怪", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "隐身怪";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "每3回合隐身1回合";
        return data;
    }

    public static HeroData CreateEnemyCurseMage(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("诅咒巫师", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "诅咒巫师";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateCurseSkill();
        data.description = "攻击降低目标攻击力，持续2回合";
        return data;
    }

    public static HeroData CreateEnemyHeavyKnight(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("重装骑士", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "重装骑士";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "极高防御，每次受击只造成1点伤害";
        return data;
    }

    public static HeroData CreateEnemyVenomSpider(int levelId = 1)
    {
        var stats = GameBalance.GetEnemyTemplate("毒液蜘蛛", levelId);
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "毒液蜘蛛";
        data.heroClass = stats.HeroClass;
        data.baseHealth = stats.Health;
        data.baseAttack = stats.Attack;
        data.baseDefense = stats.Defense;
        data.baseSpeed = stats.Speed;
        data.baseCritRate = stats.CritRate;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "攻击附带剧毒，每回合扣血";
        return data;
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
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "火球术";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.Fireball;
        card.cost = 2;
        card.effectValue = 30;
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 1.5f;
        card.description = "本场攻击变AOE，三条时伤害+50%";
        return card;
    }

    public static CardData CreateChainStrikeCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "连环斩";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.ChainStrike;
        card.cost = 2;
        card.effectValue = 2; // 攻击2次
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 2f; // 对子时3次
        card.description = "本场攻击2次，对子时3次";
        return card;
    }

    public static CardData CreateLifeStealCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "吸血攻击";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.LifeSteal;
        card.cost = 1;
        card.effectValue = 30; // 30%吸血
        card.requiredCombo = DiceCombinationType.Straight;
        card.comboMultiplier = 1.67f;
        card.description = "本场攻击造成伤害的30%转化为生命，顺子时50%";
        return card;
    }

    public static CardData CreateReviveCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "复活术";
        card.cardType = CardType.Attribute;
        card.rarity = CardRarity.Gold;
        card.effectId = CardEffectId.Revive;
        card.cost = 0;
        card.effectValue = 1;
        card.description = "本局永久+1复活次数";
        return card;
    }

    public static CardData CreatePoisonBladeCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "毒刃";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Blue;
        card.effectId = CardEffectId.PoisonBlade;
        card.cost = 1;
        card.effectValue = 5; // 每回合5点
        card.requiredCombo = DiceCombinationType.Pair;
        card.comboMultiplier = 2f;
        card.description = "本场攻击附加中毒，对子时毒害翻倍";
        return card;
    }

    public static CardData CreateEnergyBurstCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "能量爆发";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.EnergyBurst;
        card.cost = 3;
        card.effectValue = 20; // 全属性+20%
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 1.5f;
        card.description = "本场攻击、防御、速度+20%，三条时+30%";
        return card;
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
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "群体治疗";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.GroupHeal;
        card.cost = 2;
        card.effectValue = 20; // 每人恢复20%生命
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 1.5f;
        card.description = "立即恢复全体友方20%生命，三条时30%";
        return card;
    }

    public static CardData CreateLightningChainCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "闪电链";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.LightningChain;
        card.cost = 2;
        card.effectValue = 3; // 弹射3次
        card.requiredCombo = DiceCombinationType.Straight;
        card.comboMultiplier = 2f;
        card.description = "攻击弹射到3个目标，顺子时5次";
        return card;
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
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "狂暴药水";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Purple;
        card.effectId = CardEffectId.BerserkPotion;
        card.cost = 2;
        card.effectValue = 80; // 攻击+80%
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 1.5f;
        card.description = "本场攻击+80%，但防御-30%，三条时攻击+120%";
        return card;
    }

    public static CardData CreateShieldResonanceCard()
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = "护盾共振";
        card.cardType = CardType.Battle;
        card.rarity = CardRarity.Gold;
        card.effectId = CardEffectId.ShieldResonance;
        card.cost = 2;
        card.effectValue = 30; // 30%生命值护盾
        card.requiredCombo = DiceCombinationType.ThreeOfAKind;
        card.comboMultiplier = 2f;
        card.description = "给全体友方施加30%生命值护盾，三条时60%";
        return card;
    }
}


