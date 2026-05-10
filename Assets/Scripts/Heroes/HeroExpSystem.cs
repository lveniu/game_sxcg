using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 经验来源枚举
/// </summary>
public enum ExpSource
{
    LevelClear,   // 通关奖励
    KillEnemy,    // 击杀敌人
    BossKill,     // Boss击杀
    EventBonus    // 随机事件奖励
}

/// <summary>
/// BE-10 英雄经验/等级系统
/// 管理英雄经验获取、升级、属性成长、星级进化
/// 单例模式，类似 RoguelikeMapSystem
/// 配置从 exp_table.json 和 hero_exp_config.json 加载
/// </summary>
public class HeroExpSystem
{
    public static HeroExpSystem Instance { get; private set; }

    // ========== 配置数据 ==========
    private ExpTableConfig _expTableConfig;
    private HeroExpFileConfig _heroExpConfig;

    // 经验公式参数（从 exp_table.json 加载）
    private const int BASE_EXP = 20;
    private const int PER_LEVEL_EXP = 30;
    private const int ACCEL_THRESHOLD = 6;
    private const int ACCEL_BONUS = 20;

    // 经验来源配置（从 exp_table.json 加载的默认值）
    private int _killEnemyExp = 5;
    private int _bossKillBonus = 30;
    private float _eventBonusMultiplier = 1.5f;
    private float _eventBonusChance = 0.1f;

    // ========== 事件（供UI绑定） ==========
    public event System.Action<Hero, int, ExpSource> OnHeroExpGained;
    public event System.Action<Hero, int, int> OnHeroLevelUp;
    public event System.Action<Hero, int, int> OnHeroStarUpgrade;

    /// <summary>
    /// 初始化系统，加载配置
    /// </summary>
    public void Initialize()
    {
        LoadConfig();
        Debug.Log("[HeroExpSystem] 初始化完成");
    }

    /// <summary>
    /// 创建并初始化单例
    /// </summary>
    public static HeroExpSystem Create()
    {
        Instance = new HeroExpSystem();
        Instance.Initialize();
        return Instance;
    }

    /// <summary>
    /// 销毁单例
    /// </summary>
    public static void Destroy()
    {
        if (Instance != null)
        {
            Instance.OnHeroExpGained = null;
            Instance.OnHeroLevelUp = null;
            Instance.OnHeroStarUpgrade = null;
            Instance = null;
        }
    }

    // ========== 配置加载 ==========

    private void LoadConfig()
    {
        // 从 exp_table.json 加载经验表配置
        var expJson = ConfigLoader.LoadJson("exp_table");
        if (expJson != null)
        {
            _expTableConfig = expJson.ToObject<ExpTableConfig>();

            // 加载经验来源参数
            if (expJson["exp_sources"] != null)
            {
                var sources = expJson["exp_sources"];
                if (sources["kill_enemy"] != null)
                    _killEnemyExp = sources["kill_enemy"]["value"]?.Value<int>() ?? 5;
                if (sources["boss_kill_bonus"] != null)
                    _bossKillBonus = sources["boss_kill_bonus"]["value"]?.Value<int>() ?? 30;
                if (sources["event_bonus_multiplier"] != null)
                {
                    _eventBonusMultiplier = sources["event_bonus_multiplier"]["value"]?.Value<float>() ?? 1.5f;
                    _eventBonusChance = sources["event_bonus_multiplier"]["chance"]?.Value<float>() ?? 0.1f;
                }
            }
        }

        // 从 hero_exp_config.json 加载英雄经验扩展配置
        _heroExpConfig = ConfigLoader.LoadHeroExpConfig();

        Debug.Log($"[HeroExpSystem] 配置加载完成: killExp={_killEnemyExp}, bossBonus={_bossKillBonus}");
    }

    // ========== 经验公式 ==========

    /// <summary>
    /// 根据公式计算升级所需经验
    /// 公式: exp_to_next(lvl) = 20 + (lvl - 1) * 30 + MAX(0, lvl - 6) * 20
    /// </summary>
    public int GetExpToNextLevel(int currentLevel)
    {
        // 优先从配置文件读取
        if (_expTableConfig?.levels != null)
        {
            var entry = _expTableConfig.levels.Find(l => l.level == currentLevel);
            if (entry != null)
                return entry.exp_to_next;
        }

        // fallback 使用公式
        return BASE_EXP + (currentLevel - 1) * PER_LEVEL_EXP + Mathf.Max(0, currentLevel - ACCEL_THRESHOLD) * ACCEL_BONUS;
    }

    /// <summary>
    /// 获取等级属性加成百分比（每级 +5%）
    /// </summary>
    public float GetLevelBonus(int level)
    {
        float pct = BalanceProvider.GetLevelStatBonusPct();
        return (level - 1) * pct / 100f;
    }

    // ========== 经验获取 ==========

    /// <summary>
    /// 给单个英雄添加经验，处理升级
    /// </summary>
    public void GainExp(Hero hero, int amount, ExpSource source)
    {
        if (hero == null || hero.IsDead) return;
        if (amount <= 0) return;

        // 添加经验
        hero.AddExp(amount);
        OnHeroExpGained?.Invoke(hero, amount, source);

        // 检查升级
        CheckLevelUp(hero);
    }

    /// <summary>
    /// 给全队分配经验（通关/Boss击杀）
    /// 基础经验 = baseExp * level，平分给所有存活英雄
    /// </summary>
    public void GainExpForTeam(List<Hero> heroes, int baseExp, int level, bool isBossKill)
    {
        if (heroes == null || heroes.Count == 0) return;

        var aliveHeroes = heroes.Where(h => h != null && !h.IsDead).ToList();
        if (aliveHeroes.Count == 0) return;

        // 基础经验计算：baseExp * level（从配置 exp_sources.level_clear_base 公式: 10 * level）
        int totalExp = baseExp * level;

        // Boss击杀额外经验
        if (isBossKill)
        {
            totalExp += _bossKillBonus;
        }

        // 平分给存活英雄（向上取整）
        int expPerHero = Mathf.CeilToInt((float)totalExp / aliveHeroes.Count);

        ExpSource source = isBossKill ? ExpSource.BossKill : ExpSource.LevelClear;
        foreach (var hero in aliveHeroes)
        {
            GainExp(hero, expPerHero, source);
        }

        Debug.Log($"[HeroExpSystem] 队伍经验分配: 总{totalExp}, 每人{expPerHero}, 存活{aliveHeroes.Count}人");
    }

    /// <summary>
    /// 击杀经验：击杀者获得全部经验
    /// exp = killEnemyBaseExp + enemyLevel相关
    /// </summary>
    public void GainExpForKill(Hero killer, int enemyLevel)
    {
        if (killer == null || killer.IsDead) return;

        // 击杀经验 = 基础值 + 敌人等级加成
        int exp = _killEnemyExp + Mathf.Max(0, enemyLevel - 1) * 2;

        GainExp(killer, exp, ExpSource.KillEnemy);
    }

    // ========== 升级逻辑 ==========

    /// <summary>
    /// 检查英雄是否满足升级条件，处理连续升级
    /// </summary>
    private void CheckLevelUp(Hero hero)
    {
        int expToNext = GetExpToNextLevel(hero.HeroLevel);

        // 支持连续升级
        int maxIterations = 100; // 安全上限
        while (hero.CurrentExp >= expToNext && maxIterations-- > 0)
        {
            int oldLevel = hero.HeroLevel;

            // 消耗经验
            hero.SetExp(hero.CurrentExp - expToNext);

            // 升级
            hero.SetLevel(oldLevel + 1);

            OnHeroLevelUp?.Invoke(hero, oldLevel, hero.HeroLevel);
            Debug.Log($"[HeroExpSystem] {hero.Data.heroName} 升级! Lv{oldLevel} → Lv{hero.HeroLevel}");

            // 更新下一级经验需求
            expToNext = GetExpToNextLevel(hero.HeroLevel);

            // 检查被动技能阈值强化
            CheckPassiveSkillThreshold(hero);
        }
    }

    /// <summary>
    /// 检查被动技能是否达到强化阈值
    /// </summary>
    private void CheckPassiveSkillThreshold(Hero hero)
    {
        var thresholds = BalanceProvider.GetPassiveSkillThresholds();
        if (thresholds != null && thresholds.Contains(hero.HeroLevel))
        {
            float bonus = BalanceProvider.GetPassiveSkillBonusPerThreshold();
            Debug.Log($"[HeroExpSystem] {hero.Data.heroName} 达到被动技能强化阈值 Lv{hero.HeroLevel}, 加成+{bonus * 100}%");
        }
    }

    // ========== 星级进化 ==========

    /// <summary>
    /// 尝试星级合成：检查队伍中是否存在2个同名同星英雄，合并升星
    /// </summary>
    public bool TryStarUpgrade(Hero hero, List<Hero> team)
    {
        if (hero == null || team == null) return false;
        if (hero.StarLevel >= 3) return false; // 已满星

        // 查找队伍中同名同星的其他英雄
        var candidates = team.Where(h =>
            h != null &&
            h != hero &&
            !h.IsDead &&
            h.Data != null &&
            hero.Data != null &&
            h.Data.heroName == hero.Data.heroName &&
            h.StarLevel == hero.StarLevel
        ).ToList();

        if (candidates.Count < 1) return false; // 需要至少2个（自身 + 1个）

        // 找到可以合并的目标（取第一个匹配的）
        var mergeTarget = candidates[0];
        int oldStar = hero.StarLevel;

        // 合并：移除被合并的英雄，提升当前英雄星级
        hero.UpgradeStar();

        OnHeroStarUpgrade?.Invoke(hero, oldStar, hero.StarLevel);
        Debug.Log($"[HeroExpSystem] {hero.Data.heroName} 星级合成! ★{oldStar} → ★{hero.StarLevel} (合并了 {mergeTarget.Data.heroName})");

        return true;
    }

    /// <summary>
    /// 获取队伍中可合成英雄的配对信息
    /// </summary>
    public List<(Hero hero, Hero mergeTarget)> GetAvailableStarUpgrades(List<Hero> team)
    {
        var result = new List<(Hero, Hero)>();
        if (team == null) return result;

        var processed = new HashSet<Hero>();

        foreach (var hero in team)
        {
            if (hero == null || hero.IsDead || hero.StarLevel >= 3) continue;
            if (processed.Contains(hero)) continue;

            var partner = team.FirstOrDefault(h =>
                h != null &&
                h != hero &&
                !h.IsDead &&
                !processed.Contains(h) &&
                h.Data != null &&
                hero.Data != null &&
                h.Data.heroName == hero.Data.heroName &&
                h.StarLevel == hero.StarLevel
            );

            if (partner != null)
            {
                result.Add((hero, partner));
                processed.Add(hero);
                processed.Add(partner);
            }
        }

        return result;
    }

    // ========== 查询方法 ==========

    /// <summary>
    /// 获取英雄当前等级进度（0.0 ~ 1.0）
    /// </summary>
    public float GetLevelProgress(Hero hero)
    {
        if (hero == null) return 0f;
        int expToNext = GetExpToNextLevel(hero.HeroLevel);
        if (expToNext <= 0) return 1f;
        return Mathf.Clamp01((float)hero.CurrentExp / expToNext);
    }

    /// <summary>
    /// 获取英雄经验信息字符串（用于UI显示）
    /// </summary>
    public string GetExpInfoString(Hero hero)
    {
        if (hero == null) return "";
        int expToNext = GetExpToNextLevel(hero.HeroLevel);
        return $"Lv{hero.HeroLevel} ({hero.CurrentExp}/{expToNext})";
    }

    /// <summary>
    /// 获取累计经验（从1级到指定等级所需的总经验）
    /// </summary>
    public int GetCumulativeExp(int targetLevel)
    {
        if (_expTableConfig?.levels != null)
        {
            var entry = _expTableConfig.levels.Find(l => l.level == targetLevel - 1);
            if (entry != null)
                return entry.cumulative;
        }

        // fallback 计算
        int total = 0;
        for (int lvl = 1; lvl < targetLevel; lvl++)
        {
            total += GetExpToNextLevel(lvl);
        }
        return total;
    }

    /// <summary>
    /// 给予事件奖励经验（有概率触发额外加成）
    /// </summary>
    public void GrantEventBonusExp(Hero hero, int baseAmount)
    {
        if (hero == null || hero.IsDead) return;

        int finalAmount = baseAmount;

        // 随机事件加成
        if (Random.value < _eventBonusChance)
        {
            finalAmount = Mathf.RoundToInt(baseAmount * _eventBonusMultiplier);
            Debug.Log($"[HeroExpSystem] 事件经验加成触发! {baseAmount} → {finalAmount}");
        }

        GainExp(hero, finalAmount, ExpSource.EventBonus);
    }
}

// ========== 配置数据模型（对应 exp_table.json） ==========

/// <summary>
/// 经验表配置（对应 exp_table.json 的 exp_table 部分）
/// </summary>
public class ExpTableConfig
{
    public string _description;
    public List<ExpLevelEntry> levels;
    public string formula;
    public int max_level;
}

/// <summary>
/// 单级经验条目
/// </summary>
public class ExpLevelEntry
{
    public int level;
    public int exp_to_next;
    public int cumulative;
    public int est_level_at;
    public string note;
}
