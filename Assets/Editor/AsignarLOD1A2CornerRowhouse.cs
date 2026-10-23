using UnityEditor;
using UnityEngine;

/// <summary>
/// Corner y Rowhouse tienen muy pocos triángulos: su LOD1 usa el mismo mesh
/// corregido del LOD0 para conservar el fake y el techo cerrado. No toca A1 ni Paleta.
/// </summary>
public static class AsignarLOD1A2CornerRowhouse
{
    [MenuItem("UTEC/LOD1/Asignar LOD1 A2 Corner y Rowhouse")]
    public static void Asignar()
    {
        if (EditorApplication.isPlaying) return;
        int n = 0;
        foreach (var tipo in new[] { "Corner", "Rowhouse" })
        {
            string ruta = "Assets/Prefabs/Ciudad/Edificios/A2/Original/EDF_A2_Original_" + tipo + ".prefab";
            var content = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var lod = content.GetComponent<LODGroup>();
                if (lod == null) continue;
                var niveles = lod.GetLODs();
                if (niveles.Length == 0 || niveles[0].renderers.Length == 0) continue;
                var r0 = niveles[0].renderers[0];
                var mf0 = r0.GetComponent<MeshFilter>();
                if (mf0 == null || mf0.sharedMesh == null) continue;

                Transform l1 = null;
                foreach (Transform t in content.transform) if (t.name == "LOD1_Decimado") { l1 = t; break; }
                if (l1 == null) continue;
                var mf1 = l1.GetComponent<MeshFilter>();
                var mr1 = l1.GetComponent<MeshRenderer>();
                if (mf1 == null || mr1 == null) continue;

                mf1.sharedMesh = mf0.sharedMesh;
                mr1.sharedMaterials = r0.sharedMaterials;
                mr1.shadowCastingMode = r0.shadowCastingMode;
                mr1.receiveShadows = r0.receiveShadows;

                PrefabUtility.SaveAsPrefabAsset(content, ruta);
                n++;
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[A2OriginalLOD1] corner/rowhouse=" + n);
    }
}
