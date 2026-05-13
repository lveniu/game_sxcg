# BE-13/14/15 后端任务书 v1.0

> CTO → @hermes-后端
> 前置：BE-10/11/12 全部完成 ✅
> 执行顺序：BE-13 → BE-14 → BE-15

---

## BE-13：装备套装效果逻辑

### 现状
- `EquipmentData.cs` 已有 `setId`/`setName` 字段 + `BelongsToSet` 属性
- `EquipmentManager.cs` 已有4套套装定义（烈焰/磐石/疾风/命运）
- `Hero.cs` 的 `Equip()` 方法有装备逻辑，但**无套装检测/激活**
- `EquipPanel.cs` UI已有套装展示（UI层完整）

### 需要做的

#### 1. 定义套装效果表

在 `EquipmentManager.cs` 中新增套装效果配置：

```csharp
// 套装效果定义（2件激活）
static readonly Dictionary<string, (string name, int attackBonus, int defenseBonus, int healthBonus, float critBonus)> setBonuses = new()
{
    ["set_flame"] = ("烈焰之力", 8, 0, 0, 0.05f),    // +8攻击 +5%暴击
    ["set_rock"]  = ("磐石之盾", 0, 10, 30, 0f),       // +10防御 +30生命
    ["set_wind"]  = ("疾风之速", 4, 0, 0, 0.03f),      // +4攻击 +3%暴击
    ["set_fate"]  = ("命运之轮", 5, 5, 20, 0.02f),     // 全属性小加
};
```

#### 2. 新增套装检测方法

在 `EquipmentManager.cs` 新增：

```csharp
/// <summary>
/// 计算指定英雄已激活的套装效果
/// </summary>
public static Dictionary<string, int> CountSetPieces(Hero hero)
{
    var counts = new Dictionary<string, int>();
    foreach (var kvp in hero.EquippedItems)
    {
        var equip = kvp.Value;
        if (equip != null && equip.BelongsToSet)
        {
            if (!counts.ContainsKey(equip.setId))
                counts[equip.setId] = 0;
            counts[equip.setId]++;
        }
    }
    return counts;
}

/// <summary>
/// 获取已激活的套装效果列表（2件以上激活）
/// </summary>
public static List<(string setId, string bonusName, int count, int atk, int def, int hp, float crit)> GetActiveSetBonuses(Hero hero)
{
    var result = new List<...>();
    var counts = CountSetPieces(hero);
    foreach (var kvp in counts)
    {
        if (kvp.Value >= 2 && setBonuses.TryGetValue(kvp.Key, out var bonus))
        {
            result.Add((kvp.Key, bonus.name, kvp.Value, bonus.attackBonus, bonus.defenseBonus, bonus.healthBonus, bonus.critBonus));
        }
    }
    return result;
}
```

#### 3. Hero.RecalculateStats() 加入套装加成

在 `Hero.cs` 的 `RecalculateStats()` 方法末尾，`ApplyRelicBuffs()` 之前：

```csharp
// 套装效果
var setBonuses = EquipmentManager.GetActiveSetBonuses(this);
foreach (var bonus in setBonuses)
{
    Attack += bonus.atk;
    Defense += bonus.def;
    MaxHealth += bonus.hp;
    CritRate = Mathf.Clamp01(CritRate + bonus.crit);
}
```

#### 4. 装备变更时重算

`Hero.Equip()` 和 `Hero.Unequip()` 已调用 `RecalculateStats()`，无需额外改动。

### 验收标准
- [ ] `EquipmentManager.CountSetPieces()` 正确统计套装件数
- [ ] `EquipmentManager.GetActiveSetBonuses()` 返回激活的套装效果
- [ ] `Hero.RecalculateStats()` 包含套装属性加成
- [ ] 2件同套装激活效果，不足2件不激活
- [ ] 穿/脱装备后属性实时更新

---

## BE-14：装备强化系统

### 现状
- `EquipmentData.cs` 已有完整的强化字段（`enhanceLevel`, `maxEnhanceLevel`, `enhanceCostBase`, `enhanceCostMultiplier`）
- `EquipmentData` 已有 `GetEnhancedStat()`, `ApplyEnhance()`, `EnhancedXxxBonus` 属性
- **缺少强化执行逻辑**（EquipmentEnhancer 不存在）

### 需要做的

#### 1. 新建 `Equipment/EquipmentEnhancer.cs`

```csharp
/// <summary>
/// 装备强化器 — 管理强化流程（金币消耗 + 等级提升 + 属性更新）
/// </summary>
public static class EquipmentEnhancer
{
    /// <summary>强化成功率（基础80%，每级-5%）</summary>
    public static float GetSuccessRate(int currentLevel)
    {
        return Mathf.Max(0.3f, 0.8f - currentLevel * 0.05f);
    }

    /// <summary>计算强化费用</summary>
    public static int GetEnhanceCost(EquipmentData equip)
    {
        float cost = equip.enhanceCostBase * Mathf.Pow(equip.enhanceCostMultiplier, equip.enhanceLevel);
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// 执行强化（消耗金币，概率成功）
    /// </summary>
    /// <returns>(success, cost)</returns>
    public static (bool success, int cost) Enhance(EquipmentData equip, PlayerInventory inventory)
    {
        if (equip == null || equip.enhanceLevel >= equip.maxEnhanceLevel)
            return (false, 0);

        int cost = GetEnhanceCost(equip);
        if (inventory.Gold < cost)
            return (false, cost);

        // 扣费
        inventory.SpendGold(cost);

        // 概率判定
        float rate = GetSuccessRate(equip.enhanceLevel);
        bool success = Random.value <= rate;

        if (success)
        {
            equip.enhanceLevel++;
            equip.ApplyEnhance();
            Debug.Log($"[强化] {equip.equipmentName} 强化成功 → +{equip.enhanceLevel}");
        }
        else
        {
            Debug.Log($"[强化] {equip.equipmentName} 强化失败（成功率{rate:P0}）");
        }

        return (success, cost);
    }

    /// <summary>当前强化等级对应的属性倍率</summary>
    public static float GetEnhanceMultiplier(int enhanceLevel)
    {
        return 1f + enhanceLevel * 0.1f; // 每级+10%
    }
}
```

#### 2. Hero.RecalculateStats() 使用强化属性

在 `Hero.cs` 的 `RecalculateStats()` 中，装备属性读取改为使用 `EnhancedXxxBonus`：

```csharp
// 读取装备属性时
foreach (var kvp in EquippedItems)
{
    var equip = kvp.Value;
    if (equip == null) continue;
    Attack += equip.EnhancedAttackBonus;    // 替换 equip.attackBonus
    Defense += equip.EnhancedDefenseBonus;  // 替换 equip.defenseBonus
    MaxHealth += equip.EnhancedHealthBonus; // 替换 equip.healthBonus
    Speed += equip.EnhancedSpeedBonus;      // 替换 equip.speedBonus
    CritRate += equip.EnhancedCritRateBonus;// 替换 equip.critRateBonus
}
```

### 验收标准
- [ ] `EquipmentEnhancer.Enhance()` 正确扣金币+概率判定
- [ ] 强化成功后 `enhanceLevel` 递增
- [ ] `Hero.RecalculateStats()` 使用 `EnhancedXxxBonus` 属性
- [ ] 强化失败不扣等级，金币已扣
- [ ] 达到 `maxEnhanceLevel` 无法继续强化
- [ ] 费用随等级指数增长

---

## BE-15：商店后端逻辑完善

### 现状
- `ShopManager.cs` (143行) — 基础买/卖功能完整
- `ShopPanel.cs` (1433行) — UI大幅增强，包含商店等级/限购展示
- **缺口**：ShopManager 缺商店等级递增逻辑和限购次数追踪

### 需要做的

#### 1. ShopManager 增加商店等级和限购

在 `ShopManager.cs` 新增字段和方法：

```csharp
[Header("商店等级")]
public int ShopLevel { get; private set; } = 1;
public int MaxShopLevel => 5;

/// <summary>限购追踪：itemIndex → 剩余可购次数</summary>
private Dictionary<int, int> purchaseLimits = new();

/// <summary>
/// 升级商店（每关结束后可升一次，消耗金币）
/// 费用：50 * shopLevel
/// </summary>
public bool UpgradeShop(PlayerInventory inventory)
{
    if (ShopLevel >= MaxShopLevel) return false;
    int cost = 50 * ShopLevel;
    if (inventory.Gold < cost) return false;
    inventory.SpendGold(cost);
    ShopLevel++;
    Debug.Log($"[商店] 升级到 Lv.{ShopLevel}");
    return true;
}

/// <summary>商店等级对应的高级商品概率</summary>
public float GetHighRarityChance()
{
    return 0.1f + ShopLevel * 0.08f; // Lv1=18%, Lv5=50%
}
```

#### 2. GenerateShop 加入限购和等级影响

```csharp
public void GenerateShop(int levelId)
{
    CurrentItems.Clear();
    purchaseLimits.Clear();

    // ... 原有生成逻辑 ...

    // 设置限购（紫装以上限购1，其他限购2）
    for (int i = 0; i < CurrentItems.Count; i++)
    {
        int limit = CurrentItems[i].GetRarity() >= CardRarity.Purple ? 1 : 2;
        purchaseLimits[i] = limit;
    }
}
```

#### 3. BuyItem 增加限购检查

```csharp
public bool BuyItem(int itemIndex, PlayerInventory inventory)
{
    // ... 原有检查 ...
    
    // 限购检查
    if (purchaseLimits.TryGetValue(itemIndex, out int remaining) && remaining <= 0)
        return false;

    // ... 原有购买逻辑 ...

    // 扣减限购次数
    if (purchaseLimits.ContainsKey(itemIndex))
        purchaseLimits[itemIndex]--;
    
    return true;
}
```

### 验收标准
- [ ] `ShopLevel` 每关可升一次，最高5级
- [ ] `UpgradeShop()` 扣金币成功
- [ ] 高级商品随商店等级概率提升
- [ ] 紫装以上限购1，其他限购2
- [ ] 超过限购次数无法购买
- [ ] `GenerateShop()` 每次清空限购计数

---

## 接口依赖

| BE | 改动文件 | 新建文件 | 预估行数 |
|----|---------|---------|---------|
| BE-13 | EquipmentManager.cs, Hero.cs | — | ~40行 |
| BE-14 | Hero.cs | EquipmentEnhancer.cs | ~80行 |
| BE-15 | ShopManager.cs | — | ~50行 |

总计约 **170行** 新增代码，无新建JSON（全部用硬编码 fallback + 未来可配）。
