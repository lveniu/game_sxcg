using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// FE-08 Boss机制怪HUD — 战斗面板上方的Boss专属UI覆盖层
    /// 
    /// 检测敌方单位中是否有Boss，展示：
    /// 1. Boss大血条（分段/多阶段）
    /// 2. Boss名称 + 阶段指示器
    /// 3. 机制提示（如"下一阶段将释放AOE"）
    /// 4. Boss专属技能预警
    /// 
    /// 挂载方式：作为BattlePanel的子节点，或独立Overlay
    /// 数据源：BattleManager.Instance.enemyUnits 中的Boss单位
    /// Boss判定：Hero.IsBoss == true
    /// </summary>
    public class BossMechanicHUD : UIPanel
    {
        [Header("Boss血条")]
        public RectTransform bossHealthBarContainer;
        public Image bossHealthBarFill;         // 主血条填充
        public Image bossHealthBarBackground;   // 血条背景
        public Image bossHealthBarPhaseMarker;  // 阶段分割线
        public Text bossNameText;               // Boss名称
        public Text bossHealthText;             // HP数值 "1200/3000"
        public RectTransform phaseIndicators;   // 阶段指示点容器

        [Header("阶段显示")]
        public Text phaseText;                  // "阶段 1/3"
        public Image phaseBanner;               // 阶段切换横幅
        public Text phaseBannerText;            // 阶段切换文字 "第二阶段！"
        public float phaseBannerDuration = 2f;

        [Header("机制提示")]
        public RectTransform mechanicTipContainer;  // 机制提示容器
        public GameObject mechanicTipPrefab;        // 提示条预制体（可选）
        public Text mechanicTipText;            // 当前机制提示文字
        public float tipDisplayDuration = 4f;

        [Header("技能预警")]
        public RectTransform skillWarningOverlay;   // 全屏技能预警层
        public Image skillWarningFlash;             // 预警闪光
        public Text skillWarningText;               // "⚠ Boss即将释放全屏AOE！"
        public float warningDuration = 1.5f;

        [Header("动画配置")]
        public float healthBarSmoothDuration = 0.3f;
        public float shakeIntensity = 5f;
        public float shakeDuration = 0.2f;

        // 程序化创建的阶段指示点
        private readonly List<Image> phaseDots = new List<Image>();
        private readonly List<GameObject> mechanicTips = new List<GameObject>();
        private readonly List<Tweener> activeTweens = new List<Tweener>();

        // Boss状态追踪
        private Hero currentBoss;
        private int currentPhase = 0;
        private int totalPhases = 3;            // 默认3阶段
        private float lastHealthPercent = 1f;
        private bool isBossActive = false;

        // 阶段阈值（血量百分比）
        private static readonly float[] PHASE_THRESHOLDS = { 0.66f, 0.33f };

        protected override void Awake()
        {
            base.Awake();

            // 初始隐藏
            if (phaseBanner != null) phaseBanner.gameObject.SetActive(false);
            if (skillWarningOverlay != null) skillWarningOverlay.gameObject.SetActive(false);
        }

        protected override void OnShow()
        {
            // 检测Boss
            DetectBoss();
            RefreshDisplay();

            // 订阅战斗事件
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.OnBattleStarted += OnBattleStarted;
                bm.OnBattleEnded += OnBattleEnded;
            }

            // 订阅机制怪系统事件（事件驱动替代Update轮询）
            var mes = MechanicEnemySystem.Instance;
            if (mes != null)
            {
                mes.OnMechanicTriggered += OnMechanicTriggered;
                mes.OnBossPhaseChanged += OnBossPhaseChanged;
                mes.OnMechanicWarning += OnMechanicWarning;
                mes.OnMinionsSpawned += OnMinionsSpawned;
                mes.OnBombExploded += OnBombExploded;
            }
        }

        protected override void OnHide()
        {
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.OnBattleStarted -= OnBattleStarted;
                bm.OnBattleEnded -= OnBattleEnded;
            }

            // 取消订阅机制怪系统事件
            var mes = MechanicEnemySystem.Instance;
            if (mes != null)
            {
                mes.OnMechanicTriggered -= OnMechanicTriggered;
                mes.OnBossPhaseChanged -= OnBossPhaseChanged;
                mes.OnMechanicWarning -= OnMechanicWarning;
                mes.OnMinionsSpawned -= OnMinionsSpawned;
                mes.OnBombExploded -= OnBombExploded;
            }

            KillAllTweens();
            currentBoss = null;
            isBossActive = false;
        }

        private void Update()
        {
            if (!IsVisible || !isBossActive || currentBoss == null) return;

            RefreshHealthBar();
            CheckPhaseTransition();
        }

        #region Boss检测

        /// <summary>
        /// 从敌方单位中检测Boss
        /// </summary>
        private void DetectBoss()
        {
            currentBoss = null;
            isBossActive = false;
            currentPhase = 0;

            var bm = BattleManager.Instance;
            if (bm?.enemyUnits == null) return;

            foreach (var enemy in bm.enemyUnits)
            {
                if (enemy != null && enemy.IsBoss)
                {
                    currentBoss = enemy;
                    isBossActive = true;
                    currentPhase = 1;
                    lastHealthPercent = 1f;
                    break;
                }
            }

            // 没有Boss时隐藏HUD
            if (!isBossActive)
            {
                if (bossHealthBarContainer != null)
                    bossHealthBarContainer.gameObject.SetActive(false);
                return;
            }

            if (bossHealthBarContainer != null)
                bossHealthBarContainer.gameObject.SetActive(true);

            Debug.Log($"[BossHUD] 检测到Boss: {currentBoss.Data?.heroName ?? "Unknown"}");
        }

        #endregion

        #region 显示刷新

        private void RefreshDisplay()
        {
            if (!isBossActive || currentBoss == null)
            {
                if (bossHealthBarContainer != null)
                    bossHealthBarContainer.gameObject.SetActive(false);
                return;
            }

            // Boss名称
            if (bossNameText != null)
                bossNameText.text = $"\U0001F5E1 {currentBoss.Data?.heroName ?? "Boss"}";

            // 创建阶段指示
            CreatePhaseIndicators();

            // 初始血条
            RefreshHealthBar();

            // 阶段文字
            UpdatePhaseText();

            // 显示初始机制提示
            ShowMechanicTip(GetPhaseMechanicTip(currentPhase));
        }

        /// <summary>
        /// 刷新Boss血条（平滑过渡）
        /// </summary>
        private void RefreshHealthBar()
        {
            if (currentBoss == null || bossHealthBarFill == null) return;

            float hpPercent = currentBoss.MaxHealth > 0
                ? (float)currentBoss.CurrentHealth / currentBoss.MaxHealth
                : 0f;

            hpPercent = Mathf.Clamp01(hpPercent);

            // 平滑血条变化
            if (Mathf.Abs(hpPercent - lastHealthPercent) > 0.001f)
            {
                activeTweens.Add(
                    bossHealthBarFill.DOFillAmount(hpPercent, healthBarSmoothDuration)
                        .SetEase(Ease.OutQuad)
                );

                // 血量下降时轻微抖动
                if (hpPercent < lastHealthPercent && bossHealthBarContainer != null)
                {
                    activeTweens.Add(
                        bossHealthBarContainer.DOShakeAnchorPos(shakeDuration, shakeIntensity)
                    );
                }

                lastHealthPercent = hpPercent;
            }

            // HP文字
            if (bossHealthText != null)
            {
                bossHealthText.text = $"{Mathf.Max(0, currentBoss.CurrentHealth)}/{currentBoss.MaxHealth}";
            }

            // Boss死亡
            if (currentBoss.IsDead && isBossActive)
            {
                isBossActive = false;
                OnBossDefeated();
            }
        }

        /// <summary>
        /// 创建阶段指示点
        /// </summary>
        private void CreatePhaseIndicators()
        {
            // 清理旧的
            foreach (var dot in phaseDots)
            {
                if (dot != null) Destroy(dot.gameObject);
            }
            phaseDots.Clear();

            if (phaseIndicators == null) return;

            for (int i = 0; i < totalPhases; i++)
            {
                var dotGO = new GameObject($"PhaseDot_{i}");
                var dotRT = dotGO.AddComponent<RectTransform>();
                dotRT.SetParent(phaseIndicators, false);
                dotRT.sizeDelta = new Vector2(12f, 12f);

                float spacing = 20f;
                float startX = -(totalPhases - 1) * spacing / 2f;
                dotRT.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                dotRT.anchorMin = new Vector2(0.5f, 0.5f);
                dotRT.anchorMax = new Vector2(0.5f, 0.5f);
                dotRT.pivot = new Vector2(0.5f, 0.5f);

                var dotImg = dotGO.AddComponent<Image>();
                dotImg.color = i < currentPhase
                    ? new Color(1f, 0.3f, 0.3f)
                    : new Color(0.3f, 0.3f, 0.35f);
                phaseDots.Add(dotImg);
            }
        }

        private void UpdatePhaseText()
        {
            if (phaseText != null)
                phaseText.text = $"阶段 {currentPhase}/{totalPhases}";
        }

        /// <summary>
        /// 获取指定阶段的机制提示
        /// </summary>
        private string GetPhaseMechanicTip(int phase)
        {
            return phase switch
            {
                1 => "🎯 Boss处于第一阶段，攻击力较低",
                2 => "⚠ Boss进入狂暴状态！攻击力+50%",
                3 => "🔥 Boss释放终极技能！全屏AOE",
                _ => ""
            };
        }

        #endregion

        #region 阶段转换

        /// <summary>
        /// 检查Boss是否进入新阶段
        /// </summary>
        private void CheckPhaseTransition()
        {
            if (currentBoss == null || currentPhase >= totalPhases) return;

            float hpPercent = currentBoss.MaxHealth > 0
                ? (float)currentBoss.CurrentHealth / currentBoss.MaxHealth
                : 0f;

            int newPhase = currentPhase;
            for (int i = 0; i < PHASE_THRESHOLDS.Length && i < totalPhases - 1; i++)
            {
                if (hpPercent <= PHASE_THRESHOLDS[i])
                    newPhase = i + 2; // 阶段从1开始
            }

            if (newPhase > currentPhase)
            {
                OnPhaseChanged(currentPhase, newPhase);
                currentPhase = newPhase;
                UpdatePhaseText();
                CreatePhaseIndicators();
            }
        }

        /// <summary>
        /// 阶段切换动画
        /// </summary>
        private void OnPhaseChanged(int oldPhase, int newPhase)
        {
            Debug.Log($"[BossHUD] Boss阶段切换: {oldPhase} → {newPhase}");

            // 显示阶段横幅
            if (phaseBanner != null && phaseBannerText != null)
            {
                phaseBanner.gameObject.SetActive(true);
                phaseBannerText.text = newPhase switch
                {
                    2 => "⚠ 第二阶段！Boss狂暴化！",
                    3 => "🔥 终极阶段！全屏AOE预警！",
                    _ => $"第{newPhase}阶段！"
                };

                // 横幅动画
                phaseBanner.localScale = new Vector3(3f, 3f, 3f);
                activeTweens.Add(
                    phaseBanner.DOScale(1f, 0.4f).SetEase(Ease.OutBack)
                );
                activeTweens.Add(
                    DOTween.Sequence().SetLink(gameObject)
                        .AppendInterval(phaseBannerDuration)
                        .AppendCallback(() =>
                        {
                            if (phaseBanner != null)
                                phaseBanner.gameObject.SetActive(false);
                        })
                );
            }

            // 显示机制提示
            ShowMechanicTip(GetPhaseMechanicTip(newPhase));

            // 第三阶段显示技能预警
            if (newPhase >= 3)
            {
                ShowSkillWarning("⚠ Boss即将释放终极AOE！");
            }
        }

        #endregion

        #region 机制提示

        /// <summary>
        /// 显示机制提示条
        /// </summary>
        public void ShowMechanicTip(string tip)
        {
            if (string.IsNullOrEmpty(tip)) return;

            if (mechanicTipText != null)
            {
                mechanicTipText.text = tip;
            }

            // 程序化创建提示条
            if (mechanicTipContainer != null)
            {
                var tipGO = new GameObject("MechanicTip");
                var tipRT = tipGO.AddComponent<RectTransform>();
                tipRT.SetParent(mechanicTipContainer, false);
                tipRT.sizeDelta = new Vector2(300f, 30f);
                tipRT.anchorMin = new Vector2(0.5f, 1f);
                tipRT.anchorMax = new Vector2(0.5f, 1f);
                tipRT.pivot = new Vector2(0.5f, 1f);
                tipRT.anchoredPosition = new Vector2(0f, -mechanicTips.Count * 36f - 5f);

                var bgImg = tipGO.AddComponent<Image>();
                bgImg.color = new Color(0.8f, 0.2f, 0.1f, 0.85f);

                var txt = tipGO.AddComponent<Text>();
                txt.text = tip;
                txt.fontSize = 12;
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                mechanicTips.Add(tipGO);

                // 自动消失
                activeTweens.Add(
                    DOTween.Sequence().SetLink(gameObject)
                        .AppendInterval(tipDisplayDuration)
                        .AppendCallback(() =>
                        {
                            mechanicTips.Remove(tipGO);
                            if (tipGO != null) Destroy(tipGO);
                            RepositionTips();
                        })
                );
            }
        }

        private void RepositionTips()
        {
            for (int i = 0; i < mechanicTips.Count; i++)
            {
                if (mechanicTips[i] != null)
                {
                    var rt = mechanicTips[i].GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(0f, -i * 36f - 5f);
                }
            }
        }

        #endregion

        #region 技能预警

        /// <summary>
        /// 显示全屏技能预警
        /// </summary>
        public void ShowSkillWarning(string message)
        {
            if (skillWarningOverlay == null) return;

            skillWarningOverlay.gameObject.SetActive(true);

            if (skillWarningText != null)
                skillWarningText.text = message;

            if (skillWarningFlash != null)
            {
                // 红色闪烁
                skillWarningFlash.color = new Color(1f, 0f, 0f, 0.3f);
                activeTweens.Add(
                    skillWarningFlash.DOFade(0f, warningDuration)
                        .SetEase(Ease.InQuad)
                        .SetLoops(3, LoopType.Yoyo)
                );
            }

            // 自动隐藏
            activeTweens.Add(
                DOTween.Sequence().SetLink(gameObject)
                    .AppendInterval(warningDuration)
                    .AppendCallback(() =>
                    {
                        if (skillWarningOverlay != null)
                            skillWarningOverlay.gameObject.SetActive(false);
                    })
            );
        }

        #endregion

        #region 事件回调

        private void OnBattleStarted()
        {
            DetectBoss();
            RefreshDisplay();
        }

        private void OnBattleEnded(bool playerWon)
        {
            if (playerWon && isBossActive)
            {
                OnBossDefeated();
            }

            isBossActive = false;
        }

        private void OnBossDefeated()
        {
            Debug.Log("[BossHUD] Boss已被击败！");

            // 血条闪白后消失
            if (bossHealthBarFill != null)
            {
                activeTweens.Add(
                    bossHealthBarFill.DOColor(Color.white, 0.2f).SetLoops(3, LoopType.Yoyo)
                        .SetLink(gameObject)
                        .OnComplete(() =>
                    {
                        if (bossHealthBarContainer != null)
                        {
                            activeTweens.Add(
                                bossHealthBarContainer.DOScale(0f, 0.5f).SetEase(Ease.InBack)
                                    .SetLink(gameObject)
                            );
                        }
                    })
                );
            }
        }

        // ========== 机制怪系统事件回调 ==========

        /// <summary>Boss执行机制时（ShieldSwap/Reflect/Berserk等）</summary>
        private void OnMechanicTriggered(Hero boss, MechanicType type, string description)
        {
            if (boss != currentBoss) return;

            // 更新当前Boss引用（确保同步）
            currentBoss = boss;
            isBossActive = true;

            // 显示机制提示
            ShowMechanicTip($"⚙ {description}");

            // 根据机制类型显示不同的预警
            switch (type)
            {
                case MechanicType.Berserk:
                    ShowSkillWarning($"⚠ Boss狂暴！攻击力大幅提升！");
                    break;
                case MechanicType.StealthAssassin:
                    ShowSkillWarning($"⚠ Boss隐身！下次攻击将造成双倍伤害！");
                    break;
                case MechanicType.CurseSpread:
                    ShowSkillWarning($"⚠ Boss释放诅咒！全体持续掉血！");
                    break;
                case MechanicType.TimeBomb:
                    ShowSkillWarning($"💣 Boss安放炸弹！倒计时中...");
                    break;
                case MechanicType.ElementalShift:
                    ShowMechanicTip($"🔄 Boss切换了元素属性！");
                    break;
            }

            Debug.Log($"[BossHUD] 机制触发: {type} — {description}");
        }

        /// <summary>Boss阶段切换（66%→33%血量阈值）</summary>
        private void OnBossPhaseChanged(Hero boss, int newPhase, string tip)
        {
            if (boss != currentBoss) return;

            int oldPhase = currentPhase;
            currentPhase = newPhase;
            UpdatePhaseText();
            CreatePhaseIndicators();

            // 阶段横幅动画
            if (phaseBanner != null && phaseBannerText != null)
            {
                phaseBanner.gameObject.SetActive(true);
                phaseBannerText.text = $"第{newPhase}阶段！{tip}";

                phaseBanner.localScale = new Vector3(3f, 3f, 3f);
                activeTweens.Add(
                    phaseBanner.DOScale(1f, 0.4f).SetEase(Ease.OutBack)
                );
                activeTweens.Add(
                    DOTween.Sequence().SetLink(gameObject)
                        .AppendInterval(phaseBannerDuration)
                        .AppendCallback(() =>
                        {
                            if (phaseBanner != null)
                                phaseBanner.gameObject.SetActive(false);
                        })
                );
            }

            // 高阶段预警
            if (newPhase >= 2)
            {
                ShowMechanicTip(tip);
            }
            if (newPhase >= 3)
            {
                ShowSkillWarning($"⚠ 终极阶段！Boss释放全屏技能！");
            }

            Debug.Log($"[BossHUD] 阶段切换: {oldPhase} → {newPhase} ({tip})");
        }

        /// <summary>机制预警（如TimeBomb倒计时）</summary>
        private void OnMechanicWarning(Hero boss, string warning)
        {
            if (boss != currentBoss) return;
            ShowSkillWarning($"⚠ {warning}");
            Debug.Log($"[BossHUD] 机制预警: {warning}");
        }

        /// <summary>Boss召唤小怪（SpawnMinions/SplitOnDeath）</summary>
        private void OnMinionsSpawned(List<Hero> minions)
        {
            if (minions == null || minions.Count == 0) return;

            ShowMechanicTip($"👾 Boss召唤了 {minions.Count} 个小怪！");

            // 通知BattlePanel刷新敌方血条（通过BattleManager.enemyUnits自动刷新）
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                // BattlePanel的RefreshUnitBars()会在Update中自动检测新单位
                // 这里只需确保BossHUD的Boss引用仍然正确
                if (currentBoss != null && !currentBoss.IsDead)
                {
                    isBossActive = true;
                }
            }

            Debug.Log($"[BossHUD] 小怪生成: {minions.Count}个");
        }

        /// <summary>TimeBomb爆炸</summary>
        private void OnBombExploded(Hero boss, int totalDamage)
        {
            if (boss != currentBoss) return;

            ShowSkillWarning($"💣 炸弹爆炸！全体受到 {totalDamage} 伤害！");

            // 全屏红色闪烁（更强力的预警效果）
            if (skillWarningFlash != null)
            {
                skillWarningFlash.color = new Color(1f, 0.3f, 0f, 0.5f);
                activeTweens.Add(
                    skillWarningFlash.DOFade(0f, 0.8f).SetEase(Ease.InQuad)
                        .SetLoops(2, LoopType.Yoyo)
                );
            }

            Debug.Log($"[BossHUD] 炸弹爆炸: {totalDamage}伤害");
        }

        #endregion

        #region 清理

        private void KillAllTweens()
        {
            foreach (var t in activeTweens)
            {
                if (t != null && t.IsActive())
                    t.Kill();
            }
            activeTweens.Clear();
        }

        private void ClearTips()
        {
            foreach (var tip in mechanicTips)
            {
                if (tip != null) Destroy(tip);
            }
            mechanicTips.Clear();
        }

        protected override void OnDestroy()
        {
            KillAllTweens();
            ClearTips();
            base.OnDestroy();
        }

        #endregion
    }
}
