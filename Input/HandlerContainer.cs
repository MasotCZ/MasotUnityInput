using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using SD.Tools.Algorithmia.Heaps;
using Masot.Standard.Utility;

namespace Masot.Standard.Input
{
    delegate bool InputKeyCodeDelegate(KeyCode key);

    class InputContainerAddCommand<_K, _T> : ICommand
    {
        private readonly IInputContainer<_K> container;
        private readonly IHandlerData<_T> data;

        public InputContainerAddCommand(IInputContainer<_K> container, IHandlerData<_T> data)
        {
            this.container = container;
            this.data = data;
        }

        public void Execute()
        {
            container.Add(data);
        }
    }

    class InputContainerRemoveCommand<_K, _T> : ICommand
    {
        private readonly IInputContainer<_K> container;
        private readonly IHandlerData<_T> data;

        public InputContainerRemoveCommand(IInputContainer<_K> container, IHandlerData<_T> data)
        {
            this.container = container;
            this.data = data;
        }

        public void Execute()
        {
            container.Remove(data);
        }
    }

    interface IHandlerDataBase
    {
        public InputDefine Input { get; }
        int Priority { get; }
        bool Blocking { get; }

    }

    interface IHandlerData<_T> : IHandlerDataBase
    {
        InputDelegate<_T> Handler { get; }
        Type EventArgs { get; }
    }

    interface IHandlerData : IHandlerDataBase
    {
        InputDelegate Handler { get; }
    }

    abstract class HandlerDataBase : IHandlerDataBase
    {
        protected HandlerDataBase(InputDefine input, int priority, bool blocking)
        {
            Input = input;
            Priority = priority;
            Blocking = blocking;
        }

        public InputDefine Input { get; }
        public int Priority { get; }
        public bool Blocking { get; }
    }

    class HandlerData<_T> : HandlerDataBase, IHandlerData<_T>
    {
        public HandlerData(InputDefine input, int priority, bool blocking, InputDelegate<_T> handler, Type eventArgs) : base(input, priority, blocking)
        {
            Handler = handler;
            EventArgs = eventArgs;
        }

        public InputDelegate<_T> Handler { get; }
        public Type EventArgs { get; }
    }

    class HandlerData : HandlerDataBase, IHandlerData
    {
        public HandlerData(InputDefine input, int priority, bool blocking, InputDelegate handler) : base(input, priority, blocking)
        {
            Handler = handler;
        }

        public InputDelegate Handler { get; }
    }

    class PriorityKeyPair<_T>
    {
        public readonly int priority;
        public readonly List<InputDelegate<_T>> delegates;

        public PriorityKeyPair(int priority, List<InputDelegate<_T>> delegates)
        {
            this.priority = priority;
            this.delegates = delegates;
        }

        public static int Compare(PriorityKeyPair<_T> x, PriorityKeyPair<_T> y)
        {
            return x.priority - y.priority;
        }
    }

    class PriorityCollection<_T> : ICollection<_T> where _T : class
    {
        private ICollection<_T> enumerable;
        private BinaryHeap<_T> data;

        public PriorityCollection(BinaryHeap<_T> data)
        {
            this.data = data;
            this.enumerable = new List<_T>();

            int count = data.Count;
            for (int i = 0; i < count; i++)
            {
                enumerable.Add(data.ExtractRoot());
            }

            foreach (var item in enumerable)
            {
                data.Insert(item);
            }
        }

        public int Count => data.Count;

        public bool IsReadOnly => false;

        public _T Top()
        {
            return data.Root;
        }

        public void Add(_T item)
        {
            if (data.Contains(item))
            {
                return;
            }

            data.Insert(item);
            enumerable.Add(item);
        }

        public void Clear()
        {
            data.Clear();
        }

        //needs to be data so we can compare the priority and not the reference
        public bool Contains(_T item)
        {
            return data.Contains(item);
        }

        //hmm
        public void CopyTo(_T[] array, int arrayIndex)
        {
            foreach (var item in enumerable)
            {
                array[arrayIndex++] = item;
            }
        }

        public IEnumerator<_T> GetEnumerator()
        {
            return enumerable.GetEnumerator();
        }

        public bool Remove(_T item)
        {
            if (!data.Contains(item))
            {
                return false;
            }

            data.Remove(item);
            enumerable.Remove(item);
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class HandlersLow
    {
        //InputDelegate<_T>
        private readonly Dictionary<KeyCode, Dictionary<int, Dictionary<Type, List<object>>>> eventHandlers;

        public HandlersLow()
        {
            eventHandlers = new Dictionary<KeyCode, Dictionary<int, Dictionary<Type, List<object>>>>();
        }

        public void Add<_T>(IHandlerData<_T> item)
        {
            if (!eventHandlers.ContainsKey(item.Input.KeyCode))
            {
                eventHandlers.Add(item.Input.KeyCode, new Dictionary<int, Dictionary<Type, List<object>>>());
            }

            var delegates = eventHandlers[item.Input.KeyCode];

            if (!delegates.ContainsKey(item.Priority))
            {
                delegates.Add(item.Priority, new Dictionary<Type, List<object>>());
            }

            var types = delegates[item.Priority];

            if (!types.ContainsKey(item.EventArgs))
            {
                types.Add(item.EventArgs, new List<object>());
            }

            types[item.EventArgs].Add(item.Handler);
        }

        public bool Remove<_T>(IHandlerData<_T> item)
        {
            if (!eventHandlers.ContainsKey(item.Input.KeyCode))
            {
                return false;
            }

            var delegates = eventHandlers[item.Input.KeyCode];

            if (!delegates.ContainsKey(item.Priority))
            {
                return false;
            }

            var types = delegates[item.Priority];

            if (!types.ContainsKey(item.EventArgs))
            {
                types.Add(item.EventArgs, new List<object>());
            }

            var ret = types[item.EventArgs].Remove(item.Handler);

            if (types[item.EventArgs].Count == 0)
            {
                types.Remove(item.EventArgs);
            }

            if (delegates[item.Priority].Count == 0)
            {
                delegates.Remove(item.Priority);
            }

            if (eventHandlers[item.Input.KeyCode].Count == 0)
            {
                eventHandlers.Remove(item.Input.KeyCode);
            }

            return ret;
        }

        //lul
        public bool Contains<_T>(IHandlerData<_T> item)
        {
            return eventHandlers.ContainsKey(item.Input.KeyCode) &&
                eventHandlers[item.Input.KeyCode].ContainsKey(item.Priority) &&
                eventHandlers[item.Input.KeyCode][item.Priority].ContainsKey(item.EventArgs) &&
                eventHandlers[item.Input.KeyCode][item.Priority][item.EventArgs].Contains(item.Handler);
        }

        //yep, type type
        //refactor type checking is cringe
        public void Trigger<_T>(Lazy<_T> args)
        {
            //Debug.Log("---------------------------------");
            foreach (var priorityList in eventHandlers)
            {
                var maxKey = priorityList.Value.Keys.Max();
                //Debug.Log($"invoke: {handlerList.Key}, priority:{maxKey}|{handlerList.Value[maxKey]}");
                foreach (var typeList in priorityList.Value[maxKey])
                {
                    if (typeList.Key != typeof(_T))
                    {
                        continue;
                    }

                    foreach (var handler in typeList.Value)
                    {
                        ((InputDelegate<_T>)handler).Invoke(args.Value);
                    }
                }
            }
        }
    }

    interface IInputContainer<_K>
    {
        IEnumerable<_K> Keys { get; }
        void Add<_T>(IHandlerData<_T> item);
        bool Remove<_T>(IHandlerData<_T> item);
        bool Contains<_T>(IHandlerData<_T> item);
        void Iterate<_T>(Lazy<_T> args, IEnumerable<_K> filter) where _T : EventArgsBase;
        void Iterate<_T>(Lazy<_T> args) where _T : EventArgsBase;
    }
}