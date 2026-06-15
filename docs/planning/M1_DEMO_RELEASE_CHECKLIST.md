# M1 Demo 发布清单

> **状态**: ✅ 代码层已就绪（commit c644bdc）
> **执行人**: 余涛源（在 Unity Editor 中操作）
> **预估时间**: 30-60 分钟（首次构建+测试）

---

## 一、代码准入状态

✅ **已通过 M1 Demo 准入审查**（详见 `M1_DEMO_READINESS.md`）

| 准入项 | 状态 |
|--------|------|
| 状态流闭环（MainMenu→Hero→Dice→Battle→Settlement→Reward→循环） | ✅ |
| 3职业可选（战/法/刺） | ✅ |
| 战斗系统完整（伤害/死亡/胜负/奖励） | ✅ |
| 三选一奖励生成与选择 | ✅ |
| 关卡无限推进（CurrentLevel++ 无上限） | ✅ |
| 存档/恢复主链 | ✅ |
| 崩溃风险（JSON 容错、敌人空列表、数组边界） | ✅ 已加防护 |

---

## 二、构建前自检（5 分钟）

在 Unity Editor 中执行：

### 1. 编译检查
- 打开 Unity → Console 面板
- 等待编译完成，**应无 Error**（Warning 可忽略）
- 若有 Error，停止构建，反馈给 @hermes-CTO

### 2. 资源检查
- `Assets/Resources/Data/` 下应有 ≥ 22 个 JSON 配置
- 关键配置：`hero_classes.json`, `relics.json`, `roguelike_map_config.json`, `event_configs.json`

### 3. 场景检查
- 主场景应包含：`GameManager`, `BattleManager`, `GridManager`, `SaveSystem`, `RoguelikeGameManager`
- 若缺，从 `Assets/Prefabs/` 拖入

---

## 三、Editor 内 Demo 验收（15 分钟）

按 `UNITY_TEST_GUIDE.md` 执行：

### 核心 5 大测试点

| # | 测试场景 | 通过标准 |
|---|----------|----------|
| 1 | 开局选英雄（战/法/刺） | 3 职业均可选中并进入下一阶段 |
| 2 | 掷骰子+重摇1次 | 骰子点数随机变化，重摇次数从 1 减到 0 |
| 3 | 战斗结算（点触释放/自动） | 伤害数字飘字、英雄/敌人血量下降、判定胜负 |
| 4 | 三选一奖励 | 弹出 3 个奖励卡，点击任意一个后进入下一关 |
| 5 | 连续 10 关 | 关卡递增，难度可感知，无崩溃 |

### 进阶测试（可选）
- 存档：玩 5 关 → 退出 → 重启 → 点"继续"应恢复到第 5 关
- 装备强化：在背包/装备面板强化一件装备，数值应有提升
- 商店购买：在商店购买物品，金币扣减、物品入背包

---

## 四、WebGL Demo 构建（15 分钟）

> 如需对外发布 Demo（浏览器可玩），执行此步。仅内部测试可跳过。

### 构建配置
详见 `docs/planning/WebGL-build-spec.md`

### 快速步骤
1. File → Build Settings
2. 切换到 WebGL 平台
3. Player Settings 按 `WebGL-build-spec.md` 配置
4. Build → 选择输出目录（如 `Builds/WebGL-Demo-v0.1`）
5. 构建完成后用本地服务器（如 `python3 -m http.server`）测试

### 包体目标
- 首包 ≤ 4MB（微信小游戏强制）
- 当前 Editor 阶段不强制，WebGL Demo 浏览器版可放宽到 10MB

---

## 五、Demo 发布后回滚点

构建成功后建议打 Tag：

```bash
cd /root/game_sxcg
git tag -a v0.1-demo -m "M1 Demo 发布版本"
git push origin v0.1-demo
```

---

## 六、已知限制（Demo 阶段可接受）

1. **Boss 机制未实现**：每 5 关的 Boss 战当前为精英怪降级（Phase 2 补全）
2. **微信分享未对接**：GameOverPanel 的分享按钮是 placeholder（Phase 3 补全）
3. **美术占位**：使用色块+文字，正式美术由市场对接（Phase 3）
4. **音效有限**：仅核心 BGM/打击音效，完整音效待补充

---

## 七、问题反馈通道

Demo 测试中遇到问题，反馈给 @hermes-CTO：

- **崩溃（Console Error）**：附 Console 完整堆栈
- **逻辑异常**：附场景+操作步骤+预期 vs 实际
- **平衡问题**：附关卡数+英雄配置+敌人配置

---

> Demo 验收通过后，正式进入 Phase 2（机制怪+遗物扩展+骰子面深度）。
