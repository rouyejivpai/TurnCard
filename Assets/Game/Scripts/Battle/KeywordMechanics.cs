using System;
using System.Collections.Generic;
using System.Reflection;

namespace TurnCard.Game.Battle
{
    /// <summary>
    /// 机制关键词的名字常量与注册表。
    ///
    /// 只有这里列出的关键词才有规则含义；描述里的 <c>[[...]]</c> 仅用于显示，
    /// 真正驱动规则的是表里的 keywords 列。
    /// </summary>
    public static class KeywordMechanics
    {
        /// <summary>卡牌翻到此面时，立即触发这一面的效果一次。</summary>
        public const string Instant = "瞬发";

        /// <summary>回合结束时这张牌不进入弃牌堆，留在手上。</summary>
        public const string Keep = "保留";

        /// <summary>抽到这张牌时立刻执行它当前面的效果，牌仍留在手上（以吾之名）。</summary>
        public const string DrawRelease = "抽到释放";

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            Instant,
            Keep,
            DrawRelease,
        };

        public static bool IsKnown(string keyword)
        {
            return !string.IsNullOrEmpty(keyword) && Known.Contains(keyword);
        }

        public static IEnumerable<string> KnownKeywords => Known;

        /// <summary>注册一个由代码提供的关键词（光环类），供校验与查询使用。</summary>
        public static void Register(string keyword)
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                Known.Add(keyword);
            }
        }
    }

    /// <summary>
    /// 光环型关键词：只要这一面朝上（牌在手上），就持续生效。
    ///
    /// 例：「双重瞬发」——瞬发触发两次。
    /// 新光环只需要写一个实现 + 打特性，表里在 keywords 列写名字即可。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AuraKeywordAttribute : Attribute
    {
        public AuraKeywordAttribute(string keyword)
        {
            Keyword = keyword;
        }

        public string Keyword { get; }
    }

    public interface IAuraKeyword
    {
        int ModifyInstantRepeat(BattleState state, CardInstance owner);
    }

    /// <summary>光环关键词的查询入口（第一次使用时反射扫描）。</summary>
    public static class AuraKeywords
    {
        private static Dictionary<string, IAuraKeyword> s_Map;

        public static bool TryGet(string keyword, out IAuraKeyword aura)
        {
            EnsureLoaded();
            return s_Map.TryGetValue(keyword, out aura);
        }

        /// <summary>强制扫描一次光环实现，供校验器使用。</summary>
        public static void EnsureLoaded()
        {
            if (s_Map != null)
            {
                return;
            }

            s_Map = new Dictionary<string, IAuraKeyword>(StringComparer.Ordinal);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || !typeof(IAuraKeyword).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var attribute = type.GetCustomAttribute<AuraKeywordAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    s_Map[attribute.Keyword] = (IAuraKeyword)Activator.CreateInstance(type);
                    KeywordMechanics.Register(attribute.Keyword);
                }
            }
        }
    }
}
