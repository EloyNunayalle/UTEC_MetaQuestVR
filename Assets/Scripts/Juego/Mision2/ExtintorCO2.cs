using System;
using UnityEngine;

/// <summary>
/// Extintor de CO2 de la Mision 2. Usa el modelo "CO2 Fire Extinguisher Low Poly", que trae las piezas
/// separadas: bocina (Moveable01), palanca (Moveable02) y pasador (Pin).
/// - Agarrar: acerca la mano y presiona GRIP (mismo agarre por cercania que el resto del juego).
///   Se lleva colgado de la mano por el asa, y la bocina apunta hacia donde apunta el control.
/// - Pasador: tocalo con la otra mano y presiona GRIP, o presiona A/X con el extintor en la mano.
/// - Descarga: gatillo (analogico) de la mano que lo sostiene. Teclado (editor): F = descargar, P = pasador.
/// - El chorro es un Particle System de nube blanca (no agua). Lo que apaga el fuego es un cono de
///   alcance desde la boquilla con comprobacion de obstaculos (raycast), que llama a FuegoElectrico.AplicarCO2.
/// Al soltarlo cae parado al piso. Si el modelo no esta en el proyecto, se arma uno simple con primitivas.
/// </summary>
public class ExtintorCO2 : MonoBehaviour
{
    [Tooltip("Instancia del modelo (hijo). Vacio = se usa un extintor simple de reemplazo.")]
    public Transform modelo;
    public float radioAgarre = 0.14f;
    [Tooltip("Alcance util del chorro de CO2 (m).")]
    public float alcance = 3.2f;
    [Tooltip("Medio angulo del cono que cuenta como impacto (grados).")]
    public float anguloCono = 14f;

    public bool Agarrado => _mano >= 0;
    public int Mano => _mano;
    public bool PasadorRetirado { get; private set; }
    public float Caudal { get; private set; }
    public bool Descargando => Caudal > 0.05f;
    public Vector3 Centro => VisualesVR.Limites(transform, out var b) ? b.center : transform.position;

    public event Action alAgarrar;
    public event Action alRetirarPasador;
    /// <summary>Se apreto el gatillo con el pasador puesto.</summary>
    public event Action alGatilloConPasador;
    /// <summary>El chorro esta alcanzando un fuego (se dispara como maximo ~4 veces por segundo).</summary>
    public event Action<FuegoElectrico> alImpactarFuego;

    Transform _bocina, _pivoteBocina, _palanca, _pivotePalanca, _pasador, _emisor;
    Vector3 _pasadorPosLocal, _asaLocal, _posInicial, _caidaDesde, _caidaHasta;
    Quaternion _rotInicial;
    ParticleSystem _co2;
    AudioSource _sonido;
    Renderer[] _renders;
    OVRCameraRig _rig;
    int _mano = -1, _hoverPrev = -1;
    float _caidaT = 1f, _proximaVibracion, _proximoImpacto, _animPasador = -1f;
    bool _gatilloPrev, _preparado;

    public void Preparar()
    {
        if (_preparado) return;
        _preparado = true;

        if (modelo == null || !VisualesVR.Limites(modelo, out _)) ModeloReemplazo();
        _bocina = BuscarParte("Moveable01") ?? BuscarParte("Bocina");
        _palanca = BuscarParte("Moveable02");
        _pasador = BuscarParte("Pin");
        if (_pasador != null) _pasadorPosLocal = _pasador.localPosition;

        _renders = GetComponentsInChildren<Renderer>();
        VisualesVR.Limites(transform, out Bounds b);
        Vector3 min = transform.InverseTransformPoint(b.min), max = transform.InverseTransformPoint(b.max);
        Vector3 centro = transform.InverseTransformPoint(b.center);
        _asaLocal = new Vector3(centro.x, Mathf.Max(min.y, max.y) - 0.05f, centro.z);

        // Pivote de la bocina en su extremo superior (union con la manguera): asi puede apuntar.
        if (_bocina != null && VisualesVR.Limites(_bocina, out Bounds bb))
        {
            _pivoteBocina = new GameObject("Pivote_Bocina").transform;
            _pivoteBocina.SetParent(transform, false);
            _pivoteBocina.position = new Vector3(bb.center.x, bb.max.y, bb.center.z);
            _bocina.SetParent(_pivoteBocina, true);
            _emisor = new GameObject("Boquilla").transform;
            _emisor.SetParent(_pivoteBocina, false);
            _emisor.position = new Vector3(bb.center.x, bb.min.y, bb.center.z);
            _emisor.localRotation = Quaternion.Euler(90f, 0f, 0f); // +Z del emisor = hacia abajo por la bocina
        }
        else
        {
            _emisor = new GameObject("Boquilla").transform;
            _emisor.SetParent(transform, false);
            _emisor.localPosition = new Vector3(0.15f, 0.5f, 0f);
            _emisor.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // Pivote de la palanca en su bisagra (el extremo del lado de la valvula, +X local).
        if (_palanca != null && VisualesVR.Limites(_palanca, out Bounds bp))
        {
            _pivotePalanca = new GameObject("Pivote_Palanca").transform;
            _pivotePalanca.SetParent(transform, false);
            Vector3 lmin = transform.InverseTransformPoint(bp.min), lmax = transform.InverseTransformPoint(bp.max);
            Vector3 lc = transform.InverseTransformPoint(bp.center);
            _pivotePalanca.localPosition = new Vector3(Mathf.Max(lmin.x, lmax.x), lc.y, lc.z);
            _palanca.SetParent(_pivotePalanca, true);
        }

        _co2 = CrearChorro();
        _sonido = SonidosVR.CrearBucle(_emisor, SonidosVR.Descarga, 0f, 15f);

        _posInicial = transform.position;
        _rotInicial = transform.rotation;
    }

    Transform BuscarParte(string nombre)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t != transform && t.name.Contains(nombre)) return t;
        return null;
    }

    void ModeloReemplazo()
    {
        var raiz = new GameObject("Extintor_Reemplazo").transform;
        raiz.SetParent(transform, false);
        modelo = raiz;
        var rojo = VisualesVR.MaterialOpaco(new Color(0.8f, 0.08f, 0.06f));
        var negro = VisualesVR.MaterialOpaco(new Color(0.08f, 0.08f, 0.08f));
        var cuerpo = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Cuerpo", raiz, rojo);
        cuerpo.transform.localScale = new Vector3(0.18f, 0.42f, 0.18f);
        cuerpo.transform.localPosition = new Vector3(0f, 0.92f, 0.13f);
        var valvula = VisualesVR.Primitiva(PrimitiveType.Cube, "Valvula", raiz, negro);
        valvula.transform.localScale = new Vector3(0.2f, 0.06f, 0.05f);
        valvula.transform.localPosition = new Vector3(-0.03f, 1.38f, 0.13f);
        var bocina = VisualesVR.Primitiva(PrimitiveType.Cylinder, "Bocina", raiz, negro);
        bocina.transform.localScale = new Vector3(0.07f, 0.22f, 0.07f);
        bocina.transform.localPosition = new Vector3(0.14f, 0.85f, 0.13f);
    }

    ParticleSystem CrearChorro()
    {
        var go = new GameObject("Nube_CO2");
        go.transform.SetParent(_emisor, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.93f, 0.95f, 1f, 0.55f);
        main.gravityModifier = 0.05f; // el CO2 es mas pesado que el aire
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;

        var em = ps.emission;
        em.rateOverTime = 0f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone; // apunta al +Z local (hacia afuera de la bocina)
        forma.angle = 9f;
        forma.radius = 0.03f;

        // La nube se frena y se expande al salir.
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = 1.2f;
        lim.dampen = 0.12f;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 7f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.87f, 0.9f), 1f) },
                  new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.35f;
        ruido.frequency = 1.5f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ZonaFuego.MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------

    Transform ManoT(int i)
    {
        if (_rig == null) _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : null;
        if (_rig == null) return null;
        var m = i == 0 ? _rig.leftHandAnchor : _rig.rightHandAnchor;
        return m != null && m.gameObject.activeInHierarchy ? m : null;
    }

    float Distancia(Vector3 p) => VisualesVR.Limites(transform, out var b) ? Mathf.Sqrt(b.SqrDistance(p)) : Vector3.Distance(transform.position, p);

    void Update()
    {
        if (!_preparado) return;
        bool permitido = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;
        AnimarPasador();

        if (_mano < 0)
        {
            Caer();
            ApuntarBocina(null);
            Descargar(0f);

            int hover = -1;
            if (permitido)
                for (int i = 0; i < 2; i++)
                {
                    var m = ManoT(i);
                    if (m == null || Distancia(m.position) > radioAgarre) continue;
                    hover = i;
                    if (EntradaVR.AgarreDown(i == 0)) { Agarrar(i); return; }
                }
            if (hover != _hoverPrev && hover >= 0)
            {
                EntradaVR.Vibrar(hover == 0, 0.25f, 0.05f, 0.8f);
                SonidosVR.ReproducirEn(SonidosVR.Clic, Centro, 0.5f);
            }
            _hoverPrev = hover;
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            VisualesVR.Tenir(_renders, new Color(1f, 0.85f, 0.1f), hover >= 0 ? 0.35f + 0.3f * pulso : 0f);
            return;
        }

        // ---- En la mano ----
        var mano = ManoT(_mano);
        if (mano == null || !permitido || !EntradaVR.Agarrando(_mano == 0)) { Soltar(); return; }

        // Colgado del asa, vertical; su +X (lado de la bocina) mira hacia donde apunta la mano.
        Vector3 f = mano.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = transform.right;
        float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg - 90f;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 pos = mano.position - rot * _asaLocal;
        float k = 1f - Mathf.Exp(-Time.deltaTime * 18f); // un poco de inercia: se siente el peso
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k), Quaternion.Slerp(transform.rotation, rot, k));
        ApuntarBocina(mano.forward);

        // Pasador: A/X con la mano que lo sostiene, o la otra mano con GRIP sobre el pasador.
        if (!PasadorRetirado)
        {
            var otra = ManoT(1 - _mano);
            bool otraLoTira = otra != null && _pasador != null && VisualesVR.Limites(_pasador, out var bpin) &&
                              Mathf.Sqrt(bpin.SqrDistance(otra.position)) < 0.08f && EntradaVR.AgarreDown(_mano != 0);
            if (otraLoTira || EntradaVR.ConfirmarDown() || Input.GetKeyDown(KeyCode.P)) RetirarPasador();
        }

        var c = _mano == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        float gatillo = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c);
        if (Input.GetKey(KeyCode.F)) gatillo = 1f;
        bool aprieta = gatillo > 0.15f;
        if (aprieta && !_gatilloPrev && !PasadorRetirado)
        {
            SonidosVR.ReproducirEn(SonidosVR.Error, transform.position, 0.6f, 6f);
            EntradaVR.Vibrar(_mano == 0, 0.4f, 0.1f, 0.3f);
            alGatilloConPasador?.Invoke();
        }
        _gatilloPrev = aprieta;
        Descargar(PasadorRetirado ? gatillo : 0f);
    }

    void Agarrar(int i)
    {
        _mano = i;
        _hoverPrev = -1;
        _caidaT = 1f;
        _gatilloPrev = true; // el gatillo que se uso para agarrar no cuenta como disparo
        VisualesVR.Tenir(_renders, Color.white, 0f);
        EntradaVR.Vibrar(i == 0, 0.7f, 0.12f, 0.6f);
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, Centro, 0.9f);
        alAgarrar?.Invoke();
    }

    void Soltar()
    {
        if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.3f, 0.05f);
        _mano = -1;
        Descargar(0f);
        // Cae parado al piso que tenga debajo; si no hay piso, vuelve a su soporte.
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        _caidaDesde = transform.position;
        _caidaHasta = PosicionEnPiso(out bool hay);
        if (!hay) { _caidaHasta = _posInicial; transform.rotation = _rotInicial; }
        _caidaT = 0f;
    }

    void Caer()
    {
        if (_caidaT >= 1f) return;
        _caidaT = Mathf.Min(1f, _caidaT + Time.deltaTime * 3f);
        transform.position = Vector3.Lerp(_caidaDesde, _caidaHasta, _caidaT * _caidaT);
        if (_caidaT >= 1f) SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position, 0.4f, 8f);
    }

    Vector3 PosicionEnPiso(out bool hay)
    {
        hay = false;
        if (!VisualesVR.Limites(transform, out var b)) return transform.position;
        var origen = new Vector3(b.center.x, b.max.y, b.center.z);
        float mejor = float.MaxValue;
        Vector3 punto = default;
        foreach (var h in Physics.RaycastAll(origen, Vector3.down, 4f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.distance < 0.3f) continue;
            if (_rig != null && h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; }
        }
        if (mejor == float.MaxValue) return transform.position;
        hay = true;
        return transform.position + Vector3.up * (punto.y + 0.005f - b.min.y);
    }

    void ApuntarBocina(Vector3? direccion)
    {
        if (_pivoteBocina == null) return;
        Quaternion objetivo = Quaternion.identity;
        if (direccion.HasValue)
        {
            // La bocina cuelga hacia -Y; se orienta hacia la mano sin apuntar mas de 35 grados hacia arriba.
            Vector3 d = transform.InverseTransformDirection(direccion.Value).normalized;
            d.y = Mathf.Min(d.y, 0.57f);
            objetivo = Quaternion.FromToRotation(Vector3.down, d.normalized);
        }
        _pivoteBocina.localRotation = Quaternion.Slerp(_pivoteBocina.localRotation, objetivo, 1f - Mathf.Exp(-Time.deltaTime * 14f));
    }

    void RetirarPasador()
    {
        if (PasadorRetirado) return;
        PasadorRetirado = true;
        _animPasador = 0f;
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position + Vector3.up, 0.9f, 8f);
        SonidosVR.ReproducirEn(SonidosVR.Clic, transform.position + Vector3.up, 1f, 8f);
        if (_mano >= 0) EntradaVR.VibrarAmbas(0.5f, 0.1f, 0.8f);
        alRetirarPasador?.Invoke();
    }

    void AnimarPasador()
    {
        if (_pasador == null || _animPasador < 0f) return;
        _animPasador += Time.deltaTime / 0.35f;
        // Sale a lo largo de su eje (el pasador es largo en Z local del modelo) y desaparece.
        _pasador.localPosition = _pasadorPosLocal + Vector3.forward * (0.12f * Mathf.Min(1f, _animPasador));
        if (_animPasador >= 1f)
        {
            _pasador.gameObject.SetActive(false);
            _animPasador = -1f;
        }
    }

    void Descargar(float gatillo)
    {
        Caudal = Mathf.MoveTowards(Caudal, gatillo < 0.1f ? 0f : gatillo, Time.deltaTime * 6f);
        var em = _co2.emission;
        em.rateOverTime = Caudal * 240f;
        if (_pivotePalanca != null) _pivotePalanca.localRotation = Quaternion.Euler(0f, 0f, 10f * Caudal);

        if (Caudal > 0.01f)
        {
            if (!_sonido.isPlaying) _sonido.Play();
            _sonido.volume = 0.9f * Caudal;
            _sonido.pitch = 0.9f + 0.2f * Caudal;
            if (_mano >= 0 && Time.time >= _proximaVibracion)
            {
                _proximaVibracion = Time.time + 0.1f;
                EntradaVR.Vibrar(_mano == 0, 0.12f + 0.25f * Caudal, 0.12f, 0.25f);
            }
        }
        else if (_sonido.isPlaying) _sonido.Stop();

        if (Descargando) AplicarAlFuego();
    }

    void AplicarAlFuego()
    {
        Vector3 o = _emisor.position, dir = _emisor.forward;
        foreach (var fuego in FuegoElectrico.Activos)
        {
            if (fuego == null || !fuego.Encendido) continue;
            Vector3 v = fuego.Centro - o;
            float d = v.magnitude;
            if (d > alcance + fuego.radio || d < 0.01f) continue;
            float tolerancia = anguloCono + Mathf.Atan2(fuego.radio, Mathf.Max(0.2f, d)) * Mathf.Rad2Deg;
            if (Vector3.Angle(dir, v) > tolerancia) continue;
            if (Tapado(o, v / d, d, fuego)) continue;

            float potencia = Caudal * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(d / alcance));
            fuego.AplicarCO2(potencia, Time.deltaTime);
            if (Time.time >= _proximoImpacto)
            {
                _proximoImpacto = Time.time + 0.25f;
                if (_mano >= 0) EntradaVR.Vibrar(_mano == 0, 0.35f, 0.08f, 0.6f);
                alImpactarFuego?.Invoke(fuego);
            }
        }
    }

    /// <summary>Hay una pared u objeto entre la boquilla y el fuego (no cuentan el equipo ni el jugador).</summary>
    bool Tapado(Vector3 o, Vector3 dir, float d, FuegoElectrico fuego)
    {
        foreach (var h in Physics.RaycastAll(o, dir, Mathf.Max(0f, d - fuego.radio), ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            var t = h.collider.transform;
            if (t.IsChildOf(transform)) continue;
            if (fuego.ignorarColisiones != null && t.IsChildOf(fuego.ignorarColisiones)) continue;
            if (_rig != null && t.IsChildOf(_rig.transform)) continue;
            return true;
        }
        return false;
    }

    /// <summary>Vuelve a su soporte en la pared, con el pasador puesto (reinicio).</summary>
    public void Restablecer()
    {
        Preparar();
        _mano = -1;
        Caudal = 0f;
        Descargar(0f);
        PasadorRetirado = false;
        _animPasador = -1f;
        if (_pasador != null)
        {
            _pasador.localPosition = _pasadorPosLocal;
            _pasador.gameObject.SetActive(true);
        }
        if (_pivoteBocina != null) _pivoteBocina.localRotation = Quaternion.identity;
        _caidaT = 1f;
        transform.SetPositionAndRotation(_posInicial, _rotInicial);
    }
}
