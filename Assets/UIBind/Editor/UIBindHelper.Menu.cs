using UnityEditor;
using UnityEngine;
using TurnCard.UIBind;

namespace TurnCard.UIBind.Editor
{
    public static partial class UIBindHelper
    {
        //[MenuItem("GameObject/UIBind/标记绑定", false, 10)]
        //private static void MarkAsBind()
        //{
        //    if (!Selection.activeGameObject)
        //    {
        //        return;
        //    }

        //    var target = Selection.activeGameObject;
        //    UIBindMono quickUIMono = target.GetComponentInParent<UIBindMono>();
        //    if (quickUIMono == null)
        //    {
        //        Debug.LogError("UIBind Error 此 GameObject 向上未找到相关组件，请先添加 UIElementMono");
        //    }
        //    else
        //    {
        //        UIBindMonoEditor.SelectTypeAndAdd(Selection.activeGameObject);
        //    }
        //}
    }
}