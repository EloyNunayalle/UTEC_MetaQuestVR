using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Empaqueta las texturas de los billboards de árboles en un solo atlas y
/// reasigna las 26 instancias a un único material. Deja intactos los materiales
/// antiguos para la escena de respaldo B.
/// </summary>
public static class ConstructorAtlasArboles
{
    private const string Carpeta = "Assets/Models/StylizedNature/Billboards";
    private const int TamanoAtlas = 1024;

    [MenuItem("UTEC/Árboles/1 Atlas de billboards")]
    public static void ConstruirBillboards()
    {
        if (EditorApplication.isPlaying) return;

        var tipos = new Dictionary<string, Material>();
        var raices = new List<LODGroup>();

        foreach (var grupo in Object.FindObjectsOfType<LODGroup>(true))
        {
            string nombre = grupo.gameObject.name;
            if (!(nombre.Contains("Tree") || nombre.Contains("Maple") || nombre.Contains("Pine")))
                continue;

            raices.Add(grupo);
            string tipo = nombre.Replace("_LOD", "");
            if (tipos.ContainsKey(tipo)) continue;

            Transform billboard = null;
            foreach (Transform hijo in grupo.transform)
                if (hijo.name == "Billboard") { billboard = hijo; break; }
            if (billboard == null) continue;

            var renderer = billboard.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) continue;
            tipos[tipo] = renderer.sharedMaterial;
        }

        if (tipos.Count == 0)
        {
            Debug.LogWarning("[Árboles] No se encontraron billboards.");
            return;
        }

        var nombres = new List<string>();
        var texturas = new List<Texture2D>();
        foreach (var par in tipos)
        {
            var origen = par.Value.mainTexture as Texture2D;
            if (origen == null) continue;
            nombres.Add(par.Key);
            texturas.Add(Copiar(origen, 256, "BB_" + par.Key));
        }

        var atlas = new Texture2D(TamanoAtlas, TamanoAtlas, TextureFormat.RGBA32, true);
        var rects = atlas.PackTextures(texturas.ToArray(), 4, TamanoAtlas, false);
        atlas.Apply();

        string abs = Path.Combine(Application.dataPath, "Models/StylizedNature/Billboards/T_Billboard_Atlas.png");
        File.WriteAllBytes(abs, atlas.EncodeToPNG());
        string rel = Carpeta + "/T_Billboard_Atlas.png";
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
        string rutaMaterial = Carpeta + "/M_Billboard_Atlas.mat";
        var materialAtlas = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (materialAtlas == null)
        {
            materialAtlas = new Material(Shader.Find("Unlit/Transparent Cutout"));
            materialAtlas.name = "M_Billboard_Atlas";
            AssetDatabase.CreateAsset(materialAtlas, rutaMaterial);
        }
        materialAtlas.shader = Shader.Find("Unlit/Transparent Cutout");
        materialAtlas.mainTexture = texturaAtlas;
        materialAtlas.SetFloat("_Cutoff", 0.5f);
        EditorUtility.SetDirty(materialAtlas);

        string carpetaMallas = Carpeta + "/Atlas";
        if (!AssetDatabase.IsValidFolder(carpetaMallas))
            AssetDatabase.CreateFolder(Carpeta, "Atlas");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Models/StylizedNature/Billboards/Atlas"));

        var rectPorTipo = new Dictionary<string, Rect>();
        for (int i = 0; i < nombres.Count; i++)
            rectPorTipo[nombres[i]] = rects[i];

        var mallaBase = AssetDatabase.LoadAssetAtPath<Mesh>(Carpeta + "/Billboard_Cross.asset");
        var mallaPorTipo = new Dictionary<string, Mesh>();
        foreach (var par in rectPorTipo)
        {
            string tipo = par.Key;
            Rect rect = par.Value;
            var copia = Object.Instantiate(mallaBase);
            copia.name = "Billboard_" + tipo + "_Atlas";
            var uv = copia.uv;
            for (int i = 0; i < uv.Length; i++)
                uv[i] = new Vector2(rect.x + uv[i].x * rect.width,
                                    rect.y + uv[i].y * rect.height);
            copia.uv = uv;
            string ruta = AssetDatabase.GenerateUniqueAssetPath(carpetaMallas + "/Billboard_" + tipo + "_Atlas.asset");
            AssetDatabase.CreateAsset(copia, ruta);
            mallaPorTipo[tipo] = copia;
        }

        int asignados = 0;
        foreach (var grupo in raices)
        {
            string tipo = grupo.gameObject.name.Replace("_LOD", "");
            Transform billboard = null;
            foreach (Transform hijo in grupo.transform)
                if (hijo.name == "Billboard") { billboard = hijo; break; }
            if (billboard == null) continue;

            var renderer = billboard.GetComponent<Renderer>();
            if (renderer == null || !mallaPorTipo.ContainsKey(tipo)) continue;

            renderer.GetComponent<MeshFilter>().sharedMesh = mallaPorTipo[tipo];
            renderer.sharedMaterial = materialAtlas;
            asignados++;
        }

        AssetDatabase.SaveAssets();
        var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log($"[Árboles] Atlas billboards: tipos={tipos.Count}, atlas={atlas.width}x{atlas.height}, asignados={asignados}");
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
