using System;
using System.Collections.Generic;
using System.Text;
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
    enum Fase { Intro, Mision, Conduciendo, Completado }

    public static JuegoBomberos Instancia { get; private set; }

    [Header("Modo")]
    [Tooltip("Directo: sin tareas previas, vas al camion, subes y lo manejas. Desmarcar = mision completa (porton, chaleco, extintor).")]
    public bool modoDirecto = true;

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
    [Tooltip("Donde crear un camion de reemplazo si el objeto del camion no existe en la escena.")]
    public Vector3 posicionCamionReemplazo = new Vector3(-5.19f, 0.127f, -10.2f);

    [Header("Reglas")]
    public bool exigirPorton = true;
    [Tooltip("La mision empieza sola despues de estos segundos si no se presiona A.")]
    public float segundosIntroMax = 15f;
    public int puntosPorTarea = 100;
    public float tiempoExcelente = 120f;
    public float tiempoBueno = 210f;
    [Tooltip("Si no hay progreso en estos segundos se recuerda la tarea actual.")]
    public float recordatorioCada = 35f;
    public float campanaCada = 30f;

    public bool PermiteInteractuar => _fase != Fase.Intro;

    class Tarea
    {
        public string titulo, pista;
        public bool hecha;
        public Func<Vector3?> punto;
    }

    Fase _fase = Fase.Intro;
    readonly List<Tarea> _tareas = new List<Tarea>();
    Tarea _tPorton, _tChaleco, _tExtintor, _tSubir;
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
    string _calificacion = "";
    bool _avisoAgarreChaleco, _avisoAgarreExtintor;

    static readonly Color ColInfo = new Color(1f, 0.85f, 0.25f);
    static readonly Color ColExito = new Color(0.35f, 1f, 0.45f);
    static readonly Color ColError = new Color(1f, 0.35f, 0.3f);

    void Awake()
    {
        Instancia = this;
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

        // ---- HUD y marcador ----
        _hud = new GameObject("HUD_Bomberos").AddComponent<HudBomberos>();
        _hud.Inicializar(_cabeza);
        _marcador = new GameObject("Marcador_Objetivo").AddComponent<MarcadorObjetivo>();
        _marcador.Inicializar(_cabeza);

        // ---- Tareas ----
        if (porton != null)
        {
            _estadoPortonPrev = porton.estado;
            var pivote = porton.pivote != null ? porton.pivote : porton.transform;
            _motorPorton = SonidosVR.CrearBucle(pivote, SonidosVR.Motor, 0.5f, 30f);
            _motorPorton.pitch = 1.7f;
        }

        if (porton != null && !modoDirecto)
        {
            _tPorton = NuevaTarea("Abre el portón de la cochera",
                "Toca el botón rojo junto al portón con el dedo o la punta del control.",
                () => botonPorton != null ? botonPorton.transform.position + Vector3.up * 0.15f
                                          : (Vector3?)porton.pivote.position);
        }

        if (chaleco != null && !modoDirecto)
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
            _tChaleco = NuevaTarea("Ponte el chaleco",
                "Acerca la mano, agárralo con GRIP (o pellizca) y llévalo a tu pecho.",
                () => _agChaleco.Completado ? (Vector3?)null : ArribaDe(chaleco));
        }

        if (extintor != null && !modoDirecto)
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
            _tExtintor = NuevaTarea("Carga el extintor al camión",
                "Agárralo, llévalo junto al camión y suéltalo.",
                () => _agExtintor.Agarrado ? TechoCamion() : ArribaDe(extintor));
        }

        _tSubir = NuevaTarea(modoDirecto ? "Sube al camión y manéjalo" : "Sube al camión de bomberos",
            "Párate en el círculo junto a la puerta del conductor (o presiona A).",
            () => _camion.PuntoZonaSubida + Vector3.up * 2.45f);

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
        if (modoDirecto) IniciarMision();
        else MostrarIntro();
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

    Vector3 TechoCamion() => _camion.Techo;

    bool EnElPecho(Vector3 p)
    {
        Vector3 d = p - _cabeza.position;
        float horizontal = new Vector2(d.x, d.z).magnitude;
        return horizontal < 0.45f && d.y > -0.9f && d.y < 0.25f;
    }

    bool CercaDelCamion(Vector3 p) => _camion.ContienePunto(p, 1.1f);

    // ------------------------------------------------------------------
    // Flujo del juego
    // ------------------------------------------------------------------

    void Update()
    {
        if (_cabeza == null) return;
        VigilarPuertas();

        switch (_fase)
        {
            case Fase.Intro:
                float resta = segundosIntroMax - (Time.time - _inicioIntro);
                _hud.Pie($"Presiona A o pellizca para comenzar  (empieza solo en {Mathf.CeilToInt(Mathf.Max(0f, resta))} s)");
                if (EntradaVR.ConfirmarDown() || resta <= 0f) IniciarMision();
                break;

            case Fase.Mision:
                ActualizarMision();
                break;

            case Fase.Conduciendo:
                if (Time.time >= _proximoHud)
                {
                    _proximoHud = Time.time + 0.2f;
                    MostrarConduccion();
                }
                break;

            case Fase.Completado:
                if (!_camion.JugadorSentado && Time.time > _bloqueoReinicio &&
                    (EntradaVR.ConfirmarDown() || Input.GetKeyDown(KeyCode.R)))
                    Reiniciar();
                break;
        }
    }

    void MostrarIntro()
    {
        _hud.Mostrar("ESTACIÓN DE BOMBEROS VR",
            "<b>¡Suena la alarma! Hay una emergencia.</b>\n" +
            "Prepárate y sal en el camión lo más rápido posible.\n\n" +
            "<color=#FFD54A>Controles</color>\n" +
            "- Moverse: joystick o teletransporte\n" +
            "- Agarrar: GRIP / gatillo (o pellizco con la mano)\n" +
            "- Botones: tócalos con el dedo\n" +
            "- Bajar del camión: B (o mantén el pellizco)",
            "");
    }

    void IniciarMision()
    {
        _fase = Fase.Mision;
        _inicioMision = Time.time;
        _ultimoProgreso = Time.time;
        _proximaCampana = Time.time + campanaCada;
        if (modoDirecto)
        {
            Aviso("¡Sube al camión y manéjalo!\n<size=55%>Sigue la flecha amarilla</size>", TipoAviso.Info, 3.5f);
            return;
        }
        _luzAlarma.enabled = true;
        SonidosVR.Reproducir2D(SonidosVR.Alarma, 0.7f);
        EntradaVR.VibrarAmbas(0.8f, 0.5f, 0.3f);
        Aviso("¡EMERGENCIA!\n<size=55%>Equípate y sube al camión</size>", TipoAviso.Error, 3f);
        // Si el porton ya estaba abierto, cuenta como hecho.
        if (_tPorton != null && PortonAbierto()) CompletarTarea(_tPorton, "¡Portón abierto!");
    }

    void ActualizarMision()
    {
        float t = Time.time - _inicioMision;

        if (_tPorton != null && !_tPorton.hecha && PortonAbierto())
            CompletarTarea(_tPorton, "¡Portón abierto!");

        // Luz de alarma pulsante y campana periodica.
        _luzAlarma.intensity = 2.5f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * 6f), 2f);
        if (!modoDirecto && Time.time >= _proximaCampana)
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
        }

        if (Time.time >= _proximoHud)
        {
            _proximoHud = Time.time + 0.2f;
            if (modoDirecto) _hud.Mostrar("ESTACIÓN DE BOMBEROS VR", CuerpoMision(actual, punto), "");
            else _hud.Mostrar("¡EMERGENCIA EN CURSO!", CuerpoMision(actual, punto), $"Tiempo {FormatoTiempo(t)}     Puntaje {_puntaje}");
        }
    }

    Tarea TareaActual()
    {
        foreach (var t in _tareas) if (!t.hecha) return t;
        return null;
    }

    string CuerpoMision(Tarea actual, Vector3? punto)
    {
        var sb = new StringBuilder();
        if (actual != null)
        {
            sb.Append("<b>Objetivo:</b> <color=#FFD54A>").Append(actual.titulo).Append("</color>\n");
            sb.Append("<size=85%>").Append(actual.pista).Append("</size>\n");
            if (punto.HasValue) sb.Append("<size=85%><color=#9AD0FF>").Append(Direccion(punto.Value)).Append("</color></size>\n");
        }
        sb.Append('\n');
        foreach (var t in _tareas)
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

    void CompletarTarea(Tarea t, string mensaje)
    {
        if (t == null || t.hecha) return;
        t.hecha = true;
        _puntaje += puntosPorTarea;
        _ultimoProgreso = Time.time;
        _proximoHud = 0f;
        SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
        EntradaVR.VibrarAmbas(0.6f, 0.2f, 0.7f);

        if (t == _tSubir) return; // el cierre lo maneja Completar()

        var siguiente = TareaActual();
        string extra = siguiente == _tSubir ? "\n<size=50%>¡Equipo listo! Ahora sube al camión</size>"
                                             : $"\n<size=50%>+{puntosPorTarea} puntos</size>";
        Aviso(mensaje + extra, TipoAviso.Exito, 2.5f);
    }

    bool PuedeSubir()
    {
        if (_fase == Fase.Intro) return false;
        if (modoDirecto) return true;
        foreach (var t in _tareas)
            if (t != _tSubir && !t.hecha) return false;
        return true;
    }

    string MotivoNoPuedeSubir()
    {
        if (_fase == Fase.Intro) return "Comienza la misión primero";
        var pendientes = new List<string>();
        foreach (var t in _tareas)
            if (t != _tSubir && !t.hecha) pendientes.Add(t.titulo.ToLowerInvariant());
        return "Primero: " + string.Join(", ", pendientes);
    }

    void AlSubirCamion()
    {
        if (modoDirecto)
        {
            _fase = Fase.Conduciendo;
            _marcador.Ocultar();
            _luzAlarma.enabled = false;
            _hud.alturaRelativa = -0.55f;
            _proximoHud = 0f;
            if (porton != null && !PortonAbierto())
            {
                porton.Abrir();
                Aviso("¡A manejar!\n<size=55%>Abriendo el portón...</size>", TipoAviso.Exito, 3f);
            }
            else Aviso("¡A manejar!", TipoAviso.Exito, 2.5f);
            SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
            return;
        }
        if (_fase == Fase.Completado) { MostrarResultado(); return; }
        if (_fase != Fase.Mision) return;
        _tiempoFinal = Time.time - _inicioMision;
        CompletarTarea(_tSubir, "");
        _bono = Mathf.Max(0, Mathf.RoundToInt(tiempoBueno - _tiempoFinal)) * 2;
        _puntaje += _bono;
        _calificacion = _tiempoFinal <= tiempoExcelente ? "EXCELENTE" :
                        _tiempoFinal <= tiempoBueno ? "MUY BIEN" : "COMPLETADO";
        _fase = Fase.Completado;
        _marcador.Ocultar();
        _luzAlarma.enabled = false;

        SonidosVR.Reproducir2D(SonidosVR.Victoria, 0.9f);
        Aviso("¡MISIÓN CUMPLIDA!\n<size=55%>" + _calificacion + "</size>", TipoAviso.Exito, 4f);
        MostrarResultado();
    }

    void AlBajarCamion()
    {
        _bloqueoReinicio = Time.time + 1f;
        _hud.alturaRelativa = -0.32f;
        if (modoDirecto)
        {
            _fase = Fase.Mision;
            _ultimoProgreso = Time.time;
            _proximoHud = 0f;
            Aviso("Bajaste del camión\n<size=55%>Vuelve al círculo para manejar otra vez</size>", TipoAviso.Info, 2.5f);
            return;
        }
        if (_fase == Fase.Completado) MostrarResultado();
    }

    void MostrarResultado()
    {
        bool sentado = _camion.JugadorSentado;
        _hud.Mostrar("¡MISIÓN CUMPLIDA!",
            $"Tiempo: <b>{FormatoTiempo(_tiempoFinal)}</b>\n" +
            $"Puntaje: <b>{_puntaje}</b>  <size=80%>(bono por rapidez +{_bono})</size>\n" +
            $"Calificación: <color=#FFD54A><b>{_calificacion}</b></color>\n\n" +
            (sentado ? "La sirena está sonando: ¡en camino a la emergencia!\n\n- B (o mantén el pellizco 2 s): bajar del camión"
                     : "- A / pellizco: jugar de nuevo\n- Puedes volver a subir al camión cuando quieras"),
            sentado ? "Bajar: B" : "Jugar de nuevo: A");
    }

    float _ultimoAvisoChoque;

    void AlChocar()
    {
        if (Time.time - _ultimoAvisoChoque < 2f) return;
        _ultimoAvisoChoque = Time.time;
        Aviso("¡Cuidado, chocaste!", TipoAviso.Error, 1.2f);
    }

    void MostrarConduccion()
    {
        bool hayPorton = porton != null;
        string estadoPorton = !hayPorton ? "" :
            porton.estado == PortonElevadizo.Estado.Abierto ? "abierto" :
            porton.estado == PortonElevadizo.Estado.Abriendo ? "abriéndose..." :
            porton.estado == PortonElevadizo.Estado.Cerrando ? "cerrándose" : "cerrado";
        string marcha = _camion.Velocidad > 0.2f ? "Adelante" : _camion.Velocidad < -0.2f ? "Reversa" : "Detenido";
        _hud.Mostrar("CONDUCIENDO",
            $"Velocidad: <b><color=#FFD54A>{Mathf.RoundToInt(_camion.VelocidadKmh)} km/h</color></b>   ({marcha})\n" +
            $"Sirena: {(_camion.SirenaEncendida ? "<color=#FF6B5A>ENCENDIDA</color>" : "apagada")}" +
            (hayPorton ? $"     Portón: {estadoPorton}" : "") + "\n\n" +
            "- Stick izquierdo arriba / abajo: avanzar / retroceder\n" +
            "- Stick a los lados: girar\n" +
            "- A: sirena     - B: bajar del camión",
            "");
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
