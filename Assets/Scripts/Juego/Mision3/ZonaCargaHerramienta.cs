using UnityEngine;

/// <summary>
/// Punto de carga de la barra en la maletera del camion EXISTENTE. Se cuelga del camion
/// (sigue al camion en el viaje) y se ubica dentro del compartimento de InteriorMaletera.
/// Muestra una silueta de la barra mientras la llevas en la mano. Si la maletera esta cerrada
/// no recibe la herramienta; con el camion en marcha no se puede sacar.
/// </summary>
public class ZonaCargaHerramienta : MonoBehaviour
{
    [Tooltip("Distancia (m) de la barra al punto de carga para que cuente.")]
    public float radioDeteccion = 0.95f;

    public HerramientaRescate herramienta;
    public bool Cargada => herramienta != null && herramienta.EnMaletera;
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
    Transform _silueta;
    Material _matSilueta;
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
        go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // la barra queda atravesada en la maletera
        var z = go.AddComponent<ZonaCargaHerramienta>();
        z._camion = camion;
        z._puertas = camion.GetComponentsInChildren<PuertaMaletera>(true);
        z.CrearSilueta();
        return z;
    }

    void CrearSilueta()
    {
        _silueta = new GameObject("Silueta").transform;
        _silueta.SetParent(transform, false);
        _matSilueta = VisualesVR.MaterialSinLuz(new Color(1f, 0.85f, 0.1f, 0.45f));
        var barra = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Barra", _silueta, _matSilueta);
        barra.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barra.transform.localScale = new Vector3(0.05f, 0.38f, 0.05f);
        _silueta.gameObject.SetActive(false);
    }

    public bool Cerca(Vector3 p) => Vector3.Distance(p, transform.position) <= radioDeteccion;
    public bool PuedeRecibir(Vector3 p) => Cerca(p) && PuertaAbierta && _camion.VelocidadKmh < 1f;

    public void Cargar(HerramientaRescate h)
    {
        herramienta = h;
        h.GuardarEn(transform, Vector3.zero, Quaternion.identity);
        SonidosVR.ReproducirEn(SonidosVR.Metal, transform.position, 0.9f, 12f);
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
        if (herramienta == null) return;
        bool mostrar = herramienta.Agarrada && !herramienta.EnMaletera;
        if (_silueta.gameObject.activeSelf != mostrar) _silueta.gameObject.SetActive(mostrar);
        if (!mostrar) return;
        bool lista = PuedeRecibir(herramienta.Centro);
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
        _matSilueta.color = lista ? new Color(0.3f, 1f, 0.4f, 0.4f + 0.4f * pulso) : new Color(1f, 0.85f, 0.1f, 0.25f + 0.25f * pulso);
    }
}
