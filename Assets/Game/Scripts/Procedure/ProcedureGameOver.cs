using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Run;
using TurnCard.Game.UI;

namespace TurnCard.Game.Procedure
{
    /// <summary>结算：显示这一局的最终结果，可以重开。</summary>
    public sealed class ProcedureGameOver : FlowProcedureBase
    {
        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            MenuScreen screen = Open<MenuScreen>(MenuScreen.ResourcePath);
            screen.SetTitle(Run.IsVictory ? "通关" : "失败");
            screen.SetSubtitle($"到达第 {Run.NodeIndex + 1} 个节点    金币 {Run.Gold}    " +
                               $"牌组 {Run.Deck.Count} 张    剩余生命 {Run.Hp}");

            EventBus.Publish(new RunEndedEvent { Victory = Run.IsVictory });

            screen.AddButton("再来一局", () =>
            {
                RunState.NewRun();
                ChangeState<ProcedureMainMenu>(procedureOwner);
            });
        }
    }
}
