using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ProceduralUITool.Editor.Localization
{
    [System.Serializable]
    public enum SupportedLanguage
    {
        English = 0,
        Spanish = 1,
        German = 2,
        Chinese = 3
    }

    public static class LocalizationManager
    {
        private static SupportedLanguage _currentLanguage = SupportedLanguage.English;
        private static Dictionary<SupportedLanguage, Dictionary<string, string>> _translations;
        private static bool _initialized = false;
        private const string LANGUAGE_PREF_KEY = "ProceduralUITool_Language";

        public static SupportedLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    EditorPrefs.SetInt(LANGUAGE_PREF_KEY, (int)value);
                    OnLanguageChanged?.Invoke(value);
                }
            }
        }

        public static event System.Action<SupportedLanguage> OnLanguageChanged;

        static LocalizationManager()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _currentLanguage = (SupportedLanguage)EditorPrefs.GetInt(LANGUAGE_PREF_KEY, 0);
            InitializeTranslations();
            _initialized = true;
        }

        public static string GetText(string key)
        {
            if (!_initialized) Initialize();
            if (_translations != null && 
                _translations.ContainsKey(_currentLanguage) && 
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }

            if (_currentLanguage != SupportedLanguage.English && 
                _translations != null &&
                _translations.ContainsKey(SupportedLanguage.English) && 
                _translations[SupportedLanguage.English].ContainsKey(key))
            {
                return _translations[SupportedLanguage.English][key];
            }
            
            Debug.LogWarning($"Localization key not found: {key}");
            return key;
        }

        public static string[] GetLanguageNames()
        {
            return new string[] { "English", "Español", "Deutsch", "中文" };
        }

        public static string GetCurrentLanguageName()
        {
            return GetLanguageNames()[(int)_currentLanguage];
        }

        private static void InitializeTranslations()
        {
            _translations = new Dictionary<SupportedLanguage, Dictionary<string, string>>();

            var english = new Dictionary<string, string>
            {
                {"WINDOW_TITLE", "Procedural UI Tool"},
                {"WINDOW_SUBTITLE", "Procedural UI Styling"},
                {"SHADER_STATUS", "SHADER STATUS"},
                {"LIVE_PREVIEW", "LIVE PREVIEW"},
                {"EFFECT_SETTINGS", "EFFECT SETTINGS"},
                {"ACTIONS", "ACTIONS"},
                {"PRESET_MANAGEMENT", "Preset Management"},
                {"LANGUAGE", "Language"},
                {"LANGUAGE_TOOLTIP", "Select interface language"},
                {"CORNER_RADIUS", "Corner Radius"},
                {"UNIT", "Unit"},
                {"INDIVIDUAL_CORNERS", "Individual Corners"},
                {"TOP_LEFT", "Top Left"},
                {"TOP_RIGHT", "Top Right"},
                {"BOTTOM_LEFT", "Bottom Left"},
                {"BOTTOM_RIGHT", "Bottom Right"},
                {"GLOBAL_RADIUS", "Global Radius"},
                {"BORDER_SETTINGS", "Border Settings"},
                {"FILL_SETTINGS", "Fill Settings"},
                {"FILL_COLOR", "Fill Color"},
                {"WIDTH", "Width"},
                {"COLOR", "Color"},
                {"PREVIEWING_ON", "Previewing on"},
                {"CHANGES_REALTIME", "Changes are applied in real-time"},
                {"START_PREVIEW", "Start Preview"},
                {"STOP_PREVIEW", "Stop Preview"},
                {"PREVIEW_UNAVAILABLE", "Preview unavailable: Required shaders are missing"},
                {"SELECTION_MUST_HAVE", "Selection must have an Image or RawImage component"},
                {"QUICK_START", "Quick Start:"},
                {"STEP_1", "1. Select a GameObject with Image/RawImage"},
                {"STEP_2", "2. Click 'Start Preview' to see effects"},
                {"STEP_3", "3. Adjust settings below in real-time"},
                {"APPLY_TO_SELECTED", "Apply to Selected"},
                {"RESET_SETTINGS", "Reset Settings"},
                {"SAVE_PRESET", "Save Preset"},
                {"LOAD_PRESET", "Load Preset"},
                {"REFRESH_SHADER_CHECK", "Refresh Shader Check"},
                {"NO_SELECTION", "No Selection"},
                {"SELECT_GAMEOBJECT_FIRST", "Please select a GameObject first."},
                {"INVALID_TARGET", "Invalid Target"},
                {"OBJECT_MUST_HAVE_IMAGE", "Selected object must have an Image or RawImage component."},
                {"SHADERS_MISSING", "Shaders Missing"},
                {"CANNOT_PREVIEW_SHADERS", "Cannot preview: Procedural UI Tool shaders are not installed or available."},
                {"CANNOT_APPLY_SHADERS", "Cannot apply effects: Procedural UI Tool shaders are not installed or available."},
                {"APPLY_COMPLETE", "Apply Complete"},
                {"APPLIED_EFFECT_TO", "Applied UI Effect to {0} object(s)."},
                {"PRESET_SAVED", "Preset Saved"},
                {"PRESET_SAVED_SUCCESS", "Preset saved successfully to:\n{0}"},
                {"LOAD_ERROR", "Load Error"},
                {"COULD_NOT_LOAD", "Could not load preset file."},
                {"SHADER_NOT_FOUND", "Procedural UI Tool shaders not found!"},
                {"CURRENT_PIPELINE", "Current pipeline: {0}"},
                {"TRIED_SHADERS", "Tried: {0}"},
                {"ENSURE_SHADERS", "Please ensure the shaders are installed and included in the build."},
                {"PROGRESS_BORDER", "Progress Border"},
                {"ENABLE_PROGRESS_BORDER", "Enable Progress Border"},
                {"PROGRESS_VALUE", "Progress"},
                {"PROGRESS_START_ANGLE", "Start Angle"},
                {"PROGRESS_DIRECTION", "Direction"},
                {"SHAPE_SETTINGS", "Shape Settings"},
                {"SHAPE_TYPE", "Shape Type"},
                {"STAR_POINTS", "Star Points"},
                {"STAR_INNER_RATIO", "Star Inner Ratio"},
                {"CORNER_RADIUS_SETTINGS", "Corner Radius Settings"},
                {"EDGE_SHARPNESS", "Edge Sharpness"},
                {"PIXEL_PERFECT_EDGES", "Pixel Perfect Edges"},
                {"CORNER_SETTINGS_HEADER", "Corner Radius Configuration"},
                {"PROGRESS_BORDER_HEADER", "Progress Border Configuration"},
                {"FILL_CONFIG_HEADER", "Fill Configuration"}
            };
            _translations[SupportedLanguage.English] = english;

            var spanish = new Dictionary<string, string>
            {
                {"WINDOW_TITLE", "Herramienta de UI Procedural"},
                {"WINDOW_SUBTITLE", "Estilizado de UI Procedural"},
                {"SHADER_STATUS", "ESTADO DEL SHADER"},
                {"LIVE_PREVIEW", "VISTA PREVIA EN TIEMPO REAL"},
                {"EFFECT_SETTINGS", "CONFIGURACIÓN DE EFECTOS"},
                {"ACTIONS", "ACCIONES"},
                {"PRESET_MANAGEMENT", "Gestión de Presets"},
                {"LANGUAGE", "Idioma"},
                {"LANGUAGE_TOOLTIP", "Seleccionar idioma de la interfaz"},
                {"CORNER_RADIUS", "Radio de Esquinas"},
                {"UNIT", "Unidad"},
                {"INDIVIDUAL_CORNERS", "Esquinas Individuales"},
                {"TOP_LEFT", "Arriba Izquierda"},
                {"TOP_RIGHT", "Arriba Derecha"},
                {"BOTTOM_LEFT", "Abajo Izquierda"},
                {"BOTTOM_RIGHT", "Abajo Derecha"},
                {"GLOBAL_RADIUS", "Radio Global"},
                {"BORDER_SETTINGS", "Configuración de Borde"},
                {"FILL_SETTINGS", "Configuración de Relleno"},
                {"FILL_COLOR", "Color de Relleno"},
                {"WIDTH", "Ancho"},
                {"COLOR", "Color"},
                {"PREVIEWING_ON", "Vista previa en"},
                {"CHANGES_REALTIME", "Los cambios se aplican en tiempo real"},
                {"START_PREVIEW", "Iniciar Vista Previa"},
                {"STOP_PREVIEW", "Detener Vista Previa"},
                {"PREVIEW_UNAVAILABLE", "Vista previa no disponible: Faltan los shaders requeridos"},
                {"SELECTION_MUST_HAVE", "La selección debe tener un componente Image o RawImage"},
                {"QUICK_START", "Inicio Rápido:"},
                {"STEP_1", "1. Selecciona un GameObject con Image/RawImage"},
                {"STEP_2", "2. Haz clic en 'Iniciar Vista Previa' para ver efectos"},
                {"STEP_3", "3. Ajusta la configuración de abajo en tiempo real"},
                {"APPLY_TO_SELECTED", "Aplicar a Seleccionados"},
                {"RESET_SETTINGS", "Restablecer Configuración"},
                {"SAVE_PRESET", "Guardar Preset"},
                {"LOAD_PRESET", "Cargar Preset"},
                {"REFRESH_SHADER_CHECK", "Actualizar Verificación de Shader"},
                {"NO_SELECTION", "Sin Selección"},
                {"SELECT_GAMEOBJECT_FIRST", "Por favor selecciona un GameObject primero."},
                {"INVALID_TARGET", "Objetivo Inválido"},
                {"OBJECT_MUST_HAVE_IMAGE", "El objeto seleccionado debe tener un componente Image o RawImage."},
                {"SHADERS_MISSING", "Shaders Faltantes"},
                {"CANNOT_PREVIEW_SHADERS", "No se puede previsualizar: Los shaders de Procedural UI Tool no están instalados o disponibles."},
                {"CANNOT_APPLY_SHADERS", "No se pueden aplicar efectos: Los shaders de Procedural UI Tool no están instalados o disponibles."},
                {"APPLY_COMPLETE", "Aplicación Completa"},
                {"APPLIED_EFFECT_TO", "Efecto de UI aplicado a {0} objeto(s)."},
                {"PRESET_SAVED", "Preset Guardado"},
                {"PRESET_SAVED_SUCCESS", "Preset guardado exitosamente en:\n{0}"},
                {"LOAD_ERROR", "Error de Carga"},
                {"COULD_NOT_LOAD", "No se pudo cargar el archivo de preset."},
                {"SHADER_NOT_FOUND", "¡Shaders de Procedural UI Tool no encontrados!"},
                {"CURRENT_PIPELINE", "Pipeline actual: {0}"},
                {"TRIED_SHADERS", "Intentado: {0}"},
                {"ENSURE_SHADERS", "Por favor asegúrate de que los shaders estén instalados e incluidos en la build."},
                {"PROGRESS_BORDER", "Borde de Progreso"},
                {"ENABLE_PROGRESS_BORDER", "Activar Borde de Progreso"},
                {"PROGRESS_VALUE", "Progreso"},
                {"PROGRESS_START_ANGLE", "Ángulo de Inicio"},
                {"PROGRESS_DIRECTION", "Dirección"},
                {"SHAPE_SETTINGS", "Configuración de Forma"},
                {"SHAPE_TYPE", "Tipo de Forma"},
                {"STAR_POINTS", "Puntas de Estrella"},
                {"STAR_INNER_RATIO", "Radio Interno de Estrella"},
                {"CORNER_RADIUS_SETTINGS", "Configuración de Radio de Esquinas"},
                {"EDGE_SHARPNESS", "Nitidez de Borde"},
                {"PIXEL_PERFECT_EDGES", "Bordes Pixel Perfect"},
                {"CORNER_SETTINGS_HEADER", "Configuración de Radio de Esquinas"},
                {"PROGRESS_BORDER_HEADER", "Configuración de Borde de Progreso"},
                {"FILL_CONFIG_HEADER", "Configuración de Relleno"}
            };
            _translations[SupportedLanguage.Spanish] = spanish;
            
            var german = new Dictionary<string, string>
            {
                {"WINDOW_TITLE", "Prozedurales UI-Werkzeug"},
                {"WINDOW_SUBTITLE", "Prozedurales UI-Styling"},
                {"SHADER_STATUS", "SHADER-STATUS"},
                {"LIVE_PREVIEW", "LIVE-VORSCHAU"},
                {"EFFECT_SETTINGS", "EFFEKT-EINSTELLUNGEN"},
                {"ACTIONS", "AKTIONEN"},
                {"PRESET_MANAGEMENT", "Preset-Verwaltung"},
                {"LANGUAGE", "Sprache"},
                {"LANGUAGE_TOOLTIP", "Schnittstellensprache auswählen"},
                {"CORNER_RADIUS", "Eckenradius"},
                {"UNIT", "Einheit"},
                {"INDIVIDUAL_CORNERS", "Einzelne Ecken"},
                {"TOP_LEFT", "Oben Links"},
                {"TOP_RIGHT", "Oben Rechts"},
                {"BOTTOM_LEFT", "Unten Links"},
                {"BOTTOM_RIGHT", "Unten Rechts"},
                {"GLOBAL_RADIUS", "Globaler Radius"},
                {"BORDER_SETTINGS", "Rahmen-Einstellungen"},
                {"FILL_SETTINGS", "Fülleinstellungen"},
                {"FILL_COLOR", "Füllfarbe"},
                {"WIDTH", "Breite"},
                {"COLOR", "Farbe"},
                {"PREVIEWING_ON", "Vorschau auf"},
                {"CHANGES_REALTIME", "Änderungen werden in Echtzeit angewendet"},
                {"START_PREVIEW", "Vorschau Starten"},
                {"STOP_PREVIEW", "Vorschau Stoppen"},
                {"PREVIEW_UNAVAILABLE", "Vorschau nicht verfügbar: Erforderliche Shader fehlen"},
                {"SELECTION_MUST_HAVE", "Auswahl muss eine Image- oder RawImage-Komponente haben"},
                {"QUICK_START", "Schnellstart:"},
                {"STEP_1", "1. Wähle ein GameObject mit Image/RawImage"},
                {"STEP_2", "2. Klicke 'Vorschau Starten' um Effekte zu sehen"},
                {"STEP_3", "3. Passe die Einstellungen unten in Echtzeit an"},
                {"APPLY_TO_SELECTED", "Auf Ausgewählte Anwenden"},
                {"RESET_SETTINGS", "Einstellungen Zurücksetzen"},
                {"SAVE_PRESET", "Preset Speichern"},
                {"LOAD_PRESET", "Preset Laden"},
                {"REFRESH_SHADER_CHECK", "Shader-Prüfung Aktualisieren"},
                {"NO_SELECTION", "Keine Auswahl"},
                {"SELECT_GAMEOBJECT_FIRST", "Bitte wähle zuerst ein GameObject aus."},
                {"INVALID_TARGET", "Ungültiges Ziel"},
                {"OBJECT_MUST_HAVE_IMAGE", "Ausgewähltes Objekt muss eine Image- oder RawImage-Komponente haben."},
                {"SHADERS_MISSING", "Shader Fehlen"},
                {"CANNOT_PREVIEW_SHADERS", "Vorschau nicht möglich: Procedural UI Tool Shader sind nicht installiert oder verfügbar."},
                {"CANNOT_APPLY_SHADERS", "Effekte können nicht angewendet werden: Procedural UI Tool Shader sind nicht installiert oder verfügbar."},
                {"APPLY_COMPLETE", "Anwendung Abgeschlossen"},
                {"APPLIED_EFFECT_TO", "UI-Effekt auf {0} Objekt(e) angewendet."},
                {"PRESET_SAVED", "Preset Gespeichert"},
                {"PRESET_SAVED_SUCCESS", "Preset erfolgreich gespeichert unter:\n{0}"},
                {"LOAD_ERROR", "Ladefehler"},
                {"COULD_NOT_LOAD", "Preset-Datei konnte nicht geladen werden."},
                {"SHADER_NOT_FOUND", "Procedural UI Tool Shader nicht gefunden!"},
                {"CURRENT_PIPELINE", "Aktuelle Pipeline: {0}"},
                {"TRIED_SHADERS", "Versucht: {0}"},
                {"ENSURE_SHADERS", "Bitte stelle sicher, dass die Shader installiert und im Build enthalten sind."},
                {"PROGRESS_BORDER", "Fortschrittsrand"},
                {"ENABLE_PROGRESS_BORDER", "Fortschrittsrand aktivieren"},
                {"PROGRESS_VALUE", "Fortschritt"},
                {"PROGRESS_START_ANGLE", "Startwinkel"},
                {"PROGRESS_DIRECTION", "Richtung"},
                {"SHAPE_SETTINGS", "Formeinstellungen"},
                {"SHAPE_TYPE", "Formtyp"},
                {"STAR_POINTS", "Sternspitzen"},
                {"STAR_INNER_RATIO", "Stern-Innenradius"},
                {"CORNER_RADIUS_SETTINGS", "Eckenradius-Einstellungen"},
                {"EDGE_SHARPNESS", "Kanten-Schärfe"},
                {"PIXEL_PERFECT_EDGES", "Pixelgenaue Kanten"},
                {"CORNER_SETTINGS_HEADER", "Eckenradius-Konfiguration"},
                {"PROGRESS_BORDER_HEADER", "Fortschrittsrand-Konfiguration"},
                {"FILL_CONFIG_HEADER", "Füllkonfiguration"}
            };
            _translations[SupportedLanguage.German] = german;

            var chinese = new Dictionary<string, string>
            {
                {"WINDOW_TITLE", "程序化UI工具"},
                {"WINDOW_SUBTITLE", "程序化UI样式"},
                {"SHADER_STATUS", "着色器状态"},
                {"LIVE_PREVIEW", "实时预览"},
                {"EFFECT_SETTINGS", "效果设置"},
                {"ACTIONS", "操作"},
                {"PRESET_MANAGEMENT", "预设管理"},
                {"LANGUAGE", "语言"},
                {"LANGUAGE_TOOLTIP", "选择界面语言"},
                {"CORNER_RADIUS", "圆角半径"},
                {"UNIT", "单位"},
                {"INDIVIDUAL_CORNERS", "独立圆角"},
                {"TOP_LEFT", "左上"},
                {"TOP_RIGHT", "右上"},
                {"BOTTOM_LEFT", "左下"},
                {"BOTTOM_RIGHT", "右下"},
                {"GLOBAL_RADIUS", "全局半径"},
                {"BORDER_SETTINGS", "边框设置"},
                {"FILL_SETTINGS", "填充设置"},
                {"FILL_COLOR", "填充颜色"},
                {"WIDTH", "宽度"},
                {"COLOR", "颜色"},
                {"PREVIEWING_ON", "预览对象"},
                {"CHANGES_REALTIME", "更改实时应用"},
                {"START_PREVIEW", "开始预览"},
                {"STOP_PREVIEW", "停止预览"},
                {"PREVIEW_UNAVAILABLE", "预览不可用：缺少所需着色器"},
                {"SELECTION_MUST_HAVE", "选择对象必须包含Image或RawImage组件"},
                {"QUICK_START", "快速开始："},
                {"STEP_1", "1. 选择带有Image/RawImage的GameObject"},
                {"STEP_2", "2. 点击开始预览查看效果"},
                {"STEP_3", "3. 实时调整下方设置"},
                {"APPLY_TO_SELECTED", "应用到选中对象"},
                {"RESET_SETTINGS", "重置设置"},
                {"SAVE_PRESET", "保存预设"},
                {"LOAD_PRESET", "加载预设"},
                {"REFRESH_SHADER_CHECK", "刷新着色器检查"},
                {"NO_SELECTION", "未选择对象"},
                {"SELECT_GAMEOBJECT_FIRST", "请先选择一个GameObject。"},
                {"INVALID_TARGET", "无效目标"},
                {"OBJECT_MUST_HAVE_IMAGE", "选中对象必须包含Image或RawImage组件。"},
                {"SHADERS_MISSING", "着色器缺失"},
                {"CANNOT_PREVIEW_SHADERS", "无法预览：Procedural UI Tool着色器未安装或不可用。"},
                {"CANNOT_APPLY_SHADERS", "无法应用效果：Procedural UI Tool着色器未安装或不可用。"},
                {"APPLY_COMPLETE", "应用完成"},
                {"APPLIED_EFFECT_TO", "UI效果已应用到{0}个对象。"},
                {"PRESET_SAVED", "预设已保存"},
                {"PRESET_SAVED_SUCCESS", "预设成功保存到：\n{0}"},
                {"LOAD_ERROR", "加载错误"},
                {"COULD_NOT_LOAD", "无法加载预设文件。"},
                {"SHADER_NOT_FOUND", "未找到Procedural UI Tool着色器！"},
                {"CURRENT_PIPELINE", "当前管线：{0}"},
                {"TRIED_SHADERS", "尝试了：{0}"},
                {"ENSURE_SHADERS", "请确保着色器已安装并包含在构建中。"},
                {"PROGRESS_BORDER", "进度边框"},
                {"ENABLE_PROGRESS_BORDER", "启用进度边框"},
                {"PROGRESS_VALUE", "进度"},
                {"PROGRESS_START_ANGLE", "起始角度"},
                {"PROGRESS_DIRECTION", "方向"},
                {"SHAPE_SETTINGS", "形状设置"},
                {"SHAPE_TYPE", "形状类型"},
                {"STAR_POINTS", "星形点数"},
                {"STAR_INNER_RATIO", "星形内半径"},
                {"CORNER_RADIUS_SETTINGS", "圆角半径设置"},
                {"EDGE_SHARPNESS", "边缘锐度"},
                {"PIXEL_PERFECT_EDGES", "像素完美边缘"},
                {"CORNER_SETTINGS_HEADER", "圆角半径配置"},
                {"PROGRESS_BORDER_HEADER", "进度边框配置"},
                {"FILL_CONFIG_HEADER", "填充配置"}
            };
            _translations[SupportedLanguage.Chinese] = chinese;
        }
    }

    public static class LocalizedGUI
    {
        public static GUIContent Content(string key, string tooltipKey = "")
        {
            string text = LocalizationManager.GetText(key);
            string tooltip = !string.IsNullOrEmpty(tooltipKey) ? LocalizationManager.GetText(tooltipKey) : "";
            return new GUIContent(text, tooltip);
        }

        public static string Text(string key)
        {
            return LocalizationManager.GetText(key);
        }

        public static string Format(string key, params object[] args)
        {
            string formatString = LocalizationManager.GetText(key);
            return string.Format(formatString, args);
        }

        public static void LanguageSelector()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Content("LANGUAGE", "LANGUAGE_TOOLTIP"), GUILayout.Width(60));
            int currentIndex = (int)LocalizationManager.CurrentLanguage;
            string[] languageNames = LocalizationManager.GetLanguageNames();
            int newIndex = EditorGUILayout.Popup(currentIndex, languageNames, GUILayout.Width(100));
            if (newIndex != currentIndex)
            {
                LocalizationManager.CurrentLanguage = (SupportedLanguage)newIndex;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}

