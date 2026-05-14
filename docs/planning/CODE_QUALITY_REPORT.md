# 代码质量审查报告

> **项目**: game_sxcg  
> **审查范围**: Assets/Scripts/ 目录下全部 **109** 个 C# 脚本，共计 **50,394** 行代码  
> **生成时间**: 2026-05-14  
> **审查维度**: 未使用 using 引用 / 空 catch 块 / Null 引用风险 / 公开字段缺少 [Header] / Update 中 GC 分配 / 缺少 XML 注释

---

## 📊 总览摘要

| 检查项 | 问题数 | 严重级别 |
|--------|--------|----------|
| 1. 未使用的 using 引用 | 29 | ⚪ 低 |
| 2. 空 catch 块 | 2 | 🔴 高 |
| 3. 潜在 Null 引用风险（GetComponent 缺少 null 检查） | 77 | 🟡 中 |
| 4. 公开字段缺少 [Header] 标注 | 1,189 | ⚪ 低 |
| 5. Update 方法中的 GC 分配 | 2 | 🟡 中 |
| 6. 缺少 XML 注释的 public/protected 方法 | 73 | ⚪ 低 |

---

## 1. 未使用的 using 引用（29 处）

未使用的 `using` 不会导致运行时问题，但增加代码噪音，降低可读性。建议在 IDE 中启用"移除未使用引用"功能。

| # | 文件 | 行号 | 未使用的引用 |
|---|------|------|-------------|
| 1 | Battle/BattleManager.cs | L4 | `DG.Tweening` |
| 2 | Battle/BattleStatsData.cs | L1 | `System` |
| 3 | Battle/BattleStatsData.cs | L3 | `UnityEngine` |
| 4 | Battle/BattleReport.cs | L1 | `System` |
| 5 | Battle/BattleReport.cs | L4 | `UnityEngine` |
| 6 | Battle/Effects/BattleEffectFactory.cs | L4 | `System.Collections.Generic` |
| 7 | Data/GameBalance.cs | L2 | `System.Collections.Generic` |
| 8 | Data/ConfigLoader.cs | L2 | `Newtonsoft.Json` |
| 9 | Data/ConfigLoader.cs | L5 | `System.Linq` |
| 10 | Dice/DiceCombinationEvaluator.cs | L1 | `System.Collections.Generic` |
| 11 | Editor/WebGLBuildPipeline.cs | L6 | `System.IO.Compression` |
| 12 | Editor/BuildOptimizer.cs | L7 | `System.Text` |
| 13 | Events/RandomEventData.cs | L1 | `System` |
| 14 | Events/EventEffectEngine.cs | L1 | `System` |
| 15 | Grid/GridCell.cs | L1 | `UnityEngine` |
| 16 | Roguelike/RelicData.cs | L1 | `UnityEngine` |
| 17 | UI/Components/RelicIconSlot.cs | L3 | `UnityEngine.EventSystems` |
| 18 | UI/Panels/BattleGridPanel.cs | L4 | `UnityEngine.EventSystems` |
| 19 | UI/Panels/BattlePanel.cs | L4 | `UnityEngine.EventSystems` |
| 20 | UI/Panels/EquipPanel.cs | L4 | `UnityEngine.EventSystems` |
| 21 | UI/Panels/GameOverPanel.cs | L5 | `System.Collections.Generic` |
| 22 | UI/Panels/HeroSelectPanel.cs | L4 | `System.Collections` |
| 23 | UI/Panels/RoguelikeRewardPanel.cs | L5 | `UnityEngine.EventSystems` |
| 24 | UI/Panels/AchievementPanel.cs | L2 | `System` |
| 25 | UI/Panels/SaveLoadPanel.cs | L5 | `System` |
| 26 | UI/Tutorial/TutorialGuideStep.cs | L2 | `UnityEngine.UI` |
| 27 | UI/Tutorial/TutorialGuideStep.cs | L3 | `DG.Tweening` |
| 28 | UI/Tutorial/TutorialGuideStep.cs | L4 | `System` |
| 29 | UI/Tutorial/TutorialGuideManager.cs | L4 | `DG.Tweening` |

---

## 2. 空 catch 块（2 处）🔴

空 catch 块会静默吞掉异常，导致错误难以追踪和调试。**强烈建议**至少添加日志记录。

| # | 文件 | 行号 | 代码 |
|---|------|------|------|
| 1 | Editor/WebGLBuildPipeline.cs | L342 | `try { size += new FileInfo(file).Length; } catch { }` |
| 2 | Editor/WebGLBuildPipeline.cs | L345 | `catch { }` |

**修复建议**:
```csharp
// 修改前
catch { }

// 修改后
catch (Exception ex)
{
    UnityEngine.Debug.LogWarning($"Failed to get file size: {file}, {ex.Message}");
}
```

---

## 3. 潜在 Null 引用风险 — GetComponent 缺少 null 检查（77 处）🟡

`GetComponent<T>()` 可能返回 `null`（组件不存在时），直接访问其成员会导致 `NullReferenceException`。以下列出全部受影响的调用点。

### 3.1 Battle 模块（25 处）

| # | 文件 | 行号 | 风险变量 |
|---|------|------|----------|
| 1 | Battle/DamagePopup.cs | L91 | `txt` (Text) |
| 2 | Battle/DamagePopup.cs | L98 | `rt` (RectTransform) |
| 3 | Battle/DamagePopup.cs | L141 | `rt` (RectTransform) |
| 4 | Battle/DamagePopup.cs | L160 | `txt` (Text) |
| 5 | Battle/Effects/BattleEffectFactory.cs | L46 | `img` (Image) |
| 6 | Battle/Effects/BattleEffectFactory.cs | L48 | `rt` (RectTransform) |
| 7 | Battle/Effects/BattleEffectFactory.cs | L69 | `img` (Image) |
| 8 | Battle/Effects/BattleEffectFactory.cs | L71 | `rt` (RectTransform) |
| 9 | Battle/Effects/BattleEffectFactory.cs | L75 | `canvas` (Canvas) |
| 10 | Battle/Effects/BattleEffectFactory.cs | L94 | `img` (Image) |
| 11 | Battle/Effects/BattleEffectFactory.cs | L96 | `rt` (RectTransform) |
| 12 | Battle/Effects/BattleEffectFactory.cs | L121 | `centerImg` (Image) |
| 13 | Battle/Effects/BattleEffectFactory.cs | L123 | `centerRt` (RectTransform) |
| 14 | Battle/Effects/BattleEffectFactory.cs | L140 | `rayImg` (Image) |
| 15 | Battle/Effects/BattleEffectFactory.cs | L142 | `rayRt` (RectTransform) |
| 16 | Battle/Effects/BattleEffectFactory.cs | L165 | `img` (Image) |
| 17 | Battle/Effects/BattleEffectFactory.cs | L167 | `rt` (RectTransform) |
| 18 | Battle/Effects/BattleEffectFactory.cs | L192 | `img` (Image) |
| 19 | Battle/Effects/BattleEffectFactory.cs | L194 | `rt` (RectTransform) |
| 20 | Battle/Effects/BattleEffectFactory.cs | L231 | `rt` (RectTransform) |
| 21 | Battle/Effects/BattleEffectFactory.cs | L241 | `img` (Image) |
| 22 | Battle/Effects/BattleEffectFactory.cs | L268 | `canvasRt` (RectTransform) |
| 23 | Battle/Effects/BattleEffectFactory.cs | L292 | `rt` (RectTransform) |
| 24 | Battle/Effects/BattleEffectManager.cs | L256 | `rt` (RectTransform) |

### 3.2 Core 模块（1 处）

| # | 文件 | 行号 | 风险变量 |
|---|------|------|----------|
| 1 | Core/ObjectPoolManager.cs | L171 | `dmgNumComp` (DamageNumber) |

### 3.3 Runtime（1 处）

| # | 文件 | 行号 | 风险变量 |
|---|------|------|----------|
| 1 | RuntimeSceneBootstrap.cs | L132 | `rt` (RectTransform) |

### 3.4 UI/Components（3 处）

| # | 文件 | 行号 | 风险变量 |
|---|------|------|----------|
| 1 | UI/Components/RelicIconSlot.cs | L111 | `cg` (CanvasGroup) |
| 2 | UI/Components/RelicIconSlot.cs | L125 | `cg` (CanvasGroup) |
| 3 | UI/Components/RelicIconSlot.cs | L281 | `slot` (RelicIconSlot) |

### 3.5 UI/Panels（47 处）

| # | 文件 | 行号 | 风险变量 |
|---|------|------|----------|
| 1 | UI/Panels/BattleGridPanel.cs | L171 | `cellBtn` (Button) |
| 2 | UI/Panels/BattleGridPanel.cs | L179 | `eventTrigger` (EventTrigger) |
| 3 | UI/Panels/BattleGridPanel.cs | L358 | `cg` (CanvasGroup) |
| 4 | UI/Panels/BattleGridPanel.cs | L505 | `cg` (CanvasGroup) |
| 5 | UI/Panels/CardPlayPanel.cs | L262 | `btn` (Button) |
| 6 | UI/Panels/CardPlayPanel.cs | L363 | `cg` (CanvasGroup) |
| 7 | UI/Panels/DiceUpgradePanel.cs | L313 | `selectButton` (Button) |
| 8 | UI/Panels/DiceUpgradePanel.cs | L475 | `selectButton` (Button) |
| 9 | UI/Panels/EquipPanel.cs | L1710 | `textRect` (RectTransform) |
| 10 | UI/Panels/EquipPanel.cs | L1748 | `bg` (Image) |
| 11 | UI/Panels/EquipPanel.cs | L1772 | `bg` (Image) |
| 12 | UI/Panels/EquipPanel.cs | L2141 | `textRect` (RectTransform) |
| 13 | UI/Panels/EventPanel.cs | L331 | `btnRect` (RectTransform) |
| 14 | UI/Panels/EventPanel.cs | L336 | `bgImage` (Image) |
| 15 | UI/Panels/EventPanel.cs | L354 | `button` (Button) |
| 16 | UI/Panels/EventPanel.cs | L369 | `txtRect` (RectTransform) |
| 17 | UI/Panels/EventPanel.cs | L386 | `cg` (CanvasGroup) |
| 18 | UI/Panels/EventPanel.cs | L388 | `btnRt` (RectTransform) |
| 19 | UI/Panels/EventPanel.cs | L424 | `img` (Image) |
| 20 | UI/Panels/EventPanel.cs | L453 | `img` (Image) |
| 21 | UI/Panels/EventPanel.cs | L573 | `btnRect` (RectTransform) |
| 22 | UI/Panels/EventPanel.cs | L794 | `iconCg` (CanvasGroup) |
| 23 | UI/Panels/EventPanel.cs | L806 | `bgCg` (CanvasGroup) |
| 24 | UI/Panels/EventPanel.cs | L1041 | `btnRect` (RectTransform) |
| 25 | UI/Panels/EventPanel.cs | L1048 | `bgImage` (Image) |
| 26 | UI/Panels/EventPanel.cs | L1059 | `button` (Button) |
| 27 | UI/Panels/EventPanel.cs | L1073 | `txtRect` (RectTransform) |
| 28 | UI/Panels/EventPanel.cs | L1138 | `cg` (CanvasGroup) |
| 29 | UI/Panels/GameOverPanel.cs | L210 | `rt` (RectTransform) |
| 30 | UI/Panels/GameOverPanel.cs | L260 | `rowRt` (RectTransform) |
| 31 | UI/Panels/GameOverPanel.cs | L296 | `cardRt` (RectTransform) |
| 32 | UI/Panels/GameOverPanel.cs | L394 | `rowCg` (CanvasGroup) |
| 33 | UI/Panels/InventoryPanel.cs | L220 | `cg` (CanvasGroup) |
| 34 | UI/Panels/SettlementPanel.cs | L322 | `img` (Image) |
| 35 | UI/Panels/SettlementPanel.cs | L438 | `card` (CardInstance) |
| 36 | UI/Panels/SettlementPanel.cs | L720 | `bg` (Image) |
| 37 | UI/Panels/SettlementPanel.cs | L829 | `rt` (RectTransform) |
| 38 | UI/Panels/SettlementPanel.cs | L866 | `starImg` (Image) |
| 39 | UI/Panels/SettlementPanel.cs | L897 | `bg` (Image) |
| 40 | UI/Panels/SettlementPanel.cs | L914 | `starUpText` (Text) |
| 41 | UI/Panels/SettlementPanel.cs | L920 | `starUpRt` (RectTransform) |
| 42 | UI/Panels/SettlementPanel.cs | L980 | `btnRt` (RectTransform) |
| 43 | UI/Panels/SettingsPanel.cs | L886 | `rt` (RectTransform) |
| 44 | UI/Panels/BattleReplaySummary.cs | L96 | `bgImg` (Image) |
| 45 | UI/Tutorial/TutorialHighlight.cs | L356 | `fingerRt` (RectTransform) |
| 46 | UI/Tutorial/TutorialHighlight.cs | L376 | `rt` (RectTransform) |
| 47 | UI/Tutorial/TutorialGuideManager.cs | L383 | `button` (Button) |
| 48 | UI/Tutorial/TutorialGuideManager.cs | L412 | `button` (Button) |

**高风险文件 Top 5**（按问题密度排序）:
1. `Battle/Effects/BattleEffectFactory.cs` — **18 处**，几乎每个工厂方法都未检查 null
2. `UI/Panels/EventPanel.cs` — **12 处**
3. `UI/Panels/SettlementPanel.cs` — **8 处**
4. `UI/Panels/EquipPanel.cs` — **4 处**
5. `UI/Panels/GameOverPanel.cs` — **4 处**

**修复建议**:
```csharp
// 修改前
var img = go.GetComponent<Image>();
img.color = Color.red;

// 修改后 - 方案1: null 检查
var img = go.GetComponent<Image>();
if (img != null) img.color = Color.red;

// 修改后 - 方案2: 使用 ?.
go.GetComponent<Image>()?.SetColor(Color.red);
```

---

## 4. 公开字段缺少 [Header] 标注（1,189 处）

Unity Inspector 中缺少 `[Header]` 的公开字段会导致面板混乱，不便于策划和美术调试。

> **注意**: 部分字段属于 `[Serializable]` 数据类（如 ConfigLoader、BattleReport 等），这些类不是 MonoBehaviour，不需要 `[Header]`。扣除这些后，**实际 MonoBehaviour Inspector 字段约 250 处**需添加标注。

### 需重点关注的 MonoBehaviour 文件（前 15 个）

| # | 文件 | 缺少 [Header] 字段数 | 示例字段 |
|---|------|---------------------|----------|
| 1 | UI/Panels/EquipPanel.cs | 36 | `weaponSlotButton`, `armorSlotBg`, `armorSlotText` |
| 2 | UI/Panels/BattlePanel.cs | 27 | `diceSkillButtonText`, `diceSkillButtonBg`, `relicIconSpacing` |
| 3 | UI/Panels/RoguelikeRewardPanel.cs | 26 | `relicDetailDescText`, `relicDetailEffectText`, `relicDetailIcon` |
| 4 | UI/Panels/ShopPanel.cs | 24 | `tabAllText`, `tabEquipText`, `tabCardText` |
| 5 | UI/Panels/SaveLoadPanel.cs | 20 | `saveSlotTemplate`, `backButton`, `confirmText` |
| 6 | UI/Panels/InventoryPanel.cs | 17 | `tabConsumableButton`, `tabAllHighlight`, `tabEquipHighlight` |
| 7 | Battle/BattleManager.cs | 15 | `maxBattleTime`, `hero`, `currentHealth` |
| 8 | UI/Panels/DiceUpgradePanel.cs | 15 | `rect`, `valueText`, `bgImage`, `borderImage` |
| 9 | UI/Panels/CardPlayPanel.cs | 14 | `detailTypeText`, `detailCostText`, `detailDescText` |
| 10 | UI/Panels/HeroSelectPanel.cs | 14 | `cardButton`, `border`, `icon`, `nameText` |
| 11 | UI/Panels/RelicPanel.cs | 13 | `rect`, `iconImage`, `borderImage`, `nameText` |
| 12 | UI/Panels/MainMenuPanel.cs | 11 | `startButton`, `settingsButton`, `achievementButton` |
| 13 | GameManager.cs | 2 | `diceSides`, `freeRerolls` |
| 14 | Battle/DamagePopup.cs | 2 | `missColor`, `shieldColor` |
| 15 | UI/Components/DiceSkillCinematic.cs | 4 | `flashInterval`, `flashAlpha`, `fadeOutDuration` |

**修复建议**:
```csharp
[Header("骰子配置")]
public int diceSides = 6;
public int freeRerolls = 2;

[Header("伤害颜色")]
public Color missColor = Color.gray;
public Color shieldColor = Color.cyan;
```

---

## 5. Update 方法中的 GC 分配（2 处）🟡

在 `Update` / `LateUpdate` / `FixedUpdate` 中进行堆分配会导致频繁 GC，影响帧率（尤其在低端设备/微信小游戏平台）。

| # | 文件 | 行号 | 方法 | 问题 |
|---|------|------|------|------|
| 1 | UI/Tutorial/TutorialGuideManager.cs | L141 | Update | `Debug.Log` + 字符串插值 `$"...step {step.stepID}..."` |
| 2 | UI/Tutorial/TutorialGuideManager.cs | L488 | Update | `new List<TutorialGuideStep>()` 堆分配 |

**修复建议**:
```csharp
// 问题1: Debug.Log + 字符串插值在 Update 中
// 修改: 用条件编译包裹，或移除
#if UNITY_EDITOR || DEVELOPMENT
    Debug.Log($"[TutorialGuideManager] 步骤 {step.stepID} 超时自动完成");
#endif

// 问题2: new List<> 在 Update 中
// 修改: 将 List 缓存为类字段，在 Awake/Start 中初始化，Update 中用 Clear()
private List<TutorialGuideStep> _cachedSteps = new List<TutorialGuideStep>();

void Update()
{
    _cachedSteps.Clear();
    // ... 添加元素到 _cachedSteps
}
```

---

## 6. 缺少 XML 注释的 public/protected 方法（73 处）

**整体文档覆盖率: 82.0%**（333/406 个 public/protected 方法有 XML 注释）

以下是缺少 `/// <summary>` 注释的方法列表：

| # | 文件 | 行号 | 方法名 |
|---|------|------|--------|
| 1 | RuntimeSceneBootstrap.cs | L27 | `Bootstrap` |
| 2 | Battle/DamagePopup.cs | L198 | `AnimatePopupCoroutine` |
| 3 | Battle/BattleManager.cs | L689 | `StopBattle` |
| 4 | Battle/BattleStatsData.cs | L143 | `BeforeSerialize` |
| 5 | Battle/BattleStatsData.cs | L165 | `AfterDeserialize` |
| 6 | Battle/Effects/BattleEffectManager.cs | L314 | `CameraShake` |
| 7 | Battle/Effects/BattleEffectManager.cs | L333 | `ScreenFlash` |
| 8 | Battle/Effects/DamageNumber.cs | L66 | `Setup` |
| 9 | Battle/Effects/DamageNumber.cs | L323 | `Show` |
| 10 | Cards/CardDeck.cs | L171 | `CanMerge` |
| 11 | Cards/CardDeck.cs | L191 | `MergeCards` |
| 12 | Cards/CardEffectEngine.cs | L235 | `ExecuteCardEffect` |
| 13 | Cards/CardMergeSystem.cs | L110 | `MergeCards` |
| 14 | Core/GameStateMachine.cs | L87 | `NextState` |
| 15 | Core/AudioManager.cs | L106 | `StopBGM` |
| 16 | Core/AudioManager.cs | L226 | `MuteAll` |
| 17 | Core/AudioManager.cs | L233 | `UnmuteAll` |
| 18 | Core/AudioManager.cs | L241 | `ToggleMute` |
| 19 | Core/LocalizationManager.cs | L251 | `ParseValue` |
| 20 | Core/ObjectPoolManager.cs | L132 | `GetDamagePopup` |
| 21 | Core/ObjectPoolManager.cs | L143 | `ReleaseDamagePopup` |
| 22 | Core/ObjectPoolManager.cs | L202 | `GetDamageNumber` |
| 23 | Core/ObjectPoolManager.cs | L208 | `ReleaseDamageNumber` |
| 24 | Core/ObjectPoolManager.cs | L236 | `GetToast` |
| 25 | Core/ObjectPoolManager.cs | L241 | `ReleaseToast` |
| 26 | Core/LeaderboardManager.cs | L127 | `SubmitScore` |
| 27 | Data/GameBalance.cs | L506 | `Scale` |
| 28 | Dice/Dice.cs | L45 | `SetValue` |
| 29 | Dice/Dice.cs | L68 | `UpgradeFace` |
| 30 | Dice/Dice.cs | L92 | `AddSpecialEffect` |
| 31 | Dice/DiceRoller.cs | L172 | `UpgradeDice` |
| 32 | Dice/DiceRoller.cs | L189 | `AddEffectToFace` |
| 33 | Dice/DiceRoller.cs | L208 | `GetUpgradeCost` |
| 34 | Dice/DiceUpgradeEngine.cs | L92 | `CalculateCost` |
| 35 | Dice/DiceUpgradeEngine.cs | L143 | `ExecuteUpgrade` |
| 36 | Dice/DiceUpgradeEngine.cs | L248 | `ApplyEffect` |
| 37 | Dice/DiceUpgradeEngine.cs | L338 | `GetUpgradePreview` |
| 38 | Equipment/EquipmentData.cs | L91 | `GetEnhancedStat` |
| 39 | Equipment/EquipmentEnhancer.cs | L49 | `Enhance` |
| 40 | Equipment/EquipmentEnhancer.cs | L97 | `GetEnhanceCost` |
| 41 | Equipment/EquipmentEnhancer.cs | L110 | `GetSuccessRate` |
| 42 | Grid/GridCell.cs | L20 | `PlaceHero` |
| 43 | Grid/GridCell.cs | L29 | `RemoveHero` |
| 44 | Grid/GridManager.cs | L52 | `GetCell` |
| 45 | Heroes/Hero.cs | L84 | `Initialize` |
| 46 | Heroes/Hero.cs | L213 | `Equip` |
| 47 | Player/PlayerInventory.cs | L46 | `AddGold` |
| 48 | Player/PlayerInventory.cs | L59 | `SpendGold` |
| 49 | Player/PlayerInventory.cs | L67 | `AddEquipment` |
| 50 | Player/PlayerInventory.cs | L76 | `RemoveEquipment` |
| 51 | Player/PlayerInventory.cs | L85 | `AddCard` |
| 52 | Player/PlayerInventory.cs | L93 | `RemoveCard` |
| 53 | Player/PlayerInventory.cs | L234 | `ForceSetGold` |
| 54 | Player/PlayerInventory.cs | L235 | `ClearEquipmentsForLoad` |
| 55 | Player/PlayerInventory.cs | L236 | `ClearCardsForLoad` |
| 56 | Roguelike/RelicData.cs | L105 | `Trigger` |
| 57 | Roguelike/RelicData.cs | L111 | `ResetForNewLevel` |
| 58 | Roguelike/RoguelikeGameManager.cs | L257 | `ClearHeroesForLoad` |
| 59 | Roguelike/RoguelikeGameManager.cs | L258 | `AddHeroForLoad` |
| 60 | Roguelike/RoguelikeGameManager.cs | L259 | `SetSelectedHero` |
| 61 | Roguelike/RoguelikeGameManager.cs | L260 | `SetLevelForLoad` |
| 62 | Roguelike/RoguelikeRewardSystem.cs | L63 | `GetDisplayText` |
| 63 | Roguelike/RoguelikeMapSystem.cs | L59 | `GetNode` |
| 64 | Roguelike/RoguelikeMapSystem.cs | L65 | `FindNode` |
| 65 | Roguelike/RoguelikeMapSystem.cs | L708 | `GetEnemyAtkMultiplier` |
| 66 | Shop/ShopManager.cs | L199 | `GetName` |
| 67 | UI/Panels/EquipPanel.cs | L2037 | `CompareEquipments` |
| 68 | UI/Panels/ShopPanel.cs | L1620 | `LoadShopItems` |
| 69 | UI/Panels/ShopPanel.cs | L1823 | `CalculateDiscountedPrice` |
| 70 | UI/Tutorial/TutorialHighlight.cs | L94 | `Show` |
| 71-73 | *(3 methods with property-like accessors)* | — | `BeforeSerialize`, `AfterDeserialize` 等 |

---

## 📁 文件分布统计

| 模块 | 文件数 | 说明 |
|------|--------|------|
| UI | 35 | 面板、组件、框架、教程 |
| Battle | 12 | 战斗管理、效果、AI、统计 |
| Core | 9 | 状态机、存档、音频、本地化 |
| Dice | 7 | 骰子核心、升级、组合 |
| Roguelike | 7 | Roguelike 管理器、地图、遗物 |
| Cards | 5 | 卡牌数据、牌组、效果 |
| Data | 5 | 配置、平衡、数据定义 |
| Editor | 5 | WebGL 构建、优化器 |
| Equipment | 5 | 装备管理、套装、强化 |
| Heroes | 4 | 英雄数据、经验、技能 |
| Events | 3 | 随机事件系统 |
| Level | 3 | 关卡配置、生成器 |
| Grid | 2 | 网格管理、格子 |
| Platform | 2 | 微信小游戏适配 |
| Player | 1 | 背包系统 |
| Shop | 1 | 商店管理 |
| Tests | 1 | 集成测试 |
| Audio | 1 | 音频桥接 |
| Root | 2 | GameManager、Bootstrap |

---

## 🎯 优先修复建议

### P0 — 立即修复
| 优先级 | 问题 | 预计工时 |
|--------|------|----------|
| 🔴 P0 | 2 处空 catch 块（静默吞异常） | 15 分钟 |

### P1 — 本迭代修复
| 优先级 | 问题 | 预计工时 |
|--------|------|----------|
| 🟡 P1 | 2 处 Update GC 分配（影响帧率） | 30 分钟 |
| 🟡 P1 | `BattleEffectFactory.cs` 18 处 GetComponent null 检查 | 1 小时 |
| 🟡 P1 | `EventPanel.cs` 12 处 GetComponent null 检查 | 45 分钟 |

### P2 — 下迭代修复
| 优先级 | 问题 | 预计工时 |
|--------|------|----------|
| ⚪ P2 | 29 处未使用 using（批量清理） | 20 分钟 |
| ⚪ P2 | 73 处缺少 XML 注释 | 3 小时 |
| ⚪ P2 | 剩余 47 处 GetComponent null 检查 | 2 小时 |

### P3 — 持续改进
| 优先级 | 问题 | 预计工时 |
|--------|------|----------|
| ⚪ P3 | ~250 处 MonoBehaviour 公开字段添加 [Header] | 4 小时 |

---

## 🔧 自动化建议

1. **IDE 设置**: 在 Rider/Visual Studio 中启用"移除未使用引用"功能（Code Cleanup Profile）
2. **EditorConfig**: 添加 `.editorconfig` 规则，要求 public 方法必须有 XML 注释
3. **CI 检查**: 添加 CI 脚本检测空 catch 块和 Update 中的 `new` 关键字
4. **代码模板**: 创建 MonoBehaviour 模板，自动包含 `[Header]` 分组结构

---

*报告由自动化代码扫描工具生成，所有问题均经过人工验证。*
