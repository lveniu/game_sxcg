# WebGL 构建指南 + 包体分析 SOP

> 本文档适用于 **GameSXCG（骰子驱动 Roguelike）** 项目的 WebGL 构建，目标平台为微信小游戏。
> 首包限制：**4MB**（framework.wasm + framework.js）。

---

## 1. 构建前检查清单

### 1.1 环境要求

| 检查项 | 要求 | 验证方法 |
|--------|------|----------|
| Unity 版本 | 2021.3 LTS 或更高 | Help → About Unity |
| WebGL Build Support 模块 | 已安装 | Unity Hub → Installs → Add Module |
| IL2CPP 模块 | 已安装 | Unity Hub → Installs → Add Module |
| 磁盘剩余空间 | ≥ 5 GB（IL2CPP 编译需要大量临时空间） | 资源管理器查看 |
| .NET SDK | 无额外要求（IL2CPP 内置） | — |

### 1.2 PlayerSettings 关键配置

打开 **Edit → Project Settings → Player**，按以下表格逐项确认：

| 配置项 | 路径 | 设定值 | 说明 |
|--------|------|--------|------|
| Scripting Backend | Player → Other Settings → Scripting Backend | **IL2CPP** | 微信小游戏要求，性能更优 |
| API Compatibility Level | Player → Other Settings → Api Compatibility Level | **.NET Standard 2.1** | 更小包体，兼容性足够 |
| Managed Stripping Level | Player → Other Settings → Managed Stripping Level | **High** | 去除未使用的托管代码 |
| Strip Engine Code | Player → Other Settings → Strip Engine Code | **true** | 去除未使用的引擎模块 |
| WebGL Compression | Player → Publishing Settings → Compression Format | **Brotli** | 最佳压缩比，微信支持 |
| Color Space | Player → Other Settings → Color Space | **Gamma** | WebGL 兼容性更好 |
| Graphics API | Player → Other Settings → Auto Graphics API → 取消勾选 | **WebGL 2.0 only** | 性能最优，移除 WebGL 1.0 |
| Exception Support | Player → Other Settings → Il2Cpp Exception Support | **Explicitly Thrown Only** | 减小 wasm 体积 |
| Code Generation | Player → Other Settings → Il2Cpp Code Generation | **OptimizeSize** | 优先体积优化 |
| Development Build | Build Settings → Development Build | **OFF** | Release 模式构建 |

### 1.3 快速自检脚本

在 Unity Console 中执行以下菜单（如有）验证关键配置：

```
GameSXCG → Validate Settings → WebGL
```

如果菜单不存在，请手动按上表逐项检查。

---

## 2. 一键构建

### 2.1 Editor 菜单构建

1. 打开 Unity Editor
2. 菜单栏：**GameSXCG → Build WebGL**
3. 等待构建完成，Console 会输出包体报告

### 2.2 命令行构建（macOS / Linux）

```bash
unity-editor \
  -quit \
  -batchmode \
  -projectPath . \
  -executeMethod BuildWebGL.Build \
  -buildTarget WebGL
```

### 2.3 命令行构建（Windows）

```cmd
"C:\Program Files\Unity\Hub\Editor\2021.3.XX\Editor\Unity.exe" ^
  -quit ^
  -batchmode ^
  -projectPath . ^
  -executeMethod BuildWebGL.Build ^
  -buildTarget WebGL
```

> **注意**：将 `2021.3.XX` 替换为实际安装的 Unity 版本号。

### 2.4 构建输出

- **输出目录**：`Build/WebGL/`
- **关键文件**：`Build/WebGL/Build/` 目录下的 `.wasm`、`.js`、`.data` 文件
- **构建脚本**：`Assets/Editor/BuildWebGL.cs`

### 2.5 构建日志

命令行构建时，日志输出到：

| 平台 | 日志路径 |
|------|----------|
| macOS | `~/Library/Logs/Unity/Editor.log` |
| Windows | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| Linux | `~/.config/unity3d/Editor.log` |

---

## 3. 包体分析 SOP

### 3.1 自动报告

构建完成后，Console 会自动输出包体大小报告，包含：

- 各文件类型大小汇总
- 与基线数据对比
- 是否超出微信 4MB 限制的警告

### 3.2 手动检查

检查 `Build/WebGL/Build/` 目录下各文件大小：

```bash
# Linux/macOS
ls -lh Build/WebGL/Build/

# Windows PowerShell
Get-ChildItem Build/WebGL/Build/ | Format-Table Name, Length
```

### 3.3 文件类型拆分说明

| 文件类型 | 说明 | 通常占比 | 优化方向 |
|----------|------|----------|----------|
| `.wasm` | C# 经 IL2CPP 编译后的 WebAssembly 代码 | 40-60% | Strip Level、OptimizeSize、Exception Support |
| `.js` | JavaScript 框架/胶水代码 | 5-10% | 通常较难优化，压缩后占比小 |
| `.data` / `.unityweb` | 资源数据（纹理、音频、场景等） | 20-40% | Addressables 分包、纹理压缩、音频压缩 |
| `.assets` / `.resS` | 额外资源文件 | 0-20% | Addressables 远程加载 |

### 3.4 基线数据文件

基线数据存储在：`docs/build/webgl_baseline.json`

```json
{
  "version": "v0.1",
  "date": "YYYY-MM-DD",
  "total_size_bytes": 0,
  "files": {
    "framework.wasm": 0,
    "framework.js": 0,
    "data.unityweb": 0
  },
  "wechat_4mb_pass": false,
  "notes": "首次基线构建"
}
```

每次构建后应更新此文件，用于版本间对比。

---

## 4. 微信 4MB 基准

### 4.1 限制说明

微信小游戏首包限制为 **4MB**，计算公式：

```
首包大小 = framework.wasm + framework.js（解压前）
```

> 4MB = 4,194,304 字节

### 4.2 基线对比方法

1. 构建完成后运行包体分析
2. 对比 `docs/build/webgl_baseline.json` 中记录的基线数据
3. 关注增量变化：如果单次构建增长超过 **200KB**，需排查原因

```bash
# 快速检查 framework 文件大小
du -b Build/WebGL/Build/framework.wasm
du -b Build/WebGL/Build/framework.js
```

### 4.3 超标处理方案

按优先级排序：

| 优先级 | 方案 | 说明 | 预期收益 |
|--------|------|------|----------|
| 1 | **Addressables 分包** | 资源远程加载，不进首包 | 首包减小 30-60% |
| 2 | **Strip 更激进** | 添加 `link.xml` 白名单，确保不误删 | 精确控制代码体积 |
| 3 | **代码剥离** | 移除未使用的 `System.*` / `Unity.*` 模块 | 减小 wasm 5-15% |
| 4 | **压缩格式验证** | 对比 Brotli vs Gzip 实际压缩效果 | 通常 Brotli 更优，需验证 |

### 4.4 link.xml 白名单维护

`link.xml` 文件位于项目根目录，格式示例：

```xml
<linker>
  <assembly fullname="Assembly-CSharp" preserve="all"/>
  <assembly fullname="UnityEngine.CoreModule">
    <type fullname="UnityEngine.GameObject" preserve="all"/>
  </assembly>
</linker>
```

> **注意**：仅添加运行时通过反射使用的类型，避免过度白名单导致包体膨胀。

---

## 5. 后续优化方向

### 5.1 纹理压缩

| 方案 | 说明 | 适用场景 |
|------|------|----------|
| ASTC | 高质量，支持 alpha | 高端设备为主 |
| ETC2 | WebGL 标准支持 | 兼容性优先 |
| ASTC + ETC2 fallback | 双格式 + 运行时选择 | 最佳体验 |
| 纹理降分辨率 | 减小贴图尺寸 | UI/背景图 |

### 5.2 音频压缩

| 格式 | 压缩比 | 质量 | 说明 |
|------|--------|------|------|
| Vorbis | 高 | 好 | 推荐，WebGL 原生支持 |
| ADPCM | 中 | 中 | 解码快，体积较大 |
| MP3 | 高 | 好 | 兼容性需测试 |

### 5.3 Addressables 分包策略

```
首包（≤ 4MB）：
  ├── 核心逻辑代码（wasm + js）
  ├── 启动场景资源
  └── 基础 UI 资源

远程包（CDN）：
  ├── 关卡场景资源
  ├── 角色/怪物资源
  ├── 音频资源
  └── 特效资源
```

### 5.4 代码 Strip 白名单维护

- 定期审查 `link.xml`，移除不再需要的白名单条目
- 每次新增第三方库后，检查是否需要添加保留规则
- 使用 `Build Report` 分析各程序集占比

### 5.5 构建流水线集成（CI/CD）

- GitHub Actions / GitLab CI 自动构建
- 构建后自动上传 CDN
- 自动对比基线，超标时报警
- 构建产物归档 + 版本管理

---

## 6. 构建记录表

| 日期 | 版本 | 总大小 | wasm | js | data | framework | 微信4MB | 备注 |
|------|------|--------|------|----|------|-----------|---------|------|
| 基线 | v0.1 | TBD | TBD | TBD | TBD | TBD | TBD | 首次基线构建 |
| | | | | | | | | |
| | | | | | | | | |

> **填写说明**：
> - **总大小**：Build/WebGL/ 目录总大小（MB）
> - **wasm**：.wasm 文件大小（MB）
> - **js**：.js 文件大小（MB）
> - **data**：.data / .unityweb 文件大小（MB）
> - **framework**：framework.wasm + framework.js 大小（MB）
> - **微信4MB**：✅ 通过 / ❌ 超标（标注超出多少）
> - **备注**：主要变更说明

---

## 附录：常见问题

### Q1: IL2CPP 编译失败，提示空间不足
**A:** IL2CPP 编译需要大量临时文件空间（可能超过 3GB）。确保系统临时目录（`/tmp` 或 `%TEMP%`）所在磁盘有足够空间。

### Q2: Brotli 解压在微信开发者工具中失败
**A:** 确认微信开发者工具版本 ≥ 最新稳定版。Brotli 需要服务器配置 `Content-Encoding: br` 响应头。

### Q3: wasm 文件体积过大
**A:** 检查以下几点：
1. `Managed Stripping Level` 是否设为 `High`
2. `Il2Cpp Code Generation` 是否设为 `OptimizeSize`
3. 是否有大量第三方 DLL 被包含
4. 使用 `Build Report` 分析各程序集占比

### Q4: 构建后黑屏/无法运行
**A:** 检查 `link.xml` 是否误删了运行时必要的类型。在 Development Build 模式下运行查看详细错误。

---

*文档版本：v1.0 | 最后更新：2026-05-11 | 项目：GameSXCG*
