using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// =====================================================================
// 枚举定义 — 骰子面效果相关
// =====================================================================

/// <summary>
/// 骰子面效果类型
/// </summary>
public enum FaceEffectType
{
    None = 0,
    Heal = 1,           // 治疗 — 治疗血量最低的友方X%生命
    Shield = 2,         // 护盾 — 全体获得X点护盾
    ExtraDamage = 3,    // 额外伤害 — 对攻击最高敌人造成X%额外伤害
    AttackSpeed = 4,    // 攻速加成 — 全体攻速+X%
    Stun = 5,           // 眩晕 — 随机敌人眩晕1回合
    CritBoost = 6,      // 暴击加成 — 本场战斗暴击率+X%
    ArmorBreak = 7,     // 破甲 — 本次攻击忽略敌人X%防御
    LifeSteal = 8,      // 吸血 — 全体获得X%吸血
    Thorns = 9,         // 反伤 — 全体获得X%荆棘反伤
    Cleanse = 10        // 净化 — 移除全体友方的debuff
}

/// <summary>
/// 目标范围
/// </summary>
public enum FaceEffectTarget
{
    Self,
    LowestHealthAlly,
    AllAllies,
    HighestAtkEnemy,
    RandomEnemy,
    AllEnemies,
    AllUnits
}

/// <summary>
/// 触发时机
/// </summary>
public enum FaceEffectTrigger
{
    OnDiceResult,       // 骰子投出该面时立即触发
    OnBattleStart,      // 战斗开始时
    OnAttack,           // 攻击时触发
    OnDamaged,          // 受伤时触发
    OnKill,             // 击杀时触发
    PerTurn             // 每回合触发
}

/// <summary>
/// 骰子面效果定义 — 运行时使用
/// 注意：纯数据模型 FaceEffectDef（序列化用）在 ConfigLoader.cs 中定义。
/// 此处的 FaceEffectRuntimeDef 包含解析后的枚举字段，供执行器使用。
/// </summary>
public class FaceEffectRuntimeDef
{
    public string effectId;
    public FaceEffectType effectType;
    public string effectName;
    public string descriptionTemplate;
    public FaceEffectTarget targetScope;
    public FaceEffectTrigger triggerTiming;
    public float baseValue;
    public float growthPerLevel;
    public int maxLevel = 3;
    public List<int> applicableFaces;
    public string iconRef;
    public string rarity = "common";
}

/// <summary>
/// 升级选项（供UI展示）
/// </summary>
public class FaceUpgradeOption
{
    public int diceIndex;
    public int faceIndex;
    public FaceEffectRuntimeDef effectDef;
    public bool isNew;
    public int currentLevel;

    public string GetDisplayText()
    {
        if (isNew)
            return $"骰子{diceIndex + 1} 面{faceIndex + 1} → {effectDef.effectName}";
        else
            return $"骰子{diceIndex + 1} 面{faceIndex + 1} {effectDef.effectName} Lv{currentLevel}→{currentLevel + 1}";
    }
}

/// <summary>
/// 骰子面效果执行器 — 解析和执行骰子面效果
/// 文件位置: Assets/Scripts/Dice/FaceEffectExecutor.cs
///
/// 设计原则:
/// 1. Dice.FaceEffects 仍为 string[]，存储 effectId
/// 2. 本系统负责 string(effectId) → FaceEffectRuntimeDef → 执行逻辑 的完整链路
/// 3. 效果配置从 face_effects.json 读取（通过 BalanceProvider）
/// 4. 每种 FaceEffectType 对应一个独立的处理函数
/// 5. 支持效果等级（肉鸽奖励升级）
/// </summary>
public class FaceEffectExecutor
{
    // ========== 单例 ==========
    public static FaceEffectExecutor Instance { get; private set; }

    // ========== 事件 ==========
    /// <summary>面效果触发时（用于UI显示）</summary>
    public event System.Action<FaceEffectRuntimeDef, int, string> OnFaceEffectTriggered;
    // 参数: 效果定义, 骰子面值, 描述文本

    /// <summary>面效果升级时</summary>
    public event System.Action<int, FaceEffectRuntimeDef, int> OnFaceEffectUpgraded;
    // 参数: 骰子索引, 效果定义, 新等级

    // ========== 状态 ==========
    private FaceEffectsConfig _config;
    private List<FaceEffectRuntimeDef> _runtimeEffects = new List<FaceEffectRuntimeDef>();

    /// <summary>
    /// 运行时效果等级表
    /// Key: "diceIndex_faceIndex" (如 "0_3" 表示第0个骰子的第3面)
    /// Value: 效果等级 (1-based)
    /// </summary>
    private Dictionary<string, int> _effectLevels = new Dictionary<string, int>();

    /// <summary>
    /// 激活的持续效果（本场战斗有效）
    /// Key: effectId
    /// Value: 效果等级
    /// </summary>
    private Dictionary<string, int> _activeBattleEffects = new Dictionary<string, int>();

    public FaceEffectExecutor()
    {
        Instance = this;
        LoadAndConvertEffects();
    }

    /// <summary>
    /// 从 BalanceProvider 加载配置并转换为运行时定义（含枚举解析）
    /// </summary>
    private void LoadAndConvertEffects()
    {
        _config = BalanceProvider.GetFaceEffectsConfig();
        _runtimeEffects.Clear();

        if (_config?.effects == null) return;

        foreach (var raw in _config.effects)
        {
            var def = new FaceEffectRuntimeDef
            {
                effectId = raw.effectId,
                effectType = ParseEffectType(raw.effectType),
                effectName = raw.effectName,
                descriptionTemplate = raw.descriptionTemplate,
                targetScope = ParseTarget(raw.targetScope),
                triggerTiming = ParseTrigger(raw.triggerTiming),
                baseValue = raw.baseValue,
                growthPerLevel = raw.growthPerLevel,
                maxLevel = raw.maxLevel,
                applicableFaces = raw.applicableFaces,
                iconRef = raw.iconRef,
                rarity = raw.rarity
            };
            _runtimeEffects.Add(def);
        }

        Debug.Log($"[FaceEffectExecutor] 已加载 {_runtimeEffects.Count} 个面效果定义");
    }

    // ========== 骰子投掷后触发 ==========

    /// <summary>
    /// 处理骰子投掷结果 — 检查每个骰子是否有面效果
    /// 由 DiceRoller.OnDiceRolled 事件或 RoguelikeGameManager 在骰子阶段结束后调用
    /// </summary>
    public void ProcessDiceResults(Dice[] dices, int[] values,
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        for (int i = 0; i < dices.Length; i++)
        {
            if (i >= values.Length) break;

            int faceValue = values[i];
            var dice = dices[i];

            int effectIndex = faceValue - 1;
            if (effectIndex < 0 || effectIndex >= dice.FaceEffects.Length) continue;

            string effectId = dice.FaceEffects[effectIndex];
            if (string.IsNullOrEmpty(effectId)) continue;

            var effectDef = FindEffectDef(effectId);
            if (effectDef == null) continue;

            if (effectDef.triggerTiming != FaceEffectTrigger.OnDiceResult) continue;

            string levelKey = $"{i}_{effectIndex}";
            int level = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
            float effectValue = CalculateEffectValue(effectDef, level);

            ExecuteEffect(effectDef, effectValue, playerHeroes, enemyHeroes);

            string desc = FormatDescription(effectDef, effectValue);
            OnFaceEffectTriggered?.Invoke(effectDef, faceValue, desc);

            Debug.Log($"[FaceEffect] 骰子{i} 面{faceValue} → {effectDef.effectName}: {desc}");
        }
    }

    // ========== 战斗开始时激活持续效果 ==========

    /// <summary>
    /// 战斗开始时激活所有 OnBattleStart 触发的面效果
    /// 由 BattleManager.StartBattle() 调用
    /// </summary>
    public void ActivateBattleStartEffects(Dice[] dices, int[] lastValues,
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        _activeBattleEffects.Clear();

        for (int i = 0; i < dices.Length; i++)
        {
            if (i >= lastValues.Length) break;

            var dice = dices[i];
            for (int faceIdx = 0; faceIdx < dice.FaceEffects.Length; faceIdx++)
            {
                string effectId = dice.FaceEffects[faceIdx];
                if (string.IsNullOrEmpty(effectId)) continue;

                var effectDef = FindEffectDef(effectId);
                if (effectDef == null) continue;
                if (effectDef.triggerTiming != FaceEffectTrigger.OnBattleStart) continue;

                string levelKey = $"{i}_{faceIdx}";
                int level = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
                float effectValue = CalculateEffectValue(effectDef, level);

                ExecuteEffect(effectDef, effectValue, playerHeroes, enemyHeroes);
                _activeBattleEffects[effectId] = level;

                string desc = FormatDescription(effectDef, effectValue);
                OnFaceEffectTriggered?.Invoke(effectDef, faceIdx + 1, desc);
            }
        }
    }

    // ========== 回合触发 ==========

    /// <summary>
    /// 每回合检查 PerTurn 类型的效果
    /// 由 BattleManager.BattleLoop() 中每tick调用
    /// </summary>
    public void ProcessPerTurnEffects(List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        foreach (var kvp in _activeBattleEffects)
        {
            var effectDef = FindEffectDef(kvp.Key);
            if (effectDef == null || effectDef.triggerTiming != FaceEffectTrigger.PerTurn) continue;

            float value = CalculateEffectValue(effectDef, kvp.Value);
            ExecuteEffect(effectDef, value, playerHeroes, enemyHeroes);
        }
    }

    // ========== 攻击时触发 ==========

    /// <summary>
    /// 英雄攻击时检查 OnAttack 类型的面效果
    /// 由 AutoChessAI.NormalAttack() 调用
    /// </summary>
    public void ProcessOnAttackEffects(Hero attacker, Hero target,
        List<Hero> playerHeroes, List<Hero> enemyHeroes)
    {
        foreach (var kvp in _activeBattleEffects)
        {
            var effectDef = FindEffectDef(kvp.Key);
            if (effectDef == null || effectDef.triggerTiming != FaceEffectTrigger.OnAttack) continue;

            // playerHeroes 为空时跳过阵营检查（兼容 AutoChessAI 调用）
            if (playerHeroes != null && !playerHeroes.Contains(attacker)) continue;

            float value = CalculateEffectValue(effectDef, kvp.Value);
            ExecuteEffect(effectDef, value, playerHeroes, enemyHeroes, attacker, target);
        }
    }

    // ========== 升级接口 ==========

    /// <summary>
    /// 升级骰子面效果（肉鸽奖励 DiceFaceUpgrade 调用）
    /// </summary>
    public int UpgradeFaceEffect(int diceIndex, int faceIndex, string effectId, Dice targetDice)
    {
        if (targetDice == null) return 0;
        if (faceIndex < 0 || faceIndex >= targetDice.FaceEffects.Length) return 0;

        var effectDef = FindEffectDef(effectId);
        if (effectDef == null) return 0;

        string levelKey = $"{diceIndex}_{faceIndex}";

        if (targetDice.FaceEffects[faceIndex] == effectId)
        {
            int currentLevel = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;
            if (currentLevel >= effectDef.maxLevel) return 0;

            int newLevel = currentLevel + 1;
            _effectLevels[levelKey] = newLevel;
            OnFaceEffectUpgraded?.Invoke(diceIndex, effectDef, newLevel);
            return newLevel;
        }
        else
        {
            targetDice.UpgradeFace(faceIndex, effectId);
            _effectLevels[levelKey] = 1;
            OnFaceEffectUpgraded?.Invoke(diceIndex, effectDef, 1);
            return 1;
        }
    }

    /// <summary>
    /// 获取可用的升级选项（供 RoguelikeRewardSystem 调用）
    /// </summary>
    public List<FaceUpgradeOption> GetAvailableUpgrades(Dice[] dices, int levelId)
    {
        var options = new List<FaceUpgradeOption>();

        for (int diceIdx = 0; diceIdx < dices.Length; diceIdx++)
        {
            for (int faceIdx = 0; faceIdx < dices[diceIdx].FaceEffects.Length; faceIdx++)
            {
                string currentEffectId = dices[diceIdx].FaceEffects[faceIdx];

                if (string.IsNullOrEmpty(currentEffectId))
                {
                    foreach (var effectDef in _runtimeEffects)
                    {
                        // 稀有度关卡限制
                        if (effectDef.rarity == "rare" && levelId < (BalanceProvider.GetFaceEffectsConfig()?.upgrade_config?.rare_min_level ?? 8)) continue;
                        if (effectDef.rarity == "epic" && levelId < (BalanceProvider.GetFaceEffectsConfig()?.upgrade_config?.epic_min_level ?? 12)) continue;

                        if (effectDef.applicableFaces == null ||
                            effectDef.applicableFaces.Contains(faceIdx + 1))
                        {
                            options.Add(new FaceUpgradeOption
                            {
                                diceIndex = diceIdx,
                                faceIndex = faceIdx,
                                effectDef = effectDef,
                                isNew = true
                            });
                        }
                    }
                }
                else
                {
                    var currentDef = FindEffectDef(currentEffectId);
                    if (currentDef == null) continue;

                    string levelKey = $"{diceIdx}_{faceIdx}";
                    int currentLevel = _effectLevels.TryGetValue(levelKey, out int lv) ? lv : 1;

                    if (currentLevel < currentDef.maxLevel)
                    {
                        options.Add(new FaceUpgradeOption
                        {
                            diceIndex = diceIdx,
                            faceIndex = faceIdx,
                            effectDef = currentDef,
                            isNew = false,
                            currentLevel = currentLevel
                        });
                    }
                }
            }
        }

        return options;
    }

    // ========== 核心执行逻辑 ==========

    private void ExecuteEffect(FaceEffectRuntimeDef def, float value,
        List<Hero> playerHeroes, List<Hero> enemyHeroes,
        Hero specificTarget = null, Hero specificAttacker = null)
    {
        switch (def.effectType)
        {
            case FaceEffectType.Heal:
                ExecuteHeal(def, value, playerHeroes);
                break;
            case FaceEffectType.Shield:
                ExecuteShield(def, value, playerHeroes);
                break;
            case FaceEffectType.ExtraDamage:
                ExecuteExtraDamage(def, value, enemyHeroes);
                break;
            case FaceEffectType.AttackSpeed:
                ExecuteAttackSpeed(def, value, playerHeroes);
                break;
            case FaceEffectType.Stun:
                ExecuteStun(def, value, enemyHeroes);
                break;
            case FaceEffectType.CritBoost:
                ExecuteCritBoost(def, value, playerHeroes);
                break;
            case FaceEffectType.ArmorBreak:
                ExecuteArmorBreak(def, value, specificAttacker);
                break;
            case FaceEffectType.LifeSteal:
                ExecuteLifeSteal(def, value, playerHeroes);
                break;
            case FaceEffectType.Thorns:
                ExecuteThorns(def, value, playerHeroes);
                break;
            case FaceEffectType.Cleanse:
                ExecuteCleanse(def, playerHeroes);
                break;
        }
    }

    // --- 效果执行器 ---

    private void ExecuteHeal(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        float healPct = value / 100f;

        if (def.targetScope == FaceEffectTarget.LowestHealthAlly)
        {
            Hero target = FindLowestHealthAlly(allies);
            if (target != null)
            {
                int heal = Mathf.RoundToInt(target.MaxHealth * healPct);
                target.Heal(heal);
            }
        }
        else
        {
            foreach (var hero in allies)
            {
                if (hero == null || hero.IsDead) continue;
                int heal = Mathf.RoundToInt(hero.MaxHealth * healPct);
                hero.Heal(heal);
            }
        }
    }

    private void ExecuteShield(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddShield(Mathf.RoundToInt(value));
        }
    }

    private void ExecuteExtraDamage(FaceEffectRuntimeDef def, float value, List<Hero> enemies)
    {
        Hero target = def.targetScope switch
        {
            FaceEffectTarget.HighestAtkEnemy => FindHighestAttackEnemy(enemies),
            FaceEffectTarget.RandomEnemy => FindRandomEnemy(enemies),
            _ => enemies?.FirstOrDefault(h => h != null && !h.IsDead)
        };

        if (target == null) return;

        float dmgPct = value / 100f;
        int dmg = Mathf.RoundToInt(target.MaxHealth * dmgPct);
        target.TakeDamage(Mathf.Max(1, dmg));
    }

    private void ExecuteAttackSpeed(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        float speedBonus = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.BattleAttackSpeed += speedBonus;
        }
    }

    private void ExecuteStun(FaceEffectRuntimeDef def, float value, List<Hero> enemies)
    {
        var target = FindRandomEnemy(enemies);
        if (target != null)
        {
            target.SetStunned(true);
            Debug.Log($"[FaceEffect] {target.Data?.heroName} 被眩晕1回合！");
        }
    }

    private void ExecuteCritBoost(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        float critBonus = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.BattleCritRate = Mathf.Clamp01(hero.BattleCritRate + critBonus);
        }
    }

    private void ExecuteArmorBreak(FaceEffectRuntimeDef def, float value, Hero attacker)
    {
        if (attacker == null) return;
        // 破甲通过标记实现，Hero.cs 中需要 HasArmorBreak 属性
        attacker.HasArmorBreak = true;
        Debug.Log($"[FaceEffect] {attacker.Data?.heroName} 获得破甲效果（忽略{value}%防御）");
    }

    private void ExecuteLifeSteal(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        float lifestealPct = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddRelicBuff(new RelicBuff(RelicBuffType.LifeSteal, lifestealPct, "骰子面效果"));
        }
    }

    private void ExecuteThorns(FaceEffectRuntimeDef def, float value, List<Hero> allies)
    {
        float thornsPct = value / 100f;
        foreach (var hero in allies)
        {
            if (hero == null || hero.IsDead) continue;
            hero.AddRelicBuff(new RelicBuff(RelicBuffType.Thorns, thornsPct, "骰子面效果"));
        }
    }

    private void ExecuteCleanse(FaceEffectRuntimeDef def, List<Hero> allies)
    {
        var mechanicSystem = MechanicEnemySystem.Instance;
        if (mechanicSystem != null)
        {
            foreach (var hero in allies)
            {
                if (hero != null && !hero.IsDead)
                    mechanicSystem.RemoveCurseFromHero(hero);
            }
        }
        Debug.Log("[FaceEffect] 净化之光 — 已移除全体友方debuff");
    }

    // ========== 工具方法 ==========

    private FaceEffectRuntimeDef FindEffectDef(string effectId)
    {
        return _runtimeEffects.Find(e => e.effectId == effectId);
    }

    private float CalculateEffectValue(FaceEffectRuntimeDef def, int level)
    {
        return def.baseValue + def.growthPerLevel * (level - 1);
    }

    private string FormatDescription(FaceEffectRuntimeDef def, float value)
    {
        return def.descriptionTemplate?.Replace("{value}", Mathf.RoundToInt(value).ToString())
            ?? $"{def.effectName}: {value}";
    }

    private Hero FindLowestHealthAlly(List<Hero> allies)
    {
        Hero lowest = null;
        float lowestPct = 1f;
        foreach (var h in allies)
        {
            if (h == null || h.IsDead) continue;
            float pct = (float)h.CurrentHealth / h.MaxHealth;
            if (pct < lowestPct) { lowestPct = pct; lowest = h; }
        }
        return lowest;
    }

    private Hero FindHighestAttackEnemy(List<Hero> enemies)
    {
        Hero best = null;
        foreach (var h in enemies)
        {
            if (h == null || h.IsDead) continue;
            if (best == null || h.BattleAttack > best.BattleAttack) best = h;
        }
        return best;
    }

    private Hero FindRandomEnemy(List<Hero> enemies)
    {
        var alive = enemies?.FindAll(h => h != null && !h.IsDead);
        if (alive == null || alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }

    // ========== 枚举解析 ==========

    private FaceEffectType ParseEffectType(FaceEffectType val) => val;
    private FaceEffectType ParseEffectType(string val)
    {
        if (string.IsNullOrEmpty(val)) return FaceEffectType.None;
        return System.Enum.TryParse<FaceEffectType>(val, out var t) ? t : FaceEffectType.None;
    }

    private FaceEffectTarget ParseTarget(string val)
    {
        if (string.IsNullOrEmpty(val)) return FaceEffectTarget.AllAllies;
        return System.Enum.TryParse<FaceEffectTarget>(val, out var t) ? t : FaceEffectTarget.AllAllies;
    }

    private FaceEffectTrigger ParseTrigger(string val)
    {
        if (string.IsNullOrEmpty(val)) return FaceEffectTrigger.OnDiceResult;
        return System.Enum.TryParse<FaceEffectTrigger>(val, out var t) ? t : FaceEffectTrigger.OnDiceResult;
    }

    // ========== 清理 ==========

    public void ClearBattleEffects()
    {
        _activeBattleEffects.Clear();
    }

    public void ClearAll()
    {
        _effectLevels.Clear();
        _activeBattleEffects.Clear();
    }
}
