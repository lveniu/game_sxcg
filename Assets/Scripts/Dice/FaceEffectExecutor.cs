using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 骰子面效果执行器 — 处理骰子投掷后的面效果触发
/// 文件位置: Assets/Scripts/Dice/FaceEffectExecutor.cs
///
/// 设计原则:
/// 1. 面效果配置从 face_effects.json 读取
/// 2. 支持按投掷值触发和按面效果ID触发两种模式
/// 3. 通过 BalanceProvider 获取配置，保留硬编码 fallback
/// 4. 与 BattleManager、RoguelikeGameManager 通过事件通信
/// </summary>
public class FaceEffectExecutor
{
    // ========== 单例 ==========
    public static FaceEffectExecutor Instance { get; private set; }

    // ========== 事件 ==========
    public event System.Action<Hero, string, string> OnFaceEffectApplied;
    // (hero, effectId, description)

    // ========== 状态 ==========
    private List<FaceEffectEntry> _effects;
    private List<Hero> _playerHeroes;
    private List<Hero> _enemyHeroes;

    /// <summary>
    /// 创建执行器（BattleManager.Awake 中创建单例）
    /// </summary>
    public FaceEffectExecutor()
    {
        Instance = this;
        _effects = BalanceProvider.GetFaceEffects();
    }

    /// <summary>
    /// 设置战斗单位引用（由 RoguelikeGameManager 在战斗开始时调用）
    /// </summary>
    public void SetBattleUnits(List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        _playerHeroes = playerHeroes;
        _enemyHeroes = enemyHeroes;
    }

    /// <summary>
    /// 处理骰子投掷结果 — 检查所有骰子值并触发对应面效果
    /// 由 DiceRoller.RollAll() 或 RoguelikeGameManager 在投掷完成后调用
    /// </summary>
    public void ProcessRollResults(int[] diceValues, Dice[] dice)
    {
        if (_effects == null || _effects.Count == 0) return;
        if (diceValues == null) return;

        for (int i = 0; i < diceValues.Length; i++)
        {
            int value = diceValues[i];

            // 检查投掷值触发
            var valueEffect = FindEffectByValue(value);
            if (valueEffect != null)
            {
                ApplyValueTriggeredEffect(valueEffect, i);
            }

            // 检查面效果触发（特殊面）
            if (dice != null && i < dice.Length && dice[i]?.FaceEffects != null)
            {
                string faceEffect = dice[i].FaceEffects[diceValues[i] - 1];
                if (!string.IsNullOrEmpty(faceEffect))
                {
                    var faceEffectData = FindEffectById(faceEffect);
                    if (faceEffectData != null)
                    {
                        ApplyFaceEffect(faceEffectData);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理单个面效果（供外部直接调用，如骰子升级后面板）
    /// </summary>
    public void ApplyFaceEffect(string effectId)
    {
        if (_effects == null) return;
        var effect = FindEffectById(effectId);
        if (effect != null)
            ApplyFaceEffect(effect);
    }

    /// <summary>
    /// 战斗开始时激活面效果 — 由 BattleManager.StartBattle 调用
    /// </summary>
    public void ActivateBattleStartEffects(Dice[] dices, int[] lastValues,
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        SetBattleUnits(playerHeroes, enemyHeroes);
        ProcessRollResults(lastValues, dices);
    }

    /// <summary>
    /// 每回合处理持续效果 — 由 BattleManager.BattleLoop 调用
    /// </summary>
    public void ProcessPerTurnEffects(List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        // 当前MVP：回合效果由 ProcessRollResults 一次性处理
        // 后续可扩展为回合持续的 DOT/HOT 效果
    }

    /// <summary>
    /// 攻击时触发面效果 — 由 AutoChessAI.NormalAttack 调用
    /// </summary>
    public void ProcessOnAttackEffects(Hero attacker, Hero target,
        List<Hero> allies, List<Hero> enemies)
    {
        if (_effects == null) return;

        // 破甲效果
        if (attacker.HasArmorBreak && target != null)
        {
            target.BattleDefense = Mathf.RoundToInt(target.BattleDefense * 0.5f);
            Debug.Log($"[FaceEffect] {attacker.Data.heroName} 破甲！{target.Data.heroName}防御减半");
        }

        // 闪电链
        if (attacker.LightningChainBounces > 0 && enemies != null)
        {
            var aliveEnemies = enemies.FindAll(e => e != null && !e.IsDead && e != target);
            float dmgMult = 0.7f;
            for (int i = 0; i < attacker.LightningChainBounces && aliveEnemies.Count > 0; i++)
            {
                var chainTarget = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
                int chainDmg = Mathf.RoundToInt(attacker.BattleAttack * dmgMult);
                chainTarget.TakeDamage(Mathf.Max(1, chainDmg));
                Debug.Log($"[FaceEffect] 闪电链弹射: {chainTarget.Data.heroName} 受到 {chainDmg} 伤害");
                aliveEnemies.Remove(chainTarget);
                dmgMult *= 0.7f;
            }
            attacker.LightningChainBounces = 0; // 消耗掉
        }

        // 机制怪：诅咒附加
        if (MechanicEnemySystem.Instance != null && attacker != null)
        {
            MechanicEnemySystem.Instance.OnBossAttackApplyCurse(attacker, target);
        }
    }

    /// <summary>
    /// 清理战斗效果 — 由 BattleManager.EndBattle 调用
    /// </summary>
    public void ClearBattleEffects()
    {
        _playerHeroes = null;
        _enemyHeroes = null;
    }

    // ========== 效果查找 ==========

    private FaceEffectEntry FindEffectByValue(int value)
    {
        if (_effects == null) return null;
        return _effects.Find(e =>
            e.trigger == "on_roll_value" &&
            e.trigger_params != null &&
            GetTriggerParamInt(e, "value") == value);
    }

    private FaceEffectEntry FindEffectById(string id)
    {
        if (_effects == null || string.IsNullOrEmpty(id)) return null;
        return _effects.Find(e => e.id == id);
    }

    // ========== 效果应用 ==========

    private void ApplyValueTriggeredEffect(FaceEffectEntry effect, int diceIndex)
    {
        if (_playerHeroes == null || _playerHeroes.Count == 0) return;

        switch (effect.effect_type)
        {
            case "Buff":
                ApplyBuffEffect(effect);
                break;
            case "Shield":
                ApplyShieldEffect(effect);
                break;
            case "Heal":
                ApplyHealEffect(effect);
                break;
            case "Economy":
                ApplyEconomyEffect(effect);
                break;
            default:
                Debug.Log($"[FaceEffect] 未处理的值触发类型: {effect.effect_type}");
                break;
        }

        NotifyEffectApplied(effect);
    }

    private void ApplyFaceEffect(FaceEffectEntry effect)
    {
        if (_playerHeroes == null) return;

        switch (effect.effect_type)
        {
            case "Buff":
                ApplyBuffEffect(effect);
                break;
            case "Debuff":
                ApplyDebuffEffect(effect);
                break;
            case "Shield":
                ApplyShieldEffect(effect);
                break;
            case "Heal":
                ApplyHealEffect(effect);
                break;
            case "CC":
                ApplyCCEffect(effect);
                break;
            case "ChainAttack":
                ApplyChainAttackEffect(effect);
                break;
            case "AOE":
                ApplyAOEEffect(effect);
                break;
            case "Cleanse":
                ApplyCleanseEffect();
                break;
            case "Economy":
                ApplyEconomyEffect(effect);
                break;
            default:
                Debug.Log($"[FaceEffect] 未处理的面效果类型: {effect.effect_type}");
                break;
        }

        NotifyEffectApplied(effect);
    }

    // --- Buff ---
    private void ApplyBuffEffect(FaceEffectEntry effect)
    {
        if (effect.effect_params == null) return;

        string stat = GetParamString(effect.effect_params, "stat", "BattleAttack");
        string op = GetParamString(effect.effect_params, "operation", "multiply");
        float val = GetParamFloat(effect.effect_params, "value", 1f);

        foreach (var hero in _playerHeroes)
        {
            if (hero == null || hero.IsDead) continue;
            ApplyStatChange(hero, stat, op, val);
        }
    }

    // --- Debuff（对敌人） ---
    private void ApplyDebuffEffect(FaceEffectEntry effect)
    {
        if (effect.effect_params == null || _enemyHeroes == null) return;

        string target = GetParamString(effect.effect_params, "target", "current_enemy");
        string stat = GetParamString(effect.effect_params, "stat", "BattleDefense");
        string op = GetParamString(effect.effect_params, "operation", "multiply");
        float val = GetParamFloat(effect.effect_params, "value", 0.5f);

        // 对当前目标或所有敌人
        if (target == "all_enemies")
        {
            foreach (var enemy in _enemyHeroes)
            {
                if (enemy == null || enemy.IsDead) continue;
                ApplyStatChange(enemy, stat, op, val);
            }
        }
        else
        {
            // 对第一个活着的敌人
            var first = _enemyHeroes.FirstOrDefault(e => e != null && !e.IsDead);
            if (first != null)
                ApplyStatChange(first, stat, op, val);
        }
    }

    // --- Shield ---
    private void ApplyShieldEffect(FaceEffectEntry effect)
    {
        if (effect.effect_params == null) return;

        float shieldPct = GetParamFloat(effect.effect_params, "shield_pct", 0.1f);
        string target = GetParamString(effect.effect_params, "target", "all_allies");

        List<Hero> targets = (target == "all_allies") ? _playerHeroes : new List<Hero>();
        if (target == "self" && _playerHeroes != null && _playerHeroes.Count > 0)
            targets = new List<Hero> { _playerHeroes[0] };

        foreach (var hero in targets)
        {
            if (hero == null || hero.IsDead) continue;
            int shield = Mathf.RoundToInt(hero.MaxHealth * shieldPct);
            hero.AddShield(shield);
            Debug.Log($"[FaceEffect] {hero.Data.heroName} 获得护盾 {shield}");
        }
    }

    // --- Heal ---
    private void ApplyHealEffect(FaceEffectEntry effect)
    {
        if (effect.effect_params == null) return;

        float healPct = GetParamFloat(effect.effect_params, "heal_pct", 0.1f);
        string target = GetParamString(effect.effect_params, "target", "self");

        List<Hero> targets;
        if (target == "all_allies")
            targets = _playerHeroes;
        else if (target == "self" && _playerHeroes != null && _playerHeroes.Count > 0)
            targets = new List<Hero> { _playerHeroes[0] };
        else
            targets = new List<Hero>();

        foreach (var hero in targets)
        {
            if (hero == null || hero.IsDead) continue;
            int heal = Mathf.RoundToInt(hero.MaxHealth * healPct);
            hero.Heal(heal);
            Debug.Log($"[FaceEffect] {hero.Data.heroName} 治疗 {heal}");
        }
    }

    // --- CC（眩晕） ---
    private void ApplyCCEffect(FaceEffectEntry effect)
    {
        if (_enemyHeroes == null) return;

        int duration = GetParamInt(effect.effect_params, "duration_rounds", 1);
        string target = GetParamString(effect.effect_params, "target", "current_enemy");

        if (target == "all_enemies")
        {
            foreach (var enemy in _enemyHeroes)
            {
                if (enemy == null || enemy.IsDead) continue;
                enemy.SetStunned(true);
                Debug.Log($"[FaceEffect] {enemy.Data.heroName} 被眩晕 {duration} 回合");
            }
        }
        else
        {
            var first = _enemyHeroes.FirstOrDefault(e => e != null && !e.IsDead);
            if (first != null)
            {
                first.SetStunned(true);
                Debug.Log($"[FaceEffect] {first.Data.heroName} 被眩晕 {duration} 回合");
            }
        }
    }

    // --- ChainAttack ---
    private void ApplyChainAttackEffect(FaceEffectEntry effect)
    {
        if (_playerHeroes == null || _playerHeroes == null || _enemyHeroes == null) return;

        int bounces = GetParamInt(effect.effect_params, "bounces", 2);
        float decayPct = GetParamFloat(effect.effect_params, "damage_decay_pct", 0.3f);

        // 随机选一个我方英雄作为攻击源
        var attacker = _playerHeroes.FirstOrDefault(h => h != null && !h.IsDead);
        if (attacker == null) return;

        var aliveEnemies = _enemyHeroes.Where(e => e != null && !e.IsDead).ToList();
        if (aliveEnemies.Count == 0) return;

        float currentDmgMult = 1f;
        Hero lastTarget = null;

        for (int i = 0; i <= bounces && aliveEnemies.Count > 0; i++)
        {
            var target = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
            int dmg = Mathf.RoundToInt(attacker.BattleAttack * currentDmgMult);
            target.TakeDamage(Mathf.Max(1, dmg), attacker);
            Debug.Log($"[FaceEffect] 闪电链弹射{i}: {target.Data.heroName} 受到 {dmg} 伤害");

            lastTarget = target;
            currentDmgMult *= (1f - decayPct);
            aliveEnemies.Remove(target);
        }

        // 设置攻击者的闪电链属性（供AutoChessAI检查）
        attacker.LightningChainBounces = bounces;
    }

    // --- AOE ---
    private void ApplyAOEEffect(FaceEffectEntry effect)
    {
        if (_playerHeroes == null || _enemyHeroes == null) return;

        float dmgPct = GetParamFloat(effect.effect_params, "damage_pct", 0.2f);

        var attacker = _playerHeroes.FirstOrDefault(h => h != null && !h.IsDead);
        if (attacker == null) return;

        foreach (var enemy in _enemyHeroes)
        {
            if (enemy == null || enemy.IsDead) continue;
            int dmg = Mathf.RoundToInt(attacker.BattleAttack * dmgPct);
            enemy.TakeDamage(Mathf.Max(1, dmg));
        }

        Debug.Log($"[FaceEffect] AOE伤害！全体敌人受到{dmgPct * 100}%攻击力伤害");
    }

    // --- Cleanse ---
    private void ApplyCleanseEffect()
    {
        if (_playerHeroes == null) return;

        foreach (var hero in _playerHeroes)
        {
            if (hero == null || hero.IsDead) continue;

            // 移除眩晕
            hero.SetStunned(false);

            // 移除诅咒（通过MechanicEnemySystem）
            MechanicEnemySystem.Instance?.RemoveCurseFromHero(hero);
        }

        Debug.Log("[FaceEffect] 净化！全体友方解除诅咒和眩晕");
    }

    // --- Economy ---
    private void ApplyEconomyEffect(FaceEffectEntry effect)
    {
        if (effect.effect_params == null) return;

        int goldBonus = GetParamInt(effect.effect_params, "gold_bonus", 0);
        float interestBonus = GetParamFloat(effect.effect_params, "interest_bonus_pct", 0f);
        int rerollRefund = GetParamInt(effect.effect_params, "reroll_refund", 0);

        if (goldBonus > 0 || interestBonus > 0)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                // 计算实际金币：基础奖励 + 利息（按当前金币百分比）
                int totalGold = goldBonus;
                if (interestBonus > 0)
                    totalGold += Mathf.RoundToInt((inventory.Gold ?? 0) * interestBonus);
                inventory.AddGold(totalGold);
                Debug.Log($"[FaceEffect] 经济效果：金币+{totalGold}（基础{goldBonus}，利息{interestBonus * 100}%）");
            }
            else
            {
                Debug.LogWarning("[FaceEffect] PlayerInventory不存在，经济效果未生效");
            }
        }

        if (rerollRefund > 0)
        {
            var roller = RoguelikeGameManager.Instance?.DiceRoller;
            if (roller != null)
            {
                roller.AddFreeRerolls(rerollRefund);
                Debug.Log($"[FaceEffect] 重摇返还 +{rerollRefund}");
            }
        }
    }

    // ========== 工具方法 ==========

    private void ApplyStatChange(Hero hero, string stat, string operation, float value)
    {
        switch (stat)
        {
            case "BattleAttack":
                if (operation == "multiply")
                    hero.BattleAttack = Mathf.RoundToInt(hero.BattleAttack * value);
                else if (operation == "add")
                    hero.BattleAttack += Mathf.RoundToInt(value);
                break;
            case "BattleDefense":
                if (operation == "multiply")
                    hero.BattleDefense = Mathf.RoundToInt(hero.BattleDefense * value);
                else if (operation == "add")
                    hero.BattleDefense += Mathf.RoundToInt(value);
                break;
            case "BattleCritRate":
                if (operation == "add")
                    hero.BattleCritRate = Mathf.Clamp01(hero.BattleCritRate + value);
                break;
            case "BattleAttackSpeed":
                if (operation == "add")
                    hero.BattleAttackSpeed += value;
                else if (operation == "multiply")
                    hero.BattleAttackSpeed *= value;
                break;
        }
    }

    private void NotifyEffectApplied(FaceEffectEntry effect)
    {
        string desc = effect.description_cn ?? effect.id;
        foreach (var hero in _playerHeroes ?? Enumerable.Empty<Hero>())
        {
            if (hero != null && !hero.IsDead)
            {
                OnFaceEffectApplied?.Invoke(hero, effect.id, desc);
                break; // 只通知一次
            }
        }
        Debug.Log($"[FaceEffect] 触发: {effect.name_cn} — {desc}");
    }

    // --- 参数读取 ---

    private int GetTriggerParamInt(FaceEffectEntry effect, string key)
    {
        if (effect.trigger_params == null) return 0;
        return GetParamInt(effect.trigger_params, key, 0);
    }

    private static int GetParamInt(Dictionary<string, object> dict, string key, int fallback)
    {
        if (dict == null) return fallback;
        if (dict.TryGetValue(key, out object val))
        {
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is double d) return (int)d;
            if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
        }
        return fallback;
    }

    private static float GetParamFloat(Dictionary<string, object> dict, string key, float fallback)
    {
        if (dict == null) return fallback;
        if (dict.TryGetValue(key, out object val))
        {
            if (val is float f) return f;
            if (val is double d) return (float)d;
            if (val is int i) return (float)i;
            if (float.TryParse(val?.ToString(), out float parsed)) return parsed;
        }
        return fallback;
    }

    private static string GetParamString(Dictionary<string, object> dict, string key, string fallback)
    {
        if (dict == null) return fallback;
        if (dict.TryGetValue(key, out object val) && val != null)
            return val.ToString();
        return fallback;
    }
}
