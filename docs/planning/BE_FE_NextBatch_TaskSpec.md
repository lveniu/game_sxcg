# 下一批任务书 — 集成收尾 + 体验打磨

> 生成时间: 2026-05-13
> CTO: @hermes-CTO
> 状态: 待分配
> 前置条件: BE-08~16 ✅ FE-08~15 ✅ FE-25~30 ✅ 全部完成

---

## 当前状态

整体完成度 **~92%**。P0/P1 后端任务全部交付，代码库干净无残留 TODO。

**本轮目标**: 从"功能完成"推向"可玩可测"——集成联调 + P2 体验增强 + Bug 修复

---

## 后端任务（@hermes-后端）

### BE-17 战斗回放数据采集 [1天]

**问题**: 战斗日志系统缺失，BattleStatsTracker 已有基础但无回放数据序列化

**任务**:
1. BattleStatsTracker 新增 `BattleSnapshot` 每回合快照（双方HP/护盾/Buff/触发的事件）
2. 战斗结束生成 `BattleReport`（总回合数/最大连击/伤害构成/治疗总量/机制触发统计）
3. BattleReport 序列化到 BattleStatsData
4. 接口: `BattleStatsTracker.GenerateReport() → BattleReport`

**产出文件**:
- `Assets/Scripts/Battle/BattleStatsTracker.cs`（增强）
- `Assets/Scripts/Battle/BattleReport.cs`（新增数据模型）

---

### BE-18 背包物品堆叠+分类完善 [1天]

**问题**: IItem 接口已有，但 PlayerInventory 的堆叠逻辑和分类筛选不完整

**任务**:
1. `PlayerInventory.AddItem()` 完善堆叠合并逻辑（同类物品自动堆叠至上限）
2. 新增 `GetItemsByType(ItemType)` 分类筛选接口
3. 新增 `SortBy(SortCriteria)` 排序接口（按类型/稀有度/名称/获取时间）
4. 背包容量限制完善（堆叠后按 slot 占用计数）

**产出文件**:
- `Assets/Scripts/Player/PlayerInventory.cs`（增强）
- `Assets/Scripts/Data/IItem.cs`（可能需要扩展接口）

---

### BE-19 连携效果扩展 + 英雄阵营标签 [1天]

**问题**: 连携系统只有职业组合，缺英雄种族/阵营维度，策略深度不足

**任务**:
1. HeroData 新增 `faction` 字段（阵营枚举: 人类/精灵/兽人/亡灵/机械）
2. SynergySystem 新增阵营连携检测逻辑（与职业连携并列）
3. 配置化阵营连携效果（2/3/4人激活）
4. `synergy_factions.json` 新配置文件

**产出文件**:
- `Assets/Scripts/Heroes/HeroData.cs`（扩展）
- `Assets/Scripts/Battle/SynergySystem.cs`（增强）
- `Assets/Resources/Data/synergy_factions.json`（新增）

**依赖**: 无

---

## 前端任务（@hermes-前端）

### FE-31 RoguelikeMapPanel 完整对接 [2天]

**问题**: 地图UI存在但与后端 RoguelikeMapSystem 的实时状态同步不完整

**任务**:
1. RoguelikeMapPanel 订阅 RoguelikeMapSystem 的节点状态变更事件
2. 当前节点实时高亮（位置+动画）
3. 可选节点脉动动画提示
4. 已完成节点灰化+连线变暗
5. 节点点击进入 → 调用 RoguelikeMapSystem.SelectNode()
6. Boss节点特殊样式（红色/闪烁/骷髅图标）

**依赖**: RoguelikeMapSystem（已完成）✅

---

### FE-32 战报详情面板 [1.5天]

**问题**: 战斗结束后无详细战报，玩家缺乏反馈

**任务**:
1. BattleReplaySummary 与 BattleReport（BE-17）对接
2. 伤害构成饼图（物理/元素/Buff/Combo加成）
3. 关键事件时间轴（Boss机制触发/暴击/连携激活）
4. 统计数据展示（总伤害/总治疗/最大单次伤害/连击数）
5. 分享按钮占位（后续微信分享）

**依赖**: BE-17（可先做UI壳，后端完成后对接数据）

---

### FE-33 战斗特效增强 [1.5天]

**问题**: 战斗观感平淡，缺震屏/特效反馈

**任务**:
1. 暴击震屏效果（Camera.DOShakePosition）
2. Boss机制触发全屏闪烁（红/蓝/紫色按机制类型）
3. 伤害数字优化（暴击放大+红色、治疗绿色、护盾蓝色）
4. 连携激活光效（从触发英雄到受益英雄的光束连线）
5. 使用 ObjectPoolManager 管理 DamageNumber 实例

**依赖**: ObjectPoolManager ✅ DamageNumber ✅

---

## 优先级排序

| 批次 | 任务 | 负责 | 优先级 | 依赖 |
|------|------|------|--------|------|
| 第一波 | BE-17 战报数据 | 后端 | P1 | 无 |
| 第一波 | BE-18 背包完善 | 后端 | P1 | 无 |
| 第一波 | FE-31 地图对接 | 前端 | P1 | 无 |
| 第二波 | BE-19 连携扩展 | 后端 | P2 | 无 |
| 第二波 | FE-32 战报面板 | 前端 | P1 | BE-17 |
| 第二波 | FE-33 战斗特效 | 前端 | P2 | 无 |

---

## 依赖关系图

```
第一波（可立即开工）:
├── BE-17 战报数据 ──────→ FE-32 战报面板
├── BE-18 背包完善（独立）
├── FE-31 地图对接（独立）
├── BE-19 连携扩展（独立）
└── FE-33 战斗特效（独立）
```

## 并行方案

```
后端线程: [BE-17=][BE-18=][BE-19=]
前端线程: [FE-31==][FE-32==][FE-33==]
```

预计总耗时: 后端3天，前端5天，并行约5天完成全部。
