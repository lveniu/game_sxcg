# FE-08 & FE-09 前端任务详细设计

> CTO → @hermes-前端  
> 基于 GAP_ANALYSIS P0/P1 优先级

---

## FE-08：肉鸽地图路径UI

### 需求背景
当前肉鸽模式是线性推图（Level 1→2→3...），缺少分支选择。需要加入**地图路径系统**，让玩家每关结束后选择不同路径（战斗/事件/商店/休息），增强肉鸽策略性。

### 后端接口（@hermes-后端 同步开发）

**新增类 `RoguelikeMapSystem`（后端负责），前端通过以下接口交互：**

```csharp
// 地图节点类型
public enum MapNodeType
{
    Battle,      // 普通战斗
    Elite,       // 精英战斗（高奖励）
    Event,       // 随机事件
    Shop,        // 商店
    Rest,        // 休息点（回复生命）
    Boss,        // Boss关（每5关强制）
    Treasure     // 宝箱
}

// 地图节点
public class MapNode
{
    public string nodeId;           // 唯一ID "node_0_2"
    public int layer;               // 层级（0=起始, 1,2,3...）
    public int indexInLayer;        // 该层中的位置索引
    public MapNodeType nodeType;    // 节点类型
    public bool isVisited;          // 是否已访问
    public bool isAvailable;        // 当前是否可选（与已访问节点相邻）
    public List<string> nextNodeIds;// 下一层连接的节点ID
    public List<string> prevNodeIds;// 上一层连接的节点ID
    public string previewText;      // 预览文字 "3星敌人" / "随机事件"
    public int difficulty;          // 难度星级 1-5
}

// 地图数据（后端生成，前端渲染）
public class MapData
{
    public List<List<MapNode>> layers;   // 按层组织
    public string currentNodeId;         // 当前所在节点
    public int totalLayers;              // 总层数（15关）
}
```

**前端调用的后端API：**
```csharp
// RoguelikeMapSystem（后端单例）
RoguelikeMapSystem.Instance.GenerateMap(int totalLevels) → MapData
RoguelikeMapSystem.Instance.GetAvailableNodes() → List<MapNode>
RoguelikeMapSystem.Instance.SelectNode(string nodeId) → void
RoguelikeMapSystem.Instance.GetCurrentNode() → MapNode
```

### 前端UI设计

#### RoguelikeMapPanel : UIPanel

**布局结构：**
```
┌──────────────────────────────────────┐
│  [返回] 层数 3/15    [遗物] [背包]   │  顶部栏
├──────────────────────────────────────┤
│                                      │
│         ○─────○                      │  Layer 0 (起始)
│              │                       │
│         ○───○───○                    │  Layer 1
│              │                       │
│    ○───○────○────○───○              │  Layer 2 (当前层)
│         ↑                            │
│      [当前位置]                       │
│                                      │
│    ○───────○─────○                   │  Layer 3 (可选)
│                                      │
│    ○───────○─────○───○              │  Layer 4 (未来)
│                                      │
├──────────────────────────────────────┤
│  [节点信息栏：类型/难度/预览]          │  底部信息
│  [确认前往]                           │
└──────────────────────────────────────┘
```

**交互逻辑：**
1. 地图可垂直滚动（ScrollRect），起始位置在当前层
2. 可选节点：金色描边 + 呼吸动画
3. 已访问节点：绿色勾 + 半透明
4. 不可达节点：暗灰色
5. 点击可选节点 → 底部信息栏更新 → 弹出确认按钮
6. 确认 → 调用 `SelectNode(nodeId)` → 状态机切换到对应阶段

**节点图标映射：**
| 类型 | 图标 | 颜色 |
|------|------|------|
| Battle | ⚔️ 剑 | #FF6B6B 红 |
| Elite | 💀 骷髅 | #FF4500 橙红 |
| Event | ❓ 问号 | #FFD700 金 |
| Shop | 🛒 购物车 | #4ECDC4 青 |
| Rest | ⛺ 帐篷 | #95E1D3 绿 |
| Boss | 👹 Boss | #8B0000 暗红 |
| Treasure | 🎁 礼物 | #FF69B4 粉 |

**路径连线：**
- 使用 UILineRenderer（或简单的 Image + 尺寸拉伸）
- 已走过的路径：实线 + 金色
- 未来路径：虚线 + 灰色
- 可选路径：实线 + 金色呼吸

**DOTween动画：**
1. 节点入场：从底部依次飞入（每层延迟0.1s，OutBack）
2. 选中确认：目标节点放大脉冲 → 非选中节点淡出 → 屏幕过渡到下阶段
3. 可选节点呼吸：scale 1.0↔1.05，duration 1.5s，Yoyo loop
4. 所有tween必须加 `.SetLink(gameObject)`

### 技术要点
- 面板继承 `UIPanel`，注册到 `UIManager` 的 panelDict
- 不需要预制体动态创建节点（用代码生成即可，节点数量不多最多15×5=75个）
- 连线用简单的 Image（宽1px×可调高度）旋转对齐即可，不需要额外插件
- 地图数据从 `RoguelikeMapSystem.Instance` 获取，前端不自己算

---

## FE-09：背包系统UI重构

### 需求背景
当前 `PlayerInventory` 只有基础的 `List<EquipmentData>` 和 `List<CardInstance>`，UI侧没有专用背包面板。需要构建完整的背包UI，支持分类浏览、详情查看、快捷穿戴。

### 后端接口（已有，无需新增）

```csharp
// PlayerInventory.Instance — 已有API
Gold                              // 金币
List<EquipmentData> Equipments    // 装备列表
List<CardInstance> Cards          // 卡牌列表
AddEquipment(EquipmentData)       // 添加装备
RemoveEquipment(EquipmentData)    // 移除装备
EquipToHero(EquipmentData, Hero)  // 装备到英雄（从背包移除）
UnequipFromHero(EquipmentSlot, Hero) // 从英雄卸下（回到背包）

// EquipmentData — 已有字段
equipmentName, slot, rarity, attackBonus, defenseBonus, 
healthBonus, speedBonus, critRateBonus, specialEffect, description

// EquipmentSlot 枚举
Weapon, Armor, Accessory

// CardRarity 枚举（装备复用）
White, Blue, Purple, Gold
```

**后端需要新增（@hermes-后端）：**
```csharp
// 新增物品分类枚举
public enum ItemCategory
{
    All,         // 全部
    Equipment,   // 装备
    Material,    // 材料（预留）
    Consumable   // 消耗品（预留）
}

// PlayerInventory 新增方法
public List<EquipmentData> GetEquipmentsBySlot(EquipmentSlot slot)
public List<EquipmentData> GetEquipmentsByRarity(CardRarity rarity)
public int GetEquipmentCount()
```

### 前端UI设计

#### InventoryPanel : UIPanel

**布局结构：**
```
┌──────────────────────────────────────┐
│  [关闭] 背包    💰 1234金币          │  顶部栏
├──────────────────────────────────────┤
│ [全部] [装备] [材料] [消耗品]        │  Tab栏
├──────────────┬───────────────────────┤
│              │                       │
│  ┌──┐ ┌──┐  │  📋 物品详情          │
│  │🗡│ │🛡│  │  ─────────────         │
│  └──┘ └──┘  │  名称: 铁剑           │
│  ┌──┐ ┌──┐  │  稀有度: ⭐白         │
│  │💍│ │🗡│  │  槽位: 武器            │
│  └──┘ └──┘  │                       │
│  ┌──┐ ┌──┐  │  ATK +5  DEF +0      │
│  │🛡│ │📚│  │  HP  +0  SPD +2      │
│  └──┘ └──┘  │  CRIT +3%            │
│              │                       │
│              │  特效: 吸血5%         │
│              │                       │
│              │  [装备到英雄▼]        │
│              │  [丢弃]              │
│              │                       │
├──────────────┴───────────────────────┤
│  快捷栏: [装备1][装备2][装备3]...    │  底部快捷栏
└──────────────────────────────────────┘
```

**交互逻辑：**

1. **Tab切换**：
   - "全部"：显示所有物品
   - "装备"：按slot子分类（武器/防具/饰品）
   - "材料"：预留Tab，显示空状态 "暂无材料"
   - "消耗品"：预留Tab，显示空状态 "暂无消耗品"

2. **物品格子**：
   - 4列×N行网格（ScrollRect纵向滚动）
   - 稀有度边框色：白=#CCCCCC, 蓝=#4A90D9, 紫=#9B59B6, 金=#FFD700
   - 选中：放大1.1x + 描边
   - 点击 → 右侧详情面板更新

3. **详情面板**：
   - 装备类型：显示属性加成（绿色正数）
   - 特效文本
   - "装备到英雄" 按钮 → 弹出英雄选择下拉（当前队伍的英雄列表）
   - "丢弃" 按钮 → 二次确认弹窗

4. **快捷穿戴流程**：
   - 点击"装备到英雄" → 弹出英雄列表（每个英雄显示头像+名字+当前该槽位装备）
   - 选择英雄 → 调用 `PlayerInventory.Instance.EquipToHero(equip, hero)`
   - 如果该槽位已有装备 → 自动卸下旧装备回背包
   - 成功后：格子淡出动画 + 详情面板重置

**DOTween动画：**
1. Tab切换：内容区淡入淡出（0.2s）
2. 物品选中：scale 1.0→1.1→1.05（弹性，0.15s）
3. 装备成功：格子向英雄方向飞出 + 金色闪光
4. 所有tween加 `.SetLink(gameObject)`

### 技术要点
- 面板继承 `UIPanel`，注册到 `UIManager`
- 物品格子用代码动态生成（最多20-30个物品），不需要对象池
- 数据源：`PlayerInventory.Instance`，监听 `OnInventoryChanged` 事件刷新
- 背包面板可以从多个入口打开：主菜单、战斗结算、商店购买后

---

## 依赖关系

```
FE-08 地图路径UI ──依赖──→ BE-08 RoguelikeMapSystem（后端）
FE-09 背包UI重构 ──依赖──→ BE-09 PlayerInventory扩展（后端）
```

**建议开发顺序：**
1. 前端先用 MockData 开发UI和动画
2. 后端完成 API 后对接联调
3. 前后端可并行开发

## 文件预期

```
Assets/Scripts/UI/Panels/RoguelikeMapPanel.cs      (FE-08, ~500行)
Assets/Scripts/UI/Panels/InventoryPanel.cs          (FE-09, ~450行)
```

提交到 `dev` 分支，每个任务单独commit。
