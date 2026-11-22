using System.Collections.Generic;
using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Data;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using UnityEngine;

namespace TurnCard.Game.Procedure
{
    /// <summary>营地：休息回血，或删掉牌组里的一张牌。</summary>
    public sealed class ProcedureRest : FlowProcedureBase
    {
        private const int HealAmount = 20;

        private IFsm<IProcedureManager> m_Owner;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_Owner = procedureOwner;

            ShowMain();
        }

        private void ShowMain()
        {
            MenuScreen screen = Open<MenuScreen>(MenuScreen.ResourcePath);
            screen.SetTitle("营地");
            screen.SetSubtitle($"生命 {Run.Hp}/{Run.MaxHp}    牌组 {Run.Deck.Count} 张");

            screen.AddButton($"休息，回复 {HealAmount} 点生命", () =>
            {
                Run.Heal(HealAmount);
                Finish();
            });

            screen.AddButton("删掉一张牌", ShowRemovePicker, Run.Deck.Count > 1);
            screen.AddButton("直接离开", Finish);
        }

        /// <summary>用卡牌网格让玩家挑一张要删的牌。</summary>
        private void ShowRemovePicker()
        {
            CardListViewScreen list = Open<CardListViewScreen>(CardListViewScreen.ResourcePath);
            list.SetHeader("选择要删掉的牌", "点哪张就删哪张");

            List<CardDefinition> deck = BuildDeckCards();
            list.ShowCards(deck, card =>
            {
                Run.RemoveCard(card.Id);
                Finish();
            });

            list.OnCloseRequested = ShowMain;
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
    }
}
