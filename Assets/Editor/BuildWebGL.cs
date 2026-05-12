using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// WebGL 一键构建脚本
/// 
/// 使用方法:
///   1. Unity 菜单: GameSXCG → Build WebGL
///   2. 命令行: unity-editor -quit -batchmode -projectPath . -executeMethod BuildWebGL.Build -buildTarget WebGL
///   
/// 功能:
///   - 自动配置 PlayerSettings（IL2CPP + Strip + Brotli 压缩）
///   - 构建后输出包体大小报告（wasm/js/data/assets 各多少 MB）
///   - 将基线数据保存到 docs/build/webgl_baseline.json
/// </summary>
public class BuildWebGL
{
    // 构建输出目录
    private const string BuildOutputDir = "Build/WebGL";
    private const string BaselineFile = "docs/build/webgl_baseline.json";

    [MenuItem("GameSXCG/Build WebGL")]
    public static void Build()
    {
        Debug.Log("[BuildWebGL] ============================================");
        Debug.Log("[BuildWebGL] 开始 WebGL 构建流程");
        Debug.Log("[BuildWebGL] ============================================");

        // 1. 配置 PlayerSettings
        ConfigurePlayerSettings();

        // 2. 获取所有启用的场景
        string[] scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildWebGL] 没有找到启用的场景！请在 Build Settings 中添加场景。");
            return;
        }

        Debug.Log($"[BuildWebGL] 共 {scenes.Length} 个场景:");
        foreach (var s in scenes) Debug.Log($"  - {s}");

        // 3. 确保输出目录存在（清理旧构建）
        var outputPath = Path.GetFullPath(BuildOutputDir);
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }
        Directory.CreateDirectory(outputPath);

        // 4. 执行构建
        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None  // Release 构建
        };

        Debug.Log("[BuildWebGL] 正在构建 WebGL（IL2CPP + Brotli）...");
        BuildReport report = BuildPipeline.BuildPlayer(opts);

        // 5. 分析结果
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildWebGL] ✅ 构建成功！");
            Debug.Log($"[BuildWebGL] 总大小: {report.summary.totalSize / (1024.0 * 1024.0):F2} MB");
            Debug.Log($"[BuildWebGL] 耗时: {report.summary.totalTime}");

            // 6. 包体分析
            AnalyzeBuildSize(outputPath);

            // 7. 保存基线
            SaveBaseline(report);
        }
        else
        {
            Debug.LogError($"[BuildWebGL] ❌ 构建失败: {report.summary.result}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        Debug.LogError($"  {msg.content}");
                }
            }

            // 命令行模式下以错误码退出
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>
    /// 自动配置 WebGL PlayerSettings — 针对 WeChat Mini Game 优化
    /// </summary>
    private static void ConfigurePlayerSettings()
    {
        // IL2CPP 脚本后端
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

        // .NET Standard 2.1 API 兼容级别
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WebGL, ApiCompatibilityLevel.NETStandard21);

        // Strip Engine Code — 裁剪未使用的引擎代码
        PlayerSettings.stripEngineCode = true;

        // Managed Stripping Level: High — 最高级别裁剪
        EditorUserBuildSettings.managedStrippingLevel = ManagedStrippingLevel.High;

        // WebGL 压缩格式: Brotli（最佳压缩比）
        EditorUserBuildSettings.webGLCompression = WebGLCompression.Brotli;

        // WebGL 2.0 only (OpenGLES3)
        PlayerSettings.SetGraphicsAPIs(BuildTargetGroup.WebGL,
            new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

        // Color Space: Gamma（微信小游戏兼容性更好）
        PlayerSettings.colorSpace = ColorSpace.Gamma;

        // IL2CPP 代码生成: OptimizeSize（更小包体）
        // Unity 2021.2+ 支持 Il2CppCodeGeneration 枚举
#if UNITY_2021_2_OR_NEWER
        EditorUserBuildSettings.il2CppCodeGeneration = Il2CppCodeGeneration.OptimizeSize;
        Debug.Log("[BuildWebGL]   IL2CPP CodeGen: OptimizeSize (via enum)");
#else
        Debug.Log("[BuildWebGL]   IL2CPP CodeGen: 默认 (Unity < 2021.2 不支持枚举)");
#endif

        // 关闭 Development Build
        EditorUserBuildSettings.development = false;

        // 异常支持: Explicitly thrown exceptions only（更小包体）
        // Unity 2022.3 使用 PlayerSettings.WebGL.exceptionSupport 属性
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // 关闭 Profiler 连接
        EditorUserBuildSettings.connectProfiler = false;

        // 打印配置摘要
        Debug.Log("[BuildWebGL] PlayerSettings 配置完成:");
        Debug.Log("  - Scripting Backend: IL2CPP");
        Debug.Log("  - API Compatibility: .NET Standard 2.1");
        Debug.Log("  - Strip Engine Code: true");
        Debug.Log("  - Managed Stripping Level: High");
        Debug.Log("  - WebGL Compression: Brotli");
        Debug.Log("  - Graphics API: WebGL 2.0 (OpenGLES3)");
        Debug.Log("  - Color Space: Gamma");
        Debug.Log("  - Development Build: false");
        Debug.Log("  - Exception Support: ExplicitlyThrownExceptionsOnly");
        Debug.Log("  - Connect Profiler: false");
    }

    /// <summary>
    /// 获取所有在 Build Settings 中启用的场景路径
    /// </summary>
    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
    }

    /// <summary>
    /// 包体分析 — 拆分 wasm/js/data/assets 各占多少
    /// </summary>
    private static void AnalyzeBuildSize(string buildPath)
    {
        Debug.Log("[BuildWebGL] ═════════════════════════════════════════");
        Debug.Log("[BuildWebGL] 包体分析报告:");
        Debug.Log("[BuildWebGL] ═════════════════════════════════════════");

        // Unity WebGL 构建的文件在 Build/ 子目录中
        var dataDir = Path.Combine(buildPath, "Build");
        if (!Directory.Exists(dataDir))
        {
            // 某些 Unity 版本文件直接在输出根目录
            dataDir = buildPath;
        }

        if (!Directory.Exists(dataDir))
        {
            Debug.LogWarning("[BuildWebGL] 未找到构建输出目录，跳过包体分析");
            return;
        }

        long wasmSize = 0, jsSize = 0, dataSize = 0, assetSize = 0, otherSize = 0;
        long totalSize = 0;

        foreach (var file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var ext = info.Extension.ToLower();
            var size = info.Length;
            totalSize += size;

            if (ext == ".wasm") wasmSize += size;
            else if (ext == ".js") jsSize += size;
            else if (ext == ".data" || ext == ".unityweb") dataSize += size;
            else if (ext == ".assets" || ext == ".resS") assetSize += size;
            else otherSize += size;
        }

        if (totalSize == 0)
        {
            Debug.LogWarning("[BuildWebGL] 构建目录为空，无法分析");
            return;
        }

        // 输出格式化报告
        float MB = 1024f * 1024f;
        Debug.Log($"  wasm:  {wasmSize / MB,8:F2} MB  ({wasmSize * 100f / totalSize:F1}%)");
        Debug.Log($"  js:    {jsSize / MB,8:F2} MB  ({jsSize * 100f / totalSize:F1}%)");
        Debug.Log($"  data:  {dataSize / MB,8:F2} MB  ({dataSize * 100f / totalSize:F1}%)");
        Debug.Log($"  asset: {assetSize / MB,8:F2} MB  ({assetSize * 100f / totalSize:F1}%)");
        Debug.Log($"  other: {otherSize / MB,8:F2} MB  ({otherSize * 100f / totalSize:F1}%)");
        Debug.Log($"  ──────────────────────────────────────────");
        Debug.Log($"  TOTAL: {totalSize / MB,8:F2} MB");

        // 微信小游戏 4MB 首包基准判断
        // wasm + js 为首包必须加载的框架代码
        float frameworkMB = (wasmSize + jsSize) / MB;
        Debug.Log($"[BuildWebGL] framework (wasm+js): {frameworkMB:F2} MB");

        if (frameworkMB <= 4.0f)
        {
            Debug.Log($"[BuildWebGL] ✅ 在微信 4MB 首包限制内！");
        }
        else
        {
            Debug.LogWarning(
                $"[BuildWebGL] ⚠️ 超出微信 4MB 首包限制！" +
                $"需要 CDN 分包，超出 {frameworkMB - 4.0f:F2} MB");
        }

        Debug.Log("[BuildWebGL] ═════════════════════════════════════════");
    }

    /// <summary>
    /// 保存基线数据到 JSON 文件，供后续构建对比
    /// </summary>
    private static void SaveBaseline(BuildReport report)
    {
        // 确保目录存在
        var dir = Path.GetDirectoryName(BaselineFile);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 收集文件大小数据
        var buildPath = Path.GetFullPath(BuildOutputDir);
        long wasmSize = 0, jsSize = 0, dataSize = 0, assetSize = 0, otherSize = 0, totalSize = 0;

        var dataDir = Path.Combine(buildPath, "Build");
        if (!Directory.Exists(dataDir))
        {
            dataDir = buildPath;
        }

        if (Directory.Exists(dataDir))
        {
            foreach (var file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                var ext = info.Extension.ToLower();
                totalSize += info.Length;

                if (ext == ".wasm") wasmSize += info.Length;
                else if (ext == ".js") jsSize += info.Length;
                else if (ext == ".data" || ext == ".unityweb") dataSize += info.Length;
                else if (ext == ".assets" || ext == ".resS") assetSize += info.Length;
                else otherSize += info.Length;
            }
        }

        float MB = 1024f * 1024f;
        float frameworkMB = (wasmSize + jsSize) / MB;
        bool wechat4mbPass = frameworkMB <= 4.0;

        // 手动构建 JSON（避免依赖 Newtonsoft / System.Text.Json）
        var json = "{\n" +
            $"  \"timestamp\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\n" +
            $"  \"unityVersion\": \"{Application.unityVersion}\",\n" +
            $"  \"buildTarget\": \"WebGL\",\n" +
            $"  \"scriptingBackend\": \"IL2CPP\",\n" +
            $"  \"compression\": \"Brotli\",\n" +
            $"  \"managedStrippingLevel\": \"High\",\n" +
            $"  \"totalSizeBytes\": {totalSize},\n" +
            $"  \"totalSizeMB\": {totalSize / MB:F2},\n" +
            $"  \"wasmBytes\": {wasmSize},\n" +
            $"  \"wasmMB\": {wasmSize / MB:F2},\n" +
            $"  \"jsBytes\": {jsSize},\n" +
            $"  \"jsMB\": {jsSize / MB:F2},\n" +
            $"  \"dataBytes\": {dataSize},\n" +
            $"  \"dataMB\": {dataSize / MB:F2},\n" +
            $"  \"assetBytes\": {assetSize},\n" +
            $"  \"assetMB\": {assetSize / MB:F2},\n" +
            $"  \"otherBytes\": {otherSize},\n" +
            $"  \"otherMB\": {otherSize / MB:F2},\n" +
            $"  \"frameworkMB\": {frameworkMB:F2},\n" +
            $"  \"wechat4mbPass\": {(wechat4mbPass ? "true" : "false")},\n" +
            $"  \"buildTimeSeconds\": {report.summary.totalTime.TotalSeconds:F1},\n" +
            $"  \"scenes\": {report.scenes.Length},\n" +
            $"  \"notes\": \"基线构建 — 后续每次构建对比此数据\"\n" +
            "}\n";

        File.WriteAllText(BaselineFile, json);
        Debug.Log($"[BuildWebGL] 📊 基线数据已保存: {BaselineFile}");
        Debug.Log($"[BuildWebGL] ============================================");
    }
}
