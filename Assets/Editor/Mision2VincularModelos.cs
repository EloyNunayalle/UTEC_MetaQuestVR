using UnityEditor;
using UnityEngine;

/// <summary>
/// Vincula los modelos de la Mision 2 dentro del prefab de la casa explorable.
/// El extintor CO2 es un archivo local (licencia Fab, no va al repositorio), asi que no se puede
/// referenciar fijo en el prefab: este script lo instancia con la API de Prefab de Unity en cuanto
/// el archivo esta en el proyecto. Tambien repone el modelo del tablero si su instancia quedo rota.
/// Corre solo al abrir/recompilar el proyecto (no modifica nada si ya esta vinculado) y desde el menu
/// UTEC/Mision 2/Vincular modelos de la casa. Sin los archivos, la mision usa modelos simples de reemplazo.
/// </summary>
[InitializeOnLoad]
public static class Mision2VincularModelos
{
    const string RutaPrefab = "Assets/Prefabs/Ciudad/Edificios/A2/Explorable/EDF_A2_Paleta_Rowhouse_Marron_Explorable.prefab";
    const string RutaExtintor = "Assets/Models/ExtintorCO2/co2_fire_extinguisher_low_poly.glb";
    const string RutaTablero = "Assets/Models/TableroElectrico/vr-ready_fuse_box.glb";

    static Mision2VincularModelos()
    {
        EditorApplication.delayCall += () => Vincular(false);
    }

    [MenuItem("UTEC/Mision 2/Vincular modelos de la casa")]
    static void Menu() => Vincular(true);

    static void Vincular(bool avisar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) == null) return;

        var extintorModelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaExtintor);
        var tableroModelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaTablero);
        if (extintorModelo == null && tableroModelo == null)
        {
            if (avisar) Debug.LogWarning("[Mision2] No estan los modelos del extintor CO2 ni del tablero: se usan los de reemplazo.");
            return;
        }

        var raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
        bool cambio = false;
        try
        {
            // ---- Extintor CO2: colgado en el soporte (la raiz del glb trae su propia rotacion) ----
            var ext = raiz.GetComponentInChildren<ExtintorCO2>(true);
            if (ext != null && extintorModelo != null && !TieneModelo(ext.modelo))
            {
                QuitarRoto(ext.transform, "Modelo_Extintor");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(extintorModelo, ext.transform);
                inst.name = "Modelo_Extintor";
                inst.transform.localPosition = new Vector3(0f, -0.30f, 0.13f);
                ext.modelo = inst.transform;
                cambio = true;
            }

            // ---- Tablero: solo si la instancia del prefab no tiene malla (referencia rota) ----
            var panel = raiz.GetComponentInChildren<PanelElectrico>(true);
            if (panel != null && tableroModelo != null)
            {
                var actual = panel.transform.Find("Modelo_Tablero");
                if (!TieneModelo(actual))
                {
                    QuitarRoto(panel.transform, "Modelo_Tablero");
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(tableroModelo, panel.transform);
                    inst.name = "Modelo_Tablero";
                    var rotNodo = inst.transform.localRotation; // rotacion del nodo raiz del glb
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * rotNodo; // frente (-X del modelo) hacia +Z
                    inst.transform.localScale = Vector3.one * 2f;
                    cambio = true;
                }
            }

            if (cambio)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
                Debug.Log("[Mision2] Modelos de la casa explorable vinculados (extintor CO2 / tablero).");
            }
            else if (avisar) Debug.Log("[Mision2] Los modelos de la casa ya estaban vinculados.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    static bool TieneModelo(Transform t) =>
        t != null && !PrefabUtility.IsPrefabAssetMissing(t.gameObject) && t.GetComponentInChildren<Renderer>(true) != null;

    static void QuitarRoto(Transform padre, string nombre)
    {
        for (int i = padre.childCount - 1; i >= 0; i--)
        {
            var hijo = padre.GetChild(i);
            if (hijo.name.StartsWith(nombre)) Object.DestroyImmediate(hijo.gameObject);
        }
    }
}
