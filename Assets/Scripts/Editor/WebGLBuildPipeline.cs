using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// WebGL基线构建自动化脚本
/// 基于 docs/planning/WebGL-build-spec.md 规格
/// 
/// 使用方式：
/// 1. Unity Editor菜单 → Build → WebGL Baseline Build
/// 2. 或命令行: unity-editor -quit -batchmode -projectPath . -executeMethod WebGLBuildPipeline.BuildWebGL
/// 
/// 输出目录: Builds/WebGL/
/// </summary>
public static class WebGLBuildPipeline
{
    private const string OUTPUT_DIR = "Builds/WebGL";
    private const string SCENE_PATH = "Assets/Scenes/MainScene.unity";

    [MenuItem("Build/WebGL Baseline Build")]
    public static void BuildWebGL()
    {
        Debug.Log("[WebGL构建] 开始配置...");

        // Step 1: 配置Player Settings
        ConfigurePlayerSettings();

        // Step 2: 配置Quality Settings
        ConfigureQualitySettings();

        // Step 3: 配置场景
        string[] scenes = GetScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[WebGL构建] 没有找到场景文件！请确保 MainScene.unity 存在");
            return;
        }

        // Step 4: 执行构建
        Debug.Log("[WebGL构建] 开始构建...");
        var startTime = System.DateTime.Now;

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OUTPUT_DIR,
            target = BuildTarget.WebGL,
            options = BuildOptions.None  // 开发阶段不开Development模式
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);

        var elapsed = System.DateTime.Now - startTime;

        // Step 5: 生成构建报告
        GenerateBuildReport(report, elapsed);
    }

    /// <summary>
    /// [MenuItem("Build/WebGL Development Build (with Debug Symbols)")]
    /// 开发调试版本，含Debug Symbols
    /// </summary>
    [MenuItem("Build/WebGL Development Build")]
    public static void BuildWebGLDevelopment()
    {
        Debug.Log("[WebGL构建] 开发模式构建...");
        ConfigurePlayerSettings();
        PlayerSettings.WebGL.debugInformation = true;
        ConfigureQualitySettings();

        string[] scenes = GetScenes();

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OUTPUT_DIR + "_Dev",
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        };

        var startTime = System.DateTime.Now;
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var elapsed = System.DateTime.Now - startTime;

        GenerateBuildReport(report, elapsed);
    }

    // ========== Player Settings 配置 ==========

    private static void ConfigurePlayerSettings()
    {
        // 分辨率：720×1280竖屏
        PlayerSettings.defaultScreenWidth = 720;
        PlayerSettings.defaultScreenHeight = 1280;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

        // 图形API：WebGL 2.0 only
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

        // Scripting Backend: IL2CPP
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

        // API Compatibility: .NET Standard 2.1
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WebGL, ApiCompatibilityLevel.NETStandard21);

        // Managed Stripping: High
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);

        // Color Space: Linear
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // WebGL模板：Minimal
        PlayerSettings.WebGL.template = "PROJECT:Minimal";

        // 压缩：Brotli
        EditorUserBuildSettings.webGLCompression = WebGLCompression.Brotli;

        // Data caching
        PlayerSettings.WebGL.dataCaching = true;

        // Memory Size: 256MB
        PlayerSettings.WebGL.memorySize = 256;

        // Linker Target: WebAssembly
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

        // Run In Background: 关闭
        PlayerSettings.runInBackground = false;

        // 其他
        PlayerSettings.WebGL.exceptions = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.nameFilesAsHashes = true;  // 文件名hash化，防CDN缓存

        Debug.Log("[WebGL构建] Player Settings 配置完成");
    }

    // ========== Quality Settings 配置 ==========

    private static void ConfigureQualitySettings()
    {
        // 查找或创建WebGL Quality Level
        string[] qualityNames = QualitySettings.names;
        int webglIndex = -1;

        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (qualityNames[i] == "WebGL")
            {
                webglIndex = i;
                break;
            }
        }

        if (webglIndex < 0)
        {
            // 新增WebGL Quality Level
            // Unity API不直接支持新增QualityLevel，需要操作QualitySettings资产
            Debug.LogWarning("[WebGL构建] 未找到WebGL Quality Level，请手动添加：\n" +
                "Edit → Project Settings → Quality → 点击+号 → 命名为'WebGL' → 按以下配置：\n" +
                "  - Pixel Light Count: 0\n" +
                "  - Shadows: No Shadows\n" +
                "  - Texture Quality: Full Res\n" +
                "  - Anisotropic Textures: Disabled\n" +
                "  - Anti Aliasing: Disabled\n" +
                "  - Soft Particles: Disabled\n" +
                "  - VSync: Disabled\n" +
                "  - LOD Bias: 0.5\n" +
                "  - Particle Raycast Budget: 16\n" +
                "  - Streaming Mipmaps: Active");
        }

        // 设置WebGL平台默认Quality为最低档（或WebGL档）
        QualitySettings.SetQualityLevel(0, true);
        Debug.Log("[WebGL构建] Quality Settings 配置完成");
    }

    // ========== 场景获取 ==========

    private static string[] GetScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();

        if (System.IO.File.Exists(SCENE_PATH))
        {
            scenes.Add(SCENE_PATH);
        }
        else
        {
            // 搜索所有场景
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
            foreach (string guid in guids)
            {
                scenes.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        Debug.Log($"[WebGL构建] 场景列表: {string.Join(", ", scenes)}");
        return scenes.ToArray();
    }

    // ========== 构建报告 ==========

    private static void GenerateBuildReport(BuildReport report, System.TimeSpan elapsed)
    {
        string result = report.summary.result == BuildResult.Succeeded ? "✅ 成功" : "❌ 失败";
        long totalSize = report.summary.totalSize;
        double totalSizeMB = totalSize / (1024.0 * 1024.0);

        string reportText = $@"
============================================
  WebGL 基线构建报告
============================================
构建结果:  {result}
Unity版本: {report.summary.platform.ToString()}
构建耗时:  {elapsed.TotalMinutes:F1} 分钟
包体大小:  {totalSizeMB:F1} MB
警告数量:  {report.summary.totalWarnings}
错误数量:  {report.summary.totalErrors}
输出目录:  {OUTPUT_DIR}
--------------------------------------------
";

        if (report.steps.Length > 0)
        {
            reportText += "构建步骤:\n";
            foreach (var step in report.steps)
            {
                reportText += $"  [{step.name}] {step.duration.TotalSeconds:F0}s\n";
            }
        }

        if (report.summary.totalErrors > 0)
        {
            reportText += "\n错误:\n";
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        reportText += $"  ❌ {msg.content}\n";
                }
            }
        }

        reportText += "\n============================================";

        Debug.Log(reportText);

        // 同时写到文件
        string reportPath = OUTPUT_DIR + "/build_report.txt";
        try
        {
            System.IO.Directory.CreateDirectory(OUTPUT_DIR);
            System.IO.File.WriteAllText(reportPath, reportText);
            Debug.Log($"[WebGL构建] 报告已写入: {reportPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WebGL构建] 无法写入报告文件: {e.Message}");
        }
    }
}
