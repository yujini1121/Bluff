using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ProceduralUITool.Runtime;

namespace ProceduralUITool.Editor
{
    /// <summary>
    /// Handles rendering a UI effect preview in the editor.
    /// Implements IDisposable to ensure proper resource cleanup.
    /// </summary>
    public class PreviewRenderer : System.IDisposable
    {
        private RenderTexture _renderTexture;
        private Camera _previewCamera;
        private GameObject _previewRoot;
        private ProceduralUIComponent _effectComponent;

        /// <summary>
        /// Initializes a new instance of the preview renderer.
        /// </summary>
        public PreviewRenderer(int width = 128, int height = 128)
        {
            // Create a hidden scene to render the preview
            _previewRoot = new GameObject("ProceduralUI_PreviewRoot") { hideFlags = HideFlags.HideAndDontSave };

            var cameraObject = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(_previewRoot.transform);
            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = Color.clear;
            _previewCamera.orthographic = true;
            _previewCamera.enabled = false;

            var canvasObject = new GameObject("PreviewCanvas") { hideFlags = HideFlags.HideAndDontSave };
            canvasObject.transform.SetParent(_previewRoot.transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            var imageObject = new GameObject("PreviewImage") { hideFlags = HideFlags.HideAndDontSave };
            imageObject.transform.SetParent(canvas.transform);
            imageObject.AddComponent<Image>();
            _effectComponent = imageObject.AddComponent<ProceduralUIComponent>();

            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _previewCamera.targetTexture = _renderTexture;
        }

        /// <summary>
        /// Renders a preview of a given profile.
        /// </summary>
        public RenderTexture RenderPreview(ProceduralUIProfile profile)
        {
            if (profile == null) return null;
            
            _effectComponent.SetProfile(profile);
            _previewCamera.Render();
            return _renderTexture;
        }

        /// <summary>
        /// Releases all resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            if (_previewRoot != null) Object.DestroyImmediate(_previewRoot);
            if (_renderTexture != null) _renderTexture.Release();
        }
    }
}