using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    // ═══════════════════════════════════════════════════════════
    // FE-08: 肉鸽地图路径UI — 对接后端 RoguelikeMapSystem
    // ═══════════════════════════════════════════════════════════
    //
    // 布局（竖屏 720×1280）：
    // ┌──────────────────────────────────────┐
    // │  [背包] 层数 3/15    [遗物]          │  顶部栏
    // ├──────────────────────────────────────┤
    // │         ○─────○                      │  Layer 0
    // │              │                       │
    // │    ○───○────○────○───○              │  Layer N
    // ├──────────────────────────────────────┤
    // │  [节点信息：类型/难度/预览]            │  底部信息
    // │  [确认前往]                           │
    // └──────────────────────────────────────┘
    //
    // 数据源：RoguelikeMapSystem.Instance（后端单例）
    // 状态驱动：SelectNode() → 后端 DriveStateByNodeType → 状态机切换
    //
    // 三层动画安全：所有 tween 加 .SetLink(gameObject)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 肉鸽地图路径面板 — 对接后端 RoguelikeMapSystem
    /// 状态面板模式：GameState.MapSelect 时自动显示
    /// </summary>
    public class RoguelikeMapPanel : UIPanel
    {
        // ──────── 布局常量 ────────
        private const float CANVAS_WIDTH = 720f;
        private const float LAYER_HEIGHT = 180f;
        private const float NODE_SIZE = 64f;
        private const float NODE_SPACING = 130f;
        private const float LINE_THICKNESS = 3f;
        private const float PADDING_TOP = 100f;
        private const float BREATH_DURATION = 1.5f;

        // ──────── 节点颜色映射 ────────
        private static readonly Color COLOR_BATTLE    = HexColor("#FF6B6B");
        private static readonly Color COLOR_ELITE     = HexColor("#FF4500");
        private static readonly Color COLOR_EVENT     = HexColor("#FFD700");
        private static readonly Color COLOR_SHOP      = HexColor("#4ECDC4");
        private static readonly Color COLOR_REST      = HexColor("#95E1D3");
        private static readonly Color COLOR_BOSS      = HexColor("#8B0000");
        private static readonly Color COLOR_TREASURE  = HexColor("#FF69B4");

        private static readonly Color COLOR_AVAILABLE   = HexColor("#FFD700");
        private static readonly Color COLOR_LINE_WALKED = HexColor("#FFD700");
        private static readonly Color COLOR_LINE_FUTURE = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // ──────── Inspector 引用 ────────
        [Header("顶部栏")]
        public Text layerProgressText;
        public Button inventoryButton;
        public Button relicButton;

        [Header("地图区域")]
        public ScrollRect mapScrollRect;
        public RectTransform mapContent;

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
            slideInAnimation = false;

            confirmButton?.onClick.AddListener(OnConfirmClicked);
            inventoryButton?.onClick.AddListener(() =>
                NewUIManager.Instance?.ShowSubPanel("Inventory"));
        }

        public override void Show()
        {
            base.Show();
            LoadMapAndRender();
        }

        public override void OnShow()
        {
            if (bottomInfoBar != null) bottomInfoBar.gameObject.SetActive(false);
        }

        public override void OnHide()
        {
            ClearMap();
        }

        // ══════════════════════════════════════
        // 数据加载 — 对接后端 RoguelikeMapSystem
        // ══════════════════════════════════════

        /// <summary>从后端加载地图数据并渲染</summary>
        private void LoadMapAndRender()
        {
            var mapSystem = RoguelikeMapSystem.Instance;

            // 后端未初始化时安全兜底
            if (mapSystem == null || mapSystem.CurrentMap == null)
            {
                Debug.LogWarning("[MapPanel] RoguelikeMapSystem 未就绪，使用空地图");
                return;
            }

            currentMapData = mapSystem.CurrentMap;

            // 使用后端已建好的索引（MapData.BuildIndex）
            nodeLookup.Clear();
            foreach (var layer in currentMapData.layers)
            {
                foreach (var node in layer)
                {
                    nodeLookup[node.nodeId] = node;
                }
            }

            // 确保可达节点标记正确
            mapSystem.GetAvailableNodes();

            RenderMap();
            ScrollToCurrentLayer();
        }

        // ══════════════════════════════════════
        // 地图渲染
        // ══════════════════════════════════════

        private void RenderMap()
        {
            ClearMap();

            if (currentMapData == null) return;

            int totalLayers = currentMapData.totalLayers;
            float totalHeight = PADDING_TOP + totalLayers * LAYER_HEIGHT + 200f;
            mapContent.sizeDelta = new Vector2(CANVAS_WIDTH, totalHeight);

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

        private void RenderNode(MapNode node)
        {
            var go = new GameObject($"Node_{node.nodeId}");
            go.transform.SetParent(mapContent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = GetNodePosition(node);
            rect.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 背景圆形
            var bgImage = go.AddComponent<Image>();
            bgImage.color = GetNodeColor(node);
            bgImage.raycastTarget = true;

            // 按钮
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnNodeClicked(node.nodeId));

            // 节点类型图标
            CreateChildText(go, "Icon", Vector2.zero, Vector2.one,
                GetNodeIcon(node.nodeType), 28, Color.white, TextAnchor.MiddleCenter);

            // 难度星级
            if (node.difficulty > 0)
            {
                CreateChildText(go, "Difficulty",
                    new Vector2(0, 0), new Vector2(1, 0.4f),
                    new string('★', Mathf.Min(node.difficulty, 5)), 10, Color.yellow, TextAnchor.MiddleCenter);
            }

            // 状态装饰
            if (node.isVisited)
                ApplyVisitedStyle(rect, bgImage);
            else if (node.isAvailable)
                ApplyAvailableStyle(rect);
            else
                ApplyLockedStyle(bgImage);

            nodeRects[node.nodeId] = rect;
        }

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

        private void DrawLine(MapNode from, MapNode to)
        {
            var go = new GameObject($"Line_{from.nodeId}_{to.nodeId}");
            go.transform.SetParent(mapContent, false);

            var rect = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = from.isVisited ? COLOR_LINE_WALKED : COLOR_LINE_FUTURE;

            Vector2 fromPos = GetNodePosition(from);
            Vector2 toPos = GetNodePosition(to);
            Vector2 delta = toPos - fromPos;
            float distance = delta.magnitude;
            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

            rect.sizeDelta = new Vector2(LINE_THICKNESS, distance);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = fromPos;
            rect.localEulerAngles = new Vector3(0, 0, -angle);

            lineRects.Add(rect);
        }

        private void ClearMap()
        {
            foreach (var kvp in nodeRects)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            nodeRects.Clear();

            foreach (var line in lineRects)
                if (line != null) Destroy(line.gameObject);
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

            float totalWidth = (count - 1) * NODE_SPACING;
            float startX = (CANVAS_WIDTH - totalWidth) / 2f;

            float x = startX + node.indexInLayer * NODE_SPACING;
            float y = totalContentHeight - PADDING_TOP - node.layer * LAYER_HEIGHT;

            return new Vector2(x, y);
        }

        private float totalContentHeight =>
            mapContent != null ? mapContent.sizeDelta.y : 3000f;

        // ══════════════════════════════════════
        // 节点样式
        // ══════════════════════════════════════

        private static Color GetNodeColor(MapNode node) => node.nodeType switch
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

        private static string GetNodeIcon(MapNodeType type) => type switch
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

        private void ApplyVisitedStyle(RectTransform rect, Image bg)
        {
            bg.color = new Color(0.5f, 0.8f, 0.5f, 0.6f);
            CreateChildText(rect.gameObject, "Check",
                new Vector2(0.6f, 0.6f), new Vector2(1f, 1f),
                "✓", 16, Color.green, TextAnchor.MiddleCenter);
        }

        private void ApplyAvailableStyle(RectTransform rect)
        {
            // 金色描边
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

        private static void ApplyLockedStyle(Image bg)
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

            selectedNodeId = nodeId;
            if (nodeRects.TryGetValue(nodeId, out var selectedRect))
            {
                selectedRect.DOScale(new Vector3(1.15f, 1.15f, 1f), 0.15f)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);
            }

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
            {
                int diff = Mathf.Min(node.difficulty, 5);
                nodeDifficultyText.text = $"难度: {new string('★', diff)}{new string('☆', 5 - diff)}";
            }

            // 底部栏弹出
            bottomInfoBar.anchoredPosition = new Vector2(0, -200f);
            bottomInfoBar.DOAnchorPosY(0f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetLink(gameObject);
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(selectedNodeId)) return;

            // 确认动画 → 调用后端 SelectNode（后端内部驱动状态切换）
            PlayConfirmAnimation(() =>
            {
                var mapSystem = RoguelikeMapSystem.Instance;
                if (mapSystem != null)
                {
                    mapSystem.SelectNode(selectedNodeId);
                    // 后端 SelectNode → DriveStateByNodeType → 自动切换状态 → 本面板 Hide
                }
                else
                {
                    Debug.LogError("[MapPanel] RoguelikeMapSystem 为空，无法选择节点");
                }
            });
        }

        // ══════════════════════════════════════
        // 滚动定位
        // ══════════════════════════════════════

        private void ScrollToCurrentLayer()
        {
            if (currentMapData == null || mapScrollRect == null) return;

            string currentId = currentMapData.currentNodeId;
            if (!nodeLookup.TryGetValue(currentId, out var currentNode)) return;

            float contentH = mapContent.sizeDelta.y;
            float targetY = PADDING_TOP + (currentMapData.totalLayers - currentNode.layer - 1) * LAYER_HEIGHT;
            float normalizedPos = Mathf.Clamp01(targetY / contentH);

            DOVirtual.DelayedCall(0.3f, () =>
            {
                if (mapScrollRect != null)
                    mapScrollRect.verticalNormalizedPosition = normalizedPos;
            });
        }

        // ══════════════════════════════════════
        // 动画
        // ══════════════════════════════════════

        private void PlayEntryAnimation()
        {
            foreach (var layer in currentMapData.layers)
            {
                int layerIdx = layer[0].layer;
                foreach (var node in layer)
                {
                    if (!nodeRects.TryGetValue(node.nodeId, out var rect)) continue;

                    var targetPos = GetNodePosition(node);
                    rect.anchoredPosition -= new Vector2(0, 80f);
                    rect.localScale = Vector3.zero;

                    rect.DOAnchorPos(targetPos, 0.5f)
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

        private void PlayConfirmAnimation(System.Action onComplete)
        {
            if (string.IsNullOrEmpty(selectedNodeId) ||
                !nodeRects.TryGetValue(selectedNodeId, out var selectedRect))
            {
                onComplete?.Invoke();
                return;
            }

            var capturedRect = selectedRect;

            // 选中节点脉冲
            capturedRect.DOScale(Vector3.one * 1.5f, 0.4f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (capturedRect == null) { onComplete?.Invoke(); return; }

                    // 非选中节点淡出
                    foreach (var kvp in nodeRects)
                    {
                        if (kvp.Key == selectedNodeId) continue;
                        if (kvp.Value != null)
                        {
                            kvp.Value.DOScale(Vector3.zero, 0.3f)
                                .SetEase(Ease.InBack)
                                .SetLink(gameObject);
                        }
                    }

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

        /// <summary>创建子物体 Text 的快捷方法</summary>
        private static void CreateChildText(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinAsset<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
