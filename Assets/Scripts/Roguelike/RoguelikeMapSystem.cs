using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 地图节点类型
/// </summary>
public enum MapNodeType
{
    Battle,      // 普通战斗（权重最高）
    Elite,       // 精英战斗（高难度高奖励）
    Event,       // 随机事件
    Shop,        // 商店
    Rest,        // 休息点（回复生命）
    Boss,        // Boss关（每5关强制，固定位置）
    Treasure     // 宝箱
}

/// <summary>
/// 地图节点
/// </summary>
public class MapNode
{
    public string nodeId;              // 唯一ID "node_{layer}_{index}"
    public int layer;                  // 层级 0-based
    public int indexInLayer;           // 该层位置
    public MapNodeType nodeType;
    public bool isVisited;
    public bool isAvailable;           // 与已访问节点相邻且未被访问
    public List<string> nextNodeIds = new List<string>();   // 下层连接
    public List<string> prevNodeIds = new List<string>();   // 上层连接
    public string previewText;         // "3星敌人" / "随机事件"
    public int difficulty;             // 1-5星

    public override string ToString() => $"[{nodeId}] {nodeType} (难度{difficulty})";
}

/// <summary>
/// 地图数据
/// </summary>
public class MapData
{
    public List<List<MapNode>> layers = new List<List<MapNode>>();    // 按层组织
    public string currentNodeId;
    public int totalLayers;
    public string startNodeId;

    /// <summary>节点ID快速查找</summary>
    private Dictionary<string, MapNode> nodeMap = new Dictionary<string, MapNode>();

    public void BuildIndex()
    {
        nodeMap.Clear();
        foreach (var layer in layers)
            foreach (var node in layer)
                nodeMap[node.nodeId] = node;
    }

    public MapNode GetNode(string id)
    {
        nodeMap.TryGetValue(id, out var node);
        return node;
    }

    public MapNode FindNode(int layer, int indexInLayer)
    {
        return nodeMap.TryGetValue($"node_{layer}_{indexInLayer}", out var node) ? node : null;
    }
}

/// <summary>
/// BE-08 肉鸽地图路径系统
/// 15层地图生成算法，节点连接，路径选择驱动状态机
/// </summary>
public class RoguelikeMapSystem
{
    public static RoguelikeMapSystem Instance { get; private set; }

    public MapData CurrentMap { get; private set; }
    public string CurrentNodeId => CurrentMap?.currentNodeId;

    // 事件
    public event System.Action<MapData> OnMapGenerated;
    public event System.Action<MapNode> OnNodeSelected;

    // ===== 配置（可后续从JSON读取）=====
    public int totalLayers = 15;
    public int minNodesPerLayer = 2;
    public int maxNodesPerLayer = 4;
    public int bossInterval = 5;
    public int maxConnectionsPerNode = 3;

    // 节点类型权重（非Boss层）
    private readonly Dictionary<MapNodeType, float> nodeWeights = new Dictionary<MapNodeType, float>
    {
        { MapNodeType.Battle,   0.40f },
        { MapNodeType.Elite,    0.10f },
        { MapNodeType.Event,    0.20f },
        { MapNodeType.Shop,     0.15f },
        { MapNodeType.Rest,     0.10f },
        { MapNodeType.Treasure, 0.05f },
    };

    public RoguelikeMapSystem()
    {
        if (Instance != null)
            Debug.LogWarning("[RoguelikeMapSystem] 实例已存在，覆盖");
        Instance = this;
    }

    // ===== 地图生成 =====

    /// <summary>
    /// 生成完整地图（新游戏或到达新区间时调用）
    /// </summary>
    public MapData GenerateMap(int totalLevels = 15)
    {
        totalLayers = totalLevels;
        CurrentMap = new MapData { totalLayers = totalLayers };

        for (int layerIdx = 0; layerIdx < totalLayers; layerIdx++)
        {
            var layer = GenerateLayer(layerIdx);
            CurrentMap.layers.Add(layer);
        }

        // 生成连接
        GenerateConnections();

        // 建索引
        CurrentMap.BuildIndex();

        // 设置起始节点
        var startNode = CurrentMap.layers[0][0];
        startNode.isVisited = true;
        startNode.isAvailable = false;
        CurrentMap.currentNodeId = startNode.nodeId;
        CurrentMap.startNodeId = startNode.nodeId;

        // 标记第二层可达节点
        MarkAvailableNodes();

        OnMapGenerated?.Invoke(CurrentMap);
        Debug.Log($"[RoguelikeMapSystem] 地图生成完成: {totalLayers}层, {CountAllNodes()}个节点");

        return CurrentMap;
    }

    /// <summary>
    /// 生成单层节点
    /// </summary>
    List<MapNode> GenerateLayer(int layerIdx)
    {
        var layer = new List<MapNode>();

        // Boss层：固定1个Boss节点
        if (IsBossLayer(layerIdx))
        {
            var bossNode = CreateNode(layerIdx, 0, MapNodeType.Boss);
            bossNode.difficulty = layerIdx; // Boss难度=层数
            bossNode.previewText = $"Boss (难度{layerIdx})";
            layer.Add(bossNode);
            return layer;
        }

        // 随机节点数 2-4
        int nodeCount = Random.Range(minNodesPerLayer, maxNodesPerLayer + 1);

        for (int i = 0; i < nodeCount; i++)
        {
            var nodeType = PickNodeType(layerIdx);
            var node = CreateNode(layerIdx, i, nodeType);
            node.difficulty = CalculateDifficulty(layerIdx, nodeType);
            node.previewText = GeneratePreviewText(nodeType, node.difficulty);
            layer.Add(node);
        }

        // 约束：每层至少1个非Battle节点
        EnsureDiversity(layer, layerIdx);

        return layer;
    }

    MapNode CreateNode(int layer, int index, MapNodeType type)
    {
        return new MapNode
        {
            nodeId = $"node_{layer}_{index}",
            layer = layer,
            indexInLayer = index,
            nodeType = type,
            isVisited = false,
            isAvailable = false,
            difficulty = 1,
        };
    }

    /// <summary>
    /// 按权重随机选择节点类型
    /// </summary>
    MapNodeType PickNodeType(int layerIdx)
    {
        // 前2层不出Elite和Treasure
        var weights = new Dictionary<MapNodeType, float>(nodeWeights);
        if (layerIdx < 2)
        {
            weights.Remove(MapNodeType.Elite);
            weights.Remove(MapNodeType.Treasure);
        }

        // Boss前一层不出Shop
        if (layerIdx > 0 && IsBossLayer(layerIdx + 1))
        {
            weights.Remove(MapNodeType.Shop);
        }

        // 归一化
        float total = weights.Values.Sum();
        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var kv in weights)
        {
            cumulative += kv.Value;
            if (roll <= cumulative)
                return kv.Key;
        }

        return MapNodeType.Battle; // fallback
    }

    int CalculateDifficulty(int layer, MapNodeType type)
    {
        int baseDiff = Mathf.CeilToInt((layer + 1) / 3f);
        switch (type)
        {
            case MapNodeType.Elite: return baseDiff + 2;
            case MapNodeType.Boss: return baseDiff + 3;
            case MapNodeType.Battle: return baseDiff + 1;
            default: return baseDiff;
        }
    }

    string GeneratePreviewText(MapNodeType type, int difficulty)
    {
        switch (type)
        {
            case MapNodeType.Battle: return $"战斗 ({difficulty}星)";
            case MapNodeType.Elite: return $"精英 ({difficulty}星)";
            case MapNodeType.Event: return "随机事件";
            case MapNodeType.Shop: return "商店";
            case MapNodeType.Rest: return "休息点";
            case MapNodeType.Boss: return $"Boss ({difficulty}星)";
            case MapNodeType.Treasure: return "宝箱";
            default: return "未知";
        }
    }

    void EnsureDiversity(List<MapNode> layer, int layerIdx)
    {
        bool hasNonBattle = layer.Any(n => n.nodeType != MapNodeType.Battle);
        if (!hasNonBattle && layer.Count > 1)
        {
            // 随机将一个Battle改为Event或Rest
            int idx = Random.Range(0, layer.Count);
            layer[idx].nodeType = Random.value < 0.5f ? MapNodeType.Event : MapNodeType.Rest;
            layer[idx].previewText = GeneratePreviewText(layer[idx].nodeType, layer[idx].difficulty);
        }
    }

    // ===== 连接生成 =====

    /// <summary>
    /// 生成层间连接
    /// 规则：每个节点至少连下层1个（最多3个），下层每个至少被连1个，优先连临近位置
    /// </summary>
    void GenerateConnections()
    {
        for (int i = 0; i < CurrentMap.layers.Count - 1; i++)
        {
            var currentLayer = CurrentMap.layers[i];
            var nextLayer = CurrentMap.layers[i + 1];

            // Boss层特殊处理：所有上层节点连到Boss
            if (IsBossLayer(i + 1))
            {
                var bossNode = nextLayer[0];
                foreach (var node in currentLayer)
                {
                    ConnectNodes(node, bossNode);
                }
                continue;
            }

            // 1. 确保下层每个节点至少被连1个
            foreach (var nextNode in nextLayer)
            {
                // 找上层距离最近的未连接节点
                var bestParent = FindClosestNode(currentLayer, nextNode.indexInLayer, nextLayer.Count);
                if (bestParent != null && !nextNode.prevNodeIds.Contains(bestParent.nodeId))
                {
                    ConnectNodes(bestParent, nextNode);
                }
            }

            // 2. 每个上层节点额外连接（随机1-2条额外连接）
            foreach (var node in currentLayer)
            {
                int extraConnections = Random.Range(0, 2); // 0-1条额外
                for (int j = 0; j < extraConnections; j++)
                {
                    if (node.nextNodeIds.Count >= maxConnectionsPerNode) break;
                    var target = FindClosestUnconnectedNode(node, nextLayer);
                    if (target != null)
                    {
                        ConnectNodes(node, target);
                    }
                }
            }
        }
    }

    void ConnectNodes(MapNode from, MapNode to)
    {
        if (!from.nextNodeIds.Contains(to.nodeId))
            from.nextNodeIds.Add(to.nodeId);
        if (!to.prevNodeIds.Contains(from.nodeId))
            to.prevNodeIds.Add(from.nodeId);
    }

    MapNode FindClosestNode(List<MapNode> layer, int targetIndex, int layerSize)
    {
        // 按index距离排序，优先选连接数少的
        var candidates = layer
            .OrderBy(n => Mathf.Abs(n.indexInLayer - targetIndex))
            .ThenBy(n => n.nextNodeIds.Count)
            .ToList();

        return candidates.FirstOrDefault();
    }

    MapNode FindClosestUnconnectedNode(MapNode from, List<MapNode> nextLayer)
    {
        var candidates = nextLayer
            .Where(n => !from.nextNodeIds.Contains(n.nodeId))
            .OrderBy(n => Mathf.Abs(n.indexInLayer - from.indexInLayer))
            .ToList();

        return candidates.FirstOrDefault();
    }

    bool IsBossLayer(int layerIdx)
    {
        return (layerIdx + 1) % bossInterval == 0;
    }

    // ===== 节点选择 =====

    /// <summary>
    /// 获取当前可选的下一层节点
    /// </summary>
    public List<MapNode> GetAvailableNodes()
    {
        if (CurrentMap == null || string.IsNullOrEmpty(CurrentNodeId)) return new List<MapNode>();

        var current = CurrentMap.GetNode(CurrentNodeId);
        if (current == null) return new List<MapNode>();

        var available = new List<MapNode>();
        foreach (var nextId in current.nextNodeIds)
        {
            var node = CurrentMap.GetNode(nextId);
            if (node != null && !node.isVisited)
            {
                node.isAvailable = true;
                available.Add(node);
            }
        }

        return available;
    }

    /// <summary>
    /// 选择节点（玩家确认前往）
    /// 根据 nodeType 驱动状态切换
    /// </summary>
    public void SelectNode(string nodeId)
    {
        if (CurrentMap == null) return;

        var node = CurrentMap.GetNode(nodeId);
        if (node == null || node.isVisited)
        {
            Debug.LogWarning($"[RoguelikeMapSystem] 无法选择节点 {nodeId}");
            return;
        }

        // 验证：必须是当前节点的邻居
        var current = CurrentMap.GetNode(CurrentNodeId);
        if (current != null && !current.nextNodeIds.Contains(nodeId))
        {
            Debug.LogWarning($"[RoguelikeMapSystem] 节点 {nodeId} 不可达");
            return;
        }

        // 标记当前节点已完成
        if (current != null) current.isAvailable = false;

        // 更新当前节点
        node.isVisited = true;
        node.isAvailable = false;
        CurrentMap.currentNodeId = nodeId;

        Debug.Log($"[RoguelikeMapSystem] 选择节点: {node}");

        // 根据节点类型驱动状态切换
        DriveStateByNodeType(node);

        OnNodeSelected?.Invoke(node);
    }

    /// <summary>
    /// 根据节点类型驱动 GameStateMachine 状态切换
    /// </summary>
    void DriveStateByNodeType(MapNode node)
    {
        var gsm = GameStateMachine.Instance;
        if (gsm == null)
        {
            Debug.LogWarning("[RoguelikeMapSystem] GameStateMachine 未就绪");
            return;
        }

        switch (node.nodeType)
        {
            case MapNodeType.Battle:
            case MapNodeType.Elite:
            case MapNodeType.Boss:
                // 战斗类型 → 骰子阶段 → 战斗
                gsm.ChangeState(GameState.DiceRoll);
                break;

            case MapNodeType.Event:
                // 随机事件 → 打开EventPanel（100%触发，不依赖30%概率）
                {
                    int level = RoguelikeGameManager.Instance?.CurrentLevel ?? 1;
                    var uiMgr = Game.UI.NewUIManager.Instance;
                    if (uiMgr != null && uiMgr.eventPanel != null)
                    {
                        Debug.Log("[RoguelikeMapSystem] 随机事件节点 → 打开EventPanel");
                        uiMgr.eventPanel.ShowFromMapNode(level);
                    }
                    else
                    {
                        Debug.LogWarning("[RoguelikeMapSystem] EventPanel不可用，回退到骰子流程");
                        gsm.ChangeState(GameState.DiceRoll);
                    }
                }
                break;

            case MapNodeType.Shop:
                // 商店 → 暂走奖励阶段
                Debug.Log("[RoguelikeMapSystem] 商店节点（暂走奖励流程）");
                gsm.ChangeState(GameState.RoguelikeReward);
                break;

            case MapNodeType.Rest:
                // 休息点 → 恢复队伍20%最大生命 → 回到地图选择
                RestTeam();
                Debug.Log("[RoguelikeMapSystem] 休息点 — 队伍回复20%生命");
                // 休息后直接回到地图选择下一节点
                gsm.ChangeState(GameState.MapSelect);
                break;

            case MapNodeType.Treasure:
                // 宝箱 → 直接发放奖励
                GrantTreasureReward(node);
                Debug.Log("[RoguelikeMapSystem] 宝箱 — 发放奖励");
                gsm.ChangeState(GameState.RoguelikeReward);
                break;
        }
    }

    /// <summary>
    /// 休息：恢复队伍20%最大生命
    /// </summary>
    void RestTeam()
    {
        var heroes = RoguelikeGameManager.Instance?.PlayerHeroes;
        if (heroes == null) return;

        foreach (var hero in heroes)
        {
            if (hero == null || hero.IsDead) continue;
            int healAmount = Mathf.RoundToInt(hero.MaxHealth * 0.2f);
            hero.Heal(healAmount);
        }
    }

    /// <summary>
    /// 宝箱奖励：给金币
    /// </summary>
    void GrantTreasureReward(MapNode node)
    {
        int gold = 50 + node.difficulty * 20;
        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            inventory.AddGold(gold);
            Debug.Log($"[RoguelikeMapSystem] 宝箱奖励: +{gold}金币");
        }
    }

    /// <summary>
    /// 获取当前节点
    /// </summary>
    public MapNode GetCurrentNode()
    {
        if (CurrentMap == null) return null;
        return CurrentMap.GetNode(CurrentNodeId);
    }

    /// <summary>
    /// 标记可达节点
    /// </summary>
    void MarkAvailableNodes()
    {
        if (CurrentMap == null) return;

        // 清除所有标记
        foreach (var layer in CurrentMap.layers)
            foreach (var node in layer)
                node.isAvailable = false;

        // 当前节点的下层邻居
        var current = CurrentMap.GetNode(CurrentNodeId);
        if (current != null)
        {
            foreach (var nextId in current.nextNodeIds)
            {
                var node = CurrentMap.GetNode(nextId);
                if (node != null && !node.isVisited)
                    node.isAvailable = true;
            }
        }
    }

    /// <summary>
    /// 是否到达最终Boss层
    /// </summary>
    public bool IsFinalBossDefeated()
    {
        if (CurrentMap == null) return false;
        var currentNode = GetCurrentNode();
        return currentNode != null
            && currentNode.nodeType == MapNodeType.Boss
            && currentNode.isVisited
            && currentNode.layer == CurrentMap.totalLayers - 1;
    }

    /// <summary>
    /// 获取地图统计信息
    /// </summary>
    public string GetMapStats()
    {
        if (CurrentMap == null) return "无地图";

        int visited = 0, total = 0;
        foreach (var layer in CurrentMap.layers)
        {
            foreach (var node in layer)
            {
                total++;
                if (node.isVisited) visited++;
            }
        }

        var current = GetCurrentNode();
        return $"层{current?.layer + 1 ?? 0}/{totalLayers} | 节点 {visited}/{total} | 类型: {current?.nodeType}";
    }

    int CountAllNodes()
    {
        int count = 0;
        foreach (var layer in CurrentMap.layers)
            count += layer.Count;
        return count;
    }
}
