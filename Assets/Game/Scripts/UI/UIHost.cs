using TurnCard.Game.UI;

namespace TurnCard.Game
{
    /// <summary>
    /// 全流程共用的界面宿主。之前流程基类和战斗流程各自持有一个静态字段，
    /// 导致同一个游戏里出现两个 Canvas，界面层级会错乱。
    /// </summary>
    public static class UIHost
    {
        private static FormHost s_Shared;

        public static FormHost Shared
        {
            get
            {
                if (s_Shared == null)
                {
                    s_Shared = FormHost.Create(null);
                }

                return s_Shared;
            }
        }
    }
}
