using System.Collections.Generic;
using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Data;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using UnityEngine;

namespace TurnCard.Game.Procedure
{
    /// <summary>商店：买 3 张牌中的一张，或者花钱删一张牌。</summary>
    public sealed class ProcedureShop : FlowProcedureBase
    {
        private const int CardPrice = 50;
        private const int RemovePrice = 75;

        private IFsm<IProcedureManager> m_Owner;
        private List<CardDefinition> m_Stock;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_Owner = procedureOwner;
            m_Stock = PickStock(3);

            ShowMain();
        }

        private void ShowMain()
        {
            CardGridScreen screen = Open<CardGridScreen>(CardGridScreen.ResourcePath);
            screen.SetHeader("商店", $"金币 {Run.Gold}    牌组 {Run.Deck.Count} 张",
                $"点击卡牌购买（{CardPrice} 金币）");

            var affordable = new List<CardDefinition>();

            screen.ShowCards(m_Stock, 1.1f, m_Stock.Count, Buy);

            screen.AddButton($"删掉一张牌（{RemovePrice} 金币）", ShowRemovePicker,
                Run.Gold >= RemovePrice && Run.Deck.Count > 1);
            screen.AddButton("离开", Finish);
        }

        private void Buy(CardDefinition card)
        {
            if (!Run.SpendGold(CardPrice))
            {
                return;
            }

            Run.AddCard(card.Id);
            m_Stock.Remove(card);
            ShowMain();
        }

        private void ShowRemovePicker()
        {
            CardListViewScreen list = Open<CardListViewScreen>(CardListViewScreen.ResourcePath);
            list.SetHeader("选择要删掉的牌", $"花费 {RemovePrice} 金币");

            List<CardDefinition> deck = BuildDeckCards();
            list.ShowCards(deck, card =>
            {
                if (Run.SpendGold(RemovePrice))
                {
                    Run.RemoveCard(card.Id);
                }

                ShowMain();
            });

            list.OnCloseRequested = ShowMain;
        }

        private void Finish()
        {
            if (m_Owner == null)
            {
                return;
            }

            Run.MarkCleared();
            EventBus.Publish(new NodeResolvedEvent());
            ChangeState<ProcedureMap>(m_Owner);
        }

        private static List<CardDefinition> PickStock(int count)
        {
            var result = new List<CardDefinition>();
            var content = Resources.Load<BattleContent>("BattleContent");
            if (content == null || content.Cards.Length == 0)
            {
                return result;
            }

            var pool = new List<CardDefinition>(content.Cards);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        private List<CardDefinition> BuildDeckCards()
        {
            var result = new List<CardDefinition>();
            var content = Resources.Load<BattleContent>("BattleContent");
            if (content == null)
            {
                return result;
            }

            var lookup = new Dictionary<int, CardDefinition>();
            foreach (CardDefinition card in content.Cards)
            {
                lookup[card.Id] = card;
            }

            foreach (int id in Run.Deck)
            {
                if (lookup.TryGetValue(id, out CardDefinition definition))
                {
                    result.Add(definition);
                }
            }

            return result;
        }
    }
}
