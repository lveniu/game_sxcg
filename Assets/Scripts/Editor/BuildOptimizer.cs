#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 构建优化分析工具
/// [InitializeOnLoad] 在编辑器编译时自动检查资源问题
/// 
/// 功能：
/// - AnalyzeBundleSize() — 分析Resources目录下所有资源大小
/// - GetAssetBreakdown()  — 按类型统计资源
/// - SuggestOptimizations() — 给出优化建议
/// 
/// 菜单：Build → Analyze Bundle Size / Analyze Assets / Suggest Optimizations
/// </summary>
[InitializeOnLoad]
public static class BuildOptimizer
{
    // ========== 阈值配置 ==========
    private const long LARGE_IMAGE_THRESHOLD = 100 * 1024;     // 100KB
    private const long LARGE_JSON_THRESHOLD = 50 * 1024;       // 50KB
    private const long LARGE_AUDIO_THRESHOLD = 500 * 1024;     // 500KB
    private const int TOP_N_FILES = 20;

    // 资源类型分类
    private static readonly string[] TEXTURE_EXTS = { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tif", ".tiff", ".psd", ".gif", ".webp" };
    private static readonly string[] AUDIO_EXTS = { ".mp3", ".wav", ".ogg", ".aac", ".flac", ".aiff" };
    private static readonly string[] JSON_EXTS = { ".json" };
    private static readonly string[] PREFAB_EXTS = { ".prefab" };
    private static readonly string[] SCRIPT_EXTS = { ".cs" };
    private static readonly string[] MODEL_EXTS = { ".fbx", ".obj", ".blend", ".max", ".ma", ".mb", ".gltf", ".glb" };
    private static readonly string[] ANIMATION_EXTS = { ".anim", ".controller", ".playable" };
    private static readonly string[] SHADER_EXTS = { ".shader", ".cginc", ".compute" };

    /// <summary>
    /// 静态构造 — 编辑器加载时自动运行轻量检查
    /// </summary>
    static BuildOptimizer()
    {
        // 延迟到编辑器完全加载后执行，避免阻塞编译
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("BuildOptimizer_Checked", false))
            {
                SessionState.SetBool("BuildOptimizer_Checked", true);
                PerformLightweightCheck();
            }
        };
    }

    // ========== 菜单项 ==========

    [MenuItem("Build/Analyze Bundle Size")]
    public static void AnalyzeBundleSizeMenuItem()
    {
        AnalyzeBundleSize();
    }

    [MenuItem("Build/Analyze Assets (Breakdown)")]
    public static void AnalyzeAssetsMenuItem()
    {
        var breakdown = GetAssetBreakdown();
        LogAssetBreakdown(breakdown);
    }

    [MenuItem("Build/Suggest Optimizations")]
    public static void SuggestOptimizationsMenuItem()
    {
        var suggestions = SuggestOptimizations();
        LogSuggestions(suggestions);
    }

    // ========== 核心功能 ==========

    /// <summary>
    /// 分析Resources目录下所有资源大小
    /// 扫描 Resources/ 下所有文件，统计大小
    /// 输出最大的20个文件
    /// 标记建议压缩的文件（>100KB的图片、>50KB的JSON）
    /// </summary>
    public static void AnalyzeBundleSize()
    {
        Debug.Log("============================================");
        Debug.Log("  资源包大小分析");
        Debug.Log("============================================");

        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            Debug.LogWarning("[BuildOptimizer] Resources/ 目录不存在，扫描整个 Assets/");
            resourcesPath = Application.dataPath;
        }

        // 扫描所有文件
        var allFiles = new List<FileInfo>();
        ScanDirectory(new DirectoryInfo(resourcesPath), allFiles);

        if (allFiles.Count == 0)
        {
            Debug.LogWarning("[BuildOptimizer] 没有找到任何文件");
            return;
        }

        long totalSize = allFiles.Sum(f => f.Length);

        Debug.Log($"[BuildOptimizer] 总文件数: {allFiles.Count}");
        Debug.Log($"[BuildOptimizer] 总大小: {FormatSize(totalSize)}");

        // 按大小降序排列
        var sortedFiles = allFiles.OrderByDescending(f => f.Length).ToList();

        // 输出最大的TOP_N个文件
        Debug.Log($"\n--- 最大的 {Mathf.Min(TOP_N_FILES, sortedFiles.Count)} 个文件 ---");
        int displayCount = Mathf.Min(TOP_N_FILES, sortedFiles.Count);
        for (int i = 0; i < displayCount; i++)
        {
            var fi = sortedFiles[i];
            string relativePath = GetRelativePath(fi.FullName);
            string warning = GetSizeWarning(fi);
            Debug.Log($"  {i + 1,3}. {relativePath} — {FormatSize(fi.Length)} {warning}");
        }

        // 标记建议压缩的文件
        Debug.Log("\n--- 建议压缩/优化的文件 ---");
        int suggestCount = 0;
        foreach (var fi in sortedFiles)
        {
            string ext = fi.Extension.ToLower();
            string relativePath = GetRelativePath(fi.FullName);

            if (IsTextureExt(ext) && fi.Length > LARGE_IMAGE_THRESHOLD)
            {
                Debug.LogWarning($"  🖼️ 图片过大: {relativePath} ({FormatSize(fi.Length)}) — 考虑压缩纹理、降低分辨率或使用ETC/ASTC压缩");
                suggestCount++;
            }
            else if (IsJsonExt(ext) && fi.Length > LARGE_JSON_THRESHOLD)
            {
                Debug.LogWarning($"  📄 JSON过大: {relativePath} ({FormatSize(fi.Length)}) — 考虑精简数据、去除冗余字段");
                suggestCount++;
            }
            else if (IsAudioExt(ext) && fi.Length > LARGE_AUDIO_THRESHOLD)
            {
                Debug.LogWarning($"  🔊 音频过大: {relativePath} ({FormatSize(fi.Length)}) — 考虑使用Vorbis/MP3压缩，降低采样率");
                suggestCount++;
            }
        }

        if (suggestCount == 0)
        {
            Debug.Log("  ✓ 没有发现需要优化的资源");
        }
        else
        {
            Debug.Log($"\n  共 {suggestCount} 个文件建议优化");
        }

        Debug.Log("============================================");
    }

    /// <summary>
    /// 按类型统计资源（texture/audio/json/prefab/script）
    /// </summary>
    public static Dictionary<string, AssetTypeStats> GetAssetBreakdown()
    {
        var breakdown = new Dictionary<string, AssetTypeStats>
        {
            { "Texture", new AssetTypeStats { TypeName = "Texture" } },
            { "Audio", new AssetTypeStats { TypeName = "Audio" } },
            { "JSON", new AssetTypeStats { TypeName = "JSON" } },
            { "Prefab", new AssetTypeStats { TypeName = "Prefab" } },
            { "Script", new AssetTypeStats { TypeName = "Script" } },
            { "Model", new AssetTypeStats { TypeName = "Model" } },
            { "Animation", new AssetTypeStats { TypeName = "Animation" } },
            { "Shader", new AssetTypeStats { TypeName = "Shader" } },
            { "Other", new AssetTypeStats { TypeName = "Other" } }
        };

        string assetsPath = Application.dataPath;
        var allFiles = new List<FileInfo>();
        ScanDirectory(new DirectoryInfo(assetsPath), allFiles, skipPatterns: new[] { "\\Library", "\\Temp", "\\obj", "\\Logs" });

        foreach (var fi in allFiles)
        {
            string ext = fi.Extension.ToLower();
            string category = GetAssetCategory(ext);

            if (!breakdown.ContainsKey(category))
            {
                breakdown[category] = new AssetTypeStats { TypeName = category };
            }

            var stats = breakdown[category];
            stats.FileCount++;
            stats.TotalSize += fi.Length;
            if (fi.Length > stats.LargestSize)
            {
                stats.LargestSize = fi.Length;
                stats.LargestFile = GetRelativePath(fi.FullName);
            }
        }

        return breakdown;
    }

    /// <summary>
    /// 给出优化建议
    /// </summary>
    public static List<OptimizationSuggestion> SuggestOptimizations()
    {
        var suggestions = new List<OptimizationSuggestion>();

        // 1. 检查 Resources 目录
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            var resourceFiles = new List<FileInfo>();
            ScanDirectory(new DirectoryInfo(resourcesPath), resourceFiles);
            long totalResourcesSize = resourceFiles.Sum(f => f.Length);

            if (totalResourcesSize > 10 * 1024 * 1024) // > 10MB
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Priority = SuggestionPriority.High,
                    Category = "Resources",
                    Message = $"Resources/ 目录总大小 {FormatSize(totalResourcesSize)} (>10MB)，" +
                              "所有Resources下资源都会被打入包中。建议使用AssetBundle按需加载。"
                });
            }

            // 检查是否有大的音频文件
            foreach (var fi in resourceFiles)
            {
                if (IsAudioExt(fi.Extension.ToLower()) && fi.Length > LARGE_AUDIO_THRESHOLD)
                {
                    suggestions.Add(new OptimizationSuggestion
                    {
                        Priority = SuggestionPriority.Medium,
                        Category = "Audio",
                        Message = $"音频文件 {GetRelativePath(fi.FullName)} ({FormatSize(fi.Length)})，" +
                                  "建议使用StreamingAssets + AudioClip.LoadFromFile按需加载"
                    });
                }
            }
        }

        // 2. 检查纹理压缩设置
        if (Directory.Exists(resourcesPath))
        {
            var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int uncompressedCount = 0;
            int totalCount = texGuids.Length;
            int checkedCount = 0;
            int maxCheck = 50; // 只检查前50个

            foreach (string guid in texGuids)
            {
                if (checkedCount >= maxCheck) break;
                checkedCount++;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    // 检查是否未启用压缩
                    var platformSettings = importer.GetPlatformTextureSettings("WebGL");
                    if (platformSettings != null && !platformSettings.overridden)
                    {
                        uncompressedCount++;
                    }
                    else if (platformSettings == null)
                    {
                        uncompressedCount++;
                    }
                }
            }

            if (uncompressedCount > 0 && checkedCount > 0)
            {
                float pct = (float)uncompressedCount / checkedCount * 100f;
                suggestions.Add(new OptimizationSuggestion
                {
                    Priority = SuggestionPriority.High,
                    Category = "Texture",
                    Message = $"在检查的 {checkedCount}/{totalCount} 个纹理中，{uncompressedCount} 个({pct:F0}%) " +
                              "没有配置WebGL平台纹理压缩。建议在Texture Import Settings中为WebGL启用ASTC/ETC2压缩。"
                });
            }
        }

        // 3. 检查大JSON配置文件
        var jsonGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
        foreach (string guid in jsonGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".json"))
            {
                var fi = new FileInfo(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
                if (fi.Exists && fi.Length > LARGE_JSON_THRESHOLD)
                {
                    suggestions.Add(new OptimizationSuggestion
                    {
                        Priority = SuggestionPriority.Low,
                        Category = "JSON",
                        Message = $"JSON配置 {path} ({FormatSize(fi.Length)}) — 考虑精简数据结构，" +
                                  "去除运行时不需要的字段，或使用二进制格式替代"
                    });
                }
            }
        }

        // 4. 检查 Player Settings
        if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL) != ScriptingImplementation.IL2CPP)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = SuggestionPriority.High,
                Category = "PlayerSettings",
                Message = "Scripting Backend 不是 IL2CPP，WebGL包体会显著增大。建议设置为 IL2CPP。"
            });
        }

        if (PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL) != ManagedStrippingLevel.High)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = SuggestionPriority.Medium,
                Category = "PlayerSettings",
                Message = "Managed Stripping Level 不是 High，建议设为 High 以减小包体。"
            });
        }

        // 5. 检查未使用的资源（轻量提示）
        suggestions.Add(new OptimizationSuggestion
        {
            Priority = SuggestionPriority.Low,
            Category = "General",
            Message = "建议定期使用 Window → Asset Usage 检查未引用的资源，移除不用的资源可以减小包体。"
        });

        // 按优先级排序
        suggestions.Sort((a, b) => (int)a.Priority - (int)b.Priority);

        return suggestions;
    }

    // ========== 内部方法 ==========

    private static void PerformLightweightCheck()
    {
        try
        {
            string resourcesPath = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesPath)) return;

            var files = new List<FileInfo>();
            ScanDirectory(new DirectoryInfo(resourcesPath), files);

            long totalSize = files.Sum(f => f.Length);
            if (totalSize > 20 * 1024 * 1024) // > 20MB
            {
                Debug.LogWarning($"[BuildOptimizer] Resources/ 目录大小 {FormatSize(totalSize)} (>20MB)，" +
                                 "建议通过菜单 Build → Analyze Bundle Size 查看详情");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BuildOptimizer] 自动检查失败: {e.Message}");
        }
    }

    private static void ScanDirectory(DirectoryInfo dir, List<FileInfo> results, string[] skipPatterns = null)
    {
        try
        {
            if (skipPatterns != null)
            {
                foreach (var skip in skipPatterns)
                {
                    if (dir.FullName.Contains(skip)) return;
                }
            }

            // 跳过隐藏目录和常见的非资源目录
            if (dir.Name.StartsWith(".") || dir.Name == "Library" || dir.Name == "Temp" ||
                dir.Name == "obj" || dir.Name == "Logs" || dir.Name == "Packages")
            {
                return;
            }

            foreach (var file in dir.GetFiles())
            {
                // 跳过 meta 文件
                if (file.Extension == ".meta") continue;
                results.Add(file);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                ScanDirectory(subDir, results, skipPatterns);
            }
        }
        catch (System.Exception)
        {
            // 静默跳过无法访问的目录
        }
    }

    private static string GetRelativePath(string fullPath)
    {
        string basePath = Directory.GetParent(Application.dataPath).FullName;
        if (fullPath.StartsWith(basePath))
        {
            return fullPath.Substring(basePath.Length).TrimStart('/', '\\');
        }
        return fullPath;
    }

    private static string GetAssetCategory(string ext)
    {
        if (IsTextureExt(ext)) return "Texture";
        if (IsAudioExt(ext)) return "Audio";
        if (IsJsonExt(ext)) return "JSON";
        if (IsPrefabExt(ext)) return "Prefab";
        if (IsScriptExt(ext)) return "Script";
        if (IsModelExt(ext)) return "Model";
        if (IsAnimationExt(ext)) return "Animation";
        if (IsShaderExt(ext)) return "Shader";
        return "Other";
    }

    private static string GetSizeWarning(FileInfo fi)
    {
        string ext = fi.Extension.ToLower();
        if (IsTextureExt(ext) && fi.Length > LARGE_IMAGE_THRESHOLD)
            return "⚠️ 图片>100KB";
        if (IsJsonExt(ext) && fi.Length > LARGE_JSON_THRESHOLD)
            return "⚠️ JSON>50KB";
        if (IsAudioExt(ext) && fi.Length > LARGE_AUDIO_THRESHOLD)
            return "⚠️ 音频>500KB";
        return "";
    }

    private static bool IsTextureExt(string ext) => TEXTURE_EXTS.Contains(ext);
    private static bool IsAudioExt(string ext) => AUDIO_EXTS.Contains(ext);
    private static bool IsJsonExt(string ext) => JSON_EXTS.Contains(ext);
    private static bool IsPrefabExt(string ext) => PREFAB_EXTS.Contains(ext);
    private static bool IsScriptExt(string ext) => SCRIPT_EXTS.Contains(ext);
    private static bool IsModelExt(string ext) => MODEL_EXTS.Contains(ext);
    private static bool IsAnimationExt(string ext) => ANIMATION_EXTS.Contains(ext);
    private static bool IsShaderExt(string ext) => SHADER_EXTS.Contains(ext);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private static void LogAssetBreakdown(Dictionary<string, AssetTypeStats> breakdown)
    {
        Debug.Log("============================================");
        Debug.Log("  资源类型统计");
        Debug.Log("============================================");

        long grandTotal = 0;
        foreach (var kvp in breakdown)
        {
            grandTotal += kvp.Value.TotalSize;
        }

        foreach (var kvp in breakdown)
        {
            var stats = kvp.Value;
            if (stats.FileCount == 0) continue;

            float pct = grandTotal > 0 ? (float)stats.TotalSize / grandTotal * 100f : 0;
            Debug.Log($"  {stats.TypeName,-12} {stats.FileCount,5} 个文件  {FormatSize(stats.TotalSize),10}  ({pct:F1}%)  " +
                      $"最大: {stats.LargestFile} ({FormatSize(stats.LargestSize)})");
        }

        Debug.Log($"  {'总计',12} {' ',12}        {FormatSize(grandTotal),10}");
        Debug.Log("============================================");
    }

    private static void LogSuggestions(List<OptimizationSuggestion> suggestions)
    {
        Debug.Log("============================================");
        Debug.Log("  优化建议");
        Debug.Log("============================================");

        if (suggestions.Count == 0)
        {
            Debug.Log("  ✓ 暂无优化建议，项目状态良好！");
            return;
        }

        string[] priorityLabels = { "🔴 高", "🟡 中", "🟢 低" };
        for (int i = 0; i < suggestions.Count; i++)
        {
            var s = suggestions[i];
            string priorityLabel = ((int)s.Priority < priorityLabels.Length)
                ? priorityLabels[(int)s.Priority]
                : "⚪";
            Debug.Log($"\n  {i + 1}. [{priorityLabel}] [{s.Category}]");
            Debug.Log($"     {s.Message}");
        }

        Debug.Log($"\n  共 {suggestions.Count} 条建议");
        Debug.Log("============================================");
    }

    // ========== 数据结构 ==========

    /// <summary>
    /// 资源类型统计
    /// </summary>
    public class AssetTypeStats
    {
        public string TypeName;
        public int FileCount;
        public long TotalSize;
        public long LargestSize;
        public string LargestFile;
    }

    /// <summary>
    /// 优化建议
    /// </summary>
    public class OptimizationSuggestion
    {
        public SuggestionPriority Priority;
        public string Category;
        public string Message;
    }

    /// <summary>
    /// 建议优先级
    /// </summary>
    public enum SuggestionPriority
    {
        High = 0,
        Medium = 1,
        Low = 2
    }
}
#endif
