using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 一张卡牌的视图。**正反两面各自有一套完整的卡面**（背景、费用、名称、类型、描述），
    /// 背面在构建时就旋转了 180°，翻面时旋转整张卡，转到 90° 时切换两面显隐，
    /// 因此玩家看到的是背面自己的字，而不是把正面内容换掉。
    ///
    /// 卡牌预制体 <c>Resources/UI/CardView</c> 提供视觉节点；预制体缺失时退回代码搭建。
    /// 敌人卡与背面预览也复用它。
    /// </summary>
    public sealed class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float CardWidth = 190f;
        public const float CardHeight = 268f;
        public const float LiftOffset = 46f;
        public const float InspectHoverSeconds = 0.8f;

        private const string PrefabResourcePath = "UI/CardView";

        public static readonly Color AttackColor = new Color(0.55f, 0.20f, 0.22f);
        public static readonly Color SkillColor = new Color(0.16f, 0.34f, 0.52f);

        /// <summary>卡牌底色：中性色，不随类型变化。</summary>
        public static readonly Color CardBase = new Color(0.16f, 0.16f, 0.19f);
        public static readonly Color PowerColor = new Color(0.36f, 0.26f, 0.52f);
        private static readonly Color ArtPlaceholderColor = new Color(0f, 0f, 0f, 0.22f);

        /// <summary>一面卡牌的全部可视节点。</summary>
        [Serializable]
        public sealed class FaceWidgets
        {
            public RectTransform Root;
            public Image Background;
            public Image Art;
            public Image CostBadge;
            public TextMeshProUGUI CostText;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI TypeText;
            public TextMeshProUGUI DescriptionText;
        }

        [Header("正面")]
        [SerializeField] private FaceWidgets m_FrontFace;

        [Header("背面（构建时已旋转 180°）")]
        [SerializeField] private FaceWidgets m_BackFace;

        private static CardView s_Prefab;

        private RectTransform m_Rect;
        private Canvas m_Canvas;
        private Vector2 m_HomePosition;
        private bool m_IsDragging;
        private float m_BaseScale = 1f;
        private Tween m_FlipTween;
        private Coroutine m_HoverRoutine;

        private Image m_Glow;
        private static Material s_GlowMaterial;
        private static readonly Color GlowColor = new Color(1f, 1f, 1f, 0.26f);

        private CardInspectService m_Inspect;
        private KeywordTooltipPanel m_KeywordTips;
        private CardFaceSide m_ShownSide = CardFaceSide.Front;
        private string m_FrontRaw = string.Empty;
        private string m_BackRaw = string.Empty;

        public CardInstance Card { get; private set; }

        /// <summary>是否允许这张卡请求「背面预览」。预览面板自己那张卡必须关掉，否则无限套娃。</summary>
        /// <summary>悬停时的放大倍率。</summary>
        public const float HoverScale = 1.25f;

        public bool AllowInspect = true;
        public Vector2 HomePosition => m_HomePosition;
        public bool Interactable { get; set; } = true;

        /// <summary>
        /// 选择模式：为 true 时任何牌（包括需要目标的攻击牌）点一下都会触发 Clicked。
        /// 用于「从牌组里选一张」这类场景。
        /// </summary>
        public bool ChoiceMode { get; set; }

        public bool IsDragging => m_IsDragging;
        public bool IsFlipping { get; private set; }
        public CardFaceSide ShownSide => m_ShownSide;

        public event Action<CardView> Clicked;
        public event Action<CardView, PointerEventData> DragMoved;
        public event Action<CardView, PointerEventData> DragFinished;

        /// <summary>创建一张手牌（或牌堆陈列）的视图。</summary>
        public static CardView Create(Transform parent, CardInstance card, float scale = 1f)
        {
            CardView view = InstantiateOrBuild(parent, scale);
            view.Card = card;
            view.BindCard(card);
            return view;
        }

        /// <summary>创建只用于展示的卡面，不绑定卡牌实例（敌人卡、背面预览）。</summary>
        public static CardView CreateDisplay(Transform parent, float scale = 1f)
        {
            return InstantiateOrBuild(parent, scale, false);
        }

        private static CardView InstantiateOrBuild(Transform parent, float scale, bool allowInspect = true)
        {
            CardView prefab = LoadPrefab();
            CardView view;

            if (prefab != null)
            {
                view = Instantiate(prefab, parent, false);
            }
            else
            {
                var go = new GameObject("CardView", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                view = go.AddComponent<CardView>();
                view.BuildDefaultHierarchy();
            }

            // 必须在 Initialize 之前设置：Initialize 里可能会去创建预览面板
            view.AllowInspect = allowInspect;
            view.transform.localScale = Vector3.one * scale;
            view.Initialize();
            return view;
        }

        private static CardView LoadPrefab()
        {
            if (s_Prefab == null)
            {
                s_Prefab = Resources.Load<CardView>(PrefabResourcePath);
            }

            return s_Prefab;
        }

        private void Initialize()
        {
            m_Rect = (RectTransform)transform;
            m_Canvas = GetComponentInParent<Canvas>();
            m_BaseScale = m_Rect.localScale.x;

            if (AllowInspect && m_Canvas != null)
            {
                m_Inspect = CardInspectService.Ensure(m_Canvas.transform);
            }

            EnsureFont();
            ApplySkin(m_FrontFace);
            ApplySkin(m_BackFace);
            BuildGlow();
        }



        /// <summary>
        /// 把部件精灵注入到卡面节点上。
        ///
        /// 无论卡面是预制体提供的还是代码搭的，这一步都在运行时执行——
        /// 因为部件精灵是运行时用 Sprite.Create 生成的，无法序列化进预制体。
        /// 这样预制体只管结构（层级、坐标、文字），皮肤由这里统一贴。
        /// </summary>
        private static void ApplySkin(FaceWidgets face)
        {
            if (face == null || face.Root == null)
            {
                return;
            }

            var frame = face.Root.Find("Frame");
            if (frame != null)
            {
                var image = frame.GetComponent<Image>();
                if (image != null)
                {
                    CardSkin.ApplyFrame(image);
                }
            }

            BindPart(face.Root, "Banner", CardSkin.Banner);
            BindPart(face.Root, "CostBadge", CardSkin.Badge);
            BindPart(face.Root, "TypeChip", CardSkin.Chip);
            BindPart(face.Root, "DescPanel", CardSkin.Panel);
        }

        private static void BindPart(Transform root, string name, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Transform node = root.Find(name);
            if (node == null)
            {
                return;
            }

            var image = node.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
            }
        }

        private void EnsureFont()
        {
            TMP_FontAsset font = UiFactory.Font;
            ApplyFont(m_FrontFace, font);
            ApplyFont(m_BackFace, font);
        }

        private static void ApplyFont(FaceWidgets face, TMP_FontAsset font)
        {
            if (face == null)
            {
                return;
            }

            if (face.CostText != null && face.CostText.font != font) face.CostText.font = font;
            if (face.NameText != null && face.NameText.font != font) face.NameText.font = font;
            if (face.TypeText != null && face.TypeText.font != font) face.TypeText.font = font;
            if (face.DescriptionText != null && face.DescriptionText.font != font)
                face.DescriptionText.font = font;
        }

        /// <summary>
        /// 按规范尺寸搭建两面卡牌。编辑器预制体生成器与运行时兜底共用，
        /// 保证两边结构一致。
        /// </summary>
        public void BuildDefaultHierarchy()
        {
            var rect = (RectTransform)transform;
            m_Rect = rect;
            m_BaseScale = rect.localScale.x;
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            BuildGlow();

            m_FrontFace = BuildFace("FrontFace", false);
            m_BackFace = BuildFace("BackFace", true);

            EnsureFont();
            ApplySkin(m_FrontFace);
            ApplySkin(m_BackFace);
            BuildGlow();
            ApplySide(CardFaceSide.Front, false, 0f, null, null);
        }

        /// <summary>
        /// 「当前可打出」的柔和白边。用 shader 按 UV 算一圈内发光，
        /// 不吃贴图，任何卡牌尺寸都能用。
        /// </summary>
        private void BuildGlow()
        {
            if (m_Glow != null)
            {
                return;
            }

            var existing = transform.Find("PlayableGlow");
            if (existing != null)
            {
                m_Glow = existing.GetComponent<Image>();
                if (m_Glow != null)
                {
                    return;
                }
            }

            var go = new GameObject("PlayableGlow", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // 外扩一圈，光晕才能露在卡牌外面而不是被卡面盖住
            rect.offsetMin = new Vector2(-6f, -6f);
            rect.offsetMax = new Vector2(6f, 6f);
            rect.SetAsFirstSibling();

            m_Glow = go.GetComponent<Image>();
            m_Glow.sprite = TurnCard.Game.UI.Fx.UiSprites.Square;
            m_Glow.type = Image.Type.Simple;
            m_Glow.raycastTarget = false;

            if (s_GlowMaterial == null)
            {
                Shader shader = Shader.Find("TurnCard/UIEdgeGlow");
                if (shader != null)
                {
                    s_GlowMaterial = new Material(shader);
                    s_GlowMaterial.SetColor("_Color", GlowColor);
                    s_GlowMaterial.SetFloat("_Width", 0.032f);
                }
            }

            if (s_GlowMaterial != null)
            {
                m_Glow.material = s_GlowMaterial;
            }
            else
            {
                m_Glow.color = new Color(1f, 1f, 1f, 0f);
            }

            SetPlayable(false);
        }

        /// <summary>当前是否「可以打出」。只有可打出的牌才显示光边。</summary>
        public void SetPlayable(bool playable)
        {
            if (m_Glow == null)
            {
                return;
            }

            m_Glow.enabled = playable;
        }

        private FaceWidgets BuildFace(string name, bool isBack)
        {
            var widgets = new FaceWidgets();

            RectTransform root = UiFactory.CreateRect(transform, name);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            if (isBack)
            {
                // 背面预先翻转 180°，整张卡转到 180° 时正好正对镜头
                root.localEulerAngles = new Vector3(0f, 180f, 0f);
            }

            widgets.Root = root;

            // 底色：露在外框镂空处的部分，按类型着色
            var background = root.gameObject.AddComponent<Image>();
            background.color = CardBase;
            background.raycastTarget = true;
            widgets.Background = background;

            var cream = new Color(0.967f, 0.937f, 0.859f);
            var ink = new Color(0.235f, 0.141f, 0.086f);

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

            widgets.CostBadge = badge;
            widgets.CostText = UiFactory.CreateLabel(badge.transform, "Cost", "0", 24, ink,
                new Vector2(44f, 44f), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(widgets.CostText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 4) 插画窗（暂时是纯色占位，压在框内）
            RectTransform art = UiFactory.CreateBox(root, "Art", ArtPlaceholderColor,
                new Vector2(140f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, -66f));
            widgets.Art = art.GetComponent<Image>();

            // 5) 类型小牌（文字体现类型）
            Image chip = CardSkin.CreatePart(root, "TypeChip", CardSkin.Chip,
                new Vector2(74f, 22f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f));

            widgets.TypeText = UiFactory.CreateLabel(
                chip != null ? chip.transform : root, "Type", string.Empty, 12, ink,
                new Vector2(74f, 20f), new Vector2(0.5f, 0.5f), Vector2.zero);
            Place(widgets.TypeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);

            // 6) 描述底板
            Image panel = CardSkin.CreatePart(root, "DescPanel", CardSkin.Panel,
                new Vector2(158f, 86f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f));

            widgets.DescriptionText = UiFactory.CreateLabel(
                panel != null ? panel.transform : root, "Description", string.Empty, 12, ink,
                new Vector2(144f, 70f), new Vector2(0.5f, 1f), new Vector2(0f, -8f),
                TextAlignmentOptions.Top);
            Place(widgets.DescriptionText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f));

            // 7) 名称文字（压在横幅上）
            widgets.NameText = UiFactory.CreateLabel(
                banner != null ? banner.transform : root, "Name", string.Empty, 20, ink,
                new Vector2(158f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));
            Place(widgets.NameText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));

            return widgets;
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

        /// <summary>把两面都填上内容，并按卡牌当前的面摆好朝向。</summary>
        public void BindCard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            Card = card;
            CardFace front = card.Definition.Front;
            CardFace back = card.Definition.Back;

            m_FrontRaw = front.Description;
            m_BackRaw = back.Description;

            FillFace(m_FrontFace, front.DisplayName, front.Description, front.Cost, front.Type,
                BackgroundFor(front.Type), front);
            FillFace(m_BackFace, back.DisplayName, back.Description, back.Cost, back.Type,
                BackgroundFor(back.Type), back);

            ApplySide(card.Side, false, 0f, null, null);
        }

        /// <summary>
        /// 只显示一面（敌人卡、翻面预览用）。内容填在正面，背面隐藏。
        /// 传入 <paramref name="data"/> 时会解析标记（[[关键词]] / *风味* / {数值}）。
        /// </summary>
        public void ShowFace(string displayName, string description, int cost, CardType? type,
            Color background, CardFace? data = null)
        {
            m_FrontRaw = description ?? string.Empty;
            FillFace(m_FrontFace, displayName, description, cost, type, background, data);
            ApplySide(CardFaceSide.Front, false, 0f, null, null);
        }

        /// <summary>直接按某一面的数据渲染（会解析标记与占位符）。</summary>
        public void ShowFace(CardFace face, Color background, CardType? typeOverride = null)
        {
            ShowFace(face.DisplayName, face.Description, face.Cost,
                typeOverride ?? face.Type, background, face);
        }

        /// <summary>同时填充正反两面（敌人卡这种两面都有独立内容的用这个）。</summary>
        public void ShowPair(string frontName, string frontDesc, int frontCost, CardType? frontType,
            Color frontBg, string backName, string backDesc, int backCost, CardType? backType,
            Color backBg, CardFace? frontData = null, CardFace? backData = null)
        {
            m_FrontRaw = frontDesc ?? string.Empty;
            m_BackRaw = backDesc ?? string.Empty;
            FillFace(m_FrontFace, frontName, frontDesc, frontCost, frontType, frontBg, frontData);
            FillFace(m_BackFace, backName, backDesc, backCost, backType, backBg, backData);
        }

        private static void FillFace(FaceWidgets face, string displayName, string description,
            int cost, CardType? type, Color background, CardFace? data)
        {
            if (face == null)
            {
                return;
            }

            // 有卡面数据时走标记解析：[[关键词]] → 粗体、*文字* → 斜体、{damage} → 实时数值
            string formatted = data.HasValue
                ? CardText.Format(description, data.Value)
                : description ?? string.Empty;

            if (face.NameText != null) face.NameText.text = displayName ?? string.Empty;
            if (face.DescriptionText != null) face.DescriptionText.text = formatted;
            // 底色固定为中性色，不随卡牌类型变化
            if (face.Background != null) face.Background.color = CardBase;



            if (face.CostBadge != null)
            {
                face.CostBadge.gameObject.SetActive(cost >= 0);
            }

            if (face.CostText != null)
            {
                face.CostText.text = cost >= 0 ? cost.ToString() : string.Empty;
            }

            if (face.TypeText != null)
            {
                face.TypeText.gameObject.SetActive(type.HasValue);
                face.TypeText.text = type.HasValue ? TypeName(type.Value) : string.Empty;
            }
        }

        /// <summary>按卡牌实例当前的面刷新（不播动画）。</summary>
        public void RefreshVisual()
        {
            if (Card != null)
            {
                ApplySide(Card.Side, false, 0f, null, null);
            }
        }

        /// <summary>把卡摆到指定的一面；animate 为 true 时用 DOTween 真正翻过去。</summary>
        public void ApplySide(CardFaceSide side, bool animate, float duration, AudioSource audio,
            Action onCompleted)
        {
            m_ShownSide = side;
            float targetY = side == CardFaceSide.Back ? 180f : 0f;

            // 面变了，之前那面的关键词提示就不再成立
            HideKeywordTips();

            m_FlipTween?.Kill();

            if (!animate || duration <= 0f)
            {
                m_Rect.localEulerAngles = new Vector3(0f, targetY, 0f);
                SetFrontVisible(side == CardFaceSide.Front);
                IsFlipping = false;
                onCompleted?.Invoke();
                return;
            }

            if (audio != null && audio.clip != null)
            {
                audio.PlayOneShot(audio.clip);
            }

            IsFlipping = true;
            // 从当前角度出发，避免在动画中途被打断时跳变
            float currentY = NormalizeAngle(m_Rect.localEulerAngles.y);
            if (Mathf.Approximately(currentY, targetY))
            {
                SetFrontVisible(side == CardFaceSide.Front);
                IsFlipping = false;
                onCompleted?.Invoke();
                return;
            }

            // 三段式翻面：抬起 → 翻转（带轻微 Z 轴倾斜，像手里甩） → 落下
            // 用一个 t 驱动旋转、缩放和倾斜，保证三者同步
            float from = currentY;
            float to = targetY;
            float scaleBase = m_BaseScale;
            float tiltSign = to > from ? -1f : 1f;

            m_FlipTween = DOTween.To(() => 0f, t =>
            {
                float y = Mathf.Lerp(from, to, t);
                float lift = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0

                m_Rect.localEulerAngles = new Vector3(0f, y, lift * 7f * tiltSign);
                float s = scaleBase * (1f + 0.06f * lift);
                m_Rect.localScale = new Vector3(s, s, 1f);

                // 转过 90° 就换面：此时两张卡面都近乎侧对镜头，切换不可见
                float angle = NormalizeAngle(y);
                SetFrontVisible(angle < 90f || angle > 270f);
            }, 1f, duration)
                .SetEase(Ease.InOutQuad)
                .OnComplete(() =>
                {
                    m_Rect.localEulerAngles = new Vector3(0f, targetY, 0f);
                    m_Rect.localScale = new Vector3(scaleBase, scaleBase, 1f);
                    SetFrontVisible(side == CardFaceSide.Front);
                    IsFlipping = false;
                    onCompleted?.Invoke();
                });
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle;
        }

        private void SetFrontVisible(bool front)
        {
            if (m_FrontFace != null && m_FrontFace.Root != null)
            {
                m_FrontFace.Root.gameObject.SetActive(front);
            }

            if (m_BackFace != null && m_BackFace.Root != null)
            {
                m_BackFace.Root.gameObject.SetActive(!front);
            }
        }

        /// <summary>
        /// 卡牌底色。卡片不再用颜色表达类型（类型由文字标识），
        /// 一律返回中性底色，保留接口避免调用方改动。
        /// </summary>
        public static Color BackgroundFor(CardType type)
        {
            return CardBase;
        }

        public static string TypeName(CardType type)
        {
            return type == CardType.Attack ? "攻击" :
                type == CardType.Skill ? "技能" : "能力";
        }

        public void SetHome(Vector2 position)
        {
            m_HomePosition = position;
            m_Rect.anchoredPosition = position;
        }

        public void SnapHome()
        {
            m_Rect.anchoredPosition = m_HomePosition;
        }

        /// <summary>抬起 / 放下：位置与轻微放大都用补间，手感比瞬移好。</summary>
        /// <summary>
        /// 选择态高亮：把这张牌抬到空中一点并放大，表示「当前选中的是它」。
        /// 参考杀戮尖塔的选择界面。
        /// </summary>
        public void SetChoiceSelected(bool selected)
        {
            m_Rect.DOKill();
            m_Rect.DOScale(m_BaseScale * (selected ? 1.12f : 1f), 0.12f).SetEase(Ease.OutQuad);

            Vector2 target = selected ? m_HomePosition + new Vector2(0f, 56f) : m_HomePosition;
            m_Rect.DOAnchorPos(target, 0.14f).SetEase(Ease.OutQuad);
        }

        public void SetLifted(bool lifted)
        {
            DOTween.Kill(m_Rect, "lift");
            Vector2 target = m_HomePosition + (lifted ? new Vector2(0f, LiftOffset) : Vector2.zero);
            float scale = m_BaseScale * (lifted ? 1.06f : 1f);

            m_Rect.DOAnchorPos(target, 0.12f).SetEase(Ease.OutQuad).SetId("lift");
            m_Rect.DOScale(scale, 0.12f).SetEase(Ease.OutQuad).SetId("lift");
        }

        /// <summary>发牌：从指定位置飞入并淡入，delay 用于错峰。</summary>
        public void PlayDeal(Vector2 fromPosition, float duration = 0.3f, float delay = 0f)
        {
            CanvasGroup group = EnsureCanvasGroup();

            m_Rect.DOKill();
            m_Rect.anchoredPosition = fromPosition;
            m_Rect.localScale = Vector3.one * (m_BaseScale * 0.55f);
            group.alpha = 0f;

            m_Rect.DOAnchorPos(m_HomePosition, duration).SetDelay(delay).SetEase(Ease.OutCubic);
            m_Rect.DOScale(m_BaseScale, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutBack, 1.2f);
            group.DOFade(1f, duration * 0.7f).SetDelay(delay);
        }

        /// <summary>打出后飞出：朝目标位置缩小并淡出，结束后销毁自身。</summary>
        public void PlayOut(Vector2 targetPosition, Action onCompleted = null,
            float duration = 0.25f)
        {
            CanvasGroup group = EnsureCanvasGroup();

            // 先把这张牌身上所有 tween 杀掉，否则对象销毁后 tween 还会继续访问
            m_Rect.DOKill();
            group.DOKill();

            m_Rect.DOAnchorPos(targetPosition, duration).SetEase(Ease.InCubic);
            m_Rect.DOScale(0.25f, duration).SetEase(Ease.InCubic);
            group.DOFade(0f, duration).SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    onCompleted?.Invoke();
                    Destroy(gameObject);
                });
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        /// <summary>翻到另一面，播真实翻面动画。</summary>
        public void PlayFlip(float duration, AudioSource audio, Action onCompleted = null)
        {
            CardFaceSide target = Card != null
                ? Card.Side
                : (m_ShownSide == CardFaceSide.Front ? CardFaceSide.Back : CardFaceSide.Front);

            ApplySide(target, true, duration, audio, onCompleted);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Card == null || m_IsDragging)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                EnsureInspect();
                if (m_Inspect != null)
                {
                    m_Inspect.Show(Card, (RectTransform)transform, true);
                }
                return;
            }

            if (!Interactable)
            {
                return;
            }

            if (ChoiceMode)
            {
                Clicked?.Invoke(this);
                return;
            }

            if (Card.Face.NeedsTarget)
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Card == null || m_IsDragging)
            {
                return;
            }

            if (m_HoverRoutine != null)
            {
                StopCoroutine(m_HoverRoutine);
            }

            // 悬停放大约 1.25 倍，方便看清文字；同时置顶，避免被相邻的牌遮住
            transform.SetAsLastSibling();
            m_Rect.DOScale(m_BaseScale * HoverScale, 0.14f).SetEase(Ease.OutQuad).SetId("hover");
            ShowKeywordTips();
            m_HoverRoutine = StartCoroutine(HoverRoutine());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHover();
            HideKeywordTips();
            if (!m_IsDragging)
            {
                m_Rect.DOScale(m_BaseScale, 0.12f).SetEase(Ease.OutQuad).SetId("hover");
            }

            if (m_Inspect != null)
            {
                m_Inspect.HideIfTransient();
            }
        }

        private IEnumerator HoverRoutine()
        {
            yield return new WaitForSeconds(InspectHoverSeconds);
            m_HoverRoutine = null;
            if (!m_IsDragging && Card != null)
            {
                EnsureInspect();
                if (m_Inspect != null)
                {
                    m_Inspect.Show(Card, (RectTransform)transform, false);
                }
            }
        }

        /// <summary>
        /// 取一次预览服务。界面切换可能重建过面板，
        /// 所以每次使用前确认一下，避免拿到已销毁的对象。
        /// </summary>
        private void EnsureInspect()
        {
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            if (m_Canvas != null && m_Inspect == null)
            {
                m_Inspect = CardInspectService.Ensure(m_Canvas.transform);
            }
        }



        private void CancelHover()
        {
            if (m_HoverRoutine != null)
            {
                StopCoroutine(m_HoverRoutine);
                m_HoverRoutine = null;
            }
        }

        /// <summary>当前显示那一面的原始文本（带标记，未格式化）。</summary>
        private string ShownRawText => m_ShownSide == CardFaceSide.Back ? m_BackRaw : m_FrontRaw;

        /// <summary>悬停时在卡牌左侧列出这一面提到的关键词（含其描述里引用的其他关键词）。</summary>
        private void ShowKeywordTips()
        {
            if (m_Canvas == null)
            {
                return;
            }

            var seeds = CardText.ExtractKeywords(ShownRawText);
            if (seeds.Count == 0)
            {
                HideKeywordTips();
                return;
            }

            var keywords = KeywordLibrary.Expand(seeds);
            if (keywords.Count == 0)
            {
                HideKeywordTips();
                return;
            }

            m_KeywordTips = KeywordTooltipPanel.Ensure(m_Canvas.transform);
            m_KeywordTips.Show(m_Rect, keywords);
        }

        private void HideKeywordTips()
        {
            if (m_KeywordTips != null)
            {
                m_KeywordTips.Hide();
            }
        }

        private void OnDisable()
        {
            // 卡牌被销毁/隐藏时停掉所有 tween，否则 DOTween 会持续报
            // 「CanvasGroup has been destroyed but you are still trying to access it」
            if (m_Rect != null)
            {
                m_Rect.DOKill();
            }

            var group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.DOKill();
            }

            HideKeywordTips();

            HideKeywordTips();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Card == null || !Interactable || !Card.Face.NeedsTarget)
            {
                return;
            }

            m_IsDragging = true;
            CancelHover();
            SetLifted(true);
            m_Rect.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_IsDragging)
            {
                return;
            }

            DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!m_IsDragging)
            {
                return;
            }

            m_IsDragging = false;
            DragFinished?.Invoke(this, eventData);
        }
    }
}
