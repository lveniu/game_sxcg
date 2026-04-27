using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 运行时场景启动器 — 挂载到任意GameObject，点Play自动创建完整游戏场景
/// 使用方法: 创建空物体→挂载此脚本→点Play
/// </summary>
public class RuntimeSceneBootstrap : MonoBehaviour
{
    [Header("是否自动运行")]
    public bool autoBootstrap = true;

    [Header("预制体")]
    public GameObject textPrefab;
    public GameObject buttonPrefab;

    void Start()
    {
        if (autoBootstrap)
        {
            Bootstrap();
        }
    }

    public void Bootstrap()
    {
        Debug.Log("[Bootstrap] 开始初始化场景...");

        // 1. 核心管理器
        EnsureManager<GameManager>("GameManager");
        EnsureManager<GameStateMachine>("GameStateMachine");
        EnsureManager<CardDeck>("CardDeck");
        EnsureManager<GridManager>("GridManager");
        EnsureManager<BattleManager>("BattleManager");
        EnsureManager<LevelManager>("LevelManager");
        EnsureManager<DamagePopup>("DamagePopup");

        // 2. Canvas + EventSystem
        var canvas = SetupCanvas();

        // 3. UI面板
        var mainMenuPanel = CreatePanel(canvas, "MainMenuPanel");
        var heroSelectPanel = CreatePanel(canvas, "HeroSelectPanel");
        var diceRollPanel = CreatePanel(canvas, "DiceRollPanel");
        var cardPlayPanel = CreatePanel(canvas, "CardPlayPanel");
        var battlePanel = CreatePanel(canvas, "BattlePanel");
        var settlementPanel = CreatePanel(canvas, "SettlementPanel");

        // 4. 挂载UI脚本
        SetupMainMenuUI(mainMenuPanel);
        SetupHeroSelectUI(heroSelectPanel);
        SetupDiceRollUI(diceRollPanel);
        SetupCardPlayUI(cardPlayPanel);
        SetupBattleUI(battlePanel);
        SetupSettlementUI(settlementPanel);

        // 5. UIManager
        var uiMgr = canvas.gameObject.GetComponent<UIManager>();
        if (uiMgr == null) uiMgr = canvas.gameObject.AddComponent<UIManager>();
        uiMgr.mainMenuPanel = mainMenuPanel;
        uiMgr.heroSelectPanel = heroSelectPanel;
        uiMgr.diceRollPanel = diceRollPanel;
        uiMgr.cardPlayPanel = cardPlayPanel;
        uiMgr.battlePanel = battlePanel;
        uiMgr.settlementPanel = settlementPanel;

        // 6. 初始化游戏
        GameManager.Instance?.StartNewGame();

        // 隐藏所有面板，让状态机控制
        mainMenuPanel.SetActive(true);
        heroSelectPanel.SetActive(false);
        diceRollPanel.SetActive(false);
        cardPlayPanel.SetActive(false);
        battlePanel.SetActive(false);
        settlementPanel.SetActive(false);

        Debug.Log("[Bootstrap] 场景初始化完成！");
    }

    T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        var existing = Object.FindObjectOfType<T>();
        if (existing != null) return existing;
        var go = new GameObject(name);
        return go.AddComponent<T>();
    }

    Canvas SetupCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        return canvas;
    }

    GameObject CreatePanel(Canvas canvas, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        return go;
    }

    void SetupMainMenuUI(GameObject panel)
    {
        var ui = panel.GetComponent<MainMenuUI>() ?? panel.AddComponent<MainMenuUI>();
        CreateText(panel, "Title", "GameSXCG", 48, new Vector2(0, 150));
        ui.startGameButton = CreateButton(panel, "StartBtn", "开始游戏", new Vector2(0, 50));
        ui.exitGameButton = CreateButton(panel, "ExitBtn", "退出", new Vector2(0, -50));
    }

    void SetupHeroSelectUI(GameObject panel)
    {
        var ui = panel.GetComponent<HeroSelectUI>() ?? panel.AddComponent<HeroSelectUI>();
        ui.heroButtons = new List<Button>();
        ui.heroNameTexts = new List<Text>();
        ui.heroDescTexts = new List<Text>();

        CreateText(panel, "Title", "选择初始英雄", 36, new Vector2(0, 200));

        string[] names = { "坦克", "射手", "刺客" };
        float[] xPos = { -200, 0, 200 };

        for (int i = 0; i < 3; i++)
        {
            var btn = CreateButton(panel, $"HeroBtn_{i}", names[i], new Vector2(xPos[i], 0));
            ui.heroButtons.Add(btn);

            var nameTxt = CreateText(panel, $"HeroName_{i}", names[i], 24, new Vector2(xPos[i], 80));
            ui.heroNameTexts.Add(nameTxt.GetComponent<Text>());

            var descTxt = CreateText(panel, $"HeroDesc_{i}", "描述", 18, new Vector2(xPos[i], -80));
            ui.heroDescTexts.Add(descTxt.GetComponent<Text>());
        }
    }

    void SetupDiceRollUI(GameObject panel)
    {
        var ui = panel.GetComponent<DiceRollUI>() ?? panel.AddComponent<DiceRollUI>();
        ui.diceValueTexts = new List<Text>();
        ui.diceImages = new List<Image>();

        CreateText(panel, "Title", "骰子阶段", 36, new Vector2(0, 200));

        for (int i = 0; i < 3; i++)
        {
            var diceGo = new GameObject($"Dice_{i}", typeof(RectTransform));
            diceGo.transform.SetParent(panel.transform, false);
            var diceRt = diceGo.GetComponent<RectTransform>();
            diceRt.sizeDelta = new Vector2(80, 80);
            diceRt.anchoredPosition = new Vector2((i - 1) * 120, 80);

            var img = diceGo.AddComponent<Image>();
            img.color = Color.white;
            ui.diceImages.Add(img);

            var txtGo = new GameObject($"DiceText_{i}", typeof(RectTransform));
            txtGo.transform.SetParent(diceGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.text = "?";
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            ui.diceValueTexts.Add(txt);
        }

        ui.combinationText = CreateText(panel, "ComboText", "组合: 等待投掷...", 24, new Vector2(0, -20)).GetComponent<Text>();
        ui.effectText = CreateText(panel, "EffectText", "", 20, new Vector2(0, -60)).GetComponent<Text>();
        ui.rerollCountText = CreateText(panel, "RerollText", "剩余重摇: 2", 20, new Vector2(0, -100)).GetComponent<Text>();

        ui.rollButton = CreateButton(panel, "RollBtn", "掷骰子", new Vector2(-100, -180));
        ui.rerollButton = CreateButton(panel, "RerollBtn", "重摇", new Vector2(0, -180));
        ui.nextPhaseButton = CreateButton(panel, "NextBtn", "下一步", new Vector2(100, -180));
    }

    void SetupCardPlayUI(GameObject panel)
    {
        var ui = panel.GetComponent<CardPlayUI>() ?? panel.AddComponent<CardPlayUI>();

        CreateText(panel, "Title", "出牌 & 站位", 36, new Vector2(0, 220));

        var handGo = new GameObject("HandArea", typeof(RectTransform));
        handGo.transform.SetParent(panel.transform, false);
        var handRt = handGo.GetComponent<RectTransform>();
        handRt.sizeDelta = new Vector2(600, 120);
        handRt.anchoredPosition = new Vector2(0, -200);
        handGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        ui.handCardParent = handGo.transform;

        ui.gridButtons = new List<Button>();
        ui.gridTexts = new List<Text>();
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int index = y * 3 + x;
                var pos = new Vector2((x - 1) * 80, (1 - y) * 60 + 40);
                var btn = CreateButton(panel, $"Grid_{x}_{y}", $"{x},{y}", pos, new Vector2(70, 50));
                ui.gridButtons.Add(btn);
                ui.gridTexts.Add(btn.GetComponentInChildren<Text>());
            }
        }

        ui.summonButton = CreateButton(panel, "SummonBtn", "召唤英雄", new Vector2(-150, -120));
        ui.startBattleButton = CreateButton(panel, "BattleBtn", "开始战斗", new Vector2(150, -120));

        ui.populationText = CreateText(panel, "PopText", "人口: 0/3", 20, new Vector2(0, 170)).GetComponent<Text>();
        ui.comboText = CreateText(panel, "ComboText", "骰子组合: -", 20, new Vector2(0, 140)).GetComponent<Text>();

        // 卡牌预制体
        var cardPrefab = new GameObject("CardButtonPrefab", typeof(RectTransform));
        cardPrefab.transform.SetParent(panel.transform, false);
        var cardRt = cardPrefab.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(100, 120);
        cardPrefab.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
        var cardTxt = new GameObject("Text", typeof(RectTransform));
        cardTxt.transform.SetParent(cardPrefab.transform, false);
        var cardTxtRt = cardTxt.GetComponent<RectTransform>();
        cardTxtRt.anchorMin = Vector2.zero;
        cardTxtRt.anchorMax = Vector2.one;
        cardTxtRt.offsetMin = Vector2.zero;
        cardTxtRt.offsetMax = Vector2.zero;
        cardTxt.AddComponent<Text>().alignment = TextAnchor.MiddleCenter;
        cardTxt.GetComponent<Text>().fontSize = 16;
        cardPrefab.AddComponent<Button>();
        cardPrefab.SetActive(false);
        ui.cardButtonPrefab = cardPrefab;
    }

    void SetupBattleUI(GameObject panel)
    {
        var ui = panel.GetComponent<BattleUI>() ?? panel.AddComponent<BattleUI>();

        CreateText(panel, "Title", "战斗中...", 36, new Vector2(0, 220));

        var playerGo = new GameObject("PlayerPanel", typeof(RectTransform));
        playerGo.transform.SetParent(panel.transform, false);
        var pRt = playerGo.GetComponent<RectTransform>();
        pRt.sizeDelta = new Vector2(250, 300);
        pRt.anchoredPosition = new Vector2(-200, 0);
        playerGo.AddComponent<Image>().color = new Color(0, 0.3f, 0.5f, 0.3f);
        ui.playerPanel = playerGo.transform;

        var enemyGo = new GameObject("EnemyPanel", typeof(RectTransform));
        enemyGo.transform.SetParent(panel.transform, false);
        var eRt = enemyGo.GetComponent<RectTransform>();
        eRt.sizeDelta = new Vector2(250, 300);
        eRt.anchoredPosition = new Vector2(200, 0);
        enemyGo.AddComponent<Image>().color = new Color(0.5f, 0, 0, 0.3f);
        ui.enemyPanel = enemyGo.transform;

        ui.timerText = CreateText(panel, "Timer", "时间: 0.0s", 20, new Vector2(0, 170)).GetComponent<Text>();
        ui.speedButton = CreateButton(panel, "SpeedBtn", "x1", new Vector2(-80, -200));
        ui.skipButton = CreateButton(panel, "SkipBtn", "跳过", new Vector2(80, -200));
        ui.battleLogText = CreateText(panel, "LogText", "", 16, new Vector2(0, -160)).GetComponent<Text>();
        ui.battleLogText.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 100);

        var unitPrefab = new GameObject("UnitInfoPrefab", typeof(RectTransform));
        unitPrefab.transform.SetParent(panel.transform, false);
        var uRt = unitPrefab.GetComponent<RectTransform>();
        uRt.sizeDelta = new Vector2(200, 50);
        unitPrefab.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(unitPrefab.transform, false);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0.5f);
        nameRt.anchorMax = new Vector2(1, 1);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;
        nameGo.AddComponent<Text>().fontSize = 16;
        var hpGo = new GameObject("HpText", typeof(RectTransform));
        hpGo.transform.SetParent(unitPrefab.transform, false);
        var hpRt = hpGo.GetComponent<RectTransform>();
        hpRt.anchorMin = new Vector2(0, 0);
        hpRt.anchorMax = new Vector2(1, 0.5f);
        hpRt.offsetMin = Vector2.zero;
        hpRt.offsetMax = Vector2.zero;
        hpGo.AddComponent<Text>().fontSize = 14;
        unitPrefab.SetActive(false);
        ui.unitInfoPrefab = unitPrefab;
    }

    void SetupSettlementUI(GameObject panel)
    {
        var ui = panel.GetComponent<SettlementUI>() ?? panel.AddComponent<SettlementUI>();

        ui.resultText = CreateText(panel, "Result", "结果", 48, new Vector2(0, 150)).GetComponent<Text>();
        ui.levelText = CreateText(panel, "Level", "第1关", 28, new Vector2(0, 80)).GetComponent<Text>();

        var rewardGo = new GameObject("RewardArea", typeof(RectTransform));
        rewardGo.transform.SetParent(panel.transform, false);
        var rRt = rewardGo.GetComponent<RectTransform>();
        rRt.sizeDelta = new Vector2(500, 100);
        rRt.anchoredPosition = new Vector2(0, -20);
        rewardGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        ui.rewardParent = rewardGo.transform;

        ui.nextLevelButton = CreateButton(panel, "NextBtn", "下一关", new Vector2(-100, -150));
        ui.returnMenuButton = CreateButton(panel, "MenuBtn", "返回主菜单", new Vector2(100, -150));

        var rewardPrefab = new GameObject("RewardBtnPrefab", typeof(RectTransform));
        rewardPrefab.transform.SetParent(panel.transform, false);
        var rpRt = rewardPrefab.GetComponent<RectTransform>();
        rpRt.sizeDelta = new Vector2(120, 80);
        rewardPrefab.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f);
        var rpTxt = new GameObject("Text", typeof(RectTransform));
        rpTxt.transform.SetParent(rewardPrefab.transform, false);
        var rpTxtRt = rpTxt.GetComponent<RectTransform>();
        rpTxtRt.anchorMin = Vector2.zero;
        rpTxtRt.anchorMax = Vector2.one;
        rpTxtRt.offsetMin = Vector2.zero;
        rpTxtRt.offsetMax = Vector2.zero;
        rpTxt.AddComponent<Text>().alignment = TextAnchor.MiddleCenter;
        rpTxt.GetComponent<Text>().fontSize = 16;
        rewardPrefab.AddComponent<Button>();
        rewardPrefab.SetActive(false);
        ui.rewardButtonPrefab = rewardPrefab;
    }

    GameObject CreateText(GameObject parent, string name, string content, int fontSize, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 60);
        rt.anchoredPosition = pos;
        return go;
    }

    Button CreateButton(GameObject parent, string name, string label, Vector2 pos, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size ?? new Vector2(160, 50);
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.4f, 0.8f);

        var btn = go.AddComponent<Button>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 20;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        return btn;
    }
}
