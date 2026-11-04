using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reduce el ancho visual del camión de forma determinista:
/// - escala en X solo las piezas del modelo (cuerpo, puertas, ruedas, ventanas,
///   interior del GLB),
/// - mantiene el tamaño del timón/tablero Mercedes, palanca, GPS y luz de cabina,
/// - recoloca esos agregados por el mismo factor para que no haya que moverlos a mano,
/// - ajusta el BoxCollider y el factor de la luz de cabina.
/// Objetivo: 2.60 m de ancho total (el modelo actual mide ~2.98 m).
/// </summary>
public static class AjustarAnchoCamion
{
    const string Nombre = "Camion_Bomberos";
    const float AnchoObjetivo = 2.60f;

    static readonly string[] PiezasModelo =
    {
        "DOOR 1L", "DOOR 1R", "DOOR 2L", "DOOR 2R",
        "INTERIOR.001", "INTERIOR.002",
        "WHEEL BIG.1L.001", "WHEEL BIG.1R.001",
        "WHEEL BIG.2L.001", "WHEEL BIG.2R.001",
        "WINDOWS"
    };

    static readonly string[] Agregados =
    {
        "Panel_Mercedes", "Palanca_Cambios", "Pantalla_GPS", "TEMP_Luz_Cabina"
    };

    [MenuItem("UTEC/Camion/Ajustar ancho visual (2.60 m)")]
    public static void Ajustar()
    {
        var truck = GameObject.Find(Nombre);
        if (truck == null)
        {
            Debug.LogWarning("[Camion] Abre Persistente; no se encontró " + Nombre + ".");
            return;
        }

        if (truck.transform.Find("Modelo_Ancho") != null)
        {
            Debug.LogWarning("[Camion] El ancho ya fue ajustado (existe Modelo_Ancho).");
            return;
        }

        // El camión es una instancia del GLB; hay que desempacar para poder
        // reparentar sus piezas bajo un wrapper sin que Unity revierta el cambio.
        if (PrefabUtility.IsPartOfPrefabInstance(truck))
            PrefabUtility.UnpackPrefabInstance(truck, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        float anchoActual = MedirAnchoModelo(truck.transform);
        if (anchoActual < 1f)
        {
            Debug.LogWarning("[Camion] No se pudo medir el ancho del modelo.");
            return;
        }

        float factor = AnchoObjetivo / anchoActual;

        var wrapper = new GameObject("Modelo_Ancho").transform;
        wrapper.SetParent(truck.transform, false);
        wrapper.localPosition = Vector3.zero;
        wrapper.localRotation = Quaternion.identity;
        wrapper.localScale = Vector3.one;

        // El cuerpo principal está en el propio root (MeshRenderer del GLB).
        // Se replica como hijo del wrapper y se desactiva el original.
        var mfRoot = truck.GetComponent<MeshFilter>();
        var mrRoot = truck.GetComponent<MeshRenderer>();
        if (mfRoot != null && mrRoot != null && mfRoot.sharedMesh != null)
        {
            var cuerpo = new GameObject("Cuerpo_Modelo");
            cuerpo.transform.SetParent(wrapper, false);
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            cuerpo.transform.localScale = Vector3.one;

            cuerpo.AddComponent<MeshFilter>().sharedMesh = mfRoot.sharedMesh;
            var mr = cuerpo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mrRoot.sharedMaterials;
            mr.shadowCastingMode = mrRoot.shadowCastingMode;
            mr.receiveShadows = mrRoot.receiveShadows;
            mr.lightProbeUsage = mrRoot.lightProbeUsage;
            mr.reflectionProbeUsage = mrRoot.reflectionProbeUsage;
            mrRoot.enabled = false;
        }

        foreach (var nombre in PiezasModelo)
        {
            var pieza = truck.transform.Find(nombre);
            if (pieza != null) pieza.SetParent(wrapper, true);
        }

        wrapper.localScale = new Vector3(factor, 1f, 1f);

        // Los agregados conservan su tamaño; solo se desplazan en X.
        foreach (var nombre in Agregados)
        {
            var agregado = truck.transform.Find(nombre);
            if (agregado == null) continue;
            var p = agregado.localPosition;
            p.x *= factor;
            agregado.localPosition = p;
        }

        var box = truck.GetComponent<BoxCollider>();
        if (box != null)
        {
            var s = box.size;
            s.x *= factor;
            box.size = s;
            var c = box.center;
            c.x *= factor;
            box.center = c;
        }

        var interior = Object.FindObjectOfType<InteriorCamion>();
        if (interior != null)
        {
            interior.factorAnchoVisual = factor;
            EditorUtility.SetDirty(interior);
        }

        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = truck;
        Debug.Log($"[Camion] Ancho {anchoActual:0.000} m -> {AnchoObjetivo:0.000} m; factor X = {factor:0.0000}.");
    }

    static float MedirAnchoModelo(Transform truck)
    {
        float min = float.MaxValue, max = float.MinValue;
        var renderers = new List<Renderer>();
        var mrRoot = truck.GetComponent<MeshRenderer>();
        if (mrRoot != null) renderers.Add(mrRoot);
        foreach (var nombre in PiezasModelo)
        {
            var pieza = truck.transform.Find(nombre);
            if (pieza != null) renderers.AddRange(pieza.GetComponentsInChildren<Renderer>(true));
        }

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var esquina = new Vector3(
                    ((i & 1) == 0 ? b.min.x : b.max.x),
                    ((i & 2) == 0 ? b.min.y : b.max.y),
                    ((i & 4) == 0 ? b.min.z : b.max.z));
                float x = truck.InverseTransformPoint(esquina).x;
                if (x < min) min = x;
                if (x > max) max = x;
            }
        }

        return max > min ? max - min : 0f;
    }
}
