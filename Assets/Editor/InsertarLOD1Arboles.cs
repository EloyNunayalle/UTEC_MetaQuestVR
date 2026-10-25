using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Inserta el LOD1 decimado en los 26 árboles de la escena y corrige la
/// jerarquía de LODs:
///   LOD0 completo     0.18
///   LOD1 decimado     0.06
///   LOD2 billboard    0.02  (último LOD visible: el culling es a 0.02)
///   LOD3 vacío        0.00  (marcador de culled)
///
/// El LOD vacío anterior en 0.02 no extendía la visibilidad: el árbol se
/// cullaba realmente al pasar al LOD vacío, a 0.06. Ahora el billboard es el
/// último LOD visible y el culling ocurre a 0.02.
///
/// El LOD1 usa las mallas de Assets/Models/StylizedNature/LOD1Final, generadas
/// al 50% (BirchTree_3 al 70% para conservar la copa). Es idempotente.
/// </summary>
public static class InsertarLOD1Arboles
{
    private const string CarpetaLOD1 = "Assets/Models/StylizedNature/LOD1Final";

    [MenuItem("UTEC/Árboles/4 Insertar LOD1 decimado en escena")]
    public static void Insertar()
    {
        if (EditorApplication.isPlaying) return;

        int actualizados = 0;
        int errores = 0;

        foreach (var grupo in Object.FindObjectsOfType<LODGroup>(true))
        {
            string nombre = grupo.gameObject.name;
            if (!nombre.EndsWith("_LOD") || !nombre.Contains("Tree")) continue;

            string tipo = nombre.Substring(0, nombre.Length - 4);
            var malla = AssetDatabase.LoadAssetAtPath<Mesh>(
                CarpetaLOD1 + "/Tree_" + tipo + "_LOD1.asset");
            if (malla == null)
            {
                Debug.LogWarning("[Árboles] Falta LOD1 para " + tipo);
                errores++;
                continue;
            }

            var lods = grupo.GetLODs();
            if (lods.Length < 2 || lods[0].renderers.Length == 0 ||
                lods[1].renderers.Length == 0)
            {
                Debug.LogWarning("[Árboles] LODGroup incompleto: " + nombre);
                errores++;
                continue;
            }

            var l0 = lods[0].renderers[0];
            var billboard = lods[1].renderers[0];

            var hijo = grupo.transform.Find("LOD1_Decimado");
            if (hijo == null)
            {
                var go = new GameObject("LOD1_Decimado");
                go.transform.SetParent(grupo.transform, false);
                hijo = go.transform;
            }

            // Misma transformación que el árbol LOD0 (malla atlas en 1/100).
            hijo.localPosition = l0.transform.localPosition;
            hijo.localRotation = l0.transform.localRotation;
            hijo.localScale = l0.transform.localScale;

            var mf = hijo.GetComponent<MeshFilter>();
            if (mf == null) mf = hijo.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = malla;

            var mr = hijo.GetComponent<MeshRenderer>();
            if (mr == null) mr = hijo.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = l0.sharedMaterials;
            mr.shadowCastingMode = l0.shadowCastingMode;
            mr.receiveShadows = l0.receiveShadows;
            mr.lightProbeUsage = l0.lightProbeUsage;
            mr.reflectionProbeUsage = l0.reflectionProbeUsage;
            hijo.gameObject.isStatic = true;

            grupo.SetLODs(new[]
            {
                new LOD(0.18f, new[] { l0 }),
                new LOD(0.06f, new[] { mr }),
                new LOD(0.02f, new[] { billboard }),
                new LOD(0.0f, new Renderer[0])
            });
            grupo.lastLODBillboard = true;
            grupo.RecalculateBounds();
            actualizados++;
        }

        AssetDatabase.SaveAssets();

        var escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Árboles] LOD1 decimado insertado: " + actualizados +
                  " grupos, errores=" + errores);
    }
}
