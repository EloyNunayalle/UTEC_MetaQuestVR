using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Mision 3 - Rescate de una persona atrapada. Controla la secuencia completa:
/// HUB -> tomar la barra -> cargarla en la maletera del camion EXISTENTE -> abrir portones -> abordar ->
/// conducir hasta la casa -> bajar y sacar la barra -> entrar y encontrar la puerta -> forzarla ->
/// acompanar a la persona hasta la zona segura -> fin (vuelve al HUB con el flujo de siempre).
///
/// Lo crea JuegoBomberos al elegir la mision: vive en la escena persistente, porque el interior de la
/// casa (Casa_M3_Interior) se carga y descarga por proximidad. El progreso se guarda aqui, y cada vez
/// que el interior aparece se presenta con EngancharEscenario.
/// No crea ningun camion ni teletransporta al jugador: usa el camion del HUB y su conduccion.
/// </summary>
public class Mision3Controller : MonoBehaviour
{
    public enum Estado
    {
        HerramientaPendiente, HerramientaCargada, CamionListo, Traslado, Llegada, PersonaPendiente,
        PersonaLocalizada, AccesoBloqueado, PuertaLiberada, PersonaSiguiendo, PersonaEnZonaSegura, Completada
    }

    public static Mision3Controller Instancia { get; private set; }

    [Header("Escenario (coordenadas del mundo)")]
    [Tooltip("Donde estacionar el camion: la calle frente a la casa.")]
    public Vector3 puntoEstacionar = new Vector3(-39.11f, 0f, 19.65f);
    public float radioLlegada = 13f;
    [Tooltip("Frente a la puerta de entrada de la casa, para la flecha.")]
    public Vector3 puertaCasa = new Vector3(-39.11f, 1.6f, 25.3f);
    [Tooltip("Centro de la zona segura, en la vereda frente a la casa.")]
    public Vector3 zonaSegura = new Vector3(-34.2f, 0f, 21.9f);
    public float radioZonaSegura = 2.6f;

    public Estado EstadoActual { get; private set; } = Estado.HerramientaPendiente;
    public HerramientaRescate Herramienta => _herramienta;
    public IReadOnlyList<JuegoBomberos.Tarea> Tareas => _tareas;

    /// <summary>El camion solo se puede abordar con todo lo anterior listo (herramienta y portones).</summary>
    public bool PuedeAbordar
    {
        get
        {
            foreach (var t in _tareas)
            {
                if (t == _tAbordar) return true;
                if (!t.hecha) return false;
            }
            return true;
        }
    }

    public string MotivoNoAbordar
    {
        get
        {
            foreach (var t in _tareas)
                if (t == _tAbordar) break;
                else if (!t.hecha) return "Primero: " + t.titulo.ToLowerInvariant();
            return "";
        }
    }

    JuegoBomberos _juego;
    OVRCameraRig _rig;
    Transform _cabeza;
    CamionBomberos _camion;
    HerramientaRescate _herramienta;
    ZonaCargaHerramienta _zonaCarga;
    ZonaSeguraRescate _zonaSegura;
    EscenarioMision3 _escenario;
    GameObject _soporte, _anilloEstacionar;

    readonly List<JuegoBomberos.Tarea> _tareas = new List<JuegoBomberos.Tarea>();
    readonly List<System.Func<bool>> _condiciones = new List<System.Func<bool>>();
    readonly List<string> _mensajes = new List<string>();
    JuegoBomberos.Tarea _tCarga, _tAbordar;

    bool _llegado, _entro, _localizada, _puertaForzada, _personaSegura, _finalAvisado;
    float _tiempoDentro, _finRescate;

    void Awake() => Instancia = this;

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public void Iniciar(JuegoBomberos juego, OVRCameraRig rig, CamionBomberos camion)
    {
        _juego = juego;
        _rig = rig;
        _cabeza = rig.centerEyeAnchor;
        _camion = camion;

        _zonaCarga = ZonaCargaHerramienta.Crear(camion);
        CrearHerramienta();
        CrearTareas();
    }

    // ------------------------------------------------------------------
    // Preparacion en el HUB
    // ------------------------------------------------------------------

    /// <summary>La barra aparece en un soporte junto a la parte trasera del camion, en un lugar libre.</summary>
    void CrearHerramienta()
    {
        var cam = _camion.transform;
        Vector3[] candidatos =
        {
            cam.TransformPoint(2.3f, 0f, -3.6f), cam.TransformPoint(-2.3f, 0f, -3.6f),
            cam.TransformPoint(2.3f, 0f, -1.5f), cam.TransformPoint(-2.3f, 0f, -1.5f),
            cam.TransformPoint(2.6f, 0f, 1f), cam.TransformPoint(-2.6f, 0f, 1f),
            cam.TransformPoint(0f, 0f, 6.5f)
        };
        Vector3 pos = candidatos[0];
        foreach (var c in candidatos)
        {
            if (!EspacioLibre(c, cam.position.y, out float y)) continue;
            pos = new Vector3(c.x, y, c.z);
            break;
        }

        _soporte = new GameObject("Soporte_Herramienta");
        _soporte.transform.position = pos;
        var metal = VisualesVR.MaterialOpaco(new Color(0.75f, 0.1f, 0.07f));
        var poste = VisualesVR.Primitiva(PrimitiveType.Cube, "Poste", _soporte.transform, metal);
        poste.transform.localPosition = Vector3.up * 0.5f;
        poste.transform.localScale = new Vector3(0.08f, 1f, 0.08f);
        var repisa = VisualesVR.Primitiva(PrimitiveType.Cube, "Repisa", _soporte.transform, metal);
        repisa.transform.localPosition = Vector3.up * 1f;
        repisa.transform.localScale = new Vector3(0.14f, 0.03f, 0.3f);
        var cartel = VisualesVR.Texto("Cartel", _soporte.transform, 0.045f, new Vector2(0.9f, 0.1f));
        cartel.transform.localPosition = Vector3.up * 1.32f;
        cartel.fontStyle = FontStyles.Bold;
        cartel.outlineWidth = 0.2f;
        cartel.outlineColor = Color.black;
        cartel.text = "BARRA DE RESCATE";
        cartel.gameObject.AddComponent<MirarAlJugador>().objetivo = _cabeza;

        _herramienta = HerramientaRescate.Crear(_rig, pos + Vector3.up * 1.05f, Quaternion.LookRotation(_camion.transform.forward, Vector3.up));
        _herramienta.zona = _zonaCarga;
        _zonaCarga.herramienta = _herramienta;
    }

    bool EspacioLibre(Vector3 p, float alturaBase, out float y)
    {
        y = alturaBase;
        float mejor = float.MaxValue;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 1.6f, Vector3.down, 3.2f, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(_rig.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; y = h.point.y; }
        }
        if (mejor == float.MaxValue || Mathf.Abs(y - alturaBase) > 0.5f) return false;
        foreach (var c in Physics.OverlapBox(new Vector3(p.x, y + 0.8f, p.z), new Vector3(0.45f, 0.65f, 0.45f), Quaternion.identity, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(_rig.transform)) return false;
        return true;
    }

    void CrearTareas()
    {
        Tarea("Recoge la herramienta de rescate",
            "Acércate al soporte rojo, junto al camión, y agarra la barra Halligan con GRIP.",
            () => _herramienta.Centro + Vector3.up * 0.3f,
            () => _herramienta.AgarradaAlgunaVez,
            "¡Barra de rescate en la mano!");

        _tCarga = Tarea("Carga la herramienta en la maletera",
            "Ve a la parte de atrás del camión, abre la maletera (agarra el tirador con la otra mano) y suelta la barra adentro.",
            () => _herramienta.Agarrada ? _zonaCarga.PuntoMaletera : _herramienta.Centro + Vector3.up * 0.3f,
            () => _herramienta.EnMaletera,
            "¡Herramienta cargada en la maletera!");

        Tarea("Abre el portón principal",
            "Toca el botón verde del poste, junto al portón del patio.",
            () => _juego.PuntoBotonPrincipal,
            () => _juego.PortonPrincipalAbierto,
            "¡Portón principal abierto!");

        Tarea("Abre el portón de la cochera",
            "Toca el botón rojo junto al portón.",
            () => _juego.PuntoBotonCochera,
            () => _juego.PortonCocheraAbierto,
            "¡Portón de la cochera abierto!");

        _tAbordar = Tarea("Aborda el camión",
            "Párate en el círculo junto a la puerta del conductor (o presiona A).",
            () => _camion.PuntoZonaSubida + Vector3.up * 2.45f,
            () => _camion.JugadorSentado,
            "¡A la emergencia!");

        Tarea("Conduce hasta la emergencia",
            "Sal por el portón, gira a la izquierda por la calle y estaciona frente a la casa de ladrillo gris.",
            () => puntoEstacionar + Vector3.up * 3f,
            () => _llegado,
            "¡Llegaste a la emergencia!");

        Tarea("Recupera la herramienta de la maletera",
            "Bájate del camión (B), abre la maletera y agarra la barra.",
            () => _zonaCarga.PuntoMaletera,
            () => _llegado && _herramienta.Agarrada && !_camion.JugadorSentado,
            "¡Herramienta lista!");

        Tarea("Busca a la persona atrapada",
            "Se oyen gritos desde adentro: entra a la casa por la puerta principal.",
            () => puertaCasa,
            () => _entro,
            "Hay alguien adentro... ¡sigue los gritos!");

        Tarea("Localiza la puerta bloqueada",
            "Recorre la casa guiándote por los gritos y los golpes.",
            () => _tiempoDentro > 20f && _escenario != null ? _escenario.puerta.PuntoPalanca + Vector3.up * 0.9f : (Vector3?)null,
            () => _localizada,
            "Persona localizada. Libere el acceso");

        Tarea("Utiliza la barra de rescate para liberar el acceso",
            "Lleva la punta de la barra al anillo amarillo de la puerta y jala la barra hacia ti varias veces.",
            () => _escenario == null ? (Vector3?)null
                : _herramienta.Agarrada ? _escenario.puerta.PuntoPalanca + Vector3.up * 0.35f : _herramienta.Centro + Vector3.up * 0.3f,
            () => _puertaForzada,
            "¡Puerta liberada!");

        Tarea("Acompaña a la persona hasta la zona segura",
            "Acércate para que se levante y camina despacio hasta la zona verde de la calle. Te seguirá; si te alejas mucho, te pedirá que la esperes.",
            () =>
            {
                if (_escenario != null && _escenario.persona != null && _escenario.persona.EstadoActual == PersonaRescatadaNPC.Estado.Reaccionando)
                    return _escenario.persona.PuntoGuia;
                return _zonaSegura != null ? _zonaSegura.transform.position + Vector3.up * 2.6f : (Vector3?)null;
            },
            () => _personaSegura,
            "PERSONA RESCATADA");

    }

    JuegoBomberos.Tarea Tarea(string titulo, string pista, System.Func<Vector3?> punto, System.Func<bool> condicion, string mensaje)
    {
        var t = new JuegoBomberos.Tarea { titulo = titulo, pista = pista, punto = punto };
        _tareas.Add(t);
        _condiciones.Add(condicion);
        _mensajes.Add(mensaje);
        return t;
    }

    // ------------------------------------------------------------------
    // Enganche con el interior (escena cargada por proximidad)
    // ------------------------------------------------------------------

    /// <summary>El interior acaba de cargarse: se le aplica el progreso guardado.</summary>
    public void EngancharEscenario(EscenarioMision3 e)
    {
        _escenario = e;
        if (e.puerta == null) return;
        e.puerta.alForzar += AlForzarPuerta;
        if (_puertaForzada)
        {
            e.puerta.DejarForzada();
            if (e.persona != null) e.persona.Liberar();
        }
    }

    void AlForzarPuerta()
    {
        _puertaForzada = true;
        if (_escenario != null && _escenario.persona != null) _escenario.persona.Liberar();
        CrearZonaSegura();
    }

    void CrearZonaSegura()
    {
        if (_zonaSegura == null) _zonaSegura = ZonaSeguraRescate.Crear(zonaSegura, radioZonaSegura, _cabeza);
    }

    // ------------------------------------------------------------------
    // Logica
    // ------------------------------------------------------------------

    void Update()
    {
        if (_juego == null || _herramienta == null || _finalAvisado && Time.time < _finRescate) return;

        if (_finalAvisado)
        {
            _juego.Aviso("ÁREA ASEGURADA", JuegoBomberos.TipoAviso.Exito, 3f);
            _juego.CompletarMision3();
            EstadoActual = Estado.Completada;
            enabled = false;
            return;
        }

        // La barra se saco de la maletera antes de salir: hay que volver a cargarla.
        if (!_llegado && _tCarga.hecha && !_herramienta.EnMaletera) _tCarga.hecha = false;

        ActualizarLlegada();
        ActualizarCasa();

        if (_puertaForzada && !_personaSegura && _escenario != null && _escenario.persona != null && _zonaSegura != null &&
            _escenario.persona.Libre && _zonaSegura.Contiene(_escenario.persona.transform.position))
        {
            _personaSegura = true;
            _escenario.persona.MarcarSegura();
        }

        for (int i = 0; i < _tareas.Count; i++)
        {
            var t = _tareas[i];
            if (t.hecha) continue;
            if (!_condiciones[i]()) break; // en orden: no se adelanta ninguna
            _juego.CompletarTarea(t, _mensajes[i]);
        }

        CalcularEstado();

        if (_personaSegura && TodasHechas())
        {
            _finalAvisado = true;
            _finRescate = Time.time + 3.5f; // "PERSONA RESCATADA" y luego "AREA ASEGURADA"
        }
    }

    void ActualizarLlegada()
    {
        if (_llegado)
        {
            if (_anilloEstacionar != null) { Destroy(_anilloEstacionar); _anilloEstacionar = null; }
            return;
        }
        if (_tAbordar.hecha && _anilloEstacionar == null) CrearAnilloEstacionar();

        Vector3 d = _camion.transform.position - puntoEstacionar;
        d.y = 0f;
        if (_camion.JugadorSentado && d.magnitude <= radioLlegada)
        {
            _llegado = true;
            SonidosVR.Reproducir2D(SonidosVR.Campana, 0.5f);
        }
    }

    void CrearAnilloEstacionar()
    {
        _anilloEstacionar = new GameObject("Anillo_Estacionar");
        _anilloEstacionar.transform.position = ZonaSeguraRescate.PisoEn(puntoEstacionar) + Vector3.up * 0.05f;
        VisualesVR.ObjetoMalla("Anillo", _anilloEstacionar.transform, VisualesVR.Anillo(4.6f, 5f), VisualesVR.MaterialSinLuz(new Color(1f, 0.85f, 0.1f, 0.7f)));
        var t = VisualesVR.Texto("Cartel", _anilloEstacionar.transform, 0.3f, new Vector2(6f, 0.8f));
        t.transform.localPosition = Vector3.up * 3.4f;
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.2f;
        t.outlineColor = Color.black;
        t.color = new Color(1f, 0.85f, 0.25f);
        t.text = "ESTACIONA AQUÍ";
        t.gameObject.AddComponent<MirarAlJugador>().objetivo = _cabeza;
    }

    void ActualizarCasa()
    {
        if (_escenario == null) return;
        bool dentro = _escenario.Dentro(_cabeza.position);
        if (dentro) { _entro = _entro || _llegado; _tiempoDentro += Time.deltaTime; }

        if (!_localizada && _entro && _escenario.puerta != null)
        {
            Vector3 d = _escenario.puerta.PuntoPalanca - _cabeza.position;
            if (d.magnitude < 3.2f && Vector3.Angle(_cabeza.forward, d) < 55f) _localizada = true;
        }
    }

    bool TodasHechas()
    {
        foreach (var t in _tareas) if (!t.hecha) return false;
        return true;
    }

    void CalcularEstado()
    {
        var p = _escenario != null ? _escenario.persona : null;
        if (_personaSegura) EstadoActual = Estado.PersonaEnZonaSegura;
        else if (_puertaForzada)
            EstadoActual = p != null && (p.EstadoActual == PersonaRescatadaNPC.Estado.Siguiendo || p.EstadoActual == PersonaRescatadaNPC.Estado.Esperando)
                ? Estado.PersonaSiguiendo : Estado.PuertaLiberada;
        else if (_localizada) EstadoActual = _escenario != null && _escenario.puerta.Enganchada ? Estado.AccesoBloqueado : Estado.PersonaLocalizada;
        else if (_llegado) EstadoActual = _herramienta.EnMaletera ? Estado.Llegada : Estado.PersonaPendiente;
        else if (_camion.JugadorSentado) EstadoActual = Estado.Traslado;
        else if (_herramienta.EnMaletera) EstadoActual = PuedeAbordar ? Estado.CamionListo : Estado.HerramientaCargada;
        else EstadoActual = Estado.HerramientaPendiente;
    }

    public string EstadoTexto
    {
        get
        {
            switch (EstadoActual)
            {
                case Estado.HerramientaPendiente: return "<color=#FFD54A>PREPARACIÓN</color>: falta cargar la herramienta";
                case Estado.HerramientaCargada: return "<color=#6BFF7A>HERRAMIENTA CARGADA</color>: abre los portones";
                case Estado.CamionListo: return "<color=#6BFF7A>CAMIÓN LISTO</color>: sube y sal";
                case Estado.Traslado: return $"<color=#9AD0FF>EN TRASLADO</color>: {Mathf.RoundToInt(_camion.VelocidadKmh)} km/h";
                case Estado.Llegada: return "<color=#FFD54A>EN LA ESCENA</color>: saca la herramienta";
                case Estado.PersonaPendiente: return "<color=#FF6B5A>PERSONA SIN LOCALIZAR</color>: escucha los gritos";
                case Estado.PersonaLocalizada: return "<color=#FF6B5A>ACCESO BLOQUEADO</color>: usa la barra";
                case Estado.AccesoBloqueado: return "<color=#FFD54A>HACIENDO PALANCA</color>: jala la barra";
                case Estado.PuertaLiberada: return "<color=#6BFF7A>PUERTA LIBERADA</color>: acércate a la persona";
                case Estado.PersonaSiguiendo: return "<color=#9AD0FF>PERSONA SIGUIÉNDOTE</color>: ve a la zona segura";
                case Estado.PersonaEnZonaSegura: return "<color=#6BFF7A>PERSONA EN ZONA SEGURA</color>";
                default: return "";
            }
        }
    }
}
