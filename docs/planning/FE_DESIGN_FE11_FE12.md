# FE-11 & FE-12 前端任务详细设计

> CTO → @hermes-前端  
> 阶段2前端任务

---

## FE-11：随机事件UI完善

### 现状
EventPanel 已有基本功能：触发→显示→确认→应用→关闭。但缺少**选项交互**和**视觉丰富度**。

### 需要增强的点

#### 1. 选项交互（当前只有单个确认按钮，改为多选项）

**后端 RandomEventSystem 增强（@hermes-后端 会做）：**
```csharp
// RandomEvent 增强为支持多选项
public class RandomEvent
{
    public RandomEventType eventType;
    public string description;
    public string eventName;
    public List<EventOption> options;  // 新增：多个选项
    // ... 保留旧字段兼容
}

public class EventOption
{
    public string optionText;       // "打开宝箱" / "小心绕过"
    public string effectDescription; // "获得50金币" / "安全通过，无事发生"
    public bool isRiskOption;        // 是否风险选项（红色高亮）
}
```

**前端改动（EventPanel.cs 修改）：**

```
布局调整：
┌────────────────────────────┐
│   [事件图标区 - 背景插画]    │  上半部分（占60%）
│                            │
│   📦 神秘宝箱              │  事件标题
│   你发现一个古老宝箱...     │  事件描述
│                            │
│   ┌──────────────────────┐ │
│   │ 🟢 打开宝箱          │ │  选项A（安全）
│   │ 获得50金币            │ │
│   ├──────────────────────┤ │
│   │ 🔴 强行撬开          │ │  选项B（风险）
│   │ 50%获得100金币或...   │ │
│   └──────────────────────┘ │
└────────────────────────────┘
```

**交互逻辑：**
- 如果 `options != null && options.Count > 0`：显示多选项按钮
- 如果 `options == null`：兼容旧单按钮确认模式
- 风险选项 `.isRiskOption = true`：按钮背景红色，悬停时抖动
- 点击选项 → 播放结果动画 → 关闭

#### 2. 视觉增强

**入场动画（增强 PlayEnterAnimation）：**
- 弹窗从屏幕底部滑入（DOAnchorPosY -500→0）
- 同时 scale 0.5→1.0（OutBack）
- 图标区单独延迟0.2s淡入

**选项按钮交互：**
- 悬停：scale 1.0→1.03 + 轻微亮度提升
- 点击确认：按钮缩小→放大脉冲
- 结果展示：效果文字逐行打出（typewriter效果，每个字间隔0.03s）

**效果反馈动画：**
- 正面效果（金币+、治疗+）：绿色向上飘字
- 负面效果（生命-）：红色向上飘字 + 轻微屏幕震动
- 打字完成后0.5s自动关闭

**事件类型背景色（图标区域）：**
| 类型 | 背景渐变 | 图标 |
|------|----------|------|
| Treasure | 金→橙 | 📦 |
| Trap | 红→暗红 | ⚠️ |
| MysteryMerchant | 紫→深紫 | 🧙 |
| Altar | 白→蓝白 | ⛪ |
| WanderingHealer | 绿→浅绿 | 💊 |
| Arena | 橙→红橙 | ⚔️ |

#### 3. 与肉鸽地图集成

- 当地图节点类型为 `MapNodeType.Event` 时，选择该节点后打开 EventPanel
- `TriggerAndShow` 改为接受 `MapNode` 参数（可选），无 MapNode 时保持随机30%触发
- EventPanel 关闭后 → 自动回到 MapSelect 状态

### 技术要点
- 修改现有 `EventPanel.cs`，不新建文件
- 选项按钮用代码动态生成（最多3个选项）
- 所有 tween 加 `.SetLink(gameObject)`
- 兼容旧的单选项事件格式（`options == null` 时用原逻辑）
- 预估改动量：+150行

---

## FE-12：英雄升级/经验UI

### 需求背景
Hero 类目前没有等级和经验系统。需要前端做：
1. 战斗结算时的经验获取动画
2. 升级特效（光效+飘字）
3. 星级进化动画

### 后端接口（@hermes-后端 BE-10 新增，前端先用 Mock）

```csharp
// Hero.cs 新增字段（后端做）
public int Level { get; private set; } = 1;
public int CurrentExp { get; private set; } = 0;
public int ExpToNextLevel => GameBalance.GetExpRequired(Level);

// HeroExpSystem（后端新建，前端暂不需要直接调用）
HeroExpSystem.Instance.GrantExp(Hero hero, int amount)
HeroExpSystem.Instance.OnExpGained  // 事件：(Hero, int oldExp, int newExp)
HeroExpSystem.Instance.OnLevelUp    // 事件：(Hero, int newLevel)
```

### 前端设计

#### SettlementPanel 增强（在现有结算面板中嵌入经验模块）

```
结算面板（增强后）：
┌──────────────────────────────┐
│  ⭐ 战斗胜利！                │
│                              │
│  ┌────────────────────────┐  │
│  │ 🗡 铁壁战士 Lv.3       │  │
│  │ ████████░░ 80/120 EXP  │  │  经验条
│  │  +45 EXP               │  │  获得经验
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │ 🔮 奥术法师 Lv.3       │  │
│  │ ██████████ Lv.UP!      │  │  升级闪光
│  │  ★→★★ 进化!            │  │  星级进化
│  └────────────────────────┘  │
│  ┌────────────────────────┐  │
│  │ 🏹 影舞者 Lv.2         │  │
│  │ ██████░░░░ 60/80 EXP   │  │
│  │  +30 EXP               │  │
│  └────────────────────────┘  │
│                              │
│  [继续]                      │
└──────────────────────────────┘
```

#### 经验条动画

```
1. 初始状态：经验条显示当前比例
2. 经验数字飞入（+45 EXP 从右上角飘入，0.3s OutCubic）
3. 经验条平滑增长（DOFillAmount，0.5s OutQuad）
4. 如果升级：
   a. 经验条填满 → 闪光白屏 → 重置为0 → 显示新的下一级上限
   b. "LEVEL UP!" 文字从中心放大弹出（0→1.5→1.0，OutBack）
   c. 英雄卡片边框金色脉冲（scale 1.0↔1.05 × 3次）
   d. 属性提升飘字（ATK+3 DEF+1 逐行从左滑入）
5. 如果星级进化：
   a. 星星图标逐个亮起（旧星级→新星级，每颗间隔0.3s）
   b. 粒子爆发效果（金色星屑扩散）
   c. 背景光柱（垂直金色光柱0.3s闪过）
```

#### Mock 数据开发

```csharp
// 前端开发用的 Mock 类（后端完成后删除）
public static class MockHeroExp
{
    public static int GetLevel(this Hero hero) => MockData.GetHeroLevel(hero);
    public static int GetExp(this Hero hero) => MockData.GetHeroExp(hero);
    public static int GetExpToNext(this Hero hero) => MockData.GetExpToNext(hero);
}

public static class MockData
{
    // 模拟数据：每个英雄的等级和经验
    static Dictionary<Hero, (int level, int exp)> expMap = new();
    
    public static void SimulateExpGain(Hero hero, int amount)
    {
        // 模拟经验获取 + 升级判定
        // 触发模拟事件
    }
}
```

#### SettlementPanel 改动

1. 在结算面板 `OnShow()` 中，为每个参战英雄生成经验卡片
2. 监听 `OnExpGained` / `OnLevelUp` 事件（Mock阶段用本地模拟）
3. 按顺序播放经验动画（英雄A → 英雄B → 英雄C，每个间隔0.5s）
4. 全部动画完成后才显示"继续"按钮

### 技术要点
- 修改现有 `SettlementPanel.cs`，不新建文件
- 经验条用 Image（filled） + DOTween DOFillAmount
- 星星用 Image 数组（1-3颗），亮起用 DOFade + DOScale
- 所有 tween 加 `.SetLink(gameObject)`
- Mock 数据结构简单，后端 BE-10 完成后替换为真实数据源
- 预估改动量：+200行

### 依赖
- FE-12 依赖后端 BE-10（HeroExpSystem），但前端可先用 Mock 开发全部UI和动画
- FE-11 无后端阻塞（RandomEventSystem 已存在，只做前端增强）

---

## 开发顺序建议
1. **FE-11 先做**（1天工作量，改动量小，无依赖）
2. **FE-12 后做**（2天工作量，需要写 Mock 数据）
