using System.Collections.Generic;
using Michsky.UI.Shift;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Update legacy serialized labels, including scenes already open during a script reload.
[InitializeOnLoad]
public static class BettingButtonLabelOrderEditor
{
    private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
    {
        { "Call ({0}) [C]", "[C] Call ({0})" },
        { "Call [C]", "[C] Call" },
        { "All in ({0}) [C]", "[C] All in ({0})" },
        { "Bet ({0}) [R]", "[R] Bet ({0})" },
        { "Raise ({0}) [R]", "[R] Raise ({0})" },
        { "All in ({0}) [R]", "[R] All in ({0})" },
        { "Fold [F]", "[F] Fold" }
    };

    static BettingButtonLabelOrderEditor()
    {
        EditorApplication.delayCall += ApplyToOpenScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ApplyToOpenScenes;
        };
        EditorSceneManager.sceneOpened += (scene, mode) => ApplyToOpenScenes();
    }

    private static void ApplyToOpenScenes()
    {
        foreach (var view in Object.FindObjectsOfType<GameplayView>(true))
        {
            bool changed = false;
            var serialized = new SerializedObject(view);
            foreach (string field in new[] { "callLabelFormat", "callNoChipsLabel", "callAllInLabelFormat",
                         "betLabelFormat", "raiseLabelFormat", "raiseAllInLabelFormat" })
            {
                var property = serialized.FindProperty(field);
                if (property != null && Labels.TryGetValue(property.stringValue, out string label))
                {
                    property.stringValue = label;
                    changed = true;
                }
            }
            serialized.ApplyModifiedProperties();
            foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!Labels.TryGetValue(text.text, out string label)) continue;
                Undo.RecordObject(text, "Update betting button label order");
                text.text = label;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            foreach (var button in view.GetComponentsInChildren<MainButton>(true))
            {
                if (!Labels.TryGetValue(button.buttonText, out string label)) continue;
                Undo.RecordObject(button, "Update betting button label order");
                button.buttonText = label;
                EditorUtility.SetDirty(button);
                changed = true;
            }
            if (changed && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}
