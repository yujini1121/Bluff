using UnityEngine;
using UnityEngine.Events;

namespace Michsky.UI.Shift
{
    public class PressKeyEvent : MonoBehaviour
    {
        [Header("Key")]
        public KeyCode hotkey;
        public bool pressAnyKey;
        public bool invokeAtStart;

        [Header("Action")]
        public UnityEvent pressAction;

        void Start()
        {
            if (invokeAtStart)
                pressAction?.Invoke();
        }

        void Update()
        {
            if (pressAnyKey && Input.anyKeyDown) { pressAction?.Invoke(); }
            else if (Input.GetKeyDown(hotkey)) { pressAction?.Invoke(); }
        }
    }
}