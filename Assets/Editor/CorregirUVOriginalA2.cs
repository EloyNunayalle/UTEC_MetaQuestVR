using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Corrige solo los techos/pisos de A2 Original: las caras horizontales
/// dejan de muestrear el fake del atlas y pasan a una zona de ladrillo.
/// No cambia colores ni texturas del resto del edificio.
/// </summary>
public static class CorregirUVOriginalA2
{
    private const string Carpeta = "Assets/Models/CiudadNueva/FinalOriginalA2Fix";

    [MenuItem("UTEC/Edificios/Corregir UV techos originales A2")]
    public static void Corregir()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalOriginalA2Fix");
        int n = 0;
        foreach (var p in Directory.GetFiles("Assets/Prefabs/Ciudad/Edificios/A2/Original", "*.prefab", SearchOption.TopDirectoryOnly))
        {
            string ruta = p.Replace("\\", "/");
            var content = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var lod = content.GetComponent<LODGroup>(); if (lod == null) continue;
                var lv = lod.GetLODs(); if (lv.Length == 0 || lv[0].renderers.Length == 0) continue;
                var mf = lv[0].renderers[0].GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = Remapear(mf.sharedMesh, Path.GetFileNameWithoutExtension(ruta));
                if (m != null) { mf.sharedMesh = m; PrefabUtility.SaveAsPrefabAsset(content, ruta); n++; }
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[A2OriginalUV] prefabs=" + n);
    }

    private static Mesh Remapear(Mesh src, string nombre)
    {
        var v = src.vertices; var nrm = src.normals; var tan = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>();
        var map = new Dictionary<long, int>();
        int Add(int o, bool horizontal)
        {
            long k = ((long)o << 1) | (horizontal ? 1L : 0L); int i;
            if (map.TryGetValue(k, out i)) return i;
            i = nv.Count; map[k] = i; nv.Add(v[o]); if (nrm.Length == v.Length) nn.Add(nrm[o]); if (tan.Length == v.Length) nt.Add(tan[o]);
            nuv.Add(horizontal ? new Vector2(0.20f, 0.10f) : uv[o]);
            if (uv2.Length == v.Length) nuv2.Add(uv2[o]); if (col.Length == v.Length) nc.Add(col[o]);
            return i;
        }
        bool changed = false;
        var tris = new List<int>[src.subMeshCount]; for (int s = 0; s < tris.Length; s++) tris[s] = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tr = src.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3)
            {
                var nor = (nrm[tr[i]] + nrm[tr[i + 1]] + nrm[tr[i + 2]]).normalized;
                bool horizontal = s == 0 && Mathf.Abs(nor.y) > 0.55f;
                if (horizontal) changed = true;
                tris[s].Add(Add(tr[i], horizontal)); tris[s].Add(Add(tr[i + 1], horizontal)); tris[s].Add(Add(tr[i + 2], horizontal));
            }
        }
        if (!changed) return null;
        var m = new Mesh(); m.name = nombre + "_A2OrigUV"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); if (nn.Count == nv.Count) m.SetNormals(nn); if (nt.Count == nv.Count) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2); if (nc.Count == nv.Count) m.SetColors(nc);
        m.subMeshCount = tris.Length; for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s); m.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + m.name + ".asset"); AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
