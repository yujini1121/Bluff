using UnityEngine;
using UnityEngine.UI;

namespace ProceduralUITool.Runtime
{
    /// <summary>
    /// Main component for applying UI effects to a Graphic element (Image, RawImage).
    /// This component modifies the UI element's material to render effects
    /// like rounded corners and borders, based on a ProceduralUIProfile.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Procedural UI Tool/Procedural UI Component")]
    [ExecuteInEditMode]
    public class ProceduralUIComponent : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Effect Configuration")]
        [Tooltip("Profile containing all effect parameters.")]
        public ProceduralUIProfile profile;

        [Header("Runtime Configuration")]
        [Tooltip("Automatically updates the effect when its profile changes.")]
        public bool autoUpdate = true;

        [Tooltip("Forces an update every frame. Can impact performance.")]
        public bool forceUpdateEveryFrame = false;

        [Header("Debug Information")]
        [Tooltip("Shows debug information in the console.")]
        public bool showDebugInfo = false;

        #endregion

        #region Private Fields

        // References to GameObject components.
        private Image _image;
        private RawImage _rawImage;
        private Graphic _targetGraphic;
        private RectTransform _rectTransform;

        // Material management for the effect.
        private Material _originalMaterial;
        private Material _effectMaterial;
        private bool _isInitialized = false;

        // State tracking for update optimization.
        private ProfileCache _profileCache = new ProfileCache();
        private bool _hasChanges = false;
        private Vector2 _lastRectSize = Vector2.zero;

        #endregion

        #region Shader Property IDs

        // Pre-caching shader property IDs for better performance.
        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _ColorId = Shader.PropertyToID("_Color");
        private static readonly int _CornerRadiiId = Shader.PropertyToID("_CornerRadii");
        private static readonly int _CornerOffsetsId = Shader.PropertyToID("_CornerOffsets");
        private static readonly int _BorderWidthId = Shader.PropertyToID("_BorderWidth");
        private static readonly int _BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int _UseIndividualCornersId = Shader.PropertyToID("_UseIndividualCorners");
        private static readonly int _UseIndividualOffsetsId = Shader.PropertyToID("_UseIndividualOffsets");
        private static readonly int _GlobalCornerOffsetId = Shader.PropertyToID("_GlobalCornerOffset");
        private static readonly int _AAId = Shader.PropertyToID("_AA");
        private static readonly int _RectSizeId = Shader.PropertyToID("_RectSize");
        
        // Shape properties.
        private static readonly int _ShapeTypeId = Shader.PropertyToID("_ShapeType");
        private static readonly int _ShapeVerticesId = Shader.PropertyToID("_ShapeVertices");
        private static readonly int _ShapeVerticesExtId = Shader.PropertyToID("_ShapeVerticesExt");
        private static readonly int _VertexCountId = Shader.PropertyToID("_VertexCount");

        // Properties for sharp edges.
        private static readonly int _EdgeSharpnessId = Shader.PropertyToID("_EdgeSharpness");
        private static readonly int _UsePixelPerfectEdgesId = Shader.PropertyToID("_UsePixelPerfectEdges");
        
        // Progress border properties.
        private static readonly int _UseProgressBorderId = Shader.PropertyToID("_UseProgressBorder");
        private static readonly int _ProgressValueId = Shader.PropertyToID("_ProgressValue");
        private static readonly int _ProgressStartAngleId = Shader.PropertyToID("_ProgressStartAngle");
        private static readonly int _ProgressDirectionId = Shader.PropertyToID("_ProgressDirection");

        #endregion

        #region Shader Names

        // Define shader names for different render pipelines.
        private const string SHADER_URP = "ProceduralUITool/RoundedBorder_URP";
        private const string SHADER_BUILTIN = "ProceduralUITool/RoundedBorder_Builtin";
        private const string SHADER_LEGACY = "ProceduralUITool/RoundedBorder";
        
        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called when the script is first loaded.
        /// </summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>
        /// Called when the object becomes active.
        /// </summary>
        private void OnEnable()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (_isInitialized)
            {
                ApplyProfile();
                if (_targetGraphic != null)
                {
                    _targetGraphic.SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Called when the object becomes inactive.
        /// </summary>
        private void OnDisable()
        {
            RestoreOriginalMaterial();
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        private void Update()
        {
            if (!_isInitialized) return;

            CheckRectSizeChange();

            if (forceUpdateEveryFrame || (autoUpdate && _hasChanges))
            {
                UpdateEffect();
            }
        }

        /// <summary>
        /// Called when the component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            CleanupMaterials();
        }

        /// <summary>
        /// Called when the RectTransform dimensions change.
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            if (enabled && gameObject.activeInHierarchy)
            {
                _hasChanges = true;
                if (_targetGraphic != null)
                {
                    _targetGraphic.SetAllDirty();
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Gets and caches references to required components.
        /// </summary>
        private void CacheComponents()
        {
            _image = GetComponent<Image>();
            _rawImage = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
            _targetGraphic = _image != null ? (Graphic)_image : (Graphic)_rawImage;

            if (_targetGraphic == null && showDebugInfo)
            {
                Debug.LogWarning($"ProceduralUIComponent on {gameObject.name} requires an Image or RawImage component.", this);
            }

            if (_rectTransform != null)
            {
                _lastRectSize = _rectTransform.rect.size;
            }
        }
        
        /// <summary>
        /// Initializes the component, creating the effect material.
        /// </summary>
        private void Initialize()
        {
            if (_targetGraphic == null)
            {
                CacheComponents();
                if (_targetGraphic == null) return;
            }

            _originalMaterial = _targetGraphic.material;

            if (CreateEffectMaterial())
            {
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Creates a new instance of the effect material.
        /// </summary>
        /// <returns>True if the material was created successfully, otherwise False.</returns>
        private bool CreateEffectMaterial()
        {
            string shaderName = GetAvailableShaderName();
            if (string.IsNullOrEmpty(shaderName)) return false;

            Shader effectShader = Shader.Find(shaderName);
            if (effectShader == null)
            {
                Debug.LogError($"Could not find a suitable shader for Procedural UI Tool.", this);
                return false;
            }

            _effectMaterial = new Material(effectShader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"ProceduralUI_Material_{GetInstanceID()}"
            };

            CopyMaterialProperties();
            _targetGraphic.material = _effectMaterial;

            if (showDebugInfo) Debug.Log($"Effect material created using shader: {shaderName}");
            return true;
        }

        /// <summary>
        /// Copies properties (like main texture) from the original material to the new effect material.
        /// </summary>
        private void CopyMaterialProperties()
        {
            if (_originalMaterial != null && _originalMaterial != _targetGraphic.defaultMaterial)
            {
                if (_originalMaterial.HasProperty(_MainTexId)) _effectMaterial.SetTexture(_MainTexId, _originalMaterial.GetTexture(_MainTexId));
                if (_originalMaterial.HasProperty(_ColorId)) _effectMaterial.SetColor(_ColorId, _originalMaterial.GetColor(_ColorId));
            }
            else if (_image != null && _image.sprite != null)
            {
                _effectMaterial.SetTexture(_MainTexId, _image.sprite.texture);
            }
        }

        /// <summary>
        /// Gets the name of the available shader based on the current render pipeline.
        /// </summary>
        /// <returns>The name of the shader to use.</returns>
        private string GetAvailableShaderName()
        {
            string[] shaderList = { SHADER_URP, SHADER_BUILTIN, SHADER_LEGACY, "UI/Default" };

            foreach (string shaderName in shaderList)
            {
                if (Shader.Find(shaderName) != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"Using shader: {shaderName} for render pipeline: {GetCurrentPipelineName()}");
                    }
                    return shaderName;
                }
            }

            Debug.LogWarning("No suitable Procedural UI Tool shader found. Using fallback UI/Default.", this);
            return "UI/Default";
        }
        
        /// <summary>
        /// Gets the name of the current render pipeline.
        /// </summary>
        /// <returns>The pipeline name.</returns>
        private string GetCurrentPipelineName()
        {
#if UNITY_PIPELINE_URP
            return "Universal Render Pipeline";
#elif UNITY_PIPELINE_HDRP
            return "High Definition Render Pipeline";
#else
            return "Built-in Render Pipeline";
#endif
        }

        #endregion

        #region Effect Management

        /// <summary>
        /// Applies the values from the assigned profile to the effect material.
        /// </summary>
        public void ApplyProfile()
        {
            if (!_isInitialized || profile == null || _effectMaterial == null) return;

            try
            {
                ApplyProfileToMaterial(profile, _effectMaterial);
                UpdateRectSize();

                if (_targetGraphic != null && _hasChanges)
                {
                    _targetGraphic.SetAllDirty();
                }

                CacheProfileValues();
                _hasChanges = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying profile: {e.Message}", this);
            }
        }

        /// <summary>
        /// Updates the effect if changes are detected.
        /// </summary>
        public void UpdateEffect()
        {
            if (!_isInitialized) return;
            CheckForProfileChanges();
            if (_hasChanges) ApplyProfile();
        }

        /// <summary>
        /// Forces a full update of the effect, regardless of whether changes were detected.
        /// </summary>
        public void ForceUpdate()
        {
            if (profile == null) return;
            if (!_isInitialized) Initialize();
            if (_isInitialized)
            {
                _hasChanges = true;
                ApplyProfile();
            }
        }

        /// <summary>
        /// Checks if the RectTransform's size has changed.
        /// </summary>
        private void CheckRectSizeChange()
        {
            if (_rectTransform != null)
            {
                if (Vector2.Distance(_rectTransform.rect.size, _lastRectSize) > 0.1f)
                {
                    _lastRectSize = _rectTransform.rect.size;
                    _hasChanges = true;
                }
            }
        }

        /// <summary>
        /// Sends the current RectTransform size to the shader.
        /// </summary>
        private void UpdateRectSize()
        {
            if (_effectMaterial == null || _rectTransform == null) return;
            Vector2 size = _rectTransform.rect.size;
            _effectMaterial.SetVector(_RectSizeId, new Vector4(size.x, size.y, 0, 0));
        }

        #endregion

        #region Material Application

        /// <summary>
        /// Converts a value from a unit (Pixels/Percent) to absolute pixels.
        /// </summary>
        private float ConvertValueToPixels(float value, ProceduralUIProfile.Unit unit, Vector2 rectSize, bool useMinDimension)
        {
            if (unit == ProceduralUIProfile.Unit.Pixels) return value;
            if (rectSize.x <= 0 || rectSize.y <= 0) return value; 

            float referenceDim = useMinDimension ? Mathf.Min(rectSize.x, rectSize.y) : Mathf.Max(rectSize.x, rectSize.y);
            return (value / 100f) * (referenceDim * 0.5f);
        }
        
        /// <summary>
        /// Applies all properties from the profile to the material.
        /// </summary>
        private void ApplyProfileToMaterial(ProceduralUIProfile prof, Material mat)
        {
            Vector2 rectSize = _rectTransform != null ? _rectTransform.rect.size : Vector2.one * 100;

            mat.SetFloat(_ShapeTypeId, (float)prof.shapeType);

            Vector2[] vertices = prof.GetShapeVertices();
            
            if (vertices != null && vertices.Length > 0)
            {
                mat.SetFloat(_VertexCountId, vertices.Length);

                Vector4 vertices1 = Vector4.zero;
                Vector4 vertices2 = Vector4.zero;

                for (int i = 0; i < Mathf.Min(vertices.Length, 8); i++)
                {
                    Vector2 vertex = vertices[i];
                    if (i < 2) 
                    {
                        if (i == 0) { vertices1.x = vertex.x; vertices1.y = vertex.y; }
                        else { vertices1.z = vertex.x; vertices1.w = vertex.y; }
                    }
                    else if (i < 4) 
                    {
                        if (i == 2) { vertices2.x = vertex.x; vertices2.y = vertex.y; }
                        else { vertices2.z = vertex.x; vertices2.w = vertex.y; }
                    }
                }
                mat.SetVector(_ShapeVerticesId, vertices1);
                mat.SetVector(_ShapeVerticesExtId, vertices2);
            }
            else
            {
                mat.SetFloat(_VertexCountId, 4f); 
                mat.SetVector(_ShapeVerticesId, Vector4.zero);
                mat.SetVector(_ShapeVerticesExtId, Vector4.zero);
            }
            
            Vector4 cornerRadiiRaw = prof.GetCornerRadii();
            Vector4 cornerRadiiPx = new Vector4(
                ConvertValueToPixels(cornerRadiiRaw.x, prof.cornerRadiusUnit, rectSize, true),
                ConvertValueToPixels(cornerRadiiRaw.y, prof.cornerRadiusUnit, rectSize, true),
                ConvertValueToPixels(cornerRadiiRaw.z, prof.cornerRadiusUnit, rectSize, true),
                ConvertValueToPixels(cornerRadiiRaw.w, prof.cornerRadiusUnit, rectSize, true)
            );
            mat.SetVector(_CornerRadiiId, cornerRadiiPx);
            
            float borderWidthPx = ConvertValueToPixels(prof.borderWidth, prof.borderWidthUnit, rectSize, true);
            mat.SetFloat(_BorderWidthId, borderWidthPx);
            
            mat.SetVector(_CornerOffsetsId, prof.GetCornerOffsets());
            mat.SetFloat(_UseIndividualCornersId, prof.useIndividualCorners ? 1f : 0f);
            mat.SetFloat(_UseIndividualOffsetsId, prof.useIndividualOffsets ? 1f : 0f);
            mat.SetFloat(_GlobalCornerOffsetId, prof.globalCornerOffset);
            mat.SetColor(_BorderColorId, prof.borderColor);
            mat.SetColor(_ColorId, prof.fillColor);
            
            mat.SetFloat(_AAId, prof.edgeSharpness * 0.3f);
            mat.SetFloat(_EdgeSharpnessId, prof.edgeSharpness);
            mat.SetFloat(_UsePixelPerfectEdgesId, prof.usePixelPerfectEdges ? 1f : 0f);
            
            mat.SetFloat(_UseProgressBorderId, prof.useProgressBorder ? 1f : 0f);
            mat.SetFloat(_ProgressValueId, Mathf.Clamp01(prof.progressValue));
            mat.SetFloat(_ProgressStartAngleId, prof.progressStartAngle);
            mat.SetFloat(_ProgressDirectionId, (float)prof.progressDirection);
        }
        
        #endregion

        #region Change Detection

        /// <summary>
        /// Compares the current profile with the cached version to detect changes.
        /// </summary>
        private void CheckForProfileChanges()
        {
            if (profile == null)
            {
                _hasChanges = false;
                return;
            }
            if (_profileCache.HasChanged(profile))
            {
                _hasChanges = true;
            }
        }

        /// <summary>
        /// Caches the current values of the profile.
        /// </summary>
        private void CacheProfileValues()
        {
            if (profile == null) return;
            _profileCache.Update(profile);
        }

        #endregion

        #region Public API
        
        /// <summary>
        /// Configures a procedural shape directly from code.
        /// </summary>
        public void ConfigureProceduralShape(ProceduralUIProfile.ShapeType shapeType, Vector2[] vertices = null, int starPoints = 5, float starInnerRatio = 0.5f)
        {
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ProceduralUIProfile>();
            }
            profile.SetShapeData(shapeType, vertices, starPoints, starInnerRatio);
            profile.globalCornerRadius = 0f;
            profile.useIndividualCorners = false;
            ForceUpdate();
        }

        /// <summary>
        /// Assigns a new profile to the component and forces an update.
        /// </summary>
        public void SetProfile(ProceduralUIProfile newProfile)
        {
            profile = newProfile;
            ForceUpdate();
        }
        
        /// <summary>
        /// Sets the progress value (0-1) for progress borders.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (profile != null)
            {
                profile.progressValue = Mathf.Clamp01(progress);
                _hasChanges = true;
                if (autoUpdate) UpdateEffect();
            }
        }

        #endregion
        
        #region Cleanup

        /// <summary>
        /// Restores the Graphic component's original material.
        /// </summary>
        private void RestoreOriginalMaterial()
        {
            if (_targetGraphic != null && _originalMaterial != null)
            {
                _targetGraphic.material = _originalMaterial;
            }
        }
        
        /// <summary>
        /// Destroys the effect material to prevent memory leaks.
        /// </summary>
        private void CleanupMaterials()
        {
            if (_effectMaterial != null)
            {
                if (Application.isPlaying) Destroy(_effectMaterial);
                else DestroyImmediate(_effectMaterial);
                _effectMaterial = null;
            }
        }

        #endregion

        #region Editor Support

#if UNITY_EDITOR
        /// <summary>
        /// Called in the editor when values are changed in the inspector.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                _hasChanges = true;
                return;
            }
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || gameObject == null) return;
                if (_targetGraphic == null) CacheComponents();
                if (_targetGraphic == null) return;
                
                string expectedShader = GetAvailableShaderName();
                if (_effectMaterial == null || _targetGraphic.material == _originalMaterial || (_effectMaterial.shader.name != expectedShader))
                {
                    Initialize();
                }
                if (_isInitialized) ForceUpdate();
            };
        }
#endif

        #endregion

        #region Helper Classes
    
        /// <summary>
        /// Internal class to cache profile values and detect changes.
        /// </summary>
        [System.Serializable]
        internal class ProfileCache
        {
            public Vector4 cornerRadii;
            public Vector4 cornerOffsets;
            public Color borderColor;
            public Color fillColor;
            public float borderWidth;
            public bool useIndividualCorners;
            public bool useIndividualOffsets;
            public float globalCornerOffset;
            public ProceduralUIProfile.Unit cornerRadiusUnit;
            public ProceduralUIProfile.Unit borderWidthUnit;
            public float edgeSharpness;
            public bool usePixelPerfectEdges;
            public bool useProgressBorder;
            public float progressValue;
            public float progressStartAngle;
            public ProceduralUIProfile.ProgressDirection progressDirection;

            public bool HasChanged(ProceduralUIProfile p)
            {
                if (p == null) return false;
                return cornerRadii != p.GetCornerRadii() || cornerRadiusUnit != p.cornerRadiusUnit ||
                       cornerOffsets != p.GetCornerOffsets() || borderColor != p.borderColor ||
                       fillColor != p.fillColor || !Mathf.Approximately(borderWidth, p.borderWidth) ||
                       useIndividualCorners != p.useIndividualCorners || useIndividualOffsets != p.useIndividualOffsets ||
                       !Mathf.Approximately(globalCornerOffset, p.globalCornerOffset) || borderWidthUnit != p.borderWidthUnit ||
                       !Mathf.Approximately(edgeSharpness, p.edgeSharpness) || usePixelPerfectEdges != p.usePixelPerfectEdges || 
                       useProgressBorder != p.useProgressBorder ||
                       !Mathf.Approximately(progressValue, p.progressValue) ||
                       !Mathf.Approximately(progressStartAngle, p.progressStartAngle) ||
                       progressDirection != p.progressDirection;
            }

            public void Update(ProceduralUIProfile p)
            {
                if (p == null) return;
                cornerRadii = p.GetCornerRadii();
                cornerOffsets = p.GetCornerOffsets();
                borderColor = p.borderColor;
                fillColor = p.fillColor;
                borderWidth = p.borderWidth;
                useIndividualCorners = p.useIndividualCorners;
                useIndividualOffsets = p.useIndividualOffsets;
                globalCornerOffset = p.globalCornerOffset;
                cornerRadiusUnit = p.cornerRadiusUnit;
                borderWidthUnit = p.borderWidthUnit;
                edgeSharpness = p.edgeSharpness;
                usePixelPerfectEdges = p.usePixelPerfectEdges;
                useProgressBorder = p.useProgressBorder;
                progressValue = p.progressValue;
                progressStartAngle = p.progressStartAngle;
                progressDirection = p.progressDirection;
            }
        }
        #endregion
    }
}