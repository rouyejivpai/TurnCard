using System.Text;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>
    /// buff 系统的无头冒烟测试：用 -executeMethod 在 batchmode 下跑。
    /// 只验证纯逻辑（叠层、消耗、伤害修正、回合衰减），不碰 UI。
    /// </summary>
    public static class BuffSmokeTest
    {
        public static void Run()
        {
            var log = new StringBuilder();
            int failed = 0;

            void Check(string label, bool ok, string detail)
            {
                log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label + "  " + detail);
                if (!ok)
                {
                    failed++;
                }
            }

            BattleContent content = Resources.Load<BattleContent>("BattleContent");
            Check("BattleContent 可加载", content != null, content == null ? "null" : "");
            if (content == null)
            {
                Debug.Log(log.ToString());
                return;
            }

            Check("Buffs 表导入", content.Buffs != null && content.Buffs.Length == 4,
                "数量=" + (content.Buffs == null ? -1 : content.Buffs.Length));

            var state = new BattleState();
            state.Setup(content.Cards, new[] { content.Enemies[0] });

            // 1) 易伤：受到伤害 +3/层，用完一层
            state.PlayerBuffs.Clear();
            state.ApplyBuff(state.PlayerBuffs, 1, 3, -1);
            int taken = BattleState.ApplyBuffModifiers(10, "damage_taken", state.PlayerBuffs, true);
            Check("易伤 3 层 → 10 变 19", taken == 19, "实际=" + taken);
            int stacksLeft = state.PlayerBuffs.Find(1)?.Stacks ?? 0;
            Check("易伤触发后掉一层", stacksLeft == 2, "剩余=" + stacksLeft);

            // 2) 力量：敌人 AttackBonus 来自 buff
            EnemyInstance enemy = state.Enemies[0];
            enemy.Buffs.Clear();
            state.ApplyBuff(enemy.Buffs, 2, 2, -1);
            Check("力量 2 层 → 攻击加成 2", enemy.AttackBonus == 2, "实际=" + enemy.AttackBonus);

            // 3) 虚弱：造成伤害 ×0.75
            state.PlayerBuffs.Clear();
            state.ApplyBuff(state.PlayerBuffs, 3, 1, 1);
            int dealt = BattleState.ApplyBuffModifiers(20, "damage_dealt", state.PlayerBuffs);
            Check("虚弱 → 20 变 15", dealt == 15, "实际=" + dealt);

            // 4) 回合衰减：turn 型 buff 掉剩余回合
            int before = state.PlayerBuffs.Find(3)?.Remaining ?? 0;
            state.PlayerBuffs.DecayByTrigger(TurnCard.Game.Data.BuffTrigger.TurnStart);
            bool gone = state.PlayerBuffs.Find(3) == null;
            Check("虚弱回合结束消失", gone, "之前剩余=" + before);

            // 5) 叠层上限与 refresh 规则
            state.PlayerBuffs.Clear();
            BuffDefinition vuln = content.Buffs[0];
            state.PlayerBuffs.Add(vuln, 200, -1);
            int capped = state.PlayerBuffs.Find(vuln.Id)?.Stacks ?? 0;
            Check("层数受 maxStacks 限制", capped == vuln.MaxStacks, "层数=" + capped);

            // 6) 敌人 Buff 清空
            enemy.Buffs.Clear();
            Check("敌人 buff 可清空", enemy.Buffs.Buffs.Count == 0, "");

            log.AppendLine(failed == 0 ? "== 全部通过 ==" : "== 失败 " + failed + " 项 ==");
            Debug.Log("[BuffSmokeTest]\n" + log);
        }
    }
}
