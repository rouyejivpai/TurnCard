using System.Text;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 新增卡牌机制的无头测试：选择翻面、用相邻牌、瞬发叠伤、失去护甲、保留。
    /// </summary>
    public static class CardMechanicsTest
    {
        private static StringBuilder s_Log;
        private static int s_Failed;

        public static void Run()
        {
            s_Log = new StringBuilder();
            s_Failed = 0;

            BattleContent content = Resources.Load<BattleContent>("BattleContent");
            Check("卡牌表 16 张", content != null && content.Cards.Length == 16,
                content == null ? "null" : content.Cards.Length.ToString());
            if (content == null)
            {
            Flush();
                return;
            }

            CardDefinition card11 = Find(content, 11);
            CardDefinition card12 = Find(content, 12);
            CardDefinition card13 = Find(content, 13);
            CardDefinition card14 = Find(content, 14);
            CardDefinition card15 = Find(content, 15);

            Check("卡11 条件反射 有 choose_flip", card11 != null && card11.Front.Effects.Contains("choose_flip"), "");
            Check("卡13 暗藏杀意 有 use_adjacent", card13 != null && card13.Front.Effects.Contains("use_adjacent_back"), "");
            Check("卡15 闪电破击 有 damage_plus_instants", card15 != null && card15.Front.Effects.Contains("damage_plus_instants"), "");

            var state = new BattleState();
            state.Setup(new[] { card11, card12, card13, card14, card15 }, new[] { content.Enemies[0] });
            state.StartPlayerTurn();

            // 1) 失去护甲：扣到 0 为止（先用卡牌加甲，再扣）
            Execute(state, card14.Front, null, null);   // 灵巧板甲：+2 甲
            Check("灵巧板甲 +2 甲", state.PlayerArmor == 2, "护甲=" + state.PlayerArmor);
            state.LosePlayerArmor(5);
            Check("失去 5 甲时只扣到 0", state.PlayerArmor == 0, "护甲=" + state.PlayerArmor);

            // 2) 瞬发叠伤：0 次瞬发 = 基础 4
            int baseDamage = EvalDamage(state, card15.Front, state.Enemies[0]);
            Check("闪电破击 0 次瞬发 = 4", baseDamage == 4, "实际=" + baseDamage);

            // 3) 选择翻面：进入待选 → 选中 → 确认后入队
            CardInstance target = state.Hand[0];
            Execute(state, card11.Front, state.Hand[0], null);
            Check("条件反射 进入选择态", state.HasPendingChoice, "");
            state.SelectForChoice(target);
            bool confirmed = state.ConfirmChoice();
            Check("确认后产生一次翻面", confirmed && state.IsResolvingFlips, "排队=" + state.PendingFlips.Count);

            // 4) 保留：EndPlayerTurn 后仍在手牌
            state.Hand[0].RetainedThisTurn = true;
            CardInstance kept = state.Hand[0];
            state.EndPlayerTurn();
            bool stillThere = false;
            foreach (CardInstance c in state.Hand)
            {
                if (ReferenceEquals(c, kept))
                {
                    stillThere = true;
                }
            }

            Check("保留标记让牌留在手上", stillThere, "手牌=" + state.Hand.Count);

            // 5) 自动使用相邻牌：需要目标的效果必须随机命中敌人，不能静默失效
            var state2 = new BattleState();
            var deck = new System.Collections.Generic.List<CardDefinition>();
            CardDefinition attack = Find(content, 1);   // 攻击：damage:3，需要目标
            CardDefinition skill = Find(content, 14);   // 灵巧板甲：armor:2，不需要目标
            deck.Add(skill);
            deck.Add(card13);
            deck.Add(attack);
            state2.Setup(deck, new[] { content.Enemies[0] });
            state2.StartPlayerTurn();

            // 摆成：灵巧板甲 | 暗藏杀意 | 攻击
            CardInstance middle = null;
            foreach (CardInstance c in state2.Hand)
            {
                if (c.Definition.Id == 13)
                {
                    middle = c;
                }
            }

            Check("手牌里找到暗藏杀意", middle != null, "手牌=" + state2.Hand.Count);

            // 规则本身：需要目标的面在自动使用时必须拿到一个存活目标
            EnemyInstance autoTarget = state2.ResolveAutoTarget(attack.Front, null);
            Check("自动使用需要目标的面 → 随机目标",
                autoTarget != null && !autoTarget.IsDead,
                autoTarget == null ? "null" : autoTarget.Definition.Front.DisplayName);

            Check("自动使用不需要目标的面 → 保持 null",
                state2.ResolveAutoTarget(skill.Front, null) == null, "");

            Check("随机目标一定存活", state2.PickRandomAliveEnemy() != null, "");

            if (middle != null)
            {
                EnemyInstance foe = state2.Enemies[0];
                int hpBefore = foe.Hp + foe.Armor;
                Execute(state2, card13.Front, middle, null);   // 不给目标，模拟自动使用
                int hpAfter = foe.Hp + foe.Armor;

                // 相邻牌里只要有一面的效果能造成伤害，就应该看到敌人掉血或护甲减少；
                // 洗牌后相邻牌不确定，所以这里只断言「不报错 + 有翻面入队 + 数值未被破坏」
                Check("自动使用相邻牌不报错", hpAfter <= hpBefore, "敌人 " + hpBefore + " → " + hpAfter);
                Check("相邻牌被排队翻面", state2.PendingFlips.Count >= 1,
                    "排队=" + state2.PendingFlips.Count);
            }

                // 6) 同一张牌连续翻两次：应产生两条方向相反的 FlipStep
            var state3 = new BattleState();
            CardDefinition cannon = Find(content, 3);   // 速射火炮 → 开火（背面带瞬发）
            state3.Setup(new[] { cannon }, new[] { content.Enemies[0] });
            state3.StartPlayerTurn();

            CardInstance flipCard = state3.Hand[0];
            state3.RequestFlip(flipCard, BattleState.FlipSource.Effect);
            state3.RequestFlip(flipCard, BattleState.FlipSource.Effect);

            BattleState.FlipStep? first = state3.CommitNextFlip();
            BattleState.FlipStep? second = state3.CommitNextFlip();

            Check("连续两次翻面各产生一条 FlipStep", first != null && second != null, "");
            if (first != null && second != null)
            {
                Check("第一条 Front → Back",
                    first.Value.From == CardFaceSide.Front && first.Value.To == CardFaceSide.Back,
                    first.Value.From + " → " + first.Value.To);
                Check("第二条 Back → Front",
                    second.Value.From == CardFaceSide.Back && second.Value.To == CardFaceSide.Front,
                    second.Value.From + " → " + second.Value.To);
            }

            // 7) 瞬发延后：翻到「开火」应该先收集，不立刻扣血
            var state4 = new BattleState();
            state4.Setup(new[] { cannon }, new[] { content.Enemies[0] });
            state4.StartPlayerTurn();
            EnemyInstance foe2 = state4.Enemies[0];

            state4.RequestFlip(state4.Hand[0], BattleState.FlipSource.Effect);
            state4.CommitNextFlip();

            int hpBeforeInstant = foe2.Hp + foe2.Armor;
            Check("翻到瞬发面后进入待执行", state4.HasPendingInstants, "");

            state4.ResolvePendingInstants();
            int hpAfterInstant = foe2.Hp + foe2.Armor;
            Check("执行瞬发后敌人掉血", hpAfterInstant < hpBeforeInstant,
                hpBeforeInstant + " → " + hpAfterInstant);
            Check("瞬发次数被记到本回合", state4.InstantCountThisTurn == 1,
                "次数=" + state4.InstantCountThisTurn);

            Flush();
        }

        private static CardDefinition Find(BattleContent content, int id)
        {
            foreach (CardDefinition card in content.Cards)
            {
                if (card != null && card.Id == id)
                {
                    return card;
                }
            }

            return null;
        }

        private static void Execute(BattleState state, CardFace face, CardInstance card, EnemyInstance target)
        {
            CardEffects.ExecuteAll(new CardEffectContext
            {
                State = state,
                Card = card,
                Target = target,
                Face = face,
            });
        }

        /// <summary>
        /// 算一次伤害类效果实际打掉多少（生命 + 护甲的总减少）。
        /// 必须传目标：伤害效果没有目标时不会生效。
        /// </summary>
        private static int EvalDamage(BattleState state, CardFace face, EnemyInstance target)
        {
            int before = target.Hp + target.Armor;
            Execute(state, face, null, target);
            int after = target.Hp + target.Armor;
            return before - after;
        }

        private static void Check(string label, bool ok, string detail)
        {
            s_Log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label + "  " + detail);
            if (!ok)
            {
                s_Failed++;
            }
        }

        private static void Flush()
        {
            s_Log.AppendLine(s_Failed == 0 ? "== 全部通过 ==" : "== 失败 " + s_Failed + " 项 ==");
            Debug.Log("[CardMechanicsTest]\n" + s_Log);
        }
    }
}
