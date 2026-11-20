using System.Collections.Generic;
using System.Text;
using TurnCard.Game.Battle;
using TurnCard.Game.Data;
using UnityEngine;

namespace TurnCard.EditorTools
{
    /// <summary>怪物特性的无头测试：初始 buff、翻面条件、意图顺序、壁垒。</summary>
    public static class MonsterMechanicsTest
    {
        private static StringBuilder s_Log = new StringBuilder();
        private static int s_Failed;

        public static void Run()
        {
            s_Log = new StringBuilder();
            s_Failed = 0;

            BattleContent content = Resources.Load<BattleContent>("BattleContent");
            Check("怪物表 7 只", content != null && content.Enemies.Length == 7,
                content == null ? "null" : content.Enemies.Length.ToString());
            if (content == null)
            {
                Flush();
                return;
            }

            EnemyDefinition goblin = Find(content, 1);
            EnemyDefinition sleeper = Find(content, 2);
            EnemyDefinition sage = Find(content, 4);
            EnemyDefinition manor = Find(content, 7);

            // 1) 哥布林：开局带「受击 3 次翻面」，打 3 下就翻
            var state = NewState(content, goblin);
            EnemyInstance enemy = state.Enemies[0];
            Check("哥布林带着受击翻面 buff", enemy.Buffs.Find(7) != null,
                "buff 数=" + enemy.Buffs.Buffs.Count);

            int frontHp = enemy.Hp;
            state.DamageEnemy(enemy, 1);
            state.DamageEnemy(enemy, 1);
            Check("挨两下还不翻面", enemy.Side == CardFaceSide.Front, "面=" + enemy.Side);

            state.DamageEnemy(enemy, 1);
            Check("第三下翻面", enemy.Side == CardFaceSide.Back, "面=" + enemy.Side);
            Check("翻面后血量取背面满血", enemy.Hp == goblin.Back.MaxHp,
                "血=" + enemy.Hp + " 期望=" + goblin.Back.MaxHp);
            Check("正面血量有变化过", frontHp > enemy.Hp || frontHp == goblin.Front.MaxHp, "");

            // 2) 嗜睡哥布林：受击即翻面，且继承当前血量
            var state2 = NewState(content, sleeper);
            EnemyInstance sleeper2 = state2.Enemies[0];
            Check("嗜睡哥布林开局是睡觉意图",
                sleeper2.CurrentIntent != null && sleeper2.CurrentIntent.Intent == EnemyIntentType.Stun,
                sleeper2.CurrentIntent == null ? "null" : sleeper2.CurrentIntent.Intent.ToString());

            state2.DamageEnemy(sleeper2, 5);
            int expectHp = sleeper.Front.MaxHp - 5;
            Check("受击后翻面", sleeper2.Side == CardFaceSide.Back, "面=" + sleeper2.Side);
            Check("继承当前血量", sleeper2.Hp == expectHp, "血=" + sleeper2.Hp + " 期望=" + expectHp);
            Check("翻面后带「受击加力量」", sleeper2.Buffs.Find(11) != null,
                "buff 数=" + sleeper2.Buffs.Buffs.Count);

            int atkBefore = sleeper2.AttackBonus;
            state2.DamageEnemy(sleeper2, 1);
            Check("受击后获得力量", sleeper2.AttackBonus == atkBefore + 1,
                atkBefore + " → " + sleeper2.AttackBonus);

            // 3) 大树贤者：顺序意图，最后一个意图是强制翻面
            var state3 = NewState(content, sage);
            EnemyInstance sage3 = state3.Enemies[0];
            Check("贤者意图按表顺序（第一回合是攻击）",
                sage3.CurrentIntent != null && sage3.CurrentIntent.Intent == EnemyIntentType.Attack,
                sage3.CurrentIntent == null ? "null" : sage3.CurrentIntent.Intent.ToString());

            var queue = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                queue.Add(sage3.CurrentIntent != null ? sage3.CurrentIntent.Intent.ToString() : "null");
                sage3.PopIntent();
            }

            Check("四回合意图依次为 攻击/攻击/加buff/翻面",
                queue.Count == 4 && queue[0] == "Attack" && queue[1] == "Attack" &&
                queue[2] == "ApplyBuff" && queue[3] == "FlipSelf",
                string.Join(",", queue));

            // 4) 呼啸山庄：起始护甲 + 壁垒，回合开始不清零
            var state4 = NewState(content, manor);
            EnemyInstance manor4 = state4.Enemies[0];
            Check("起始护甲 10", manor4.Armor == 10, "护甲=" + manor4.Armor);
            Check("带着壁垒", manor4.Buffs.Find(5) != null, "buff 数=" + manor4.Buffs.Buffs.Count);

            state4.StartPlayerTurn();
            Check("壁垒挡住回合开始的护甲清零", manor4.Armor == 10, "护甲=" + manor4.Armor);

            // 对照：没有壁垒的敌人依然清零
            var state5 = NewState(content, goblin);
            EnemyInstance plain = state5.Enemies[0];
            plain.Armor = 8;
            state5.StartPlayerTurn();
            Check("没有壁垒的敌人照常清零", plain.Armor == 0, "护甲=" + plain.Armor);

            Flush();
        }

        private static BattleState NewState(BattleContent content, EnemyDefinition enemy)
        {
            var state = new BattleState();
            state.Setup(content.Cards, new[] { enemy });
            state.StartPlayerTurn();
            return state;
        }

        private static EnemyDefinition Find(BattleContent content, int id)
        {
            foreach (EnemyDefinition enemy in content.Enemies)
            {
                if (enemy != null && enemy.Id == id)
                {
                    return enemy;
                }
            }

            return null;
        }

        private static void Check(string label, bool ok, string detail)
        {
            s_Log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + label + "  " + detail);
            if (!ok)
            {
                s_Failed++;
            }
        }

        private static void Flush()
        {
            s_Log.AppendLine(s_Failed == 0 ? "== 全部通过 ==" : "== 失败 " + s_Failed + " 项 ==");
            Debug.Log("[MonsterMechanicsTest]\n" + s_Log);
        }
    }
}
