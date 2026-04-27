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
胜利 → 获得奖励卡牌/装备/金币 → 随机事件/商店 → 进入下一关
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
| Positioning | 站位调整 |
| Battle | 自动战斗 |
| Settlement | 结算阶段 |
| GameOver | 游戏结束 |

状态转换由 `NextState()` 自动按顺序推进，也支持跳过 (如 SkipToBattle)。

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
- 复活次数: `ReviveCount`

**CardInstance** 是运行时实例，支持：
- 升星 (`Merge()`): 1星→2星→3星
- 组合检查 (`HasComboBonus()`): 检查骰子组合是否触发卡牌联动

**21张卡牌**
- 5张属性卡: 力量训练、坚固护甲、灵敏训练、神圣祝福、召唤强化
- 15张战斗卡: 斩击、重摇、护盾冲击、寻找弱点、火焰斩、冰霜护甲、疾风步、致命一击、火球术、连环斩、吸血攻击、毒刃、能量爆发、破甲攻击、群体治疗、闪电链、荊棘反伤、狂暴药水、护盾共振
- 1张进化卡: 进化觉醒

### 2.4 英雄系统 (Hero System)

**Hero** 继承 MonoBehaviour，是占场上的实体：
- 基础属性: 生命/攻击/防御/速度/暴击率
- 星级倍率: 1星=1x, 2星=1.5x, 3星=2.2x
- 进化: `Evolve()` 切换到 `evolutionForm` 数据
- 战斗属性: `BattleAttack`/`BattleDefense`/`BattleCritRate` (受骰子+站位+卡牌影响)
- 装备槽: 武器/防具/饰品

**5个基础英雄 + 5个进化形态**
| 基础 | 进化 | 特色 |
|------|------|------|
| 坦克 | 链甲使者 | 高防高血，护盾反弹 |
| 射手 | 巡游射手 | 远程输出，穿透敌阵 |
| 刺客 | 影舞者 | 高速暴发，闪避背刺必暴 |
| 法师 | 大法师 | AOE法术，场外伤害+25% |
| 战士 | 狂战士 | 攻防兼备，狂暴斩 |

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
- 连携技: 战斗开始时自动检测英雄职业组合并施加Buff

**AutoChessAI** 控制单位行动：
1. 治疗者AI: 优先治疗血量最低的友方
2. 刺客AI: 优先攻击血量最低的敌人
3. Boss AI: 优先攻击输出最高的我方英雄
4. 普通单位: 寻找距离最近的敌人

### 2.7 装备系统 (Equipment System)

**EquipmentManager** 管理装备生成和掉落：
- 装备槽: 武器(攻击)/防具(防御+生命)/饰品(随机属性)
- 品质: 白/蓝/紫/金
- 掉落: 每3关必掉，其他关卡有30%概率
- 属性: 攻击/防御/生命/速度/暴击率

### 2.8 商店系统 (Shop System)

**ShopManager** 管理战关商店：
- 商品: 装备 + 卡牌
- 折扣: 20%概率五折
- 货品数量随关卡推进增加

**PlayerInventory** 管理玩家资源：
- 金币
- 装备背包
- 卡牌背包
- 装备/卸装操作

### 2.9 随机事件 (Random Events)

**RandomEventSystem** 战斗间随机触发 (30%概率)：
| 事件 | 效果 |
|------|------|
| 宝箱 | 获得金币 |
| 陷阱 | 全体扣血 |
| 神秘商人 | 商店5折 |
| 古老祭坛 | 全体永久+1攻击 |
| 流浪医者 | 全体回血 |
| 竞技场 | 额外战斗+金币 |

### 2.10 连携技系统 (Synergy System)

根据场上英雄职业组合自动触发：
| 连携技 | 条件 | 效果 |
|------|------|------|
| 前排铁壁 | 2+坦克 | 所有坦克防御+30% |
| 远程火力 | 2+射手 | 所有射手攻击+20% |
| 暗影突袭 | 2+刺客 | 所有刺客暴击率+15% |
| 均衡阵容 | 每职业至少1个 | 全体全属性+10% |
| 狂战之心 | 场上有狂战士 | 全体攻击+10% |

### 2.11 UI系统 (UI System)

**UIManager** 管理所有界面面板，自动跟随GameState切换：

| 界面 | 对应状态 | 主要功能 |
|------|----------|--------|
| MainMenuUI | MainMenu | 开始游戏/退出 |
| HeroSelectUI | HeroSelect | 选择初始英雄 |
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

### 3.3 装备数据 (EquipmentData)

| 字段 | 类型 | 说明 |
|------|------|------|
| equipmentName | string | 名称 |
| slot | EquipmentSlot | 武器/防具/饰品 |
| rarity | CardRarity | 品质 |
| attackBonus | int | 攻击加成 |
| defenseBonus | int | 防御加成 |
| healthBonus | int | 生命加成 |
| speedBonus | int | 速度加成 |
| critRateBonus | float | 暴击率加成 |

### 3.4 数值公式

- 关卡难度: `1 + (关卡-1) * 0.15`
- 星级倍率: 1星=1x, 2星=1.5x, 3星=2.2x
- 伤害: `攻击 * 倍率 * 暴击倍率 - 防御` (最低1点)
- 治疗: `基础治疗量 * 倍率`

---

## 4. 关卡设计 (15关)

| 关卡 | 敌人配置 | 奖励 |
|------|----------|------|
| 1 | 小怪×2 | 力量训练 |
| 2 | 小怪×3 | 坚固护甲 |
| 3 | 精英 | 灵敏训练 |
| 4 | 小怪×2+精英 | 斩击 |
| 5 | 自爆怪×2+治疗者 | 重摇 |
| 6 | 护盾怪×2+精英 | 护盾冲击 |
| 7 | 分裂怪×2+小怪 | 火焰斩 |
| 8 | 精英×2+治疗者 | 冰霜护甲 |
| 9 | Boss+小怪×2 | 进化觉醒 |
| 10 | Boss×2+治疗者 | 复活术 |
| 11 | 诅咒巫师+小怪×2 | 破甲攻击 |
| 12 | 重装骑士+精英 | 荊棘反伤 |
| 13 | 毒液蜘蛛×2+治疗者 | 群体治疗 |
| 14 | Boss+诅咒巫师+重装骑士 | 闪电链 |
| 15 | Boss×2+毒液蜘蛛+治疗者 | 护盾共振 |

---

## 5. 敌人设计 (11种)

| 敌人 | 特点 |
|------|------|
| 小怪 | 普通近战 |
| 精英 | 远程+技能 |
| Boss | 高血高防+AOE |
| 自爆怪 | 死亡时AOE伤害 |
| 治疗者 | 每回合治疗友方 |
| 护盾怪 | 高防御+62a4盾 |
| 分裂怪 | 死亡时分裂 |
| 隐身怪 | 高速+闪避 |
| 诅咒巫师 | Debuff型 |
| 重装骑士 | 极高防御+生命 |
| 毒液蜘蛛 | 中毒效果 |

---

## 6. 文件结构

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
│   │   ├── AutoChessAI.cs
│   │   ├── DamagePopup.cs
│   │   └── SynergySystem.cs
│   ├── UI/
│   │   ├── UIManager.cs
│   │   ├── MainMenuUI.cs
│   │   ├── HeroSelectUI.cs
│   │   ├── DiceRollUI.cs
│   │   ├── CardPlayUI.cs
│   │   ├── BattleUI.cs
│   │   └── SettlementUI.cs
│   ├── Data/
│   │   └── GameBalance.cs
│   │   └── GameData.cs
│   ├── Equipment/
│   │   ├── EquipmentSlot.cs
│   │   ├── EquipmentData.cs
│   │   └── EquipmentManager.cs
│   ├── Shop/
│   │   └── ShopManager.cs
│   ├── Player/
│   │   └── PlayerInventory.cs
│   ├── Events/
│   │   └── RandomEventSystem.cs
│   ├── Tests/
│   │   └── IntegrationTest.cs
│   └── GameManager.cs
├── Resources/
├── Scenes/
└── docs/
    ├── planning/
    │   ├── GDD.md
    │   ├── MVP.md
    │   ├── CORE_GAMEPLAY_LOOP.md
    │   └── GAME_DESIGN.md
    ├── management/
    │   └── WORKFLOW.md
    └── versions/
        └── CHANGELOG.md
```

---

## 7. 技术注意事项

- **无局外Meta**: MVP单局即终，无存档、无天赋树
- **无音效美术**: 用程序临时占位符和颜色块代替
- **SO资源**: 所有配置数据均通过 `ScriptableObject` 存储，默认数据由 `GameData.cs` 代码生成
- **标准UGUI**: 所有UI使用Unity原生UGUI，无第三方库
