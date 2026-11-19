using UnityEngine;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 界面组辅助器。给每个界面组的根节点挂 Canvas 与 GraphicRaycaster，
    /// 通过 sortingOrder 实现组之间的层次。
    /// </summary>
    public class UGuiGroupHelper : UIGroupHelperBase
    {
        private const int DepthFactor = 10000;

        private Canvas m_CachedCanvas;

        public override void SetDepth(int depth)
        {
            if (m_CachedCanvas == null)
            {
                return;
            }

            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = depth * DepthFactor;
        }

        private void Awake()
        {
            var rect = gameObject.GetOrAddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            m_CachedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = 0;

            gameObject.GetOrAddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
    }
}
