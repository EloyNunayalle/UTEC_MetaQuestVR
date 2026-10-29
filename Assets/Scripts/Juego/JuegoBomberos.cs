using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor del juego "Emergencia en la estacion": el jugador debe abrir el porton,
/// ponerse el chaleco, cargar el extintor al camion y subir al camion de bomberos
/// lo mas rapido posible. Da feedback con HUD, avisos, flecha guia, sonidos,
/// vibracion, luz de alarma y puntaje/calificacion final.
///
/// Solo hace falta un GameObject con este componente en la escena: el resto
/// (camion interactivo, objetos agarrables, HUD, marcador) se arma solo al iniciar,
/// buscando los objetos por nombre/tipo si las referencias estan vacias.
/// </summary>
public class JuegoBomberos : MonoBehaviour
{
    public enum TipoAviso { Info, Exito, Error }
    enum Fase { Intro, Explorar, Mision, Mision2, Completado }

    public static JuegoBomberos Instancia { get; private set; }

    [Header("Referencias (vacias = se buscan solas)")]
    public OVRCameraRig rig;
    public Transform camion;
    public Transform chaleco;
    public Transform extintor;
    public PortonElevadizo porton;
    public BotonPorton botonPorton;
    public PuertaAutomatica puertaChica;

    [Header("Busqueda automatica por nombre")]
    public string nombreCamion = "Camion_Bomberos";
    public string nombreChaleco = "Chaleco";
    public string nombreExtintor = "Extintor";
    public string nombrePortonPrincipal = "Porton_Principal";

    [Header("Incendio")]
    [Tooltip("Crea un edificio en llamas cruzando la calle (ultima tarea de la mision; tambien en Explorar).")]
    public bool incendioEnEdificio = true;
    [Tooltip("Nombre del edificio que se incendia (opcional). Si hay varios con el mismo nombre se usa el mas cercano a 'Posicion Edificio Fuego'.")]
    public string nombreEdificioFuego = "";
    [Tooltip("Se incendia el edificio (raiz de escena) mas cercano a este punto: el edificio gris alto de la calle este.")]
    public Vector3 posicionEdificioFuego = new Vector3(13.96f, 0f, -27.31f);
    [Tooltip("Punto de la calle desde donde se ve la fachada en llamas (los fuegos van en la cara que mira hacia aca).")]
    public Vector3 calleFrenteEdificio = new Vector3(5.65f, 0f, -27.31f);
    [Tooltip("Donde crear un camion de reemplazo si el objeto del camion no existe en la escena.")]
    public Vector3 posicionCamionReemplazo = new Vector3(-5.19f, 0.127f, -10.2f);

    [Header("Mision 2 (incendio electrico en la casa)")]
    [Tooltip("Tiempos de referencia de la Mision 2 para la calificacion.")]
    public float tiempoExcelenteM2 = 150f;
    public float tiempoBuenoM2 = 270f;
    [Tooltip("Puntos que se restan por cada error de seguridad en la Mision 2.")]
    public int penalizacionError = 25;

    [Header("Reglas")]
    public bool exigirPorton = true;
    [Tooltip("Crea la manguera fisica con chorro de agua en el costado del camion.")]
    public bool usarManguera = true;
    public int puntosPorTarea = 100;
    public float tiempoExcelente = 120f;
    public float tiempoBueno = 210f;
    [Tooltip("Si no hay progreso en estos segundos se recuerda la tarea actual.")]
    public float recordatorioCada = 35f;
    public float campanaCada = 30f;

    public bool PermiteInteractuar => _fase != Fase.Intro;
    public bool EnMision2 => _fase == Fase.Mision2;

    /// <summary>
    /// El interior de la estación (decoración, chaleco, extintor, marcas de piso)
    /// puede descargarse cuando la misión no está en curso o cuando el equipamiento
    /// (chaleco y extintor) ya se completó: el manager deja de evaluar esas tareas.
    /// </summary>
    public bool PermiteDescargarEstacionInterior =>
        _fase != Fase.Mision ||
        ((_tChaleco == null || _tChaleco.hecha) && (_tExtintor == null || _tExtintor.hecha));

    /// <summary>Tarea de una mision (la Mision 2 tambien las usa para el HUD y la flecha).</summary>
    public class Tarea
    {
        public string titulo, pista;
        public bool hecha;
        /// <summary>Ya sumo puntos (si se deshace y se vuelve a hacer, no suma de nuevo).</summary>
        public bool puntuada;
        public Func<Vector3?> punto;
    }

    Fase _fase = Fase.Intro;
    readonly List<Tarea> _tareas = new List<Tarea>();
    Tarea _tPrincipal, _tPorton, _tChaleco, _tExtintor, _tSubir;
    PortonPrincipal _principal;
    EdificioEnLlamas _edificio;
    Tarea _tFuego;
    float _limiteBusquedaEdificio, _proximaBusquedaEdificio, _reavivarEn = -1f;
    float _limiteBusquedaPrincipal, _proximaBusquedaPrincipal;

    /// <summary>El porton de la cochera solo se puede abrir despues del porton principal.</summary>
    public bool CocheraHabilitada => _fase == Fase.Explorar || _fase == Fase.Mision2 || _tPrincipal == null || _tPrincipal.hecha;
    Transform _cabeza;
    HudBomberos _hud;
    MarcadorObjetivo _marcador;
    CamionBomberos _camion;
    ObjetoAgarrable _agChaleco, _agExtintor;
    Light _luzAlarma;
    AudioSource _motorPorton;
    PortonElevadizo.Estado _estadoPortonPrev;
    bool _puertaPrev;

    float _inicioIntro, _inicioMision, _tiempoFinal, _ultimoProgreso, _proximaCampana, _proximoHud, _bloqueoReinicio;
    int _puntaje, _bono;
    // Mision 2
    Mision2Electrica _m2;
    int _errores, _misionTerminada = 1;
    bool _incendioM1Pausado;
    string _calificacion = "";
    bool _avisoAgarreChaleco, _avisoAgarreExtintor;
    bool _equipamientoListo;
    float _proximoBusquedaEquipamiento;

    static readonly Color ColInfo = new Color(1f, 0.85f, 0.25f);
    static readonly Color ColExito = new Color(0.35f, 1f, 0.45f);
    static readonly Color ColError = new Color(1f, 0.35f, 0.3f);

    void Awake()
    {
        Instancia = this;
        AjustarCollidersGigantes();
    }

    /// <summary>
    /// Algunos modelos importados (pack de casilleros, etc.) traen escala x100 y su BoxCollider
    /// termina midiendo decenas de metros: una caja invisible que bloquea al jugador en media
    /// estacion. Aqui se ajusta cada BoxCollider desproporcionado al tamano real de su modelo.
    /// </summary>
    static void AjustarCollidersGigantes()
    {
        foreach (var c in FindObjectsOfType<BoxCollider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            var renders = c.GetComponentsInChildren<Renderer>();
            if (renders.Length == 0) continue;
            Bounds rb = renders[0].bounds;
            foreach (var r in renders) rb.Encapsulate(r.bounds);

            Vector3 cs = c.bounds.size, ms = rb.size;
            bool gigante = cs.x > ms.x * 3f + 0.5f || cs.y > ms.y * 3f + 0.5f || cs.z > ms.z * 3f + 0.5f;
            if (!gigante) continue;

            var t = c.transform;
            Vector3 antes = cs;
            c.center = t.InverseTransformPoint(rb.center);
            Vector3 ls = t.InverseTransformVector(rb.size);
            c.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            Debug.Log($"[JuegoBomberos] Collider gigante corregido en {Ruta(t)}: {antes} -> {c.bounds.size}", c);
        }
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Start()
    {
        if (rig == null) rig = FindObjectOfType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogError("[JuegoBomberos] No hay OVRCameraRig en la escena; el juego no puede iniciar.");
            enabled = false;
            return;
        }
        _cabeza = rig.centerEyeAnchor;

        if (porton == null) porton = FindObjectOfType<PortonElevadizo>();
        if (botonPorton == null) botonPorton = FindObjectOfType<BotonPorton>();
        if (puertaChica == null) puertaChica = FindObjectOfType<PuertaAutomatica>();
        if (chaleco == null) chaleco = Buscar(nombreChaleco);
        if (extintor == null) extintor = Buscar(nombreExtintor);
        if (camion == null) camion = Buscar(nombreCamion);
        if (camion == null)
        {
            camion = new GameObject(nombreCamion + " (reemplazo)").transform;
            camion.position = posicionCamionReemplazo;
        }

        // ---- Camion ----
        _camion = camion.GetComponent<CamionBomberos>();
        if (_camion == null) _camion = camion.gameObject.AddComponent<CamionBomberos>();
        _camion.Inicializar(rig, ReferenciaFrenteCamion());
        _camion.puedeSubir = PuedeSubir;
        _camion.motivoNoPuede = MotivoNoPuedeSubir;
        _camion.alSubir += AlSubirCamion;
        _camion.alBajar += AlBajarCamion;
        _camion.alChocar += AlChocar;

        // ---- Manguera con agua ----
        if (usarManguera)
        {
            var manguera = camion.GetComponent<MangueraBomberos>();
            if (manguera == null) manguera = camion.gameObject.AddComponent<MangueraBomberos>();
            manguera.Inicializar(rig, _camion);
        }

        // ---- HUD y marcador ----
        _hud = new GameObject("HUD_Bomberos").AddComponent<HudBomberos>();
        _hud.Inicializar(_cabeza);
        _marcador = new GameObject("Marcador_Objetivo").AddComponent<MarcadorObjetivo>();
        _marcador.Inicializar(_cabeza);

        // ---- Tareas ----
        // 1) Porton principal del cerco (vive en la escena de la ciudad: se engancha en cuanto aparece).
        _tPrincipal = NuevaTarea("Abre el portón principal",
            "Toca el botón verde del poste, junto al portón del patio.",
            () => _principal == null || _principal.AbiertoOAbriendo ? (Vector3?)null : _principal.PuntoBoton + Vector3.up * 0.3f);
        _limiteBusquedaPrincipal = Time.time + 10f;
        PrepararPortonPrincipal();

        // 2) Porton de la cochera.
        if (porton != null)
        {
            _tPorton = NuevaTarea("Abre el portón de la cochera",
                "Toca el botón rojo junto al portón con el dedo o la punta del control.",
                () => botonPorton != null ? botonPorton.transform.position + Vector3.up * 0.15f
                                          : (Vector3?)porton.pivote.position);
            _estadoPortonPrev = porton.estado;
            var pivote = porton.pivote != null ? porton.pivote : porton.transform;
            _motorPorton = SonidosVR.CrearBucle(pivote, SonidosVR.Motor, 0.5f, 30f);
            _motorPorton.pitch = 1.7f;
        }

        // ---- Equipamiento (vive en Estacion_Interior, cargada por proximidad) ----
        // Las tareas se crean SIEMPRE (con guardas) y los objetos se enganchan en
        // cuanto aparecen: el interior puede cargarse un frame después del Start.
        _tChaleco = NuevaTarea("Ponte el chaleco",
            "Acerca la mano, agárralo con GRIP (o pellizca) y llévalo a tu pecho.",
            () => _agChaleco == null || _agChaleco.Completado ? (Vector3?)null : ArribaDe(chaleco));

        _tExtintor = NuevaTarea("Carga el extintor al camión",
            "Agárralo, llévalo junto al camión y suéltalo.",
            () => _agExtintor == null ? (Vector3?)null
                                      : (_agExtintor.Agarrado ? TechoCamion() : ArribaDe(extintor)));

        PrepararEquipamiento();

        _tSubir = NuevaTarea("Sube al camión de bomberos",
            "Párate en el círculo junto a la puerta del conductor (o presiona A).",
            () => _camion.PuntoZonaSubida + Vector3.up * 2.45f);

        // Ultima tarea: apagar el edificio en llamas (vive en la escena de la ciudad: se engancha en cuanto aparece).
        if (incendioEnEdificio)
        {
            _tFuego = NuevaTarea("Apaga el incendio",
                "Sal por el portón y maneja hasta el edificio gris alto de la calle del costado. Bájate, toma la manguera y apunta el chorro a cada fuego; sube la presión con la válvula para los pisos altos.",
                () =>
                {
                    var z = _edificio != null ? _edificio.ZonaActivaMasCercana(_cabeza.position) : null;
                    return z == null ? (Vector3?)null : z.Centro + Vector3.up * 1.6f;
                });
            _limiteBusquedaEdificio = Time.time + 10f;
            PrepararEdificio();
        }

        if (puertaChica != null) _puertaPrev = puertaChica.EstaAbierta;

        _luzAlarma = new GameObject("Luz_Alarma").AddComponent<Light>();
        _luzAlarma.type = LightType.Point;
        _luzAlarma.color = new Color(1f, 0.1f, 0.05f);
        _luzAlarma.range = 18f;
        _luzAlarma.intensity = 0f;
        _luzAlarma.renderMode = LightRenderMode.ForcePixel;
        _luzAlarma.transform.position = _camion.Limites.center + Vector3.up * (_camion.Limites.extents.y + 1.5f);
        _luzAlarma.enabled = false;

        _inicioIntro = Time.time;
        MostrarIntro();
        CrearSelector();
    }

    Tarea NuevaTarea(string titulo, string pista, Func<Vector3?> punto)
    {
        var t = new Tarea { titulo = titulo, pista = pista, punto = punto };
        _tareas.Add(t);
        return t;
    }

    // ------------------------------------------------------------------
    // Busquedas y geometria
    // ------------------------------------------------------------------

    Transform Buscar(string nombre)
    {
        Transform empieza = null;
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (rig != null && t.IsChildOf(rig.transform)) continue;
            if (t.name == nombre) return t;
            if (empieza == null && t.name.StartsWith(nombre)) empieza = t;
        }
        return empieza;
    }

    Vector3? ReferenciaFrenteCamion()
    {
        // La bahia del camion tiene una marca "..._Frente" en el piso: el frente apunta hacia ella.
        Transform mejor = null;
        float mejorD = float.MaxValue;
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.Contains("Frente")) continue;
            Vector3 d = t.position - camion.position;
            d.y = 0f;
            if (d.magnitude < mejorD) { mejorD = d.magnitude; mejor = t; }
        }
        if (mejor != null && mejorD < 12f) return mejor.position;
        return rig.transform.position;
    }

    static Vector3 ArribaDe(Transform t)
    {
        if (VisualesVR.Limites(t, out var b)) return new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        return t.position + Vector3.up * 0.5f;
    }

    Vector3 TechoCamion()
    {
        var b = _camion.Limites;
        return new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
    }

    bool EnElPecho(Vector3 p)
    {
        Vector3 d = p - _cabeza.position;
        float horizontal = new Vector2(d.x, d.z).magnitude;
        return horizontal < 0.45f && d.y > -0.9f && d.y < 0.25f;
    }

    bool CercaDelCamion(Vector3 p)
    {
        var b = _camion.Limites;
        b.Expand(new Vector3(2.2f, 3f, 2.2f));
        return b.Contains(p);
    }

    // ------------------------------------------------------------------
    // Flujo del juego
    // ------------------------------------------------------------------

    /// <summary>
    /// Busca el chaleco y el extintor (viven en Estacion_Interior, que se carga por
    /// proximidad) y les añade la interacción en cuanto están disponibles. Se vuelve
    /// a llamar desde Update hasta encontrar ambos (por eso las guardas).
    /// </summary>
    void PrepararEdificio()
    {
        if (_edificio != null) return;

        // Edificio = raiz de escena mas cercana a la posicion configurada (hay varios con el mismo nombre).
        Transform elegido = null;
        float mejor = float.MaxValue;
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (t.parent != null) continue; // solo raices de escena (los edificios del kit)
            string n = t.name;
            bool candidato = string.IsNullOrEmpty(nombreEdificioFuego)
                ? n.StartsWith("Building") || n.StartsWith("Rowhouse") || n.Contains("Shop")
                : n == nombreEdificioFuego;
            if (!candidato) continue;
            Vector3 d = t.position - posicionEdificioFuego; d.y = 0f;
            if (d.sqrMagnitude < mejor) { mejor = d.sqrMagnitude; elegido = t; }
        }
        if (elegido == null || mejor > 15f * 15f) return; // el edificio aun no se cargo

        _edificio = elegido.GetComponent<EdificioEnLlamas>();
        if (_edificio == null) _edificio = elegido.gameObject.AddComponent<EdificioEnLlamas>();
        _edificio.alturaPiso = 3.3f;
        _edificio.Inicializar(calleFrenteEdificio); // fachada que da a la calle
        _edificio.alApagarZona += AlApagarZonaFuego;
        _edificio.alApagarTodo += AlApagarIncendio;
        Debug.Log($"[JuegoBomberos] Edificio en llamas: {elegido.name} en {elegido.position} ({_edificio.Total} focos)", elegido);
    }

    void AlApagarZonaFuego(ZonaFuego z)
    {
        if (_fase == Fase.Intro || _edificio.TodoApagado) return;
        _proximoHud = 0f;
        _ultimoProgreso = Time.time;
        SonidosVR.Reproducir2D(SonidosVR.Exito, 0.8f);
        EntradaVR.VibrarAmbas(0.5f, 0.15f, 0.7f);
        Aviso($"¡Fuego apagado!\n<size=55%>{_edificio.Apagadas} de {_edificio.Total}</size>", TipoAviso.Exito, 2f);
    }

    void AlApagarIncendio()
    {
        if (_fase == Fase.Explorar)
        {
            SonidosVR.Reproducir2D(SonidosVR.Victoria, 0.9f);
            Aviso("¡INCENDIO APAGADO!\n<size=55%>Se volverá a encender en 30 s para practicar</size>", TipoAviso.Exito, 4f);
            _reavivarEn = Time.time + 30f;
            return;
        }
        if (_fase != Fase.Mision || _tFuego == null) return;
        CompletarTarea(_tFuego, "¡Incendio apagado!");
        if (_tSubir == null || _tSubir.hecha) CompletarMision();
    }

    void PrepararPortonPrincipal()
    {
        if (_principal != null) return;
        var t = Buscar(nombrePortonPrincipal);
        if (t == null) return;
        _principal = t.GetComponent<PortonPrincipal>();
        if (_principal == null) _principal = t.gameObject.AddComponent<PortonPrincipal>();
        _principal.Inicializar(_camion.Limites.center);
    }

    void PrepararEquipamiento()
    {
        if (chaleco == null) chaleco = Buscar(nombreChaleco);
        if (extintor == null) extintor = Buscar(nombreExtintor);

        if (chaleco != null && _agChaleco == null)
        {
            _agChaleco = chaleco.gameObject.AddComponent<ObjetoAgarrable>();
            _agChaleco.esDestinoValido = EnElPecho;
            _agChaleco.alAgarrar = () =>
            {
                if (_avisoAgarreChaleco) return;
                _avisoAgarreChaleco = true;
                Aviso("Llévalo a tu pecho y suéltalo", TipoAviso.Info, 2f);
            };
            _agChaleco.alEntrarDestino = () => Aviso("¡Suéltalo para ponértelo!", TipoAviso.Info, 1.2f);
            _agChaleco.alSoltarEnDestino = () => CompletarTarea(_tChaleco, "¡Chaleco puesto!");
        }

        if (extintor != null && _agExtintor == null)
        {
            _agExtintor = extintor.gameObject.AddComponent<ObjetoAgarrable>();
            _agExtintor.radioAgarre = 0.18f;
            _agExtintor.esDestinoValido = CercaDelCamion;
            _agExtintor.alAgarrar = () =>
            {
                if (_avisoAgarreExtintor) return;
                _avisoAgarreExtintor = true;
                Aviso("Llévalo hasta el camión y suéltalo", TipoAviso.Info, 2f);
            };
            _agExtintor.alEntrarDestino = () => Aviso("¡Suéltalo para cargarlo!", TipoAviso.Info, 1.2f);
            _agExtintor.alSoltarEnDestino = () => CompletarTarea(_tExtintor, "¡Extintor cargado en el camión!");
        }

        _equipamientoListo = chaleco != null && extintor != null;
    }

    /// <summary>
    /// Llamado por GestorEscenas cuando Estacion_Interior termina de recargarse:
    /// vuelve a enganchar chaleco/extintor si sus tareas siguen pendientes
    /// (las referencias antiguas quedaron destruidas al descargar la escena).
    /// </summary>
    public void RevisarEquipamiento()
    {
        chaleco = null;
        extintor = null;
        _agChaleco = null;
        _agExtintor = null;
        _equipamientoListo = false;
        PrepararEquipamiento();
    }

    void Update()
    {
        if (_cabeza == null) return;

        if (!_equipamientoListo && Time.time >= _proximoBusquedaEquipamiento)
        {
            _proximoBusquedaEquipamiento = Time.time + 0.5f;
            PrepararEquipamiento();
        }

        if (_principal == null && _tPrincipal != null && Time.time >= _proximaBusquedaPrincipal)
        {
            _proximaBusquedaPrincipal = Time.time + 0.5f;
            PrepararPortonPrincipal();
            // Si la escena no tiene porton principal, la tarea se quita para no trabar la mision.
            if (_principal == null && Time.time > _limiteBusquedaPrincipal)
            {
                _tareas.Remove(_tPrincipal);
                _tPrincipal = null;
            }
        }

        if (_edificio == null && incendioEnEdificio && Time.time >= _proximaBusquedaEdificio)
        {
            _proximaBusquedaEdificio = Time.time + 0.5f;
            PrepararEdificio();
            // Sin edificio en la escena: la tarea se quita (la mision termina al subir al camion, como antes).
            if (_edificio == null && Time.time > _limiteBusquedaEdificio && _tFuego != null)
            {
                _tareas.Remove(_tFuego);
                _tFuego = null;
            }
        }

        VigilarPuertas();
        DetectarBloqueo();
        PedirInstrucciones();

        // Durante la Mision 2 el edificio de la Mision 1 no arde (se engancha cuando aparece).
        if (_fase == Fase.Mision2 && !_incendioM1Pausado && _edificio != null)
        {
            _edificio.Pausar(true);
            _incendioM1Pausado = true;
        }

        switch (_fase)
        {
            case Fase.Intro:
                ActualizarSelector();
                break;

            case Fase.Explorar:
                if (_reavivarEn > 0f && Time.time >= _reavivarEn && _edificio != null)
                {
                    _reavivarEn = -1f;
                    _edificio.Reavivar();
                    Aviso("¡El edificio volvió a encenderse!", TipoAviso.Error, 2.5f);
                }
                if (Time.time >= _proximoHud)
                {
                    _proximoHud = Time.time + 0.25f;
                    MostrarExplorar();
                }
                break;

            case Fase.Mision:
                ActualizarMision();
                break;

            case Fase.Mision2:
                ActualizarMision2();
                break;

            case Fase.Completado:
                // Mientras maneja, el panel muestra la velocidad en vivo.
                if (_camion.JugadorSentado && Time.time >= _proximoHud)
                {
                    _proximoHud = Time.time + 0.2f;
                    MostrarResultado();
                }
                if (!_camion.JugadorSentado && Time.time > _bloqueoReinicio &&
                    (EntradaVR.ConfirmarDown() || Input.GetKeyDown(KeyCode.R)))
                    Reiniciar();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Menu de modo: se elige con el joystick (arriba/abajo) y se acepta con A / gatillo / clic del stick
    // ------------------------------------------------------------------

    Transform _selector, _barraSel;
    TextMeshPro _flechaSel, _descripcionSel;
    readonly List<TextMeshPro> _opcionesSel = new List<TextMeshPro>();
    float _inicioSelector;
    int _indiceSel;
    bool _stickListo;
    readonly List<GameObject> _locomocionMenu = new List<GameObject>();

    static readonly string[] OpcionesMenu = { "EXPLORAR", "MISIÓN 1", "MISIÓN 2" };
    static readonly string[] DescripcionesMenu =
    {
        "Recorre todo libremente, sin tareas ni tiempo",
        "Emergencia en la estación: portones, equipo, camión y edificio en llamas",
        "Incendio eléctrico en una casa: corta la luz, comprueba y apaga con CO<sub>2</sub>"
    };
    static readonly Color[] ColoresMenu = { new Color(0.35f, 0.65f, 1f), new Color(1f, 0.4f, 0.32f), new Color(1f, 0.7f, 0.15f) };
    const float AltoFila = 0.11f;

    void CrearSelector()
    {
        _selector = new GameObject("Selector_Modo").transform;
        _inicioSelector = Time.time;
        _indiceSel = 0;
        _stickListo = false;

        var fondo = VisualesVR.Primitiva(PrimitiveType.Quad, "Fondo", _selector, VisualesVR.MaterialSinLuz(new Color(0.05f, 0.05f, 0.08f, 0.85f)));
        fondo.transform.localPosition = new Vector3(0f, 0.02f, 0.02f);
        fondo.transform.localScale = new Vector3(0.95f, 0.66f, 1f);

        var titulo = VisualesVR.Texto("Titulo", _selector, 0.05f, new Vector2(0.9f, 0.08f));
        titulo.transform.localPosition = new Vector3(0f, 0.27f, 0f);
        titulo.fontStyle = FontStyles.Bold;
        titulo.text = "ELIGE UN MODO";

        _barraSel = VisualesVR.Primitiva(PrimitiveType.Quad, "Seleccion", _selector, VisualesVR.MaterialSinLuz(new Color(1f, 1f, 1f, 0.15f))).transform;
        _barraSel.localScale = new Vector3(0.78f, AltoFila * 0.9f, 1f);

        for (int i = 0; i < OpcionesMenu.Length; i++)
        {
            var t = VisualesVR.Texto("Opcion_" + i, _selector, 0.045f, new Vector2(0.7f, AltoFila), TextAlignmentOptions.Left);
            t.transform.localPosition = new Vector3(0.04f, FilaY(i), 0f);
            t.fontStyle = FontStyles.Bold;
            t.text = OpcionesMenu[i];
            _opcionesSel.Add(t);
        }

        _flechaSel = VisualesVR.Texto("Flecha", _selector, 0.05f, new Vector2(0.08f, AltoFila));
        _flechaSel.fontStyle = FontStyles.Bold;
        _flechaSel.text = ">";
        _flechaSel.color = new Color(1f, 0.85f, 0.25f);

        _descripcionSel = VisualesVR.Texto("Descripcion", _selector, 0.026f, new Vector2(0.88f, 0.08f));
        _descripcionSel.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        _descripcionSel.color = new Color(0.85f, 0.85f, 0.85f);

        var ayuda = VisualesVR.Texto("Ayuda", _selector, 0.022f, new Vector2(0.88f, 0.05f));
        ayuda.transform.localPosition = new Vector3(0f, -0.27f, 0f);
        ayuda.color = new Color(1f, 0.85f, 0.3f);
        ayuda.text = "Joystick arriba / abajo: elegir     A, gatillo o clic del joystick: aceptar";

        // Mientras el menu esta abierto el joystick elige opciones: no mueve al jugador.
        _locomocionMenu.Clear();
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Locomot") || !t.gameObject.activeSelf) continue;
            if (_cabeza.IsChildOf(t)) continue;
            _locomocionMenu.Add(t.gameObject);
        }
        foreach (var go in _locomocionMenu) go.SetActive(false);

        ActualizarMenu(false);
        ColocarSelector();
    }

    static float FilaY(int i) => 0.13f - i * AltoFila;

    void ActualizarMenu(bool conSonido)
    {
        for (int i = 0; i < _opcionesSel.Count; i++)
        {
            bool sel = i == _indiceSel;
            _opcionesSel[i].color = sel ? ColoresMenu[i] : new Color(0.6f, 0.6f, 0.6f);
            _opcionesSel[i].fontSize = (sel ? 0.05f : 0.042f) * 10f;
        }
        _barraSel.localPosition = new Vector3(0f, FilaY(_indiceSel), 0.01f);
        _flechaSel.transform.localPosition = new Vector3(-0.38f, FilaY(_indiceSel), 0f);
        _descripcionSel.text = DescripcionesMenu[_indiceSel];
        if (conSonido)
        {
            SonidosVR.Reproducir2D(SonidosVR.Clic, 0.8f);
            EntradaVR.VibrarAmbas(0.25f, 0.04f, 0.8f);
        }
    }

    /// <summary>Frente al jugador, a 0.9 m y un poco debajo de la vista, mirandolo.</summary>
    void ColocarSelector()
    {
        Vector3 f = _cabeza.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        f.Normalize();
        Vector3 pos = _cabeza.position + f * 0.9f + Vector3.up * -0.12f;
        _selector.SetPositionAndRotation(pos, Quaternion.LookRotation(f, Vector3.up)); // +Z lejos del jugador
    }

    void ActualizarSelector()
    {
        if (_selector == null) return;
        // Los primeros segundos la pose del visor se esta ubicando: se recoloca seguido.
        // Despues, solo si el jugador se aleja o deja de mirarlo.
        Vector3 a = _selector.position - _cabeza.position; a.y = 0f;
        Vector3 f = _cabeza.forward; f.y = 0f;
        if (Time.time - _inicioSelector < 2f || a.magnitude > 2f || Vector3.Angle(a, f) > 60f)
            ColocarSelector();

        // Joystick (cualquiera de los dos) o flechas del teclado: una opcion por movimiento.
        float y = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).y +
                  OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y = 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y = -1f;
        if (Mathf.Abs(y) < 0.3f) _stickListo = true;
        if (_stickListo && Mathf.Abs(y) > 0.65f)
        {
            _stickListo = false;
            _indiceSel = (_indiceSel + (y > 0f ? -1 : 1) + OpcionesMenu.Length) % OpcionesMenu.Length;
            ActualizarMenu(true);
        }

        // La flecha late para que se note cual esta elegida.
        _flechaSel.transform.localPosition = new Vector3(-0.38f + 0.012f * Mathf.Sin(Time.time * 6f), FilaY(_indiceSel), 0f);

        if (Time.time - _inicioSelector < 0.6f) return; // evita aceptar sin querer al empezar
        bool aceptar = EntradaVR.ConfirmarDown() ||
                       OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                       OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                       OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch) ||
                       OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (!aceptar) return;
        if (_indiceSel == 0) ElegirExplorar();
        else if (_indiceSel == 1) ElegirMision();
        else ElegirMision2();
    }

    void CerrarSelector()
    {
        if (_selector != null) Destroy(_selector.gameObject);
        _selector = null;
        _opcionesSel.Clear();
        foreach (var go in _locomocionMenu) if (go != null) go.SetActive(true);
        _locomocionMenu.Clear();
    }

    /// <summary>Clic de cualquier joystick (o tecla I): muestra u oculta las instrucciones.</summary>
    void PedirInstrucciones()
    {
        if (_fase == Fase.Intro) return;
        bool clic = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch) ||
                    OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch) ||
                    Input.GetKeyDown(KeyCode.I);
        if (!clic) return;
        _proximoHud = 0f;
        if (_hud.Visible) _hud.Ocultar();
        else _hud.MostrarPor(10f);
    }

    void ElegirExplorar()
    {
        if (_fase != Fase.Intro) return;
        CerrarSelector();
        _fase = Fase.Explorar;
        _marcador.Ocultar();
        _proximoHud = 0f;
        SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
        Aviso("MODO EXPLORAR\n<size=55%>Presiona el joystick para ver qué puedes probar</size>", TipoAviso.Info, 3f);
        _hud.MostrarPor(8f);
        // La casa de la Mision 2 queda con su incendio electrico para practicar (sin tareas ni puntaje).
        if (BuscarMision2()) _m2.Iniciar(this, true);
    }

    void ElegirMision()
    {
        if (_fase != Fase.Intro) return;
        CerrarSelector();
        IniciarMision();
    }

    void MostrarExplorar()
    {
        if (_camion.JugadorSentado)
        {
            _hud.Mostrar("MODO EXPLORAR - CONDUCIENDO",
                "<b>¡A manejar!</b>  Stick izquierdo: avanzar / retroceder\n" +
                "Stick a los lados: girar   -   B: bajar\n" +
                "A: sirena   -   X: luces",
                $"Velocidad {Mathf.RoundToInt(_camion.VelocidadKmh)} km/h     " +
                $"Sirena {(_camion.SirenaEncendida ? "ON" : "OFF")}     Luces {(_camion.LucesEncendidas ? "ON" : "OFF")}");
            return;
        }
        _hud.Mostrar("MODO EXPLORAR",
            "Recorre la estación libremente: sin tareas, sin orden y sin tiempo.\n\n" +
            "<color=#FFD54A>Puedes probar</color>\n" +
            "- Botón verde del poste: portón principal\n" +
            "- Botón rojo: portón de la cochera\n" +
            "- Manguera: en el panel del camión (GRIP y gatillo)\n" +
            "- Chaleco y extintor: agárralos con GRIP\n" +
            "- Camión: párate en el círculo amarillo para manejar\n" +
            (_edificio != null ? $"- Edificio en llamas (calle del costado): {_edificio.Apagadas}/{_edificio.Total} apagados\n" : "") +
            (_m2 != null ? "- Casa de ladrillo (calle este): incendio eléctrico de práctica\n  " + _m2.EstadoTexto + "\n" : "") +
            "- Válvula del panel del camión: sube la presión del agua",
            "");
    }

    void MostrarIntro()
    {
        _hud.Mostrar("ESTACIÓN DE BOMBEROS VR",
            "<b>Elige un modo con el joystick y acepta con A o el gatillo:</b>\n" +
            "- <color=#5AA9FF>EXPLORAR</color>: recorre todo libremente, sin tareas.\n" +
            "- <color=#FF6B5A>MISIÓN 1</color>: ¡emergencia! Portón principal, cochera, chaleco,\n  extintor, sal en el camión y apaga el edificio en llamas.\n" +
            "- <color=#FFB020>MISIÓN 2</color>: incendio eléctrico en una casa: corta la corriente,\n  comprueba que no hay tensión y apaga el fuego con CO<sub>2</sub>.\n\n" +
            "<color=#FFD54A>Controles</color>\n" +
            "- Moverse: joystick o teletransporte\n" +
            "- Agarrar: GRIP / gatillo (o pellizco con la mano)\n" +
            "- Botones: tócalos con el dedo\n" +
            "- Camión: joystick para manejar (B para bajar)\n" +
            "- Camión: A sirena, X luces (independientes)\n" +
            "- Manguera: GRIP para tomar, gatillo para el agua; presión con la válvula del panel",
            "");
    }

    void IniciarMision()
    {
        _fase = Fase.Mision;
        _inicioMision = Time.time;
        _ultimoProgreso = Time.time;
        _proximaCampana = Time.time + campanaCada;
        _luzAlarma.enabled = true;
        SonidosVR.Reproducir2D(SonidosVR.Alarma, 0.7f);
        EntradaVR.VibrarAmbas(0.8f, 0.5f, 0.3f);
        Aviso("¡EMERGENCIA!\n<size=55%>Equípate y sube al camión</size>", TipoAviso.Error, 3f);
        _hud.MostrarPor(8f);
        // Si el porton ya estaba abierto, cuenta como hecho.
        if (_tPrincipal != null && _principal != null && _principal.AbiertoOAbriendo) CompletarTarea(_tPrincipal, "¡Portón principal abierto!");
        if (_tPorton != null && PortonAbierto()) CompletarTarea(_tPorton, "¡Portón abierto!");
    }

    void ActualizarMision()
    {
        float t = Time.time - _inicioMision;
        if (_tFuego != null && _edificio != null)
            _tFuego.titulo = $"Apaga el incendio ({_edificio.Apagadas}/{_edificio.Total})";

        if (_tPrincipal != null && !_tPrincipal.hecha && _principal != null && _principal.AbiertoOAbriendo)
            CompletarTarea(_tPrincipal, "¡Portón principal abierto!");

        if (_tPorton != null && !_tPorton.hecha && PortonAbierto())
            CompletarTarea(_tPorton, "¡Portón abierto!");

        // Luz de alarma pulsante y campana periodica.
        _luzAlarma.intensity = 2.5f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * 6f), 2f);
        if (Time.time >= _proximaCampana)
        {
            _proximaCampana = Time.time + campanaCada;
            SonidosVR.Reproducir2D(SonidosVR.Campana, 0.35f);
        }

        var actual = TareaActual();
        Vector3? punto = actual?.punto();
        if (punto.HasValue) _marcador.Senalar(punto.Value);
        else _marcador.Ocultar();

        if (actual != null && Time.time - _ultimoProgreso > recordatorioCada)
        {
            _ultimoProgreso = Time.time;
            Aviso(actual.titulo + "\n<size=55%>" + actual.pista + "</size>", TipoAviso.Info, 4f);
            EntradaVR.VibrarAmbas(0.3f, 0.1f);
            _hud.MostrarPor(6f);
        }

        if (Time.time >= _proximoHud)
        {
            _proximoHud = Time.time + 0.2f;
            _hud.Mostrar("¡EMERGENCIA EN CURSO!", CuerpoMision(actual, punto), $"Tiempo {FormatoTiempo(t)}     Puntaje {_puntaje}");
        }
    }

    Tarea TareaActual()
    {
        foreach (var t in _tareas) if (!t.hecha) return t;
        return null;
    }

    string CuerpoMision(Tarea actual, Vector3? punto) => CuerpoMision(actual, punto, _tareas);

    string CuerpoMision(Tarea actual, Vector3? punto, IReadOnlyList<Tarea> tareas)
    {
        var sb = new StringBuilder();
        if (actual != null)
        {
            sb.Append("<b>Objetivo:</b> <color=#FFD54A>").Append(actual.titulo).Append("</color>\n");
            sb.Append("<size=85%>").Append(actual.pista).Append("</size>\n");
            if (punto.HasValue) sb.Append("<size=85%><color=#9AD0FF>").Append(Direccion(punto.Value)).Append("</color></size>\n");
        }
        sb.Append('\n');
        foreach (var t in tareas)
        {
            if (t.hecha) sb.Append("<color=#6BFF7A>[OK]  ").Append(t.titulo).Append("</color>\n");
            else if (t == actual) sb.Append("<color=#FFD54A>[  ]  <b>").Append(t.titulo).Append("</b></color>\n");
            else sb.Append("<color=#BBBBBB>[  ]  ").Append(t.titulo).Append("</color>\n");
        }
        return sb.ToString();
    }

    string Direccion(Vector3 punto)
    {
        Vector3 f = _cabeza.forward; f.y = 0f;
        Vector3 d = punto - _cabeza.position; d.y = 0f;
        float dist = d.magnitude;
        if (dist < 1.5f) return "¡Está aquí, muy cerca!";
        float ang = Vector3.SignedAngle(f, d, Vector3.up);
        string lado = Mathf.Abs(ang) < 35f ? "al frente" :
                      Mathf.Abs(ang) > 135f ? "detrás de ti" :
                      ang > 0f ? "a tu derecha" : "a tu izquierda";
        return $"Está {lado}, a {Mathf.RoundToInt(dist)} m (sigue la flecha amarilla)";
    }

    bool PortonAbierto() =>
        porton != null && (porton.estado == PortonElevadizo.Estado.Abriendo || porton.estado == PortonElevadizo.Estado.Abierto);

    public void CompletarTarea(Tarea t, string mensaje)
    {
        if (t == null || t.hecha) return;
        if (_fase == Fase.Explorar)
        {
            // Sin puntaje ni orden: solo el feedback de lo que hiciste.
            t.hecha = true;
            SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
            EntradaVR.VibrarAmbas(0.6f, 0.2f, 0.7f);
            if (!string.IsNullOrEmpty(mensaje)) Aviso(mensaje, TipoAviso.Exito, 2f);
            return;
        }
        t.hecha = true;
        bool suma = !t.puntuada;
        if (suma) _puntaje += puntosPorTarea;
        t.puntuada = true;
        _ultimoProgreso = Time.time;
        _proximoHud = 0f;
        _hud.MostrarPor(6f); // la siguiente tarea se ve un momento
        SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
        EntradaVR.VibrarAmbas(0.6f, 0.2f, 0.7f);

        if (t == _tSubir) return; // el cierre lo maneja Completar()

        if (_fase == Fase.Mision2)
        {
            var sig = _m2 != null ? PrimeraPendiente(_m2.Tareas) : null;
            Aviso(mensaje + (sig != null ? $"\n<size=50%>Ahora: {sig.titulo}</size>" : "") +
                  (suma ? $"\n<size=45%>+{puntosPorTarea} puntos</size>" : ""), TipoAviso.Exito, 2.5f);
            return;
        }

        var siguiente = TareaActual();
        string extra = siguiente == _tSubir ? "\n<size=50%>¡Equipo listo! Ahora sube al camión</size>"
                                             : $"\n<size=50%>+{puntosPorTarea} puntos</size>";
        Aviso(mensaje + extra, TipoAviso.Exito, 2.5f);
    }

    bool PuedeSubir()
    {
        if (_fase == Fase.Intro) return false;
        if (_fase == Fase.Explorar || _fase == Fase.Mision2) return true;
        // Solo cuentan las tareas anteriores a subir al camion (el incendio se apaga despues).
        foreach (var t in _tareas)
        {
            if (t == _tSubir) break;
            if (!t.hecha) return false;
        }
        return true;
    }

    string MotivoNoPuedeSubir()
    {
        if (_fase == Fase.Intro) return "Primero elige un modo";
        var pendientes = new List<string>();
        foreach (var t in _tareas)
        {
            if (t == _tSubir) break;
            if (!t.hecha) pendientes.Add(t.titulo.ToLowerInvariant());
        }
        return "Primero: " + string.Join(", ", pendientes);
    }

    void AlSubirCamion()
    {
        if (_fase == Fase.Explorar || _fase == Fase.Mision2)
        {
            _hud.alturaRelativa = -0.55f;
            _proximoHud = 0f;
            _hud.MostrarPor(6f);
            Aviso("¡A manejar!", TipoAviso.Exito, 2f);
            return;
        }
        if (_fase == Fase.Completado) { MostrarResultado(); return; }
        if (_fase != Fase.Mision) return;
        CompletarTarea(_tSubir, "");
        _hud.alturaRelativa = -0.55f;

        // Sin incendio (o ya apagado): la mision termina al subir, como antes.
        if (_tFuego == null || _tFuego.hecha) { CompletarMision(); return; }
        Aviso("¡Al camión!\n<size=55%>Sigue la flecha hasta el edificio en llamas</size>", TipoAviso.Exito, 3.5f);
    }

    void CompletarMision()
    {
        if (_fase != Fase.Mision) return;
        _tiempoFinal = Time.time - _inicioMision;
        // Con incendio la mision es mas larga: los tiempos de referencia se amplian.
        float extra = _tFuego != null ? 180f : 0f;
        _bono = Mathf.Max(0, Mathf.RoundToInt(tiempoBueno + extra - _tiempoFinal)) * 2;
        _puntaje += _bono;
        _calificacion = _tiempoFinal <= tiempoExcelente + extra ? "EXCELENTE" :
                        _tiempoFinal <= tiempoBueno + extra ? "MUY BIEN" : "COMPLETADO";
        _fase = Fase.Completado;
        _marcador.Ocultar();
        _luzAlarma.enabled = false;
        _hud.alturaRelativa = _camion.JugadorSentado ? -0.55f : -0.32f;

        SonidosVR.Reproducir2D(SonidosVR.Victoria, 0.9f);
        Aviso("¡MISIÓN CUMPLIDA!\n<size=55%>" + _calificacion + "</size>", TipoAviso.Exito, 4f);
        MostrarResultado();
        _hud.MostrarPor(20f);
    }

    void AlBajarCamion()
    {
        _bloqueoReinicio = Time.time + 1f;
        _hud.alturaRelativa = -0.32f;
        _proximoHud = 0f;
        if (_fase == Fase.Completado) { MostrarResultado(); _hud.MostrarPor(10f); }
    }

    void MostrarResultado()
    {
        bool sentado = _camion.JugadorSentado;
        _hud.Mostrar(_misionTerminada == 2 ? "¡MISIÓN 2 CUMPLIDA! ÁREA ASEGURADA" : "¡MISIÓN CUMPLIDA!",
            $"Tiempo: <b>{FormatoTiempo(_tiempoFinal)}</b>\n" +
            $"Puntaje: <b>{_puntaje}</b>  <size=80%>(bono por rapidez +{_bono})</size>\n" +
            (_misionTerminada == 2 ? $"Errores de seguridad: <b>{_errores}</b>  <size=80%>(-{penalizacionError} c/u)</size>\n" : "") +
            $"Calificación: <color=#FFD54A><b>{_calificacion}</b></color>\n\n" +
            (sentado ? "<b>¡A manejar!</b>  Stick izquierdo: avanzar / retroceder\n" +
                       "Stick a los lados: girar   -   B: bajar\n" +
                       "A: sirena   -   X: luces"
                     : "- A / pellizco: jugar de nuevo\n- Puedes volver a subir al camión para manejar\n- Clic del joystick: mostrar / ocultar este panel"),
            sentado ? $"Velocidad {Mathf.RoundToInt(_camion.VelocidadKmh)} km/h     " +
                      $"Sirena {(_camion.SirenaEncendida ? "ON" : "OFF")}     Luces {(_camion.LucesEncendidas ? "ON" : "OFF")}"
                    : "Jugar de nuevo: A");
    }

    // ------------------------------------------------------------------
    // Mision 2: incendio electrico en la casa (Mision2Electrica en el prefab de la casa)
    // ------------------------------------------------------------------

    bool BuscarMision2()
    {
        if (_m2 == null) _m2 = FindObjectOfType<Mision2Electrica>();
        return _m2 != null;
    }

    void ElegirMision2()
    {
        if (_fase != Fase.Intro) return;
        if (!BuscarMision2())
        {
            Aviso("No se encontró la casa de la Misión 2\n<size=55%>Revisa que la escena de la ciudad esté cargada</size>", TipoAviso.Error, 3f);
            return;
        }
        CerrarSelector();
        IniciarMision2();
    }

    void IniciarMision2()
    {
        _fase = Fase.Mision2;
        _inicioMision = Time.time;
        _ultimoProgreso = Time.time;
        _puntaje = 0;
        _errores = 0;
        _proximoHud = 0f;
        if (_edificio != null) { _edificio.Pausar(true); _incendioM1Pausado = true; }
        _m2.Iniciar(this, false);
        MoverJugador(_m2.InicioJugador, _m2.InicioAdelante);
        SonidosVR.Reproducir2D(SonidosVR.Alarma, 0.7f);
        EntradaVR.VibrarAmbas(0.8f, 0.5f, 0.3f);
        Aviso("¡EMERGENCIA ELÉCTRICA!\n<size=55%>Un equipo se incendia dentro de la casa. Entra con cuidado.</size>", TipoAviso.Error, 4f);
        _hud.MostrarPor(8f);
    }

    void ActualizarMision2()
    {
        float t = Time.time - _inicioMision;
        var tareas = _m2.Tareas;
        var actual = PrimeraPendiente(tareas);
        Vector3? punto = actual?.punto();
        if (punto.HasValue) _marcador.Senalar(punto.Value);
        else _marcador.Ocultar();

        if (actual != null && Time.time - _ultimoProgreso > recordatorioCada)
        {
            _ultimoProgreso = Time.time;
            Aviso(actual.titulo + "\n<size=55%>" + actual.pista + "</size>", TipoAviso.Info, 4f);
            EntradaVR.VibrarAmbas(0.3f, 0.1f);
            _hud.MostrarPor(6f);
        }

        if (Time.time >= _proximoHud)
        {
            _proximoHud = Time.time + 0.2f;
            _hud.Mostrar("MISIÓN 2: INCENDIO ELÉCTRICO",
                CuerpoMision(actual, punto, tareas) + "\n<size=85%>" + _m2.EstadoTexto + "</size>",
                $"Tiempo {FormatoTiempo(t)}     Puntaje {_puntaje}     Errores {_errores}");
        }
    }

    static Tarea PrimeraPendiente(IReadOnlyList<Tarea> tareas)
    {
        if (tareas == null) return null;
        foreach (var t in tareas) if (!t.hecha) return t;
        return null;
    }

    /// <summary>Error de seguridad (Mision 2): resta puntos al final, con sonido, vibracion y aviso rojo.</summary>
    public void RegistrarError(string mensaje)
    {
        if (_fase != Fase.Mision2) { Aviso(mensaje, TipoAviso.Error, 3f); return; }
        _errores++;
        _proximoHud = 0f;
        SonidosVR.Reproducir2D(SonidosVR.Error, 0.9f);
        EntradaVR.VibrarAmbas(0.7f, 0.25f, 0.3f);
        Aviso(mensaje + $"\n<size=45%>Error de seguridad (-{penalizacionError})</size>", TipoAviso.Error, 3.5f);
    }

    /// <summary>Fin de la Mision 2 (area asegurada): tiempo, puntaje con bono y errores, calificacion.</summary>
    public void CompletarMision2()
    {
        if (_fase != Fase.Mision2) return;
        _tiempoFinal = Time.time - _inicioMision;
        _bono = Mathf.Max(0, Mathf.RoundToInt(tiempoBuenoM2 - _tiempoFinal)) * 2;
        _puntaje = Mathf.Max(0, _puntaje + _bono - _errores * penalizacionError);
        _calificacion = _tiempoFinal <= tiempoExcelenteM2 && _errores == 0 ? "EXCELENTE" :
                        _tiempoFinal <= tiempoBuenoM2 && _errores <= 1 ? "MUY BIEN" : "COMPLETADO";
        _misionTerminada = 2;
        _fase = Fase.Completado;
        _bloqueoReinicio = Time.time + 3f; // que el A/X de la mision no reinicie sin querer
        _marcador.Ocultar();
        SonidosVR.Reproducir2D(SonidosVR.Victoria, 0.9f);
        Aviso("¡ÁREA ASEGURADA!\n<size=55%>Misión 2 cumplida - " + _calificacion + "</size>", TipoAviso.Exito, 4f);
        MostrarResultado();
        _hud.MostrarPor(20f);
    }

    /// <summary>Lleva al jugador a un punto (pies) mirando hacia una direccion, y avisa al locomotor.</summary>
    void MoverJugador(Vector3 pies, Vector3 adelante)
    {
        var rigT = rig.transform;
        float alturaRig = rigT.position.y - SueloBajo(_cabeza.position, rigT.position.y);
        adelante.y = 0f;
        if (adelante.sqrMagnitude > 0.001f)
        {
            float yaw = Quaternion.LookRotation(adelante.normalized, Vector3.up).eulerAngles.y;
            rigT.RotateAround(_cabeza.position, Vector3.up, Mathf.DeltaAngle(_cabeza.eulerAngles.y, yaw));
        }
        Vector3 d = pies - _cabeza.position; d.y = 0f;
        Vector3 nueva = rigT.position + d;
        nueva.y = pies.y + alturaRig;
        rigT.position = nueva;
        CamionBomberos.TeletransportarLocomotor(new Vector3(pies.x, nueva.y, pies.z), Quaternion.Euler(0f, _cabeza.eulerAngles.y, 0f));
    }

    /// <summary>Altura del piso bajo un punto (ignora al jugador y la manguera).</summary>
    float SueloBajo(Vector3 desde, float porDefecto)
    {
        float mejor = float.MaxValue, y = porDefecto;
        foreach (var h in Physics.RaycastAll(desde, Vector3.down, 4f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(rig.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; y = h.point.y; }
        }
        return y;
    }

    float _ultimoAvisoChoque;

    // ------------------------------------------------------------------
    // Diagnostico: si el jugador empuja el stick y no avanza, dice que lo bloquea.
    // ------------------------------------------------------------------

    Vector3 _posBloqueo;
    float _tiempoBloqueo;
    readonly HashSet<Collider> _bloqueosAvisados = new HashSet<Collider>();

    void DetectarBloqueo()
    {
        if (_camion != null && _camion.JugadorSentado) { _tiempoBloqueo = 0f; return; }
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector3 pos = _cabeza.position; pos.y = 0f;
        if (stick.magnitude < 0.6f) { _tiempoBloqueo = 0f; _posBloqueo = pos; return; }

        if (_tiempoBloqueo == 0f) _posBloqueo = pos;
        _tiempoBloqueo += Time.deltaTime;
        if (_tiempoBloqueo < 0.6f) return;
        bool atascado = Vector3.Distance(pos, _posBloqueo) < 0.1f;
        _tiempoBloqueo = 0f;
        if (!atascado) return;

        // Direccion en la que intenta caminar.
        Vector3 f = _cabeza.forward; f.y = 0f; f.Normalize();
        Vector3 r = _cabeza.right; r.y = 0f; r.Normalize();
        Vector3 dir = (f * stick.y + r * stick.x).normalized;

        float pies = rig.transform.position.y;
        Vector3 a = new Vector3(_cabeza.position.x, pies + 0.6f, _cabeza.position.z);
        Vector3 b = new Vector3(_cabeza.position.x, Mathf.Max(pies + 0.65f, _cabeza.position.y - 0.1f), _cabeza.position.z);
        var hits = Physics.CapsuleCastAll(a, b, 0.3f, dir, 0.6f, ~0, QueryTriggerInteraction.Ignore);
        var cerca = Physics.OverlapCapsule(a, b, 0.35f, ~0, QueryTriggerInteraction.Ignore);
        var candidatos = new List<Collider>();
        foreach (var h in hits) candidatos.Add(h.collider);
        candidatos.AddRange(cerca);

        foreach (var c in candidatos)
        {
            if (c == null || c.transform.IsChildOf(rig.transform)) continue;
            if (c.gameObject.layer == MangueraBomberos.CapaManguera) continue; // la manguera en el piso no cuenta
            if (!_bloqueosAvisados.Add(c)) continue;
            string ruta = Ruta(c.transform);
            Debug.LogWarning($"[JuegoBomberos] Bloqueado por: {ruta} ({c.GetType().Name}, limites {c.bounds.center} tam {c.bounds.size})", c);
            Aviso("Te bloquea:\n<size=55%>" + ruta + "</size>", TipoAviso.Error, 4f);
            return;
        }
    }

    static string Ruta(Transform t)
    {
        string ruta = t.name;
        for (var p = t.parent; p != null; p = p.parent) ruta = p.name + "/" + ruta;
        return ruta;
    }

    void AlChocar()
    {
        if (Time.time - _ultimoAvisoChoque < 2f) return;
        _ultimoAvisoChoque = Time.time;
        Aviso("¡Cuidado, chocaste!", TipoAviso.Error, 1.2f);
    }

    void Reiniciar()
    {
        var escena = SceneManager.GetActiveScene();
        if (escena.buildIndex >= 0) SceneManager.LoadScene(escena.buildIndex);
        else SceneManager.LoadScene(escena.name);
    }

    // ------------------------------------------------------------------
    // Feedback del entorno (porton y puerta automatica)
    // ------------------------------------------------------------------

    void VigilarPuertas()
    {
        if (porton != null && porton.estado != _estadoPortonPrev)
        {
            var e = porton.estado;
            bool moviendo = e == PortonElevadizo.Estado.Abriendo || e == PortonElevadizo.Estado.Cerrando;
            if (moviendo && !_motorPorton.isPlaying) _motorPorton.Play();
            if (!moviendo)
            {
                _motorPorton.Stop();
                SonidosVR.ReproducirEn(SonidosVR.Puerta, _motorPorton.transform.position, 1f, 30f);
            }
            _estadoPortonPrev = e;
        }

        if (puertaChica != null && puertaChica.EstaAbierta != _puertaPrev)
        {
            _puertaPrev = puertaChica.EstaAbierta;
            SonidosVR.ReproducirEn(SonidosVR.Puerta, puertaChica.pivote.position + Vector3.up * 1f, 0.8f, 15f);
        }
    }

    // ------------------------------------------------------------------

    public void Aviso(string texto, TipoAviso tipo, float duracion = 2.5f)
    {
        if (_hud == null) return;
        _hud.Aviso(texto, tipo == TipoAviso.Exito ? ColExito : tipo == TipoAviso.Error ? ColError : ColInfo, duracion);
    }

    static string FormatoTiempo(float s)
    {
        int m = (int)(s / 60f);
        int sec = (int)(s % 60f);
        return $"{m:00}:{sec:00}";
    }
}
