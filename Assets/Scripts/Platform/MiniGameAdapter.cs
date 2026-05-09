using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// FE-10 小游戏适配层 — 统一封装音频、存储、分享等平台API
/// 
/// 设计原则：
/// - 游戏其他模块只调用 MiniGameAdapter，不直接依赖平台SDK
/// - 默认提供 Unity 原生实现（PC/移动端）
/// - 子平台（微信小游戏等）通过 partial class 或子类覆盖
/// 
/// 使用方式：MiniGameAdapter.Audio.PlayBGM("bgm_battle");
/// </summary>
public static class MiniGameAdapter
{
    // ============================================================
    // 音频模块
    // ============================================================
    public static class Audio
    {
        private static AudioSource _bgmSource;
        private static AudioSource _sfxSource;
        private static AudioSource _uiSource;
        private static float _masterVolume = 1f;
        private static float _bgmVolume = 0.7f;
        private static float _sfxVolume = 1f;
        private static bool _initialized = false;

        /// <summary> 确保有AudioSource（懒初始化） </summary>
        private static void EnsureInit()
        {
            if (_initialized) return;

            var go = new GameObject("[MiniGameAdapter_Audio]");
            UnityEngine.Object.DontDestroyOnLoad(go);

            _bgmSource = go.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            _sfxSource = go.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;

            _uiSource = go.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;

            // 加载保存的音量设置
            _masterVolume = Storage.GetFloat("audio_master", 1f);
            _bgmVolume = Storage.GetFloat("audio_bgm", 0.7f);
            _sfxVolume = Storage.GetFloat("audio_sfx", 1f);

            ApplyVolumes();
            _initialized = true;
        }

        // ---- BGM ----

        public static void PlayBGM(string clipName, float fadeIn = 0.5f)
        {
            EnsureInit();
            var clip = LoadClip(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] BGM未找到: {clipName}");
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            // 简单淡入（DOTween可能不可用，用LeanTween或手动）
            _bgmSource.volume = _bgmVolume * _masterVolume;
        }

        public static void StopBGM(float fadeOut = 0.3f)
        {
            EnsureInit();
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.volume = 0f;
                _bgmSource.Stop();
            }
        }

        // ---- SFX ----

        public static void PlaySFX(string clipName, float volumeScale = 1f)
        {
            EnsureInit();
            var clip = LoadClip(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] SFX未找到: {clipName}");
                return;
            }
            _sfxSource.PlayOneShot(clip, volumeScale * _sfxVolume * _masterVolume);
        }

        // ---- UI音效 ----

        public static void PlayUIClick()
        {
            EnsureInit();
            var clip = LoadClip("ui_click");
            if (clip != null)
                _uiSource.PlayOneShot(clip, _sfxVolume * _masterVolume);
        }

        public static void PlayUIHover()
        {
            EnsureInit();
            var clip = LoadClip("ui_hover");
            if (clip != null)
                _uiSource.PlayOneShot(clip, _sfxVolume * _masterVolume * 0.5f);
        }

        // ---- 音量控制 ----

        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                Storage.SetFloat("audio_master", _masterVolume);
                ApplyVolumes();
            }
        }

        public static float BGMVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                Storage.SetFloat("audio_bgm", _bgmVolume);
                ApplyVolumes();
            }
        }

        public static float SFXVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                Storage.SetFloat("audio_sfx", _sfxVolume);
            }
        }

        private static void ApplyVolumes()
        {
            if (_bgmSource != null)
                _bgmSource.volume = _bgmVolume * _masterVolume;
        }

        // ---- 资源加载 ----

        private static readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

        private static AudioClip LoadClip(string clipName)
        {
            if (_clipCache.TryGetValue(clipName, out var cached))
                return cached;

            // 从 Resources/Audio/ 加载
            var clip = Resources.Load<AudioClip>($"Audio/{clipName}");
            if (clip != null)
            {
                _clipCache[clipName] = clip;
                return clip;
            }

            // 尝试不带后缀
            clip = Resources.Load<AudioClip>(clipName);
            if (clip != null)
            {
                _clipCache[clipName] = clip;
                return clip;
            }

            return null;
        }

        public static void ClearCache()
        {
            _clipCache.Clear();
        }
    }

    // ============================================================
    // 存储模块
    // ============================================================
    public static class Storage
    {
        private static string _saveDir;
        private static bool _initialized = false;

        private static void EnsureInit()
        {
            if (_initialized) return;
            _saveDir = Path.Combine(Application.persistentDataPath, "SaveData");
            if (!Directory.Exists(_saveDir))
                Directory.CreateDirectory(_saveDir);
            _initialized = true;
        }

        // ---- 键值对存储（轻量设置） ----

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt($"mg_{key}", value);
            PlayerPrefs.Save();
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt($"mg_{key}", defaultValue);
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat($"mg_{key}", value);
            PlayerPrefs.Save();
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat($"mg_{key}", defaultValue);
        }

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString($"mg_{key}", value);
            PlayerPrefs.Save();
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString($"mg_{key}", defaultValue);
        }

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey($"mg_{key}");
            PlayerPrefs.Save();
        }

        public static bool HasKey(string key)
        {
            return PlayerPrefs.HasKey($"mg_{key}");
        }

        // ---- JSON文件存储（游戏存档） ----

        /// <summary>
        /// 保存JSON到文件
        /// </summary>
        public static bool SaveJson(string fileName, string jsonData)
        {
            EnsureInit();
            try
            {
                var filePath = Path.Combine(_saveDir, $"{fileName}.json");
                File.WriteAllText(filePath, jsonData);
                Debug.Log($"[Storage] 已保存: {fileName} ({jsonData.Length} bytes)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Storage] 保存失败 {fileName}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件读取JSON
        /// </summary>
        public static string LoadJson(string fileName)
        {
            EnsureInit();
            try
            {
                var filePath = Path.Combine(_saveDir, $"{fileName}.json");
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[Storage] 文件不存在: {fileName}");
                    return null;
                }
                return File.ReadAllText(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Storage] 读取失败 {fileName}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存序列化对象（自动转JSON）
        /// </summary>
        public static bool SaveObject<T>(string fileName, T obj)
        {
            var json = JsonUtility.ToJson(obj, true);
            return SaveJson(fileName, json);
        }

        /// <summary>
        /// 加载序列化对象
        /// </summary>
        public static T LoadObject<T>(string fileName)
        {
            var json = LoadJson(fileName);
            if (string.IsNullOrEmpty(json)) return default;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Storage] 反序列化失败 {fileName}: {e.Message}");
                return default;
            }
        }

        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        public static bool SaveExists(string fileName)
        {
            EnsureInit();
            var filePath = Path.Combine(_saveDir, $"{fileName}.json");
            return File.Exists(filePath);
        }

        /// <summary>
        /// 删除存档
        /// </summary>
        public static bool DeleteSave(string fileName)
        {
            EnsureInit();
            try
            {
                var filePath = Path.Combine(_saveDir, $"{fileName}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[Storage] 已删除: {fileName}");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Storage] 删除失败 {fileName}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取存档目录路径
        /// </summary>
        public static string GetSaveDirectory()
        {
            EnsureInit();
            return _saveDir;
        }
    }

    // ============================================================
    // 分享模块
    // ============================================================
    public static class Share
    {
        /// <summary>
        /// 分享类型
        /// </summary>
        public enum ShareType
        {
            Text,           // 纯文字
            Image,          // 图片
            Screenshot,     // 截图
            Link            // 链接
        }

        /// <summary>
        /// 分享结果
        /// </summary>
        public struct ShareResult
        {
            public bool Success;
            public string Message;
        }

        // ---- 分享接口 ----

        /// <summary>
        /// 分享文字内容
        /// </summary>
        public static ShareResult ShareText(string title, string content)
        {
            Debug.Log($"[Share] 分享文字: {title}");

#if UNITY_ANDROID || UNITY_IOS
            // 原生分享（移动端）
            return ShareNative(title, content, null);
#else
            // PC端：复制到剪贴板
            GUIUtility.systemCopyBuffer = $"{title}\n{content}";
            Debug.Log("[Share] 已复制到剪贴板（PC模式）");
            return new ShareResult { Success = true, Message = "已复制到剪贴板" };
#endif
        }

        /// <summary>
        /// 分享截图
        /// </summary>
        public static ShareResult ShareScreenshot(string title)
        {
            Debug.Log($"[Share] 分享截图: {title}");

            StartCoroutine(ShareScreenshotCoroutine(title));
            return new ShareResult { Success = true, Message = "截图分享中..." };
        }

        /// <summary>
        /// 分享游戏链接（邀请好友等）
        /// </summary>
        public static ShareResult ShareLink(string title, string url, string description = "")
        {
            Debug.Log($"[Share] 分享链接: {title} -> {url}");

            var content = $"{title}\n{description}\n{url}";
            GUIUtility.systemCopyBuffer = content;
            return new ShareResult { Success = true, Message = "链接已复制" };
        }

        /// <summary>
        /// 生成分享用的战绩图片数据
        /// </summary>
        public static string GenerateBattleResultText(int floor, bool victory, string diceCombo, int score)
        {
            var result = victory ? "通关胜利！" : "挑战失败";
            return $@"🎲 骰子传说 — {result}
📍 第{floor}层 | 组合: {diceCombo}
⭐ 得分: {score}
#骰子传说 #Roguelike";
        }

        // ---- 内部实现 ----

        private static System.Collections.IEnumerator ShareScreenshotCoroutine(string title)
        {
            yield return new WaitForEndOfFrame();

            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            var filePath = Path.Combine(Application.temporaryCachePath, "share_screenshot.png");
            File.WriteAllBytes(filePath, bytes);

            UnityEngine.Object.Destroy(texture);

#if UNITY_ANDROID || UNITY_IOS
            ShareNative(title, "看看我的战绩！", filePath);
#else
            Debug.Log($"[Share] 截图已保存: {filePath}");
#endif
        }

        private static void StartCoroutine(System.Collections.IEnumerator coroutine)
        {
            // 使用一个隐藏的MonoBehaviour来启动协程
            if (_shareHelper == null)
            {
                var go = new GameObject("[ShareHelper]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _shareHelper = go.AddComponent<ShareCoroutineHelper>();
            }
            _shareHelper.StartCoroutine(coroutine);
        }

        private static ShareCoroutineHelper _shareHelper;

        private static ShareResult ShareNative(string title, string content, string imagePath)
        {
            // 预留给原生SDK实现的钩子
            // 微信小游戏 / 抖音小游戏等平台会通过 partial class 覆盖此方法
            Debug.Log($"[Share] Native分享: title={title}, hasImage={imagePath != null}");
            return new ShareResult { Success = true, Message = "分享成功" };
        }
    }

    /// <summary>
    /// 协程辅助类（用于Share模块的截图协程）
    /// </summary>
    private class ShareCoroutineHelper : MonoBehaviour { }
}
