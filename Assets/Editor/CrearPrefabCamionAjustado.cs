using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Guarda el camión ya ajustado como prefab propio y reemplaza la instancia
/// desempacada de Persistente por una instancia de ese prefab.
/// </summary>
public static class CrearPrefabCamionAjustado
{
    const string Nombre = "Camion_Bomberos";
    const string Carpeta = "Assets/Prefabs/Juego";
    const string Ruta = Carpeta + "/Camion_Bomberos_Ajustado.prefab";

    [MenuItem("UTEC/Camion/Guardar prefab ajustado y reemplazar")]
    public static void Crear()
    {
        var truck = GameObject.Find(Nombre);
        if (truck == null)
        {
            Debug.LogWarning("[Camion] Abre Persistente; no se encontró " + Nombre + ".");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(truck))
        {
            Debug.LogWarning("[Camion] El camión ya es una instancia de prefab.");
            return;
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Juego"));
        AssetDatabase.Refresh();

        Vector3 pos = truck.transform.position;
        Quaternion rot = truck.transform.rotation;
        Vector3 escala = truck.transform.localScale;

        var prefab = PrefabUtility.SaveAsPrefabAsset(truck, Ruta);
        if (prefab == null)
        {
            Debug.LogError("[Camion] No se pudo guardar el prefab.");
            return;
        }

        Object.DestroyImmediate(truck);
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instancia.name = Nombre;
        instancia.transform.SetPositionAndRotation(pos, rot);
        instancia.transform.localScale = escala;

        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = instancia;

        Debug.Log("[Camion] Prefab ajustado guardado en " + Ruta + " y colocado en la escena.");
    }
}
