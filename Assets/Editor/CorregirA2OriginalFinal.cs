using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Corrección final de los A2 Original:
/// - Techos/pisos horizontales pasan a la zona de asfalto del atlas (sin manchas fake).
/// - Corner y Rowhouse recuperan el fake de sus ventanas usando la geometría fake
///   de la malla fuente y una celda de interior del atlas.
/// No toca materiales, colores, A1 ni versiones Paleta.
/// </summary>
public static class CorregirA2OriginalFinal
{
    private const string Carpeta = "Assets/Models/CiudadNueva/FinalA2Final";
    private const string Prefijos = "Assets/Prefabs/Ciudad/Edificios/A2/Original";

    // Celdas reales dentro de T_Atlas_A2_Final (4096 x 4096), con margen interno.
    private static readonly Rect FakeRect = new Rect(3096f / 4096f, 0f, 512f / 4096f, 512f / 4096f);
    private static readonly Rect[] FakeRects =
    {
        new Rect(3096f / 4096f, 0f, 512f / 4096f, 512f / 4096f),          // interior iluminado
        new Rect(2064f / 4096f, 0f, 512f / 4096f, 512f / 4096f),          // interior frío
        new Rect(0f, 1032f / 4096f, 512f / 4096f, 512f / 4096f),          // interior oscuro
    };
    private static readonly Rect AsphaltRect = new Rect(0f, 2064f / 4096f, 512f / 4096f, 512f / 4096f);

    private class Fuente
    {
        public string tipo;
        public string prefab;
        public string source;
        public bool agregarFake;
    }

    [MenuItem("UTEC/Edificios/Corregir A2 Original final")]
    public static void Corregir()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalA2Final");

        var fuentes = new List<Fuente>
        {
            new Fuente { tipo="Small",    prefab="EDF_A2_Original_Small",    source="Assets/Models/DowntownCity/FBX/Building_Small_1.fbx",              agregarFake=false },
            new Fuente { tipo="Medium",   prefab="EDF_A2_Original_Medium",   source="Assets/Models/DowntownCity/FBX/Building_Medium_2_001.fbx",         agregarFake=false },
            new Fuente { tipo="Large",    prefab="EDF_A2_Original_Large",    source="Assets/Models/DowntownCity/FBX/Building_Large_2.fbx",              agregarFake=false },
            new Fuente { tipo="Corner",   prefab="EDF_A2_Original_Corner",   source="Assets/Models/CiudadNueva/Corner_Shop_B_Combined.prefab",         agregarFake=true  },
            new Fuente { tipo="Rowhouse", prefab="EDF_A2_Original_Rowhouse", source="Assets/Models/CiudadNueva/Rowhouse_A_Combined.prefab",             agregarFake=true  },
        };

        int n = 0;
        foreach (var f in fuentes)
        {
            string ruta = Prefijos + "/" + f.prefab + ".prefab";
            var content = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                var lod = content.GetComponent<LODGroup>();
                if (lod == null) continue;
                var niveles = lod.GetLODs();
                if (niveles.Length == 0 || niveles[0].renderers.Length == 0) continue;

                Mesh fakeSrc = null;
                if (f.agregarFake)
                {
                    var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(f.source);
                    if (srcGo != null)
                    {
                        foreach (var r in srcGo.GetComponentsInChildren<Renderer>(true))
                        {
                            var mf = r.GetComponent<MeshFilter>();
                            if (mf == null || mf.sharedMesh == null) continue;
                            for (int s = 0; s < mf.sharedMesh.subMeshCount && s < r.sharedMaterials.Length; s++)
                            {
                                var mat = r.sharedMaterials[s];
                                if (mat != null && mat.name == "MI_FakeInterior") { fakeSrc = mf.sharedMesh; break; }
                            }
                            if (fakeSrc != null) break;
                        }
                    }
                }

                var mr0 = niveles[0].renderers[0];
                var mf0 = mr0.GetComponent<MeshFilter>();
                if (mf0 == null || mf0.sharedMesh == null) continue;
                if (mf0.sharedMesh.name.EndsWith("_Final"))
                {
                    Debug.LogWarning("[A2OriginalFinal] Ya aplicado, se omite: " + f.prefab);
                    continue;
                }
                var nuevo0 = Procesar(mf0.sharedMesh, f.tipo, true, fakeSrc);
                mf0.sharedMesh = Guardar(nuevo0, f.prefab + "_LOD0_Final", true);

                // LOD1: mismo atlas y mismas UV, solo corrige los techos.
                Transform l1 = null;
                foreach (Transform t in content.transform) if (t.name == "LOD1_Decimado") { l1 = t; break; }
                if (l1 != null)
                {
                    var mf1 = l1.GetComponent<MeshFilter>();
                    if (mf1 != null && mf1.sharedMesh != null)
                    {
                        var nuevo1 = Procesar(mf1.sharedMesh, f.tipo, false, null);
                        mf1.sharedMesh = Guardar(nuevo1, f.prefab + "_LOD1_Final", true);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(content, ruta);
                n++;
            }
            finally { PrefabUtility.UnloadPrefabContents(content); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[A2OriginalFinal] prefabs=" + n);
    }

    private static Mesh Procesar(Mesh src, string tipo, bool esLod0, Mesh fakeSrc)
    {
        var v = src.vertices;
        var nrm = src.normals;
        var tan = src.tangents;
        var uv = src.uv;
        var uv2 = src.uv2;
        var col = src.colors;
        bool hn = nrm != null && nrm.Length == v.Length;
        bool ht = tan != null && tan.Length == v.Length;
        bool huv2 = uv2 != null && uv2.Length == v.Length;
        bool hc = col != null && col.Length == v.Length;

        var nv = new List<Vector3>(v);
        var nn = new List<Vector3>(hn ? nrm : new Vector3[v.Length]);
        var nt = new List<Vector4>(ht ? tan : new Vector4[v.Length]);
        var nuv = new List<Vector2>(uv);
        var nuv2 = new List<Vector2>(huv2 ? uv2 : new Vector2[v.Length]);
        var nc = new List<Color>(hc ? col : new Color[v.Length]);

        var tris = new List<int>[src.subMeshCount];
        for (int s = 0; s < src.subMeshCount; s++) tris[s] = new List<int>();

        // 1) Copiar triángulos originales, quitando el fake viejo de Corner/Rowhouse.
        int quitarDesde = -1, quitarHasta = -1;
        if (esLod0 && fakeSrc != null && tipo == "Corner") { quitarDesde = 7924; quitarHasta = 7990; }
        if (esLod0 && fakeSrc != null && tipo == "Rowhouse") { quitarDesde = 2468; quitarHasta = 2490; }

        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tr = src.GetTriangles(s);
            for (int i = 0; i < tr.Length; i += 3)
            {
                int idxTri = i / 3;
                if (s == 0 && idxTri >= quitarDesde && idxTri < quitarHasta) continue;
                int a = tr[i], b = tr[i + 1], c = tr[i + 2];
                var na = (hn ? nrm[a] : Vector3.up) + (hn ? nrm[b] : Vector3.up) + (hn ? nrm[c] : Vector3.up);
                bool horizontal = s == 0 && na.normalized.y > 0.55f;
                if (!horizontal)
                {
                    tris[s].Add(a); tris[s].Add(b); tris[s].Add(c);
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int o = tr[i + k];
                    int idx = nv.Count;
                    nv.Add(v[o]);
                    nn.Add(hn ? nrm[o] : Vector3.up);
                    nt.Add(ht ? tan[o] : Vector4.zero);
                    nuv.Add(UvAsfalto(v[o], src.bounds));
                    nuv2.Add(huv2 ? uv2[o] : Vector2.zero);
                    nc.Add(hc ? col[o] : Color.white);
                    tris[s].Add(idx);
                }
            }
        }

        // 2) Añadir el fake real en las ventanas de Corner/Rowhouse.
        if (esLod0 && fakeSrc != null)
        {
            for (int fs = 0; fs < fakeSrc.subMeshCount; fs++)
            {
                var fakeTr = fakeSrc.GetTriangles(fs);
                if (fakeTr.Length == 0) continue;
                var fv = fakeSrc.vertices;
                var fn = fakeSrc.normals;
                var ft = fakeSrc.tangents;
                var fuv = fakeSrc.uv;
                var fuv2 = fakeSrc.uv2;
                var fc = fakeSrc.colors;
                bool fhn = fn != null && fn.Length == fv.Length;
                bool fht = ft != null && ft.Length == fv.Length;
                bool fhuv2 = fuv2 != null && fuv2.Length == fv.Length;
                bool fhc = fc != null && fc.Length == fv.Length;

                // En la malla combinada el fake está en el submesh 2 (MI_FakeInterior).
                if (fs != 2) continue;
                for (int i = 0; i < fakeTr.Length; i += 3)
                {
                    int a = fakeTr[i], b = fakeTr[i + 1], c = fakeTr[i + 2];
                    var va = fv[a]; var vb = fv[b]; var vc = fv[c];
                    var centro = (va + vb + vc) / 3f;
                    var normal = Vector3.Cross(vb - va, vc - va).normalized;
                    if (normal.sqrMagnitude < 0.0001f && fhn) normal = (fn[a] + fn[b] + fn[c]).normalized;
                    var haciaFuera = centro - src.bounds.center;
                    if (Vector3.Dot(normal, haciaFuera) < 0f) normal = -normal;
                    // Las ventanas de Corner/Rowhouse tienen el fake hundido detrás del vidrio.
                    // Se adelanta lo suficiente para que quede por delante de la luna.
                    va += normal * 0.15f; vb += normal * 0.15f; vc += normal * 0.15f;

                    // Dos triángulos por ventana; se alternan tres interiores para evitar repetición.
                    int quad = (i / 3) / 2;
                    var rf = FakeRects[(quad * 7) % FakeRects.Length];
                    int ia = nv.Count; nv.Add(va); nn.Add(fhn ? fn[a] : Vector3.up); nt.Add(fht ? ft[a] : Vector4.zero); nuv.Add(UvFake(fuv, a, fuv.Length, rf)); nuv2.Add(fhuv2 ? fuv2[a] : Vector2.zero); nc.Add(fhc ? fc[a] : Color.white);
                    int ib = nv.Count; nv.Add(vb); nn.Add(fhn ? fn[b] : Vector3.up); nt.Add(fht ? ft[b] : Vector4.zero); nuv.Add(UvFake(fuv, b, fuv.Length, rf)); nuv2.Add(fhuv2 ? fuv2[b] : Vector2.zero); nc.Add(fhc ? fc[b] : Color.white);
                    int ic = nv.Count; nv.Add(vc); nn.Add(fhn ? fn[c] : Vector3.up); nt.Add(fht ? ft[c] : Vector4.zero); nuv.Add(UvFake(fuv, c, fuv.Length, rf)); nuv2.Add(fhuv2 ? fuv2[c] : Vector2.zero); nc.Add(fhc ? fc[c] : Color.white);
                    tris[0].Add(ia); tris[0].Add(ib); tris[0].Add(ic);
                }
            }
        }

        var m = new Mesh();
        m.name = src.name + "_Final";
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv);
        if (nn.Count == nv.Count) m.SetNormals(nn);
        if (nt.Count == nv.Count) m.SetTangents(nt);
        m.SetUVs(0, nuv);
        if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2);
        if (nc.Count == nv.Count) m.SetColors(nc);
        m.subMeshCount = tris.Length;
        for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s);
        m.RecalculateBounds();
        return m;
    }

    private static Vector2 UvFake(Vector2[] uv, int i, int len, Rect rect)
    {
        if (uv == null || uv.Length != len) return new Vector2(rect.center.x, rect.center.y);
        float u = Mathf.Clamp01(uv[i].x);
        float v = Mathf.Clamp01(uv[i].y);
        // Margen de 2 px para no tocar celdas vecinas.
        return new Vector2(rect.x + 0.002f + u * (rect.width - 0.004f),
                           rect.y + 0.002f + v * (rect.height - 0.004f));
    }

    private static Vector2 UvAsfalto(Vector3 p, Bounds b)
    {
        float sx = Mathf.InverseLerp(b.min.x, b.max.x, p.x);
        float sz = Mathf.InverseLerp(b.min.z, b.max.z, p.z);
        return new Vector2(AsphaltRect.x + 0.002f + sx * (AsphaltRect.width - 0.004f),
                           AsphaltRect.y + 0.002f + sz * (AsphaltRect.height - 0.004f));
    }

    private static Mesh Guardar(Mesh src, string nombre, bool reemplazar)
    {
        string ruta = Carpeta + "/" + nombre + ".asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente == null)
        {
            AssetDatabase.CreateAsset(src, ruta);
            return src;
        }
        existente.Clear();
        existente.indexFormat = src.indexFormat;
        existente.SetVertices(new List<Vector3>(src.vertices));
        if (src.normals != null && src.normals.Length == src.vertexCount) existente.SetNormals(new List<Vector3>(src.normals));
        if (src.tangents != null && src.tangents.Length == src.vertexCount) existente.SetTangents(new List<Vector4>(src.tangents));
        existente.SetUVs(0, new List<Vector2>(src.uv));
        if (src.uv2 != null && src.uv2.Length == src.vertexCount) existente.SetUVs(1, new List<Vector2>(src.uv2));
        if (src.colors != null && src.colors.Length == src.vertexCount) existente.SetColors(new List<Color>(src.colors));
        existente.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) existente.SetTriangles(src.GetTriangles(s), s);
        existente.RecalculateBounds();
        EditorUtility.SetDirty(existente);
        return existente;
    }
}
