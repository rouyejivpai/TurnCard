using System.Collections.Generic;
using System.Text;
using TurnCard.Game.UI;
using TurnCard.UIBind;
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 校验流程界面预制体：能被 Resources 按路径加载、挂了 UIBindMono、绑定表与
    /// 生成的 &lt;类名&gt;.Components.cs 一致、关键节点齐全。
    ///
    /// 重点兜住两种回退：
    /// <list type="bullet">
    /// <item>预制体路径写错（界面直接打不开）；</item>
    /// <item>绑定量和生成脚本的字段量对不上（运行时会空引用，或者字段全是 null）。</item>
    /// </list>
    /// </summary>
    public static class FlowPrefabCheck
    {
        [MenuItem("Tools/TurnCard/校验流程界面预制体", priority = 212)]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var log = new StringBuilder();
            int failed = 0;

            void Check(string label, bool ok, string detail)
            {
                log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label + (string.IsNullOrEmpty(detail) ? string.Empty : "  " + detail));
                if (!ok)
                {
                    failed++;
                }
            }

            CheckScreen<MenuScreen>(MenuScreen.ResourcePath, log, Check,
                new[] { "txtTitle", "txtSubtitle", "goButtons", "btnTemplate" },
                new[] { "txtTitle", "txtSubtitle", "goButtons", "btnTemplate" });

            CheckScreen<CardGridScreen>(CardGridScreen.ResourcePath, log, Check,
                new[] { "txtTitle", "txtSubtitle", "goCards", "goBottom", "btnTemplate", "txtHint" },
                new[] { "txtTitle", "txtSubtitle", "goCards", "goBottom", "btnTemplate", "txtHint" });

            CheckScreen<CardListViewScreen>(CardListViewScreen.ResourcePath, log, Check,
                new[]
                {
                    "btnOverlay", "goPanel", "txtTitle", "txtSubtitle", "btnClose", "scrollList",
                    "goContent",
                },
                new[]
                {
                    "btnOverlay", "goPanel", "txtTitle", "txtSubtitle", "btnClose", "scrollList",
                    "goContent",
                });

            log.AppendLine(failed == 0 ? "== 全部通过 ==" : "== 失败 " + failed + " 项 ==");
            return "[FlowPrefabCheck]\n" + log;
        }

        private static void CheckScreen<T>(string resourcePath, StringBuilder log,
            System.Action<string, bool, string> check,
            string[] expectedBindings, string[] expectedNodes) where T : MonoBehaviour
        {
            check(typeof(T).Name + " 资源 " + resourcePath, Resources.Load<GameObject>(resourcePath) != null,
                string.Empty);

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return;
            }

            var mono = prefab.GetComponent<UIBindMono>();
            check(typeof(T).Name + " 挂有 UIBindMono", mono != null, string.Empty);
            if (mono == null)
            {
                return;
            }

            var names = new List<string>(mono.UIObjectsName);
            check(typeof(T).Name + " 绑定数量", names.Count == expectedBindings.Length,
                "实际 " + names.Count + " / 期望 " + expectedBindings.Length + " [" +
                string.Join(", ", names.ToArray()) + "]");

            for (int i = 0; i < expectedBindings.Length; i++)
            {
                string expected = expectedBindings[i];
                bool match = i < names.Count && names[i] == expected;
                check(typeof(T).Name + " 绑定#" + i + " = " + expected, match,
                    match ? string.Empty : "实际 " + (i < names.Count ? names[i] : "(缺失)"));
            }

            foreach (string node in expectedNodes)
            {
                check(typeof(T).Name + " 节点 " + node, FindDeep(prefab.transform, node) != null,
                    string.Empty);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
