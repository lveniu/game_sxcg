using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// 微信小游戏平台适配器
/// 继承/包装 MiniGameAdapter，专门处理微信小游戏API
/// 
/// 功能：
/// - 微信登录（wx.login）模拟
/// - 分享（wx.shareAppMessage）模拟
/// - 广告（wx.createRewardedVideoAd）模拟
/// - 性能监控（帧率<30fps自动降画质）
/// - 存储（微信 localStorage 已由 PlayerPrefs 处理）
/// 
/// 所有方法都有 fallback，非微信环境不会崩溃。
/// 
/// 使用方式：
///   WechatMiniGameAdapter.Login(OnLoginSuccess, OnLoginFail);
///   WechatMiniGameAdapter.ShareAppMessage("标题", "描述");
///   WechatMiniGameAdapter.ShowRewardedAd(OnReward, OnAdError);
/// </summary>
public static class WechatMiniGameAdapter
{
    // ========== 平台检测 ==========

    /// <summary>
    /// 是否运行在微信小游戏环境
    /// WebGL构建中通过jslib检测，Editor中始终返回false
    /// </summary>
    public static bool IsWechatEnvironment
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return _CheckWechatEnvironment();
#else
            return false;
#endif
        }
    }

    // ========== 登录模块 ==========

    /// <summary>
    /// 登录结果回调
    /// </summary>
    public struct LoginResult
    {
        public bool Success;
        public string Code;         // 微信临时登录凭证
        public string OpenId;       // 用户唯一标识（需要服务端解密）
        public string ErrorMessage;
    }

    private static Action<LoginResult> _loginCallback;

    /// <summary>
    /// 微信登录：模拟 wx.login() 流程
    /// 非微信环境下返回模拟数据
    /// </summary>
    public static void Login(Action<LoginResult> onSuccess = null, Action<string> onFail = null)
    {
        Debug.Log("[WechatAdapter] 开始登录...");

        if (!IsWechatEnvironment)
        {
            Debug.Log("[WechatAdapter] 非微信环境，使用模拟登录");
            var mockResult = new LoginResult
            {
                Success = true,
                Code = "mock_code_" + UnityEngine.Random.Range(1000, 9999),
                OpenId = "mock_openid_" + UnityEngine.Random.Range(10000, 99999),
                ErrorMessage = null
            };
            onSuccess?.Invoke(mockResult);
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        _loginCallback = (result) =>
        {
            if (result.Success)
            {
                Debug.Log($"[WechatAdapter] 登录成功, code={result.Code}");
                onSuccess?.Invoke(result);
            }
            else
            {
                Debug.LogWarning($"[WechatAdapter] 登录失败: {result.ErrorMessage}");
                onFail?.Invoke(result.ErrorMessage);
            }
        };
        _WxLogin();
#else
        // 不应该到这里，但作为安全回退
        onFail?.Invoke("不支持的平台");
#endif
    }

    /// <summary>
    /// 登录成功回调（由jslib调用）
    /// </summary>
    private static void OnLoginSuccess(string code)
    {
        var result = new LoginResult
        {
            Success = true,
            Code = code,
            OpenId = "", // 需要服务端解密
            ErrorMessage = null
        };
        _loginCallback?.Invoke(result);
    }

    /// <summary>
    /// 登录失败回调（由jslib调用）
    /// </summary>
    private static void OnLoginFail(string errorMsg)
    {
        var result = new LoginResult
        {
            Success = false,
            Code = null,
            OpenId = null,
            ErrorMessage = errorMsg
        };
        _loginCallback?.Invoke(result);
    }

    // ========== 分享模块 ==========

    /// <summary>
    /// 分享结果
    /// </summary>
    public struct ShareResult
    {
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// 分享App消息：模拟 wx.shareAppMessage()
    /// </summary>
    public static void ShareAppMessage(string title, string desc = "", string imageUrl = "", string query = "")
    {
        Debug.Log($"[WechatAdapter] 分享App消息: {title}");

        if (!IsWechatEnvironment)
        {
            Debug.Log($"[WechatAdapter] 非微信环境，模拟分享 — 标题: {title}, 描述: {desc}");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        _WxShareAppMessage(title, desc, imageUrl, query);
#endif
    }

    /// <summary>
    /// 分享到朋友圈（需要用户点击触发）
    /// </summary>
    public static void ShareTimeline(string title, string imageUrl = "", string query = "")
    {
        Debug.Log($"[WechatAdapter] 分享到朋友圈: {title}");

        if (!IsWechatEnvironment)
        {
            Debug.Log($"[WechatAdapter] 非微信环境，模拟分享朋友圈 — 标题: {title}");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        _WxShareTimeline(title, imageUrl, query);
#endif
    }

    // ========== 广告模块 ==========

    /// <summary>
    /// 广告状态
    /// </summary>
    public enum AdState
    {
        NotLoaded,
        Loading,
        Ready,
        Showing,
        Closed,
        Error
    }

    private static AdState _rewardedAdState = AdState.NotLoaded;
    private static Action<bool> _rewardCallback;
    private static Action<string> _adErrorCallback;

    /// <summary>
    /// 创建激励视频广告：模拟 wx.createRewardedVideoAd()
    /// </summary>
    public static void CreateRewardedAd(string adUnitId = "")
    {
        Debug.Log($"[WechatAdapter] 创建激励视频广告: {adUnitId}");

        if (!IsWechatEnvironment)
        {
            Debug.Log("[WechatAdapter] 非微信环境，广告状态设为Ready");
            _rewardedAdState = AdState.Ready;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        _rewardedAdState = AdState.Loading;
        _WxCreateRewardedAd(adUnitId);
#endif
    }

    /// <summary>
    /// 展示激励视频广告
    /// </summary>
    /// <param name="onReward">观看完成回调（true=获得奖励）</param>
    /// <param name="onError">广告错误回调</param>
    public static void ShowRewardedAd(Action<bool> onReward = null, Action<string> onError = null)
    {
        _rewardCallback = onReward;
        _adErrorCallback = onError;

        if (!IsWechatEnvironment)
        {
            Debug.Log("[WechatAdapter] 非微信环境，模拟广告展示（2秒后自动完成）");
            StartAdSimulation();
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (_rewardedAdState != AdState.Ready)
        {
            Debug.LogWarning("[WechatAdapter] 广告未就绪，尝试重新加载");
            _WxShowRewardedAd();
            return;
        }
        _rewardedAdState = AdState.Showing;
        _WxShowRewardedAd();
#endif
    }

    /// <summary>
    /// 获取当前广告状态
    /// </summary>
    public static AdState GetAdState() => _rewardedAdState;

    /// <summary>
    /// 广告加载完成回调（由jslib调用）
    /// </summary>
    private static void OnAdLoaded()
    {
        _rewardedAdState = AdState.Ready;
        Debug.Log("[WechatAdapter] 激励视频广告加载完成");
    }

    /// <summary>
    /// 广告关闭回调（由jslib调用）
    /// </summary>
    private static void OnAdClose(string isEnded)
    {
        _rewardedAdState = AdState.Closed;
        bool rewarded = isEnded == "true" || isEnded == "1";
        Debug.Log($"[WechatAdapter] 广告关闭, isEnded={isEnded}, rewarded={rewarded}");
        _rewardCallback?.Invoke(rewarded);
    }

    /// <summary>
    /// 广告错误回调（由jslib调用）
    /// </summary>
    private static void OnAdError(string errorMsg)
    {
        _rewardedAdState = AdState.Error;
        Debug.LogError($"[WechatAdapter] 广告错误: {errorMsg}");
        _adErrorCallback?.Invoke(errorMsg);
    }

    // 模拟广告（非微信环境）
    private static void StartAdSimulation()
    {
        var runner = GetOrCreateRunner();
        runner.StartCoroutine(AdSimulationCoroutine());
    }

    private static IEnumerator AdSimulationCoroutine()
    {
        _rewardedAdState = AdState.Showing;
        Debug.Log("[WechatAdapter] 📺 模拟广告播放中...");
        yield return new WaitForSeconds(2f);
        _rewardedAdState = AdState.Closed;
        Debug.Log("[WechatAdapter] ✓ 模拟广告播放完成，获得奖励");
        _rewardCallback?.Invoke(true);
    }

    // ========== 性能监控模块 ==========

    /// <summary>
    /// 性能级别
    /// </summary>
    public enum PerformanceLevel
    {
        High,       // 高画质
        Medium,     // 中画质
        Low         // 低画质
    }

    private static PerformanceLevel _currentPerfLevel = PerformanceLevel.High;
    private static float _fpsCheckInterval = 2f;        // 每2秒检查一次帧率
    private static int _frameCount = 0;
    private static float _lastCheckTime = 0f;
    private static bool _perfMonitoringActive = false;
    private static WechatAdapterRunner _runner;

    /// <summary>
    /// 当前性能级别
    /// </summary>
    public static PerformanceLevel CurrentPerformanceLevel => _currentPerfLevel;

    /// <summary>
    /// 帧率低于此阈值自动降画质
    /// </summary>
    public static float LowFpsThreshold = 30f;

    /// <summary>
    /// 帧率高于此阈值自动升画质
    /// </summary>
    public static float HighFpsThreshold = 50f;

    /// <summary>
    /// 当前平均帧率
    /// </summary>
    public static float CurrentFPS { get; private set; }

    /// <summary>
    /// 启动性能监控
    /// </summary>
    public static void StartPerformanceMonitoring()
    {
        if (_perfMonitoringActive) return;

        Debug.Log("[WechatAdapter] 启动性能监控");
        _perfMonitoringActive = true;
        _lastCheckTime = Time.unscaledTime;
        _frameCount = 0;

        var runner = GetOrCreateRunner();
        runner.StartCoroutine(PerformanceMonitorCoroutine());
    }

    /// <summary>
    /// 停止性能监控
    /// </summary>
    public static void StopPerformanceMonitoring()
    {
        _perfMonitoringActive = false;
        Debug.Log("[WechatAdapter] 停止性能监控");
    }

    /// <summary>
    /// 手动设置性能级别
    /// </summary>
    public static void SetPerformanceLevel(PerformanceLevel level)
    {
        _currentPerfLevel = level;
        ApplyPerformanceSettings(level);
    }

    private static IEnumerator PerformanceMonitorCoroutine()
    {
        while (_perfMonitoringActive)
        {
            yield return null;
            _frameCount++;

            float now = Time.unscaledTime;
            float elapsed = now - _lastCheckTime;

            if (elapsed >= _fpsCheckInterval)
            {
                CurrentFPS = _frameCount / elapsed;
                _frameCount = 0;
                _lastCheckTime = now;

                // 自动调整画质
                AutoAdjustPerformance();
            }
        }
    }

    private static void AutoAdjustPerformance()
    {
        float fps = CurrentFPS;

        if (fps < LowFpsThreshold && _currentPerfLevel != PerformanceLevel.Low)
        {
            // 降一级
            PerformanceLevel newLevel = _currentPerfLevel == PerformanceLevel.High
                ? PerformanceLevel.Medium
                : PerformanceLevel.Low;

            Debug.LogWarning($"[WechatAdapter] FPS={fps:F1} < {LowFpsThreshold}，降画质: {_currentPerfLevel} → {newLevel}");
            _currentPerfLevel = newLevel;
            ApplyPerformanceSettings(newLevel);
        }
        else if (fps > HighFpsThreshold && _currentPerfLevel != PerformanceLevel.High)
        {
            // 升一级
            PerformanceLevel newLevel = _currentPerfLevel == PerformanceLevel.Low
                ? PerformanceLevel.Medium
                : PerformanceLevel.High;

            Debug.Log($"[WechatAdapter] FPS={fps:F1} > {HighFpsThreshold}，升画质: {_currentPerfLevel} → {newLevel}");
            _currentPerfLevel = newLevel;
            ApplyPerformanceSettings(newLevel);
        }
    }

    private static void ApplyPerformanceSettings(PerformanceLevel level)
    {
        switch (level)
        {
            case PerformanceLevel.High:
                QualitySettings.SetQualityLevel(Mathf.Max(QualitySettings.names.Length - 1, 0), true);
                Application.targetFrameRate = 60;
                QualitySettings.antiAliasing = 0;
                Debug.Log("[WechatAdapter] 画质: 高 (60fps)");
                break;

            case PerformanceLevel.Medium:
                QualitySettings.SetQualityLevel(Mathf.Max(QualitySettings.names.Length / 2, 0), true);
                Application.targetFrameRate = 45;
                QualitySettings.antiAliasing = 0;
                Debug.Log("[WechatAdapter] 画质: 中 (45fps)");
                break;

            case PerformanceLevel.Low:
                QualitySettings.SetQualityLevel(0, true);
                Application.targetFrameRate = 30;
                QualitySettings.antiAliasing = 0;
                // 降低分辨率
                if (Screen.width > 540)
                {
                    float scale = 540f / Screen.width;
                    Screen.SetResolution(540, Mathf.RoundToInt(Screen.height * scale), true);
                }
                Debug.Log("[WechatAdapter] 画质: 低 (30fps, 降低分辨率)");
                break;
        }
    }

    // ========== 存储模块 ==========
    // 微信 localStorage 已由 PlayerPrefs 处理
    // 这里提供微信特有的存储API封装

    /// <summary>
    /// 微信云存储：设置用户数据（需要开放数据域）
    /// 非微信环境fallback到 PlayerPrefs
    /// </summary>
    public static void SetCloudData(string key, string value)
    {
        if (!IsWechatEnvironment)
        {
            Debug.Log($"[WechatAdapter] 非微信环境，使用本地存储: {key}");
            MiniGameAdapter.Storage.SetString($"cloud_{key}", value);
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        _WxSetUserCloudStorage(key, value);
#endif
    }

    /// <summary>
    /// 微信云存储：获取用户数据
    /// </summary>
    public static void GetCloudData(string key, Action<string> callback)
    {
        if (!IsWechatEnvironment)
        {
            Debug.Log($"[WechatAdapter] 非微信环境，读取本地存储: {key}");
            string value = MiniGameAdapter.Storage.GetString($"cloud_{key}", "");
            callback?.Invoke(value);
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // 实际实现需要通过jslib调用 wx.getUserCloudStorage
        Debug.LogWarning("[WechatAdapter] 云存储读取需要开放数据域支持，fallback到本地");
        string value = MiniGameAdapter.Storage.GetString($"cloud_{key}", "");
        callback?.Invoke(value);
#endif
    }

    // ========== 系统信息 ==========

    /// <summary>
    /// 获取微信系统信息
    /// </summary>
    public static string GetSystemInfo()
    {
        if (!IsWechatEnvironment)
        {
            return $"模拟环境 | Unity {Application.unityVersion} | {SystemInfo.deviceModel}";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        return _WxGetSystemInfoSync();
#else
        return "Unknown";
#endif
    }

    /// <summary>
    /// 振动反馈
    /// </summary>
    public static void Vibrate(bool heavy = false)
    {
        if (!IsWechatEnvironment)
        {
            Debug.Log($"[WechatAdapter] 非微信环境，跳过振动反馈 (heavy={heavy})");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (heavy)
            _WxVibrateLong();
        else
            _WxVibrateShort();
#endif
    }

    /// <summary>
    /// 创建桌面快捷方式（微信小游戏特有）
    /// </summary>
    public static void AddToDesktop(Action<bool> callback = null)
    {
        if (!IsWechatEnvironment)
        {
            Debug.Log("[WechatAdapter] 非微信环境，跳过添加到桌面");
            callback?.Invoke(false);
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[WechatAdapter] 添加到桌面功能需要特殊权限");
        callback?.Invoke(false);
#endif
    }

    // ========== 内部工具 ==========

    private static WechatAdapterRunner GetOrCreateRunner()
    {
        if (_runner != null) return _runner;

        var go = GameObject.Find("[WechatAdapter]");
        if (go == null)
        {
            go = new GameObject("[WechatAdapter]");
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        _runner = go.GetComponent<WechatAdapterRunner>();
        if (_runner == null)
        {
            _runner = go.AddComponent<WechatAdapterRunner>();
        }

        return _runner;
    }

    // ========== JSLIB 声明（WebGL构建时链接） ==========
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool _CheckWechatEnvironment();
    [DllImport("__Internal")] private static extern void _WxLogin();
    [DllImport("__Internal")] private static extern void _WxShareAppMessage(string title, string desc, string imageUrl, string query);
    [DllImport("__Internal")] private static extern void _WxShareTimeline(string title, string imageUrl, string query);
    [DllImport("__Internal")] private static extern void _WxCreateRewardedAd(string adUnitId);
    [DllImport("__Internal")] private static extern void _WxShowRewardedAd();
    [DllImport("__Internal")] private static extern void _WxSetUserCloudStorage(string key, string value);
    [DllImport("__Internal")] private static extern string _WxGetSystemInfoSync();
    [DllImport("__Internal")] private static extern void _WxVibrateShort();
    [DllImport("__Internal")] private static extern void _WxVibrateLong();
#else
    // Editor/非WebGL环境的空实现（确保编译通过）
    private static bool _CheckWechatEnvironment() => false;
    private static void _WxLogin() { }
    private static void _WxShareAppMessage(string title, string desc, string imageUrl, string query) { }
    private static void _WxShareTimeline(string title, string imageUrl, string query) { }
    private static void _WxCreateRewardedAd(string adUnitId) { }
    private static void _WxShowRewardedAd() { }
    private static void _WxSetUserCloudStorage(string key, string value) { }
    private static string _WxGetSystemInfoSync() => "Editor Mode";
    private static void _WxVibrateShort() { }
    private static void _WxVibrateLong() { }
#endif

    /// <summary>
    /// 协程运行器（用于性能监控和模拟广告）
    /// </summary>
    private class WechatAdapterRunner : MonoBehaviour { }
}
