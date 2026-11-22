using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Run;
using TurnCard.Game.UI;

namespace TurnCard.Game.Procedure
{
    /// <summary>地图：线性推进，只有「下一个节点」可以点。</summary>
    public sealed class ProcedureMap : FlowProcedureBase
    {
        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            MenuScreen screen = Open<MenuScreen>(MenuScreen.ResourcePath);
            screen.SetTitle(CurrentTitle());
            screen.SetSubtitle(
                $"生命 {Run.Hp}/{Run.MaxHp}    金币 {Run.Gold}    牌组 {Run.Deck.Count} 张");

            int next = Run.NodeIndex + 1;
            for (int i = 0; i < Run.Map.Count; i++)
            {
                MapNode node = Run.Map[i];
                bool isNext = i == next;
                string label = (node.Cleared ? "✔ " : string.Empty) + node.Label +
                               (isNext ? "  ← 下一步" : string.Empty);

                int index = i;
                screen.AddButton(label,
                    () => OnNodeClicked(procedureOwner, index),
                    isNext && !Run.IsOver);
            }

            screen.AddButton("查看牌组", ShowDeck);

            if (Run.IsOver)
            {
                screen.AddButton("查看结算", () => ChangeState<ProcedureGameOver>(procedureOwner));
            }
        }

        /// <summary>叠一层牌组列表，关掉后回到地图。</summary>
        private void ShowDeck()
        {
            CardListViewScreen list = OpenOverlay<CardListViewScreen>(CardListViewScreen.ResourcePath);
            list.SetHeader("牌组", $"共 {Run.Deck.Count} 张");
            list.ShowCards(BuildDeckCards(), null);
            list.OnCloseRequested = () =>
            {
                var form = list.GetComponent<UnityGameFramework.Runtime.UIForm>();
                if (form != null)
                {
                    CloseOverlay(form);
                }
            };
        }

        private string CurrentTitle()
        {
            if (Run.IsVictory)
            {
                return "全部通关";
            }

            if (Run.IsDefeat)
            {
                return "旅程结束";
            }

            return "地图";
        }

        private void OnNodeClicked(IFsm<IProcedureManager> owner, int index)
        {
            if (Run.IsOver)
            {
                return;
            }

            // 只允许推进到下一个节点
            if (index != Run.NodeIndex + 1)
            {
                return;
            }

            MapNode node = Run.Advance();
            if (node == null)
            {
                return;
            }

            EventBus.Publish(new NodeSelectedEvent { Index = index });
            EnterNode(owner, node.Type);
        }
    }
}
