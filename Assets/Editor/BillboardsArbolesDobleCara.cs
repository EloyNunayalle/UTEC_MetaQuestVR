using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Convierte los billboards de los árboles (LOD1) a doble cara duplicando la
/// geometría con winding invertido. El shader Unlit/Transparent Cutout culla
/// caras traseras y el cross original de 2 planos solo se veía desde 2 de las
/// 4 direcciones; desde las otras el árbol desaparecía al entrar el LOD1.
///
/// Se modifican las mallas en sitio (mismos GUIDs) y la operación es idempotente.
/// </summary>
public static class BillboardsArbolesDobleCara
{
    [MenuItem("UTEC/Árboles/3 Billboards a doble cara")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) return;

        var mallas = new HashSet<Mesh>();
        foreach (var grupo in Object.FindObjectsOfType<LODGroup>(true))
        {
            string nombre = grupo.gameObject.name;
            if (!nombre.EndsWith("_LOD") || !nombre.Contains("Tree")) continue;

            var lods = grupo.GetLODs();
            if (lods.Length < 2) continue;

            foreach (var renderer in lods[1].renderers)
            {
                if (renderer == null) continue;
                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null &&
                    mf.sharedMesh.name.StartsWith("Billboard_"))
                {
                    mallas.Add(mf.sharedMesh);
                }
            }
        }

        int convertidas = 0;
        foreach (var malla in mallas)
        {
            if (HacerDobleCara(malla)) convertidas++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Árboles] Billboards a doble cara: " + convertidas + " de " +
                  mallas.Count + " mallas convertidas.");
    }

    private static bool HacerDobleCara(Mesh malla)
    {
        var v = malla.vertices;
        var tris = malla.triangles;
        if (v == null || tris == null || v.Length == 0 || tris.Length == 0) return false;

        // Ya convertida (8 vértices / 12 índices originales -> 16 / 24).
        if (v.Length == 16 && tris.Length == 24) return false;

        if (v.Length != 8 || tris.Length != 12)
        {
            Debug.LogWarning("[Árboles] Billboard con geometría inesperada: " +
                             malla.name + " v=" + v.Length + " tris=" + tris.Length +
                             "; se omite.");
            return false;
        }

        var n = malla.normals;
        var t = malla.tangents;
        var uv = malla.uv;
        var uv2 = malla.uv2;
        var col = malla.colors;

        int N = v.Length;
        var nv = new Vector3[N * 2];
        var nn = new Vector3[N * 2];
        var nt = new Vector4[N * 2];
        var nuv = new Vector2[N * 2];
        var nuv2 = new Vector2[N * 2];
        var ncol = new Color[N * 2];

        bool hn = n != null && n.Length == N;
        bool ht = t != null && t.Length == N;
        bool huv = uv != null && uv.Length == N;
        bool huv2 = uv2 != null && uv2.Length == N;
        bool hc = col != null && col.Length == N;

        for (int i = 0; i < N; i++)
        {
            nv[i] = v[i];
            nv[N + i] = v[i];
            if (hn) { nn[i] = n[i]; nn[N + i] = -n[i]; }
            if (ht) { nt[i] = t[i]; nt[N + i] = t[i]; }
            if (huv) { nuv[i] = uv[i]; nuv[N + i] = uv[i]; }
            if (huv2) { nuv2[i] = uv2[i]; nuv2[N + i] = uv2[i]; }
            if (hc) { ncol[i] = col[i]; ncol[N + i] = col[i]; }
        }

        var ntris = new int[tris.Length * 2];
        for (int i = 0; i < tris.Length; i++) ntris[i] = tris[i];
        for (int i = 0; i < tris.Length; i += 3)
        {
            ntris[tris.Length + i] = tris[i + 2] + N;
            ntris[tris.Length + i + 1] = tris[i + 1] + N;
            ntris[tris.Length + i + 2] = tris[i] + N;
        }

        var nueva = new Mesh();
        nueva.name = malla.name;
        nueva.SetVertices(nv);
        if (hn) nueva.SetNormals(nn);
        if (ht) nueva.SetTangents(nt);
        if (huv) nueva.SetUVs(0, nuv);
        if (huv2) nueva.SetUVs(1, nuv2);
        if (hc) nueva.SetColors(ncol);
        nueva.subMeshCount = 1;
        nueva.SetTriangles(ntris, 0);
        nueva.RecalculateBounds();

        CopiarMalla(malla, nueva);
        EditorUtility.SetDirty(malla);
        Object.DestroyImmediate(nueva);
        return true;
    }

    private static void CopiarMalla(Mesh destino, Mesh origen)
    {
        destino.Clear();
        destino.indexFormat = origen.indexFormat;
        destino.SetVertices(origen.vertices);

        var normales = origen.normals;
        if (normales != null && normales.Length > 0) destino.SetNormals(normales);

        var tangentes = origen.tangents;
        if (tangentes != null && tangentes.Length > 0) destino.SetTangents(tangentes);

        destino.SetUVs(0, origen.uv);

        var uv2 = origen.uv2;
        if (uv2 != null && uv2.Length > 0) destino.SetUVs(1, uv2);

        var colores = origen.colors;
        if (colores != null && colores.Length > 0) destino.SetColors(colores);

        destino.subMeshCount = origen.subMeshCount;
        for (int s = 0; s < origen.subMeshCount; s++)
            destino.SetTriangles(origen.GetTriangles(s), s);

        destino.RecalculateBounds();
    }
}
