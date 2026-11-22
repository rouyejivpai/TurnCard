using UnityEngine;
using UnityEngine.UI;
using TurnCard.UIBind;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 生成的窗口脚本。InitComponents 里的绑定由 UIBind 自动维护，不要手改标记区域。
    /// 结构与 UIBind 的 WindowComponentTemplate 一致；基类在 CardGridScreen.cs 里声明。
    /// </summary>
    public partial class CardGridScreen
    {
        #region (禁止编辑)自动生成变量
        // HashCode:180
        protected TMPro.TextMeshProUGUI txtTitle;
        protected TMPro.TextMeshProUGUI txtSubtitle;
        protected UnityEngine.RectTransform goCards;
        protected UnityEngine.RectTransform goBottom;
        protected UnityEngine.UI.Button btnTemplate;
        protected TMPro.TextMeshProUGUI txtHint;
        #endregion (禁止编辑)自动生成变量

        protected override void OnCreate()
        {
            InitComponents();
        }

        private void InitComponents()
        {
            UIBindMono quickUI = RectTransform.GetComponent<UIBindMono>();

            #region (禁止编辑)自动生成绑定
            txtTitle = quickUI.UIObjectsArray[0] as TMPro.TextMeshProUGUI;
            txtSubtitle = quickUI.UIObjectsArray[1] as TMPro.TextMeshProUGUI;
            goCards = quickUI.UIObjectsArray[2] as UnityEngine.RectTransform;
            goBottom = quickUI.UIObjectsArray[3] as UnityEngine.RectTransform;
            btnTemplate = quickUI.UIObjectsArray[4] as UnityEngine.UI.Button;
            txtHint = quickUI.UIObjectsArray[5] as TMPro.TextMeshProUGUI;
            #endregion (禁止编辑)自动生成绑定
        }
    }
}
