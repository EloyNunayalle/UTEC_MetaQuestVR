using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configura las texturas y materiales del equipo AC de la Misión 2.
/// Las texturas son locales (licencia CGTrader); los materiales sí se versionan.
/// </summary>
public static class ConfigurarEquipoAC
{
    const string Carpeta = "Assets/Models/EquipoIncendio";
    const string RutaFbx = Carpeta + "/Ac_Units.fbx";
    const string CarpetaMateriales = Carpeta + "/Materials";

    [MenuItem("UTEC/Debug/Configurar equipo AC")]
    public static void Configurar()
    {
        ConfigurarTexturas();
        AssetDatabase.Refresh();

        var matAc1 = CrearMaterial("M_AC1", "AC_M_AC1");
        var matAc2 = CrearMaterial("M_AC2", "AC_M_AC2");
        var matDetalle = CrearDetalle();

        RemapearMateriales(matAc1, matAc2, matDetalle);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AC] Materiales configurados: M_AC1, M_AC2 y M_AC_Detalle.");
    }

    static void ConfigurarTexturas()
    {
        string[] texturas =
        {
            "AC_M_AC1_BaseColor", "AC_M_AC1_Normal", "AC_M_AC1_Metallic", "AC_M_AC1_Roughness", "AC_M_AC1_Height",
            "AC_M_AC2_BaseColor", "AC_M_AC2_Normal", "AC_M_AC2_Metallic", "AC_M_AC2_Roughness", "AC_M_AC2_Height"
        };

        foreach (var nombre in texturas)
        {
            string ruta = $"{Carpeta}/Textures/{nombre}.png";
            var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[AC] No se encontró la textura " + ruta);
                continue;
            }

            bool normal = nombre.Contains("_Normal");
            bool baseColor = nombre.Contains("_BaseColor");

            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = baseColor;
            importer.maxTextureSize = normal ? 1024 : (baseColor ? 1024 : 512);
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    static Material CrearMaterial(string nombre, string prefijoTextura)
    {
        string ruta = $"{CarpetaMateriales}/{nombre}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory(CarpetaMateriales);
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, ruta);
        }

        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Carpeta}/Textures/{prefijoTextura}_BaseColor.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Carpeta}/Textures/{prefijoTextura}_Normal.png");

        mat.shader = Shader.Find("Standard");
        mat.SetColor("_Color", Color.white);
        mat.SetTexture("_MainTex", baseColor);
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_Metallic", 0.25f);
        mat.SetFloat("_Glossiness", 0.35f);
        if (normal != null)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 0.8f);
        }
        else
        {
            mat.DisableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CrearDetalle()
    {
        string ruta = $"{CarpetaMateriales}/M_AC_Detalle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory(CarpetaMateriales);
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, ruta);
        }

        mat.shader = Shader.Find("Standard");
        mat.SetColor("_Color", new Color(0.16f, 0.18f, 0.20f));
        mat.SetFloat("_Metallic", 0.45f);
        mat.SetFloat("_Glossiness", 0.25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void RemapearMateriales(Material ac1, Material ac2, Material detalle)
    {
        var importer = AssetImporter.GetAtPath(RutaFbx) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[AC] No se pudo cargar el ModelImporter de " + RutaFbx);
            return;
        }

        var materiales = AssetDatabase.LoadAllAssetsAtPath(RutaFbx).OfType<Material>().ToList();
        if (materiales.Count == 0)
        {
            Debug.LogWarning("[AC] El FBX no tiene materiales importados.");
            return;
        }

        var usados = new HashSet<string>();
        foreach (var material in materiales)
        {
            if (material == null || string.IsNullOrEmpty(material.name)) continue;
            if (!usados.Add(material.name)) continue;

            Material destino;
            if (material.name.Contains("AC1")) destino = ac1;
            else if (material.name.Contains("AC2")) destino = ac2;
            else destino = detalle;

            try
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), destino);
                Debug.Log($"[AC] Remap {material.name} -> {destino.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AC] No se pudo remapear {material.name}: {e.Message}");
            }
        }

        importer.SaveAndReimport();
    }
}
