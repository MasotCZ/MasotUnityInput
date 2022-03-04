using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Masot.Standard.Input
{
    public class EventArgsBase : EventArgs
    {
        //make a method that check whetver anyone inheriting from this base modifies the input or uses it
        //or make an event that fires on input modify and make it abstract or virtual
        public InputDefine Input { get; }

        public EventArgsBase(InputDefine input)
        {
            Debug.Assert(input != null, "Input is null");

            Input = input;
        }
    }

    public class MousePositionEventArgs : EventArgsBase
    {
        public Vector2 ScreenPosition { get; }
        public Vector2 WorldPosition { get; }

        public MousePositionEventArgs(Vector2 screenPosition, Vector2 worldPosition, InputDefine input) : base(input)
        {
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
        }

        public MousePositionEventArgs(InputDefine input) : this(InputController.Instance.MouseScreenPosition, InputController.Instance.MouseWorldPosition, input)
        {
        }
    }

    public abstract class MouseGraphicsRaycastEventArgsBase : MousePositionEventArgs
    {
        protected MouseGraphicsRaycastEventArgsBase(Vector2 screenPosition, Vector2 worldPosition, InputDefine input, EventSystem eventSystem, GraphicRaycaster raycaster)
            : base(screenPosition, worldPosition, input)
        {
            Hits = new List<RaycastResult>();
            PointerEventData = new PointerEventData(eventSystem);
            PointerEventData.position = screenPosition;
            raycaster.Raycast(PointerEventData, Hits);

            if (Hits.Count == 0)
            {
                DidHit = false;
            }
        }

        public PointerEventData PointerEventData { get; }
        public List<RaycastResult> Hits { get; }
        public bool DidHit { get; } = true;
    }

    public class MouseScreenGraphicsRaycastEventArgs : MouseGraphicsRaycastEventArgsBase
    {
        public MouseScreenGraphicsRaycastEventArgs(Vector2 screenPosition, Vector2 worldPosition, InputDefine input)
            : base(screenPosition, worldPosition, input, InputController.Instance.eventSystem, InputController.Instance.ScreenRaycaster) { }

        public MouseScreenGraphicsRaycastEventArgs(InputDefine input)
            : base(InputController.Instance.MouseScreenPosition, InputController.Instance.MouseWorldPosition, input, InputController.Instance.eventSystem, InputController.Instance.ScreenRaycaster) { }
    }

    public class MouseWorldGraphicsRaycastEventArgs : MouseGraphicsRaycastEventArgsBase
    {
        public MouseWorldGraphicsRaycastEventArgs(Vector2 screenPosition, Vector2 worldPosition, InputDefine input)
            : base(screenPosition, worldPosition, input, InputController.Instance.eventSystem, InputController.Instance.WorldRaycaster) { }

        public MouseWorldGraphicsRaycastEventArgs(InputDefine input)
            : base(InputController.Instance.MouseScreenPosition, InputController.Instance.MouseWorldPosition, input, InputController.Instance.eventSystem, InputController.Instance.WorldRaycaster) { }
    }

    public abstract class MouseRaycastBase : MousePositionEventArgs
    {
        public bool DidHit => DidRayHit();

        public MouseRaycastBase(InputDefine input) : base(input)
        {
        }

        public MouseRaycastBase(Vector2 screenPosition, Vector2 worldPosition, InputDefine input) : base(screenPosition, worldPosition, input)
        {
        }

        protected abstract bool DidRayHit();
    }

    public class MouseRaycast3DEventArgs : MouseRaycastBase
    {
        private RaycastHit _hit;
        public RaycastHit Hit => _hit;

        public MouseRaycast3DEventArgs(Camera camera, Vector2 screenPosition, Vector2 worldPosition, InputDefine input, float distance = Mathf.Infinity, int layer = -1)
            : base(screenPosition, worldPosition, input)
        {
            var ray = camera.ScreenPointToRay(screenPosition);
            if (layer != -1)
            {
                Physics.Raycast(ray.origin, ray.direction, out _hit, distance, layer);
            }
            else
            {
                Physics.Raycast(ray.origin, ray.direction, out _hit, distance);
            }
        }

        public MouseRaycast3DEventArgs(InputDefine input, float distance = Mathf.Infinity, int layer = -1)
            : this(InputController.Instance.MainCamera, InputController.Instance.MouseScreenPosition, InputController.Instance.MouseScreenPosition, input, distance, layer) { }

        public MouseRaycast3DEventArgs(InputDefine input)
            : this(input, Mathf.Infinity, -1)
        {
        }

        protected override bool DidRayHit()
        {
            return Hit.collider != null;
        }
    }

    public class MouseRaycast2DEventArgs : MouseRaycastBase
    {
        public RaycastHit2D Hit { get; }

        public MouseRaycast2DEventArgs(Camera camera, Vector2 screenPosition, Vector2 worldPosition, InputDefine input, float distance = Mathf.Infinity, int layer = -1)
            : base(screenPosition, worldPosition, input)
        {
            var ray = camera.ScreenPointToRay(screenPosition);
            if (layer != -1)
            {
                Hit = Physics2D.GetRayIntersection(ray, distance, layer);
            }
            else
            {
                Hit = Physics2D.GetRayIntersection(ray, distance);
            }
        }

        public MouseRaycast2DEventArgs(InputDefine input, float distance = Mathf.Infinity, int layer = -1)
            : this(InputController.Instance.MainCamera, InputController.Instance.MouseScreenPosition, InputController.Instance.MouseScreenPosition, input, distance, layer) { }

        public MouseRaycast2DEventArgs(InputDefine input)
            : this(input, Mathf.Infinity, -1)
        {
        }

        protected override bool DidRayHit()
        {
            return Hit.collider != null;
        }
    }

    public class MouseScrollEventArgs : MousePositionEventArgs
    {
        public float ScrollValue { get; }

        public MouseScrollEventArgs(float scrollValue, Vector2 screenPosition, Vector2 worldPosition, InputDefine input) : base(screenPosition, worldPosition, input)
        {
            ScrollValue = scrollValue;
        }

        public MouseScrollEventArgs(InputDefine input)
            : this(InputController.Instance.MouseScroll, InputController.Instance.MouseScreenPosition, InputController.Instance.MouseWorldPosition, input)
        {

        }
    }

    public class MouseAxisDragEventArgs : MousePositionEventArgs
    {
        public Vector2 Drag { get; }

        public MouseAxisDragEventArgs(Vector2 drag, Vector2 screenPosition, Vector2 worldPosition, InputDefine input) : base(screenPosition, worldPosition, input)
        {
            Drag = drag;
        }

        public MouseAxisDragEventArgs(InputDefine input)
            : this(InputController.Instance.MouseDrag, InputController.Instance.MouseScreenPosition, InputController.Instance.MouseWorldPosition, input)
        {
        }
    }
}
