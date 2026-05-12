# FE-30 性能优化 — 对象池审计报告

## 1. Instantiate 审计清单

| # | 文件 | 调用位置 | 频率 | 池化优先级 | 状态 |
|---|------|---------|------|-----------|------|
| 1 | `DamagePopup.cs:73` | `Instantiate(popupPrefab, popupParent)` | **极高**（每帧多次） | P0 | ✅ 已池化 |
| 2 | `DamageNumber.cs:144` | `new GameObject("DmgNum")` | **极高**（每次伤害/治疗） | P0 | ✅ 已池化 |
| 3 | `BattleEffectManager.cs` | `CreatePooledObject` | 高 | P1 | ✅ 已有内部池 |
| 4 | `AchievementToast.cs:129` | `new GameObject("AchievementToast")` | 中（成就解锁时） | P1 | ✅ 已接入 ObjectPoolManager |
| 5 | `BattlePanel.cs:543` | `Instantiate(relicIconPrefab, ...)` | 中（面板打开时） | P2 | 🔲 待后续 |
| 6 | `BattlePanel.cs:1096` | `Instantiate(unitBarPrefab, ...)` | 中（每回合创建） | P2 | 🔲 待后续 |
| 7 | `CardPlayPanel.cs:198` | `Instantiate(cardItemPrefab, ...)` | 中（手牌生成） | P2 | 🔲 待后续 |
| 8 | `DiceUpgradePanel.cs:177` | `Instantiate(faceSlotPrefab, ...)` | 低 | P3 | 🔲 待后续 |
| 9 | `EquipPanel.cs:311/586` | `Instantiate(heroItemPrefab/backpackItemPrefab)` | 低 | P3 | 🔲 待后续 |
| 10 | `EventPanel.cs:322/1025` | `Instantiate(optionButtonPrefab, ...)` | 低 | P3 | 🔲 待后续 |
| 11 | `RoguelikeRewardPanel.cs:304` | `Instantiate(rewardCardPrefab, ...)` | 低 | P3 | 🔲 待后续 |
| 12 | `ShopPanel.cs:353/588` | `Instantiate(shopItemPrefab, ...)` | 低 | P3 | 🔲 待后续 |
| 13 | `SettlementPanel.cs:421` | `Instantiate(heroExpCardPrefab, ...)` | 极低 | P4 | 🔲 待后续 |
| 14 | `SaveLoadPanel.cs:225` | `Instantiate(saveSlotTemplate, ...)` | 极低 | P4 | 🔲 待后续 |

**Data.Game.cs:776** `Object.Instantiate(eq)` — 数据克隆，非 UI，不池化
**DiceSkillCinematic.cs:141/235** — 临时对象，一次性使用，不池化
**CardDeck.cs:149/160** — Hero 对象，生命周期由管理器控制
**UI 面板预制体创建** — 面板级 Instantiate，频率低，非关键路径

## 2. Destroy 审计清单

| # | 文件 | 调用位置 | 类型 | 状态 |
|---|------|---------|------|------|
| 1 | `DamagePopup.cs:137` | `Destroy(go)` | 动画结束销毁 | ✅ 改为池化回收 |
| 2 | `DamageNumber.cs:89` | `OnDestroy` | `Destroy(gameObject)` | 动画结束销毁 | ✅ 改为池化回收 |
| 3 | `AchievementToast.cs:120` | `Destroy(toast)` | Toast 滑出销毁 | ⚠️ 可池化但频率低 |
| 4 | `BattleEffectManager.cs` | `Destroy(go)` | 特效结束销毁 | ✅ 已有池化 |
| 5 | `DiceSkillCinematic.cs:141/235` | `Destroy(go)` | 过场动画临时 | 🔲 一次性使用 |
| 6 | `BattleGridPanel.cs:142` | `Destroy(child) | 网格清理 | 🔲 面板级清理 |
| 7 | 单例 `Destroy(gameObject)` | 多个文件 | 重复单例 | 🔲 正常行为 |
| 8 | `CardDeck.cs:149/160` | `Destroy(hero)` | 英雄销毁 | 🔲 由管理器控制 |

**核心发现：高频 Destroy 只有 DamagePopup 和 DamageNumber，已修复。**

## 3. 实施总结

### 已交付文件:
1. `Assets/Scripts/Core/ObjectPoolManager.cs` — 全局对象池管理器 (~8.
   - DamagePopup 池（20 预创建）
   - DamageNumber 池（30 预创建）
   - Toast 池（10 预创建）
2. `Assets/Scripts/Battle/DamagePopup.cs` — 池化改造
   - ShowDamage/ShowHeal/ShowText 现在通过 ObjectPoolManager 获取对象
   - 动画完成后自动回收到池，不再 Destroy
   - 保留 prefab 回退路径
3. `Assets/Scripts/UI/Components/DamageNumber.cs` — 池化改造
   - Play 完成后自动回收而非销毁
   - Spawn 静态方法优先从池获取
   - 保留旧版程序化创建作为回退
4. `Assets/Editor/DrawCallStatsTool.cs` — DrawCall 统计 Editor 工具
   - Menu: `Tools > FE-30 > 统计 DrawCall`
   - 按 Canvas 分组统计 Graphic/材质/预估 DrawCall
   - 标注问题项并输出可复制报告
5. `Assets/Editor/MemorySnapshotTool.cs` — 内存快照 Editor 工具
   - Menu: `Tools > FE-30 > 内存快照工具`
   - 每 5 秒自动采样
   - 泄漏检测（趋势分析）
   - 快照对比报告

### 后续优化建议:
- P2 优先级面板预制体池化（BattlePanel unitBar、CardPlayPanel 手牌）
- P3 优先级场景面板预制体池化（ShopPanel、EquipPanel 等）
- 结合 `DrawCallStatsTool` 发现独立材质 Image → 建议使用 Sprite Atlas 合批
- 结合 `MemorySnapshotTool` 定期采样，监控 Texture 内存泄漏
