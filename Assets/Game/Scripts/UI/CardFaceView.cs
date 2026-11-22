using TMPro;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 一张卡面（正面/背面共用同一个预制体）。
    ///
    /// 模板有两种模式：
    /// <list type="bullet">
    /// <item><b>整图模式</b>（默认）：背景是一张完整的卡框图，文字直接叠在图上的对应槽位。
    /// 改样子就换这张图。</item>
    /// <item><b>部件模式</b>：外框 / 横幅 / 角标 / 类型牌 / 底板各自一张图，由代码拼装。
    /// 需要单独换某个部件时才用。</item>
    /// </list>
    ///
    /// 两种模式的结构都只在这里定义一次，CardView 里放两个 <c>CardFace.prefab</c> 的嵌套实例，
    /// 改模板改一次，正反两面同时生效。
    /// </summary>
    public sealed class CardFaceView : MonoBehaviour
    {
        /// <summary>整图模式下卡框图的资源名（相对 Resources）。</summary>
        public const string CardBackgroundResource = "Art/UI/cards/card_frame_wood";

        [Header("模板模式")]
        [Tooltip("勾选 = 部件拼装（5 张图）；不勾 = 整图背景（1 张图 + 文字叠加）")]
        [SerializeField] private bool m_UsePartSprites;

        [SerializeField] private Image m_Background;
        [SerializeField] private Image m_Art;
        [SerializeField] private TextMeshProUGUI m_CostText;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private TextMeshProUGUI m_TypeText;
        [SerializeField] private TextMeshProUGUI m_DescriptionText;

        public bool UsePartSprites => m_UsePartSprites;
        public Image Background => m_Background;
        public Image Art => m_Art;
        public TextMeshProUGUI CostText => m_CostText;
        public TextMeshProUGUI NameText => m_NameText;
        public TextMeshProUGUI TypeText => m_TypeText;
        public TextMeshProUGUI DescriptionText => m_DescriptionText;

        // ---- 整图模式槽位（按 cardframeWood 逐行采样量的）----
        // 图内分区：横幅 y7..23 / 插画窗 y26..107 / 红丝带 y113..123 / 描述板 y124..176
        private const float CostCenterX = 20f;
        private const float CostCenterY = 20f;
        private const float CostBoxSize = 26f;
        private const float NameCenterY = 15f;
        private const float NameWidth = 104f;
        private const float NameHeight = 15f;
        private const float ArtTop = 26f;
        private const float ArtBottom = 88f;
        private const float ArtInset = 12f;
        private const float DescTop = 124f;
        private const float DescBottom = 19f;
        private const float DescInset = 12f;
        private const float TypeBottom = 23f;
        private const float TypeSizeX = 40f;
        private const float TypeSizeY = 11f;

        // ---- 部件模式槽位 ----
        private const float FrameBorderPixels = 54f;
        private const float FrameTargetWidthRatio = 0.14f;
        private const float BannerHeight = 38f;
        private const float BannerInset = 9f;
        private const float BadgeSize = 36f;
        private const float BadgeInset = 6f;
        private const float PanelHeight = 62f;
        private const float PanelInset = 8f;
        private const float PanelBottom = 8f;
        private const float ChipHeight = 18f;
        private const float ChipGap = 3f;

        /// <summary>导出成 CardView 使用的部件集合。</summary>
        public CardView.FaceWidgets ToWidgets()
        {
            var widgets = new CardView.FaceWidgets
            {
                Root = (RectTransform)transform,
                Background = m_Background,
                Art = m_Art,
                CostText = m_CostText,
                NameText = m_NameText,
                TypeText = m_TypeText,
                DescriptionText = m_DescriptionText,
                TintBackground = m_UsePartSprites,
            };

            if (widgets.Art == null)
            {
                widgets.Art = FindImage("Art");
            }

            if (widgets.CostText == null)
            {
                widgets.CostText = FindText("Cost");
            }

            if (widgets.NameText == null)
            {
                widgets.NameText = FindText("Name");
            }

            if (widgets.TypeText == null)
            {
                widgets.TypeText = FindText("Type");
            }

            if (widgets.DescriptionText == null)
            {
                widgets.DescriptionText = FindText("Description");
            }

            return widgets;
        }

        private Image FindImage(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private TextMeshProUGUI FindText(string childName)
        {
            // 先找直接子节点，再找子节点内部（部件模式文字挂在横幅/底板上）
            Transform direct = transform.Find(childName);
            if (direct != null)
            {
                var own = direct.GetComponent<TextMeshProUGUI>();
                if (own != null)
                {
                    return own;
                }
            }

            foreach (Transform sub in transform)
            {
                var nested = sub.Find(childName);
                if (nested != null)
                {
                    return nested.GetComponent<TextMeshProUGUI>();
                }
            }

            return null;
        }

        /// <summary>
        /// 用代码搭一份标准卡面。编辑器生成预制体、以及没有预制体时的运行时兜底都用它。
        /// <paramref name="backgroundSprite"/> 为空时尝试从 Resources 读整图卡框。
        /// </summary>
        public void BuildDefault(Sprite backgroundSprite = null)
        {
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardView.CardWidth, CardView.CardHeight);
            root.localEulerAngles = Vector3.zero;

            if (m_UsePartSprites)
            {
                BuildParts(root);
            }
            else
            {
                BuildSingleBackground(root, backgroundSprite);
            }
        }

        /// <summary>整图模式：一张卡框图打底，文字按图上槽位叠加。</summary>
        private void BuildSingleBackground(RectTransform root, Sprite backgroundSprite)
        {
            float width = CardView.CardWidth;
            float height = CardView.CardHeight;

            // 卡框图分区：顶部横幅和费用圆是浅色（用深字），
            // 底部描述区是深棕底板（用浅字），否则字和底色糊在一起。
            var ink = new Color(0.235f, 0.141f, 0.086f);
            var cream = new Color(0.96f, 0.92f, 0.84f);

            if (backgroundSprite == null)
            {
                backgroundSprite = Resources.Load<Sprite>(CardBackgroundResource);
            }

            // 背景：整张卡框图（就是这一张，不再拼各部件）
            var backdrop = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.SetParent(root, false);
            Stretch(backdropRect);
            m_Background = backdrop.GetComponent<Image>();
            m_Background.sprite = backgroundSprite;
            m_Background.type = Image.Type.Simple;
            m_Background.color = Color.white;
            m_Background.raycastTarget = true;

            // 插画窗（图上已留好位置，这里只放占位色块，之后换真图）
            RectTransform art = UiFactory.CreateRect(root, "Art");
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(S(ArtInset), S(ArtBottom));
            art.offsetMax = new Vector2(-S(ArtInset), -S(ArtTop));
            m_Art = art.gameObject.AddComponent<Image>();
            m_Art.color = CardView.ArtPlaceholderColor;
            m_Art.raycastTarget = false;

            // 费用：左上角圆形槽
            m_CostText = UiFactory.CreateLabel(root, "Cost", "0", S(14f), ink,
                new Vector2(S(CostBoxSize), S(CostBoxSize)), new Vector2(0f, 1f),
                new Vector2(S(CostCenterX), -S(CostCenterY)));
            Place(m_CostText.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(S(CostCenterX), -S(CostCenterY)));
            m_CostText.fontStyle = FontStyles.Bold;

            // 名称：顶部横幅
            m_NameText = UiFactory.CreateLabel(root, "Name", string.Empty, S(10f), ink,
                new Vector2(S(NameWidth), S(NameHeight)), new Vector2(0.5f, 1f),
                new Vector2(0f, -S(NameCenterY)));
            Place(m_NameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -S(NameCenterY)));
            m_NameText.enableAutoSizing = true;
            m_NameText.fontSizeMin = S(8f);
            m_NameText.fontSizeMax = S(10f);

            // 描述：底部深色底板
            m_DescriptionText = UiFactory.CreateLabel(root, "Description", string.Empty, S(7f), cream,
                new Vector2(width - S(DescInset) * 2f, height - S(DescTop) - S(DescBottom)),
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);
            var descRect = m_DescriptionText.rectTransform;
            descRect.anchorMin = Vector2.zero;
            descRect.anchorMax = Vector2.one;
            descRect.offsetMin = new Vector2(S(DescInset), S(DescBottom));
            descRect.offsetMax = new Vector2(-S(DescInset), -S(DescTop));
            m_DescriptionText.enableAutoSizing = true;
            m_DescriptionText.fontSizeMin = S(5f);
            m_DescriptionText.fontSizeMax = S(7f);

            // 类型：底板右下角
            m_TypeText = UiFactory.CreateLabel(root, "Type", string.Empty, S(8f),
                new Color(0.96f, 0.92f, 0.84f, 0.8f), new Vector2(S(TypeSizeX), S(TypeSizeY)),
                new Vector2(1f, 0f), new Vector2(-S(DescInset), S(TypeBottom)));
            Place(m_TypeText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f),
                new Vector2(-S(DescInset), S(TypeBottom)));
            m_TypeText.alignment = TextAlignmentOptions.Right;
        }

        /// <summary>部件模式：外框 / 横幅 / 角标 / 类型牌 / 底板各自一张图。</summary>
        private void BuildParts(RectTransform root)
        {
            float width = CardView.CardWidth;
            float height = CardView.CardHeight;
            var ink = new Color(0.235f, 0.141f, 0.086f);

            m_Background = gameObject.AddComponent<Image>();
            m_Background.color = CardView.CardBase;
            m_Background.raycastTarget = true;

            RectTransform frame = UiFactory.CreateRect(root, "Frame");
            Stretch(frame);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.raycastTarget = false;

            Image banner = CardSkin.CreatePart(root, "Banner", CardSkin.Banner,
                new Vector2(width - 18f, BannerHeight), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -BannerInset));

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

            m_CostText = UiFactory.CreateLabel(badge.transform, "Cost", "0", 20, ink,
                new Vector2(BadgeSize, BadgeSize), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_CostText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            float artTop = BannerHeight + 4f;
            float artBottom = PanelBottom + PanelHeight + ChipGap + ChipHeight + 4f;
            RectTransform art = UiFactory.CreateRect(root, "Art");
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(ArtInset, artBottom);
            art.offsetMax = new Vector2(-ArtInset, -artTop);
            m_Art = art.gameObject.AddComponent<Image>();
            m_Art.color = CardView.ArtPlaceholderColor;
            m_Art.raycastTarget = false;

            Image panel = CardSkin.CreatePart(root, "DescPanel", CardSkin.Panel,
                new Vector2(width - PanelInset * 2f, PanelHeight), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, PanelBottom));

            m_DescriptionText = UiFactory.CreateLabel(
                panel != null ? panel.transform : root, "Description", string.Empty, 10, ink,
                new Vector2(width - PanelInset * 2f - 14f, PanelHeight - 12f),
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);
            Place(m_DescriptionText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            m_DescriptionText.enableAutoSizing = true;
            m_DescriptionText.fontSizeMin = 7f;
            m_DescriptionText.fontSizeMax = 10f;

            Image chip = CardSkin.CreatePart(root, "TypeChip", CardSkin.Chip,
                new Vector2(62f, ChipHeight), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, PanelBottom + PanelHeight + ChipGap));

            m_TypeText = UiFactory.CreateLabel(
                chip != null ? chip.transform : root, "Type", string.Empty, 10, ink,
                new Vector2(62f, ChipHeight), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_TypeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            m_NameText = UiFactory.CreateLabel(
                banner != null ? banner.transform : root, "Name", string.Empty, 15, ink,
                new Vector2(width - 56f, BannerHeight - 8f), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(m_NameText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            m_NameText.enableAutoSizing = true;
            m_NameText.fontSizeMin = 9f;
            m_NameText.fontSizeMax = 15f;

            AssignPartSprites();
        }

        /// <summary>部件模式：把 5 张部件图直接赋到 Image 上。</summary>
        private void AssignPartSprites()
        {
            ApplySliced(FindImage("Frame"), "card_frame", FrameBorderPixels, FrameTargetWidthRatio, false);
            ApplySimple(FindImage("Banner"), "card_banner");
            ApplySimple(FindImage("DescPanel"), "card_desc_panel");
            ApplySimple(FindImage("CostBadge"), "card_cost_badge");
            ApplySimple(FindImage("TypeChip"), "card_type_chip");
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
                float target = CardView.CardWidth * targetBorderRatio;
                image.pixelsPerUnitMultiplier = Mathf.Clamp(
                    borderPixels / Mathf.Max(1f, target), 0.05f, 10f);
            }
        }

        /// <summary>美术像素 → 显示像素。</summary>
        private static float S(float artPixels)
        {
            return CardView.ArtPixels(artPixels);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
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
