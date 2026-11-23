#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 涓€娆℃€э細鎶婃暟鎹〃閲嶆柊鐢熸垚鎴?SO锛圡CP 閫氶亾鏂紑鏃剁敤锛夈€?    /// 瀹屾垚鍚庡啓鏍囪鏂囦欢锛屼箣鍚庝笉鍐嶆墽琛岋紱琛ヤ竵鍙互鍒犳帀銆?    /// </summary>
    [InitializeOnLoad]
    public static class OneOffRegenerateData
    {
        private const int VersionTag = 2;
        private const string MarkerPath = "Library/TurnCard_regen_done.txt";

        static OneOffRegenerateData()
        {
            if (System.IO.File.Exists(MarkerPath))
            {
                return;
            }

            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (System.IO.File.Exists(MarkerPath))
            {
                return;
            }

            DataTableImporter.GenerateAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = DataTableImporter.BuildReport();
            Debug.Log("[涓€娆℃€ 鏁版嵁琛ㄥ凡閲嶆柊鐢熸垚銆? +
                      (string.IsNullOrEmpty(report) ? "涓€鑷存€э細閫氳繃" : "涓€鑷存€э細" + report));

            System.IO.File.WriteAllText(MarkerPath, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
#endif
