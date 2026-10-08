using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ProceduralUITool.Runtime;

namespace ProceduralUITool.Editor
{
    /// <summary>
    /// Manages the editor window for viewing, applying, and managing ProceduralUIProfile presets.
    /// </summary>
    public class PresetManagerWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "Preset Manager";
        private const string MENU_PATH = "Window/Procedural UI Tool/Preset Manager";

        private Vector2 _scrollPosition;
        private List<PresetManager.PresetInfo> _presetList;
        private int _selectedPresetIndex = -1;
        private ProceduralUIProfile _previewProfile;
        private GUIStyle _presetButtonStyle, _selectedPresetStyle;
        private bool _stylesInitialized = false;

        public System.Action<ProceduralUIProfile> OnPresetSelected;

        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            GetWindow<PresetManagerWindow>(WINDOW_TITLE).Show();
        }

        private void OnEnable()
        {
            RefreshPresetList();
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawHeader();
            DrawPresetList();
            DrawActionButtons();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            _presetButtonStyle = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 5, 5) };
            _selectedPresetStyle = new GUIStyle(_presetButtonStyle) { normal = { background = EditorStyles.miniButton.active.background } };
            _stylesInitialized = true;
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Procedural UI Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh"))
            {
                RefreshPresetList();
            }
            EditorGUILayout.Space(5);
        }

        private void DrawPresetList()
        {
            if (_presetList == null || _presetList.Count == 0)
            {
                EditorGUILayout.HelpBox("No presets found.", MessageType.Info);
                return;
            }

            using (var scrollScope = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scrollScope.scrollPosition;
                for (int i = 0; i < _presetList.Count; i++)
                {
                    var preset = _presetList[i];
                    GUIStyle buttonStyle = i == _selectedPresetIndex ? _selectedPresetStyle : _presetButtonStyle;

                    if (GUILayout.Button(preset.displayName, buttonStyle))
                    {
                        SelectPreset(i);
                    }
                }
            }
        }
        
        private void DrawActionButtons()
        {
            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(_selectedPresetIndex == -1);
            if (GUILayout.Button("Apply to Selected Object"))
            {
                ApplySelectedToSceneObjects();
            }
             if (GUILayout.Button("Load in Main Editor"))
            {
                LoadPresetInMainWindow(_presetList[_selectedPresetIndex]);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void RefreshPresetList()
        {
            _presetList = PresetManager.ListAllPresets();
            _selectedPresetIndex = -1;
            _previewProfile = null;
        }

        private void SelectPreset(int index)
        {
            if (index < 0 || index >= _presetList.Count) return;
            _selectedPresetIndex = index;
            _previewProfile = PresetManager.LoadPreset(_presetList[index].path);
        }

        private void ApplyPreset(PresetManager.PresetInfo presetInfo)
        {
            var profile = PresetManager.LoadPreset(presetInfo.path);
            if (profile == null) return;

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select one or more GameObjects to apply the preset.", "OK");
                return;
            }
            
            foreach (GameObject obj in selectedObjects)
            {
                if(obj.GetComponent<UnityEngine.UI.Image>() == null && obj.GetComponent<UnityEngine.UI.RawImage>() == null) continue;

                var effectComponent = obj.GetComponent<ProceduralUIComponent>() ?? Undo.AddComponent<ProceduralUIComponent>(obj);
                Undo.RecordObject(effectComponent, "Apply UI Preset");
                effectComponent.SetProfile(profile);
            }
        }
        
        private void LoadPresetInMainWindow(PresetManager.PresetInfo presetInfo)
        {
            var profile = PresetManager.LoadPreset(presetInfo.path);
            if (profile != null)
            {
                OnPresetSelected?.Invoke(profile);
                var mainWindow = GetWindow<ProceduralUIToolWindow>(null, false);
                if (mainWindow != null)
                {
                    mainWindow.Focus();
                }
            }
        }

        private void ApplySelectedToSceneObjects()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presetList.Count)
            {
                EditorUtility.DisplayDialog("No Preset Selected", "Please select a preset first.", "OK");
                return;
            }
            ApplyPreset(_presetList[_selectedPresetIndex]);
        }
    }
}