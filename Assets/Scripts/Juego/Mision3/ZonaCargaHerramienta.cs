using UnityEngine;

/// <summary>
/// Puntos de carga en la maletera del camion EXISTENTE: la barra de rescate y el extintor CO2.
/// Se cuelga del camion (sigue al camion en el viaje) y se ubica dentro del compartimento de
/// InteriorMaletera. Muestra una silueta de lo que llevas en la mano. Si la maletera esta cerrada
/// no recibe nada; con el camion en marcha no se puede sacar nada.
/// </summary>
public class ZonaCargaHerramienta : MonoBehaviour
{
    [Tooltip("Distancia (m) del objeto al punto de carga para que cuente.")]
    public float radioDeteccion = 0.95f;

    public HerramientaRescate herramienta;
    public ExtintorCO2 extintor;

    // Puntos dentro del compartimento. Ejes de esta zona: +X hacia la abertura trasera, +Z a lo ancho
    // del camion, +Y arriba. Origen: 15 cm sobre el piso, en el centro del compartimento (55 cm de fondo).
    // - Extintor: abajo y adelante (borde a ~3.5 cm de la abertura), acostado a lo ancho con el asa y la
    //   boquilla hacia afuera: se ve y se agarra desde atras con una mano.
    // - Barra: arriba, sobre dos soportes, 8 cm por encima del extintor y ~3 cm detras del plano de las
    //   puertas (no la golpean al cerrar).
    static readonly Vector3 PuntoBarra = new Vector3(0.21f, 0.22f, 0f);
    static readonly Vector3 PuntoExtintor = new Vector3(0.11f, -0.05f, 0f);
    static readonly Quaternion RotExtintor = Quaternion.Euler(90f, 0f, 0f); // acostado a lo ancho, asa hacia la abertura

    public bool Cargada => herramienta != null && herramienta.EnMaletera;
    public bool ExtintorCargado => extintor != null && extintor.EnMaletera;
    public Vector3 Punto => transform.position;
    /// <summary>Punto para la flecha: en la parte de atras del camion, sobre la maletera.</summary>
    public Vector3 PuntoMaletera => transform.position + Vector3.up * 1f - _camion.transform.forward * 0.3f;
    public bool PuertaAbierta
    {
        get
        {
            if (_puertas == null || _puertas.Length == 0) return true;
            foreach (var p in _puertas) if (p != null && p.abierta) return true;
            return false;
        }
    }
    /// <summary>Solo se puede sacar con la maletera abierta y el camion detenido.</summary>
    public bool SePuedeSacar => PuertaAbierta && _camion.VelocidadKmh < 1f && !_camion.JugadorSentado;

    CamionBomberos _camion;
    PuertaMaletera[] _puertas;
    Transform _siluetaBarra, _siluetaExtintor;
    Material _matBarra, _matExtintor;
    float _proximoAviso;

    public static ZonaCargaHerramienta Crear(CamionBomberos camion)
    {
        // Piso del compartimento de la maletera (si el camion trae InteriorMaletera).
        Vector3 centro = new Vector3(0f, 0.9f, -3.95f);
        var interior = camion.GetComponent<InteriorMaletera>();
        if (interior != null) centro = interior.centro + Vector3.down * (interior.alto * 0.5f - 0.15f);

        var go = new GameObject("Zona_Carga_Herramienta");
        go.transform.SetParent(camion.transform, false);
        go.transform.localPosition = centro;
        go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // a lo ancho de la maletera
        var z = go.AddComponent<ZonaCargaHerramienta>();
        z._camion = camion;
        z._puertas = camion.GetComponentsInChildren<PuertaMaletera>(true);
        z._matBarra = VisualesVR.MaterialSinLuz(new Color(1f, 0.85f, 0.1f, 0.45f));
        z._matExtintor = VisualesVR.MaterialSinLuz(new Color(1f, 0.85f, 0.1f, 0.45f));
        z._siluetaBarra = z.CrearSilueta("Silueta_Barra", PuntoBarra, new Vector3(0.05f, 0.38f, 0.05f), z._matBarra);
        z._siluetaExtintor = z.CrearSilueta("Silueta_Extintor", PuntoExtintor, new Vector3(0.15f, 0.38f, 0.15f), z._matExtintor);
        z.CrearSoportesBarra();
        return z;
    }

    /// <summary>Dos soportes (visuales) donde descansa la barra, cerca de sus extremos.</summary>
    void CrearSoportesBarra()
    {
        var mat = VisualesVR.MaterialOpaco(new Color(0.18f, 0.18f, 0.2f));
        foreach (float lado in new[] { -0.33f, 0.33f })
        {
            var s = VisualesVR.Primitiva(PrimitiveType.Cube, "Soporte_Barra", transform, mat);
            s.transform.localPosition = PuntoBarra + new Vector3(0f, -0.135f, lado);
            s.transform.localScale = new Vector3(0.10f, 0.03f, 0.04f);
        }
    }

    Transform CrearSilueta(string nombre, Vector3 pos, Vector3 escala, Material mat)
    {
        var s = VisualesVR.Primitiva(PrimitiveType.Cylinder, nombre, transform, mat);
        s.transform.localPosition = pos;
        s.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        s.transform.localScale = escala;
        s.SetActive(false);
        return s.transform;
    }

    public bool Cerca(Vector3 p) => Vector3.Distance(p, transform.position) <= radioDeteccion;
    public bool PuedeRecibir(Vector3 p) => Cerca(p) && PuertaAbierta && _camion.VelocidadKmh < 1f;

    public void Cargar(HerramientaRescate h)
    {
        herramienta = h;
        h.GuardarEn(transform, PuntoBarra, Quaternion.identity);
        Confirmar(SonidosVR.Metal);
    }

    public void CargarExtintor(ExtintorCO2 e)
    {
        extintor = e;
        e.GuardarEn(transform, PuntoExtintor, RotExtintor);
        Confirmar(SonidosVR.Puerta);
    }

    void Confirmar(AudioClip golpe)
    {
        SonidosVR.ReproducirEn(golpe, transform.position, 0.9f, 12f);
        SonidosVR.Reproducir2D(SonidosVR.Exito, 0.7f);
        EntradaVR.VibrarAmbas(0.6f, 0.15f, 0.7f);
    }

    public void AvisarPuertaCerrada()
    {
        if (Time.time < _proximoAviso) return;
        _proximoAviso = Time.time + 3f;
        SonidosVR.Reproducir2D(SonidosVR.Error, 0.5f);
        JuegoBomberos.Instancia?.Aviso("Abre la maletera primero\n<size=55%>Agarra el tirador trasero con la otra mano (GRIP)</size>", JuegoBomberos.TipoAviso.Error, 3f);
    }

    void Update()
    {
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
        if (herramienta != null) Silueta(_siluetaBarra, _matBarra, herramienta.Agarrada && !herramienta.EnMaletera, herramienta.Centro, pulso);
        if (extintor != null) Silueta(_siluetaExtintor, _matExtintor, extintor.Agarrado && !extintor.EnMaletera, extintor.Centro, pulso);
    }

    void Silueta(Transform silueta, Material mat, bool mostrar, Vector3 objeto, float pulso)
    {
        if (silueta.gameObject.activeSelf != mostrar) silueta.gameObject.SetActive(mostrar);
        if (!mostrar) return;
        mat.color = PuedeRecibir(objeto) ? new Color(0.3f, 1f, 0.4f, 0.4f + 0.4f * pulso) : new Color(1f, 0.85f, 0.1f, 0.25f + 0.25f * pulso);
    }
}
