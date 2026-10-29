using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Mision 2 - Incendio electrico en la casa explorable (Rowhouse). Controla el orden de las tareas,
/// las validaciones y el final; el HUD, la flecha, el puntaje y los sonidos los pone JuegoBomberos.
///
/// Maquina de estados del incendio:
///   ENERGIZADO   -> hay tension, chispas y arcos; el CO2 lo baja un momento pero se reaviva.
///   DESCONECTADO -> interruptor principal abajo; sin chispas ni arcos; el fuego sigue; falta medir.
///   VERIFICADO   -> el comprobador midio SIN TENSION en los bornes: el CO2 ya puede extinguirlo.
///   EXTINGUIDO   -> llamas apagadas, humo residual bajando; falta revisar el area.
///   ASEGURADO    -> el jugador reviso el equipo (sin llamas ni chispas): fin de la mision.
/// Volver a subir el interruptor principal en cualquier momento devuelve a ENERGIZADO (con error).
///
/// En modo Explorar funciona como practica (sin tareas ni puntaje) y se reinicia sola.
/// Todo vive en el prefab de la casa explorable; las piezas se enganchan por referencia.
/// </summary>
public class Mision2Electrica : MonoBehaviour
{
    public enum Estado { Inactivo, Energizado, Desconectado, Verificado, Extinguido, Asegurado }

    [Header("Piezas de la mision")]
    public PanelElectrico panel;
    public MaquinaElectrica maquina;
    public ExtintorCO2 extintor;
    public ComprobadorTension comprobador;
    public PuertaCasa puerta;
    [Tooltip("Donde aparece el jugador al elegir la Mision 2 (afuera, frente a la puerta). Su +Z mira a la casa.")]
    public Transform puntoInicio;

    [Header("Zonas (coordenadas locales de este objeto)")]
    public Vector3 centroCasa = new Vector3(0f, 2.5f, 0f);
    public Vector3 tamanoCasa = new Vector3(7.6f, 3.4f, 11.6f);
    public Vector3 centroCuarto = new Vector3(0f, 2.5f, -2.43f);
    public Vector3 tamanoCuarto = new Vector3(7.6f, 3.4f, 6.74f);
    [Tooltip("Paso (puerta interior) entre la sala y el cuarto de servicio.")]
    public Vector3 pasoCuarto = new Vector3(0.9f, 2.7f, 1.0f);

    [Header("Reglas")]
    [Tooltip("Segundos mirando el equipo de cerca para dar el area por segura.")]
    public float tiempoInspeccion = 2.5f;
    [Tooltip("En modo Explorar (practica), segundos hasta que el equipo vuelve a fallar.")]
    public float reinicioPractica = 30f;

    public Estado EstadoActual { get; private set; } = Estado.Inactivo;
    public bool Practica { get; private set; }
    public IReadOnlyList<JuegoBomberos.Tarea> Tareas => _tareas;
    public Vector3 InicioJugador => puntoInicio != null ? puntoInicio.position : transform.TransformPoint(new Vector3(1f, 0f, 9.2f));
    public Vector3 InicioAdelante => puntoInicio != null ? puntoInicio.forward : -transform.forward;

    JuegoBomberos _juego;
    Transform _cabeza;
    readonly List<JuegoBomberos.Tarea> _tareas = new List<JuegoBomberos.Tarea>();
    readonly List<System.Func<bool>> _condiciones = new List<System.Func<bool>>();
    readonly List<string> _mensajes = new List<string>();
    JuegoBomberos.Tarea _tCorte, _tComprobar, _tExtinguir;
    readonly Dictionary<string, float> _ultimoAviso = new Dictionary<string, float>();
    float _mirandoEquipo, _inspeccion, _reiniciarEn = -1f;
    bool _riesgoVisto, _tableroVisto, _errorCO2, _finalizado, _avisoExtintor, _avisoComprobador, _preparado;

    // ------------------------------------------------------------------
    // Preparacion (una sola vez, al cargar la escena)
    // ------------------------------------------------------------------

    void Start() => Preparar();

    void Preparar()
    {
        if (_preparado) return;
        _preparado = true;
        if (panel == null) panel = GetComponentInChildren<PanelElectrico>();
        if (maquina == null) maquina = GetComponentInChildren<MaquinaElectrica>();
        if (extintor == null) extintor = GetComponentInChildren<ExtintorCO2>();
        if (comprobador == null) comprobador = GetComponentInChildren<ComprobadorTension>();
        if (puerta == null) puerta = transform.root.GetComponentInChildren<PuertaCasa>();
        if (panel == null || maquina == null || extintor == null || comprobador == null)
        {
            Debug.LogError("[Mision2] Faltan piezas de la mision en el prefab de la casa.", this);
            enabled = false;
            return;
        }

        panel.Preparar();
        if (maquina.panel == null) maquina.panel = panel;
        maquina.Preparar();
        extintor.Preparar();
        if (comprobador.panel == null) comprobador.panel = panel;
        comprobador.Preparar();
        CrearCarteles();

        // Eventos (se suscriben una sola vez: reiniciar no los duplica).
        panel.alAccionarSecundario += AlAccionarSecundario;
        maquina.Fuego.alCO2Bloqueado += AlCO2Bloqueado;
        comprobador.alMedir += AlMedir;
        comprobador.alMedirSinContacto += () => Avisar("contacto", "Sin contacto\n<size=55%>Acerca la punta a los bornes PUNTO DE PRUEBA del tablero</size>", JuegoBomberos.TipoAviso.Info, 2.5f);
        comprobador.alAgarrar += () =>
        {
            if (_avisoComprobador || EstadoActual == Estado.Inactivo) return;
            _avisoComprobador = true;
            Avisar("comp", "Comprobador de tensión\n<size=55%>Toca los bornes del tablero con la punta y presiona el gatillo</size>", JuegoBomberos.TipoAviso.Info, 3f);
        };
        extintor.alAgarrar += () =>
        {
            if (_avisoExtintor || EstadoActual == Estado.Inactivo) return;
            _avisoExtintor = true;
            Avisar("ext", "Extintor CO<sub>2</sub>\n<size=55%>Primero retira el pasador (A/X, o tócalo con la otra mano y GRIP)</size>", JuegoBomberos.TipoAviso.Info, 3f);
        };
        extintor.alGatilloConPasador += () => Avisar("pasador", "Retira el pasador\n<size=55%>Presiona A/X, o tócalo con la otra mano y presiona GRIP</size>", JuegoBomberos.TipoAviso.Error, 2.5f);
        extintor.alRetirarPasador += () => Avisar("pasador_ok", "Pasador retirado\n<size=55%>Apunta a la base del fuego y aprieta el gatillo</size>", JuegoBomberos.TipoAviso.Info, 2.5f);

        Detener();
    }

    void CrearCarteles()
    {
        var amarillo = new Color(1f, 0.82f, 0.1f);
        Cartel("¡PELIGRO!\n<size=70%>RIESGO ELÉCTRICO</size>", new Vector3(0.6f, 3.3f, -5.77f), new Vector2(0.9f, 0.32f), amarillo, Color.black);
        Cartel("FUEGO ELÉCTRICO\n<size=70%>NO USAR AGUA - USAR CO<sub>2</sub></size>", new Vector3(2.75f, 2.55f, -5.77f), new Vector2(0.85f, 0.28f), new Color(0.85f, 0.1f, 0.08f), Color.white);
        Cartel("CUARTO DE SERVICIO", new Vector3(0.9f, 3.5f, 1.075f), new Vector2(0.9f, 0.14f), new Color(0.15f, 0.3f, 0.6f), Color.white);
        if (extintor != null)
        {
            Vector3 p = transform.InverseTransformPoint(extintor.transform.position);
            Cartel("EXTINTOR\n<size=80%>CO<sub>2</sub></size>", new Vector3(p.x, 3.05f, p.z - 0.01f), new Vector2(0.36f, 0.22f), new Color(0.85f, 0.1f, 0.08f), Color.white);
        }
    }

    /// <summary>Cartel plano mirando hacia +Z local (hacia la sala / el cuarto).</summary>
    void Cartel(string texto, Vector3 posLocal, Vector2 tam, Color fondo, Color colorTexto)
    {
        var raiz = new GameObject("Cartel").transform;
        raiz.SetParent(transform, false);
        raiz.localPosition = posLocal;
        var placa = VisualesVR.Primitiva(PrimitiveType.Cube, "Placa", raiz, VisualesVR.MaterialOpaco(fondo));
        placa.transform.localScale = new Vector3(tam.x, tam.y, 0.012f);
        var t = VisualesVR.Texto("Texto", raiz, tam.y * 0.3f, tam * 0.94f);
        t.transform.localPosition = new Vector3(0f, 0f, 0.008f);
        t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP se lee desde su -Z
        t.text = texto;
        t.color = colorTexto;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
    }

    // ------------------------------------------------------------------
    // Inicio / fin
    // ------------------------------------------------------------------

    /// <summary>Enciende el incidente. practica = modo Explorar (sin tareas ni puntaje).</summary>
    public void Iniciar(JuegoBomberos juego, bool practica)
    {
        Preparar();
        if (!enabled) return;
        _juego = juego;
        _cabeza = juego != null && juego.rig != null ? juego.rig.centerEyeAnchor : null;
        Practica = practica;

        panel.Restablecer();
        maquina.Restablecer();
        extintor.Restablecer();
        comprobador.Restablecer();

        panel.Activo = true;
        panel.Refrescar();
        maquina.EnFalla = true;
        maquina.Fuego.Encender();
        EstadoActual = Estado.Energizado;

        _riesgoVisto = _tableroVisto = _errorCO2 = _finalizado = false;
        _avisoExtintor = _avisoComprobador = false;
        _mirandoEquipo = _inspeccion = 0f;
        _reiniciarEn = -1f;
        CrearTareas();
    }

    /// <summary>Apaga el incidente y deja todo en reposo.</summary>
    public void Detener()
    {
        EstadoActual = Estado.Inactivo;
        _tareas.Clear();
        _condiciones.Clear();
        _mensajes.Clear();
        if (panel != null) { panel.Restablecer(); panel.Activo = false; panel.Refrescar(); }
        if (maquina != null) maquina.Restablecer();
    }

    void CrearTareas()
    {
        _tareas.Clear();
        _condiciones.Clear();
        _mensajes.Clear();
        if (Practica) return;
        var fuego = maquina.Fuego;

        Tarea("Identifica el riesgo eléctrico",
            "Entra a la casa y busca el equipo que echa chispas y humo. ¡No lo toques ni uses agua!",
            () => Guia(maquina.PuntoMirar + Vector3.up * 0.9f),
            () => _riesgoVisto || !panel.Energizado,
            "¡Riesgo eléctrico identificado!");

        Tarea("Localiza el tablero principal",
            "El tablero eléctrico está en la pared del cuarto de servicio. Acércate a él.",
            () => Guia(panel.Centro + Vector3.up * 0.45f),
            () => _tableroVisto || panel.Tocado || !panel.Energizado,
            "¡Tablero localizado!");

        _tCorte = Tarea("Corta la alimentación eléctrica",
            "Baja la palanca ROJA del interruptor PRINCIPAL (agárrala con GRIP o empújala hacia abajo). Los breakers C1-C3 son circuitos secundarios.",
            () => Guia(panel.PuntoInterruptor + Vector3.up * 0.15f),
            () => !panel.Energizado,
            "¡Alimentación cortada!");

        _tComprobar = Tarea("Comprueba que no hay tensión",
            "Toma el comprobador de la mesa (GRIP), acerca la punta a los bornes PUNTO DE PRUEBA del tablero y presiona el gatillo.",
            () => comprobador.Agarrado ? Guia(panel.PuntoPrueba + Vector3.up * 0.12f) : Guia(comprobador.Centro + Vector3.up * 0.15f),
            () => panel.Verificado && !panel.Energizado,
            "¡SIN TENSIÓN: corte verificado!");

        _tExtinguir = Tarea("Extingue el incendio con CO<sub>2</sub>",
            "Toma el extintor CO<sub>2</sub> junto al paso al cuarto, retira el pasador (A/X), apunta a la base del fuego y aprieta el gatillo.",
            () => extintor.Agarrado ? Guia(fuego.Centro + Vector3.up * 0.9f) : Guia(extintor.Centro + Vector3.up * 0.75f),
            () => fuego.Extinguido,
            "¡INCENDIO EXTINGUIDO!");

        Tarea("Verifica que el área es segura",
            "Acércate al equipo y míralo unos segundos: confirma que no quedan llamas ni chispas.",
            () => Guia(maquina.PuntoMirar + Vector3.up * 0.9f),
            () => EstadoActual == Estado.Asegurado,
            "¡ÁREA ASEGURADA!");
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
    // Logica
    // ------------------------------------------------------------------

    void Update()
    {
        if (EstadoActual == Estado.Inactivo || _juego == null) return;
        if (_cabeza == null && _juego.rig != null) _cabeza = _juego.rig.centerEyeAnchor;
        var fuego = maquina.Fuego;

        switch (EstadoActual)
        {
            case Estado.Energizado:
                if (!panel.Energizado) Pasar(Estado.Desconectado);
                break;
            case Estado.Desconectado:
                if (panel.Energizado) Reenergizado();
                else if (panel.Verificado) Pasar(Estado.Verificado);
                break;
            case Estado.Verificado:
                if (panel.Energizado) Reenergizado();
                else if (fuego.Extinguido) Pasar(Estado.Extinguido);
                break;
            case Estado.Extinguido:
                if (panel.Energizado) Reenergizado();
                else InspeccionFinal();
                break;
            case Estado.Asegurado:
                if (Practica && _reiniciarEn > 0f && Time.time >= _reiniciarEn)
                {
                    Iniciar(_juego, true);
                    Avisar("practica", "¡El equipo volvió a fallar!\n<size=55%>Práctica: repite el procedimiento</size>", JuegoBomberos.TipoAviso.Error, 3f);
                }
                break;
        }

        // El CO2 solo puede apagarlo del todo con el corte verificado.
        fuego.extincionPermitida = EstadoActual == Estado.Verificado;

        DetectarHallazgos();
        if (!Practica) AvanzarTareas();

        if (!Practica && !_finalizado && EstadoActual == Estado.Asegurado && TodasHechas())
        {
            _finalizado = true;
            _juego.CompletarMision2();
        }
    }

    void Pasar(Estado nuevo)
    {
        EstadoActual = nuevo;
        switch (nuevo)
        {
            case Estado.Desconectado:
                Avisar("estado", "Alimentación cortada\n<size=55%>Ya no hay chispas, pero el fuego sigue. Comprueba que no hay tensión.</size>", JuegoBomberos.TipoAviso.Info, 3.5f);
                break;
            case Estado.Verificado:
                Avisar("estado", "SIN TENSIÓN\n<size=55%>Corte confirmado: apaga el fuego con el extintor CO<sub>2</sub></size>", JuegoBomberos.TipoAviso.Exito, 3.5f);
                break;
            case Estado.Extinguido:
                _inspeccion = 0f;
                Avisar("estado", "INCENDIO EXTINGUIDO\n<size=55%>Revisa el equipo de cerca: que no queden llamas ni chispas</size>", JuegoBomberos.TipoAviso.Exito, 3.5f);
                break;
            case Estado.Asegurado:
                SonidosVR.Reproducir2D(SonidosVR.Exito, 0.9f);
                if (Practica)
                {
                    _reiniciarEn = Time.time + reinicioPractica;
                    Avisar("estado", "ÁREA ASEGURADA\n<size=55%>Práctica completa: el equipo volverá a fallar en " + Mathf.RoundToInt(reinicioPractica) + " s</size>", JuegoBomberos.TipoAviso.Exito, 4f);
                }
                break;
        }
    }

    /// <summary>Se volvio a subir el interruptor principal: hay tension otra vez.</summary>
    void Reenergizado()
    {
        bool reaviva = EstadoActual == Estado.Extinguido;
        if (reaviva) maquina.Fuego.Encender(); // las chispas del equipo danado vuelven a encenderlo
        string msg = reaviva
            ? "¡Volviste a energizar el equipo dañado!\n<size=55%>Las chispas reavivaron el fuego. Baja el interruptor PRINCIPAL.</size>"
            : "¡Volviste a conectar la alimentación!\n<size=55%>Hay tensión otra vez: baja el interruptor PRINCIPAL</size>";
        if (Practica) Avisar("reenergizado", msg, JuegoBomberos.TipoAviso.Error, 3.5f);
        else _juego.RegistrarError(msg);

        // Las tareas que dependian del corte vuelven a quedar pendientes.
        if (_tCorte != null) _tCorte.hecha = false;
        if (_tComprobar != null) _tComprobar.hecha = false;
        if (reaviva && _tExtinguir != null) _tExtinguir.hecha = false;
        EstadoActual = Estado.Energizado;
    }

    void InspeccionFinal()
    {
        if (_cabeza == null) return;
        bool limpio = !maquina.Fuego.Encendido && !maquina.EfectosElectricos;
        Vector3 objetivo = maquina.PuntoMirar;
        Vector3 d = objetivo - _cabeza.position;
        Vector3 h = d; h.y = 0f;
        bool cerca = h.magnitude < 2.4f;
        bool mira = Vector3.Angle(_cabeza.forward, d) < 35f;
        if (limpio && cerca && mira)
        {
            float antes = _inspeccion;
            _inspeccion += Time.deltaTime;
            if (antes < tiempoInspeccion * 0.5f && _inspeccion >= tiempoInspeccion * 0.5f)
                SonidosVR.Reproducir2D(SonidosVR.Pitido, 0.6f);
        }
        if (_inspeccion >= tiempoInspeccion) Pasar(Estado.Asegurado);
    }

    void DetectarHallazgos()
    {
        if (_cabeza == null) return;
        bool dentro = Dentro(centroCasa, tamanoCasa, _cabeza.position);

        // Riesgo: mirar el equipo (chispas/llamas) desde dentro de la casa durante un momento.
        if (!_riesgoVisto)
        {
            Vector3 d = maquina.PuntoMirar - _cabeza.position;
            bool mira = dentro && d.magnitude < 8f && Vector3.Angle(_cabeza.forward, d) < 25f;
            _mirandoEquipo = mira ? _mirandoEquipo + Time.deltaTime : Mathf.Max(0f, _mirandoEquipo - Time.deltaTime);
            if (_mirandoEquipo > 0.8f) _riesgoVisto = true;
        }

        // Tablero: estar cerca de el.
        if (!_tableroVisto)
        {
            Vector3 d = panel.Centro - _cabeza.position; d.y = 0f;
            if (dentro && d.magnitude < 1.6f) _tableroVisto = true;
        }
    }

    void AvanzarTareas()
    {
        for (int i = 0; i < _tareas.Count; i++)
        {
            var t = _tareas[i];
            if (t.hecha) continue;
            if (!_condiciones[i]()) break; // en orden: no se adelanta ninguna
            _juego.CompletarTarea(t, _mensajes[i]);
        }
    }

    bool TodasHechas()
    {
        foreach (var t in _tareas) if (!t.hecha) return false;
        return true;
    }

    // ------------------------------------------------------------------
    // Eventos de las piezas
    // ------------------------------------------------------------------

    void AlAccionarSecundario(int circuito, bool encendido)
    {
        if (EstadoActual == Estado.Inactivo) return;
        if (!encendido && panel.Energizado)
            Avisar("secundario", $"Cortaste el circuito C{circuito} (secundario)\n<size=55%>La alimentación general SIGUE ACTIVA: usa el interruptor PRINCIPAL rojo</size>",
                JuegoBomberos.TipoAviso.Error, 3.5f);
    }

    void AlCO2Bloqueado()
    {
        if (EstadoActual == Estado.Inactivo) return;
        if (panel.Energizado)
        {
            const string msg = "¡Corta la alimentación eléctrica primero!\n<size=55%>Con tensión el fuego se reaviva y hay riesgo de descarga</size>";
            if (!Practica && !_errorCO2 && _juego != null)
            {
                _errorCO2 = true; // el error se cuenta una vez; despues solo se recuerda
                _juego.RegistrarError(msg);
                _ultimoAviso["co2"] = Time.time;
            }
            else Avisar("co2", msg, JuegoBomberos.TipoAviso.Error, 3f, 4f);
        }
        else
            Avisar("co2_verif", "Comprueba que no hay tensión\n<size=55%>Mide en los bornes del tablero antes de usar el CO<sub>2</sub></size>", JuegoBomberos.TipoAviso.Info, 3f, 4f);
    }

    void AlMedir(bool sinTension)
    {
        if (EstadoActual == Estado.Inactivo) return;
        if (!sinTension)
            Avisar("medicion", "PELIGRO: CIRCUITO ENERGIZADO\n<size=55%>Hay tensión: baja el interruptor PRINCIPAL antes de seguir</size>", JuegoBomberos.TipoAviso.Error, 3f);
        else if (EstadoActual != Estado.Desconectado)
            Avisar("medicion", "SIN TENSIÓN", JuegoBomberos.TipoAviso.Exito, 1.5f);
        // Desconectado + sin tension: el cambio a VERIFICADO lo anuncia Pasar().
    }

    // ------------------------------------------------------------------
    // Ayudas
    // ------------------------------------------------------------------

    /// <summary>Aviso con limite de repeticion por clave.</summary>
    void Avisar(string clave, string texto, JuegoBomberos.TipoAviso tipo, float duracion, float cadaMinimo = 0.8f)
    {
        if (_juego == null) return;
        if (_ultimoAviso.TryGetValue(clave, out float t) && Time.time - t < cadaMinimo) return;
        _ultimoAviso[clave] = Time.time;
        if (tipo == JuegoBomberos.TipoAviso.Error) SonidosVR.Reproducir2D(SonidosVR.Error, 0.6f);
        _juego.Aviso(texto, tipo, duracion);
    }

    bool Dentro(Vector3 centro, Vector3 tam, Vector3 puntoMundo)
    {
        Vector3 p = transform.InverseTransformPoint(puntoMundo) - centro;
        return Mathf.Abs(p.x) <= tam.x * 0.5f && Mathf.Abs(p.y) <= tam.y * 0.5f && Mathf.Abs(p.z) <= tam.z * 0.5f;
    }

    /// <summary>Lleva la flecha por la puerta de la casa y por el paso al cuarto antes de llegar al objetivo.</summary>
    Vector3? Guia(Vector3 objetivo)
    {
        if (_cabeza == null) return objetivo;
        if (!Dentro(centroCasa, tamanoCasa, _cabeza.position) && puerta != null) return puerta.PuntoEntrada;
        if (Dentro(centroCuarto, tamanoCuarto, objetivo) && !Dentro(centroCuarto, tamanoCuarto, _cabeza.position))
            return transform.TransformPoint(pasoCuarto);
        return objetivo;
    }

    /// <summary>Linea de estado para el HUD.</summary>
    public string EstadoTexto
    {
        get
        {
            switch (EstadoActual)
            {
                case Estado.Energizado: return "<color=#FF6B5A>INCENDIO ENERGIZADO</color>: hay tensión, chispas y arcos";
                case Estado.Desconectado: return "<color=#FFD54A>ALIMENTACIÓN DESCONECTADA</color>: falta comprobar que no hay tensión";
                case Estado.Verificado: return "<color=#6BFF7A>CORTE VERIFICADO</color>: ya puedes usar el CO<sub>2</sub>";
                case Estado.Extinguido:
                    return "<color=#9AD0FF>INCENDIO EXTINGUIDO</color>: verifica el área" +
                           (_inspeccion > 0f ? $" ({Mathf.RoundToInt(100f * Mathf.Clamp01(_inspeccion / tiempoInspeccion))} %)" : "");
                case Estado.Asegurado: return "<color=#6BFF7A>ÁREA ASEGURADA</color>";
                default: return "";
            }
        }
    }
}
