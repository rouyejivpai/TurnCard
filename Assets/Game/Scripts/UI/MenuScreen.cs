using System;
using System.Collections.Generic;
using TMPro;
using TurnCard.UIBind;
using UnityEngine;
using UnityEngine.UI;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 流程类的简单界面：标题 + 副标题 + 一列按钮。
    /// 主菜单、地图、营地、结算都复用它。
    ///
    /// 结构与坐标来自预制体 <c>Resources/UI/Flow/MenuScreenWindow</c>（UIBind 绑定），
    /// 按钮由预制体里的 <c>btnTemplate</c> 克隆出来 —— 改样式只改预制体，不需要动代码。
    /// </summary>
    public partial class MenuScreen : UIBindWindow
    {
        /// <summary>预制体路径（Resources 下，无扩展名）。</summary>
        public const string ResourcePath = "UI/Flow/MenuScreenWindow";

        private readonly List<GameObject> m_Buttons = new List<GameObject>();

        protected override void OnShow()
        {
            // 预制体里的字体是生成当时写进去的，这里统一成当前全局字体（像素字 + 回退链）
            UiFactory.EnsureFont(transform);
        }

        public void SetTitle(string text)
        {
            if (txtTitle != null) txtTitle.text = text;
        }

        public void SetSubtitle(string text)
        {
            if (txtSubtitle != null) txtSubtitle.text = text;
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
            if (btnTemplate == null)
            {
                Debug.LogError("[MenuScreen] 预制体缺少 btnTemplate，无法加按钮：" + label);
                return null;
            }

            Button button = Instantiate(btnTemplate, goButtons, false);
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

            m_Buttons.Add(button.gameObject);
            return button;
        }
    }
}
