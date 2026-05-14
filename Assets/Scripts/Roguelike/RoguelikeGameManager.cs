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
    public int RunSeed { get; private set; }
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
        RunSeed = UnityEngine.Random.Range(0, int.MaxValue);
        PlayerHeroes.Clear();

        RewardSystem = new RoguelikeRewardSystem();
        RelicSystem = new RelicSystem();
        DiceRoller = new DiceRoller(3);
        LevelGenerator = new LevelGenerator();

        // BE-10: 初始化经验系统
        if (HeroExpSystem.Instance != null)
            HeroExpSystem.Destroy();
        HeroExpSystem.Create();

        // 清除旧存档
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.DeleteSave();

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

        // 成就系统：追踪关卡到达 → level_reached 类成就
        var achMgr = AchievementManager.Instance;
        if (achMgr != null)
        {
            achMgr.TrackProgress("level_reached", CurrentLevel);
        }

        // 自动存档（常规存档 + 肉鸽运行存档）
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.Save();
            SaveRun();
        }
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
        HeroData heroData = GameData.CreateHeroByJsonId(HeroNameToJsonId(reward.HeroTemplateName));
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

            // 成就系统：追踪英雄培养 → heroes_in_run 类成就
            var achMgr = AchievementManager.Instance;
            if (achMgr != null)
            {
                achMgr.TrackProgress("heroes_in_run", PlayerHeroes.Count);
            }
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

    /// <summary>
    /// 中文名 → JSON classId 映射
    /// </summary>
    private static string HeroNameToJsonId(string nameCn) => nameCn switch
    {
        "战士" => "warrior",
        "法师" => "mage",
        "刺客" => "assassin",
        "链甲使者" => "chain_knight",
        "狂战士" => "berserker",
        "大法师" => "arch_mage",
        "巡游法师" => "wandering_mage",
        "影舞者" => "shadow_dancer",
        _ => "warrior"
    };

    // ===== 存档支持 =====
    public void ClearHeroesForLoad() { PlayerHeroes.Clear(); SelectedHero = null; }
    public void AddHeroForLoad(Hero hero) { PlayerHeroes.Add(hero); }
    public void SetSelectedHero(Hero hero) { SelectedHero = hero; }
    public void SetLevelForLoad(int current, int maxReached) { CurrentLevel = current; MaxLevelReached = maxReached; }

    // ===== 肉鸽运行恢复 =====

    /// <summary>
    /// 从 RoguelikeRunData 恢复肉鸽运行状态
    /// </summary>
    public void ResumeRun(RoguelikeRunData data)
    {
        if (data == null)
        {
            Debug.LogError("[肉鸽] ResumeRun: data is null");
            return;
        }

        // 1. 恢复关卡进度
        CurrentLevel = data.currentFloor;
        MaxLevelReached = data.currentFloor;
        RunSeed = data.seed;
        IsGameOver = false;

        // 2. 初始化子系统
        RewardSystem = new RoguelikeRewardSystem();
        RelicSystem = new RelicSystem();
        DiceRoller = new DiceRoller(3);
        LevelGenerator = new LevelGenerator();

        // 3. 恢复金币
        var inv = PlayerInventory.Instance;
        if (inv != null)
            inv.ForceSetGold(data.currentGold);

        // 4. 恢复英雄
        PlayerHeroes.Clear();
        SelectedHero = null;
        for (int i = 0; i < data.selectedHeroes.Count; i++)
        {
            string heroId = data.selectedHeroes[i];
            var heroData = GameData.CreateHeroDataByTemplateName(heroId);
            if (heroData == null)
            {
                // 尝试通过JSON ID创建
                heroData = GameData.CreateHeroByJsonId(heroId);
            }
            if (heroData == null)
            {
                Debug.LogWarning($"[肉鸽] ResumeRun: 无法创建英雄 {heroId}, 跳过");
                continue;
            }

            var heroGO = new GameObject($"Hero_{heroId}");
            var hero = heroGO.AddComponent<Hero>();
            hero.Initialize(heroData, 1);

            // 恢复血量
            if (data.currentPlayerHP != null && data.currentPlayerHP.ContainsKey(heroId))
            {
                hero.SetCurrentHealth(data.currentPlayerHP[heroId]);
            }

            PlayerHeroes.Add(hero);

            // 第一个英雄作为默认选中
            if (i == 0)
                SelectedHero = hero;
        }

        // 5. 恢复遗物
        if (RelicSystem != null)
        {
            RelicSystem.ClearRelicsForLoad();
            foreach (var relicId in data.ownedRelics)
            {
                var relic = GameData.GetRelicDataById(relicId);
                if (relic != null)
                    RelicSystem.AcquireRelic(relic);
            }
        }

        // 6. 恢复卡牌
        if (inv != null && data.ownedCards != null)
        {
            inv.ClearCardsForLoad();
            foreach (var cardName in data.ownedCards)
            {
                var cardData = GameData.GetCardDataByName(cardName);
                if (cardData != null)
                    inv.AddCard(new CardInstance(cardData));
                else
                    Debug.LogWarning($"[肉鸽] ResumeRun: 无法创建卡牌 {cardName}, 跳过");
            }
        }

        // 7. 恢复装备
        if (inv != null && data.ownedEquipments != null)
        {
            inv.ClearEquipmentsForLoad();
            foreach (var equipName in data.ownedEquipments)
            {
                var equipData = GameData.CreateEquipmentByName(equipName);
                if (equipData != null)
                    inv.AddEquipment(equipData);
                else
                    Debug.LogWarning($"[肉鸽] ResumeRun: 无法创建装备 {equipName}, 跳过");
            }
        }

        // 8. 恢复骰子面值和效果
        if (DiceRoller != null && data.diceFaces != null && data.diceFaces.Count > 0)
        {
            int sides = data.diceSides > 0 ? data.diceSides : 6;
            int count = data.diceCount > 0 ? data.diceCount : 3;
            DiceRoller = new DiceRoller(count);
            for (int di = 0; di < count && di < DiceRoller.Dices.Length; di++)
            {
                var dice = DiceRoller.Dices[di];
                for (int fi = 0; fi < sides && (di * sides + fi) < data.diceFaces.Count; fi++)
                {
                    int faceVal = data.diceFaces[di * sides + fi];
                    dice.UpgradeFace(fi, faceVal);

                    // 恢复面效果
                    if (data.diceFaceEffects != null && (di * sides + fi) < data.diceFaceEffects.Count)
                    {
                        string effect = data.diceFaceEffects[di * sides + fi];
                        if (!string.IsNullOrEmpty(effect))
                            dice.AddSpecialEffect(fi, effect);
                    }
                }
            }
        }

        // 9. 恢复英雄星级和等级
        if (data.heroStarLevels != null)
        {
            foreach (var hero in PlayerHeroes)
            {
                string heroId = hero.HeroData?.templateName ?? hero.name;
                if (data.heroStarLevels.ContainsKey(heroId))
                    hero.SetStarLevel(data.heroStarLevels[heroId]);
                if (data.heroLevels.ContainsKey(heroId))
                    hero.SetLevel(data.heroLevels[heroId]);
            }
        }

        // 10. 恢复商店等级
        var shopMgr = ShopManager.Instance;
        if (shopMgr != null)
            shopMgr.SetShopLevelForLoad(data.shopLevel);

        // 7. 恢复地图访问节点
        var mapSys = RoguelikeMapSystem.Instance;
        if (mapSys?.CurrentMap != null && data.visitedNodes != null && data.visitedNodes.Count > 0)
        {
            mapSys.CurrentMap.BuildIndex();
            var allNodes = new List<RoguelikeMapNode>();
            foreach (var layer in mapSys.CurrentMap.layers)
                allNodes.AddRange(layer);

            foreach (int nodeIdx in data.visitedNodes)
            {
                if (nodeIdx >= 0 && nodeIdx < allNodes.Count)
                {
                    allNodes[nodeIdx].isVisited = true;
                    allNodes[nodeIdx].isAvailable = false;
                }
            }
            mapSys.RefreshAvailableNodes();
        }

        Debug.Log($"[肉鸽] 运行恢复完成! Floor={data.currentFloor}, Heroes={PlayerHeroes.Count}, Relics={data.ownedRelics.Count}, Gold={data.currentGold}, Seed={data.seed}");
    }

    /// <summary>
    /// 采集当前运行状态为 RoguelikeRunData（用于肉鸽存档）
    /// </summary>
    public RoguelikeRunData CaptureRunData()
    {
        var data = new RoguelikeRunData
        {
            currentFloor = CurrentLevel,
            currentGold = PlayerInventory.Instance?.Gold ?? 0,
            shopLevel = ShopManager.Instance?.ShopLevel ?? 1,
            seed = RunSeed
        };

        // 英雄
        foreach (var hero in PlayerHeroes)
        {
            string heroId = hero.HeroData?.templateName ?? hero.name;
            data.selectedHeroes.Add(heroId);
            data.currentPlayerHP[heroId] = hero.CurrentHealth;
            data.heroStarLevels[heroId] = hero.StarLevel;
            data.heroLevels[heroId] = hero.HeroLevel;
        }

        // 遗物
        if (RelicSystem != null)
            data.ownedRelics = RelicSystem.GetOwnedRelicIds();

        // 卡牌
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            foreach (var card in inv.Cards)
                data.ownedCards.Add(card.Data?.cardName ?? card.ToString());
            foreach (var equip in inv.Equipments)
                data.ownedEquipments.Add(equip.equipmentName ?? equip.name);
        }

        // 骰子面值和效果（扁平化存储）
        if (DiceRoller != null)
        {
            data.diceCount = DiceRoller.Dices.Length;
            data.diceSides = DiceRoller.Dices.Length > 0 ? DiceRoller.Dices[0].Faces.Length : 6;
            foreach (var dice in DiceRoller.Dices)
            {
                for (int fi = 0; fi < dice.Faces.Length; fi++)
                {
                    data.diceFaces.Add(dice.Faces[fi]);
                    data.diceFaceEffects.Add(dice.GetFaceEffect(fi) ?? "");
                }
            }
        }

        // 访问节点
        var mapSys = RoguelikeMapSystem.Instance;
        if (mapSys?.CurrentMap != null)
        {
            mapSys.CurrentMap.BuildIndex();
            int idx = 0;
            foreach (var layer in mapSys.CurrentMap.layers)
            {
                foreach (var node in layer)
                {
                    if (node.isVisited)
                        data.visitedNodes.Add(idx);
                    idx++;
                }
            }
        }

        return data;
    }

    /// <summary>
    /// 保存肉鸽运行存档（在每个关卡开始时调用）
    /// </summary>
    public void SaveRun()
    {
        if (SaveSystem.Instance == null) return;
        var runData = CaptureRunData();
        SaveSystem.Instance.SaveRoguelikeRun(runData);
    }
}
