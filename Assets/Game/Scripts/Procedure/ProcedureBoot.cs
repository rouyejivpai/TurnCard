using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.Procedure
{
    /// <summary>
    /// 启动流程：框架就绪后进入主菜单。之后真正的资源检查、版本更新都会挂在这里。
    /// </summary>
    public sealed class ProcedureBoot : ProcedureBase
    {
        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            Log.Info("[流程] 启动完成，进入主菜单。");
            ChangeState<ProcedureMainMenu>(procedureOwner);
        }
    }
}
