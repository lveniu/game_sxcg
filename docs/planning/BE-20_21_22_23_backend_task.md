# 后端下一批任务书 BE-20 ~ BE-23

> 生成时间: 2026-05-13
> CTO: @hermes-CTO
> 状态: **待分配 → @hermes-后端**
> 前置条件: BE-17~19 ✅ 全部完成
> 整体完成度: ~92% → 目标 96%

---

## 当前状态

P0 已消除，P1 仅剩 3 项（卡牌合成、肉鸽存档、引导UI）。本轮冲刺 P1 收尾 + P2 启动。

---

### BE-20 卡牌合成系统 CardMergeSystem [P1] [预估 1.5天]

**问题**: CardData.rarity 和 CardEffectEngine 已就绪，但缺卡牌合成逻辑。卡牌收集是核心驱动力，合成是稀有度体系的关键闭环。

**任务**:
1. 新增 `CardMergeSystem` 单例（MonoBehaviour）
2. `MergeCards(CardInstance a, CardInstance b)` — 合成两张同稀有度卡牌
   - 验证: 两张卡稀有度相同，且非 Legendary
   - 从高一档稀有度的卡牌池中随机选一张作为结果
   - 合成消耗: 50 金币（可配）
   - 销毁素材卡牌，生成新卡实例
3. 稀有度晋升链: Common → Rare → Epic → Legendary（Legendary 不可合成）
4. `GetMergePreview(CardInstance a, CardInstance b)` — 预览合成结果池（前端展示用）
5. `CanMerge(CardInstance a, CardInstance b)` — 检查是否可合成
6. 事件回调: `OnMergeComplete(CardInstance result)`

**接口定义**:
```csharp
public class CardMergeSystem : MonoBehaviour
{
    public static CardMergeSystem Instance { get; private set; }
    public event System.Action<CardInstance> OnMergeComplete;

    public bool CanMerge(CardInstance a, CardInstance b);
    public List<CardData> GetMergePreview(CardInstance a, CardInstance b);
    public CardInstance MergeCards(CardInstance a, CardInstance b);
}
```

**依赖**:
- CardData.rarity ✅
- CardEffectEngine ✅（效果定义已有）
- PlayerInventory（扣金币 + 卡牌增删）✅
- GameData.GetAllCardData()（卡牌池）✅

**产出文件**:
- `Assets/Scripts/Cards/CardMergeSystem.cs`（新增 ~200行）

---

### BE-21 肉鸽存档完善 — RoguelikeRunData 序列化 [P1] [预估 1天]

**问题**: SaveSystem 已有骨架（308行），但 RoguelikeRunData 的序列化不完整，中途退出无法恢复肉鸽进度。

**任务**:
1. RoguelikeRunData 新增序列化支持:
   - `[Serializable]` 标记 + 以下字段序列化:
     - currentFloor (int)
     - selectedHeroes (List<string> heroId)
     - ownedRelics (List<string> relicId)
     - currentGold (int)
     - shopLevel (int)
     - seed (int)
     - visitedNodes (List<int> nodeIndex)
     - currentPlayerHP (Dictionary<string, int> heroId → HP)
2. SaveSystem 新增:
   - `SaveRoguelikeRun(RoguelikeRunData data)` — 序列化到 PlayerPrefs 或 JSON 文件
   - `LoadRoguelikeRun() → RoguelikeRunData` — 反序列化
   - `HasSavedRun() → bool` — 检测是否有存档
   - `DeleteSavedRun()` — 结算后清除
3. RoguelikeGameManager 新增:
   - `ResumeRun(RoguelikeRunData data)` — 从存档恢复游戏状态
4. 主菜单新增"继续游戏"入口（状态判断：HasSavedRun）

**依赖**:
- SaveSystem ✅
- RoguelikeRunData ✅（需扩展）
- RoguelikeGameManager ✅（需扩展）

**产出文件**:
- `Assets/Scripts/Core/SaveSystem.cs`（增强，新增肉鸽存档方法）
- `Assets/Scripts/Roguelike/RoguelikeRunData.cs`（增强，新增序列化字段）
- `Assets/Scripts/Roguelike/RoguelikeGameManager.cs`（增强，新增 ResumeRun）

---

### BE-22 肉鸽事件池配置化 [P1→P2] [预估 1天]

**问题**: EventEffectEngine ✅ 已完成，RandomEventData ✅ 已有。但 RandomEventSystem 的事件池仍有硬编码，需改为纯 JSON 驱动。

**任务**:
1. 新增 `random_events.json` 配置文件:
   - 每个事件: eventId / title / description / choices[]
   - 每个 choice: choiceText / effects[] / weight
   - 触发条件: minFloor / maxFloor / requiredRelic / requiredHero
2. RandomEventSystem 新增:
   - `LoadEventsFromConfig()` — 从 JSON 加载事件池
   - `GetAvailableEvents(int floor)` — 按层过滤可用事件
   - 移除硬编码事件定义，全部走 JSON
3. RandomEventData 新增字段:
   - `triggerCondition`（触发条件对象）
   - `weight`（权重，用于加权随机）

**依赖**:
- EventEffectEngine ✅
- RandomEventData ✅（需扩展）
- ConfigLoader / BalanceProvider ✅

**产出文件**:
- `Assets/Resources/Data/random_events.json`（新增 ~200行）
- `Assets/Scripts/Events/RandomEventSystem.cs`（增强）
- `Assets/Scripts/Events/RandomEventData.cs`（增强）

---

### BE-23 StraightFlush 骰子组合完善 + 微信排行榜骨架 [P2] [预估 1天]

**问题**: 两个独立小任务合并，都是 P2 优先级，半天各一个。

**任务 A — StraightFlush 骰子组合**:
1. DiceCombinationEvaluator 完善 StraightFlush 评估逻辑:
   - 判定: 5个骰子面值构成顺子（如 1-2-3-4-5），且花色相同
   - 骰子面需新增 `suit`（花色）字段: Sword / Shield / Magic
   - 倍率: 15x（最高组合）
2. Dice.cs FaceDefinition 新增 `suit` 字段
3. `dice_system.json` 新增花色配置

**任务 B — 微信排行榜骨架**:
1. 新增 `LeaderboardManager.cs` 单例
2. 接口设计:
   - `SubmitScore(int score)` — 提交分数到微信好友排行
   - `GetFriendRanking() → List<RankEntry>` — 获取好友排行
   - `ShowRankingUI()` — 调起微信排行榜 UI
3. 使用微信小游戏 `wx.getOpenDataContext()` 或 `wx.setUserCloudStorage()`
4. 先实现本地 Mock，微信 API 调用通过 MiniGameAdapter 桥接

**产出文件**:
- `Assets/Scripts/Dice/DiceCombinationEvaluator.cs`（增强）
- `Assets/Scripts/Dice/Dice.cs`（FaceDefinition 新增 suit）
- `Assets/Scripts/Core/LeaderboardManager.cs`（新增 ~150行）

---

## 优先级排序

| 批次 | 任务 | 优先级 | 预估 | 依赖 |
|------|------|--------|------|------|
| 第一波 | BE-20 卡牌合成 | **P1** | 1.5天 | 无 |
| 第一波 | BE-21 肉鸽存档 | **P1** | 1天 | 无 |
| 第二波 | BE-22 事件配置化 | P1→P2 | 1天 | 无 |
| 第二波 | BE-23 骰子+排行 | P2 | 1天 | BE-20（花色） |

## 依赖关系图

```
第一波（可并行）:
├── BE-20 卡牌合成（独立）
└── BE-21 肉鸽存档（独立）

第二波:
├── BE-22 事件配置化（独立）
└── BE-23 骰子+排行（独立）
```

## 并行方案

```
后端线程: [BE-20=][BE-21=][BE-22=][BE-23=]
```

预计总耗时: **4.5天**，可压缩至 3天（前两个并行）。

---

## 完成后预期

- P1 全部清零
- 整体完成度 92% → 96%
- 剩余仅 P2/P3 打磨项
- 可进入内测阶段
