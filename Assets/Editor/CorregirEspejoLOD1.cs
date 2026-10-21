using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// El pipeline OBJ -> Blender -> FBX devuelve la malla espejada en X.
/// Este corrector crea la malla definitiva del LOD1 desde la importada del FBX,
/// invierte X/normales/tangentes y el winding, y la asigna al prefab.
/// </summary>
public static class CorregirEspejoLOD1
{
    private const string Carpeta = "Assets/Models/CiudadNueva/LOD1Final";

    [MenuItem("UTEC/LOD1/3 Corregir espejo X de LOD1")]
    public static void Corregir()
    {
        int n = 0, errores = 0;
        foreach (var dir in new[] { "Assets/Prefabs/Ciudad/Edificios/A1", "Assets/Prefabs/Ciudad/Edificios/A2" })
        {
            foreach (var p in Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories))
            {
                string ruta = p.Replace("\\", "/");
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                var content = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    Transform l1 = null;
                    foreach (Transform t in content.transform) if (t.name == "LOD1_Decimado") { l1 = t; break; }
                    if (l1 == null) { errores++; continue; }
                    var mf = l1.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { errores++; continue; }
                    var src = mf.sharedMesh;

                    string meshPath = Carpeta + "/Mesh_" + nombre + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = new Mesh(); mesh.name = "LOD1_" + nombre;
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    CopiarEspejado(src, mesh);
                    mf.sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(content, ruta);
                    n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[LOD1] espejo corregido=" + n + " errores=" + errores);
    }

    private static void CopiarEspejado(Mesh src, Mesh dst)
    {
        var v = src.vertices; for (int i = 0; i < v.Length; i++) v[i].x = -v[i].x;
        var nrm = src.normals; for (int i = 0; i < nrm.Length; i++) nrm[i].x = -nrm[i].x;
        var tan = src.tangents; for (int i = 0; i < tan.Length; i++) tan[i].x = -tan[i].x;
        dst.Clear();
        dst.indexFormat = src.indexFormat;
        dst.SetVertices(v);
        if (nrm.Length == v.Length) dst.SetNormals(nrm);
        if (tan.Length == v.Length) dst.SetTangents(tan);
        if (src.uv != null && src.uv.Length == v.Length) dst.SetUVs(0, src.uv);
        if (src.uv2 != null && src.uv2.Length == v.Length) dst.SetUVs(1, src.uv2);
        if (src.colors != null && src.colors.Length == v.Length) dst.SetColors(src.colors);
        dst.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tr = src.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3) { int tmp = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = tmp; }
            dst.SetTriangles(tr, s);
        }
        dst.RecalculateBounds();
        EditorUtility.SetDirty(dst);
    }
}
