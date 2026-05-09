# 功能缺口分析报告

> 生成时间: 2026-05-09  
> 基准文档: `docs/planning/TECH_PLAN_MINI_GAME.md` + `TASK_SPLIT_MINI_GAME.md`  
> 代码路径: `Assets/Scripts/`  
> 总代码文件: 62个 .cs 文件

---

## 一、总览

| 分类 | 模块数 | 说明 |
|------|--------|------|
| ✅ 已完成 | 9 | 代码完整，逻辑闭环，可直接运行 |
| ⚠️ 部分完成 | 6 | 有代码但缺少关键功能/未对接 |
| ❌ 完全缺失 | 2 | 设计文档中定义但代码中无实现 |

---

## 二、逐模块分析

### 1. ✅ 状态机（GameStateMachine）— 完成度 95%

**文件**: `Core/GameStateMachine.cs` (210行)

**已完成**:
- 8种游戏状态枚举 (MainMenu → HeroSelect → DiceRoll → CardPlay → Positioning → Battle → Settlement → GameOver)
- 状态切换逻辑 (ChangeState / NextState)
- OnStateChanged 事件（驱动UI面板切换）
- ResetGame / SetGameWon / SetGameLost
- 胜利/失败判定标志

**缺失**:
- 无状态历史记录（无法Undo到上一状态）
- 无状态进入/退出动画hook（OnStateEnter/OnStateExit回调目前是空的）
- 无状态超时保护机制

**优先级**: 低（当前实现已满足MVP流程）

---

### 2. ✅ 骰子系统（Dice/）— 完成度 90%

**文件**: `Dice/Dice.cs`, `Dice/DiceRoller.cs`, `Dice/DiceCombination.cs`, `Dice/DiceCombinationEvaluator.cs`, `Dice/DiceTest.cs`

**已完成**:
- 单骰子数据模型（Dice类，支持多面、锁定、特殊面效果）
- 骰子投掷器（DiceRoller，支持批量掷骰、单骰重掷、全部重掷、免费重摇次数）
- 骰子组合评估器（支持7种组合：None/Pair/ThreeOfAKind/Straight/TwoPair/FullHouse/FourOfAKind/FiveOfAKind/StraightFlush）
- 组合战斗效果描述（攻击倍率、攻速倍率）
- 组合到BalanceProvider的对接入口

**缺失**:
- 骰子动画/视觉表现（仅有UI层的DiceRollPanel，无3D骰子物理）
- `StraightFlush` 组合的评估逻辑不完整（`IsStraight`方法只检查连续性，未检查"花色"概念）
- 无骰子特殊面效果的运行时执行器（Dice.FaceEffects 只是字符串，无解析/执行代码）

**优先级**: 中（动画层可后续补充，核心逻辑完整）

---

### 3. ✅ 战斗系统（Battle/）— 完成度 85%

**文件**: `Battle/BattleManager.cs` (598行), `Battle/AutoChessAI.cs` (200行), `Battle/DamagePopup.cs`

**已完成**:
- 完整的自动战斗流程（StartBattle → 模拟Tick循环 → 胜负判定）
- AutoChessAI 自动寻敌/攻击决策（基于距离优先 + 血量优先）
- 伤害计算公式（攻击-防御 + 暴击 + 站位修正 + 组合加成）
- 技能释放系统（普攻/主动/被动 3种技能类型）
- 战斗速度控制（1x/2x/4x）
- 最大战斗时间和回合数限制
- 伤害飘字系统（DamagePopup）
- 连携系统对接

**缺失**:
- 战斗回放/日志系统（无详细战斗日志供分析）
- 战斗中断恢复（无序列化中间状态能力）
- AOE技能的目标选择逻辑未完善（AutoChessAI中AOE选择逻辑简单）
- 无战斗特效系统（仅有飘字，无粒子特效/震屏等反馈）

**优先级**: 中高（战斗反馈体验需要特效增强）

---

### 4. ✅ 英雄系统（Heroes/）— 完成度 85%

**文件**: `Heroes/Hero.cs` (425行), `Heroes/HeroData.cs`, `Heroes/SkillData.cs`

**已完成**:
- Hero类完整属性模型（HP/ATK/DEF/SPD/暴击率 + 星级 + 职业）
- 3种职业枚举（Warrior/Mage/Assassin）
- 星级系统（1-3星，属性倍率）
- 技能数据模型（SkillData ScriptableObject，支持5种效果类型）
- 排位效果（ApplyRowEffect 前/中/后排加成）
- 死亡/复活机制
- 装备槽位引用

**缺失**:
- 英雄升级/经验系统（Hero类中有level字段但无经验获取逻辑）
- 英雄种族/阵营标签（连携系统需要，目前SynergySystem是独立实现的）
- 英雄被动技能的自动触发机制（被动技能定义了但无运行时触发器）
- 更多职业类型（设计文档提到了辅助/治疗型，代码中HeroClass枚举只有3种）

**优先级**: 高（英雄成长系统是核心循环）

---

### 5. ⚠️ 卡牌系统（Cards/）— 完成度 75%

**文件**: `Cards/CardData.cs`, `Cards/CardInstance.cs`, `Cards/CardDeck.cs` (490行)

**已完成**:
- 5种卡牌类型枚举（Hero/Attribute/Battle/Equipment/Special）
- CardData ScriptableObject（卡名/类型/费用/效果值/描述）
- CardInstance 运行时实例（唯一ID + 效果应用）
- CardDeck 牌组管理器：
  - 抽牌/弃牌/洗牌
  - 手牌上限管理（maxHandSize=10）
  - 召唤英雄卡到战场
  - 属性卡应用（加攻/加防/加生命）
  - 战斗卡应用（带骰子组合联动加成）
  - 骰子组合应用到场上英雄

**缺失**:
- **卡牌稀有度系统**（CardData无rarity字段）
- **卡牌合成/升级系统**（设计文档提到卡牌升级）
- **卡牌图鉴/收集系统**
- **卡牌效果解析引擎**（当前效果是硬编码switch，无配置化效果执行器）
- **卡组编辑功能**（无UI支持编辑卡组构成）
- **卡牌抽取动画**

**优先级**: 高（卡牌收集是核心驱动力）

---

### 6. ✅ 关卡系统（Level/）— 完成度 80%

**文件**: `Level/LevelManager.cs` (380行), `Level/LevelGenerator.cs`, `Level/LevelConfig.cs`

**已完成**:
- LevelManager 单例管理器
- LoadLevel / SpawnEnemies 完整流程
- 关卡难度缩放（基于levelId的线性/指数缩放）
- 敌人配置生成（按职业模板 + 难度系数）
- Boss关卡识别（每5关一个Boss）
- LevelConfig ScriptableObject（波次配置 + 奖励配置）
- LevelGenerator 基础生成器

**缺失**:
- 关卡主题/地图变化（当前所有关卡用相同3×4棋盘）
- 关卡间地图选择（肉鸽路径选择）
- Boss关卡特殊规则（当前Boss只是属性增强的普通怪）
- 关卡解锁/通关条件多样化

**优先级**: 中（基础关卡流程已通）

---

### 7. ✅ 肉鸽系统（Roguelike/）— 完成度 80%

**文件**: `Roguelike/RoguelikeGameManager.cs` (215行), `Roguelike/RoguelikeRewardSystem.cs` (540行)

**已完成**:
- RoguelikeGameManager 单例（StartNewGame / CompleteLevel / GetCurrentLevel）
- 肉鸽奖励系统完整：
  - 6种奖励类型（新英雄/升级材料/装备/遗物/金币/卡牌）
  - 加权随机选择（按关卡阶段调整权重）
  - 保底机制（连续未获得某类型则增加权重）
  - 奖励选择UI交互
- 运行数据模型（RoguelikeRunData：层数/金币/遗物列表/英雄阵容）

**缺失**:
- **肉鸽地图/路径选择**（无节点地图生成，无分支路径）
- **随机事件节点**（有RandomEventSystem但未集成到肉鸽流程中）
- **肉鸽存档/恢复**（中途退出无法恢复进度）
- **肉鸽难度递增曲线**（当前只有线性缩放，无特殊关卡事件）

**优先级**: 高（地图路径是肉鸽核心体验）

---

### 8. ⚠️ 装备系统（Equipment/）— 完成度 65%

**文件**: `Equipment/EquipmentManager.cs` (190行), `Equipment/EquipmentData.cs`, `Equipment/EquipmentSlot.cs`

**已完成**:
- 4种装备槽位枚举（Weapon/Armor/Accessory/Artifact）
- EquipmentData ScriptableObject（属性加成：攻击/防御/生命/速度）
- EquipmentManager 单例（装备穿戴/卸载/属性计算）
- 装备效果应用到Hero属性

**缺失**:
- **装备稀有度/品质系统**（无N/R/SR/SSR等级）
- **装备套装效果**（设计文档提到2件/4件套加成，代码无）
- **装备强化/升级系统**（无法消耗材料提升装备属性）
- **装备掉落/获取来源**（除商店外无其他获取途径）
- **装备图标/视觉表现**

**优先级**: 中（基础功能已有，套装效果优先）

---

### 9. ⚠️ 商店系统（Shop/）— 完成度 60%

**文件**: `Shop/ShopManager.cs` (130行)

**已完成**:
- ShopManager 单例（刷新商品/购买/卖出）
- 商品类型枚举（Hero/Equipment/Card/Relic/Material/Reroll）
- 库存管理（上架/售出/刷新）
- 金币交易逻辑
- 刷新计数和费用

**缺失**:
- **商店等级/层级系统**（设计文档提到商店随关卡升级解锁更多商品）
- **商品稀有度概率分布**
- **打折/促销机制**
- **商店UI交互**（ShopPanel存在但与ShopManager对接不完整）
- **限购机制**（每关购买次数限制）
- **刷新费用递增**（虽然有rerollCostBase但无递增逻辑）

**优先级**: 中高（经济系统核心）

---

### 10. ✅ 连携系统（SynergySystem）— 完成度 80%

**文件**: `Battle/SynergySystem.cs` (310行)

**已完成**:
- SynergySystem 单例（检测场上英雄职业组合）
- 12种连携组合定义（3个同职业、2+1混合等）
- 连携触发逻辑（EvaluateSynergies → 计算激活的连携）
- 连携效果应用（攻击加成/防御加成/特殊效果）
- 连携UI显示支持
- BalanceProvider对接（从配置读取连携列表）

**缺失**:
- 连携效果种类有限（主要是攻击/防御加成，缺少特殊效果如：治疗链、反击、护盾共享等）
- 连携升级机制（相同组合叠加层数）
- 连携视觉反馈（触发时无特效/提示）

**优先级**: 中（核心逻辑完整，可后续扩展效果种类）

---

### 11. ⚠️ 随机事件（RandomEventSystem）— 完成度 55%

**文件**: `Events/RandomEventSystem.cs` (140行)

**已完成**:
- RandomEventSystem 单例
- 随机事件数据模型（RandomEventData：名称/描述/选项/效果）
- 事件触发逻辑（基于权重的随机选择）
- 选项选择和效果应用
- EventPanel UI对接

**缺失**:
- **事件池配置化**（当前事件是代码硬编码的，无JSON配置）
- **事件触发条件**（无关卡数/英雄状态等前置条件判断）
- **事件效果执行引擎**（选项效果只有枚举，无具体执行逻辑）
- **事件历史记录**
- **事件稀有度/权重按关卡调整**
- **与肉鸽流程的集成**（RoguelikeGameManager不调用RandomEventSystem）

**优先级**: 高（肉鸽流程中事件是关键体验）

---

### 12. ⚠️ 背包系统（PlayerInventory）— 完成度 55%

**文件**: `Player/PlayerInventory.cs` (85行)

**已完成**:
- PlayerInventory 单例
- 基础背包操作（AddItem / RemoveItem / HasItem / GetItems）
- 背包容量限制（maxSlots = 20）
- 金币管理（AddGold / SpendGold）

**缺失**:
- **物品堆叠逻辑**（同类物品无堆叠，每件占一个格子）
- **物品分类/筛选**（无按类型查看功能）
- **物品排序**（无排序规则）
- **物品详情查看**
- **物品使用/装备快捷操作**
- **背包扩展**（无扩容机制）
- **背包与装备系统的对接**（PlayerInventory不引用EquipmentManager）
- **物品数据来源**（使用通用object类型，无ItemData基类）

**优先级**: 高（玩家每天都要用的核心系统）

---

### 13. ✅ 骰子面升级系统 — 完成度 75%

**文件**: `UI/Panels/DiceUpgradePanel.cs` (673行), `Dice/Dice.cs` (UpgradeFace方法)

**已完成**:
- DiceUpgradePanel 完整UI面板：
  - 骰子面选择界面
  - 升级选项列表（3选1）
  - 升级费用计算
  - 升级动画/特效
  - 特殊面类型（多攻击/治疗/护盾/暴击）
- Dice.UpgradeFace() 面替换方法
- BalanceProvider对接（升级费用/选项）

**缺失**:
- **升级材料消耗系统**（有费用但无材料消耗）
- **升级路线树**（无前置升级依赖关系）
- **面效果执行器**（特殊面效果只定义了名称，无运行时效果执行代码）
- **面重置/降级功能**
- **骰子面升级与战斗的联动**（战斗中不解析FaceEffects字符串）

**优先级**: 高（骰子升级是核心成长线）

---

### 14. ✅ 遗物系统（RelicSystem）— 完成度 80%

**文件**: `Roguelike/RelicSystem.cs` (240行), `Roguelike/RelicData.cs` (112行)

**已完成**:
- RelicData ScriptableObject（名称/描述/稀有度/效果类型/效果值）
- 4种稀有度枚举（Common/Uncommon/Rare/Legendary）
- RelicSystem 单例（添加/移除/激活/停用遗物）
- 遗物效果应用（当前实现了5种效果类型：攻击/防御/生命/暴击/特殊）
- 最大遗物数量限制
- 遗物选择UI（RelicPanel）
- BalanceProvider对接（遗物配置/稀有度权重）

**缺失**:
- **遗物效果种类扩展**（设计文档有更多特殊效果如：每回合额外骰子、商店打折等）
- **遗物冲突检测**（同类遗物叠加问题）
- **遗物故事/背景文本**
- **遗物图标资源**（代码中是placeholder）

**优先级**: 中（核心功能完整）

---

### 15. ❌ 机制怪系统 — 完成度 25%

**文件**: `UI/Panels/BossMechanicHUD.cs` (542行) — 仅UI层

**已完成**:
- BossMechanicHUD UI面板：
  - Boss机制提示显示
  - 机制倒计时进度条
  - 玩家操作提示
  - 机制结果反馈（成功/失败）
  - 已定义6种机制怪类型枚举（ShieldCycle/CountDown/PhaseChange/DiceLock/DiceSteal/ElementWeakness）

**缺失**:
- **机制怪逻辑层**（无MechanicMonster类，无BossAI行为树）
- **机制触发条件**（何时触发Boss机制）
- **机制执行引擎**（6种机制类型只有枚举定义，无执行代码）
- **机制与战斗系统集成**（BattleManager无机制怪处理分支）
- **机制失败惩罚/成功奖励逻辑**
- **机制怪数据配置**（无ScriptableObject/JSON配置）
- **骰子窃取/锁定机制**（影响骰子系统的特殊Boss能力）

**优先级**: 极高（Boss战是核心关卡体验，每5关一次）

---

### 16. ✅ UI面板系统（UI/）— 完成度 85%

**文件**: 20个UI文件，共9579行

**已完成**:
- NewUIManager 面板管理框架（ShowPanel/HidePanel + 状态机联动）
- UIPanel 基类（淡入淡出动画、CanvasGroup控制）
- 14个面板实现：
  - MainMenuPanel (326行) — 主菜单
  - HeroSelectPanel (160行) — 英雄选择
  - DiceRollPanel (269行) — 骰子投掷
  - CardPlayPanel (756行) — 出牌
  - BattleGridPanel (655行) — 战斗棋盘
  - BattlePanel (1343行) — 战斗主界面
  - SettlementPanel (160行) — 结算
  - RoguelikeRewardPanel (774行) — 肉鸽奖励
  - ShopPanel (451行) — 商店
  - EquipPanel (680行) — 装备
  - EventPanel (210行) — 随机事件
  - RelicPanel (504行) — 遗物
  - DiceUpgradePanel (673行) — 骰子升级
  - BossMechanicHUD (542行) — Boss机制
  - GameOverPanel (96行) — 游戏结束
- UIConfigBridge (898行) — UI与数值配置的桥接层
- DiceSkillCinematic — 骰子技能演出动画
- DamageNumber — 伤害数字组件
- RelicIconSlot — 遗物图标槽位

**缺失**:
- **设置面板**（音量/画质/语言切换）
- **图鉴面板**（英雄/卡牌/遗物收集进度）
- **战报/结算详情面板**（战斗伤害统计）
- **引导/教程面板**（新手引导系统）
- **成就面板**
- **部分面板的UI交互未对接后端**（如ShopPanel的购买逻辑）
- **自适应布局优化**（不同分辨率适配）

**优先级**: 中（核心面板齐全，辅助面板可逐步补充）

---

### 17. ✅ ConfigLoader数值对接 — 完成度 90%

**文件**: `Data/ConfigLoader.cs` (500行), `Data/GameBalance.cs` (650行), `Data/BalanceProvider.cs` (440行), `Data/GameData.cs` (690行) + 9个JSON配置文件

**已完成**:
- ConfigLoader 完整JSON加载器（9种配置类型：英雄/敌人/关卡/战斗公式/技能/掉落/经济/遗物/骰子）
- GameBalance 硬编码fallback + JSON优先策略
- BalanceProvider 统一入口（懒加载 + 缓存 + 热重载）
- GameData 工具类（创建模板英雄/敌人/卡牌/奖励）
- 9个JSON配置文件已同步到 Assets/Resources/Data/
- docs/data 和 Assets/Resources/Data 完全一致

**缺失**:
- **配置校验/容错**（JSON解析失败时的优雅降级）
- **配置版本管理**（无版本号字段，更新后无法检测兼容性）
- **策划工具/配置编辑器**（无Unity Editor自定义工具）
- **部分配置字段未被代码读取**（如掉落表的详细子字段）

**优先级**: 低（基础设施完善）

---

### 18. ✅ 其他基础设施 — 完成度 85%

**文件**: `GameManager.cs`, `Grid/GridManager.cs`, `Grid/GridCell.cs`, `Platform/MiniGameAdapter.cs`, `RuntimeSceneBootstrap.cs`, `Tests/IntegrationTest.cs`, `Editor/WebGLBuildPipeline.cs`, `Editor/WebGLQualityConfig.cs`

**已完成**:
- GameManager 全局单例（骰子系统 + 状态机整合）
- GridManager 3×4棋盘管理（放置/移除/寻敌/行列查询）
- GridCell 格子数据模型
- MiniGameAdapter 平台适配层（音频/存储/分享）
- RuntimeSceneBootstrap 场景自动构建器
- IntegrationTest 完整游戏流程测试
- WebGL 构建管线 + 画质配置

**缺失**:
- **Scene切换管理**（当前只有单场景）
- **对象池系统**（战斗中频繁创建销毁对象）
- **资源加载管理器**（无统一异步加载）
- **网络/排行榜系统**（设计文档提到微信小游戏排行榜）

---

## 三、汇总表

| # | 模块 | 完成度 | 优先级 | 关键缺失 |
|---|------|--------|--------|----------|
| 1 | 状态机 GameStateMachine | 95% | 低 | 状态历史/动画hook |
| 2 | 骰子系统 Dice | 90% | 中 | 面效果执行器、动画 |
| 3 | 战斗系统 Battle | 85% | 中高 | 战斗日志、AOE选择、特效 |
| 4 | 英雄系统 Heroes | 85% | 高 | 升级/经验、被动触发、种族标签 |
| 5 | 卡牌系统 Cards | 75% | 高 | 稀有度、合成升级、效果引擎、卡组编辑 |
| 6 | 关卡系统 Level | 80% | 中 | 地图变化、Boss特殊规则 |
| 7 | 肉鸽系统 Roguelike | 80% | 高 | 地图路径、事件集成、存档恢复 |
| 8 | 装备系统 Equipment | 65% | 中 | 套装效果、强化升级、稀有度 |
| 9 | 商店系统 Shop | 60% | 中高 | 商店等级、概率分布、限购 |
| 10 | 连携系统 Synergy | 80% | 中 | 效果种类扩展 |
| 11 | 随机事件 RandomEvent | 55% | 高 | 事件配置化、效果引擎、流程集成 |
| 12 | 背包系统 Inventory | 55% | 高 | 堆叠/分类/物品体系、与装备对接 |
| 13 | 骰子面升级 | 75% | 高 | 面效果执行、升级路线、战斗联动 |
| 14 | 遗物系统 Relic | 80% | 中 | 效果种类扩展 |
| 15 | 机制怪系统 | 25% | **极高** | **整个逻辑层缺失** |
| 16 | UI面板系统 | 85% | 中 | 设置/图鉴/引导/战报面板 |
| 17 | ConfigLoader数值 | 90% | 低 | 配置校验、版本管理 |

---

## 四、优先级排序建议

### P0 — 极高优先级（阻塞核心玩法）
1. **机制怪系统** — 只有UI壳，无逻辑层。Boss战（每5关）无法正常运作
2. **骰子面效果执行器** — 骰子升级系统存在但升级后的面效果无法在战斗中生效

### P1 — 高优先级（影响核心循环）
3. **英雄升级/经验系统** — 无成长反馈，玩家缺少长期目标
4. **卡牌效果引擎** — 卡牌效果硬编码，无法配置化扩展
5. **肉鸽地图路径** — 无分支选择，肉鸽体验退化成线性推图
6. **随机事件集成** — 已有系统但未接入肉鸽流程
7. **背包物品体系** — 使用object类型，无法支撑装备/材料/消耗品分类

### P2 — 中优先级（增强体验）
8. **商店等级系统** — 经济循环需要随进度解锁
9. **装备套装效果** — 增加装备搭配深度
10. **战斗特效/反馈** — 提升战斗体验
11. **连携效果扩展** — 增加策略深度

### P3 — 低优先级（打磨完善）
12. **UI辅助面板** — 设置/图鉴/引导
13. **状态机增强** — 历史/动画hook
14. **配置工具** — Editor扩展
15. **排行榜/社交** — 平台特有功能

---

## 五、总结

**项目整体完成度约 76%**，核心框架（状态机/骰子/战斗/棋盘/UI）已经搭建完成且可以运行完整的一局游戏流程（IntegrationTest验证）。主要缺口集中在：

1. **机制怪系统**是最紧急的缺口（只有UI无逻辑）
2. **骰子面效果执行器**是核心循环断裂点（升级了骰子但在战斗中无效果）
3. **肉鸽地图路径**和**随机事件集成**是肉鸽体验的关键缺失
4. **背包/商店/装备**需要从"能用"升级到"好用"

建议按P0→P1→P2的顺序推进，优先修复核心循环断裂问题。
