using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reconstruye A2 Original desde las mallas fuente del kit (sin paleta):
/// - Ladrillo: material original del kit (MI_RedBrick / MI_RedBrick_Pale).
/// - Detalle: atlas con las texturas originales de cada material (trim, concreto, asfalto, mármol).
/// - Fake: solo en las caras MI_FakeInterior, con su interior real del atlas fake.
/// - Vidrio: M_Glass_Transparente.
/// Cada superficie muestrea solo su propia textura; techos y pisos no reciben fake.
/// No toca A1 ni A2 Paleta.
/// </summary>
public static class ConstructorA2OriginalKit
{
    private const string Carpeta = "Assets/Models/CiudadNueva/FinalA2Kit";
    private const string PrefabsOriginal = "Assets/Prefabs/Ciudad/Edificios/A2/Original";
    private const float Margen = 0.02f;

    private class Fuente
    {
        public string tipo;
        public string prefab;
        public string origen;
        public bool esCombinada;
    }

    private class Atlas
    {
        public Texture2D tex;
        public Material mat;
        public Dictionary<string, Rect> rects = new Dictionary<string, Rect>();
    }

    [MenuItem("UTEC/Edificios/Reconstruir A2 Original desde kit")]
    public static void Reconstruir()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalA2Kit");

        var fuentes = new List<Fuente>
        {
            new Fuente { tipo="Small",    prefab="EDF_A2_Original_Small",    origen="Assets/Models/DowntownCity/FBX/Building_Small_1.fbx",          esCombinada=false },
            new Fuente { tipo="Medium",   prefab="EDF_A2_Original_Medium",   origen="Assets/Models/DowntownCity/FBX/Building_Medium_2_001.fbx",     esCombinada=false },
            new Fuente { tipo="Large",    prefab="EDF_A2_Original_Large",    origen="Assets/Models/DowntownCity/FBX/Building_Large_2.fbx",          esCombinada=false },
            new Fuente { tipo="Corner",   prefab="EDF_A2_Original_Corner",   origen="Assets/Models/CiudadNueva/Corner_Shop_B_Combined.prefab",     esCombinada=true },
            new Fuente { tipo="Rowhouse", prefab="EDF_A2_Original_Rowhouse", origen="Assets/Models/CiudadNueva/Rowhouse_A_Combined.prefab",         esCombinada=true },
        };

        // Un atlas compartido con todas las texturas del kit usadas por A2.
        var atlas = ConstruirAtlas(fuentes);
        int n = 0;
        foreach (var f in fuentes)
        {
            var renderers = ObtenerRenderers(f.origen);
            if (renderers.Count == 0) { Debug.LogWarning("[A2Kit] sin renderers: " + f.origen); continue; }
            var malla = ConstruirMalla(renderers, f.tipo, atlas);
            if (malla == null) continue;
            string ruta = Carpeta + "/A2Kit_" + f.tipo + ".asset";
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente == null) AssetDatabase.CreateAsset(malla, ruta);
            else { existente.Clear(); existente.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; existente.SetVertices(new List<Vector3>(malla.vertices)); existente.SetNormals(new List<Vector3>(malla.normals)); existente.SetTangents(new List<Vector4>(malla.tangents)); existente.SetUVs(0, new List<Vector2>(malla.uv)); existente.subMeshCount = malla.subMeshCount; for (int s = 0; s < malla.subMeshCount; s++) existente.SetTriangles(malla.GetTriangles(s), s); existente.RecalculateBounds(); EditorUtility.SetDirty(existente); }
            AplicarAPrefab(f, AssetDatabase.LoadAssetAtPath<Mesh>(ruta), atlas);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[A2Kit] prefabs=" + n);
    }

    private static List<MeshRenderer> ObtenerRenderers(string origen)
    {
        var lista = new List<MeshRenderer>();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(origen);
        if (go == null) return lista;
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) lista.Add(mr);
        }
        return lista;
    }

    private static Atlas ConstruirAtlas(List<Fuente> fuentes)
    {
        var nombres = new List<string>();
        var texturas = new List<Texture2D>();
        foreach (var f in fuentes)
            foreach (var mr in ObtenerRenderers(f.origen))
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null || EsVidrio(m) || EsFakeTexturado(m) || nombres.Contains(m.name)) continue;
                    nombres.Add(m.name);
                    texturas.Add(TexturaDe(m));
                }
        nombres.Add("__FAKE__");
        texturas.Add(CopiarRT(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/DowntownCity/Textures/T_lit_interior_1.png"), 512, "FAKE_lit1"));
        nombres.Add("__FAKE2__");
        texturas.Add(CopiarRT(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/DowntownCity/Textures/T_lit_interior_2.png"), 512, "FAKE_lit2"));
        nombres.Add("__FAKE3__");
        texturas.Add(CopiarRT(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/DowntownCity/Textures/T_dark_interior.png"), 512, "FAKE_dark"));

        var tex = new Texture2D(4096, 4096, TextureFormat.RGBA32, true);
        var rects = tex.PackTextures(texturas.ToArray(), 4, 4096, false);
        tex.Apply();
        string png = Carpeta + "/T_Atlas_A2Kit.png";
        File.WriteAllBytes(Path.Combine(Application.dataPath, "Models/CiudadNueva/FinalA2Kit/T_Atlas_A2Kit.png"), tex.EncodeToPNG());
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false; imp.mipmapEnabled = true; imp.maxTextureSize = 4096; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();

        var atlas = new Atlas();
        atlas.tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        for (int i = 0; i < nombres.Count; i++) atlas.rects[nombres[i]] = rects[i];

        string matPath = Carpeta + "/M_Atlas_A2Kit.mat";
        atlas.mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (atlas.mat == null) { atlas.mat = new Material(Shader.Find("Standard")); atlas.mat.name = "M_Atlas_A2Kit"; AssetDatabase.CreateAsset(atlas.mat, matPath); }
        atlas.mat.shader = Shader.Find("Standard");
        atlas.mat.mainTexture = atlas.tex;
        atlas.mat.color = Color.white;
        atlas.mat.SetFloat("_Glossiness", 0.1f);
        atlas.mat.SetFloat("_Metallic", 0f);
        atlas.mat.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(atlas.mat);
        return atlas;
    }

    private static Mesh ConstruirMalla(List<MeshRenderer> renderers, string tipo, Atlas atlas)
    {
        // Submallas: 0 ladrillo original, 1 detalle/fake en atlas, 2 vidrio.
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>();
        var tris = new List<int>[3]; for (int i = 0; i < 3; i++) tris[i] = new List<int>();

        Bounds caja = new Bounds(); bool primero = true;
        foreach (var mr in renderers)
        {
            var mf = mr.GetComponent<MeshFilter>();
            var mesh = mf.sharedMesh;
            var b = mesh.bounds;
            if (primero) { caja = b; primero = false; } else caja.Encapsulate(b);
        }
        var centro = caja.center;

        foreach (var mr in renderers)
        {
            var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices; var n = mesh.normals; var t = mesh.tangents; var uv = mesh.uv;
            bool hn = n != null && n.Length == v.Length, ht = t != null && t.Length == v.Length, huv = uv != null && uv.Length == v.Length;
            var mats = mr.sharedMaterials;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var m = mats[s]; if (m == null) continue;
                bool ladrillo = EsLadrillo(m), vidrio = EsVidrio(m), fake = EsFakeTexturado(m);
                int destino = ladrillo ? 0 : (vidrio ? 2 : 1);
                var tr = mesh.GetTriangles(s);
                for (int i = 0; i < tr.Length; i += 3)
                {
                    int a = tr[i], b2 = tr[i + 1], c = tr[i + 2];
                    Vector3 pa = v[a], pb = v[b2], pc = v[c];
                    if (fake)
                    {
                        // Ventanas fake: adelantar un poco hacia afuera para que quede frente al vidrio.
                        var normal = Vector3.Cross(pb - pa, pc - pa).normalized;
                        if (Vector3.Dot(normal, (pa + pb + pc) / 3f - centro) < 0f) normal = -normal;
                        pa += normal * Margen; pb += normal * Margen; pc += normal * Margen;
                    }
                    int ia = AgregarVertice(nv, nn, nt, nuv, pa, hn ? n[a] : Vector3.up, ht ? t[a] : Vector4.zero, UvPara(m, uv, huv, a, ladrillo, vidrio, fake, atlas));
                    int ib = AgregarVertice(nv, nn, nt, nuv, pb, hn ? n[b2] : Vector3.up, ht ? t[b2] : Vector4.zero, UvPara(m, uv, huv, b2, ladrillo, vidrio, fake, atlas));
                    int ic = AgregarVertice(nv, nn, nt, nuv, pc, hn ? n[c] : Vector3.up, ht ? t[c] : Vector4.zero, UvPara(m, uv, huv, c, ladrillo, vidrio, fake, atlas));
                    tris[destino].Add(ia); tris[destino].Add(ib); tris[destino].Add(ic);
                }
            }
        }

        var malla = new Mesh();
        malla.name = "A2Kit_" + tipo;
        malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malla.SetVertices(nv); malla.SetNormals(nn); malla.SetTangents(nt); malla.SetUVs(0, nuv);
        malla.subMeshCount = 3;
        for (int s = 0; s < 3; s++) malla.SetTriangles(tris[s], s);
        malla.RecalculateBounds();
        return malla;
    }

    private static int AgregarVertice(List<Vector3> nv, List<Vector3> nn, List<Vector4> nt, List<Vector2> nuv, Vector3 p, Vector3 n, Vector4 t, Vector2 u)
    {
        nv.Add(p); nn.Add(n); nt.Add(t); nuv.Add(u);
        return nv.Count - 1;
    }

    private static Vector2 UvPara(Material m, Vector2[] uv, bool huv, int i, bool ladrillo, bool vidrio, bool fake, Atlas atlas)
    {
        if (vidrio) return Vector2.zero;
        if (ladrillo)
        {
            // Ladrillo: UV original del kit; el material ladrillo lo muestrea directamente.
            return huv ? uv[i] : Vector2.zero;
        }
        string clave = fake ? ClaveFake(m) : m.name;
        Rect r;
        if (!atlas.rects.TryGetValue(clave, out r)) r = atlas.rects["__FAKE__"];
        float u = huv ? Mathf.Clamp01(uv[i].x) : 0.5f;
        float v = huv ? Mathf.Clamp01(uv[i].y) : 0.5f;
        return new Vector2(r.x + 0.002f + u * (r.width - 0.004f), r.y + 0.002f + v * (r.height - 0.004f));
    }

    private static string ClaveFake(Material m)
    {
        switch (m.name)
        {
            case "MI_FakeInterior_2": return "__FAKE2__";
            case "MI_FakeInterior_4": return "__FAKE3__";
            case "MI_FakeInterior_3": return "__FAKE2__";
            default: return "__FAKE__";
        }
    }

    private static void AplicarAPrefab(Fuente f, Mesh malla, Atlas atlas)
    {
        string ruta = PrefabsOriginal + "/" + f.prefab + ".prefab";
        var content = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            var lod = content.GetComponent<LODGroup>();
            if (lod == null) return;
            var niveles = lod.GetLODs();
            if (niveles.Length == 0 || niveles[0].renderers.Length == 0) return;
            var mr0 = niveles[0].renderers[0] as MeshRenderer;
            var mf0 = mr0.GetComponent<MeshFilter>();
            mf0.sharedMesh = malla;
            var ladrilloMat = ObtenerLadrillo(f);
            mr0.sharedMaterials = new Material[] { ladrilloMat, atlas.mat, AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Glass_Transparente.mat") };
            PrefabUtility.SaveAsPrefabAsset(content, ruta);
        }
        finally { PrefabUtility.UnloadPrefabContents(content); }
    }

    private static Material ObtenerLadrillo(Fuente f)
    {
        // Mismo ladrillo que usa cada FBX/prefab fuente en su malla de ladrillo.
        return BuscarMaterial(f.tipo == "Small" || f.tipo == "Corner" || f.tipo == "Rowhouse" ? "MI_RedBrick_Pale" : "MI_RedBrick");
    }

    private static Material BuscarMaterial(string nombre)
    {
        foreach (var g in AssetDatabase.FindAssets(nombre + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                var m = a as Material;
                if (m != null && m.name == nombre) return m;
            }
        }
        return null;
    }

    private static Texture2D TexturaDe(Material m)
    {
        var tex = m.mainTexture as Texture2D;
        if (tex != null) return CopiarRT(tex, 512, "K_" + m.name);
        var c = m.HasProperty("_Color") ? m.color : Color.white;
        var solid = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var p = new Color[256];
        for (int i = 0; i < p.Length; i++) p[i] = new Color(c.r, c.g, c.b, 1f);
        solid.SetPixels(p); solid.Apply(); solid.name = "S_" + m.name;
        return solid;
    }

    private static Texture2D CopiarRT(Texture2D origen, int tamano, string nombre)
    {
        var rt = RenderTexture.GetTemporary(tamano, tamano, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, rt);
        var previo = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0); tex.Apply();
        RenderTexture.active = previo; RenderTexture.ReleaseTemporary(rt);
        tex.name = nombre;
        return tex;
    }

    private static bool EsLadrillo(Material m) => m != null && m.name.ToLowerInvariant().Contains("brick");
    private static bool EsVidrio(Material m) => m != null && (m.name.ToLowerInvariant().Contains("glass") || m.renderQueue >= 3000);
    private static bool EsFake(Material m) => m != null && m.name.StartsWith("MI_FakeInterior");
    // Solo los interiores con imagen del kit (Small/Medium/Large) usan el fake.
    // Corner y Rowhouse traen MI_FakeInterior sin textura: se conserva su color original.
    private static bool EsFakeTexturado(Material m) => EsFake(m) && m.mainTexture != null;
}
