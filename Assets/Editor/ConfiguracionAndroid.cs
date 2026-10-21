using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Fuerza la compresión de texturas ASTC para Android/Quest.
///
/// El ajuste "Texture Compression" del Build Settings se guarda en Library/
/// (no versionado), así que cada máquina lo perdía. Este script lo fija:
/// - al abrir el proyecto / recargar el dominio ([InitializeOnLoad])
/// - antes de cada build de Android (IPreprocessBuildWithReport)
///
/// Menú: UTEC > Forzar ASTC (Android)
/// </summary>
[InitializeOnLoad]
public static class ConfiguracionAndroid
{
    const MobileTextureSubtarget Compresion = MobileTextureSubtarget.ASTC;

    static ConfiguracionAndroid()
    {
        Aplicar();
    }

    [MenuItem("UTEC/Forzar ASTC (Android)")]
    static void ForzarMenu()
    {
        Aplicar();
        Debug.Log("[ConfiguracionAndroid] Compresión de texturas Android = ASTC");
    }

    static void Aplicar()
    {
        if (EditorUserBuildSettings.androidBuildSubtarget != Compresion)
            EditorUserBuildSettings.androidBuildSubtarget = Compresion;
    }
}

/// <summary>Garantiza ASTC justo antes de compilar para Android.</summary>
public class ForzarASTCEnBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
    }
}
