namespace TurnCard.Game.Run
{
    /// <summary>战斗结束。由战斗界面发出，战斗流程接收。</summary>
    public sealed class BattleFinishedEvent
    {
        public bool Victory;
        public int RemainingHp;
    }

    /// <summary>地图上选择了某个节点。</summary>
    public sealed class NodeSelectedEvent
    {
        public int Index;
    }

    /// <summary>奖励界面选了一张牌（CardId 为 0 表示跳过）。</summary>
    public sealed class RewardChosenEvent
    {
        public int CardId;
    }

    /// <summary>营地/商店完成。</summary>
    public sealed class NodeResolvedEvent
    {
    }

    /// <summary>整局结束（胜利或失败）。</summary>
    public sealed class RunEndedEvent
    {
        public bool Victory;
    }
}
