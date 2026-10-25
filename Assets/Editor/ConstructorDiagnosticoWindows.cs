using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera una escena de diagnóstico y construye (modo desarrollo) Measurement.exe,
/// con el objetivo de obtener draw calls reales en build.
/// </summary>
public static class ConstructorDiagnosticoWindows
{
    [MenuItem("UTEC/Diagnostico/1 Construir Windows y medir")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) return;

        var escenaOriginal = SceneManager.GetActiveScene();
        if (escenaOriginal.path == string.Empty)
        {
            Debug.LogWarning("[Diagnostico] no hay escena activa guardada.");
            return;
        }
        if (escenaOriginal.isDirty)
            EditorSceneManager.SaveScene(escenaOriginal);

        var rutaOriginal = escenaOriginal.path;
        var rutaNueva = "Assets/Scenes/Diagnostico_Medicion.unity";

        // Copia del contenido actual para el build (sin tocar la escena original).
        EditorSceneManager.SaveScene(escenaOriginal, rutaNueva);
        EditorSceneManager.OpenScene(rutaNueva, OpenSceneMode.Single);
        var escenaDiagnostic = SceneManager.GetActiveScene();
        var go = new GameObject("DiagnosticoRT");
        go.AddComponent<DiagnosticoRT>();
        EditorSceneManager.MarkSceneDirty(escenaDiagnostic);
        EditorSceneManager.SaveScene(escenaDiagnostic);

        var opciones = new BuildPlayerOptions
        {
            scenes = new string[] { rutaNueva },
            locationPathName = "DiagnosticBuilds/Windows64/Medicion.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        var resultado = BuildPipeline.BuildPlayer(opciones);
        var resumen = resultado.summary;
        var detalles = string.Format(
            "resultado={0}, size={1}, errores={2}, warnings={3}, tiempo={4}",
            resumen.result,
            resumen.totalSize,
            resumen.totalErrors,
            resumen.totalWarnings,
            resumen.totalTime);
        Debug.Log("[Diagnostico] build " + detalles);

        // Restaurar la escena original.
        EditorSceneManager.OpenScene(rutaOriginal, OpenSceneMode.Single);
    }
}
