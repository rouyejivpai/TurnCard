using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TurnCard.UIBind
{
    /// <summary>
    /// UIBindMono UI 快速绑定。获取 UI 组件的入口。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public partial class UIBindMono : MonoBehaviour
    {
        /// <summary>
        /// 所有已绑定物件的数组
        /// </summary>
        [SerializeField]
        public List<Object> UIObjectsArray = new List<Object>();

        /// <summary>
        /// 所有已绑定物件的变量命名数组
        /// </summary>
        [SerializeField]
        public List<string> UIObjectsName = new List<string>();

        /// <summary>
        /// 通过变量名获取在绑定列表中的索引，未找到返回 -1
        /// </summary>
        public int GetIndexByName(string varName)
        {
            return UIObjectsName.IndexOf(varName);
        }

        /// <summary>
        /// 通过变量名获取对应绑定的 Object，未找到返回 null
        /// </summary>
        public UnityEngine.Object GetObjectByName(string varName)
        {
            int index = GetIndexByName(varName);
            return index >= 0 ? UIObjectsArray[index] : null;
        }

    }
}