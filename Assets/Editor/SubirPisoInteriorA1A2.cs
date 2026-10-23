using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Corrige el parpadeo (z-fighting) del piso interior en Medium y Large:
/// la losa interior quedaba a y≈0, coplanar con el suelo exterior de la escena.
/// Sube +3 cm los triángulos horizontales del piso interior en LOD0/LOD1
/// de los prefabs A1/A2 Medium y Large (Original y Paleta).
/// No toca techos, paredes ni otros materiales.
/// </summary>
public static class SubirPisoInteriorA1A2
{
    private const string Carpeta = "Assets/Models/CiudadNueva/PisoInteriorFix";
    private const float Delta = 0.03f;

    [MenuItem("UTEC/Edificios/Subir piso interior Medium y Large (A1/A2)")]
    public static void Subir()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "PisoInteriorFix");

        int prefabs = 0;
        foreach (var dir in new[] { "Assets/Prefabs/Ciudad/Edificios/A1", "Assets/Prefabs/Ciudad/Edificios/A2" })
        {
            foreach (var p in Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories))
            {
                string ruta = p.Replace("\\", "/");
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                if (!nombre.Contains("Medium") && !nombre.Contains("Large")) continue;

                var content = PrefabUtility.LoadPrefabContents(ruta);
                bool cambio = false;
                try
                {
                    var lod = content.GetComponent<LODGroup>();
                    if (lod == null) continue;
                    var niveles = lod.GetLODs();
                    var procesadas = new HashSet<int>();
                    for (int li = 0; li < niveles.Length; li++)
                    {
                        if (niveles[li].renderers == null) continue;
                        foreach (var r in niveles[li].renderers)
                        {
                            if (r == null || r.name.Contains("Caja")) continue;
                            var mf = r.GetComponent<MeshFilter>();
                            if (mf == null || mf.sharedMesh == null) continue;
                            if (!procesadas.Add(mf.sharedMesh.GetInstanceID())) continue;
                            var nueva = Levantar(mf.sharedMesh, r.sharedMaterials);
                            if (nueva == null) continue;
                            string rutaMesh = AssetDatabase.GenerateUniqueAssetPath(
                                Carpeta + "/" + nombre + "_" + mf.sharedMesh.name.Replace(" ", "_") + "_PisoUp.asset");
                            AssetDatabase.CreateAsset(nueva, rutaMesh);
                            mf.sharedMesh = nueva;
                            cambio = true;
                        }
                    }
                    if (cambio)
                    {
                        PrefabUtility.SaveAsPrefabAsset(content, ruta);
                        prefabs++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(content); }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[PisoInterior] prefabs corregidos=" + prefabs);
    }

    private static Mesh Levantar(Mesh src, Material[] mats)
    {
        var v = src.vertices; var n = src.normals; var t = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        bool hn = n != null && n.Length == v.Length, ht = t != null && t.Length == v.Length;
        bool huv = uv != null && uv.Length == v.Length, huv2 = uv2 != null && uv2.Length == v.Length, hc = col != null && col.Length == v.Length;

        var nv = new List<Vector3>(v);
        var nn = new List<Vector3>(hn ? n : new Vector3[v.Length]);
        var nt = new List<Vector4>(ht ? t : new Vector4[v.Length]);
        var nuv = new List<Vector2>(huv ? uv : new Vector2[v.Length]);
        var nuv2 = new List<Vector2>(huv2 ? uv2 : new Vector2[v.Length]);
        var nc = new List<Color>(hc ? col : new Color[v.Length]);

        var tris = new List<int>[src.subMeshCount];
        for (int s = 0; s < src.subMeshCount; s++) tris[s] = new List<int>(src.GetTriangles(s));

        bool cambio = false;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            // Solo submallas de detalle/atlas; el ladrillo y el vidrio no tienen piso.
            var mat = s < mats.Length ? mats[s] : null;
            if (mat == null) continue;
            string mn = mat.name.ToLowerInvariant();
            if (mn.Contains("brick") || mn.Contains("glass")) continue;

            var tr = tris[s];
            for (int i = 0; i < tr.Count; i += 3)
            {
                int a = tr[i], b = tr[i + 1], c = tr[i + 2];
                var nrm = (hn ? n[a] : Vector3.up) + (hn ? n[b] : Vector3.up) + (hn ? n[c] : Vector3.up);
                if (nrm.normalized.y <= 0.55f) continue;                       // solo caras hacia arriba
                float cy = (v[a].y + v[b].y + v[c].y) / 3f;
                if (cy > 0.2f) continue;                                       // solo el piso a nivel de suelo

                // Vértices nuevos elevados (sin tocar los originales por si se comparten).
                int na = nv.Count; nv.Add(v[a] + Vector3.up * Delta); nn.Add(hn ? n[a] : Vector3.up); nt.Add(ht ? t[a] : Vector4.zero); nuv.Add(huv ? uv[a] : Vector2.zero); nuv2.Add(huv2 ? uv2[a] : Vector2.zero); nc.Add(hc ? col[a] : Color.white);
                int nb = nv.Count; nv.Add(v[b] + Vector3.up * Delta); nn.Add(hn ? n[b] : Vector3.up); nt.Add(ht ? t[b] : Vector4.zero); nuv.Add(huv ? uv[b] : Vector2.zero); nuv2.Add(huv2 ? uv2[b] : Vector2.zero); nc.Add(hc ? col[b] : Color.white);
                int ncv = nv.Count; nv.Add(v[c] + Vector3.up * Delta); nn.Add(hn ? n[c] : Vector3.up); nt.Add(ht ? t[c] : Vector4.zero); nuv.Add(huv ? uv[c] : Vector2.zero); nuv2.Add(huv2 ? uv2[c] : Vector2.zero); nc.Add(hc ? col[c] : Color.white);
                tr[i] = na; tr[i + 1] = nb; tr[i + 2] = ncv;
                cambio = true;
            }
        }
        if (!cambio) return null;

        var m = new Mesh();
        m.name = src.name + "_PisoUp";
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); m.SetNormals(nn); m.SetTangents(nt); m.SetUVs(0, nuv); m.SetUVs(1, nuv2); m.SetColors(nc);
        m.subMeshCount = tris.Length;
        for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s);
        m.RecalculateBounds();
        return m;
    }
}
