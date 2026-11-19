using TMPro;
using TurnCard.Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 单个关键词的说明框。**这是一个预制体**（Resources/UI/KeywordTip），
    /// 面板按需实例化多个，竖直排在卡牌左侧。
    ///
    /// 高度不手工计算：用 VerticalLayoutGroup + ContentSizeFitter 让 UGUI 自己排版，
    /// 这样关键词名换行、描述长短不一都不会溢出。
    /// </summary>
    public sealed class KeywordTip : MonoBehaviour
    {
        public const float Width = 340f;

        [SerializeField] private RectTransform m_Rect;
        [SerializeField] private Image m_Background;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private TextMeshProUGUI m_DescriptionText;

        private static KeywordTip s_Prefab;

        public static KeywordTip Create(Transform parent)
        {
            if (s_Prefab == null)
            {
                s_Prefab = Resources.Load<KeywordTip>("UI/KeywordTip");
            }

            KeywordTip tip;
            if (s_Prefab != null)
            {
                tip = Instantiate(s_Prefab, parent, false);
            }
            else
            {
                var go = new GameObject("KeywordTip", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                tip = go.AddComponent<KeywordTip>();
                tip.BuildDefault();
            }

            tip.Initialize();
            return tip;
        }

        private void Initialize()
        {
            if (m_Rect == null)
            {
                m_Rect = (RectTransform)transform;
            }

            TMP_FontAsset font = UiFactory.Font;
            if (m_NameText != null && m_NameText.font != font) m_NameText.font = font;
            if (m_DescriptionText != null && m_DescriptionText.font != font)
                m_DescriptionText.font = font;
        }

        public void BuildDefault()
        {
            m_Rect = (RectTransform)transform;
            m_Rect.sizeDelta = new Vector2(Width, 120f);
            m_Rect.anchorMin = new Vector2(0.5f, 0.5f);
            m_Rect.anchorMax = new Vector2(0.5f, 0.5f);
            m_Rect.pivot = new Vector2(1f, 0.5f);

            m_Background = gameObject.AddComponent<Image>();
            m_Background.color = new Color(0.10f, 0.11f, 0.15f, 1f);
            m_Background.raycastTarget = false;

            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            m_NameText = UiFactory.CreateLabel(m_Rect, "Name", string.Empty, 21,
                new Color(1f, 0.85f, 0.45f), new Vector2(Width - 28f, 28f),
                new Vector2(0f, 1f), Vector2.zero, TextAlignmentOptions.Left);
            m_NameText.fontStyle = FontStyles.Bold;
            m_NameText.enableWordWrapping = true;

            m_DescriptionText = UiFactory.CreateLabel(m_Rect, "Description", string.Empty, 18,
                new Color(0.88f, 0.89f, 0.93f), new Vector2(Width - 28f, 60f),
                new Vector2(0f, 1f), Vector2.zero, TextAlignmentOptions.TopLeft);
            m_DescriptionText.enableWordWrapping = true;
            m_DescriptionText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>填入内容并返回本框高度（供面板竖向排列）。</summary>
        public float Apply(string keywordName, string description)
        {
            if (m_NameText != null)
            {
                m_NameText.text = keywordName;
            }

            if (m_DescriptionText != null)
            {
                // 描述里也可能带 [[关键词]] / *风味*，走同一套标记解析
                var dummy = new CardFace(keywordName, 0, CardType.Skill, 0, 0, string.Empty,
                    string.Empty, null, null);
                m_DescriptionText.text = CardText.Format(description, dummy);
            }

            // 宽度固定，高度由 ContentSizeFitter 依据排版结果决定
            m_Rect.sizeDelta = new Vector2(Width, m_Rect.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_Rect);

            return m_Rect.rect.height;
        }
    }
}
