using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Event;

namespace TurnCard.Game
{
    /// <summary>
    /// 类型化事件总线。底层用 GameFramework 的 <see cref="IEventManager"/>，
    /// 外面包一层泛型门面，业务代码只关心「发什么事件、谁听」。
    ///
    /// 用法：
    /// <code>
    /// EventBus.Subscribe&lt;BattleFinishedEvent&gt;(OnBattleFinished);
    /// EventBus.Publish(new BattleFinishedEvent { Victory = true });
    /// EventBus.Unsubscribe&lt;BattleFinishedEvent&gt;(OnBattleFinished);
    /// </code>
    ///
    /// 约定：事件用不可变的 class 承载，不要用结构体（避免装箱与意外拷贝）。
    /// </summary>
    public static class EventBus
    {
        private sealed class TypedArgs<T> : GameEventArgs where T : class
        {
            public T Payload;

            public override int Id => EventIds.Of(typeof(T));

            public override void Clear()
            {
                Payload = null;
            }
        }

        private static readonly Dictionary<Type, EventHandler<GameEventArgs>> Dispatchers =
            new Dictionary<Type, EventHandler<GameEventArgs>>();

        private static IEventManager Manager
        {
            get
            {
                IEventManager manager = GameFrameworkEntry.GetModule<IEventManager>();
                if (manager == null)
                {
                    throw new InvalidOperationException("找不到 IEventManager，检查 GameEntry 是否初始化");
                }

                return manager;
            }
        }

        public static void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            int id = EventIds.Of(typeof(T));
            EventHandler<GameEventArgs> dispatcher = GetDispatcher<T>();
            Manager.Subscribe(id, dispatcher);
            TypedHandlers<T>.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            TypedHandlers<T>.Remove(handler);

            if (TypedHandlers<T>.Empty)
            {
                // 没人听了就把分发器也摘掉
                Manager.Unsubscribe(EventIds.Of(typeof(T)), GetDispatcher<T>());
            }
        }

        /// <summary>派发时的 sender（事件源），固定成一个静态标识，方便回调里识别。</summary>
        private static readonly object Sender = new object();

        /// <summary>立即派发（不走队列，避免要等一帧）。</summary>
        public static void Publish<T>(T payload) where T : class
        {
            var args = new TypedArgs<T> { Payload = payload };
            Manager.FireNow(Sender, args);
        }

        private static EventHandler<GameEventArgs> GetDispatcher<T>() where T : class
        {
            if (Dispatchers.TryGetValue(typeof(T), out EventHandler<GameEventArgs> existing))
            {
                return existing;
            }

            EventHandler<GameEventArgs> dispatcher = (sender, args) =>
            {
                if (args is TypedArgs<T> typed && typed.Payload != null)
                {
                    TypedHandlers<T>.Invoke(typed.Payload);
                }
            };

            Dispatchers[typeof(T)] = dispatcher;
            return dispatcher;
        }

        /// <summary>每种事件的订阅者列表。</summary>
        private static class TypedHandlers<T> where T : class
        {
            private static readonly List<Action<T>> Handlers = new List<Action<T>>();

            public static bool Empty => Handlers.Count == 0;

            public static void Add(Action<T> handler)
            {
                if (!Handlers.Contains(handler))
                {
                    Handlers.Add(handler);
                }
            }

            public static void Remove(Action<T> handler)
            {
                Handlers.Remove(handler);
            }

            public static void Invoke(T payload)
            {
                // 复制一份，允许回调里退订
                var snapshot = Handlers.ToArray();
                foreach (Action<T> handler in snapshot)
                {
                    handler(payload);
                }
            }
        }
    }

    /// <summary>事件类型 → 事件 id 的映射（GameFramework 的事件是 int id）。</summary>
    internal static class EventIds
    {
        private static readonly Dictionary<Type, int> Ids = new Dictionary<Type, int>();
        private static int s_Next = 1;

        public static int Of(Type type)
        {
            if (Ids.TryGetValue(type, out int id))
            {
                return id;
            }

            id = s_Next++;
            Ids[type] = id;
            return id;
        }
    }
}
