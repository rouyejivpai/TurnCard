using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TurnCard.Game.Data
{
    /// <summary>同种 buff 再次施加时的处理方式。</summary>
    public enum BuffStackRule
    {
        /// <summary>叠加层数（可受最大层数限制）。</summary>
        Stack,

        /// <summary>只刷新持续时间，层数不变。</summary>
        Refresh,

        /// <summary>取层数较大的那个。</summary>
        Max,

        /// <summary>不叠加，已经存在就忽略。</summary>
        None,
    }

    /// <summary>buff 层数怎么消耗。</summary>
    public enum BuffConsume
    {
        /// <summary>按回合：每个回合结束减 1 层。</summary>
        Turn,

        /// <summary>按次数：每次触发后减 1 层。</summary>
        Count,

        /// <summary>永久，直到被驱散。</summary>
        Permanent,
    }

    /// <summary>buff 在什么时机触发效果。</summary>
    public enum BuffTrigger
    {
        None,
        TurnStart,
        TurnEnd,
        Flip,
        CardPlayed,
        Death,
    }

    /// <summary>一条被动修饰器：对某个属性做一次运算。</summary>
    public readonly struct BuffModifier
    {
        public BuffModifier(string stat, char op, float value)
        {
            Stat = stat;
            Op = op;
            Value = value;
        }

        /// <summary>属性名，如 damage_taken、damage_dealt、attack、armor_gain。</summary>
        public string Stat { get; }

        /// <summary>运算符：+ - *</summary>
        public char Op { get; }

        public float Value { get; }
    }

    /// <summary>
    /// 一个 buff 的定义。由 <c>DataTables/Buffs.xlsx</c> 生成。
    ///
    /// 表列：id、name、displayName、description、icon、
    ///       stackRule、consume、maxStacks、modifier、trigger、triggerEffect
    /// </summary>
    [CreateAssetMenu(menuName = "TurnCard/Data/Buff", fileName = "Buff")]
    public sealed class BuffDefinition : ScriptableObject
    {
        [SerializeField] private int m_Id;
        [SerializeField] private string m_Name = string.Empty;
        [SerializeField] private string m_DisplayName = string.Empty;

        [TextArea]
        [SerializeField] private string m_Description = string.Empty;

        [Tooltip("图标资源名，放在 Resources/Art/UI/buffs 下")]
        [SerializeField] private string m_Icon = string.Empty;

        [SerializeField] private BuffStackRule m_StackRule = BuffStackRule.Stack;
        [SerializeField] private BuffConsume m_Consume = BuffConsume.Count;

        [Tooltip("最大层数，0 表示不限")]
        [SerializeField] private int m_MaxStacks;

        [Tooltip("被动修饰器，多条用分号分隔，如 damage_taken:+3;damage_dealt:*0.5")]
        [SerializeField] private string m_Modifier = string.Empty;

        [SerializeField] private BuffTrigger m_Trigger = BuffTrigger.None;

        [Tooltip("触发时执行的效果串，复用卡牌效果的写法，如 damage:3,random")]
        [SerializeField] private string m_TriggerEffect = string.Empty;

        private List<BuffModifier> m_Parsed;

        public int Id => m_Id;
        public string Name => m_Name;
        public string DisplayName => m_DisplayName;
        public string Description => m_Description;
        public string Icon => m_Icon;
        public BuffStackRule StackRule => m_StackRule;
        public BuffConsume Consume => m_Consume;
        public int MaxStacks => m_MaxStacks;
        public BuffTrigger Trigger => m_Trigger;
        public string TriggerEffect => m_TriggerEffect;

        /// <summary>解析后的被动修饰器列表（惰性解析并缓存）。</summary>
        public IReadOnlyList<BuffModifier> Modifiers
        {
            get
            {
                if (m_Parsed == null)
                {
                    m_Parsed = ParseModifiers(m_Modifier);
                }

                return m_Parsed;
            }
        }

        private static List<BuffModifier> ParseModifiers(string spec)
        {
            var result = new List<BuffModifier>();
            if (string.IsNullOrWhiteSpace(spec))
            {
                return result;
            }

            foreach (string token in spec.Split(';'))
            {
                string entry = token.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                int colon = entry.IndexOf(':');
                if (colon <= 0 || colon + 1 >= entry.Length)
                {
                    Debug.LogWarning($"[buff] 修饰器写法不对：{entry}");
                    continue;
                }

                string stat = entry.Substring(0, colon).Trim();
                string rest = entry.Substring(colon + 1).Trim();
                char op = rest[0];
                if (op != '+' && op != '-' && op != '*')
                {
                    Debug.LogWarning($"[buff] 不支持的运算符：{entry}");
                    continue;
                }

                string number = rest.Substring(1).Trim();
                if (!float.TryParse(number, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float value))
                {
                    Debug.LogWarning($"[buff] 数值解析失败：{entry}");
                    continue;
                }

                result.Add(new BuffModifier(stat, op, value));
            }

            return result;
        }
    }
}
