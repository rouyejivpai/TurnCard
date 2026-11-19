using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 最小可用的 xlsx 读取器：读第一个工作表，返回「行 × 列」的字符串表。
    ///
    /// 只依赖 .NET 自带的压缩与 XML，不引入第三方库，
    /// 因此策划直接用 Excel 编辑 DataTables/*.xlsx 即可，不需要另存 CSV。
    /// </summary>
    internal static class Xlsx
    {
        public static List<List<string>> ReadRows(string path)
        {
            var rows = new List<List<string>>();

            // 用 FileShare.ReadWrite 打开：即使 Excel / WPS 正开着这张表，也能读取生成
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> shared = ReadSharedStrings(archive);

                ZipArchiveEntry sheet = archive.Entries
                    .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.FullName, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (sheet == null)
                {
                    throw new InvalidOperationException($"xlsx 里找不到工作表：{path}");
                }

                using (Stream sheetStream = sheet.Open())
                {
                    XDocument document = XDocument.Load(sheetStream);
                    XNamespace ns = document.Root?.Name.Namespace ?? XNamespace.None;

                    // 行数据在 <sheetData> 里面，不是 worksheet 的直接子节点
                    XElement sheetData = document.Root?.Element(ns + "sheetData");
                    if (sheetData == null)
                    {
                        return rows;
                    }

                    foreach (XElement rowElement in sheetData.Elements(ns + "row"))
                    {
                        var row = new List<string>();
                        foreach (XElement cell in rowElement.Elements(ns + "c"))
                        {
                            int column = ColumnIndex((string)cell.Attribute("r"));
                            while (row.Count <= column)
                            {
                                row.Add(string.Empty);
                            }

                            row[column] = ReadCell(cell, ns, shared);
                        }

                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var result = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return result;
            }

            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                XNamespace ns = document.Root?.Name.Namespace ?? XNamespace.None;

                foreach (XElement item in document.Root.Elements(ns + "si"))
                {
                    // 一个 si 里可能被拆成多个 r/t（富文本），拼起来即可
                    result.Add(string.Concat(item.Descendants(ns + "t").Select(t => t.Value)));
                }
            }

            return result;
        }

        private static string ReadCell(XElement cell, XNamespace ns, List<string> shared)
        {
            string type = (string)cell.Attribute("t") ?? string.Empty;

            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                string indexText = (string)cell.Element(ns + "v");
                if (int.TryParse(indexText, out int index) && index >= 0 && index < shared.Count)
                {
                    return shared[index];
                }

                return string.Empty;
            }

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
            }

            if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase))
            {
                string raw = (string)cell.Element(ns + "v");
                return raw == "1" ? "true" : "false";
            }

            return (string)cell.Element(ns + "v") ?? string.Empty;
        }

        /// <summary>把单元格引用（如 "AB12"）换算成从 0 开始的列号。</summary>
        private static int ColumnIndex(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return 0;
            }

            int column = 0;
            foreach (char ch in reference)
            {
                if (!char.IsLetter(ch))
                {
                    break;
                }

                column = column * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }

            return Math.Max(0, column - 1);
        }
    }
}
