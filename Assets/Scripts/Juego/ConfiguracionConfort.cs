using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Opcion de confort de Meta: la "viñeta" (tunneling) del building block de locomocion, que oscurece
/// los bordes de la vista mientras el jugador se desplaza o gira con el joystick para reducir el mareo.
/// El rig ya trae los objetos "SmoothMovementTunneling" (LocomotionTunneling + TunnelingEffect); esta
/// clase solo los activa o desactiva. Se guarda en PlayerPrefs (por defecto: activada).
/// </summary>
public static class ConfiguracionConfort
{
    const string Clave = "UTEC.VinetaConfort";

    public static bool Vineta
    {
        get => PlayerPrefs.GetInt(Clave, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(Clave, value ? 1 : 0);
            PlayerPrefs.Save();
            Aplicar();
        }
    }

    public static string Nombre => Vineta ? "Sí" : "No";

    /// <summary>Activa/desactiva la viñeta en todos los locomotores de la escena (incluidos los inactivos).</summary>
    public static void Aplicar()
    {
        bool activa = Vineta;
        foreach (var t in Resources.FindObjectsOfTypeAll<LocomotionTunneling>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue; // ignora prefabs/assets
            if (t.gameObject.activeSelf != activa) t.gameObject.SetActive(activa);
        }
    }
}
