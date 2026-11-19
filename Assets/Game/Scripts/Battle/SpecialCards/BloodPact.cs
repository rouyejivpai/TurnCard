using TurnCard.Game.Battle;

namespace TurnCard.Game.Battle.SpecialCards
{
    /// <summary>
    /// 示例特殊卡：血祭。失去生命，换取高额伤害。
    ///
    /// 配置表里写 <c>special:blood_pact</c> 就会走到这里；
    /// 也可以带参数，例如 <c>special:blood_pact,4,15</c> 表示失去 4 点生命造成 15 点伤害。
    ///
    /// 新增特殊卡只需要：新建一个类、实现 ICardEffect、打上 [SpecialCard("key")]，
    /// 表里填 special:key 即可，不用改任何分发代码。
    /// </summary>
    [SpecialCard("blood_pact")]
    public sealed class BloodPact : ICardEffect
    {
        private const int DefaultHpCost = 3;
        private const int DefaultDamage = 12;

        public void Apply(CardEffectContext context, string[] args)
        {
            int hpCost = ReadArg(args, 0, DefaultHpCost);
            int damage = ReadArg(args, 1, DefaultDamage);

            context.State.LosePlayerHp(hpCost);
            if (context.State.IsBattleOver)
            {
                return;
            }

            if (context.State.DamageEnemy(context.Target, damage))
            {
                context.FlippedEnemy = context.Target;
            }
        }

        private static int ReadArg(string[] args, int index, int fallback)
        {
            if (args == null || index >= args.Length)
            {
                return fallback;
            }

            return int.TryParse(args[index], out int value) ? value : fallback;
        }
    }
}
