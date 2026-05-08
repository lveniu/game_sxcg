using UnityEngine;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// UI面板基类 - 微信小游戏竖屏适配
    /// 所有面板继承此类，实现OnShow/OnHide
    /// </summary>
    public class UIPanel : MonoBehaviour
    {
        [Header("面板配置")]
        [Tooltip("面板标识，对应GameState")]
        public string panelId;
        
        [Tooltip("是否缓存面板（不销毁）")]
        public bool cachePanel = true;

        protected CanvasGroup canvasGroup;
        protected RectTransform rectTransform;

        /// <summary>面板是否正在显示</summary>
        public bool IsVisible { get; protected set; }

        /// <summary>面板隐藏完成后的回调（用于子面板流程串联）</summary>
        public event System.Action OnHidden;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            rectTransform = GetComponent<RectTransform>();
        }

        /// <summary>显示面板（带动画）</summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
            IsVisible = true;

            // DOTween 淡入动画
            canvasGroup.alpha = 0f;
            canvasGroup.DOKill();
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad);

            // 竖屏从底部滑入
            rectTransform.anchoredPosition = new Vector2(0, -50f);
            rectTransform.DOAnchorPosY(0f, 0.25f).SetEase(Ease.OutQuad);

            OnShow();
        }

        /// <summary>隐藏面板（带动画）</summary>
        public virtual void Hide()
        {
            IsVisible = false;

            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                gameObject.SetActive(false);
                OnHidden?.Invoke();
            });

            OnHide();
        }

        /// <summary>立即隐藏（无动画，用于初始化）</summary>
        public virtual void HideImmediate()
        {
            IsVisible = false;
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>子类重写：面板显示时调用</summary>
        protected virtual void OnShow() { }

        /// <summary>子类重写：面板隐藏时调用</summary>
        protected virtual void OnHide() { }
    }
}
