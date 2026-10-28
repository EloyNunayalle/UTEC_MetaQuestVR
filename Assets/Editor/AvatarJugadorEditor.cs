using UnityEditor;
using UnityEngine;

/// <summary>
/// Muestra los 33 presets locales de Meta Avatars con etiqueta legible.
/// El paquete no trae nombres de display, así que 0/1 son Mujer/Hombre y
/// el resto Avatar 2..32; se pueden renombrar en AvatarJugador.NombresPresets.
/// </summary>
[CustomEditor(typeof(AvatarJugador))]
public class AvatarJugadorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var avatar = (AvatarJugador)target;
        serializedObject.Update();

        var preset = serializedObject.FindProperty("presetAvatar");
        if (preset != null)
        {
            int actual = Mathf.Clamp(preset.intValue, 0, AvatarJugador.NombresPresets.Length - 1);
            int elegido = EditorGUILayout.Popup("Avatar (preset)", actual, AvatarJugador.NombresPresets);
            if (elegido != preset.intValue)
                preset.intValue = elegido;
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "presetAvatar");
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            EditorGUILayout.HelpBox(
                "El preset se aplica al cargar el avatar. Si lo cambias en Play, vuelve a entrar a Play para recargarlo.",
                MessageType.Info);
    }
}
