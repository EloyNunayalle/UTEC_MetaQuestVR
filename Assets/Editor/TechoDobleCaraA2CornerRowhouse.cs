using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hace doble cara el ensamblado del techo (plano de asfalto, banda del pretil
/// y aro superior de la cornisa) en Corner y Rowhouse de A2 (Original y Paleta).
/// Causa: el shader Standard ignora _Cull=0, y esas caras son de una sola cara;
/// desde el interior o en vistas rasantes quedan culled y se ve a través.
/// Fix: duplicar esos triángulos con winding y normales invertidas (copias
/// coplanares, sin z-fighting y sin geometría que sobresalga).
/// </summary>
public static class TechoDobleCaraA2CornerRowhouse
{
    private const string Carpeta = "Assets/Models/CiudadNueva/TechoDobleCaraFix";

    [MenuItem("UTEC/Edificios/Techo doble cara Corner y Rowhouse (A2)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "TechoDobleCaraFix");

        var rutas = new[]
        {
            "Assets/Prefabs/Ciudad/Edificios/A2/Original/EDF_A2_Original_Corner.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A2/Original/EDF_A2_Original_Rowhouse.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A2/Paleta/EDF_A2_Paleta_Corner_RedBrick.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A2/Paleta/EDF_A2_Paleta_Rowhouse_Marron.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A1/Original/EDF_A1_Original_Corner.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A1/Original/EDF_A1_Original_Rowhouse.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A1/Paleta/EDF_A1_Paleta_Corner_RedBrick.prefab",
            "Assets/Prefabs/Ciudad/Edificios/A1/Paleta/EDF_A1_Paleta_Rowhouse_Marron.prefab",
        };

        int prefabs = 0;
        foreach (var ruta in rutas)
        {
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            var content = PrefabUtility.LoadPrefabContents(ruta);
            bool cambio = false;
            try
            {
                var lod = content.GetComponent<LODGroup>();
                if (lod == null) continue;
                var niveles = lod.GetLODs();
                var mapa = new Dictionary<int, Mesh>();
                for (int li = 0; li < niveles.Length; li++)
                {
                    if (niveles[li].renderers == null) continue;
                    foreach (var r in niveles[li].renderers)
                    {
                        if (r == null || r.name.Contains("Caja")) continue;
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        // Idempotencia: no reprocesar una malla ya doble-cara.
                        if (mf.sharedMesh.name.EndsWith("_Techo2C")) continue;

                        Mesh yaLista;
                        if (mapa.TryGetValue(mf.sharedMesh.GetInstanceID(), out yaLista))
                        {
                            if (mf.sharedMesh != yaLista) { mf.sharedMesh = yaLista; cambio = true; }
                            continue;
                        }

                        var nueva = DuplicarTecho(mf.sharedMesh, nombre);
                        if (nueva == null) continue;
                        string rutaMesh = AssetDatabase.GenerateUniqueAssetPath(
                            Carpeta + "/" + nombre + "_" + mf.sharedMesh.name.Replace(" ", "_") + "_Techo2C.asset");
                        AssetDatabase.CreateAsset(nueva, rutaMesh);
                        mapa[mf.sharedMesh.GetInstanceID()] = nueva;
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
        AssetDatabase.SaveAssets();
        Debug.Log("[Techo2C] prefabs=" + prefabs);
    }

    private static Mesh DuplicarTecho(Mesh src, string etiqueta)
    {
        float tope = src.bounds.max.y;
        float yRoofMin = tope - 1.15f, yRoofMax = tope - 0.4f;   // plano del asfalto
        float yBandaMin = tope - 1.15f, yBandaMax = tope + 0.15f; // banda vertical del pretil
        float yAroMin = tope - 0.4f, yAroMax = tope + 0.15f;      // aro superior de la cornisa

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

        int duplicados = 0;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tr = tris[s];
            var agregados = new List<int>();
            for (int i = 0; i < tr.Count; i += 3)
            {
                int a = tr[i], b = tr[i + 1], c = tr[i + 2];
                var nrm = (hn ? n[a] : Vector3.up) + (hn ? n[b] : Vector3.up) + (hn ? n[c] : Vector3.up);
                nrm.Normalize();
                float cy = (v[a].y + v[b].y + v[c].y) / 3f;
                bool esTecho = nrm.y > 0.55f && cy >= yRoofMin && cy <= yRoofMax;             // plano asfalto
                bool esAro = nrm.y > 0.55f && cy > yAroMin && cy <= yAroMax;                  // aro superior
                bool esBanda = Mathf.Abs(nrm.y) < 0.35f && cy > yBandaMin && cy <= yBandaMax; // pretil vertical
                if (!esTecho && !esAro && !esBanda) continue;

                var idx = new int[3];
                var srcIdx = new int[3] { a, b, c };
                for (int k = 0; k < 3; k++)
                {
                    int o = srcIdx[k];
                    int ni = nv.Count;
                    nv.Add(v[o]);
                    nn.Add(hn ? -n[o] : Vector3.down);
                    if (ht) { var tt = t[o]; nt.Add(new Vector4(-tt.x, -tt.y, -tt.z, tt.w)); }
                    else nt.Add(Vector4.zero);
                    nuv.Add(huv ? uv[o] : Vector2.zero);
                    nuv2.Add(huv2 ? uv2[o] : Vector2.zero);
                    nc.Add(hc ? col[o] : Color.white);
                    idx[k] = ni;
                }
                // Winding invertido para que sea frontal desde el lado opuesto.
                agregados.Add(idx[2]); agregados.Add(idx[1]); agregados.Add(idx[0]);
                duplicados++;
            }
            tr.AddRange(agregados);
        }
        if (duplicados == 0) return null;

        var m = new Mesh();
        m.name = src.name + "_Techo2C";
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); m.SetNormals(nn);
        if (ht) m.SetTangents(nt);
        m.SetUVs(0, nuv);
        if (huv2) m.SetUVs(1, nuv2);
        if (hc) m.SetColors(nc);
        m.subMeshCount = tris.Length;
        for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s);
        m.RecalculateBounds();
        Debug.Log("[Techo2C] " + etiqueta + ": " + duplicados + " tris duplicados en " + src.name);
        return m;
    }
}
