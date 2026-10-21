using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera materiales de paleta para personalizar todas las partes de los edificios.
/// NO crea prefabs y NO aplica nada a la escena.
/// </summary>
public static class ConstructorPaletaPartes
{
    private const string Raiz = "Assets/Models/CiudadNueva/PaletaPartes";

    private class ColorPaleta
    {
        public string nombre;
        public Color color;
        public ColorPaleta(string n, Color c) { nombre = n; color = c; }
    }

    [MenuItem("UTEC/Paleta/Generar materiales de partes")]
    public static void Generar()
    {
        if (!AssetDatabase.IsValidFolder(Raiz)) AssetDatabase.CreateFolder("Assets/Models/CiudadNueva", "PaletaPartes");

        var colores = new List<ColorPaleta>
        {
            new ColorPaleta("Blanco", new Color(1.00f,1.00f,1.00f)),
            new ColorPaleta("Crema", new Color(0.95f,0.90f,0.75f)),
            new ColorPaleta("Arena", new Color(0.82f,0.72f,0.55f)),
            new ColorPaleta("Beige", new Color(0.78f,0.70f,0.55f)),
            new ColorPaleta("GrisClaro", new Color(0.72f,0.72f,0.72f)),
            new ColorPaleta("Gris", new Color(0.50f,0.50f,0.50f)),
            new ColorPaleta("Marron", new Color(0.35f,0.22f,0.15f)),
            new ColorPaleta("Ocre", new Color(0.65f,0.50f,0.20f)),
            new ColorPaleta("Terracota", new Color(0.62f,0.30f,0.20f)),
            new ColorPaleta("RojoOscuro", new Color(0.45f,0.12f,0.10f)),
            new ColorPaleta("VerdeOscuro", new Color(0.25f,0.35f,0.28f)),
            new ColorPaleta("VerdeClaro", new Color(0.45f,0.55f,0.38f)),
            new ColorPaleta("AzulGris", new Color(0.28f,0.36f,0.45f)),
            new ColorPaleta("Negro", new Color(0.08f,0.08f,0.08f))
        };

        var trim = Textura("T_Trim_BaseColor");
        var roof = Textura("T_RoofSlate_BaseColor");
        var metal = Textura("T_MetalConcrete_BaseColor");
        var concreto = Textura("T_Concrete_BaseColor");
        var ladrillo = Textura("T_RedBrick_BaseColor");
        var marmol = Textura("T_MarbleFloor_BaseColor");
        var brickGris = Textura("T_Brick_Gris");
        var lit1 = Textura("T_lit_interior_1");
        var lit2 = Textura("T_lit_interior_2");
        var dark = Textura("T_dark_interior");

        int creados = 0;
        creados += Grupo("Trim", "M_Parte_Trim_", trim, colores, 0.15f, 0f);
        creados += Grupo("Cornice", "M_Parte_Cornice_", trim, colores, 0.15f, 0f);
        creados += Grupo("Frame", "M_Parte_Frame_", trim, colores, 0.15f, 0f);
        creados += Grupo("Roof", "M_Parte_Roof_", roof, colores, 0.25f, 0f);
        creados += Grupo("Door", "M_Parte_Door_", metal, colores, 0.30f, 0.10f);
        creados += Grupo("Foundation", "M_Parte_Foundation_", concreto, colores, 0.10f, 0f);
        creados += Grupo("Stairs", "M_Parte_Stairs_", concreto, colores, 0.10f, 0f);
        creados += Grupo("InteriorWall", "M_Parte_InteriorWall_", ladrillo, colores, 0.10f, 0f);
        creados += Grupo("InteriorFloor", "M_Parte_InteriorFloor_", marmol, colores, 0.10f, 0f);

        // Ladrillos extra (los 8 originales ya existen como M_Brick_*).
        var extraBrick = new List<ColorPaleta>
        {
            new ColorPaleta("Beige", new Color(0.78f,0.70f,0.55f)),
            new ColorPaleta("Terracota", new Color(0.62f,0.30f,0.20f)),
            new ColorPaleta("VerdeClaro", new Color(0.45f,0.55f,0.38f)),
            new ColorPaleta("AzulGris", new Color(0.28f,0.36f,0.45f))
        };
        creados += Grupo("BrickExtra", "M_Parte_Brick_", brickGris, extraBrick, 0.15f, 0f);

        // Fake interior: variantes de iluminación/tinte.
        var fakes = new[]
        {
            new { n="Neutral", t=lit1, c=new Color(1f,1f,1f) },
            new { n="Calido", t=lit1, c=new Color(1f,0.82f,0.65f) },
            new { n="Frio", t=lit2, c=new Color(0.75f,0.85f,1f) },
            new { n="Oscuro", t=dark, c=new Color(0.60f,0.60f,0.60f) },
            new { n="Tenue", t=lit2, c=new Color(0.60f,0.60f,0.60f) }
        };
        foreach (var f in fakes) { CrearMaterial("Fake", "M_Parte_Fake_" + f.n, f.t, f.c, 0.05f, 0f); creados++; }

        // Vidrio: variantes transparentes.
        var vidrios = new[]
        {
            new { n="Clear", c=new Color(0.25f,0.25f,0.25f,0.59f) },
            new { n="Azul", c=new Color(0.12f,0.22f,0.38f,0.55f) },
            new { n="Verde", c=new Color(0.12f,0.30f,0.22f,0.55f) },
            new { n="Ahumado", c=new Color(0.06f,0.06f,0.08f,0.72f) },
            new { n="Bronce", c=new Color(0.35f,0.25f,0.12f,0.60f) },
            new { n="Lechoso", c=new Color(0.80f,0.80f,0.80f,0.70f) }
        };
        foreach (var v in vidrios) { CrearVidrio("M_Parte_Glass_" + v.n, v.c); creados++; }

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EscribirCatalogo(colores, extraBrick, fakes.Length, vidrios.Length);
        Debug.Log("[PaletaPartes] materiales generados=" + creados);
    }

    private static int Grupo(string sub, string prefijo, Texture2D tex, List<ColorPaleta> colores, float gloss, float metal)
    {
        foreach (var c in colores) CrearMaterial(sub, prefijo + c.nombre, tex, c.color, gloss, metal);
        return colores.Count;
    }

    private static void CrearMaterial(string sub, string nombre, Texture2D tex, Color color, float gloss, float metal)
    {
        string carpeta = Raiz + "/" + sub;
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(Raiz, sub);
        string ruta = carpeta + "/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")); mat.name = nombre;
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.shader = Shader.Find("Standard");
        mat.mainTexture = tex;
        mat.SetColor("_Color", color);
        mat.SetFloat("_Glossiness", gloss);
        mat.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(mat);
    }

    private static void CrearVidrio(string nombre, Color color)
    {
        string carpeta = Raiz + "/Glass";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(Raiz, "Glass");
        string ruta = carpeta + "/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard")); mat.name = nombre;
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.shader = Shader.Find("Standard");
        mat.SetColor("_Color", color);
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.SetFloat("_Glossiness", 0.65f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
    }

    private static Texture2D Textura(string nombre)
    {
        foreach (var g in AssetDatabase.FindAssets(nombre + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (t != null && t.name == nombre) return t;
        }
        return null;
    }

    private static void EscribirCatalogo(List<ColorPaleta> colores, List<ColorPaleta> extraBrick, int fakes, int vidrios)
    {
        string ruta = "PALETA_PARTES_EDIFICIOS.md";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Paleta de partes de edificios");
        sb.AppendLine();
        sb.AppendLine("Materiales generados para personalizar por partes. **No están aplicados a la escena ni a prefabs.**");
        sb.AppendLine();
        sb.AppendLine("## Grupos generados");
        sb.AppendLine();
        sb.AppendLine("| Grupo | Carpeta | Textura base |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine("| Trim | `PaletaPartes/Trim/` | T_Trim_BaseColor |");
        sb.AppendLine("| Cornice | `PaletaPartes/Cornice/` | T_Trim_BaseColor |");
        sb.AppendLine("| Frame | `PaletaPartes/Frame/` | T_Trim_BaseColor |");
        sb.AppendLine("| Roof | `PaletaPartes/Roof/` | T_RoofSlate_BaseColor |");
        sb.AppendLine("| Door | `PaletaPartes/Door/` | T_MetalConcrete_BaseColor |");
        sb.AppendLine("| Foundation | `PaletaPartes/Foundation/` | T_Concrete_BaseColor |");
        sb.AppendLine("| Stairs | `PaletaPartes/Stairs/` | T_Concrete_BaseColor |");
        sb.AppendLine("| InteriorWall | `PaletaPartes/InteriorWall/` | T_RedBrick_BaseColor |");
        sb.AppendLine("| InteriorFloor | `PaletaPartes/InteriorFloor/` | T_MarbleFloor_BaseColor |");
        sb.AppendLine("| Fake | `PaletaPartes/Fake/` | T_lit_interior_1/2, T_dark_interior |");
        sb.AppendLine("| Glass | `PaletaPartes/Glass/` | sin textura |");
        sb.AppendLine("| BrickExtra | `PaletaPartes/BrickExtra/` | T_Brick_Gris |");
        sb.AppendLine();
        sb.AppendLine("## Colores base");
        sb.AppendLine();
        sb.AppendLine("Blanco, Crema, Arena, Beige, GrisClaro, Gris, Marron, Ocre, Terracota, RojoOscuro, VerdeOscuro, VerdeClaro, AzulGris, Negro.");
        sb.AppendLine();
        sb.AppendLine("Los 8 ladrillos originales siguen en `Assets/Models/CiudadNueva/Materials/M_Brick_*`.");
        sb.AppendLine();
        sb.AppendLine("## Fake");
        sb.AppendLine();
        sb.AppendLine("Neutral, Calido, Frio, Oscuro, Tenue.");
        sb.AppendLine();
        sb.AppendLine("## Vidrio");
        sb.AppendLine();
        sb.AppendLine("Clear, Azul, Verde, Ahumado, Bronce, Lechoso.");
        sb.AppendLine();
        sb.AppendLine("## Flujo");
        sb.AppendLine();
        sb.AppendLine("1. Asignar estos materiales a los submeshes/materiales separados del edificio editable.");
        sb.AppendLine("2. Personalizar los colores/texturas por edificio.");
        sb.AppendLine("3. Cuando esté definido, hornear el resultado en atlas (opaco + vidrio; fake aparte si requiere propiedades).");
        sb.AppendLine("4. El atlas final queda fijo; conservar la versión editable para futuros cambios.");
        File.WriteAllText(ruta, sb.ToString());
    }
}
