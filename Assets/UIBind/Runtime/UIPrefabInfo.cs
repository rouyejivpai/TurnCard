#if UNITY_EDITOR
using TurnCard.UIBind;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TurnCard.UIBind
{
    public class UIPrefabInfo
    {
        #region 项目配置
        /// <summary>
        /// 项目配置
        /// </summary>
        private static class Setting
        {
            public enum UIBasePath
            {
                Window,
                Common
            }

            // Common 和 Window 对应根目录
            public static readonly Dictionary<UIBasePath, string> UIPrefabBasePath = new Dictionary<UIBasePath, string>
            {
                { UIBasePath.Window, "Window" },
                { UIBasePath.Common, "Common" }
            };

            // 预制体根目录：任意 Assets 下的预制体都可用
            public static readonly string PrefabPath = "Assets/Game/Resources/UI/";
            // 生成脚本的根目录：不存在会自动创建
            public static readonly string BaseScriptPath = "Assets/Game/Scripts/UI/";
            // Sprite 目录（当前版本未使用，保留给后续扩展）
            public static readonly string BaseSpritePath = "Assets/";
        }

        /// <summary>
        /// 预制体类型
        /// </summary>
        public enum UIPrefabType
        {
            Window = 0, // 窗口
            Fragment = 1, // Fragment
            Item = 2 // 小组件
        }
        #endregion

        public string ScriptName; // 脚本基础名
        public string PrefabName; // 预制体名
        public string ClipPath; // 脚本所在目录
        public string NormalScriptPath; // 正常脚本位置
        public string ComponentScriptPath; // .Components 脚本位置
        public string ModuleName; // 模块名，用于 namespace
        public string AssetPath; // 资源所在目录
        public bool IsCommon;  // Common 还是普通组件
        public UIPrefabType PrefabType; // UI 预制体类型

        public UnityEngine.Object NormalScriptObject; // 脚本 Object
        public UnityEngine.Object ComponentScriptObject; // .Components 脚本 Object

        /// <summary>
        /// 是否是快速绑定生成的
        /// </summary>
        public bool IsCreateByUIBind = true;
        public int ScriptHashCode = 0;


        public static UIPrefabInfo Make(GameObject from)
        {
            var prefabPath = GetPrefabPath(from);
            if (prefabPath == null)
            {
                UIBindLog.Error("UIBind Error 生成 PrefabInfo 失败，必须对预制体操作！");
                return null;
            }

            string[] pathArray = prefabPath.Split(new string[] { Setting.PrefabPath }, System.StringSplitOptions.None);
            if (pathArray.Length > 1)
            {
                try
                {
                    UIPrefabInfo result = new UIPrefabInfo();
                    var realPath = pathArray[1];

                    pathArray = realPath.Split('/');
                    // 前置路径
                    string realName = pathArray[pathArray.Length - 1];
                    // Common 路径下还是 Window 路径下
                    result.IsCommon = pathArray[0] == Setting.UIPrefabBasePath[Setting.UIBasePath.Common];
                    // Window 还是 Fragment 还是 realName
                    result.PrefabType = realName.IndexOf("Frag") > 0 ? UIPrefabType.Fragment : (realPath.IndexOf("Window") > 0 ? UIPrefabType.Window :
                      ((realName.IndexOf("Item") > 0 || realName.IndexOf("Cell") > 0) ? UIPrefabType.Item : UIPrefabType.Window));
                    result.ClipPath = realPath.Split(new string[] { realName }, System.StringSplitOptions.None)[0];
                    // 模块名字
                    result.ModuleName = pathArray[1].Split('.')[0];
                    // 资源路径路径
                    result.AssetPath = realPath.Split('.')[0];
                    // 重构后的名字
                    string scriptName = realName.Split('.')[0];

                    if (result.PrefabType != UIPrefabType.Item &&
                        (from.name.EndsWith("Cell") || from.name.EndsWith("Item")) &&
                        from.transform.parent != null)
                    {
                        result.PrefabType = UIPrefabType.Item;
                        realPath = "Cell";
                    }

                    switch (result.PrefabType)
                    {
                        case UIPrefabType.Window:
                            scriptName = scriptName.Replace("Window", "");
                            break;
                        case UIPrefabType.Fragment:
                            scriptName = scriptName.Replace("Fragment", "");
                            scriptName = scriptName.Replace("Frag", "");
                            break;
                        case UIPrefabType.Item:
                            scriptName = scriptName.Replace("Item", "");
                            scriptName = scriptName.Replace("Cell", "");
                            break;
                    }


                    // 构建名字
                    result.ScriptName = scriptName + (result.PrefabType == UIPrefabType.Window ? "Window" : (result.PrefabType == UIPrefabType.Item ? (realPath.IndexOf("Cell") > 0 ? "Cell" : "Item") : "Fragment"));
                    result.PrefabName = scriptName + (result.PrefabType == UIPrefabType.Window ? "Window" : (result.PrefabType == UIPrefabType.Item ? (realPath.IndexOf("Cell") > 0 ? "Cell" : "Item") : "Frag"));

                    var normalName = result.ScriptName + ".cs";
                    var compName = result.ScriptName + ".Component";
                    System.IO.Directory.CreateDirectory(Setting.BaseScriptPath);
                    var findPath = System.IO.Directory.GetFiles(Setting.BaseScriptPath, result.ScriptName + "*.cs", System.IO.SearchOption.AllDirectories);
                    bool find = false;
                    // 绑定脚本路径
                    if (findPath.Length >= 2)
                    {
                        foreach (var item in findPath)
                        {
                            var fileName = System.IO.Path.GetFileName(item);

                            if (fileName.StartsWith(compName))
                                result.ComponentScriptPath = item;
                            else if (fileName == normalName)
                                result.NormalScriptPath = item;
                        }

                        if (!string.IsNullOrEmpty(result.NormalScriptPath) && !string.IsNullOrEmpty(result.ComponentScriptPath))
                        {
                            find = true;
                        }
                    }

                    if (!find)
                    {
                        result.NormalScriptPath = Setting.BaseScriptPath + result.ClipPath + normalName;
                        result.ComponentScriptPath = Setting.BaseScriptPath + result.ClipPath + result.ScriptName + ".Components.cs";
                    }

                    result.NormalScriptObject = AssetDatabase.LoadAssetAtPath(result.NormalScriptPath, typeof(UnityEngine.Object));
                    result.ComponentScriptObject = AssetDatabase.LoadAssetAtPath(result.ComponentScriptPath, typeof(UnityEngine.Object));

                    return result;
                }
                catch (System.Exception ex)
                {
                    UIBindLog.Error($"UIBind Error 生成 PrefabInfo 失败，路径或名称出错 {ex}");
                }
            }
            else
            {
                UIBindLog.Error("UIBind Error 生成 PrefabInfo 失败，选择的 Prefab 不在 UI 路径内！");
            }
            return null;
        }

        /// <summary>
        /// 获取预制体的真实路径
        /// </summary>
        /// <param name="from">任意位置的 GameObject</param>
        /// <returns>真实路径</returns>
        public static string GetPrefabPath(GameObject from)
        {
            // Project 中的预制体
            if (PrefabUtility.IsPartOfPrefabAsset(from))
            {
                return AssetDatabase.GetAssetPath(from);
            }

            // Scene 中的预制体
            if (PrefabUtility.IsPartOfPrefabInstance(from) || PrefabUtility.IsAnyPrefabInstanceRoot(from))
            {
                // 判断是否正在编辑，且编辑的是它自己
                var curPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (curPrefabStage != null &&  curPrefabStage.scene.name == from.name)
                {
                    return curPrefabStage.assetPath;
                }

                // 判断自身是否是实例
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(from);
                return AssetDatabase.GetAssetPath(prefabAsset);
            }

            // PrefabMode 中的预制体
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(from);
            if (prefabStage)
            {
                return prefabStage.assetPath;
            }

            return null;
        }

        /// <summary>
        /// 判断预制体是否需要保存
        /// </summary>
        /// <returns></returns>
        public static bool IsDirtyPrefab(GameObject from)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(from);
            if (prefabStage != null)
            {
                return prefabStage.scene.isDirty;
            }
            else
            {
                var propertyModifications = PrefabUtility.GetObjectOverrides(from);
                if (propertyModifications.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取预制体原型
        /// </summary>
        public static GameObject GetPrefabOrigin(GameObject from)
        {
            return PrefabUtility.GetCorrespondingObjectFromOriginalSource(from);
        }

        /// <summary>
        /// 判断某个已绑定对象是否在原型中也绑定了
        /// </summary>
        /// <returns></returns>
        public static bool IsObjectInOriginal(GameObject from, string objectName)
        {
            var origin = GetPrefabOrigin(from);
            if (!origin)
            {
                return false;
            }

            // 如果自身就是基类
            var fromPath = GetPrefabPath(from);
            var originPath = GetPrefabPath(origin);
            if (fromPath == originPath)
            {
                return false;
            }

            var originUIBind = origin.GetComponent<UIBindMono>();
            if (!originUIBind)
            {
                return false;
            }

            return originUIBind.UIObjectsName?.Contains(objectName) ?? false;
        }
    }
}
#endif