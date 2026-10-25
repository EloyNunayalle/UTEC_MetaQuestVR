using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Reduce la pasada de sombras: apaga casters en vegetación baja, vidrio/fake,
/// props combinados, cerco, mecanismos de la estación y detalles pequeños.
/// Mantiene sombra en edificios, árboles LOD0 y estructura principal de la estación.
/// </summary>
public static class OptimizarSombras
{
    private static readonly HashSet<string> MaterialesSinSombra = new HashSet<string>
    {
        "M_Flowers", "M_Cesped", "M_Bush_Leaves", "M_Rock",
        "M_Glass_Transparente", "M_Detalle_BVerdeFake_Doble",
        "Translucent_Glass_Gray", "Light", "Hose",
        "Metal_Gris", "Metal_Cromado", "Boton_Rojo", "Color_M05",
        "MI_Trim_MetalConcrete", "MI_Asphalt", "MI_Dirt", "MI_StreetDecals",
        "MI_Ornaments", "M_Suelo_Ciudad", "Sidewalk01", "Standard",
        "M_Billboard_Atlas", "Muro_Ladrillo"
    };

    [MenuItem("UTEC/Optimizacion/3 Reducir sombras")]
    public static void Ejecutar()
    {
        if (EditorApplication.isPlaying) return;

        int apagados = 0;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (r.shadowCastingMode == ShadowCastingMode.Off) continue;

            var t = r.transform;
            while (t.parent != null) t = t.parent;
            string root = t.name;

            bool off = root == "Props_Combinados" ||
                       root == "Cerco_Combinado" ||
                       root == "Cerco_Patio" ||
                       (root == "estacion_bomberos_editada" && !r.gameObject.isStatic) ||
                       r.name == "LOD1_Decimado";

            foreach (var m in r.sharedMaterials)
                if (m != null && MaterialesSinSombra.Contains(m.name)) { off = true; break; }

            if (!off) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            apagados++;
        }

        // Las pruebas de instancing no mejoraron; volver a dejar los materiales como estaban.
        foreach (var ruta in new[]
                 {
                     "Assets/Models/StylizedNature/Billboards/M_Arboles_Atlas.mat",
                     "Assets/Models/StylizedNature/Billboards/M_Billboard_Atlas.mat",
                     "Assets/Models/StylizedNature/Materials/M_Bush_Leaves.mat",
                     "Assets/Models/StylizedNature/Materials/M_Rock.mat"
                 })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m != null) { m.enableInstancing = false; EditorUtility.SetDirty(m); }
        }

        AssetDatabase.SaveAssets();
        var escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Sombras] casters apagados=" + apagados);
    }
}
