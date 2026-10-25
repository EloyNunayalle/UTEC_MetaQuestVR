using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Agrega un LODGroup al exterior de la estación de bomberos:
///   LOD0: estructura actual (combos estáticos + portón/puerta dinámicos)
///   LOD1: estructura combinada duplicada + portón cerrado simple
///   LOD2: caja lejana de 1 material
///   Culled
/// No toca colliders ni scripts; solo renderers.
/// </summary>
public static class AgregarLODEstacion
{
    private const string RootLOD1 = "Estacion_LOD1";
    private const string RootBox = "Estacion_LOD2_Box";

    [MenuItem("UTEC/Optimizacion/4 LOD exterior estacion")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) return;

        var estacion = GameObject.Find("estacion_bomberos_editada");
        if (estacion == null)
        {
            Debug.LogWarning("[Estacion] No se encontró estacion_bomberos_editada");
            return;
        }

        var combinada = GameObject.Find("Estacion_Combinada");
        var viejoLod = estacion.GetComponent<LODGroup>();
        if (viejoLod != null) Object.DestroyImmediate(viejoLod);
        foreach (var n in new[] { RootLOD1, RootBox })
        {
            var viejo = GameObject.Find(n);
            if (viejo != null) Object.DestroyImmediate(viejo);
        }

        // LOD0: dinámicos de la estación + combos estáticos ya existentes.
        var lod0 = new List<Renderer>();
        foreach (var r in estacion.GetComponentsInChildren<MeshRenderer>(true))
            if (r.enabled) lod0.Add(r);
        if (combinada != null)
            foreach (var r in combinada.GetComponentsInChildren<MeshRenderer>(true))
                if (r.enabled) lod0.Add(r);

        if (lod0.Count == 0)
        {
            Debug.LogWarning("[Estacion] Sin renderers para LOD0");
            return;
        }

        var bounds = lod0[0].bounds;
        foreach (var r in lod0) bounds.Encapsulate(r.bounds);

        // LOD1: duplicar combos estáticos + portón cerrado simple.
        var root1 = new GameObject(RootLOD1);
        var lod1 = new List<Renderer>();
        if (combinada != null)
        {
            foreach (var r in combinada.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled) continue;
                lod1.Add(Duplicar(r, root1.transform));
            }
        }

        var porton = estacion.GetComponentInChildren<MeshRenderer>(true);
        foreach (var r in estacion.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name == "Porton_Cochera") { porton = r; break; }

        if (porton != null)
        {
            Material matPorton = porton.sharedMaterials.Length > 0 ? porton.sharedMaterials[0] : null;
            var go = CrearCaja("Porton_LOD1", root1.transform, porton.bounds.center,
                porton.bounds.size, matPorton);
            var mr = go.GetComponent<MeshRenderer>();
            mr.enabled = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            lod1.Add(mr);
        }

        // LOD2: caja simple con material de la estación.
        var root2 = new GameObject(RootBox);
        Material matCaja = null;
        if (combinada != null)
        {
            foreach (var r in combinada.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.name.Contains("Concrete") || m.name.Contains("Brick") ||
                        m.name.Contains("Wood") || m.name.Contains("Painted"))
                    {
                        matCaja = m;
                        break;
                    }
                }
                if (matCaja != null) break;
            }
        }
        var caja = CrearCaja("Caja_Estacion", root2.transform, bounds.center, bounds.size, matCaja);
        var cajaMr = caja.GetComponent<MeshRenderer>();
        cajaMr.enabled = false;
        cajaMr.shadowCastingMode = ShadowCastingMode.Off;

        var lg = estacion.AddComponent<LODGroup>();
        lg.SetLODs(new[]
        {
            new LOD(0.25f, lod0.ToArray()),
            new LOD(0.08f, lod1.ToArray()),
            new LOD(0.03f, new[] { cajaMr }),
            new LOD(0.0f, new Renderer[0])
        });
        lg.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        lg.localReferencePoint = estacion.transform.InverseTransformPoint(bounds.center);
        lg.lastLODBillboard = false;
        lg.RecalculateBounds();

        var escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Estacion] LOD agregado: LOD0=" + lod0.Count +
                  ", LOD1=" + lod1.Count + ", LOD2=1");
    }

    private static MeshRenderer Duplicar(MeshRenderer origen, Transform padre)
    {
        var go = new GameObject(origen.name + "_LOD1");
        go.transform.SetParent(padre, false);
        go.transform.position = origen.transform.position;
        go.transform.rotation = origen.transform.rotation;
        go.transform.localScale = origen.transform.localScale;
        go.AddComponent<MeshFilter>().sharedMesh = origen.GetComponent<MeshFilter>().sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = origen.sharedMaterials;
        mr.shadowCastingMode = origen.shadowCastingMode;
        mr.receiveShadows = origen.receiveShadows;
        go.isStatic = true;
        mr.enabled = false;
        return mr;
    }

    private static GameObject CrearCaja(string nombre, Transform padre, Vector3 centro,
                                        Vector3 tam, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        go.transform.localScale = tam;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }
}
