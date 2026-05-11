# FE-20：成就系统UI 接口定义 + 详细设计

> CTO → @hermes-前端（私聊）
> 成就系统前端任务，含完整接口定义

---

## 一、后端接口定义

### 1. AchievementData 数据模型

```csharp
/// <summary>
/// 成就数据 — 配置于 achievements.json
/// </summary>
[System.Serializable]
public class AchievementData
{
    public string id;              // 唯一ID，如 "first_kill", "triple_master"
    public string name;            // 显示名称
    public string description;     // 描述
    public string category;        // 分类：combat / exploration / collection / special
    public string iconId;          // 图标ID（前端映射到占位色块）
    public int tier;               // 等级：1=铜, 2=银, 3=金
    public int targetValue;        // 目标值（如击杀100只）
    public string statKey;         // 统计字段名，如 "total_kills", "triples_count"
    public string rewardType;      // 奖励类型：gold / gem / skin
    public int rewardAmount;       // 奖励数量
    public bool isHidden;          // 是否隐藏成就（未解锁前不显示详情）
}
```

### 2. AchievementProgress 运行时进度

```csharp
/// <summary>
/// 成就运行时进度 — 存 PlayerPrefs
/// </summary>
[System.Serializable]
public class AchievementProgress
{
    public string id;              // 对应 AchievementData.id
    public int currentValue;       // 当前进度值
    public bool isUnlocked;        // 是否已解锁
    public bool isRewardClaimed;   // 奖励是否已领取
    public long unlockTimestamp;   // 解锁时间戳（0=未解锁）
}
```

### 3. AchievementManager 单例接口

```csharp
/// <summary>
/// 成就管理器 — 后端 BE-17 实现，前端用 Mock
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }
    
    // ========== 查询接口 ==========
    
    /// <summary>
    /// 获取所有成就配置
    /// </summary>
    public List<AchievementData> GetAllAchievements()
    {
        // 从 achievements.json 加载
        // return ConfigLoader.Load<AchievementList>("achievements").items;
    }
    
    /// <summary>
    /// 获取所有成就进度
    /// </summary>
    public List<AchievementProgress> GetAllProgress()
    {
        // 从 PlayerPrefs 加载（JSON序列化）
    }
    
    /// <summary>
    /// 获取单个成就进度
    /// </summary>
    public AchievementProgress GetProgress(string achievementId)
    {
        // return progressMap.TryGetValue(achievementId, out var p) ? p : new() { id = achievementId };
    }
    
    /// <summary>
    /// 获取统计摘要
    /// </summary>
    public (int total, int unlocked, int rewardPending) GetSummary()
    {
        // total=总数, unlocked=已解锁, rewardPending=已解锁但未领奖
    }
    
    // ========== 事件接口 ==========
    
    /// <summary>
    /// 成就解锁事件（前端订阅此事件显示 Toast）
    /// </summary>
    public event Action<AchievementData> OnAchievementUnlocked;
    
    /// <summary>
    /// 成就进度更新事件（前端订阅此事件更新进度条）
    /// </summary>
    public event Action<string, int, int> OnProgressUpdated; // (id, current, target)
    
    // ========== 操作接口 ==========
    
    /// <summary>
    /// 领取奖励
    /// </summary>
    public void ClaimReward(string achievementId)
    {
        // 标记已领取 → 发放奖励到 PlayerInventory
    }
    
    /// <summary>
    /// 更新统计值（由后端各系统调用）
    /// </summary>
    public void UpdateStat(string statKey, int delta)
    {
        // 遍历所有成就，匹配 statKey → 更新进度 → 检查是否解锁
    }
}
```

### 4. achievements.json 配置

```json
{
  "items": [
    {
      "id": "first_kill",
      "name": "初次击杀",
      "description": "击败第一个敌人",
      "category": "combat",
      "iconId": "sword",
      "tier": 1,
      "targetValue": 1,
      "statKey": "total_kills",
      "rewardType": "gold",
      "rewardAmount": 50,
      "isHidden": false
    },
    {
      "id": "kill_100",
      "name": "百人斩",
      "description": "累计击杀100个敌人",
      "category": "combat",
      "iconId": "skull",
      "tier": 2,
      "targetValue": 100,
      "statKey": "total_kills",
      "rewardType": "gold",
      "rewardAmount": 500,
      "isHidden": false
    },
    {
      "id": "triple_master",
      "name": "三条大师",
      "description": "累计掷出10次三条",
      "category": "combat",
      "iconId": "dice",
      "tier": 2,
      "targetValue": 10,
      "statKey": "triples_count",
      "rewardType": "gold",
      "rewardAmount": 300,
      "isHidden": false
    },
    {
      "id": "relic_collector",
      "name": "遗物收藏家",
      "description": "单局收集8件遗物",
      "category": "collection",
      "iconId": "gem",
      "tier": 3,
      "targetValue": 8,
      "statKey": "max_relics_one_run",
      "rewardType": "gold",
      "rewardAmount": 800,
      "isHidden": false
    },
    {
      "id": "speed_demon",
      "name": "速通达人",
      "description": "在10分钟内通关",
      "category": "special",
      "iconId": "clock",
      "tier": 3,
      "targetValue": 600,
      "statKey": "fastest_clear_time",
      "rewardType": "gold",
      "rewardAmount": 1000,
      "isHidden": true
    },
    {
      "id": "boss_slayer",
      "name": "屠龙者",
      "description": "击败10个Boss",
      "category": "combat",
      "iconId": "dragon",
      "tier": 3,
      "targetValue": 10,
      "statKey": "boss_kills",
      "rewardType": "gold",
      "rewardAmount": 1000,
      "isHidden": false
    },
    {
      "id": "streak_5",
      "name": "五连胜",
      "description": "连续胜利5场",
      "category": "combat",
      "iconId": "fire",
      "tier": 2,
      "targetValue": 5,
      "statKey": "max_win_streak",
      "rewardType": "gold",
      "rewardAmount": 400,
      "isHidden": false
    },
    {
      "id": "straight_flush",
      "name": "顺子之王",
      "description": "累计掷出5次顺子",
      "category": "combat",
      "iconId": "dice",
      "tier": 2,
      "targetValue": 5,
      "statKey": "straights_count",
      "rewardType": "gold",
      "rewardAmount": 300,
      "isHidden": false
    },
    {
      "id": "perfect_boss",
      "name": "完美击杀",
      "description": "无伤击败Boss",
      "category": "special",
      "iconId": "shield",
      "tier": 3,
      "targetValue": 1,
      "statKey": "perfect_boss_kills",
      "rewardType": "gold",
      "rewardAmount": 1500,
      "isHidden": true
    },
    {
      "id": "all_heroes",
      "name": "全英雄精通",
      "description": "使用全部3个职业通关",
      "category": "collection",
      "iconId": "star",
      "tier": 3,
      "targetValue": 3,
      "statKey": "unique_hero_clears",
      "rewardType": "gold",
      "rewardAmount": 2000,
      "isHidden": false
    },
    {
      "id": "rich",
      "name": "富甲天下",
      "description": "单局累计获得2000金币",
      "category": "collection",
      "iconId": "coin",
      "tier": 2,
      "targetValue": 2000,
      "statKey": "max_gold_earned",
      "rewardType": "gold",
      "rewardAmount": 500,
      "isHidden": false
    },
    {
      "id": "level_20",
      "name": "深渊探索者",
      "description": "到达第20关",
      "category": "exploration",
      "iconId": "map",
      "tier": 3,
      "targetValue": 20,
      "statKey": "max_level_reached",
      "rewardType": "gold",
      "rewardAmount": 1000,
      "isHidden": false
    }
  ]
}
```

---

## 二、前端接口（前端实现）

### 1. AchievementPanel.cs

**panelId**: `"Achievement"`

**布局**：

```
┌──────────────────────────────────────┐
│  🏆 成就                     [X]    │
│  12/24 已解锁  ⭐ 3 待领奖          │  顶部摘要
├──────────────────────────────────────┤
│  [全部] [战斗] [收集] [探索] [特殊]  │  分类Tab
├──────────────────────────────────────┤
│  ┌────────────────────────────────┐ │
│  │ 🗡 初次击杀          ✅ 已完成  │ │
│  │ 击败第一个敌人                  │ │
│  │ ████████████████ 1/1           │ │  进度条满
│  │ [领取 50💰]                    │ │  待领奖按钮
│  ├────────────────────────────────┤ │
│  │ 💀 百人斩            银牌      │ │
│  │ 累计击杀100个敌人               │ │
│  │ ████████░░░░░░░ 47/100         │ │  进度中
│  │                                │ │
│  ├────────────────────────────────┤ │
│  │ ❓ ???                 ???     │ │  隐藏成就
│  │ 继续探索以解锁此成就            │ │
│  │ ░░░░░░░░░░░░░░░░ ???           │ │
│  │                                │ │
│  └────────────────────────────────┘ │
│  ──── 下拉加载更多 ────              │
└──────────────────────────────────────┘
```

**接口方法**：

```csharp
public class AchievementPanel : UIPanel
{
    [Header("UI引用")]
    public Text summaryText;          // "12/24 已解锁  ⭐ 3 待领奖"
    public Transform tabContainer;    // 分类Tab容器
    public Transform listContainer;   // 成就列表容器
    public GameObject achievementItemPrefab; // 单个成就卡片Prefab

    // 分类筛选
    private string currentCategory = "all";
    
    /// <summary>
    /// 初始化成就列表
    /// </summary>
    public void RefreshList(string category = "all")
    {
        // 1. 从 AchievementManager 获取所有成就 + 进度
        // 2. 按 category 过滤
        // 3. 排序：待领奖 > 进行中 > 已完成 > 隐藏
        // 4. 生成成就卡片列表
    }
    
    /// <summary>
    /// 创建单个成就卡片
    /// </summary>
    private void CreateAchievementItem(AchievementData data, AchievementProgress progress)
    {
        // 已解锁：完整显示 + 绿色边框 + 进度条满
        // 进行中：完整显示 + 蓝色边框 + 进度条
        // 隐藏未解锁：??? + 灰色 + 问号图标
        // 待领奖：金色边框脉冲 + "领取"按钮
    }
    
    /// <summary>
    /// 领取奖励
    /// </summary>
    private void OnClaimReward(string achievementId)
    {
        // AchievementManager.Instance.ClaimReward(achievementId);
        // 播放金币飞出动画
        // 刷新列表
    }
}
```

### 2. AchievementToast.cs

**挂载到**：全局 Canvas（不跟随面板开关）

```csharp
/// <summary>
/// 成就解锁弹窗通知 — 从屏幕顶部滑入，自动消失
/// </summary>
public class AchievementToast : MonoBehaviour
{
    public Image iconImage;
    public Text titleText;
    public Text descText;
    public RectTransform toastRect;
    
    /// <summary>
    /// 显示解锁通知
    /// </summary>
    public void Show(AchievementData achievement)
    {
        // 1. 设置内容
        titleText.text = $"🏆 成就解锁！{achievement.name}";
        descText.text = achievement.description;
        
        // 2. 入场动画：从顶部滑入
        toastRect.anchoredPosition = new Vector2(0, 200);
        toastRect.DOAnchorPosY(-50, 0.5f).SetEase(Ease.OutBack);
        
        // 3. 金色边框脉冲
        
        // 4. 2.5s后自动滑出
        DOTween.Sequence()
            .AppendInterval(2.5f)
            .Append(toastRect.DOAnchorPosY(200, 0.3f).SetEase(Ease.InBack))
            .OnComplete(() => gameObject.SetActive(false));
    }
}
```

### 3. 入场动画

```
成就解锁时：
1. Toast 从屏幕顶部滑入（OutBack，0.5s）
2. 金色闪光边框
3. 停留 2.5s
4. 自动滑出（InBack，0.3s）
5. 如果多个成就同时解锁，队列间隔 1s 依次弹出
```

### 4. 成就图标色块映射

```csharp
// 前端占位色块（无美术资源时用纯色替代）
private static readonly Dictionary<string, Color> IconColors = new()
{
    { "sword", new Color(0.9f, 0.3f, 0.3f) },    // 红-剑
    { "skull", new Color(0.5f, 0.5f, 0.5f) },     // 灰-骷髅
    { "dice", new Color(0.9f, 0.7f, 0.2f) },      // 金-骰子
    { "gem", new Color(0.4f, 0.7f, 1f) },          // 蓝-宝石
    { "clock", new Color(0.3f, 0.9f, 0.5f) },      // 绿-时钟
    { "dragon", new Color(0.8f, 0.2f, 0.8f) },     // 紫-龙
    { "fire", new Color(1f, 0.5f, 0.1f) },         // 橙-火
    { "shield", new Color(0.7f, 0.7f, 0.9f) },     // 银白-盾
    { "star", new Color(1f, 0.85f, 0.2f) },        // 金-星
    { "coin", new Color(1f, 0.8f, 0.1f) },         // 金-币
    { "map", new Color(0.6f, 0.4f, 0.2f) },        // 棕-地图
};
```

### 5. 等级视觉

```
tier 1 (铜)：铜色边框 + 铜色进度条
tier 2 (银)：银色边框 + 银色进度条
tier 3 (金)：金色边框 + 金色进度条 + 微光动画
```

---

## 三、集成点

### 1. 入口
- MainMenuPanel 标题区添加 🏆 成就按钮（与设置按钮并列）
- 点击 → `NewUIManager.Instance.ShowPanel("Achievement")`

### 2. 运行时监听
```csharp
// 在 RuntimeSceneBootstrap 或新建 AchievementToastManager 中订阅
void Start()
{
    AchievementManager.Instance.OnAchievementUnlocked += OnUnlocked;
}

void OnUnlocked(AchievementData data)
{
    // 实例化 AchievementToast Prefab → 调用 Show(data)
}
```

### 3. Mock 数据（前端先行）
```csharp
// #region Mock — 后端 BE-17 完成后删除
public static class MockAchievementManager
{
    public static List<AchievementData> GetAllAchievements() { /* 返回 12 条 Mock 数据 */ }
    public static List<AchievementProgress> GetAllProgress() { /* 返回混合进度 */ }
    public static (int, int, int) GetSummary() { return (12, 5, 2); }
    public static void SimulateUnlock(string id) { /* 触发 OnAchievementUnlocked 事件 */ }
}
// #endregion
```

---

## 四、文件清单

| 文件 | 类型 | 预估行数 |
|------|------|---------|
| `Assets/Scripts/UI/Panels/AchievementPanel.cs` | 修改 | +250 |
| `Assets/Scripts/UI/Components/AchievementToast.cs` | 新建 | +120 |
| `Assets/Resources/Data/achievements.json` | 新建 | ~100 |
| NewUIManager 注册 + MainMenuPanel 入口 | 修改 | +10 |

**总计**：+380行（不含JSON配置）

---

## 五、开发说明

1. 先用 MockAchievementManager 开发全部 UI + 交互 + 动画
2. 后端 BE-17 完成 AchievementManager 后替换 Mock
3. achievements.json 直接用上面定义的12条配置
4. Toast 是全局组件，不跟 AchievementPanel 生命周期绑定
5. 隐藏成就只在解锁时才显示详情
6. 领取奖励动画：金币从成就卡片飞向 HUD 金币图标
