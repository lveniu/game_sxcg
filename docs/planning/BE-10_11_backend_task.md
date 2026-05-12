# BE-10/BE-11 后端任务书 v1.0

> CTO → @hermes-后端
> 前置：P0 EventOption 阻断已修 ✅ (f4e5fba)

---

## 优先级：BE-10 > BE-11

BE-10 是核心成长系统，直接影响游戏循环。BE-11 是锦上添花的配置化。

---

## BE-10：英雄经验/升级系统 — 集成到肉鸽流程

### 现状
- `HeroExpSystem.cs` (410行) 代码完整，但**完全未接入主流程**
- `Hero.cs` 已有 `SetLevel()`, `SetExp()`, `AddExp()`, `RecalculateStats()` 等方法
- `ConfigLoader` + `BalanceProvider` 已存在，HeroExpSystem 的配置加载已对接
- `Hero.RecalculateStats()` 已包含等级加成：`float levelMultiplier = 1f + (HeroLevel - 1) * 0.05f`

### 需要做的（3个集成点）

#### 集成点1：HeroExpSystem 创建时机

**文件**: `RoguelikeGameManager.cs` → `StartNewGame()` 方法 (第46行)

在 `RewardSystem = new RoguelikeRewardSystem();` 之后加：

```csharp
// BE-10: 初始化经验系统
if (HeroExpSystem.Instance != null)
    HeroExpSystem.Destroy();
HeroExpSystem.Create();
```

#### 集成点2：战斗结算触发经验

**文件**: `GameStateMachine.cs` → `HandleStateEntered()` 的 `GameState.Settlement` 分支 (第205行)

当前 Settlement 分支只有一行 log，需要加经验结算：

```csharp
case GameState.Settlement:
    // BE-10: 战斗结算经验
    GrantBattleExp();
    Debug.Log($"[StateMachine] 进入结算 — 第{CurrentLevel}关");
    break;
```

在 `GameStateMachine` 类中新增方法：

```csharp
/// <summary>
/// 战斗结算：给存活英雄分配经验
/// </summary>
private void GrantBattleExp()
{
    var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
    if (heroes == null || heroes.Count == 0) return;

    int level = CurrentLevel;
    bool isBoss = (level % 5 == 0); // 每5关Boss
    int baseExp = 10; // 通关基础经验

    HeroExpSystem.Instance?.GainExpForTeam(heroes, baseExp, level, isBoss);
}
```

#### 集成点3：击杀经验（可选，P2）

**文件**: `BattleManager.cs` → 敌人死亡时

击杀经验暂不接入（影响面大），先用通关结算经验。MVP够用。

### 验收标准
- [ ] `RoguelikeGameManager.StartNewGame()` 调用 `HeroExpSystem.Create()`
- [ ] `GameStateMachine` Settlement 状态触发 `GainExpForTeam`
- [ ] 英雄获得经验后等级提升
- [ ] `Hero.RecalculateStats()` 在升级后被调用（HeroExpSystem.CheckLevelUp 已调用 `hero.SetLevel()` → `RecalculateStats()`）
- [ ] 编译通过，无报错

### 注意事项
- `HeroExpSystem` 是纯C#类（非MonoBehaviour），用 `HeroExpSystem.Create()` 手动创建
- `HeroExpSystem.Destroy()` 在 `GameOver` 或新游戏时需要调用来清理事件
- 不要改动 `HeroExpSystem.cs` 和 `Hero.cs`，它们已经完备

---

## BE-11：随机事件系统 — 配置化（P2，BE-10完成后做）

### 现状
- `RandomEventSystem.cs` (529行) 完整可用
- 事件数据硬编码在 `PopulateEvent()` 的 switch-case 中
- 技术方案文档 `BE-10_11_12_tech_design.md` 已有 JSON 结构设计

### 需要做的

#### 步骤1：创建 random_events.json

创建 `Assets/Resources/Data/random_events.json`，结构参考 `BE-10_11_12_tech_design.md` 第133-167行。

#### 步骤2：ConfigLoader 加载方法

在 `ConfigLoader.cs` 加 `LoadRandomEvents()` 方法，返回 `RandomEventsConfig` 数据模型。

#### 步骤3：BalanceProvider 暴露接口

在 `BalanceProvider.cs` 加 `RandomEvents` 懒加载属性和查询方法。

#### 步骤4：RandomEventSystem 从 JSON 读数据

`PopulateEvent()` 改为优先从 JSON 读取，fallback 到现有硬编码。

### 验收标准
- [ ] JSON 配置文件创建
- [ ] ConfigLoader + BalanceProvider 链路打通
- [ ] 运行时事件数据从 JSON 加载
- [ ] 删除 JSON 后 fallback 到硬编码正常工作

### 优先级说明
BE-11 不影响核心循环，BE-10 完成后再做。

---

## 接口依赖

| 调用方 | 被调用方 | 方法 |
|--------|---------|------|
| GameStateMachine | HeroExpSystem | `GainExpForTeam()` |
| RoguelikeGameManager | HeroExpSystem | `Create()` / `Destroy()` |
| RandomEventSystem | BalanceProvider | `GetRandomEvents()` (待建) |

---

## 文件清单

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| `Roguelike/RoguelikeGameManager.cs` | 加2行 | StartNewGame 中 Create HeroExpSystem |
| `Core/GameStateMachine.cs` | 加10行 | Settlement 触发经验结算 |
| `Resources/Data/random_events.json` | 新建 | BE-11 事件配置 |
| `Data/ConfigLoader.cs` | 加方法 | BE-11 LoadRandomEvents |
| `Data/BalanceProvider.cs` | 加属性+方法 | BE-11 RandomEvents 暴露 |
| `Events/RandomEventSystem.cs` | 改造 | BE-11 JSON优先 |

改动量极小，BE-10 约12行代码，BE-11 约80行。
