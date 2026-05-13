using UnityEngine;
using System.Diagnostics;

/// <summary>
/// 轻量级日志系统，替代 Debug.Log。
/// 
/// - DEBUG:   仅在 UNITY_EDITOR 中生效，构建时自动剔除。
/// - WARNING: 保留但可通过 enableWarning 开关控制。
/// - ERROR:   保留但可通过 enableError 开关控制。
/// 
/// 用法: GameLogger.Debug("msg"); GameLogger.Warning("msg"); GameLogger.Error("msg");
/// </summary>
public static class GameLogger
{
    // ==================== 全局开关 ====================
    /// <summary>全局总开关，关闭后所有日志均不输出。</summary>
    public static bool enableLogging = true;

    /// <summary>WARNING 级别开关（关闭后 Warning 调用不输出）。</summary>
    public static bool enableWarning = true;

    /// <summary>ERROR 级别开关（关闭后 Error 调用不输出）。</summary>
    public static bool enableError = true;

    // ==================== DEBUG 级别 ====================
    // [Conditional] 使方法仅在定义了对应编译符号时才保留调用处代码。
    // UNITY_EDITOR 在 Unity 编辑器构建中自动定义，发布构建中不存在 → 调用被剔除。

    [Conditional("UNITY_EDITOR")]
    public static void Debug(string message, Object context = null)
    {
        if (!enableLogging) return;
        UnityEngine.Debug.Log($"[DEBUG] {message}", context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void DebugFormat(string format, Object context = null, params object[] args)
    {
        if (!enableLogging) return;
        UnityEngine.Debug.LogFormat(context, $"[DEBUG] {format}", args);
    }

    // ==================== WARNING 级别 ====================

    public static void Warning(string message, Object context = null)
    {
        if (!enableLogging || !enableWarning) return;
        UnityEngine.Debug.LogWarning($"[WARN] {message}", context);
    }

    public static void WarningFormat(string format, Object context = null, params object[] args)
    {
        if (!enableLogging || !enableWarning) return;
        UnityEngine.Debug.LogWarningFormat(context, $"[WARN] {format}", args);
    }

    // ==================== ERROR 级别 ====================

    public static void Error(string message, Object context = null)
    {
        if (!enableLogging || !enableError) return;
        UnityEngine.Debug.LogError($"[ERROR] {message}", context);
    }

    public static void ErrorFormat(string format, Object context = null, params object[] args)
    {
        if (!enableLogging || !enableError) return;
        UnityEngine.Debug.LogErrorFormat(context, $"[ERROR] {format}", args);
    }
}
