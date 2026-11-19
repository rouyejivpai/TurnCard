using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 流程类的简单界面：标题 + 副标题 + 一列按钮。
    /// 由 <c>FormHost.OpenRuntime</c> 在代码里搭建，不依赖预制体。
    ///
    /// 布局在 Awake 里建好，外部随后调 SetTitle / AddButton 填内容。
    /// </summary>
    public class MenuScreen : UGuiForm
    {
        private RectTransform m_Content;
        private TextMeshProUGUI m_Title;
        private TextMeshProUGUI m_Subtitle;
        private readonly List<GameObject> m_Buttons = new List<GameObject>();

        private void Awake()
        {
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(1920f, 1080f);

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.075f, 0.09f);
            background.raycastTarget = true;

            m_Title = UiFactory.CreateLabel(root, "Title", string.Empty, 64, Color.white,
                new Vector2(1200f, 90f), new Vector2(0.5f, 1f), new Vector2(0f, -110f));

            m_Subtitle = UiFactory.CreateLabel(root, "Subtitle", string.Empty, 26,
                new Color(0.75f, 0.78f, 0.86f), new Vector2(1400f, 40f),
                new Vector2(0.5f, 1f), new Vector2(0f, -190f));

            m_Content = UiFactory.CreateRect(root, "Buttons");
            m_Content.anchorMin = new Vector2(0.5f, 0.5f);
            m_Content.anchorMax = new Vector2(0.5f, 0.5f);
            m_Content.sizeDelta = new Vector2(720f, 600f);
            m_Content.anchoredPosition = new Vector2(0f, -80f);

            var layout = m_Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        public void SetTitle(string text)
        {
            if (m_Title != null) m_Title.text = text;
        }

        public void SetSubtitle(string text)
        {
            if (m_Subtitle != null) m_Subtitle.text = text;
        }

        public void ClearButtons()
        {
            foreach (GameObject button in m_Buttons)
            {
                if (button != null)
                {
                    Destroy(button);
                }
            }

            m_Buttons.Clear();
        }

        /// <summary>加一个按钮；interactable 为 false 时显示为不可点。</summary>
        public Button AddButton(string label, Action onClick, bool interactable = true)
        {
            RectTransform rect = UiFactory.CreateBox(m_Content, "Btn_" + label,
                new Color(0.16f, 0.20f, 0.28f), new Vector2(700f, 72f),
                new Vector2(0.5f, 0.5f), Vector2.zero);

            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 72f;
            layoutElement.preferredWidth = 700f;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.interactable = interactable;

            TextMeshProUGUI caption = UiFactory.CreateLabel(rect, "Caption", label, 30,
                Color.white, new Vector2(680f, 64f), new Vector2(0.5f, 0.5f), Vector2.zero);
            caption.raycastTarget = false;

            if (onClick != null && interactable)
            {
                button.onClick.AddListener(() => onClick());
            }

            if (!interactable)
            {
                caption.color = new Color(0.45f, 0.47f, 0.52f);
                rect.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f);
            }

            m_Buttons.Add(rect.gameObject);
            return button;
        }
    }
}
