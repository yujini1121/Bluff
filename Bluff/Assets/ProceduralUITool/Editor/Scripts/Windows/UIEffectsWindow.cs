using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ProceduralUITool.Editor.Localization;

namespace ProceduralUITool.Editor
{
    /// <summary>
    /// The main editor window for creating and managing procedural UI effects.
    /// Provides a centralized interface for designing, previewing and applying profiles.
    /// </summary>
    public class ProceduralUIToolWindow : EditorWindow
    {
        // --- Window configuration constants ---
        private const string WINDOW_TITLE = "Procedural UI Tool";
        private const string MENU_PATH = "Window/Procedural UI Tool/Effects Window";
        private const float MIN_WINDOW_WIDTH = 380f;
        private const float MIN_WINDOW_HEIGHT = 500f;
        
        // --- Shader paths used by the tool ---
        private const string SHADER_URP = "ProceduralUITool/RoundedBorder_URP";
        private const string SHADER_BUILTIN = "ProceduralUITool/RoundedBorder_Builtin";
        private const string SHADER_LEGACY = "ProceduralUITool/RoundedBorder";
        
        // --- Profile Management ---
        // Temporary profile used before applying changes
        private Runtime.ProceduralUIProfile _workingProfile;
        private SerializedObject _serializedProfile;
        
        // --- Live Preview System ---
        private bool _previewEnabled = false;
        private Runtime.ProceduralUIComponent _previewTarget;
        private Runtime.ProceduralUIProfile _originalPreviewProfile;
        
        // --- Fields to preserve the original state of the Image component during preview ---
        private Sprite _originalSprite;
        private Image.Type _originalImageType;
        private bool _hadOriginalSprite = false;
        
        // --- GUI State ---
        private Vector2 _scrollPosition;
        private bool _cornersFoldout = true;
        private bool _borderFoldout = true;
        private bool _fillFoldout = true;
        
        // --- Custom GUI styles for a professional look ---
        private GUIStyle _titleStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _previewPanelStyle;
        private GUIStyle _warningBoxStyle;
        private GUIStyle _infoBoxStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _separatorStyle;
        private bool _stylesInitialized = false;
        
        // --- Shader Status ---
        private bool _shaderMissing = false;
        private string _missingShaderMessage = "";

        #region Window Management
        
        /// <summary>
        /// Shows the editor window. Accessed from the Unity top menu.
        /// </summary>
        [MenuItem(MENU_PATH)]
        public static void ShowWindow()
        {
            ProceduralUIToolWindow window = GetWindow<ProceduralUIToolWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
            window.Show();
        }
        
        /// <summary>
        /// Called when the window is enabled.
        /// Subscribes to events and performs initial setup.
        /// </summary>
        private void OnEnable()
        {
            if (_workingProfile == null)
            {
                CreateDefaultWorkingProfile();
            }

            UpdateSerializedObject();
            Selection.selectionChanged += OnSelectionChanged;
            CheckShaderAvailability();
            
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }
        
        /// <summary>
        /// Called when the window is disabled.
        /// Cleans up event subscriptions to prevent memory leaks.
        /// </summary>
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            DisablePreview();
        }
        
        /// <summary>
        /// Callback executed when the language changes to redraw the window.
        /// </summary>
        private void OnLanguageChanged(SupportedLanguage newLanguage)
        {
            Repaint();
        }
        
        #endregion
        
        #region Window Visual Styles
        
        /// <summary>
        /// Initializes custom GUI styles for the window.
        /// Runs only once to optimize performance.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;
            
            _titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 15, 20),
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.2f, 0.2f, 0.2f) }
            };
            
            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(5, 5, 8, 5),
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.3f, 0.3f, 0.3f) }
            };
            
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 12, 12),
                margin = new RectOffset(8, 8, 4, 8)
            };
            
            _previewPanelStyle = new GUIStyle(_panelStyle)
            {
                normal = { 
                    background = EditorGUIUtility.isProSkin ? 
                        MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f, 0.2f)) : 
                        MakeTex(1, 1, new Color(0.6f, 0.8f, 1f, 0.2f))
                }
            };
            
            _warningBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 5, 8),
                fontSize = 11
            };
            
            _infoBoxStyle = new GUIStyle(_warningBoxStyle)
            {
                normal = { 
                    background = EditorGUIUtility.isProSkin ? 
                        MakeTex(1, 1, new Color(0.2f, 0.5f, 0.8f, 0.15f)) : 
                        MakeTex(1, 1, new Color(0.5f, 0.7f, 1f, 0.15f))
                }
            };
            
            _primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(15, 15, 8, 8),
                margin = new RectOffset(2, 2, 3, 3)
            };
            
            _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(12, 12, 6, 6),
                margin = new RectOffset(2, 2, 2, 2)
            };
            
            _separatorStyle = new GUIStyle
            {
                normal = { 
                    background = EditorGUIUtility.isProSkin ? 
                        MakeTex(1, 1, new Color(0.4f, 0.4f, 0.4f, 0.6f)) : 
                        MakeTex(1, 1, new Color(0.6f, 0.6f, 0.6f, 0.6f))
                },
                margin = new RectOffset(10, 10, 8, 8),
                fixedHeight = 1
            };
            
            _stylesInitialized = true;
        }
        
        /// <summary>
        /// Creates a 1x1 texture of a specific color. Utility for styles.
        /// </summary>
        private Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        #endregion
        
        #region Main GUI Drawing
        
        /// <summary>
        /// Main method called to render the window.
        /// </summary>
        private void OnGUI()
        {
            InitializeStyles();
            
            if (_workingProfile == null || _serializedProfile == null)
            {
                CreateDefaultWorkingProfile();
                UpdateSerializedObject();
            }
            
            _serializedProfile.Update();
            
            DrawHeader();
            
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.ExpandHeight(true)))
            {
                _scrollPosition = scrollScope.scrollPosition;
                
                DrawLanguageSelector();
                DrawSeparator();
                DrawShaderStatus();
                DrawSeparator();
                DrawPreviewSection();
                DrawSeparator();
                DrawProfileEditor();
                DrawSeparator();
                DrawActionButtons();
                
                GUILayout.Space(20);
            }
            
            if (_serializedProfile.ApplyModifiedProperties())
            {
                if (_previewEnabled && _previewTarget != null)
                {
                    _previewTarget.ForceUpdate();
                    EditorUtility.SetDirty(_previewTarget);
                    SceneView.RepaintAll();
                }
            }
        }
        
        /// <summary>
        /// Draws the window header.
        /// </summary>
        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField(LocalizedGUI.Text("WINDOW_TITLE"), _titleStyle);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(LocalizedGUI.Text("WINDOW_SUBTITLE"), EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                
                GUILayout.Space(5);
            }
        }
        
        /// <summary>
        /// Draws the language selector.
        /// </summary>
        private void DrawLanguageSelector()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    LocalizedGUI.LanguageSelector();
                    GUILayout.FlexibleSpace();
                }
            }
        }
        
        /// <summary>
        /// Draws a visual separator.
        /// </summary>
        private void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            GUILayout.Box("", _separatorStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.Space(5);
        }
        
        /// <summary>
        /// Draws the shader status if shaders are missing.
        /// </summary>
        private void DrawShaderStatus()
        {
            if (_shaderMissing)
            {
                using (new EditorGUILayout.VerticalScope(_warningBoxStyle))
                {
                    EditorGUILayout.LabelField($"⚠️ {LocalizedGUI.Text("SHADER_STATUS")}", _sectionHeaderStyle);
                    EditorGUILayout.HelpBox(_missingShaderMessage, MessageType.Error);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(LocalizedGUI.Text("REFRESH_SHADER_CHECK"), _secondaryButtonStyle, GUILayout.Width(200)))
                        {
                            CheckShaderAvailability();
                        }
                    }
                }
                EditorGUILayout.Space(5);
            }
        }
        
        /// <summary>
        /// Draws the live preview section.
        /// </summary>
        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(_previewPanelStyle))
            {
                EditorGUILayout.LabelField(LocalizedGUI.Text("LIVE_PREVIEW"), _sectionHeaderStyle);
                
                GameObject selectedObject = Selection.activeGameObject;
                bool hasValidTarget = selectedObject != null && 
                    (selectedObject.GetComponent<Image>() != null || selectedObject.GetComponent<RawImage>() != null);
                
                if (_previewEnabled && _previewTarget != null)
                {
                    using (new EditorGUILayout.VerticalScope(_infoBoxStyle))
                    {
                        EditorGUILayout.LabelField($"{LocalizedGUI.Text("PREVIEWING_ON")}: {_previewTarget.gameObject.name}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(LocalizedGUI.Text("CHANGES_REALTIME"), EditorStyles.miniLabel);
                    }
                }
                
                EditorGUILayout.Space(8);
                
                EditorGUI.BeginDisabledGroup(!hasValidTarget || _shaderMissing);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(LocalizedGUI.Text("START_PREVIEW"), _primaryButtonStyle))
                    {
                        EnablePreviewOnSelected();
                    }
                    
                    if (GUILayout.Button(LocalizedGUI.Text("STOP_PREVIEW"), _secondaryButtonStyle))
                    {
                        DisablePreview();
                    }
                }
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.Space(5);
                
                if (_shaderMissing)
                {
                    EditorGUILayout.HelpBox(LocalizedGUI.Text("PREVIEW_UNAVAILABLE"), MessageType.Error);
                }
                else if (!hasValidTarget && selectedObject != null)
                {
                    EditorGUILayout.HelpBox(LocalizedGUI.Text("SELECTION_MUST_HAVE"), MessageType.Warning);
                }
                else if (selectedObject == null)
                {
                    using (new EditorGUILayout.VerticalScope(_infoBoxStyle))
                    {
                        EditorGUILayout.LabelField(LocalizedGUI.Text("QUICK_START"), EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(LocalizedGUI.Text("STEP_1"), EditorStyles.miniLabel);
                        EditorGUILayout.LabelField(LocalizedGUI.Text("STEP_2"), EditorStyles.miniLabel);
                        EditorGUILayout.LabelField(LocalizedGUI.Text("STEP_3"), EditorStyles.miniLabel);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws the main effect profile editor.
        /// </summary>
        private void DrawProfileEditor()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                EditorGUILayout.LabelField(LocalizedGUI.Text("EFFECT_SETTINGS"), _sectionHeaderStyle);
                EditorGUILayout.Space(5);
                
                DrawCornerSettings();
                EditorGUILayout.Space(8);
                
                DrawBorderSettings();
                EditorGUILayout.Space(8);
                
                DrawFillSettings();
            }
        }
        
        /// <summary>
        /// Draws the corner settings.
        /// </summary>
        private void DrawCornerSettings()
        {
            _cornersFoldout = EditorGUILayout.Foldout(_cornersFoldout, LocalizedGUI.Text("CORNER_RADIUS"), true, EditorStyles.foldoutHeader);
            if (_cornersFoldout)
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("cornerRadiusUnit"), LocalizedGUI.Content("UNIT"));
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("useIndividualCorners"), LocalizedGUI.Content("INDIVIDUAL_CORNERS"));
                    
                    bool useIndividual = _serializedProfile.FindProperty("useIndividualCorners").boolValue;
                    string radiusLabel = (_workingProfile.cornerRadiusUnit == Runtime.ProceduralUIProfile.Unit.Percent) ? "%" : "px";
                    
                    EditorGUILayout.Space(3);
                    
                    if (useIndividual)
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.PropertyField(_serializedProfile.FindProperty("cornerRadiusTopLeft"), 
                                LocalizedGUI.Content("TOP_LEFT", $"{LocalizedGUI.Text("TOP_LEFT")} ({radiusLabel})"));
                            EditorGUILayout.PropertyField(_serializedProfile.FindProperty("cornerRadiusTopRight"), 
                                LocalizedGUI.Content("TOP_RIGHT", $"{LocalizedGUI.Text("TOP_RIGHT")} ({radiusLabel})"));
                            EditorGUILayout.PropertyField(_serializedProfile.FindProperty("cornerRadiusBottomLeft"), 
                                LocalizedGUI.Content("BOTTOM_LEFT", $"{LocalizedGUI.Text("BOTTOM_LEFT")} ({radiusLabel})"));
                            EditorGUILayout.PropertyField(_serializedProfile.FindProperty("cornerRadiusBottomRight"), 
                                LocalizedGUI.Content("BOTTOM_RIGHT", $"{LocalizedGUI.Text("BOTTOM_RIGHT")} ({radiusLabel})"));
                        }
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(_serializedProfile.FindProperty("globalCornerRadius"), 
                            LocalizedGUI.Content("GLOBAL_RADIUS", $"{LocalizedGUI.Text("GLOBAL_RADIUS")} ({radiusLabel})"));
                    }
                    
                    EditorGUI.indentLevel--;
                }
            }
        }
        
        /// <summary>
        /// Draws the border settings.
        /// </summary>
        private void DrawBorderSettings()
        {
            _borderFoldout = EditorGUILayout.Foldout(_borderFoldout, LocalizedGUI.Text("BORDER_SETTINGS"), true, EditorStyles.foldoutHeader);
            if (_borderFoldout)
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("borderWidthUnit"), LocalizedGUI.Content("UNIT"));
                    string borderLabel = (_workingProfile.borderWidthUnit == Runtime.ProceduralUIProfile.Unit.Percent) ? "%" : "px";
                    
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("borderWidth"), 
                        LocalizedGUI.Content("WIDTH", $"{LocalizedGUI.Text("WIDTH")} ({borderLabel})"));
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("borderColor"), 
                        LocalizedGUI.Content("COLOR"));
                    
                    EditorGUILayout.Space(5);
                    
                    // --- PROGRESS SECTION ---
                    EditorGUILayout.LabelField(LocalizedGUI.Text("PROGRESS_BORDER"), EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("useProgressBorder"), 
                        LocalizedGUI.Content("ENABLE_PROGRESS_BORDER"));
                    
                    if (_workingProfile.useProgressBorder)
                    {
                        EditorGUI.indentLevel++;
                        
                        EditorGUILayout.PropertyField(_serializedProfile.FindProperty("progressValue"), 
                            LocalizedGUI.Content("PROGRESS_VALUE"));
                        
                        EditorGUILayout.PropertyField(_serializedProfile.FindProperty("progressStartAngle"), 
                            LocalizedGUI.Content("PROGRESS_START_ANGLE"));
                        
                        EditorGUILayout.PropertyField(_serializedProfile.FindProperty("progressDirection"), 
                            LocalizedGUI.Content("PROGRESS_DIRECTION"));
                        
                        EditorGUI.indentLevel--;
                    }
                    
                    EditorGUI.indentLevel--;
                }
            }
        }

        /// <summary>
        /// Draws the fill settings.
        /// </summary>
        private void DrawFillSettings()
        {
            _fillFoldout = EditorGUILayout.Foldout(_fillFoldout, LocalizedGUI.Text("FILL_SETTINGS"), true, EditorStyles.foldoutHeader);
            if (_fillFoldout)
            {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_serializedProfile.FindProperty("fillColor"), LocalizedGUI.Content("FILL_COLOR"));
                    EditorGUI.indentLevel--;
                }
            }
        }
        
        /// <summary>
        /// Draws the main action buttons (Apply, Reset, etc.).
        /// </summary>
        private void DrawActionButtons()
        {
            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                EditorGUILayout.LabelField(LocalizedGUI.Text("ACTIONS"), _sectionHeaderStyle);
                EditorGUILayout.Space(8);
                
                EditorGUI.BeginDisabledGroup(_shaderMissing);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(LocalizedGUI.Text("APPLY_TO_SELECTED"), _primaryButtonStyle, GUILayout.Height(35)))
                    {
                        ApplyToSelected();
                    }
                    
                    if (GUILayout.Button(LocalizedGUI.Text("RESET_SETTINGS"), GUILayout.Height(35)))
                    {
                        ResetProfile();
                    }
                }
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.Space(15);
                
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(LocalizedGUI.Text("PRESET_MANAGEMENT"), EditorStyles.boldLabel);
                    EditorGUILayout.Space(3);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(LocalizedGUI.Text("SAVE_PRESET"), _secondaryButtonStyle))
                        {
                            SavePreset();
                        }
                        
                        if (GUILayout.Button(LocalizedGUI.Text("LOAD_PRESET"), _secondaryButtonStyle))
                        {
                            LoadPreset();
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Preview System
        
        /// <summary>
        /// Called when the editor selection changes to repaint the window.
        /// </summary>
        private void OnSelectionChanged()
        {
            Repaint();
        }

        /// <summary>
        /// Enables preview on the selected object.
        /// IMPROVED: Now preserves the original sprite and image type to restore them later.
        /// </summary>
        private void EnablePreviewOnSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(LocalizedGUI.Text("NO_SELECTION"), LocalizedGUI.Text("SELECT_GAMEOBJECT_FIRST"), "OK");
                return;
            }
            
            var image = selected.GetComponent<Image>();
            var rawImage = selected.GetComponent<RawImage>();
            
            if (image == null && rawImage == null)
            {
                EditorUtility.DisplayDialog(LocalizedGUI.Text("INVALID_TARGET"), 
                    LocalizedGUI.Text("OBJECT_MUST_HAVE_IMAGE"), "OK");
                return;
            }
            
            if (_shaderMissing)
            {
                EditorUtility.DisplayDialog(LocalizedGUI.Text("SHADERS_MISSING"), 
                    LocalizedGUI.Text("CANNOT_PREVIEW_SHADERS"), "OK");
                return;
            }
            
            if (_previewEnabled && _previewTarget != null && _previewTarget.gameObject != selected)
            {
                CleanupPreviousPreview();
            }
            
            // Get the existing ProceduralUIComponent
            var existingComponent = selected.GetComponent<Runtime.ProceduralUIComponent>();
            
            // If the object already has a component with a profile, copy its values to the interface
            if (existingComponent != null && existingComponent.profile != null)
            {
                CopyProfileToInterface(existingComponent.profile);
            }
            else
            {
                // If it has no component or profile, prepare the image and use default values from the interface
                if (image != null)
                {
                    // Save the original state of the Image component
                    _originalSprite = image.sprite;
                    _originalImageType = image.type;
                    _hadOriginalSprite = (_originalSprite != null);
                    
                    Undo.RecordObject(image, "Prepare Image for UI Effect");
                    image.sprite = null;
                    image.type = Image.Type.Simple;
                }
            }
            
            _previewTarget = existingComponent;
            if (_previewTarget == null)
            {
                _previewTarget = Undo.AddComponent<Runtime.ProceduralUIComponent>(selected);
            }
            
            _originalPreviewProfile = _previewTarget.profile;
            
            Undo.RecordObject(_previewTarget, "Enable UI Preview");
            
            _previewTarget.SetProfile(_workingProfile);
            _previewTarget.ForceUpdate();
            
            EditorUtility.SetDirty(_previewTarget);
            SceneView.RepaintAll();
            
            _previewEnabled = true;
        }

        /// <summary>
        /// Copies values from an existing profile to the tool's interface.
        /// </summary>
        private void CopyProfileToInterface(Runtime.ProceduralUIProfile sourceProfile)
        {
            if (sourceProfile == null || _workingProfile == null) return;
            
            try
            {
                Undo.RecordObject(_workingProfile, "Copy Profile to Interface");
                
                // Copy shape configuration
                _workingProfile.shapeType = sourceProfile.shapeType;
                _workingProfile.starPoints = sourceProfile.starPoints;
                _workingProfile.starInnerRatio = sourceProfile.starInnerRatio;
                if (sourceProfile.customVertices != null)
                {
                    _workingProfile.customVertices = (Vector2[])sourceProfile.customVertices.Clone();
                }
                
                // Copy corner configuration
                _workingProfile.cornerRadiusUnit = sourceProfile.cornerRadiusUnit;
                _workingProfile.useIndividualCorners = sourceProfile.useIndividualCorners;
                _workingProfile.globalCornerRadius = sourceProfile.globalCornerRadius;
                _workingProfile.cornerRadiusTopLeft = sourceProfile.cornerRadiusTopLeft;
                _workingProfile.cornerRadiusTopRight = sourceProfile.cornerRadiusTopRight;
                _workingProfile.cornerRadiusBottomLeft = sourceProfile.cornerRadiusBottomLeft;
                _workingProfile.cornerRadiusBottomRight = sourceProfile.cornerRadiusBottomRight;
                
                // Copy corner transition configuration
                _workingProfile.useIndividualOffsets = sourceProfile.useIndividualOffsets;
                _workingProfile.globalCornerOffset = sourceProfile.globalCornerOffset;
                _workingProfile.cornerOffsetTopLeft = sourceProfile.cornerOffsetTopLeft;
                _workingProfile.cornerOffsetTopRight = sourceProfile.cornerOffsetTopRight;
                _workingProfile.cornerOffsetBottomLeft = sourceProfile.cornerOffsetBottomLeft;
                _workingProfile.cornerOffsetBottomRight = sourceProfile.cornerOffsetBottomRight;
                
                // Copy border configuration
                _workingProfile.borderWidthUnit = sourceProfile.borderWidthUnit;
                _workingProfile.borderWidth = sourceProfile.borderWidth;
                _workingProfile.borderColor = sourceProfile.borderColor;
                _workingProfile.edgeSharpness = sourceProfile.edgeSharpness;
                _workingProfile.usePixelPerfectEdges = sourceProfile.usePixelPerfectEdges;
                
                // Copy fill configuration
                _workingProfile.fillColor = sourceProfile.fillColor;
                
                // Copy progress border configuration
                _workingProfile.useProgressBorder = sourceProfile.useProgressBorder;
                _workingProfile.progressValue = sourceProfile.progressValue;
                _workingProfile.progressStartAngle = sourceProfile.progressStartAngle;
                _workingProfile.progressDirection = sourceProfile.progressDirection;
                
                // Update the SerializedObject to reflect changes in the interface
                UpdateSerializedObject();
                
                Debug.Log($"[Procedural UI Tool] Copied values from object '{sourceProfile.name}' to the interface.");
                
                // Force interface update
                Repaint();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Procedural UI Tool] Error copying profile to interface: {e.Message}");
            }
        }

        /// <summary>
        /// Cleans up the preview from the previous object if a new one is selected.
        /// IMPROVED: Now also restores the original sprite.
        /// </summary>
        private void CleanupPreviousPreview()
        {
            if (_previewTarget != null)
            {
                GameObject previousTargetObject = _previewTarget.gameObject;

                if (_previewTarget.profile == _workingProfile)
                {
                    if (_originalPreviewProfile != null)
                    {
                        Undo.RecordObject(_previewTarget, "Restore Original Profile");
                        _previewTarget.SetProfile(_originalPreviewProfile);
                    }
                    else
                    {
                        Undo.DestroyObjectImmediate(_previewTarget);
                    }
                }
                
                // Restore the original state of the Image component
                RestoreOriginalImageState(previousTargetObject);
                
                if(previousTargetObject != null)
                {
                    EditorUtility.SetDirty(previousTargetObject);
                }
            }
            
            // Reset saved values
            _originalSprite = null;
            _originalImageType = Image.Type.Simple;
            _hadOriginalSprite = false;
            _originalPreviewProfile = null;
        }
        
        /// <summary>
        /// Disables the preview and restores the object's original state.
        /// IMPROVED: Now includes sprite restoration.
        /// </summary>
        private void DisablePreview()
        {
            if (_previewTarget != null)
            {
                try
                {
                    GameObject targetObject = _previewTarget.gameObject;

                    if (_previewTarget.profile == _workingProfile)
                    {
                        if (_originalPreviewProfile != null)
                        {
                            Undo.RecordObject(_previewTarget, "Restore Original Profile");
                            _previewTarget.SetProfile(_originalPreviewProfile);
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(_previewTarget);
                        }
                    }
                    
                    // Restore the original state of the Image component
                    RestoreOriginalImageState(targetObject);
                    
                    if(targetObject != null)
                    {
                        EditorUtility.SetDirty(targetObject);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Error disabling preview: {e.Message}");
                }
            }
            
            _previewEnabled = false;
            _previewTarget = null;
            _originalPreviewProfile = null;
            
            // Reset saved values
            _originalSprite = null;
            _originalImageType = Image.Type.Simple;
            _hadOriginalSprite = false;
            
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Restores the original state of the Image component (sprite and type).
        /// </summary>
        private void RestoreOriginalImageState(GameObject targetObject)
        {
            if (targetObject == null) return;
            
            var image = targetObject.GetComponent<Image>();
            if (image != null && _hadOriginalSprite)
            {
                Undo.RecordObject(image, "Restore Original Sprite");
                image.sprite = _originalSprite;
                image.type = _originalImageType;
            }
        }
        
        #endregion
        
        #region Apply and Preset Actions
        
        /// <summary>
        /// Applies the current configuration to the selected objects.
        /// </summary>
        private void ApplyToSelected()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog(LocalizedGUI.Text("NO_SELECTION"), 
                    LocalizedGUI.Text("SELECT_GAMEOBJECT_FIRST"), "OK");
                return;
            }
            
            if (_shaderMissing)
            {
                EditorUtility.DisplayDialog(LocalizedGUI.Text("SHADERS_MISSING"), 
                    LocalizedGUI.Text("CANNOT_APPLY_SHADERS"), "OK");
                return;
            }
            
            int appliedCount = 0;
            
            foreach (GameObject obj in selectedObjects)
            {
                var image = obj.GetComponent<Image>();
                var rawImage = obj.GetComponent<RawImage>();
                
                if (image == null && rawImage == null) continue;

                if (image != null)
                {
                    Undo.RecordObject(image, "Prepare Image for UI Effect");
                    // We DO NOT remove the sprite on apply, to not lose the reference.
                    // The effect material will render on top.
                    // image.sprite = null; 
                    image.type = Image.Type.Simple;
                }
                
                var effectComponent = obj.GetComponent<Runtime.ProceduralUIComponent>();
                if (effectComponent == null)
                {
                    effectComponent = Undo.AddComponent<Runtime.ProceduralUIComponent>(obj);
                }
                
                var profileCopy = Instantiate(_workingProfile);
                profileCopy.name = $"ProceduralUI_{obj.name}_{System.DateTime.Now.Ticks}";
                
                string directory = "Assets/ProceduralUITool/Presets";
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/ProceduralUITool"))
                    {
                        AssetDatabase.CreateFolder("Assets", "ProceduralUITool");
                    }
                    AssetDatabase.CreateFolder("Assets/ProceduralUITool", "Presets");
                }
                
                string assetPath = $"{directory}/AppliedProfile_{obj.name}_{System.Guid.NewGuid().ToString("N")[..8]}.asset";
                AssetDatabase.CreateAsset(profileCopy, assetPath);
                
                Undo.RecordObject(effectComponent, "Apply UI Profile");
                effectComponent.SetProfile(profileCopy);
                effectComponent.ForceUpdate();
                
                EditorUtility.SetDirty(effectComponent);
                appliedCount++;
            }
            
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            
            EditorUtility.DisplayDialog(LocalizedGUI.Text("APPLY_COMPLETE"), 
                LocalizedGUI.Format("APPLIED_EFFECT_TO", appliedCount), "OK");
        }
        
        /// <summary>
        /// Saves the current configuration as a new preset (.asset file).
        /// </summary>
        private void SavePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                LocalizedGUI.Text("SAVE_PRESET"),
                "NewProceduralUIPreset",
                "asset",
                "Choose a location to save the preset");
            
            if (!string.IsNullOrEmpty(path))
            {
                var presetCopy = Instantiate(_workingProfile);
                AssetDatabase.CreateAsset(presetCopy, path);
                AssetDatabase.SaveAssets();
                
                EditorUtility.DisplayDialog(LocalizedGUI.Text("PRESET_SAVED"), 
                    LocalizedGUI.Format("PRESET_SAVED_SUCCESS", path), "OK");
            }
        }
        
        /// <summary>
        /// Loads an existing preset into the editor window.
        /// </summary>
        private void LoadPreset()
        {
            string path = EditorUtility.OpenFilePanel(
                LocalizedGUI.Text("LOAD_PRESET"),
                Application.dataPath,
                "asset");

            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }

                var preset = AssetDatabase.LoadAssetAtPath<Runtime.ProceduralUIProfile>(path);
                if (preset != null)
                {
                    CopyProfileToInterface(preset);

                    if (_previewEnabled && _previewTarget != null)
                    {
                        _previewTarget.ForceUpdate();
                        EditorUtility.SetDirty(_previewTarget);
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog(LocalizedGUI.Text("LOAD_ERROR"), LocalizedGUI.Text("COULD_NOT_LOAD"), "OK");
                }
            }
        }
        
        #endregion
        
        #region Shader Validation
        
        /// <summary>
        /// Checks if the necessary shaders are available in the project.
        /// </summary>
        private void CheckShaderAvailability()
        {
            bool urpFound = Shader.Find(SHADER_URP) != null;
            bool builtinFound = Shader.Find(SHADER_BUILTIN) != null;
            bool legacyFound = Shader.Find(SHADER_LEGACY) != null;
            
            _shaderMissing = !urpFound && !builtinFound && !legacyFound;
            
            if (_shaderMissing)
            {
                string pipeline = "Built-in";
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                {
                    var pipelineName = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name;
                    if (pipelineName.Contains("Universal"))
                    {
                        pipeline = "URP";
                    }
                }
                
                _missingShaderMessage = $"{LocalizedGUI.Text("SHADER_NOT_FOUND")}\n" +
                    $"{LocalizedGUI.Format("CURRENT_PIPELINE", pipeline)}\n" +
                    $"{LocalizedGUI.Format("TRIED_SHADERS", $"{SHADER_URP}, {SHADER_BUILTIN}, {SHADER_LEGACY}")}\n" +
                    $"{LocalizedGUI.Text("ENSURE_SHADERS")}";
            }
        }
        
        #endregion
        
        #region Profile Management
        
        /// <summary>
        /// Creates a default working profile if one does not exist.
        /// </summary>
        private void CreateDefaultWorkingProfile()
        {
            _workingProfile = CreateInstance<Runtime.ProceduralUIProfile>();
            _workingProfile.name = "Working Profile";
            _workingProfile.ResetToDefaults();
        }
        
        /// <summary>
        /// Updates the SerializedObject to reflect the current working profile.
        /// </summary>
        private void UpdateSerializedObject()
        {
            if (_workingProfile != null)
            {
                _serializedProfile = new SerializedObject(_workingProfile);
            }
        }
        
        /// <summary>
        /// Resets the working profile to its default values.
        /// </summary>
        private void ResetProfile()
        {
            if (_workingProfile == null) return;
            
            Undo.RecordObject(_workingProfile, "Reset UI Profile");
            _workingProfile.ResetToDefaults();
            UpdateSerializedObject();
            
            if (_previewEnabled && _previewTarget != null)
            {
                _previewTarget.ForceUpdate();
                EditorUtility.SetDirty(_previewTarget);
                SceneView.RepaintAll();
            }
        }
        
        #endregion
    }
}