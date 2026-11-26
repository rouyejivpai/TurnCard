using UnityEngine;
using UnityEngine.UI;
using TurnCard.UIBind;

namespace Window.BattleForm
{
    /// <summary>
    /// 生成的窗口脚本。InitComponents 里的绑定由 UIBind 自动维护，不要手改标记区域。
    /// </summary>
    public partial class BattleForm : UIBindWindow
    {
        #region (禁止编辑)自动生成变量
        // HashCode:454
        protected TMPro.TextMeshProUGUI txtStatus;
        protected TMPro.TextMeshProUGUI txtDrawCount;
        protected TMPro.TextMeshProUGUI txtDiscardCount;
        protected TMPro.TextMeshProUGUI txtHint;
        protected TMPro.TextMeshProUGUI txtResult;
        protected UnityEngine.UI.Button btnEndTurn;
        protected UnityEngine.UI.Button btnDrawPile;
        protected UnityEngine.UI.Button btnDiscardPile;
        protected UnityEngine.GameObject goHand;
        protected UnityEngine.UI.Button btnExhaustPile;
        protected TMPro.TextMeshProUGUI txtExhaustCount;
        #endregion (禁止编辑)自动生成变量

        private void InitComponents()
        {
            UIBindMono quickUI = RectTransform.GetComponent<UIBindMono>();
            // 在这里初始化组件

            #region (禁止编辑)自动生成绑定
            txtStatus = quickUI.UIObjectsArray[0] as TMPro.TextMeshProUGUI;
            txtDrawCount = quickUI.UIObjectsArray[1] as TMPro.TextMeshProUGUI;
            txtDiscardCount = quickUI.UIObjectsArray[2] as TMPro.TextMeshProUGUI;
            txtHint = quickUI.UIObjectsArray[3] as TMPro.TextMeshProUGUI;
            txtResult = quickUI.UIObjectsArray[4] as TMPro.TextMeshProUGUI;
            btnEndTurn = quickUI.UIObjectsArray[5] as UnityEngine.UI.Button;
            btnDrawPile = quickUI.UIObjectsArray[6] as UnityEngine.UI.Button;
            btnDiscardPile = quickUI.UIObjectsArray[7] as UnityEngine.UI.Button;
            goHand = quickUI.UIObjectsArray[8] as UnityEngine.GameObject;
            btnExhaustPile = quickUI.UIObjectsArray[9] as UnityEngine.UI.Button;
            txtExhaustCount = quickUI.UIObjectsArray[10] as TMPro.TextMeshProUGUI;
            #endregion (禁止编辑)自动生成绑定
        }
    }
}
