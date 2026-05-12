using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using System.IO.Compression;
using System.Text;

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

    // ========== 构建关键文件清单 ==========
    private static readonly string[] CRITICAL_FILES = new string[]
    {
        "index.html",
        "Build/Build.data",
        "Build/Build.framework.js",
        "Build/Build.loader.js",
        "Build/Build.wasm"
    };

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

        // Step 6: 构建后自动优化
        if (report.summary.result == BuildResult.Succeeded)
        {
            PostBuildOptimization();
        }
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

        if (report.summary.result == BuildResult.Succeeded)
        {
            PostBuildOptimization(OUTPUT_DIR + "_Dev");
        }
    }

    // ========== 构建后优化 ==========

    /// <summary>
    /// 构建后自动优化：gzip压缩JS、生成build-report.json、检查关键文件
    /// </summary>
    public static void PostBuildOptimization(string outputDir = null)
    {
        string targetDir = outputDir ?? OUTPUT_DIR;
        Debug.Log($"[WebGL构建后优化] 开始处理: {targetDir}");

        if (!Directory.Exists(targetDir))
        {
            Debug.LogError($"[WebGL构建后优化] 输出目录不存在: {targetDir}");
            return;
        }

        int compressedCount = 0;
        long totalOriginalSize = 0;
        long totalCompressedSize = 0;

        // 1. gzip压缩所有 .js 和 .wasm 文件
        try
        {
            string[] jsFiles = Directory.GetFiles(targetDir, "*.js", SearchOption.AllDirectories);
            string[] wasmFiles = Directory.GetFiles(targetDir, "*.wasm", SearchOption.AllDirectories);

            var allCompressible = new System.Collections.Generic.List<string>(jsFiles);
            allCompressible.AddRange(wasmFiles);

            foreach (string filePath in allCompressible)
            {
                try
                {
                    FileInfo fi = new FileInfo(filePath);
                    totalOriginalSize += fi.Length;

                    string gzPath = filePath + ".gz";
                    using (var sourceStream = File.OpenRead(filePath))
                    using (var destStream = File.Create(gzPath))
                    using (var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal))
                    {
                        sourceStream.CopyTo(gzipStream);
                    }

                    FileInfo gzFi = new FileInfo(gzPath);
                    totalCompressedSize += gzFi.Length;
                    compressedCount++;

                    double ratio = (1.0 - (double)gzFi.Length / fi.Length) * 100.0;
                    Debug.Log($"[Gzip] {Path.GetFileName(filePath)}: {FormatSize(fi.Length)} → {FormatSize(gzFi.Length)} ({ratio:F1}% 压缩)");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Gzip] 压缩失败 {Path.GetFileName(filePath)}: {e.Message}");
                }
            }

            Debug.Log($"[WebGL构建后优化] 压缩完成: {compressedCount} 个文件, " +
                $"{FormatSize(totalOriginalSize)} → {FormatSize(totalCompressedSize)}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WebGL构建后优化] 压缩阶段出错: {e.Message}");
        }

        // 2. 生成 build-report.json（文件大小统计）
        GenerateBuildSizeReport(targetDir);

        // 3. 验证构建完整性
        ValidateBuild(targetDir);

        Debug.Log("[WebGL构建后优化] 完成 ✓");
    }

    /// <summary>
    /// 返回构建大小报告（JSON字符串）
    /// </summary>
    public static string GetBuildSizeReport(string outputDir = null)
    {
        string targetDir = outputDir ?? OUTPUT_DIR;

        if (!Directory.Exists(targetDir))
        {
            return $"{{ \"error\": \"目录不存在: {targetDir}\" }}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"buildDirectory\": \"{targetDir.Replace("\\", "/")}\",");
        sb.AppendLine($"  \"generatedAt\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");

        // 总大小
        long totalSize = GetDirectorySize(targetDir);
        sb.AppendLine($"  \"totalSize\": {totalSize},");
        sb.AppendLine($"  \"totalSizeFormatted\": \"{FormatSize(totalSize)}\",");

        // 按文件类型统计
        sb.AppendLine("  \"byExtension\": {");
        var extMap = new System.Collections.Generic.Dictionary<string, long>();
        var extCount = new System.Collections.Generic.Dictionary<string, int>();

        foreach (string file in Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = "(no ext)";

            long size = new FileInfo(file).Length;
            if (!extMap.ContainsKey(ext)) { extMap[ext] = 0; extCount[ext] = 0; }
            extMap[ext] += size;
            extCount[ext]++;
        }

        bool first = true;
        foreach (var kvp in extMap)
        {
            if (!first) sb.AppendLine(",");
            first = false;
            sb.Append($"    \"{kvp.Key}\": {{ \"size\": {kvp.Value}, \"sizeFormatted\": \"{FormatSize(kvp.Value)}\", \"count\": {extCount[kvp.Key]} }}");
        }
        sb.AppendLine();
        sb.AppendLine("  },");

        // 文件列表（按大小降序，最多50个）
        sb.AppendLine("  \"files\": [");
        var fileList = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, long>>();
        foreach (string file in Directory.GetFiles(targetDir, "*.*", SearchOption.AllDirectories))
        {
            string relPath = file.Substring(targetDir.Length).TrimStart('/', '\\').Replace("\\", "/");
            long size = new FileInfo(file).Length;
            fileList.Add(new System.Collections.Generic.KeyValuePair<string, long>(relPath, size));
        }
        fileList.Sort((a, b) => b.Value.CompareTo(a.Value));

        int count = System.Math.Min(fileList.Count, 50);
        for (int i = 0; i < count; i++)
        {
            sb.Append($"    {{ \"path\": \"{fileList[i].Key}\", \"size\": {fileList[i].Value}, \"sizeFormatted\": \"{FormatSize(fileList[i].Value)}\" }}");
            if (i < count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// 验证构建完整性，检查关键文件是否存在
    /// </summary>
    public static bool ValidateBuild(string outputDir = null)
    {
        string targetDir = outputDir ?? OUTPUT_DIR;
        Debug.Log($"[构建验证] 检查目录: {targetDir}");

        if (!Directory.Exists(targetDir))
        {
            Debug.LogError($"[构建验证] 输出目录不存在: {targetDir}");
            return false;
        }

        bool allValid = true;
        var missingFiles = new System.Collections.Generic.List<string>();

        foreach (string criticalFile in CRITICAL_FILES)
        {
            string fullPath = Path.Combine(targetDir, criticalFile);
            if (File.Exists(fullPath))
            {
                long size = new FileInfo(fullPath).Length;
                Debug.Log($"[构建验证] ✓ {criticalFile} ({FormatSize(size)})");
            }
            else
            {
                Debug.LogError($"[构建验证] ✗ 缺少关键文件: {criticalFile}");
                missingFiles.Add(criticalFile);
                allValid = false;
            }
        }

        // 检查 index.html 是否包含 loader 引用
        if (allValid)
        {
            string indexPath = Path.Combine(targetDir, "index.html");
            if (File.Exists(indexPath))
            {
                string indexContent = File.ReadAllText(indexPath);
                if (!indexContent.Contains("loader.js") && !indexContent.Contains("Build.loader.js"))
                {
                    Debug.LogWarning("[构建验证] index.html 可能缺少 loader.js 引用");
                }
            }
        }

        if (allValid)
        {
            Debug.Log($"[构建验证] ✓ 构建完整，所有关键文件存在");
        }
        else
        {
            Debug.LogError($"[构建验证] ✗ 构建不完整，缺少 {missingFiles.Count} 个关键文件");
        }

        return allValid;
    }

    // ========== 构建报告辅助 ==========

    private static void GenerateBuildSizeReport(string targetDir)
    {
        try
        {
            string reportJson = GetBuildSizeReport(targetDir);
            string reportPath = Path.Combine(targetDir, "build-report.json");
            File.WriteAllText(reportPath, reportJson);
            Debug.Log($"[构建报告] build-report.json 已生成: {reportPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[构建报告] 生成 build-report.json 失败: {e.Message}");
        }
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
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
