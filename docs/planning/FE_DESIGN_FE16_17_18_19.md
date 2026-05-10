# FE-16~19 前端体验优化任务详细设计

> CTO → @hermes-前端  
> 体验增强 + 收尾任务（不依赖后端）

---

## FE-16：设置面板 SettingsPanel

**新建文件**：`Assets/Scripts/UI/Panels/SettingsPanel.cs`

### 布局

```
┌──────────────────────────────────┐
│  ⚙ 设置                    [X]  │
├──────────────────────────────────┤
│                                  │
│  🔊 音效音量                     │
│  ████████░░░░░░ 70%             │
│                                  │
│  🎵 背景音乐                     │
│  ██████████░░░░ 80%             │
│                                  │
│  📱 画质                        │
│  [低] [中] [高]                  │
│                                  │
│  📳 屏幕震动        [✓ ON]      │
│                                  │
│  🌐 语言          [中文 ▼]      │
│                                  │
│  [恢复默认]                      │
└──────────────────────────────────┘
```

### 功能清单

1. **音量控制**
   - 两个 Slider（音效/BGM），范围 0~1
   - 滑动时实时预览音效（播放一个测试音）
   - 存 PlayerPrefs：`"sfx_volume"`, `"bgm_volume"`

2. **画质选择**
   - 三个 Toggle 按钮（低/中/高）
   - 低：关闭阴影、降低粒子质量、30fps cap
   - 中：简化阴影、正常粒子、45fps cap
   - 高：全效果、60fps
   - 通过 QualitySettings API 切换
   - 存 PlayerPrefs：`"quality_level"`

3. **屏幕震动开关**
   - Toggle，控制 DOTween 震动效果是否播放
   - 存 PlayerPrefs：`"screen_shake_enabled"`

4. **语言切换（预留）**
   - 下拉框（中文/English）
   - 当前只存 PlayerPrefs，不做实际文本切换
   - 存 PlayerPrefs：`"language"`

5. **恢复默认**
   - 一键恢复所有设置到默认值

### 技术要点
- 继承 UIPanel，注册到 NewUIManager（panelId = "Settings"）
- 入场动画：从右侧滑入（DOAnchorPosX 720→0）
- 所有设置变更通过事件通知：`SettingsManager.OnSettingsChanged`
- SettingsManager 用单例管理，其他面板读取设置时通过它

```csharp
// SettingsManager.cs（新建，轻量单例）
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }
    
    public float SfxVolume { get; private set; } = 0.7f;
    public float BgmVolume { get; private set; } = 0.8f;
    public int QualityLevel { get; private set; } = 2; // High
    public bool ScreenShakeEnabled { get; private set; } = true;
    
    void Awake() { /* 单例 + 从 PlayerPrefs 加载 */ }
    public void Save() { /* 写入 PlayerPrefs */ }
    public void ResetToDefault() { /* 恢复默认 */ }
}
```

**预估**：SettingsPanel.cs + SettingsManager.cs，+250行

---

## FE-17：GameOverPanel 战报增强

**修改文件**：`Assets/Scripts/UI/Panels/GameOverPanel.cs`

### 当前问题
- 击杀数是 placeholder（注释写了 `// Kill count placeholder`）
- 缺少战斗统计维度
- 没有动画效果

### 增强内容

```
┌──────────────────────────────────────┐
│  💀 英雄阵亡                         │
│            （或 ⭐ 通关成功！）         │
├──────────────────────────────────────┤
│                                      │
│  📊 战斗统计                         │
│                                      │
│  🏰 到达关卡    ████████  第 12 关   │  数字滚动
│  ⭐ 收集遗物    ████      5 件       │
│  💀 击杀总数    ██████    47 只      │
│  ⏱ 战斗时长    ███       23:45      │  mm:ss格式
│  🔥 最高连胜    █████     8 连胜     │
│  ⚔ 总伤害输出  ████████  12,450     │  带千位分隔符
│                                      │
│  🏆 MVP英雄                          │
│  ┌────────────────────────┐         │
│  │ 🗡 铁壁战士 ★★★        │         │
│  │ 输出: 5,200 | 承伤: 3,100│         │
│  │ 击杀: 18 | 助攻: 12     │         │
│  └────────────────────────┘         │
│                                      │
│  [再来一局]     [分享战绩]            │
└──────────────────────────────────────┘
```

### 动画效果

1. **数字滚动动画**：
   - 每个统计项数字从 0 滚动到实际值
   - DOTween：`DOTween.To(() => 0, x => text.text = x.ToString(), targetValue, 1.0f)`
   - 依次触发（间隔 0.2s）

2. **MVP英雄卡片**：
   - 延迟 0.5s 后从底部弹出（OutBack）
   - 金色边框脉冲光效
   - 英雄头像放大 + 星星亮起

3. **通关 vs 阵亡区分**：
   - 阵亡：标题红色 + 暗色调背景
   - 通关：标题金色 + 烟花粒子效果（可选）

### 数据来源

```csharp
// 从 RoguelikeGameManager 和 GameStateMachine 获取
var rgm = RoguelikeGameManager.Instance;
var gsm = GameStateMachine.Instance;

// 已有字段
int levelReached = rgm?.currentLevel ?? 0;
int relicCount = rgm?.collectedRelics?.Count ?? 0;

// 需要在 GameStateMachine 新增的统计字段（前端先 Mock）
int killCount = GameStateMachine.TotalKills;       // 总击杀
float battleDuration = GameStateMachine.SessionDuration; // 战斗时长
int maxWinStreak = GameStateMachine.MaxWinStreak;   // 最高连胜
long totalDamage = GameStateMachine.TotalDamageDealt; // 总伤害
Hero mvpHero = GameStateMachine.GetMvpHero();       // MVP英雄
```

> 前端先用 Mock 数据实现全部UI和动画，后端在 GameStateMachine 补充统计字段后联调。

**预估**：+180行

---

## FE-18：战斗节奏视觉优化

**修改文件**：`Assets/Scripts/UI/Panels/BattlePanel.cs`

### 增强内容

#### 1. 回合指示器

```
战斗面板顶部新增：
┌──────────────────────────────┐
│  Round 3/5    ⏱ 12s         │  回合数 + 倒计时
│  ▓▓▓░░                    │  回合进度条
└──────────────────────────────┘
```

- 每回合开始：回合数字放大弹出（1.5x→1.0x，0.3s）
- 最后一回合：数字变红 + "FINAL ROUND" 文字闪现

#### 2. 骰子组合屏幕特效

```
触发组合时全屏叠加层（0.5s 淡入淡出）：

三条（Triple）：
  - 全屏金色闪光（白色→透明，0.3s）
  - 顶部文字 "TRIPLE!" 从中心爆开
  
顺子（Straight）：
  - 蓝色流光从左到右扫过屏幕（DOFade + DOMoveX）
  - 顶部文字 "STRAIGHT!" 从左侧滑入

对子（Pair）：
  - 绿色脉冲（边框亮度提升）
  - 顶部文字 "PAIR!" 简单弹出

无组合：
  - 无特效（正常流程）
```

实现方式：
- 在 BattlePanel 中新增一个 Image overlay（全屏，初始透明）
- 组合评估后根据结果播放对应 tween 序列
- 不影响战斗逻辑，纯视觉层

#### 3. Boss阶段转换全屏过渡

```
Boss换阶段时：
1. 全屏黑幕淡入（0.3s）
2. Boss大图居中显示（占屏幕60%）
3. 阶段文字 "PHASE 2" 打字机效果
4. Boss属性变化提示（ATK↑ DEF↑ 等）
5. 1.5s后黑幕淡出，恢复战斗
```

- 从 MechanicEnemyState.currentPhase 获取阶段数
- Boss大图用 placeholder 色块即可

#### 4. 受击/治疗屏幕反馈

```
英雄受击：
  - 屏幕边缘红色脉冲（4个红色Image从边缘向中心渐变）
  - 轻微震动（0.1s，幅度根据伤害比例）
  - 受击音效（预留，SettingsManager.SfxVolume 控制）

英雄治疗：
  - 屏幕边缘绿色脉冲
  - 轻微放大恢复效果

Boss受击（大伤害）：
  - 屏幕震动加强（0.2s）
  - 白闪（暴击时）
```

### 技术要点
- 所有特效叠加层放在一个专用 Canvas 层级（"BattleFX"），不影响战斗UI交互
- 用对象池管理特效实例（避免频繁创建销毁）
- 震动强度读取 SettingsManager.ScreenShakeEnabled

**预估**：+200行

---

## FE-19：新手引导框架

**新建文件**：
- `Assets/Scripts/UI/Tutorial/TutorialManager.cs`
- `Assets/Scripts/UI/Panels/TutorialOverlayPanel.cs`
- `Assets/Resources/Data/tutorial_steps.json`

### 架构设计

```
TutorialManager（单例）
├── 加载 tutorial_steps.json
├── 管理当前步骤索引
├── 高亮目标UI元素（遮罩镂空）
├── 显示手势箭头和提示文字
└── 监听步骤完成条件

TutorialOverlayPanel（全屏覆盖层）
├── 半透明黑色遮罩（目标区域镂空）
├── 手势箭头（指向目标）
├── 提示气泡（步骤描述文字）
└── 点击任意处 → 下一步
```

### 引导步骤配置

```json
{
  "tutorial_id": "first_game",
  "steps": [
    {
      "step": 1,
      "title": "选择英雄",
      "description": "点击选择你的第一位英雄",
      "target_panel": "HeroSelect",
      "target_element": "hero_card_warrior",
      "highlight_shape": "rect",
      "arrow_direction": "down",
      "complete_condition": "hero_selected"
    },
    {
      "step": 2,
      "title": "掷骰子",
      "description": "点击掷骰按钮，开始战斗！",
      "target_panel": "DiceRoll",
      "target_element": "roll_button",
      "highlight_shape": "rect",
      "arrow_direction": "up",
      "complete_condition": "dice_rolled"
    },
    {
      "step": 3,
      "title": "观察战斗",
      "description": "英雄会根据骰子组合自动战斗",
      "target_panel": "Battle",
      "target_element": "battle_grid",
      "highlight_shape": "rect",
      "arrow_direction": "down",
      "complete_condition": "battle_round_complete"
    },
    {
      "step": 4,
      "title": "领取奖励",
      "description": "选择你的战斗奖励！",
      "target_panel": "RoguelikeReward",
      "target_element": "reward_card_1",
      "highlight_shape": "rect",
      "arrow_direction": "up",
      "complete_condition": "reward_selected"
    }
  ]
}
```

### 遮罩镂空实现

```csharp
// 核心技术：用 UI Graphic + RectMask2D 或 Shader 实现镂空
// 简单方案：4个黑色半透明Image围住目标区域（上/下/左/右）
// 目标区域不覆盖，形成"挖洞"效果

void UpdateHighlight(RectTransform target)
{
    // 获取目标在屏幕中的矩形
    Rect targetRect = GetScreenRect(target);
    
    // 调整4个遮罩Image的大小和位置
    topMask.rectTransform.sizeDelta = ...;
    bottomMask.rectTransform.sizeDelta = ...;
    leftMask.rectTransform.sizeDelta = ...;
    rightMask.rectTransform.sizeDelta = ...;
}
```

### 交互流程

1. 首次启动 → 检查 PlayerPrefs `"tutorial_completed"`
2. 未完成 → 自动进入引导模式
3. 每步：
   - 遮罩覆盖全屏
   - 目标元素镂空高亮（边框金色脉冲）
   - 手势箭头指向目标
   - 提示气泡显示描述
   - 用户操作目标 → 完成条件判定 → 下一步
4. 全部完成 → 标记 PlayerPrefs → 移除遮罩
5. 已完成用户 → 直接进入主菜单

### 手势箭头动画
- 上下浮动（DOAnchorPosY ±10px，循环）
- 指向目标方向的箭头（Image旋转）
- 步骤切换时箭头飞到新目标位置（0.3s OutCubic）

**预估**：TutorialManager.cs +200行 + TutorialOverlayPanel.cs +150行 = +350行

---

## 开发顺序

1. **FE-16 设置面板**（+250行，1天）— 最简单，直接开工
2. **FE-17 战报增强**（+180行，1天）— 纯UI+动画
3. **FE-18 战斗节奏**（+200行，1.5天）— 改BattlePanel
4. **FE-19 新手引导**（+350行，2天）— 新建框架，最复杂

总计约 +980行，5.5天工作量。全部不依赖后端。
