using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Combina por material los props estáticos del tile (veredas, vegetación baja,
/// asfalto, tierra y decals). Deja los GameObjects originales con sus colliders
/// y desactiva únicamente sus renderers. Los combos quedan en un root nuevo.
/// Soporta meshes con varios submeshes si todos sus materiales están en la lista.
/// </summary>
public static class CombinarPropsEstaticos
{
    private const string Carpeta = "Assets/Models/Optimizacion";
    private const string NombreRoot = "Props_Combinados";

    private static readonly string[] Materiales =
    {
        "MI_Trim_MetalConcrete",
        "M_Flowers",
        "M_Cesped",
        "MI_Asphalt",
        "MI_Dirt",
        "MI_StreetDecals"
    };

    private static readonly HashSet<string> Objetivos = new HashSet<string>(Materiales);

    [MenuItem("UTEC/Optimizacion/1 Combinar props estaticos")]
    public static void Combinar()
    {
        if (EditorApplication.isPlaying) return;

        var escena = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(escena.path))
        {
            Debug.LogWarning("[Props] La escena activa no está guardada.");
            return;
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/Optimizacion"));

        var viejo = GameObject.Find(NombreRoot);
        if (viejo != null) Object.DestroyImmediate(viejo);

        // Rehabilitar originales que ya estuvieran combinados.
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.isStatic) continue;
            var mats = r.sharedMaterials;
            if (mats.Length == 0) continue;
            bool todos = true;
            foreach (var m in mats) if (m == null || !Objetivos.Contains(m.name)) { todos = false; break; }
            if (todos) r.enabled = true;
        }

        var combos = new Dictionary<string, List<CombineInstance>>();
        var materiales = new Dictionary<string, Material>();
        var aDesactivar = new HashSet<MeshRenderer>();

        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.isStatic) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mats = r.sharedMaterials;
            if (mats.Length == 0 || mf.sharedMesh.subMeshCount != mats.Length) continue;

            bool todos = true;
            foreach (var m in mats)
                if (m == null || !Objetivos.Contains(m.name)) { todos = false; break; }
            if (!todos) continue;

            aDesactivar.Add(r);
            for (int s = 0; s < mats.Length; s++)
            {
                string nombre = mats[s].name;
                if (!combos.ContainsKey(nombre))
                {
                    combos[nombre] = new List<CombineInstance>();
                    materiales[nombre] = mats[s];
                }
                combos[nombre].Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = s,
                    transform = r.transform.localToWorldMatrix
                });
            }
        }

        var root = new GameObject(NombreRoot);
        int totalCombos = 0;
        long totalTris = 0;

        foreach (var par in combos)
        {
            if (par.Value.Count == 0) continue;
            var mat = materiales[par.Key];

            var malla = new Mesh();
            malla.name = "Combo_" + par.Key;
            malla.indexFormat = IndexFormat.UInt32;
            malla.CombineMeshes(par.Value.ToArray(), true, true);
            malla.RecalculateBounds();

            string ruta = Carpeta + "/Combo_" + par.Key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null)
                AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(malla, ruta);
            totalTris += malla.triangles.Length / 3;

            var go = new GameObject("Combo_" + par.Key);
            go.transform.SetParent(root.transform, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = malla;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { mat };
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.isStatic = true;

            totalCombos++;
            Debug.Log("[Props] " + par.Key + ": " + par.Value.Count +
                      " submeshes -> 1 combo, tris=" + (malla.triangles.Length / 3));
        }

        foreach (var r in aDesactivar) r.enabled = false;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Props] combos=" + totalCombos + ", renderers originales=" +
                  aDesactivar.Count + ", tris combos=" + totalTris);
    }
}
