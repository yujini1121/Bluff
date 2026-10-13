using UnityEngine;
using UnityEditor;
using ProceduralUITool.Editor.Localization;

namespace ProceduralUITool.Editor
{

    public class LocalizationPreferences : EditorWindow
    {
        private const string MENU_PATH = "Window/Procedural UI Tool/Language Settings";
        
        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            GetWindow<LocalizationPreferences>("Language Settings").Show();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(SupportedLanguage newLanguage)
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(LocalizedGUI.Text("LANGUAGE"), EditorStyles.boldLabel);
            GUILayout.Space(10);
            LocalizedGUI.LanguageSelector();
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Language changes apply to all Procedural UI Tool windows and are saved automatically.", MessageType.Info);
        }
    }


    public static class ProceduralUIToolLocalizationSettings
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/Procedural UI Tool", SettingsScope.User)
            {
                label = "Procedural UI Tool - Language",
                guiHandler = (searchContext) =>
                {
                    EditorGUILayout.LabelField("Language Settings", EditorStyles.boldLabel);
                    LocalizedGUI.LanguageSelector();
                },
                keywords = new[] { "language", "localization", "idioma", "sprache", "语言", "procedural ui" }
            };
            return provider;
        }
    }
}