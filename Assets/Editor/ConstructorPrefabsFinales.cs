using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Genera prefabs finales A1/A2 a partir de la escena principal actual.
/// A2 = 2 materiales (atlas opaco + vidrio). A1 = 1 material (atlas + vidrio opaco).
/// No modifica la escena.
/// </summary>
public static class ConstructorPrefabsFinales
{
    private const string CarpetaA1 = "Assets/Models/CiudadNueva/FinalA1_Actual";
    private const string PrefabsA1 = "Assets/Prefabs/Ciudad/Edificios/A1/Paleta";
    private const string PrefabsA2 = "Assets/Prefabs/Ciudad/Edificios/A2/Paleta";
    private const int AtlasSize = 4096;

    private class Dato
    {
        public GameObject root;
        public LODGroup grupo;
        public Renderer principal;
        public Mesh malla;
        public Renderer caja;
        public string tipo;
        public string color;
    }

    [MenuItem("UTEC/Prefabs/Generar prefabs finales A1 y A2")]
    public static void Generar()
    {
        if (EditorApplication.isPlaying) return;
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (escena.name != "Ciudad_Tile_00_Estacion") { Debug.LogWarning("[PrefabsFinal] Abre la escena principal."); return; }

        CrearCarpeta("Assets/Prefabs/Ciudad", "Edificios");
        CrearCarpeta("Assets/Prefabs/Ciudad/Edificios", "A1");
        CrearCarpeta("Assets/Prefabs/Ciudad/Edificios/A1", "Paleta");
        CrearCarpeta("Assets/Prefabs/Ciudad/Edificios", "A2");
        CrearCarpeta("Assets/Prefabs/Ciudad/Edificios/A2", "Paleta");
        if (!AssetDatabase.IsValidFolder(CarpetaA1)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "FinalA1_Actual");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/CiudadNueva/FinalA1_Actual"));

        var datos = new List<Dato>();
        foreach (var g in Object.FindObjectsOfType<LODGroup>())
        {
            string n = g.gameObject.name; if (n.Contains("Tree") || n.Contains("Maple") || n.Contains("Pine")) continue;
            var l = g.GetLODs(); if (l.Length == 0 || l[0].renderers.Length == 0) continue;
            var r = l[0].renderers[0]; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 2) continue;
            Renderer caja = null; foreach (Transform t in g.transform) if (t.name.Contains("Caja")) { caja = t.GetComponent<Renderer>(); break; }
            datos.Add(new Dato { root = g.gameObject, grupo = g, principal = r, malla = mf.sharedMesh, caja = caja, tipo = Tipo(g), color = ColorDe(g) });
        }
        if (datos.Count == 0) return;

        // Atlas A1 actual: atlas 2mat como una región + vidrio opaco.
        var tex2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/CiudadNueva/2MatMain/T_Atlas_2Mat.png");
        var copia2 = Copiar(tex2, 2048, "A1_2MAT");
        var vidrio = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64]; for (int i = 0; i < px.Length; i++) px[i] = new Color(0.20f, 0.24f, 0.30f, 1f);
        vidrio.SetPixels(px); vidrio.Apply(); vidrio.name = "A1_GLASS";
        var atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(new[] { copia2, vidrio }, 4, AtlasSize, false);
        var rect2 = rects[0];
        // PackTextures puede devolver un rect nulo para texturas pequeñas.
        // Reservar un bloque sólido de vidrio opaco en una zona libre.
        var rectGlass = new Rect(0.90f, 0.75f, 0.02f, 0.01f);
        PintarBloque(atlas, rectGlass, new Color(0.20f, 0.24f, 0.30f, 1f));
        atlas.Apply();
        string abs = Path.Combine(Application.dataPath, "Models/CiudadNueva/FinalA1_Actual/T_Atlas_A1_Actual.png");
        File.WriteAllBytes(abs, atlas.EncodeToPNG());
        string rel = CarpetaA1 + "/T_Atlas_A1_Actual.png"; AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(rel);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = false; imp.mipmapEnabled = true; imp.maxTextureSize = AtlasSize; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
        var texAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
        string rutaMat = CarpetaA1 + "/M_Atlas_A1_Actual.mat";
        var matA1 = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (matA1 == null) { matA1 = new Material(Shader.Find("Standard")); matA1.name = "M_Atlas_A1_Actual"; AssetDatabase.CreateAsset(matA1, rutaMat); }
        matA1.shader = Shader.Find("Standard"); matA1.mainTexture = texAtlas; matA1.SetColor("_Color", Color.white); matA1.SetFloat("_Glossiness", 0.15f); matA1.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(matA1);

        int a1 = 0, a2 = 0;
        foreach (var d in datos)
        {
            // A2 actual: duplicar el root en el origen y guardar prefab.
            string nombreA2 = "EDF_A2_Paleta_" + d.tipo + "_" + d.color;
            var dup = Object.Instantiate(d.root);
            dup.name = nombreA2;
            dup.transform.SetParent(null, true);
            dup.transform.position = Vector3.zero;
            dup.transform.rotation = d.root.transform.rotation;
            string rutaA2 = PrefabsA2 + "/" + nombreA2 + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(dup, rutaA2);
            Object.DestroyImmediate(dup);
            ArreglarCajaA2(rutaA2, d);
            a2++;

            // A1 actual: un solo material, vidrio opaco en el atlas.
            var mallaA1 = ConstruirA1(d.malla, rect2, rectGlass, "A1_" + d.root.name + "_LOD0");
            if (mallaA1 == null) continue;
            string rutaMalla = AssetDatabase.GenerateUniqueAssetPath(CarpetaA1 + "/" + mallaA1.name + ".asset");
            AssetDatabase.CreateAsset(mallaA1, rutaMalla);
            string nombreA1 = "EDF_A1_Paleta_" + d.tipo + "_" + d.color;
            CrearPrefabA1(d, mallaA1, matA1, rect2, nombreA1);
            a1++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PrefabsFinal] A2 actuales=" + a2 + " A1 actuales=" + a1);
    }

    private static Mesh ConstruirA1(Mesh origen, Rect rect2, Rect rectGlass, string nombre)
    {
        var v = origen.vertices; var n = origen.normals; var t = origen.tangents; var uv = origen.uv; var uv2 = origen.uv2; var col = origen.colors;
        bool hn = n != null && n.Length == v.Length, ht = t != null && t.Length == v.Length, huv2 = uv2 != null && uv2.Length == v.Length, hc = col != null && col.Length == v.Length;
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var ncol = new List<Color>(); var tris = new List<int>();
        var map = new Dictionary<long, int>();
        for (int s = 0; s < origen.subMeshCount; s++)
        {
            int kind = s; Rect rect = s == 0 ? rect2 : rectGlass;
            foreach (int orig in origen.GetTriangles(s))
            {
                long k = ((long)orig << 32) | (uint)kind; int ni;
                if (!map.TryGetValue(k, out ni))
                {
                    ni = nv.Count; map[k] = ni; nv.Add(v[orig]); if (hn) nn.Add(n[orig]); if (ht) nt.Add(t[orig]);
                    if (s == 0) nuv.Add(new Vector2(rect.x + Mathf.Repeat(uv[orig].x, 1f) * rect.width, rect.y + Mathf.Repeat(uv[orig].y, 1f) * rect.height));
                    else nuv.Add(new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f));
                    if (huv2) nuv2.Add(uv2[orig]); if (hc) ncol.Add(col[orig]);
                }
                tris.Add(ni);
            }
        }
        if (tris.Count == 0) return null;
        var nm = new Mesh(); nm.name = nombre; if (nv.Count > 65535) nm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        nm.SetVertices(nv); if (hn) nm.SetNormals(nn); if (ht) nm.SetTangents(nt); nm.SetUVs(0, nuv); if (huv2) nm.SetUVs(1, nuv2); if (hc) nm.SetColors(ncol);
        nm.subMeshCount = 1; nm.SetTriangles(tris, 0); nm.RecalculateBounds(); return nm;
    }

    private static void CrearPrefabA1(Dato d, Mesh malla, Material mat, Rect rect2, string nombre)
    {
        var root = new GameObject(nombre);
        root.transform.rotation = d.root.transform.rotation;
        var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
        l0.AddComponent<MeshFilter>().sharedMesh = malla; var mr = l0.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        var caja = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(caja.GetComponent<Collider>()); caja.name = "LOD1_Caja"; caja.transform.SetParent(root.transform, false);
        if (d.caja != null) { caja.transform.localPosition = d.caja.transform.localPosition; caja.transform.localRotation = d.caja.transform.localRotation; caja.transform.localScale = d.caja.transform.localScale; }
        var cr = caja.GetComponent<MeshRenderer>(); cr.sharedMaterial = mat; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Un MaterialPropertyBlock no se serializa al guardar el prefab. La caja usa
        // una UV fija dentro del ladrillo del atlas A1 (región 2mat).
        var punto = PuntoLadrillo(d, rect2);
        var mallaCaja = CrearMallaCaja(punto, "CajaA1_" + nombre);
        caja.GetComponent<MeshFilter>().sharedMesh = mallaCaja;
        var col = root.AddComponent<BoxCollider>();
        if (d.caja != null) { col.center = d.caja.transform.localPosition; col.size = d.caja.transform.localScale; }
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODS(new[] { new LOD(0.25f, new Renderer[] { mr }), new LOD(0.10f, new Renderer[] { cr }), new LOD(0.03f, new Renderer[0]) });
        lod.RecalculateBounds();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabsA1 + "/" + nombre + ".prefab");
        Object.DestroyImmediate(root);
    }

    private static Vector2 PuntoLadrillo(Dato d, Rect rect2)
    {
        var st = new Vector4(1f, 1f, 0f, 0f);
        if (d.caja != null)
        {
            var mpb = new MaterialPropertyBlock(); d.caja.GetPropertyBlock(mpb);
            var leido = mpb.GetVector("_MainTex_ST");
            if (leido != Vector4.zero) st = leido;
        }
        // Centro de la región de ladrillo dentro de la región 2mat del atlas A1.
        return new Vector2(rect2.x + (st.z + st.x * 0.5f) * rect2.width,
                           rect2.y + (st.w + st.y * 0.5f) * rect2.height);
    }

    private static Mesh CrearMallaCaja(Vector2 uvPoint, string nombre)
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(temp);
        mesh.name = nombre;
        var uv = mesh.uv; for (int i = 0; i < uv.Length; i++) uv[i] = uvPoint; mesh.uv = uv;
        string ruta = AssetDatabase.GenerateUniqueAssetPath(CarpetaA1 + "/" + nombre + ".asset");
        AssetDatabase.CreateAsset(mesh, ruta);
        return mesh;
    }

    private static void ArreglarCajaA2(string prefabPath, Dato d)
    {
        var st = new Vector4(1f, 1f, 0f, 0f);
        if (d.caja != null)
        {
            var mpb = new MaterialPropertyBlock(); d.caja.GetPropertyBlock(mpb);
            var leido = mpb.GetVector("_MainTex_ST");
            if (leido != Vector4.zero) st = leido;
        }
        var punto = new Vector2(st.z + st.x * 0.5f, st.w + st.y * 0.5f);
        var content = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform caja = null; foreach (Transform t in content.transform) if (t.name.Contains("Caja")) { caja = t; break; }
            if (caja == null) return;
            var malla = CrearMallaCaja(punto, "CajaA2_" + d.tipo + "_" + d.color);
            caja.GetComponent<MeshFilter>().sharedMesh = malla;
            caja.GetComponent<MeshRenderer>().SetPropertyBlock(new MaterialPropertyBlock());
            PrefabUtility.SaveAsPrefabAsset(content, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(content); }
    }

    private static void PintarBloque(Texture2D tex, Rect rect, Color color)
    {
        int x0 = Mathf.Clamp(Mathf.RoundToInt(rect.x * tex.width), 0, tex.width - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(rect.y * tex.height), 0, tex.height - 1);
        int x1 = Mathf.Clamp(Mathf.RoundToInt((rect.x + rect.width) * tex.width), x0 + 1, tex.width);
        int y1 = Mathf.Clamp(Mathf.RoundToInt((rect.y + rect.height) * tex.height), y0 + 1, tex.height);
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) tex.SetPixel(x, y, color);
    }

    private static string Tipo(LODGroup g)
    {
        string n = g.gameObject.name;
        if (n.Contains("Medium")) return "Medium";
        if (n.Contains("Corner")) return "Corner";
        if (n.Contains("Rowhouse")) return "Rowhouse";
        return "Small";
    }

    private static string ColorDe(LODGroup g)
    {
        string n = g.gameObject.name; var p = g.transform.position;
        if (n.Contains("Medium")) return "Ocre";
        if (n.Contains("Corner")) return "RedBrick";
        if (n.Contains("Rowhouse")) return "Marron";
        if (p.x < -20f) return "Gris";
        if (p.x < 0f) return "Arena";
        return "Blanco";
    }

    private static void CrearCarpeta(string padre, string nombre)
    {
        if (!AssetDatabase.IsValidFolder(padre + "/" + nombre)) AssetDatabase.CreateFolder(padre, nombre);
    }

    private static Texture2D Copiar(Texture2D origen, int tamano, string nombre)
    {
        var rt = RenderTexture.GetTemporary(tamano, tamano, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, rt); var previo = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0); tex.Apply();
        RenderTexture.active = previo; RenderTexture.ReleaseTemporary(rt); tex.name = nombre; return tex;
    }
}
