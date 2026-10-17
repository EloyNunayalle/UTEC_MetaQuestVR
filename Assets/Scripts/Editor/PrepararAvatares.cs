using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prepara los zips de avatares preset (Light) para el runtime:
/// los copia desde Assets/Oculus/Avatar2_SampleAssets a Assets/StreamingAssets/Oculus
/// con el nombre que espera OvrAvatarManager (PresetAvatars_Rift/Quest.zip).
/// Uso: menu "Juego Bomberos > Preparar avatares (copiar presets)".
/// (Los zips NO van al repositorio: son grandes y se regeneran con este menu.)
/// </summary>
public static class PrepararAvatares
{
    const string Origen = "Assets/Oculus/Avatar2_SampleAssets/SampleAssets/SampleAssets";
    const string Destino = "Assets/StreamingAssets";

    [MenuItem("Juego Bomberos/Preparar avatares (copiar presets)")]
    public static void Copiar()
    {
        Directory.CreateDirectory(Destino);

        bool ok1 = CopiarUno("PresetAvatars_Rift_Light.zip", "PresetAvatars_Rift_Light.zip");
        bool ok2 = CopiarUno("PresetAvatars_Quest_Light.zip", "PresetAvatars_Quest_Light.zip");

        if (ok1 && ok2)
        {
            AssetDatabase.Refresh();
            Debug.Log("[PrepararAvatares] Presets copiados correctamente a " + Destino);
            EditorUtility.DisplayDialog("Avatares", "Presets copiados correctamente.", "OK");
        }
        else
        {
            Debug.LogError("[PrepararAvatares] No se encontraron los zips originales en " + Origen +
                           ". Instala primero el Meta Avatars SDK (Sample Assets) desde la Asset Store.");
            EditorUtility.DisplayDialog("Avatares",
                "No se encontraron los zips.\n\nInstala primero 'Meta Avatars SDK' y 'Sample Assets' desde la Asset Store (Window > Package Manager > My Assets).",
                "OK");
        }
    }

    static bool CopiarUno(string origenNombre, string destinoNombre)
    {
        string rutaOrigen = Origen + "/" + origenNombre;
        string rutaDestino = Destino + "/" + destinoNombre;
        if (!File.Exists(rutaOrigen)) return false;
        if (File.Exists(rutaDestino) && new FileInfo(rutaDestino).Length == new FileInfo(rutaOrigen).Length) return true;
        File.Copy(rutaOrigen, rutaDestino, true);
        return true;
    }
}
