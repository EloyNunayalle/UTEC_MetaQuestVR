using UnityEngine;

/// <summary>
/// Puerta de entrada de la casa explorable (Mision 2): la hoja original de la Rowhouse, separada de la
/// fachada y con el pivote en la bisagra. Se abre hacia adentro al acercarse o al tocarla con la mano,
/// y se cierra sola solo cuando el jugador se aleja de la casa (no se cierra encima del jugador).
/// Se accede caminando: no hay teletransporte a traves de la pared.
/// </summary>
public class PuertaCasa : MonoBehaviour
{
    [Tooltip("Grados de apertura (positivo = hacia el interior de la casa).")]
    public float anguloAbierto = 100f;
    public float duracion = 1.1f;
    [Tooltip("Distancia horizontal de la cabeza a la puerta para abrirla.")]
    public float distanciaAbrir = 1.7f;
    [Tooltip("Se cierra si el jugador esta mas lejos que esto durante unos segundos.")]
    public float distanciaCerrar = 7f;
    [Tooltip("Ancho de la hoja (m): la manija esta en el extremo libre.")]
    public float ancho = 1f;

    public bool Abierta { get; private set; }
    /// <summary>Punto frente a la puerta, del lado de la calle (para la flecha guia).</summary>
    public Vector3 PuntoEntrada => transform.TransformPoint(new Vector3(ancho * 0.5f, 1.6f, 0.6f));

    Quaternion _rotCerrada;
    float _t, _lejos, _proximaBusqueda;
    Transform _cabeza;
    OVRCameraRig _rig;

    void Awake() => _rotCerrada = transform.localRotation;

    void Update()
    {
        if (_rig == null && Time.time >= _proximaBusqueda)
        {
            _proximaBusqueda = Time.time + 1f;
            _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : FindObjectOfType<OVRCameraRig>();
            if (_rig != null) _cabeza = _rig.centerEyeAnchor;
        }
        if (_cabeza == null) return;

        Vector3 centro = transform.TransformPoint(new Vector3(ancho * 0.5f, 1.2f, 0f));
        Vector3 d = _cabeza.position - centro; d.y = 0f;
        float dist = d.magnitude;
        bool tocada = ManoCerca(centro, 0.6f);

        if (!Abierta && (dist < distanciaAbrir || tocada)) Abrir();
        if (Abierta)
        {
            _lejos = dist > distanciaCerrar ? _lejos + Time.deltaTime : 0f;
            if (_lejos > 3f) Cerrar();
        }

        _t = Mathf.MoveTowards(_t, Abierta ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, duracion));
        float e = _t * _t * (3f - 2f * _t);
        transform.localRotation = _rotCerrada * Quaternion.Euler(0f, anguloAbierto * e, 0f);
    }

    bool ManoCerca(Vector3 centro, float radio)
    {
        if (_rig == null) return false;
        foreach (var m in new[] { _rig.leftHandAnchor, _rig.rightHandAnchor })
            if (m != null && m.gameObject.activeInHierarchy && (m.position - centro).sqrMagnitude < radio * radio) return true;
        return false;
    }

    public void Abrir()
    {
        if (Abierta) return;
        Abierta = true;
        _lejos = 0f;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position + Vector3.up * 1.2f, 0.8f, 12f);
    }

    public void Cerrar()
    {
        if (!Abierta) return;
        Abierta = false;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position + Vector3.up * 1.2f, 0.6f, 12f);
    }
}
