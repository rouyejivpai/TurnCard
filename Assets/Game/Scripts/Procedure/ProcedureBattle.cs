using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using UnityGameFramework.Runtime;
using Window.BattleForm;

namespace TurnCard.Game.Procedure
{
    /// <summary>
    /// 战斗流程：打开战斗界面，等它发出 <see cref="BattleFinishedEvent"/> 再决定去哪。
    /// </summary>
    public sealed class ProcedureBattle : ProcedureBase
    {
        
        private BattleForm m_Form;
        private IFsm<IProcedureManager> m_Owner;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Owner = procedureOwner;
            Log.Info("[流程] 进入战斗流程。");

            EventBus.Subscribe<BattleFinishedEvent>(OnBattleFinished);

            Log.Info("[战斗] A 准备打开界面");

            // UIBind 保存预制体时会把窗口命名为 <类名>Window，
            // 这里用 nameof 拼路径，类名改了路径自动跟着变，不会再对不上。
            string resourcePath = "UI/Battle/" + nameof(BattleForm) + "Window";
            m_Form = UIHost.Shared.Open<BattleForm>(resourcePath);
            Log.Info("[战斗] B 界面已打开");
            if (m_Form == null)
            {
                Log.Error("[流程] 战斗界面打开失败。");
            }
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            EventBus.Unsubscribe<BattleFinishedEvent>(OnBattleFinished);

            if (m_Form != null)
            {
                UIForm form = m_Form.GetComponent<UIForm>();
                if (form != null)
                {
                    UIHost.Shared.Close(form);
                }

                m_Form = null;
            }

            Log.Info("[流程] 离开战斗流程。");
            base.OnLeave(procedureOwner, isShutdown);
        }

        private void OnBattleFinished(BattleFinishedEvent evt)
        {
            if (m_Owner == null)
            {
                return;
            }

            RunState.Current.SetHp(evt.RemainingHp);

            if (evt.Victory)
            {
                ChangeState<ProcedureReward>(m_Owner);
            }
            else
            {
                ChangeState<ProcedureGameOver>(m_Owner);
            }
        }
    }
}
