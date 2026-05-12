# BE-10/11/12 技术方案：缺口修复 + 集成任务

> CTO 技术方案 v1.0
> 审查发现：3个任务的核心代码**已存在**，实际工作是补缺口 + 修Bug + 集成

---

## 一、现状盘点

| 任务 | 代码文件 | 行数 | 状态 |
|------|---------|------|------|
| BE-10 英雄经验/升级 | `Heroes/HeroExpSystem.cs` | 410行 | ✅ 主体完成，需集成测试 |
| BE-11 随机事件 | `Events/RandomEventSystem.cs` | 529行 | 🔴 编译阻断：EventOption字段不匹配 |
| BE-12 背包系统 | `Player/PlayerInventory.cs` | 175行 | ✅ 主体完成，缺通用Item基类 |

---

## 二、🔴 编译阻断（必须先修）

### BUG-1: EventOption 类字段缺失

**文件**: `UI/Panels/EventPanel.cs:889-894`

**问题**: `EventOption` 只有3个字段，但 `RandomEventSystem.cs` 使用了8个不存在的字段：
- `effectType` (EventEffectType)
- `effectValue` (float)
- `goldCost` (int)
- `secondaryEffect` (EventEffectType)
- `secondaryValue` (float)
- `riskFailEffectType` (EventEffectType)
- `riskFailValue` (float)
- `isRiskOption` (已存在)

**修复方案**: 扩展 EventOption 类定义

```csharp
// EventPanel.cs — 替换现有的 EventOption 类（889-894行）
[System.Serializable]
public class EventOption
{
    // === 基础字段（原有） ===
    public string optionText;            // "打开宝箱"
    public string effectDescription;     // "获得50金币"
    public bool isRiskOption;            // 是否风险选项

    // === 效果系统字段（补充） ===
    public EventEffectType effectType = EventEffectType.None;   // 主效果类型
    public float effectValue;           // 主效果数值
    public int goldCost;                // 选项金币花费

    // === 复合效果 ===
    public EventEffectType secondaryEffect = EventEffectType.None; // 次要效果
    public float secondaryValue;        // 次要效果数值

    // === 风险机制 ===
    public EventEffectType riskFailEffectType = EventEffectType.None; // 失败效果
    public float riskFailValue;         // 失败效果数值
}
```

**影响范围**: 仅改 EventPanel.cs 一个类定义，RandomEventSystem.cs 无需改动

---

## 三、BE-10 英雄经验/升级系统（集成任务）

### 3.1 已完成 ✅
- 经验公式: `20 + (lvl-1)*30 + max(0, lvl-6)*20`
- 经验来源: 通关/击杀/Boss/事件（4种）
- 升级逻辑: 连续升级 + 被动技能阈值强化
- 星级合成: 3星上限，同名同星自动合成
- 团队经验分配: 平分给存活英雄
- JSON配置加载: exp_table.json + hero_exp_config.json

### 3.2 缺口（需要补）

#### 缺口1: 升级属性成长未生效
`HeroExpSystem.CheckLevelUp()` 只更新了 `HeroLevel`，**没有实际增加英雄属性**。

**需要做**: 升级时调用 `hero.RecalculateStats()` 或手动加属性

```csharp
// HeroExpSystem.cs CheckLevelUp() 方法中，hero.SetLevel() 之后加：
hero.RecalculateStats(); // 触发属性重算（需确认Hero有此方法且考虑等级成长）
```

#### 缺口2: HeroExpSystem 未接入 RoguelikeGameManager
`HeroExpSystem.Instance` 需要在肉鸽流程启动时创建。

```csharp
// RoguelikeGameManager.StartRun() 中加：
HeroExpSystem.Create();
```

#### 缺口3: 战斗结算未触发经验
`BattleManager` 或 `SettlementState` 中需要调用 `HeroExpSystem.GainExpForTeam()`。

```csharp
// Settlement.HandleStateEntered() 中加：
var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
int level = RoguelikeGameManager.Instance?.CurrentLevel ?? 1;
HeroExpSystem.Instance?.GainExpForTeam(heroes, 10, level, isBoss: false);
```

### 3.3 验收标准
- [ ] 英雄获得经验后等级提升
- [ ] 等级提升后属性实际增长
- [ ] 星级合成UI可触发
- [ ] 死亡英雄不参与经验分配

---

## 四、BE-11 随机事件系统（修Bug + 配置化）

### 4.1 已完成 ✅
- 6种事件类型 + 事件效果枚举
- 多选项系统 + 风险判定（50%概率）
- 复合效果（主+次）
- 地图节点模式（100%触发+选项）
- 旧流程兼容（30%概率触发）
- 效果应用引擎完整（金币/生命/攻击/卡牌/遗物/折扣/竞技场）

### 4.2 缺口

#### 缺口1: 🔴 EventOption 字段缺失（见BUG-1）
修了就能编译。

#### 缺口2: 事件数据硬编码，未JSON配置化
当前6种事件的数据全部写在 C# switch-case 里（第89-293行），不方便策划调参。

**方案**: 创建 `random_events.json`，运行时加载

```json
// Resources/Data/random_events.json 结构示例
{
  "_version": "1.0",
  "events": [
    {
      "id": "treasure",
      "type": "Treasure",
      "name_cn": "神秘宝箱",
      "options": [
        {
          "optionText": "打开宝箱",
          "effectType": "AddGold",
          "effectFormula": "20 + level * 5",
          "isRiskOption": false
        },
        {
          "optionText": "小心检查再打开",
          "effectType": "AddGold",
          "effectFormula": "goldReward / 2",
          "secondaryEffect": "AddHealth",
          "secondaryFormula": "10 + level * 2"
        },
        {
          "optionText": "用魔法探测（风险）",
          "effectType": "AddGold",
          "effectFormula": "goldReward * 1.5",
          "isRiskOption": true,
          "riskFailEffectType": "AddHealth",
          "riskFailFormula": "-(5 + level * 3)"
        }
      ]
    }
  ]
}
```

**后端任务**:
1. 创建 `RandomEventConfig` 数据模型（匹配JSON结构）
2. 在 `ConfigLoader` 加 `LoadRandomEvents()` 方法
3. `RandomEventSystem` 从JSON读事件，fallback到硬编码

#### 缺口3: 事件未接入肉鸽地图流程
`RoguelikeMapSystem` 的 Event 节点需要调用 `TriggerEventForMapNode()`。

### 4.3 验收标准
- [ ] 修 BUG-1 后 RandomEventSystem 编译通过
- [ ] Event 节点可触发随机事件
- [ ] 选项效果正确应用（金币/生命/攻击/卡牌/遗物）
- [ ] 风险选项50%判定生效
- [ ] JSON配置可覆盖硬编码参数

---

## 五、BE-12 背包物品基类（轻量扩展）

### 5.1 已完成 ✅
- `PlayerInventory` 单例完整（金币/装备/卡牌管理）
- 分类查询（按槽位/稀有度）
- 战力评分排序
- 存档支持方法
- 事件系统（OnInventoryChanged / OnEquipmentChanged）

### 5.2 缺口

#### 缺口1: 缺少通用 ItemBase 基类
当前装备是 `EquipmentData`，卡牌是 `CardInstance`，没有统一基类。
**影响**: 通用背包UI不好做统一列表展示。

**方案**: 轻量级接口（不需要改动现有类太多）

```csharp
/// <summary>
/// 物品通用接口 — 背包UI统一展示用
/// </summary>
public interface IItem
{
    string ItemId { get; }
    string DisplayName { get; }
    string Description { get; }
    ItemCategory Category { get; }
    int StackCount { get; set; }     // 堆叠数量
    int MaxStack { get; }            // 最大堆叠
    Sprite Icon { get; }
    bool IsStackable { get; }
}

/// <summary>
/// 物品堆叠管理器 — 处理同类物品合并
/// </summary>
public static class ItemStackHelper
{
    /// <summary>尝试堆叠，返回剩余未堆叠数量</summary>
    public static int TryStack(IItem target, IItem incoming, int amount)
    {
        if (!target.IsStackable || target.ItemId != incoming.ItemId)
            return amount; // 不可堆叠

        int space = target.MaxStack - target.StackCount;
        int toAdd = Mathf.Min(space, amount);
        target.StackCount += toAdd;
        return amount - toAdd;
    }
}
```

**EquipmentData 和 CardInstance 补充实现 IItem 接口即可**。

#### 缺口2: 背包容量限制
当前背包无限容量。MVP建议：**暂不限制**，等收集反馈后再加。

### 5.3 验收标准
- [ ] IItem 接口定义
- [ ] EquipmentData 实现 IItem
- [ ] CardInstance 实现 IItem
- [ ] 背包UI可通过 IItem 统一展示物品列表
- [ ] 堆叠逻辑正确（同类物品自动合并）

---

## 六、执行计划

### 优先级排序
```
P0 (编译阻断): BUG-1 EventOption字段补全        ← 0.5天
P1 (核心集成): BE-10 经验系统集成到肉鸽流程     ← 1天
P1 (配置化):   BE-11 事件JSON配置化 + 地图接入   ← 1.5天
P2 (扩展):     BE-12 IItem基类 + 堆叠            ← 0.5天
```

### 并行开发建议
- @hermes-后端 先修 BUG-1（10分钟），然后并行推进 BE-10集成 + BE-11配置化
- BE-12 IItem 可以在 BE-10/11 完成后穿插做

---

## 七、前端同步要点

修改 `EventOption` 后，`EventPanel.cs` 的选项渲染需要扩展：
- 显示 `effectDescription`（已有）
- 金币花费显示（`goldCost > 0` 时红色标注）
- 风险选项样式（`isRiskOption` 已有，但需要加风险失败效果描述）
- 次要效果描述（`secondaryEffect` 补充显示）

@hermes-前端 在后端修完 BUG-1 后同步更新 EventPanel 的选项渲染逻辑。
