using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Quita las tapas superiores que sobresalían y hace doble cara el material
/// opaco de detalle/atlas. Así los bordes del techo no desaparecen desde arriba
/// sin agregar geometría extra.
/// </summary>
public static class QuitarTapasDobleCara
{
    private const string Carpeta = "Assets/Models/CiudadNueva/Final3Mat2Mat";
    private static Shader _ds;

    [MenuItem("UTEC/Edificios/Quitar tapas y poner detalle a doble cara")]
    public static void Ejecutar()
    {
        _ds = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/StandardDobleCara.shader");
        if (_ds == null) { Debug.LogError("No se encontró StandardDobleCara.shader"); return; }
        int n = 0;
        foreach (var grupo in new[] {
            new { dir="Assets/Prefabs/Ciudad/Edificios/A1/Paleta", mat="Assets/Models/CiudadNueva/Final3Mat2Mat/M_Atlas_A1_2Mat.mat", n="M_Atlas_A1_2Mat_Doble" },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A2/Paleta", mat="Assets/Models/CiudadNueva/BVerdeFake/M_Detalle_BVerdeFake.mat", n="M_Detalle_BVerdeFake_Doble" },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A1/Original", mat="Assets/Models/CiudadNueva/FinalOriginal3Mat2Mat/M_Atlas_Original_A1.mat", n="M_Atlas_Original_A1_Doble" },
            new { dir="Assets/Prefabs/Ciudad/Edificios/A2/Original", mat="Assets/Models/CiudadNueva/FinalOriginal3Mat2Mat/M_Atlas_Original_A2.mat", n="M_Atlas_Original_A2_Doble" }
        })
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(grupo.mat);
            if (baseMat == null) { Debug.LogWarning("Falta " + grupo.mat); continue; }
            var mat = CrearDoble(baseMat, grupo.n);
            foreach (var p in Directory.GetFiles(grupo.dir, "*.prefab", SearchOption.TopDirectoryOnly))
            {
                string ruta = p.Replace("\\", "/");
                var content = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    var lod = content.GetComponent<LODGroup>(); if (lod == null) continue;
                    var lv = lod.GetLODs(); if (lv.Length == 0 || lv[0].renderers.Length == 0) continue;
                    var mr = lv[0].renderers[0];
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var sin = QuitarTapa(mf.sharedMesh, Path.GetFileNameWithoutExtension(ruta));
                    if (sin != null) mf.sharedMesh = sin;
                    var mats = mr.sharedMaterials; if (mats.Length > 1) mats[1] = mat; mr.sharedMaterials = mats;
                    PrefabUtility.SaveAsPrefabAsset(content, ruta);
                    n++;
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Tapas/Doble] prefabs=" + n);
    }

    private static Material CrearDoble(Material baseMat, string nombre)
    {
        string path = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(baseMat); m.name = nombre; AssetDatabase.CreateAsset(m, path); }
        m.shader = _ds;
        m.mainTexture = baseMat.mainTexture;
        m.SetColor("_Color", baseMat.HasProperty("_Color") ? baseMat.GetColor("_Color") : Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Mesh QuitarTapa(Mesh src, string nombre)
    {
        int n = src.vertices.Length;
        if (n < 4) return null;
        // La tapa son los últimos 4 vértices, todos en maxY.
        var v = src.vertices; float maxY = -999f;
        for (int i = 0; i < n; i++) maxY = Mathf.Max(maxY, v[i].y);
        for (int i = n - 4; i < n; i++) if (Mathf.Abs(v[i].y - maxY) > 0.02f) return null;

        int nuevoN = n - 4;
        var nv = new List<Vector3>(nuevoN); var nn = new List<Vector3>(nuevoN); var nt = new List<Vector4>(nuevoN); var nuv = new List<Vector2>(nuevoN);
        var nuv2 = new List<Vector2>(); var nc = new List<Color>();
        for (int i = 0; i < nuevoN; i++) { nv.Add(src.vertices[i]); nuv.Add(src.uv[i]); }
        var sn = src.normals; if (sn != null && sn.Length == n) for (int i = 0; i < nuevoN; i++) nn.Add(sn[i]);
        var st = src.tangents; if (st != null && st.Length == n) for (int i = 0; i < nuevoN; i++) nt.Add(st[i]);
        var su2 = src.uv2; if (su2 != null && su2.Length == n) for (int i = 0; i < nuevoN; i++) nuv2.Add(su2[i]);
        var sc = src.colors; if (sc != null && sc.Length == n) for (int i = 0; i < nuevoN; i++) nc.Add(sc[i]);

        var tris = new List<int>[src.subMeshCount]; bool quitado = false;
        for (int s = 0; s < tris.Length; s++)
        {
            tris[s] = new List<int>();
            var tr = src.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3)
            {
                if (tr[i] >= nuevoN || tr[i + 1] >= nuevoN || tr[i + 2] >= nuevoN) { quitado = true; continue; }
                tris[s].Add(tr[i]); tris[s].Add(tr[i + 1]); tris[s].Add(tr[i + 2]);
            }
        }
        if (!quitado) return null;
        var m = new Mesh(); m.name = nombre + "_SinTapa"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); if (nn.Count == nuevoN) m.SetNormals(nn); if (nt.Count == nuevoN) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nuevoN) m.SetUVs(1, nuv2); if (nc.Count == nuevoN) m.SetColors(nc);
        m.subMeshCount = tris.Length; for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s); m.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + m.name + ".asset"); AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
