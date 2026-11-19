using System;
using UnityEditor;
using TurnCard.UIBind;
using UnityEngine;

namespace TurnCard.UIBind.Editor
{
    /// <summary>
    /// 模板目录解析：不写死路径，递归搜 Assets 下的 WindowTemplate.txt，
    /// 所以 Assets/UIBind 整个目录可以放在任意位置。
    /// </summary>
    public static class UIBindTemplatePath
    {
        private const string Fallback = "Assets/UIBind/Editor/ScriptTemplate/";

        public static string Resolve()
        {
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("WindowTemplate t:TextAsset"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("/WindowTemplate.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        return path.Substring(0, path.LastIndexOf('/') + 1);
                    }
                }
            }
            catch (Exception e)
            {
                UIBindLog.Warning("模板目录自动探测失败，回退默认路径：" + e.Message);
            }

            return Fallback;
        }
    }
}
