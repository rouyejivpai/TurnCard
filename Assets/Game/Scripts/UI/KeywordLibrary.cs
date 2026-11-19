using System.Collections.Generic;
using TurnCard.Game.Data;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 关键词提示的展开逻辑：把卡面描述里出现的关键词作为种子，
    /// 递归展开它们说明里提到的其他关键词
    /// （去重、按首次出现顺序、最多 <see cref="MaxTooltips"/> 个、遇到循环直接跳过）。
    /// </summary>
    public static class KeywordLibrary
    {
        public const int MaxTooltips = 5;

        public static KeywordDefinition Find(string name)
        {
            return KeywordCatalog.Find(name);
        }

        /// <summary>把一组关键词名递归展开成要显示的列表。</summary>
        public static List<KeywordDefinition> Expand(IEnumerable<string> seeds)
        {
            var result = new List<KeywordDefinition>();
            var visited = new HashSet<string>();

            var queue = new Queue<string>();
            if (seeds != null)
            {
                foreach (string seed in seeds)
                {
                    if (!string.IsNullOrEmpty(seed))
                    {
                        queue.Enqueue(seed);
                    }
                }
            }

            while (queue.Count > 0 && result.Count < MaxTooltips)
            {
                string name = queue.Dequeue();
                if (!visited.Add(name))
                {
                    continue;
                }

                KeywordDefinition keyword = Find(name);
                if (keyword == null)
                {
                    continue;
                }

                result.Add(keyword);

                // 说明里可能又提到别的关键词，继续展开
                foreach (string nested in CardText.ExtractKeywords(keyword.Description))
                {
                    if (!visited.Contains(nested))
                    {
                        queue.Enqueue(nested);
                    }
                }
            }

            return result;
        }

        /// <summary>内容重新生成后清掉缓存。</summary>
        public static void Invalidate()
        {
            KeywordCatalog.Invalidate();
        }
    }
}
