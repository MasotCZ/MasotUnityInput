using UnityEngine;

namespace Masot.Standard.Input
{
    public class KeyEventDetector : MonoBehaviour
    {
        InputController input;

        public bool fixedupdate = false;
        public bool update = false;
        public bool lateUpdate = false;
        public bool onGui = false;

#if DEBUG
        [Header("Debug")]
        public bool debug = false;
#endif

        private void OnEnable()
        {
            input = InputController.Instance;
        }

        private void OnDisable()
        {
            input = null;
        }

        private void FixedUpdate()
        {
            if (!fixedupdate)
            {
                return;
            }

            ProcessEvents();
        }
        private void LateUpdate()
        {
            if (!lateUpdate)
            {
                return;
            }

            ProcessEvents();
        }

        private void Update()
        {
            if (!update)
            {
                return;
            }

            ProcessEvents();
        }


        void OnGUI()
        {
            AddEvent(Event.current);
#if DEBUG
            if (debug && Event.current.keyCode != KeyCode.None)
            {
                Debug.Log($"event: {Event.current.type}|{Event.current.keyCode}");
            }
#endif
            if (!onGui)
            {
                return;
            }

            ProcessEvents();
        }

        //todo could be a problem with pressing multiple times every frame
        //exploit with scripts
        //clicking = faster than holding
        //if you click more than once every frame
        // Detects if the shift key was pressed
        private void AddEvent(Event inputEvent)
        {
            input.AddGuiEvent(inputEvent);
        }

        private void ProcessEvents()
        {
            input.ProcessEvents();
        }
    }
}