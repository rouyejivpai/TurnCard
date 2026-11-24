using System;
using System.Collections;
using System.Collections.Generic;
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
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ICardFlipView
    {
        // ---- 卡牌尺寸：只改 CardScale，其它全部派生 ----
        // 美术原生像素（cardframeWood.png 的实际尺寸，不要改）
        public const float CardArtWidth = 160f;
        public const float CardArtHeight = 196f;

        /// <summary>
        /// 卡牌显示缩放（唯一开关）。
        /// 像素风建议用整数倍（1 / 2）保持锐利；1.5 也能用，但像素会略微不均匀。
        /// </summary>
        public const float CardScale = 1.5f;

        // 派生尺寸：布局、预制体、字号都由它推导
        public const float CardWidth = CardArtWidth * CardScale;
        public const float CardHeight = CardArtHeight * CardScale;

        /// <summary>把「美术像素」换算成「显示像素」。</summary>
        public static float ArtPixels(float artPixels) => artPixels * CardScale;
        public const float LiftOffset = 46f;
        public const float InspectHoverSeconds = 0.8f;

        private const string PrefabResourcePath = "UI/CardView";

        public static readonly Color AttackColor = new Color(0.55f, 0.20f, 0.22f);
        public static readonly Color SkillColor = new Color(0.16f, 0.34f, 0.52f);

        /// <summary>卡牌底色：中性色，不随类型变化。</summary>
        public static readonly Color CardBase = new Color(0.16f, 0.16f, 0.19f);
        public static readonly Color PowerColor = new Color(0.36f, 0.26f, 0.52f);
        internal static readonly Color ArtPlaceholderColor = new Color(0f, 0f, 0f, 0.22f);

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

            /// <summary>是否要把 Background 染成中性底色（整图模式不染，否则会把卡框图涂黑）。</summary>
            public bool TintBackground;
        }

        [Header("正面")]
        [SerializeField] private FaceWidgets m_FrontFace;

        [Header("背面（构建时已旋转 180°）")]
        [SerializeField] private FaceWidgets m_BackFace;

        [Header("卡面视图（正反共用 CardFace.prefab）")]
        [SerializeField] private CardFaceView m_FrontView;
        [SerializeField] private CardFaceView m_BackView;

        private static CardView s_Prefab;

        private RectTransform m_Rect;
        private Canvas m_Canvas;
        private Vector2 m_HomePosition;
        private bool m_IsDragging;
        private float m_BaseScale = 1f;
        private Tween m_FlipTween;

        private struct QueuedFlip
        {
            public CardFaceSide Side;
            public float Duration;
            public System.Action Done;
        }

        private readonly System.Collections.Generic.Queue<QueuedFlip> m_FlipQueue =
            new System.Collections.Generic.Queue<QueuedFlip>();
        private Coroutine m_HoverRoutine;

        /// <summary>显示用的费用覆盖（贪婪免费额度）；null = 用卡面原始费用。</summary>
        private int? m_CostOverride;

        // 数值预览：{damage}/{armor} 显示成经过 buff 结算后的真实值
        private int? m_PreviewDamage;
        private int? m_PreviewArmor;
        private bool m_PreviewMark;
        private System.Collections.Generic.IReadOnlyDictionary<string, int> m_PreviewTokens;
        private string m_PreviewTokenSignature;

        private static Material s_GlowMaterial;

        /// <summary>描边材质资产（Resources 下，保证 shader 不被打包剔除）。</summary>
        private const string GlowMaterialResource = "UI/Card/CardOutline";

        private CardInspectService m_Inspect;
        private KeywordTooltipPanel m_KeywordTips;
        private CardFaceSide m_ShownSide = CardFaceSide.Front;
        private string m_FrontRaw = string.Empty;
        private string m_BackRaw = string.Empty;

        public CardInstance Card { get; private set; }

        /// <summary>是否允许这张卡请求「背面预览」。预览面板自己那张卡必须关掉，否则无限套娃。</summary>
        /// <summary>悬停时的放大倍率。</summary>
        public const float HoverScale = 1.25f;

        /// <summary>影子材质（只是 shader 载体，偏移/浓淡由 HandLayoutSettings 控制）。</summary>
        private const string ShadowResourcePath = "UI/Card/CardShadow";

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

            // 正反共用同一个卡面预制体：部件引用统一从视图取
            if (m_FrontView != null)
            {
                m_FrontFace = m_FrontView.ToWidgets();
            }

            if (m_BackView != null)
            {
                m_BackFace = m_BackView.ToWidgets();
            }

            EnsureFont();
            ApplySkin(m_FrontFace);
            ApplySkin(m_BackFace);
        }



        /// <summary>
        /// 补齐缺失的部件精灵。
        ///
        /// 现在卡牌模板就在 CardFace.prefab 里配（图、颜色、尺寸都序列化了），
        /// 所以这里只处理「预制体没配到图」的情况，作为兜底，不再覆盖预制体的设置。
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
                if (image != null && image.sprite == null)
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
            if (image != null && image.sprite == null)
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


            m_FrontFace = BuildFace("FrontFace", false);
            m_BackFace = BuildFace("BackFace", true);

            EnsureFont();
            ApplySkin(m_FrontFace);
            ApplySkin(m_BackFace);
            ApplySide(CardFaceSide.Front, false, 0f, null, null);
        }

        /// <summary>
        /// 「当前可打出」的描边。直接画在 Background 这张图上——
        /// 卡框贴图自带透明边距（cardframeWood 左 8 / 上 4 / 右 10 / 下 10 贴图像素），
        /// 描边就落在边距里，不需要额外垫一张大一圈的图。
        /// </summary>
        private void EnsureGlowMaterial()
        {
            if (s_GlowMaterial != null)
            {
                return;
            }

            // 用 Resources 里的材质资产，保证打包时 shader 不会因为没人引用被剔掉
            s_GlowMaterial = Resources.Load<Material>(GlowMaterialResource);
            if (s_GlowMaterial == null)
            {
                Shader shader = Shader.Find("TurnCard/UICardOutline");
                if (shader == null)
                {
                    return;
                }

                s_GlowMaterial = new Material(shader);
            }

            // 这些参数所有卡牌共用，改共享材质即可
            CardVisualSettings cfg = CardVisualSettings.Current;
            s_GlowMaterial.SetColor("_Color", cfg.OutlineColor);
            s_GlowMaterial.SetColor("_PaleColor", cfg.OutlinePaleColor);
            s_GlowMaterial.SetFloat("_OuterAlpha", cfg.OutlineOuterAlpha);
            s_GlowMaterial.SetFloat("_Width", cfg.OutlineWidth / CardWidth);
            s_GlowMaterial.SetFloat("_Intensity", cfg.OutlineIntensity);
            s_GlowMaterial.SetFloat("_Softness", cfg.Softness);
            s_GlowMaterial.SetColor("_WaveColor", cfg.WaveColor);
            s_GlowMaterial.SetFloat("_WaveStrength", cfg.WaveStrength);
            s_GlowMaterial.SetFloat("_WaveSpeed", cfg.WaveSpeed);
            s_GlowMaterial.SetFloat("_WaveBand", cfg.WaveBand);
        }

        /// <summary>当前是否「可以打出」：只有可打出的牌，Background 才换成描边材质。</summary>
        public void SetPlayable(bool playable)
        {
            EnsureGlowMaterial();
            if (s_GlowMaterial == null)
            {
                return;
            }

            ApplyGlowMaterial(m_FrontFace, playable);
            ApplyGlowMaterial(m_BackFace, playable);
        }

        private static void ApplyGlowMaterial(FaceWidgets face, bool playable)
        {
            if (face == null || face.Background == null)
            {
                return;
            }

            Material material = playable ? s_GlowMaterial : null;
            if (face.Background.material == material)
            {
                return;
            }

            face.Background.material = material;
        }

        private FaceWidgets BuildFace(string name, bool isBack)
        {
            RectTransform root = UiFactory.CreateRect(transform, name);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);

            var view = root.gameObject.AddComponent<CardFaceView>();

            if (isBack)
            {
                // 背面预先翻转 180°，整张卡转到 180° 时正好正对镜头
                root.localEulerAngles = new Vector3(0f, 180f, 0f);
                m_BackView = view;
            }
            else
            {
                m_FrontView = view;
            }

            // 结构只由 CardFaceView 定义一次，正反两面不会再各写一份
            view.BuildDefault();
            return view.ToWidgets();
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

        /// <summary>
        /// 某一面的预览选项：战斗中有 BattleState 就向它要（按这一面算 token），
        /// 否则用本视图自己的预览值。
        /// </summary>
        private CardText.FormatOptions OptionsForFace(CardFace face)
        {
            if (CardPreviewSource.State != null)
            {
                return CardPreviewSource.BuildOptions(face);
            }

            return PreviewOptions();
        }

        /// <summary>当前预览选项（没设置时就是卡面原值）。</summary>
        private CardText.FormatOptions PreviewOptions()
        {
            return new CardText.FormatOptions
            {
                Damage = m_PreviewDamage,
                Armor = m_PreviewArmor,
                MarkModified = m_PreviewMark,
                Tokens = m_PreviewTokens,
            };
        }

        /// <summary>
        /// 设置数值预览（经 buff 结算后的真实伤害/护甲，以及效果提供的额外 token）。
        /// 传 null 表示该数值没有预览，用卡面原值。
        /// </summary>
        public void SetValuePreview(int? damage, int? armor, bool markModified,
            System.Collections.Generic.IReadOnlyDictionary<string, int> tokens = null)
        {
            string signature = TokenSignature(tokens);
            if (m_PreviewDamage == damage && m_PreviewArmor == armor
                && m_PreviewMark == markModified && m_PreviewTokenSignature == signature)
            {
                return;
            }

            m_PreviewDamage = damage;
            m_PreviewArmor = armor;
            m_PreviewMark = markModified;
            m_PreviewTokens = tokens;
            m_PreviewTokenSignature = signature;
            RefreshDescription();
        }

        private static string TokenSignature(System.Collections.Generic.IReadOnlyDictionary<string, int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            foreach (System.Collections.Generic.KeyValuePair<string, int> pair in tokens)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
            }

            return builder.ToString();
        }

        /// <summary>只重刷当前显示那一面的描述文本（预览变化时用，不重排整张卡）。</summary>
        private void RefreshDescription()
        {
            if (Card == null)
            {
                return;
            }

            FaceWidgets face = m_ShownSide == CardFaceSide.Back ? m_BackFace : m_FrontFace;
            if (face == null || face.DescriptionText == null)
            {
                return;
            }

            // 用「正在显示的那一面」来解析，避免显示面与当前面不一致时数字串味
            CardFace shownFace = Card != null ? Card.Definition.GetFace(m_ShownSide) : Card.Face;
            CardText.FormatOptions shownOptions = m_ShownSide == Card.Side
                ? PreviewOptions()
                : CardPreviewSource.BuildOptions(shownFace);
            face.DescriptionText.text = CardText.Format(ShownRawText, shownFace, shownOptions);
        }

        private void FillFace(FaceWidgets face, string displayName, string description,
            int cost, CardType? type, Color background, CardFace? data)
        {
            if (face == null)
            {
                return;
            }

            // 有卡面数据时走标记解析：[[关键词]] → 粗体、*文字* → 斜体、{damage} → 实时数值
            // 预览按「这一面」算：隐藏面也能拿到自己的 token（{total_armor} 之类）
            string formatted = data.HasValue
                ? CardText.Format(description, data.Value, OptionsForFace(data.Value))
                : description ?? string.Empty;

            if (face.NameText != null) face.NameText.text = displayName ?? string.Empty;
            if (face.DescriptionText != null) face.DescriptionText.text = formatted;
            // 底色固定为中性色，不随卡牌类型变化；整图模式下背景是卡框图，不能染
            if (face.Background != null && face.TintBackground)
            {
                face.Background.color = CardBase;
            }



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

            // 翻面/重刷后把「免费」的费用覆盖重新贴回当前面
            if (m_CostOverride.HasValue)
            {
                SetCostOverride(m_CostOverride);
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

            m_ShownSide = front ? CardFaceSide.Front : CardFaceSide.Back;
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

        /// <summary>只更新目标位置，不动当前显示位置（供布局过渡用）。</summary>
        public void SetHomeSilent(Vector2 position)
        {
            m_HomePosition = position;
        }

        /// <summary>
        /// 平滑移动到新的 home 位置。用独立的 <c>layout</c> id，
        /// 不会和悬停、抬起、翻面那些补间互相打断。
        /// </summary>
        public void TweenHome(float duration = 0.18f, float delay = 0f)
        {
            if (m_Rect == null)
            {
                return;
            }

            DOTween.Kill(m_Rect, "layout");
            m_Rect.DOAnchorPos(m_HomePosition, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutCubic)
                .SetId("layout");
        }

        /// <summary>当前是否正在移动到位（布局过渡期间为 true）。</summary>
        public bool IsMovingHome { get; private set; }

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

            PlayFlipTo(target, duration, audio, onCompleted);
        }

        /// <summary>
        /// 翻到指定的一面。
        ///
        /// 同一张牌在一次结算里可能被连续翻多次（例如「使用相邻牌」再翻面），
        /// 这里用队列串起来，保证每一次翻面都有独立的动画，而不是被合并成一次。
        /// </summary>
        public void PlayFlipTo(CardFaceSide side, float duration, AudioSource audio = null,
            Action onCompleted = null)
        {
            m_FlipQueue.Enqueue(new QueuedFlip { Side = side, Duration = duration, Done = onCompleted });

            if (!IsFlipping)
            {
                StartNextQueuedFlip(audio);
            }
        }

        private void StartNextQueuedFlip(AudioSource audio)
        {
            if (m_FlipQueue.Count == 0)
            {
                return;
            }

            QueuedFlip step = m_FlipQueue.Dequeue();
            float targetY = step.Side == CardFaceSide.Back ? 180f : 0f;

            // 视图已经停在目标面（连翻两次会回到原点）：先把朝向摆到对侧再翻，
            // 否则 ApplySide 会因为「角度相同」直接结束，看不见任何动画。
            if (Mathf.Approximately(NormalizeAngle(m_Rect.localEulerAngles.y), targetY))
            {
                float oppositeY = step.Side == CardFaceSide.Back ? 0f : 180f;
                m_Rect.localEulerAngles = new Vector3(0f, oppositeY, 0f);
                SetFrontVisible(step.Side != CardFaceSide.Back);
                m_ShownSide = step.Side == CardFaceSide.Back ? CardFaceSide.Front : CardFaceSide.Back;
            }

            ApplySide(step.Side, true, step.Duration, audio, () =>
            {
                step.Done?.Invoke();
                StartNextQueuedFlip(audio);
            });
        }

        /// <summary>清空未播完的翻面队列（战斗结束或界面关闭时用）。</summary>
        public void ClearFlipQueue()
        {
            m_FlipQueue.Clear();
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
            m_Rect.DOScale(m_BaseScale * Layout.HoverScale, 0.14f).SetEase(Ease.OutQuad).SetId("hover");
            ShowShadow(true);
            StartFloat();
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

            StopFloat();
            ShowShadow(false);
        }

        // ---------------------------------------------------------------- 悬停：投影 + 浮动

        /// <summary>手牌表现参数（悬停放大 / 抬起 / 浮动 / 投影）都在这里，见 Resources/Config/HandLayoutSettings。</summary>
        private static HandLayoutSettings Layout => HandLayoutSettings.Current;

        private Image m_Shadow;

        /// <summary>浮动动画的句柄；Sequence 必须持引用才能可靠 Kill。</summary>
        private Tween m_FloatTween;

        /// <summary>
        /// 影子画在卡牌**下面一层**（第一个子节点），用卡框贴图的 alpha 当剪影，
        /// 所以换卡框贴图时影子会自动跟着变，不需要额外的影子素材。
        /// </summary>
        private void EnsureShadow()
        {
            if (m_Shadow != null || m_Rect == null)
            {
                return;
            }

            var go = new GameObject("imgShadow", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(m_Rect, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            Vector2 offset = Layout.ShadowOffset;
            rect.offsetMin = offset;
            rect.offsetMax = offset;
            rect.localScale = Vector3.one;

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            Material material = Resources.Load<Material>(ShadowResourcePath);
            if (material != null)
            {
                image.material = material;
            }

            Sprite sprite = m_FrontFace != null && m_FrontFace.Background != null
                ? m_FrontFace.Background.sprite
                : null;
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0f);

            m_Shadow = image;
        }

        private void ShowShadow(bool show)
        {
            EnsureShadow();
            if (m_Shadow == null)
            {
                return;
            }

            m_Shadow.DOKill();
            if (show)
            {
                m_Shadow.gameObject.SetActive(true);
                m_Shadow.DOFade(Layout.ShadowOpacity, Layout.ShadowFade).SetEase(Ease.OutQuad).SetId("shadow");
            }
            else
            {
                m_Shadow.DOFade(0f, Layout.ShadowFade).SetEase(Ease.InQuad).SetId("shadow")
                    .OnComplete(() =>
                    {
                        if (m_Shadow != null)
                        {
                            m_Shadow.gameObject.SetActive(false);
                        }
                    });
            }
        }

        /// <summary>
        /// 悬停浮动：先抬到「悬浮高度」，再按一个**随机方向**做小幅度往返摆动。
        /// 方向随机但幅度有上限（2.5~4.5px），所以不会看起来跑偏。
        /// </summary>
        private void StartFloat()
        {
            if (m_Rect == null)
            {
                return;
            }

            // 已经在浮动了就别起重开，否则 Sequence 会叠在一起
            if (m_FloatTween != null && m_FloatTween.IsActive())
            {
                return;
            }

            KillFloat();

            HandLayoutSettings cfg = Layout;
            float angle = UnityEngine.Random.Range(-cfg.FloatAngleRange, cfg.FloatAngleRange) * Mathf.Deg2Rad;
            float amp = UnityEngine.Random.Range(cfg.FloatAmplitudeMin, cfg.FloatAmplitudeMax);
            var wobble = new Vector2(Mathf.Sin(angle) * amp, Mathf.Cos(angle) * amp * 0.7f);
            var baseTarget = m_HomePosition + new Vector2(0f, cfg.LiftHeight);

            Sequence seq = DOTween.Sequence().SetId("float");
            seq.Append(m_Rect.DOAnchorPos(baseTarget, 0.14f).SetEase(Ease.OutQuad));
            seq.Append(m_Rect.DOAnchorPos(baseTarget + wobble, cfg.FloatPeriod)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

            m_FloatTween = seq;
        }

        /// <summary>只杀浮动。Sequence 必须持有引用再 Kill —— 按 id 杀不掉循环里的子 tween。</summary>
        private void KillFloat()
        {
            if (m_FloatTween != null)
            {
                m_FloatTween.Kill();
                m_FloatTween = null;
            }
        }

        private void StopFloat()
        {
            if (m_Rect == null)
            {
                return;
            }

            KillFloat();

            if (!m_IsDragging)
            {
                DOTween.Kill(m_Rect, "layout");
                m_Rect.DOAnchorPos(m_HomePosition, Layout.ReturnDuration).SetEase(Ease.OutQuad).SetId("layout");
            }
        }

        /// <summary>
        /// 覆盖手牌上显示的费用（贪婪的「本回合第一张免费」用它把数字显示成 0）。
        /// 传 null 恢复成卡面原始费用。
        /// </summary>
        public void SetCostOverride(int? cost)
        {
            m_CostOverride = cost;

            FaceWidgets face = m_ShownSide == CardFaceSide.Back ? m_BackFace : m_FrontFace;
            if (face == null || face.CostText == null || Card == null)
            {
                return;
            }

            // 直接写这一面的费用文字：不能靠 RefreshVisual，因为「同一面」时它不会重刷
            int shown = cost ?? Card.Face.Cost;
            face.CostText.text = shown >= 0 ? shown.ToString() : string.Empty;
            face.CostText.color = cost.HasValue ? new Color(1f, 0.86f, 0.35f) : Color.white;
        }

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
