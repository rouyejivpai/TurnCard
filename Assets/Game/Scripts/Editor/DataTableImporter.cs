using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TurnCard.Game.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 把 DataTables/*.xlsx 生成为 ScriptableObject 资产。
    ///
    /// 约定：
    /// - xlsx 是唯一数据源，位于工程根目录的 DataTables/（不进 Assets），用 Excel 直接编辑；
    /// - 生成产物位于 Assets/Game/Data/Generated/&lt;表名&gt;/&lt;id&gt;.asset，是产物，不手工修改；
    /// - 同一 id 复用已有资产，保证 GUID 不变，外部引用不会断；
    /// - 每张表生成后记录文件哈希，用于发现「改了表但没重新生成」。
    /// </summary>
    public static class DataTableImporter
    {
        private const string SourceFolder = "DataTables";
        private const string GeneratedRoot = "Assets/Game/Data/Generated";
        private const string ManifestPath = "DataTables/.generated.json";
        private const string MenuRoot = "Tools/TurnCard/数据表/";

        private sealed class TableSpec
        {
            public TableSpec(string tableName, Type assetType, string subFolder)
            {
                TableName = tableName;
                AssetType = assetType;
                FolderPath = GeneratedRoot + "/" + subFolder;
            }

            public string TableName { get; }
            public Type AssetType { get; }
            public string FolderPath { get; }
        }

        private static readonly TableSpec[] Specs =
        {
            new TableSpec("Cards", typeof(CardDefinition), "Cards"),
            new TableSpec("Enemies", typeof(EnemyDefinition), "Enemies"),
            new TableSpec("Keywords", typeof(KeywordDefinition), "Keywords"),
            new TableSpec("StarterDeck", typeof(StarterDeckEntry), "StarterDeck"),
            new TableSpec("EnemyIntents", typeof(EnemyIntentDefinition), "EnemyIntents"),
            new TableSpec("Encounters", typeof(EncounterEntry), "Encounters"),
            new TableSpec("Buffs", typeof(BuffDefinition), "Buffs"),
        };

        [MenuItem(MenuRoot + "生成 SO", priority = 100)]
        public static void GenerateAll()
        {
            var log = new StringBuilder();
            int created = 0;
            int updated = 0;
            var manifest = LoadManifest();

            foreach (TableSpec spec in Specs)
            {
                string tablePath = TablePath(spec.TableName);
                if (!File.Exists(tablePath))
                {
                    log.AppendLine($"跳过 {spec.TableName}：找不到 {tablePath}");
                    continue;
                }

                List<List<string>> rows;
                try
                {
                    rows = Xlsx.ReadRows(tablePath);
                }
                catch (Exception exception)
                {
                    // 表被 Excel / WPS 独占打开时会读不到，不让它中断其他表
                    log.AppendLine($"跳过 {spec.TableName}：读取失败（{exception.Message}）。" +
                                   "请关闭正在编辑该表的程序后重试。");
                    continue;
                }

                if (rows.Count < 2)
                {
                    log.AppendLine($"跳过 {spec.TableName}：只有表头，没有数据行");
                    continue;
                }

                List<string> header = rows[0];
                int idColumn = header.FindIndex(c =>
                    string.Equals(c.Trim(), "id", StringComparison.OrdinalIgnoreCase));
                if (idColumn < 0)
                {
                    log.AppendLine($"跳过 {spec.TableName}：表头缺少 id 列");
                    continue;
                }

                EnsureFolder(spec.FolderPath);
                var knownIds = new HashSet<int>();
                var usedProperties = new HashSet<string>(StringComparer.Ordinal);
                var knownFields = DescribeFields(spec.AssetType);

                for (int r = 1; r < rows.Count; r++)
                {
                    List<string> row = rows[r];
                    if (row.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    if (!int.TryParse(Value(row, idColumn), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int id))
                    {
                        log.AppendLine($"{spec.TableName} 第 {r + 1} 行：id 不是整数，已跳过");
                        continue;
                    }

                    if (!knownIds.Add(id))
                    {
                        log.AppendLine($"{spec.TableName} 第 {r + 1} 行：id {id} 重复，已跳过");
                        continue;
                    }

                    string assetPath = $"{spec.FolderPath}/{id}.asset";
                    ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    bool isNew = asset == null;
                    if (isNew)
                    {
                        asset = ScriptableObject.CreateInstance(spec.AssetType);
                    }

                    ApplyRow(asset, header, row, usedProperties, knownFields, log, spec, r + 1);

                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(asset, assetPath);
                        created++;
                    }
                    else
                    {
                        EditorUtility.SetDirty(asset);
                        updated++;
                    }
                }

                ReportOrphans(spec, knownIds, log);
                manifest.Set(spec.TableName, HashFile(tablePath), knownIds.Count);
                log.AppendLine($"{spec.TableName}：{knownIds.Count} 行");
            }

            UpdateBattleContent();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SaveManifest(manifest);

            string summary = $"新建 {created} 个，更新 {updated} 个。";
            Debug.Log("[数据表] 生成完成。" + summary + Environment.NewLine + log);
        }

        [MenuItem(MenuRoot + "检查一致性", priority = 101)]
        public static void CheckAll()
        {
            string report = BuildReport();
            if (report.Length == 0)
            {
                Debug.Log("[数据表] 检查通过，源表与生成产物一致。");
            }
            else
            {
                Debug.LogWarning("[数据表] 检查发现问题：" + Environment.NewLine + report);
            }
        }

        /// <summary>
        /// 返回源表与生成产物之间的全部不一致项；完全一致时返回空字符串。
        /// 不写日志、不弹窗，便于被编辑器脚本或自动化调用。
        /// </summary>
        public static string BuildReport()
        {
            var problems = new StringBuilder();
            var manifest = LoadManifest();

            foreach (TableSpec spec in Specs)
            {
                string tablePath = TablePath(spec.TableName);
                if (!File.Exists(tablePath))
                {
                    problems.AppendLine($"{spec.TableName}：源表不存在（{tablePath}）");
                    continue;
                }

                string recordedHash = manifest.GetHash(spec.TableName);
                string actualHash = HashFile(tablePath);
                if (string.IsNullOrEmpty(recordedHash))
                {
                    problems.AppendLine($"{spec.TableName}：从未生成过");
                }
                else if (!string.Equals(recordedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    problems.AppendLine($"{spec.TableName}：源表已修改，但生成产物未更新");
                }

                List<List<string>> rows = Xlsx.ReadRows(tablePath);
                if (rows.Count >= 2)
                {
                    int idColumn = rows[0].FindIndex(c =>
                        string.Equals(c.Trim(), "id", StringComparison.OrdinalIgnoreCase));
                    if (idColumn >= 0)
                    {
                        var ids = new HashSet<int>();
                        for (int r = 1; r < rows.Count; r++)
                        {
                            if (rows[r].All(string.IsNullOrWhiteSpace))
                            {
                                continue;
                            }

                            if (int.TryParse(Value(rows[r], idColumn), NumberStyles.Integer,
                                    CultureInfo.InvariantCulture, out int id))
                            {
                                ids.Add(id);
                                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                                        $"{spec.FolderPath}/{id}.asset") == null)
                                {
                                    problems.AppendLine($"{spec.TableName}：id {id} 缺少对应的资产");
                                }
                            }
                        }

                        ReportOrphans(spec, ids, problems);
                    }
                }
            }

            return problems.ToString();
        }

        private static void ApplyRow(Object asset, List<string> header, List<string> row,
            HashSet<string> usedProperties, Dictionary<string, string> knownFields,
            StringBuilder log, TableSpec spec, int rowNumber)
        {
            var serialized = new SerializedObject(asset);

            for (int c = 0; c < header.Count; c++)
            {
                string column = header[c].Trim();
                if (column.Length == 0)
                {
                    continue;
                }

                string propertyName = "m_" + ToPascalCase(column);
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    if (!usedProperties.Contains(propertyName))
                    {
                        log.AppendLine($"{spec.TableName}：列「{column}」找不到字段 {propertyName}，" +
                                       "已忽略该列");
                        usedProperties.Add(propertyName);
                    }

                    continue;
                }

                string raw = Value(row, c);
                if (!Assign(serialized, property, raw, knownFields, out string error))
                {
                    log.AppendLine($"{spec.TableName} 第 {rowNumber} 行「{column}」：{error}");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Assign(SerializedObject serialized, SerializedProperty property,
            string raw, Dictionary<string, string> knownFields, out string error)
        {
            error = null;
            string value = raw?.Trim() ?? string.Empty;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (value.Length == 0)
                    {
                        property.intValue = 0;
                        return true;
                    }

                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out int intValue))
                    {
                        property.intValue = intValue;
                        return true;
                    }

                    error = $"「{value}」不是整数";
                    return false;

                case SerializedPropertyType.Float:
                    if (value.Length == 0)
                    {
                        property.floatValue = 0f;
                        return true;
                    }

                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float floatValue))
                    {
                        property.floatValue = floatValue;
                        return true;
                    }

                    error = $"「{value}」不是数字（小数请用 . 而不是 ,）";
                    return false;

                case SerializedPropertyType.Boolean:
                    if (TryParseBool(value, out bool boolValue))
                    {
                        property.boolValue = boolValue;
                        return true;
                    }

                    error = $"「{value}」不是布尔值（true/false/1/0）";
                    return false;

                case SerializedPropertyType.String:
                    property.stringValue = raw ?? string.Empty;
                    return true;

                case SerializedPropertyType.Enum:
                    if (value.Length == 0)
                    {
                        property.enumValueIndex = 0;
                        return true;
                    }

                    if (!knownFields.TryGetValue(property.name, out string enumTypeName) ||
                        !TryParseEnum(enumTypeName, value, out int enumIndex))
                    {
                        error = $"「{value}」不是有效枚举值";
                        return false;
                    }

                    property.enumValueIndex = enumIndex;
                    return true;

                case SerializedPropertyType.ObjectReference:
                    if (value.Length == 0)
                    {
                        property.objectReferenceValue = null;
                        return true;
                    }

                    Object referenced = AssetDatabase.LoadAssetAtPath<Object>(value);
                    if (referenced == null)
                    {
                        error = $"找不到资源「{value}」（路径需从 Assets/ 开始）";
                        return false;
                    }

                    property.objectReferenceValue = referenced;
                    return true;

                default:
                    error = $"字段类型 {property.propertyType} 暂不支持";
                    return false;
            }
        }

        private static Dictionary<string, string> DescribeFields(Type assetType)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (FieldInfo field in assetType.GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                result[field.Name] = field.FieldType.AssemblyQualifiedName;
            }

            return result;
        }

        private static bool TryParseEnum(string assemblyQualifiedName, string value, out int index)
        {
            index = -1;
            Type type = Type.GetType(assemblyQualifiedName);
            if (type == null || !type.IsEnum)
            {
                return false;
            }

            string[] names = Enum.GetNames(type);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBool(string value, out bool result)
        {
            result = false;
            if (value.Length == 0)
            {
                return true;
            }

            switch (value.ToLowerInvariant())
            {
                case "true":
                case "1":
                case "是":
                case "y":
                case "yes":
                    result = true;
                    return true;
                case "false":
                case "0":
                case "否":
                case "n":
                case "no":
                    result = false;
                    return true;
                default:
                    return false;
            }
        }

        private static void ReportOrphans(TableSpec spec, HashSet<int> knownIds, StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(spec.FolderPath))
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject",
                         new[] { spec.FolderPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (int.TryParse(fileName, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int id) && !knownIds.Contains(id))
                {
                    // 生成产物是表的镜像：表里删掉的行，资产也要删掉
                    AssetDatabase.DeleteAsset(path);
                    log.AppendLine($"{spec.TableName}：id {id} 在源表中已删除，资产 {path} 已移除");
                }
            }
        }

        private static string Value(List<string> row, int index)
        {
            return index >= 0 && index < row.Count ? row[index] : string.Empty;
        }

        /// <summary>
        /// 生成一份内容索引，把当前所有卡牌与敌人聚合成单个资产，
        /// 运行时只需 Resources.Load 这一个文件。
        /// </summary>
        private static void UpdateBattleContent()
        {
            const string resourcesFolder = "Assets/Resources";
            const string contentPath = "Assets/Resources/BattleContent.asset";

            EnsureFolder(resourcesFolder);

            BattleContent content = AssetDatabase.LoadAssetAtPath<BattleContent>(contentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<BattleContent>();
                AssetDatabase.CreateAsset(content, contentPath);
            }

            content.SetContent(
                LoadAll<CardDefinition>(GeneratedRoot + "/Cards"),
                LoadAll<EnemyDefinition>(GeneratedRoot + "/Enemies"),
                LoadAll<KeywordDefinition>(GeneratedRoot + "/Keywords"),
                LoadAll<StarterDeckEntry>(GeneratedRoot + "/StarterDeck"),
                LoadAll<EnemyIntentDefinition>(GeneratedRoot + "/EnemyIntents"),
                LoadAll<EncounterEntry>(GeneratedRoot + "/Encounters"),
                LoadAll<BuffDefinition>(GeneratedRoot + "/Buffs"));
            EditorUtility.SetDirty(content);
        }

        private static T[] LoadAll<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return new T[0];
            }

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            var assets = new List<T>(guids.Length);
            foreach (string guid in guids)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            assets.Sort((a, b) => string.CompareOrdinal(
                AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            return assets.ToArray();
        }

        private static string ToPascalCase(string column)
        {
            string[] parts = column.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder();
            foreach (string part in parts)
            {
                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part.Substring(1));
                }
            }

            return builder.ToString();
        }

        private static string TablePath(string tableName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, SourceFolder, tableName + ".xlsx");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string HashFile(string path)
        {
            try
            {
                // 允许共享读：表格可能正被 Excel / WPS 打开
                using (var sha = SHA256.Create())
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class Manifest
        {
            public List<Entry> entries = new List<Entry>();

            public void Set(string table, string hash, int rows)
            {
                Entry entry = entries.FirstOrDefault(e => e.table == table);
                if (entry == null)
                {
                    entry = new Entry { table = table };
                    entries.Add(entry);
                }

                entry.hash = hash;
                entry.rows = rows;
                entry.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture);
            }

            public string GetHash(string table)
            {
                Entry entry = entries.FirstOrDefault(e => e.table == table);
                return entry?.hash;
            }

            [Serializable]
            public sealed class Entry
            {
                public string table;
                public string hash;
                public int rows;
                public string generatedAt;
            }
        }

        private static Manifest LoadManifest()
        {
            string path = ManifestFullPath();
            if (!File.Exists(path))
            {
                return new Manifest();
            }

            try
            {
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(path, Encoding.UTF8))
                       ?? new Manifest();
            }
            catch (Exception)
            {
                return new Manifest();
            }
        }

        private static void SaveManifest(Manifest manifest)
        {
            string path = ManifestFullPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        }

        private static string ManifestFullPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, ManifestPath);
        }
    }
}
