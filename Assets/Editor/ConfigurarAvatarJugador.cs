using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambia el AvatarJugador de Meta Avatars entre cuerpo completo (con piernas)
/// y cuerpo superior. El recorte real de piernas se hace en AvatarJugador.
/// </summary>
public static class ConfigurarAvatarJugador
{
    private const string Nombre = "AvatarJugador";

    [MenuItem("UTEC/Avatar/Checkpoint con piernas (mujer)")]
    public static void ConPiernas()
    {
        Configurar(true);
    }

    [MenuItem("UTEC/Avatar/Manos y torso (sin piernas)")]
    public static void SinPiernas()
    {
        Configurar(false);
    }

    private static void Configurar(bool piernas)
    {
        var avatar = GameObject.Find(Nombre);
        if (avatar == null)
        {
            Debug.LogWarning("[Avatar] Abre la escena Persistente; no se encontró " + Nombre + ".");
            return;
        }

        var componente = avatar.GetComponent<AvatarJugador>();
        if (componente == null)
        {
            Debug.LogWarning("[Avatar] " + Nombre + " no tiene el componente AvatarJugador.");
            return;
        }

        var so = new SerializedObject(componente);

        var features = so.FindProperty("_creationInfo.features");
        if (features != null) features.intValue = 382;

        var renderFilters = so.FindProperty("_creationInfo.renderFilters.manifestationFlags");
        if (renderFilters != null) renderFilters.intValue = 3;

        var quality = so.FindProperty("_creationInfo.renderFilters.quality");
        if (quality != null) quality.intValue = 1;

        // Sin piernas: usar primitivos (Half) en vez del LOD combinado completo.
        var useRenderLods = so.FindProperty("useRenderLods");
        if (useRenderLods != null) useRenderLods.boolValue = true;

        var activeView = so.FindProperty("_activeView");
        if (activeView != null) activeView.intValue = piernas ? 2 : 1; // Third / First

        var activeManifestation = so.FindProperty("_activeManifestation");
        if (activeManifestation != null) activeManifestation.intValue = piernas ? 1 : 2;

        var activeSubMeshes = so.FindProperty("_activeSubMeshes");
        if (activeSubMeshes != null) activeSubMeshes.intValue = -1;

        var includeUntyped = so.FindProperty("_activeSubMeshesIncludeUntyped");
        if (includeUntyped != null) includeUntyped.boolValue = true;

        var mostrarPiernas = so.FindProperty("mostrarPiernas");
        if (mostrarPiernas != null) mostrarPiernas.boolValue = piernas;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(componente);

        var escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Avatar] AvatarJugador: piernas=" + piernas);
    }
}
