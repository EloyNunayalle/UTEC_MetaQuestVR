using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconstruye el atlas de árboles (corteza + hojas) y las mallas LOD0.
///
/// La corteza original usa UVs tileados fuera de [0,1] (p. ej. v de -4.3 a 5.3).
/// Por eso su celda del atlas contiene una repetición 2x2 de la textura: cada
/// triángulo se desplaza por un número entero de tiles y se mapea dentro de la
/// celda, de modo que nunca muestrea fuera del atlas. Las hojas ya usan UVs 0..1.
/// Los pocos triángulos de corteza que abarcan más de 2 tiles (Normal) se
/// ajustan uniformemente a la celda; el resto conserva la densidad original.
///
/// Los assets se modifican en sitio para conservar GUIDs y las referencias de
/// la escena. Se puede ejecutar varias veces sin crear assets duplicados.
/// </summary>
public static class ConstructorAtlasArbolesLOD0
{
    private const string Carpeta = "Assets/Models/StylizedNature/Billboards";
    private const string CarpetaMallas = Carpeta + "/Atlas";
    private const string CarpetaPrefabs = "Assets/Models/StylizedNature/Prefabs";
    private const string RutaAtlas = Carpeta + "/T_Arboles_Atlas.png";
    private const string RutaMaterial = Carpeta + "/M_Arboles_Atlas.mat";
    private const int TamanoAtlas = 2048;
    private const int TamanoCelda = 504;
    private const int Padding = 4;
    private const int FactorCorteza = 2;
    private const float InsetTexeles = 1.5f;

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

        var tipos = RecolectarTipos();
        if (tipos.Count == 0)
        {
            Debug.LogWarning("[Árboles] No se encontraron árboles LOD para reconstruir.");
            return;
        }

        var familias = new Dictionary<string, Material[]>();
        foreach (var datos in tipos.Values)
        {
            if (!familias.ContainsKey(datos.familia))
                familias[datos.familia] = new[] { datos.corteza, datos.hojas };
        }

        var nombresFamilias = new List<string>(familias.Keys);
        nombresFamilias.Sort();

        var claves = new List<string>();
        var texturas = new List<Texture2D>();
        foreach (var familia in nombresFamilias)
        {
            var mats = familias[familia];
            if (mats[0] == null || mats[1] == null ||
                mats[0].mainTexture == null || mats[1].mainTexture == null)
            {
                Debug.LogError("[Árboles] Falta textura para la familia " + familia);
                return;
            }

            claves.Add(familia + "_Bark");
            texturas.Add(CrearCeldaCorteza((Texture2D)mats[0].mainTexture));

            claves.Add(familia + "_Leaves");
            texturas.Add(CrearCeldaHojas((Texture2D)mats[1].mainTexture));
        }

        var atlas = new Texture2D(TamanoAtlas, TamanoAtlas, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), Padding, TamanoAtlas, false);
        atlas.Apply();
        foreach (var textura in texturas) Object.DestroyImmediate(textura);

        string rutaAbsoluta = Path.Combine(Application.dataPath,
            "Models/StylizedNature/Billboards/T_Arboles_Atlas.png");
        File.WriteAllBytes(rutaAbsoluta, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);

        AssetDatabase.ImportAsset(RutaAtlas, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(RutaAtlas);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = TamanoAtlas;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        var texturaAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaAtlas);
        var baseHojas = familias[nombresFamilias[0]][1];
        var materialAtlas = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (materialAtlas == null)
        {
            materialAtlas = new Material(baseHojas);
            materialAtlas.name = "M_Arboles_Atlas";
            AssetDatabase.CreateAsset(materialAtlas, RutaMaterial);
        }

        materialAtlas.shader = baseHojas.shader;
        materialAtlas.CopyPropertiesFromMaterial(baseHojas);
        materialAtlas.mainTexture = texturaAtlas;
        materialAtlas.mainTextureScale = Vector2.one;
        materialAtlas.mainTextureOffset = Vector2.zero;
        materialAtlas.SetColor("_Color", Color.white);
        materialAtlas.SetFloat("_Mode", 1f);
        materialAtlas.SetFloat("_Cutoff", 0.5f);
        materialAtlas.SetInt("_SrcBlend", (int)BlendMode.One);
        materialAtlas.SetInt("_DstBlend", (int)BlendMode.Zero);
        materialAtlas.SetInt("_ZWrite", 1);
        materialAtlas.EnableKeyword("_ALPHATEST_ON");
        materialAtlas.DisableKeyword("_ALPHABLEND_ON");
        materialAtlas.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        materialAtlas.SetOverrideTag("RenderType", "TransparentCutout");
        materialAtlas.renderQueue = 2450;
        EditorUtility.SetDirty(materialAtlas);

        var rectPorClave = new Dictionary<string, Rect>();
        for (int i = 0; i < claves.Count; i++) rectPorClave[claves[i]] = rects[i];

        bool escenaCambiada = false;
        int mallasCreadas = 0;
        foreach (var datos in tipos.Values)
        {
            var mallaTemporal = ConstruirMalla(datos, rectPorClave);
            if (mallaTemporal == null) continue;

            string rutaMalla = CarpetaMallas + "/Tree_" + datos.tipo + "_Atlas.asset";
            var mallaFinal = GuardarMallaEnSitio(mallaTemporal, rutaMalla);
            mallasCreadas++;

            foreach (var renderer in datos.renderers)
            {
                if (renderer == null) continue;

                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != mallaFinal)
                {
                    mf.sharedMesh = mallaFinal;
                    escenaCambiada = true;
                }

                var mats = renderer.sharedMaterials;
                if (mats.Length != 1 || mats[0] != materialAtlas)
                {
                    renderer.sharedMaterials = new[] { materialAtlas };
                    escenaCambiada = true;
                }
            }
        }

        AssetDatabase.SaveAssets();

        if (escenaCambiada)
        {
            var escena = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }

        Debug.Log("[Árboles] Atlas LOD0 reconstruido: familias=" + familias.Count +
                  ", tipos=" + tipos.Count + ", mallas=" + mallasCreadas +
                  ", atlas=" + texturaAtlas.width + "x" + texturaAtlas.height);
    }

    private static Dictionary<string, DatosTipo> RecolectarTipos()
    {
        var tipos = new Dictionary<string, DatosTipo>();

        foreach (var grupo in Object.FindObjectsOfType<LODGroup>(true))
        {
            string nombre = grupo.gameObject.name;
            if (!nombre.EndsWith("_LOD") || !nombre.Contains("Tree")) continue;

            string tipo = nombre.Substring(0, nombre.Length - 4);
            var lods = grupo.GetLODs();
            if (lods.Length == 0 || lods[0].renderers.Length == 0) continue;

            var renderer = lods[0].renderers[0];
            if (renderer == null) continue;

            if (!tipos.TryGetValue(tipo, out var datos))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    CarpetaPrefabs + "/" + tipo + ".prefab");
                if (prefab == null)
                {
                    Debug.LogWarning("[Árboles] No existe el prefab fuente " + tipo + ".prefab");
                    continue;
                }

                var mr = prefab.GetComponentInChildren<MeshRenderer>(true);
                var mf = mr != null ? mr.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterials.Length < 2)
                {
                    Debug.LogWarning("[Árboles] El prefab " + tipo + " no tiene malla y 2 materiales.");
                    continue;
                }

                var corteza = mr.sharedMaterials[0];
                var hojas = mr.sharedMaterials[1];
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
            }

            if (!datos.renderers.Contains(renderer)) datos.renderers.Add(renderer);
        }

        return tipos;
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

        if (v == null || v.Length == 0 || uv == null || uv.Length != v.Length)
        {
            Debug.LogError("[Árboles] Malla sin UVs válidos: " + datos.tipo);
            return null;
        }

        bool hn = n != null && n.Length == v.Length;
        bool ht = t != null && t.Length == v.Length;
        bool huv2 = uv2 != null && uv2.Length == v.Length;
        bool hc = col != null && col.Length == v.Length;

        var nv = new List<Vector3>(v.Length);
        var nn = new List<Vector3>(v.Length);
        var nt = new List<Vector4>(v.Length);
        var nuv = new List<Vector2>(v.Length);
        var nuv2 = new List<Vector2>(v.Length);
        var ncol = new List<Color>(v.Length);
        var tris = new List<int>(v.Length * 3);
        var mapa = new Dictionary<(int, int, int, int), int>();
        int ajustados = 0;

        float inset = InsetTexeles / TamanoAtlas;

        int Agregar(int original, int region, int su, int sv, bool esCorteza, Rect rect)
        {
            var clave = (original, region, su, sv);
            if (mapa.TryGetValue(clave, out int existente)) return existente;

            int indice = nv.Count;
            mapa.Add(clave, indice);
            nv.Add(v[original]);
            if (hn) nn.Add(n[original]);
            if (ht) nt.Add(t[original]);

            Vector2 baseUv = uv[original];
            float u, w;
            if (esCorteza)
            {
                u = (baseUv.x - su) / FactorCorteza;
                w = (baseUv.y - sv) / FactorCorteza;
            }
            else
            {
                u = Mathf.Clamp01(baseUv.x);
                w = Mathf.Clamp01(baseUv.y);
            }

            nuv.Add(new Vector2(rect.x + u * rect.width,
                                rect.y + w * rect.height));

            if (huv2) nuv2.Add(uv2[original]);
            if (hc) ncol.Add(col[original]);
            return indice;
        }

        int AgregarAjustado(int original, Rect rect, float minU, float minV, float escala)
        {
            int indice = nv.Count;
            nv.Add(v[original]);
            if (hn) nn.Add(n[original]);
            if (ht) nt.Add(t[original]);

            Vector2 baseUv = uv[original];
            float u = Mathf.Clamp01((baseUv.x - minU) * escala);
            float w = Mathf.Clamp01((baseUv.y - minV) * escala);

            nuv.Add(new Vector2(rect.x + u * rect.width,
                                rect.y + w * rect.height));

            if (huv2) nuv2.Add(uv2[original]);
            if (hc) ncol.Add(col[original]);
            return indice;
        }

        for (int s = 0; s < malla.subMeshCount && s < 2; s++)
        {
            bool esCorteza = s == 0;
            string clave = datos.familia + (esCorteza ? "_Bark" : "_Leaves");
            if (!rects.TryGetValue(clave, out Rect rectOriginal))
            {
                Debug.LogError("[Árboles] Sin región de atlas: " + clave);
                return null;
            }

            var rect = new Rect(
                rectOriginal.x + inset,
                rectOriginal.y + inset,
                rectOriginal.width - 2f * inset,
                rectOriginal.height - 2f * inset);

            int[] indices = malla.GetTriangles(s);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];

                int su = 0;
                int sv = 0;

                if (esCorteza)
                {
                    float minU = Mathf.Min(uv[a].x, Mathf.Min(uv[b].x, uv[c].x));
                    float minV = Mathf.Min(uv[a].y, Mathf.Min(uv[b].y, uv[c].y));
                    float maxU = Mathf.Max(uv[a].x, Mathf.Max(uv[b].x, uv[c].x));
                    float maxV = Mathf.Max(uv[a].y, Mathf.Max(uv[b].y, uv[c].y));
                    float spanU = maxU - minU;
                    float spanV = maxV - minV;

                    if (spanU < FactorCorteza && spanV < FactorCorteza)
                    {
                        su = Mathf.FloorToInt(minU);
                        sv = Mathf.FloorToInt(minV);
                    }
                    else
                    {
                        // Triángulo que abarca más de FactorCorteza tiles (solo
                        // algunos de Normal). Se ajusta uniformemente a la celda;
                        // el resto de triángulos conserva su escala original.
                        float escala = 1f / Mathf.Max(spanU, spanV);
                        int ia = AgregarAjustado(a, rect, minU, minV, escala);
                        int ib = AgregarAjustado(b, rect, minU, minV, escala);
                        int ic = AgregarAjustado(c, rect, minU, minV, escala);
                        tris.Add(ia);
                        tris.Add(ib);
                        tris.Add(ic);
                        ajustados++;
                        continue;
                    }
                }

                int na = Agregar(a, s, su, sv, esCorteza, rect);
                int nb = Agregar(b, s, su, sv, esCorteza, rect);
                int nc = Agregar(c, s, su, sv, esCorteza, rect);
                tris.Add(na);
                tris.Add(nb);
                tris.Add(nc);
            }
        }

        if (ajustados > 0)
        {
            Debug.Log("[Árboles] " + datos.tipo + ": " + ajustados +
                      " triángulos de corteza ajustados (span > " +
                      FactorCorteza + " tiles).");
        }

        var nueva = new Mesh();
        nueva.name = "Tree_" + datos.tipo + "_Atlas";
        if (nv.Count > 65535) nueva.indexFormat = IndexFormat.UInt32;
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

    private static Mesh GuardarMallaEnSitio(Mesh nueva, string ruta)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente == null)
        {
            AssetDatabase.CreateAsset(nueva, ruta);
            return nueva;
        }

        CopiarMalla(existente, nueva);
        EditorUtility.SetDirty(existente);
        Object.DestroyImmediate(nueva);
        return existente;
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

    /// <summary>
    /// Celda de corteza: FactorCorteza x FactorCorteza repeticiones de la textura
    /// con alfa opaco. La corteza original es un material opaco; algunas texturas
    /// (Normal) traen alfa intermedio y el material atlas es cutout.
    /// </summary>
    private static Texture2D CrearCeldaCorteza(Texture2D origen)
    {
        int tile = TamanoCelda / FactorCorteza;
        var mini = Copiar(origen, tile, "tmp_corteza_mini");
        var pixeles = mini.GetPixels32();
        var destino = new Color32[TamanoCelda * TamanoCelda];

        for (int y = 0; y < TamanoCelda; y++)
        {
            int filaOrigen = (y % tile) * tile;
            int filaDestino = y * TamanoCelda;
            for (int x = 0; x < TamanoCelda; x++)
            {
                var color = pixeles[filaOrigen + (x % tile)];
                color.a = 255;
                destino[filaDestino + x] = color;
            }
        }

        var textura = new Texture2D(TamanoCelda, TamanoCelda, TextureFormat.RGBA32, false);
        textura.SetPixels32(destino);
        textura.Apply();
        textura.name = "tmp_corteza";
        Object.DestroyImmediate(mini);
        return textura;
    }

    private static Texture2D CrearCeldaHojas(Texture2D origen)
    {
        return Copiar(origen, TamanoCelda, "tmp_hojas");
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
