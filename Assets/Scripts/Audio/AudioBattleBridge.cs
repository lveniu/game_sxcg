using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗音效桥接层 — 订阅BattleManager/DiceRoller事件，自动触发对应音效
/// 挂载方式：由BattleManager初始化时创建，随战斗生命周期
/// </summary>
public class AudioBattleBridge : MonoBehaviour
{
    // ============================================================
    // 音效间隔控制 — 同类型音效最小间隔，防止叠加噪音
    // ============================================================

    private Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
    private const float DEFAULT_MIN_INTERVAL = 0.1f;

    // ============================================================
    // 生命周期
    // ============================================================

    private BattleManager battleMgr;
    private DiceRoller diceRoller;

    /// <summary>
    /// 初始化 — 在BattleManager.StartBattle时调用
    /// </summary>
    public void Initialize(BattleManager bm, DiceRoller roller = null)
    {
        battleMgr = bm;
        diceRoller = roller;

        SubscribeEvents();
        Debug.Log("[AudioBattleBridge] 初始化完成，已订阅战斗事件");
    }

    /// <summary>
    /// 清理 — 在BattleManager.EndBattle时调用
    /// </summary>
    public void Cleanup()
    {
        UnsubscribeEvents();

        // 战斗结束停止所有战斗SFX
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopAllSFX();

        battleMgr = null;
        diceRoller = null;
        Debug.Log("[AudioBattleBridge] 已清理");
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ============================================================
    // 事件订阅
    // ============================================================

    private void SubscribeEvents()
    {
        if (battleMgr != null)
        {
            battleMgr.OnBattleStarted += OnBattleStarted;
            battleMgr.OnBattleEnded += OnBattleEnded;
            battleMgr.OnDamageDealt += OnDamageDealt;
            battleMgr.OnHealDone += OnHealDone;
            battleMgr.OnShieldGained += OnShieldGained;
            battleMgr.OnUnitKilled += OnUnitKilled;
            battleMgr.OnDiceSkillTriggered += OnDiceSkillTriggered;
        }

        if (diceRoller != null)
        {
            diceRoller.OnDiceRolled += OnDiceRolled;
            diceRoller.OnRerollUsed += OnRerollUsed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (battleMgr != null)
        {
            battleMgr.OnBattleStarted -= OnBattleStarted;
            battleMgr.OnBattleEnded -= OnBattleEnded;
            battleMgr.OnDamageDealt -= OnDamageDealt;
            battleMgr.OnHealDone -= OnHealDone;
            battleMgr.OnShieldGained -= OnShieldGained;
            battleMgr.OnUnitKilled -= OnUnitKilled;
            battleMgr.OnDiceSkillTriggered -= OnDiceSkillTriggered;
        }

        if (diceRoller != null)
        {
            diceRoller.OnDiceRolled -= OnDiceRolled;
            diceRoller.OnRerollUsed -= OnRerollUsed;
        }
    }

    // ============================================================
    // 战斗事件 → 音效映射
    // ============================================================

    /// <summary> 战斗开始：播放战前号角 + 切换战斗BGM </summary>
    private void OnBattleStarted()
    {
        PlayThrottled("sfx_battle_start", 0.5f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.CrossFadeBGM("battle_bgm", 1.0f);
    }

    /// <summary> 战斗结束：胜利/失败不同音效 + 恢复普通BGM </summary>
    private void OnBattleEnded(bool playerWon)
    {
        if (playerWon)
        {
            PlayThrottled("sfx_victory", 1.0f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.CrossFadeBGM("victory_bgm", 0.8f);
        }
        else
        {
            PlayThrottled("sfx_defeat", 1.0f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.CrossFadeBGM("defeat_bgm", 0.8f);
        }
    }

    /// <summary> 伤害触发：区分暴击/普攻，暴击带随机pitch + 震屏联动 </summary>
    private void OnDamageDealt(Hero attacker, Hero target, int damage)
    {
        if (attacker == null || target == null) return;

        // 暴击判断：伤害超过攻击者基础攻击力视为暴击
        bool isCrit = damage > attacker.BattleAttack;

        if (isCrit)
        {
            PlayThrottled("sfx_crit", DEFAULT_MIN_INTERVAL);
            // 暴击带随机pitch变化
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXRandomPitch("sfx_crit_impact", 0.85f, 1.15f);
            // 联动震屏
            BattleEffectManager.Instance?.CameraShake(0.3f, 0.4f);
        }
        else
        {
            // 普攻随机pitch增加打击感变化
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXRandomPitch("sfx_hit", 0.9f, 1.1f);
            else
                PlayThrottled("sfx_hit", DEFAULT_MIN_INTERVAL);
        }
    }

    /// <summary> 治疗触发 </summary>
    private void OnHealDone(Hero healer, Hero target, int healAmount)
    {
        PlayThrottled("sfx_heal", DEFAULT_MIN_INTERVAL);
    }

    /// <summary> 护盾获得 </summary>
    private void OnShieldGained(Hero hero, int shieldAmount)
    {
        PlayThrottled("sfx_shield", DEFAULT_MIN_INTERVAL);
    }

    /// <summary> 单位死亡 </summary>
    private void OnUnitKilled(Hero killer, Hero victim)
    {
        PlayThrottled("sfx_death", 0.3f);
    }

    /// <summary> 骰子技能触发 — 按稀有度区分音效 </summary>
    private void OnDiceSkillTriggered(string skillDesc)
    {
        // 根据描述关键词判断骰子组合类型，播放不同稀有度音效
        string clipId = "sfx_combo_pair"; // 默认对子

        if (skillDesc.Contains("三条") || skillDesc.Contains("大招"))
            clipId = "sfx_combo_three";
        else if (skillDesc.Contains("顺子"))
            clipId = "sfx_combo_straight";
        else if (skillDesc.Contains("对子"))
            clipId = "sfx_combo_pair";

        PlayThrottled(clipId, 0.2f);
    }

    // ============================================================
    // 骰子事件 → 音效映射
    // ============================================================

    /// <summary> 掷骰结果 — 3次连续短音模拟骰子落地 </summary>
    private void OnDiceRolled(int[] values)
    {
        StartCoroutine(DiceRollSoundSequence());
    }

    /// <summary>
    /// 骰子落地音效序列 — 3次短促音，间隔递减模拟"哒哒哒"落地感
    /// </summary>
    private IEnumerator DiceRollSoundSequence()
    {
        for (int i = 0; i < 3; i++)
        {
            PlayThrottled("sfx_dice_roll", 0.05f);
            // 递减延迟：0.12s → 0.08s → 0.05s
            yield return new WaitForSecondsRealtime(0.12f - i * 0.035f);
        }
    }

    /// <summary> 重摇使用 — 锁定音效 </summary>
    private void OnRerollUsed(int usedCount)
    {
        PlayThrottled("sfx_dice_lock", 0.1f);
    }

    // ============================================================
    // 公开方法 — 供UI面板直接调用（如DiceRollPanel点击锁定Toggle）
    // ============================================================

    /// <summary> 骰子锁定/解锁音效 — 由DiceRollPanel的Toggle回调触发 </summary>
    public void PlayDiceLockSound()
    {
        PlayThrottled("sfx_dice_lock", 0.05f);
    }

    /// <summary> 骰子开始旋转音效 — 由DiceRollPanel掷骰按钮触发 </summary>
    public void PlayDiceSpinSound()
    {
        PlayThrottled("sfx_dice_spin", 0.15f);
    }

    // ============================================================
    // 节流工具
    // ============================================================

    /// <summary>
    /// 节流播放 — 同clipId在minInterval秒内不重复播放，防叠加噪音
    /// </summary>
    private void PlayThrottled(string clipId, float minInterval)
    {
        float now = Time.unscaledTime;
        if (lastPlayTime.TryGetValue(clipId, out float lastTime))
        {
            if (now - lastTime < minInterval) return;
        }
        lastPlayTime[clipId] = now;

        if (AudioManager.Instance != null)
            AudioManager.PlaySFX(clipId);
    }
}
