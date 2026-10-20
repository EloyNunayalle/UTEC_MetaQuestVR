using UnityEngine;

public class PuertaAutomatica : MonoBehaviour
{
    [Header("Puerta")]
    public Transform pivote;
    public float anguloAbierto = 85f;
    public float duracion = 1.4f;

    [Header("Deteccion de jugador")]
    public Transform jugador;
    public float distanciaAbrir = 2.2f;
    public float distanciaCerrar = 3.2f;
    public float retrasoCierre = 1.2f;

    public bool EstaAbierta => _abierto;

    Quaternion _rotCerrada;
    float _t;
    float _tiempoFuera;
    bool _abierto;
    float _proximaBusquedaJugador;

    void Awake()
    {
        if (pivote == null) pivote = transform;
        _rotCerrada = pivote.rotation;
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

    public void Abrir()
    {
        _abierto = true;
        _tiempoFuera = 0f;
    }

    public void Cerrar()
    {
        _abierto = false;
    }

    void Update()
    {
        if (jugador == null) BuscarJugador();
        if (jugador == null) return;

        Vector3 a = jugador.position; a.y = 0f;
        Vector3 b = pivote.position; b.y = 0f;
        float dist = Vector3.Distance(a, b);

        if (!_abierto)
        {
            _tiempoFuera = 0f;
            if (dist <= distanciaAbrir) _abierto = true;
        }
        else
        {
            if (dist >= distanciaCerrar) _tiempoFuera += Time.deltaTime;
            else _tiempoFuera = 0f;
            if (_tiempoFuera >= retrasoCierre)
            {
                _abierto = false;
                _tiempoFuera = 0f;
            }
        }

        float dt = Time.deltaTime / Mathf.Max(0.1f, duracion);
        _t = Mathf.MoveTowards(_t, _abierto ? 1f : 0f, dt);
        float e = _t * _t * (3f - 2f * _t);
        pivote.rotation = Quaternion.AngleAxis(anguloAbierto * e, Vector3.up) * _rotCerrada;
    }
}