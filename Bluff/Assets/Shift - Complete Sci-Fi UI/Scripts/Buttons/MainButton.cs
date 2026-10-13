using UnityEngine;
using TMPro;

namespace Michsky.UI.Shift
{
    [ExecuteInEditMode]
    public class MainButton : MonoBehaviour
    {
        [Header("Settings")]
        public string buttonText = "My Title";
        public bool useCustomText = false;

        [Header("Resources")]
        public TextMeshProUGUI normalText;
        public TextMeshProUGUI highlightedText;
        public TextMeshProUGUI pressedText;

        void Update()
        {
            if (useCustomText == false)
            {
                if (normalText != null && normalText.text != buttonText) { normalText.text = buttonText; }
                if (highlightedText != null && highlightedText.text != buttonText) { highlightedText.text = buttonText; }
                if (pressedText != null && pressedText.text != buttonText) { pressedText.text = buttonText; }
            }
        }
    }
}