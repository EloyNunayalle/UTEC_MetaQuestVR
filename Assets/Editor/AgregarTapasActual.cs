using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agrega una tapa superior a los prefabs A1/A2 actuales para que al mirar
/// desde arriba no se vea el interior ni se pierdan los bordes.
/// Usa el submesh opaco de detalle/atlas, sin agregar materiales.
/// </summary>
public static class AgregarTapasActual
{
    private const string Carpeta = "Assets/Models/CiudadNueva/Final3Mat2Mat";

    [MenuItem("UTEC/Edificios/Agregar tapas superiores A1/A2 actuales")]
    public static void Agregar()
    {
        int n = 0;
        foreach (var dir in new[] { "Assets/Prefabs/Ciudad/Edificios/A1/Paleta", "Assets/Prefabs/Ciudad/Edificios/A2/Paleta" })
        {
            bool a1 = dir.Contains("A1");
            foreach (var p in Directory.GetFiles(dir, "*.prefab", SearchOption.TopDirectoryOnly))
            {
                string ruta = p.Replace("\\", "/");
                var content = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    var lod = content.GetComponent<LODGroup>();
                    if (lod == null) continue;
                    var lv = lod.GetLODs();
                    if (lv.Length == 0 || lv[0].renderers.Length == 0) continue;
                    var mf = lv[0].renderers[0].GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var src = mf.sharedMesh;
                    var v = src.vertices; var nrm = src.normals; var tan = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
                    var nv = new List<Vector3>(v); var nn = new List<Vector3>(nrm); var nt = new List<Vector4>(tan); var nuv = new List<Vector2>(uv); var nuv2 = new List<Vector2>(uv2); var nc = new List<Color>(col);
                    var b = src.bounds; int start = nv.Count;
                    Vector2 capUv = a1 ? new Vector2(0.10f, 0.25f) : new Vector2(0.10f, 0.50f);
                    nv.Add(new Vector3(b.min.x, b.max.y + 0.003f, b.min.z)); nn.Add(Vector3.up); nuv.Add(capUv);
                    nv.Add(new Vector3(b.max.x, b.max.y + 0.003f, b.min.z)); nn.Add(Vector3.up); nuv.Add(capUv);
                    nv.Add(new Vector3(b.max.x, b.max.y + 0.003f, b.max.z)); nn.Add(Vector3.up); nuv.Add(capUv);
                    nv.Add(new Vector3(b.min.x, b.max.y + 0.003f, b.max.z)); nn.Add(Vector3.up); nuv.Add(capUv);
                    var tris = new List<int>[src.subMeshCount]; for (int s = 0; s < tris.Length; s++) tris[s] = new List<int>(src.GetTriangles(s));
                    tris[1].Add(start); tris[1].Add(start + 2); tris[1].Add(start + 1);
                    tris[1].Add(start); tris[1].Add(start + 3); tris[1].Add(start + 2);
                    var m = new Mesh(); m.name = src.name + "_Tapa"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    m.SetVertices(nv); if (nn.Count == nv.Count) m.SetNormals(nn); if (nt.Count == nv.Count) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2); if (nc.Count == nv.Count) m.SetColors(nc);
                    m.subMeshCount = src.subMeshCount; for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s); m.RecalculateBounds();
                    string mp = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + m.name + ".asset"); AssetDatabase.CreateAsset(m, mp);
                    mf.sharedMesh = m;
                    PrefabUtility.SaveAsPrefabAsset(content, ruta);
                    n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Tapas] prefabs=" + n);
    }
}
