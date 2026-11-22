using UnityEngine;
using UnityEngine.UI;
using TurnCard.UIBind;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 生成的窗口脚本。InitComponents 里的绑定由 UIBind 自动维护，不要手改标记区域。
    /// 结构与 UIBind 的 WindowComponentTemplate 一致；基类在 CardListViewScreen.cs 里声明。
    /// </summary>
    public partial class CardListViewScreen
    {
        #region (禁止编辑)自动生成变量
        // HashCode:255
        protected UnityEngine.UI.Button btnOverlay;
        protected UnityEngine.RectTransform goPanel;
        protected TMPro.TextMeshProUGUI txtTitle;
        protected TMPro.TextMeshProUGUI txtSubtitle;
        protected UnityEngine.UI.Button btnClose;
        protected UnityEngine.UI.ScrollRect scrollList;
        protected UnityEngine.RectTransform goContent;
        #endregion (禁止编辑)自动生成变量

        protected override void OnCreate()
        {
            InitComponents();
        }

        private void InitComponents()
        {
            UIBindMono quickUI = RectTransform.GetComponent<UIBindMono>();

            #region (禁止编辑)自动生成绑定
            btnOverlay = quickUI.UIObjectsArray[0] as UnityEngine.UI.Button;
            goPanel = quickUI.UIObjectsArray[1] as UnityEngine.RectTransform;
            txtTitle = quickUI.UIObjectsArray[2] as TMPro.TextMeshProUGUI;
            txtSubtitle = quickUI.UIObjectsArray[3] as TMPro.TextMeshProUGUI;
            btnClose = quickUI.UIObjectsArray[4] as UnityEngine.UI.Button;
            scrollList = quickUI.UIObjectsArray[5] as UnityEngine.UI.ScrollRect;
            goContent = quickUI.UIObjectsArray[6] as UnityEngine.RectTransform;
            #endregion (禁止编辑)自动生成绑定
        }
    }
}
