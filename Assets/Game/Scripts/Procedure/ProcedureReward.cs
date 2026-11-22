using System.Collections.Generic;
using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Data;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using UnityEngine;

namespace TurnCard.Game.Procedure
{
    /// <summary>战斗奖励：金币 + 三张牌选一张（可跳过）。</summary>
    public sealed class ProcedureReward : FlowProcedureBase
    {
        private const int GoldReward = 25;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            // 首领是最后一个节点：打赢直接通关，不再发奖励
            if (Run.CurrentNode != null && Run.CurrentNode.Type == MapNodeType.Boss)
            {
                Run.MarkCleared();
                Run.MarkVictory();
                EventBus.Publish(new RunEndedEvent { Victory = true });
                ChangeState<ProcedureGameOver>(procedureOwner);
                return;
            }

            Run.MarkCleared();
            Run.AddGold(GoldReward);

            CardGridScreen screen = Open<CardGridScreen>(CardGridScreen.ResourcePath);
            screen.SetHeader("战斗胜利",
                $"获得 {GoldReward} 金币    当前金币 {Run.Gold}",
                "鼠标悬停卡牌可以查看背面，点击即拿走");

            List<CardDefinition> offer = PickThree();
            screen.ShowCards(offer, 1.25f, offer.Count,
                card => Choose(procedureOwner, card.Id));

            screen.AddButton("跳过", () => Choose(procedureOwner, 0));
        }

        private void Choose(IFsm<IProcedureManager> owner, int cardId)
        {
            if (cardId != 0)
            {
                Run.AddCard(cardId);
            }

            EventBus.Publish(new RewardChosenEvent { CardId = cardId });
            ChangeState<ProcedureMap>(owner);
        }

        private static List<CardDefinition> PickThree()
        {
            var result = new List<CardDefinition>();
            var content = Resources.Load<BattleContent>("BattleContent");
            if (content == null || content.Cards.Length == 0)
            {
                return result;
            }

            var pool = new List<CardDefinition>(content.Cards);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }
    }
}
