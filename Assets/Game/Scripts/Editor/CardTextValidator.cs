using System.Collections.Generic;
using System.Text;
using TurnCard.Game;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEditor;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// 文本与效果串的静态校验。改完表跑一次，能提前发现：
    /// - 卡面里 [[关键词]] 没有对应关键词表条目
    /// - 占位符 {xxx} 解析不到
    /// - 效果串写了不存在的效果名 / 特殊卡 key
    /// - 关键词表里有定义但没有任何卡牌使用
    /// </summary>
    public static class CardTextValidator
    {
        [MenuItem("Tools/TurnCard/数据表/检查文本与效果", priority = 102)]
        public static void Run()
        {
            string report = BuildReport();
            if (report.Length == 0)
            {
                Debug.Log("[校验] 文本与效果串检查通过。");
            }
            else
            {
                Debug.LogWarning("[校验] 发现问题：" + System.Environment.NewLine + report);
            }
        }

        /// <summary>返回问题清单；无问题时返回空字符串。</summary>
        public static string BuildReport()
        {
            var report = new StringBuilder();
            var content = Resources.Load<BattleContent>("BattleContent");
            if (content == null)
            {
                return "找不到 BattleContent，先执行「生成 SO」。";
            }

            var keywords = new HashSet<string>();
            foreach (KeywordDefinition keyword in content.Keywords)
            {
                if (keyword != null && !string.IsNullOrEmpty(keyword.Name))
                {
                    keywords.Add(keyword.Name);
                }
            }

            var usedKeywords = new HashSet<string>();
            // 先触发一次光环扫描，让 [AuraKeyword] 提供的关键词也进入已知集合
            AuraKeywords.EnsureLoaded();
            var knownSpecials = new HashSet<string>(CardEffects.KnownSpecialKeys());

            foreach (CardDefinition card in content.Cards)
            {
                CheckFace(card, "正面", card.Front, keywords, usedKeywords, knownSpecials, report);
                CheckFace(card, "背面", card.Back, keywords, usedKeywords, knownSpecials, report);
            }

            foreach (string keyword in keywords)
            {
                if (!usedKeywords.Contains(keyword))
                {
                    report.AppendLine($"[未使用] 关键词「{keyword}」定义了，但没有卡面引用它");
                }
            }

            return report.ToString();
        }

        private static void CheckFace(CardDefinition card, string side, CardFace face,
            HashSet<string> keywords, HashSet<string> usedKeywords, HashSet<string> knownSpecials,
            StringBuilder report)
        {
            string where = $"卡牌 {card.Id}（{card.name}）{side}";

            // 描述里出现的关键词（现在按文本检索）
            foreach (string keyword in CardText.ExtractKeywords(face.Description))
            {
                usedKeywords.Add(keyword);
            }

            // 占位符是否都能解析
            string rendered = CardText.Format(face.Description, face);
            if (rendered.Contains("{"))
            {
                report.AppendLine($"[占位符] {where}：有占位符解析不到 → {rendered}");
            }

            // 机制关键词必须在代码里有实现
            if (face.Keywords != null)
            {
                foreach (string keyword in face.Keywords)
                {
                    if (!KeywordMechanics.IsKnown(keyword))
                    {
                        report.AppendLine(
                            $"[机制] {where}：keywords 列里的「{keyword}」没有对应的规则实现");
                    }
                }
            }

            // 效果串
            foreach (string token in SplitEffects(face.Effects))
            {
                string key = token;
                string[] args = new string[0];
                int colon = token.IndexOf(':');
                if (colon >= 0)
                {
                    key = token.Substring(0, colon).Trim();
                    args = token.Substring(colon + 1).Split(',');
                }

                if (string.Equals(key, "special", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length == 0 || !knownSpecials.Contains(args[0].Trim()))
                    {
                        report.AppendLine(
                            $"[特殊卡] {where}：special:{args[0].Trim()} 没有对应的实现类");
                    }

                    continue;
                }

                if (!CardEffects.IsKnownKey(key))
                {
                    report.AppendLine($"[效果] {where}：未知效果「{key}」（{face.Effects}）");
                }
            }
        }

        private static IEnumerable<string> SplitEffects(string effects)
        {
            if (string.IsNullOrWhiteSpace(effects))
            {
                yield break;
            }

            foreach (string token in effects.Split(';'))
            {
                string trimmed = token.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }
    }
}
