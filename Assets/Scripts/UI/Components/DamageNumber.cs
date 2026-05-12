using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 伤害飘字可复用组件 — FE-04.2
    /// FE-30: 使用 ObjectPool 管理，避免运行时 Instantiate/Destroy
    /// 
    /// 动画序列：
    /// 1. 缩放弹入（OutBack）
    /// 2. 上浮移动
    /// 3. 渐隐消失
    /// 
    /// 支持：
    /// - 普通伤害（白色/红色）
    /// - 暴击伤害（放大+特殊文字）
    /// - 治疗数字（绿色）
    /// - 自定义颜色（按骰子组合类型）
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [Header("显示元素")]
        public Text numberText;
        public Outline outline;

        [Header("动画参数")]
        public float riseDistance = 80f;
        public float riseDuration = 1.2f;
        public float fadeStartTime = 0.4f;
        public float critScaleMultiplier = 1.5f;
        public float normalScaleMultiplier = 1.2f;

        private Sequence animSequence;

        /// <summary>
        /// 播放伤害飘字动画
        /// </summary>
        public void Play(string text, Color color, bool isCrit = false, int fontSize = 0)
        {
            // 清理旧动画
            KillAnimation();

            // 设置文本
            if (numberText != null)
            {
                if (fontSize > 0)
                    numberText.fontSize = fontSize;
                else
                    numberText.fontSize = isCrit ? 42 : 32;

                numberText.text = text;
                numberText.color = color;
            }

            // 设置描边
            if (outline != null)
            {
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2, -2);
            }

            // 动画序列
            animSequence = DOTween.Sequence();
            var rect = transform as RectTransform;

            // 缩放弹入
            float targetScale = isCrit ? critScaleMultiplier : normalScaleMultiplier;
            animSequence.Append(rect.DOScale(Vector3.one * targetScale, 0.2f).SetEase(Ease.OutBack));

            // 上浮
            float startY = rect.anchoredPosition.y;
            animSequence.Join(rect.DOAnchorPosY(startY + riseDistance, riseDuration).SetEase(Ease.OutCubic));

            // 渐隐
            if (numberText != null)
            {
                animSequence.Insert(fadeStartTime, numberText.DOFade(0f, riseDuration - fadeStartTime));
            }

            // 完成后回收（不是销毁）
            animSequence.OnComplete(() =>
            {
                if (gameObject != null)
                {
                    Recycle();
                }
            });
        }

        /// <summary>
        /// 回收对象到池中
        /// </summary>
        void Recycle()
        {
            KillAnimation();
            gameObject.SetActive(false);

            var pool = ObjectPoolManager.Instance;
            if (pool != null)
                pool.ReleaseDamageNumber(this);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// 播放治疗飘字
        /// </summary>
        public void PlayHeal(int amount)
        {
            Play($"+{amount}", new Color(0.3f, 1f, 0.5f), false);
        }

        /// <summary>
        /// 播放暴击伤害飘字
        /// </summary>
        public void PlayCrit(int damage)
        {
            Play($\"暴击 -{damage}\", new Color(1f, 0.2f, 0.2f), true);
        }

        /// <summary>
        /// 播放普通伤害飘字
        /// </summary>
        public void PlayDamage(int damage)
        {
            Play($\"-{damage}\", new Color(1f, 0.6f, 0.4f), false);
        }

        private void KillAnimation()
        {
            if (animSequence != null)
            {
                animSequence.Kill();
                animSequence = null;
            }
        }

        private void OnDestroy()
        {
            KillAnimation();
        }

        // ========== 程序化创建（旧版） ==========

        /// <summary>
        /// 创建一个伤害飘字实例（使用对象池）
        /// </summary>
        /// <param name="parent">父容器</param>
        /// <param name="position">初始位置（局部坐标）</param>
        /// <param name="damage">伤害值</param>
        /// <param name="color">颜色</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="staggerDelay">延迟时间（用于多个飘字依次出现）</param>
        public static DamageNumber Spawn(RectTransform parent, Vector2 position, int damage, Color color, bool isCrit = false, float staggerDelay = 0f)
        {
            var pool = ObjectPoolManager.Instance;
            DamageNumber dmgNum;

            if (pool != null)
            {
                dmgNum = pool.GetDamageNumber();
                if (dmgNum != null)
                {
                    dmgNum.transform.SetParent(parent, false);
                    dmgNum.transform.localPosition = position;
                    dmgNum.gameObject.SetActive(true);
                    // Play 会在 OnComplete 时自动回收
                    if (staggerDelay > 0f)
                    {
                        DOVirtual.DelayedCall(staggerDelay, () =>
                        {
                            if (dmgNum != null && dmgNum.gameObject != null)
                                dmgNum.Play(isCrit ? $"暴击 -{damage}" : $"-{damage}", color, isCrit);
                        });
                    }
                    else
                    {
                        dmgNum.Play(isCrit ? $"暴击 -{damage}" : $"-{damage}", color, isCrit);
                    }
                    return dmgNum;
                }
            }

            // 回退：旧版程序化创建（直接 Destroy）
            var go = new GameObject("DmgNum");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200, 60);
            rect.localScale = Vector3.one;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = isCrit ? 42 : 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = isCrit ? $"暴击 -{damage}" : $"-{damage}";
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            var dmgNumOld = go.AddComponent<DamageNumber>();
            dmgNumOld.numberText = text;
            dmgNumOld.outline = outline;

            if (staggerDelay > 0f)
            {
                go.SetActive(false);
                DOVirtual.DelayedCall(staggerDelay, () =>
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                        dmgNumOld.Play(isCrit ? $"暴击 -{damage}" : $"-{damage}", color, isCrit);
                    }
                });
            }
            else
            {
                dmgNumOld.Play(isCrit ? $"暴击 -{damage}" : $"-{damage}", color, isCrit);
            }

            return dmgNumOld;
        }
    }
}
