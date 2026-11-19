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
    /// 卡牌列表界面：全屏半透明遮罩 + 中央面板 + 遮罩区滚动网格。
    /// 牌堆、弃牌堆、牌组、删牌都复用它。
    ///
    /// 结构：
    /// 遮罩（点击关闭） → 面板 → 标题 / 关闭按钮 / ScrollRect(viewport + RectMask2D + GridLayoutGroup)
    ///
    /// 卡牌用真实 <see cref="CardView"/>，所以悬停看背面、关键词提示直接可用；
    /// 点卡是否回调由 <c>onPick</c> 决定。
    /// </summary>
    public class CardListViewScreen : UGuiForm
    {
        public const int Columns = 5;
        public const float CardWidth = 250f;
        public const float CellGap = 22f;

        private RectTransform m_Panel;
        private RectTransform m_Content;
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI m_Subtitle;

        private readonly List<CardView> m_Cards = new List<CardView>();

        private void Awake()
        {
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(1920f, 1080f);

            // 半透明遮罩：点空白处关闭
            var overlay = gameObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.72f);
            overlay.raycastTarget = true;

            var overlayButton = gameObject.AddComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.onClick.AddListener(Close);

            // 中央面板
            RectTransform panel = UiFactory.CreateBox(root, "Panel",
                new Color(0.09f, 0.10f, 0.13f, 0.98f), new Vector2(1560f, 880f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            m_Panel = panel;

            // 面板本身吃掉点击，避免点到面板也关闭
            var panelBlocker = panel.gameObject.AddComponent<Button>();
            panelBlocker.transition = Selectable.Transition.None;

            m_Title = UiFactory.CreateLabel(panel, "Title", string.Empty, 42, Color.white,
                new Vector2(900f, 60f), new Vector2(0f, 1f), new Vector2(30f, -42f),
                TextAlignmentOptions.Left);

            m_Subtitle = UiFactory.CreateLabel(panel, "Subtitle", string.Empty, 22,
                new Color(0.72f, 0.75f, 0.82f), new Vector2(900f, 34f),
                new Vector2(0f, 1f), new Vector2(30f, -86f), TextAlignmentOptions.Left);

            BuildCloseButton(panel);
            BuildScroll(panel);
        }

        private void BuildCloseButton(RectTransform panel)
        {
            RectTransform rect = UiFactory.CreateBox(panel, "Close",
                new Color(0.18f, 0.22f, 0.30f), new Vector2(120f, 52f),
                new Vector2(1f, 1f), new Vector2(-80f, -46f));

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            TextMeshProUGUI caption = UiFactory.CreateLabel(rect, "Caption", "关闭", 24,
                Color.white, new Vector2(120f, 52f), new Vector2(0.5f, 0.5f), Vector2.zero);
            caption.raycastTarget = false;

            button.onClick.AddListener(Close);
        }

        private void BuildScroll(RectTransform panel)
        {
            RectTransform viewport = UiFactory.CreateRect(panel, "Viewport");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(30f, 30f);
            viewport.offsetMax = new Vector2(-30f, -110f);

            // RectMask2D 比 Mask 便宜，且不需要额外材质
            viewport.gameObject.AddComponent<RectMask2D>();

            // 手动排版：不用 GridLayoutGroup，避免它改写卡牌的 sizeDelta 导致「缩放两次」
            m_Content = UiFactory.CreateRect(viewport, "Content");
            m_Content.anchorMin = new Vector2(0f, 1f);
            m_Content.anchorMax = new Vector2(1f, 1f);
            m_Content.pivot = new Vector2(0.5f, 1f);
            m_Content.anchoredPosition = Vector2.zero;
            m_Content.sizeDelta = new Vector2(0f, 0f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = m_Content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 40f;
        }

        /// <summary>设置标题与副标题。</summary>
        public void SetHeader(string title, string subtitle = null)
        {
            if (m_Title != null) m_Title.text = title ?? string.Empty;
            if (m_Subtitle != null) m_Subtitle.text = subtitle ?? string.Empty;
        }

        /// <summary>
        /// 填入卡牌。onPick 为 null 表示只查看；不为 null 时点击任意一张都会回调。
        /// </summary>
        public void ShowCards(IList<CardDefinition> cards, Action<CardDefinition> onPick)
        {
            ClearCards();
            if (cards == null)
            {
                return;
            }

            float scale = CardWidth / CardView.CardWidth;
            float cardWidth = CardView.CardWidth * scale;
            float cardHeight = CardView.CardHeight * scale;

            int rows = Mathf.Max(1, Mathf.CeilToInt(cards.Count / (float)Columns));
            float contentHeight = rows * cardHeight + (rows - 1) * CellGap + CellGap * 2f;

            // 内容宽度跟随可视区，高度按行数撑开，ScrollRect 才能正确滚动
            m_Content.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                int inRow = Mathf.Min(Columns, cards.Count - row * Columns);

                float rowWidth = inRow * cardWidth + (inRow - 1) * CellGap;
                float left = -rowWidth * 0.5f + cardWidth * 0.5f;

                var instance = new CardInstance(cards[i]);
                CardView view = CardView.Create(m_Content, instance, scale);

                // 卡牌锚点在内容区中心（CardView 默认 0.5/0.5），所以从顶部往下算时要减去半高
                float centerY = contentHeight * 0.5f;
                view.SetHome(new Vector2(
                    left + column * (cardWidth + CellGap),
                    centerY - CellGap - cardHeight * 0.5f - row * (cardHeight + CellGap)));

                view.ChoiceMode = onPick != null;
                view.Interactable = onPick != null;

                if (onPick != null)
                {
                    CardDefinition picked = cards[i];
                    view.Clicked += _ => onPick(picked);
                }

                m_Cards.Add(view);
            }

            var scroll = m_Panel != null ? m_Panel.GetComponentInChildren<ScrollRect>(true) : null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void ClearCards()
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

        /// <summary>关闭回调，由打开方设置。</summary>
        public Action OnCloseRequested { get; set; }

        private void Close()
        {
            OnCloseRequested?.Invoke();
        }
    }
}
