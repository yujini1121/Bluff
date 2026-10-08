using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using ProceduralUITool.Runtime;

namespace ProceduralUITool.Editor
{
    /// <summary>
    /// Static utility class for managing ProceduralUIProfile presets in the editor.
    /// Handles creation, loading, and listing of preset asset files.
    /// </summary>
    public static class PresetManager
    {
        private const string DEFAULT_PRESET_DIRECTORY = "Assets/ProceduralUITool/Presets";

        /// <summary>
        /// Creates a new preset asset from a ProceduralUIProfile instance.
        /// </summary>
        public static bool CreatePresetFromProfile(ProceduralUIProfile profile, string path)
        {
            if (profile == null)
            {
                Debug.LogError("PresetManager: Cannot create preset from a null profile.");
                return false;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("PresetManager: An invalid path was provided for preset creation.");
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                var presetCopy = profile.Clone();
                presetCopy.name = Path.GetFileNameWithoutExtension(path);

                AssetDatabase.CreateAsset(presetCopy, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"PresetManager: Preset successfully created at {path}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PresetManager: Failed to create preset at {path}. Error: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Scans the entire project for ProceduralUIProfile assets and returns them in a list.
        /// </summary>
        public static List<PresetInfo> ListAllPresets()
        {
            var presets = new List<PresetInfo>();
            string[] guids = AssetDatabase.FindAssets("t:ProceduralUIProfile");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<ProceduralUIProfile>(path);

                if (profile != null)
                {
                    presets.Add(new PresetInfo
                    {
                        path = path,
                        displayName = profile.name,
                        fileName = Path.GetFileNameWithoutExtension(path)
                    });
                }
            }
            
            presets.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));
            return presets;
        }

        /// <summary>
        /// Loads a ProceduralUIProfile preset from a specific asset path.
        /// </summary>
        public static ProceduralUIProfile LoadPreset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("PresetManager: An invalid path was provided to load the preset.");
                return null;
            }

            try
            {
                var profile = AssetDatabase.LoadAssetAtPath<ProceduralUIProfile>(path);
                if (profile == null)
                {
                    Debug.LogError($"PresetManager: Could not load preset from {path}.");
                    return null;
                }
                return profile;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PresetManager: Failed to load preset from {path}. Error: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Simple data structure to hold key information about a preset.
        /// </summary>
        [System.Serializable]
        public class PresetInfo
        {
            public string path;
            public string displayName;
            public string fileName;
        }
    }
}