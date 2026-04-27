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
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "坦克";
        data.heroClass = HeroClass.Tank;
        data.baseHealth = 150;
        data.baseAttack = 8;
        data.baseDefense = 10;
        data.baseSpeed = 6;
        data.baseCritRate = 0.02f;
        data.summonCost = 2;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateShieldBashSkill();
        data.description = "高防高血，吸收伤害，护盾反弹";
        return data;
    }

    public static HeroData CreateArcherHero()
    {
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "射手";
        data.heroClass = HeroClass.Archer;
        data.baseHealth = 80;
        data.baseAttack = 14;
        data.baseDefense = 4;
        data.baseSpeed = 10;
        data.baseCritRate = 0.08f;
        data.summonCost = 2;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreatePierceShotSkill();
        data.description = "远程输出，越远越痛";
        return data;
    }

    public static HeroData CreateAssassinHero()
    {
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "刺客";
        data.heroClass = HeroClass.Assassin;
        data.baseHealth = 70;
        data.baseAttack = 16;
        data.baseDefense = 3;
        data.baseSpeed = 14;
        data.baseCritRate = 0.12f;
        data.summonCost = 1;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreateBackstabSkill();
        data.description = "高速爆发，闪避背刺";
        return data;
    }

    // ========== 敌人数据模板 ==========

    public static HeroData CreateEnemyGrunt()
    {
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "小怪";
        data.heroClass = HeroClass.Tank;
        data.baseHealth = 60;
        data.baseAttack = 6;
        data.baseDefense = 3;
        data.baseSpeed = 5;
        data.baseCritRate = 0f;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.description = "普通小怪";
        return data;
    }

    public static HeroData CreateEnemyElite()
    {
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "精英";
        data.heroClass = HeroClass.Archer;
        data.baseHealth = 120;
        data.baseAttack = 12;
        data.baseDefense = 6;
        data.baseSpeed = 8;
        data.baseCritRate = 0.05f;
        data.summonCost = 0;
        data.normalAttack = CreateNormalAttack();
        data.activeSkill = CreatePierceShotSkill();
        data.description = "精英敌人";
        return data;
    }

    public static HeroData CreateEnemyBoss()
    {
        var data = ScriptableObject.CreateInstance<HeroData>();
        data.heroName = "Boss";
        data.heroClass = HeroClass.Tank;
        data.baseHealth = 300;
        data.baseAttack = 15;
        data.baseDefense = 10;
        data.baseSpeed = 5;
        data.baseCritRate = 0.1f;
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
        return cards;
    }
}
