using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Corrige las caras traseras/laterales de los A1/A2 actuales: el atlas de detalle
/// contiene fake y algunas caras de pared lo muestreaban como triángulos/manchas.
/// Las UV de esas caras se remapean al ladrillo del mismo edificio.
/// No toca las fachadas frontales ni el vidrio.
/// </summary>
public static class CorrectorCarasActual
{
    [MenuItem("UTEC/Edificios/Corregir caras traseras A1/A2 actuales")]
    public static void Corregir()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogWarning("[Caras] Abre la escena principal."); return; }

        // UV de ladrillo por edificio, tomadas de la caja del LOD1 de la escena.
        var rects = new Dictionary<string, Vector4>();
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string n = g.gameObject.name;
            if (n.Contains("Tree") || n.Contains("Maple") || n.Contains("Pine")) continue;
            foreach (Transform t in g.transform)
            {
                if (!t.name.Contains("Caja")) continue;
                var r = t.GetComponent<Renderer>(); if (r == null) break;
                var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
                var st = mpb.GetVector("_MainTex_ST"); if (st != Vector4.zero)
                {
                    string tipo = n.Contains("Medium") ? "Medium" : n.Contains("Corner") ? "Corner" : n.Contains("Rowhouse") ? "Rowhouse" : "Small";
                    string color = n.Contains("Medium") ? "Ocre" : n.Contains("Corner") ? "RedBrick" : n.Contains("Rowhouse") ? "Marron" : (g.transform.position.x < -20f ? "Gris" : g.transform.position.x < 0f ? "Arena" : "Blanco");
                    rects[tipo + "_" + color] = st;
                }
                break;
            }
        }

        // Fallback: rects fijas usadas por Constructor2MatMain (los property blocks
        // de la caja no se serializan de forma fiable al recargar la escena).
        if (!rects.ContainsKey("Small_Arena")) rects["Small_Arena"] = new Vector4(0.13f, 0.13f, 0.50f, 0f);
        if (!rects.ContainsKey("Small_Gris")) rects["Small_Gris"] = new Vector4(0.13f, 0.13f, 0.63f, 0f);
        if (!rects.ContainsKey("Small_Blanco")) rects["Small_Blanco"] = new Vector4(0.13f, 0.13f, 0.75f, 0f);
        if (!rects.ContainsKey("Medium_Ocre")) rects["Medium_Ocre"] = new Vector4(0.13f, 0.13f, 0.50f, 0.13f);
        if (!rects.ContainsKey("Corner_RedBrick")) rects["Corner_RedBrick"] = new Vector4(0.13f, 0.13f, 0.50f, 0.25f);
        if (!rects.ContainsKey("Rowhouse_Marron")) rects["Rowhouse_Marron"] = new Vector4(0.13f, 0.13f, 0.63f, 0.13f);

        int editados = 0;
        editados += ProcesarCarpeta("Assets/Prefabs/Ciudad/Edificios/A2/Paleta", true, rects);
        editados += ProcesarCarpeta("Assets/Prefabs/Ciudad/Edificios/A1/Paleta", false, rects);
        AssetDatabase.SaveAssets();
        Debug.Log("[Caras] prefabs editados=" + editados);
    }

    private static int ProcesarCarpeta(string carpeta, bool esA2, Dictionary<string, Vector4> rects)
    {
        int n = 0;
        foreach (var p in Directory.GetFiles(carpeta, "*.prefab", SearchOption.TopDirectoryOnly))
        {
            string archivo = Path.GetFileNameWithoutExtension(p);
            string[] partes = archivo.Split('_');
            string tipo = partes.Length >= 4 ? partes[3] : "Small";
            string color = partes.Length >= 5 ? partes[4] : "Ocre";
            string clave = tipo + "_" + color;
            if (!rects.ContainsKey(clave)) continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace("\\", "/"));
            if (prefab == null) continue;
            MeshFilter mf = null;
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                if (f.sharedMesh.subMeshCount == (esA2 ? 2 : 1)) { mf = f; break; }
            }
            if (mf == null) continue;

            var st = rects[clave];
            var rect = esA2
                ? new Rect(st.z, st.w, st.x, st.y)
                : new Rect(st.z, st.w * 0.5f, st.x, st.y * 0.5f);

            // Small/Medium tienen laterales ciegos; Corner/Rowhouse conservan su fachada lateral.
            bool laterales = tipo == "Small" || tipo == "Medium";
            if (Remapear(mf.sharedMesh, rect, laterales))
            {
                EditorUtility.SetDirty(mf.sharedMesh);
                n++;
            }
        }
        return n;
    }

    private static bool Remapear(Mesh old, Rect rect, bool incluirLaterales)
    {
        var v = old.vertices; var nrm = old.normals; var tan = old.tangents; var uv = old.uv; var uv2 = old.uv2; var col = old.colors;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var ncol = new List<Color>();
        var idx = new Dictionary<long, int>();
        var tris = new List<int>[old.subMeshCount]; for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();
        int cambiados = 0;
        var centro = old.bounds.center; var tam = old.bounds.size;

        for (int s = 0; s < old.subMeshCount; s++)
        {
            var fuente = old.GetTriangles(s);
            for (int i = 0; i < fuente.Length; i += 3)
            {
                var centroCara = (v[fuente[i]] + v[fuente[i + 1]] + v[fuente[i + 2]]) / 3f;
                var dir = (nrm[fuente[i]] + nrm[fuente[i + 1]] + nrm[fuente[i + 2]]).normalized;
                bool atras = dir.z < -0.55f || centroCara.z < centro.z - tam.z * 0.28f;
                bool lateral = incluirLaterales && (Mathf.Abs(dir.x) > 0.55f || Mathf.Abs(centroCara.x - centro.x) > tam.x * 0.34f);
                bool cambiar = atras || lateral;
                if (cambiar) cambiados++;
                for (int k = 0; k < 3; k++)
                {
                    int o = fuente[i + k];
                    long key = ((long)o << 1) | (cambiar ? 1L : 0L);
                    int ni;
                    if (!idx.TryGetValue(key, out ni))
                    {
                        ni = nv.Count; idx[key] = ni;
                        nv.Add(v[o]); if (nrm != null && nrm.Length == v.Length) nn.Add(nrm[o]); if (tan != null && tan.Length == v.Length) nt.Add(tan[o]);
                        if (cambiar)
                        {
                            // Punto fijo en el ladrillo: evita que las UV originales del
                            // detalle/fake sigan manchando la pared al decimar.
                            nuv.Add(new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f));
                        }
                        else nuv.Add(uv[o]);
                        if (uv2 != null && uv2.Length == v.Length) nuv2.Add(uv2[o]);
                        if (col != null && col.Length == v.Length) ncol.Add(col[o]);
                    }
                    tris[s].Add(ni);
                }
            }
        }
        if (cambiados == 0) return false;

        var nm = new Mesh(); nm.name = old.name + "_CarasFix"; if (nv.Count > 65535) nm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        nm.SetVertices(nv); if (nn.Count == nv.Count) nm.SetNormals(nn); if (nt.Count == nv.Count) nm.SetTangents(nt);
        nm.SetUVs(0, nuv); if (nuv2.Count == nv.Count) nm.SetUVs(1, nuv2); if (ncol.Count == nv.Count) nm.SetColors(ncol);
        nm.subMeshCount = old.subMeshCount; for (int s = 0; s < tris.Length; s++) nm.SetTriangles(tris[s], s); nm.RecalculateBounds();
        EditorUtility.CopySerialized(nm, old);
        Object.DestroyImmediate(nm);
        return true;
    }
}
