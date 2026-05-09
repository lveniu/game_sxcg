using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    // ═══════════════════════════════════════════════════════════
    // FE-08: 肉鸽地图路径UI — 分层节点图 + 路径连线 + 选择交互
    // ═══════════════════════════════════════════════════════════
    //
    // 布局（竖屏 720×1280）：
    // ┌──────────────────────────────────────┐
    // │  [返回] 层数 3/15    [遗物] [背包]   │  顶部栏
    // ├──────────────────────────────────────┤
    // │                                      │
    // │         ○─────○                      │  Layer 0 (起始)
    // │              │                       │
    // │    ○───○────○────○───○              │  Layer N (可选)
    // │         ↑                            │
    // │      [当前位置]                       │
    // ├──────────────────────────────────────┤
    // │  [节点信息栏：类型/难度/预览]          │  底部信息
    // │  [确认前往]                           │
    // └──────────────────────────────────────┘
    //
    // 交互：
    // 1. 地图垂直滚动（ScrollRect），自动定位到当前层
    // 2. 可选节点：金色描边 + 呼吸动画
    // 3. 已访问节点：绿色勾 + 半透明
    // 4. 不可达节点：暗灰色
    // 5. 点击可选节点 → 底部信息栏更新 → 确认按钮
    // 6. 确认 → SelectNode(nodeId) → 状态机切换
    //
    // 三层动画安全：
    // - 全部 tween 加 .SetLink(gameObject)
    // - 基类 UIPanel.Hide() 兜底 DOTween.Kill(gameObject)
    // - OnComplete 闭包加 null guard
    // ═══════════════════════════════════════════════════════════

    /// <summary>地图节点类型（与后端 MapNodeType 对齐）</summary>
    public enum MapNodeType
    {
        Battle,      // 普通战斗
        Elite,       // 精英战斗（高奖励）
        Event,       // 随机事件
        Shop,        // 商店
        Rest,        // 休息点（回复生命）
        Boss,        // Boss关（每5关强制）
        Treasure     // 宝箱
    }

    /// <summary>地图节点数据（Mock版，后端就绪后替换为后端类）</summary>
    public class MapNode
    {
        public string nodeId;
        public int layer;
        public int indexInLayer;
        public MapNodeType nodeType;
        public bool isVisited;
        public bool isAvailable;
        public List<string> nextNodeIds = new List<string>();
        public List<string> prevNodeIds = new List<string>();
        public string previewText;
        public int difficulty;
    }

    /// <summary>地图整体数据（Mock版）</summary>
    public class MapData
    {
        public List<List<MapNode>> layers = new List<List<MapNode>>();
        public string currentNodeId;
        public int totalLayers;
    }

    /// <summary>
    /// FE-08 肉鸽地图路径面板
    /// 子面板模式：从 SettlementPanel 或 RoguelikeRewardPanel 打开
    /// </summary>
    public class RoguelikeMapPanel : UIPanel
    {
        // ──────── 布局常量 ────────
        private const float CANVAS_WIDTH = 720f;
        private const float LAYER_HEIGHT = 180f;      // 层间距
        private const float NODE_SIZE = 64f;           // 节点大小
        private const float NODE_SPACING = 130f;       // 同层节点间距
        private const float LINE_THICKNESS = 3f;       // 连线粗细
        private const float PADDING_TOP = 100f;        // 内容区顶部留白
        private const float BREATH_DURATION = 1.5f;    // 呼吸动画周期

        // ──────── 节点颜色映射 ────────
        private static readonly Color COLOR_BATTLE    = HexColor("#FF6B6B");
        private static readonly Color COLOR_ELITE     = HexColor("#FF4500");
        private static readonly Color COLOR_EVENT     = HexColor("#FFD700");
        private static readonly Color COLOR_SHOP      = HexColor("#4ECDC4");
        private static readonly Color COLOR_REST      = HexColor("#95E1D3");
        private static readonly Color COLOR_BOSS      = HexColor("#8B0000");
        private static readonly Color COLOR_TREASURE  = HexColor("#FF69B4");

        private static readonly Color COLOR_AVAILABLE  = HexColor("#FFD700"); // 金色描边
        private static readonly Color COLOR_VISITED    = new Color(0.5f, 0.8f, 0.5f, 0.6f);
        private static readonly Color COLOR_LOCKED     = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        private static readonly Color COLOR_LINE_WALKED = HexColor("#FFD700");
        private static readonly Color COLOR_LINE_FUTURE = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // ──────── Inspector 引用 ────────
        [Header("顶部栏")]
        public Text layerProgressText;
        public Button backButton;
        public Button relicButton;
        public Button inventoryButton;

        [Header("地图区域")]
        public ScrollRect mapScrollRect;
        public RectTransform mapContent;      // ScrollRect 的 content

        [Header("底部信息栏")]
        public RectTransform bottomInfoBar;
        public Text nodeTypeText;
        public Text nodePreviewText;
        public Text nodeDifficultyText;
        public Button confirmButton;

        // ──────── 运行时状态 ────────
        private MapData currentMapData;
        private string selectedNodeId;
        private readonly Dictionary<string, RectTransform> nodeRects = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, MapNode> nodeLookup = new Dictionary<string, MapNode>();
        private readonly List<RectTransform> lineRects = new List<RectTransform>();

        // ══════════════════════════════════════
        // 生命周期
        // ══════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            slideInAnimation = false; // 子面板不用滑入

            confirmButton?.onClick.AddListener(OnConfirmClicked);
            backButton?.onClick.AddListener(OnBackClicked);
        }

        public override void Show()
        {
            base.Show();
            LoadMapAndRender();
        }

        public override void OnShow()
        {
            // 默认隐藏底部信息栏
            if (bottomInfoBar != null) bottomInfoBar.gameObject.SetActive(false);
        }

        public override void OnHide()
        {
            ClearMap();
        }

        // ══════════════════════════════════════
        // 数据加载（Mock先行，后端就绪后替换）
        // ══════════════════════════════════════

        /// <summary>加载地图数据并渲染</summary>
        private void LoadMapAndRender()
        {
            // TODO: 后端就绪后替换为 RoguelikeMapSystem.Instance.GenerateMap()
            currentMapData = GenerateMockMap(15);

            // 构建节点查找表
            nodeLookup.Clear();
            foreach (var layer in currentMapData.layers)
            {
                foreach (var node in layer)
                {
                    nodeLookup[node.nodeId] = node;
                }
            }

            RenderMap();
            ScrollToCurrentLayer();
        }

        /// <summary>
        /// Mock地图生成 — 15层，每层2-4个节点，随机类型
        /// </summary>
        private MapData GenerateMockMap(int totalLayers)
        {
            var map = new MapData
            {
                totalLayers = totalLayers,
                currentNodeId = "node_0_0"
            };

            var rand = new System.Random(42); // 固定种子方便测试

            for (int i = 0; i < totalLayers; i++)
            {
                int nodeCount = (i == 0) ? 1 : (i == totalLayers - 1) ? 1 : rand.Next(2, 5);
                var layer = new List<MapNode>();

                for (int j = 0; j < nodeCount; j++)
                {
                    var node = new MapNode
                    {
                        nodeId = $"node_{i}_{j}",
                        layer = i,
                        indexInLayer = j,
                        nodeType = GetRandomNodeType(rand, i, totalLayers),
                        isVisited = (i == 0),      // 起始点已访问
                        isAvailable = (i == 1),     // 第1层可选
                        previewText = "",
                        difficulty = Mathf.Clamp(i / 3 + 1, 1, 5)
                    };
                    node.previewText = GetPreviewText(node.nodeType, node.difficulty);
                    layer.Add(node);
                }
                map.layers.Add(layer);
            }

            // 生成连接关系
            for (int i = 0; i < totalLayers - 1; i++)
            {
                var currentLayer = map.layers[i];
                var nextLayer = map.layers[i + 1];

                foreach (var node in currentLayer)
                {
                    // 每个节点连接1-2个下一层节点
                    int connections = rand.Next(1, Mathf.Min(3, nextLayer.Count + 1));
                    for (int c = 0; c < connections; c++)
                    {
                        int targetIdx = rand.Next(0, nextLayer.Count);
                        var target = nextLayer[targetIdx];
                        if (!node.nextNodeIds.Contains(target.nodeId))
                        {
                            node.nextNodeIds.Add(target.nodeId);
                            target.prevNodeIds.Add(node.nodeId);
                        }
                    }
                }

                // 确保下一层每个节点至少有一个前驱
                foreach (var nextNode in nextLayer)
                {
                    if (nextNode.prevNodeIds.Count == 0)
                    {
                        int sourceIdx = rand.Next(0, currentLayer.Count);
                        var source = currentLayer[sourceIdx];
                        nextNode.prevNodeIds.Add(source.nodeId);
                        source.nextNodeIds.Add(nextNode.nodeId);
                    }
                }
            }

            return map;
        }

        private static MapNodeType GetRandomNodeType(System.Random rand, int layer, int total)
        {
            // Boss 每5层
            if (layer > 0 && layer % 5 == 0 && layer < total - 1) return MapNodeType.Boss;
            // 第0层总是起始(用Rest表示)
            if (layer == 0) return MapNodeType.Rest;

            double r = rand.NextDouble();
            if (r < 0.35) return MapNodeType.Battle;
            if (r < 0.50) return MapNodeType.Elite;
            if (r < 0.65) return MapNodeType.Event;
            if (r < 0.78) return MapNodeType.Shop;
            if (r < 0.90) return MapNodeType.Rest;
            return MapNodeType.Treasure;
        }

        private static string GetPreviewText(MapNodeType type, int difficulty)
        {
            return type switch
            {
                MapNodeType.Battle   => $"⚔ {difficulty}星敌人",
                MapNodeType.Elite    => $"💀 {difficulty}星精英",
                MapNodeType.Event    => "❓ 随机事件",
                MapNodeType.Shop     => "🛒 商店",
                MapNodeType.Rest     => "⛺ 休息点",
                MapNodeType.Boss     => $"👹 Boss (Lv.{difficulty})",
                MapNodeType.Treasure => "🎁 宝箱",
                _ => "???"
            };
        }

        // ══════════════════════════════════════
        // 地图渲染
        // ══════════════════════════════════════

        private void RenderMap()
        {
            ClearMap();

            if (currentMapData == null) return;

            int totalLayers = currentMapData.totalLayers;
            float contentHeight = PADDING_TOP + totalLayers * LAYER_HEIGHT + 200f;
            mapContent.sizeDelta = new Vector2(CANVAS_WIDTH, contentHeight);

            // 1. 先画连线（在节点下面）
            RenderLines();

            // 2. 再画节点
            foreach (var layer in currentMapData.layers)
            {
                foreach (var node in layer)
                {
                    RenderNode(node);
                }
            }

            // 3. 更新顶部进度
            UpdateProgressText();

            // 4. 入场动画
            PlayEntryAnimation();
        }

        /// <summary>渲染单个节点</summary>
        private void RenderNode(MapNode node)
        {
            // 创建节点 GameObject
            var go = new GameObject($"Node_{node.nodeId}");
            go.transform.SetParent(mapContent, false);

            var rect = go.AddComponent<RectTransform>();
            Vector2 pos = GetNodePosition(node);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 背景圆形
            var bgImage = go.AddComponent<Image>();
            bgImage.color = GetNodeColor(node);
            bgImage.raycastTarget = true;

            // 按钮组件
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnNodeClicked(node.nodeId));

            // 节点类型图标（文字模拟）
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGo.AddComponent<Text>();
            iconText.text = GetNodeIcon(node.nodeType);
            iconText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 28;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.raycastTarget = false;

            // 难度星级（小字）
            if (node.difficulty > 0)
            {
                var diffGo = new GameObject("Difficulty");
                diffGo.transform.SetParent(go.transform, false);
                var diffRect = diffGo.AddComponent<RectTransform>();
                diffRect.anchorMin = new Vector2(0, 0);
                diffRect.anchorMax = new Vector2(1, 0.4f);
                diffRect.offsetMin = Vector2.zero;
                diffRect.offsetMax = Vector2.zero;
                var diffText = diffGo.AddComponent<Text>();
                diffText.text = new string('★', node.difficulty);
                diffText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
                diffText.fontSize = 10;
                diffText.alignment = TextAnchor.MiddleCenter;
                diffText.color = Color.yellow;
                diffText.raycastTarget = false;
            }

            // 状态装饰
            if (node.isVisited)
            {
                ApplyVisitedStyle(rect, bgImage);
            }
            else if (node.isAvailable)
            {
                ApplyAvailableStyle(rect);
            }
            else
            {
                ApplyLockedStyle(bgImage);
            }

            // 记录
            nodeRects[node.nodeId] = rect;
        }

        /// <summary>渲染所有路径连线</summary>
        private void RenderLines()
        {
            foreach (var layer in currentMapData.layers)
            {
                foreach (var node in layer)
                {
                    if (node.nextNodeIds == null) continue;
                    foreach (var nextId in node.nextNodeIds)
                    {
                        if (!nodeLookup.TryGetValue(nextId, out var nextNode)) continue;
                        DrawLine(node, nextNode);
                    }
                }
            }
        }

        /// <summary>画两点之间的连线（Image + 旋转拉伸）</summary>
        private void DrawLine(MapNode from, MapNode to)
        {
            var go = new GameObject($"Line_{from.nodeId}_{to.nodeId}");
            go.transform.SetParent(mapContent, false);

            var rect = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            // 路径样式：已走过=金色实线，未走=灰色半透明
            bool walked = from.isVisited;
            image.color = walked ? COLOR_LINE_WALKED : COLOR_LINE_FUTURE;

            // 计算起点终点
            Vector2 fromPos = GetNodePosition(from);
            Vector2 toPos = GetNodePosition(to);
            Vector2 delta = toPos - fromPos;
            float distance = delta.magnitude;
            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

            // 设置位置和旋转
            rect.sizeDelta = new Vector2(LINE_THICKNESS, distance);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = fromPos;
            rect.localEulerAngles = new Vector3(0, 0, -angle);

            lineRects.Add(rect);
        }

        /// <summary>清除地图（节点+连线）</summary>
        private void ClearMap()
        {
            foreach (var kvp in nodeRects)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            nodeRects.Clear();

            foreach (var line in lineRects)
            {
                if (line != null) Destroy(line.gameObject);
            }
            lineRects.Clear();

            nodeLookup.Clear();
            selectedNodeId = null;
        }

        // ══════════════════════════════════════
        // 节点位置计算
        // ══════════════════════════════════════

        private Vector2 GetNodePosition(MapNode node)
        {
            var layer = currentMapData.layers[node.layer];
            int count = layer.Count;

            // 居中分布
            float totalWidth = (count - 1) * NODE_SPACING;
            float startX = (CANVAS_WIDTH - totalWidth) / 2f;

            float x = startX + node.indexInLayer * NODE_SPACING;
            float y = contentHeight - PADDING_TOP - node.layer * LAYER_HEIGHT;

            return new Vector2(x, y);
        }

        private float contentHeight =>
            mapContent != null ? mapContent.sizeDelta.y : 3000f;

        // ══════════════════════════════════════
        // 节点样式
        // ══════════════════════════════════════

        private Color GetNodeColor(MapNode node)
        {
            return node.nodeType switch
            {
                MapNodeType.Battle   => COLOR_BATTLE,
                MapNodeType.Elite    => COLOR_ELITE,
                MapNodeType.Event    => COLOR_EVENT,
                MapNodeType.Shop     => COLOR_SHOP,
                MapNodeType.Rest     => COLOR_REST,
                MapNodeType.Boss     => COLOR_BOSS,
                MapNodeType.Treasure => COLOR_TREASURE,
                _ => Color.gray
            };
        }

        private static string GetNodeIcon(MapNodeType type)
        {
            return type switch
            {
                MapNodeType.Battle   => "⚔",
                MapNodeType.Elite    => "💀",
                MapNodeType.Event    => "?",
                MapNodeType.Shop     => "$",
                MapNodeType.Rest     => "⛺",
                MapNodeType.Boss     => "👹",
                MapNodeType.Treasure => "🎁",
                _ => "?"
            };
        }

        private void ApplyVisitedStyle(RectTransform rect, Image bg)
        {
            // 半透明 + 绿色调
            bg.color = new Color(0.5f, 0.8f, 0.5f, 0.6f);

            // 添加勾号标记
            var checkGo = new GameObject("Check");
            checkGo.transform.SetParent(rect, false);
            var checkRect = checkGo.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.6f, 0.6f);
            checkRect.anchorMax = new Vector2(1f, 1f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            var checkText = checkGo.AddComponent<Text>();
            checkText.text = "✓";
            checkText.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            checkText.fontSize = 16;
            checkText.alignment = TextAnchor.MiddleCenter;
            checkText.color = Color.green;
            checkText.raycastTarget = false;
        }

        private void ApplyAvailableStyle(RectTransform rect)
        {
            // 金色描边效果：外层放大一点的半透明金色圈
            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(rect, false);
            var outlineRect = outlineGo.AddComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = new Vector2(-6f, -6f);
            outlineRect.offsetMax = new Vector2(6f, 6f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            var outlineImage = outlineGo.AddComponent<Image>();
            outlineImage.color = COLOR_AVAILABLE;
            outlineImage.raycastTarget = false;
            outlineGo.transform.SetAsFirstSibling();

            // 呼吸动画
            rect.localScale = Vector3.one;
            rect.DOScale(new Vector3(1.05f, 1.05f, 1f), BREATH_DURATION)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void ApplyLockedStyle(Image bg)
        {
            var c = bg.color;
            bg.color = new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, 0.5f);
        }

        // ══════════════════════════════════════
        // 交互逻辑
        // ══════════════════════════════════════

        private void OnNodeClicked(string nodeId)
        {
            if (!nodeLookup.TryGetValue(nodeId, out var node)) return;

            // 只允许点击可选节点
            if (!node.isAvailable)
            {
                // 不可选时抖动反馈
                if (nodeRects.TryGetValue(nodeId, out var rect))
                {
                    rect.DOShakeAnchorPos(0.2f, 5f, 20, 90f, false, true, ShakeRandomnessMode.Harmonic)
                        .SetLink(gameObject);
                }
                return;
            }

            // 取消之前的选中
            if (!string.IsNullOrEmpty(selectedNodeId) && nodeRects.TryGetValue(selectedNodeId, out var prevRect))
            {
                prevRect.DOKill();
                prevRect.localScale = Vector3.one;
            }

            // 选中当前节点
            selectedNodeId = nodeId;
            if (nodeRects.TryGetValue(nodeId, out var selectedRect))
            {
                selectedRect.DOScale(new Vector3(1.15f, 1.15f, 1f), 0.15f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);
            }

            // 更新底部信息栏
            UpdateBottomInfo(node);
        }

        private void UpdateBottomInfo(MapNode node)
        {
            if (bottomInfoBar == null) return;

            bottomInfoBar.gameObject.SetActive(true);

            if (nodeTypeText != null)
                nodeTypeText.text = $"{GetNodeIcon(node.nodeType)} {node.nodeType}";

            if (nodePreviewText != null)
                nodePreviewText.text = node.previewText;

            if (nodeDifficultyText != null)
                nodeDifficultyText.text = $"难度: {new string('★', node.difficulty)}{new string('☆', 5 - node.difficulty)}";

            // 底部栏弹出动画
            bottomInfoBar.anchoredPosition = new Vector2(0, -200f);
            bottomInfoBar.DOAnchorPosY(0f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(selectedNodeId)) return;
            if (!nodeLookup.TryGetValue(selectedNodeId, out var node)) return;

            // TODO: 后端就绪后替换为 RoguelikeMapSystem.Instance.SelectNode(selectedNodeId)
            Debug.Log($"[MapPanel] 选择节点: {selectedNodeId} ({node.nodeType})");

            // 确认动画：选中节点放大脉冲 → 非选中淡出 → 过渡
            PlayConfirmAnimation(node, () =>
            {
                // 根据节点类型切换状态
                TransitionByNodeType(node);
            });
        }

        private void OnBackClicked()
        {
            Hide();
        }

        /// <summary>根据节点类型切换到对应游戏阶段</summary>
        private void TransitionByNodeType(MapNode node)
        {
            // TODO: 后端联调时完善状态切换逻辑
            switch (node.nodeType)
            {
                case MapNodeType.Battle:
                case MapNodeType.Elite:
                case MapNodeType.Boss:
                    // 战斗类 → DiceRoll
                    GameStateMachine.Instance?.ChangeState(GameState.DiceRoll);
                    break;

                case MapNodeType.Event:
                    // 事件 → 显示EventPanel子面板
                    NewUIManager.Instance?.ShowSubPanel("Event");
                    break;

                case MapNodeType.Shop:
                    // 商店 → 显示ShopPanel子面板
                    NewUIManager.Instance?.ShowSubPanel("Shop");
                    break;

                case MapNodeType.Rest:
                case MapNodeType.Treasure:
                    // 休息/宝箱 → 直接进下一关DiceRoll
                    GameStateMachine.Instance?.ChangeState(GameState.DiceRoll);
                    break;
            }
        }

        // ══════════════════════════════════════
        // 滚动定位
        // ══════════════════════════════════════

        private void ScrollToCurrentLayer()
        {
            if (currentMapData == null || mapScrollRect == null) return;

            // 找到当前节点所在层
            string currentId = currentMapData.currentNodeId;
            if (!nodeLookup.TryGetValue(currentId, out var currentNode)) return;

            float contentH = mapContent.sizeDelta.y;
            float targetY = PADDING_TOP + (currentMapData.totalLayers - currentNode.layer - 1) * LAYER_HEIGHT;
            float normalizedPos = Mathf.Clamp01(targetY / contentH);

            // 延迟滚动（等渲染完成）
            DOVirtual.DelayedCall(0.3f, () =>
            {
                if (mapScrollRect != null)
                    mapScrollRect.verticalNormalizedPosition = normalizedPos;
            });
        }

        // ══════════════════════════════════════
        // 动画
        // ══════════════════════════════════════

        /// <summary>入场动画：节点从底部依次飞入</summary>
        private void PlayEntryAnimation()
        {
            foreach (var layer in currentMapData.layers)
            {
                int layerIdx = layer[0].layer;
                foreach (var node in layer)
                {
                    if (!nodeRects.TryGetValue(node.nodeId, out var rect)) continue;

                    rect.anchoredPosition -= new Vector2(0, 80f);
                    rect.localScale = Vector3.zero;
                    rect.DOAnchorPos(GetNodePosition(node), 0.5f)
                        .SetDelay(layerIdx * 0.08f)
                        .SetEase(Ease.OutBack)
                        .SetLink(gameObject);
                    rect.DOScale(Vector3.one, 0.4f)
                        .SetDelay(layerIdx * 0.08f)
                        .SetEase(Ease.OutBack)
                        .SetLink(gameObject);
                }
            }
        }

        /// <summary>确认动画：选中节点放大脉冲 → 非选中淡出</summary>
        private void PlayConfirmAnimation(MapNode selected, System.Action onComplete)
        {
            if (!nodeRects.TryGetValue(selected.nodeId, out var selectedRect))
            {
                onComplete?.Invoke();
                return;
            }

            // 选中节点脉冲
            selectedRect.DOScale(Vector3.one * 1.5f, 0.4f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (selectedRect == null) { onComplete?.Invoke(); return; }

                    // 非选中节点淡出
                    foreach (var kvp in nodeRects)
                    {
                        if (kvp.Key == selected.nodeId) continue;
                        if (kvp.Value != null)
                        {
                            kvp.Value.DOScale(Vector3.zero, 0.3f)
                                .SetEase(Ease.InBack)
                                .SetLink(gameObject);
                        }
                    }

                    // 延迟后执行回调
                    DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
                });
        }

        // ══════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════

        private void UpdateProgressText()
        {
            if (layerProgressText == null || currentMapData == null) return;

            int currentLayer = 0;
            if (nodeLookup.TryGetValue(currentMapData.currentNodeId, out var node))
                currentLayer = node.layer;

            layerProgressText.text = $"地图 {currentLayer + 1}/{currentMapData.totalLayers}";
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
