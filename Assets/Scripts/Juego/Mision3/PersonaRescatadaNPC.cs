using TMPro;
using UnityEngine;

/// <summary>
/// Persona atrapada de la Mision 3. Es un personaje independiente del jugador: no usa el rig,
/// ni el seguimiento de manos, ni el Meta Avatar del jugador (cuerpo armado con primitivas y
/// animado por codigo, con las proporciones de una persona de 1.70 m).
/// Estados: Atrapada (sentada en el piso, pide ayuda) -> Reaccionando (se abrio la puerta) ->
/// Levantandose (cuando te acercas) -> Siguiendo / Esperando -> Segura.
///
/// Seguimiento: camina por el MISMO recorrido que hizo el jugador (migas de pan cada 35 cm). Asi no
/// atraviesa paredes y no hace falta un NavMesh horneado. Si te alejas demasiado se detiene y pide
/// que la esperes; si un tramo quedo tapado (p. ej. teletransporte) salta a la miga siguiente.
/// </summary>
public class PersonaRescatadaNPC : MonoBehaviour
{
    public enum Estado { Atrapada, Reaccionando, Levantandose, Siguiendo, Esperando, Segura }

    [Header("Seguimiento")]
    public float separacion = 1.5f;
    public float velocidad = 1.25f;
    public float velocidadRapida = 1.9f;
    [Tooltip("Si el jugador esta mas lejos que esto, se detiene y pide que la esperes.")]
    public float distanciaEspera = 11f;
    [Tooltip("Distancia a la que el jugador la hace levantarse.")]
    public float distanciaLevantar = 2.8f;

    public Estado EstadoActual { get; private set; } = Estado.Atrapada;
    public bool Libre => EstadoActual != Estado.Atrapada;
    public Vector3 PuntoGuia => transform.position + Vector3.up * 1.9f;

    Transform _raiz, _caderas, _torso, _cabeza, _hombroI, _hombroD, _codoI, _codoD, _piernaI, _piernaD, _rodillaI, _rodillaD;
    TextMeshPro _globo;
    AudioSource _voz;
    Transform _jugador;
    OVRCameraRig _rig;
    readonly System.Collections.Generic.List<Vector3> _camino = new System.Collections.Generic.List<Vector3>();
    float _kParado, _kMarcha, _faseMarcha, _proximoGrito, _finGlobo, _atascado, _kSaludo;
    bool _golpesToca;

    struct Pose
    {
        public float cadera, torso, cabeza, hombro, codo, pierna, rodilla;
        public Pose(float cadera, float torso, float cabeza, float hombro, float codo, float pierna, float rodilla)
        {
            this.cadera = cadera; this.torso = torso; this.cabeza = cabeza; this.hombro = hombro;
            this.codo = codo; this.pierna = pierna; this.rodilla = rodilla;
        }
    }

    // Angulos en grados: pierna/hombro negativos = hacia adelante; rodilla positiva = dobla atras; codo negativo = dobla adelante.
    static readonly Pose Sentada = new Pose(0.14f, -14f, 14f, -18f, -50f, -74f, 34f);
    static readonly Pose Parada = new Pose(0.93f, 3f, 0f, 4f, -10f, 0f, 2f);

    void Awake()
    {
        Construir();
        _proximoGrito = Time.time + Random.Range(1f, 3f);
        Aplicar(Sentada, Parada, 0f, 0f, 0f);
    }

    // ------------------------------------------------------------------
    // Cuerpo con primitivas
    // ------------------------------------------------------------------

    void Construir()
    {
        var piel = VisualesVR.MaterialOpaco(new Color(0.86f, 0.66f, 0.52f));
        var camisa = VisualesVR.MaterialOpaco(new Color(0.2f, 0.42f, 0.66f));
        var pantalon = VisualesVR.MaterialOpaco(new Color(0.17f, 0.19f, 0.27f));
        var pelo = VisualesVR.MaterialOpaco(new Color(0.13f, 0.09f, 0.06f));
        var zapato = VisualesVR.MaterialOpaco(new Color(0.09f, 0.08f, 0.08f));

        _raiz = Pivote("Cuerpo", transform, Vector3.zero);
        _caderas = Pivote("Caderas", _raiz, Vector3.up * 0.93f);
        Parte(PrimitiveType.Capsule, "Pelvis", _caderas, pantalon, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.12f, 0.2f));

        _torso = Pivote("Torso", _caderas, Vector3.up * 0.05f);
        Parte(PrimitiveType.Capsule, "Pecho", _torso, camisa, new Vector3(0f, 0.25f, 0f), new Vector3(0.34f, 0.27f, 0.21f));
        Parte(PrimitiveType.Cylinder, "Cuello", _torso, piel, new Vector3(0f, 0.54f, 0f), new Vector3(0.09f, 0.05f, 0.09f));

        _cabeza = Pivote("Cabeza", _torso, Vector3.up * 0.57f);
        Parte(PrimitiveType.Sphere, "Rostro", _cabeza, piel, new Vector3(0f, 0.11f, 0.01f), new Vector3(0.19f, 0.23f, 0.21f));
        Parte(PrimitiveType.Sphere, "Pelo", _cabeza, pelo, new Vector3(0f, 0.15f, -0.025f), new Vector3(0.21f, 0.2f, 0.21f));

        _hombroI = Pivote("Hombro_I", _torso, new Vector3(-0.2f, 0.46f, 0f));
        _codoI = Brazo(_hombroI, camisa, piel);
        _hombroD = Pivote("Hombro_D", _torso, new Vector3(0.2f, 0.46f, 0f));
        _codoD = Brazo(_hombroD, camisa, piel);

        _piernaI = Pivote("Pierna_I", _caderas, new Vector3(-0.09f, -0.02f, 0f));
        _rodillaI = Pierna(_piernaI, pantalon, zapato);
        _piernaD = Pivote("Pierna_D", _caderas, new Vector3(0.09f, -0.02f, 0f));
        _rodillaD = Pierna(_piernaD, pantalon, zapato);

        _globo = VisualesVR.Texto("Globo", transform, 0.055f, new Vector2(1.2f, 0.25f));
        _globo.transform.localPosition = Vector3.up * 1.05f;
        _globo.fontStyle = FontStyles.Bold;
        _globo.outlineWidth = 0.25f;
        _globo.outlineColor = Color.black;
        _globo.color = Color.white;
        _globo.text = "";

        _voz = SonidosVR.CrearBucle(_raiz, SonidosVR.Auxilio, 1f, 26f);
        _voz.loop = false;
    }

    Transform Brazo(Transform hombro, Material manga, Material piel)
    {
        Parte(PrimitiveType.Capsule, "Brazo", hombro, manga, new Vector3(0f, -0.14f, 0f), new Vector3(0.085f, 0.16f, 0.085f));
        var codo = Pivote("Codo", hombro, Vector3.down * 0.29f);
        Parte(PrimitiveType.Capsule, "Antebrazo", codo, piel, new Vector3(0f, -0.13f, 0f), new Vector3(0.07f, 0.14f, 0.07f));
        Parte(PrimitiveType.Sphere, "Mano", codo, piel, new Vector3(0f, -0.28f, 0f), new Vector3(0.08f, 0.1f, 0.05f));
        return codo;
    }

    Transform Pierna(Transform pierna, Material tela, Material zapato)
    {
        Parte(PrimitiveType.Capsule, "Muslo", pierna, tela, new Vector3(0f, -0.21f, 0f), new Vector3(0.13f, 0.23f, 0.13f));
        var rodilla = Pivote("Rodilla", pierna, Vector3.down * 0.43f);
        Parte(PrimitiveType.Capsule, "Pantorrilla", rodilla, tela, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.22f, 0.1f));
        Parte(PrimitiveType.Cube, "Zapato", rodilla, zapato, new Vector3(0f, -0.43f, 0.05f), new Vector3(0.1f, 0.07f, 0.24f));
        return rodilla;
    }

    static Transform Pivote(string nombre, Transform padre, Vector3 pos)
    {
        var t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        t.localPosition = pos;
        return t;
    }

    static void Parte(PrimitiveType tipo, string nombre, Transform padre, Material mat, Vector3 pos, Vector3 escala)
    {
        var p = VisualesVR.Primitiva(tipo, nombre, padre, mat);
        p.transform.localPosition = pos;
        // Capsula y cilindro miden 2 de alto: la escala Y es la mitad del largo.
        p.transform.localScale = escala;
    }

    // ------------------------------------------------------------------
    // Animacion por codigo
    // ------------------------------------------------------------------

    static float L(float a, float b, float k) => Mathf.Lerp(a, b, k);

    /// <summary>kParado: 0 sentada - 1 de pie. kMarcha: 0 quieta - 1 caminando. saludo: brazo en alto pidiendo ayuda.</summary>
    void Aplicar(Pose a, Pose b, float kParado, float kMarcha, float saludo)
    {
        float k = Mathf.SmoothStep(0f, 1f, kParado);
        float s = Mathf.Sin(_faseMarcha);
        float rebote = Mathf.Abs(Mathf.Cos(_faseMarcha)) * 0.02f * kMarcha;

        _caderas.localPosition = new Vector3(0f, L(a.cadera, b.cadera, k) + rebote, 0f);
        _torso.localRotation = Quaternion.Euler(L(a.torso, b.torso, k) + 3f * kMarcha, 0f, 0f);
        _cabeza.localRotation = Quaternion.Euler(L(a.cabeza, b.cabeza, k), 0f, 0f);

        float hombro = L(a.hombro, b.hombro, k);
        float codo = L(a.codo, b.codo, k);
        float pierna = L(a.pierna, b.pierna, k);
        float rodilla = L(a.rodilla, b.rodilla, k);

        // Marcha: piernas y brazos en contrafase.
        float swing = 26f * kMarcha;
        _piernaI.localRotation = Quaternion.Euler(pierna + s * swing, 0f, 0f);
        _piernaD.localRotation = Quaternion.Euler(pierna - s * swing, 0f, 0f);
        _rodillaI.localRotation = Quaternion.Euler(rodilla + Mathf.Max(0f, -s) * 34f * kMarcha, 0f, 0f);
        _rodillaD.localRotation = Quaternion.Euler(rodilla + Mathf.Max(0f, s) * 34f * kMarcha, 0f, 0f);
        _hombroI.localRotation = Quaternion.Euler(hombro - s * swing * 0.7f, 0f, -4f);
        _codoI.localRotation = Quaternion.Euler(codo, 0f, 0f);

        // El brazo derecho puede levantarse para pedir ayuda.
        float saludoAng = Mathf.Sin(Time.time * 7f) * 14f;
        float hombroD = L(hombro + s * swing * 0.7f, -155f, saludo);
        float codoD = L(codo, -18f, saludo);
        _hombroD.localRotation = Quaternion.Euler(hombroD, 0f, 4f + saludoAng * saludo);
        _codoD.localRotation = Quaternion.Euler(codoD, 0f, 0f);
    }

    // ------------------------------------------------------------------
    // Estados
    // ------------------------------------------------------------------

    /// <summary>La puerta se abrio: la persona reacciona.</summary>
    public void Liberar()
    {
        if (EstadoActual != Estado.Atrapada) return;
        EstadoActual = Estado.Reaccionando;
        Decir("¡Aquí! ¡Por favor, ayúdeme!", 3.5f);
        Gritar(1.15f);
    }

    public void MarcarSegura()
    {
        EstadoActual = Estado.Segura;
        _camino.Clear();
        Decir("¡Gracias! ¡Me salvó la vida!", 5f);
        Gritar(1.25f);
    }

    void Decir(string texto, float segundos)
    {
        _globo.text = texto;
        _finGlobo = Time.time + segundos;
    }

    void Gritar(float tono)
    {
        if (_voz == null) return;
        _voz.pitch = tono;
        _voz.PlayOneShot(SonidosVR.Auxilio, 0.9f);
    }

    void Update()
    {
        if (_jugador == null)
        {
            _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : null;
            if (_rig == null) return;
            _jugador = _rig.centerEyeAnchor;
        }

        float dt = Time.deltaTime;
        float saludo = 0f;
        float objetivoMarcha = 0f;

        switch (EstadoActual)
        {
            case Estado.Atrapada:
                saludo = PedirAyuda();
                break;

            case Estado.Reaccionando:
                saludo = 1f;
                MirarAlJugador(dt);
                if (DistanciaAlJugador() <= distanciaLevantar)
                {
                    EstadoActual = Estado.Levantandose;
                    Decir("Gracias... me cuesta caminar, voy con usted", 3.5f);
                }
                break;

            case Estado.Levantandose:
                MirarAlJugador(dt);
                _kParado = Mathf.MoveTowards(_kParado, 1f, dt / 1.6f);
                if (_kParado >= 1f)
                {
                    EstadoActual = Estado.Siguiendo;
                    _camino.Clear();
                    Decir("¡Voy detrás de usted! No vaya muy rápido", 3f);
                }
                break;

            case Estado.Siguiendo:
                objetivoMarcha = Seguir(dt);
                if (DistanciaAlJugador() > distanciaEspera)
                {
                    EstadoActual = Estado.Esperando;
                    _proximoGrito = Time.time;
                }
                break;

            case Estado.Esperando:
                MirarAlJugador(dt);
                RegistrarCamino();
                saludo = 1f;
                if (Time.time >= _proximoGrito)
                {
                    _proximoGrito = Time.time + 6f;
                    Decir("¡Espéreme! ¡No me deje aquí!", 4f);
                    Gritar(1.1f);
                }
                if (DistanciaAlJugador() < distanciaEspera - 3f) EstadoActual = Estado.Siguiendo;
                break;

            case Estado.Segura:
                MirarAlJugador(dt);
                saludo = Time.time < _finGlobo ? 1f : 0f;
                break;
        }

        _kMarcha = Mathf.MoveTowards(_kMarcha, objetivoMarcha, dt * 4f);
        _kSaludo = Mathf.MoveTowards(_kSaludo, saludo, dt * 3f);
        Aplicar(Sentada, Parada, _kParado, _kMarcha, _kSaludo);
        ActualizarGlobo();
    }

    /// <summary>Atrapada: alterna gritos y golpes en la pared para que se la pueda encontrar por sonido.</summary>
    float PedirAyuda()
    {
        if (Time.time >= _proximoGrito)
        {
            _proximoGrito = Time.time + Random.Range(5f, 8f);
            _golpesToca = !_golpesToca;
            if (_golpesToca) _voz.PlayOneShot(SonidosVR.Golpes, 1f);
            else Gritar(Random.Range(0.97f, 1.08f));
            if (DistanciaAlJugador() < 9f) Decir(_golpesToca ? "*golpes*" : "¡Ayuda! ¡Estoy aquí adentro!", 2.5f);
        }
        return Time.time < _finGlobo && !_golpesToca ? 1f : 0f;
    }

    float DistanciaAlJugador()
    {
        Vector3 d = _jugador.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void MirarAlJugador(float dt)
    {
        Vector3 d = _jugador.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 4f));
    }

    void RegistrarCamino()
    {
        Vector3 p = _jugador.position;
        p.y = transform.position.y;
        if (_camino.Count == 0 || Plano(_camino[_camino.Count - 1], p) > 0.35f) _camino.Add(p);
    }

    static float Plano(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>Avanza por las migas del jugador manteniendo la separacion. Devuelve 0-1 de marcha.</summary>
    float Seguir(float dt)
    {
        RegistrarCamino();

        // Largo del camino que falta hasta el jugador.
        float falta = 0f;
        Vector3 previo = transform.position;
        foreach (var p in _camino) { falta += Plano(previo, p); previo = p; }

        if (falta <= separacion || _camino.Count == 0)
        {
            MirarAlJugador(dt);
            SeguirPiso(dt);
            return 0f;
        }

        Vector3 objetivo = _camino[0];
        objetivo.y = transform.position.y;
        Vector3 d = objetivo - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.15f)
        {
            _camino.RemoveAt(0);
            _atascado = 0f;
            return 1f;
        }

        // Tramo tapado (teletransporte, puerta en movimiento): espera un poco y salta a esa miga.
        if (Tapado(transform.position + Vector3.up * 0.9f, d / dist, Mathf.Min(dist, 0.6f)))
        {
            _atascado += dt;
            if (_atascado > 2.5f)
            {
                transform.position = objetivo;
                _camino.RemoveAt(0);
                _atascado = 0f;
            }
            return 0f;
        }
        _atascado = 0f;

        float v = falta > 3.5f ? velocidadRapida : velocidad;
        transform.position += d / dist * Mathf.Min(v * dt, dist);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 7f));
        _faseMarcha += v * dt * 5.5f;
        SeguirPiso(dt);
        return Mathf.Clamp01(v / velocidad);
    }

    bool Tapado(Vector3 origen, Vector3 dir, float distancia)
    {
        foreach (var h in Physics.RaycastAll(origen, dir, distancia, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (_rig != null && h.collider.transform.IsChildOf(_rig.transform)) continue;
            return true;
        }
        return false;
    }

    /// <summary>Se mantiene sobre el piso (rampa de la entrada, vereda).</summary>
    void SeguirPiso(float dt)
    {
        float mejor = float.MaxValue, y = transform.position.y;
        foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 1.1f, Vector3.down, 3f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (_rig != null && h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; y = h.point.y; }
        }
        Vector3 p = transform.position;
        p.y = Mathf.MoveTowards(p.y, y, dt * 3f);
        transform.position = p;
    }

    void ActualizarGlobo()
    {
        bool visible = Time.time < _finGlobo;
        if (_globo.gameObject.activeSelf != visible) _globo.gameObject.SetActive(visible);
        if (!visible) return;
        _globo.transform.position = transform.position + Vector3.up * L(0.95f, 2.0f, _kParado);
        Vector3 mirar = _globo.transform.position - _jugador.position;
        if (mirar.sqrMagnitude > 0.001f) _globo.transform.rotation = Quaternion.LookRotation(mirar, Vector3.up);
    }
}
