using UnityEngine;

/// <summary>
/// Puerta trasera de la maletera. Se abre y cierra agarrándola con la mano
/// (GRIP / gatillo / pellizco) cerca del tirador. Los tiradores son solo
/// decorativos: no se presionan.
/// - La puerta sigue el giro de la mano alrededor de la bisagra.
/// - Al soltar, termina de abrir o de cerrar según cuánto se haya movido.
/// - Si el camión arranca, se cierra sola.
/// </summary>
public class PuertaMaletera : MonoBehaviour
{
    [Header("Apertura")]
    [Tooltip("Grados de apertura. El signo define el sentido de giro.")]
    public float anguloAbierto = 95f;
    [Tooltip("Grados por segundo al soltar o al abrir/cerrar automáticamente.")]
    public float velocidad = 110f;
    [Tooltip("Si esta activo, la puerta se cierra sola cuando el camion empieza a moverse.")]
    public bool bloquearEnMovimiento = true;
    [Tooltip("Velocidad (km/h) a partir de la cual se considera que el camion esta en movimiento.")]
    public float velocidadMaximaParaAbrir = 1f;

    [Header("Agarre")]
    [Tooltip("Punto del tirador donde se detecta la mano. Si esta vacio se usa el pivote.")]
    public Transform agarre;
    [Tooltip("Radio (m) alrededor del tirador para poder agarrar.")]
    public float radioAgarre = 0.20f;
    [Tooltip("Fraccion del recorrido a partir de la cual, al soltar, la puerta queda abierta.")]
    [Range(0.1f, 0.9f)] public float umbralApertura = 0.35f;
    public Color colorResaltado = new Color(1f, 0.85f, 0.1f);

    public bool abierta;
    public CamionBomberos camion;

    Quaternion _cerrada;
    float _objetivo;
    float _anguloActual;
    int _mano = -1;
    float _anguloManoInicial;
    float _anguloPuertaInicial;
    OVRCameraRig _rig;
    Renderer[] _renders;

    CamionBomberos Camion
    {
        get
        {
            if (camion == null) camion = GetComponentInParent<CamionBomberos>();
            return camion;
        }
    }

    OVRCameraRig Rig
    {
        get
        {
            if (_rig == null)
            {
                _rig = JuegoBomberos.Instancia != null
                    ? JuegoBomberos.Instancia.rig
                    : FindObjectOfType<OVRCameraRig>();
            }
            return _rig;
        }
    }

    Transform PuntoAgarre => agarre != null ? agarre : transform;

    public bool PuedeAbrir
    {
        get
        {
            if (!bloquearEnMovimiento) return true;
            var c = Camion;
            return c == null || c.VelocidadKmh <= velocidadMaximaParaAbrir;
        }
    }

    void Awake()
    {
        _cerrada = transform.localRotation;
        _renders = GetComponentsInChildren<Renderer>(true);
    }

    void OnDisable()
    {
        VisualesVR.Tenir(_renders, Color.white, 0f);
    }

    public void Alternar()
    {
        if (abierta) Cerrar();
        else Abrir();
    }

    public void Abrir()
    {
        if (abierta || !PuedeAbrir) return;
        abierta = true;
        _objetivo = anguloAbierto;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, PuntoAgarre.position, 0.8f);
    }

    public void Cerrar()
    {
        if (!abierta) return;
        abierta = false;
        _objetivo = 0f;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, PuntoAgarre.position, 0.7f);
    }

    void Update()
    {
        // Si el camion arranca con la maletera abierta, se cierra sola.
        if (bloquearEnMovimiento && (abierta || _mano >= 0) && !PuedeAbrir)
        {
            _mano = -1;
            abierta = false;
            _objetivo = 0f;
            VisualesVR.Tenir(_renders, Color.white, 0f);
        }

        if (_mano >= 0) ActualizarAgarre();
        else
        {
            BuscarAgarre();
            if (_mano < 0)
            {
                _anguloActual = Mathf.MoveTowards(_anguloActual, _objetivo, velocidad * Time.deltaTime);
                AplicarAngulo();
            }
        }
    }

    void BuscarAgarre()
    {
        int mano = -1;
        float mejor = radioAgarre;

        for (int i = 0; i < 2; i++)
        {
            var m = Mano(i);
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(m.position, PuntoAgarre.position);
            if (d >= mejor) continue;
            if (!EsLaMasCercana(m)) continue;
            mejor = d;
            mano = i;
        }

        if (mano < 0)
        {
            VisualesVR.Tenir(_renders, Color.white, 0f);
            return;
        }

        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
        VisualesVR.Tenir(_renders, colorResaltado, 0.25f + 0.15f * pulso);

        if (EntradaVR.AgarreDown(mano == 0))
        {
            Transform m = Mano(mano);
            _mano = mano;
            _anguloManoInicial = AnguloMano(m);
            _anguloPuertaInicial = _anguloActual;
            _objetivo = _anguloActual;
            VisualesVR.Tenir(_renders, colorResaltado, 0.35f);
            EntradaVR.Vibrar(mano == 0, 0.45f, 0.08f, 0.7f);
            SonidosVR.ReproducirEn(SonidosVR.Agarrar, PuntoAgarre.position, 0.8f);
        }
    }

    void ActualizarAgarre()
    {
        var m = Mano(_mano);
        if (m == null || !m.gameObject.activeInHierarchy || !EntradaVR.Agarrando(_mano == 0))
        {
            Soltar();
            return;
        }

        float anguloMano = AnguloMano(m);
        float delta = Mathf.DeltaAngle(_anguloManoInicial, anguloMano);
        float minimo = Mathf.Min(0f, anguloAbierto);
        float maximo = Mathf.Max(0f, anguloAbierto);
        _anguloActual = Mathf.Clamp(_anguloPuertaInicial - delta, minimo, maximo);
        _objetivo = _anguloActual;
        AplicarAngulo();
    }

    void Soltar()
    {
        if (_mano < 0) return;
        int mano = _mano;
        _mano = -1;
        abierta = Mathf.Abs(_anguloActual) > Mathf.Abs(anguloAbierto) * umbralApertura;
        _objetivo = abierta ? anguloAbierto : 0f;
        VisualesVR.Tenir(_renders, Color.white, 0f);
        EntradaVR.Vibrar(mano == 0, 0.3f, 0.05f, 0.7f);
        SonidosVR.ReproducirEn(SonidosVR.Puerta, PuntoAgarre.position, 0.7f);
    }

    void AplicarAngulo()
    {
        transform.localRotation = _cerrada * Quaternion.Euler(0f, _anguloActual, 0f);
    }

    Transform Mano(int i)
    {
        var rig = Rig;
        if (rig == null) return null;
        return i == 0 ? rig.leftHandAnchor : rig.rightHandAnchor;
    }

    /// <summary>Angulo de la mano alrededor de la bisagra, en el plano XZ del camion.</summary>
    float AnguloMano(Transform mano)
    {
        Vector3 d = mano.position - transform.position;
        Transform raiz = transform.parent != null ? transform.parent : transform;
        float x = Vector3.Dot(d, raiz.right);
        float z = Vector3.Dot(d, raiz.forward);
        return Mathf.Atan2(z, x) * Mathf.Rad2Deg;
    }

    /// <summary>Los dos tiradores estan muy cerca: solo el mas cercano puede agarrarse.</summary>
    bool EsLaMasCercana(Transform mano)
    {
        float miDistancia = Vector3.Distance(mano.position, PuntoAgarre.position);
        var puertas = transform.root.GetComponentsInChildren<PuertaMaletera>(true);
        foreach (var p in puertas)
        {
            if (p == this) continue;
            float suDistancia = Vector3.Distance(mano.position, p.PuntoAgarre.position);
            if (suDistancia < miDistancia - 0.001f) return false;
            if (Mathf.Abs(suDistancia - miDistancia) <= 0.001f &&
                p.GetInstanceID() < GetInstanceID()) return false;
        }
        return true;
    }
}
