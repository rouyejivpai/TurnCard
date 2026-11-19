using System.Text;
using UnityEngine;
using Window.BattleForm;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 校验战斗界面预制体仍然能被 Resources 按路径加载，且关键节点齐全。
    /// UIBind 保存时会按规范把预制体改名为 &lt;类名&gt;Window，这个检查用来兜住路径写错的情况。
    /// </summary>
    public static class BattlePrefabCheck
    {
        public static void Run()
        {
            var log = new StringBuilder();
            int failed = 0;

            void Check(string label, bool ok, string detail)
            {
                log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label + "  " + detail);
                if (!ok)
                {
                    failed++;
                }
            }

            string path = "UI/Battle/" + nameof(BattleForm) + "Window";
            var prefab = Resources.Load<GameObject>(path);
            Check("按规范路径加载 " + path, prefab != null, prefab == null ? "找不到" : "ok");

            if (prefab != null)
            {
                // 界面逻辑允许运行时补挂（FormHost.Open 会 AddComponent），
                // 真正必须存在的是 UIBindMono —— 绑定表靠它在运行时取。
                Check("挂有 UIBindMono", prefab.GetComponent<TurnCard.UIBind.UIBindMono>() != null, "");

                string[] required = { "txtStatus", "txtHint", "btnEndTurn", "btnDrawPile", "btnDiscardPile", "Hand" };
                foreach (string name in required)
                {
                    Transform node = FindDeep(prefab.transform, name);
                    Check("节点 " + name, node != null, node == null ? "缺失" : "");
                }
            }

            // 旧路径应该已经不存在，避免以后又悄悄回退
            Check("旧路径 UI/Battle/BattleForm 已失效",
                Resources.Load<GameObject>("UI/Battle/BattleForm") == null, "");

            log.AppendLine(failed == 0 ? "== 全部通过 ==" : "== 失败 " + failed + " 项 ==");
            Debug.Log("[BattlePrefabCheck]\n" + log);
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
