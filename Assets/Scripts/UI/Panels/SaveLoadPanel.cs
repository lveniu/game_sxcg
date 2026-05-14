using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Core;
using System;

namespace Game.UI
{
    /// <summary>
    /// 存档面板 — 自动存档 + 手动存档/读档
    /// 
    /// UI元素（Inspector绑定）：
    /// - slotContainer:    存档槽位容器（VerticalLayoutGroup）
    /// - saveSlotTemplate:  存档槽位模板（会被复制3次）
    /// - backButton:        返回按钮
    /// - confirmDialog:     覆盖确认弹窗（GameObject）
    /// - confirmText:       确认弹窗文本
    /// - confirmYesButton:  确认按钮
    /// - confirmNoButton:   取消按钮
    /// 
    /// 流程：
    /// 1. 显示时读取SaveSystem存档信息填充槽位
    /// 2. 空槽位显示"空"，可点击存档
    /// 3. 已占用槽位显示关卡/英雄/时长，可加载或覆盖
    /// 4. 覆盖/删除需二次确认弹窗
    /// 5. 加载时校验存档版本兼容性
    /// </summary>
    public class SaveLoadPanel : UIPanel
    {
        // ============================================================
        // Inspector 字段
        // ============================================================

        [Header("UI引用")]
        [Tooltip("存档槽位容器")]
        public RectTransform slotContainer;

        [Tooltip("存档槽位模板（第一项，会被克隆）")]
        public GameObject saveSlotTemplate;

        [Tooltip("返回按钮")]
        public Button backButton;

        [Header("确认弹窗")]
        [Tooltip("覆盖确认弹窗根节点")]
        public GameObject confirmDialog;

        [Tooltip("确认弹窗文本")]
        public Text confirmText;

        [Tooltip("确认按钮")]
        public Button confirmYesButton;

        [Tooltip("取消按钮")]
        public Button confirmNoButton;

        [Header("配置")]
        [Tooltip("存档槽位数")]
        public int slotCount = 3;

        [Tooltip("当前存档版本号（用于兼容性校验）")]
        public int currentSaveVersion = 1;

        [Tooltip("确认弹窗动画时长")]
        public float dialogAnimDuration = 0.2f;

        [Header("槽位颜色")]
        [Tooltip("空槽位背景色")]
        public Color emptySlotColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);

        [Tooltip("已占用槽位背景色")]
        public Color occupiedSlotColor = new Color(0.15f, 0.25f, 0.15f, 0.8f);

        [Tooltip("选中槽位高亮色")]
        public Color selectedSlotColor = new Color(0.9f, 0.75f, 0.2f, 0.9f);

        // ============================================================
        // 内部状态
        // ============================================================

        /// <summary>存档槽位UI实例</summary>
        private SaveSlotUI[] slots;

        /// <summary>当前选中的槽位索引</summary>
        private int selectedSlotIndex = -1;

        /// <summary>当前等待确认的操作类型</summary>
        private ConfirmAction pendingConfirmAction = ConfirmAction.None;

        /// <summary>等待确认操作的目标槽位</summary>
        private int pendingConfirmSlot = -1;

        /// <summary>面板打开模式</summary>
        private SaveLoadMode currentMode = SaveLoadMode.Save;

        /// <summary>确认弹窗CanvasGroup</summary>
        private CanvasGroup confirmDialogCG;

        /// <summary>来源面板（用于返回）</summary>
        private string returnPanelId;

        private enum ConfirmAction { None, OverwriteSave, DeleteSave, LoadIncompatible }
        private enum SaveLoadMode { Save, Load }

        // ============================================================
        // 生命周期
        // ============================================================

        protected override void Awake()
        {
            base.Awake();
            panelId = "SaveLoad";

            // 确认弹窗CanvasGroup
            if (confirmDialog != null)
            {
                confirmDialogCG = confirmDialog.GetComponent<CanvasGroup>();
                if (confirmDialogCG == null)
                    confirmDialogCG = confirmDialog.AddComponent<CanvasGroup>();
            }
        }

        protected override void OnShow()
        {
            backButton?.onClick.RemoveAllListeners();
            confirmYesButton?.onClick.RemoveAllListeners();
            confirmNoButton?.onClick.RemoveAllListeners();

            backButton?.onClick.AddListener(OnBackClicked);
            confirmYesButton?.onClick.AddListener(OnConfirmYes);
            confirmNoButton?.onClick.AddListener(OnConfirmNo);

            // 隐藏确认弹窗
            HideConfirmDialog();

            // 根据是否有存档决定默认模式
            currentMode = SaveSystem.HasSave ? SaveLoadMode.Load : SaveLoadMode.Save;

            // 创建槽位
            CreateSlots();

            // 刷新槽位信息
            RefreshSlots();

            // 播放入场动画
            PlaySlotEntryAnimation();
        }

        protected override void OnHide()
        {
            backButton?.onClick.RemoveAllListeners();
            confirmYesButton?.onClick.RemoveAllListeners();
            confirmNoButton?.onClick.RemoveAllListeners();

            // 清理槽位动画
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.root != null)
                        slot.root.DOKill();
                }
            }

            HideConfirmDialog();
        }

        // ============================================================
        // 公开接口 — 供外部调用
        // ============================================================

        /// <summary>
        /// 以保存模式打开
        /// </summary>
        /// <param name="returnTo">返回的目标面板ID</param>
        public void OpenForSave(string returnTo = "MainMenu")
        {
            currentMode = SaveLoadMode.Save;
            returnPanelId = returnTo;
            Show();
        }

        /// <summary>
        /// 以读取模式打开
        /// </summary>
        /// <param name="returnTo">返回的目标面板ID</param>
        public void OpenForLoad(string returnTo = "MainMenu")
        {
            currentMode = SaveLoadMode.Load;
            returnPanelId = returnTo;
            Show();
        }

        // ============================================================
        // 槽位创建
        // ============================================================

        /// <summary>创建存档槽位UI</summary>
        private void CreateSlots()
        {
            // 清理旧槽位
            if (slots != null)
            {
                for (int i = slots.Length - 1; i >= 0; i--)
                {
                    if (slots[i]?.root != null && slots[i].root != saveSlotTemplate)
                        Destroy(slots[i].root);
                }
            }

            slots = new SaveSlotUI[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotGO;
                if (i == 0 && saveSlotTemplate != null)
                {
                    // 第一个复用模板
                    slotGO = saveSlotTemplate;
                    slotGO.SetActive(true);
                }
                else
                {
                    // 克隆模板
                    slotGO = Instantiate(saveSlotTemplate, slotContainer);
                    slotGO.name = $"SaveSlot_{i}";
                }

                var slotUI = new SaveSlotUI
                {
                    root = slotGO.transform as RectTransform,
                    slotIndex = i,
                    // 绑定子元素 — 按命名约定查找
                    slotLabel = FindText(slotGO, "SlotLabel"),
                    levelText = FindText(slotGO, "LevelText"),
                    heroText = FindText(slotGO, "HeroText"),
                    timeText = FindText(slotGO, "TimeText"),
                    statusText = FindText(slotGO, "StatusText"),
                    bgImage = FindImage(slotGO, "Background"),
                    saveButton = FindButton(slotGO, "SaveButton"),
                    loadButton = FindButton(slotGO, "LoadButton"),
                    deleteButton = FindButton(slotGO, "DeleteButton"),
                };

                // 绑定按钮事件
                int captured = i; // 闭包捕获
                slotUI.saveButton?.onClick.AddListener(() => OnSaveSlotClicked(captured));
                slotUI.loadButton?.onClick.AddListener(() => OnLoadSlotClicked(captured));
                slotUI.deleteButton?.onClick.AddListener(() => OnDeleteSlotClicked(captured));

                slots[i] = slotUI;
            }
        }

        // ============================================================
        // 槽位刷新
        // ============================================================

        /// <summary>刷新所有槽位显示</summary>
        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                RefreshSlot(i);
            }
        }

        /// <summary>刷新单个槽位</summary>
        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            var slot = slots[index];
            if (slot == null) return;

            // 当前系统只有1个存档位（PlayerPrefs），映射到slot0
            // slot1、slot2 为扩展预留，始终显示空
            bool hasData = (index == 0) && SaveSystem.HasSave;
            SaveData data = hasData ? SaveSystem.Instance?.Load() : null;

            // 槽位标题
            SetText(slot.slotLabel,
                LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("save.slot_label", (index + 1).ToString())
                    : $"存档 {index + 1}");

            if (hasData && data != null)
            {
                // --- 有存档 ---
                // 关卡
                SetText(slot.levelText,
                    LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("save.level_info", data.currentLevel.ToString())
                        : $"关卡 {data.currentLevel}");

                // 英雄
                string heroInfo = FormatHeroInfo(data);
                SetText(slot.heroText, heroInfo);

                // 时长（从时间戳推算游戏时长近似值）
                string timeInfo = FormatPlayTime(data);
                SetText(slot.timeText, timeInfo);

                // 状态
                SetText(slot.statusText,
                    LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("save.status_occupied")
                        : "已存档");

                // 背景色
                if (slot.bgImage != null)
                    slot.bgImage.color = occupiedSlotColor;

                // 按钮可见性
                if (slot.saveButton != null) slot.saveButton.gameObject.SetActive(currentMode == SaveLoadMode.Save);
                if (slot.loadButton != null) slot.loadButton.gameObject.SetActive(true);
                if (slot.deleteButton != null) slot.deleteButton.gameObject.SetActive(true);
            }
            else
            {
                // --- 空槽位 ---
                SetText(slot.levelText, "");
                SetText(slot.heroText, "");
                SetText(slot.timeText, "");
                SetText(slot.statusText,
                    LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("save.status_empty")
                        : "空");

                if (slot.bgImage != null)
                    slot.bgImage.color = emptySlotColor;

                if (slot.saveButton != null) slot.saveButton.gameObject.SetActive(currentMode == SaveLoadMode.Save);
                if (slot.loadButton != null) slot.loadButton.gameObject.SetActive(false);
                if (slot.deleteButton != null) slot.deleteButton.gameObject.SetActive(false);
            }
        }

        // ============================================================
        // 格式化工具
        // ============================================================

        /// <summary>格式化英雄信息</summary>
        private string FormatHeroInfo(SaveData data)
        {
            if (data?.heroes == null || data.heroes.Count == 0)
            {
                return LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("save.no_hero")
                    : "无英雄";
            }

            // 显示选中英雄的名字和等级
            int idx = data.selectedHeroIndex;
            if (idx >= 0 && idx < data.heroes.Count)
            {
                var hero = data.heroes[idx];
                return LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("save.hero_info", hero.heroName, hero.level.ToString())
                    : $"{hero.heroName} Lv{hero.level}";
            }

            // 没有选中的，显示第一个
            var first = data.heroes[0];
            return $"{first.heroName} Lv{first.level}";
        }

        /// <summary>格式化游戏时长（近似值，基于时间戳差）</summary>
        private string FormatPlayTime(SaveData data)
        {
            if (data == null) return "";

            // 用关卡数近似推算时长（每关约2分钟）
            int minutes = data.currentLevel * 2;
            if (minutes < 60)
            {
                return LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("save.time_minutes", minutes.ToString())
                    : $"{minutes}分钟";
            }
            int hours = minutes / 60;
            int remainMin = minutes % 60;
            return LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetText("save.time_hours", hours.ToString(), remainMin.ToString())
                : $"{hours}小时{remainMin}分";
        }

        /// <summary>校验存档版本兼容性</summary>
        private bool IsSaveVersionCompatible(SaveData data)
        {
            if (data == null) return false;
            // 当前版本号 = currentSaveVersion，存档版本号 = data.version
            // 如果存档版本 > 当前版本（用新版存档在旧版打开），拒绝加载
            if (data.version > currentSaveVersion)
            {
                Debug.LogWarning($"[SaveLoadPanel] 存档版本 {data.version} > 当前版本 {currentSaveVersion}，不兼容");
                return false;
            }
            return true;
        }

        // ============================================================
        // 按钮回调
        // ============================================================

        /// <summary>存档按钮点击</summary>
        private void OnSaveSlotClicked(int slotIndex)
        {
            if (slotIndex != 0)
            {
                // 当前仅支持1个存档位，其他槽位提示预留
                Debug.Log($"[SaveLoadPanel] 槽位 {slotIndex} 暂未开放");
                return;
            }

            bool hasExisting = SaveSystem.HasSave;
            if (hasExisting)
            {
                // 有存档，弹确认覆盖
                pendingConfirmAction = ConfirmAction.OverwriteSave;
                pendingConfirmSlot = slotIndex;
                ShowConfirmDialog(
                    LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("save.confirm_overwrite")
                        : "已有存档，确认覆盖？");
            }
            else
            {
                // 直接存档
                ExecuteSave(slotIndex);
            }
        }

        /// <summary>读档按钮点击</summary>
        private void OnLoadSlotClicked(int slotIndex)
        {
            if (slotIndex != 0) return;

            var data = SaveSystem.Instance?.Load();
            if (data == null)
            {
                Debug.LogWarning("[SaveLoadPanel] 存档数据为空");
                return;
            }

            // 版本兼容性校验
            if (!IsSaveVersionCompatible(data))
            {
                pendingConfirmAction = ConfirmAction.LoadIncompatible;
                pendingConfirmSlot = slotIndex;
                ShowConfirmDialog(
                    LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.GetText("save.confirm_incompatible")
                        : "存档版本不兼容，可能丢失数据。仍要加载？");
                return;
            }

            ExecuteLoad(slotIndex);
        }

        /// <summary>删除按钮点击</summary>
        private void OnDeleteSlotClicked(int slotIndex)
        {
            if (slotIndex != 0) return;

            pendingConfirmAction = ConfirmAction.DeleteSave;
            pendingConfirmSlot = slotIndex;
            ShowConfirmDialog(
                LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetText("save.confirm_delete")
                    : "确认删除存档？此操作不可撤销。");
        }

        // ============================================================
        // 确认弹窗
        // ============================================================

        /// <summary>显示确认弹窗</summary>
        private void ShowConfirmDialog(string message)
        {
            if (confirmDialog == null) return;

            SetText(confirmText, message);
            confirmDialog.SetActive(true);

            if (confirmDialogCG != null)
            {
                confirmDialogCG.DOKill();
                confirmDialogCG.alpha = 0f;
                confirmDialogCG.DOFade(1f, dialogAnimDuration).SetEase(Ease.OutQuad);
            }

            // 弹窗缩放动画
            var rt = confirmDialog.transform as RectTransform;
            if (rt != null)
            {
                rt.DOKill();
                rt.localScale = Vector3.one * 0.8f;
                rt.DOScale(Vector3.one, dialogAnimDuration).SetEase(Ease.OutBack);
            }
        }

        /// <summary>隐藏确认弹窗</summary>
        private void HideConfirmDialog()
        {
            if (confirmDialog == null) return;

            if (confirmDialogCG != null)
            {
                confirmDialogCG.DOKill();
                confirmDialogCG.alpha = 0f;
            }

            confirmDialog.SetActive(false);
            pendingConfirmAction = ConfirmAction.None;
            pendingConfirmSlot = -1;
        }

        /// <summary>确认 — 执行待确认操作</summary>
        private void OnConfirmYes()
        {
            switch (pendingConfirmAction)
            {
                case ConfirmAction.OverwriteSave:
                    ExecuteSave(pendingConfirmSlot);
                    break;

                case ConfirmAction.DeleteSave:
                    ExecuteDelete(pendingConfirmSlot);
                    break;

                case ConfirmAction.LoadIncompatible:
                    ExecuteLoad(pendingConfirmSlot);
                    break;
            }

            HideConfirmDialog();
        }

        /// <summary>取消 — 关闭确认弹窗</summary>
        private void OnConfirmNo()
        {
            HideConfirmDialog();
        }

        // ============================================================
        // 存档操作
        // ============================================================

        /// <summary>执行存档</summary>
        private void ExecuteSave(int slotIndex)
        {
            if (SaveSystem.Instance == null)
            {
                Debug.LogError("[SaveLoadPanel] SaveSystem不存在");
                return;
            }

            SaveSystem.Instance.Save();
            Debug.Log($"[SaveLoadPanel] 存档完成 → 槽位 {slotIndex}");

            // 刷新显示
            RefreshSlots();

            // 存档成功反馈动画
            if (slots != null && slotIndex < slots.Length && slots[slotIndex]?.root != null)
            {
                slots[slotIndex].root.DOPunchScale(Vector3.one * 0.1f, 0.3f, 8, 0.5f);
            }
        }

        /// <summary>执行读档</summary>
        private void ExecuteLoad(int slotIndex)
        {
            if (SaveSystem.Instance == null)
            {
                Debug.LogError("[SaveLoadPanel] SaveSystem不存在");
                return;
            }

            var data = SaveSystem.Instance.Load();
            if (data == null)
            {
                Debug.LogError("[SaveLoadPanel] 读档失败：数据为空");
                return;
            }

            Debug.Log($"[SaveLoadPanel] 加载存档 → 槽位 {slotIndex}, 关卡 {data.currentLevel}");

            // 恢复游戏状态
            bool restored = SaveSystem.Instance.RestoreSave(data);
            if (!restored)
            {
                Debug.LogError("[SaveLoadPanel] 存档恢复失败");
                return;
            }

            // 通知状态机跳转到对应状态
            var gsm = GameStateMachine.Instance;
            if (gsm != null)
            {
                // 如果有肉鸽运行存档，优先恢复肉鸽进度
                if (SaveSystem.Instance.HasSavedRun())
                {
                    gsm.ResumeSavedRun();
                }
                else
                {
                    // 没有肉鸽运行存档，仅恢复普通存档后跳转地图选择
                    gsm.ChangeState(GameState.MapSelect);
                }
            }

            // 关闭面板
            Hide();
        }

        /// <summary>执行删除</summary>
        private void ExecuteDelete(int slotIndex)
        {
            if (SaveSystem.Instance == null) return;

            SaveSystem.Instance.DeleteSave();
            Debug.Log($"[SaveLoadPanel] 存档已删除 → 槽位 {slotIndex}");

            // 刷新显示
            RefreshSlots();

            // 删除反馈动画 — 淡出再刷新
            if (slots != null && slotIndex < slots.Length && slots[slotIndex]?.root != null)
            {
                slots[slotIndex].root.DOPunchScale(Vector3.one * -0.05f, 0.3f, 8, 0.5f);
            }
        }

        // ============================================================
        // 返回
        // ============================================================

        /// <summary>返回上一面板</summary>
        private void OnBackClicked()
        {
            Hide();

            // 返回来源面板
            if (!string.IsNullOrEmpty(returnPanelId))
            {
                var uiMgr = NewUIManager.Instance;
                if (uiMgr != null)
                {
                    uiMgr.ShowSubPanel(returnPanelId);
                }
            }
        }

        // ============================================================
        // 入场动画
        // ============================================================

        /// <summary>槽位依次入场动画</summary>
        private void PlaySlotEntryAnimation()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]?.root == null) continue;

                slots[i].root.anchoredPosition = new Vector2(0, -30f);
                slots[i].root.anchoredPosition += new Vector2(0, -30f * i);
                slots[i].root.DOAnchorPosY(0f, 0.3f)
                    .SetDelay(i * 0.08f)
                    .SetEase(Ease.OutCubic);
            }
        }

        // ============================================================
        // UI 工具
        // ============================================================

        private static Text FindText(GameObject go, string childName)
        {
            var t = go.transform.Find(childName);
            return t?.GetComponent<Text>();
        }

        private static Image FindImage(GameObject go, string childName)
        {
            var t = go.transform.Find(childName);
            return t?.GetComponent<Image>();
        }

        private static Button FindButton(GameObject go, string childName)
        {
            var t = go.transform.Find(childName);
            return t?.GetComponent<Button>();
        }

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }

        // ============================================================
        // 内部结构
        // ============================================================

        /// <summary>存档槽位UI缓存</summary>
        private class SaveSlotUI
        {
            public RectTransform root;
            public int slotIndex;
            public Text slotLabel;
            public Text levelText;
            public Text heroText;
            public Text timeText;
            public Text statusText;
            public Image bgImage;
            public Button saveButton;
            public Button loadButton;
            public Button deleteButton;
        }
    }
}
