#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 发布构建准备工具 —— DebugLog 清理器
/// 
/// 功能：
///   1) 一键扫描并清除非 Editor 层的 Debug.Log / LogWarning 调用
///   2) Debug.LogError 默认保留（运行时必要错误日志）
///   3) 保留 #if DEBUG / DEVELOPMENT / UNITY_EDITOR 包裹的日志块
///   4) 生成详细的清除报告
///   5) 提供一键恢复功能（基于备份）
/// 
/// 菜单：
///   Tools → Release Prep → Scan Debug Logs        — 扫描统计
///   Tools → Release Prep → Clean Debug Logs        — 执行清除
///   Tools → Release Prep → Restore Debug Logs      — 恢复备份
///   Tools → Release Prep → Show Last Report        — 查看上次报告
/// </summary>
public static class ReleaseBuildPrep
{
    // ===================== 配置 =====================

    /// <summary>是否清除 Debug.Log（普通日志）</summary>
    private const bool CLEAN_LOG = true;

    /// <summary>是否清除 Debug.LogWarning（警告日志）</summary>
    private const bool CLEAN_WARNING = true;

    /// <summary>是否清除 Debug.LogError（错误日志）—— 默认保留</summary>
    private const bool CLEAN_ERROR = false;

    /// <summary>备份目录（相对于项目根目录）</summary>
    private const string BACKUP_DIR = "Library/DebugLogCleanerBackup";

    /// <summary>报告文件路径</summary>
    private const string REPORT_PATH = "Library/DebugLogCleanerBackup/report.txt";

    /// <summary>报告时间戳</summary>
    private static string reportTimestamp;

    // ===================== 匹配模式 =====================

    // 匹配 Debug.Log / LogWarning / LogError 的整行调用（含多行参数）
    // 支持 Debug.Log(...), Debug.LogWarning(...), Debug.LogError(...)
    // 以及 Debug.LogFormat(...), Debug.LogWarningFormat(...), Debug.LogErrorFormat(...)
    private static readonly Regex DebugLogRegex = new Regex(
        @"(?<indent>\s*)"                                                          // 缩进捕获
        + @"(?<fullline>Debug\.Log(?:Warning|Error|Format|WarningFormat|ErrorFormat)?"
        + @"\s*\((?:[^""()]*|""(?:[^""\\]|\\.)*"")*\));\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled
    );

    // 更宽泛的匹配：处理跨行 Debug.Log 调用（如字符串拼接跨行）
    private static readonly Regex DebugLogMultilineRegex = new Regex(
        @"(?<indent>\s*)"
        + @"Debug\.Log(?:Warning|Error|Format|WarningFormat|ErrorFormat)?"
        + @"\s*\("
        + @"(?:[^()]|\((?<depth>[^()]*)\))*(?(depth)(?!))"
        + @"\)\s*;\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled
    );

    // 匹配 UnityEngine.Debug.Log 形式
    private static readonly Regex UnityEngineDebugLogRegex = new Regex(
        @"(?<indent>\s*)"
        + @"UnityEngine\.Debug\.Log(?:Warning|Error|Format|WarningFormat|ErrorFormat)?"
        + @"\s*\("
        + @"(?:[^()]|\((?<depth>[^()]*)\))*(?(depth)(?!))"
        + @"\)\s*;\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled
    );

    // 预处理指令（这些块内的日志应该保留）
    private static readonly string[] PreserveDirectives = new[]
    {
        "#if DEBUG",
        "#if DEVELOPMENT",
        "#if UNITY_EDITOR",
        "#if UNITY_ASSERTIONS",
        "#if DEVELOPMENT_BUILD",
    };

    // ===================== MenuItem 入口 =====================

    [MenuItem("Tools/Release Prep/1. Scan Debug Logs")]
    public static void ScanAndReport()
    {
        reportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Debug.Log("[ReleaseBuildPrep] ===== 开始扫描 Debug.Log 调用 =====");

        var stats = ScanAllFiles();
        ShowScanResults(stats);
    }

    [MenuItem("Tools/Release Prep/2. Clean Debug Logs")]
    public static void CleanDebugLogs()
    {
        reportTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Debug.Log("[ReleaseBuildPrep] ===== 开始清理 Debug.Log 调用 =====");

        if (!EditorUtility.DisplayDialog(
            "DebugLog 清理确认",
            "将清除以下调用：\n"
            + (CLEAN_LOG ? "  ✓ Debug.Log\n" : "")
            + (CLEAN_WARNING ? "  ✓ Debug.LogWarning\n" : "")
            + (CLEAN_ERROR ? "  ✓ Debug.LogError\n" : "  ✗ Debug.LogError (保留)\n")
            + "\nEditor 文件夹下的日志不受影响\n"
            + "#if DEBUG / DEVELOPMENT 包裹的日志将被保留\n\n"
            + "清理前会自动备份，是否继续？",
            "开始清理", "取消"))
        {
            Debug.Log("[ReleaseBuildPrep] 清理已取消");
            return;
        }

        var result = PerformClean();
        ShowCleanResult(result);
    }

    [MenuItem("Tools/Release Prep/3. Restore Debug Logs")]
    public static void RestoreDebugLogs()
    {
        string backupDir = Path.GetFullPath(BACKUP_DIR);
        if (!Directory.Exists(backupDir) || !Directory.GetFiles(backupDir, "*.cs.bak").Any())
        {
            EditorUtility.DisplayDialog("恢复", "没有找到备份文件。\n请先执行清理操作。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "恢复确认",
            "将从备份恢复所有已清理的 Debug.Log 调用。\n当前修改将被覆盖，是否继续？",
            "恢复", "取消"))
        {
            return;
        }

        var result = RestoreFromBackup();
        string msg = $"恢复完成！\n恢复了 {result.restoredFiles} 个文件";
        Debug.Log($"[ReleaseBuildPrep] {msg}");
        EditorUtility.DisplayDialog("恢复完成", msg, "确定");
    }

    [MenuItem("Tools/Release Prep/4. Show Last Report")]
    public static void ShowLastReport()
    {
        string reportPath = Path.GetFullPath(REPORT_PATH);
        if (!File.Exists(reportPath))
        {
            EditorUtility.DisplayDialog("报告", "没有找到上次清理报告。\n请先执行扫描或清理。", "确定");
            return;
        }

        string content = File.ReadAllText(reportPath);
        // 用 EditorGUILayout 显示太复杂，这里直接输出到 Console
        Debug.Log($"[ReleaseBuildPrep] 上次清理报告:\n{content}");

        // 同时写入剪贴板
        GUIUtility.systemCopyBuffer = content;
        EditorUtility.DisplayDialog("报告",
            "报告已输出到 Console 并复制到剪贴板。\n\n内容摘要见 Console 面板。", "确定");
    }

    // ===================== 扫描逻辑 =====================

    private class ScanStats
    {
        public int totalFiles;
        public int totalLogCalls;
        public int totalWarningCalls;
        public int totalErrorCalls;
        public int totalLogFormatCalls;
        public int totalWarningFormatCalls;
        public int totalErrorFormatCalls;
        public int editorFiles;
        public int editorLogCalls;
        public int preservedByDirective;
        public int cleanableLog;
        public int cleanableWarning;
        public int cleanableError;
        public List<FileScanInfo> fileDetails = new List<FileScanInfo>();
    }

    private class FileScanInfo
    {
        public string filePath;
        public bool isEditor;
        public int logCount;
        public int warningCount;
        public int errorCount;
        public int logFormatCount;
        public int warningFormatCount;
        public int errorFormatCount;
        public int preservedCount;
        public List<LogEntry> entries = new List<LogEntry>();
    }

    private class LogEntry
    {
        public int lineNumber;
        public string logType;  // Log, LogWarning, LogError, LogFormat, etc.
        public string lineContent;
        public bool isPreserved; // 在预处理指令块内
        public bool isEditorFile;
    }

    private static ScanStats ScanAllFiles()
    {
        var stats = new ScanStats();
        string projectRoot = Path.GetFullPath(".");

        // 查找所有 C# 文件
        var csFiles = Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/Temp/"))
            .ToList();

        stats.totalFiles = csFiles.Count;

        foreach (var file in csFiles)
        {
            var fileInfo = ScanFile(file, projectRoot);
            if (fileInfo.logCount + fileInfo.warningCount + fileInfo.errorCount
                + fileInfo.logFormatCount + fileInfo.warningFormatCount + fileInfo.errorFormatCount > 0)
            {
                stats.fileDetails.Add(fileInfo);
            }
        }

        // 汇总统计
        foreach (var fi in stats.fileDetails)
        {
            if (fi.isEditor) { stats.editorFiles++; stats.editorLogCalls += fi.logCount + fi.warningCount + fi.errorCount + fi.logFormatCount + fi.warningFormatCount + fi.errorFormatCount; }
            stats.totalLogCalls += fi.logCount;
            stats.totalWarningCalls += fi.warningCount;
            stats.totalErrorCalls += fi.errorCount;
            stats.totalLogFormatCalls += fi.logFormatCount;
            stats.totalWarningFormatCalls += fi.warningFormatCount;
            stats.totalErrorFormatCalls += fi.errorFormatCount;
            stats.preservedByDirective += fi.preservedCount;
        }

        stats.cleanableLog = stats.totalLogCalls + stats.totalLogFormatCalls;
        stats.cleanableWarning = stats.totalWarningCalls + stats.totalWarningFormatCalls;
        stats.cleanableError = stats.totalErrorCalls + stats.totalErrorFormatCalls;

        return stats;
    }

    private static FileScanInfo ScanFile(string filePath, string projectRoot)
    {
        var info = new FileScanInfo();
        info.filePath = filePath.StartsWith(projectRoot)
            ? filePath.Substring(projectRoot.Length).TrimStart('/', '\\')
            : filePath;
        info.isEditor = info.filePath.Contains("/Editor/") || info.filePath.Contains("\\Editor\\");

        string[] lines = File.ReadAllLines(filePath);

        // 预处理：构建条件编译栈
        var directiveStack = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // 追踪预处理指令
            if (trimmed.StartsWith("#if "))
            {
                directiveStack.Add(trimmed);
            }
            else if (trimmed == "#endif" || trimmed.StartsWith("#endif "))
            {
                if (directiveStack.Count > 0) directiveStack.RemoveAt(directiveStack.Count - 1);
            }
            else if (trimmed == "#else" || trimmed.StartsWith("#else "))
            {
                // #else 反转状态 — 简化处理，不做深度分析
            }

            // 检查是否包含 Debug.Log 调用
            if (!ContainsDebugLog(trimmed)) continue;

            // 判断是否在保留的预处理指令块内
            bool isPreserved = false;
            foreach (var directive in directiveStack)
            {
                foreach (var preserveDir in PreserveDirectives)
                {
                    if (directive.Contains(preserveDir.Replace("#if ", "")))
                    {
                        isPreserved = true;
                        break;
                    }
                }
                if (isPreserved) break;
            }

            var entry = new LogEntry
            {
                lineNumber = i + 1,
                lineContent = trimmed,
                isPreserved = isPreserved,
                isEditorFile = info.isEditor
            };

            // 分类
            if (trimmed.Contains("Debug.LogError") || trimmed.Contains("Debug.LogErrorFormat"))
            {
                if (trimmed.Contains("LogErrorFormat")) info.errorFormatCount++;
                else info.errorCount++;
                entry.logType = "LogError";
            }
            else if (trimmed.Contains("Debug.LogWarning") || trimmed.Contains("Debug.LogWarningFormat"))
            {
                if (trimmed.Contains("LogWarningFormat")) info.warningFormatCount++;
                else info.warningCount++;
                entry.logType = "LogWarning";
            }
            else if (trimmed.Contains("Debug.Log") || trimmed.Contains("Debug.LogFormat"))
            {
                if (trimmed.Contains("LogFormat")) info.logFormatCount++;
                else info.logCount++;
                entry.logType = "Log";
            }

            if (isPreserved) info.preservedCount++;
            info.entries.Add(entry);
        }

        return info;
    }

    private static bool ContainsDebugLog(string line)
    {
        return line.Contains("Debug.Log") || line.Contains("UnityEngine.Debug.Log");
    }

    // ===================== 清理逻辑 =====================

    private class CleanResult
    {
        public int totalScanned;
        public int totalCleaned;
        public int logCleaned;
        public int warningCleaned;
        public int errorCleaned;
        public int preservedCount;
        public int editorSkipped;
        public int filesModified;
        public List<string> modifiedFiles = new List<string>();
        public List<string> detailLog = new List<string>();
    }

    private static CleanResult PerformClean()
    {
        var result = new CleanResult();
        string projectRoot = Path.GetFullPath(".");
        string backupDir = Path.GetFullPath(BACKUP_DIR);

        // 创建/清空备份目录
        if (Directory.Exists(backupDir))
            Directory.Delete(backupDir, true);
        Directory.CreateDirectory(backupDir);

        // 查找所有非 Editor 的 C# 文件
        var csFiles = Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/Temp/"))
            .Where(f =>
            {
                string rel = f.Substring(projectRoot.Length).TrimStart('/', '\\');
                bool isEd = rel.Contains("/Editor/") || rel.Contains("\\Editor\\");
                // Editor 文件跳过
                if (isEd) { result.editorSkipped++; return false; }
                // GameLogger.cs 本身跳过（它定义了日志系统）
                if (rel.EndsWith("GameLogger.cs")) return false;
                return true;
            })
            .ToList();

        result.totalScanned = csFiles.Count;

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            if (!ContainsDebugLog(content))
                continue;

            string relPath = file.Substring(projectRoot.Length).TrimStart('/', '\\');
            string originalContent = content;
            int cleanedInFile = 0;

            // 备份
            string backupPath = Path.Combine(backupDir, relPath.Replace('/', '_').Replace('\\', '_') + ".bak");
            File.WriteAllText(backupPath, originalContent, Encoding.UTF8);

            // 执行清理
            var cleanResult = CleanContent(content);
            content = cleanResult.cleanedContent;
            cleanedInFile = cleanResult.cleanedCount;
            result.logCleaned += cleanResult.logCleaned;
            result.warningCleaned += cleanResult.warningCleaned;
            result.errorCleaned += cleanResult.errorCleaned;
            result.preservedCount += cleanResult.preservedCount;

            if (cleanedInFile > 0)
            {
                File.WriteAllText(file, content, Encoding.UTF8);
                result.filesModified++;
                result.modifiedFiles.Add(relPath);
                result.totalCleaned += cleanedInFile;
                result.detailLog.Add($"  [{relPath}] 清除 {cleanedInFile} 处 (Log:{cleanResult.logCleaned} Warn:{cleanResult.warningCleaned} Err:{cleanResult.errorCleaned} Preserved:{cleanResult.preservedCount})");
            }
            else
            {
                // 没有需要清除的，删除备份
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
        }

        // 生成报告
        GenerateReport(result);

        return result;
    }

    private class SingleFileCleanResult
    {
        public string cleanedContent;
        public int cleanedCount;
        public int logCleaned;
        public int warningCleaned;
        public int errorCleaned;
        public int preservedCount;
    }

    private static SingleFileCleanResult CleanContent(string content)
    {
        var result = new SingleFileCleanResult();
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var output = new List<string>();
        var directiveStack = new List<string>();
        bool skipNextBlank = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // 追踪预处理指令栈
            if (trimmed.StartsWith("#if "))
            {
                directiveStack.Add(trimmed);
                output.Add(line);
                skipNextBlank = false;
                continue;
            }
            if (trimmed == "#endif" || trimmed.StartsWith("#endif "))
            {
                if (directiveStack.Count > 0) directiveStack.RemoveAt(directiveStack.Count - 1);
                output.Add(line);
                skipNextBlank = false;
                continue;
            }
            if (trimmed == "#else" || trimmed.StartsWith("#else ") || trimmed == "#elif" || trimmed.StartsWith("#elif "))
            {
                output.Add(line);
                skipNextBlank = false;
                continue;
            }

            // 检查是否在保留的预处理块内
            bool isInPreservedBlock = false;
            foreach (var directive in directiveStack)
            {
                foreach (var preserveDir in PreserveDirectives)
                {
                    if (directive.Contains(preserveDir.Replace("#if ", "")))
                    {
                        isInPreservedBlock = true;
                        break;
                    }
                }
                if (isInPreservedBlock) break;
            }

            // 检查是否包含 Debug.Log 调用
            if (!ContainsDebugLog(trimmed) && !trimmed.Contains("UnityEngine.Debug.Log"))
            {
                output.Add(line);
                skipNextBlank = false;
                continue;
            }

            // 在保留块内 → 保留
            if (isInPreservedBlock)
            {
                output.Add(line);
                result.preservedCount++;
                skipNextBlank = false;
                continue;
            }

            // 判断日志类型
            bool isLog = trimmed.Contains("Debug.Log") && !trimmed.Contains("Debug.LogError") && !trimmed.Contains("Debug.LogWarning");
            bool isWarning = trimmed.Contains("Debug.LogWarning") || trimmed.Contains("Debug.LogWarningFormat");
            bool isError = trimmed.Contains("Debug.LogError") || trimmed.Contains("Debug.LogErrorFormat");

            // 也处理 UnityEngine.Debug.xxx 形式
            if (!isLog && !isWarning && !isError)
            {
                isLog = trimmed.Contains("UnityEngine.Debug.Log") && !trimmed.Contains("UnityEngine.Debug.LogError") && !trimmed.Contains("UnityEngine.Debug.LogWarning");
                isWarning = trimmed.Contains("UnityEngine.Debug.LogWarning");
                isError = trimmed.Contains("UnityEngine.Debug.LogError");
            }

            // 决定是否清除
            bool shouldClean = false;
            if (isLog && CLEAN_LOG) shouldClean = true;
            if (isWarning && CLEAN_WARNING) shouldClean = true;
            if (isError && CLEAN_ERROR) shouldClean = true;

            if (!shouldClean)
            {
                // 保留（比如 LogError）
                output.Add(line);
                skipNextBlank = false;
                continue;
            }

            // ===== 执行清除 =====

            // 尝试匹配整行调用（单行）
            if (IsSingleLineCall(trimmed))
            {
                // 清除此行
                result.cleanedCount++;
                if (isLog) result.logCleaned++;
                else if (isWarning) result.warningCleaned++;
                else if (isError) result.errorCleaned++;

                // 跳过此行（不添加到输出）
                skipNextBlank = true;
                continue;
            }

            // 多行调用（Debug.Log 内有换行）
            if (trimmed.StartsWith("Debug.Log") || trimmed.StartsWith("UnityEngine.Debug.Log"))
            {
                // 收集完整的语句直到找到分号
                var statementLines = new List<string> { line };
                int j = i + 1;
                int parenDepth = CountChar(trimmed, '(') - CountChar(trimmed, ')');

                while (j < lines.Length && parenDepth > 0)
                {
                    string nextLine = lines[j];
                    string nextTrimmed = nextLine.Trim();
                    statementLines.Add(nextLine);
                    parenDepth += CountChar(nextTrimmed, '(') - CountChar(nextTrimmed, ')');
                    j++;
                }

                // 检查语句最后一行是否以分号结尾
                if (j <= lines.Length && parenDepth <= 0)
                {
                    result.cleanedCount++;
                    if (isLog) result.logCleaned++;
                    else if (isWarning) result.warningCleaned++;
                    else if (isError) result.errorCleaned++;

                    i = j - 1; // 跳过多行语句
                    skipNextBlank = true;
                    continue;
                }
            }

            // 无法精确判断的，保留（安全策略）
            output.Add(line);
            skipNextBlank = false;
        }

        result.cleanedContent = string.Join("\n", output);
        return result;
    }

    private static bool IsSingleLineCall(string trimmed)
    {
        // 简单判断：以 Debug.Log 开头且行内包含分号
        if (!(trimmed.StartsWith("Debug.Log") || trimmed.StartsWith("UnityEngine.Debug.Log")))
            return false;

        int semicolonIdx = trimmed.LastIndexOf(';');
        if (semicolonIdx < 0) return false;

        // 确保分号在行尾附近
        string afterSemicolon = trimmed.Substring(semicolonIdx + 1).Trim();
        return string.IsNullOrEmpty(afterSemicolon);
    }

    private static int CountChar(string s, char c)
    {
        int count = 0;
        foreach (char ch in s)
        {
            if (ch == c) count++;
            // 忽略字符串内的括号（简化处理）
        }
        return count;
    }

    // ===================== 恢复逻辑 =====================

    private class RestoreResult
    {
        public int restoredFiles;
        public List<string> restoredFileNames = new List<string>();
    }

    private static RestoreResult RestoreFromBackup()
    {
        var result = new RestoreResult();
        string backupDir = Path.GetFullPath(BACKUP_DIR);
        string projectRoot = Path.GetFullPath(".");

        if (!Directory.Exists(backupDir))
        {
            Debug.LogWarning("[ReleaseBuildPrep] 备份目录不存在");
            return result;
        }

        // 读取备份映射
        var bakFiles = Directory.GetFiles(backupDir, "*.cs.bak");
        Debug.Log($"[ReleaseBuildPrep] 找到 {bakFiles.Length} 个备份文件");

        foreach (var bakFile in bakFiles)
        {
            string bakName = Path.GetFileName(bakFile);
            // 备份文件名格式: Assets_Scripts_XXX.cs.bak
            // 需要还原为: Assets/Scripts/XXX.cs
            string relPath = bakName
                .Substring(0, bakName.Length - ".bak".Length)
                .Replace('_', '/');

            // 但是文件名中的 _ 也可能是原始的，所以需要更聪明的映射
            // 策略：在项目中查找匹配的文件
            string restored = RestoreFileByBackup(bakFile, projectRoot);
            if (restored != null)
            {
                result.restoredFiles++;
                result.restoredFileNames.Add(restored);
            }
        }

        // 刷新资源
        AssetDatabase.Refresh();

        return result;
    }

    private static string RestoreFileByBackup(string bakFile, string projectRoot)
    {
        // 使用备份文件中存储的原始路径映射
        string mappingFile = Path.Combine(Path.GetFullPath(BACKUP_DIR), "path_mapping.txt");
        if (!File.Exists(mappingFile))
        {
            Debug.LogWarning("[ReleaseBuildPrep] 路径映射文件不存在，尝试模糊匹配");
            return FuzzyRestore(bakFile, projectRoot);
        }

        var mappings = File.ReadAllLines(mappingFile);
        string bakName = Path.GetFileName(bakFile);

        foreach (var mapping in mappings)
        {
            var parts = mapping.Split(new[] { " -> " }, StringSplitOptions.None);
            if (parts.Length == 2 && parts[0] == bakName)
            {
                string targetPath = Path.Combine(projectRoot, parts[1].Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(targetPath))
                {
                    string content = File.ReadAllText(bakFile, Encoding.UTF8);
                    File.WriteAllText(targetPath, content, Encoding.UTF8);
                    Debug.Log($"[ReleaseBuildPrep] 恢复: {parts[1]}");
                    return parts[1];
                }
            }
        }

        return null;
    }

    private static string FuzzyRestore(string bakFile, string projectRoot)
    {
        string bakName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(bakFile));
        // bakName like: Assets_Scripts_Battle_BattleManager.cs
        // 找到 .cs 的位置
        string csName = bakName;
        // 搜索匹配的文件
        var candidates = Directory.GetFiles(Path.Combine(projectRoot, "Assets"), Path.GetFileName(csName) + "", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs"))
            .ToList();

        // 找最佳匹配
        foreach (var candidate in candidates)
        {
            string rel = candidate.Substring(projectRoot.Length).TrimStart('/', '\\');
            string normalized = rel.Replace('/', '_').Replace('\\', '_').Replace(".cs", "");
            if (normalized == bakName.Replace(".cs", ""))
            {
                string content = File.ReadAllText(bakFile, Encoding.UTF8);
                File.WriteAllText(candidate, content, Encoding.UTF8);
                Debug.Log($"[ReleaseBuildPrep] 恢复(fuzzy): {rel}");
                return rel;
            }
        }

        Debug.LogWarning($"[ReleaseBuildPrep] 无法找到备份对应的文件: {bakName}");
        return null;
    }

    // ===================== 报告生成 =====================

    private static void GenerateReport(CleanResult result)
    {
        string backupDir = Path.GetFullPath(BACKUP_DIR);
        Directory.CreateDirectory(backupDir);

        var sb = new StringBuilder();
        sb.AppendLine($"========================================");
        sb.AppendLine($"  DebugLog 清理报告");
        sb.AppendLine($"  时间: {reportTimestamp}");
        sb.AppendLine($"========================================");
        sb.AppendLine();
        sb.AppendLine($"扫描文件数:   {result.totalScanned}");
        sb.AppendLine($"Editor跳过:   {result.editorSkipped}");
        sb.AppendLine($"修改文件数:   {result.filesModified}");
        sb.AppendLine();
        sb.AppendLine($"--- 清除统计 ---");
        sb.AppendLine($"Debug.Log 清除:       {result.logCleaned}");
        sb.AppendLine($"Debug.LogWarning 清除: {result.warningCleaned}");
        sb.AppendLine($"Debug.LogError 清除:   {result.errorCleaned}");
        sb.AppendLine($"总计清除:             {result.totalCleaned}");
        sb.AppendLine($"条件编译保留:         {result.preservedCount}");
        sb.AppendLine();
        sb.AppendLine($"--- 修改的文件列表 ---");
        foreach (var file in result.modifiedFiles)
        {
            sb.AppendLine($"  {file}");
        }
        sb.AppendLine();
        sb.AppendLine($"--- 详细日志 ---");
        foreach (var detail in result.detailLog)
        {
            sb.AppendLine(detail);
        }
        sb.AppendLine();
        sb.AppendLine($"========================================");

        string report = sb.ToString();
        File.WriteAllText(Path.GetFullPath(REPORT_PATH), report, Encoding.UTF8);

        Debug.Log($"[ReleaseBuildPrep] 报告已保存到: {REPORT_PATH}");

        // 保存路径映射
        SavePathMapping(result);
    }

    private static void SavePathMapping(CleanResult result)
    {
        string mappingPath = Path.Combine(Path.GetFullPath(BACKUP_DIR), "path_mapping.txt");
        var sb = new StringBuilder();
        foreach (var relPath in result.modifiedFiles)
        {
            string bakName = relPath.Replace('/', '_').Replace('\\', '_') + ".bak";
            sb.AppendLine($"{bakName} -> {relPath}");
        }
        File.WriteAllText(mappingPath, sb.ToString(), Encoding.UTF8);
    }

    // ===================== 显示结果 =====================

    private static void ShowScanResults(ScanStats stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== Debug.Log 扫描结果 =====");
        sb.AppendLine();
        sb.AppendLine($"扫描 C# 文件总数:    {stats.totalFiles}");
        sb.AppendLine($"含日志的文件数:      {stats.fileDetails.Count}");
        sb.AppendLine($"Editor 文件夹文件:   {stats.editorFiles}");
        sb.AppendLine();
        sb.AppendLine("--- 日志分类统计 ---");
        sb.AppendLine($"Debug.Log:           {stats.totalLogCalls}");
        sb.AppendLine($"Debug.LogWarning:    {stats.totalWarningCalls}");
        sb.AppendLine($"Debug.LogError:      {stats.totalErrorCalls}");
        sb.AppendLine($"Debug.LogFormat:     {stats.totalLogFormatCalls}");
        sb.AppendLine($"Debug.LogWarningFormat: {stats.totalWarningFormatCalls}");
        sb.AppendLine($"Debug.LogErrorFormat:   {stats.totalErrorFormatCalls}");
        sb.AppendLine($"总计:                {stats.totalLogCalls + stats.totalWarningCalls + stats.totalErrorCalls + stats.totalLogFormatCalls + stats.totalWarningFormatCalls + stats.totalErrorFormatCalls}");
        sb.AppendLine();
        sb.AppendLine("--- 清理预览 ---");
        sb.AppendLine($"可清除 Log:          {stats.cleanableLog} (Editor跳过后)");
        sb.AppendLine($"可清除 Warning:      {stats.cleanableWarning} (Editor跳过后)");
        sb.AppendLine($"LogError(将保留):    {stats.cleanableError}");
        sb.AppendLine($"条件编译保留数:      {stats.preservedByDirective}");
        sb.AppendLine();
        sb.AppendLine("--- Top 20 文件 (按日志数) ---");
        foreach (var fi in stats.fileDetails
            .OrderByDescending(f => f.logCount + f.warningCount + f.errorCount + f.logFormatCount + f.warningFormatCount + f.errorFormatCount)
            .Take(20))
        {
            int total = fi.logCount + fi.warningCount + fi.errorCount + fi.logFormatCount + fi.warningFormatCount + fi.errorFormatCount;
            string tag = fi.isEditor ? "[Editor]" : "[Runtime]";
            sb.AppendLine($"  {tag} {fi.filePath}: {total} (Log:{fi.logCount} Warn:{fi.warningCount} Err:{fi.errorCount})");
        }

        string reportText = sb.ToString();
        Debug.Log($"[ReleaseBuildPrep]\n{reportText}");

        // 同时保存扫描报告
        string scanReportPath = Path.GetFullPath("Library/DebugLogCleanerBackup/scan_report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(scanReportPath));
        File.WriteAllText(scanReportPath, $"扫描时间: {reportTimestamp}\n\n{reportText}", Encoding.UTF8);
    }

    private static void ShowCleanResult(CleanResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== DebugLog 清理结果 =====");
        sb.AppendLine();
        sb.AppendLine($"扫描文件:    {result.totalScanned}");
        sb.AppendLine($"修改文件:    {result.filesModified}");
        sb.AppendLine($"清除总数:    {result.totalCleaned}");
        sb.AppendLine($"  Log 清除:  {result.logCleaned}");
        sb.AppendLine($"  Warning:   {result.warningCleaned}");
        sb.AppendLine($"  Error:     {result.errorCleaned}");
        sb.AppendLine($"  条件编译保留: {result.preservedCount}");
        sb.AppendLine();
        sb.AppendLine("修改的文件:");
        foreach (var f in result.modifiedFiles)
        {
            sb.AppendLine($"  {f}");
        }

        string msg = sb.ToString();
        Debug.Log($"[ReleaseBuildPrep]\n{msg}");

        EditorUtility.DisplayDialog("清理完成",
            $"清除完成！\n\n"
            + $"修改文件: {result.filesModified}\n"
            + $"清除 Debug.Log: {result.logCleaned}\n"
            + $"清除 LogWarning: {result.warningCleaned}\n"
            + $"清除 LogError: {result.errorCleaned}\n"
            + $"条件编译保留: {result.preservedCount}\n\n"
            + $"备份保存在: {BACKUP_DIR}\n"
            + $"可通过 Tools → Release Prep → Restore 恢复",
            "确定");

        // 刷新资源
        AssetDatabase.Refresh();
    }
}
#endif
