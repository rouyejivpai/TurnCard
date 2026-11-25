using System;
using System.Collections.Generic;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using TurnCard.UIBind;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 卡牌列表界面：全屏半透明遮罩 + 中央面板 + 滚动网格。
    /// 牌堆、弃牌堆、牌组、删牌都复用它。
    ///
    /// 结构与坐标来自预制体 <c>Resources/UI/Flow/CardListViewScreenWindow</c>（UIBind 绑定）：
    /// <code>
    /// btnOverlay（遮罩，点击关闭） → goPanel（吃点击）
    ///                                  ├─ txtTitle / txtSubtitle / btnClose
    ///                                  └─ scrollList（RectMask2D + ScrollRect）
    ///                                       └─ goContent（按行摆放卡牌）
    /// </code>
    /// 卡牌用真实 <see cref="CardView"/>，所以悬停看背面、关键词提示直接可用；
    /// 点卡是否回调由 <c>onPick</c> 决定。
    /// </summary>
    public partial class CardListViewScreen : UIBindWindow
    {
        /// <summary>预制体路径（Resources 下，无扩展名）。</summary>
        public const string ResourcePath = "UI/Flow/CardListViewScreenWindow";

        public const int Columns = 5;
        public const float CardWidth = 250f;
        public const float CellGap = 22f;

        private readonly List<CardView> m_Cards = new List<CardView>();

        /// <summary>关闭回调，由打开方设置。</summary>
        public Action OnCloseRequested { get; set; }

        protected override void OnShow()
        {
            // 预制体里的字体是生成当时写进去的，这里统一成当前全局字体（像素字 + 回退链）
            UiFactory.EnsureFont(transform);

            // OnShow 可能被多次调用（重新打开），先清监听避免叠加
            if (btnOverlay != null)
            {
                btnOverlay.onClick.RemoveAllListeners();
                btnOverlay.onClick.AddListener(Close);
            }

            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(Close);
            }
        }

        /// <summary>设置标题与副标题。</summary>
        public void SetHeader(string title, string subtitle = null)
        {
            if (txtTitle != null) txtTitle.text = title ?? string.Empty;
            if (txtSubtitle != null) txtSubtitle.text = subtitle ?? string.Empty;
        }

        /// <summary>
        /// 填入卡牌。onPick 为 null 表示只查看；不为 null 时点击任意一张都会回调。
        /// </summary>
        /// <summary>
        /// 用真实卡牌实例展示（选牌流程用）：点击回调直接给回同一个 CardInstance，
        /// 这样翻面状态、临时保留标记都不会丢。
        /// </summary>
        public void ShowCards(IList<CardInstance> cards, Action<CardInstance> onPick)
        {
            List<CardDefinition> definitions = new List<CardDefinition>();
            foreach (CardInstance card in cards)
            {
                definitions.Add(card != null ? card.Definition : null);
            }

            ShowCards(definitions, null);

            if (onPick == null || m_Cards == null)
            {
                return;
            }

            for (int i = 0; i < m_Cards.Count && i < cards.Count; i++)
            {
                CardInstance instance = cards[i];
                CardView view = m_Cards[i];
                if (view == null || instance == null)
                {
                    continue;
                }

                view.ChoiceMode = true;
                view.Interactable = true;
                view.Clicked += _ => onPick(instance);
            }
        }

        public void ShowCards(IList<CardDefinition> cards, Action<CardDefinition> onPick)
        {
            ClearCards();
            if (cards == null || goContent == null)
            {
                return;
            }

            float scale = CardWidth / CardView.CardWidth;
            float cardWidth = CardView.CardWidth * scale;
            float cardHeight = CardView.CardHeight * scale;

            int rows = Mathf.Max(1, Mathf.CeilToInt(cards.Count / (float)Columns));
            float contentHeight = rows * cardHeight + (rows - 1) * CellGap + CellGap * 2f;

            // 内容宽度跟随可视区，高度按行数撑开，ScrollRect 才能正确滚动
            goContent.sizeDelta = new Vector2(0f, contentHeight);

            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                int inRow = Mathf.Min(Columns, cards.Count - row * Columns);

                float rowWidth = inRow * cardWidth + (inRow - 1) * CellGap;
                float left = -rowWidth * 0.5f + cardWidth * 0.5f;

                var instance = new CardInstance(cards[i]);
                CardView view = CardView.Create(goContent, instance, scale);

                // 战斗里打开牌组列表时也显示真实数值（非战斗时自动保持原值）
                CardPreviewSource.Apply(view, instance.Face);

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

            if (scrollList != null)
            {
                scrollList.verticalNormalizedPosition = 1f;
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

        private void Close()
        {
            OnCloseRequested?.Invoke();
        }
    }
}
