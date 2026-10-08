using UnityEngine;
using UnityEditor;
using ProceduralUITool.Runtime;
using ProceduralUITool.Editor.Localization;

namespace ProceduralUITool.Editor
{
    [CustomEditor(typeof(ProceduralUIProfile))]
    public class ProceduralUIProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var profile = (ProceduralUIProfile)target;

            // --- Shape Configuration ---
            EditorGUILayout.LabelField(LocalizedGUI.Text("SHAPE_SETTINGS"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapeType"), LocalizedGUI.Content("SHAPE_TYPE"));
            
            if (profile.shapeType == ProceduralUIProfile.ShapeType.Star)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("starPoints"), LocalizedGUI.Content("STAR_POINTS"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("starInnerRatio"), LocalizedGUI.Content("STAR_INNER_RATIO"));
            }
            
            EditorGUILayout.Space();

            // --- Corner Radius Configuration ---
            EditorGUILayout.LabelField(LocalizedGUI.Text("CORNER_RADIUS_SETTINGS"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadiusUnit"), LocalizedGUI.Content("UNIT"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useIndividualCorners"), LocalizedGUI.Content("INDIVIDUAL_CORNERS"));

            if (profile.useIndividualCorners)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadiusTopLeft"), LocalizedGUI.Content("TOP_LEFT"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadiusTopRight"), LocalizedGUI.Content("TOP_RIGHT"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadiusBottomLeft"), LocalizedGUI.Content("BOTTOM_LEFT"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerRadiusBottomRight"), LocalizedGUI.Content("BOTTOM_RIGHT"));
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("globalCornerRadius"), LocalizedGUI.Content("GLOBAL_RADIUS"));
            }

            EditorGUILayout.Space();

            // --- Border Configuration ---
            EditorGUILayout.LabelField(LocalizedGUI.Text("BORDER_SETTINGS"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("borderWidthUnit"), LocalizedGUI.Content("UNIT"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("borderWidth"), LocalizedGUI.Content("WIDTH"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("borderColor"), LocalizedGUI.Content("COLOR"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeSharpness"), LocalizedGUI.Content("EDGE_SHARPNESS"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("usePixelPerfectEdges"), LocalizedGUI.Content("PIXEL_PERFECT_EDGES"));
            
            EditorGUILayout.Space();

            // --- Fill Configuration ---
            EditorGUILayout.LabelField(LocalizedGUI.Text("FILL_SETTINGS"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fillColor"), LocalizedGUI.Content("FILL_COLOR"));

            EditorGUILayout.Space();
            
            // --- Progress Border Configuration ---
            EditorGUILayout.LabelField(LocalizedGUI.Text("PROGRESS_BORDER"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useProgressBorder"), LocalizedGUI.Content("ENABLE_PROGRESS_BORDER"));
            if (profile.useProgressBorder)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("progressValue"), LocalizedGUI.Content("PROGRESS_VALUE"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("progressStartAngle"), LocalizedGUI.Content("PROGRESS_START_ANGLE"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("progressDirection"), LocalizedGUI.Content("PROGRESS_DIRECTION"));
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}