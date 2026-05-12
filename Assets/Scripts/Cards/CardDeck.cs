using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡组管理器 — 管理手牌、场上英雄、合成
/// </summary>
public class CardDeck : MonoBehaviour
{
    public static CardDeck Instance { get; private set; }

    [Header("手牌（当前卡组中已选卡牌）")]
    public List<CardInstance> handCards = new List<CardInstance>();

    [Header("卡池（未加入卡组的卡牌）")]
    public List<CardInstance> cardPool = new List<CardInstance>();

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
    public int ReviveCount { get; private set; }

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

    // ════════════════════════ 卡组编辑：卡组 ↔ 卡池 ════════════════════════

    /// <summary>
    /// 从 PlayerInventory 同步所有卡牌到卡组编辑系统。
    /// 在 handCards 中的视为"已加入卡组"，其余放入 cardPool。
    /// </summary>
    public void SyncFromInventory()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        var deckSet = new HashSet<CardInstance>(handCards);
        cardPool.Clear();
        foreach (var card in inv.Cards)
        {
            if (card != null && !deckSet.Contains(card))
                cardPool.Add(card);
        }
    }

    /// <summary>
    /// 将卡牌从卡池加入卡组（handCards）
    /// </summary>
    public bool AddToDeck(CardInstance card)
    {
        if (card == null || !cardPool.Contains(card)) return false;
        cardPool.Remove(card);
        handCards.Add(card);
        Debug.Log($"加入卡组：{card.CardName}");
        return true;
    }

    /// <summary>
    /// 将卡牌从卡组移回卡池
    /// </summary>
    public bool RemoveFromDeck(CardInstance card)
    {
        if (card == null || !handCards.Contains(card)) return false;
        handCards.Remove(card);
        cardPool.Add(card);
        Debug.Log($"移出卡组：{card.CardName}");
        return true;
    }

    /// <summary>
    /// 重置卡池（清空，用于新局开始）
    /// </summary>
    public void ResetCardPool()
    {
        cardPool.Clear();
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
    /// 检查是否有足够的同名卡进行合成（按cardId/cardName匹配）
    /// 与 TryMergeCards 功能一致，此方法仅做检查不执行
    /// </summary>
    /// <param name="cardId">目标卡牌名称</param>
    /// <returns>是否有足够的同名卡可合成</returns>
    public bool CanMerge(string cardId)
    {
        int count = 0;
        foreach (var card in handCards)
        {
            if (card.CardName == cardId && card.StarLevel < 3)
            {
                count++;
                if (count >= 2) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 消耗材料卡进行合成升级
    /// 消耗2张同名同星卡，生成一张升星后的卡牌
    /// </summary>
    /// <param name="cardId">要合成的卡牌名称</param>
    /// <returns>合成后的卡牌实例；失败返回null</returns>
    public CardInstance MergeCards(string cardId)
    {
        // 查找所有同名同星的非进化卡
        var candidates = new List<CardInstance>();
        int minStar = int.MaxValue;

        // 找到最低星级的同名卡
        foreach (var card in handCards)
        {
            if (card.CardName == cardId && card.StarLevel < 3 && card.Data.cardType != CardType.Evolution)
            {
                if (card.StarLevel < minStar) minStar = card.StarLevel;
            }
        }

        if (minStar == int.MaxValue) return null;

        // 收集最低星级的同名卡
        foreach (var card in handCards)
        {
            if (card.CardName == cardId && card.StarLevel == minStar && card.Data.cardType != CardType.Evolution)
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count < 2) return null;

        // 检查金币消耗（如果有upgradeCost）
        int upgradeCost = candidates[0].Data.upgradeCost;
        if (upgradeCost > 0)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("PlayerInventory 不存在，无法扣除金币");
                return null;
            }
            if (inventory.Gold < upgradeCost)
            {
                Debug.LogWarning($"金币不足：需要 {upgradeCost}，当前 {inventory.Gold}");
                return null;
            }
            inventory.SpendGold(upgradeCost);
        }

        // 移除两张材料卡
        var card1 = candidates[0];
        var card2 = candidates[1];
        handCards.Remove(card1);
        handCards.Remove(card2);

        // 创建升星后的卡牌
        var merged = new CardInstance(card1.Data);
        merged.StarLevel = card1.StarLevel;
        merged.Merge(); // 升星

        // 继承原卡等级（取较高者）
        // merged.Level 默认为1，此处保留原始等级

        handCards.Add(merged);
        Debug.Log($"合成成功：{cardId} {minStar}星 → {merged.StarLevel}星" +
                  (upgradeCost > 0 ? $"（花费{upgradeCost}金币）" : ""));
        return merged;
    }

    /// <summary>
    /// 检查指定卡牌是否可以通过消耗材料卡升级（等级提升）
    /// </summary>
    /// <param name="card">目标卡牌实例</param>
    /// <returns>是否可以升级</returns>
    public bool CanUpgradeCard(CardInstance card)
    {
        if (card == null) return false;
        return card.CanUpgrade(handCards);
    }

    /// <summary>
    /// 执行卡牌升级（消耗材料卡提升等级）
    /// </summary>
    /// <param name="card">目标卡牌实例</param>
    /// <returns>升级是否成功</returns>
    public bool UpgradeCard(CardInstance card)
    {
        if (card == null) return false;
        if (!card.CanUpgrade(handCards)) return false;

        // 消耗2张材料卡
        string materialName = card.Data.upgradeFrom;
        int consumed = 0;
        var toRemove = new List<CardInstance>();

        foreach (var c in handCards)
        {
            if (c != card && c.Data.cardName == materialName && consumed < 2)
            {
                toRemove.Add(c);
                consumed++;
            }
        }

        if (consumed < 2) return false;

        // 移除材料卡
        foreach (var c in toRemove)
        {
            handCards.Remove(c);
        }

        // 执行升级
        return card.Upgrade();
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
            case CardEffectId.Revive:
                ReviveCount += card.Data.effectValue;
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
    /// 对场上所有英雄执行操作（跳过null）
    /// </summary>
    private void ApplyToAllHeroes(System.Action<Hero> action)
    {
        foreach (var hero in fieldHeroes)
        {
            if (hero != null) action(hero);
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
                ApplyToAllHeroes(h => h.BattleAttack = Mathf.RoundToInt(h.Attack * (1 + card.Data.effectValue / 100f * multiplier)));
                break;

            case CardEffectId.ShieldBash:
                int shieldAmount = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                ApplyToAllHeroes(h => h.AddShield(shieldAmount));
                break;

            case CardEffectId.FindWeakness:
                ApplyToAllHeroes(h => h.BattleCritRate += card.Data.effectValue / 100f * multiplier);
                break;

            case CardEffectId.FlameSlash:
                float flameBonus = card.Data.effectValue / 100f * multiplier;
                ApplyToAllHeroes(h =>
                {
                    h.BattleAttack = Mathf.RoundToInt(h.Attack * (1 + flameBonus));
                    h.HasFlameAOE = hasCombo;
                });
                break;

            case CardEffectId.FrostArmor:
                int frostShield = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                ApplyToAllHeroes(h =>
                {
                    h.AddShield(frostShield);
                    h.HasFrostSlow = true;
                });
                break;

            case CardEffectId.WindStep:
                ApplyToAllHeroes(h =>
                {
                    h.BattleSpeed = Mathf.RoundToInt(h.Speed * (1 + card.Data.effectValue / 100f));
                    if (hasCombo) h.BattleDodgeRate += 0.2f;
                });
                break;

            case CardEffectId.FatalBlow:
                ApplyToAllHeroes(h =>
                {
                    h.BattleCritDamage += card.Data.effectValue / 100f;
                    if (hasCombo) h.BattleCritRate = 1f;
                });
                break;

            case CardEffectId.Fireball:
                ApplyToAllHeroes(h =>
                {
                    h.HasFlameAOE = true;
                    h.BattleAttack = Mathf.RoundToInt(h.Attack * (1 + card.Data.effectValue / 100f * multiplier));
                });
                break;

            case CardEffectId.ChainStrike:
                ApplyToAllHeroes(h => h.ChainStrikeCount = hasCombo ? 3 : card.Data.effectValue);
                break;

            case CardEffectId.LifeSteal:
                ApplyToAllHeroes(h => h.LifeStealRate = card.Data.effectValue / 100f * multiplier);
                break;

            case CardEffectId.PoisonBlade:
                ApplyToAllHeroes(h =>
                {
                    h.PoisonDamage = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                    h.HasPoisonBlade = true;
                });
                break;

            case CardEffectId.EnergyBurst:
                float burstBonus = card.Data.effectValue / 100f * multiplier;
                ApplyToAllHeroes(h =>
                {
                    h.BattleAttack = Mathf.RoundToInt(h.Attack * (1 + burstBonus));
                    h.BattleDefense = Mathf.RoundToInt(h.Defense * (1 + burstBonus));
                    h.BattleSpeed = Mathf.RoundToInt(h.Speed * (1 + burstBonus));
                });
                break;

            case CardEffectId.ArmorBreak:
                ApplyToAllHeroes(h => h.HasArmorBreak = true);
                break;

            case CardEffectId.GroupHeal:
                int healPercent = Mathf.RoundToInt(card.Data.effectValue * multiplier);
                ApplyToAllHeroes(h => h.Heal(Mathf.RoundToInt(h.MaxHealth * healPercent / 100f)));
                break;

            case CardEffectId.LightningChain:
                ApplyToAllHeroes(h => h.LightningChainBounces = hasCombo ? 5 : card.Data.effectValue);
                break;

            case CardEffectId.Thorns:
                ApplyToAllHeroes(h => h.BattleThornsRate = card.Data.effectValue / 100f * multiplier);
                break;

            case CardEffectId.BerserkPotion:
                float berserkAtkBonus = card.Data.effectValue / 100f * multiplier;
                ApplyToAllHeroes(h =>
                {
                    h.BattleAttack = Mathf.RoundToInt(h.Attack * (1 + berserkAtkBonus));
                    h.BattleDefense = Mathf.RoundToInt(h.Defense * 0.7f);
                    h.HasBerserk = true;
                });
                break;

            case CardEffectId.ShieldResonance:
                ApplyToAllHeroes(h => h.AddShield(Mathf.RoundToInt(h.MaxHealth * card.Data.effectValue / 100f * multiplier)));
                break;

            case CardEffectId.Reroll:
                // 重摇卡由 DiceRoller 处理
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
        ApplyToAllHeroes(h => h.ApplyDiceCombination(combo));
    }

    /// <summary>
    /// 应用站位效果到所有场上英雄
    /// </summary>
    public void ApplyPositioningToField(GridManager grid)
    {
        ApplyToAllHeroes(h => h.ApplyRowEffect(grid.GetRow(h.GridPosition)));
    }

    /// <summary>
    /// 重置局内状态（每局开始时）
    /// </summary>
    public void ResetForNewGame()
    {
        handCards.Clear();
        cardPool.Clear();
        ClearField();
        BonusAttack = 0;
        BonusDefense = 0;
        BonusSpeed = 0;
        BonusMaxHealth = 0;
        SummonCostReduction = 0;
        ReviveCount = 0;
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
