using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inserta el LOD1 decimado en los prefabs finales A1/A2 y rearma el LODGroup:
/// LOD0 completo, LOD1 decimado, caja, culled.
/// Los materiales del LOD1 son los mismos del LOD0.
/// </summary>
public static class AgregarLOD1Prefabs
{
    private const string LOD1 = "Assets/Models/CiudadNueva/LOD1Final";

    [MenuItem("UTEC/LOD1/2 Insertar LOD1 en prefabs")]
    public static void Insertar()
    {
        int n = 0, errores = 0;
        foreach (var dir in new[] { "Assets/Prefabs/Ciudad/Edificios/A1", "Assets/Prefabs/Ciudad/Edificios/A2" })
        {
            foreach (var p in Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories))
            {
                string ruta = p.Replace("\\", "/");
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                string fbx = LOD1 + "/" + nombre + ".fbx";
                var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (fbxGo == null) { Debug.LogWarning("[LOD1] Falta FBX " + fbx); errores++; continue; }
                Mesh fbxMesh = null;
                foreach (var mf in fbxGo.GetComponentsInChildren<MeshFilter>(true)) { if (mf.sharedMesh != null) { fbxMesh = mf.sharedMesh; break; } }
                if (fbxMesh == null) { errores++; continue; }

                var content = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    var lod = content.GetComponent<LODGroup>();
                    if (lod == null) { errores++; continue; }
                    var niveles = lod.GetLODs();
                    if (niveles.Length == 0 || niveles[0].renderers.Length == 0) { errores++; continue; }
                    var l0 = niveles[0].renderers[0];
                    Renderer caja = null;
                    foreach (var r in content.GetComponentsInChildren<Renderer>(true))
                        if (r != l0 && r.name.Contains("Caja")) { caja = r; break; }
                    if (caja == null) { errores++; continue; }

                    var viejo = content.transform.Find("LOD1_Decimado");
                    if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

                    var hijo = new GameObject("LOD1_Decimado");
                    hijo.transform.SetParent(content.transform, false);
                    var tr0 = l0.transform;
                    hijo.transform.localPosition = tr0.localPosition;
                    hijo.transform.localRotation = tr0.localRotation;
                    hijo.transform.localScale = tr0.localScale;
                    hijo.AddComponent<MeshFilter>().sharedMesh = fbxMesh;
                    var mr = hijo.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = l0.sharedMaterials;
                    mr.shadowCastingMode = l0.shadowCastingMode;
                    mr.receiveShadows = l0.receiveShadows;
                    hijo.isStatic = true;

                    var nivelesNuevos = new List<LOD>();
                    nivelesNuevos.Add(new LOD(0.25f, new Renderer[] { l0 }));
                    nivelesNuevos.Add(new LOD(0.12f, new Renderer[] { mr }));
                    nivelesNuevos.Add(new LOD(0.05f, new Renderer[] { caja }));
                    nivelesNuevos.Add(new LOD(0.02f, new Renderer[0]));
                    lod.SetLODs(nivelesNuevos.ToArray());
                    lod.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(content, ruta);
                    n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LOD1] prefabs actualizados=" + n + " errores=" + errores);
    }
}
