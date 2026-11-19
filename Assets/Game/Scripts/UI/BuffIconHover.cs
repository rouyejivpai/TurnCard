using System.Collections.Generic;
using TurnCard.Game.Battle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 鼠标悬浮在 buff 图标上时，用「关键词说明框」同一套面板弹出解释。
    ///
    /// 只负责把鼠标事件转成 <see cref="KeywordTooltipPanel.ShowEntries"/> 调用，
    /// 外观与避让逻辑完全复用关键词提示，两处不会各自漂移。
    /// </summary>
    public sealed class BuffIconHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private RectTransform m_Anchor;
        private BuffContainer m_Container;
        private int m_BuffId;

        public static void Attach(GameObject target, RectTransform anchor,
            BuffContainer container, int buffId)
        {
            var hover = target.AddComponent<BuffIconHover>();
            hover.m_Anchor = anchor;
            hover.m_Container = container;
            hover.m_BuffId = buffId;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        /// <summary>右键也能钉住提示（和卡牌右键看背面一致）。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Show();
            }
        }

        private void Show()
        {
            if (m_Container == null || m_Anchor == null)
            {
                return;
            }

            BuffInstance buff = m_Container.Find(m_BuffId);
            if (buff == null || buff.Definition == null)
            {
                return;
            }

            var entries = new List<TipEntry>
            {
                new TipEntry(
                    buff.Definition.DisplayName + " ×" + buff.Stacks,
                    buff.Definition.Description),
            };

            Canvas canvas = m_Anchor.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            KeywordTooltipPanel.Ensure(canvas.transform).ShowEntries(m_Anchor, entries);
        }

        private void Hide()
        {
            Canvas canvas = m_Anchor != null ? m_Anchor.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                return;
            }

            KeywordTooltipPanel.Ensure(canvas.transform).Hide();
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
