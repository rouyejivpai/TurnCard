using System;
using System.Collections.Generic;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.Game.Battle
{
    /// <summary>一个生效中的 buff：定义 + 强度层数 + 剩余回合。</summary>
    public sealed class BuffInstance
    {
        public BuffInstance(BuffDefinition definition, int stacks, int turns)
        {
            Definition = definition;
            Stacks = Mathf.Max(1, stacks);
            Remaining = turns;
        }

        public BuffDefinition Definition { get; }

        /// <summary>强度层数：修饰器的数值会乘以它。</summary>
        public int Stacks { get; set; }

        /// <summary>剩余回合数（Turn 消耗用）；-1 表示无限。</summary>
        public int Remaining { get; set; }

        /// <summary>
        /// 形态能力：来自敌人的 frontBuffs/backBuffs，只在对应那一面朝上时存在。
        /// 翻面时会被清掉并换成新面的能力，而战斗中获得的普通 buff（虚弱/易伤/力量…）会保留。
        /// </summary>
        public bool FormBound { get; set; }

        /// <summary>
        /// 这一层是刚挂上的：第一次衰减要跳过，否则「回合开始掉层」会把本回合刚施加的
        /// 虚弱/易伤立刻抹掉（开幕效果、出牌施加的 debuff 都会踩到）。
        /// </summary>
        public bool Fresh { get; set; }

        /// <summary>触发计数（受击次数这类条件用它累计）。</summary>
        public int Counter { get; set; }

        public bool IsExpired => Stacks <= 0 ||
                                 (Definition.Consume == BuffConsume.Turn && Remaining == 0);
    }

    /// <summary>
    /// 一个单位（玩家或敌人）身上的 buff 集合。
    ///
    /// 只负责「存、叠、扣、算」；什么时候该扣、该在哪个结算点询问，由
    /// <see cref="BattleState"/> 在对应钩子里调用。
    /// </summary>
    public sealed class BuffContainer
    {
        private readonly List<BuffInstance> m_Buffs = new List<BuffInstance>();

        /// <summary>内容有变化（增加、叠加、移除、扣层）。UI 订阅它刷新图标。</summary>
        public event Action<BuffContainer> Changed;

        /// <summary>当前生效的 buff（外部只读，别直接改）。</summary>
        public IReadOnlyList<BuffInstance> Buffs => m_Buffs;

        public void Add(BuffDefinition definition, int stacks, int turns)
        {
            if (definition == null)
            {
                return;
            }

            BuffInstance existing = Find(definition.Id);
            if (existing == null)
            {
                // 新建时也要受最大层数限制
                int limit = definition.MaxStacks;
                int initial = limit > 0 ? Mathf.Min(Mathf.Max(1, stacks), limit) : Mathf.Max(1, stacks);
                m_Buffs.Add(new BuffInstance(definition, initial, turns) { Fresh = true });
                Raise();
                return;
            }

            switch (definition.StackRule)
            {
                case BuffStackRule.Stack:
                {
                    int max = definition.MaxStacks;
                    int next = existing.Stacks + Mathf.Max(1, stacks);
                    existing.Stacks = max > 0 ? Mathf.Min(next, max) : next;
                    break;
                }

                case BuffStackRule.Max:
                    existing.Stacks = Mathf.Max(existing.Stacks, stacks);
                    break;

                case BuffStackRule.Refresh:
                case BuffStackRule.None:
                    break;
            }

            if (turns >= 0)
            {
                existing.Remaining = turns;
            }

            existing.Fresh = true;
            Raise();
        }

        public BuffInstance Find(int id)
        {
            foreach (BuffInstance buff in m_Buffs)
            {
                if (buff.Definition != null && buff.Definition.Id == id)
                {
                    return buff;
                }
            }

            return null;
        }

        public bool Has(int id)
        {
            return Find(id) != null;
        }

        public bool Remove(int id)
        {
            for (int i = 0; i < m_Buffs.Count; i++)
            {
                if (m_Buffs[i].Definition != null && m_Buffs[i].Definition.Id == id)
                {
                    m_Buffs.RemoveAt(i);
                    Raise();
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            if (m_Buffs.Count == 0)
            {
                return;
            }

            m_Buffs.Clear();
            Raise();
        }

        /// <summary>移除所有「形态能力」（翻面时用），普通 buff 保留。</summary>
        public void RemoveFormBound()
        {
            bool dirty = false;
            for (int i = m_Buffs.Count - 1; i >= 0; i--)
            {
                if (m_Buffs[i] != null && m_Buffs[i].FormBound)
                {
                    m_Buffs.RemoveAt(i);
                    dirty = true;
                }
            }

            if (dirty)
            {
                Raise();
            }
        }

        /// <summary>收集某个触发时机的 buff。</summary>
        public List<BuffInstance> WithTrigger(BuffTrigger trigger)
        {
            var result = new List<BuffInstance>();
            foreach (BuffInstance buff in m_Buffs)
            {
                if (buff.Definition != null && buff.Definition.Trigger == trigger)
                {
                    result.Add(buff);
                }
            }

            return result;
        }

        /// <summary>
        /// 叠加式数值：所有 <c>+x</c> / <c>-x</c> 修饰器之和（乘上各自层数）。
        /// </summary>
        public float Flat(string stat)
        {
            float sum = 0f;
            foreach (BuffInstance buff in m_Buffs)
            {
                foreach (BuffModifier modifier in buff.Definition.Modifiers)
                {
                    if (!string.Equals(modifier.Stat, stat, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (modifier.Op == '+')
                    {
                        sum += modifier.Value * buff.Stacks;
                    }
                    else if (modifier.Op == '-')
                    {
                        sum -= modifier.Value * buff.Stacks;
                    }
                }
            }

            return sum;
        }

        /// <summary>
        /// 乘算式数值：<c>*x</c> 修饰器按层数线性叠加成 1 + (x-1)*层数，再累乘。
        /// </summary>
        public float Factor(string stat)
        {
            float factor = 1f;
            foreach (BuffInstance buff in m_Buffs)
            {
                foreach (BuffModifier modifier in buff.Definition.Modifiers)
                {
                    if (!string.Equals(modifier.Stat, stat, StringComparison.OrdinalIgnoreCase) ||
                        modifier.Op != '*')
                    {
                        continue;
                    }

                    factor *= 1f + (modifier.Value - 1f) * buff.Stacks;
                }
            }

            return factor;
        }

        /// <summary>是否有 buff 拦住了某个规则（如 armor_clear）。</summary>
        public bool BlocksRule(string rule)
        {
            foreach (BuffInstance buff in m_Buffs)
            {
                if (buff.Definition != null && buff.Definition.BlocksRule(rule))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 累计一次触发计数，返回是否达到阈值。
        /// 阈值取 buff 自己的 TriggerCount（0/1 表示每次都算达到）。
        /// </summary>
        public bool TickCounter(BuffInstance buff)
        {
            if (buff == null)
            {
                return false;
            }

            buff.Counter++;
            int need = buff.Definition.TriggerCount;
            if (need <= 1)
            {
                return true;
            }

            return buff.Counter >= need;
        }

        /// <summary>重置计数（触发后清零，方便下一轮再累计）。</summary>
        public void ResetCounter(BuffInstance buff)
        {
            if (buff != null)
            {
                buff.Counter = 0;
            }
        }

        /// <summary>
        /// 按触发时机衰减：数据里配了 <c>decayTrigger</c> 的 buff 在该时机掉层，掉光即移除。
        /// 这是通用行为（虚弱/易伤按回合掉层、以后要「回合结束掉层」也只需改表），
        /// 所以不写在回合流程里，而是由触发时机统一驱动。
        /// </summary>
        public void DecayByTrigger(Data.BuffTrigger trigger)
        {
            if (trigger == Data.BuffTrigger.None)
            {
                return;
            }

            bool dirty = false;
            for (int i = m_Buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = m_Buffs[i];
                if (buff == null || buff.Definition == null)
                {
                    continue;
                }

                if (buff.Definition.DecayTrigger != trigger)
                {
                    continue;
                }

                // 刚挂上的那层先跳过一次衰减（用了豁免就清掉标记，下次正常掉）
                if (buff.Fresh)
                {
                    buff.Fresh = false;
                    continue;
                }

                buff.Stacks -= buff.Definition.DecayStacks;
                dirty = true;

                if (buff.Stacks <= 0)
                {
                    m_Buffs.RemoveAt(i);
                }
            }

            if (dirty)
            {
                Raise();
            }
        }

        /// <summary>
        /// 扣掉「按次数消耗」且参与了某类结算的层数。
        /// 只有声明了对应 stat 修饰器的 buff 才算参与。
        /// </summary>
        public void ConsumeByStat(string stat, int amount = 1)
        {
            bool dirty = false;
            for (int i = m_Buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = m_Buffs[i];
                if (buff.Definition == null || buff.Definition.Consume != BuffConsume.Count)
                {
                    continue;
                }

                bool participates = false;
                foreach (BuffModifier modifier in buff.Definition.Modifiers)
                {
                    if (string.Equals(modifier.Stat, stat, StringComparison.OrdinalIgnoreCase))
                    {
                        participates = true;
                        break;
                    }
                }

                if (!participates)
                {
                    continue;
                }

                buff.Stacks -= amount;
                dirty = true;
                if (buff.Stacks <= 0)
                {
                    m_Buffs.RemoveAt(i);
                }
            }

            if (dirty)
            {
                Raise();
            }
        }

        /// <summary>扣掉指定 buff 的层数（触发型 buff 用）。</summary>
        public void Consume(BuffInstance buff, int amount = 1)
        {
            if (buff == null || buff.Definition == null)
            {
                return;
            }

            buff.Stacks -= amount;
            if (buff.Stacks <= 0)
            {
                m_Buffs.Remove(buff);
            }

            Raise();
        }

        /// <summary>单位死亡或战斗结束时清空。</summary>
        public void Raise()
        {
            Changed?.Invoke(this);
        }
    }
}
