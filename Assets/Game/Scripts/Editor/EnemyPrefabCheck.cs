using System.Text;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 校验敌人卡预制体：能被 Resources 加载、视图组件在、10 个绑定都不为空、尺寸符合标准。
    ///
    /// 重点兜住「生成器跑完但绑定漏了」这种情况 —— 运行时那会退化成一张没有立绘/
    /// 没有血条的空卡，而且不报错。
    /// </summary>
    public static class EnemyPrefabCheck
    {
        private const string ResourcePath = "UI/Enemy/EnemyCard";

        [MenuItem("Tools/TurnCard/校验敌人卡预制体", priority = 214)]
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
                log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label +
                               (string.IsNullOrEmpty(detail) ? string.Empty : "  " + detail));
                if (!ok)
                {
                    failed++;
                }
            }

            var prefab = Resources.Load<EnemyCardView>(ResourcePath);
            Check("资源 " + ResourcePath, prefab != null, string.Empty);
            if (prefab == null)
            {
                return log.ToString();
            }

            var rect = (RectTransform)prefab.transform;
            Check("卡框尺寸 192×256", Mathf.Approximately(rect.rect.width, 192f) &&
                                     Mathf.Approximately(rect.rect.height, 256f),
                "实际 " + rect.rect.size);

            var serialized = new SerializedObject(prefab);
            string[] fields =
            {
                "m_Body", "m_Art", "m_Placeholder", "m_IntentRoot", "m_IntentIcon",
                "m_IntentValue", "m_HpClip", "m_HpFill", "m_HpText", "m_ArmorRoot", "m_ArmorText",
            };

            foreach (string field in fields)
            {
                SerializedProperty property = serialized.FindProperty(field);
                bool ok = property != null && property.objectReferenceValue != null;
                Check("绑定 " + field, ok,
                    ok ? string.Empty : (property == null ? "字段不存在" : "为空"));
            }

            // 旋转体必须包住立绘与卡框：翻面动画只转它
            var body = serialized.FindProperty("m_Body")?.objectReferenceValue as RectTransform;
            if (body != null)
            {
                Check("goBody 尺寸与卡框一致", Mathf.Approximately(body.rect.width, 192f) &&
                                              Mathf.Approximately(body.rect.height, 256f),
                    "实际 " + body.rect.size);
                Check("goBody 下有 imgArt", body.Find("imgArt") != null, string.Empty);
                Check("goBody 下有 imgFrame", body.Find("imgFrame") != null, string.Empty);
            }

            log.AppendLine(failed == 0 ? "== 全部通过 ==" : "== 失败 " + failed + " 项 ==");
            return "[EnemyPrefabCheck]\n" + log;
        }
    }
}
