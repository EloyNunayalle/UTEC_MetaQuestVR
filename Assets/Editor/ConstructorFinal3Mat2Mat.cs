using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reconstruye las versiones actuales:
/// A2 = 3 materiales (ladrillo + detalle/fake + vidrio).
/// A1 = 2 materiales (ladrillo + atlas opaco sin fake).
/// Corrige la escalera de Rowhouse/Corner elevando el edificio 1.006 m y
/// agregando una base. No guarda ni modifica la escena B_FakeFix.
/// </summary>
public static class ConstructorFinal3Mat2Mat
{
    private const string Carpeta = "Assets/Models/CiudadNueva/Final3Mat2Mat";
    private const string BScene = "Assets/Scenes/Ciudad_Tile_00_Estacion_B_FakeFix.unity";
    private const string MainScene = "Assets/Scenes/Ciudad_Tile_00_Estacion.unity";
    private static Material _a1Mat;

    private class Dato
    {
        public GameObject root;
        public Mesh mesh;
        public Material brick;
        public Material detail;
        public Material glass;
        public string tipo;
        public string color;
        public bool nuevaGeometria;
    }

    [MenuItem("UTEC/Edificios/Construir final 3mat A2 y 2mat A1")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) return;
        var previa = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/Final3Mat2Mat"));
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "Final3Mat2Mat");

        var datos = new List<Dato>();
        var bscene = EditorSceneManager.OpenScene(BScene, OpenSceneMode.Single);
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string n = g.gameObject.name;
            if (n.Contains("Tree") || n.Contains("Maple") || n.Contains("Pine")) continue;
            var lv = g.GetLODs(); if (lv.Length == 0 || lv[0].renderers.Length == 0) continue;
            var r = lv[0].renderers[0]; var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || r.sharedMaterials.Length < 3) continue;
            string tipo = n.Contains("Medium") ? "Medium" : n.Contains("Corner") ? "Corner" : n.Contains("Rowhouse") ? "Rowhouse" : "Small";
            string color = n.Contains("Medium") ? "Ocre" : n.Contains("Corner") ? "RedBrick" : n.Contains("Rowhouse") ? "Marron" : (g.transform.position.x < -20f ? "Gris" : g.transform.position.x < 0f ? "Arena" : "Blanco");
            datos.Add(new Dato { root = g.gameObject, mesh = mf.sharedMesh, brick = r.sharedMaterials[0], detail = r.sharedMaterials[1], glass = r.sharedMaterials[2], tipo = tipo, color = color, nuevaGeometria = tipo == "Rowhouse" || tipo == "Corner" });
        }

        CrearAtlasA1();
        int nA2 = 0, nA1 = 0;
        foreach (var d in datos)
        {
            Mesh meshA2 = d.mesh;
            if (d.nuevaGeometria) meshA2 = CorregirEscalera(d);
            CrearPrefab(d, meshA2, false);
            nA2++;
            var meshA1 = ConstruirA1(meshA2);
            if (meshA1 != null) { CrearPrefab(d, meshA1, true); nA1++; }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        // Reemplazar las instancias A2 de la escena por los prefabs nuevos.
        int reemplazos = ReemplazarEscenaA2();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[Final3Mat2Mat] A2=" + nA2 + " A1=" + nA1 + " escena=" + reemplazos);
    }

    private static void CrearAtlasA1()
    {
        var detalle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/BVerdeFake/M_Detalle_BVerdeFake.mat");
        var tex = detalle != null ? detalle.mainTexture as Texture2D : null;
        if (tex == null) return;
        int W = 4096, H = 4096;
        var px = new Color32[W * H];
        var fondo = new Color32(60, 55, 45, 255);
        for (int i = 0; i < px.Length; i++) px[i] = fondo;
        var src = LeerPixels(tex);
        int sw = tex.width, sh = tex.height;
        // detalle original en la mitad inferior, sin escalar.
        for (int y = 0; y < Mathf.Min(sh, H / 2); y++)
            for (int x = 0; x < Mathf.Min(sw, W); x++)
                px[y * W + x] = src[y * sw + x];
        // La mitad derecha del detalle contiene los fake; en A1 se vuelve ventana opaca.
        var ventana = new Color32(48, 58, 72, 255);
        for (int y = 0; y < H / 2; y++)
            for (int x = W / 2; x < W; x++) px[y * W + x] = ventana;
        // Bloque de ventana opaca en la mitad superior.
        for (int y = H / 2; y < H / 2 + 256; y++)
            for (int x = 0; x < 256; x++) px[y * W + x] = ventana;

        var atlas = new Texture2D(W, H, TextureFormat.RGBA32, true);
        atlas.SetPixels32(px); atlas.Apply();
        string png = Carpeta + "/T_Atlas_A1_2Mat.png";
        File.WriteAllBytes(Path.Combine(Application.dataPath, "Models/CiudadNueva/Final3Mat2Mat/T_Atlas_A1_2Mat.png"), atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false; imp.mipmapEnabled = true; imp.maxTextureSize = 4096; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        var texAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        string matPath = Carpeta + "/M_Atlas_A1_2Mat.mat";
        _a1Mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (_a1Mat == null) { _a1Mat = new Material(Shader.Find("Standard")); _a1Mat.name = "M_Atlas_A1_2Mat"; AssetDatabase.CreateAsset(_a1Mat, matPath); }
        _a1Mat.shader = Shader.Find("Standard"); _a1Mat.mainTexture = texAtlas; _a1Mat.SetColor("_Color", Color.white); _a1Mat.SetFloat("_Glossiness", 0.1f); _a1Mat.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(_a1Mat);
    }

    private static Color32[] LeerPixels(Texture2D tex)
    {
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); t.Apply();
        var px = t.GetPixels32();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(t);
        return px;
    }

    private static Mesh CorregirEscalera(Dato d)
    {
        string fuente = d.tipo == "Rowhouse" ? "Assets/Models/CiudadNueva/Rowhouse_A.prefab" : "Assets/Models/CiudadNueva/Corner_Shop_B.prefab";
        var pref = AssetDatabase.LoadAssetAtPath<GameObject>(fuente);
        if (pref == null) return d.mesh;
        Transform stairs = null;
        foreach (var tr in pref.GetComponentsInChildren<Transform>(true)) if (tr.name == "Stairs_Entrance_Concrete") { stairs = tr; break; }
        if (stairs == null) return d.mesh;
        var smf = stairs.GetComponent<MeshFilter>(); if (smf == null || smf.sharedMesh == null) return d.mesh;
        var inv = pref.transform.worldToLocalMatrix;
        var stairSet = new HashSet<string>();
        float maxY = 0f;
        foreach (var p in smf.sharedMesh.vertices)
        {
            var w = inv.MultiplyPoint3x4(stairs.transform.localToWorldMatrix.MultiplyPoint3x4(p));
            stairSet.Add(w.x.ToString("F3") + "," + w.y.ToString("F3") + "," + w.z.ToString("F3"));
            maxY = Mathf.Max(maxY, w.y);
        }
        if (maxY < 0.5f) maxY = 1.0067f;

        var src = d.mesh;
        var v = src.vertices; var n = src.normals; var t = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        for (int i = 0; i < v.Length; i++)
        {
            string key = v[i].x.ToString("F3") + "," + v[i].y.ToString("F3") + "," + v[i].z.ToString("F3");
            if (!stairSet.Contains(key)) v[i].y += maxY;
        }

        // Base/fundación bajo el edificio (sin la escalera).
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in pref.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("Stairs")) continue;
            var rr = r.bounds; var c = rr.center; var e = rr.extents;
            for (int k = 0; k < 8; k++)
            {
                var q = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                q = inv.MultiplyPoint3x4(q); if (first) { b = new Bounds(q, Vector3.zero); first = false; } else b.Encapsulate(q);
            }
        }
        if (first) b = src.bounds;
        var NV = new List<Vector3>(); var NN = new List<Vector3>(); var NT = new List<Vector4>(); var NUV = new List<Vector2>(); var NUV2 = new List<Vector2>(); var NC = new List<Color>();
        for (int i = 0; i < v.Length; i++)
        {
            NV.Add(v[i]); if (n.Length == v.Length) NN.Add(n[i]); if (t.Length == v.Length) NT.Add(t[i]); NUV.Add(uv[i]);
            if (uv2.Length == v.Length) NUV2.Add(uv2[i]); if (col.Length == v.Length) NC.Add(col[i]);
        }
        int baseStart = NV.Count;
        float bx = Mathf.Max(0.1f, b.size.x * 0.98f), bz = Mathf.Max(0.1f, b.size.z * 0.98f);
        Vector3 bc = new Vector3(b.center.x, maxY * 0.5f, b.center.z);
        Vector3[] c8 = {
            bc+new Vector3(-bx/2,-maxY/2,-bz/2), bc+new Vector3(bx/2,-maxY/2,-bz/2), bc+new Vector3(bx/2,maxY/2,-bz/2), bc+new Vector3(-bx/2,maxY/2,-bz/2),
            bc+new Vector3(-bx/2,-maxY/2,bz/2), bc+new Vector3(bx/2,-maxY/2,bz/2), bc+new Vector3(bx/2,maxY/2,bz/2), bc+new Vector3(-bx/2,maxY/2,bz/2)
        };
        for (int i = 0; i < 8; i++) { NV.Add(c8[i]); NN.Add(Vector3.up); NUV.Add(new Vector2(0.5f, 0.5f)); }
        int[] btri = {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,3,0,4,3,4,7,1,2,6,1,6,5};

        var tris = new List<int>[src.subMeshCount]; for (int s = 0; s < tris.Length; s++) tris[s] = new List<int>(src.GetTriangles(s));
        foreach (var q in btri) tris[0].Add(baseStart + q);
        var mesh = new Mesh(); mesh.name = d.mesh.name + "_EscaleraFix";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(NV); if (NN.Count == NV.Count) mesh.SetNormals(NN); if (NT.Count == NV.Count) mesh.SetTangents(NT);
        mesh.SetUVs(0, NUV); if (NUV2.Count == NV.Count) mesh.SetUVs(1, NUV2); if (NC.Count == NV.Count) mesh.SetColors(NC);
        mesh.subMeshCount = tris.Length; for (int s = 0; s < tris.Length; s++) mesh.SetTriangles(tris[s], s); mesh.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + d.tipo + "_EscaleraFix.asset");
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh ConstruirA1(Mesh src)
    {
        if (_a1Mat == null) return null;
        var v = src.vertices; var n = src.normals; var t = src.tangents; var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>();
        var tris = new List<int>[2] { new List<int>(), new List<int>() };
        var map = new Dictionary<long, int>();
        int Add(int o, int sub)
        {
            long k = ((long)o << 2) | (uint)sub; int i;
            if (map.TryGetValue(k, out i)) return i;
            i = nv.Count; map[k] = i; nv.Add(v[o]);
            if (n.Length == v.Length) nn.Add(n[o]); if (t.Length == v.Length) nt.Add(t[o]);
            if (uv2.Length == v.Length) nuv2.Add(uv2[o]); if (col.Length == v.Length) nc.Add(col[o]);
            if (sub == 0) nuv.Add(uv[o]);
            else if (sub == 1) nuv.Add(new Vector2(uv[o].x, uv[o].y * 0.5f));
            else nuv.Add(new Vector2(128f / 4096f, 0.53125f));
            return i;
        }
        if (src.subMeshCount > 0) foreach (var o in src.GetTriangles(0)) tris[0].Add(Add(o, 0));
        if (src.subMeshCount > 1) foreach (var o in src.GetTriangles(1)) tris[1].Add(Add(o, 1));
        if (src.subMeshCount > 2) foreach (var o in src.GetTriangles(2)) tris[1].Add(Add(o, 2));
        var m = new Mesh(); m.name = src.name + "_A1_2Mat"; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(nv); if (nn.Count == nv.Count) m.SetNormals(nn); if (nt.Count == nv.Count) m.SetTangents(nt); m.SetUVs(0, nuv); if (nuv2.Count == nv.Count) m.SetUVs(1, nuv2); if (nc.Count == nv.Count) m.SetColors(nc);
        m.subMeshCount = 2; m.SetTriangles(tris[0], 0); m.SetTriangles(tris[1], 1); m.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + src.name + "_A1_2Mat.asset");
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static void CrearPrefab(Dato d, Mesh mesh, bool a1)
    {
        string nombre = a1 ? "EDF_A1_Paleta_" + d.tipo + "_" + d.color : "EDF_A2_Paleta_" + d.tipo + "_" + d.color;
        string carpeta = a1 ? "Assets/Prefabs/Ciudad/Edificios/A1/Paleta" : "Assets/Prefabs/Ciudad/Edificios/A2/Paleta";
        var root = new GameObject(nombre);
        root.transform.rotation = d.root.transform.rotation;
        var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
        l0.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = l0.AddComponent<MeshRenderer>();
        mr.sharedMaterials = a1 ? new[] { d.brick, _a1Mat } : new[] { d.brick, d.detail, d.glass };
        var caja = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(caja.GetComponent<Collider>()); caja.name = "LOD2_Caja"; caja.transform.SetParent(root.transform, false);
        caja.transform.localPosition = mesh.bounds.center; caja.transform.localScale = mesh.bounds.size;
        var cr = caja.GetComponent<MeshRenderer>(); cr.sharedMaterial = d.brick; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var col = root.AddComponent<BoxCollider>(); col.center = mesh.bounds.center; col.size = mesh.bounds.size;
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(0.25f, new Renderer[] { mr }), new LOD(0.05f, new Renderer[] { cr }), new LOD(0.02f, new Renderer[0]) });
        lod.RecalculateBounds();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        PrefabUtility.SaveAsPrefabAsset(root, carpeta + "/" + nombre + ".prefab");
        Object.DestroyImmediate(root);
    }

    private static int ReemplazarEscenaA2()
    {
        int n = 0;
        var roots = new List<GameObject>();
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string nm = g.name; if (nm.Contains("Tree") || nm.Contains("Maple") || nm.Contains("Pine")) continue;
            roots.Add(g.gameObject);
        }
        foreach (var old in roots)
        {
            string nm = old.name;
            string tipo = nm.Contains("Medium") ? "Medium" : nm.Contains("Corner") ? "Corner" : nm.Contains("Rowhouse") ? "Rowhouse" : "Small";
            string color = nm.Contains("Medium") ? "Ocre" : nm.Contains("Corner") ? "RedBrick" : nm.Contains("Rowhouse") ? "Marron" : (old.transform.position.x < -20f ? "Gris" : old.transform.position.x < 0f ? "Arena" : "Blanco");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ciudad/Edificios/A2/Paleta/EDF_A2_Paleta_" + tipo + "_" + color + ".prefab");
            if (prefab == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
            inst.name = nm; inst.transform.position = old.transform.position; inst.transform.rotation = old.transform.rotation; inst.transform.localScale = old.transform.localScale;
            Object.DestroyImmediate(old); n++;
        }
        return n;
    }
}
