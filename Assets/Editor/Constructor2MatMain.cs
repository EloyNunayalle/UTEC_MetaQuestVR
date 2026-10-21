using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Convierte los edificios de la escena principal (transportados del B fix) a 2 materiales:
/// atlas opaco (ladrillo + detalle/fake + trims alternos) + vidrio transparente.
/// No crea prefabs.
/// </summary>
public static class Constructor2MatMain
{
    private const string Carpeta = "Assets/Models/CiudadNueva/2MatMain";
    private const int AtlasSize = 4096;

    private class Dato
    {
        public LODGroup grupo;
        public Renderer principal;
        public Mesh malla;
        public Material[] mats;
        public Material brick;
        public Material detail;
        public readonly List<Renderer> trims = new List<Renderer>();
        public Renderer caja;
    }

    private class Acum
    {
        public List<Vector3> v = new List<Vector3>();
        public List<Vector3> n = new List<Vector3>();
        public List<Vector4> t = new List<Vector4>();
        public List<Vector2> uv = new List<Vector2>();
        public List<Vector2> uv2 = new List<Vector2>();
        public List<Color> col = new List<Color>();
        public List<int> opacos = new List<int>();
        public List<int> vidrios = new List<int>();
        public Dictionary<string, int> map = new Dictionary<string, int>();

        public int Add(Mesh mesh, int orig, Rect rect, string key)
        {
            string k = key + "|" + orig;
            int idx;
            if (map.TryGetValue(k, out idx)) return idx;
            var vv = mesh.vertices; var nn = mesh.normals; var tt = mesh.tangents; var uu = mesh.uv; var u2 = mesh.uv2; var cc = mesh.colors;
            idx = v.Count; map[k] = idx;
            v.Add(vv[orig]);
            if (nn != null && nn.Length == vv.Length) n.Add(nn[orig]);
            if (tt != null && tt.Length == vv.Length) t.Add(tt[orig]);
            var uv0 = uu[orig];
            float ux = Mathf.Repeat(uv0.x, 1f);
            float uy = Mathf.Repeat(uv0.y, 1f);
            uv.Add(new Vector2(rect.x + ux * rect.width, rect.y + uy * rect.height));
            if (u2 != null && u2.Length == vv.Length) uv2.Add(u2[orig]);
            if (cc != null && cc.Length == vv.Length) col.Add(cc[orig]);
            return idx;
        }
    }

    [MenuItem("UTEC/Escenas/Convertir main a 2 materiales")]
    public static void Convertir()
    {
        if (EditorApplication.isPlaying) return;
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogWarning("[2Mat] Abre la escena principal."); return; }
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "2MatMain");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/2MatMain"));

        var datos = new List<Dato>();
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string n = g.gameObject.name; if (n.Contains("Tree") || n.Contains("Maple") || n.Contains("Pine")) continue;
            var l = g.GetLODs(); if (l.Length == 0 || l[0].renderers.Length == 0) continue;
            var principal = l[0].renderers[0]; var mf = principal.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 3) continue;
            var mats = principal.sharedMaterials;
            if (mats.Length < 3) continue;
            var d = new Dato { grupo = g, principal = principal, malla = mf.sharedMesh, mats = mats, brick = mats[0], detail = mats[1] };
            // Trims alternos: renderers del root con material M_Parte_Trim_*.
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r == principal) continue;
                if (r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null && r.sharedMaterials[0].name.StartsWith("M_Parte_Trim_")) d.trims.Add(r);
            }
            foreach (Transform t in g.transform) if (t.name.Contains("Caja")) { d.caja = t.GetComponent<Renderer>(); break; }
            datos.Add(d);
        }
        if (datos.Count == 0) return;

        // Atlas: detalle/fake BVF + ladrillos + trims alternos.
        var claves = new List<string>(); var texturas = new List<Texture2D>();
        var detalleTex = datos[0].detail.mainTexture as Texture2D;
        claves.Add("__DETAIL__"); texturas.Add(Copiar(detalleTex, 2048, "DETAIL_2MAT"));
        var bricks = new Dictionary<string, Material>(); var trims = new Dictionary<string, Material>();
        foreach (var d in datos)
        {
            if (d.brick != null && !bricks.ContainsKey(d.brick.name)) bricks[d.brick.name] = d.brick;
            foreach (var r in d.trims)
            {
                var m = r.sharedMaterials[0];
                if (m != null && !trims.ContainsKey(m.name)) trims[m.name] = m;
            }
        }
        foreach (var par in bricks) { var t = par.Value.mainTexture as Texture2D; if (t != null) { claves.Add("BRICK:" + par.Key); texturas.Add(Copiar(t, 512, "BRICK_" + par.Key)); } }
        foreach (var par in trims) { var t = par.Value.mainTexture as Texture2D; var c = par.Value.HasProperty("_Color") ? par.Value.GetColor("_Color") : Color.white; claves.Add("TRIM:" + par.Key); texturas.Add(Copiar(t, 512, "TRIM_" + par.Key, c)); }

        var atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), 4, AtlasSize, false); atlas.Apply();
        var mapa = new Dictionary<string, Rect>(); for (int i = 0; i < claves.Count; i++) mapa[claves[i]] = rects[i];
        string abs = Path.Combine(Application.dataPath, "Models/CiudadNueva/2MatMain/T_Atlas_2Mat.png");
        File.WriteAllBytes(abs, atlas.EncodeToPNG());
        string rel = Carpeta + "/T_Atlas_2Mat.png"; AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(rel);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false;
        imp.mipmapEnabled = true; imp.maxTextureSize = AtlasSize; imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        var texAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
        string rutaMat = Carpeta + "/M_Atlas_2Mat.mat";
        var matAtlas = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (matAtlas == null) { matAtlas = new Material(Shader.Find("Standard")); matAtlas.name = "M_Atlas_2Mat"; AssetDatabase.CreateAsset(matAtlas, rutaMat); }
        matAtlas.shader = Shader.Find("Standard"); matAtlas.mainTexture = texAtlas; matAtlas.SetColor("_Color", Color.white); matAtlas.SetFloat("_Glossiness", 0.15f); matAtlas.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(matAtlas);
        var vidrio = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/CiudadNueva/Materials/M_Glass_Transparente.mat");
        var rectDetalle = mapa["__DETAIL__"];
        var rectBrickPorNombre = new Dictionary<string, Rect>();
        foreach (var par in bricks) if (mapa.ContainsKey("BRICK:" + par.Key)) rectBrickPorNombre[par.Key] = mapa["BRICK:" + par.Key];
        var rectTrimPorNombre = new Dictionary<string, Rect>();
        foreach (var par in trims) if (mapa.ContainsKey("TRIM:" + par.Key)) rectTrimPorNombre[par.Key] = mapa["TRIM:" + par.Key];

        int convertidos = 0;
        foreach (var d in datos)
        {
            var acc = new Acum();
            var mesh = d.malla;
            // Brick (sub 0).
            var rectB = rectBrickPorNombre.ContainsKey(d.brick.name) ? rectBrickPorNombre[d.brick.name] : rectDetalle;
            foreach (int orig in mesh.GetTriangles(0)) acc.opacos.Add(acc.Add(mesh, orig, rectB, "BRICK"));
            // Detalle (sub 1).
            foreach (int orig in mesh.GetTriangles(1)) acc.opacos.Add(acc.Add(mesh, orig, rectDetalle, "DETAIL"));
            // Trims alternos.
            int ti = 0;
            foreach (var r in d.trims)
            {
                var m = r.sharedMaterials[0]; var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !rectTrimPorNombre.ContainsKey(m.name)) { ti++; continue; }
                var rm = mf.sharedMesh; var rt = rectTrimPorNombre[m.name];
                foreach (int orig in rm.GetTriangles(0)) acc.opacos.Add(acc.Add(rm, orig, rt, "TRIM" + ti));
                ti++;
            }
            // Vidrio (sub 2).
            foreach (int orig in mesh.GetTriangles(2)) acc.vidrios.Add(acc.Add(mesh, orig, new Rect(0, 0, 1, 1), "GLASS"));

            var nm = new Mesh(); nm.name = "2Mat_" + d.grupo.gameObject.name;
            if (acc.v.Count > 65535) nm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            nm.SetVertices(acc.v); if (acc.n.Count == acc.v.Count) nm.SetNormals(acc.n); if (acc.t.Count == acc.v.Count) nm.SetTangents(acc.t); nm.SetUVs(0, acc.uv); if (acc.uv2.Count == acc.v.Count) nm.SetUVs(1, acc.uv2); if (acc.col.Count == acc.v.Count) nm.SetColors(acc.col);
            nm.subMeshCount = 2; nm.SetTriangles(acc.opacos, 0); nm.SetTriangles(acc.vidrios, 1); nm.RecalculateBounds();
            string ruta = AssetDatabase.GenerateUniqueAssetPath(Carpeta + "/" + nm.name + ".asset"); AssetDatabase.CreateAsset(nm, ruta);
            d.principal.GetComponent<MeshFilter>().sharedMesh = nm;
            d.principal.sharedMaterials = new[] { matAtlas, vidrio };
            foreach (var r in d.trims) if (r != null) Object.DestroyImmediate(r.gameObject);
            if (d.caja != null)
            {
                d.caja.sharedMaterial = matAtlas;
                var rect = rectB;
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector("_MainTex_ST", new Vector4(rect.width, rect.height, rect.x, rect.y));
                d.caja.SetPropertyBlock(mpb);
            }
            var niveles = new List<LOD>(); niveles.Add(new LOD(0.25f, new Renderer[] { d.principal })); if (d.caja != null) niveles.Add(new LOD(0.10f, new Renderer[] { d.caja })); niveles.Add(new LOD(0.03f, new Renderer[0]));
            d.grupo.SetLODS(niveles.ToArray()); d.grupo.RecalculateBounds();
            convertidos++;
        }

        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        Debug.Log("[2Mat] edificios=" + convertidos + " atlas=" + atlas.width + "x" + atlas.height);
    }

    private static Texture2D Copiar(Texture2D origen, int tamano, string nombre)
    {
        return Copiar(origen, tamano, nombre, Color.white);
    }

    private static Texture2D Copiar(Texture2D origen, int tamano, string nombre, Color color)
    {
        var rt = RenderTexture.GetTemporary(tamano, tamano, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, rt);
        var previo = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0); tex.Apply();
        RenderTexture.active = previo; RenderTexture.ReleaseTemporary(rt);
        if (color.r < 0.999f || color.g < 0.999f || color.b < 0.999f)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * color.r, px[i].g * color.g, px[i].b * color.b, px[i].a);
            tex.SetPixels(px); tex.Apply();
        }
        tex.name = nombre; return tex;
    }
}
