using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Agrupa los 26 árboles (LOD0 + LOD1 + billboard) en celdas de 25 m.
/// Cada celda queda con un solo LODGroup y un renderer por nivel:
///   26 LODGroups/renderers activos -> 7 LODGroups/renderers activos.
/// No toca colliders: los árboles originales conservan sus GameObjects, solo
/// se desactivan sus LODGroup y MeshRenderer.
/// </summary>
public static class AgruparArbolesPorCelda
{
    private const float TamCelda = 25f;
    private const string Carpeta = "Assets/Models/Optimizacion/Arboles";
    private const string NombreRoot = "Arboles_Celda";

    [MenuItem("UTEC/Optimizacion/5 Agrupar arboles por celda (25 m)")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) return;

        var escena = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(escena.path)) return;

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/Optimizacion/Arboles"));
        AssetDatabase.Refresh();

        var viejo = GameObject.Find(NombreRoot);
        if (viejo != null) Object.DestroyImmediate(viejo);

        // Rehabilitar los árboles originales si ya se agrupó antes.
        var grupos = new List<LODGroup>();
        foreach (var lg in Object.FindObjectsOfType<LODGroup>(true))
        {
            if (!EsArbol(lg)) continue;
            lg.enabled = true;
            foreach (var lod in lg.GetLODs())
                foreach (var r in lod.renderers)
                    if (r != null) r.enabled = true;
            grupos.Add(lg);
        }

        var celdas = new Dictionary<string, List<LODGroup>>();
        foreach (var lg in grupos)
        {
            string clave = Mathf.FloorToInt(lg.transform.position.x / TamCelda) + "_" +
                           Mathf.FloorToInt(lg.transform.position.z / TamCelda);
            if (!celdas.TryGetValue(clave, out var lista))
                celdas[clave] = lista = new List<LODGroup>();
            lista.Add(lg);
        }

        var root = new GameObject(NombreRoot);
        int creados = 0;
        long tris = 0;

        foreach (var par in celdas)
        {
            var lista = par.Value;
            var lod0 = new List<CombineInstance>();
            var lod1 = new List<CombineInstance>();
            var billboards = new List<CombineInstance>();
            Material matLod0 = null, matLod1 = null, matBillboard = null;
            var sombraLod0 = ShadowCastingMode.On;
            var sombraLod1 = ShadowCastingMode.On;
            float alturaMax = 0f;
            Vector3 centro = Vector3.zero;
            int validos = 0;

            foreach (var lg in lista)
            {
                var lods = lg.GetLODs();
                if (lods.Length < 3) continue;
                var r0 = PrimerRenderer(lods[0]);
                var r1 = PrimerRenderer(lods[1]);
                var rb = PrimerRenderer(lods[2]);
                if (r0 == null || r1 == null || rb == null) continue;

                var mf0 = r0.GetComponent<MeshFilter>();
                var mf1 = r1.GetComponent<MeshFilter>();
                var mfb = rb.GetComponent<MeshFilter>();
                if (mf0 == null || mf0.sharedMesh == null ||
                    mf1 == null || mf1.sharedMesh == null ||
                    mfb == null || mfb.sharedMesh == null) continue;

                var m0 = r0.sharedMaterial;
                var m1 = r1.sharedMaterial;
                var mb = rb.sharedMaterial;
                if (m0 == null || m1 == null || mb == null) continue;
                if (matLod0 == null) { matLod0 = m0; matLod1 = m1; matBillboard = mb; }
                else if (m0 != matLod0 || m1 != matLod1 || mb != matBillboard) continue;

                lod0.Add(new CombineInstance { mesh = mf0.sharedMesh, subMeshIndex = 0, transform = r0.transform.localToWorldMatrix });
                lod1.Add(new CombineInstance { mesh = mf1.sharedMesh, subMeshIndex = 0, transform = r1.transform.localToWorldMatrix });
                billboards.Add(new CombineInstance { mesh = mfb.sharedMesh, subMeshIndex = 0, transform = rb.transform.localToWorldMatrix });
                sombraLod0 = r0.shadowCastingMode;
                sombraLod1 = r1.shadowCastingMode;
                alturaMax = Mathf.Max(alturaMax, r0.bounds.size.y);
                centro += lg.transform.position;
                validos++;
            }

            if (validos == 0) continue;
            centro /= validos;

            string id = "Celda_" + par.Key;
            var malla0 = CrearCombo(lod0, "Arboles_" + id + "_LOD0", id + "_LOD0");
            var malla1 = CrearCombo(lod1, "Arboles_" + id + "_LOD1", id + "_LOD1");
            var mallab = CrearCombo(billboards, "Arboles_" + id + "_Billboard", id + "_Billboard");
            tris += malla0.triangles.Length / 3 + malla1.triangles.Length / 3 + mallab.triangles.Length / 3;

            var go = new GameObject("Arboles_" + id);
            go.transform.SetParent(root.transform, false);
            go.transform.position = Vector3.zero;

            var mr0 = CrearRenderer(go.transform, "LOD0", malla0, matLod0, sombraLod0);
            var mr1 = CrearRenderer(go.transform, "LOD1", malla1, matLod1, sombraLod1);
            var mrb = CrearRenderer(go.transform, "Billboard", mallab, matBillboard, ShadowCastingMode.Off);

            var lgNuevo = go.AddComponent<LODGroup>();
            lgNuevo.SetLODs(new[]
            {
                new LOD(0.18f, new Renderer[] { mr0 }),
                new LOD(0.06f, new Renderer[] { mr1 }),
                new LOD(0.02f, new Renderer[] { mrb }),
                new LOD(0.0f, new Renderer[0])
            });
            lgNuevo.size = Mathf.Max(0.1f, alturaMax);
            lgNuevo.localReferencePoint = centro;
            lgNuevo.lastLODBillboard = false;

            foreach (var lg in lista)
            {
                lg.enabled = false;
                foreach (var lod in lg.GetLODs())
                    foreach (var r in lod.renderers)
                        if (r != null) r.enabled = false;
            }

            creados++;
            Debug.Log("[Arboles] " + id + " arboles=" + validos + " altura=" + alturaMax.ToString("F1") +
                      " trisLOD0=" + (malla0.triangles.Length / 3));
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Arboles] celdas=" + creados + " de " + grupos.Count + " arboles, tris=" + tris);
    }

    private static bool EsArbol(LODGroup lg)
    {
        string n = lg.gameObject.name.ToLowerInvariant();
        return n.Contains("tree");
    }

    private static Renderer PrimerRenderer(LOD lod)
    {
        if (lod.renderers == null || lod.renderers.Length == 0) return null;
        return lod.renderers[0];
    }

    private static Mesh CrearCombo(List<CombineInstance> instancias, string nombre, string archivo)
    {
        var malla = new Mesh();
        malla.name = nombre;
        malla.indexFormat = IndexFormat.UInt32;
        malla.CombineMeshes(instancias.ToArray(), true, true);
        malla.RecalculateBounds();

        string ruta = Carpeta + "/" + archivo + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null)
            AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
        return malla;
    }

    private static MeshRenderer CrearRenderer(Transform padre, string nombre, Mesh malla,
                                               Material material, ShadowCastingMode sombra)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = sombra;
        mr.receiveShadows = true;
        go.isStatic = true;
        return mr;
    }
}
