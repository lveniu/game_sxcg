using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡牌合成系统 — 合成两张同稀有度卡牌，获得更高稀有度的随机卡牌
/// 稀有度晋升链: White → Blue → Purple → Gold（Gold不可合成）
///
/// 使用方式:
///   CanMerge(a, b)       — 检查两张卡是否可合成
///   GetMergePreview(a, b) — 预览合成结果池（前端展示用）
///   MergeCards(a, b)      — 执行合成（扣金币、销毁素材、生成新卡、触发事件）
///
/// 依赖:
///   CardData.rarity       ✅
///   CardEffectEngine      ✅
///   PlayerInventory       ✅（扣金币 + 卡牌增删）
///   GameData              ✅（卡牌池）
/// </summary>
public class CardMergeSystem : MonoBehaviour
{
    public static CardMergeSystem Instance { get; private set; }

    /// <summary>合成完成事件 — 前端可监听以播放动画、刷新UI</summary>
    public event System.Action<CardInstance> OnMergeComplete;

    [Header("合成配置")]
    [Tooltip("合成消耗金币")]
    public int mergeCost = 50;

    /// <summary>
    /// 稀有度晋升链（key=当前稀有度，value=合成后的下一级稀有度）
    /// White → Blue → Purple → Gold
    /// </summary>
    private static readonly Dictionary<CardRarity, CardRarity> RarityUpgradeMap = new Dictionary<CardRarity, CardRarity>
    {
        { CardRarity.White,  CardRarity.Blue   },
        { CardRarity.Blue,   CardRarity.Purple },
        { CardRarity.Purple, CardRarity.Gold   }
    };

    /// <summary>最高稀有度（不可再合成）</summary>
    private const CardRarity MaxRarity = CardRarity.Gold;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ════════════════════════ 公开接口 ════════════════════════

    /// <summary>
    /// 检查两张卡牌是否可以合成
    /// 条件: 两张卡都不为null、稀有度相同、非最高稀有度
    /// </summary>
    public bool CanMerge(CardInstance a, CardInstance b)
    {
        // 基础校验
        if (a == null || b == null) return false;
        if (a.Data == null || b.Data == null) return false;

        // 稀有度必须相同
        if (a.Rarity != b.Rarity) return false;

        // 已是最高稀有度，无法再合成
        if (a.Rarity == MaxRarity) return false;

        // 进化卡不可作为合成素材
        if (a.Data.cardType == CardType.Evolution || b.Data.cardType == CardType.Evolution)
            return false;

        // 检查是否有下一级稀有度
        if (!RarityUpgradeMap.ContainsKey(a.Rarity)) return false;

        // 检查玩家金币是否足够（仅检查，不扣款）
        var inventory = PlayerInventory.Instance;
        if (inventory != null && inventory.Gold < mergeCost)
            return false;

        return true;
    }

    /// <summary>
    /// 预览合成结果池 — 返回可能获得的所有下一级稀有度卡牌
    /// 前端用于展示"可能获得的卡牌"列表
    /// </summary>
    /// <returns>下一级稀有度的所有卡牌数据；不可合成时返回空列表</returns>
    public List<CardData> GetMergePreview(CardInstance a, CardInstance b)
    {
        if (!CanMerge(a, b)) return new List<CardData>();

        CardRarity nextRarity = GetNextRarity(a.Rarity);
        return GetCardPoolByRarity(nextRarity);
    }

    /// <summary>
    /// 执行合成 — 核心方法
    /// 1. 验证可合成
    /// 2. 扣除金币
    /// 3. 从下一级稀有度卡牌池中随机选一张
    /// 4. 从背包销毁两张素材卡
    /// 5. 生成新卡牌实例加入背包
    /// 6. 触发 OnMergeComplete 事件
    /// </summary>
    /// <returns>合成后的新卡牌实例；失败返回null</returns>
    public CardInstance MergeCards(CardInstance a, CardInstance b)
    {
        // 1. 验证
        if (!CanMerge(a, b))
        {
            Debug.LogWarning("[CardMergeSystem] 无法合成：条件不满足");
            return null;
        }

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogError("[CardMergeSystem] PlayerInventory 不存在，无法执行合成");
            return null;
        }

        // 2. 扣除金币
        if (!inventory.SpendGold(mergeCost))
        {
            Debug.LogWarning($"[CardMergeSystem] 金币不足：需要 {mergeCost}，当前 {inventory.Gold}");
            return null;
        }

        // 3. 计算目标稀有度并从卡牌池随机选取
        CardRarity nextRarity = GetNextRarity(a.Rarity);
        List<CardData> pool = GetCardPoolByRarity(nextRarity);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[CardMergeSystem] 下一级稀有度 {nextRarity} 的卡牌池为空，退还金币");
            inventory.AddGold(mergeCost);
            return null;
        }

        CardData resultData = pool[Random.Range(0, pool.Count)];

        // 4. 销毁两张素材卡
        inventory.RemoveCard(a);
        inventory.RemoveCard(b);

        // 5. 创建新卡牌实例并加入背包
        CardInstance resultCard = new CardInstance(resultData);
        inventory.AddCard(resultCard);

        // 6. 触发事件
        Debug.Log($"[CardMergeSystem] 合成成功！{a.CardName}({a.Rarity}) + {b.CardName}({b.Rarity}) " +
                  $"→ {resultCard.CardName}({nextRarity})，花费 {mergeCost} 金币");

        OnMergeComplete?.Invoke(resultCard);

        return resultCard;
    }

    // ════════════════════════ 内部工具方法 ════════════════════════

    /// <summary>
    /// 获取下一级稀有度
    /// </summary>
    private CardRarity GetNextRarity(CardRarity current)
    {
        if (RarityUpgradeMap.TryGetValue(current, out CardRarity next))
            return next;
        return current; // 已是最高级，返回自身
    }

    /// <summary>
    /// 获取指定稀有度的所有卡牌数据
    /// 优先从 GameData.GetCardDataByName / CreateRewardCards 获取完整卡牌池
    /// </summary>
    private List<CardData> GetCardPoolByRarity(CardRarity rarity)
    {
        var pool = new List<CardData>();
        var seen = new HashSet<string>(); // 按卡牌名去重

        // 1. 从 GameData.CreateRewardCards() 获取奖励池中的卡牌
        CollectFromInstances(GameData.CreateRewardCards(), rarity, pool, seen);

        // 2. 从 GameData.CreateStartingDeck() 补充初始卡组中的卡牌
        CollectFromInstances(GameData.CreateStartingDeck(), rarity, pool, seen);

        // 3. 尝试从 Resources/CardData 加载 ScriptableObject 卡牌资产
        var resourceCards = Resources.LoadAll<CardData>("Cards");
        if (resourceCards != null)
        {
            foreach (var cardData in resourceCards)
            {
                if (cardData != null && cardData.rarity == rarity && seen.Add(cardData.cardName))
                {
                    pool.Add(cardData);
                }
            }
        }

        return pool;
    }

    /// <summary>
    /// 从 CardInstance 列表中收集指定稀有度的卡牌数据（按名称去重）
    /// </summary>
    private void CollectFromInstances(List<CardInstance> instances, CardRarity targetRarity,
        List<CardData> pool, HashSet<string> seen)
    {
        if (instances == null) return;
        foreach (var instance in instances)
        {
            if (instance?.Data != null
                && instance.Data.rarity == targetRarity
                && seen.Add(instance.Data.cardName))
            {
                pool.Add(instance.Data);
            }
        }
    }

    // ════════════════════════ 调试 / 编辑器辅助 ════════════════════════

    /// <summary>
    /// 获取稀有度晋升描述（编辑器调试用）
    /// </summary>
    public static string GetRarityChainDescription()
    {
        return "White → Blue → Purple → Gold (Gold不可合成)";
    }

    /// <summary>
    /// 获取当前合成费用（外部UI查询用）
    /// </summary>
    public int CurrentMergeCost => mergeCost;
}
