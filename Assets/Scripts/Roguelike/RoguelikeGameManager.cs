using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 肉鸽模式总管理器 — 协调所有肉鸽子系统
/// 连接 GameStateMachine、BattleManager、RewardSystem、RelicSystem、LevelGenerator
/// </summary>
public class RoguelikeGameManager : MonoBehaviour
{
    public static RoguelikeGameManager Instance { get; private set; }

    // 子系统引用
    public RoguelikeRewardSystem RewardSystem { get; private set; }
    public RelicSystem RelicSystem { get; private set; }
    public DiceRoller DiceRoller { get; private set; }
    public LevelGenerator LevelGenerator { get; private set; }

    // 游戏状态
    public int CurrentLevel { get; private set; }
    public Hero SelectedHero { get; private set; }
    public List<Hero> PlayerHeroes { get; private set; } = new List<Hero>();
    public DiceCombination LastDiceCombo { get; private set; }
    public bool IsGameOver { get; private set; }
    public int MaxLevelReached { get; private set; }

    // 事件
    public event System.Action<int> OnLevelStarted;
    public event System.Action<List<RewardOption>> OnRewardsGenerated;
    public event System.Action<RewardOption> OnRewardApplied;
    public event System.Action<int> OnGameOver;
    public event System.Action OnNewGameStarted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 初始化新游戏
    /// </summary>
    public void StartNewGame()
    {
        CurrentLevel = 0;
        IsGameOver = false;
        MaxLevelReached = 0;
        PlayerHeroes.Clear();

        RewardSystem = new RoguelikeRewardSystem();
        RelicSystem = new RelicSystem();
        DiceRoller = new DiceRoller(3);
        LevelGenerator = new LevelGenerator();

        OnNewGameStarted?.Invoke();
        Debug.Log("[肉鸽] 新游戏开始！");
    }

    /// <summary>
    /// 选择英雄（HeroSelect阶段）
    /// </summary>
    public void SelectHero(Hero hero)
    {
        SelectedHero = hero;
        PlayerHeroes.Add(hero);
        Debug.Log($"[肉鸽] 选择英雄: {hero.Data.heroName}");
    }

    /// <summary>
    /// 进入下一关（由GameStateMachine在DiceRoll状态进入时调用）
    /// 关卡计数由GameStateMachine管理，这里同步并初始化子系统
    /// </summary>
    public void EnterNextLevel()
    {
        // 同步关卡编号（GameStateMachine已在NextState中递增）
        CurrentLevel = GameStateMachine.Instance?.CurrentLevel ?? CurrentLevel + 1;
        if (CurrentLevel > MaxLevelReached)
            MaxLevelReached = CurrentLevel;

        if (RelicSystem != null) RelicSystem.ResetForNewLevel();
        OnLevelStarted?.Invoke(CurrentLevel);
        Debug.Log($"[肉鸽] 进入第 {CurrentLevel} 关");
    }

    /// <summary>
    /// 设置骰子组合（DiceRoll阶段完成后调用）
    /// </summary>
    public void SetDiceCombo(DiceCombination combo)
    {
        LastDiceCombo = combo;
        Debug.Log($"[肉鸽] 骰子组合: {combo.Description}");
    }

    /// <summary>
    /// 开始战斗（Battle阶段）
    /// </summary>
    public void StartBattle()
    {
        var enemies = LevelGenerator.GenerateEnemies(CurrentLevel);

        // 应用遗物效果
        RelicSystem.ApplyRelicEffects(PlayerHeroes);

        // 重摇次数 = 基础1 + 遗物额外
        DiceRoller.SetFreeRerolls(1 + RelicSystem.GetExtraRerolls());

        // 调用BattleManager开始战斗
        BattleManager.Instance?.StartBattle(PlayerHeroes, enemies, LastDiceCombo);
    }

    /// <summary>
    /// 生成奖励选项（RoguelikeReward阶段）
    /// </summary>
    public List<RewardOption> GenerateRewards()
    {
        var rewards = RewardSystem.GenerateRewards(CurrentLevel, PlayerHeroes, DiceRoller);
        OnRewardsGenerated?.Invoke(rewards);
        return rewards;
    }

    /// <summary>
    /// 选择奖励
    /// </summary>
    public void ChooseReward(RewardOption reward)
    {
        // 如果是新单位，需要创建Hero并加入队伍
        if (reward.Type == RewardType.NewUnit)
        {
            CreateNewUnit(reward);
        }

        RewardSystem.ApplyReward(reward, PlayerHeroes, DiceRoller, RelicSystem);
        OnRewardApplied?.Invoke(reward);
    }

    /// <summary>
    /// 创建新单位并加入队伍
    /// </summary>
    void CreateNewUnit(RewardOption reward)
    {
        if (PlayerHeroes.Count >= 5)
        {
            Debug.LogWarning($"[肉鸽] 队伍已满({PlayerHeroes.Count}/5)，无法招募新单位");
            return;
        }

        // 通过GameData工厂方法创建HeroData
        HeroData heroData = GameData.CreateHeroDataByTemplateName(reward.HeroTemplateName);
        if (heroData == null)
        {
            Debug.LogError($"[肉鸽] 无法创建英雄模板: {reward.HeroTemplateName}");
            return;
        }

        // 创建GameObject + Hero组件（与CardDeck.SummonHero相同模式）
        var go = new GameObject($"Hero_{heroData.heroName}");
        var hero = go.AddComponent<Hero>();
        hero.Initialize(heroData, reward.Rarity);

        // 加入队伍
        AddHeroToTeam(hero);
        Debug.Log($"[肉鸽] 成功招募 {heroData.heroName}（星{reward.Rarity}）加入队伍！当前队伍: {PlayerHeroes.Count}人");
    }

    /// <summary>
    /// 添加已创建的Hero到队伍
    /// </summary>
    public void AddHeroToTeam(Hero hero)
    {
        if (hero != null && !PlayerHeroes.Contains(hero))
        {
            PlayerHeroes.Add(hero);
            Debug.Log($"[肉鸽] {hero.Data.heroName} 加入队伍！当前队伍: {PlayerHeroes.Count}人");
        }
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GameOver()
    {
        IsGameOver = true;
        OnGameOver?.Invoke(MaxLevelReached);
        Debug.Log($"[肉鸽] 游戏结束！最远到达第 {MaxLevelReached} 关");
    }

    /// <summary>
    /// 获取当前关卡信息
    /// </summary>
    public string GetLevelInfo()
    {
        var config = LevelGenerator?.GetLevelConfig(CurrentLevel);
        if (config == null) return $"第{CurrentLevel}关";
        return $"第{CurrentLevel}关 | {config.LevelName} | 难度{config.Difficulty:F1}x";
    }

    /// <summary>
    /// 获取游戏状态摘要
    /// </summary>
    public string GetGameSummary()
    {
        return $"关卡: {CurrentLevel} | 队伍: {PlayerHeroes.Count}人 | 遗物: {RelicSystem.RelicCount}个 | 最远: {MaxLevelReached}关";
    }
}
