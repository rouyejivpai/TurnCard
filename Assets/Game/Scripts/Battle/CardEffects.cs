using System;
using System.Collections.Generic;
using System.Reflection;
using TurnCard.Game.Data;

namespace TurnCard.Game.Battle
{
    /// <summary>执行效果时能拿到的一切上下文。</summary>
    public sealed class CardEffectContext
    {
        public BattleState State;
        public CardInstance Card;
        public EnemyInstance Target;
        public CardFace Face;

        /// <summary>本次结算中发生翻面的敌人（供表现层播翻转动画）。</summary>
        public EnemyInstance FlippedEnemy;

        /// <summary>触发这次效果的敌人自己（buff 触发型效果用，如「受击获得力量」）。</summary>
        public EnemyInstance SelfEnemy;

        /// <summary>
        /// 效果执行到「需要玩家选牌」时置位：后面的效果必须先停下，
        /// 等玩家选完（BattleState.CompletePlay）再继续，否则会抢跑。
        /// </summary>
        public bool Suspended;
    }

    /// <summary>一条效果的实现。</summary>
    public interface ICardEffect
    {
        void Apply(CardEffectContext context, string[] args);
    }

    /// <summary>
    /// 标记一个类是一张「特殊卡」的效果实现。
    /// 表里写 <c>special:key</c> 就会调用带同名 Key 的实现。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SpecialCardAttribute : Attribute
    {
        public SpecialCardAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }

    /// <summary>
    /// 卡牌效果执行器。
    ///
    /// 效果串写在配置表的两面效果列里，形如：
    /// <code>damage:6;armor:3;draw:2;special:blood_pact</code>
    ///
    /// 通用效果（伤害、护甲、抽牌、治疗、能量）由内置实现处理，
    /// 需要特殊逻辑的卡则用 <c>special:key</c> 指向一个带
    /// <see cref="SpecialCardAttribute"/> 的类，逻辑写在那个类里。
    /// </summary>
    public static class CardEffects
    {
        private static readonly Dictionary<string, ICardEffect> Builtins =
            new Dictionary<string, ICardEffect>(StringComparer.OrdinalIgnoreCase)
            {
                { "damage", new DamageEffect() },
                { "armor", new ArmorEffect() },
                { "draw", new DrawEffect() },
                { "heal", new HealEffect() },
                { "energy", new EnergyEffect() },
                { "losehp", new LoseHpEffect() },
                { "flip_adjacent", new FlipAdjacentEffect() },
                { "flip_all_others", new FlipAllOthersEffect() },
                { "discard_all", new DiscardAllEffect() },
                { "flip_left_discard_right", new FlipLeftDiscardRightEffect() },
                { "flip_right_discard_left", new FlipRightDiscardLeftEffect() },
                { "damage_per_attack", new DamagePerAttackCardsEffect() },
                { "armor_per_skill", new ArmorPerSkillCardsEffect() },
                { "choose_flip", new ChooseFlipEffect() },
                { "choose_retain_status", new ChooseRetainEffect() },
                { "pick_draw_pile", new PickFromDrawPileEffect() },
                { "pick_discard_pile", new PickFromDiscardPileEffect() },
                { "damage_times", new DamageTimesEffect() },
                { "armor_times", new ArmorTimesEffect() },
                { "flip_status", new FlipStatusEffect() },
                { "temp_strength", new TempStrengthEffect() },
                { "armor_per_status", new ArmorPerStatusEffect() },
                { "damage_times_adjacent_status", new DamageTimesAdjacentStatusEffect() },
                { "discard_adjacent", new DiscardAdjacentEffect() },
                { "play_all_status_front", new PlayAllStatusFrontEffect() },
                { "play_discard_all_status", new PlayDiscardAllStatusEffect() },
                { "losehp_per_status", new LoseHpPerStatusEffect() },
                { "exhaust_self", new ExhaustSelfEffect() },
                { "discard_status", new DiscardStatusEffect() },
                { "buff", new BuffEffect() },
                { "buff_self", new BuffSelfEffect() },
                { "lose_armor", new LoseArmorEffect() },
                { "damage_plus_instants", new DamagePlusInstantsEffect() },
                { "use_adjacent_back", new UseAdjacentEffect(false, true) },
                { "use_adjacent_front", new UseAdjacentEffect(true, false) },
                { "retain_self", new RetainSelfEffect() },
                { "flip_self", new FlipSelfEffect() },
                { "flip_self_front", new FlipSelfToFrontEffect() },
                { "self_damage", new SelfDamageEffect() },
                { "self_heal", new SelfHealEffect() },
                { "next_hits", new NextHitsEffect() },
                { "flip_pair", new FlipPairEffect() },
            };

        private static Dictionary<string, ICardEffect> s_Specials;

        /// <summary>解析并依次执行一面的全部效果。</summary>
        public static void ExecuteAll(CardEffectContext context)
        {
            string spec = context.Face.Effects;
            if (string.IsNullOrWhiteSpace(spec))
            {
                // 兼容旧表：没有效果列时，按伤害 / 护甲两个数值走默认行为
                if (context.Face.Damage > 0)
                {
                    Builtins["damage"].Apply(context, new[] { context.Face.Damage.ToString() });
                }

                if (context.Face.Armor > 0)
                {
                    Builtins["armor"].Apply(context, new[] { context.Face.Armor.ToString() });
                }

                return;
            }

            foreach (string token in spec.Split(';'))
            {
                if (context.Suspended)
                {
                    break;
                }

                string entry = token.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                string key = entry;
                string[] args = Array.Empty<string>();

                // {X} / {x} 替换成当前出牌结算里的 X 值（X 费、手牌数、状态牌数…）
                entry = entry.Replace('{', ' ').Replace('}', ' ');
                entry = entry.Replace(" X ", " " + context.State.CurrentX + " ");
                entry = entry.Replace(" x ", " " + context.State.CurrentX + " ");

                int colon = entry.IndexOf(':');
                if (colon >= 0)
                {
                    key = entry.Substring(0, colon).Trim();
                    args = entry.Substring(colon + 1)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < args.Length; i++)
                    {
                        args[i] = args[i].Trim();
                    }
                }

                if (string.Equals(key, "special", StringComparison.OrdinalIgnoreCase))
                {
                    // special 的参数是特殊卡的 key
                    if (args.Length > 0 && TryGetSpecial(args[0], out ICardEffect special))
                    {
                        special.Apply(context, SubArgs(args));
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"[卡牌效果] 找不到特殊卡实现：{(args.Length > 0 ? args[0] : "(空)")}");
                    }

                    continue;
                }

                if (Builtins.TryGetValue(key, out ICardEffect effect))
                {
                    effect.Apply(context, args);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[卡牌效果] 未知效果：{key}（效果串：{spec}）");
                }
            }
        }

        /// <summary>
        /// 效果能给界面提供「显示用数值」（例如「每瞬发 +4」的实时值、协同攻击的总伤害）。
        /// 只做计算，不改战斗状态。
        /// </summary>
        public interface ICardEffectPreview
        {
            void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens);
        }

        /// <summary>
        /// 收集这一面所有效果能提供的预览数值。
        /// key 会和卡面描述里的 {token} 对应。
        /// </summary>
        public static void CollectPreviewTokens(CardFace face, BattleState state,
            EnemyInstance target, IDictionary<string, int> tokens)
        {
            if (state == null || tokens == null)
            {
                return;
            }

            CollectTokensFromSpec(face, face.Effects, state, target, tokens);
        }

        /// <summary>
        /// 按一段效果串收预览 token。
        /// 能力牌的数值常常写在 buff 的 triggerEffect 里（佯攻、损伤、计谋），
        /// 所以遇到 <c>buff:id,...</c> 时会把那个 buff 的 triggerEffect 也解析一遍。
        /// </summary>
        private static void CollectTokensFromSpec(CardFace face, string spec, BattleState state,
            EnemyInstance target, IDictionary<string, int> tokens)
        {
            if (state == null || tokens == null || string.IsNullOrWhiteSpace(spec))
            {
                return;
            }

            var context = new CardEffectContext
            {
                State = state,
                Card = null,
                Target = target,
                Face = face,
            };

            // 预览时把 {X} 也换成真实值，否则「5×X」这类效果算不出总量
            int xValue = state.EvaluateX(face);

            foreach (string token in spec.Split(';'))
            {
                string entry = token.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                entry = entry.Replace("{X}", xValue.ToString()).Replace("{x}", xValue.ToString())
                    .Replace("{X }", xValue.ToString()).Replace("{ X}", xValue.ToString());

                string key = entry;
                string[] args = Array.Empty<string>();

                int colon = entry.IndexOf(':');
                if (colon >= 0)
                {
                    key = entry.Substring(0, colon).Trim();
                    args = entry.Substring(colon + 1)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < args.Length; i++)
                    {
                        args[i] = args[i].Trim();
                    }
                }

                if (!Builtins.TryGetValue(key, out ICardEffect effect))
                {
                    continue;
                }

                if (effect is ICardEffectPreview preview)
                {
                    preview.CollectPreview(context, args, tokens);
                }

                // 能力牌的数值常在 buff 的触发效果里：buff:35,1,-1,player → 解析 buff35.TriggerEffect
                if (string.Equals(key, "buff", StringComparison.OrdinalIgnoreCase) && args.Length > 0 &&
                    int.TryParse(args[0], out int buffId))
                {
                    BuffDefinition definition = state.FindBuff(buffId);
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.TriggerEffect))
                    {
                        CollectTokensFromSpec(face, definition.TriggerEffect, state, target, tokens);
                    }
                }
            }
        }

        /// <summary>
        /// 力破万法：造成 base 点伤害 normalTimes 次；若这张牌左右相邻有「状态牌」则改成 statusTimes 次。
        /// </summary>
        private sealed class DamageTimesAdjacentStatusEffect : ICardEffect, ICardEffectPreview
        {
            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int baseDamage = Arg(args, 0, 0);
                int normalTimes = args.Length > 1 ? Arg(args, 1, 1) : 1;
                int boostedTimes = args.Length > 2 ? Arg(args, 2, normalTimes) : normalTimes;

                // 预览拿不到手牌位置时就按基础次数算
                if (baseDamage > 0 && normalTimes > 0)
                {
                    tokens["total_damage"] = baseDamage * normalTimes;
                    tokens["total_damage_max"] = baseDamage * boostedTimes;
                }
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                int baseDamage = Arg(args, 0, 0);
                int normalTimes = args.Length > 1 ? Arg(args, 1, 1) : 1;
                int boostedTimes = args.Length > 2 ? Arg(args, 2, normalTimes) : normalTimes;

                int times = HasAdjacentStatus(context.State, context.Card) ? boostedTimes : normalTimes;
                for (int i = 0; i < times; i++)
                {
                    Builtins["damage"].Apply(context, new[] { baseDamage.ToString() });
                }
            }

            private static bool HasAdjacentStatus(BattleState state, CardInstance card)
            {
                IReadOnlyList<CardInstance> hand = state.Hand;
                int index = IndexOf(hand, card);
                if (index < 0)
                {
                    return false;
                }

                if (index > 0 && hand[index - 1] != null && hand[index - 1].Face.Type == CardType.Status)
                {
                    return true;
                }

                return index + 1 < hand.Count && hand[index + 1] != null &&
                       hand[index + 1].Face.Type == CardType.Status;
            }
        }

        /// <summary>撕毁：丢弃左右相邻的牌；其中是「状态牌」的改为消耗。</summary>
        private sealed class DiscardAdjacentEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                IReadOnlyList<CardInstance> hand = context.State.Hand;
                int index = IndexOf(hand, context.Card);
                if (index < 0)
                {
                    return;
                }

                var targets = new List<CardInstance>();
                if (index > 0) targets.Add(hand[index - 1]);
                if (index + 1 < hand.Count) targets.Add(hand[index + 1]);

                foreach (CardInstance card in targets)
                {
                    if (card == null)
                    {
                        continue;
                    }

                    if (card.Face.Type == CardType.Status)
                    {
                        context.State.ExhaustCard(card);
                    }
                    else
                    {
                        context.State.DiscardCard(card);
                    }
                }
            }
        }

        /// <summary>
        /// 放逐：登记手牌里的状态牌，之后由表现层逐张推进
        /// （翻面动画 → 执行正面），全部处理完再继续。
        /// </summary>
        private sealed class PlayAllStatusFrontEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.BeginStatusExile(false);
            }
        }

        /// <summary>重整旗鼓：登记手牌里的状态牌，逐张翻面 → 打出正面 → 丢弃。</summary>
        private sealed class PlayDiscardAllStatusEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.BeginStatusExile(true);
            }
        }

        /// <summary>获得「每张手牌状态牌 × mult」点护甲（佯攻）。</summary>
        private sealed class ArmorPerStatusEffect : ICardEffect, ICardEffectPreview
        {
            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int mult = Arg(args, 0, 1);
                int count = context.State == null ? 0 : context.State.CountHandOfType(CardType.Status);
                if (count > 0 && mult > 0)
                {
                    tokens["total_armor"] = count * mult;
                }
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                int mult = Arg(args, 0, 1);
                int count = context.State.CountHandOfType(CardType.Status);
                if (count > 0 && mult > 0)
                {
                    Builtins["armor"].Apply(context, new[] { (count * mult).ToString() });
                }
            }
        }

        /// <summary>受到「每张手牌状态牌 × mult」点伤害（损伤）。</summary>
        private sealed class LoseHpPerStatusEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int mult = Arg(args, 0, 1);
                int count = context.State.CountHandOfType(CardType.Status);
                if (count > 0 && mult > 0)
                {
                    Builtins["losehp"].Apply(context, new[] { (count * mult).ToString() });
                }
            }
        }

        /// <summary>造成 a 点伤害，重复 count 次（每次独立入队，表现层逐段播）。</summary>
        private sealed class DamageTimesEffect : ICardEffect, ICardEffectPreview
        {
            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int amount = Arg(args, 0, 0);
                int times = args.Length > 1 ? Arg(args, 1, 1) : 1;
                if (amount > 0 && times > 0)
                {
                    tokens["total_damage"] = amount * times;
                }
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                int amount = Arg(args, 0, 0);
                int times = Arg(args, 1, 1);
                for (int i = 0; i < times; i++)
                {
                    Builtins["damage"].Apply(context, new[] { amount.ToString() });
                }
            }
        }

        /// <summary>获得 a 点护甲，重复 count 次。</summary>
        private sealed class ArmorTimesEffect : ICardEffect, ICardEffectPreview
        {
            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int amount = Arg(args, 0, 0);
                int times = args.Length > 1 ? Arg(args, 1, 1) : 1;
                if (amount > 0 && times > 0)
                {
                    tokens["total_armor"] = amount * times;
                }
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                int amount = Arg(args, 0, 0);
                int times = Arg(args, 1, 1);
                for (int i = 0; i < times; i++)
                {
                    Builtins["armor"].Apply(context, new[] { amount.ToString() });
                }
            }
        }

        /// <summary>翻面手牌中最前面 count 张「状态牌」。</summary>
        private sealed class FlipStatusEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int count = Math.Max(1, Arg(args, 0, 1));
                int flipped = 0;
                foreach (CardInstance card in context.State.Hand)
                {
                    if (flipped >= count)
                    {
                        break;
                    }

                    if (card != null && card.Face.Type == CardType.Status)
                    {
                        context.State.RequestFlip(card, BattleState.FlipSource.Effect);
                        flipped++;
                    }
                }
            }
        }

        /// <summary>获得临时力量（回合结束消失）。</summary>
        private sealed class TempStrengthEffect : ICardEffect, ICardEffectPreview
        {
            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int stacks = args.Length > 0 ? Arg(args, 0, 1) : 1;
                if (stacks > 0)
                {
                    tokens["total_strength"] = stacks;
                }
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                int stacks = Arg(args, 0, 1);
                if (stacks <= 0)
                {
                    return;
                }

                // 临时力量的 buff id 在表里固定为 32（buff.xlsx）
                context.State.ApplyBuff(context.State.PlayerBuffs, 32, stacks, -1);
            }
        }

        /// <summary>选一张手牌翻面（条件反射）。</summary>
        private sealed class ChooseFlipEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.State.IsForcedPlaying)
                {
                    return;
                }

                context.State.BeginChoice(ChoiceKind.FlipHandCard);
                context.Suspended = true;
            }
        }

        /// <summary>选一张手牌（状态牌）保留到被打出为止（深吸 / 剑盾）。</summary>
        private sealed class ChooseRetainEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                bool hasStatus = false;
                foreach (CardInstance card in context.State.Hand)
                {
                    if (card != null && card.Face.Type == CardType.Status &&
                        !ReferenceEquals(card, context.Card))
                    {
                        hasStatus = true;
                        break;
                    }
                }

                if (!hasStatus || context.State.IsForcedPlaying)
                {
                    return;
                }

                context.State.BeginChoice(ChoiceKind.RetainHandCard);
                context.Suspended = true;
            }
        }

        /// <summary>从抽牌堆选一张置入手牌（协议）。</summary>
        private sealed class PickFromDrawPileEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.State.DrawPile.Count == 0 || context.State.IsForcedPlaying)
                {
                    return;
                }

                context.State.BeginChoice(ChoiceKind.PickFromDrawPile);
                context.Suspended = true;
            }
        }

        /// <summary>从弃牌堆选一张置入手牌（追悔）。</summary>
        private sealed class PickFromDiscardPileEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.State.DiscardPile.Count == 0 || context.State.IsForcedPlaying)
                {
                    return;
                }

                context.State.BeginChoice(ChoiceKind.PickFromDiscardPile);
                context.Suspended = true;
            }
        }

        /// <summary>把打出的这张牌放进消耗堆（本场不再回来）。</summary>
        private sealed class ExhaustSelfEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.Card != null)
                {
                    context.State.ExhaustCard(context.Card);
                }
            }
        }

        /// <summary>丢弃手牌中所有「状态牌」，并按丢弃数量抽牌。</summary>
        private sealed class DiscardStatusEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                var statusCards = new List<CardInstance>();
                foreach (CardInstance card in context.State.Hand)
                {
                    if (card != null && card.Face.Type == CardType.Status)
                    {
                        statusCards.Add(card);
                    }
                }

                int count = statusCards.Count;
                foreach (CardInstance card in statusCards)
                {
                    context.State.DiscardCard(card);
                }

                if (count > 0)
                {
                    context.State.DrawCards(count);
                }
            }
        }

        /// <summary>已注册的通用效果名（供编辑器校验使用）。</summary>
        public static IEnumerable<string> KnownEffectKeys()
        {
            return Builtins.Keys;
        }

        /// <summary>特殊卡的 key 集合（供编辑器校验使用）。</summary>
        public static IEnumerable<string> KnownSpecialKeys()
        {
            if (s_Specials == null)
            {
                BuildSpecialIndex();
            }

            return s_Specials.Keys;
        }

        /// <summary>判断效果串里的 key 是否存在。</summary>
        public static bool IsKnownKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (string.Equals(key, "special", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Builtins.ContainsKey(key);
        }

        private static string[] SubArgs(string[] args)
        {
            if (args.Length <= 1)
            {
                return Array.Empty<string>();
            }

            var rest = new string[args.Length - 1];
            Array.Copy(args, 1, rest, 0, rest.Length);
            return rest;
        }

        private static bool TryGetSpecial(string key, out ICardEffect effect)
        {
            if (s_Specials == null)
            {
                BuildSpecialIndex();
            }

            return s_Specials.TryGetValue(key, out effect);
        }

        /// <summary>扫描所有带 SpecialCard 特性的类，建立 key → 实例 的索引。</summary>
        private static void BuildSpecialIndex()
        {
            s_Specials = new Dictionary<string, ICardEffect>(StringComparer.OrdinalIgnoreCase);

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
                    if (type == null || type.IsAbstract || !typeof(ICardEffect).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var attribute = type.GetCustomAttribute<SpecialCardAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    if (s_Specials.ContainsKey(attribute.Key))
                    {
                        UnityEngine.Debug.LogWarning($"[卡牌效果] 特殊卡 key 重复：{attribute.Key}（{type.Name}）");
                        continue;
                    }

                    s_Specials[attribute.Key] = (ICardEffect)Activator.CreateInstance(type);
                }
            }
        }

        private static int Arg(string[] args, int index, int fallback = 0)
        {
            if (index >= args.Length)
            {
                return fallback;
            }

            return int.TryParse(args[index], out int value) ? value : fallback;
        }

        private sealed class DamageEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int amount = Arg(args, 0);
                string mode = args.Length > 1 ? args[1] : string.Empty;

                if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (EnemyInstance enemy in context.State.Enemies)
                    {
                        if (context.State.DamageEnemy(enemy, amount))
                        {
                            context.FlippedEnemy = enemy;
                        }
                    }

                    return;
                }

                if (string.Equals(mode, "random", StringComparison.OrdinalIgnoreCase))
                {
                    EnemyInstance pick = PickRandomAlive(context.State);
                    if (pick != null && context.State.DamageEnemy(pick, amount))
                    {
                        context.FlippedEnemy = pick;
                    }

                    return;
                }

                // 没有目标（自动使用场景）就随机挑一个，避免效果静默不生效
                EnemyInstance target = context.Target;
                if (target == null || target.IsDead)
                {
                    target = PickRandomAlive(context.State);
                }

                if (target != null && context.State.DamageEnemy(target, amount))
                {
                    context.FlippedEnemy = target;
                }
            }

            private static EnemyInstance PickRandomAlive(BattleState state)
            {
                int alive = 0;
                foreach (EnemyInstance enemy in state.Enemies)
                {
                    if (!enemy.IsDead)
                    {
                        alive++;
                    }
                }

                if (alive == 0)
                {
                    return null;
                }

                int index = UnityEngine.Random.Range(0, alive);
                foreach (EnemyInstance enemy in state.Enemies)
                {
                    if (enemy.IsDead)
                    {
                        continue;
                    }

                    if (index-- == 0)
                    {
                        return enemy;
                    }
                }

                return null;
            }
        }

        private sealed class ArmorEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.GainArmor(Arg(args, 0));
            }
        }

        private sealed class DrawEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.DrawCards(Arg(args, 0, 1));
            }
        }

        private sealed class HealEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.HealPlayer(Arg(args, 0, 1));
            }
        }

        private sealed class EnergyEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.GainEnergy(Arg(args, 0, 1));
            }
        }

        private sealed class LoseHpEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.LosePlayerHp(Arg(args, 0, 1));
            }
        }

        /// <summary>翻面手牌里紧挨着这张牌的左右两张。</summary>
        private sealed class FlipAdjacentEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                IReadOnlyList<CardInstance> hand = context.State.Hand;
                int index = IndexOf(hand, context.Card);
                if (index < 0)
                {
                    return;
                }

                if (index > 0)
                {
                    context.State.RequestFlip(hand[index - 1], BattleState.FlipSource.Effect);
                }

                if (index + 1 < hand.Count)
                {
                    context.State.RequestFlip(hand[index + 1], BattleState.FlipSource.Effect);
                }
            }
        }

        /// <summary>翻面手牌里除自己以外的所有牌。</summary>
        private sealed class FlipAllOthersEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                foreach (CardInstance other in context.State.Hand)
                {
                    if (!ReferenceEquals(other, context.Card))
                    {
                        context.State.RequestFlip(other, BattleState.FlipSource.Effect);
                    }
                }
            }
        }

        /// <summary>丢弃整只手牌。</summary>
        private sealed class DiscardAllEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.DiscardHand();
            }
        }

        /// <summary>翻面左边相邻的一张，丢弃右边相邻的一张（缺哪边就跳过哪边）。</summary>
        private sealed class FlipLeftDiscardRightEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                IReadOnlyList<CardInstance> hand = context.State.Hand;
                int index = IndexOf(hand, context.Card);
                if (index < 0)
                {
                    return;
                }

                if (index > 0)
                {
                    context.State.RequestFlip(hand[index - 1], BattleState.FlipSource.Effect);
                }

                if (index + 1 < hand.Count)
                {
                    context.State.DiscardCard(hand[index + 1]);
                }
            }
        }

        /// <summary>翻面右边相邻的一张，丢弃左边相邻的一张（缺哪边就跳过哪边）。</summary>
        private sealed class FlipRightDiscardLeftEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                IReadOnlyList<CardInstance> hand = context.State.Hand;
                int index = IndexOf(hand, context.Card);
                if (index < 0)
                {
                    return;
                }

                if (index + 1 < hand.Count)
                {
                    context.State.RequestFlip(hand[index + 1], BattleState.FlipSource.Effect);
                }

                if (index > 0)
                {
                    context.State.DiscardCard(hand[index - 1]);
                }
            }
        }

        /// <summary>造成「手牌里的攻击牌数 × 参数」点伤害（统计包含自己）。</summary>
        private sealed class DamagePerAttackCardsEffect : ICardEffect, ICardEffectPreview
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int per = Arg(args, 0, 1);
                int count = context.State.CountHandOfType(CardType.Attack);
                int total = per * Math.Max(1, count);
                Builtins["damage"].Apply(context, new[] { total.ToString() });
            }

            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int per = Arg(args, 0, 1);
                int count = Math.Max(1, context.State.CountHandOfType(CardType.Attack));
                tokens["demage"] = per;
                tokens["total_damage"] = per * count;
            }
        }

        /// <summary>获得「手牌里的技能牌数 × 参数」点护甲（统计包含自己）。</summary>
        private sealed class ArmorPerSkillCardsEffect : ICardEffect, ICardEffectPreview
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int per = Arg(args, 0, 1);
                int count = context.State.CountHandOfType(CardType.Skill);
                int total = per * Math.Max(1, count);
                Builtins["armor"].Apply(context, new[] { total.ToString() });
            }

            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int per = Arg(args, 0, 1);
                int count = Math.Max(1, context.State.CountHandOfType(CardType.Skill));
                tokens["total_armor"] = per * count;
            }
        }

        /// <summary>
        /// 施加 buff：<c>buff:&lt;id&gt;,&lt;层数&gt;,&lt;回合&gt;,&lt;player|enemy&gt;</c>
        /// 不给目标时默认给对方（敌人），让卡牌能给敌人挂易伤/虚弱。
        /// </summary>
        private sealed class BuffEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int id = Arg(args, 0, 0);
                if (id <= 0)
                {
                    return;
                }

                int stacks = Arg(args, 1, 1);
                int turns = args.Length > 2 && int.TryParse(args[2], out int parsedTurns)
                    ? parsedTurns
                    : -1;
                string target = args.Length > 3 ? args[3].Trim() : "enemy";

                if (string.Equals(target, "player", System.StringComparison.OrdinalIgnoreCase))
                {
                    context.State.ApplyBuff(context.State.PlayerBuffs, id, stacks, turns);
                    return;
                }

                if (string.Equals(target, "all", System.StringComparison.OrdinalIgnoreCase))
                {
                    // 「对所有单位」= 玩家 + 所有活着的敌人
                    context.State.ApplyBuff(context.State.PlayerBuffs, id, stacks, turns);
                    foreach (EnemyInstance enemy in context.State.Enemies)
                    {
                        if (enemy != null && !enemy.IsDead)
                        {
                            context.State.ApplyBuff(enemy.Buffs, id, stacks, turns);
                        }
                    }

                    return;
                }

                if (context.Target != null)
                {
                    context.State.ApplyBuff(context.Target.Buffs, id, stacks, turns);
                }
            }
        }

        /// <summary>失去护甲，最多扣到 0。</summary>
        private sealed class LoseArmorEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int amount = Arg(args, 0, 1);
                context.State.LosePlayerArmor(amount);
            }
        }

        /// <summary>造成「基础值 + 本回合瞬发次数 × 每层加成」点伤害。</summary>
        private sealed class DamagePlusInstantsEffect : ICardEffect, ICardEffectPreview
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int baseValue = Arg(args, 0, 1);
                int perInstant = args.Length > 1 ? Arg(args, 1, 0) : 0;
                int total = baseValue + perInstant * context.State.InstantCountThisTurn;
                Builtins["damage"].Apply(context, new[] { total.ToString() });
            }

            public void CollectPreview(CardEffectContext context, string[] args,
                IDictionary<string, int> tokens)
            {
                int baseValue = Arg(args, 0, 1);
                int perInstant = args.Length > 1 ? Arg(args, 1, 0) : 0;
                // 描述里的 {demage} 就是「现在打出去会是多少」
                tokens["demage"] = baseValue + perInstant * context.State.InstantCountThisTurn;
            }
        }

        /// <summary>
        /// 使用左右相邻手牌指定面的效果。
        /// <c>useFront = true</c> 用正面，否则用背面；
        /// <c>discardAfter = true</c> 用完直接丢弃（不翻面）。
        /// </summary>
        private sealed class UseAdjacentEffect : ICardEffect
        {
            private readonly bool m_UseFront;
            private readonly bool m_DiscardAfter;

            public UseAdjacentEffect(bool useFront, bool discardAfter = false)
            {
                m_UseFront = useFront;
                m_DiscardAfter = discardAfter;
            }

            public void Apply(CardEffectContext context, string[] args)
            {
                // 被别的牌「使用」时不再连锁使用相邻牌：
                // 否则两张暗藏杀意相邻会互相使用对方，无限递归直接栈溢出。
                if (!context.State.CanUseAdjacent)
                {
                    return;
                }

                IReadOnlyList<CardInstance> hand = context.State.Hand;
                int index = IndexOf(hand, context.Card);
                if (index < 0)
                {
                    return;
                }

                // 先记住左右是谁：执行效果可能把邻牌挪走/丢弃，索引会变
                CardInstance left = index - 1 >= 0 && index - 1 < hand.Count ? hand[index - 1] : null;
                CardInstance right = index + 1 >= 0 && index + 1 < hand.Count ? hand[index + 1] : null;

                context.State.BeginAdjacentUse();
                try
                {
                    UseFaceAt(context, left);
                    UseFaceAt(context, right);
                }
                finally
                {
                    context.State.EndAdjacentUse();
                }
            }

            private void UseFaceAt(CardEffectContext context, CardInstance neighbour)
            {
                if (neighbour == null || ReferenceEquals(neighbour, context.Card))
                {
                    return;
                }

                // 已经被前面的效果弃掉 / 挪走就不再结算
                if (!context.State.IsInHand(neighbour))
                {
                    return;
                }

                CardFaceSide side = m_UseFront ? CardFaceSide.Front : CardFaceSide.Back;
                CardFace face = neighbour.Definition.GetFace(side);

                // 被自动使用的牌如果需要目标，就随机选一个敌人（设计规则）
                var nested = new CardEffectContext
                {
                    State = context.State,
                    Card = neighbour,
                    Target = context.State.ResolveAutoTarget(face, context.Target),
                    Face = face,
                };

                CardInstance previousCard = context.State.EnterCardContext(neighbour);
                try
                {
                    CardEffects.ExecuteAll(nested);
                }
                finally
                {
                    context.State.ExitCardContext(previousCard);
                }

                if (m_DiscardAfter)
                {
                    // 暗藏杀意：用完直接把邻牌丢进弃牌堆（不翻面）
                    context.State.DiscardCard(neighbour);
                    return;
                }

                // 图穷匕现：用完让它翻面
                context.State.RequestFlip(neighbour, BattleState.FlipSource.Effect);
            }
        }

        /// <summary>
        /// 翻到正面（已经是正面就不动）。
        /// 电光火石用这个：避免「双重瞬发」时来回翻面形成死循环。
        /// </summary>
        private sealed class FlipSelfToFrontEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.Card != null)
                {
                    // 指定目标面：双重瞬发连点两次也只会收敛到正面，不会来回翻
                    context.State.RequestFlipTo(context.Card, CardFaceSide.Front,
                        BattleState.FlipSource.Keyword);
                }
            }
        }

        /// <summary>本牌本回合保留，回合结束不进弃牌堆。</summary>
        private sealed class RetainSelfEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.Card != null)
                {
                    context.Card.RetainedThisTurn = true;
                }
            }
        }

        /// <summary>把自己翻面（电光火石：瞬发结算后翻回正面）。</summary>
        private sealed class FlipSelfEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.Card != null)
                {
                    context.State.RequestFlip(context.Card, BattleState.FlipSource.Keyword);
                }
            }
        }

        /// <summary>触发者自己失去生命：<c>self_damage:&lt;点数&gt;</c>（不经护甲，用于「疯狂」）。</summary>
        private sealed class SelfDamageEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.SelfEnemy == null)
                {
                    return;
                }

                context.State.ApplySelfDamage(context.SelfEnemy, Arg(args, 0, 1));
            }
        }

        /// <summary>触发者自己回血：<c>self_heal:&lt;点数&gt;</c>（用于「嗜血」「自愈」）。</summary>
        private sealed class SelfHealEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.SelfEnemy == null)
                {
                    return;
                }

                context.State.ApplySelfHeal(context.SelfEnemy, Arg(args, 0, 1));
            }
        }

        /// <summary>下一次攻击追加段数：<c>next_hits:&lt;段数&gt;</c>（用于「嗜血」）。</summary>
        private sealed class NextHitsEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                if (context.SelfEnemy == null)
                {
                    return;
                }

                context.SelfEnemy.BonusHits += Arg(args, 0, 1);
            }
        }

        /// <summary>翻面与自己同 pairId 的其他敌人（羁绊）。</summary>
        private sealed class FlipPairEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                context.State.FlipPairedAllies(context.SelfEnemy);
            }
        }

        /// <summary>给自己（触发者）挂 buff：<c>buff_self:&lt;id&gt;,&lt;层数&gt;,&lt;回合&gt;</c>。</summary>
        private sealed class BuffSelfEffect : ICardEffect
        {
            public void Apply(CardEffectContext context, string[] args)
            {
                int id = Arg(args, 0, 0);
                if (id <= 0)
                {
                    return;
                }

                int stacks = Arg(args, 1, 1);
                int turns = args.Length > 2 && int.TryParse(args[2], out int parsed) ? parsed : -1;

                if (context.SelfEnemy != null)
                {
                    context.State.ApplyBuff(context.SelfEnemy.Buffs, id, stacks, turns);
                }
            }
        }

        private static int IndexOf(IReadOnlyList<CardInstance> list, CardInstance item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
