using System;
using System.Collections.Generic;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 卡牌选择界面：把若干张牌按网格摆出来，点哪张就回调哪张。
    /// 奖励三选一、商店货架、营地/商店的删牌都复用它。
    ///
    /// 卡牌用 <see cref="CardView"/> 的真实视图创建，所以悬停看背面、关键词提示、
    /// 右键查看这些行为和手牌完全一致。
    /// </summary>
    public class CardGridScreen : UGuiForm
    {
        private RectTransform m_CardRoot;
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI m_Subtitle;
        private TextMeshProUGUI m_Hint;
        private RectTransform m_Bottom;

        private readonly List<CardView> m_Cards = new List<CardView>();

        private void Awake()
        {
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(1920f, 1080f);

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.065f, 0.08f);
            background.raycastTarget = true;

            m_Title = UiFactory.CreateLabel(root, "Title", string.Empty, 56, Color.white,
                new Vector2(1400f, 80f), new Vector2(0.5f, 1f), new Vector2(0f, -80f));

            m_Subtitle = UiFactory.CreateLabel(root, "Subtitle", string.Empty, 24,
                new Color(0.75f, 0.78f, 0.86f), new Vector2(1400f, 40f),
                new Vector2(0.5f, 1f), new Vector2(0f, -146f));

            m_CardRoot = UiFactory.CreateRect(root, "Cards");
            m_CardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            m_CardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            m_CardRoot.sizeDelta = new Vector2(1600f, 620f);
            m_CardRoot.anchoredPosition = new Vector2(0f, -30f);

            m_Bottom = UiFactory.CreateRect(root, "Bottom");
            m_Bottom.anchorMin = new Vector2(0.5f, 0f);
            m_Bottom.anchorMax = new Vector2(0.5f, 0f);
            m_Bottom.sizeDelta = new Vector2(900f, 120f);
            m_Bottom.anchoredPosition = new Vector2(0f, 130f);

            var layout = m_Bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            m_Hint = UiFactory.CreateLabel(root, "Hint", string.Empty, 22,
                new Color(0.62f, 0.65f, 0.72f), new Vector2(1400f, 34f),
                new Vector2(0.5f, 0f), new Vector2(0f, 66f));
        }

        /// <summary>三个标签：标题、副标题（可空）、底部提示（可空）。</summary>
        public void SetHeader(string title, string subtitle = null, string hint = null)
        {
            if (m_Title != null) m_Title.text = title ?? string.Empty;
            if (m_Subtitle != null) m_Subtitle.text = subtitle ?? string.Empty;
            if (m_Hint != null) m_Hint.text = hint ?? string.Empty;
        }

        public void ClearCards()
        {
            foreach (CardView card in m_Cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            m_Cards.Clear();
        }

        /// <summary>
        /// 摆出卡牌网格。columns 为每行几张，click 为点击回调（null 表示只展示不可点）。
        /// </summary>
        public void ShowCards(IList<CardDefinition> cards, float scale, int columns, Action<CardDefinition> click)
        {
            ClearCards();
            if (cards == null || cards.Count == 0)
            {
                return;
            }

            columns = Mathf.Max(1, columns);
            float cardWidth = CardView.CardWidth * scale;
            float cardHeight = CardView.CardHeight * scale;
            const float gapX = 56f;
            const float gapY = 84f;

            int rows = Mathf.CeilToInt(cards.Count / (float)columns);
            float totalHeight = rows * cardHeight + (rows - 1) * gapY;
            float top = totalHeight * 0.5f;

            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int inRow = Mathf.Min(columns, cards.Count - row * columns);

                float rowWidth = inRow * cardWidth + (inRow - 1) * gapX;
                float left = -rowWidth * 0.5f + cardWidth * 0.5f;

                Vector2 position = new Vector2(
                    left + col * (cardWidth + gapX),
                    top - cardHeight * 0.5f - row * (cardHeight + gapY));

                var instance = new CardInstance(cards[i]);
                CardView view = CardView.Create(m_CardRoot, instance, scale);
                view.SetHome(position);
                view.Interactable = click != null;
                m_Cards.Add(view);

                if (click != null)
                {
                    CardDefinition picked = cards[i];
                    view.Clicked += _ => click(picked);
                }
            }
        }

        /// <summary>底部加一个按钮。</summary>
        public Button AddButton(string label, Action onClick, bool interactable = true)
        {
            RectTransform rect = UiFactory.CreateBox(m_Bottom, "Btn_" + label,
                new Color(0.16f, 0.20f, 0.28f), new Vector2(260f, 68f),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 260f;
            element.preferredHeight = 68f;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.interactable = interactable;

            TextMeshProUGUI caption = UiFactory.CreateLabel(rect, "Caption", label, 26, Color.white,
                new Vector2(250f, 60f), new Vector2(0.5f, 0.5f), Vector2.zero);
            caption.raycastTarget = false;

            if (!interactable)
            {
                caption.color = new Color(0.45f, 0.47f, 0.52f);
                rect.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f);
            }
            else if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }
    }
}
