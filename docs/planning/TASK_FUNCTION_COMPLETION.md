# 功能补全任务清单

> 生成时间: 2026-05-09
> 基于: GAP_ANALYSIS.md 功能缺口分析
> 状态: 待CEO审批
> 整体完成度: 76% → 目标95%

---

## 总览

| 指标 | 数值 |
|------|------|
| 总任务数 | 16个（BE 9个 + FE 7个） |
| 总估算 | 后端15工作日 + 前端10工作日 |
| 并行后总耗时 | 约3周 |
| 依赖关系 | 3个阶段串行，阶段内前后端并行 |

---

## 阶段1: P0 核心循环修复（约5天）

> 目标：修复核心循环断裂点，让骰子升级和Boss战真正运作

### BE-08 机制怪系统逻辑层 [5天] ⭐最高优先级

**问题**: Boss战（每5关）只有UI壳，无逻辑层，无法运作

**任务清单**:
1. 创建 `BossAI` 类 — Boss行为决策（技能释放优先级 + 机制触发时机）
2. 创建 `MechanicExecutor` — 6种Boss机制执行引擎:
   - ShieldCycle: Boss周期性获得/失去护盾，护盾期间免疫伤害
   - CountDown: Boss蓄力倒计时，到0释放大招，玩家需在时间内击破
   - PhaseChange: Boss血量分阶段切换形态和技能
   - DiceLock: Boss锁定玩家部分骰子面，限制重摇
   - DiceSteal: Boss偷取玩家骰子，减少可用骰子数
   - ElementWeakness: Boss周期性切换弱点元素，对应元素伤害2x
3. `MechanicTrigger` — 机制触发条件判定（血量阈值/回合数/骰子组合）
4. 与 `BattleManager` 集成 — Boss战分支处理
5. Boss数据配置 — ScriptableObject / JSON，每种Boss的机制类型+参数
6. 机制成功奖励/失败惩罚逻辑

**产出文件**:
- `Assets/Scripts/Boss/BossAI.cs`
- `Assets/Scripts/Boss/MechanicExecutor.cs`
- `Assets/Scripts/Boss/MechanicTrigger.cs`
- `Assets/Scripts/Boss/BossData.cs`
- `Assets/Resources/Data/boss_configs.json`

**依赖**: 无（可立即开工）

---

### BE-09 骰子面效果执行器 [3天]

**问题**: 骰子升级了但在战斗中无效果（FaceEffects只是字符串）

**任务清单**:
1. 创建 `FaceEffectParser` — 解析FaceEffects字符串为结构化效果指令
2. 创建 `FaceEffectExecutor` — 在战斗中执行特殊面效果:
   - MultiAttack: 额外攻击次数
   - Heal: 回复生命值
   - Shield: 获得护盾
   - Critical: 下次攻击必定暴击
   - ElementalDamage: 附加元素伤害
3. 与 `DiceRoller` 集成 — 掷骰结果触发面效果
4. 与 `BattleManager` 集成 — 面效果影响战斗Tick
5. 效果数值与BalanceProvider对接

**产出文件**:
- `Assets/Scripts/Dice/FaceEffectParser.cs`
- `Assets/Scripts/Dice/FaceEffectExecutor.cs`

**依赖**: 无（可与BE-08并行）

---

### FE-08 Boss机制HUD对接 [2天]

**问题**: BossMechanicHUD是空壳，无后端数据对接

**任务清单**:
1. BossMechanicHUD订阅BossAI事件（机制触发/倒计时/结果）
2. 机制触发时的视觉反馈（屏幕闪烁/震动）
3. 操作提示交互（ElementWeakness需要玩家点选元素）
4. 机制成功/失败的UI反馈动画

**依赖**: BE-08（后端逻辑层先完成）

---

## 阶段2: P1 核心循环补全（约6天）

> 目标：补全肉鸽地图路径、英雄成长、随机事件、背包系统

### BE-10 英雄升级/经验系统 [3天]

**问题**: 英雄有level字段但无经验获取逻辑，无成长反馈

**任务清单**:
1. `HeroExpSystem` — 经验获取规则（击杀/胜利/事件奖励）
2. 升级属性成长公式（与BalanceProvider对接）
3. 星级进化机制（3星→升阶需要特殊材料）
4. 与肉鸽奖励系统集成（升级材料作为奖励选项）
5. 英雄被动技能自动触发机制

**产出文件**:
- `Assets/Scripts/Heroes/HeroExpSystem.cs`
- `Assets/Resources/Data/hero_exp_config.json`

---

### BE-11 肉鸽地图路径生成 [4天]

**问题**: 无分支路径选择，肉鸽退化成线性推图

**任务清单**:
1. `RoguelikeMapGenerator` — 节点图生成算法:
   - 起点（固定）→ 多层分支 → Boss层（固定）
   - 每层3-5个节点，2-3条可选路径
2. 节点类型: 战斗/精英/事件/商店/休息/宝箱
3. `MapNode` 数据模型 — 类型/状态(未解锁/可选/已完成)/奖励预览
4. 路径选择逻辑 — 当前位置的相邻节点解锁
5. 与 `RoguelikeGameManager` 集成 — 替换线性关卡推进
6. 难度递增曲线（层深影响敌人属性/事件稀有度）

**产出文件**:
- `Assets/Scripts/Roguelike/RoguelikeMapGenerator.cs`
- `Assets/Scripts/Roguelike/MapNode.cs`
- `Assets/Scripts/Roguelike/RoguelikeMapData.cs`
- `Assets/Resources/Data/roguelike_map_config.json`

---

### BE-12 随机事件系统集成 [2天]

**问题**: RandomEventSystem存在但未接入肉鸽流程

**任务清单**:
1. 事件池JSON配置化（从代码硬编码迁移到JSON）
2. 事件效果执行引擎（选项效果枚举→具体逻辑映射）
3. 事件触发条件（关卡数/英雄状态/遗物持有）
4. 接入 `RoguelikeGameManager` — 事件节点触发流程
5. 事件稀有度权重随关卡调整

**产出文件**:
- `Assets/Resources/Data/event_configs.json`（从硬编码迁移）

---

### BE-13 背包物品体系重构 [3天]

**问题**: PlayerInventory用object类型，无物品基类，无法支撑分类/堆叠

**任务清单**:
1. `ItemData` 抽象基类 — ID/名称/描述/图标/类型/稀有度/最大堆叠数
2. 子类: `EquipmentItemData`, `MaterialItemData`, `ConsumableItemData`, `RelicItemData`
3. 背包堆叠逻辑（同类合并）
4. 分类/筛选接口（按类型/稀有度过滤）
5. 与 `EquipmentManager` 对接（装备操作）
6. 与 `ShopManager` 对接（买卖操作）

**产出文件**:
- `Assets/Scripts/Items/ItemData.cs`
- `Assets/Scripts/Items/EquipmentItemData.cs`
- `Assets/Scripts/Items/MaterialItemData.cs`
- `Assets/Scripts/Items/ConsumableItemData.cs`
- `Assets/Scripts/Player/PlayerInventory.cs`（重构）

---

### FE-09 肉鸽地图路径UI [3天]

**依赖**: BE-11

1. 路径节点可视化（S型/层状布局）
2. 节点图标（战斗=剑/事件=?/商店=金币/休息=帐篷）
3. 分支选择交互（点击可选节点进入）
4. 当前位置高亮 + 已完成节点灰化
5. 节点奖励预览Tooltip

---

### FE-10 英雄升级/经验UI [2天]

**依赖**: BE-10

1. 经验条UI（战斗结算时展示经验获取）
2. 升级特效（光效+属性提升飘字）
3. 星级进化动画（星星亮起+特效）

---

### FE-11 随机事件UI完善 [1天]

**依赖**: BE-12

1. EventPanel与后端RandomEventSystem对接
2. 选项按钮交互（点击选择→效果应用→反馈）
3. 事件插画区域（placeholder）

---

### FE-12 背包UI [3天]

**依赖**: BE-13

1. 物品列表（Grid布局）
2. 分类Tab（全部/装备/材料/消耗品）
3. 堆叠数量显示
4. 物品详情Tooltip
5. 使用/装备/丢弃操作

---

## 阶段3: P2 体验增强（约4天）

> 目标：经济系统深度、装备搭配深度、卡牌配置化

### BE-14 商店等级系统 [2天]
1. 商店等级（随关卡推进解锁更高等级商品）
2. 商品稀有度概率分布（高等级商店出稀有商品概率高）
3. 刷新费用递增
4. 限购机制

### BE-15 装备套装效果 [2天]
1. 套装定义（2件套/4件套效果）
2. 套装激活判定
3. 与EquipmentManager集成

### BE-16 卡牌效果引擎 [3天]
1. 效果配置化（从硬编码switch迁移到JSON）
2. 效果解析引擎（通用效果执行器）
3. 卡牌稀有度系统
4. 卡组编辑后端接口

### FE-13 商店UI完善 [2天]
- 商店等级显示 + 商品稀有度标识 + 购买/刷新交互完善

### FE-14 装备套装UI [1天]
- 套装标识 + 激活状态 + 效果提示

### FE-15 卡组编辑UI [2天]
- 卡组编辑界面 + 拖拽排列 + 卡牌详情

---

## 依赖关系图

```
阶段1（P0）
├── BE-08 机制怪 ─────┐
├── BE-09 骰子效果 ───┤（并行）
└── FE-08 Boss HUD ←──┘ BE-08

阶段2（P1）
├── BE-10 英雄升级 ─────→ FE-10 升级UI
├── BE-11 肉鸽地图 ─────→ FE-09 地图UI
├── BE-12 随机事件 ─────→ FE-11 事件UI
├── BE-13 背包重构 ─────→ FE-12 背包UI
（后端4个任务可并行，前端依赖后端完成）

阶段3（P2）
├── BE-14 商店等级 ─────→ FE-13 商店UI
├── BE-15 装备套装 ─────→ FE-14 套装UI
├── BE-16 卡牌引擎 ─────→ FE-15 卡组UI
（可与阶段2后半段并行启动）
```

---

## 并行推进甘特图（简化）

```
Week 1:
  后端: [BE-08====][BE-10===]
  前端:          [FE-08==]

Week 2:
  后端: [BE-09===][BE-11====]
  前端: [FE-10==][FE-09===]

Week 3:
  后端: [BE-12==][BE-13===][BE-14==]
  前端: [FE-11=][FE-12===][FE-13==]

Week 4:
  后端: [BE-15==][BE-16===]
  前端: [FE-14=][FE-15==]
```

---

## CTO建议

1. **阶段1立即启动** — BE-08和BE-09后端并行开工，这是核心循环断裂点
2. **WebGL空构建不影响** — 功能补全是C#逻辑层，与WebGL构建管线互不阻塞
3. **阶段3可与阶段2后半段并行** — 后端资源足够时提前启动BE-14/15
4. **每个阶段完成后做一次全流程联调** — 避免积累集成问题
