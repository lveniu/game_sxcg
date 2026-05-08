using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 站位系统面板 — 棋盘区（下半屏）
    /// 
    /// 竖屏720x1280布局（Inspector拖拽绑定）：
    /// ┌──────────────────────────┐
    /// │  排标签：后排│中排│前排   │
    /// ├──────────────────────────┤
    /// │ [3,3] [2,3] [1,3] ← 后排 │ y=3
    /// │ [3,2] [2,2] [1,2] ← 中排 │ y=2
    /// │ [3,1] [2,1] [1,1] ← 前排 │ y=1
    /// │ [3,0] [2,0] [1,0] ← 前排 │ y=0
    /// ├──────────────────────────┤
    /// │ 站位Buff提示区           │
    │ [开始战斗] 按钮           │
    /// └──────────────────────────┘
    /// 
    /// 交互流程：
    /// 1. OnShow → 渲染3×4棋盘，填充已召唤英雄
    /// 2. 点击空格 → 无操作
    /// 3. 长按英雄格 → 开始拖拽
    /// 4. 拖到空格 → 移动英雄
    /// 5. 拖到另一英雄格 → 交换位置
    /// 6. 放置后 → 实时显示站位Buff
    /// 7. 点击"开始战斗" → 应用站位效果 → 进入Battle状态
    /// </summary>
    public class BattleGridPanel : UIPanel
    {
        // ========== 棋盘格子 ==========
        [Header("棋盘容器")]
        public RectTransform gridContainer;
        public GameObject cellPrefab;

        [Header("排标签")]
        public Text frontRowLabel;
        public Text middleRowLabel;
        public Text backRowLabel;

        // ========== 站位提示 ==========
        [Header("站位提示")]
        public RectTransform buffTipPanel;
        public Text buffTipText;
        public Image buffTipIcon;

        // ========== 操作按钮 ==========
        [Header("操作按钮")]
        public Button startBattleButton;
        public Text startBattleButtonText;

        // ========== 拖拽状态 ==========
        [Header("拖拽配置")]
        [Tooltip("长按触发阈值（秒）")]
        public float longPressThreshold = 0.3f;
        [Tooltip("拖拽偏移量（像素）")]
        public float dragOffset = 60f;

        // ========== 内部状态 ==========
        private const int GRID_WIDTH = 3;
        private const int GRID_HEIGHT = 4;

        private RectTransform[,] cellRects = new RectTransform[GRID_WIDTH, GRID_HEIGHT];
        private Text[,] cellTexts = new Text[GRID_WIDTH, GRID_HEIGHT];
        private Image[,] cellBgs = new Image[GRID_WIDTH, GRID_HEIGHT];
        private Image[,] cellBorders = new Image[GRID_WIDTH, GRID_HEIGHT];

        // 拖拽状态机
        private enum DragState { None, Pressing, Dragging }
        private DragState dragState = DragState.None;
        private Vector2Int dragFrom = new Vector2Int(-1, -1);
        private float pressStartTime;
        private RectTransform dragGhost; // 拖拽时的半透明副本

        // 排色带配置
        private static readonly Color FRONT_ROW_COLOR = new Color(0.9f, 0.4f, 0.3f, 0.15f);   // 红色调
        private static readonly Color MIDDLE_ROW_COLOR = new Color(0.9f, 0.8f, 0.3f, 0.15f);  // 黄色调
        private static readonly Color BACK_ROW_COLOR = new Color(0.3f, 0.6f, 0.9f, 0.15f);    // 蓝色调

        // 站位Buff描述
        private static readonly Dictionary<string, string> POSITION_BUFFS = new Dictionary<string, string>
        {
            { "Warrior_Front", "战士前排：防御+30%" },
            { "Warrior_Back", "战士后排：无特殊加成" },
            { "Mage_Front", "法师前排：无特殊加成" },
            { "Mage_Back", "法师后排：攻击+20%" },
            { "Assassin_Front", "刺客前排：无特殊加成" },
            { "Assassin_Back", "刺客后排：无特殊加成" },
        };

        protected override void Awake()
        {
            base.Awake();
            panelId = "BattleGrid";
        }

        protected override void OnShow()
        {
            startBattleButton?.onClick.RemoveAllListeners();
            startBattleButton?.onClick.AddListener(OnStartBattleClicked);

            // 初始化棋盘格子
            InitializeGridCells();

            // 刷新棋盘内容
            RefreshGrid();

            // 初始化拖拽状态
            dragState = DragState.None;
            dragFrom = new Vector2Int(-1, -1);

            // 隐藏站位提示
            if (buffTipPanel != null) buffTipPanel.gameObject.SetActive(false);

            // 更新开始战斗按钮状态
            UpdateStartBattleButton();

            // 棋盘入场动画
            PlayGridEnterAnimation();
        }

        protected override void OnHide()
        {
            startBattleButton?.onClick.RemoveAllListeners();
            CleanupDragGhost();
        }

        // ========== 棋盘初始化 ==========

        private void InitializeGridCells()
        {
            if (gridContainer == null || cellPrefab == null) return;

            // 清除旧格子
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }

            for (int y = GRID_HEIGHT - 1; y >= 0; y--)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var go = Instantiate(cellPrefab, gridContainer);
                    var rect = go.GetComponent<RectTransform>();
                    cellRects[x, y] = rect;

                    // 查找子组件
                    var nameObj = go.transform.Find("NameText");
                    cellTexts[x, y] = nameObj?.GetComponent<Text>();

                    var bgObj = go.transform.Find("Background");
                    cellBgs[x, y] = bgObj?.GetComponent<Image>();

                    var borderObj = go.transform.Find("Border");
                    cellBorders[x, y] = borderObj?.GetComponent<Image>();

                    // 设置排色带
                    var rowColor = GetRowColor(y);
                    if (cellBgs[x, y] != null)
                    {
                        cellBgs[x, y].color = rowColor;
                    }

                    // 存储坐标到事件数据
                    var cellBtn = go.GetComponent<Button>();
                    if (cellBtn != null)
                    {
                        int cx = x, cy = y;
                        cellBtn.onClick.AddListener(() => OnCellClicked(cx, cy));
                    }

                    // 添加拖拽事件触发器
                    var eventTrigger = go.GetComponent<EventTrigger>();
                    if (eventTrigger == null)
                        eventTrigger = go.AddComponent<EventTrigger>();

                    int dx = x, dy = y;

                    // PointerDown
                    var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    downEntry.callback.AddListener(_ => OnPointerDown(dx, dy));
                    eventTrigger.triggers.Add(downEntry);

                    // PointerUp
                    var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                    upEntry.callback.AddListener(_ => OnPointerUp(dx, dy));
                    eventTrigger.triggers.Add(upEntry);

                    // Drag
                    var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                    dragEntry.callback.AddListener(_ => OnDrag(dx, dy));
                    eventTrigger.triggers.Add(dragEntry);

                    // 初始隐藏
                    rect.localScale = Vector3.zero;
                }
            }

            // 设置排标签
            if (frontRowLabel != null) frontRowLabel.text = "← 前排（防御位）";
            if (middleRowLabel != null) middleRowLabel.text = "← 中排（均衡位）";
            if (backRowLabel != null) backRowLabel.text = "← 后排（输出位）";
        }

        // ========== 棋盘刷新 ==========

        private void RefreshGrid()
        {
            var grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogWarning("[BattleGrid] GridManager 未初始化");
                return;
            }

            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    var cell = grid.GetCell(x, y);
                    bool occupied = cell != null && cell.IsOccupied;

                    // 格子文字
                    if (cellTexts[x, y] != null)
                    {
                        if (occupied)
                        {
                            var hero = cell.Occupant;
                            string starStr = GetStarString(hero.StarLevel);
                            string classIcon = GetClassIcon(hero.Data.heroClass);
                            cellTexts[x, y].text = $"{classIcon}{hero.Data.heroName}\n{starStr}";
                            cellTexts[x, y].color = Color.white;
                        }
                        else
                        {
                            cellTexts[x, y].text = "";
                        }
                    }

                    // 格子边框
                    if (cellBorders[x, y] != null)
                    {
                        cellBorders[x, y].color = occupied
                            ? GetClassColor(cell.Occupant.Data.heroClass)
                            : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    }

                    // 背景色
                    if (cellBgs[x, y] != null)
                    {
                        var rowColor = GetRowColor(y);
                        if (occupied)
                        {
                            // 有英雄的格子背景稍亮
                            cellBgs[x, y].color = new Color(
                                rowColor.r + 0.1f,
                                rowColor.g + 0.1f,
                                rowColor.b + 0.1f,
                                0.4f
                            );
                        }
                        else
                        {
                            cellBgs[x, y].color = rowColor;
                        }
                    }
                }
            }
        }

        // ========== 点击/拖拽交互 ==========

        private void OnCellClicked(int x, int y)
        {
            // 点击空格 → 显示该排站位说明
            var grid = GridManager.Instance;
            var cell = grid?.GetCell(x, y);
            if (cell == null) return;

            if (cell.IsOccupied)
            {
                ShowPositionBuff(cell.Occupant, x, y);
            }
        }

        private void OnPointerDown(int x, int y)
        {
            var grid = GridManager.Instance;
            var cell = grid?.GetCell(x, y);
            if (cell == null || !cell.IsOccupied) return;

            // 开始长按检测
            dragState = DragState.Pressing;
            dragFrom = new Vector2Int(x, y);
            pressStartTime = Time.time;
        }

        private void OnPointerUp(int x, int y)
        {
            if (dragState == DragState.Dragging)
            {
                // 拖拽结束 → 尝试放置
                HandleDrop(x, y);
            }

            dragState = DragState.None;
            dragFrom = new Vector2Int(-1, -1);
            CleanupDragGhost();
        }

        private void OnDrag(int x, int y)
        {
            if (dragState == DragState.Pressing)
            {
                // 检测是否超过长按阈值
                if (Time.time - pressStartTime >= longPressThreshold)
                {
                    dragState = DragState.Dragging;
                    CreateDragGhost(dragFrom.x, dragFrom.y);
                }
            }

            if (dragState == DragState.Dragging && dragGhost != null)
            {
                // 移动拖拽副本跟随手指/鼠标
                var cellRect = cellRects[x, y];
                if (cellRect != null)
                {
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        gridContainer, Input.mousePosition, null, out localPoint);
                    dragGhost.anchoredPosition = localPoint + new Vector2(0, dragOffset);
                }
            }
        }

        /// <summary>
        /// 创建拖拽半透明副本
        /// </summary>
        private void CreateDragGhost(int fromX, int fromY)
        {
            CleanupDragGhost();

            var grid = GridManager.Instance;
            var cell = grid?.GetCell(fromX, fromY);
            if (cell == null || !cell.IsOccupied) return;

            // 原格子半透明
            var fromRect = cellRects[fromX, fromY];
            if (fromRect != null)
            {
                var cg = fromRect.GetComponent<CanvasGroup>();
                if (cg == null) cg = fromRect.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.4f;
            }

            // 创建幽灵副本
            var go = new GameObject("DragGhost");
            go.transform.SetParent(gridContainer, false);
            dragGhost = go.AddComponent<RectTransform>();
            dragGhost.sizeDelta = fromRect != null ? fromRect.sizeDelta : new Vector2(100, 100);

            var img = go.AddComponent<Image>();
            img.color = GetClassColor(cell.Occupant.Data.heroClass);
            img.raycastTarget = false;

            var txt = new GameObject("Text");
            txt.transform.SetParent(go.transform, false);
            var txtRect = txt.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txtComp = txt.AddComponent<Text>();
            txtComp.text = $"{GetClassIcon(cell.Occupant.Data.heroClass)}{cell.Occupant.Data.heroName}";
            txtComp.alignment = TextAnchor.MiddleCenter;
            txtComp.fontSize = 18;
            txtComp.color = Color.white;
            txtComp.raycastTarget = false;

            var ghostCg = go.AddComponent<CanvasGroup>();
            ghostCg.alpha = 0.7f;

            // 缩放动画
            dragGhost.localScale = Vector3.one * 1.1f;
            dragGhost.DOScale(Vector3.one * 1.2f, 0.3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutQuad);
        }

        private void CleanupDragGhost()
        {
            if (dragGhost != null)
            {
                dragGhost.DOKill();
                Destroy(dragGhost.gameObject);
                dragGhost = null;
            }

            // 恢复原格子透明度
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    if (cellRects[x, y] != null)
                    {
                        var cg = cellRects[x, y].GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 1f;
                    }
                }
            }
        }

        /// <summary>
        /// 处理放下（移动/交换）
        /// </summary>
        private void HandleDrop(int toX, int toY)
        {
            if (dragFrom.x < 0 || dragFrom.y < 0) return;
            if (dragFrom.x == toX && dragFrom.y == toY) return;

            var grid = GridManager.Instance;
            if (grid == null) return;

            var fromCell = grid.GetCell(dragFrom.x, dragFrom.y);
            var toCell = grid.GetCell(toX, toY);
            if (fromCell == null) return;

            if (toCell == null)
            {
                // 越界 → 归位
                RefreshGrid();
                return;
            }

            if (!toCell.IsOccupied)
            {
                // 移动到空格
                var hero = fromCell.Occupant;
                grid.RemoveHero(dragFrom.x, dragFrom.y);
                grid.PlaceHero(hero, toX, toY);

                Debug.Log($"[BattleGrid] 移动 {hero.Data.heroName}: ({dragFrom.x},{dragFrom.y}) → ({toX},{toY})");
            }
            else
            {
                // 交换两个英雄位置
                var heroA = fromCell.Occupant;
                var heroB = toCell.Occupant;

                grid.RemoveHero(dragFrom.x, dragFrom.y);
                grid.RemoveHero(toX, toY);
                grid.PlaceHero(heroA, toX, toY);
                grid.PlaceHero(heroB, dragFrom.x, dragFrom.y);

                Debug.Log($"[BattleGrid] 交换 {heroA.Data.heroName} ↔ {heroB.Data.heroName}");
            }

            // 播放格子反馈动画
            PlayCellFeedback(toX, toY);
            if (toCell.IsOccupied || !fromCell.IsOccupied)
            {
                // 交换时也动画原来位置
                PlayCellFeedback(dragFrom.x, dragFrom.y);
            }

            RefreshGrid();
            ShowPositionBuffForCell(toX, toY);
        }

        // ========== 站位Buff显示 ==========

        private void ShowPositionBuff(Hero hero, int x, int y)
        {
            if (buffTipPanel == null) return;
            buffTipPanel.gameObject.SetActive(true);

            var row = GridManager.Instance.GetRow(new Vector2Int(x, y));
            string rowName = row switch
            {
                GridRow.Front => "前排",
                GridRow.Middle => "中排",
                GridRow.Back => "后排",
                _ => "未知"
            };

            string classKey = hero.Data.heroClass.ToString();
            string lookupKey = $"{classKey}_{row}";
            string buffDesc;
            if (!POSITION_BUFFS.TryGetValue(lookupKey, out buffDesc)) buffDesc = "无特殊加成";

            if (buffTipText != null)
            {
                buffTipText.text = $"站位：{rowName}\n{hero.Data.heroName} — {buffDesc}";
            }

            // 提示面板入场动画
            buffTipPanel.anchoredPosition = new Vector2(0f, -20f);
            buffTipPanel.DOAnchorPosY(0f, 0.2f).SetEase(Ease.OutQuad);

            var cg = buffTipPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.DOFade(1f, 0.15f);
            }

            // 2秒后自动隐藏
            DOVirtual.DelayedCall(2f, () =>
            {
                if (buffTipPanel != null) buffTipPanel.gameObject.SetActive(false);
            });
        }

        private void ShowPositionBuffForCell(int x, int y)
        {
            var grid = GridManager.Instance;
            var cell = grid?.GetCell(x, y);
            if (cell != null && cell.IsOccupied)
            {
                ShowPositionBuff(cell.Occupant, x, y);
            }
        }

        // ========== 开始战斗 ==========

        private void OnStartBattleClicked()
        {
            var grid = GridManager.Instance;
            var deck = CardDeck.Instance;

            // 应用站位效果到所有场上英雄
            if (deck != null && grid != null)
            {
                deck.ApplyPositioningToField(grid);
            }

            // 按钮动画反馈
            if (startBattleButton != null)
            {
                startBattleButton.interactable = false;
                startBattleButton.transform.DOScale(Vector3.one * 0.9f, 0.1f)
                    .OnComplete(() =>
                    {
                        startBattleButton.transform.DOScale(Vector3.one, 0.1f)
                            .OnComplete(() =>
                            {
                                // 进入战斗状态
                                GameStateMachine.Instance?.NextState();
                            });
                    });
            }
            else
            {
                GameStateMachine.Instance?.NextState();
            }
        }

        private void UpdateStartBattleButton()
        {
            var deck = CardDeck.Instance;
            bool hasHeroes = deck != null && deck.CurrentPopulation > 0;

            if (startBattleButton != null)
            {
                startBattleButton.interactable = hasHeroes;
            }
            if (startBattleButtonText != null)
            {
                startBattleButtonText.text = hasHeroes
                    ? "开始战斗"
                    : "请先召唤英雄";
            }
        }

        // ========== 动画 ==========

        private void PlayGridEnterAnimation()
        {
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    if (cellRects[x, y] == null) continue;
                    float delay = ((GRID_HEIGHT - 1 - y) * GRID_WIDTH + x) * 0.05f;

                    cellRects[x, y].DOKill();
                    cellRects[x, y].localScale = Vector3.zero;
                    cellRects[x, y].DOScale(Vector3.one, 0.3f)
                        .SetDelay(delay)
                        .SetEase(Ease.OutBack);
                }
            }
        }

        private void PlayCellFeedback(int x, int y)
        {
            if (cellRects[x, y] == null) return;

            cellRects[x, y].DOKill();
            cellRects[x, y].DOScale(Vector3.one * 1.15f, 0.1f)
                .OnComplete(() =>
                {
                    if (cellRects[x, y] != null)
                        cellRects[x, y].DOScale(Vector3.one, 0.15f).SetEase(Ease.OutQuad);
                });
        }

        // ========== 工具方法 ==========

        private static Color GetRowColor(int y)
        {
            if (y <= 1) return FRONT_ROW_COLOR;
            if (y == 2) return MIDDLE_ROW_COLOR;
            return BACK_ROW_COLOR;
        }

        private static string GetClassIcon(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "⚔",
                HeroClass.Mage => "🔮",
                HeroClass.Assassin => "🗡",
                _ => "●"
            };
        }

        private static Color GetClassColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => new Color(0.9f, 0.4f, 0.3f),   // 红
                HeroClass.Mage => new Color(0.3f, 0.5f, 0.9f),      // 蓝
                HeroClass.Assassin => new Color(0.7f, 0.3f, 0.9f),  // 紫
                _ => Color.gray
            };
        }

        private static string GetStarString(int starLevel)
        {
            return starLevel switch
            {
                1 => "★",
                2 => "★★",
                3 => "★★★",
                _ => ""
            };
        }
    }
}
