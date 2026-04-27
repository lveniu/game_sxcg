using UnityEngine;

/// <summary>
/// 运行时英雄单位 — 占场上的实体
/// </summary>
public class Hero : MonoBehaviour
{
    public HeroData Data { get; private set; }
    public int StarLevel { get; private set; } = 1;
    public bool IsEvolved { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    // 装备
    public System.Collections.Generic.Dictionary<EquipmentSlot, EquipmentData> EquippedItems { get; private set; } = new();

    // 基础属性（受星级影响）
    public int MaxHealth { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Speed { get; private set; }
    public float CritRate { get; private set; }

    // 当前状态
    public int CurrentHealth { get; private set; }

    // 战斗中属性（受骰子组合 + Buff 影响）
    public int BattleAttack { get; set; }
    public float BattleAttackSpeed { get; set; } = 1f;
    public float BattleCritRate { get; set; }
    public int BattleDefense { get; set; }
    public int BattleSpeed { get; set; }
    public float BattleDodgeRate { get; set; }
    public float BattleCritDamage { get; set; } = 0.5f; // 暴击伤害加成 (50%)

    // 卡牌特殊状态
    public bool HasFlameAOE { get; set; }
    public bool HasFrostSlow { get; set; }
    public bool HasPoisonBlade { get; set; }
    public int ChainStrikeCount { get; set; }
    public float LifeStealRate { get; set; }
    public int PoisonDamage { get; set; }
    public float BattleThornsRate { get; set; }
    public bool HasArmorBreak { get; set; }
    public int LightningChainBounces { get; set; }
    public bool HasBerserk { get; set; }

    // 棋盘位置
    public Vector2Int GridPosition { get; set; }
    public GridRow CurrentRow { get; set; }

    public void Initialize(HeroData data, int starLevel = 1)
    {
        Data = data;
        StarLevel = Mathf.Clamp(starLevel, 1, 3);
        RecalculateStats();
        CurrentHealth = MaxHealth;
    }

    /// <summary>
    /// 根据星级和装备重新计算属性
    /// </summary>
    public void RecalculateStats()
    {
        float multiplier = GameBalance.GetStarMultiplier(StarLevel);
        int equipHealth = 0, equipAtk = 0, equipDef = 0, equipSpd = 0;
        float equipCrit = 0f;

        foreach (var item in EquippedItems.Values)
        {
            if (item == null) continue;
            equipHealth += item.healthBonus;
            equipAtk += item.attackBonus;
            equipDef += item.defenseBonus;
            equipSpd += item.speedBonus;
            equipCrit += item.critRateBonus;
        }

        MaxHealth = Mathf.RoundToInt(Data.baseHealth * multiplier) + equipHealth;
        Attack = Mathf.RoundToInt(Data.baseAttack * multiplier) + equipAtk;
        Defense = Mathf.RoundToInt(Data.baseDefense * multiplier) + equipDef;
        Speed = Mathf.RoundToInt(Data.baseSpeed * multiplier) + equipSpd;
        CritRate = Mathf.Clamp01(Data.baseCritRate * multiplier + equipCrit);
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    public void Equip(EquipmentData equipment)
    {
        if (equipment == null) return;
        EquippedItems[equipment.slot] = equipment;
        RecalculateStats();
        Debug.Log($"{Data.heroName} 装备了 {equipment.equipmentName}");
    }

    /// <summary>
    /// 卸下装备
    /// </summary>
    public EquipmentData Unequip(EquipmentSlot slot)
    {
        if (EquippedItems.TryGetValue(slot, out var equipment))
        {
            EquippedItems.Remove(slot);
            RecalculateStats();
            Debug.Log($"{Data.heroName} 卸下了 {equipment.equipmentName}");
            return equipment;
        }
        return null;
    }

    /// <summary>
    /// 接受伤害（伤害已经由GameBalance.CalculateDamage计算好）
    /// </summary>
    public void TakeDamage(int damage, Hero attacker = null)
    {
        int actual = Mathf.Max(0, damage);
        CurrentHealth -= actual;
        if (CurrentHealth < 0) CurrentHealth = 0;

        // 荊棘反伤
        if (attacker != null && BattleThornsRate > 0f && !attacker.IsDead)
        {
            int thornsDmg = Mathf.RoundToInt(actual * BattleThornsRate);
            if (thornsDmg > 0)
            {
                attacker.CurrentHealth -= thornsDmg;
                if (attacker.CurrentHealth < 0) attacker.CurrentHealth = 0;
                Debug.Log($"{Data.heroName} 荊棘反伤 {attacker.Data.heroName} {thornsDmg} 点伤害");
            }
        }

        // 伤害飘字
        DamagePopup.Instance?.ShowDamage(transform.position, actual);
    }

    /// <summary>
    /// 治疗
    /// </summary>
    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);

        // 治疗飘字
        DamagePopup.Instance?.ShowHeal(transform.position, amount);
    }

    /// <summary>
    /// 获得护盾
    /// </summary>
    public void AddShield(int shieldAmount)
    {
        // MVP简化：护盾转化为临时生命
        CurrentHealth = Mathf.Min(MaxHealth + shieldAmount, CurrentHealth + shieldAmount);
    }

    /// <summary>
    /// 升星（2合1）
    /// </summary>
    public bool UpgradeStar()
    {
        if (StarLevel >= 3) return false;
        StarLevel++;
        RecalculateStats();
        CurrentHealth = MaxHealth; // 升星回满血
        return true;
    }

    /// <summary>
    /// 进化
    /// </summary>
    public void Evolve()
    {
        if (Data.evolutionForm == null || IsEvolved) return;
        Data = Data.evolutionForm;
        IsEvolved = true;
        RecalculateStats();
        CurrentHealth = MaxHealth;
    }

    /// <summary>
    /// 应用骰子组合效果
    /// </summary>
    public void ApplyDiceCombination(DiceCombination combo)
    {
        BattleAttack = Attack;
        BattleAttackSpeed = 1f;
        BattleCritRate = CritRate;
        BattleDefense = Defense;
        BattleSpeed = Speed;
        BattleDodgeRate = 0f;
        BattleCritDamage = 0.5f;
        HasFlameAOE = false;
        HasFrostSlow = false;
        HasPoisonBlade = false;
        ChainStrikeCount = 0;
        LifeStealRate = 0f;
        PoisonDamage = 0;
        BattleThornsRate = 0f;
        HasArmorBreak = false;
        LightningChainBounces = 0;
        HasBerserk = false;

        switch (combo.Type)
        {
            case DiceCombinationType.ThreeOfAKind:
                BattleAttack = Mathf.RoundToInt(Attack * 1.2f);
                break;
            case DiceCombinationType.Straight:
                BattleAttackSpeed = 1.2f;
                break;
            case DiceCombinationType.Pair:
                // 单点数字匹配召唤消耗时，该英雄获得额外Buff
                if (combo.SingleValue == Data.summonCost)
                {
                    BattleCritRate += 0.15f;
                }
                break;
        }
    }

    /// <summary>
    /// 重置战斗属性（每场战斗前调用）
    /// </summary>
    public void ResetBattleStats()
    {
        BattleAttack = Attack;
        BattleAttackSpeed = 1f;
        BattleCritRate = CritRate;
        BattleDefense = Defense;
        BattleSpeed = Speed;
        BattleDodgeRate = 0f;
        BattleCritDamage = 0.5f;
        HasFlameAOE = false;
        HasFrostSlow = false;
        HasPoisonBlade = false;
        ChainStrikeCount = 0;
        LifeStealRate = 0f;
        PoisonDamage = 0;
        BattleThornsRate = 0f;
        HasArmorBreak = false;
        LightningChainBounces = 0;
        HasBerserk = false;
    }

    /// <summary>
    /// 应用站位行效果
    /// </summary>
    public void ApplyRowEffect(GridRow row)
    {
        CurrentRow = row;
        switch (row)
        {
            case GridRow.Front:
                if (Data.heroClass == HeroClass.Tank)
                    BattleDefense = Mathf.RoundToInt(Defense * 1.3f);
                break;
            case GridRow.Back:
                if (Data.heroClass == HeroClass.Archer)
                    BattleAttack = Mathf.RoundToInt(Attack * 1.2f);
                break;
        }
    }

    void OnDestroy()
    {
        // 清理
    }
}

public enum GridRow
{
    Front,  // 前排
    Middle, // 中排
    Back    // 后排
}
