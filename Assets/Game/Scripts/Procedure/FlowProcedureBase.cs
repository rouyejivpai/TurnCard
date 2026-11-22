using GameFramework.Fsm;
using GameFramework.Procedure;
using TurnCard.Game.Run;
using TurnCard.Game.UI;
using UnityGameFramework.Runtime;

namespace TurnCard.Game.Procedure
{
    /// <summary>
    /// 流程基类：复用界面宿主，管理「当前界面」的打开与关闭。
    /// 界面一律从预制体打开（路径写在各界面的 <c>ResourcePath</c> 常量里），
    /// 不再有「代码搭建」的界面。
    /// </summary>
    public abstract class FlowProcedureBase : ProcedureBase
    {
        private UIForm m_Form;
        private readonly System.Collections.Generic.List<UIForm> m_Overlays =
            new System.Collections.Generic.List<UIForm>();

        protected RunState Run => RunState.Current;

        protected static FormHost Host => UIHost.Shared;

        /// <summary>把牌组 id 列表解析成卡牌定义（跳过找不到的 id）。</summary>
        protected System.Collections.Generic.List<TurnCard.Game.Data.CardDefinition> BuildDeckCards()
        {
            var result = new System.Collections.Generic.List<TurnCard.Game.Data.CardDefinition>();
            var content = UnityEngine.Resources.Load<TurnCard.Game.Data.BattleContent>("BattleContent");
            if (content == null)
            {
                return result;
            }

            var lookup = new System.Collections.Generic.Dictionary<int, TurnCard.Game.Data.CardDefinition>();
            foreach (var card in content.Cards)
            {
                lookup[card.Id] = card;
            }

            foreach (int id in Run.Deck)
            {
                if (lookup.TryGetValue(id, out var definition))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        /// <summary>在当前界面之上再叠一个界面（比如地图上查看牌组），不影响流程主界面。</summary>
        protected T OpenOverlay<T>(string resourcePath) where T : UGuiForm
        {
            T screen = Host.Open<T>(resourcePath);
            if (screen == null)
            {
                return null;
            }

            UIForm form = screen.GetComponent<UIForm>();
            if (form != null)
            {
                m_Overlays.Add(form);
            }

            return screen;
        }

        /// <summary>关掉某一个叠层界面。</summary>
        protected void CloseOverlay(UIForm form)
        {
            if (form == null)
            {
                return;
            }

            m_Overlays.Remove(form);
            Host.Close(form);
        }

        protected void CloseOverlays()
        {
            foreach (UIForm form in m_Overlays)
            {
                if (form != null)
                {
                    Host.Close(form);
                }
            }

            m_Overlays.Clear();
        }

        /// <summary>打开一个界面预制体，替换掉本流程之前开的那个。</summary>
        protected T Open<T>(string resourcePath) where T : UGuiForm
        {
            CloseScreen();
            T screen = Host.Open<T>(resourcePath);
            m_Form = screen != null ? screen.GetComponent<UIForm>() : null;
            return screen;
        }

        protected void CloseScreen()
        {
            if (m_Form == null)
            {
                return;
            }

            Host.Close(m_Form);
            m_Form = null;
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            CloseOverlays();
            CloseScreen();
            base.OnLeave(procedureOwner, isShutdown);
        }

        /// <summary>按节点类型切到对应流程。</summary>
        protected void EnterNode(IFsm<IProcedureManager> owner, MapNodeType type)
        {
            switch (type)
            {
                case MapNodeType.Rest:
                    ChangeState<ProcedureRest>(owner);
                    break;
                case MapNodeType.Shop:
                    ChangeState<ProcedureShop>(owner);
                    break;
                default:
                    UnityEngine.Debug.Log("[战斗] 0 点击节点，准备切战斗流程");
                    ChangeState<ProcedureBattle>(owner);
                    UnityEngine.Debug.Log("[战斗] 0b 战斗流程已切换");
                    break;
            }
        }
    }
}
