using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器 — 单例，统一管理BGM/SFX
/// 资源路径：Resources/Audio/
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ---- 音源 ----
    private AudioSource bgmSource;
    private AudioSource bgmFadeSource; // 淡入淡出用交叉源
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private const int SFX_POOL_SIZE = 8;

    // ---- 音量 ----
    private float masterVol = 1f;
    private float bgmVol = 0.7f;
    private float sfxVol = 1f;
    private bool isMuted = false;

    // ---- 缓存 ----
    private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
    private string currentBGM = "";

    // ---- 淡入淡出 ----
    private Coroutine fadeCoroutine;

    // ============================================================
    // 生命周期
    // ============================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建BGM源
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        bgmFadeSource = gameObject.AddComponent<AudioSource>();
        bgmFadeSource.loop = true;
        bgmFadeSource.playOnAwake = false;

        // 创建SFX对象池
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = false;
            src.playOnAwake = false;
            sfxPool.Add(src);
        }

        // 加载保存的音量
        masterVol = PlayerPrefs.GetFloat("Audio_Master", 1f);
        bgmVol = PlayerPrefs.GetFloat("Audio_BGM", 0.7f);
        sfxVol = PlayerPrefs.GetFloat("Audio_SFX", 1f);

        ApplyVolumes();
    }

    void Start()
    {
        // 监听游戏状态变化自动切换BGM
        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged += OnGameStateChanged;
    }

    void OnDestroy()
    {
        if (GameStateMachine.Instance != null)
            GameStateMachine.Instance.OnStateChanged -= OnGameStateChanged;
    }

    // ============================================================
    // BGM
    // ============================================================

    /// <summary>
    /// 播放BGM，支持淡入淡出切换
    /// </summary>
    public void PlayBGM(string clipName, float fadeDuration = 0.8f)
    {
        if (currentBGM == clipName && bgmSource.isPlaying) return;

        var clip = LoadClip(clipName);
        if (clip == null) return;

        currentBGM = clipName;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CrossFadeBGM(clip, fadeDuration));
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        if (!bgmSource.isPlaying && !bgmFadeSource.isPlaying) return;
        currentBGM = "";
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutBGM(fadeDuration));
    }

    IEnumerator CrossFadeBGM(AudioClip newClip, float duration)
    {
        // 把当前BGM挪到fade源淡出
        if (bgmSource.isPlaying)
        {
            bgmFadeSource.clip = bgmSource.clip;
            bgmFadeSource.time = bgmSource.time;
            bgmFadeSource.volume = bgmSource.volume;
            bgmFadeSource.Play();
            bgmSource.Stop();
        }

        // 新BGM淡入
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        float timer = 0f;
        float targetVol = isMuted ? 0f : bgmVol * masterVol;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / duration;
            bgmSource.volume = Mathf.Lerp(0f, targetVol, t);
            bgmFadeSource.volume = Mathf.Lerp(bgmFadeSource.volume, 0f, t);
            yield return null;
        }

        bgmSource.volume = targetVol;
        bgmFadeSource.Stop();
        fadeCoroutine = null;
    }

    IEnumerator FadeOutBGM(float duration)
    {
        float startVol = bgmSource.volume;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / duration);
            yield return null;
        }
        bgmSource.Stop();
        bgmSource.volume = 0f;
        fadeCoroutine = null;
    }

    // ============================================================
    // SFX（对象池）
    // ============================================================

    /// <summary>
    /// 播放音效，从对象池取空闲AudioSource
    /// </summary>
    public void PlaySFXClip(string clipName, float volumeScale = 1f)
    {
        var clip = LoadClip(clipName);
        if (clip == null) return;

        var src = GetAvailableSFXSource();
        if (src == null) return;

        float vol = isMuted ? 0f : volumeScale * sfxVol * masterVol;
        src.PlayOneShot(clip, vol);
    }

    AudioSource GetAvailableSFXSource()
    {
        // 优先找空闲的
        for (int i = 0; i < sfxPool.Count; i++)
        {
            if (!sfxPool[i].isPlaying)
                return sfxPool[i];
        }
        // 全忙时取第一个（会被截断，但保证能播放）
        return sfxPool[0];
    }

    // ============================================================
    // 音量控制（三通道，存PlayerPrefs）
    // ============================================================

    public float MasterVolume
    {
        get => masterVol;
        set { masterVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio_Master", masterVol); ApplyVolumes(); }
    }

    public float BGMVolume
    {
        get => bgmVol;
        set { bgmVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio_BGM", bgmVol); ApplyVolumes(); }
    }

    public float SFXVolume
    {
        get => sfxVol;
        set { sfxVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio_SFX", sfxVol); }
    }

    void ApplyVolumes()
    {
        if (bgmSource != null)
            bgmSource.volume = isMuted ? 0f : bgmVol * masterVol;
    }

    // ============================================================
    // 静音
    // ============================================================

    public void MuteAll()
    {
        isMuted = true;
        ApplyVolumes();
        // 已在播放的SFX不受影响（OneShot无法中途改音量），新播放的为0
    }

    public void UnmuteAll()
    {
        isMuted = false;
        ApplyVolumes();
    }

    public bool IsMuted => isMuted;

    public void ToggleMute()
    {
        if (isMuted) UnmuteAll(); else MuteAll();
    }

    // ============================================================
    // 场景化BGM：根据GameState自动切换
    // ============================================================

    void OnGameStateChanged(GameState oldState, GameState newState)
    {
        string bgm = GetBGMForState(newState);
        if (!string.IsNullOrEmpty(bgm))
            PlayBGM(bgm);
    }

    static string GetBGMForState(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:     return "menu_bgm";
            case GameState.Battle:       return "battle_bgm";
            case GameState.Settlement:
            case GameState.MapSelect:    return "settlement_bgm";
            default:                     return "calm_bgm";
        }
    }

    // ============================================================
    // 资源加载（Resources/Audio/）
    // ============================================================

    AudioClip LoadClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;

        if (clipCache.TryGetValue(clipName, out var cached))
            return cached;

        var clip = Resources.Load<AudioClip>($"Audio/{clipName}");
        if (clip != null)
        {
            clipCache[clipName] = clip;
            return clip;
        }

        Debug.LogWarning($"[AudioManager] 音频资源未找到: Audio/{clipName}");
        // 缓存null避免反复加载
        clipCache[clipName] = null;
        return null;
    }

    // ============================================================
    // 静态快捷方法 — 供其他系统直接调用
    // ============================================================

    /// <summary> 播放音效，如 AudioManager.PlaySFX("hit") </summary>
    public static void PlaySFX(string clipName, float volumeScale = 1f)
    {
        if (Instance != null)
            Instance.PlaySFXClip(clipName, volumeScale);
    }

    // ---- 战斗音效快捷 ----

    public static void PlayHit()    => PlaySFX("sfx_hit");
    public static void PlayCrit()   => PlaySFX("sfx_crit");
    public static void PlayDeath()  => PlaySFX("sfx_death");
    public static void PlayVictory()=> PlaySFX("sfx_victory");
    public static void PlayDefeat() => PlaySFX("sfx_defeat");

    // ---- 骰子音效快捷 ----

    public static void PlayDiceRoll() => PlaySFX("sfx_dice_roll");
}
