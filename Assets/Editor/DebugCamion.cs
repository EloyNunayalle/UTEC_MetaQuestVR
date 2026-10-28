using UnityEditor;
using UnityEngine;

/// <summary>
/// Pruebas manuales del camion en Play. Solo editor; no va al build.
/// </summary>
public static class DebugCamion
{
    static CamionBomberos Buscar()
    {
        var camion = Object.FindObjectOfType<CamionBomberos>();
        if (camion == null) Debug.LogWarning("[DebugCamion] No hay CamionBomberos en la escena.");
        return camion;
    }

    [MenuItem("UTEC/Debug/Camion/Subir")]
    public static void Subir()
    {
        var c = Buscar();
        if (c == null) return;
        c.Subir();
        Debug.Log("[DebugCamion] Subir; sentado=" + c.JugadorSentado +
                  " luces=" + c.LucesEncendidas + " sirena=" + c.SirenaEncendida);
    }

    [MenuItem("UTEC/Debug/Camion/Bajar")]
    public static void Bajar()
    {
        var c = Buscar();
        if (c == null) return;
        c.Bajar();
        Debug.Log("[DebugCamion] Bajar; sentado=" + c.JugadorSentado);
    }

    [MenuItem("UTEC/Debug/Camion/Sirena ON-OFF")]
    public static void Sirena()
    {
        var c = Buscar();
        if (c == null) return;
        c.AlternarSirena();
        Debug.Log("[DebugCamion] Sirena=" + c.SirenaEncendida);
    }

    [MenuItem("UTEC/Debug/Camion/Luces ON-OFF")]
    public static void Luces()
    {
        var c = Buscar();
        if (c == null) return;
        c.AlternarLuces();
        Debug.Log("[DebugCamion] Luces=" + c.LucesEncendidas);
    }
}
