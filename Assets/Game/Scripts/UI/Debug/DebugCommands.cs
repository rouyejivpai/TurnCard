using System.Collections.Generic;
using TurnCard.Game.Data;
using TurnCard.Game.Run;
using UnityEngine;
using Window.BattleForm;

namespace TurnCard.Game.UI.DebugTools
{
    /// <summary>
    /// 调试台的命令分发。每条命令自己负责打日志，返回值只用于「是否需要刷新」。
    /// </summary>
    public static class DebugCommands
    {
        private static BattleContent s_Content;

        private static BattleContent Content
        {
            get
            {
                if (s_Content == null)
                {
                    s_Content = Resources.Load<BattleContent>("BattleContent");
                }

                return s_Content;
            }
        }

        public static void Execute(string line, DebugConsole console)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            string command = parts[0].ToLowerInvariant();
            switch (command)
            {
                case "help":
                case "?":
                    Help(console);
                    break;

                case "battle":
                    Battle(parts, console);
                    break;

                case "addcard":
                    AddCard(parts, console);
                    break;

                case "removecard":
                    RemoveCard(parts, console);
                    break;

                case "deck":
                    DumpDeck(console);
                    break;

                case "cards":
                    DumpCards(console);
                    break;

                case "enemies":
                    DumpEnemies(console);
                    break;

                case "intents":
                    DumpIntents(parts, console);
                    break;

                case "hp":
                    SetHp(parts, console);
                    break;

                case "energy":
                    SetEnergy(parts, console);
                    break;

                case "gold":
                    SetGold(parts, console);
                    break;

                case "kill":
                    KillAll(console);
                    break;

                case "clear":
                    console.SetVisible(false);
                    console.SetVisible(true);
                    break;

                case "close":
                    console.SetVisible(false);
                    break;

                default:
                    console.Write("未知命令：" + command + "（输入 help 查看）");
                    break;
            }
        }

        private static void Help(DebugConsole console)
        {
            console.Write("battle <敌人id...>   直接打一场（如 battle 6 / battle 8 9）");
            console.Write("battle              用当前关卡池子的随机编队");
            console.Write("addcard <卡牌id>     往牌组加一张（战斗中同时进抽牌堆）");
            console.Write("removecard <卡牌id>  从牌组删一张");
            console.Write("deck                看当前牌组");
            console.Write("cards               列出所有卡牌 id");
            console.Write("enemies             列出所有怪物 id");
            console.Write("intents <敌人id>     看某只怪的招式表");
            console.Write("hp <n> / energy <n> / gold <n>   改数值");
            console.Write("kill                秒掉所有敌人");
            console.Write("clear / close       清屏 / 关闭");
        }

        private static void Battle(string[] parts, DebugConsole console)
        {
            BattleForm form = Object.FindObjectOfType<BattleForm>();

            if (parts.Length <= 1)
            {
                if (form != null)
                {
                    form.DebugRestartWith(null);
                    console.Write("已用当前池子的随机编队重开一场");
                    return;
                }

                console.Write("请给敌人 id，例如 battle 6");
                return;
            }

            var lineup = new List<EnemyDefinition>();
            for (int i = 1; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int id))
                {
                    console.Write("id 不是数字：" + parts[i]);
                    return;
                }

                EnemyDefinition def = FindEnemy(id);
                if (def == null)
                {
                    console.Write("没有 id=" + id + " 的怪物");
                    return;
                }

                lineup.Add(def);
            }

            if (form == null)
            {
                BattleForm.DebugPendingLineup = lineup.ToArray();
                UIHost.Shared.Open<BattleForm>("UI/Battle/BattleFormWindow");
                console.Write("已开战：" + Describe(lineup));
                return;
            }

            form.DebugRestartWith(lineup.ToArray());
            console.Write("已重开为：" + Describe(lineup));
        }

        private static void AddCard(string[] parts, DebugConsole console)
        {
            if (parts.Length <= 1 || !int.TryParse(parts[1], out int id))
            {
                console.Write("用法：addcard <卡牌id>（cards 查看）");
                return;
            }

            CardDefinition def = FindCard(id);
            if (def == null)
            {
                console.Write("没有 id=" + id + " 的卡牌");
                return;
            }

            RunState.Current.AddCard(id);

            BattleForm form = Object.FindObjectOfType<BattleForm>();
            if (form != null && form.DebugAddCardToBattle(def))
            {
                console.Write("已加入牌组，并放进本场抽牌堆：" + def.Front.DisplayName);
                return;
            }

            console.Write("已加入牌组（下一场生效）：" + def.Front.DisplayName);
        }

        private static void RemoveCard(string[] parts, DebugConsole console)
        {
            if (parts.Length <= 1 || !int.TryParse(parts[1], out int id))
            {
                console.Write("用法：removecard <卡牌id>");
                return;
            }

            console.Write(RunState.Current.RemoveCard(id) ? "已从牌组移除 " + id : "牌组里没有 " + id);
        }

        private static void DumpDeck(DebugConsole console)
        {
            IReadOnlyList<int> deck = RunState.Current.Deck;
            if (deck.Count == 0)
            {
                console.Write("牌组是空的");
                return;
            }

            console.Write("牌组（" + deck.Count + "）：");
            for (int i = 0; i < deck.Count; i++)
            {
                CardDefinition def = FindCard(deck[i]);
                console.Write("  [" + i + "] " + deck[i] + " " + (def == null ? "?" : def.Front.DisplayName));
            }
        }

        private static void DumpCards(DebugConsole console)
        {
            if (Content == null)
            {
                console.Write("读不到 BattleContent");
                return;
            }

            foreach (CardDefinition card in Content.Cards)
            {
                if (card != null)
                {
                    console.Write("  " + card.Id + "  " + card.Front.DisplayName + "  费用 " + card.Front.Cost);
                }
            }
        }

        private static void DumpEnemies(DebugConsole console)
        {
            if (Content == null)
            {
                console.Write("读不到 BattleContent");
                return;
            }

            foreach (EnemyDefinition enemy in Content.Enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                console.Write("  " + enemy.Id + "  " + enemy.Front.DisplayName + " / " + enemy.Back.DisplayName
                    + "  HP " + enemy.Front.MaxHp + "/" + enemy.Back.MaxHp
                    + "  " + enemy.Pool + (enemy.Disabled ? "  [占位]" : string.Empty));
            }
        }

        private static void DumpIntents(string[] parts, DebugConsole console)
        {
            if (Content == null)
            {
                console.Write("读不到 BattleContent");
                return;
            }

            if (parts.Length <= 1 || !int.TryParse(parts[1], out int id))
            {
                console.Write("用法：intents <敌人id>");
                return;
            }

            console.Write("敌人 " + id + " 的招式：");
            foreach (EnemyIntentDefinition intent in Content.EnemyIntents)
            {
                if (intent == null || intent.EnemyId != id)
                {
                    continue;
                }

                console.Write("  " + intent.Form + "  " + intent.Intent + "  v=" + intent.Value
                    + "  x" + intent.Hits + "  buff=" + intent.BuffId + "  →" + intent.BuffTarget);
            }
        }

        private static void SetHp(string[] parts, DebugConsole console)
        {
            if (parts.Length <= 1 || !int.TryParse(parts[1], out int value))
            {
                console.Write("用法：hp <数值>");
                return;
            }

            RunState.Current.SetHp(value);
            console.Write("玩家血量设为 " + value + "（下一场战斗生效）");
        }

        private static void SetEnergy(string[] parts, DebugConsole console)
        {
            BattleForm form = Object.FindObjectOfType<BattleForm>();
            if (form == null)
            {
                console.Write("不在战斗里");
                return;
            }

            if (parts.Length <= 1 || !int.TryParse(parts[1], out int value))
            {
                console.Write("用法：energy <数值>");
                return;
            }

            console.Write(form.DebugSetEnergy(value) ? "能量设为 " + value : "设置失败");
        }

        private static void SetGold(string[] parts, DebugConsole console)
        {
            if (parts.Length <= 1 || !int.TryParse(parts[1], out int value))
            {
                console.Write("用法：gold <数值>");
                return;
            }

            RunState.Current.AddGold(value - RunState.Current.Gold);
            console.Write("金币设为 " + value);
        }

        private static void KillAll(DebugConsole console)
        {
            BattleForm form = Object.FindObjectOfType<BattleForm>();
            if (form == null)
            {
                console.Write("不在战斗里");
                return;
            }

            console.Write(form.DebugKillAll() ? "已清空敌人" : "没有可杀的敌人");
        }

        private static EnemyDefinition FindEnemy(int id)
        {
            if (Content == null)
            {
                return null;
            }

            foreach (EnemyDefinition enemy in Content.Enemies)
            {
                if (enemy != null && enemy.Id == id)
                {
                    return enemy;
                }
            }

            return null;
        }

        private static CardDefinition FindCard(int id)
        {
            if (Content == null)
            {
                return null;
            }

            foreach (CardDefinition card in Content.Cards)
            {
                if (card != null && card.Id == id)
                {
                    return card;
                }
            }

            return null;
        }

        private static string Describe(List<EnemyDefinition> lineup)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lineup.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" + ");
                }

                sb.Append(lineup[i].Front.DisplayName);
                sb.Append("(");
                sb.Append(lineup[i].Id);
                sb.Append(")");
            }

            return sb.ToString();
        }
    }
}
