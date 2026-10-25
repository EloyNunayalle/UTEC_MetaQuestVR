using UnityEngine;

/// <summary>
/// Ajusta el sesgo de LOD según el visor. Los umbrales de los LODGroup están
/// pensados para Quest 2; en Quest 3 se mantiene el detalle un poco más lejos.
/// </summary>
public static class AjusteLODDispositivo
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Aplicar()
    {
        if (Application.platform != RuntimePlatform.Android)
            return;

        string modelo = SystemInfo.deviceModel.ToLowerInvariant();

        // Menos cascadas = menos pasada de sombras. Con sombras a 40 m alcanza
        // para la escala de la ciudad y libera draw calls en Quest 2/3.
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadows = ShadowQuality.HardOnly;

        if (modelo.Contains("quest 3"))
            QualitySettings.lodBias = 1.5f;
        else if (modelo.Contains("quest 2"))
            QualitySettings.lodBias = 1.0f;
        else
            QualitySettings.lodBias = 1.0f;
    }
}
