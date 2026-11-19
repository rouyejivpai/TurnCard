using System.Collections.Generic;
using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>卡牌品质，影响掉落权重与边框配色（当前仅记录）。</summary>
    public enum CardQuality
    {
        Common,     // 普通
        Uncommon,   // 精良
        Rare,       // 稀有
        Legendary,  // 传说
    }

    public enum CardType
    {
        Attack = 0,
        Skill = 1,
        Power = 2,
    }

    public enum CardFaceSide
    {
        Front = 0,
        Back = 1,
    }

    /// <summary>运行时读取的一面数据。</summary>
    public readonly struct CardFace
    {
        public CardFace(string displayName, int cost, CardType type, int damage, int armor,
            string description, string effects, IReadOnlyDictionary<string, string> parameters,
            string[] keywords)
        {
            DisplayName = displayName;
            Cost = cost;
            Type = type;
            Damage = damage;
            Armor = armor;
            Description = description;
            Effects = effects;
            Params = parameters;
            Keywords = keywords ?? EmptyKeywords;
        }

        private static readonly string[] EmptyKeywords = new string[0];

        public string DisplayName { get; }
        public int Cost { get; }
        public CardType Type { get; }
        public int Damage { get; }
        public int Armor { get; }
        public string Description { get; }

        /// <summary>
        /// 效果串，形如 "damage:6;armor:3;draw:2"。
        /// 为空表示按 Damage / Armor 两个数值走默认效果（旧表兼容）。
        /// </summary>
        public string Effects { get; }

        /// <summary>自定义参数（来自表里 params 列），供 {占位符} 引用。</summary>
        public IReadOnlyDictionary<string, string> Params { get; }

        /// <summary>
        /// 机制关键词（来自表里 keywords 列）。
        /// 与描述里的 [[...]] 无关——那个只影响显示，这里才是真正驱动规则的。
        /// </summary>
        public string[] Keywords { get; }

        public bool HasKeyword(string keyword)
        {
            if (Keywords == null || string.IsNullOrEmpty(keyword))
            {
                return false;
            }

            foreach (string owned in Keywords)
            {
                if (string.Equals(owned, keyword, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 是否需要玩家指定目标。由效果串决定，而不是看卡牌类型：
        /// <c>damage:3,all</c> 打全体、<c>damage:3,random</c> 随机一名，都不需要选目标。
        /// </summary>
        public bool NeedsTarget
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Effects))
                {
                    return Type == CardType.Attack;
                }

                foreach (string token in Effects.Split(';'))
                {
                    string entry = token.Trim();
                    if (entry.Length == 0)
                    {
                        continue;
                    }

                    int colon = entry.IndexOf(':');
                    string key = colon >= 0 ? entry.Substring(0, colon).Trim() : entry;

                    // 所有「伤害类」效果都要选目标：damage、damage_per_attack ...
                    // 只认字面 damage 的话，damage_per_attack 这类派生效果会既不需要目标、
                    // 结算时又拿不到 target，表现就是「打出后什么也没发生」。
                    if (!key.StartsWith("damage", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] args = colon >= 0
                        ? entry.Substring(colon + 1).Split(',')
                        : new string[0];

                    if (args.Length > 1)
                    {
                        string mode = args[1].Trim();
                        if (string.Equals(mode, "all", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mode, "random", System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// 一张双面卡的定义。由 DataTables/Cards.csv 生成，不要手工编辑生成出来的资产。
    /// 两面的费用、类型与效果互相独立（Q17）。
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Card Definition", fileName = "Card")]
    public sealed class CardDefinition : ScriptableObject
    {
        [SerializeField] private int m_Id;

        [Tooltip("起始牌组里放几张（已废弃，改用 StarterDeck 表）")]
        [SerializeField] private int m_StarterCount;

        [Tooltip("卡牌品质")]
        [SerializeField] private CardQuality m_Quality = CardQuality.Common;

        [SerializeField] private string m_FrontName = string.Empty;
        [SerializeField] private int m_FrontCost;
        [SerializeField] private CardType m_FrontType;
        [SerializeField] private int m_FrontDamage;
        [SerializeField] private int m_FrontArmor;
        [TextArea] [SerializeField] private string m_FrontDescription = string.Empty;
        [Tooltip("形如 damage:6;armor:3;draw:2，留空则按 Damage/Armor 走默认效果")]
        [SerializeField] private string m_FrontEffects = string.Empty;
        [Tooltip("自定义参数，形如 dmg=6;hits=2，供卡面文本的 {dmg} 占位符引用")]
        [SerializeField] private string m_FrontParams = string.Empty;
        [Tooltip("机制关键词，分号分隔，如「瞬发;保留」。描述里的 [[...]] 只负责显示")]
        [SerializeField] private string m_FrontKeywords = string.Empty;

        [SerializeField] private string m_BackName = string.Empty;
        [SerializeField] private int m_BackCost;
        [SerializeField] private CardType m_BackType;
        [SerializeField] private int m_BackDamage;
        [SerializeField] private int m_BackArmor;
        [TextArea] [SerializeField] private string m_BackDescription = string.Empty;
        [Tooltip("形如 damage:6;armor:3;draw:2，留空则按 Damage/Armor 走默认效果")]
        [SerializeField] private string m_BackEffects = string.Empty;
        [Tooltip("自定义参数，形如 dmg=6;hits=2，供卡面文本的 {dmg} 占位符引用")]
        [SerializeField] private string m_BackParams = string.Empty;
        [Tooltip("机制关键词，分号分隔，如「瞬发;保留」。描述里的 [[...]] 只负责显示")]
        [SerializeField] private string m_BackKeywords = string.Empty;

        public int Id => m_Id;

        /// <summary>起始牌组里这张牌放几张（已废弃，改用 StarterDeck 表）。</summary>
        public int StarterCount => m_StarterCount;

        /// <summary>卡牌品质。</summary>
        public CardQuality Quality => m_Quality;

        public CardFace Front => new CardFace(m_FrontName, m_FrontCost, m_FrontType,
            m_FrontDamage, m_FrontArmor, m_FrontDescription, m_FrontEffects,
            ParseParams(m_FrontParams), ParseKeywords(m_FrontKeywords));

        public CardFace Back => new CardFace(m_BackName, m_BackCost, m_BackType,
            m_BackDamage, m_BackArmor, m_BackDescription, m_BackEffects,
            ParseParams(m_BackParams), ParseKeywords(m_BackKeywords));

        private static string[] ParseKeywords(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[0];
            }

            var result = new List<string>();
            foreach (string entry in raw.Split(';'))
            {
                string keyword = entry.Trim();
                if (keyword.Length > 0 && !result.Contains(keyword))
                {
                    result.Add(keyword);
                }
            }

            return result.ToArray();
        }

        private static readonly Dictionary<string, string> EmptyParams =
            new Dictionary<string, string>(0);

        private static IReadOnlyDictionary<string, string> ParseParams(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return EmptyParams;
            }

            var result = new Dictionary<string, string>();
            foreach (string entry in raw.Split(';'))
            {
                string pair = entry.Trim();
                if (pair.Length == 0)
                {
                    continue;
                }

                int eq = pair.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                result[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
            }

            return result.Count == 0 ? EmptyParams : result;
        }

        public CardFace GetFace(CardFaceSide side)
        {
            return side == CardFaceSide.Front ? Front : Back;
        }
    }
}
