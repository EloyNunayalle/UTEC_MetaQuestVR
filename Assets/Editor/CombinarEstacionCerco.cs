using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Combina por material la parte estática de la estación de bomberos y del cerco.
/// Deja intactos los renderers dinámicos (portones, puertas y mecanismos).
/// </summary>
public static class CombinarEstacionCerco
{
    private const string Carpeta = "Assets/Models/Optimizacion";

    [MenuItem("UTEC/Optimizacion/2 Combinar estacion y cerco")]
    public static void Combinar()
    {
        if (EditorApplication.isPlaying) return;

        var escena = SceneManager.GetActiveScene();
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/Optimizacion"));

        CombinarRoot("estacion_bomberos_editada", "Estacion_Combinada", "Estacion", null, false);
        CombinarRoot("Cerco_Patio", "Cerco_Combinado", "Cerco", "Porton_Principal", true);

        var rampa = GameObject.Find("Rampa_Porton");
        if (rampa != null && !rampa.isStatic) rampa.isStatic = true;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    private static void CombinarRoot(string nombreRoot, string nombreComboRoot,
                                     string prefijo, string excluir,
                                     bool incluirNoEstaticos)
    {
        var viejo = GameObject.Find(nombreComboRoot);
        if (viejo != null) Object.DestroyImmediate(viejo);

        var raiz = GameObject.Find(nombreRoot);
        if (raiz == null)
        {
            Debug.LogWarning("[Optimizacion] No existe " + nombreRoot);
            return;
        }

        // Rehabilitar solo los que estaban visibles y ya se combinaron antes.
        foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == excluir) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            if (r.gameObject.isStatic || incluirNoEstaticos) r.enabled = true;
        }

        var combos = new Dictionary<string, List<CombineInstance>>();
        var mats = new Dictionary<string, Material>();
        var aDesactivar = new HashSet<MeshRenderer>();

        foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == excluir) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            if (!incluirNoEstaticos && !r.gameObject.isStatic) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var materiales = r.sharedMaterials;
            if (materiales.Length == 0 || mf.sharedMesh.subMeshCount != materiales.Length) continue;

            if (incluirNoEstaticos) r.gameObject.isStatic = true;
            aDesactivar.Add(r);
            for (int s = 0; s < materiales.Length; s++)
            {
                var m = materiales[s];
                if (m == null) continue;
                if (!combos.ContainsKey(m.name))
                {
                    combos[m.name] = new List<CombineInstance>();
                    mats[m.name] = m;
                }
                combos[m.name].Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = s,
                    transform = r.transform.localToWorldMatrix
                });
            }
        }

        var root = new GameObject(nombreComboRoot);
        int n = 0;
        long tris = 0;
        foreach (var par in combos)
        {
            if (par.Value.Count == 0) continue;
            var malla = new Mesh();
            malla.name = "Combo_" + prefijo + "_" + par.Key;
            malla.indexFormat = IndexFormat.UInt32;
            malla.CombineMeshes(par.Value.ToArray(), true, true);
            malla.RecalculateBounds();

            string ruta = Carpeta + "/Combo_" + prefijo + "_" + par.Key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null)
                AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(malla, ruta);
            tris += malla.triangles.Length / 3;

            var go = new GameObject("Combo_" + par.Key);
            go.transform.SetParent(root.transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = malla;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mats[par.Key] };
            mr.shadowCastingMode = prefijo == "Estacion" &&
                par.Key != "Translucent_Glass_Gray" &&
                par.Key != "Light" && par.Key != "Hose"
                ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.isStatic = true;
            n++;
        }

        foreach (var r in aDesactivar) r.enabled = false;
        Debug.Log("[Optimizacion] " + nombreRoot + ": " + aDesactivar.Count +
                  " renderers -> " + n + " combos, tris=" + tris);
    }
}
