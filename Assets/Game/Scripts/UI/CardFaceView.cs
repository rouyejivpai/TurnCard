using TMPro;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 一张卡面（正面/背面共用同一个预制体）。
    ///
    /// 之前正反面各手搓了一份结构，改一侧另一侧就漂移。现在结构只在这里定义一次，
    /// CardView 里放两个 <c>CardFace.prefab</c> 的嵌套实例即可：
    /// 改布局改一次，正反两面同时生效；颜色/图/文字各自用实例覆盖。
    /// </summary>
    public sealed class CardFaceView : MonoBehaviour
    {
        [SerializeField] private Image m_Background;
        [SerializeField] private Image m_Art;
        [SerializeField] private Image m_CostBadge;
        [SerializeField] private TextMeshProUGUI m_CostText;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private TextMeshProUGUI m_TypeText;
        [SerializeField] private TextMeshProUGUI m_DescriptionText;

        public Image Background => m_Background;
        public Image Art => m_Art;
        public Image CostBadge => m_CostBadge;
        public TextMeshProUGUI CostText => m_CostText;
        public TextMeshProUGUI NameText => m_NameText;
        public TextMeshProUGUI TypeText => m_TypeText;
        public TextMeshProUGUI DescriptionText => m_DescriptionText;

        /// <summary>导出成 CardView 使用的部件集合。</summary>
        public CardView.FaceWidgets ToWidgets()
        {
            var widgets = new CardView.FaceWidgets
            {
                Root = (RectTransform)transform,
                Background = m_Background,
                Art = m_Art,
                CostBadge = m_CostBadge,
                CostText = m_CostText,
                NameText = m_NameText,
                TypeText = m_TypeText,
                DescriptionText = m_DescriptionText,
            };

            // 老预制体可能只有序列化引用、没有挂组件，这里按名字兜一次
            if (widgets.Art == null)
            {
                Transform art = transform.Find("Art");
                if (art != null)
                {
                    widgets.Art = art.GetComponent<Image>();
                }
            }

            if (widgets.CostBadge == null)
            {
                Transform badge = transform.Find("CostBadge");
                if (badge != null)
                {
                    widgets.CostBadge = badge.GetComponent<Image>();
                    widgets.CostText = badge.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (widgets.NameText == null)
            {
                Transform banner = transform.Find("Banner");
                if (banner != null)
                {
                    widgets.NameText = banner.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (widgets.TypeText == null)
            {
                Transform chip = transform.Find("TypeChip");
                if (chip != null)
                {
                    widgets.TypeText = chip.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (widgets.DescriptionText == null)
            {
                Transform panel = transform.Find("DescPanel");
                if (panel != null)
                {
                    widgets.DescriptionText = panel.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            return widgets;
        }

        /// <summary>
        /// 用代码搭一份标准卡面（编辑器生成预制体、以及没有预制体时的运行时兜底都用它）。
        /// 背面不需要在这里翻转——朝向由外面的实例决定。
        /// </summary>
        public void BuildDefault()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            // 底色：露在外框镂空处的部分，按类型着色
            m_Background = gameObject.AddComponent<Image>();
            m_Background.color = CardView.CardBase;
            m_Background.raycastTarget = true;

            var cream = new Color(0.967f, 0.937f, 0.859f);
            var ink = new Color(0.235f, 0.141f, 0.086f);
            _ = cream;

            // 1) 外框：九宫格，四角保持斜切原样
            RectTransform frame = UiFactory.CreateRect(root, "Frame");
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.raycastTarget = false;
            CardSkin.ApplyFrame(frameImage);

            // 2) 顶部名称横幅
            Image banner = CardSkin.CreatePart(root, "Banner", CardSkin.Banner,
                new Vector2(190f, 52f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f));

            // 3) 左上费用角标
            Image badge = CardSkin.CreatePart(root, "CostBadge", CardSkin.Badge,
                new Vector2(44f, 44f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, -30f));
            if (badge == null)
            {
                RectTransform badgeRect = UiFactory.CreateBox(root, "CostBadgeFallback",
                    new Color(0.08f, 0.09f, 0.12f, 0.92f), new Vector2(44f, 44f),
                    new Vector2(0f, 1f), new Vector2(26f, -26f));
                badge = badgeRect.GetComponent<Image>();
            }

            m_CostBadge = badge;
            m_CostText = UiFactory.CreateLabel(badge.transform, "Cost", "0", 24, ink,
                new Vector2(44f, 44f), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_CostText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 4) 插画窗（暂时是纯色占位，压在框内）
            RectTransform art = UiFactory.CreateBox(root, "Art", CardView.ArtPlaceholderColor,
                new Vector2(140f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, -66f));
            m_Art = art.GetComponent<Image>();

            // 5) 类型小牌（文字体现类型）
            Image chip = CardSkin.CreatePart(root, "TypeChip", CardSkin.Chip,
                new Vector2(74f, 22f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f));

            m_TypeText = UiFactory.CreateLabel(
                chip != null ? chip.transform : root, "Type", string.Empty, 12, ink,
                new Vector2(74f, 20f), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_TypeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 6) 描述底板
            Image panel = CardSkin.CreatePart(root, "DescPanel", CardSkin.Panel,
                new Vector2(158f, 86f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f));

            m_DescriptionText = UiFactory.CreateLabel(
                panel != null ? panel.transform : root, "Description", string.Empty, 12, ink,
                new Vector2(144f, 70f), new Vector2(0.5f, 1f), new Vector2(0f, -8f),
                TextAlignmentOptions.Top);
            Place(m_DescriptionText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f));

            // 7) 名称文字（压在横幅上）
            m_NameText = UiFactory.CreateLabel(
                banner != null ? banner.transform : root, "Name", string.Empty, 20, ink,
                new Vector2(158f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));
            Place(m_NameText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));
        }

        /// <summary>文字统一摆位：CreateLabel 会把 pivot 固定成中心，这里显式改回来。</summary>
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }
    }
}
