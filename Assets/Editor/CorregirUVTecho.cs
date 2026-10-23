using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Remapea las caras superiores del techo a una zona oscura del atlas.
/// No agrega tapas ni cambia la geometría: conserva el techo original.
/// </summary>
public static class CorregirUVTecho
{
    private const string Carpeta = "Assets/Models/CiudadNueva/Final3Mat2Mat";

    [MenuItem("UTEC/Edificios/Corregir UV de techos sin tapas")]
    public static void Corregir()
    {
        int n = 0;
        foreach (var grupo in new[] {
            new { dir="Assets/Prefabs/Ciudad/Edificios/A2/Paleta", uv=new Vector2(0.10f, 0.60f) },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A1/Paleta", uv=new Vector2(0.10f, 0.25f) },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A2/Original", uv=new Vector2(0.10f, 0.25f) },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A1/Original", uv=new Vector2(0.10f, 0.25f) }
        })
        {
            foreach (var p in Directory.GetFiles(grupo.dir, "*.prefab", SearchOption.TopDirectoryOnly))
            {
                string ruta = p.Replace("\\", "/");
                var content = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    var lod = content.GetComponent<LODGroup>(); if (lod == null) continue;
                    var lv = lod.GetLODs(); if (lv.Length == 0 || lv[0].renderers.Length == 0) continue;
                    var mf = lv[0].renderers[0].GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var src = mf.sharedMesh;
                    var mesh = Remapear(src, grupo.uv, Path.GetFileNameWithoutExtension(ruta));
                    if (mesh != null) { mf.sharedMesh = mesh; PrefabUtility.SaveAsPrefabAsset(content, ruta); n++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[TechoUV] prefabs=" + n);
    }

    private static Mesh Remapear(Mesh src, Vector2 uvTecho, string nombre)
    {
        var v = src.vertices; var nrm = src.normals; var tan = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>();
        var map = new Dictionary<long, int>();
        int cambiados = 0;
        float maxY = src.bounds.max.y;
        int Add(int o, bool techo)
        {
            long k = ((long)o << 1) | (techo ? 1L : 0L); int i;
            if (map.TryGetValue(k, out i)) return i;
            i = nv.Count; map[k] = i; nv.Add(v[o]); if (nrm.Length == v.Length) nn.Add(nrm[o]); if (tan.Length == v.Length) nt.Add(tan[o]); nuv.Add(techo ? uvTecho : uv[o]); if (uv2.Length == v.Length) nuv2.Add(uv2[o]); if (col.Length == v.Length) nc.Add(col[o]); return i;
        }
        var tris = new List<int>[src.subMeshCount]; for (int s = 0; s < tris.Length; s++) tris[s] = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tr = src.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3)
            {
                var a = v[tr[i]]; var b = v[tr[i + 1]]; var c = v[tr[i + 2]];
                var cen = (a + b + c) / 3f;
                var nor = (nrm[tr[i]] + nrm[tr[i + 1]] + nrm[tr[i + 2]]).normalized;
                // Solo caras superiores del techo, no cornisas laterales.
                bool techo = s == 1 && nor.y > 0.55f && cen.y > maxY - 2.2f;
                if (techo) cambiados++;
                tris[s].Add(Add(tr[i], techo)); tris[s].Add(Add(tr[i + 1], techo)); tris[s].Add(Add(tr[i + 2], techo));
            }
        }
        if (cambiados == 0) return null;
        var m = new Mesh(); m.name = nombre + "_TechoUV"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); if (nn.Count == nv.Count) m.SetNormals(nn); if (nt.Count == nv.Count) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2); if (nc.Count == nv.Count) m.SetColors(nc);
        m.subMeshCount = tris.Length; for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s); m.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + m.name + ".asset"); AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
