# GameSXCG — 游戏设计文档 (GDD)

> 核心循环: **Dice-Driven Roguelike Auto-Battler**
> 
> 玩家通过掷骰子获得点数，消耗点数召唤英雄、打出卡牌，然后观看自走棋自动战斗。

---

## 1. 核心循环 (Core Loop)

```
[选择初始英雄 + 初始卡组] → 第1关
    ↓
[骰子阶段] 掷3个六面骰 → 重摇至多2次 → 获得组合加成
    ↓
[出牌阶段] 召唤英雄(消耗点数) → 打属性卡(本局永久) → 打战斗卡(本场临时) → 卡牌合成
    ↓
[站位阶段] 在3×4棋盘上拖拽调整英雄位置(前/中/后排效果)
    ↓
[战斗阶段] 自走棋AI自动战斗 → 玩家可加速/跳过
    ↓
胜利 → 获得奖励卡牌 → 进入下一关
失败 → 结束本局
```

---

## 2. 系统架构

### 2.1 状态机 (GameStateMachine)

管理游戏的全局状态流转，支持以下状态：

| 状态 | 说明 |
|------|------|
| MainMenu | 主菜单 |
| HeroSelect | 选择初始英雄 |
| DiceRoll | 骰子投掷与重摇 |
| CardPlay | 出牌阶段 |
| Positioning | 站位调整 (与CardPlay共用同一UI) |
| Battle | 自动战斗 |
| Settlement | 结算阶段 |
| GameOver | 游戏结束 |

状态转换由 `NextState()` 自动按顺序推进，也支持跳迆 (如 SkipToBattle)。

### 2.2 骰子系统 (Dice System)

**DiceRoller** 管理3个六面骰子的投掷与重摇：
- `RollAll()`: 投掷所有骰子
- `Reroll(keepMask)`: 按保留面具重摇
- `RerollAll()`: 全部重摇
- `GetCurrentCombination()`: 评估当前组合

**DiceCombinationEvaluator** 评估组合类型：
| 组合 | 条件 | 效果 |
|------|------|------|
| ThreeOfAKind | 三个相同 | 全体攻击+20% |
| Straight | 连续数字 | 全体攻速+20% |
| Pair | 两个相同 | 单点数字匹配召唤消耗时该英雄暴击+15% |

### 2.3 卡牌系统 (Card System)

**CardDeck** 管理手牌、场上英雄、属性累积：
- 手牌区: `handCards`
- 场上区: `fieldHeroes`
- 人口上限: `maxPopulation = 3`
- 属性累积: `BonusAttack/BonusDefense/BonusSpeed` (本局永久)

**CardInstance** 是运行时实例，支持：
- 升星 (`Merge()`): 1星→2星→3星
- 组合检查 (`HasComboBonus()`): 检查骰子组合是否触发卡牌联动

### 2.4 英雄系统 (Hero System)

**Hero** 继承 MonoBehaviour，是占场上的实体：
- 基础属性: 生命/攻击/防御/速度/暴击率
- 星级信数: 1星=1x, 2星=1.5x, 3星=2x
- 进化: `Evolve()` 切换到 `evolutionForm` 数据
- 战斗属性: `BattleAttack`/`BattleDefense`/`BattleCritRate` (受骰子+站位+卡牌影响)

### 2.5 棋盘系统 (Grid System)

**GridManager** 管理3×4棋盘：
- 行分区: y=0~1 前排, y=2 中排, y=3 后排
- 站位效果: 坦克前排防御+30%, 射手后排攻击+20%
- 寻敌: `FindNearestEnemy()` 近似距离计算

### 2.6 战斗系统 (Battle System)

**BattleManager** 管理自动战斗循环：
- 行动间隔: 0.5秒/次 (可加速最高4x)
- 超时: 30秒后敌方胜利
- 胜负判定: 任意一方全灭即结束

**AutoChessAI** 控制单位行动：
1. 寻找最近敌人
2. 移动到攻击范围内 (根据速度)
3. 攻击/释放技能

### 2.7 UI系统 (UI System)

**UIManager** 管理所有界面面板，自动跟随GameState切换：

| 界面 | 对应状态 | 主要功能 |
|------|----------|--------|
| MainMenuUI | MainMenu | 开始游戏/退出 |
| HeroSelectUI | HeroSelect | 三选一初始英雄 |
| DiceRollUI | DiceRoll | 投骰/重摇/显示组合 |
| CardPlayUI | CardPlay + Positioning | 手牌/召唤/棋盘站位 |
| BattleUI | Battle | 单位血条/战斗日志/加速 |
| SettlementUI | Settlement + GameOver | 胜利失败/奖励选择 |

---

## 3. 数据定义

### 3.1 英雄数据 (HeroData)

| 字段 | 类型 | 说明 |
|------|------|------|
| heroName | string | 名称 |
| heroClass | HeroClass | 职业 |
| baseHealth | int | 基础生命 |
| baseAttack | int | 基础攻击 |
| baseDefense | int | 基础防御 |
| baseSpeed | int | 基础速度 |
| baseCritRate | float | 基础暴击率 |
| summonCost | int | 召唤点数消耗 |
| normalAttack | SkillData | 普攻 |
| activeSkill | SkillData | 主动技能 |
| evolutionForm | HeroData | 进化形态 (可空) |

### 3.2 卡牌数据 (CardData)

| 字段 | 类型 | 说明 |
|------|------|------|
| cardName | string | 名称 |
| cardType | CardType | Hero/Attribute/Battle/Evolution |
| rarity | CardRarity | 白/蓝/紫/金 |
| effectId | CardEffectId | 效果类型 |
| cost | int | 点数消耗 |
| effectValue | int | 效果值 |
| requiredCombo | DiceCombinationType | 骰子联动组合 (可None) |
| comboMultiplier | float | 联动倍率 |

### 3.3 MVP数据清单

**英雄 (3个)**
- 坦克: 高防高血, 护盾反弹, 召唤2点
- 射手: 远程输出, 越远越痛, 召唤2点
- 刺客: 爆发突进, 闪避背刺, 召唤1点

**敌人模板 (3种)**
- 小怪: 普通近战
- 精英: 远程+技能
- Boss: 高血高防+AOE

**卡牌 (8张)**
- 3张属性卡 (力量/坚固/灵敏)
- 4张战斗卡 (斩击/重摇/护盾冲击/寻找弱点)
- 1张进化卡 (进化觉醒)

---

## 4. 文件结构

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── GameStateMachine.cs
│   ├── Dice/
│   │   ├── Dice.cs
│   │   ├── DiceRoller.cs
│   │   ├── DiceCombination.cs
│   │   └── DiceCombinationEvaluator.cs
│   ├── Heroes/
│   │   ├── Hero.cs
│   │   ├── HeroData.cs
│   │   └── SkillData.cs
│   ├── Cards/
│   │   ├── CardData.cs
│   │   ├── CardInstance.cs
│   │   └── CardDeck.cs
│   ├── Grid/
│   │   ├── GridManager.cs
│   │   └── GridCell.cs
│   ├── Battle/
│   │   ├── BattleManager.cs
│   │   └── AutoChessAI.cs
│   ├── UI/
│   │   ├── UIManager.cs
│   │   ├── MainMenuUI.cs
│   │   ├── HeroSelectUI.cs
│   │   ├── DiceRollUI.cs
│   │   ├── CardPlayUI.cs
│   │   ├── BattleUI.cs
│   │   └── SettlementUI.cs
│   ├── Data/
│   │   └── GameData.cs
│   ├── Tests/
│   │   └── IntegrationTest.cs
│   └── GameManager.cs
├── Resources/
│   └── Data/     (待创建ScriptableObject资源)
├── Scenes/
│   └── MainScene.unity
└── docs/planning/
    ├── GDD.md
    └── MVP.md
```

---

## 5. 技术注意事项

- **无局外Meta**: MVP单局即终，无存档、无天赋树
- **无音效美术**: 用程序临时占位符和颜色块代替
- **SO资源**: 所有配置数据均通过 `ScriptableObject` 存储，默认数据由 `GameData.cs` 代码生成
- **标准UGUI**: 所有UI使用Unity原生UGUI，无第三方库
