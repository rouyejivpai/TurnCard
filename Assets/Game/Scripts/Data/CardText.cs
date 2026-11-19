using System.Collections.Generic;
using System.Text;
using TurnCard.Game.Data;

namespace TurnCard.Game
{
    /// <summary>
    /// 卡面文本的渲染与关键词检索。
    ///
    /// 配置规则：
    /// - 用**加粗标记**包住的关键词：<c>**瞬发**</c>（也兼容 <c>[[瞬发]]</c> 与 <c>&lt;b&gt;瞬发&lt;/b&gt;</c>）
    /// - <c>*风味文字*</c> 斜体
    /// - <c>{damage}</c> 数值占位符，按当前面实时取值
    ///
    /// 提示栏只认**加粗片段里、且能在关键词表里查到的词**：
    /// 加粗但不在词表里的（如 <c>**双管**</c>）只加粗，不弹提示。
    ///
    /// 注意：Cards 表的 keywords 列是机制 tag，与这里的显示关键词无关。
    /// </summary>
    public static class CardText
    {
        public const string BoldPrefix = "**";
        public const string LegacyBoldPrefix = "[[";
        public const string LegacyBoldSuffix = "]]";
        public const string TagBoldOpen = "<b>";
        public const string TagBoldClose = "</b>";

        /// <summary>把带标记的原始文本转成 TMP 富文本。</summary>
        public static string Format(string raw, CardFace face)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length + 16);
            int i = 0;

            while (i < raw.Length)
            {
                // 加粗：**文字**
                if (StartsWith(raw, i, BoldPrefix))
                {
                    int end = raw.IndexOf(BoldPrefix, i + BoldPrefix.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        string bold = raw.Substring(i + BoldPrefix.Length,
                            end - i - BoldPrefix.Length);
                        builder.Append("<b>").Append(ResolveAll(bold, face)).Append("</b>");
                        i = end + BoldPrefix.Length;
                        continue;
                    }
                }

                // 加粗（兼容旧写法）：[[文字]]
                if (StartsWith(raw, i, LegacyBoldPrefix))
                {
                    int end = raw.IndexOf(LegacyBoldSuffix, i + LegacyBoldPrefix.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        string bold = raw.Substring(i + LegacyBoldPrefix.Length,
                            end - i - LegacyBoldPrefix.Length);
                        builder.Append("<b>").Append(ResolveAll(bold, face)).Append("</b>");
                        i = end + LegacyBoldSuffix.Length;
                        continue;
                    }
                }

                // 加粗（直接写 TMP 标签）：<b>文字</b>
                if (StartsWith(raw, i, TagBoldOpen))
                {
                    int end = raw.IndexOf(TagBoldClose, i + TagBoldOpen.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        string bold = raw.Substring(i + TagBoldOpen.Length,
                            end - i - TagBoldOpen.Length);
                        builder.Append("<b>").Append(ResolveAll(bold, face)).Append("</b>");
                        i = end + TagBoldClose.Length;
                        continue;
                    }
                }

                // 斜体：*文字*
                if (raw[i] == '*')
                {
                    int end = raw.IndexOf('*', i + 1);
                    if (end > i)
                    {
                        string flavor = raw.Substring(i + 1, end - i - 1);
                        builder.Append("<i>").Append(ResolveAll(flavor, face)).Append("</i>");
                        i = end + 1;
                        continue;
                    }

                    // 没有配对的 *（例如「攻击牌数* 点伤害」这种数学乘号）：
                    // 当成普通字符输出，否则下面的扫描循环不会前进，直接死循环
                    builder.Append('*');
                    i++;
                    continue;
                }

                if (raw[i] == '{')
                {
                    int end = raw.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        string token = raw.Substring(i + 1, end - i - 1).Trim();
                        builder.Append(Resolve(token, face));
                        i = end + 1;
                        continue;
                    }

                    // 没有配对的 { ：同样当普通字符，避免指针不前进
                    builder.Append('{');
                    i++;
                    continue;
                }

                int next = i;
                while (next < raw.Length && raw[next] != '*' && raw[next] != '{' &&
                       !StartsWith(raw, next, LegacyBoldPrefix) &&
                       !StartsWith(raw, next, TagBoldOpen))
                {
                    next++;
                }

                // 兜底：任何情况下都要前进，否则解析器会卡死
                if (next <= i)
                {
                    next = i + 1;
                }

                builder.Append(raw, i, next - i);
                i = next;
            }

            return builder.ToString();
        }

        /// <summary>
        /// 描述里被**加粗**、且在关键词表里查得到的词（按首次出现顺序，已去重）。
        /// 这就是悬停提示栏要显示的关键词。
        /// </summary>
        public static List<string> ExtractKeywords(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            foreach (string bold in ExtractBoldSegments(raw))
            {
                string name = bold.Trim();
                if (name.Length == 0 || result.Contains(name))
                {
                    continue;
                }

                // 加粗了但不在关键词表里的，只是强调，不弹提示
                if (KeywordCatalog.Find(name) != null)
                {
                    result.Add(name);
                }
            }

            return result;
        }

        /// <summary>取出文本里所有加粗片段（三种写法都认）。</summary>
        public static List<string> ExtractBoldSegments(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            int i = 0;
            while (i < raw.Length)
            {
                if (StartsWith(raw, i, BoldPrefix))
                {
                    int end = raw.IndexOf(BoldPrefix, i + BoldPrefix.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        result.Add(raw.Substring(i + BoldPrefix.Length,
                            end - i - BoldPrefix.Length));
                        i = end + BoldPrefix.Length;
                        continue;
                    }
                }

                if (StartsWith(raw, i, LegacyBoldPrefix))
                {
                    int end = raw.IndexOf(LegacyBoldSuffix, i + LegacyBoldPrefix.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        result.Add(raw.Substring(i + LegacyBoldPrefix.Length,
                            end - i - LegacyBoldPrefix.Length));
                        i = end + LegacyBoldSuffix.Length;
                        continue;
                    }
                }

                if (StartsWith(raw, i, TagBoldOpen))
                {
                    int end = raw.IndexOf(TagBoldClose, i + TagBoldOpen.Length,
                        System.StringComparison.Ordinal);
                    if (end > i)
                    {
                        result.Add(raw.Substring(i + TagBoldOpen.Length,
                            end - i - TagBoldOpen.Length));
                        i = end + TagBoldClose.Length;
                        continue;
                    }
                }

                i++;
            }

            return result;
        }

        /// <summary>去掉所有标记、替换占位符后的纯文本。</summary>
        public static string ToPlain(string raw, CardFace face)
        {
            string formatted = Format(raw, face);
            var builder = new StringBuilder(formatted.Length);
            bool inTag = false;

            foreach (char c in formatted)
            {
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>')
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        /// <summary>把一段文字里的占位符全部替换掉。</summary>
        private static string ResolveAll(string text, CardFace face)
        {
            if (text.IndexOf('{') < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    int end = text.IndexOf('}', i + 1);
                    if (end > i)
                    {
                        builder.Append(Resolve(text.Substring(i + 1, end - i - 1).Trim(), face));
                        i = end + 1;
                        continue;
                    }
                }

                builder.Append(text[i]);
                i++;
            }

            return builder.ToString();
        }

        private static string Resolve(string token, CardFace face)
        {
            switch (token)
            {
                case "name":
                    return face.DisplayName;
                case "cost":
                    return face.Cost.ToString();
                case "damage":
                    return face.Damage.ToString();
                case "armor":
                    return face.Armor.ToString();
            }

            if (face.Params != null && face.Params.TryGetValue(token, out string value))
            {
                return value;
            }

            // 解析不到就原样保留，方便在界面上发现问题
            return "{" + token + "}";
        }

        private static bool StartsWith(string text, int index, string value)
        {
            return index + value.Length <= text.Length &&
                   string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }
    }
}
