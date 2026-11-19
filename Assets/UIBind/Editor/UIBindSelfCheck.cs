using System.IO;
using System.Text;
using UnityEditor;
using TurnCard.UIBind;
using UnityEngine;

namespace TurnCard.UIBind.Editor
{
    /// <summary>自检：确认安装完整。菜单 UIBind/自检。</summary>
    public static class UIBindSelfCheck
    {
        [MenuItem("UIBind/自检 (Self Check)", false, 100)]
        public static void Run()
        {
            var sb = new StringBuilder();
            var pass = true;

            sb.AppendLine("=== UIBind 自检 ===");
            sb.AppendLine("Unity 版本: " + Application.unityVersion);

            var tempDir = UIBindTemplatePath.Resolve();
            var tempOk = Directory.Exists(tempDir);
            sb.AppendLine("[模板目录] " + (tempOk ? "OK   " : "FAIL ") + tempDir);
            if (tempOk)
            {
                foreach (var name in new[] { "WindowTemplate.txt", "WindowComponentTemplate.txt", "FragmentTemplate.txt", "FragmentComponentTemplate.txt", "ItemTemplate.txt", "ItemComponentTemplate.txt" })
                {
                    var exists = File.Exists(Path.Combine(tempDir, name));
                    if (!exists) { pass = false; }
                    sb.AppendLine("    " + (exists ? "ok   " : "缺失 ") + name);
                }
            }
            else
            {
                pass = false;
            }

            var scriptRoot = "Assets/Scripts/UI/";
            var scriptOk = Directory.Exists(scriptRoot);
            if (!scriptOk)
            {
                try { Directory.CreateDirectory(scriptRoot); scriptOk = Directory.Exists(scriptRoot); } catch { scriptOk = false; }
            }
            sb.AppendLine("[脚本输出] " + (scriptOk ? "OK   " : "FAIL ") + scriptRoot);
            if (!scriptOk) { pass = false; }

            sb.AppendLine("[运行时]  UIBindMono: " + (typeof(UIBindMono) != null ? "OK" : "FAIL"));
            sb.AppendLine("=== 结论: " + (pass ? "通过，可以开始用" : "有缺失项，见上面 FAIL / 缺失") + " ===");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("UIBind 自检", pass ? "全部通过，可以开始用。\n结果见 Console。" : "有缺失项，结果见 Console。", "好");
        }

        [MenuItem("UIBind/打开脚本输出目录", false, 101)]
        public static void OpenScriptRoot()
        {
            var path = Application.dataPath + "/Scripts/UI";
            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }
            EditorUtility.RevealInFinder(path + "/");
        }

        [MenuItem("UIBind/打开模板目录", false, 102)]
        public static void OpenTemplateDir()
        {
            EditorUtility.RevealInFinder(UIBindTemplatePath.Resolve());
        }

        /// <summary>
        /// 供命令行/CI 使用：
        /// Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod TurnCard.UIBind.Editor.UIBindSelfCheck.BatchCheck
        /// 通过时 ExitCode=0，失败时 ExitCode=1。
        /// </summary>
        public static void BatchCheck()
        {
            var ok = true;
            var tempDir = UIBindTemplatePath.Resolve();
            if (!Directory.Exists(tempDir))
            {
                UIBindLog.Error("BatchCheck 失败：模板目录不存在 " + tempDir);
                ok = false;
            }
            else
            {
                foreach (var name in new[] { "WindowTemplate.txt", "WindowComponentTemplate.txt", "FragmentTemplate.txt", "FragmentComponentTemplate.txt", "ItemTemplate.txt", "ItemComponentTemplate.txt" })
                {
                    if (!File.Exists(Path.Combine(tempDir, name)))
                    {
                        UIBindLog.Error("BatchCheck 失败：缺少模板 " + name);
                        ok = false;
                    }
                }
            }

            if (typeof(UIBindMono) == null)
            {
                UIBindLog.Error("BatchCheck 失败：UIBindMono 未加载");
                ok = false;
            }

            UIBindLog.Info("BatchCheck " + (ok ? "通过" : "失败"));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
