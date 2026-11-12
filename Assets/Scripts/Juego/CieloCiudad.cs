using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Atmosfera de la ciudad (atardecer costero). Vive en Persistente y aplica el mismo cielo, sol,
/// luz ambiente y niebla sin importar que escena este activa: la escena aditiva del tile trae la luz
/// direccional, pero el RenderSettings activo es el de Persistente.
///
/// Se aplica al iniciar y cada vez que se carga la ciudad (no cada frame), para no pisar la niebla
/// temporal que usa IncendioCasa dentro de la casa de la Mision 3.
/// </summary>
public class CieloCiudad : MonoBehaviour
{
    [Header("Cielo")]
    public Material cielo;

    [Header("Sol (direccion = hacia donde apunta la luz)")]
    [Tooltip("Elevacion del sol sobre el horizonte (grados).")]
    public float elevacionSol = 30f;
    [Tooltip("Azimut del sol (grados, 0 = +Z/norte, 90 = +X). 300: sobre el mar, al noroeste.")]
    public float azimutSol = 300f;
    public Color colorSol = new Color(1f, 0.86f, 0.70f);
    public float intensidadSol = 1.2f;
    public LightShadows sombras = LightShadows.Soft;
    [Range(0f, 1f)] public float fuerzaSombra = 0.7f;

    [Header("Ambiente (tri-color, sin GI en tiempo real)")]
    public Color ambienteCielo = new Color(0.66f, 0.74f, 0.88f);
    public Color ambienteHorizonte = new Color(0.78f, 0.72f, 0.66f);
    public Color ambienteSuelo = new Color(0.40f, 0.37f, 0.34f);

    [Header("Niebla lineal (mismo color que el horizonte del cielo)")]
    public bool niebla = true;
    public Color colorNiebla = new Color(0.93f, 0.80f, 0.66f);
    public float nieblaInicio = 70f;
    public float nieblaFin = 420f;

    void Awake()
    {
        Aplicar();
        SceneManager.sceneLoaded += AlCargar;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= AlCargar;

    void AlCargar(Scene s, LoadSceneMode modo)
    {
        // Solo la ciudad o la persistente: los interiores no tocan la atmosfera.
        if (s.name == GestorEscenas.EscenaMundo || s.name == GestorEscenas.EscenaPersistente) Aplicar();
    }

    public void Aplicar()
    {
        if (cielo != null) RenderSettings.skybox = cielo;

        var sol = BuscarSol();
        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(elevacionSol, azimutSol + 180f, 0f);
            sol.color = colorSol;
            sol.intensity = intensidadSol;
            sol.shadows = sombras;
            sol.shadowStrength = fuerzaSombra;
            RenderSettings.sun = sol;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambienteCielo;
        RenderSettings.ambientEquatorColor = ambienteHorizonte;
        RenderSettings.ambientGroundColor = ambienteSuelo;

        RenderSettings.fog = niebla;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = colorNiebla;
        RenderSettings.fogStartDistance = nieblaInicio;
        RenderSettings.fogEndDistance = nieblaFin;
    }

    static Light BuscarSol()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) return RenderSettings.sun;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional && l.gameObject.scene.name == GestorEscenas.EscenaMundo) return l;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) return l;
        return null;
    }
}
