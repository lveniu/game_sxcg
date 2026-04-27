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
    /// 根据星级重新计算属性：1星=1x, 2星=1.5x, 3星=2x
    /// </summary>
    public void RecalculateStats()
    {
        float multiplier = 1f + (StarLevel - 1) * 0.5f;
        MaxHealth = Mathf.RoundToInt(Data.baseHealth * multiplier);
        Attack = Mathf.RoundToInt(Data.baseAttack * multiplier);
        Defense = Mathf.RoundToInt(Data.baseDefense * multiplier);
        Speed = Mathf.RoundToInt(Data.baseSpeed * multiplier);
        CritRate = Data.baseCritRate * multiplier;
    }

    /// <summary>
    /// 接受伤害
    /// </summary>
    public void TakeDamage(int damage)
    {
        int actual = Mathf.Max(1, damage - BattleDefense);
        CurrentHealth -= actual;
        if (CurrentHealth < 0) CurrentHealth = 0;

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

    /// <summary>
    /// 重置战斗属性（每场战斗前调用）
    /// </summary>
    public void ResetBattleStats()
    {
        BattleAttack = Attack;
        BattleAttackSpeed = 1f;
        BattleCritRate = CritRate;
        BattleDefense = Defense;
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
