using UnityEngine;

public class PortonElevadizo : MonoBehaviour
{
    public enum Estado { Cerrado, Abriendo, Abierto, Cerrando }

    [Header("Pivote y recorrido")]
    public Transform pivote;
    public float anguloAbierto = 95f;
    public float duracion = 4.5f;

    [Header("Brazos hidraulicos")]
    public Transform anclaIzq;
    public Transform anclaDer;
    public Transform adjuntoIzq;
    public Transform adjuntoDer;
    public Transform fundaIzq;
    public Transform varaIzq;
    public Transform fundaDer;
    public Transform varaDer;
    public float radioFunda = 0.05f;
    public float radioVara = 0.025f;

    [Header("Seguridad")]
    public Transform jugador;
    public Vector3 zonaBarridoCentro;
    public Vector3 zonaBarridoTamano;

    [Header("Activacion extra")]
    [Tooltip("Tecla para abrir/cerrar (editor o teclado).")]
    public KeyCode teclaAlternar = KeyCode.P;
    [Tooltip("Abre solo cuando el jugador se acerca manejando el camion.")]
    public bool abrirAlAcercarseCamion = true;
    [Tooltip("Distancia (m) entre el camion y la zona del porton para abrir.")]
    public float distanciaCamion = 5f;

    public Estado estado = Estado.Cerrado;
    public bool EstaAbierto => estado == Estado.Abierto;

    Quaternion _rotCerrado;
    float _t;
    float _proximaBusquedaJugador;
    CamionBomberos _camion;
    float _proximaBusquedaCamion;

    void Awake()
    {
        if (pivote == null) pivote = transform;
        _rotCerrado = pivote.localRotation;
    }

    /// <summary>
    /// El jugador vive en la escena persistente (división de escenas), así que la
    /// referencia serializada puede quedar vacía: se busca el rig automáticamente.
    /// </summary>
    void BuscarJugador()
    {
        if (Time.unscaledTime < _proximaBusquedaJugador) return;
        _proximaBusquedaJugador = Time.unscaledTime + 0.5f;

        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) jugador = rig.centerEyeAnchor != null ? rig.centerEyeAnchor : rig.transform;
    }

    public void Alternar()
    {
        if (estado == Estado.Cerrado || estado == Estado.Cerrando) Abrir();
        else Cerrar();
    }

    public void Abrir()
    {
        estado = Estado.Abriendo;
    }

    public void Cerrar()
    {
        estado = HayJugadorEnZona() ? Estado.Abierto : Estado.Cerrando;
    }

    void Update()
    {
        if (teclaAlternar != KeyCode.None && Input.GetKeyDown(teclaAlternar)) Alternar();
        if (abrirAlAcercarseCamion && (estado == Estado.Cerrado || estado == Estado.Cerrando) && CamionCerca(distanciaCamion, true))
            Abrir();

        float dt = Time.deltaTime / Mathf.Max(0.1f, duracion);
        if (estado == Estado.Abriendo)
        {
            _t = Mathf.Min(1f, _t + dt);
            if (_t >= 1f) estado = Estado.Abierto;
        }
        else if (estado == Estado.Cerrando)
        {
            if (HayJugadorEnZona())
            {
                estado = Estado.Abriendo;
            }
            else
            {
                _t = Mathf.Max(0f, _t - dt);
                if (_t <= 0f) estado = Estado.Cerrado;
            }
        }

        float e = _t * _t * (3f - 2f * _t);
        pivote.localRotation = _rotCerrado * Quaternion.Euler(anguloAbierto * e, 0f, 0f);

        ActualizarBrazo(anclaIzq, adjuntoIzq, fundaIzq, varaIzq);
        ActualizarBrazo(anclaDer, adjuntoDer, fundaDer, varaDer);
    }

    void ActualizarBrazo(Transform ancla, Transform adjunto, Transform funda, Transform vara)
    {
        if (ancla == null || adjunto == null || funda == null || vara == null) return;
        Vector3 a = ancla.position;
        Vector3 b = adjunto.position;
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Vector3 n = d / len;
        float fundaLen = Mathf.Min(0.52f * len, Mathf.Max(0.1f, len - 0.18f));
        float varaLen = Mathf.Max(0.05f, len - fundaLen);

        funda.position = a + n * (fundaLen * 0.5f);
        funda.rotation = Quaternion.FromToRotation(Vector3.up, n);
        funda.localScale = new Vector3(radioFunda * 2f, fundaLen * 0.5f, radioFunda * 2f);

        vara.position = a + n * (fundaLen + varaLen * 0.5f);
        vara.rotation = funda.rotation;
        vara.localScale = new Vector3(radioVara * 2f, varaLen * 0.5f, radioVara * 2f);
    }

    /// <summary>El camion (opcionalmente solo si alguien lo maneja) esta a menos de "margen" de la zona del porton.</summary>
    bool CamionCerca(float margen, bool soloManejando)
    {
        if (_camion == null && Time.unscaledTime >= _proximaBusquedaCamion)
        {
            _proximaBusquedaCamion = Time.unscaledTime + 1f;
            _camion = FindObjectOfType<CamionBomberos>();
        }
        if (_camion == null || (soloManejando && !_camion.JugadorSentado)) return false;
        Bounds zona = new Bounds(zonaBarridoCentro, zonaBarridoTamano);
        Bounds b = _camion.Limites;
        b.Expand(margen * 2f);
        return b.Intersects(zona);
    }

    bool HayJugadorEnZona()
    {
        // Seguridad: nunca se cierra sobre el camion.
        if (CamionCerca(0f, false)) return true;

        if (jugador == null) BuscarJugador();
        if (jugador == null) return false;
        Bounds zona = new Bounds(zonaBarridoCentro, zonaBarridoTamano);
        CharacterController cc = jugador.GetComponent<CharacterController>();
        if (cc != null) return zona.Intersects(cc.bounds);
        return zona.Contains(jugador.position);
    }
}