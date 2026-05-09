# FE-04 战斗面板增强 & FE-05 肉鸽奖励面板增强

> 技术方案 by CTO | 前端任务指派

---

## FE-04 BattlePanel 增强（战斗观战面板）

### 当前状态
`Assets/Scripts/UI/Panels/BattlePanel.cs` 已有 675 行基础框架：
- 血条刷新 ✅
- 战斗日志 ✅  
- 速度切换 ✅
- 结果弹窗 ✅
- 骰子技能按钮 ✅

### 需要新增的功能

#### 4.1 遗物效果图标栏
**位置**：战斗面板底部，水平排列的遗物图标条

```
┌──────────────────────────────────────┐
│ 🛡️ ⚔️ 🎲 ...  (最多10个遗物图标)    │
└──────────────────────────────────────┘
```

**数据源**：`RoguelikeGameManager.Instance.RelicSystem`
```csharp
// 接口
RelicSystem.GetAcquiredRelics() → List<RelicData>
// RelicData 字段: relicId, relicName, description, rarity, effectType, effectValue
```

**交互**：
- 鼠标悬停/长按 → 显示Tooltip（遗物名称 + 描述 + 稀有度星标）
- 遗物触发效果时 → 图标闪烁 + 缩放弹跳动画 (0.2s Punch scale)

**稀有度颜色**（与JSON relics.json对齐）：
```json
{
  "common":     "color(0.85, 0.85, 0.85)",  // 灰白
  "rare":       "color(0.26, 0.53, 0.96)",  // 蓝
  "epic":       "color(0.64, 0.21, 0.93)",  // 紫
  "legendary":  "color(1.0, 0.84, 0.0)"     // 金
}
```

**新增Inspector字段**：
```csharp
[Header("遗物栏")]
public RectTransform relicBar;
public GameObject relicIconPrefab;   // 遗物图标预制体
public int maxRelicDisplay = 10;
```

#### 4.2 骰子技能释放全屏特效
**触发**：点击骰子技能按钮 `diceSkillButton` → `BattleManager.TriggerDiceSkill()`

**动画序列**：
```
0.0s  全屏暗化 (半透明黑 Overlay alpha 0→0.6, 0.2s)
0.1s  骰子图标居中放大 (scale 0→2.0, Ease.OutBack, 0.4s)
0.3s  组合类型文字弹出 ("三条·AOE" 金色大字, 0.3s)
0.5s  全屏闪光 (白色 flash alpha 0→1→0, 0.15s)
0.6s  伤害数字飘出 (每个受击单位头顶 -XX 红字, stagger 0.1s)
0.8s  全屏暗化消退 (alpha 0.6→0, 0.3s)
```

**新增Inspector字段**：
```csharp
[Header("骰子技能特效")]
public Image screenOverlay;          // 全屏暗化层
public Image screenFlash;            // 全屏闪光层
public RectTransform diceSkillIcon;  // 居中骰子图标
public Text diceSkillLabel;          // 组合类型文字
public GameObject damageNumberPrefab; // 伤害飘字预制体
```

**组合类型对应特效颜色**（与 dice_system.json 对齐）：
```csharp
static readonly Color COMBO_COLOR_THREE = new Color(1f, 0.2f, 0.2f);    // 三条=红
static readonly Color COMBO_COLOR_STRAIGHT = new Color(0.2f, 0.6f, 1f); // 顺子=蓝  
static readonly Color COMBO_COLOR_PAIR = new Color(0.2f, 0.9f, 0.4f);   // 对子=绿
```

#### 4.3 战斗结束弹窗美化
**当前**：简单的缩放弹出 + 文字
**增强**：

胜利动画：
```
0.0s  弹窗从底部滑入 (anchoredPosition Y -500 → 0, Ease.OutBack, 0.5s)
0.2s  金色粒子散落效果 (如果有的话，没有就跳过)
0.3s  "🏆 战斗胜利！" 金色文字打字机效果
0.5s  副标题淡入 ("即将进入结算...")
0.5s  通关奖励预览（3个奖励类型图标，小尺寸预览）
2.5s  弹窗淡出 (0.5s) → 自动进入下一状态
```

失败动画：
```
0.0s  全屏红色闪动 (alpha 0→0.3→0, 0.3s)
0.1s  弹窗从中心缩小弹出 (scale 1.5→1.0, Ease.InOutQuad, 0.3s)
0.3s  "💀 战斗失败..." 红色文字
2.5s  弹窗淡出 → 返回主菜单
```

**新增Inspector字段**：
```csharp
[Header("结算奖励预览")]
public RectTransform rewardPreviewContainer;
public Image[] rewardPreviewIcons; // 3个小图标
```

### 数据对接

**ConfigLoader 使用方式**：
```csharp
// 获取英雄属性（从 hero_classes.json）
var classData = ConfigLoader.Instance.GetClassData("warrior");
// classData.base_stats.max_health, attack, defense, speed, crit_rate

// 获取遗物数据（从 relics.json）  
var relicData = ConfigLoader.Instance.GetRelicData("iron_shield");
// relicData.name_cn, description_cn, rarity, effect
```

**UIConfigBridge 桥接层**（已有）：
```csharp
UIConfigBridge.GetClassIcon(HeroClass.Warrior) → "⚔"
UIConfigBridge.GetStatDisplayText(StatType.Attack) → "攻击"
```

---

## FE-05 RoguelikeRewardPanel 增强（肉鸽奖励面板）

### 当前状态
`Assets/Scripts/UI/Panels/RoguelikeRewardPanel.cs` 已有 189 行基础框架：
- 3张奖励卡片 ✅
- 稀有度边框颜色 ✅
- 选择奖励逻辑 ✅

### 需要新增的功能

#### 5.1 卡片翻转入场动画
**入场序列**（面板打开时）：
```
0.0s  3张卡片从屏幕底部飞入 (Y +500 → 0, stagger 0.15s)
0.3s  卡片翻面动画 (绕Y轴旋转 180° → 0°, 0.4s, Ease.OutCubic)
0.5s  卡片微微浮动 (Y ±5px 循环, 每张相位错开)
```

**选中动画**：
```
0.0s  选中卡片放大到1.2x (0.3s, Ease.OutBack)
0.0s  未选中卡片灰化+缩小到0.8x (0.3s)
0.3s  选中卡片居中 + 翻转确认效果 (Y轴旋转 360°, 0.4s)
0.7s  全部卡片淡出 → 进入下一阶段
```

**新增Inspector字段**：
```csharp
[Header("卡片动画")]
public RectTransform[] cardRects;      // 3张卡片的RectTransform
public CanvasGroup[] cardCanvasGroups; // 3张卡片的CanvasGroup（用于灰化）
public float cardFloatAmplitude = 5f;  // 浮动振幅
public float cardFloatSpeed = 2f;      // 浮动速度
```

#### 5.2 奖励类型图标区分
4种奖励类型，每种用不同图标和底色：

```csharp
// 图标映射（可以用emoji或自定义sprite）
static readonly (string icon, Color bg)[] REWARD_STYLES = {
    (/* NewUnit */    "👥", new Color(0.2f, 0.6f, 1f)),    // 蓝色底
    (/* DiceUpgrade */ "🎲", new Color(1f, 0.85f, 0.2f)),  // 金色底
    (/* StatBoost */  "📈", new Color(0.2f, 0.85f, 0.4f)), // 绿色底
    (/* Relic */      "✨", new Color(0.64f, 0.21f, 0.93f)) // 紫色底
};
```

**数据映射**：
```csharp
// 从 RewardOption.Type 获取图标
private (string icon, Color bg) GetRewardStyle(RewardType type)
{
    return type switch
    {
        RewardType.NewUnit => REWARD_STYLES[0],
        RewardType.DiceFaceUpgrade => REWARD_STYLES[1],
        RewardType.StatBoost => REWARD_STYLES[2],
        RewardType.Relic => REWARD_STYLES[3],
        _ => ("?", Color.white)
    };
}
```

#### 5.3 遗物奖励详情弹窗
**触发**：点击遗物类型的奖励卡片 → 弹出详情弹窗（不是立即选择）

```
┌──────────────────────────────────────┐
│           ✨ 骰子大师                │
│          ★★ 稀有遗物               │
│                                      │
│  ┌──────────────────────────────┐    │
│  │   🎲 遗物图标（大）         │    │
│  └──────────────────────────────┘    │
│                                      │
│  每关额外+1次重摇                    │
│  可叠加 (最多3层)                    │
│                                      │
│  效果类型：骰子重摇                  │
│  触发时机：每关开始                  │
│                                      │
│    [取消]          [确认选择]        │
└──────────────────────────────────────┘
```

**新增类**：`RelicDetailPopup`（挂载到弹窗GameObject上）
```csharp
public class RelicDetailPopup : MonoBehaviour
{
    public Text relicNameText;
    public Text relicRarityText;
    public Image relicIcon;
    public Text relicDescText;
    public Text relicEffectTypeText;
    public Text relicTriggerText;
    public Button confirmButton;
    public Button cancelButton;
    
    private RewardOption pendingReward;
    private System.Action<RewardOption> onConfirm;
    
    public void Show(RelicData data, RewardOption reward, System.Action<RewardOption> confirmCallback) { ... }
    public void Hide() { ... }
}
```

**遗物稀有度文字**：
```csharp
static string GetRarityNameCN(string rarity) => rarity switch
{
    "common" => "普通遗物",
    "rare" => "稀有遗物", 
    "epic" => "史诗遗物",
    "legendary" => "传说遗物",
    _ => "遗物"
};

static string GetRarityStars(string rarity) => rarity switch
{
    "common" => "★",
    "rare" => "★★",
    "epic" => "★★★",
    "legendary" => "★★★★",
    _ => ""
};
```

#### 5.4 选择后卡片状态

非遗物类型（新单位/骰子强化/属性强化）：点击直接选中
遗物类型：点击先弹详情弹窗，确认后才选中

选中后的状态流程：
```
点击卡片
  ├─ 非遗物 → 直接进入选择流程
  └─ 遗物 → 弹出RelicDetailPopup
              ├─ 取消 → 关闭弹窗，回到三选一
              └─ 确认 → 进入选择流程

选择流程：
1. 调用 RoguelikeGameManager.Instance.ChooseReward(selectedReward)
2. 选中卡片放大居中 + 翻转
3. 其余卡片灰化缩小
4. 0.7s后自动关闭面板
5. GameStateMachine.Instance.NextState() 进入下一阶段
```

### 数据对接

**遗物数据来源**（双重获取，JSON优先）：
```csharp
// 方式1：从ConfigLoader读JSON（推荐）
var relicJson = ConfigLoader.Instance.GetRelicData("dice_master");
// relicJson.name_cn, description_cn, rarity, effect, trigger, stackable, max_stacks

// 方式2：从RewardSystem的内置数据库（兜底）
var rewardSystem = RoguelikeGameManager.Instance?.RewardSystem;
var relicData = rewardSystem?.GetRelicData("dice_master");
```

---

## 依赖关系

```
FE-04 BattlePanel增强
├── 依赖 ConfigLoader（已完成 ✅）
├── 依赖 RelicSystem（已完成 ✅）
├── 依赖 BattleManager 事件（已完成 ✅）
└── 依赖 DOTween（已有 ✅）

FE-05 RoguelikeRewardPanel增强  
├── 依赖 ConfigLoader（已完成 ✅）
├── 依赖 RoguelikeRewardSystem（已完成 ✅）
├── 依赖 RoguelikeGameManager（已完成 ✅）
└── 依赖 DOTween（已有 ✅）
```

## 文件修改清单

| 文件 | 操作 | 预估行数 |
|------|------|----------|
| `UI/Panels/BattlePanel.cs` | 修改（增强） | +200~300行 |
| `UI/Panels/RoguelikeRewardPanel.cs` | 修改（增强） | +150~200行 |
| `UI/Panels/RelicDetailPopup.cs` | 新增 | ~80行 |
| `UI/Components/RelicIconSlot.cs` | 新增（遗物图标组件） | ~50行 |
| `UI/Components/DamageNumber.cs` | 新增（伤害飘字组件） | ~40行 |

**总计预估**：+520~670行

## 优先级
**FE-04优先** → 战斗面板是玩家体验最核心的环节，骰子技能特效直接影响爽感。

---

*Generated by @hermes-CTO*
