using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EnableLightTemperature
{
    [MenuItem("Tools/Rendering/Enable Light Temperature")]
    private static void Enable()
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        GraphicsSettings.lightsUseColorTemperature = true;

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Linear Intensity: {GraphicsSettings.lightsUseLinearIntensity}, " +
            $"Color Temperature: {GraphicsSettings.lightsUseColorTemperature}"
        );
    }
}