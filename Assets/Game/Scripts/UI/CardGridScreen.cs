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
    /// 卡牌选择界面：把若干张牌按网格摆出来，点哪张就回调哪张。
    /// 奖励三选一、商店货架、营地/商店的删牌都复用它。
    ///
    /// 结构与坐标来自预制体 <c>Resources/UI/Flow/CardGridScreenWindow</c>（UIBind 绑定），
    /// 卡的摆放位置仍由代码按数量算（卡数是运行期才知道的）。
    /// 卡牌用 <see cref="CardView"/> 的真实视图创建，所以悬停看背面、关键词提示、
    /// 右键查看这些行为和手牌完全一致。
    /// </summary>
    public partial class CardGridScreen : UIBindWindow
    {
        /// <summary>预制体路径（Resources 下，无扩展名）。</summary>
        public const string ResourcePath = "UI/Flow/CardGridScreenWindow";

        private readonly List<CardView> m_Cards = new List<CardView>();

        protected override void OnShow()
        {
            // 预制体里的字体是生成当时写进去的，这里统一成当前全局字体（像素字 + 回退链）
            UiFactory.EnsureFont(transform);
        }

        /// <summary>三个标签：标题、副标题（可空）、底部提示（可空）。</summary>
        public void SetHeader(string title, string subtitle = null, string hint = null)
        {
            if (txtTitle != null) txtTitle.text = title ?? string.Empty;
            if (txtSubtitle != null) txtSubtitle.text = subtitle ?? string.Empty;
            if (txtHint != null) txtHint.text = hint ?? string.Empty;
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
        public void ShowCards(IList<CardDefinition> cards, float scale, int columns,
            Action<CardDefinition> click)
        {
            ClearCards();
            if (cards == null || cards.Count == 0 || goCards == null)
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
                CardView view = CardView.Create(goCards, instance, scale);
                view.SetHome(position);
                view.Interactable = click != null;

                // 关键：奖励/商店这类「点一下就拿走」的卡牌必须走 ChoiceMode。
                // 否则 OnPointerClick 里会命中战斗规则 Card.Face.NeedsTarget → 直接 return，
                // 表现得就像「有些选项点不了」（攻击牌基本都中招）。
                view.ChoiceMode = click != null;

                m_Cards.Add(view);

                if (click != null)
                {
                    CardDefinition picked = cards[i];
                    view.Clicked += _ => click(picked);
                }
            }
        }

        /// <summary>底部加一个按钮（克隆预制体里的 btnTemplate）。</summary>
        public Button AddButton(string label, Action onClick, bool interactable = true)
        {
            if (btnTemplate == null)
            {
                Debug.LogError("[CardGridScreen] 预制体缺少 btnTemplate，无法加按钮：" + label);
                return null;
            }

            Button button = Instantiate(btnTemplate, goBottom, false);
            button.name = "Btn_" + label;
            button.gameObject.SetActive(true);
            button.interactable = interactable;

            TextMeshProUGUI caption = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (caption != null)
            {
                caption.text = label;
            }

            if (!interactable)
            {
                if (caption != null) caption.color = new Color(0.45f, 0.47f, 0.52f);
                var image = button.GetComponent<Image>();
                if (image != null) image.color = new Color(0.11f, 0.12f, 0.15f);
            }
            else if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }
    }
}
