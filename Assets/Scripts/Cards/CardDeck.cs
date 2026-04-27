using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡组管理器 — 管理手牌、场上英雄、合成
/// </summary>
public class CardDeck : MonoBehaviour
{
    public static CardDeck Instance { get; private set; }

    [Header("手牌")]
    public List<CardInstance> handCards = new List<CardInstance>();

    [Header("场上英雄")]
    public List<Hero> fieldHeroes = new List<Hero>();

    [Header("人口上限")]
    public int maxPopulation = 3;

    public int CurrentPopulation => fieldHeroes.Count;
    public bool HasSpace => CurrentPopulation < maxPopulation;

    // 属性累积（本局永久）
    public int BonusAttack { get; private set; }
    public int BonusDefense { get; private set; }
    public int BonusSpeed { get; private set; }
    public int BonusMaxHealth { get; private set; }
    public int SummonCostReduction { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 添加卡牌到手牌
    /// </summary>
    public void AddCard(CardInstance card)
    {
        if (card == null) return;
        handCards.Add(card);
    }

    /// <summary>
    /// 移除手牌
    /// </summary>
    public void RemoveCard(CardInstance card)
    {
        handCards.Remove(card);
    }

    /// <summary>
    /// 召唤英雄上场
    /// </summary>
    public Hero SummonHero(HeroData heroData, int starLevel = 1)
    {
        if (!HasSpace)
        {
            Debug.LogWarning("人口已满，无法召唤更多英雄");
            return null;
        }

        int actualCost = Mathf.Max(1, heroData.summonCost - SummonCostReduction);

        var go = new GameObject($"Hero_{heroData.heroName}");
        var hero = go.AddComponent<Hero>();
        hero.Initialize(heroData, starLevel);

        // 应用属性累积
        hero.Attack += BonusAttack;
        hero.Defense += BonusDefense;
        hero.Speed += BonusSpeed;
        hero.MaxHealth += BonusMaxHealth;
        hero.CurrentHealth = hero.MaxHealth;

        fieldHeroes.Add(hero);
        Debug.Log($"召唤英雄：{heroData.heroName}，成本：{actualCost} (原价：{heroData.summonCost})");
        return hero;
    }

    /// <summary>
    /// 下场英雄
    /// </summary>
    public bool RemoveHero(Hero hero)
    {
        if (!fieldHeroes.Contains(hero)) return false;
        fieldHeroes.Remove(hero);
        if (hero != null) Destroy(hero.gameObject);
        return true;
    }

    /// <summary>
    /// 下场所有英雄
    /// </summary>
    public void ClearField()
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero != null) Destroy(hero.gameObject);
        }
        fieldHeroes.Clear();
    }

    /// <summary>
    /// 查找可合成的同名同星卡牌
    /// </summary>
    public List<CardInstance> FindMergeableCards(string cardName, int starLevel)
    {
        var result = new List<CardInstance>();
        foreach (var card in handCards)
        {
            if (card.CardName == cardName && card.StarLevel == starLevel)
            {
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// 执行2合1合成
    /// </summary>
    public bool TryMergeCards(string cardName, int starLevel)
    {
        var matches = FindMergeableCards(cardName, starLevel);
        if (matches.Count < 2) return false;

        // 移除两张，创建升星后的卡牌
        var card1 = matches[0];
        var card2 = matches[1];

        handCards.Remove(card1);
        handCards.Remove(card2);

        var merged = new CardInstance(card1.Data);
        merged.StarLevel = card1.StarLevel;
        merged.Merge(); // 升星

        handCards.Add(merged);
        Debug.Log($"合成成功：{cardName} {starLevel}星 → {merged.StarLevel}星");
        return true;
    }

    /// <summary>
    /// 打出属性卡
    /// </summary>
    public void PlayAttributeCard(CardInstance card)
    {
        if (card.Data.cardType != CardType.Attribute) return;

        switch (card.Data.effectId)
        {
            case CardEffectId.PowerTraining:
                BonusAttack += card.Data.effectValue;
                ApplyAttributeToField(attack: card.Data.effectValue);
                break;
            case CardEffectId.ArmorTraining:
                BonusDefense += card.Data.effectValue;
                ApplyAttributeToField(defense: card.Data.effectValue);
                break;
            case CardEffectId.SpeedTraining:
                BonusSpeed += card.Data.effectValue;
                ApplyAttributeToField(speed: card.Data.effectValue);
                break;
            case CardEffectId.HolyBless:
                BonusMaxHealth += card.Data.effectValue;
                ApplyAttributeToField(maxHealth: card.Data.effectValue);
                break;
            case CardEffectId.SummonBoost:
                SummonCostReduction += card.Data.effectValue;
                break;
        }

        handCards.Remove(card);
        Debug.Log($"打出属性卡：{card.CardName}"
        );
    }

    /// <summary>
    /// 将属性加成应用到场上所有英雄
    /// </summary>
    private void ApplyAttributeToField(int attack = 0, int defense = 0, int speed = 0, int maxHealth = 0)
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero == null) continue;
            if (attack != 0) hero.Attack += attack;
            if (defense != 0) hero.Defense += defense;
            if (speed != 0) hero.Speed += speed;
            if (maxHealth != 0)
            {
                hero.MaxHealth += maxHealth;
                hero.CurrentHealth += maxHealth;
            }
        }
    }

    /// <summary>
    /// 打出战斗卡（返回是否触发联动）
    /// </summary>
    public bool PlayBattleCard(CardInstance card, DiceCombination combo)
    {
        if (card.Data.cardType != CardType.Battle) return false;

        bool hasCombo = card.HasComboBonus(combo);
        float multiplier = hasCombo ? card.Data.comboMultiplier : 1f;

        switch (card.Data.effectId)
        {
            case CardEffectId.Slash:
                // 斩击：本场攻击+50%，对子时翻倍
                foreach (var hero in fieldHeroes)
                    hero.BattleAttack = Mathf.RoundToInt(hero.Attack * (1 + card.Data.effectValue / 100f * multiplier));
                break;

            case CardEffectId.ShieldBash:
                // 护盾冲击：获得护盾，三条时护盾+50%
                int shieldAmount = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                foreach (var hero in fieldHeroes)
                    hero.AddShield(shieldAmount);
                break;

            case CardEffectId.FindWeakness:
                // 寻找弱点：暴击率+30%，顺子时额外+20%
                foreach (var hero in fieldHeroes)
                    hero.BattleCritRate += (card.Data.effectValue / 100f * multiplier);
                break;

            case CardEffectId.FlameSlash:
                // 火焰斩：本场攻击附加20%火焰伤害，三条时变AOE
                float flameBonus = card.Data.effectValue / 100f * multiplier;
                foreach (var hero in fieldHeroes)
                {
                    hero.BattleAttack = Mathf.RoundToInt(hero.Attack * (1 + flameBonus));
                    hero.HasFlameAOE = hasCombo; // 三条时AOE
                }
                break;

            case CardEffectId.FrostArmor:
                // 冰霜护甲：获得护盾并减速，顺子时护盾翻倍
                int frostShield = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                foreach (var hero in fieldHeroes)
                {
                    hero.AddShield(frostShield);
                    hero.HasFrostSlow = true;
                }
                break;

            case CardEffectId.WindStep:
                // 疾风步：本场速度+50%，对子时闪避+20%
                foreach (var hero in fieldHeroes)
                {
                    hero.BattleSpeed = Mathf.RoundToInt(hero.Speed * (1 + card.Data.effectValue / 100f));
                    if (hasCombo) hero.BattleDodgeRate += 0.2f;
                }
                break;

            case CardEffectId.FatalBlow:
                // 致命一击：暴击伤害+50%，三条时必暴
                foreach (var hero in fieldHeroes)
                {
                    hero.BattleCritDamage += card.Data.effectValue / 100f;
                    if (hasCombo) hero.BattleCritRate = 1f; // 必暴
                }
                break;

            case CardEffectId.Reroll:
                // 重摇卡由 DiceRoller 处理，这里不处理
                break;
        }

        handCards.Remove(card);
        Debug.Log($"打出战斗卡：{card.CardName} {(hasCombo ? "[骰子联动触发]" : "")}");
        return hasCombo;
    }

    /// <summary>
    /// 打出进化卡
    /// </summary>
    public bool PlayEvolutionCard(CardInstance card, Hero targetHero)
    {
        if (card.Data.cardType != CardType.Evolution) return false;
        if (targetHero == null || targetHero.IsEvolved) return false;

        targetHero.Evolve();
        handCards.Remove(card);
        Debug.Log($"英雄进化：{targetHero.Data.heroName} → {targetHero.Data.heroName}");
        return true;
    }

    /// <summary>
    /// 应用骰子组合效果到所有场上英雄
    /// </summary>
    public void ApplyDiceCombinationToField(DiceCombination combo)
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero == null) continue;
            hero.ApplyDiceCombination(combo);
        }
    }

    /// <summary>
    /// 应用站位效果到所有场上英雄
    /// </summary>
    public void ApplyPositioningToField(GridManager grid)
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero == null) continue;
            var row = grid.GetRow(hero.GridPosition);
            hero.ApplyRowEffect(row);
        }
    }

    /// <summary>
    /// 重置局内状态（每局开始时）
    /// </summary>
    public void ResetForNewGame()
    {
        handCards.Clear();
        ClearField();
        BonusAttack = 0;
        BonusDefense = 0;
        BonusSpeed = 0;
        BonusMaxHealth = 0;
        SummonCostReduction = 0;
    }

    /// <summary>
    /// 重置战斗属性（每场战斗前）
    /// </summary>
    public void ResetForNewBattle()
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero == null) continue;
            hero.ResetBattleStats();
            hero.CurrentHealth = hero.MaxHealth; // 每场回满血
        }
    }
}
