using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rehace los prefabs ORIGINALES con la misma arquitectura que las versiones actuales:
/// A2 = 3 materiales (ladrillo + atlas detalle/fake + vidrio).
/// A1 = 2 materiales (ladrillo + atlas opaco sin fake).
/// Usa las mallas y colores originales, no la paleta actual.
/// </summary>
public static class ConstructorOriginal3Mat2Mat
{
    private const string Carpeta = "Assets/Models/CiudadNueva/FinalOriginal3Mat2Mat";
    private static readonly Dictionary<string, Material> MatA2 = new Dictionary<string, Material>();
    private static Material _atlasA2, _atlasA1;
    private static Dictionary<string, Rect> _rectsA2, _rectsA1;
    private static Rect _windowRect;
    private static Rect _fakeA2Rect;

    private class Fuente
    {
        public string tipo;
        public string path;
        public Mesh mesh;
        public Material[] mats;
        public Material brick;
        public bool nueva;
    }

    [MenuItem("UTEC/Edificios/Construir originales 3mat A2 y 2mat A1")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalOriginal3Mat2Mat");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/FinalOriginal3Mat2Mat"));

        var fuentes = new List<Fuente>();
        AgregarFbx(fuentes, "Small", "Assets/Models/DowntownCity/FBX/Building_Small_1.fbx", false);
        AgregarFbx(fuentes, "Medium", "Assets/Models/DowntownCity/FBX/Building_Medium_2_001.fbx", false);
        AgregarFbx(fuentes, "Large", "Assets/Models/DowntownCity/FBX/Building_Large_2.fbx", false);
        AgregarFbx(fuentes, "Corner", "Assets/Models/CiudadNueva/Corner_Shop_B_Combined.prefab", true);
        AgregarFbx(fuentes, "Rowhouse", "Assets/Models/CiudadNueva/Rowhouse_A_Combined.prefab", true);

        ConstruirAtlas(fuentes, true);
        ConstruirAtlas(fuentes, false);
        int n = 0;
        foreach (var f in fuentes)
        {
            var mesh = f.mesh;
            if (f.nueva) mesh = CorregirEscalera(f);
            var a2 = ConstruirMesh(mesh, f.mats, true);
            if (a2 != null) { CrearPrefab("A2", f.tipo, a2, true, f.brick); n++; }
            var a1 = ConstruirMesh(mesh, f.mats, false);
            if (a1 != null) { CrearPrefab("A1", f.tipo, a1, false, f.brick); n++; }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Original3Mat2Mat] prefabs=" + n);
    }

    private static void AgregarFbx(List<Fuente> lista, string tipo, string path, bool nueva)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) { Debug.LogWarning("Falta " + path); return; }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Material brick = null; foreach (var m in r.sharedMaterials) if (EsLadrillo(m)) { brick = m; break; }
            if (brick == null) brick = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Brick_Marron.mat");
            lista.Add(new Fuente { tipo = tipo, path = path, mesh = mf.sharedMesh, mats = r.sharedMaterials, brick = brick, nueva = nueva });
            break;
        }
    }

    private static void ConstruirAtlas(List<Fuente> fuentes, bool a2)
    {
        var claves = new List<string>(); var texturas = new List<Texture2D>();
        foreach (var f in fuentes)
        {
            foreach (var m in f.mats)
            {
                if (m == null) continue;
                if (EsLadrillo(m) || EsVidrio(m)) continue;
                if (!a2 && EsFake(m)) continue;
                if (claves.Contains(m.name)) continue;
                claves.Add(m.name);
                texturas.Add(TexturaDe(m));
            }
        }
        if (!a2)
        {
            claves.Add("__WINDOW__");
            var w = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var p = new Color[64 * 64]; for (int i = 0; i < p.Length; i++) p[i] = new Color(0.18f, 0.22f, 0.28f, 1f);
            w.SetPixels(p); w.Apply(); w.name = "WINDOW_ORIGINAL"; texturas.Add(w);
        }
        else
        {
            // El crop fake conserva la textura original del kit y hace visible la cara.
            var fakeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Carpeta + "/T_FakeTile.png");
            if (fakeTex != null) { claves.Add("__FAKE__"); texturas.Add(fakeTex); }
        }
        var atlas = new Texture2D(4096, 4096, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), 4, 4096, false);
        atlas.Apply();
        // rellenar transparencias con un neutro para que nunca se vea negro
        var px = atlas.GetPixels32(); var fondo = new Color32(70, 65, 55, 255);
        for (int i = 0; i < px.Length; i++) if (px[i].a == 0) px[i] = fondo;
        atlas.SetPixels32(px); atlas.Apply();
        var mapa = new Dictionary<string, Rect>();
        for (int i = 0; i < claves.Count; i++) mapa[claves[i]] = rects[i];
        string png = Carpeta + (a2 ? "/T_Atlas_Original_A2.png" : "/T_Atlas_Original_A1.png");
        File.WriteAllBytes(Path.Combine(Application.dataPath, "Models/CiudadNueva/FinalOriginal3Mat2Mat/" + Path.GetFileName(png)), atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false; imp.mipmapEnabled = true; imp.maxTextureSize = 4096; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        string matPath = Carpeta + (a2 ? "/M_Atlas_Original_A2.mat" : "/M_Atlas_Original_A1.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); mat.name = Path.GetFileNameWithoutExtension(matPath); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = Shader.Find("Standard"); mat.mainTexture = tex; mat.SetColor("_Color", Color.white); mat.SetFloat("_Glossiness", 0.1f); mat.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(mat);
        if (a2) { _atlasA2 = mat; _rectsA2 = mapa; if (mapa.ContainsKey("__FAKE__")) _fakeA2Rect = mapa["__FAKE__"]; }
        else { _atlasA1 = mat; _rectsA1 = mapa; _windowRect = mapa["__WINDOW__"]; }
    }

    private static Texture2D TexturaDe(Material m)
    {
        var tex = m.mainTexture as Texture2D;
        if (tex != null)
        {
            var rt = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt); var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(512, 512, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); t.name = "F_" + m.name; return t;
        }
        var c = m.HasProperty("_Color") ? m.color : Color.white;
        var solid = new Texture2D(16, 16, TextureFormat.RGBA32, false); var p = new Color[256];
        for (int i = 0; i < p.Length; i++) p[i] = new Color(c.r, c.g, c.b, 1f);
        solid.SetPixels(p); solid.Apply(); solid.name = "S_" + m.name; return solid;
    }

    private static Mesh CorregirEscalera(Fuente f)
    {
        string srcPrefab = f.tipo == "Rowhouse" ? "Assets/Models/CiudadNueva/Rowhouse_A.prefab" : "Assets/Models/CiudadNueva/Corner_Shop_B.prefab";
        var pref = AssetDatabase.LoadAssetAtPath<GameObject>(srcPrefab);
        if (pref == null) return f.mesh;
        Transform stairs = null;
        foreach (var tr in pref.GetComponentsInChildren<Transform>(true)) if (tr.name == "Stairs_Entrance_Concrete") { stairs = tr; break; }
        if (stairs == null) return f.mesh;
        var smf = stairs.GetComponent<MeshFilter>(); if (smf == null || smf.sharedMesh == null) return f.mesh;
        var inv = pref.transform.worldToLocalMatrix;
        var set = new HashSet<string>(); float maxY = 0f;
        foreach (var p in smf.sharedMesh.vertices)
        {
            var w = inv.MultiplyPoint3x4(stairs.transform.localToWorldMatrix.MultiplyPoint3x4(p));
            set.Add(w.x.ToString("F3") + "," + w.y.ToString("F3") + "," + w.z.ToString("F3"));
            maxY = Mathf.Max(maxY, w.y);
        }
        if (maxY < 0.5f) maxY = 1.0067f;
        var v = f.mesh.vertices; var n = f.mesh.normals; var t = f.mesh.tangents; var uv = f.mesh.uv; var uv2 = f.mesh.uv2; var col = f.mesh.colors;
        for (int i = 0; i < v.Length; i++)
        {
            string k = v[i].x.ToString("F3") + "," + v[i].y.ToString("F3") + "," + v[i].z.ToString("F3");
            if (!set.Contains(k)) v[i].y += maxY;
        }
        // Base solo bajo el edificio, sin incluir la escalera.
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in pref.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("Stairs")) continue;
            var rb = r.bounds; var c = rb.center; var e = rb.extents;
            for (int k = 0; k < 8; k++)
            {
                var q = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                q = inv.MultiplyPoint3x4(q);
                if (first) { b = new Bounds(q, Vector3.zero); first = false; } else b.Encapsulate(q);
            }
        }
        if (first) b = f.mesh.bounds;
        var nv = new List<Vector3>(v); var nn = new List<Vector3>(n); var nt = new List<Vector4>(t); var nuv = new List<Vector2>(uv); var nuv2 = new List<Vector2>(uv2); var nc = new List<Color>(col);
        int start = nv.Count; float bx = b.size.x * 0.98f, bz = b.size.z * 0.98f;
        var bc = new Vector3(b.center.x, maxY * 0.5f, b.center.z);
        Vector3[] c8 = {
            bc+new Vector3(-bx/2,-maxY/2,-bz/2),bc+new Vector3(bx/2,-maxY/2,-bz/2),bc+new Vector3(bx/2,maxY/2,-bz/2),bc+new Vector3(-bx/2,maxY/2,-bz/2),
            bc+new Vector3(-bx/2,-maxY/2,bz/2),bc+new Vector3(bx/2,-maxY/2,bz/2),bc+new Vector3(bx/2,maxY/2,bz/2),bc+new Vector3(-bx/2,maxY/2,bz/2)
        };
        for (int i = 0; i < 8; i++) { nv.Add(c8[i]); nn.Add(Vector3.up); nuv.Add(new Vector2(0.5f, 0.5f)); }
        int[] tri = {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,3,0,4,3,4,7,1,2,6,1,6,5};
        int brick = 0; for (int i = 0; i < f.mats.Length; i++) if (EsLadrillo(f.mats[i])) { brick = i; break; }
        var tris = new List<int>[f.mesh.subMeshCount]; for (int s = 0; s < tris.Length; s++) tris[s] = new List<int>(f.mesh.GetTriangles(s));
        foreach (var q in tri) tris[brick].Add(start + q);
        var m = new Mesh(); m.name = f.mesh.name + "_StairsFix"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); if (nn.Count == nv.Count) m.SetNormals(nn); if (nt.Count == nv.Count) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2); if (nc.Count == nv.Count) m.SetColors(nc);
        m.subMeshCount = tris.Length; for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s); m.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + f.tipo + "_StairsFix.asset"); AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Mesh ConstruirMesh(Mesh src, Material[] mats, bool a2)
    {
        var rects = a2 ? _rectsA2 : _rectsA1;
        var atlas = a2 ? _atlasA2 : _atlasA1;
        if (atlas == null || rects == null) return null;
        var v = src.vertices; var n = src.normals; var t = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>();
        int targetCount = a2 ? 3 : 2;
        var tris = new List<int>[targetCount]; for (int i = 0; i < targetCount; i++) tris[i] = new List<int>();
        var map = new Dictionary<long, int>();
        int Add(int o, int target, Vector2 u, bool fakeOffset)
        {
            long k = ((long)o << 4) | (uint)target | (fakeOffset ? 8L : 0L); int i;
            if (map.TryGetValue(k, out i)) return i;
            i = nv.Count; map[k] = i;
            var pos = v[o];
            if (fakeOffset) pos += n[o].normalized * 0.04f;
            nv.Add(pos); if (n.Length == v.Length) nn.Add(n[o]); if (t.Length == v.Length) nt.Add(t[o]); if (uv2.Length == v.Length) nuv2.Add(uv2[o]); if (col.Length == v.Length) nc.Add(col[o]); nuv.Add(u); return i;
        }
        Vector2 windowUv = new Vector2(_windowRect.x + _windowRect.width * 0.5f, _windowRect.y + _windowRect.height * 0.5f);
        for (int s = 0; s < src.subMeshCount && s < mats.Length; s++)
        {
            var m = mats[s]; if (m == null) continue;
            bool brick = EsLadrillo(m), glass = EsVidrio(m), fake = EsFake(m);
            int target = brick ? 0 : (a2 && glass ? 2 : 1);
            bool toWindow = !a2 && (glass || fake);
            Rect r = default;
            if (target == 1 && !toWindow)
            {
                if (a2 && fake && _fakeA2Rect.width > 0f) r = _fakeA2Rect;
                else if (!rects.TryGetValue(m.name, out r)) continue;
            }
            foreach (var o in src.GetTriangles(s))
            {
                Vector2 u;
                if (target == 0) u = uv[o];
                else if (target == 2) u = uv[o];
                else if (toWindow) u = windowUv;
                else u = new Vector2(r.x + Mathf.Repeat(uv[o].x, 1f) * r.width, r.y + Mathf.Repeat(uv[o].y, 1f) * r.height);
                tris[target].Add(Add(o, target, u, a2 && fake));
            }
        }
        // Tapa superior: evita que al mirar desde arriba se vea el interior o se pierdan los bordes.
        Rect capR;
        if (!rects.TryGetValue("MI_Concrete", out capR))
            if (!rects.TryGetValue("MI_Asphalt", out capR))
                if (!rects.TryGetValue("MI_InteriorFloor", out capR))
                    capR = a2 ? FirstRect(rects) : _windowRect;
        Vector2 capUv = new Vector2(capR.x + capR.width * 0.5f, capR.y + capR.height * 0.5f);
        var b = src.bounds; int capStart = nv.Count;
        nv.Add(new Vector3(b.min.x, b.max.y + 0.002f, b.min.z)); nn.Add(Vector3.up); nuv.Add(capUv);
        nv.Add(new Vector3(b.max.x, b.max.y + 0.002f, b.min.z)); nn.Add(Vector3.up); nuv.Add(capUv);
        nv.Add(new Vector3(b.max.x, b.max.y + 0.002f, b.max.z)); nn.Add(Vector3.up); nuv.Add(capUv);
        nv.Add(new Vector3(b.min.x, b.max.y + 0.002f, b.max.z)); nn.Add(Vector3.up); nuv.Add(capUv);
        tris[1].Add(capStart); tris[1].Add(capStart + 2); tris[1].Add(capStart + 1);
        tris[1].Add(capStart); tris[1].Add(capStart + 3); tris[1].Add(capStart + 2);

        var m2 = new Mesh(); m2.name = src.name + (a2 ? "_Original_A2" : "_Original_A1");
        m2.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m2.SetVertices(nv); if (nn.Count == nv.Count) m2.SetNormals(nn); if (nt.Count == nv.Count) m2.SetTangents(nt); m2.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m2.SetUVs(1, nuv2); if (nc.Count == nv.Count) m2.SetColors(nc);
        m2.subMeshCount = targetCount; for (int i = 0; i < targetCount; i++) m2.SetTriangles(tris[i], i); m2.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + m2.name + ".asset"); AssetDatabase.CreateAsset(m2, path);
        return m2;
    }

    private static Rect FirstRect(Dictionary<string, Rect> rects)
    {
        foreach (var kv in rects) return kv.Value;
        return new Rect(0, 0, 1, 1);
    }

    private static void CrearPrefab(string version, string tipo, Mesh mesh, bool a2, Material brick)
    {
        string nombre = "EDF_" + version + "_Original_" + tipo;
        string carpeta = "Assets/Prefabs/Ciudad/Edificios/" + version + "/Original";
        var root = new GameObject(nombre);
        var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false); l0.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = l0.AddComponent<MeshRenderer>();
        var mats = new List<Material>();
        mats.Add(brick);
        if (a2) { mats.Add(_atlasA2); mats.Add(AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Glass_Transparente.mat")); }
        else mats.Add(_atlasA1);
        mr.sharedMaterials = mats.ToArray();
        var caja = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(caja.GetComponent<Collider>()); caja.name = "LOD2_Caja"; caja.transform.SetParent(root.transform, false);
        caja.transform.localPosition = mesh.bounds.center; caja.transform.localScale = mesh.bounds.size; var cr = caja.GetComponent<MeshRenderer>(); cr.sharedMaterial = mats[0]; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var bc = root.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size;
        var lod = root.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(0.25f, new Renderer[] { mr }), new LOD(0.05f, new Renderer[] { cr }), new LOD(0.02f, new Renderer[0]) }); lod.RecalculateBounds();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        PrefabUtility.SaveAsPrefabAsset(root, carpeta + "/" + nombre + ".prefab"); Object.DestroyImmediate(root);
    }

    private static Material MaterialDeLadrillo()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Brick_Red.mat");
        if (m != null) return m;
        foreach (var g in AssetDatabase.FindAssets("MI_RedBrick t:Material")) { var p = AssetDatabase.GUIDToAssetPath(g); var mm = AssetDatabase.LoadAssetAtPath<Material>(p); if (mm != null) return mm; }
        return AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Brick_Marron.mat");
    }

    private static bool EsLadrillo(Material m) => m != null && m.name.ToLowerInvariant().Contains("brick");
    private static bool EsVidrio(Material m) => m != null && (m.name.ToLowerInvariant().Contains("glass") || m.renderQueue >= 3000);
    private static bool EsFake(Material m) => m != null && m.name.ToLowerInvariant().Contains("fake");
}
