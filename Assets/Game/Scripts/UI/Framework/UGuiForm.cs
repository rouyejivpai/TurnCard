using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// UGUI 界面基类。负责 Canvas 与 CanvasGroup 的管理、按层级计算排序、
    /// 显隐与交互开关。由 <see cref="UGuiFormHelper"/> 在打开界面时挂到界面实例上。
    /// </summary>
    public class UGuiForm : UIFormLogic
    {
        private const int DepthFactor = 100;
        private const int BaseSortingOrder = 0;

        private Canvas m_CachedCanvas;
        private CanvasGroup m_CachedCanvasGroup;
        private CanvasScaler m_CachedCanvasScaler;

        /// <summary>原始层级，关闭时还原。</summary>
        public int OriginalLayer { get; private set; }

        /// <summary>原始排序。</summary>
        public int OriginalCanvasSortingOrder { get; private set; }

        public int CanvasSortingOrder
        {
            get => m_CachedCanvas != null ? m_CachedCanvas.sortingOrder : 0;
            set
            {
                if (m_CachedCanvas != null)
                {
                    m_CachedCanvas.sortingOrder = value;
                }
            }
        }

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = BaseSortingOrder;

            m_CachedCanvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();

            m_CachedCanvasScaler = gameObject.GetComponent<CanvasScaler>();
            OriginalLayer = gameObject.layer;
        }

        protected override void OnRecycle()
        {
            base.OnRecycle();
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetVisible(true);
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            SetVisible(false);
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            base.OnDepthChanged(uiGroupDepth, depthInUIGroup);
            CanvasSortingOrder = uiGroupDepth * DepthFactor + (depthInUIGroup + 1) * DepthFactor;
        }

        /// <summary>整体透明度。</summary>
        public void SetAlpha(float alpha)
        {
            if (m_CachedCanvasGroup != null)
            {
                m_CachedCanvasGroup.alpha = alpha;
            }
        }

        /// <summary>显隐（同时控制交互）。</summary>
        public void SetVisible(bool visible)
        {
            if (m_CachedCanvasGroup == null)
            {
                return;
            }

            m_CachedCanvasGroup.alpha = visible ? 1f : 0f;
            m_CachedCanvasGroup.interactable = visible;
            m_CachedCanvasGroup.blocksRaycasts = visible;
        }
    }
}
