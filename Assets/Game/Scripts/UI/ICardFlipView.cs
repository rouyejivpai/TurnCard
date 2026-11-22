using System;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.Game.UI
{
    /// <summary>
    /// 「能翻面的卡」的统一契约。
    ///
    /// 玩家卡与敌人卡的卡面完全不同（一个是信息卡，一个是角色立牌），所以**不共用实现**；
    /// 但战斗流程只依赖这一个接口就能同等对待两者：都能被要求翻到某一面，
    /// 也都能报告当前朝向与是否正在翻。
    ///
    /// 语义约定（两边实现都必须遵守，见 `Docs/20-设计规则.md` 第 9 条）：
    /// <list type="bullet">
    /// <item>同一张牌连续翻两次必须播两次动画，不允许合并；</item>
    /// <item>动画时长 <c>duration &lt;= 0</c> 或 <c>animate == false</c> 时立刻切面，不播动画；</item>
    /// <item><c>onCompleted</c> 必须在动画真正结束后回调，且只回调一次。</item>
    /// </list>
    /// </summary>
    public interface ICardFlipView
    {
        /// <summary>当前朝上的是哪一面。</summary>
        CardFaceSide ShownSide { get; }

        /// <summary>是否正在播翻面动画（含队列中尚未开始的）。</summary>
        bool IsFlipping { get; }

        /// <summary>翻到指定面；同一张牌多次调用会排队串行播放。</summary>
        void PlayFlipTo(CardFaceSide side, float duration, AudioSource audio = null,
            Action onCompleted = null);

        /// <summary>把卡摆到指定的一面；animate 为 true 时播动画。</summary>
        void ApplySide(CardFaceSide side, bool animate, float duration, AudioSource audio,
            Action onCompleted);
    }
}
