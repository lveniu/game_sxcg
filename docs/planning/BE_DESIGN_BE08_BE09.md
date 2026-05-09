# BE-08 & BE-09 后端任务详细设计

> CTO → @hermes-后端  
> 基于 GAP_ANALYSIS P0/P1 优先级

---

## BE-08：肉鸽地图路径系统

### 需求背景
当前肉鸽模式线性推图，需要加入**地图路径系统**：每层2-4个节点，玩家选择路径前进。增强策略性和重玩价值。

### 系统设计

#### 新增文件：`Assets/Scripts/Roguelike/RoguelikeMapSystem.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图节点类型
/// </summary>
public enum MapNodeType
{
    Battle,      // 普通战斗（权重最高）
    Elite,       // 精英战斗（高难度高奖励）
    Event,       // 随机事件
    Shop,        // 商店
    Rest,        // 休息点（回复生命）
    Boss,        // Boss关（每5关强制，固定位置）
    Treasure     // 宝箱
}

/// <summary>
/// 地图节点
/// </summary>
public class MapNode
{
    public string nodeId;              // 唯一ID "node_{layer}_{index}"
    public int layer;                  // 层级 0-based
    public int indexInLayer;           // 该层位置
    public MapNodeType nodeType;       
    public bool isVisited;             
    public bool isAvailable;           // 与已访问节点相邻且未被访问
    public List<string> nextNodeIds;   // 下层连接
    public List<string> prevNodeIds;   // 上层连接
    public string previewText;         // "3星敌人" / "随机事件"
    public int difficulty;             // 1-5星
}

/// <summary>
/// 地图数据
/// </summary>
public class MapData
{
    public List<List<MapNode>> layers;    // 按层组织
    public string currentNodeId;          
    public int totalLayers;               // 总层数
    public string startNodeId;            // 起始节点
}
```

#### 核心逻辑

```csharp
public class RoguelikeMapSystem
{
    public static RoguelikeMapSystem Instance { get; private set; }
    
    public MapData CurrentMap { get; private set; }
    public string CurrentNodeId { get; private set; }
    public event System.Action<MapData> OnMapGenerated;
    public event System.Action<MapNode> OnNodeSelected;
    
    // ===== 地图生成 =====
    
    /// <summary>
    /// 生成完整地图（新游戏或到达新区间时调用）
    /// </summary>
    public MapData GenerateMap(int totalLevels = 15)
    {
        // 算法：
        // 1. 每层 2-4 个节点（随机）
        // 2. 每个节点连接下层 1-3 个节点（至少1个，避免死路）
        // 3. 第 5/10/15 层固定为 Boss 节点
        // 4. 节点类型按权重随机分配
        // 5. 确保每层至少有1个非Battle节点（提供多样性）
        
        // 节点类型权重（非Boss层）：
        // Battle: 40% | Elite: 10% | Event: 20% | Shop: 15% | Rest: 10% | Treasure: 5%
        
        // Boss层：只有1个Boss节点
        // Boss前一层的所有节点都连接到Boss
    }
    
    /// <summary>
    /// 获取当前可选的下一层节点
    /// </summary>
    public List<MapNode> GetAvailableNodes()
    {
        // 从 CurrentNode 的 nextNodeIds 中找到所有节点
        // 标记 isAvailable = true
    }
    
    /// <summary>
    /// 选择节点（玩家确认前往）
    /// </summary>
    public void SelectNode(string nodeId)
    {
        // 1. 标记当前节点 isVisited = true
        // 2. 更新 CurrentNodeId
        // 3. 根据 nodeType 触发对应状态切换：
        //    Battle/Elite/Boss → GameState.Battle (通过GameStateMachine)
        //    Event → 触发 RandomEventSystem
        //    Shop → GameState.Shop (需要新增或复用)
        //    Rest → 恢复队伍20%最大生命
        //    Treasure → 直接发放奖励
        // 4. 触发 OnNodeSelected 事件
    }
    
    /// <summary>
    /// 获取当前节点
    /// </summary>
    public MapNode GetCurrentNode()
}
```

### 地图生成算法要点

```
Layer 0:  [Start] (1个节点，固定Battle)
Layer 1:  [Node1] [Node2] [Node3]    (2-4个，随机数量)
Layer 2:  [Node1] [Node2]            (2-4个)
Layer 3:  [Node1] [Node2] [Node3] [Node4]
...
Layer 4:  [Node1] [Node2] [Node3]
Layer 5:  [Boss]                      (固定Boss)
Layer 6:  [Node1] [Node2]
...
Layer 10: [Boss]
...
Layer 15: [Final Boss]
```

**连接规则：**
1. 每个节点至少连下层1个，最多3个
2. 下层每个节点至少被1个上层节点连接
3. 相邻层级节点优先连接临近位置（index差不超过1）
4. Boss层的所有上层节点都连到Boss

**节点类型分配（非Boss层）：**
| 类型 | 权重 | 说明 |
|------|------|------|
| Battle | 40% | 基础战斗 |
| Elite | 10% | 高难高奖，第3层后出现 |
| Event | 20% | 调用 RandomEventSystem |
| Shop | 15% | 打开商店 |
| Rest | 10% | 回复20%生命 |
| Treasure | 5% | 直接给金币/装备 |

**约束：**
- Boss层（5/10/15）固定1个Boss节点
- 前2层不出Elite和Treasure
- 每层至少1个非Battle节点
- Shop不出现在Boss前一层

### 与现有系统集成

1. **RoguelikeGameManager.StartNewGame()** 中调用 `RoguelikeMapSystem.Instance.GenerateMap(15)`
2. **GameStateMachine** 需要新增 `MapSelect` 状态（在 Settlement 之后，DiceRoll 之前）
3. 战斗结束后回到 `MapSelect`，玩家选择下一个节点
4. `SelectNode()` 根据 nodeType 驱动后续流程

### JSON配置（可选，后续扩展）

`Assets/Resources/Data/map_config.json`：
```json
{
  "node_weights": {
    "battle": 0.40, "elite": 0.10, "event": 0.20,
    "shop": 0.15, "rest": 0.10, "treasure": 0.05
  },
  "nodes_per_layer": { "min": 2, "max": 4 },
  "boss_interval": 5,
  "total_layers": 15
}
```

---

## BE-09：PlayerInventory 扩展

### 需求背景
当前 `PlayerInventory` 功能基础（金币/装备/卡牌的增删），需要扩展支持分类查询、物品统计、装备过滤，为前端背包UI提供充分的数据接口。

### 扩展设计

#### PlayerInventory 新增方法

在 `Assets/Scripts/Player/PlayerInventory.cs` 中添加：

```csharp
// ===== 分类查询 =====

/// <summary>按槽位筛选装备</summary>
public List<EquipmentData> GetEquipmentsBySlot(EquipmentSlot slot)
{
    return Equipments.FindAll(e => e.slot == slot);
}

/// <summary>按稀有度筛选装备</summary>
public List<EquipmentData> GetEquipmentsByRarity(CardRarity rarity)
{
    return Equipments.FindAll(e => e.rarity == rarity);
}

/// <summary>获取装备总数</summary>
public int GetEquipmentCount() => Equipments.Count;

/// <summary>获取卡牌总数</summary>
public int GetCardCount() => Cards.Count;

// ===== 事件系统 =====

/// <summary>背包变更事件（前端监听刷新UI）</summary>
public event System.Action OnInventoryChanged;

/// <summary>装备变更事件</summary>
public event System.Action<EquipmentData, bool> OnEquipmentChanged; // (装备, 是否添加)

// 在 AddEquipment/RemoveEquipment/EquipToHero/UnequipFromHero 中触发事件
```

#### 物品分类枚举

```csharp
/// <summary>物品大类（前端Tab用）</summary>
public enum ItemCategory
{
    All,         // 全部
    Equipment,   // 装备
    Material,    // 材料（预留，MVP不实现）
    Consumable   // 消耗品（预留，MVP不实现）
}
```

#### 装备比较辅助

```csharp
/// <summary>获取装备的战力评分（用于排序和比较）</summary>
public static int GetEquipmentPower(EquipmentData equip)
{
    return equip.attackBonus * 3 + equip.defenseBonus * 2 
         + equip.healthBonus + equip.speedBonus * 2 
         + Mathf.RoundToInt(equip.critRateBonus * 100);
}

/// <summary>按战力排序装备</summary>
public List<EquipmentData> GetEquipmentsSortedByPower(bool descending = true)
{
    var sorted = new List<EquipmentData>(Equipments);
    if (descending)
        sorted.Sort((a, b) => GetEquipmentPower(b) - GetEquipmentPower(a));
    else
        sorted.Sort((a, b) => GetEquipmentPower(a) - GetEquipmentPower(b));
    return sorted;
}
```

### 改动范围

仅修改 `PlayerInventory.cs`，新增约80-100行代码。不涉及其他文件。

### 验证

- 在 `IntegrationTest.cs` 中添加背包扩展功能的测试用例
- 测试：添加装备 → 按槽位筛选 → 排序 → 装备到英雄 → 验证事件触发

---

## 依赖关系

```
BE-08 RoguelikeMapSystem ──被依赖──→ FE-08 地图路径UI
BE-09 PlayerInventory扩展 ──被依赖──→ FE-09 背包UI重构

BE-08 还需要 GameStateMachine 配合（新增 MapSelect 状态）
```

**开发顺序：**
1. BE-09 简单，先完成（~1小时工作量）
2. BE-08 核心逻辑，重点在地图生成算法（~3小时工作量）
3. 两个BE任务可并行开发

## 文件预期

```
Assets/Scripts/Roguelike/RoguelikeMapSystem.cs     (BE-08, 新增 ~350行)
Assets/Scripts/Player/PlayerInventory.cs           (BE-09, 修改 +80行)
Assets/Scripts/Core/GameStateMachine.cs            (BE-08, 修改 +10行，新增MapSelect状态)
```

提交到 `dev` 分支，每个任务单独commit。
