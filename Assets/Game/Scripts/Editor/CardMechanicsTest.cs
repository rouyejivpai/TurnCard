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
