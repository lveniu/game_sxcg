#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 命令行构建入口（CI/CD用）
/// 
/// 支持的命令行参数：
///   -language <zh|en|ja>     — 语言版本（默认zh）
///   -quality <low|mid|high>  — 画质档位（默认mid）
///   -development             — 开发模式构建
///   -buildType <Debug|Release|MiniGame> — 构建类型
///   -outputDir <path>        — 输出目录覆盖
///   -skipOptimization        — 跳过构建后优化
///   -validate                — 仅验证已有构建
/// 
/// 用法示例：
///   unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.Build
///   unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.BuildMiniGame
///   unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.Validate
/// 
/// 菜单：
///   Build → CI/CD → Build Release
///   Build → CI/CD → Build Debug
///   Build → CI/CD → Build MiniGame
///   Build → CI/CD → Validate Build
/// </summary>
public static class BuildCommand
{
    // ========== 命令行参数解析 ==========

    /// <summary>
    /// 构建配置
    /// </summary>
    public class BuildConfig
    {
        public string Language = "zh";              // 语言版本
        public string Quality = "mid";              // 画质档位
        public bool Development = false;            // 是否开发模式
        public string BuildType = "Release";        // Debug / Release / MiniGame
        public string OutputDir = "Builds/WebGL";   // 输出目录
        public bool SkipOptimization = false;       // 跳过后优化
        public bool ValidateOnly = false;           // 仅验证
    }

    /// <summary>
    /// 从命令行参数解析构建配置
    /// </summary>
    public static BuildConfig ParseCommandLineArgs()
    {
        var config = new BuildConfig();
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-language":
                    if (i + 1 < args.Length)
                        config.Language = args[++i];
                    break;

                case "-quality":
                    if (i + 1 < args.Length)
                        config.Quality = args[++i];
                    break;

                case "-development":
                    config.Development = true;
                    break;

                case "-buildType":
                    if (i + 1 < args.Length)
                        config.BuildType = args[++i];
                    break;

                case "-outputDir":
                    if (i + 1 < args.Length)
                        config.OutputDir = args[++i];
                    break;

                case "-skipOptimization":
                    config.SkipOptimization = true;
                    break;

                case "-validate":
                    config.ValidateOnly = true;
                    break;
            }
        }

        return config;
    }

    // ========== 菜单项 ==========

    [MenuItem("Build/CI-CD/Build Release")]
    public static void BuildReleaseMenuItem()
    {
        var config = new BuildConfig
        {
            BuildType = "Release",
            Development = false
        };
        ExecuteBuild(config);
    }

    [MenuItem("Build/CI-CD/Build Debug")]
    public static void BuildDebugMenuItem()
    {
        var config = new BuildConfig
        {
            BuildType = "Debug",
            Development = true,
            OutputDir = "Builds/WebGL_Debug"
        };
        ExecuteBuild(config);
    }

    [MenuItem("Build/CI-CD/Build MiniGame")]
    public static void BuildMiniGameMenuItem()
    {
        var config = new BuildConfig
        {
            BuildType = "MiniGame",
            Development = false,
            OutputDir = "Builds/WebGL_MiniGame"
        };
        ExecuteBuild(config);
    }

    [MenuItem("Build/CI-CD/Validate Build")]
    public static void ValidateMenuItem()
    {
        bool valid = WebGLBuildPipeline.ValidateBuild();
        if (valid)
            Debug.Log("✓ 构建验证通过");
        else
            Debug.LogError("✗ 构建验证失败");

        // CI/CD模式下，验证失败需要退出
        if (Application.isBatchMode && !valid)
        {
            EditorApplication.Exit(1);
        }
    }

    // ========== 命令行入口 ==========

    /// <summary>
    /// 主命令行构建入口
    /// unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.Build
    /// </summary>
    public static void Build()
    {
        Debug.Log("============================================");
        Debug.Log("  CI/CD 构建开始");
        Debug.Log("============================================");

        var config = ParseCommandLineArgs();
        LogBuildConfig(config);

        if (config.ValidateOnly)
        {
            Validate();
            return;
        }

        ExecuteBuild(config);
    }

    /// <summary>
    /// MiniGame专用构建入口
    /// unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.BuildMiniGame
    /// </summary>
    public static void BuildMiniGame()
    {
        Debug.Log("============================================");
        Debug.Log("  CI/CD 微信小游戏构建开始");
        Debug.Log("============================================");

        var config = ParseCommandLineArgs();
        config.BuildType = "MiniGame";
        if (config.OutputDir == "Builds/WebGL")
            config.OutputDir = "Builds/WebGL_MiniGame";

        LogBuildConfig(config);
        ExecuteBuild(config);
    }

    /// <summary>
    /// 验证构建
    /// unity-editor -quit -batchmode -projectPath . -executeMethod BuildCommand.Validate
    /// </summary>
    public static void Validate()
    {
        Debug.Log("[CI/CD] 开始构建验证...");
        var config = ParseCommandLineArgs();
        string outputDir = config.OutputDir ?? "Builds/WebGL";

        bool valid = WebGLBuildPipeline.ValidateBuild(outputDir);

        if (valid)
        {
            Debug.Log("[CI/CD] ✓ 构建验证通过");
        }
        else
        {
            Debug.LogError("[CI/CD] ✗ 构建验证失败");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    // ========== 构建执行 ==========

    private static void ExecuteBuild(BuildConfig config)
    {
        var startTime = DateTime.Now;

        try
        {
            // Step 1: 应用构建配置
            ApplyBuildConfig(config);

            // Step 2: 配置Player Settings
            ConfigurePlayerSettingsForBuild(config);

            // Step 3: 获取场景
            string[] scenes = GetScenesForBuild();
            if (scenes.Length == 0)
            {
                Debug.LogError("[CI/CD] 没有找到场景文件！");
                ExitWithCode(1);
                return;
            }

            // Step 4: 执行构建
            Debug.Log($"[CI/CD] 开始构建 {config.BuildType} ({scenes.Length} 个场景)...");

            BuildOptions options = BuildOptions.None;
            if (config.Development)
                options |= BuildOptions.Development;

            // MiniGame特殊配置
            if (config.BuildType == "MiniGame")
            {
                ConfigureMiniGameSettings();
            }

            BuildPlayerOptions buildOpts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = config.OutputDir,
                target = BuildTarget.WebGL,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOpts);
            var elapsed = DateTime.Now - startTime;

            // Step 5: 处理构建结果
            ProcessBuildResult(report, elapsed, config);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CI/CD] 构建异常: {e.Message}\n{e.StackTrace}");
            ExitWithCode(1);
        }
    }

    private static void ApplyBuildConfig(BuildConfig config)
    {
        // 设置语言
        Debug.Log($"[CI/CD] 设置语言: {config.Language}");
        // 可通过 PlayerPrefs 或自定义配置文件传递语言设置
        // 实际项目中可以通过定义预处理符号来切换语言
        switch (config.Language)
        {
            case "zh":
                // 中文配置
                break;
            case "en":
                // 英文配置
                break;
            case "ja":
                // 日文配置
                break;
            default:
                Debug.LogWarning($"[CI/CD] 未知语言: {config.Language}，使用默认中文");
                break;
        }

        // 设置画质
        Debug.Log($"[CI/CD] 设置画质: {config.Quality}");
        switch (config.Quality)
        {
            case "low":
                QualitySettings.SetQualityLevel(0, true);
                break;
            case "mid":
                int midLevel = QualitySettings.names.Length / 2;
                QualitySettings.SetQualityLevel(midLevel, true);
                break;
            case "high":
                QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
                break;
            default:
                Debug.LogWarning($"[CI/CD] 未知画质: {config.Quality}，使用默认设置");
                break;
        }
    }

    private static void ConfigurePlayerSettingsForBuild(BuildConfig config)
    {
        // 基础配置
        PlayerSettings.defaultScreenWidth = 720;
        PlayerSettings.defaultScreenHeight = 1280;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

        // 图形API
        UnityEngine.Rendering.GraphicsDeviceType[] gfxApis = { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 };
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, gfxApis);

        // IL2CPP
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

        // .NET Standard 2.1
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.WebGL, ApiCompatibilityLevel.NETStandard21);

        // 根据构建类型调整
        if (config.BuildType == "Debug")
        {
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Disabled);
            PlayerSettings.WebGL.debugInformation = true;
        }
        else
        {
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.WebGL.debugInformation = false;
        }

        // 颜色空间
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // WebGL模板
        if (config.BuildType == "MiniGame")
        {
            PlayerSettings.WebGL.template = "PROJECT:MiniGame";
        }
        else
        {
            PlayerSettings.WebGL.template = "PROJECT:Minimal";
        }

        // 压缩
        EditorUserBuildSettings.webGLCompression = WebGLCompression.Brotli;

        // 其他
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.runInBackground = false;
        PlayerSettings.WebGL.exceptions = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.nameFilesAsHashes = true;

        Debug.Log($"[CI/CD] Player Settings 配置完成 ({config.BuildType})");
    }

    private static void ConfigureMiniGameSettings()
    {
        // 微信小游戏特殊配置
        Debug.Log("[CI/CD] 应用微信小游戏特殊配置");

        // 小游戏内存限制更严格
        PlayerSettings.WebGL.memorySize = 256;

        // 更激进的代码裁剪
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);

        // 异常处理：最小化
        PlayerSettings.WebGL.exceptions = WebGLExceptionSupport.None;

        // 堆栈追踪：无
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

        Debug.Log("[CI/CD] 微信小游戏配置应用完成");
    }

    private static string[] GetScenesForBuild()
    {
        var scenes = new List<string>();

        // 查找所有场景
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            scenes.Add(path);
        }

        // 如果没有找到场景，尝试查找所有场景
        if (scenes.Count == 0)
        {
            guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 排除Editor测试场景
                if (!path.Contains("/Editor/") && !path.Contains("/Tests/"))
                    scenes.Add(path);
            }
        }

        Debug.Log($"[CI/CD] 场景列表 ({scenes.Count}): {string.Join(", ", scenes)}");
        return scenes.ToArray();
    }

    private static void ProcessBuildResult(BuildReport report, TimeSpan elapsed, BuildConfig config)
    {
        bool succeeded = report.summary.result == BuildResult.Succeeded;

        string resultLabel = succeeded ? "✅ 成功" : "❌ 失败";
        double totalSizeMB = report.summary.totalSize / (1024.0 * 1024.0);

        string reportText = $@"
============================================
  CI/CD 构建报告
============================================
构建类型:   {config.BuildType}
开发模式:   {config.Development}
语言:       {config.Language}
画质:       {config.Quality}
构建结果:   {resultLabel}
构建耗时:   {elapsed.TotalMinutes:F1} 分钟
包体大小:   {totalSizeMB:F1} MB
警告数量:   {report.summary.totalWarnings}
错误数量:   {report.summary.totalErrors}
输出目录:   {config.OutputDir}
============================================";

        Debug.Log(reportText);

        if (report.summary.totalErrors > 0)
        {
            Debug.LogError("[CI/CD] 构建错误:");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        Debug.LogError($"  ❌ {msg.content}");
                }
            }
        }

        if (report.summary.totalWarnings > 0)
        {
            Debug.LogWarning($"[CI/CD] {report.summary.totalWarnings} 个警告");
        }

        // 构建后优化
        if (succeeded && !config.SkipOptimization)
        {
            Debug.Log("[CI/CD] 执行构建后优化...");
            WebGLBuildPipeline.PostBuildOptimization(config.OutputDir);
        }

        // 写入构建摘要文件
        WriteBuildSummary(config, report, elapsed);

        // 退出码
        if (!succeeded)
        {
            ExitWithCode(1);
        }
        else
        {
            Debug.Log("[CI/CD] ✓ 构建完成！");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
    }

    private static void WriteBuildSummary(BuildConfig config, BuildReport report, TimeSpan elapsed)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"buildType\": \"{config.BuildType}\",");
            sb.AppendLine($"  \"language\": \"{config.Language}\",");
            sb.AppendLine($"  \"quality\": \"{config.Quality}\",");
            sb.AppendLine($"  \"development\": {config.Development.ToString().ToLower()},");
            sb.AppendLine($"  \"success\": {(report.summary.result == BuildResult.Succeeded).ToString().ToLower()},");
            sb.AppendLine($"  \"totalSizeBytes\": {report.summary.totalSize},");
            sb.AppendLine($"  \"totalSizeMB\": {(report.summary.totalSize / (1024.0 * 1024.0)).ToString("F2")},");
            sb.AppendLine($"  \"buildDurationSeconds\": {elapsed.TotalSeconds:F0},");
            sb.AppendLine($"  \"warnings\": {report.summary.totalWarnings},");
            sb.AppendLine($"  \"errors\": {report.summary.totalErrors},");
            sb.AppendLine($"  \"outputDir\": \"{config.OutputDir.Replace("\\", "/")}\",");
            sb.AppendLine($"  \"unityVersion\": \"{Application.unityVersion}\",");
            sb.AppendLine($"  \"builtAt\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
            sb.AppendLine("}");

            string summaryPath = System.IO.Path.Combine(config.OutputDir, "build-summary.json");
            System.IO.Directory.CreateDirectory(config.OutputDir);
            System.IO.File.WriteAllText(summaryPath, sb.ToString());
            Debug.Log($"[CI/CD] 构建摘要已写入: {summaryPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CI/CD] 写入构建摘要失败: {e.Message}");
        }
    }

    private static void LogBuildConfig(BuildConfig config)
    {
        Debug.Log($"[CI/CD] 构建配置:");
        Debug.Log($"  Language:      {config.Language}");
        Debug.Log($"  Quality:       {config.Quality}");
        Debug.Log($"  Development:   {config.Development}");
        Debug.Log($"  BuildType:     {config.BuildType}");
        Debug.Log($"  OutputDir:     {config.OutputDir}");
        Debug.Log($"  SkipOptim:     {config.SkipOptimization}");
        Debug.Log($"  ValidateOnly:  {config.ValidateOnly}");
        Debug.Log($"  BatchMode:     {Application.isBatchMode}");
    }

    private static void ExitWithCode(int code)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(code);
        }
        else
        {
            Debug.LogError($"[CI/CD] 构建失败 (Exit Code: {code})");
        }
    }
}
#endif
