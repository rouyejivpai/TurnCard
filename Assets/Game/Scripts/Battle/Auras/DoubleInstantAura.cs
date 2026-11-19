using TurnCard.Game.Battle;

namespace TurnCard.Game.Battle.Auras
{
    /// <summary>
    /// 示例光环：这一面朝上时（牌在手上），场上的「瞬发」每次触发两次。
    ///
    /// 用法：某张卡的 keywords 列写 <c>双重瞬发</c>，只要它当前朝上的面带这个词，
    /// 本回合里所有瞬发效果都会触发两次。
    ///
    /// 新光环的写法同理：实现 <see cref="IAuraKeyword"/>，打上
    /// <c>[AuraKeyword("名字")]</c>，表里写名字即可，不需要改任何分发代码。
    /// </summary>
    [AuraKeyword("双重瞬发")]
    public sealed class DoubleInstantAura : IAuraKeyword
    {
        public int ModifyInstantRepeat(BattleState state, CardInstance owner)
        {
            // 每次触发多来一次
            return 1;
        }
    }
}
