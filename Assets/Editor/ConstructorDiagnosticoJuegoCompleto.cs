using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye un player Windows con el juego completo:
///   Persistente + Ciudad_Tile_00_Estacion + Estacion_Interior.
/// Agrega temporalmente DiagnosticoRT a la escena del tile, construye y
/// restaura la escena al terminar. El CSV se llama diagnostico_juego.csv.
/// </summary>
public static class ConstructorDiagnosticoJuegoCompleto
{
    private const string EscenaPersistente = "Assets/Scenes/Persistente.unity";
    private const string EscenaTile = "Assets/Scenes/Ciudad_Tile_00_Estacion.unity";
    private const string EscenaInterior = "Assets/Scenes/Estacion_Interior.unity";
    private const string NombreDiagnostico = "DiagnosticoRT";
    private const string CarpetaBuild = "DiagnosticBuilds/Windows64";
    private const string Exe = "JuegoCompleto.exe";

    [MenuItem("UTEC/Diagnostico/2 Construir juego completo y medir")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) return;

        string escenaPrevia = SceneManager.GetActiveScene().path;
        var tile = EditorSceneManager.OpenScene(EscenaTile, OpenSceneMode.Single);

        var existente = GameObject.Find(NombreDiagnostico);
        if (existente != null) Object.DestroyImmediate(existente);

        var diagnostico = new GameObject(NombreDiagnostico);
        diagnostico.AddComponent<DiagnosticoRT>();
        EditorSceneManager.MarkSceneDirty(tile);
        EditorSceneManager.SaveScene(tile);
        Debug.Log("[DiagnosticoJuego] DiagnosticoRT agregado temporalmente a " + EscenaTile);

        try
        {
            if (Directory.Exists(CarpetaBuild))
                Directory.Delete(CarpetaBuild, true);
            Directory.CreateDirectory(CarpetaBuild);

            var opciones = new BuildPlayerOptions
            {
                scenes = new[] { EscenaPersistente, EscenaTile, EscenaInterior },
                locationPathName = CarpetaBuild + "/" + Exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var reporte = BuildPipeline.BuildPlayer(opciones);
            var resumen = reporte.summary;
            Debug.Log("[DiagnosticoJuego] build=" + resumen.result +
                      " exe=" + opciones.locationPathName +
                      " size=" + resumen.totalSize +
                      " errores=" + resumen.totalErrors +
                      " warnings=" + resumen.totalWarnings +
                      " tiempo=" + resumen.totalTime);
        }
        finally
        {
            var temporal = GameObject.Find(NombreDiagnostico);
            if (temporal != null) Object.DestroyImmediate(temporal);
            EditorSceneManager.MarkSceneDirty(tile);
            EditorSceneManager.SaveScene(tile);
            Debug.Log("[DiagnosticoJuego] DiagnosticoRT removido de la escena del tile");

            if (!string.IsNullOrEmpty(escenaPrevia))
                EditorSceneManager.OpenScene(escenaPrevia, OpenSceneMode.Single);
        }
    }
}
