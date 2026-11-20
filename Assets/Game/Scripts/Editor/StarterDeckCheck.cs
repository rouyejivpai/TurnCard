using System.Text;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>确认起始卡组已经把每种卡都包进来了。</summary>
    public static class StarterDeckCheck
    {
        public static void Run()
        {
            var log = new StringBuilder();
            BattleContent content = Resources.Load<BattleContent>("BattleContent");
            if (content == null)
            {
                Debug.Log("[StarterDeckCheck] BattleContent 缺失");
                return;
            }

            int total = 0;
            foreach (StarterDeckEntry entry in content.StarterDeck)
            {
                total += entry.Count;
            }

            log.AppendLine("卡牌总数=" + content.Cards.Length
                + "  卡组条目=" + content.StarterDeck.Length
                + "  卡组张数=" + total);

            int missing = 0;
            foreach (CardDefinition card in content.Cards)
            {
                bool found = false;
                foreach (StarterDeckEntry entry in content.StarterDeck)
                {
                    if (entry.CardId == card.Id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    missing++;
                    log.AppendLine("  [缺失] 卡 " + card.Id + " " + card.Front.DisplayName);
                }
            }

            log.AppendLine(missing == 0
                ? "== 每种卡都在卡组里 =="
                : "== 缺 " + missing + " 种 ==");

            Debug.Log("[StarterDeckCheck]\n" + log);
        }
    }
}
