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
/// BE-08 肉鸽地图路径系统 / BE-11 增强路径生成
/// 15层地图生成算法，节点连接，路径选择驱动状态机
/// 支持：JSON配置驱动、路径分叉保证、难度缩放、特殊规则
/// </summary>
public class RoguelikeMapSystem
{
    public static RoguelikeMapSystem Instance { get; private set; }

    public MapData CurrentMap { get; private set; }
    public string CurrentNodeId => CurrentMap?.currentNodeId;

    // 事件
    public event System.Action<MapData> OnMapGenerated;
    public event System.Action<MapNode> OnNodeSelected;

    // ===== 配置（从JSON加载，仍可外部覆盖）=====
    public int totalLayers = 15;
    public int minNodesPerLayer = 2;
    public int maxNodesPerLayer = 4;
    public int bossInterval = 5;
    public int maxConnectionsPerNode = 3;

    // 节点类型权重（非Boss层）— 保留作为fallback
    private readonly Dictionary<MapNodeType, float> nodeWeights = new Dictionary<MapNodeType, float>
    {
        { MapNodeType.Battle,   0.40f },
        { MapNodeType.Elite,    0.10f },
        { MapNodeType.Event,    0.20f },
        { MapNodeType.Shop,     0.15f },
        { MapNodeType.Rest,     0.10f },
        { MapNodeType.Treasure, 0.05f },
    };

    // ===== BE-11 新增缓存 =====
    private MapGenerationConfig _mapGenConfig;
    private List<int> _forkLayers = new List<int>();
    private List<int> _convergenceLayers = new List<int>();
    private RoguelikeMapSpecialRulesConfig _specialRules;

    public RoguelikeMapSystem()
    {
        if (Instance != null)
            Debug.LogWarning("[RoguelikeMapSystem] 实例已存在，覆盖");
        Instance = this;
    }

    // ===== 配置加载 =====

    /// <summary>
    /// 从JSON加载地图生成配置（带fallback）
    /// </summary>
    void LoadConfigFromJson()
    {
        try
        {
            _mapGenConfig = BalanceProvider.GetMapGenerationConfig();
            if (_mapGenConfig != null)
            {
                // 只在第一次或配置改变时更新
                totalLayers = _mapGenConfig.total_layers;
                minNodesPerLayer = _mapGenConfig.min_nodes_per_layer;
                maxNodesPerLayer = _mapGenConfig.max_nodes_per_layer;
                bossInterval = _mapGenConfig.boss_interval;
                maxConnectionsPerNode = _mapGenConfig.max_connections_per_node;

                _forkLayers = _mapGenConfig.fork_layers ?? new List<int>();
                _convergenceLayers = _mapGenConfig.convergence_layers ?? new List<int>();

                Debug.Log($"[RoguelikeMapSystem] JSON配置加载成功: {totalLayers}层, " +
                    $"分叉层[{string.Join(",", _forkLayers)}], 收敛层[{string.Join(",", _convergenceLayers)}]");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RoguelikeMapSystem] JSON配置加载失败，使用默认值: {e.Message}");
            _forkLayers = new List<int> { 2, 4, 7, 9, 12 };
            _convergenceLayers = new List<int> { 4, 9, 14 };
        }

        _specialRules = BalanceProvider.GetMapSpecialRules();
    }

    // ===== 地图生成 =====

    /// <summary>
    /// 生成完整地图（新游戏或到达新区间时调用）
    /// </summary>
    public MapData GenerateMap(int totalLevels = 15)
    {
        // BE-11: 从JSON加载配置
        LoadConfigFromJson();

        totalLayers = totalLevels;
        CurrentMap = new MapData { totalLayers = totalLayers };

        for (int layerIdx = 0; layerIdx < totalLayers; layerIdx++)
        {
            var layer = GenerateLayer(layerIdx);
            CurrentMap.layers.Add(layer);
        }

        // 生成连接
        GenerateConnections();

        // BE-11: 应用路径分叉保证
        ApplyForkGuarantees();

        // BE-11: 应用特殊规则
        ApplySpecialRules();

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

        // BE-11: 分叉层保证最少节点数
        int minNodes = minNodesPerLayer;
        int maxNodes = maxNodesPerLayer;
        if (_forkLayers.Contains(layerIdx))
        {
            int forkMin = _mapGenConfig?.fork_min_paths ?? 2;
            int forkMax = _mapGenConfig?.fork_max_paths ?? 3;
            minNodes = Mathf.Max(minNodes, forkMin);
            maxNodes = Mathf.Max(maxNodes, forkMin);
        }

        // 随机节点数
        int nodeCount = Random.Range(minNodes, maxNodes + 1);

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
    /// 按权重随机选择节点类型（BE-11: 使用JSON配置的阶段权重）
    /// </summary>
    MapNodeType PickNodeType(int layerIdx)
    {
        // BE-11: 从JSON获取权重
        var jsonWeights = BalanceProvider.GetNodeWeightsForLayer(layerIdx);

        // 构建权重表
        var weights = new Dictionary<MapNodeType, float>();

        foreach (var kv in jsonWeights)
        {
            MapNodeType? nt = ParseNodeTypeName(kv.Key);
            if (nt.HasValue)
                weights[nt.Value] = kv.Value;
        }

        // 如果JSON权重为空，使用fallback
        if (weights.Count == 0)
            weights = new Dictionary<MapNodeType, float>(nodeWeights);

        // 前2层不出Elite和Treasure
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
        if (total <= 0) return MapNodeType.Battle;
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

    /// <summary>
    /// 将字符串节点类型名转为枚举
    /// </summary>
    MapNodeType? ParseNodeTypeName(string name)
    {
        switch (name)
        {
            case "Battle": return MapNodeType.Battle;
            case "Elite": return MapNodeType.Elite;
            case "Event": return MapNodeType.Event;
            case "Shop": return MapNodeType.Shop;
            case "Rest": return MapNodeType.Rest;
            case "Treasure": return MapNodeType.Treasure;
            case "Boss": return MapNodeType.Boss;
            default: return null;
        }
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

    void DisconnectNodes(MapNode from, MapNode to)
    {
        from.nextNodeIds.Remove(to.nodeId);
        to.prevNodeIds.Remove(from.nodeId);
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

    // ===== BE-11: 路径分叉保证 =====

    /// <summary>
    /// 确保分叉层有2-3条独立路径，且节点类型有足够多样性
    /// </summary>
    void ApplyForkGuarantees()
    {
        if (CurrentMap == null || CurrentMap.layers.Count == 0) return;

        foreach (int forkLayerIdx in _forkLayers)
        {
            // 分叉层必须在有效范围内且不是Boss层
            if (forkLayerIdx <= 0 || forkLayerIdx >= totalLayers - 1) continue;
            if (IsBossLayer(forkLayerIdx)) continue;

            var forkLayer = CurrentMap.layers[forkLayerIdx];

            int forkMin = _mapGenConfig?.fork_min_paths ?? 2;
            int forkMax = _mapGenConfig?.fork_max_paths ?? 3;
            int desiredPaths = Random.Range(forkMin, forkMax + 1);

            // 确保该层有足够节点
            if (forkLayer.Count < desiredPaths)
            {
                // 需要添加节点
                while (forkLayer.Count < desiredPaths)
                {
                    int newIdx = forkLayer.Count;
                    var nodeType = PickNodeType(forkLayerIdx);
                    var node = CreateNode(forkLayerIdx, newIdx, nodeType);
                    node.difficulty = CalculateDifficulty(forkLayerIdx, nodeType);
                    node.previewText = GeneratePreviewText(nodeType, node.difficulty);
                    forkLayer.Add(node);
                }
            }

            // 确保每个分叉节点连接到下一层的不同子集（路径独立性）
            EnsurePathDiversity(forkLayerIdx, forkLayer);

            Debug.Log($"[RoguelikeMapSystem] 分叉保证: 层{forkLayerIdx + 1}, {forkLayer.Count}条路径");
        }

        // 收敛层：确保路径在Boss前收敛
        foreach (int convLayerIdx in _convergenceLayers)
        {
            if (convLayerIdx <= 0 || convLayerIdx >= totalLayers - 1) continue;
            if (IsBossLayer(convLayerIdx)) continue;

            // 收敛层节点数不超过2，连接指向单一目标
            var convLayer = CurrentMap.layers[convLayerIdx];
            if (convLayer.Count > 2)
            {
                // 保留首尾两个节点
                while (convLayer.Count > 2)
                {
                    int removeIdx = Random.Range(1, convLayer.Count - 1);
                    var removed = convLayer[removeIdx];
                    // 清除连接
                    foreach (var nextId in removed.nextNodeIds.ToList())
                    {
                        var nextNode = CurrentMap.layers[convLayerIdx + 1]
                            .FirstOrDefault(n => n.nodeId == nextId);
                        if (nextNode != null)
                            DisconnectNodes(removed, nextNode);
                    }
                    foreach (var prevId in removed.prevNodeIds.ToList())
                    {
                        var prevNode = CurrentMap.layers[convLayerIdx - 1]
                            .FirstOrDefault(n => n.nodeId == prevId);
                        if (prevNode != null)
                            DisconnectNodes(prevNode, removed);
                    }
                    convLayer.RemoveAt(removeIdx);
                }
                // 重新编号
                for (int i = 0; i < convLayer.Count; i++)
                {
                    convLayer[i].indexInLayer = i;
                    convLayer[i].nodeId = $"node_{convLayerIdx}_{i}";
                }
            }

            Debug.Log($"[RoguelikeMapSystem] 收敛层: 层{convLayerIdx + 1}, {convLayer.Count}个节点");
        }
    }

    /// <summary>
    /// 确保分叉层中每个节点到下一层有独立路径
    /// </summary>
    void EnsurePathDiversity(int forkLayerIdx, List<MapNode> forkLayer)
    {
        if (forkLayerIdx >= totalLayers - 1) return;
        var nextLayer = CurrentMap.layers[forkLayerIdx + 1];
        if (nextLayer.Count == 0) return;

        // 为每个分叉节点分配至少一个独立的下一层连接目标
        for (int i = 0; i < forkLayer.Count; i++)
        {
            var node = forkLayer[i];

            // 如果该节点没有任何连接到下一层
            bool hasConnection = node.nextNodeIds.Any(id =>
            {
                var n = CurrentMap.layers[forkLayerIdx + 1].FirstOrDefault(nn => nn.nodeId == id);
                return n != null;
            });

            if (!hasConnection && nextLayer.Count > 0)
            {
                // 分配到最近的下一层节点
                int targetIdx = Mathf.Min(i, nextLayer.Count - 1);
                ConnectNodes(node, nextLayer[targetIdx]);
            }
        }
    }

    // ===== BE-11: 特殊规则 =====

    /// <summary>
    /// 应用特殊规则：商店/精英保底、Boss后休息、最少休息点、连续战斗上限
    /// </summary>
    void ApplySpecialRules()
    {
        if (CurrentMap == null || CurrentMap.layers.Count == 0) return;

        int firstShopLayer = _specialRules?.first_shop_guaranteed_layer ?? 2;
        int firstEliteLayer = _specialRules?.first_elite_guaranteed_layer ?? 3;
        bool restAfterBoss = _specialRules?.rest_after_boss ?? true;
        int minRestCount = _specialRules?.min_rest_count ?? 2;
        int maxConsecutive = _specialRules?.max_consecutive_battles ?? 3;

        // 1. 第N层保底商店
        ApplyGuaranteedNodeType(firstShopLayer - 1, MapNodeType.Shop); // layer is 0-based

        // 2. 第N层保底精英
        ApplyGuaranteedNodeType(firstEliteLayer - 1, MapNodeType.Elite);

        // 3. Boss后休息点
        if (restAfterBoss)
        {
            for (int i = 0; i < totalLayers - 1; i++)
            {
                if (IsBossLayer(i) && i + 1 < totalLayers && !IsBossLayer(i + 1))
                {
                    var nextLayer = CurrentMap.layers[i + 1];
                    bool hasRest = nextLayer.Any(n => n.nodeType == MapNodeType.Rest);
                    if (!hasRest && nextLayer.Count > 0)
                    {
                        // 将第一个非Boss非Rest节点改为Rest
                        var target = nextLayer.FirstOrDefault(n =>
                            n.nodeType != MapNodeType.Boss && n.nodeType != MapNodeType.Rest);
                        if (target != null)
                        {
                            target.nodeType = MapNodeType.Rest;
                            target.previewText = GeneratePreviewText(MapNodeType.Rest, target.difficulty);
                        }
                    }
                }
            }
        }

        // 4. 最少休息点数量
        int restCount = 0;
        foreach (var layer in CurrentMap.layers)
            restCount += layer.Count(n => n.nodeType == MapNodeType.Rest);

        if (restCount < minRestCount)
        {
            // 在合适的层添加更多休息点
            for (int i = 1; i < totalLayers && restCount < minRestCount; i++)
            {
                if (IsBossLayer(i)) continue;
                var layer = CurrentMap.layers[i];
                // 优先改Battle节点为Rest（避免改已经多样化的层）
                var battleNode = layer.FirstOrDefault(n => n.nodeType == MapNodeType.Battle);
                if (battleNode != null && layer.Count > 1)
                {
                    battleNode.nodeType = MapNodeType.Rest;
                    battleNode.previewText = GeneratePreviewText(MapNodeType.Rest, battleNode.difficulty);
                    restCount++;
                }
            }
        }

        // 5. 最大连续战斗限制
        int consecutiveBattles = 0;
        for (int i = 0; i < totalLayers; i++)
        {
            var layer = CurrentMap.layers[i];
            bool layerHasBattle = layer.Any(n => n.nodeType == MapNodeType.Battle || n.nodeType == MapNodeType.Elite);

            if (layerHasBattle)
            {
                consecutiveBattles++;
                if (consecutiveBattles > maxConsecutive)
                {
                    // 将该层一个Battle节点改为非战斗类型
                    var battleNode = layer.FirstOrDefault(n =>
                        n.nodeType == MapNodeType.Battle || n.nodeType == MapNodeType.Elite);
                    if (battleNode != null)
                    {
                        battleNode.nodeType = Random.value < 0.5f ? MapNodeType.Event : MapNodeType.Rest;
                        battleNode.previewText = GeneratePreviewText(battleNode.nodeType, battleNode.difficulty);
                        consecutiveBattles = 0;
                    }
                }
            }
            else
            {
                consecutiveBattles = 0;
            }
        }
    }

    /// <summary>
    /// 在指定层确保存在某类型的节点
    /// </summary>
    void ApplyGuaranteedNodeType(int layerIdx, MapNodeType guaranteedType)
    {
        if (layerIdx < 0 || layerIdx >= totalLayers) return;
        if (IsBossLayer(layerIdx)) return;

        var layer = CurrentMap.layers[layerIdx];
        bool hasType = layer.Any(n => n.nodeType == guaranteedType);

        if (!hasType && layer.Count > 0)
        {
            // 将第一个Battle节点改为目标类型
            var target = layer.FirstOrDefault(n => n.nodeType == MapNodeType.Battle);
            if (target != null)
            {
                target.nodeType = guaranteedType;
                target.previewText = GeneratePreviewText(guaranteedType, target.difficulty);
            }
        }
    }

    // ===== BE-11: 难度倍率 =====

    /// <summary>
    /// 获取指定层的敌人属性倍率
    /// </summary>
    public float GetEnemyHpMultiplier(int layer)
    {
        return BalanceProvider.GetEnemyHpMultiplier(layer);
    }

    public float GetEnemyAtkMultiplier(int layer)
    {
        return BalanceProvider.GetEnemyAtkMultiplier(layer);
    }

    /// <summary>
    /// 获取指定层的完整难度倍率信息
    /// </summary>
    public Dictionary<string, float> GetDifficultyMultiplier(int layer)
    {
        float hpMult = BalanceProvider.GetEnemyHpMultiplier(layer);
        float atkMult = BalanceProvider.GetEnemyAtkMultiplier(layer);

        var result = new Dictionary<string, float>
        {
            { "hp_multiplier", hpMult },
            { "atk_multiplier", atkMult },
            { "rarity_boost", layer * 0.02f }
        };

        // 精英加成
        var scaling = BalanceProvider.RoguelikeMapConfig?.difficulty_scaling;
        if (scaling != null)
        {
            result["elite_hp_multiplier"] = hpMult * scaling.elite_bonus_multiplier;
            result["elite_atk_multiplier"] = atkMult * scaling.elite_bonus_multiplier;
            result["boss_hp_multiplier"] = hpMult * scaling.boss_bonus_multiplier;
            result["boss_atk_multiplier"] = atkMult * scaling.boss_bonus_multiplier;
            result["rarity_boost"] = layer * scaling.rarity_boost_per_layer;
        }
        else
        {
            result["elite_hp_multiplier"] = hpMult * 1.5f;
            result["elite_atk_multiplier"] = atkMult * 1.5f;
            result["boss_hp_multiplier"] = hpMult * 2.0f;
            result["boss_atk_multiplier"] = atkMult * 2.0f;
        }

        return result;
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
    /// <summary>供 SaveSystem 恢复存档后刷新可达节点（public）</summary>
    public void RefreshAvailableNodes() => MarkAvailableNodes();

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
