using System.Collections.Generic;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.Game
{
    /// <summary>
    /// 关键词表在运行时的索引。
    ///
    /// 说明：Cards 表的 keywords 列是**机制标记（tag）**，与这里无关。
    /// 这里索引的是 Keywords.xlsx —— 用于「描述里出现了哪些词」的检索与悬停说明。
    /// </summary>
    public static class KeywordCatalog
    {
        private static Dictionary<string, KeywordDefinition> s_Map;
        private static List<string> s_Names;

        /// <summary>全部关键词名，按长度从长到短（保证「双重瞬发」优先于「瞬发」这类包含关系）。</summary>
        public static IReadOnlyList<string> Names
        {
            get
            {
                EnsureLoaded();
                return s_Names;
            }
        }

        public static KeywordDefinition Find(string name)
        {
            EnsureLoaded();
            return name != null && s_Map.TryGetValue(name, out KeywordDefinition keyword)
                ? keyword
                : null;
        }

        public static void EnsureLoaded()
        {
            if (s_Map != null)
            {
                return;
            }

            s_Map = new Dictionary<string, KeywordDefinition>();
            s_Names = new List<string>();

            var content = Resources.Load<BattleContent>("BattleContent");
            if (content == null || content.Keywords == null)
            {
                return;
            }

            foreach (KeywordDefinition keyword in content.Keywords)
            {
                if (keyword == null || string.IsNullOrEmpty(keyword.Name))
                {
                    continue;
                }

                s_Map[keyword.Name] = keyword;
                s_Names.Add(keyword.Name);
            }

            s_Names.Sort((a, b) => b.Length != a.Length
                ? b.Length.CompareTo(a.Length)
                : string.CompareOrdinal(a, b));
        }

        /// <summary>内容重新生成后清掉缓存。</summary>
        public static void Invalidate()
        {
            s_Map = null;
            s_Names = null;
        }
    }
}
