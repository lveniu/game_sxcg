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

    // ========== 英雄数据（三职业：战士/法师/刺客） ==========

    public static HeroData CreateAssassinHero()
    {
        return CreateHeroFromTemplate("刺客", "刺客", CreateNormalAttack(), CreateBackstabSkill(), CreateAssassinEvolved(), "高速爆发，闪避背刺");
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
        return CreateHeroFromTemplate("法师", "法师", CreateNormalAttack(), CreateFireballSkill(), CreateMageEvolved(), "远程AOE法术输出");
    }

    public static HeroData CreateWarriorHero()
    {
        return CreateHeroFromTemplate("战士", "战士", CreateNormalAttack(), CreateWhirlwindSkill(), CreateWarriorEvolved(), "近战连击型输出");
    }

    public static HeroData CreateMageEvolved()
    {
        return CreateHeroFromTemplate("大法师", "大法师", CreateNormalAttack(), CreateMeteorSkill(), desc: "元素的终极代言，陨石毁天灭地");
    }

    public static HeroData CreateWarriorEvolved()
    {
        return CreateHeroFromTemplate("狂战士", "狂战士", CreateNormalAttack(), CreateBerserkSkill(), desc: "鲜血与屠杀的化身，越战越勇");
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
}


