# Phase 0 技术验证报告

> **版本**: v1.0  
> **日期**: 2026-05-08  
> **作者**: CTO  
> **状态**: 验证完成  
> **验证方式**: 离线代码分析 + 依赖审计 + 包体估算（服务器无Unity编辑器）  

---

## 执行摘要

| 项目 | 结论 |
|------|------|
| **最终判定** | ⚠️ **有条件 GO（CONDITIONAL GO）** |
| **核心风险** | 首包4MB限制——Unity WebGL空壳已达2.5-3.5MB，留给代码+资源的空间极其有限 |
| **置信度** | 中等（70%）——需在本地Unity编辑器实测确认 |
| **建议** | 立即在本地Unity编辑器执行第7节的验证步骤文档，1天内出实测结论 |

---

## 目录

1. [验证环境](#1-验证环境)
2. [项目代码资产审计](#2-项目代码资产审计)
3. [包体大小估算](#3-包体大小估算)
4. [性能可行性分析](#4-性能可行性分析)
5. [微信小游戏适配分析](#5-微信小游戏适配分析)
6. [GO/NO-GO 决策](#6-go-no-go-决策)
7. [本地验证步骤文档（待执行）](#7-本地验证步骤文档待执行)
8. [风险与缓解方案](#8-风险与缓解方案)
9. [结论与下一步行动](#9-结论与下一步行动)

---

## 1. 验证环境

### 1.1 验证条件

| 项目 | 状态 |
|------|------|
| Unity编辑器 | ❌ 未安装（服务器无GUI） |
| 构建能力 | ❌ 无法实际构建WebGL |
| 代码分析 | ✅ 完整审计完成 |
| 依赖分析 | ✅ 完成 |
| 包体估算 | ✅ 基于行业经验估算 |
| 实测验证 | ❌ 需本地Unity编辑器执行 |

### 1.2 验证方法

由于服务器环境无Unity编辑器和GUI，本次验证采用**离线分析**方式：
- 全量代码审计（38个脚本文件，5486行C#）
- 依赖关系梳理（Unity模块、第三方包使用情况）
- 基于Unity官方文档和行业经验进行包体估算
- 制定详细的本地验证步骤文档

---

## 2. 项目代码资产审计

### 2.1 项目概况

| 指标 | 数值 |
|------|------|
| Unity版本 | 2022.3 LTS |
| 脚本文件数 | 38个 |
| 总代码行数 | 5,486行 |
| 场景文件 | 1个（MainScene.unity, 5.8KB） |
| 美术资源 | 无（仅.gitkeep占位） |
| Prefab | 无 |
| 第三方插件 | 无 |

### 2.2 模块代码分布

| 模块 | 文件数 | 代码行数 | 保留/砍掉 | 复用率 |
|------|--------|----------|-----------|--------|
| Core/GameStateMachine | 1 | 198 | 保留改造 | 60% |
| Dice（骰子系统） | 4 | 521 | ✅ 直接复用 | 90% |
| Cards（卡牌系统） | 3 | 558 | ❌ 砍掉 | 0% |
| Heroes（英雄系统） | 3 | 337 | 保留改造 | 70% |
| Battle（战斗系统） | 4 | 563 | 保留改造 | 65% |
| Grid（棋盘系统） | 2 | 252 | 保留改造 | 50% |
| UI（界面系统） | 7 | 742 | ❌ 重写 | 10% |
| Data（数据配置） | 2 | 772 | 保留改造 | 60% |
| Level（关卡系统） | 2 | 245 | 保留改造 | 50% |
| Equipment（装备系统） | 3 | 201 | 保留改造 | 40% |
| Shop（商店系统） | 1 | 117 | ❌ 砍掉 | 0% |
| Player（背包系统） | 1 | 83 | 保留改造 | 30% |
| Events（随机事件） | 1 | 121 | ❌ 砍掉 | 0% |
| Tests（测试） | 1 | 304 | 保留参考 | — |
| Root（GameManager+Bootstrap） | 2 | 472 | 保留改造 | 70% |

### 2.3 核心骰子系统（最小可运行集）

仅运行核心骰子循环所需的代码：

| 文件 | 行数 | 说明 |
|------|------|------|
| GameStateMachine.cs | 198 | 游戏状态机 |
| Dice.cs | 53 | 单个骰子 |
| DiceRoller.cs | 154 | 骰子投掷管理 |
| DiceCombination.cs | 80 | 组合类型定义 |
| DiceCombinationEvaluator.cs | 144 | 组合评估算法 |
| GameManager.cs | 85 | 全局管理器 |
| **合计** | **714** | |

### 2.4 外部依赖分析

#### C# 命名空间使用

| 命名空间 | 使用次数 | 说明 |
|----------|----------|------|
| UnityEngine | 35 | 核心Unity API |
| System.Collections.Generic | 20 | List/Dictionary |
| UnityEngine.UI | 8 | Button/Text/Image |
| System | 4 | Action/Random |
| System.Collections | 3 | IEnumerator/协程 |
| System.Linq | 1 | GroupBy/OrderBy |

#### Unity API 使用热力图

| API | 使用频次 | WebGL必需 | 包体影响 |
|-----|----------|-----------|----------|
| GameObject | 60 | ✅ | 核心 |
| Debug | 85 | ✅ | 核心 |
| Vector2 | 86 | ✅ | 核心 |
| Mathf | 53 | ✅ | 核心 |
| Text (UI) | 49 | ✅ | UGUI模块 |
| RectTransform | 39 | ✅ | UGUI模块 |
| Button (UI) | 21 | ✅ | UGUI模块 |
| Image (UI) | 12 | ✅ | UGUI模块 |
| Color | 29 | ✅ | 核心 |
| MonoBehaviour | 21 | ✅ | 核心 |
| Destroy | 19 | ✅ | 核心 |
| Random | 15 | ✅ | 核心 |
| Canvas | 7 | ✅ | UGUI模块 |
| Coroutine/WaitForSeconds | 29 | ✅ | 核心 |
| Instantiate | 4 | ✅ | 核心 |
| Camera | 2 | ⚠️ | 可替换为UI方案 |
| FindObjectOfType | 5 | ✅ | 核心 |
| DontDestroyOnLoad | 3 | ✅ | 核心 |

#### Unity模块使用审计

| 模块 | manifest中 | 代码中使用 | 裁剪建议 | 预估节省 |
|------|-----------|-----------|----------|----------|
| **unity.modules.ai** | ✅ 引入 | ❌ 未使用（AutoChessAI是自定义类，非NavMesh） | 🔴 **必须裁剪** | ~200KB |
| **unity.modules.androidjni** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~50KB |
| **unity.modules.animation** | ✅ 引入 | ❌ 未使用（无Animator） | 🔴 **必须裁剪** | ~300KB |
| **unity.modules.cloth** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~100KB |
| **unity.modules.particlesystem** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~200KB |
| **unity.modules.physics** | ✅ 引入 | ❌ 未使用（GraphicRaycaster不依赖Physics） | 🔴 **必须裁剪** | ~250KB |
| **unity.modules.physics2d** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~200KB |
| **unity.modules.terrain** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~150KB |
| **unity.modules.terrainphysics** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~50KB |
| **unity.modules.tilemap** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~150KB |
| **unity.modules.video** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~200KB |
| **unity.modules.vr** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~100KB |
| **unity.modules.xr** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~100KB |
| **unity.modules.vehicles** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~100KB |
| **unity.modules.wind** | ✅ 引入 | ❌ 未使用 | 🔴 **必须裁剪** | ~50KB |
| **unity.modules.audio** | ✅ 引入 | ⚠️ 暂未使用，但后续需要 | 保留但精简 | — |
| **unity.modules.ui** | ✅ 引入 | ✅ 大量使用 | ✅ 保留 | — |
| **unity.modules.uielements** | ✅ 引入 | ❌ 未使用（没用UIElements/UI Toolkit） | 🔴 **裁剪** | ~300KB |
| **unity.modules.imgui** | ✅ 引入 | ❌ 未使用 | 🔴 **裁剪** | ~100KB |
| **unity.modules.director** | ✅ 引入 | ❌ 未使用（无Timeline） | 🔴 **裁剪** | ~100KB |
| **unity.modules.screencapture** | ✅ 引入 | ❌ 未使用 | 🔴 **裁剪** | ~50KB |
| **unity.modules.unityanalytics** | ✅ 引入 | ❌ 未使用 | 🔴 **裁剪** | ~50KB |
| **unity.modules.unitywebrequest*** | ✅ 引入 | ❌ 暂未使用 | 后续CDN需要时保留 | — |

#### Package依赖审计

| Package | 必要性 | 说明 | 裁剪建议 |
|---------|--------|------|----------|
| **com.unity.feature.2d** | ❌ 不必要 | 引入大量2D子包（2D Animation, IK, Pixel Perfect等），但代码中未使用任何2D精灵功能 | 🔴 **删除**，节省~500KB |
| **com.unity.textmeshpro** | ⚠️ 可选 | 代码中未使用TMP，只用Legacy Text | 🟡 **删除**，使用Legacy Text，节省~300KB |
| **com.unity.visualscripting** | ❌ 不必要 | 未使用 | 🔴 **删除**，节省~200KB |
| **com.unity.timeline** | ❌ 不必要 | 未使用 | 🔴 **删除**，节省~150KB |
| **com.unity.collab-proxy** | ❌ 不必要 | 版本控制用，运行时不需要 | 🔴 **删除** |
| **com.unity.ide.*** | ❌ 不必要 | IDE支持，不影响构建 | 不影响构建 |
| **com.unity.test-framework** | ❌ 构建时不需要 | 测试框架 | 构建时自动排除 |

---

## 3. 包体大小估算

### 3.1 Unity WebGL 构建产物构成

Unity WebGL构建产物主要包括：

| 文件 | 说明 | 体积特征 |
|------|------|----------|
| `Build/xxx.framework.js` | Unity运行时JS胶水层 | 固定开销 ~150-300KB |
| `Build/xxx.wasm` | 编译后的WebAssembly（IL2CPP编译的C++代码+C#代码+引擎代码） | 主要体积来源 |
| `Build/xxx.data` | 资源数据（纹理、音频、Mesh等） | 取决于资源量 |
| `Build/xxx.loader.js` | 加载器 | ~5-10KB |
| `TemplateData/` | HTML模板 | ~5KB |

### 3.2 体积估算模型

**关键参数**：

1. **Unity引擎基线（空场景 + IL2CPP + Brotli）**
   - Unity 2022.3 LTS 官方数据：空场景 WebGL 构建约 **2.5-3.5MB**（Brotli压缩后）
   - 其中 wasm 文件约 2-3MB，framework.js 约 200KB，data 约 200KB

2. **引擎模块裁剪节省**
   - 当前引入了 **22个 Unity 模块**，其中约 **16个未使用**
   - 估算可裁剪模块节省：**~1.5-2.5MB**（wasm体积，裁剪后）
   - **但注意**：Unity的模块裁剪并非线性节省，部分模块有交叉依赖

3. **Package裁剪节省**
   - 删除 feature.2d、textmeshpro、visualscripting、timeline 等
   - 估算节省：**~0.5-1MB**

4. **用户代码（5486行C#）**
   - IL2CPP 编译后 C# 代码体积约：**~100-300KB**（Brotli压缩后）
   - 714行核心代码约：**~30-80KB**

### 3.3 包体估算场景

| 场景 | 估算体积（Brotli压缩后） | 说明 |
|------|--------------------------|------|
| **场景A：空项目**（Unity 2022.3 LTS 空场景 + IL2CPP + High Strip） | **2.5-3.5MB** | 行业基线数据 |
| **场景B：空项目 + 裁剪模块**（去Physics/Animation/AI/Video等16个模块） | **1.5-2.0MB** | 估算，需实测 |
| **场景C：裁剪模块 + 核心代码**（骰子系统714行 + UGUI） | **1.8-2.5MB** | **MVP构建目标** |
| **场景D：全量代码 + 裁剪模块**（5486行 + UGUI） | **2.0-3.0MB** | 无美术资源时 |
| **场景E：全量代码 + 少量美术**（~500KB资源） | **2.5-3.5MB** | 接近4MB上限 |
| **场景F：完整游戏**（全代码 + 分包资源 + 微信适配插件） | **3.0-4.0MB** | ⚠️ 临界区间 |

### 3.4 关键结论

> **首包（代码+首屏资源）控制在4MB以内——理论可行但余量极小。**

- **乐观估计**：裁剪模块后核心代码构建 ≈ 1.8-2.5MB，留 1.5-2MB 给首屏资源 → ✅ 可行
- **悲观估计**：Unity模块裁剪效果不及预期，空壳仍 ≥ 3MB，留给资源仅 1MB → ⚠️ 极紧张
- **结论**：必须在本地实测确认裁剪后基线。如果空项目裁剪后 ≤ 2MB，则GO；如果 ≥ 3MB，则需启动Plan B

---

## 4. 性能可行性分析

### 4.1 渲染需求分析

本项目的渲染特征极为轻量：

| 特征 | 现状 | WebGL影响 |
|------|------|-----------|
| 3D渲染 | ❌ 不需要（纯2D UI游戏） | 无GPU压力 |
| 粒子系统 | ❌ 未使用 | 无开销 |
| 实时阴影 | ❌ 未使用 | 无开销 |
| 后处理 | ❌ 未使用 | 无开销 |
| 物理引擎 | ❌ 未使用 | 无CPU开销 |
| Animator | ❌ 未使用 | 无开销 |
| 同屏单位 | ≤ 6个（3v3） | 极低 |
| UI层级 | Canvas + Panel + Button/Text | 中等Draw Call |

**渲染分析结论**：本项目是纯UI驱动的2D游戏，无3D渲染、无物理、无粒子，WebGL性能压力极小。

### 4.2 性能估算

| 性能指标 | 目标 | 估算可行性 | 依据 |
|----------|------|-----------|------|
| 帧率 | ≥ 25fps | ✅ 高度可行 | 纯UI渲染，无GPU瓶颈 |
| 内存 | ≤ 200MB | ✅ 可行 | 无大纹理/网格资源 |
| 加载速度 | 冷启动 ≤ 5秒 | ⚠️ 取决于包体 | IL2CPP的wasm解析需要时间 |
| Draw Call | ≤ 50 | ✅ 可行 | 简单UI面板，合批友好 |

### 4.3 潜在性能瓶颈

1. **IL2CPP WASM解析时间**：首次加载时wasm文件解析可能耗时2-4秒，但微信小游戏有缓存机制
2. **UI重绘**：战斗中频繁更新血条/伤害数字，需使用对象池避免GC
3. **协程开销**：BattleManager使用协程驱动战斗循环，WebGL下协程切换有额外开销，但Tick制（0.3s/次）频率低

---

## 5. 微信小游戏适配分析

### 5.1 微信Unity适配插件

**插件名称**：微信小游戏 Unity 导出插件（官方）
**最新版本**：适配 Unity 2022.3 LTS
**安装方式**：通过 Unity Package Manager 导入 `.unitypackage`

**核心功能**：
- WebGL构建产物自动转换为微信小游戏格式
- 适配微信API（存储、音频、分享、广告等）
- 分包加载支持
- 性能分析工具集成

**服务器无法安装**：需要Unity编辑器GUI操作。本地验证时优先安装。

### 5.2 微信小游戏限制与适配

| 限制 | 影响 | 适配方案 |
|------|------|----------|
| 首包 ≤ 4MB | 核心风险 | 代码裁剪 + 资源分包 |
| WebGL 2.0支持 | 部分低端机仅支持WebGL 1.0 | 使用标准Shader，不用WebGL 2.0特有功能 |
| 音频格式限制 | 部分格式不支持 | 统一使用MP3 |
| 无同步XHR | 不能用同步网络请求 | 改为异步（当前代码无网络请求） |
| 内存限制 | 微信可能回收内存 | 控制资源加载，及时释放 |
| 分包大小 | 每个分包 ≤ 20MB | 足够用 |

### 5.3 当前代码的微信兼容性

| 兼容项 | 状态 | 说明 |
|--------|------|------|
| System.IO | ❌ 不使用 | 微信小游戏不支持本地文件IO |
| PlayerPrefs | ✅ 兼容 | 微信适配层会桥接到 wx.setStorageSync |
| Resources.Load | ✅ 兼容 | 微信适配层支持 |
| Coroutines | ✅ 兼容 | WebGL支持协程 |
| JSON序列化 | ✅ 兼容 | JsonUtility可用 |
| 随机数 | ✅ 兼容 | System.Random / UnityEngine.Random 均可用 |
| 多线程 | ⚠️ 限制 | WebGL不支持System.Threading，当前代码未使用 |

---

## 6. GO/NO-GO 决策

### 6.1 验证标准对照

| 验证项 | 标准 | 离线评估结果 | 判定 |
|--------|------|-------------|------|
| **首包大小** | ≤ 4MB | 估算 1.8-3.5MB（裁剪模块后） | ⚠️ 需实测确认 |
| **代码基线** | wasm ≤ 1.5MB | 裁剪后估算 1.0-2.0MB | ⚠️ 需实测确认 |
| **运行帧率** | ≥ 25fps | 纯UI游戏，高度可行 | ✅ 评估通过 |
| **内存占用** | ≤ 200MB | 无大资源，高度可行 | ✅ 评估通过 |
| **加载速度** | 冷启动 ≤ 5秒 | 取决于wasm大小 | ⚠️ 需实测确认 |
| **微信API兼容** | 核心API可用 | 代码无平台特定API | ✅ 评估通过 |
| **资源分包** | CDN分包可加载 | 架构可行，需搭建测试 | ⚠️ 需实测确认 |

### 6.2 最终判定

# ⚠️ 有条件 GO（CONDITIONAL GO）

### 6.3 判定理由

**支持GO的因素**：
1. ✅ **项目渲染极轻量**：纯UI 2D游戏，无3D、无物理、无粒子，WebGL性能无瓶颈
2. ✅ **代码量小**：5486行C#代码编译后仅~100-300KB，不是包体瓶颈
3. ✅ **模块裁剪空间大**：22个Unity模块中有16个未使用，4个Package可删除
4. ✅ **无美术资源**：当前零资源，首包几乎全是引擎代码
5. ✅ **微信兼容性好**：代码无平台特定API，适配成本低
6. ✅ **架构适合分包**：关卡/英雄/敌人资源天然适合按需加载

**风险因素**：
1. ⚠️ **Unity空壳偏大**：Unity 2022.3 WebGL空项目已达2.5-3.5MB，留给资源空间少
2. ⚠️ **裁剪效果不确定**：模块裁剪的节省量需实测，可能有交叉依赖导致裁不掉
3. ⚠️ **微信适配插件体积**：插件本身可能增加100-300KB
4. ⚠️ **IL2CPP编译不可控**：IL2CPP的代码生成可能有不可预测的体积

### 6.4 条件（必须在Phase 1启动前完成）

1. **24小时内完成本地实测**：按第7节步骤执行空项目构建，确认裁剪后基线
2. **实测判断标准**：
   - 空项目（裁剪模块后）≤ 2.0MB → **确认GO**
   - 空项目（裁剪模块后）2.0-3.0MB → **确认CONDITIONAL GO**，需极致优化
   - 空项目（裁剪模块后）> 3.0MB → **转为NO-GO**，启动Plan B

---

## 7. 本地验证步骤文档（待执行）

> **在本地Unity 2022.3 LTS编辑器上执行以下步骤，每步记录结果。**

### 7.1 准备工作

```
前置条件：
- Unity Hub 已安装
- Unity 2022.3 LTS 已安装（含 WebGL Build Support 模块）
- 微信开发者工具已安装
- 磁盘空间 ≥ 5GB（构建产物较大）
```

### 7.2 Step 1：空项目基线测量

**目的**：确认Unity 2022.3 WebGL构建的基线体积

**步骤**：
1. Unity Hub → New Project → 2D(Core) 模板 → 命名 "WebGLSizeTest"
2. File → Build Settings → WebGL → Switch Platform
3. Player Settings 配置：
   ```
   Company Name: Test
   Product Name: SizeTest
   Resolution: 960 x 600
   
   Other Settings:
     - Color Space: Gamma（Linear在WebGL性能差）
     - Auto Graphics API: 关闭 → 只留 WebGL 2.0
     - Managed Stripping Level: High
     - Strip Engine Code: ✅ 勾选
   
   Publishing Settings:
     - Compression Format: Brotli
     - Code Generation: IL2CPP（如选项存在）
   ```
4. Build → 选择空目录 → 等待构建完成
5. 测量构建产物：

```bash
# 在构建输出目录执行
ls -la Build/
du -sh Build/
echo "=== 各文件大小 ==="
ls -lh Build/
echo "=== 总大小 ==="
du -sh . --exclude=Editor/
```

**记录**：
| 文件 | 大小 |
|------|------|
| xxx.framework.js | ? MB |
| xxx.wasm | ? MB |
| xxx.data | ? MB |
| xxx.loader.js | ? KB |
| **总计** | **? MB** |

### 7.3 Step 2：模块裁剪后的基线

**目的**：测量裁剪未使用模块后的体积

**步骤**：
1. 打开 `Packages/manifest.json`
2. 移除以下模块（从 dependencies 中删除对应行）：
   ```json
   // 删除这些行：
   "com.unity.modules.ai": "1.0.0",
   "com.unity.modules.androidjni": "1.0.0",
   "com.unity.modules.animation": "1.0.0",
   "com.unity.modules.cloth": "1.0.0",
   "com.unity.modules.director": "1.0.0",
   "com.unity.modules.imgui": "1.0.0",
   "com.unity.modules.particlesystem": "1.0.0",
   "com.unity.modules.physics": "1.0.0",
   "com.unity.modules.physics2d": "1.0.0",
   "com.unity.modules.screencapture": "1.0.0",
   "com.unity.modules.terrain": "1.0.0",
   "com.unity.modules.terrainphysics": "1.0.0",
   "com.unity.modules.tilemap": "1.0.0",
   "com.unity.modules.uielements": "1.0.0",
   "com.unity.modules.vehicles": "1.0.0",
   "com.unity.modules.video": "1.0.0",
   "com.unity.modules.vr": "1.0.0",
   "com.unity.modules.wind": "1.0.0",
   "com.unity.modules.unityanalytics": "1.0.0",
   "com.unity.modules.unitywebrequest": "1.0.0",
   "com.unity.modules.unitywebrequestassetbundle": "1.0.0",
   "com.unity.modules.unitywebrequestaudio": "1.0.0",
   "com.unity.modules.unitywebrequesttexture": "1.0.0",
   "com.unity.modules.unitywebrequestwww": "1.0.0"
   ```
3. 同时删除不必要的Package：
   ```json
   // 删除这些行：
   "com.unity.feature.2d": "2.0.1",
   "com.unity.textmeshpro": "3.0.6",
   "com.unity.timeline": "1.7.6",
   "com.unity.visualscripting": "1.9.4",
   "com.unity.collab-proxy": "2.3.1"
   ```
4. 等待Unity刷新（可能需要重新编译）
5. 检查是否有编译错误（裁剪可能导致某些依赖缺失）
6. 如果有编译错误，根据错误信息逐步恢复必要模块
7. 重新 Build WebGL
8. 测量构建产物大小

**记录**：
| 文件 | 大小 | 与Step 1对比 |
|------|------|-------------|
| xxx.wasm | ? MB | ? |
| xxx.framework.js | ? MB | ? |
| xxx.data | ? MB | ? |
| **总计** | **? MB** | **节省 ? MB** |

### 7.4 Step 3：引入核心代码构建

**目的**：测量包含核心骰子系统代码的构建体积

**步骤**：
1. 在空项目中创建 `Assets/Scripts/` 目录
2. 复制以下文件到项目中：
   ```
   Assets/Scripts/Core/GameStateMachine.cs
   Assets/Scripts/Dice/Dice.cs
   Assets/Scripts/Dice/DiceRoller.cs
   Assets/Scripts/Dice/DiceCombination.cs
   Assets/Scripts/Dice/DiceCombinationEvaluator.cs
   Assets/Scripts/GameManager.cs
   ```
3. 创建最小启动场景：
   - 在Main Camera下创建空GameObject，挂载 `GameManager` 脚本
   - 确保编译通过
4. Build WebGL
5. 测量构建产物大小

**记录**：
| 文件 | 大小 | 与Step 2对比 |
|------|------|-------------|
| xxx.wasm | ? MB | +? KB |
| **总计** | **? MB** | **+? KB** |

### 7.5 Step 4：安装微信小游戏Unity插件

**目的**：测量微信适配插件增加的体积

**步骤**：
1. 下载微信小游戏Unity导出插件：
   - GitHub: https://github.com/wechat-miniprogram/minigame-unity-webgl-transform
   - 或从微信开放平台下载最新版
2. Assets → Import Package → Custom Package → 选择下载的 `.unitypackage`
3. 导入后检查编译是否通过
4. Build WebGL
5. 测量构建产物大小

**记录**：
| 文件 | 大小 | 与Step 3对比 |
|------|------|-------------|
| **总计** | **? MB** | **+? KB** |

### 7.6 Step 5：微信开发者工具运行测试

**目的**：验证在微信开发者工具中可正常运行

**步骤**：
1. 使用微信插件导出小游戏格式
2. 打开微信开发者工具 → 导入项目
3. 验证：
   - [ ] 启动画面正常显示
   - [ ] 控制台无严重报错
   - [ ] UI渲染正常
   - [ ] 点击事件响应正常
   - [ ] Debug.Log输出正常
4. 打开 Performance 面板：
   - 记录FPS
   - 记录内存占用
   - 记录首屏加载时间

**记录**：
| 性能指标 | 数值 | 是否达标 |
|----------|------|----------|
| FPS | ? | ≥ 25fps ? |
| 内存 | ? MB | ≤ 200MB ? |
| 冷启动时间 | ? 秒 | ≤ 5秒 ? |
| 首屏时间 | ? 秒 | ≤ 3秒 ? |

### 7.7 Step 6：真机测试

**目的**：在中低端真机上验证性能

**步骤**：
1. 微信开发者工具 → 预览 → 扫码
2. 在真机上运行
3. 测试设备建议：
   - 中低端Android（如 Redmi Note 系列、OPPO A系列）
   - 中低端iOS（如 iPhone SE、iPhone 8）
4. 记录帧率、内存、加载时间

### 7.8 结果汇总表

| 构建场景 | 总体积 | wasm大小 | data大小 |
|----------|--------|----------|----------|
| Step 1: 空项目基线 | ? MB | ? MB | ? MB |
| Step 2: 裁剪模块后 | ? MB | ? MB | ? MB |
| Step 3: +核心代码 | ? MB | ? MB | ? MB |
| Step 4: +微信插件 | ? MB | ? MB | ? MB |

**最终判定**（基于实测数据）：

- Step 4 总体积 ≤ 3.5MB → **✅ 确认 GO**
- Step 4 总体积 3.5-4.0MB → **⚠️ 确认 CONDITIONAL GO**
- Step 4 总体积 > 4.0MB → **❌ 转为 NO-GO，启动 Plan B**

---

## 8. 风险与缓解方案

### 8.1 首包4MB超限的应对方案

如果裁剪后仍超4MB，按以下优先级执行优化：

| 优先级 | 优化措施 | 预估节省 | 风险 |
|--------|----------|----------|------|
| P0 | IL2CPP + High Strip + Brotli | 已作为基线 | 无 |
| P0 | 裁剪16个未使用Unity模块 | ~1.5-2.5MB | 可能裁剪过度导致运行时错误 |
| P1 | 删除feature.2d/textmeshpro/visualscripting/timeline | ~0.5-1MB | 需确认无编译依赖 |
| P2 | 使用 link.xml 精确控制裁剪 | ~200KB | 需要反复测试 |
| P3 | 首包仅含加载器，游戏逻辑分包 | 使首包 < 1MB | 架构变更较大 |
| P4 | 使用微信小游戏WASM分包 | 分拆wasm | 依赖微信支持 |

### 8.2 Plan B：首包只含加载器

如果无论如何优化，代码都超过4MB：

```
方案B架构：
  首包（< 1MB）：
    ├── 微信小游戏加载器
    ├── 最小启动画面
    └── 资源加载框架
  
  分包1（首次加载，~2MB）：
    ├── 游戏核心wasm
    └── 核心UI资源
  
  分包2（按需加载）：
    ├── 英雄/敌人资源
    └── 音效
```

### 8.3 Plan C：换引擎

如果Unity WebGL完全不适用于微信小游戏（首包压不到可接受范围）：

| 方案 | 引擎 | 首包大小 | 代码复用 | 开发效率 |
|------|------|----------|----------|----------|
| C1 | Cocos Creator | ~1-2MB | 0%（JS/TS重写） | 中等 |
| C2 | Laya Air | ~1-2MB | 0%（JS/TS重写） | 中等 |
| C3 | 原生微信Canvas2D | ~100KB | 0%（JS重写） | 低效 |

**Plan C评估**：如果走到这一步，意味着全部重写，工期从8周延长到12-16周。应极力避免。

---

## 9. 结论与下一步行动

### 9.1 离线验证结论

基于代码审计和行业经验分析：

1. **首包4MB限制**是最大的技术风险，但**理论可行**——项目有以下有利条件：
   - 纯UI 2D游戏，渲染极轻量
   - 无美术资源，首包几乎全是引擎代码
   - 16个Unity模块未使用，裁剪空间大
   - 代码量5486行，编译后仅占100-300KB
   
2. **性能不是瓶颈**——纯UI + Tick制战斗 + 无物理/粒子/阴影

3. **微信兼容性好**——代码无平台特定API

4. **关键不确定因素**：Unity WebGL空壳裁剪后的实际体积，必须在本地实测确认

### 9.2 下一步行动

| 序号 | 行动 | 负责人 | 截止时间 | 优先级 |
|------|------|--------|----------|--------|
| 1 | 在本地Unity编辑器执行Step 1-4（包体测量） | 全栈 | 24小时内 | 🔴 P0 |
| 2 | 根据实测结果更新本报告，给出最终GO/NO-GO | CTO | Step 1完成后 | 🔴 P0 |
| 3 | 如确认GO：安装微信插件，执行Step 5-6 | 全栈 | 48小时内 | 🔴 P0 |
| 4 | 如确认CONDITIONAL GO：制定极致优化方案 | CTO | 48小时内 | 🟡 P1 |
| 5 | 如确认NO-GO：评估Plan B/C，启动技术方案调整 | CTO | 48小时内 | 🔴 P0 |

### 9.3 对Phase 1的影响

- **如果确认GO**：按原计划启动Phase 1（核心玩法开发），第3周开始
- **如果CONDITIONAL GO**：Phase 1正常启动，但需在Phase 1中同步进行包体优化（持续测量每次构建产物）
- **如果NO-GO**：暂停Phase 1，先用1-2周评估Plan B（加载器分包方案）或Plan C（换引擎）

---

## 附录

### A. 裁剪后的 manifest.json 参考

以下为裁剪后推荐的 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.unity.ide.visualstudio": "2.0.22",
    "com.unity.ide.vscode": "1.2.5",
    "com.unity.test-framework": "1.1.35",
    "com.unity.modules.assetbundle": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.umbra": "1.0.0"
  }
}
```

> **注意**：这是极限裁剪方案，可能导致编译错误。实际裁剪时需根据编译错误逐步恢复必要模块。建议从全量开始，逐步删除并测试。

### B. link.xml 配置参考

```xml
<linker>
  <!-- 保留所有游戏代码的反射能力 -->
  <assembly fullname="Assembly-CSharp" preserve="all"/>
  
  <!-- 保留UGUI必要的反射 -->
  <assembly fullname="UnityEngine.UI">
    <type fullname="UnityEngine.UI.*" preserve="all"/>
  </assembly>
  
  <!-- 保留JSON序列化需要的反射 -->
  <assembly fullname="UnityEngine.JSONSerializeModule" preserve="all"/>
</linker>
```

### C. WebGL Player Settings 优化清单

```
Player Settings:
  ├── Resolution and Presentation
  │   ├── Default Screen Width: 960
  │   ├── Default Screen Height: 600
  │   └── Fullscreen Mode: Windowed
  ├── Other Settings
  │   ├── Color Space: Gamma
  │   ├── Auto Graphics API: ❌ → Manual: WebGL 2.0 only
  │   ├── Managed Stripping Level: High
  │   ├── Strip Engine Code: ✅
  │   └── IL2CPP Code Generation: Faster (smaller) builds
  ├── Publishing Settings
  │   ├── Compression Format: Brotli
  │   ├── Data caching: ✅
  │   └── Debug Symbols: ❌ (Release build)
  └── Quality Settings
      ├── WebGL: Low
      └── VSync: Don't Sync
```

### D. 参考数据来源

1. Unity官方WebGL优化文档：https://docs.unity3d.com/Manual/webgl-optimizing.html
2. 微信小游戏Unity适配文档：https://developers.weixin.qq.com/minigame/dev/guide/game-engine/unity.html
3. Unity 2022.3 LTS WebGL Build Size 基线数据（社区测试报告）
4. 微信小游戏包体限制官方说明：首包4MB，总包20MB

---

> **文档结束**  
> **关键行动：请在24小时内完成本地Step 1-4的实测验证，确认最终的GO/NO-GO判定。**
