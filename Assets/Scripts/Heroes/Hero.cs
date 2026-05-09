using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 遗物Buff — 肉鸽奖励赋予的永久增益
/// </summary>
public enum RelicBuffType
{
    AttackBonus,      // 攻击加成
    DefenseBonus,     // 防御加成
    HealthBonus,      // 生命加成
    SpeedBonus,       // 速度加成
    CritRateBonus,    // 暴击率加成
    CritDamageBonus,  // 暴击伤害加成
    LifeSteal,        // 吸血
    Thorns,           // 荆棘反伤
    DodgeRate,        // 闪避率
    ExtraReroll,      // 额外重摇次数
}

public class RelicBuff
{
    public RelicBuffType buffType;
    public float value;        // 百分比或固定值
    public string sourceName;  // 来源遗物名称（用于显示）

    public RelicBuff(RelicBuffType type, float val, string source = "")
    {
        buffType = type;
        value = val;
        sourceName = source;
    }
}

/// <summary>
/// 运行时英雄单位 — 占场上的实体
/// </summary>
public class Hero : MonoBehaviour
{
    public HeroData Data { get; private set; }
    public int StarLevel { get; private set; } = 1;
    public bool IsDead => CurrentHealth <= 0;
    public bool IsBoss { get; set; } = false;

    // 装备
    public Dictionary<EquipmentSlot, EquipmentData> EquippedItems { get; private set; } = new();

    // 遗物Buff列表（肉鸽奖励获得，整局有效）
    public List<RelicBuff> RelicBuffs { get; private set; } = new();

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
    public float LifeStealRate { get; set; }            // 吸血率（百分比）
    public float BattleThornsRate { get; set; }          // 战斗荆棘反伤率（百分比）

    // 机制怪系统 & 面效果状态
    public bool IsStunned { get; private set; }
    public bool HasArmorBreak { get; set; }
    public int LightningChainBounces { get; set; } = 0;

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

        // 应用遗物Buff
        ApplyRelicBuffs();
    }

    /// <summary>
    /// 应用遗物Buff到基础属性
    /// </summary>
    private void ApplyRelicBuffs()
    {
        foreach (var buff in RelicBuffs)
        {
            switch (buff.buffType)
            {
                case RelicBuffType.AttackBonus:
                    Attack += Mathf.RoundToInt(Attack * buff.value);
                    break;
                case RelicBuffType.DefenseBonus:
                    Defense += Mathf.RoundToInt(Defense * buff.value);
                    break;
                case RelicBuffType.HealthBonus:
                    MaxHealth += Mathf.RoundToInt(MaxHealth * buff.value);
                    break;
                case RelicBuffType.SpeedBonus:
                    Speed += Mathf.RoundToInt(Speed * buff.value);
                    break;
                case RelicBuffType.CritRateBonus:
                    CritRate = Mathf.Clamp01(CritRate + buff.value);
                    break;
                case RelicBuffType.CritDamageBonus:
                    BattleCritDamage += buff.value;
                    break;
            }
        }
    }

    /// <summary>
    /// 添加遗物Buff
    /// </summary>
    public void AddRelicBuff(RelicBuff buff)
    {
        RelicBuffs.Add(buff);
        RecalculateStats();
    }

    /// <summary>是否已进化（星级≥3视为进化状态）</summary>
    public bool IsEvolved => StarLevel >= 3;

    /// <summary>
    /// 进化：星级+1（上限3），重算属性
    /// </summary>
    public void Evolve()
    {
        if (StarLevel >= 3)
        {
            Debug.Log($"{Data.heroName} 已是满星，无法继续进化");
            return;
        }
        StarLevel++;
        RecalculateStats();
        Debug.Log($"{Data.heroName} 进化成功 → 星级{StarLevel}");
    }

    /// <summary>
    /// 获取指定槽位的已装备物品
    /// </summary>
    public EquipmentData GetEquippedItem(EquipmentSlot slot)
    {
        EquippedItems.TryGetValue(slot, out var item);
        return item;
    }

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
        // 闪避判定
        if (BattleDodgeRate > 0f && Random.value < BattleDodgeRate)
        {
            Debug.Log($"{Data.heroName} 闪避了攻击！");
            DamagePopup.Instance?.ShowDamage(transform.position, 0);
            return;
        }

        int actual = Mathf.Max(0, damage);
        CurrentHealth -= actual;
        if (CurrentHealth < 0) CurrentHealth = 0;

        // 遗物荆棘反伤
        float thornsRate = GetRelicBuffValue(RelicBuffType.Thorns);
        if (attacker != null && thornsRate > 0f && !attacker.IsDead)
        {
            int thornsDmg = Mathf.RoundToInt(actual * thornsRate);
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
    /// 复活 — 遗物等效果将已死亡英雄恢复到指定血量
    /// </summary>
    public void Revive(int healAmount)
    {
        if (!IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, Mathf.Max(1, healAmount));
        IsStunned = false;
        HasArmorBreak = false;
        Debug.Log($"{Data.heroName} 复活！恢复至 {CurrentHealth}/{MaxHealth} HP");
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

    // ========== 肉鸽属性强化方法 ==========

    /// <summary>
    /// 强化最大生命（百分比加成）
    /// </summary>
    public void BoostMaxHealth(float percentage)
    {
        int bonus = Mathf.RoundToInt(MaxHealth * percentage);
        MaxHealth += bonus;
        CurrentHealth = Mathf.Min(CurrentHealth + bonus, MaxHealth);
    }

    /// <summary>
    /// 强化攻击（百分比加成）
    /// </summary>
    public void BoostAttack(float percentage)
    {
        int bonus = Mathf.RoundToInt(Attack * percentage);
        Attack += bonus;
        BattleAttack += bonus;
    }

    /// <summary>
    /// 强化防御（百分比加成）
    /// </summary>
    public void BoostDefense(float percentage)
    {
        int bonus = Mathf.RoundToInt(Defense * percentage);
        Defense += bonus;
        BattleDefense += bonus;
    }

    /// <summary>
    /// 强化速度（百分比加成）
    /// </summary>
    public void BoostSpeed(float percentage)
    {
        int bonus = Mathf.RoundToInt(Speed * percentage);
        Speed += bonus;
        BattleSpeed += bonus;
    }

    /// <summary>
    /// 强化暴击率（百分比加成）
    /// </summary>
    public void BoostCritRate(float percentage)
    {
        CritRate = Mathf.Clamp01(CritRate + percentage);
        BattleCritRate = Mathf.Clamp01(BattleCritRate + percentage);
    }

    /// <summary>
    /// 获取指定遗物Buff类型的总值
    /// </summary>
    public float GetRelicBuffValue(RelicBuffType type)
    {
        float total = 0f;
        foreach (var buff in RelicBuffs)
        {
            if (buff.buffType == type)
                total += buff.value;
        }
        return total;
    }

    /// <summary>
    /// 重置所有战斗状态（局内属性）
    /// </summary>
    private void ClearBattleEffects()
    {
        BattleAttack = Attack;
        BattleAttackSpeed = 1f;
        BattleCritRate = CritRate;
        BattleDefense = Defense;
        BattleSpeed = Speed;
        BattleDodgeRate = GetRelicBuffValue(RelicBuffType.DodgeRate);
        BattleCritDamage = 0.5f + GetRelicBuffValue(RelicBuffType.CritDamageBonus);
        LifeStealRate = GetRelicBuffValue(RelicBuffType.LifeSteal);
        BattleThornsRate = GetRelicBuffValue(RelicBuffType.Thorns);
        IsStunned = false;
        HasArmorBreak = false;
        LightningChainBounces = 0;
    }

    /// <summary>
    /// 设置/取消眩晕状态
    /// </summary>
    public void SetStunned(bool stunned) => IsStunned = stunned;

    /// <summary>
    /// 应用骰子组合效果
    /// </summary>
    public void ApplyDiceCombination(DiceCombination combo)
    {
        ClearBattleEffects();

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
        ClearBattleEffects();
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
                if (Data.heroClass == HeroClass.Warrior)
                    BattleDefense = Mathf.RoundToInt(Defense * 1.3f);
                break;
            case GridRow.Back:
                if (Data.heroClass == HeroClass.Mage)
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
