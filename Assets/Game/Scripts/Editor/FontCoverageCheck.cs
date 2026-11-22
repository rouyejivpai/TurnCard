using System.Collections.Generic;
using System.Text;
using TMPro;
using TurnCard.Game.Data;
using TurnCard.Game.UI;
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 检查全局字体（像素字 + 回退链）对**游戏里会显示的文字**的覆盖情况。
    ///
    /// 为什么需要它：ArkPixel12 缺若干汉字与符号（然/即/瞬/≥/≤/✔/✓），
    /// 靠 SimHei 回退兜住前五个；缺字在运行时只会渲染成方块，不报错，
    /// 所以每次加文案都要跑一次这个检查。
    /// </summary>
    public static class FontCoverageCheck
    {
        [MenuItem("Tools/TurnCard/检查字体缺字", priority = 220)]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            TMP_FontAsset font = UiFactory.Font;
            if (font == null)
            {
                return "[字体检查] 拿不到全局字体（UiFactory.Font 为空）";
            }

            var strings = new List<string>
            {
                // 界面文案（代码里写死的那些）
                "开始新游戏 退出 第 1 战 营地 商店 首领 查看牌组 下一步 结束回合 抽牌堆 弃牌堆 关闭 跳过 离开 再来一局 通关 失败",
                "生命 金币 牌组 张 能量 护甲 选择一张手牌翻面，然后点勾确认 战斗胜利 获得 当前金币 点击即拿走 翻面后 · 背面 正面 √ ←",
            };

            var content = Resources.Load<BattleContent>("BattleContent");
            if (content != null)
            {
                foreach (CardDefinition card in content.Cards)
                {
                    strings.Add(card.Front.DisplayName);
                    strings.Add(card.Front.Description);
                    strings.Add(card.Back.DisplayName);
                    strings.Add(card.Back.Description);
                }

                foreach (EnemyDefinition enemy in content.Enemies)
                {
                    strings.Add(enemy.Front.DisplayName);
                    strings.Add(enemy.Front.Description);
                    strings.Add(enemy.Back.DisplayName);
                    strings.Add(enemy.Back.Description);
                }

                foreach (BuffDefinition buff in content.Buffs)
                {
                    strings.Add(buff.DisplayName);
                    strings.Add(buff.Description);
                }

                foreach (KeywordDefinition keyword in content.Keywords)
                {
                    strings.Add(keyword.Name);
                    strings.Add(keyword.Description);
                }
            }

            var seen = new HashSet<char>();
            var missing = new Dictionary<char, string>();
            foreach (string s in strings)
            {
                if (string.IsNullOrEmpty(s))
                {
                    continue;
                }

                foreach (char ch in s)
                {
                    if (ch == '\n' || ch == ' ')
                    {
                        continue;
                    }

                    if (!seen.Add(ch))
                    {
                        continue;
                    }

                    // tryAddCharacter = true：回退字体是动态的，要先尝试加入图集
                    if (!font.HasCharacter(ch, true, true))
                    {
                        missing[ch] = s.Length > 24 ? s.Substring(0, 24) + "…" : s;
                    }
                }
            }

            var log = new StringBuilder();
            log.AppendLine("[字体检查] 字体=" + font.name +
                           " 回退=" + (font.fallbackFontAssetTable == null
                               ? "无"
                               : string.Join(",", System.Array.ConvertAll(
                                   font.fallbackFontAssetTable.ToArray(), f => f.name))));
            log.AppendLine("扫描字符=" + seen.Count + "  缺字=" + missing.Count);

            foreach (KeyValuePair<char, string> pair in missing)
            {
                log.AppendLine(string.Format("  [缺字] U+{0:X4} '{1}'  例：{2}", (int)pair.Key, pair.Key,
                    pair.Value));
            }

            log.AppendLine(missing.Count == 0
                ? "== 全部可显示 =="
                : "== 有缺字，换字或加回退字体 ==");
            return log.ToString();
        }
    }
}
