using SD.Tools.Algorithmia.Heaps;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Masot.Standard.Utility;

namespace Masot.Standard.Input
{
    public delegate void InputDelegate<_T>(_T argument);
    public delegate void InputDelegate();

    public enum GetKeyType
    {
        Release,
        Hold,
        Press
    }

    class InputDefineComparer : IEqualityComparer<InputDefine>
    {
        public bool Equals(InputDefine x, InputDefine y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(InputDefine obj)
        {
            return obj.GetHashCode();
        }
    }

    [Serializable]
    public class InputDefine
    {
        public static InputDefine Default = new InputDefine(KeyCode.None);

        public KeyCode KeyCode;
        public GetKeyType GetKeyType;

        public InputDefine(KeyCode keyCode, GetKeyType getKeyType = GetKeyType.Press)
        {
            KeyCode = keyCode;
            GetKeyType = getKeyType;
        }

        public override bool Equals(System.Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                var p = (InputDefine)obj;
                return (KeyCode == p.KeyCode) && (GetKeyType == p.GetKeyType);
            }
        }

        public override int GetHashCode()
        {
            int hashCode = -1220448001;
            hashCode = hashCode * -1521134295 + KeyCode.GetHashCode();
            hashCode = hashCode * -1521134295 + GetKeyType.GetHashCode();
            return hashCode;
        }
    }

    public enum MovementType
    {
        Up,
        Left,
        Down,
        Right
    }

    [Serializable]
    public class MovementInputDefine
    {
        public InputDefine input;
        public MovementType movementType;

        public MovementInputDefine(InputDefine input, MovementType movementType)
        {
            this.input = input;
            this.movementType = movementType;
        }
    }

    interface IKeyCodeEventData
    {
        int Priority { get; }
        void Trigger(InputDefine input);
    }

    internal class KeyCodeEventData<_T> : IKeyCodeEventData where _T : EventArgsBase
    {
        private readonly InputDefine input;
        private readonly InputDelegate<_T> callback;
        public int Priority { get; }
        private readonly bool blocking;

        public KeyCodeEventData(InputDefine input, InputDelegate<_T> callback, int priority, bool blocking)
        {
            this.input = input;
            this.callback = callback;
            Priority = priority;
            this.blocking = blocking;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                var p = (KeyCodeEventData<_T>)obj;
                return p.callback == callback && p.input.Equals(input) && p.blocking.Equals(blocking) && p.Priority.Equals(Priority);
            }
        }

        public override int GetHashCode()
        {
            int hashCode = 626592536;
            hashCode = hashCode * -1521134295 + Priority.GetHashCode();
            hashCode = hashCode * -1521134295 + blocking.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<InputDefine>.Default.GetHashCode(input);
            hashCode = hashCode * -1521134295 + EqualityComparer<InputDelegate<_T>>.Default.GetHashCode(callback);
            return hashCode;
        }

        public void Trigger(InputDefine input)
        {
            //2 ways to go about it
            //1 create default Argument and then give input to it
            //problem - input could be used in the constructor and hence the values in the argument could be invalid

            //var arg = new _T();
            //arg.Input = input;
            //callback.Invoke(arg);

            //2 create Argument at runtime via reflection and supply the argument
            //probly best , could be slow and have problems with arguments
            //could be slowing down the event system, probly not

            callback.Invoke(Activator.CreateInstance(typeof(_T), input) as _T);
        }
    }

    abstract class KeyCodeEventContainerBase : Dictionary<InputDefine, PriorityCollection<IKeyCodeEventData>>
    {
        private CommandBuffer commandBuffer;
        private Dictionary<Type, ICollection<(InputDefine, IKeyCodeEventData)>> clearFlags;

        protected KeyCodeEventContainerBase() : base(new InputDefineComparer())
        {
            commandBuffer = new CommandBuffer();
            clearFlags = new Dictionary<Type, ICollection<(InputDefine, IKeyCodeEventData)>>();
        }

        public void Add(InputDefine input, IKeyCodeEventData data)
        {
            if (!ContainsKey(input))
            {
                Add(input, new PriorityCollection<IKeyCodeEventData>(new BinaryHeap<IKeyCodeEventData>((a, b) => { return a.Priority - b.Priority; }, false)));
            }

            commandBuffer.Add(new CollectionAddCommand<IKeyCodeEventData>(this[input], data));
            commandBuffer.Add(new DictionaryRemoveCommand<InputDefine, PriorityCollection<IKeyCodeEventData>>(this, input, () => { return this[input].Count == 0; }));
            //this[input].Add(data);
        }

        public bool Remove(InputDefine input, IKeyCodeEventData data)
        {
            if (!ContainsKey(input))
            {
                return false;
            }

            commandBuffer.Add(new CollectionRemoveCommand<IKeyCodeEventData>(this[input], data));
            return true;
            //return this[input].Remove(data);
        }

        public void Clear<_T>(InputDelegate<_T> handler)
        {
            if (!clearFlags.ContainsKey(typeof(_T)))
            {
                return;
            }

            foreach (var item in clearFlags[typeof(_T)])
            {
                Remove(item.Item1, item.Item2);
            }

            //WARNING
            //can be a problem if theres an error with removing items
            //for example if they are protected or w/e
            //if the remove doesnt run it should have a method adding clear flags back
            clearFlags.Remove(typeof(_T));
        }

        public virtual bool Trigger(InputDefine input)
        {
            commandBuffer.Process();
            return true;
        }
    }

    class NonBlockingKeyCodeEventContainer : KeyCodeEventContainerBase
    {
        public override bool Trigger(InputDefine input)
        {
            base.Trigger(input);

            if (!ContainsKey(input))
            {
                return false;
            }

            foreach (var item in this[input])
            {
                item.Trigger(input);
            }

            return true;
        }
    }

    class BlockingKeyCodeEventContainer : KeyCodeEventContainerBase
    {
        public override bool Trigger(InputDefine input)
        {
            base.Trigger(input);

            if (!ContainsKey(input) || this[input].Count == 0)
            {
                return false;
            }

            this[input].Top().Trigger(input);

            return true;
        }
    }

    [CreateAssetMenu(fileName = "InputController", menuName = "Controllers/InputController")]
    public class InputController : ControllerScriptableObjectBase<InputController>
    {
        private static KeyCode GetMouseButton(int button) => button switch
        {
            0 => KeyCode.Mouse0,
            1 => KeyCode.Mouse1,
            2 => KeyCode.Mouse2,
            3 => KeyCode.Mouse3,
            4 => KeyCode.Mouse4,
            5 => KeyCode.Mouse5,
            6 => KeyCode.Mouse6,
            _ => KeyCode.None
        };

        private const bool _block_default = false;
        private const int _priority_default = 0;

        private readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        private readonly CommandBuffer<EventCommand> eventBuffer = new CommandBuffer<EventCommand>();

        //private Handlers eventContainer = new Handlers();
        private NonBlockingKeyCodeEventContainer nonBlockingInputEvents;
        private BlockingKeyCodeEventContainer blockingInputEvents;

        public EventSystem eventSystem = null;

        private GraphicRaycaster _screenRaycaster;
        private GraphicRaycaster _worldRaycaster;
        private Camera _mainCamera = null;

        public Camera MainCamera
        {
            get
            {
                if (_mainCamera == null)
                {
                    _mainCamera = Camera.main;
                }
                return _mainCamera;
            }
        }

        //---Mouse---
        public bool IsMouseOverGameWindow => !(0 > UnityEngine.Input.mousePosition.x || 0 > UnityEngine.Input.mousePosition.y || Screen.width < UnityEngine.Input.mousePosition.x || Screen.height < UnityEngine.Input.mousePosition.y);
        public Vector2 ScreenPositionBL
        {
            get { return Vector2.zero; }
        }

        public Vector2 ScreenPositionTL
        {
            get { return new Vector2(0, Screen.height); }
        }

        public Vector2 ScreenMiddle
        {
            get { return new Vector2(Screen.width / 2, Screen.height / 2); }
        }

        public Vector2 MouseScreenPosition
        {
            get { return UnityEngine.Input.mousePosition; }
        }

        //todo it works only while the camera is not tilted
        public Vector3 MouseWorldPosition
        {
            get { return MainCamera.ScreenToWorldPoint(new Vector3(MouseScreenPosition.x, MouseScreenPosition.y, -MainCamera.transform.position.z)); }
        }

        public Vector3 WorldToScreenMulti
        {
            get
            {
                return MainCamera.WorldToScreenPoint(MainCamera.transform.position + Vector3.one) -
                   MainCamera.WorldToScreenPoint(MainCamera.transform.position);
            }
        }

        private Vector2 HalfSize
        {
            get => new Vector2(Screen.width, Screen.height) / 2;
        }

        public Vector3 WorldToScreen(Vector3 world)
        {
            var half = HalfSize;

            return new Vector3(
                (world.x - MainCamera.transform.position.x) * WorldToScreenMulti.x + half.x,
                (world.y - MainCamera.transform.position.y) * WorldToScreenMulti.y + half.y,
                (world.z - MainCamera.transform.position.z) * WorldToScreenMulti.z
                );
        }

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            var half = HalfSize;
            var m = WorldToScreenMulti;
            var ok1 = MainCamera.ScreenToWorldPoint(MainCamera.transform.position + Vector3.one);
            var ok2 = MainCamera.ScreenToWorldPoint(MainCamera.transform.position);
            m = ok1 - ok2;

            return new Vector3(
                (screen.x - half.x) / m.x + MainCamera.transform.position.x,
                (screen.y - half.y) / m.y + MainCamera.transform.position.y,
                (screen.z) / m.z);
        }

        //---Mouse drag---
        public Vector2 MouseDrag
        {
            get
            {
                return new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y"));
            }
        }

        //---Mouse scroll---
        public float MouseScroll
        {
            get
            {
                return UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            }
        }

        public GraphicRaycaster ScreenRaycaster
        {
            get
            {
                if (_screenRaycaster is null)
                {
                    _screenRaycaster = GameObject.FindGameObjectWithTag("ScreenCanvas").GetComponent<Canvas>().GetComponent<GraphicRaycaster>();
                }

                return _screenRaycaster;
            }
            set => _screenRaycaster = value;
        }
        public GraphicRaycaster WorldRaycaster
        {
            get
            {
                if (_worldRaycaster is null)
                {
                    _worldRaycaster = GameObject.FindGameObjectWithTag("WorldCanvas").GetComponent<Canvas>().GetComponent<GraphicRaycaster>();
                }

                return _worldRaycaster;
            }
            set => _worldRaycaster = value;
        }

        public void Register<_EVENTARGS>(IEnumerable<InputDefine> input, InputDelegate<_EVENTARGS> handler,
            int priority = _priority_default, bool blocking = _block_default) where _EVENTARGS : EventArgsBase
        {
            foreach (var item in input)
            {
                Register(item, handler, priority, blocking);
            }
        }

        //---clear---
        public void Clear<_EVENTARGS>(InputDelegate<_EVENTARGS> handler) where _EVENTARGS : EventArgsBase
        {
            nonBlockingInputEvents.Clear(handler);
            blockingInputEvents.Clear(handler);
        }

        public void Clear(InputDelegate<EventArgsBase> handler)
        {
            Clear<EventArgsBase>(handler);
        }

        //---Handlers---
        public void Register<_EVENTARGS>(InputDefine input, InputDelegate<_EVENTARGS> handler,
            int priority = _priority_default, bool blocking = _block_default) where _EVENTARGS : EventArgsBase
        {
            var arg = new KeyCodeEventData<_EVENTARGS>(input, handler, priority, blocking);

            if (blocking)
            {
                blockingInputEvents.Add(input, arg);
            }
            else
            {
                nonBlockingInputEvents.Add(input, arg);
            }
        }

        public void Remove<_EVENTARGS>(IEnumerable<InputDefine> input, InputDelegate<_EVENTARGS> handler,
            int priority = _priority_default, bool blocking = _block_default) where _EVENTARGS : EventArgsBase
        {
            foreach (var item in input)
            {
                Remove(item, handler, priority, blocking);
            }
        }


        public void Remove<_EVENTARGS>(InputDefine input, InputDelegate<_EVENTARGS> handler,
            int priority = _priority_default, bool blocking = _block_default) where _EVENTARGS : EventArgsBase
        {
            var arg = new KeyCodeEventData<_EVENTARGS>(input, handler, priority, blocking);

            if (blocking)
            {
                blockingInputEvents?.Remove(input, arg);
            }
            else
            {
                nonBlockingInputEvents?.Remove(input, arg);
            }
        }

        //---Non-template handlers---
        public void Register(IEnumerable<InputDefine> input, InputDelegate<EventArgsBase> handler,
           int priority = _priority_default, bool blocking = _block_default)
        {
            foreach (var item in input)
            {
                Register(item, handler, priority, blocking);
            }
        }

        public void Register(InputDefine input, InputDelegate<EventArgsBase> handler,
           int priority = _priority_default, bool blocking = _block_default)
        {
            Register<EventArgsBase>(input, handler, priority, blocking);
        }

        public void Remove(IEnumerable<InputDefine> input, InputDelegate<EventArgsBase> handler,
           int priority = _priority_default, bool blocking = _block_default)
        {
            foreach (var item in input)
            {
                Remove(item, handler, priority, blocking);
            }
        }

        public void Remove(InputDefine input, InputDelegate<EventArgsBase> handler,
           int priority = _priority_default, bool blocking = _block_default)
        {
            Remove<EventArgsBase>(input, handler, priority, blocking);
        }

        public InputDelegate<MouseAxisDragEventArgs> MouseDragEventHandler;
        private void OnMouseDragEvent(Vector2 drag)
        {
            MouseDragEventHandler?.Invoke(new MouseAxisDragEventArgs(drag, MouseScreenPosition, MouseWorldPosition, InputDefine.Default));
        }

        public InputDelegate<MouseScrollEventArgs> MouseScrollEventHandler;
        private void OnMouseScrollEvent(float scroll)
        {
            MouseScrollEventHandler?.Invoke(new MouseScrollEventArgs(scroll, MouseScreenPosition, MouseWorldPosition, InputDefine.Default));
        }

        //--NEW--
        //---Object based input methods---

        public class ObjectInputArgument { }

        public void Register(IEnumerable<ObjectInputArgument> inputArguments, object key)
        {
            throw new NotImplementedException("TODO");
        }

        public void Remove(IEnumerable<ObjectInputArgument> inputArguments)
        {
            throw new NotImplementedException("TODO");
        }

        public void Remove(object key)
        {
            throw new NotImplementedException("TODO");

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true on frame it is pressed</returns>
        public bool Pressed(KeyCode key)
        {
            return UnityEngine.Input.GetKeyDown(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true if key is pressed</returns>
        public bool Held(KeyCode key)
        {
            return UnityEngine.Input.GetKey(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true if key was released on this frame</returns>
        public bool Released(KeyCode key)
        {
            return UnityEngine.Input.GetKeyUp(key);
        }

        //---Enable KeyStroke checker---
        protected override void Init()
        {
            base.Init();
            eventSystem = FindObjectOfType<EventSystem>();
            Debug.Assert(eventSystem != null, "Missing EventSystem in the scene");

            blockingInputEvents = new BlockingKeyCodeEventContainer();
            nonBlockingInputEvents = new NonBlockingKeyCodeEventContainer();
        }

        //---Disable KeyStroke checker---
        private void OnDisable()
        {
            eventSystem = null;

            blockingInputEvents = null;
            nonBlockingInputEvents = null;

            heldKeys.Clear();
            eventBuffer.Clear();
        }

        private bool TriggerEvent(InputDefine input)
        {
            if (blockingInputEvents.Trigger(input))
            {
                return true;
            }

            return nonBlockingInputEvents.Trigger(input);
        }

        private GetKeyType KeyUp(KeyCode k)
        {
            heldKeys.Remove(k);
            return GetKeyType.Release;
        }

        private GetKeyType KeyDown(KeyCode k)
        {
            heldKeys.Add(k);
            return GetKeyType.Press;
        }

        //Get events from key event detector
        private void KeyCodeEvent(EventData e)
        {
            GetKeyType type = e.type is EventType.KeyUp ?
                KeyUp(e.keyCode) : heldKeys.Contains(e.keyCode) ?
                GetKeyType.Hold :
                KeyDown(e.keyCode);

            if (type is GetKeyType.Hold)
            {
                //theres a delay from windows on the second key down if holding
                //it doenst cause double triggers, you are fine
                return;
            }

            //raycast on UI
            var worldRaycastResult = RaycastOntoWorld();
            var screenRaycastResult = RaycastOntoScreen();

            if ((worldRaycastResult is not null && worldRaycastResult.Count != 0) ||
                (screenRaycastResult is not null && screenRaycastResult.Count != 0))
            {
                //hit Ui element
                //cancel trigger invoke
                KeyDown(e.keyCode);
                return;
            }

            TriggerEvent(new InputDefine(e.keyCode, type));
        }

        public ICollection<RaycastResult> RaycastOntoWorld()
        {
            if (_screenRaycaster == null)
            {
                return null;
            }

            var ret = new List<RaycastResult>();
            _screenRaycaster.Raycast(new PointerEventData(eventSystem), ret);
            return ret;
        }

        public ICollection<RaycastResult> RaycastOntoScreen()
        {
            if (_worldRaycaster == null)
            {
                return null;
            }

            var ret = new List<RaycastResult>();
            _worldRaycaster.Raycast(new PointerEventData(eventSystem), ret);
            return ret;
        }

        private void MouseKeyEvent(EventData e)
        {
            GetKeyType type = e.type is EventType.MouseDown ? heldKeys.Contains(e.keyCode) ?
                GetKeyType.Hold :
                KeyDown(e.keyCode) :
                KeyUp(e.keyCode);

            if (type is GetKeyType.Hold)
            {
                return;
            }

            TriggerEvent(new InputDefine(e.keyCode, type));
        }

        private void MouseScrollEvent(EventData e)
        {
            //Debug.Log($"Scroll: {e.delta}");
            OnMouseScrollEvent(e.delta.y);
        }

        private void CheckForHeldKeys()
        {
            foreach (var item in heldKeys)
            {
                TriggerEvent(new InputDefine(item, GetKeyType.Hold));
            }
        }

        private void CheckForMouseDrag()
        {
            var drag = MouseDrag;
            if (drag != Vector2.zero)
            {
                //Debug.Log($"Mouse Dragging deez nuts: {drag}");
                OnMouseDragEvent(drag);
            }
        }

        public void ProcessEvents()
        {
            DetectNonEvents();
            eventBuffer.Process();
        }

        private class EventData
        {
            public readonly KeyCode keyCode;
            public readonly EventType type;
            public readonly Vector2 delta;

            public EventData(Event e)
            {
                keyCode = e.isMouse ? GetMouseButton(e.button) : e.keyCode;
                type = e.type;
                delta = e.delta;
            }

            public override string ToString()
            {
                return $"key:{keyCode}|type:{type}|delta:{delta}";
            }
        }

        private class EventCommand : ICommand
        {
            private readonly UnityAction<EventData> action;
            private readonly EventData e;
            public EventCommand(UnityAction<EventData> action, EventData e)
            {
                this.action = action;
                this.e = e;
            }

            public void Execute()
            {
                //Debug.Log($"event {e}");
                action?.Invoke(e);
            }
        }

        //todo make event more strict event sift
        //key has to have keyCode
        //mouse doesnt need keycode, uses event.button for keycode and mouse drag doesnt need either
        //scroll is always scroll
        public void AddGuiEvent(Event e)
        {
            AddEventCommand(GetEventCommand(e));
        }

        //event filter
        private EventCommand GetEventCommand(Event e) => e switch
        {
            { isKey: true, keyCode: not KeyCode.None } => new EventCommand(KeyCodeEvent, new EventData(e)),
            { isMouse: true, type: EventType.MouseDown or EventType.MouseUp } => new EventCommand(MouseKeyEvent, new EventData(e)),
            { isScrollWheel: true } => new EventCommand(MouseScrollEvent, new EventData(e)),
            _ => null
        };

        private void AddEventCommand(EventCommand cmd)
        {
            if (cmd is null)
            {
                return;
            }

            eventBuffer.Add(cmd);
        }

        private void DetectNonEvents()
        {
            if (!IsMouseOverGameWindow)
            {
                return;
            }

            CheckForMouseDrag();
            CheckForHeldKeys();
        }
    }
}