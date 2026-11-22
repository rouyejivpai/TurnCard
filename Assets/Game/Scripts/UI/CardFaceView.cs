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
        /// <summary>外框贴图里的角部像素，九宫格用。</summary>
        private const float FrameBorderPixels = 54f;

        /// <summary>希望外框在屏上占卡宽的比例。</summary>
        private const float FrameTargetWidthRatio = 0.14f;

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
        /// 卡牌模板的排版规范（设计像素，基准分辨率 1920×1080）。
        ///
        /// 尺寸由外框美术反推：外框 481×551（比例 0.873），卡牌取 192×220（0.873），
        /// 这样九宫格拉伸时斜切角不变形。
        ///
        /// 内部不用绝对坐标硬摆，而是「贴边 + 固定高度」：
        /// 上：横幅 / 左上：费用角标 / 中：插画 / 下：类型牌 + 描述底板。
        /// 改卡牌尺寸时只需要改 CardWidth/CardHeight，内部会自动跟随。
        /// </summary>
        private const float BannerHeight = 38f;

        /// <summary>横幅距卡片顶边的距离（让开外框厚度）。</summary>
        private const float BannerInset = 9f;

        private const float BadgeSize = 36f;

        private const float BadgeInset = 6f;

        private const float DescHeight = 62f;

        private const float DescInset = 8f;

        private const float DescBottom = 8f;

        private const float ChipHeight = 18f;

        private const float ChipGap = 3f;

        /// <summary>插画区左右留白。</summary>
        private const float ArtInset = 14f;

        public void BuildDefault()
        {
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);

            float width = CardView.CardWidth;
            float height = CardView.CardHeight;

            // 底色：露在外框镂空处的部分，按类型着色
            m_Background = gameObject.AddComponent<Image>();
            m_Background.color = CardView.CardBase;
            m_Background.raycastTarget = true;

            var ink = new Color(0.235f, 0.141f, 0.086f);

            // 1) 外框：铺满整张卡，九宫格
            RectTransform frame = UiFactory.CreateRect(root, "Frame");
            Stretch(frame);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.raycastTarget = false;

            // 2) 顶部名称横幅：贴顶居中，固定高度
            Image banner = CardSkin.CreatePart(root, "Banner", CardSkin.Banner,
                new Vector2(width - 18f, BannerHeight), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -BannerInset));

            // 3) 左上费用角标：贴左上角，固定尺寸
            Image badge = CardSkin.CreatePart(root, "CostBadge", CardSkin.Badge,
                new Vector2(BadgeSize, BadgeSize), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(BadgeInset, -BadgeInset - 2f));

            if (badge == null)
            {
                RectTransform badgeRect = UiFactory.CreateBox(root, "CostBadgeFallback",
                    new Color(0.08f, 0.09f, 0.12f, 0.92f), new Vector2(BadgeSize, BadgeSize),
                    new Vector2(0f, 1f), new Vector2(BadgeInset, -BadgeInset));
                badge = badgeRect.GetComponent<Image>();
            }

            m_CostBadge = badge;
            m_CostText = UiFactory.CreateLabel(badge.transform, "Cost", "0", 20, ink,
                new Vector2(BadgeSize, BadgeSize), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_CostText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 4) 插画区：左右留白，上让开横幅、下让开类型牌
            float artTop = BannerHeight + 4f;
            float artBottom = DescBottom + DescHeight + ChipGap + ChipHeight + 4f;
            RectTransform art = UiFactory.CreateRect(root, "Art");
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = new Vector2(ArtInset, artBottom);
            art.offsetMax = new Vector2(-ArtInset, -artTop);
            var artImage = art.gameObject.AddComponent<Image>();
            artImage.color = CardView.ArtPlaceholderColor;
            artImage.raycastTarget = false;
            m_Art = artImage;

            // 5) 描述底板：贴底居中，固定高度
            Image panel = CardSkin.CreatePart(root, "DescPanel", CardSkin.Panel,
                new Vector2(width - DescInset * 2f, DescHeight), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, DescBottom));

            m_DescriptionText = UiFactory.CreateLabel(
                panel != null ? panel.transform : root, "Description", string.Empty, 10, ink,
                new Vector2(width - DescInset * 2f - 14f, DescHeight - 12f),
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);
            m_DescriptionText.enableAutoSizing = true;
            m_DescriptionText.fontSizeMin = 7f;
            m_DescriptionText.fontSizeMax = 10f;
            Place(m_DescriptionText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 6) 类型牌：压在描述底板上沿
            Image chip = CardSkin.CreatePart(root, "TypeChip", CardSkin.Chip,
                new Vector2(62f, ChipHeight), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, DescBottom + DescHeight + ChipGap));

            m_TypeText = UiFactory.CreateLabel(
                chip != null ? chip.transform : root, "Type", string.Empty, 10, ink,
                new Vector2(62f, ChipHeight), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_TypeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 7) 名称文字：压在横幅上
            m_NameText = UiFactory.CreateLabel(
                banner != null ? banner.transform : root, "Name", string.Empty, 15, ink,
                new Vector2(width - 56f, BannerHeight - 8f), new Vector2(0.5f, 0.5f), Vector2.zero);
            m_NameText.enableAutoSizing = true;
            m_NameText.fontSizeMin = 9f;
            m_NameText.fontSizeMax = 15f;
            Place(m_NameText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            AssignPartSprites();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }


        private void AssignPartSprites()
        {
            // 只有外框适合九宫格（四角固定、中间拉伸）；
            // 横幅/底板/角标/类型牌是按固定比例画的美术，用 Simple 整体缩放才不变形。
            // 底色不贴图：它是按卡牌类型染色的纯色块，露在外框镂空处。
            ApplySliced(FindImage("Frame"), "card_frame", FrameBorderPixels, FrameTargetWidthRatio, false);
            ApplySimple(FindImage("Banner"), "card_banner");
            ApplySimple(FindImage("DescPanel"), "card_desc_panel");
            ApplySimple(m_CostBadge, "card_cost_badge");
            ApplySimple(FindImage("TypeChip"), "card_type_chip");
        }

        private Image FindImage(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void ApplySimple(Image image, string spriteName)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>("Art/UI/card_parts/" + spriteName);
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }

        private static void ApplySliced(Image image, string spriteName, float borderPixels,
            float targetBorderRatio, bool fillCenter = true)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = Resources.Load<Sprite>("Art/UI/card_parts/" + spriteName);
            if (sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = fillCenter;
            image.color = Color.white;

            if (targetBorderRatio > 0f && borderPixels > 0f)
            {
                // 边框在屏上的宽度 = 卡宽 × 比例；multiplier 越大边框越细
                float target = CardView.CardWidth * targetBorderRatio;
                image.pixelsPerUnitMultiplier = Mathf.Clamp(borderPixels / Mathf.Max(1f, target), 0.05f, 10f);
            }
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
