using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Run;
using TurnCard.Game.UI;

namespace TurnCard.Game.Procedure
{
    /// <summary>主菜单：开一局新的，或退出。</summary>
    public sealed class ProcedureMainMenu : FlowProcedureBase
    {
        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            MenuScreen screen = Open<MenuScreen>("MainMenu");
            screen.SetTitle("回合牌");
            screen.SetSubtitle("一张牌，两面都是它");

            screen.AddButton("开始新游戏", () =>
            {
                RunState.NewRun();
                ChangeState<ProcedureMap>(procedureOwner);
            });

            screen.AddButton("退出", UnityEngine.Application.Quit);
        }
    }
}
