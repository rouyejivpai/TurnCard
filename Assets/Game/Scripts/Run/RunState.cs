using System.Collections.Generic;
using TurnCard.Game.Data;

namespace TurnCard.Game.Run
{
    public enum MapNodeType
    {
        Battle = 0,
        Elite = 1,
        Rest = 2,
        Shop = 3,
        Boss = 4,
    }

    public sealed class MapNode
    {
        public MapNode(MapNodeType type, string label)
        {
            Type = type;
            Label = label;
        }

        public MapNodeType Type { get; }
        public string Label { get; }
        public bool Cleared { get; set; }
    }

    /// <summary>
    /// 一局游戏（一次爬塔）的全局状态。程序流程之间靠它传递数据，
    /// 界面只读不写，改状态一律走这里的方法。
    /// </summary>
    public sealed class RunState
    {
        public const int StartingHp = 80;
        public const int StartingGold = 0;

        private static RunState s_Current;

        public static RunState Current
        {
            get
            {
                if (s_Current == null)
                {
                    s_Current = NewRun();
                }

                return s_Current;
            }
        }

        public int MaxHp { get; private set; } = StartingHp;
        public int Hp { get; private set; } = StartingHp;
        public int Gold { get; private set; } = StartingGold;

        /// <summary>牌组（卡牌 id 列表）。</summary>
        public List<int> Deck { get; } = new List<int>();

        /// <summary>地图节点，线性推进。</summary>
        public List<MapNode> Map { get; } = new List<MapNode>();

        /// <summary>当前所在节点下标；-1 表示还没开始。</summary>
        public int NodeIndex { get; private set; } = -1;

        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }
        public bool IsOver => IsVictory || IsDefeat;

        /// <summary>生命变化（当前值）。</summary>
        public event System.Action<int> HpChanged;

        /// <summary>金币变化（当前值）。</summary>
        public event System.Action<int> GoldChanged;

        /// <summary>牌组变化（增删牌）。</summary>
        public event System.Action DeckChanged;

        public MapNode CurrentNode =>
            NodeIndex >= 0 && NodeIndex < Map.Count ? Map[NodeIndex] : null;

        /// <summary>开一局新的，返回新状态。</summary>
        public static RunState NewRun()
        {
            var state = new RunState();
            state.BuildStarterDeck();
            state.BuildMap();
            s_Current = state;
            return state;
        }

        private void BuildStarterDeck()
        {
            Deck.Clear();

            // 初始牌组来自 DataTables/StarterDeck.xlsx（id=卡牌 id，count=张数）
            var content = UnityEngine.Resources.Load<BattleContent>("BattleContent");
            if (content != null && content.StarterDeck != null)
            {
                foreach (StarterDeckEntry entry in content.StarterDeck)
                {
                    if (entry == null || entry.Count <= 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < entry.Count; i++)
                    {
                        Deck.Add(entry.CardId);
                    }
                }
            }

            // 表没配置时退回一套保底牌组，保证游戏能开起来
            if (Deck.Count == 0)
            {
                int[] fallback = { 1, 1, 1, 2, 2, 5, 5, 7, 7, 9 };
                foreach (int id in fallback)
                {
                    Deck.Add(id);
                }
            }
        }

        private void BuildMap()
        {
            Map.Clear();
            Map.Add(new MapNode(MapNodeType.Battle, "第 1 战"));
            Map.Add(new MapNode(MapNodeType.Battle, "第 2 战"));
            Map.Add(new MapNode(MapNodeType.Rest, "营地"));
            Map.Add(new MapNode(MapNodeType.Battle, "第 3 战"));
            Map.Add(new MapNode(MapNodeType.Shop, "商店"));
            Map.Add(new MapNode(MapNodeType.Boss, "首领"));
            NodeIndex = -1;
        }

        // ---------------- 路线：每步随机抽 1~3 个节点 ----------------

        private readonly List<MapNode> m_RouteOptions = new List<MapNode>();

        /// <summary>当前这一步可以选的节点（1~3 个）。</summary>
        public IReadOnlyList<MapNode> RouteOptions => m_RouteOptions;

        /// <summary>已经走过的节点数。</summary>
        public int RouteStep { get; private set; }

        /// <summary>重新抽一次路线候选。</summary>
        public void RollRouteOptions()
        {
            int count = UnityEngine.Random.Range(1, 4);   // 1、2、3
            m_RouteOptions.Clear();

            // 同一轮里不重复类型：先把候选池洗一遍，取前 count 个
            var pool = new List<MapNodeType>
            {
                MapNodeType.Battle,
                MapNodeType.Battle,      // 普通怪权重高一点
                MapNodeType.Elite,
                MapNodeType.Rest,
                MapNodeType.Shop,
            };

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pick = UnityEngine.Random.Range(0, pool.Count);
                MapNodeType type = pool[pick];
                pool.RemoveAt(pick);
                m_RouteOptions.Add(new MapNode(type, LabelFor(type)));
            }
        }

        /// <summary>选定第 index 个节点，返回它并进入下一步。</summary>
        public MapNode ChooseRoute(int index)
        {
            if (index < 0 || index >= m_RouteOptions.Count)
            {
                return null;
            }

            MapNode node = m_RouteOptions[index];
            m_RouteOptions.Clear();
            RouteStep++;
            return node;
        }

        public static string LabelFor(MapNodeType type)
        {
            switch (type)
            {
                case MapNodeType.Elite:
                    return "精英战斗";
                case MapNodeType.Rest:
                    return "火堆";
                case MapNodeType.Shop:
                    return "商店";
                case MapNodeType.Boss:
                    return "首领";
                default:
                    return "普通战斗";
            }
        }

        /// <summary>火堆回血：回复上限的 30%。</summary>
        public int RestHeal()
        {
            int amount = System.Math.Max(1, MaxHp * 3 / 10);
            int before = Hp;
            Hp = System.Math.Min(MaxHp, Hp + amount);
            int healed = Hp - before;
            if (healed > 0)
            {
                HpChanged?.Invoke(Hp);
            }

            return healed;
        }

        /// <summary>推进到下一个节点，返回该节点；没有更多节点时返回 null。</summary>
        public MapNode Advance()
        {
            if (NodeIndex + 1 >= Map.Count)
            {
                return null;
            }

            NodeIndex++;
            return Map[NodeIndex];
        }

        public void MarkCleared()
        {
            if (CurrentNode != null)
            {
                CurrentNode.Cleared = true;
            }
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Hp = System.Math.Max(0, Hp - amount);
            HpChanged?.Invoke(Hp);
            if (Hp <= 0)
            {
                IsDefeat = true;
            }
        }

        /// <summary>直接设定生命（战斗结算后回写用）。</summary>
        public void SetHp(int value)
        {
            Hp = System.Math.Max(0, System.Math.Min(MaxHp, value));
            HpChanged?.Invoke(Hp);
            if (Hp <= 0)
            {
                IsDefeat = true;
            }
        }

        public void Heal(int amount)
        {
            if (amount > 0)
            {
                Hp = System.Math.Min(MaxHp, Hp + amount);
            }

            HpChanged?.Invoke(Hp);
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                Gold += amount;
            }

            GoldChanged?.Invoke(Gold);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void AddCard(int cardId)
        {
            Deck.Add(cardId);
            DeckChanged?.Invoke();
        }

        public bool RemoveCard(int cardId)
        {
            bool removed = Deck.Remove(cardId);
            if (removed)
            {
                DeckChanged?.Invoke();
            }

            return removed;
        }

        public void MarkVictory()
        {
            IsVictory = true;
        }

        /// <summary>把牌组 id 列表换成定义（找不到的 id 会被跳过）。</summary>
        public CardDefinition[] ResolveDeck(BattleContent content)
        {
            var lookup = new Dictionary<int, CardDefinition>();
            foreach (CardDefinition card in content.Cards)
            {
                lookup[card.Id] = card;
            }

            var result = new List<CardDefinition>(Deck.Count);
            foreach (int id in Deck)
            {
                if (lookup.TryGetValue(id, out CardDefinition definition))
                {
                    result.Add(definition);
                }
            }

            return result.ToArray();
        }
    }
}
