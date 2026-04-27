using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 整合测试 — 模拟完整的一局游戏流程
/// 挂载到场景中的任意GameObject，点Play查看Console输出
/// </summary>
public class IntegrationTest : MonoBehaviour
{
    [Header("测试配置")]
    public bool runOnStart = true;
    public float stepDelay = 0.5f;

    private GameManager gm;
    private CardDeck deck;
    private GridManager grid;
    private BattleManager battle;

    void Start()
    {
        if (runOnStart)
        {
            StartCoroutine(RunFullGameTest());
        }
    }

    IEnumerator RunFullGameTest()
    {
        Debug.Log("\n========== 整合测试开始 ==========\n");

        // 1. 初始化游戏
        yield return InitializeGame();

        // 2. 选择初始英雄
        yield return SelectHeroPhase();

        // 3-6. 模拟3关战斗
        for (int level = 1; level <= 3; level++)
        {
            yield return RunBattleLevel(level);
            if (gm.StateMachine.IsGameLost) break;
        }

        // 7. 结束
        yield return EndGame();

        Debug.Log("\n========== 整合测试结束 ==========");
    }

    IEnumerator InitializeGame()
    {
        Debug.Log("[1] 初始化游戏...");

        // 确保核心系统存在
        gm = GameManager.Instance;
        if (gm == null)
        {
            var go = new GameObject("GameManager");
            gm = go.AddComponent<GameManager>();
        }

        deck = CardDeck.Instance;
        if (deck == null)
        {
            var go = new GameObject("CardDeck");
            deck = go.AddComponent<CardDeck>();
        }

        grid = GridManager.Instance;
        if (grid == null)
        {
            var go = new GameObject("GridManager");
            grid = go.AddComponent<GridManager>();
        }

        battle = BattleManager.Instance;
        if (battle == null)
        {
            var go = new GameObject("BattleManager");
            battle = go.AddComponent<BattleManager>();
        }

        gm.StateMachine.ResetGame();
        deck.ResetForNewGame();
        grid.InitializeGrid();
        battle.ClearBattle();

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator SelectHeroPhase()
    {
        Debug.Log("\n[2] 选择初始英雄: 坦克");

        var heroData = GameData.CreateTankHero();
        var heroCard = new CardInstance(ScriptableObject.CreateInstance<CardData>());
        // 注：这里简化处理，直接把英雄卡放入手牌
        var heroCardData = ScriptableObject.CreateInstance<CardData>();
        heroCardData.cardName = "坦克";
        heroCardData.cardType = CardType.Hero;
        heroCardData.cost = 2;
        heroCard = new CardInstance(heroCardData);

        deck.AddCard(heroCard);

        // 给予初始卡组
        var startingDeck = GameData.CreateStartingDeck();
        foreach (var card in startingDeck)
        {
            deck.AddCard(card);
        }

        Debug.Log($"手牌数量: {deck.handCards.Count}");
        foreach (var card in deck.handCards)
        {
            Debug.Log($"  - {card.CardName} ({card.Type})");
        }

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator RunBattleLevel(int level)
    {
        Debug.Log($"\n========== 第{level}关 ==========");

        // 3. 骰子阶段
        yield return DiceRollPhase();

        // 4. 出牌阶段
        yield return CardPlayPhase();

        // 5. 站位阶段
        yield return PositioningPhase();

        // 6. 战斗阶段
        yield return BattlePhase(level);

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator DiceRollPhase()
    {
        Debug.Log("\n[3] 骰子阶段 — 掷3个六面骰...");

        gm.DiceRoller.Reset();
        int[] results = gm.DiceRoller.RollAll();
        var combo = gm.DiceRoller.GetCurrentCombination();

        Debug.Log($"骰子结果: [{string.Join(", ", results)}]");
        Debug.Log($"组合: {combo.Description}");
        Debug.Log($"组合效果: {combo.EffectDescription}");

        // 如果是差组合，尝试重摇
        if (combo.Type == DiceCombinationType.None && gm.DiceRoller.CanReroll)
        {
            Debug.Log("组合不佳，重摇一次...");
            int[] rerollResults = gm.DiceRoller.RerollAll();
            combo = gm.DiceRoller.GetCurrentCombination();
            Debug.Log($"重摇后: [{string.Join(", ", rerollResults)}] → {combo.Description}");
        }

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator CardPlayPhase()
    {
        Debug.Log("\n[4] 出牌阶段...");

        var combo = gm.DiceRoller.GetCurrentCombination();

        // 召唤英雄
        var heroCard = deck.handCards.Find(c => c.Type == CardType.Hero);
        if (heroCard != null && deck.HasSpace)
        {
            var heroData = GameData.CreateTankHero();
            var hero = deck.SummonHero(heroData);
            Debug.Log($"召唤英雄: {hero.Data.heroName} (生命{hero.MaxHealth}/攻击{hero.Attack}/防御{hero.Defense})");
        }

        // 打出属性卡（复制列表避免foreach中修改集合）
        var attrCards = deck.handCards.FindAll(c => c.Type == CardType.Attribute);
        foreach (var card in attrCards.ToArray())
        {
            deck.PlayAttributeCard(card);
            Debug.Log($"打出属性卡: {card.CardName}");
        }

        // 打出战斗卡
        var battleCards = deck.handCards.FindAll(c => c.Type == CardType.Battle);
        foreach (var card in battleCards.ToArray())
        {
            bool comboTriggered = deck.PlayBattleCard(card, combo);
            if (comboTriggered)
            {
                Debug.Log($"骰子联动触发！{card.CardName} 效果翻倍");
            }
        }

        // 应用骰子组合到场上英雄
        deck.ApplyDiceCombinationToField(combo);

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator PositioningPhase()
    {
        Debug.Log("\n[5] 站位阶段...");

        // 简化：坦克放前排中间，射手放后排
        int heroIndex = 0;
        foreach (var hero in deck.fieldHeroes)
        {
            int x = heroIndex;
            int y = hero.Data.heroClass == HeroClass.Tank ? 0 : 2;
            grid.PlaceHero(hero, x, y);
            hero.ApplyRowEffect(grid.GetRow(new Vector2Int(x, y)));
            Debug.Log($"站位: {hero.Data.heroName} → [{x},{y}] ({grid.GetRow(new Vector2Int(x, y))})");
            heroIndex++;
        }

        grid.PrintGrid();

        yield return new WaitForSeconds(stepDelay);
    }

    IEnumerator BattlePhase(int level)
    {
        Debug.Log("\n[6] 自动战斗开始...");

        // 创建敌人
        var enemies = CreateEnemiesForLevel(level);
        Debug.Log($"敌人: {enemies.Count}人");
        foreach (var e in enemies)
        {
            Debug.Log($"  - {e.Data.heroName} (生命{e.MaxHealth}/攻击{e.Attack})");
        }

        // 开始战斗
        battle.StartBattle(deck.fieldHeroes, enemies);

        // 等待战斗结束（简化：等几秒后检查结果）
        yield return new WaitForSeconds(3f);

        if (battle.IsBattleActive)
        {
            battle.StopBattle();
        }

        if (battle.PlayerWon)
        {
            Debug.Log($"第{level}关 胜利！");
            gm.StateMachine.SetGameWon();

            // 模拟奖励
            var rewards = GameData.CreateRewardCards();
            var reward = rewards[Random.Range(0, rewards.Count)];
            deck.AddCard(reward);
            Debug.Log($"获得奖励: {reward.CardName}");
        }
        else
        {
            Debug.Log($"第{level}关 失败...");
            gm.StateMachine.SetGameLost();
        }

        // 清理
        foreach (var enemy in enemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        grid.ClearGrid();

        yield return new WaitForSeconds(stepDelay);
    }

    List<Hero> CreateEnemiesForLevel(int level)
    {
        var enemies = new List<Hero>();

        if (level == 1)
        {
            // 第1关: 2个小怪
            for (int i = 0; i < 2; i++)
            {
                var data = GameData.CreateEnemyGrunt();
                var go = new GameObject($"Enemy_{i}");
                var enemy = go.AddComponent<Hero>();
                enemy.Initialize(data);
                enemy.GridPosition = new Vector2Int(i, 3);
                enemies.Add(enemy);
            }
        }
        else if (level == 2)
        {
            // 第2关: 1精英 + 1小怪
            var eliteData = GameData.CreateEnemyElite();
            var go1 = new GameObject("Enemy_Elite");
            var elite = go1.AddComponent<Hero>();
            elite.Initialize(eliteData);
            elite.GridPosition = new Vector2Int(1, 3);
            enemies.Add(elite);

            var gruntData = GameData.CreateEnemyGrunt();
            var go2 = new GameObject("Enemy_Grunt");
            var grunt = go2.AddComponent<Hero>();
            grunt.Initialize(gruntData);
            grunt.GridPosition = new Vector2Int(0, 3);
            enemies.Add(grunt);
        }
        else
        {
            // 第3关: Boss
            var bossData = GameData.CreateEnemyBoss();
            var go = new GameObject("Enemy_Boss");
            var boss = go.AddComponent<Hero>();
            boss.Initialize(bossData);
            boss.GridPosition = new Vector2Int(1, 3);
            enemies.Add(boss);
        }

        return enemies;
    }

    IEnumerator EndGame()
    {
        Debug.Log("\n[7] 游戏结束");

        if (gm.StateMachine.IsGameWon)
        {
            Debug.Log("🎉 恭喜通关！");
        }
        else
        {
            Debug.Log("💀 游戏结束，再来一局？");
        }

        yield return new WaitForSeconds(stepDelay);
    }
}
