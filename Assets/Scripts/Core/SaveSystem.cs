using System.Collections.Generic;
using UnityEngine;

/// <summary>可序列化的存档数据</summary>
[System.Serializable]
public class SaveData
{
    public int version = 1;
    public long saveTimestamp;

    // 肉鸽进度
    public int currentLevel;
    public int maxLevelReached;
    public int gold;

    // 英雄列表（名字+等级+当前血量）
    public List<HeroSaveEntry> heroes = new List<HeroSaveEntry>();

    // 选中的英雄索引
    public int selectedHeroIndex = -1;

    // 装备列表（装备名字+已装备到哪个英雄）
    public List<EquipmentSaveEntry> equipments = new List<EquipmentSaveEntry>();

    // 卡牌列表（卡牌名字）
    public List<string> cardNames = new List<string>();

    // 遗物列表（遗物ID）
    public List<string> relicIds = new List<string>();

    // 骰子状态
    public DiceSaveData diceData;
}

[System.Serializable]
public class HeroSaveEntry
{
    public string heroName;      // 用名字反向查找HeroData
    public int level;
    public int currentHealth;
    public int exp;             // 经验值
    public int equippedWeapon;   // 装备索引(-1=无)
    public int equippedArmor;
    public int equippedAccessory;
    public int equippedArtifact;
}

[System.Serializable]
public class EquipmentSaveEntry
{
    public string equipName;
    public int equippedToHeroIndex = -1; // -1=在背包
}

[System.Serializable]
public class DiceSaveData
{
    public List<int> faceValues = new List<int>();
    public int freeRerollCount;
}

/// <summary>存档系统 — 自动存档+手动存档</summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const string SAVE_KEY = "game_save_v1";
    private const string HAS_SAVE_KEY = "has_save";

    public static bool HasSave => PlayerPrefs.GetInt(HAS_SAVE_KEY, 0) == 1;

    public event System.Action OnSaveComplete;
    public event System.Action<SaveData> OnLoadComplete;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>保存当前游戏状态</summary>
    public void Save()
    {
        var data = CaptureCurrentState();
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.SetInt(HAS_SAVE_KEY, 1);
        PlayerPrefs.Save();
        Debug.Log($"[SaveSystem] 存档完成 Lv{data.currentLevel} 金币{data.gold}");
        OnSaveComplete?.Invoke();
    }

    /// <summary>加载存档</summary>
    public SaveData Load()
    {
        if (!HasSave) return null;
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return null;

        var data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log($"[SaveSystem] 读档完成 Lv{data.currentLevel}");
        return data;
    }

    /// <summary>恢复存档到游戏状态</summary>
    public bool RestoreSave(SaveData data)
    {
        if (data == null) return false;

        var rgm = RoguelikeGameManager.Instance;
        if (rgm == null) { Debug.LogError("[SaveSystem] RoguelikeGameManager不存在"); return false; }

        // 1. 恢复金币
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.ForceSetGold(data.gold);
        }

        // 2. 恢复英雄
        rgm.ClearHeroesForLoad();
        for (int i = 0; i < data.heroes.Count; i++)
        {
            var entry = data.heroes[i];
            var heroData = GameData.CreateHeroDataByTemplateName(entry.heroName);
            if (heroData == null) continue;

            var heroGO = new GameObject($"Hero_{entry.heroName}");
            var hero = heroGO.AddComponent<Hero>();
            // 使用现有Initialize方法，然后设置等级和经验
            hero.Initialize(heroData, 1); // 先用1星初始化
            hero.SetLevel(entry.level);
            hero.SetExp(entry.exp);
            // 设置当前血量（Initialize会将血量设为MaxHealth，这里覆盖为存档值）
            hero.SetCurrentHealth(entry.currentHealth);

            rgm.AddHeroForLoad(hero);

            if (i == data.selectedHeroIndex)
                rgm.SetSelectedHero(hero);
        }

        // 3. 恢复装备
        if (inv != null)
        {
            inv.ClearEquipmentsForLoad();
            for (int i = 0; i < data.equipments.Count; i++)
            {
                var eqEntry = data.equipments[i];
                var equipData = GameData.CreateEquipmentByName(eqEntry.equipName);
                if (equipData == null) continue;

                if (eqEntry.equippedToHeroIndex >= 0 && eqEntry.equippedToHeroIndex < rgm.PlayerHeroes.Count)
                {
                    var hero = rgm.PlayerHeroes[eqEntry.equippedToHeroIndex];
                    hero.Equip(equipData);
                }
                else
                {
                    inv.AddEquipment(equipData);
                }
            }
        }

        // 4. 恢复卡牌
        if (inv != null)
        {
            inv.ClearCardsForLoad();
            foreach (var cardName in data.cardNames)
            {
                var cardData = GameData.GetCardDataByName(cardName);
                if (cardData != null)
                    inv.AddCard(new CardInstance(cardData));
            }
        }

        // 5. 恢复遗物
        var relicSys = rgm.RelicSystem;
        if (relicSys != null)
        {
            relicSys.ClearRelicsForLoad();
            foreach (var relicId in data.relicIds)
            {
                var relic = GameData.GetRelicDataById(relicId);
                if (relic != null)
                    relicSys.AcquireRelic(relic);
            }
        }

        // 6. 恢复关卡进度
        rgm.SetLevelForLoad(data.currentLevel, data.maxLevelReached);

        OnLoadComplete?.Invoke(data);
        Debug.Log("[SaveSystem] 状态恢复完成");
        return true;
    }

    /// <summary>删除存档</summary>
    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.DeleteKey(HAS_SAVE_KEY);
        PlayerPrefs.Save();
        Debug.Log("[SaveSystem] 存档已删除");
    }

    /// <summary>捕获当前状态到SaveData</summary>
    SaveData CaptureCurrentState()
    {
        var data = new SaveData();
        data.saveTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var rgm = RoguelikeGameManager.Instance;
        var inv = PlayerInventory.Instance;

        if (rgm != null)
        {
            data.currentLevel = rgm.CurrentLevel;
            data.maxLevelReached = rgm.MaxLevelReached;

            // 英雄
            for (int i = 0; i < rgm.PlayerHeroes.Count; i++)
            {
                var hero = rgm.PlayerHeroes[i];
                if (hero == null || hero.Data == null) continue;

                var entry = new HeroSaveEntry
                {
                    heroName = hero.Data.heroName,
                    level = hero.HeroLevel,
                    currentHealth = hero.CurrentHealth,
                    exp = hero.CurrentExp,
                    equippedWeapon = -1,
                    equippedArmor = -1,
                    equippedAccessory = -1,
                    equippedArtifact = -1
                };

                data.heroes.Add(entry);

                if (hero == rgm.SelectedHero)
                    data.selectedHeroIndex = i;
            }
        }

        if (inv != null)
        {
            data.gold = inv.Gold;

            // 装备（背包中的）
            foreach (var eq in inv.Equipments)
            {
                if (eq == null) continue;
                data.equipments.Add(new EquipmentSaveEntry
                {
                    equipName = eq.equipmentName,
                    equippedToHeroIndex = -1 // 在背包
                });
            }

            // 卡牌
            foreach (var card in inv.Cards)
            {
                if (card?.Data != null)
                    data.cardNames.Add(card.Data.cardName);
            }
        }

        // 遗物
        if (rgm?.RelicSystem != null)
        {
            foreach (var owned in rgm.RelicSystem.OwnedRelics)
            {
                if (owned?.Data != null)
                    data.relicIds.Add(owned.Data.relicId);
            }
        }

        // 骰子（如果有DiceRoller）
        if (rgm?.DiceRoller != null)
        {
            var diceData = new DiceSaveData();
            var dices = rgm.DiceRoller.Dices;
            if (dices != null)
            {
                foreach (var d in dices)
                    diceData.faceValues.Add(d.CurrentValue);
            }
            diceData.freeRerollCount = rgm.DiceRoller.FreeRerolls;
            data.diceData = diceData;
        }

        return data;
    }
}
