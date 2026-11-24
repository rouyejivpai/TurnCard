using System.Collections.Generic;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 战斗里的「数值预览」来源。
    /// 战斗界面把当前 BattleState 挂进来，其他展示卡面的地方（悬停预览、牌组列表）
    /// 调 <see cref="Apply"/> 就能拿到和手里完全一致的预览数值；
    /// 不在战斗里（State 为 null）就保持卡面原值。
    /// </summary>
    public static class CardPreviewSource
    {
        public static BattleState State { get; set; }

        /// <summary>拖牌指着的目标（影响易伤计算），没有就 null。</summary>
        public static EnemyInstance Target { get; set; }

        /// <summary>
        /// 按某一面算整套预览选项（含效果 token）。
        /// 卡面「填充隐藏面」时用它，未显示的那一面也不会漏成 {total_armor}。
        /// </summary>
        public static CardText.FormatOptions BuildOptions(CardFace face)
        {
            if (State == null || State.IsBattleOver)
            {
                return default;
            }

            BattleState.ValuePreview damage = State.PreviewPlayerDamage(face, Target);
            BattleState.ValuePreview armor = State.PreviewPlayerArmor(face);

            var tokens = new Dictionary<string, int>();
            State.CollectPreviewTokens(face, Target, tokens);

            return new CardText.FormatOptions
            {
                Damage = damage.Value,
                Armor = armor.Value,
                MarkModified = true,
                Tokens = tokens.Count > 0 ? tokens : null,
            };
        }

        /// <summary>把预览套到一张卡上；没有战斗上下文时什么也不做。</summary>
        public static void Apply(CardView view, CardFace face)
        {
            // 战斗结束后不再给任何卡面提供战斗中的数值
            if (view == null || State == null || State.IsBattleOver)
            {
                return;
            }

            BattleState.ValuePreview damage = State.PreviewPlayerDamage(face, Target);
            BattleState.ValuePreview armor = State.PreviewPlayerArmor(face);

            var tokens = new Dictionary<string, int>();
            State.CollectPreviewTokens(face, Target, tokens);

            // MarkModified 只是允许染色，实际是否变红由 CardText 比较结果决定
            view.SetValuePreview(damage.Value, armor.Value, true,
                tokens.Count > 0 ? tokens : null);
        }
    }
}
