using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using Game.Roguelike;

namespace Game.UI
{
    /// <summary>
    /// FE-07 骰子面升级面板 — 展示6面状态，选择升级目标面和效果
    /// 数据源: GameManager.DiceRoller.Dices (Dice[])
    /// 升级选项: UIConfigBridge.GetSpecialFaceDisplay(faceId)
    /// </summary>
    public class DiceUpgradePanel : UIPanel
    {
        [System.Serializable]
        public class FaceSlot
        {
            public RectTransform rect;
            public Text valueText;      // 面值数字 (1-6)
            public Image bgImage;       // 面背景
            public Image borderImage;   // 选中/特殊效果边框
            public Text effectText;     // 特殊效果名称（无效果则空）
            public Button selectButton; // 点击选择此面
            public int faceIndex;       // 面索引 0-5
            public bool hasSpecialEffect;
        }

        [System.Serializable]
        public class UpgradeOption
        {
            public RectTransform rect;
            public Text nameText;       // 效果名称
            public Text descText;       // 效果描述
            public Image iconBg;        // 图标背景（带颜色）
            public Text iconText;       // emoji 图标
            public Button selectButton; // 点击选择此升级
            public string effectId;     // 特殊效果ID
        }

        [Header("骰子面展示")]
        public RectTransform faceContainer;       // 6面容器
        public GameObject faceSlotPrefab;         // 面槽位预制体（可选）
        public int diceIndex = 0;                 // 当前展示哪个骰子（默认第一个）

        [Header("升级选项")]
        public RectTransform upgradeContainer;    // 升级选项容器
        public GameObject upgradeOptionPrefab;    // 升级选项预制体（可选）
        public Text upgradeTitleText;             // "选择升级效果"

        [Header("操作")]
        public Button confirmButton;              // 确认升级
        public Button skipButton;                 // 跳过
        public Text statusText;                   // 状态提示

        [Header("骰子切换（多骰子时）")]
        public Button prevDiceButton;
        public Button nextDiceButton;
        public Text diceIndexText;                // "骰子 1/3"

        [Header("动画")]
        public float slotAnimDuration = 0.25f;
        public float optionAnimDuration = 0.2f;

        // 程序化创建的元素
        private readonly List<FaceSlot> faceSlots = new List<FaceSlot>();
        private readonly List<UpgradeOption> upgradeOptions = new List<UpgradeOption>();
        private readonly List<Tweener> activeTweens = new List<Tweener>();

        // 状态
        private int selectedFaceIndex = -1;       // 选中的面索引
        private string selectedEffectId = null;   // 选中的升级效果ID
        private DiceRoller diceRoller;
        private int currentDiceIndex = 0;

        // 可用的升级效果列表
        private static readonly string[] AvailableEffects = new[]
        {
            "lightning",  // ⚡闪电 — 连锁闪电x3
            "shield",     // 🛡护盾 — 全体护盾+15%
            "heal",       // 💚治疗 — 全体回复10%
            "poison",     // ☠毒素 — 中毒5%/回合
            "critical",   // 💥暴击 — 必暴+50%爆伤
        };

        protected override void Awake()
        {
            base.Awake();

            confirmButton?.onClick.AddListener(OnConfirmClicked);
            skipButton?.onClick.AddListener(OnSkipClicked);
            prevDiceButton?.onClick.AddListener(() => SwitchDice(-1));
            nextDiceButton?.onClick.AddListener(() => SwitchDice(1));
        }

        protected override void OnShow()
        {
            selectedFaceIndex = -1;
            selectedEffectId = null;

            // 获取骰子数据
            var rgm = RoguelikeGameManager.Instance;
            if (rgm != null && rgm.DiceRoller != null)
                diceRoller = rgm.DiceRoller;
            else if (GameManager.Instance != null)
                diceRoller = GameManager.Instance.DiceRoller;

            currentDiceIndex = diceIndex;
            RefreshPanel();

            UpdateStatus("请选择要升级的骰子面");
            if (confirmButton) confirmButton.interactable = false;
        }

        protected override void OnHide()
        {
            KillAllTweens();
            diceRoller = null;
        }

        #region 面板刷新

        /// <summary>
        /// 完整刷新面板
        /// </summary>
        public void RefreshPanel()
        {
            ClearFaceSlots();
            ClearUpgradeOptions();
            KillAllTweens();

            if (diceRoller == null || diceRoller.Dices == null || diceRoller.Dices.Length == 0)
            {
                UpdateStatus("没有可用骰子");
                return;
            }

            // 骰子切换按钮
            int totalDice = diceRoller.Dices.Length;
            bool multiDice = totalDice > 1;
            if (prevDiceButton) prevDiceButton.interactable = multiDice && currentDiceIndex > 0;
            if (nextDiceButton) nextDiceButton.interactable = multiDice && currentDiceIndex < totalDice - 1;
            if (diceIndexText) diceIndexText.text = multiDice ? $"骰子 {currentDiceIndex + 1}/{totalDice}" : "骰子";

            currentDiceIndex = Mathf.Clamp(currentDiceIndex, 0, totalDice - 1);
            var dice = diceRoller.Dices[currentDiceIndex];

            // 创建6面展示
            CreateFaceSlots(dice);

            // 创建升级选项
            CreateUpgradeOptions();

            // 重置选择
            selectedFaceIndex = -1;
            selectedEffectId = null;
            if (confirmButton) confirmButton.interactable = false;
        }

        #endregion

        #region 骰子面展示

        /// <summary>
        /// 创建6个面的展示槽位
        /// </summary>
        private void CreateFaceSlots(Dice dice)
        {
            int faceCount = dice.Faces.Length;

            for (int i = 0; i < faceCount; i++)
            {
                FaceSlot slot;

                if (faceSlotPrefab != null)
                {
                    var go = Instantiate(faceSlotPrefab, faceContainer);
                    slot = ExtractFaceSlot(go.transform, i);
                }
                else
                {
                    slot = CreateFaceSlotProcedurally(i, faceCount, dice);
                }

                // 填充数据
                slot.faceIndex = i;
                string effect = dice.FaceEffects[i];
                slot.hasSpecialEffect = !string.IsNullOrEmpty(effect);

                if (slot.valueText != null)
                    slot.valueText.text = dice.Faces[i].ToString();

                if (slot.effectText != null)
                {
                    if (slot.hasSpecialEffect)
                    {
                        var displayData = UIConfigBridge.GetSpecialFaceDisplay(effect);
                        slot.effectText.text = displayData.nameCN;
                        slot.effectText.color = displayData.color;
                        if (slot.borderImage != null)
                            slot.borderImage.color = displayData.color;
                    }
                    else
                    {
                        slot.effectText.text = "";
                    }
                }

                // 入场动画
                if (slot.rect != null)
                {
                    slot.rect.localScale = Vector3.zero;
                    var t = slot.rect.DOScale(1f, slotAnimDuration)
                        .SetEase(Ease.OutBack)
                        .SetDelay(i * 0.06f);
                    activeTweens.Add(t);
                }

                faceSlots.Add(slot);
            }
        }

        /// <summary>
        /// 程序化创建面槽位
        /// </summary>
        private FaceSlot CreateFaceSlotProcedurally(int index, int totalFaces, Dice dice)
        {
            var slot = new FaceSlot();

            // 根节点 — 横向排列
            var rootGO = new GameObject($"Face_{index}");
            var rootRT = rootGO.AddComponent<RectTransform>();
            rootRT.SetParent(faceContainer, false);

            // 计算水平等分布局
            float slotWidth = 72f;
            float gap = 10f;
            float totalWidth = totalFaces * slotWidth + (totalFaces - 1) * gap;
            float startX = -totalWidth / 2f + slotWidth / 2f;

            rootRT.sizeDelta = new Vector2(slotWidth, 90f);
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = new Vector2(startX + index * (slotWidth + gap), 0f);

            // 背景按钮
            var btn = rootGO.AddComponent<Button>();
            var bgImg = rootGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(() => OnFaceClicked(index));
            slot.bgImage = bgImg;
            slot.selectButton = btn;

            // 边框
            var borderGO = new GameObject("Border");
            var borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.SetParent(rootRT, false);
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = new Vector2(-4f, -4f);
            slot.borderImage = borderGO.AddComponent<Image>();
            slot.borderImage.color = new Color(0.4f, 0.4f, 0.45f, 0.6f);
            slot.borderImage.type = Image.Type.Sliced;

            // 面值数字
            var valueGO = new GameObject("Value");
            var valueRT = valueGO.AddComponent<RectTransform>();
            valueRT.SetParent(rootRT, false);
            valueRT.anchorMin = new Vector2(0f, 0.5f);
            valueRT.anchorMax = new Vector2(1f, 1f);
            valueRT.offsetMin = new Vector2(0f, -2f);
            valueRT.offsetMax = new Vector2(0f, -2f);
            var valueTxt = valueGO.AddComponent<Text>();
            valueTxt.text = dice.Faces[index].ToString();
            valueTxt.fontSize = 24;
            valueTxt.fontStyle = FontStyle.Bold;
            valueTxt.alignment = TextAnchor.MiddleCenter;
            valueTxt.color = Color.white;
            valueTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.valueText = valueTxt;

            // 效果标签
            var effectGO = new GameObject("Effect");
            var effectRT = effectGO.AddComponent<RectTransform>();
            effectRT.SetParent(rootRT, false);
            effectRT.anchorMin = new Vector2(0f, 0f);
            effectRT.anchorMax = new Vector2(1f, 0.5f);
            effectRT.offsetMin = new Vector2(2f, 2f);
            effectRT.offsetMax = new Vector2(-2f, 2f);
            var effectTxt = effectGO.AddComponent<Text>();
            effectTxt.fontSize = 9;
            effectTxt.alignment = TextAnchor.MiddleCenter;
            effectTxt.color = new Color(0.8f, 0.8f, 0.8f);
            effectTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.effectText = effectTxt;

            slot.rect = rootRT;

            return slot;
        }

        private FaceSlot ExtractFaceSlot(Transform t, int index)
        {
            var slot = new FaceSlot
            {
                rect = t as RectTransform,
                valueText = t.Find("Value")?.GetComponent<Text>(),
                bgImage = t.GetComponent<Image>(),
                borderImage = t.Find("Border")?.GetComponent<Image>(),
                effectText = t.Find("Effect")?.GetComponent<Text>(),
                selectButton = t.GetComponent<Button>(),
                faceIndex = index
            };

            if (slot.selectButton != null)
            {
                slot.selectButton.onClick.AddListener(() => OnFaceClicked(index));
            }

            return slot;
        }

        #endregion

        #region 升级选项

        /// <summary>
        /// 创建升级选项列表
        /// </summary>
        private void CreateUpgradeOptions()
        {
            for (int i = 0; i < AvailableEffects.Length; i++)
            {
                var effectId = AvailableEffects[i];
                var displayData = UIConfigBridge.GetSpecialFaceDisplay(effectId);
                UpgradeOption option;

                if (upgradeOptionPrefab != null)
                {
                    var go = Instantiate(upgradeOptionPrefab, upgradeContainer);
                    option = ExtractUpgradeOption(go.transform, effectId);
                }
                else
                {
                    option = CreateUpgradeOptionProcedurally(i, effectId, displayData);
                }

                // 填充数据
                if (option.nameText != null)
                    option.nameText.text = displayData.nameCN;

                if (option.descText != null)
                    option.descText.text = displayData.description;

                if (option.iconText != null)
                {
                    // 从 nameCN 中提取 emoji（第一个字符）
                    option.iconText.text = displayData.nameCN.Length > 0
                        ? displayData.nameCN.Substring(0, 1)
                        : "?";
                    option.iconText.fontSize = 20;
                    option.iconText.alignment = TextAnchor.MiddleCenter;
                    option.iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                if (option.iconBg != null)
                    option.iconBg.color = displayData.color;

                // 入场动画
                if (option.rect != null)
                {
                    option.rect.localScale = Vector3.zero;
                    var t = option.rect.DOScale(1f, optionAnimDuration)
                        .SetEase(Ease.OutBack)
                        .SetDelay(0.3f + i * 0.08f);
                    activeTweens.Add(t);
                }

                upgradeOptions.Add(option);
            }
        }

        /// <summary>
        /// 程序化创建升级选项
        /// </summary>
        private UpgradeOption CreateUpgradeOptionProcedurally(int index, string effectId, DiceFaceDisplayData displayData)
        {
            var option = new UpgradeOption { effectId = effectId };

            // 根节点 — 垂直列表
            var rootGO = new GameObject($"Upgrade_{effectId}");
            var rootRT = rootGO.AddComponent<RectTransform>();
            rootRT.SetParent(upgradeContainer, false);
            rootRT.sizeDelta = new Vector2(280f, 56f);
            rootRT.anchorMin = new Vector2(0.5f, 1f);
            rootRT.anchorMax = new Vector2(0.5f, 1f);
            rootRT.pivot = new Vector2(0.5f, 1f);
            rootRT.anchoredPosition = new Vector2(0f, -index * 64f - 10f);

            // 背景按钮
            var btn = rootGO.AddComponent<Button>();
            var bgImg = rootGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(() => OnUpgradeClicked(effectId));

            // 图标背景
            var iconBgGO = new GameObject("IconBg");
            var iconBgRT = iconBgGO.AddComponent<RectTransform>();
            iconBgRT.SetParent(rootRT, false);
            iconBgRT.anchorMin = new Vector2(0f, 0.5f);
            iconBgRT.anchorMax = new Vector2(0f, 0.5f);
            iconBgRT.sizeDelta = new Vector2(40f, 40f);
            iconBgRT.anchoredPosition = new Vector2(25f, 0f);
            iconBgRT.pivot = new Vector2(0.5f, 0.5f);
            option.iconBg = iconBgGO.AddComponent<Image>();
            option.iconBg.color = displayData.color;

            // 图标文字 (emoji)
            var iconTxtGO = new GameObject("IconText");
            var iconTxtRT = iconTxtGO.AddComponent<RectTransform>();
            iconTxtRT.SetParent(iconBgRT, false);
            iconTxtRT.anchorMin = Vector2.zero;
            iconTxtRT.anchorMax = Vector2.one;
            iconTxtRT.sizeDelta = Vector2.zero;
            option.iconText = iconTxtGO.AddComponent<Text>();
            option.iconText.fontSize = 20;
            option.iconText.alignment = TextAnchor.MiddleCenter;
            option.iconText.color = Color.white;
            option.iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 名称
            var nameGO = new GameObject("Name");
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.SetParent(rootRT, false);
            nameRT.anchorMin = new Vector2(0f, 0.5f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = new Vector2(55f, -2f);
            nameRT.offsetMax = new Vector2(-10f, -2f);
            option.nameText = nameGO.AddComponent<Text>();
            option.nameText.fontSize = 14;
            option.nameText.fontStyle = FontStyle.Bold;
            option.nameText.color = displayData.color;
            option.nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 描述
            var descGO = new GameObject("Desc");
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.SetParent(rootRT, false);
            descRT.anchorMin = new Vector2(0f, 0f);
            descRT.anchorMax = new Vector2(1f, 0.5f);
            descRT.offsetMin = new Vector2(55f, 2f);
            descRT.offsetMax = new Vector2(-10f, 2f);
            option.descText = descGO.AddComponent<Text>();
            option.descText.fontSize = 11;
            option.descText.color = new Color(0.7f, 0.7f, 0.7f);
            option.descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            option.rect = rootRT;

            return option;
        }

        private UpgradeOption ExtractUpgradeOption(Transform t, string effectId)
        {
            var option = new UpgradeOption
            {
                rect = t as RectTransform,
                nameText = t.Find("Name")?.GetComponent<Text>(),
                descText = t.Find("Desc")?.GetComponent<Text>(),
                iconBg = t.Find("IconBg")?.GetComponent<Image>(),
                iconText = t.Find("IconBg/IconText")?.GetComponent<Text>(),
                selectButton = t.GetComponent<Button>(),
                effectId = effectId
            };

            if (option.selectButton != null)
                option.selectButton.onClick.AddListener(() => OnUpgradeClicked(effectId));

            return option;
        }

        #endregion

        #region 交互

        /// <summary>
        /// 点击某个面 — 选中该面
        /// </summary>
        private void OnFaceClicked(int faceIndex)
        {
            // 取消之前选中
            foreach (var s in faceSlots)
            {
                if (s.bgImage != null)
                    s.bgImage.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            }

            selectedFaceIndex = faceIndex;

            // 高亮选中面
            var slot = faceSlots.Find(s => s.faceIndex == faceIndex);
            if (slot != null)
            {
                if (slot.bgImage != null)
                    slot.bgImage.color = new Color(0.3f, 0.5f, 0.8f, 0.95f);

                // 弹跳动画
                if (slot.rect != null)
                {
                    activeTweens.Add(slot.rect.DOScale(1.1f, 0.1f).SetLoops(2, LoopType.Yoyo));
                }
            }

            UpdateStatus($"已选第 {faceIndex + 1} 面 → 请选择升级效果");
            TryEnableConfirm();
        }

        /// <summary>
        /// 点击某个升级选项
        /// </summary>
        private void OnUpgradeClicked(string effectId)
        {
            // 取消之前选中
            foreach (var opt in upgradeOptions)
            {
                if (opt.bgImage != null)
                {
                    // 通过 Button 的目标图形恢复
                    var img = opt.rect?.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                }
            }

            selectedEffectId = effectId;

            // 高亮选中项
            var selected = upgradeOptions.Find(o => o.effectId == effectId);
            if (selected != null)
            {
                var img = selected.rect?.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(0.2f, 0.35f, 0.2f, 0.95f);
            }

            var displayData = UIConfigBridge.GetSpecialFaceDisplay(effectId);
            UpdateStatus(selectedFaceIndex >= 0
                ? $"面 {selectedFaceIndex + 1} → {displayData.nameCN}: {displayData.description}"
                : $"已选 {displayData.nameCN} → 请选择要升级的面");

            TryEnableConfirm();
        }

        /// <summary>
        /// 两边都选了才能确认
        /// </summary>
        private void TryEnableConfirm()
        {
            if (confirmButton != null)
                confirmButton.interactable = selectedFaceIndex >= 0 && !string.IsNullOrEmpty(selectedEffectId);
        }

        /// <summary>
        /// 确认升级
        /// </summary>
        private void OnConfirmClicked()
        {
            if (diceRoller == null || selectedFaceIndex < 0 || string.IsNullOrEmpty(selectedEffectId))
                return;

            var dice = diceRoller.Dices[currentDiceIndex];
            dice.UpgradeFace(selectedFaceIndex, selectedEffectId);

            var displayData = UIConfigBridge.GetSpecialFaceDisplay(selectedEffectId);
            Debug.Log($"[骰子升级] 骰子{currentDiceIndex + 1} 第{selectedFaceIndex + 1}面 → {displayData.nameCN}");

            // 播放升级动画
            var slot = faceSlots.Find(s => s.faceIndex == selectedFaceIndex);
            if (slot != null && slot.rect != null)
            {
                activeTweens.Add(
                    slot.rect.DOScale(1.3f, 0.15f).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        if (slot.rect != null)
                            activeTweens.Add(slot.rect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
                    })
                );
            }

            // 刷新面板
            RefreshPanel();
            UpdateStatus($"✅ 升级成功！第{selectedFaceIndex + 1}面 → {displayData.nameCN}");
        }

        /// <summary>
        /// 跳过升级
        /// </summary>
        private void OnSkipClicked()
        {
            Debug.Log("[骰子升级] 玩家跳过升级");
            Hide();
        }

        /// <summary>
        /// 切换骰子
        /// </summary>
        private void SwitchDice(int direction)
        {
            if (diceRoller == null) return;
            int newIndex = Mathf.Clamp(currentDiceIndex + direction, 0, diceRoller.Dices.Length - 1);
            if (newIndex != currentDiceIndex)
            {
                currentDiceIndex = newIndex;
                RefreshPanel();
            }
        }

        #endregion

        #region 辅助

        private void UpdateStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        #endregion

        #region 清理

        private void ClearFaceSlots()
        {
            foreach (var slot in faceSlots)
            {
                if (slot.rect != null)
                    Destroy(slot.rect.gameObject);
            }
            faceSlots.Clear();
        }

        private void ClearUpgradeOptions()
        {
            foreach (var opt in upgradeOptions)
            {
                if (opt.rect != null)
                    Destroy(opt.rect.gameObject);
            }
            upgradeOptions.Clear();
        }

        private void KillAllTweens()
        {
            foreach (var t in activeTweens)
            {
                if (t != null && t.IsActive())
                    t.Kill();
            }
            activeTweens.Clear();
        }

        protected override void OnDestroy()
        {
            KillAllTweens();
            base.OnDestroy();
        }

        #endregion
    }
}
