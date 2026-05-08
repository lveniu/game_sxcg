# 微信小游戏版技术方案文档

> **版本**: v1.0  
> **日期**: 2026-05-08  
> **作者**: CTO  
> **状态**: 待评审  

---

## 目录

1. [现有代码复用评估](#1-现有代码复用评估)
2. [需要新增的系统](#2-需要新增的系统)
3. [需要改造的系统](#3-需要改造的系统)
4. [Unity转微信小游戏技术方案](#4-unity转微信小游戏的技术方案)
5. [开发排期](#5-开发排期)
6. [风险点](#6-风险点)

---

## 1. 现有代码复用评估

### 总览

| 系统 | 文件 | 代码行 | 复用判定 | 复用率 | 说明 |
|------|------|--------|----------|--------|------|
| GameStateMachine | GameStateMachine.cs | 198 | ⚠️ 需改造 | 60% | 砍掉CardPlay/Positioning状态，新增RoguelikeReward |
| 骰子系统 | DiceRoller/Evaluator/Dice/DiceCombination | 431 | ✅ 直接复用 | 90% | 仅改重摇次数2→1，核心逻辑完整 |
| 英雄系统 | Hero/HeroData/SkillData | 337 | ⚠️ 需改造 | 70% | 去掉进化/卡牌相关字段，简化为3职业 |
| 卡牌系统 | CardDeck/CardInstance/CardData | 558 | ❌ 砍掉 | 0% | 新玩法不使用卡牌，用肉鸽奖励替代 |
| 棋盘系统 | GridManager/GridCell | 230 | ⚠️ 需改造 | 50% | 简化或去除棋盘，新玩法不用手动站位 |
| 战斗系统 | BattleManager/AutoChessAI | 320 | ⚠️ 需改造 | 65% | 需加点触释放骰子技能、加速、跳过 |
| 装备系统 | EquipmentManager/EquipmentData | 192 | ⚠️ 需改造 | 40% | 简化融入肉鸽奖励，去掉商店购买 |
| 商店系统 | ShopManager | 117 | ❌ 砍掉 | 0% | 不再需要金币购买机制 |
| 随机事件 | RandomEventSystem | 121 | ❌ 砍掉 | 0% | 肉鸽奖励三选一完全替代 |
| 连携技系统 | SynergySystem | 104 | ⚠️ 需改造 | 50% | 保留框架，适配新肉鸽队伍构成 |
| GameBalance | GameBalance.cs | 214 | ✅ 直接复用 | 85% | 伤害公式/星级倍率/难度曲线核心可用 |
| GameData | GameData.cs | 558 | ⚠️ 需改造 | 60% | 保留英雄/敌人模板，去掉卡牌相关 |
| PlayerInventory | PlayerInventory.cs | 83 | ⚠️ 需改造 | 30% | 简化为遗物/骰子面持有管理 |
| UI系统 | UIManager/各UI面板 | ~400 | ❌ 重写 | 10% | 小游戏UI需完全重做 |

---

### 1.1 GameStateMachine（⚠️ 需改造，复用率60%）

**现有代码分析**：
- `GameState` 枚举有8个状态：MainMenu, HeroSelect, DiceRoll, CardPlay, Positioning, Battle, Settlement, GameOver
- `NextState()` 按 `MainMenu → HeroSelect → DiceRoll → CardPlay → Positioning → Battle → Settlement → 循环/GameOver` 流转
- 事件机制（`OnStateChanged`/`OnStateEntered`/`OnStateExited`）设计良好

**复用评估**：
- ✅ 事件驱动架构可直接复用
- ✅ 单例模式、状态切换核心逻辑可复用
- ❌ `CardPlay`（出牌阶段）需砍掉——新玩法没有卡牌
- ❌ `Positioning`（站位阶段）需砍掉——新玩法不用手动站位
- ➕ 需新增 `RoguelikeReward`（肉鸽三选一奖励）状态
- ➕ Settlement 需改造为判断英雄阵亡 → GameOver

**新状态流**：
```
MainMenu → HeroSelect → DiceRoll → Battle → Settlement → 
  ├→ 英雄存活 → RoguelikeReward → DiceRoll（下一关）
  └→ 英雄阵亡 → GameOver
```

**改造工作量**：1人天

---

### 1.2 骰子系统（✅ 直接复用，复用率90%）

**现有代码分析**：
- `DiceRoller`：3骰管理、投掷/重摇/组合评估、免费重摇次数=2、keepMask保留机制
- `DiceCombinationEvaluator`：支持3骰组合判定（三条/顺子/对子/两对/葫芦/四条/五条）
- `Dice`：单骰子模型，支持多面、特殊面效果（`FaceEffects`）、锁定
- `DiceCombination`：组合类型枚举+评估结果+效果描述

**复用评估**：
- ✅ `DiceRoller` 核心逻辑完全复用，仅 `FreeRerolls` 从2改为1（一行改动）
- ✅ `DiceCombinationEvaluator` 评估算法完全复用
- ✅ `Dice.UpgradeFace()` 方法已预留骰子面升级接口
- ✅ `DiceCombination` 的组合类型和效果描述可复用
- ✅ `DiceRoller.SetFreeRerolls()` 已支持动态修改重摇次数

**需改动**：
- `FreeRerolls` 默认值从2改为1（第15行）
- 后续肉鸽奖励可能增加额外重摇次数（`SetFreeRerolls()` 已支持）

**改造工作量**：0.5人天

---

### 1.3 英雄系统（⚠️ 需改造，复用率70%）

**现有代码分析**：
- `Hero`（267行）：MonoBehaviour，包含基础属性/战斗属性/卡牌特殊状态/装备/站位，支持初始化/升星/进化/伤害/治疗/骰子组合应用
- `HeroData`（39行）：ScriptableObject，5职业（Tank/Archer/Assassin）+ 5进化形态
- `SkillData`（31行）：技能数据（单体/AOE/自身，伤害/治疗/护盾/Buff/Debuff）

**复用评估**：
- ✅ `Hero` 核心属性系统（HP/ATK/DEF/SPD/暴击）完全复用
- ✅ `Hero.Initialize()`/`RecalculateStats()` 可复用
- ✅ `Hero.TakeDamage()`/`Heal()` 可复用
- ✅ `Hero.ApplyDiceCombination()` 可复用
- ✅ `SkillData` 技能类型系统完全复用
- ⚠️ 需砍掉卡牌特殊状态（HasFlameAOE/HasFrostSlow/PoisonBlade等约15个bool字段）
- ⚠️ 需砍掉 `Evolve()` 进化相关
- ⚠️ `HeroData` 从5职业精简为3职业（战士/法师/刺客），对应CEO确认的进场三选一
- ➕ 需新增遗物Buff持有列表
- ⚠️ `GridPosition`/`CurrentRow` 可能简化（如果不手动站位）

**新玩法中的英雄定位**：
- 进场固定1个英雄（战/法/刺三选一）
- 肉鸽奖励可获得新单位（作为"解题工具"）
- 英雄保留星级和属性成长

**改造工作量**：2人天

---

### 1.4 卡牌系统（❌ 砍掉，复用率0%）

**现有代码分析**：
- `CardDeck`（409行）：管理手牌、场上英雄、属性累积、2合1合成、战斗卡效果（18种CardEffectId）
- `CardInstance`（73行）：运行时卡牌实例，升星/联动检查
- `CardData`（76行）：卡牌类型/稀有度/效果/骰子联动配置

**砍掉原因**：
- CEO确认的新玩法循环中没有卡牌阶段
- 肉鸽三选一奖励完全替代了卡牌获取机制
- 不再有出牌、合成、骰子联动卡牌等操作
- 21张卡牌的18种战斗效果不再适用

**可提取复用的设计模式**：
- `CardEffectId` 枚举中的效果类型可参考，转化为骰子面效果或遗物效果
- `ApplyToAllHeroes()` 的批量操作模式可复用

**工作量**：0人天（直接标记deprecated，不删除避免编译错误）

---

### 1.5 棋盘系统（⚠️ 需改造，复用率50%）

**现有代码分析**：
- `GridManager`（214行）：3×4棋盘，支持放置/移除/寻敌/行排判定
- `GridCell`：格子模型

**复用评估**：
- ✅ `GridManager.FindNearestEnemy()` 寻敌逻辑可复用（AutoChessAI依赖）
- ✅ `GridManager.GetRow()` 行排判定可复用
- ⚠️ 新玩法不用手动站位（无Positioning阶段）
- ⚠️ 敌人自动放置，我方英雄可能简化为固定站位或自动站位
- ➕ 可能需要简化为1×N线性格局（左右排列）以适配手机竖屏

**建议**：
- 保留GridManager的核心数据结构（2D数组、格子管理）
- 去掉手动拖拽放置交互，改为自动站位
- 棋盘尺寸可能从3×4改为适配竖屏的布局

**改造工作量**：2人天

---

### 1.6 战斗系统（⚠️ 需改造，复用率65%）

**现有代码分析**：
- `BattleManager`（161行）：管理战斗循环、速度控制、胜负判定
- `AutoChessAI`（159行）：4种AI策略（治疗者/刺客/Boss/默认近战寻敌）

**复用评估**：
- ✅ `BattleManager.BattleLoop()` 协程框架完全复用
- ✅ `BattleManager.SetBattleSpeed()` 已有加速接口（0.5x~4x）
- ✅ `AutoChessAI.TakeAction()` 行动决策框架可复用
- ✅ `AutoChessAI.FindTarget()` 多策略寻敌完全复用
- ✅ `AutoChessAI.NormalAttack()`/`UseSkill()` 伤害计算可复用
- ✅ `BattleManager.CheckBattleEnd()` 胜负判定可复用
- ⚠️ 需新增"跳过战斗"功能（当前只有加速，没有直接结算）
- ⚠️ 需新增"点触释放骰子技能"机制
- ⚠️ 超时判定需调整（当前30s超时算敌方赢，需改为模拟结算）
- ❌ `SynergySystem.ApplySynergies()` 调用需改造（连携技条件变化）

**新增功能**：
1. **点触释放骰子技能**：战斗中，玩家点击骰子组合技能按钮释放一次性技能
2. **跳过战斗**：直接模拟计算战斗结果，跳过动画
3. **战斗结算简化**：单局2-3分钟，战斗阶段不超过30秒

**改造工作量**：4人天

---

### 1.7 装备系统（⚠️ 需改造，复用率40%）

**现有代码分析**：
- `EquipmentManager`（154行）：装备生成/掉落/商店货品
- `EquipmentData`（38行）：装备属性（武器/防具/饰品三槽位）
- `EquipmentSlot`（9行）：槽位枚举

**复用评估**：
- ✅ `EquipmentData` 数据结构可复用（名称/属性加成/稀有度）
- ✅ `Hero.Equip()`/`Unequip()` 装备逻辑可复用
- ⚠️ 不再有独立装备系统和商店购买
- ⚠️ 装备变为肉鸽奖励的一种类型
- ❌ `EquipmentManager.GetLevelDrops()`/`GenerateShopItems()` 砍掉
- ❌ 不再需要动态生成装备

**建议**：
- 将装备简化为肉鸽奖励的"属性强化"选项
- 保留 `EquipmentData` 结构用于遗物系统参考
- 或者直接砍掉装备，用遗物替代

**改造工作量**：1.5人天

---

### 1.8 商店系统（❌ 砍掉，复用率0%）

**砍掉原因**：
- 新玩法无金币系统
- 无商店购买环节
- 肉鸽三选一奖励完全替代

**工作量**：0人天

---

### 1.9 随机事件（❌ 砍掉，复用率0%）

**砍掉原因**：
- 6种随机事件（宝箱/陷阱/商人/祭坛/医者/竞技场）设计与肉鸽循环冲突
- 肉鸽三选一奖励已包含事件系统的正面效果
- 单局2-3分钟的节奏没有空间插入额外事件

**工作量**：0人天

---

### 1.10 连携技系统（⚠️ 需改造，复用率50%）

**现有代码分析**：
- `SynergySystem`（104行）：基于职业组合触发团队Buff（前排铁壁/远程火力/暗影突袭/均衡阵容/狂战之心）

**复用评估**：
- ✅ 职业计数+条件判断框架可复用
- ✅ 批量应用Buff的模式可复用
- ⚠️ 需适配新职业体系（3职业：战士/法师/刺客）
- ⚠️ 触发条件需调整（从固定组合改为更灵活的条件）
- ⚠️ 可能需要加入遗物触发的连携

**改造工作量**：1.5人天

---

## 2. 需要新增的系统

### 2.1 肉鸽奖励系统（RoguelikeRewardSystem）

**功能描述**：
- 通关后展示3个奖励选项，玩家选择1个
- 奖励池：新单位/骰子面升级/属性强化/遗物

**类设计**：
```csharp
// 奖励类型
public enum RewardType
{
    NewUnit,          // 新单位（解题工具）
    DiceFaceUpgrade,  // 骰子面升级
    StatBoost,        // 属性强化（全体+ATK/DEF/HP）
    Relic,            // 遗物（被动效果）
    ExtraReroll       // 额外重摇次数
}

// 奖励实例
public class RewardOption
{
    public RewardType type;
    public string name;
    public string description;
    public Sprite icon;
    public object data; // HeroData / DiceFaceUpgrade / StatBoost / RelicData
}

// 奖励系统
public class RoguelikeRewardSystem
{
    public RewardOption[] GenerateRewards(int levelId, int runProgress);
    public void ApplyReward(RewardOption reward, PlayerState state);
}
```

**核心逻辑**：
- 根据当前关卡数生成适配奖励
- 前10关偏向属性强化和新单位
- 10关后偏向骰子面升级和遗物（应对机制怪）
- 奖励权重系统：根据玩家当前阵容缺少什么，提高对应奖励出现概率

**工作量**：5人天

---

### 2.2 骰子面升级系统（DiceFaceUpgradeSystem）

**功能描述**：
- 每个骰子有6个面，每个面可以独立升级
- 升级效果：增加特殊效果（如治疗/护盾/暴击/闪避）
- 肉鸽奖励获得升级机会

**类设计**：
```csharp
// 骰子面升级数据
public class DiceFaceUpgrade
{
    public int faceIndex;      // 0-5，对应骰子6面
    public string effectId;    // 效果ID
    public float effectValue;  // 效果值
    public string description; // 效果描述
}

// 骰子面升级管理器
public class DiceFaceUpgradeSystem
{
    // 应用升级
    public void ApplyUpgrade(Dice dice, DiceFaceUpgrade upgrade);
    // 获取随机升级选项
    public DiceFaceUpgrade[] GetRandomUpgradeOptions(int level);
    // 战斗中触发骰子面效果
    public void TriggerFaceEffect(Dice dice, int faceValue, Hero hero, List<Hero> allies, List<Hero> enemies);
}
```

**与现有代码的关系**：
- `Dice.FaceEffects`（string[]）已预留面效果接口
- `Dice.UpgradeFace(faceIndex, effect)` 方法已实现
- 本系统在此基础上构建效果解析和触发逻辑

**骰子面效果列表**：

| 面值 | 可升级效果 | 触发时机 |
|------|-----------|---------|
| 1 | 治疗最低血量友方X% | 骰子结果含1时 |
| 2 | 全体护盾X点 | 骰子结果含2时 |
| 3 | 攻击最高敌人X%额外伤害 | 骰子结果含3时 |
| 4 | 全体攻速+X% | 骰子结果含4时 |
| 5 | 随机敌人眩晕1回合 | 骰子结果含5时 |
| 6 | 暴击率+X%（本场） | 骰子结果含6时 |

**工作量**：4人天

---

### 2.3 遗物系统（RelicSystem）

**功能描述**：
- 遗物是永久被动效果，贯穿整局游戏
- 通过肉鸽奖励获得
- 影响骰子/英雄/战斗/奖励等各方面

**类设计**：
```csharp
// 遗物数据
[CreateAssetMenu(fileName = "Relic", menuName = "Game/Relic")]
public class RelicData : ScriptableObject
{
    public string relicName;
    public string description;
    public Sprite icon;
    public RelicRarity rarity;
    public RelicTriggerType triggerType;
    public string effectId;
    public float effectValue;
}

public enum RelicRarity { Common, Rare, Epic, Legendary }
public enum RelicTriggerType
{
    Passive,          // 被动永久生效
    OnBattleStart,    // 战斗开始
    OnKill,           // 击杀敌人时
    OnDamageTaken,    // 受到伤害时
    OnDiceRoll,       // 掷骰子时
    OnRewardSelect,   // 选择奖励时
    OnLevelClear      // 通关时
}

// 遗物管理器
public class RelicSystem
{
    public List<RelicData> ownedRelics = new();
    
    public void AddRelic(RelicData relic);
    public void ApplyPassiveEffects(PlayerState state);
    public void TriggerRelics(RelicTriggerType trigger, params object[] args);
}
```

**遗物示例**：

| 遗物名 | 稀有度 | 效果 |
|--------|--------|------|
| 幸运硬币 | 普通 | 重摇后骰子结果+1（最大6） |
| 铁壁符文 | 普通 | 全体防御+15% |
| 吸血鬼之牙 | 稀有 | 全体吸血10% |
| 骰子大师 | 稀有 | 每关首次掷骰额外+1次重摇 |
| 不死鸟之羽 | 史诗 | 主角每关首次死亡复活50%血量 |
| 机制破解器 | 传说 | 对机制怪额外伤害30% |

**工作量**：4人天

---

### 2.4 机制怪系统（MechanicEnemySystem）

**功能描述**：
- 10关后出现特殊机制怪
- 机制怪有独特的被动机制，不是纯数值
- 需要特定阵容/策略应对

**类设计**：
```csharp
// 机制类型
public enum MechanicType
{
    ShieldSwap,      // 护盾互换（与最低血量友方互换护盾）
    DamageReflect,   // 伤害反弹（反弹30%受到的伤害）
    HealOnAttack,    // 攻击回血（每次攻击回复造成伤害的50%）
    SpawnMinions,    // 召唤小怪（每3回合召唤1个小怪）
    ElementalShift,  // 元素切换（每回合切换免疫的元素类型）
    TauntAndBuff,    // 嘲讽强化（嘲讽时防御递增）
    Berserk,         // 狂暴（血量越低攻击越高）
    TimeBomb,        // 定时炸弹（5回合后全体高额伤害）
    StealthAssassin, // 隐身刺杀（每2回合隐身+攻击最高攻击单位）
    CurseSpread      // 诅咒扩散（攻击附带诅咒，被诅咒单位每回合掉血）
}

// 机制怪数据
public class MechanicEnemyData
{
    public string enemyName;
    public MechanicType mechanic;
    public string mechanicDescription;
    public HeroData baseData;       // 基础数值模板
    public float mechanicStrength;   // 机制强度（随关卡递增）
}

// 机制怪管理器
public class MechanicEnemySystem
{
    // 根据关卡数生成机制怪
    public MechanicEnemyData GenerateMechanicEnemy(int levelId);
    // 在战斗中处理机制
    public void ProcessMechanic(Hero enemy, MechanicType type, List<Hero> allies, List<Hero> enemies);
}
```

**机制怪列表（10关后按顺序出现）**：

| 关卡 | 机制怪 | 机制描述 | 应对策略 |
|------|--------|----------|---------|
| 11 | 护盾守卫 | 每3回合与最低血量友方互换护盾 | 高爆发秒杀 |
| 12 | 反伤傀儡 | 反弹30%受到的伤害 | 吸血/多段低伤 |
| 13 | 分裂母体 | 死亡分裂为2个50%属性的副本 | AOE清场 |
| 14 | 狂暴巨兽 | 血量每降低25%，攻击+50% | 护盾/控制 |
| 15 | 召唤领主 | 每2回合召唤1个小怪 | 单体高伤快速击杀 |
| 16+ | 随机组合 | 随机选取2种机制组合 | 需要遗物+阵容配合 |

**工作量**：6人天

---

### 2.5 加速/跳过战斗系统

**功能描述**：
- 战斗中可加速（2x/4x）或直接跳过

**加速（已有基础）**：
- `BattleManager.SetBattleSpeed(float speed)` 已实现
- 需要增加UI按钮（1x/2x/4x切换）

**跳过战斗（需新增）**：
```csharp
// 在BattleManager中新增
public BattleResult SimulateBattle(List<Hero> players, List<Hero> enemies)
{
    // 快速模拟战斗（无动画、无协程，纯数值计算）
    int maxRounds = 100; // 防止死循环
    for (int round = 0; round < maxRounds; round++)
    {
        // 我方行动
        foreach (var unit in players.Where(u => !u.IsDead))
            AutoChessAI.TakeAction(unit, enemies, players);
        // 敌方行动
        foreach (var unit in enemies.Where(u => !u.IsDead))
            AutoChessAI.TakeAction(unit, players, enemies);
        // 清理死亡
        players.RemoveAll(u => u.IsDead);
        enemies.RemoveAll(u => u.IsDead);
        // 检查结束
        if (players.Count == 0 || enemies.Count == 0) break;
    }
    return new BattleResult { playerWon = enemies.Count == 0 };
}
```

**工作量**：2人天

---

### 2.6 小游戏适配层（MiniGameAdapter）

**功能描述**：
- 封装微信小游戏API调用
- 管理资源加载/卸载
- 处理平台差异

**类设计**：
```csharp
public class MiniGameAdapter : MonoBehaviour
{
    // 平台检测
    public static bool IsWeChatMiniGame => 
        Application.platform == RuntimePlatform.WebGLPlayer;
    
    // 音频管理（小游戏音频限制）
    public void PlayBGM(string clipName);
    public void PlaySFX(string clipName);
    
    // 存储（微信云存储/本地存储适配）
    public void SaveData(string key, string json);
    public string LoadData(string key);
    
    // 分享
    public void ShareGame(string title, string imageUrl);
    
    // 性能监控
    public void ReportFPS();
}
```

**工作量**：3人天

---

## 3. 需要改造的系统

### 3.1 状态机改造

**改动清单**：

```
GameState 枚举改造：
  ❌ 删除 CardPlay       → 新玩法无卡牌
  ❌ 删除 Positioning    → 新玩法无手动站位
  ➕ 新增 RoguelikeReward → 肉鸽三选一奖励阶段
  ➕ 可选新增 HeroRevive  → 英雄阵亡判定（或合并入Settlement）

NextState() 流转改造：
  MainMenu → HeroSelect → DiceRoll → Battle → Settlement →
    ├ 存活 → RoguelikeReward → DiceRoll（下一关）
    └ 阵亡 → GameOver
```

**具体改动**：

```csharp
// GameState 改造后
public enum GameState
{
    MainMenu,         // 主菜单
    HeroSelect,       // 选择初始英雄（战/法/刺三选一）
    DiceRoll,         // 骰子阶段（3骰+1次重摇）
    Battle,           // 自动战斗（可加速/跳过/点触释放骰子技能）
    Settlement,       // 战斗结算
    RoguelikeReward,  // 肉鸽三选一奖励
    GameOver          // 阵亡结束
}

// NextState() 改造
public void NextState()
{
    switch (currentState)
    {
        case GameState.MainMenu:
            ChangeState(GameState.HeroSelect);
            break;
        case GameState.HeroSelect:
            ChangeState(GameState.DiceRoll);
            break;
        case GameState.DiceRoll:
            ChangeState(GameState.Battle); // 跳过出牌和站位，直接进战斗
            break;
        case GameState.Battle:
            ChangeState(GameState.Settlement);
            break;
        case GameState.Settlement:
            if (IsGameLost)
                ChangeState(GameState.GameOver);
            else
                ChangeState(GameState.RoguelikeReward);
            break;
        case GameState.RoguelikeReward:
            CurrentLevel++;
            ChangeState(GameState.DiceRoll);
            break;
        case GameState.GameOver:
            ChangeState(GameState.MainMenu);
            break;
    }
}
```

**UIManager同步改造**：
- 去掉 `cardPlayPanel`
- 新增 `roguelikeRewardPanel`
- 状态-面板映射更新

**工作量**：1人天

---

### 3.2 战斗系统改造

**改动1：点触释放骰子技能**

```csharp
// BattleManager 新增
private DiceCombination currentDiceCombo;
private bool diceSkillUsed = false;

public void UseDiceSkill()
{
    if (diceSkillUsed || currentDiceCombo == null) return;
    diceSkillUsed = true;
    
    // 根据骰子组合类型释放不同技能
    switch (currentDiceCombo.Type)
    {
        case DiceCombinationType.ThreeOfAKind:
            // 全体大爆发（攻击+50%，持续3回合）
            ApplyToAllPlayers(h => h.BattleAttack = Mathf.RoundToInt(h.BattleAttack * 1.5f));
            break;
        case DiceCombinationType.Straight:
            // 全体加速+闪避
            ApplyToAllPlayers(h => { h.BattleAttackSpeed *= 1.5f; h.BattleDodgeRate += 0.3f; });
            break;
        case DiceCombinationType.Pair:
            // 单体高额爆发伤害（自动选择最强敌人）
            var target = AutoChessAI.FindTarget_STRONGEST(enemyUnits);
            if (target != null) target.TakeDamage(CalculateDiceDamage(currentDiceCombo));
            break;
    }
}
```

**改动2：跳过战斗**

在 `BattleManager` 中新增 `SkipBattle()` 方法，调用 `SimulateBattle()` 直接计算结果。

**改动3：战斗节奏优化**
- `maxBattleTime` 从30s调整为20s
- `battleTickInterval` 从0.5s调整为0.3s（更快节奏）
- 超时不再算敌方赢，改为按剩余血量百分比判定

**工作量**：4人天

---

### 3.3 关卡系统改造

**改动清单**：

```
LevelManager 改造：
  ❌ 删除固定关卡配置（levelConfigs列表 + CreateDefaultLevel的15关硬编码）
  ➕ 新增无限关卡生成算法
  ➕ 新增难度曲线系统
  ➕ 新增10关后机制怪生成
  ➕ 新增肉鸽奖励生成
```

**无限关卡生成**：

```csharp
public class LevelGenerator
{
    // 难度曲线：前10关纯数值递增
    public static float GetDifficultyMultiplier(int levelId)
    {
        // 前10关：线性增长 1.0 → 2.5
        if (levelId <= 10)
            return 1f + (levelId - 1) * 0.15f;
        // 10关后：增长放缓但有机制怪
        return 2.5f + (levelId - 10) * 0.08f;
    }
    
    // 生成敌人波次
    public static List<EnemyWave> GenerateEnemies(int levelId)
    {
        if (levelId <= 10)
            return GenerateNormalEnemies(levelId);
        else
            return GenerateMechanicEnemies(levelId);
    }
    
    // 前10关：纯数值递增的小怪+精英+Boss
    static List<EnemyWave> GenerateNormalEnemies(int levelId)
    {
        float diff = GetDifficultyMultiplier(levelId);
        int count = Mathf.Min(1 + levelId / 3, 4); // 1-4个敌人
        // 每5关出Boss
        // ...
    }
    
    // 10关后：含机制怪
    static List<EnemyWave> GenerateMechanicEnemies(int levelId)
    {
        float diff = GetDifficultyMultiplier(levelId);
        // 1个机制怪 + 若干小怪
        // 机制怪类型随关卡解锁
        // ...
    }
}
```

**工作量**：4人天

---

### 3.4 重摇次数改为1次

**改动极其简单**：

```csharp
// DiceRoller.cs 第15行
// 改前：
public int FreeRerolls { get; private set; } = 2;
// 改后：
public int FreeRerolls { get; private set; } = 1;
```

**后续扩展**：
- 遗物"骰子大师"可增加额外重摇
- 骰子面升级可能有"重摇面"，掷到时额外+1次重摇
- 这些通过 `SetFreeRerolls()` 动态调整

**工作量**：0.5人天

---

## 4. Unity转微信小游戏的技术方案

### 4.1 技术路线选型

**方案：Unity → WebGL → 微信小游戏**

```
Unity 2022.3 LTS 
  → Build Settings: WebGL
  → 微信小游戏 WebGL适配（minigame目录）
  → 微信开发者工具 → 上传/发布
```

**关键依赖**：
- 微信官方 Unity转小游戏插件（微信小游戏 Unity Adapter）
- Unity 2022.3 LTS 对 WebGL 支持良好
- 微信小游戏支持 WebGL 2.0

---

### 4.2 包体优化策略（首包4MB限制）

微信小游戏首包限制**4MB**（代码+资源），这是最大的技术挑战。

**优化策略**：

#### 4.2.1 代码精简（目标 < 1MB）

| 措施 | 预估节省 | 说明 |
|------|----------|------|
| IL2CPP + Code Stripping | ~40% | Unity 2022 默认支持 |
| 去掉未使用的Unity模块 | ~500KB | Physics 2D/3D、AI、NavMesh、Video 等不需要的模块 |
| 砍掉卡牌/商店/事件系统 | ~50KB | 减少编译产物 |
| 合并重复代码 | ~10KB | GameData中大量重复方法可优化 |

**具体操作**：
```
Project Settings → Player → Other Settings:
  - Managed Stripping Level: High
  - Strip Engine Code: ✅ (勾选)
  
Project Settings → Player → WebGL:
  - Code Generation: IL2CPP (更小更快)
  - Compression: Brotli (压缩率最高)
  - Data caching: ✅

link.xml 配置保留必要反射：
  <linker>
    <assembly fullname="Assembly-CSharp" preserve="all"/>
  </linker>
```

#### 4.2.2 资源精简（目标 < 2MB）

| 资源类型 | 策略 | 预估大小 |
|----------|------|----------|
| 贴图 | 全部使用ASTC压缩，256x256以下 | < 500KB |
| 音频 | 背景音乐走CDN，只保留必要音效 | < 200KB |
| 字体 | 使用系统字体或极小bitmap字体 | < 100KB |
| 动画 | 简化为骨骼动画/序列帧，不用Animator | < 300KB |
| Prefab | 精简，复用组件 | < 200KB |
| Shader | 使用微信小游戏内置Shader，不自定义 | < 50KB |

#### 4.2.3 分包加载策略

```
首包（4MB以内）：
  ├── 游戏代码（IL2CPP编译后 ~1MB）
  ├── 核心资源（UI框架、基础图标、首屏资源 ~1.5MB）
  └── 启动界面/加载界面

分包1（通过CDN按需加载）：
  ├── 英雄Sprite资源
  ├── 敌人Sprite资源
  └── 骰子/遗物图标

分包2：
  ├── 音效文件
  └── 特效资源

分包3（10关后加载）：
  └── 机制怪资源
```

---

### 4.3 WebGL性能优化

#### 4.3.1 渲染优化

| 优化项 | 方案 |
|--------|------|
| Draw Call | 合批：使用SpriteAtlas，单张图集合并 |
| Overdraw | 减少UI层级，避免全屏半透明遮罩 |
| GPU压力 | 不使用后处理、不使用实时阴影 |
| 帧率 | 目标30fps（小游戏足够），`Application.targetFrameRate = 30` |
| 分辨率 | 适配手机屏幕，不超1080p |
| 粒子效果 | 极简或不用，用UI动画替代 |

#### 4.3.2 内存优化

| 优化项 | 方案 |
|--------|------|
| 资源释放 | 切换场景时主动 `Resources.UnloadUnusedAssets()` |
| 对象池 | Hero/Enemy使用对象池，不要频繁Instantiate/Destroy |
| 纹理 | 所有贴图开启Read/Write关闭，压缩格式ASTC |
| GC | 避免战斗循环中的 `new` 和装箱，使用预分配数组 |

#### 4.3.3 加载优化

| 优化项 | 方案 |
|--------|------|
| 首屏 | 极简启动画面 → 预加载核心资源 → 进入游戏 |
| 异步加载 | `Addressables` 或 `AssetBundle` 异步加载分包 |
| 进度条 | 真实加载进度，避免假进度 |

---

### 4.4 资源分包/CDN方案

```
资源服务器架构：
  ├── CDN（腾讯云COS / 阿里云OSS）
  │   ├── /assets/heroes/        # 英雄Sprite
  │   ├── /assets/enemies/       # 敌人Sprite
  │   ├── /assets/audio/         # 音效
  │   ├── /assets/effects/       # 特效
  │   └── /assets/bundles/       # AssetBundle
  │
  └── 微信云开发（备选）
      ├── 云存储
      └── 云函数（排行榜、数据上报）

加载流程：
  1. 首包加载（4MB内）→ 显示启动界面
  2. 检测网络 → 异步下载英雄选择界面资源（~500KB）
  3. 进入英雄选择 → 后台预加载第1关敌人资源
  4. 进入战斗 → 后台预加载下一关资源
  5. 10关前预加载机制怪资源包
```

**微信小游戏资源加载API**：
```javascript
// 微信小游戏 wx.downloadFile / wx.loadSubpackage
wx.loadSubpackage({
    name: 'heroes',
    success: () => { /* 加载完成 */ },
    fail: () => { /* 降级处理 */ }
});
```

---

### 4.5 预估额外工期

| 工作项 | 人天 | 说明 |
|--------|------|------|
| WebGL Build 配置与调试 | 3 | 初次构建、踩坑、解决兼容性问题 |
| 包体优化（达标4MB） | 5 | 反复测试、压缩、分包 |
| 微信小游戏适配层 | 3 | API适配、平台差异处理 |
| 资源分包系统 | 3 | Addressables/AB打包、CDN配置 |
| 性能优化 | 3 | 帧率、内存、加载速度 |
| 微信小游戏提审 | 2 | 提交审核、修复被拒问题 |
| **合计** | **19人天** | |

---

## 5. 开发排期

### 5.1 里程碑总览

| 阶段 | 周期 | 产出 |
|------|------|------|
| M1: 核心玩法改造 | 第1-2周 | 状态机+骰子+英雄+战斗改造完成 |
| M2: 肉鸽系统开发 | 第3-4周 | 肉鸽奖励+骰子面升级+遗物系统 |
| M3: 关卡与机制怪 | 第5周 | 无限关卡+机制怪 |
| M4: 小游戏适配 | 第6-7周 | WebGL构建+包体优化+适配 |
| M5: 测试与调优 | 第8周 | 全量测试+数值调优+提审 |

### 5.2 详细任务排期

#### 第1周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 状态机改造（砍CardPlay/Positioning，新增RoguelikeReward） | P0 | 1天 | 后端 | ❌ | 无 |
| 骰子系统改造（重摇2→1） | P0 | 0.5天 | 后端 | ✅ | 无 |
| 英雄系统改造（砍卡牌字段、精简3职业） | P0 | 2天 | 后端 | ✅ | 状态机完成 |
| 棋盘系统改造（简化站位） | P1 | 2天 | 后端 | ✅ | 英雄系统完成 |
| 战斗系统改造（点触释放骰子技能+跳过战斗） | P0 | 4天 | 后端 | ❌ | 英雄+骰子完成 |

**第1周关键产出**：核心循环可跑通（选英雄→掷骰→战斗→结算）

---

#### 第2周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 战斗系统改造（续） | P0 | 2天 | 后端 | ❌ | 第1周 |
| GameData清理（去掉卡牌相关，保留英雄/敌人模板） | P1 | 1天 | 后端 | ✅ | 英雄改造完成 |
| GameBalance适配（难度曲线调整） | P1 | 1天 | 后端 | ✅ | 无 |
| 连携技系统改造 | P2 | 1.5天 | 后端 | ✅ | 英雄改造完成 |
| 装备系统改造（融入肉鸽奖励） | P2 | 1.5天 | 后端 | ✅ | 英雄改造完成 |
| UI框架搭建（小游戏适配） | P1 | 3天 | 前端 | ❌ | 状态机完成 |

**第2周关键产出**：战斗+骰子技能可用，旧系统清理完毕

---

#### 第3周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 肉鸽奖励系统（RoguelikeRewardSystem） | P0 | 5天 | 后端 | ❌ | 状态机RoguelikeReward状态 |
| 骰子面升级系统 | P0 | 4天 | 后端 | ✅ | 骰子系统 |
| 英雄选择UI（战/法/刺三选一） | P0 | 2天 | 前端 | ✅ | 英雄系统 |
| 骰子UI（掷骰+重摇+组合显示） | P0 | 2天 | 前端 | ✅ | 骰子系统 |

**第3周关键产出**：肉鸽奖励三选一可用，骰子面升级可用

---

#### 第4周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 遗物系统（RelicSystem） | P0 | 4天 | 后端 | ❌ | 肉鸽奖励系统 |
| 肉鸽奖励UI（三选一选择界面） | P0 | 2天 | 前端 | ✅ | 肉鸽奖励系统 |
| 遗物列表UI | P1 | 1天 | 前端 | ✅ | 遗物系统 |
| 骰子面升级UI | P1 | 1天 | 前端 | ✅ | 骰子面升级系统 |
| 战斗UI（加速/跳过按钮+骰子技能按钮） | P0 | 2天 | 前端 | ✅ | 战斗系统 |

**第4周关键产出**：完整肉鸽循环可玩（选英雄→掷骰→战斗→三选一→下一关）

---

#### 第5周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 关卡系统改造（无限关卡+难度曲线） | P0 | 4天 | 后端 | ❌ | 肉鸽奖励+战斗完成 |
| 机制怪系统（MechanicEnemySystem） | P0 | 6天 | 后端 | ❌ | 关卡系统 |

**第5周关键产出**：无限关卡+10关后机制怪可用

---

#### 第6周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| WebGL Build配置与首次构建 | P0 | 3天 | 全栈 | ❌ | 全部游戏逻辑完成 |
| 包体优化（压缩+剥离） | P0 | 3天 | 全栈 | ❌ | WebGL构建完成 |
| 资源分包（Addressables+CDN） | P0 | 3天 | 后端 | ✅ | 包体优化 |
| 小游戏适配层（MiniGameAdapter） | P1 | 3天 | 前端 | ✅ | WebGL构建 |

**第6周关键产出**：可在微信开发者工具中运行

---

#### 第7周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 性能优化（帧率+内存） | P0 | 3天 | 全栈 | ❌ | 小游戏可运行 |
| 机制怪UI（Boss特殊机制展示） | P1 | 2天 | 前端 | ✅ | 机制怪系统 |
| 游戏结束UI（战绩展示） | P1 | 1天 | 前端 | ✅ | 无 |
| 主菜单UI | P1 | 1天 | 前端 | ✅ | 无 |
| 音效接入 | P2 | 2天 | 前端 | ✅ | 小游戏适配层 |

**第7周关键产出**：小游戏完整可玩，性能达标

---

#### 第8周（5人天 × 2人 = 10人天）

| 任务 | 优先级 | 工时 | 负责人 | 可并行 | 前置依赖 |
|------|--------|------|--------|--------|----------|
| 全量测试+Bug修复 | P0 | 3天 | 全员 | ❌ | 全部开发完成 |
| 数值调优（难度曲线+肉鸽奖励权重） | P0 | 2天 | 策划+后端 | ✅ | 测试中 |
| 微信提审 | P0 | 2天 | 全栈 | ❌ | 测试通过 |
| 分享/排行榜接入 | P2 | 2天 | 前端 | ✅ | 小游戏适配层 |
| 广告位预留 | P3 | 1天 | 前端 | ✅ | 无 |

**第8周关键产出**：提审通过，上线

---

### 5.3 工时汇总

| 类别 | 人天 |
|------|------|
| 现有系统改造 | 22 |
| 新系统开发 | 24 |
| 小游戏适配+优化 | 19 |
| 测试+调优+提审 | 8 |
| UI开发 | 17 |
| **总计** | **90人天** |

**团队配置**：2人（1后端 + 1前端/全栈）× 8周 = 80人天可用 → 略紧，建议加1人或延长至9周

---

## 6. 风险点

### 6.1 技术风险

#### 风险1：首包4MB限制（🔴 高风险）

**风险描述**：
Unity WebGL构建的代码体积通常2-5MB，加上基础资源很容易超4MB。

**应对方案**：
1. **IL2CPP + Brotli压缩**：Unity 2022默认IL2CPP，编译后体积更小
2. **代码裁剪**：设置 `Managed Stripping Level = High`，配置 `link.xml` 保留必要反射
3. **引擎模块裁剪**：在 `Project Settings` 中关闭不需要的模块（Physics、NavMesh、Video等）
4. **资源全外置**：首包只含代码和启动界面，所有游戏资源走CDN分包加载
5. **B计划**：如果实在压不下来，考虑使用微信官方的「分包加载」功能，首包只含加载器

**早期验证**：第1周结束前先做一次空项目WebGL构建，摸底代码体积基线。

---

#### 风险2：WebGL性能不足（🟡 中风险）

**风险描述**：
微信小游戏跑在WebView/WebGL环境中，性能远低于原生。复杂战斗场景可能卡顿。

**应对方案**：
1. **帧率目标30fps**：不追求60fps
2. **简化渲染**：不使用后处理、实时阴影、粒子系统
3. **UI驱动**：战斗画面尽量用UI元素（Image+DOTween）而非3D渲染
4. **限制同屏单位**：最多6个单位（我方3+敌方3）
5. **战斗Tick制**：当前已是Tick制（0.3s一次行动），不依赖物理帧
6. **早期验证**：第2周做战斗性能Profile

---

#### 风险3：微信小游戏API兼容性（🟡 中风险）

**风险描述**：
微信小游戏WebGL有一些平台特有的限制（如不支持某些WebGL扩展、音频格式限制等）。

**应对方案**：
1. 使用微信官方的Unity转小游戏适配插件
2. 音频统一用MP3格式（微信支持最好）
3. 不使用WebGL 2.0特有功能，保持WebGL 1.0兼容
4. 参考微信小游戏官方文档和示例项目

---

#### 风险4：机制怪系统复杂度膨胀（🟡 中风险）

**风险描述**：
机制怪的10种机制需要独立的逻辑实现，且需要与战斗系统深度集成，容易延期。

**应对方案**：
1. **MVP先做3种机制**：护盾互换、伤害反弹、狂暴
2. **机制接口统一**：定义 `IMechanic` 接口，每种机制实现该接口
3. **数据驱动**：机制参数用配置表，方便调优
4. **延后策略**：如果时间不够，10关后先用数值强化怪替代，机制怪v1.1再上

---

#### 风险5：肉鸽奖励平衡性（🟢 低风险但重要）

**风险描述**：
肉鸽奖励的权重设计直接影响游戏体验，奖励太弱玩家无聊，太强没有挑战。

**应对方案**：
1. **权重系统**：每种奖励类型有基础权重，根据当前阵容动态调整
2. **保底机制**：连续3次不出现某类型时提高权重
3. **快速迭代**：权重配置外置（JSON），可热更新
4. **埋点验证**：记录玩家选择数据，用数据驱动平衡调整

---

### 6.2 项目风险

#### 风险6：工期紧张（🟡 中风险）

**风险描述**：
90人天的工作量，2人8周=80人天，余量不足。

**应对方案**：
1. **优先级明确**：P0任务优先，P2/P3可延后
2. **并行开发**：前端UI和后端逻辑尽量并行
3. **MVP先行**：先实现核心循环（掷骰→战斗→三选一），再逐步加系统
4. **加人备选**：如果第3周发现进度落后，临时加1名前端

---

#### 风险7：美术资源缺失（🟡 中风险）

**风险描述**：
目前代码层面完整，但UI素材、英雄/敌人立绘、骰子面图标等美术资源缺失。

**应对方案**：
1. **使用占位图**：开发阶段用色块+文字替代
2. **购买素材包**：Unity Asset Store有大量廉价素材
3. **简约风格**：小游戏用户对美术容忍度高，可走像素/扁平风格
4. **分批替换**：核心玩法完成后再替换美术

---

### 6.3 风险矩阵

| 风险 | 可能性 | 影响 | 综合评级 | 应对优先级 |
|------|--------|------|----------|-----------|
| 首包4MB超限 | 高 | 致命 | 🔴 高 | 立即验证 |
| WebGL性能不足 | 中 | 高 | 🟡 中 | 第2周验证 |
| API兼容性 | 中 | 中 | 🟡 中 | 第6周处理 |
| 机制怪复杂度 | 中 | 中 | 🟡 中 | MVP先做3种 |
| 奖励平衡性 | 高 | 中 | 🟡 中 | 上线后迭代 |
| 工期紧张 | 中 | 高 | 🟡 中 | 按P0优先 |
| 美术缺失 | 高 | 低 | 🟢 低 | 占位图先行 |

---

## 附录

### A. 代码资产清单

| 文件 | 行数 | 保留/砍掉 |
|------|------|-----------|
| Core/GameStateMachine.cs | 198 | 保留改造 |
| Dice/DiceRoller.cs | 154 | 保留 |
| Dice/DiceCombinationEvaluator.cs | 144 | 保留 |
| Dice/DiceCombination.cs | 80 | 保留 |
| Dice/Dice.cs | 53 | 保留 |
| Heroes/Hero.cs | 267 | 保留改造 |
| Heroes/HeroData.cs | 39 | 保留改造 |
| Heroes/SkillData.cs | 31 | 保留 |
| Cards/CardDeck.cs | 409 | ❌ 砍掉 |
| Cards/CardInstance.cs | 73 | ❌ 砍掉 |
| Cards/CardData.cs | 76 | ❌ 砍掉 |
| Grid/GridManager.cs | 214 | 保留改造 |
| Grid/GridCell.cs | ~50 | 保留 |
| Battle/BattleManager.cs | 161 | 保留改造 |
| Battle/AutoChessAI.cs | 159 | 保留改造 |
| Battle/SynergySystem.cs | 104 | 保留改造 |
| Battle/DamagePopup.cs | ~40 | 保留 |
| Equipment/EquipmentManager.cs | 154 | 保留改造 |
| Equipment/EquipmentData.cs | 38 | 保留参考 |
| Equipment/EquipmentSlot.cs | 9 | 保留参考 |
| Shop/ShopManager.cs | 117 | ❌ 砍掉 |
| Events/RandomEventSystem.cs | 121 | ❌ 砍掉 |
| Level/LevelManager.cs | 214 | 保留改造 |
| Level/LevelConfig.cs | 31 | 保留改造 |
| Data/GameBalance.cs | 214 | 保留 |
| Data/GameData.cs | 558 | 保留改造 |
| Player/PlayerInventory.cs | 83 | 保留改造 |
| UI/*.cs | ~400 | ❌ 重写 |
| **合计** | **~5,400** | **复用率 ~55%** |

### B. 新增代码预估

| 新系统 | 预估行数 |
|--------|---------|
| RoguelikeRewardSystem | ~300 |
| DiceFaceUpgradeSystem | ~200 |
| RelicSystem | ~250 |
| MechanicEnemySystem | ~400 |
| LevelGenerator | ~200 |
| MiniGameAdapter | ~150 |
| SkipBattle/SimulateBattle | ~100 |
| 新UI代码 | ~800 |
| **合计** | **~2,400** |

### C. 参考文档

- [微信小游戏官方文档 - Unity适配](https://developers.weixin.qq.com/minigame/dev/guide/)
- [Unity WebGL优化指南](https://docs.unity3d.com/Manual/webgl-optimizing.html)
- [微信小游戏包体优化最佳实践](https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity.html)

---

> **文档结束**  
> 评审通过后进入开发阶段，建议第1周先验证WebGL包体体积基线。
