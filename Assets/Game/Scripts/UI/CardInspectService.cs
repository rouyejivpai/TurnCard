using TurnCard.Game.Battle;
using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 卡牌背面查看服务。所有卡牌共用同一个预览面板（面板由卡牌预制体构成，
    /// 所以样式和真卡完全一致），固定在屏幕右上角显示。
    ///
    /// 宿主与面板都挂在根 Canvas 下：界面自己的 Canvas 会随界面销毁，
    /// 若面板挂在它下面，单例就会指向已销毁对象，下一次使用报 MissingReference。
    /// </summary>
    public sealed class CardInspectService : MonoBehaviour
    {
        private static CardInspectService s_Instance;
        private static bool s_Creating;

        private CardInspectPanel m_Panel;
        private bool m_Sticky;

        public static CardInspectService Ensure(Transform canvas)
        {
            // 正在创建面板时被再次请求（面板里的卡牌也会请求）。直接返回，避免无限套娃。
            if (s_Creating)
            {
                return s_Instance;
            }

            if (s_Instance != null && s_Instance.m_Panel != null)
            {
                return s_Instance;
            }

            if (s_Instance != null)
            {
                Destroy(s_Instance.gameObject);
                s_Instance = null;
            }

            Canvas self = canvas != null ? canvas.GetComponentInParent<Canvas>() : null;
            Transform root = self != null && self.rootCanvas != null
                ? self.rootCanvas.transform
                : canvas;

            var host = new GameObject("CardInspectService", typeof(RectTransform));
            host.transform.SetParent(root, false);

            s_Instance = host.AddComponent<CardInspectService>();

            s_Creating = true;
            try
            {
                s_Instance.m_Panel = CardInspectPanel.Create(root);
            }
            finally
            {
                s_Creating = false;
            }

            return s_Instance;
        }

        /// <summary>在右上角显示这张牌翻面之后的样子。sticky=true 表示右键触发，不自动收起。</summary>
        public void Show(CardInstance card, RectTransform cardRect, bool sticky)
        {
            if (m_Panel == null)
            {
                return;
            }

            m_Sticky = sticky;
            m_Panel.Show(card, cardRect);
        }

        public void HideIfTransient()
        {
            if (!m_Sticky && m_Panel != null)
            {
                m_Panel.Hide();
            }
        }

        public void Hide()
        {
            m_Sticky = false;
            if (m_Panel != null)
            {
                m_Panel.Hide();
            }
        }
    }
}