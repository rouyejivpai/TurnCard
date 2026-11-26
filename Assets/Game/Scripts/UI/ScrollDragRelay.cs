using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 把拖拽事件转给外层 ScrollRect。
    ///
    /// 列表里的卡牌自己是拖拽处理器（战斗里要拖动出牌），会吃掉拖拽事件，
    /// 导致「按住列表拖不动」。挂上这个组件后，卡牌上的拖拽会同时驱动列表滚动。
    /// </summary>
    public sealed class ScrollDragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ScrollRect Target;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target != null)
            {
                Target.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target != null)
            {
                Target.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Target != null)
            {
                Target.OnEndDrag(eventData);
            }
        }
    }
}
