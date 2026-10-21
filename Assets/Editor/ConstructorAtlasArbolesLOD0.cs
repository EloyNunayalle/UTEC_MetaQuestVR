using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Empaqueta corteza+hojas de las 4 familias de árboles en un atlas y deja
/// cada árbol LOD0 con una sola submesh y un solo material Standard cutout.
/// </summary>
public static class ConstructorAtlasArbolesLOD0
{
    private const string Carpeta = "Assets/Models/StylizedNature/Billboards";
    private const string CarpetaMallas = "Assets/Models/StylizedNature/Billboards/Atlas";
    private const int TamanoAtlas = 2048;

    private class DatosTipo
    {
        public string tipo;
        public string familia;
        public Mesh malla;
        public Material corteza;
        public Material hojas;
        public readonly List<Renderer> renderers = new List<Renderer>();
    }

    [MenuItem("UTEC/Árboles/2 Atlas de árboles LOD0")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) return;

        var tipos = new Dictionary<string, DatosTipo>();
        var familias = new Dictionary<string, Material[]>();

        foreach (var grupo in Object.FindObjectsOfType<LODGroup>(true))
        {
            string nombre = grupo.gameObject.name;
            if (!(nombre.Contains("Tree") || nombre.Contains("Maple") || nombre.Contains("Pine")))
                continue;
            string tipo = nombre.Replace("_LOD", "");
            var lods = grupo.GetLODs();
            if (lods.Length == 0 || lods[0].renderers.Length == 0) continue;
            var renderer = lods[0].renderers[0];
            var mf = renderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || renderer.sharedMaterials.Length < 2) continue;

            if (!tipos.TryGetValue(tipo, out var datos))
            {
                var corteza = renderer.sharedMaterials[0];
                var hojas = renderer.sharedMaterials[1];
                if (corteza == null || hojas == null) continue;
                string familia = corteza.name.Replace("M_", "").Replace("_Bark", "");
                datos = new DatosTipo
                {
                    tipo = tipo,
                    familia = familia,
                    malla = mf.sharedMesh,
                    corteza = corteza,
                    hojas = hojas
                };
                tipos[tipo] = datos;
                if (!familias.ContainsKey(familia)) familias[familia] = new[] { corteza, hojas };
            }
            datos.renderers.Add(renderer);
        }

        if (tipos.Count == 0 || familias.Count == 0)
        {
            Debug.LogWarning("[Árboles] No hay árboles LOD0 para atlas.");
            return;
        }

        var claves = new List<string>();
        var texturas = new List<Texture2D>();
        foreach (var par in familias)
        {
            string familia = par.Key;
            foreach (var material in par.Value)
            {
                if (material.mainTexture == null) continue;
                string clave = familia + (material.name.Contains("Bark") ? "_Bark" : "_Leaves");
                claves.Add(clave);
                texturas.Add(Copiar((Texture2D)material.mainTexture, 512, clave));
            }
        }

        var atlas = new Texture2D(TamanoAtlas, TamanoAtlas, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), 4, TamanoAtlas, false);
        atlas.Apply();

        string abs = Path.Combine(Application.dataPath, "Models/StylizedNature/Billboards/T_Arboles_Atlas.png");
        File.WriteAllBytes(abs, atlas.EncodeToPNG());
        string rel = Carpeta + "/T_Arboles_Atlas.png";
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(rel);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = TamanoAtlas;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        var texturaAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
        var baseHojas = familias["Birch"][1];
        string rutaMaterial = Carpeta + "/M_Arboles_Atlas.mat";
        var materialAtlas = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (materialAtlas == null)
        {
            materialAtlas = new Material(baseHojas);
            materialAtlas.name = "M_Arboles_Atlas";
            AssetDatabase.CreateAsset(materialAtlas, rutaMaterial);
        }
        materialAtlas.shader = baseHojas.shader;
        materialAtlas.CopyPropertiesFromMaterial(baseHojas);
        materialAtlas.mainTexture = texturaAtlas;
        materialAtlas.SetColor("_Color", Color.white);
        materialAtlas.SetFloat("_Cutoff", 0.5f);
        EditorUtility.SetDirty(materialAtlas);

        if (!AssetDatabase.IsValidFolder(CarpetaMallas))
            AssetDatabase.CreateFolder(Carpeta, "Atlas");

        var rectPorClave = new Dictionary<string, Rect>();
        for (int i = 0; i < claves.Count; i++) rectPorClave[claves[i]] = rects[i];

        int mallasCreadas = 0;
        foreach (var par in tipos)
        {
            var datos = par.Value;
            var mallaAtlas = ConstruirMalla(datos, rectPorClave);
            if (mallaAtlas == null) continue;
            string ruta = AssetDatabase.GenerateUniqueAssetPath(CarpetaMallas + "/Tree_" + datos.tipo + "_Atlas.asset");
            AssetDatabase.CreateAsset(mallaAtlas, ruta);
            foreach (var renderer in datos.renderers)
            {
                renderer.GetComponent<MeshFilter>().sharedMesh = mallaAtlas;
                renderer.sharedMaterials = new[] { materialAtlas };
            }
            mallasCreadas++;
        }

        AssetDatabase.SaveAssets();
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log($"[Árboles] Atlas LOD0: familias={familias.Count}, tipos={tipos.Count}, mallas={mallasCreadas}, atlas={atlas.width}x{atlas.height}");
    }

    private static Mesh ConstruirMalla(DatosTipo datos, Dictionary<string, Rect> rects)
    {
        var malla = datos.malla;
        var v = malla.vertices;
        var n = malla.normals;
        var t = malla.tangents;
        var uv = malla.uv;
        var uv2 = malla.uv2;
        var col = malla.colors;
        bool hn = n != null && n.Length == v.Length;
        bool ht = t != null && t.Length == v.Length;
        bool huv2 = uv2 != null && uv2.Length == v.Length;
        bool hc = col != null && col.Length == v.Length;

        var nv = new List<Vector3>();
        var nn = new List<Vector3>();
        var nt = new List<Vector4>();
        var nuv = new List<Vector2>();
        var nuv2 = new List<Vector2>();
        var ncol = new List<Color>();
        var tris = new List<int>();
        var mapa = new Dictionary<long, int>();
        int region = 0;

        for (int s = 0; s < malla.subMeshCount && s < 2; s++)
        {
            var material = s == 0 ? datos.corteza : datos.hojas;
            string clave = datos.familia + (material.name.Contains("Bark") ? "_Bark" : "_Leaves");
            if (!rects.ContainsKey(clave))
            {
                Debug.LogWarning("[Árboles] Sin región de atlas: " + clave);
                return null;
            }
            Rect rect = rects[clave];
            int[] indices = malla.GetTriangles(s);
            foreach (int original in indices)
            {
                long claveVertice = ((long)original << 32) | (uint)region;
                int nuevo;
                if (!mapa.TryGetValue(claveVertice, out nuevo))
                {
                    nuevo = nv.Count;
                    mapa[claveVertice] = nuevo;
                    nv.Add(v[original]);
                    if (hn) nn.Add(n[original]);
                    if (ht) nt.Add(t[original]);
                    Vector2 baseUv = uv[original];
                    nuv.Add(new Vector2(rect.x + baseUv.x * rect.width,
                                        rect.y + baseUv.y * rect.height));
                    if (huv2) nuv2.Add(uv2[original]);
                    if (hc) ncol.Add(col[original]);
                }
                tris.Add(nuevo);
            }
            region++;
        }

        var nueva = new Mesh();
        nueva.name = "Tree_" + datos.tipo + "_Atlas";
        if (nv.Count > 65535) nueva.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        nueva.SetVertices(nv);
        if (hn) nueva.SetNormals(nn);
        if (ht) nueva.SetTangents(nt);
        nueva.SetUVs(0, nuv);
        if (huv2) nueva.SetUVs(1, nuv2);
        if (hc) nueva.SetColors(ncol);
        nueva.subMeshCount = 1;
        nueva.SetTriangles(tris, 0);
        nueva.RecalculateBounds();
        return nueva;
    }

    private static Texture2D Copiar(Texture2D origen, int tamano, string nombre)
    {
        var rt = RenderTexture.GetTemporary(tamano, tamano, 0, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, rt);
        var previo = RenderTexture.active;
        RenderTexture.active = rt;
        var textura = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false);
        textura.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0);
        textura.Apply();
        RenderTexture.active = previo;
        RenderTexture.ReleaseTemporary(rt);
        textura.name = nombre;
        return textura;
    }
}
