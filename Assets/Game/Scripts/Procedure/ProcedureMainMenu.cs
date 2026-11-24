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

            MenuScreen screen = Open<MenuScreen>(MenuScreen.ResourcePath);
            screen.SetTitle("回合牌");
            screen.SetSubtitle("一张牌，两面都是它");

            screen.AddButton("开始新游戏", () =>
            {
                RunState.NewRun();

                // 选关不再用独立的地图界面：直接进战斗界面，先显示路线选项
                Window.BattleForm.BattleForm.PendingRouteSelect = true;
                ChangeState<ProcedureBattle>(procedureOwner);
            });

            screen.AddButton("退出", UnityEngine.Application.Quit);
        }
    }
}
