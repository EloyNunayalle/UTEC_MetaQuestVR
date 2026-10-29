using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Camion de bomberos interactivo y manejable.
/// - Subir: parate en el circulo amarillo junto a la puerta del conductor (se llena) o presiona A / pellizca.
/// - Manejar (controles): stick izquierdo arriba/abajo = avanzar/retroceder, a los lados = girar
///   (tambien sirve el stick derecho para girar). Teclado: flechas o WASD.
/// - A: prender/apagar la sirena. X: prender/apagar las balizas (independientes).
/// - Bajar: B (o Y), o mantener el pellizco 2 segundos.
/// Si el modelo del camion no esta en el proyecto (asset de pago no incluido en git),
/// se genera un camion simple de reemplazo para que el juego funcione igual.
/// </summary>
[DefaultExecutionOrder(-900)] // Temprano: el rig debe moverse antes de que el SDK de avatares (orden -1) muestree la pose.
public class CamionBomberos : MonoBehaviour
{
    [Header("Orientacion")]
    [Tooltip("Invierte el frente detectado del camion (si la cabina queda atras).")]
    public bool invertirFrente;
    [Tooltip("Lado del conductor: true = izquierda (Peru/EE.UU.).")]
    public bool conductorIzquierda = true;

    [Header("Ajustes finos (ejes del camion: X derecha, Y arriba, Z frente)")]
    [Tooltip("El asiento arranca 0.28 m hacia atras para que la vista no quede pegada al timon; ajustable en Play.")]
    public Vector3 ajusteAsiento = new Vector3(0f, 0f, -0.28f);
    public Vector3 ajusteZonaSubida = Vector3.zero;
    public Vector3 ajusteBalizas = Vector3.zero;

    [Header("Abordaje")]
    public float radioZona = 0.7f;
    public float segundosParaSubir = 1.5f;
    public float segundosPellizcoParaBajar = 2f;

    [Header("Manejo")]
    public bool manejable = true;
    [Tooltip("Velocidad maxima hacia adelante (m/s). 8 m/s ~ 29 km/h.")]
    public float velocidadMaxima = 8f;
    public float velocidadMaximaReversa = 3f;
    public float aceleracion = 3.5f;
    public float frenado = 7f;
    [Tooltip("Grados por segundo de giro a buena velocidad.")]
    public float giroMaximo = 40f;
    [Tooltip("Altura maxima (m) de la caja de choque al manejar. Mas baja = no se engancha con techos, ramas ni marcos de portones.")]
    public float alturaChoque = 2.5f;
    [Tooltip("Zona muerta del joystick.")]
    public float zonaMuerta = 0.15f;

    [Header("Camion de reemplazo (si falta el modelo)")]
    public Vector3 tamanoReemplazo = new Vector3(2.5f, 3.0f, 8.5f);

    [Header("Luces y sirena")]
    [Tooltip("Las balizas arrancan encendidas. La sirena siempre arranca apagada; se controla aparte.")]
    public bool lucesEncendidasAlIniciar = true;
    [Tooltip("Ciclos completos por segundo de las balizas. 1.5 Hz = 90 destellos por minuto, ritmo de camion real.")]
    [Range(0.5f, 3f)] public float frecuenciaBalizas = 1.5f;

    public bool JugadorSentado { get; private set; }
    public bool SirenaEncendida => _sirena != null && _sirena.isPlaying;
    public bool LucesEncendidas => _lucesOn;
    public float Velocidad => _velocidad;
    public float VelocidadKmh => Mathf.Abs(_velocidad) * 3.6f;
    public Vector3 PuntoZonaSubida => _zona != null ? _zona.position : transform.position;
    public Vector3 Frente => transform.TransformDirection(_fwdLocal);

    public Func<bool> puedeSubir;
    public Func<string> motivoNoPuede;
    /// <summary>Si esta seteado, el giro lo manda el volante fisico (-1..1) en vez del stick.</summary>
    public Func<float> ejeGiroManual;
    /// <summary>Si esta seteado, la marcha (0=P, 1=R, 2=N, 3=D) limita el movimiento.</summary>
    public Func<int> marchaManual;
    public event Action alSubir;
    public event Action alBajar;
    public event Action alChocar;

    OVRCameraRig _rig;
    // Geometria guardada en el espacio local del camion (para que siga valida al moverlo).
    Vector3 _centroBaseLocal, _fwdLocal, _derLocal;
    float _largoMedio, _anchoMedio, _alto;

    Transform _efectos, _zona, _relleno;
    Renderer _anilloR;
    TextMeshPro _etiqueta;
    readonly Light[] _luces = new Light[2];
    readonly Renderer[] _balizas = new Renderer[2];
    AudioSource _sirena, _motor;
    bool _lucesOn;
    float _progreso, _enfriamiento, _pellizcoBajar, _proximoTic, _proximaVibracion, _ultimoChoque;
    Vector3 _rigPosLocal;
    Quaternion _rigRotLocal;
    float _rigAlturaSobreZona;
    float _velocidad;
    readonly List<GameObject> _locomocionApagada = new List<GameObject>();
    bool _listo;
    bool _pellizcoLiberado;

    static readonly Color Amarillo = new Color(1f, 0.85f, 0.1f, 0.85f);
    static readonly Color Verde = new Color(0.2f, 1f, 0.35f, 0.9f);
    static readonly Color Gris = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    // ------------------------------------------------------------------
    // Geometria
    // ------------------------------------------------------------------

    Vector3 Fwd => transform.TransformDirection(_fwdLocal);
    Vector3 Der => transform.TransformDirection(_derLocal);
    Vector3 CentroBase => transform.TransformPoint(_centroBaseLocal);
    float LadoConductor => conductorIzquierda ? -1f : 1f;

    /// <summary>Punto en coordenadas del camion (x derecha, y arriba desde el piso, z frente desde el centro).</summary>
    Vector3 Local(float x, float y, float z) => CentroBase + Der * x + Vector3.up * y + Fwd * z;

    /// <summary>Caja (aproximada, alineada a los ejes del mundo) que ocupa el camion ahora.</summary>
    public Bounds Limites
    {
        get
        {
            var b = new Bounds(Local(0f, _alto * 0.5f, 0f), Vector3.zero);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    b.Encapsulate(Local(sx * _anchoMedio, 0f, sz * _largoMedio));
                    b.Encapsulate(Local(sx * _anchoMedio, _alto, sz * _largoMedio));
                }
            return b;
        }
    }

    /// <summary>True si el punto esta dentro del camion (en sus propios ejes) con un margen.</summary>
    public bool ContienePunto(Vector3 p, float margen)
    {
        Vector3 d = p - CentroBase;
        return Mathf.Abs(Vector3.Dot(d, Der)) <= _anchoMedio + margen &&
               Mathf.Abs(Vector3.Dot(d, Fwd)) <= _largoMedio + margen &&
               d.y >= -0.5f && d.y <= _alto + margen;
    }

    public Vector3 Techo => Local(0f, _alto + 0.3f, 0f);

    /// <summary>Configura el camion. referenciaFrente: punto hacia donde mira el frente (salida de la bahia).</summary>
    public void Inicializar(OVRCameraRig rig, Vector3? referenciaFrente)
    {
        _rig = rig;

        if (!VisualesVR.Limites(transform, out Bounds b) && !LimitesPorColliders(out b))
        {
            Debug.LogWarning("[CamionBomberos] No se encontro el modelo del camion: se genera uno de reemplazo.");
            CrearReemplazo(referenciaFrente);
            VisualesVR.Limites(transform, out b);
        }

        // Frente: eje horizontal mas largo de los limites, con el signo hacia la referencia.
        Vector3 eje = b.size.z >= b.size.x ? Vector3.forward : Vector3.right;
        if (referenciaFrente.HasValue && Vector3.Dot(referenciaFrente.Value - b.center, eje) < 0f) eje = -eje;
        if (invertirFrente) eje = -eje;
        Vector3 der = Vector3.Cross(Vector3.up, eje);

        _largoMedio = Mathf.Abs(Vector3.Dot(b.extents, eje));
        _anchoMedio = Mathf.Abs(Vector3.Dot(b.extents, der));
        _alto = b.size.y;
        _fwdLocal = transform.InverseTransformDirection(eje);
        _derLocal = transform.InverseTransformDirection(der);
        _centroBaseLocal = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));

        _efectos = new GameObject("Camion_Efectos").transform;
        _efectos.SetParent(transform, true);
        _efectos.SetPositionAndRotation(CentroBase, Quaternion.LookRotation(eje, Vector3.up));

        CrearZonaSubida();
        CrearBalizas();

        _sirena = SonidosVR.CrearBucle(_efectos, SonidosVR.Sirena, 0.55f, 60f);
        _sirena.transform.position = PosBalizas();
        _motor = SonidosVR.CrearBucle(_efectos, SonidosVR.Motor, 0.6f, 25f);
        _motor.transform.position = Local(0f, 1f, _largoMedio - 1f);

        // Balizas independientes de la sirena: pueden quedar encendidas sin sonido.
        EncenderLuces(lucesEncendidasAlIniciar);
        Debug.Log($"[CamionBomberos] Balizas {frecuenciaBalizas:0.0} Hz " +
                  $"({frecuenciaBalizas * 60f:0} destellos/min); sirena independiente.");

        _listo = true;
    }

    Vector3 PosAsiento()
    {
        float alto = Mathf.Clamp(_alto * 0.72f, 1.9f, 2.7f);
        return Local(LadoConductor * _anchoMedio * 0.45f + ajusteAsiento.x,
                     alto + ajusteAsiento.y,
                     _largoMedio - 1.1f + ajusteAsiento.z);
    }

    Vector3 PosBalizas() => Local(ajusteBalizas.x, _alto + 0.08f + ajusteBalizas.y, _largoMedio - 0.9f + ajusteBalizas.z);

    bool LimitesPorColliders(out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!hay) { b = c.bounds; hay = true; }
            else b.Encapsulate(c.bounds);
        }
        // Un collider muy chico no sirve como referencia de tamano.
        return hay && b.size.y > 1f;
    }

    // ------------------------------------------------------------------
    // Construccion de piezas
    // ------------------------------------------------------------------

    void CrearZonaSubida()
    {
        Vector3 p = Local(LadoConductor * (_anchoMedio + 0.8f) + ajusteZonaSubida.x, 0f,
                          _largoMedio - 1.4f + ajusteZonaSubida.z);
        // Ajusta la altura al piso real si hay collider debajo.
        float y = p.y;
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
            y = hit.point.y;
        p.y = y + 0.02f + ajusteZonaSubida.y;

        _zona = new GameObject("Zona_Subida").transform;
        _zona.SetParent(_efectos, false);
        _zona.position = p;

        var anillo = VisualesVR.ObjetoMalla("Anillo", _zona, VisualesVR.Anillo(radioZona - 0.08f, radioZona), VisualesVR.MaterialSinLuz(Amarillo));
        _anilloR = anillo.GetComponent<Renderer>();
        var relleno = VisualesVR.ObjetoMalla("Relleno", _zona, VisualesVR.Disco(radioZona - 0.1f), VisualesVR.MaterialSinLuz(new Color(0.2f, 1f, 0.35f, 0.45f)));
        _relleno = relleno.transform;
        _relleno.localPosition = Vector3.up * 0.005f;
        _relleno.localScale = Vector3.zero;

        _etiqueta = VisualesVR.Texto("Etiqueta", _zona, 0.09f, new Vector2(1.6f, 0.6f));
        _etiqueta.transform.localPosition = Vector3.up * 2.1f;
        _etiqueta.fontStyle = FontStyles.Bold;
        _etiqueta.outlineWidth = 0.2f;
        _etiqueta.outlineColor = Color.black;
    }

    void CrearBalizas()
    {
        Vector3 c = PosBalizas();
        for (int i = 0; i < 2; i++)
        {
            float lado = i == 0 ? -1f : 1f;
            Color col = i == 0 ? new Color(1f, 0.1f, 0.05f) : new Color(1f, 1f, 1f);
            var b = VisualesVR.Primitiva(PrimitiveType.Sphere, "Baliza_" + i, _efectos, VisualesVR.MaterialSinLuz(col * 0.35f));
            b.transform.position = c + Der * (lado * Mathf.Max(0.4f, _anchoMedio * 0.5f));
            b.transform.localScale = Vector3.one * 0.22f;
            _balizas[i] = b.GetComponent<Renderer>();

            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(b.transform, false);
            l.type = LightType.Point;
            l.color = col;
            l.range = 9f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            _luces[i] = l;
        }
    }

    void CrearReemplazo(Vector3? referenciaFrente)
    {
        Vector3 fwd = Vector3.forward;
        if (referenciaFrente.HasValue && Vector3.Dot(referenciaFrente.Value - transform.position, fwd) < 0f) fwd = -fwd;
        var raiz = new GameObject("Camion_Reemplazo").transform;
        raiz.SetParent(transform, false);
        raiz.SetPositionAndRotation(transform.position, Quaternion.LookRotation(fwd, Vector3.up));

        float W = tamanoReemplazo.x, H = tamanoReemplazo.y, L = tamanoReemplazo.z;
        var rojo = VisualesVR.MaterialOpaco(new Color(0.75f, 0.05f, 0.05f));
        var negro = VisualesVR.MaterialOpaco(new Color(0.08f, 0.08f, 0.08f));
        var gris = VisualesVR.MaterialOpaco(new Color(0.7f, 0.7f, 0.72f));
        var vidrio = VisualesVR.MaterialOpaco(new Color(0.35f, 0.55f, 0.75f));

        void Caja(string n, Material m, Vector3 pos, Vector3 esc)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(raiz, false);
            g.transform.localPosition = pos;
            g.transform.localScale = esc;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        float cab = L * 0.28f;
        Caja("Carroceria", rojo, new Vector3(0f, 0.55f + (H * 0.72f - 0.55f) / 2f, -cab / 2f), new Vector3(W, H * 0.72f - 0.55f, L - cab));
        Caja("Cabina", rojo, new Vector3(0f, 0.55f + (H * 0.9f - 0.55f) / 2f, L / 2f - cab / 2f), new Vector3(W, H * 0.9f - 0.55f, cab));
        Caja("Parabrisas", vidrio, new Vector3(0f, H * 0.68f, L / 2f + 0.01f), new Vector3(W * 0.85f, H * 0.3f, 0.02f));
        Caja("Escalera", gris, new Vector3(0f, H * 0.72f + 0.1f, -cab / 2f), new Vector3(0.6f, 0.15f, (L - cab) * 0.95f));
        Caja("Parachoques", gris, new Vector3(0f, 0.45f, L / 2f + 0.1f), new Vector3(W, 0.25f, 0.2f));

        float[] zs = { L / 2f - cab * 0.5f, -L * 0.15f, -L * 0.35f };
        foreach (float z in zs)
            for (int s = -1; s <= 1; s += 2)
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                r.name = "Rueda";
                r.transform.SetParent(raiz, false);
                r.transform.localPosition = new Vector3(s * (W / 2f - 0.15f), 0.5f, z);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                r.transform.localScale = new Vector3(1f, 0.18f, 1f);
                r.GetComponent<Renderer>().sharedMaterial = negro;
                Destroy(r.GetComponent<Collider>());
            }
    }

    // ------------------------------------------------------------------
    // Abordaje (a pie)
    // ------------------------------------------------------------------

    void Update()
    {
        if (!_listo) return;
        ActualizarLuces();
        if (JugadorSentado)
        {
            if (manejable) Manejar();

            // Mantiene al jugador sentado en la cabina (sigue al camion cuando se mueve).
            // En Update (temprano, orden -900) para que el SDK de avatares muestree el rig
            // ya movido en el mismo frame; si no, el avatar del jugador va 1 frame atras.
            if (_rig != null)
            {
                _rig.transform.SetPositionAndRotation(transform.TransformPoint(_rigPosLocal), transform.rotation * _rigRotLocal);

                // El pellizco usado para subir debe soltarse antes de contar para bajar.
                bool pellizco = EntradaVR.AlgunPellizco();
                if (!pellizco) _pellizcoLiberado = true;
                if (pellizco && _pellizcoLiberado) _pellizcoBajar += Time.deltaTime;
                else _pellizcoBajar = 0f;

                if (EntradaVR.CancelarDown() || _pellizcoBajar >= segundosPellizcoParaBajar) Bajar();
            }
            return;
        }

        var cabeza = _rig != null ? _rig.centerEyeAnchor : null;
        if (cabeza == null) return;

        // Etiqueta mirando al jugador (solo giro horizontal).
        Vector3 aCabeza = _etiqueta.transform.position - cabeza.position;
        aCabeza.y = 0f;
        if (aCabeza.sqrMagnitude > 0.001f) _etiqueta.transform.rotation = Quaternion.LookRotation(aCabeza);

        bool habilitado = puedeSubir == null || puedeSubir();
        bool interactuar = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;
        Vector3 d = cabeza.position - _zona.position;
        d.y = 0f;
        bool dentro = interactuar && d.magnitude <= radioZona + 0.15f;

        _enfriamiento -= Time.deltaTime;
        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);

        if (!habilitado)
        {
            _etiqueta.text = "CAMIÓN DE BOMBEROS\n<size=60%>" + (motivoNoPuede != null ? motivoNoPuede() : "Aún no puedes subir") + "</size>";
            _anilloR.sharedMaterial.color = Color.Lerp(Gris, Amarillo, 0.25f * pulso);
            if (dentro && _enfriamiento <= 0f && (_progreso > 0.35f || EntradaVR.ConfirmarDown()))
                Rechazar();
            else if (dentro) _progreso += Time.deltaTime / segundosParaSubir;
            else _progreso = 0f;
            _relleno.localScale = Vector3.zero;
            return;
        }

        _etiqueta.text = manejable
            ? "SUBIR Y MANEJAR\n<size=60%>Párate en el círculo o presiona A / pellizca</size>"
            : "SUBIR AL CAMIÓN\n<size=60%>Párate en el círculo o presiona A / pellizca</size>";
        if (dentro && _enfriamiento <= 0f)
        {
            _progreso += Time.deltaTime / segundosParaSubir;
            if (Time.time >= _proximoTic)
            {
                _proximoTic = Time.time + 0.25f;
                EntradaVR.VibrarAmbas(0.15f + 0.4f * _progreso, 0.04f);
                SonidosVR.ReproducirEn(SonidosVR.Clic, _zona.position + Vector3.up, 0.3f + 0.4f * _progreso);
            }
            if (_progreso >= 1f || EntradaVR.ConfirmarDown()) Subir();
        }
        else
        {
            _progreso = Mathf.Max(0f, _progreso - Time.deltaTime * 2f);
        }

        _anilloR.sharedMaterial.color = dentro ? Verde : Color.Lerp(Amarillo * 0.7f, Amarillo, pulso);
        float s = Mathf.Clamp01(_progreso);
        _relleno.localScale = new Vector3(s, 1f, s);
        _zona.localScale = Vector3.one * (dentro ? 1f : 1f + 0.05f * pulso);
    }

    void Rechazar()
    {
        _progreso = 0f;
        _enfriamiento = 3f;
        SonidosVR.Reproducir2D(SonidosVR.Error, 0.8f);
        EntradaVR.VibrarAmbas(0.8f, 0.25f, 0.2f);
        if (JuegoBomberos.Instancia != null)
            JuegoBomberos.Instancia.Aviso(motivoNoPuede != null ? motivoNoPuede() : "Aún no puedes subir", JuegoBomberos.TipoAviso.Error);
    }

    // ------------------------------------------------------------------
    // Manejo
    // ------------------------------------------------------------------

    void Manejar()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Entrada: stick izquierdo (adelante/atras + giro), stick derecho tambien gira; teclado flechas/WASD.
        Vector2 izq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 der = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        float acel = izq.y;
        float giro = Mathf.Abs(der.x) > Mathf.Abs(izq.x) ? der.x : izq.x;
        float tv = Input.GetAxisRaw("Vertical"), th = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(tv) > Mathf.Abs(acel)) acel = tv;
        if (Mathf.Abs(th) > Mathf.Abs(giro)) giro = th;
        acel = Mathf.Abs(acel) < zonaMuerta ? 0f : acel;
        giro = Mathf.Abs(giro) < zonaMuerta ? 0f : giro;

        // Volante y palanca fisicos (si estan instalados).
        if (ejeGiroManual != null) giro = Mathf.Clamp(ejeGiroManual(), -1f, 1f);
        bool marchaBaja = false;
        if (marchaManual != null)
        {
            int m = marchaManual();
            if (m == 3) acel = Mathf.Max(0f, acel);
            else if (m == 1) acel = Mathf.Min(0f, acel);
            else if (m == 4) { acel = Mathf.Max(0f, acel); marchaBaja = true; }
            else acel = 0f;
        }

        // Velocidad: acelera hacia el objetivo; si sueltas el stick frena solo.
        // En L (baja) la velocidad maxima es menor y el freno de motor es mas fuerte.
        float velMax = marchaBaja ? velocidadMaxima * 0.45f : velocidadMaxima;
        float objetivo = acel >= 0f ? acel * velMax : acel * velocidadMaximaReversa;
        bool frenando = acel == 0f || Mathf.Sign(objetivo) != Mathf.Sign(_velocidad) && Mathf.Abs(_velocidad) > 0.1f;
        float frenadoActual = marchaBaja ? frenado * 1.6f : frenado;
        _velocidad = Mathf.MoveTowards(_velocidad, objetivo, (frenando ? frenadoActual : aceleracion) * dt);

        // Giro: solo cuando el camion se mueve (en reversa se invierte, como un auto real).
        float factor = Mathf.Clamp(_velocidad / 3f, -1f, 1f);
        float grados = giro * giroMaximo * factor * dt;
        if (Mathf.Abs(grados) > 0.0001f)
        {
            Quaternion antes = transform.rotation;
            Vector3 posAntes = transform.position;
            RecordarContactos();
            transform.RotateAround(CentroBase, Vector3.up, grados);
            if (HayChoqueAqui()) { transform.SetPositionAndRotation(posAntes, antes); Chocar(); }
        }

        // Avance con deteccion de choques.
        float paso = _velocidad * dt;
        if (Mathf.Abs(paso) > 0.0001f)
        {
            Vector3 dir = Fwd * Mathf.Sign(paso);
            if (HayChoqueEnCamino(dir, Mathf.Abs(paso) + 0.1f)) Chocar();
            else transform.position += Fwd * paso;
        }

        // Motor mas agudo con la velocidad y vibracion suave proporcional.
        float rapidez = Mathf.Abs(_velocidad) / velocidadMaxima;
        _motor.pitch = 0.8f + 0.9f * rapidez;
        if (Time.time >= _proximaVibracion && rapidez > 0.05f)
        {
            _proximaVibracion = Time.time + 0.12f;
            EntradaVR.VibrarAmbas(0.04f + 0.12f * rapidez, 0.1f, 0.2f);
        }

        // Controles: A = sirena; X = luces; B/Y = bajar. En editor: H = sirena, L = luces.
        if (OVRInput.GetDown(OVRInput.Button.One) || Input.GetKeyDown(KeyCode.H))
            AlternarSirena();
        if (OVRInput.GetDown(OVRInput.Button.Three) || Input.GetKeyDown(KeyCode.L))
            AlternarLuces();
    }

    void Chocar()
    {
        float golpe = Mathf.Abs(_velocidad);
        _velocidad = 0f;
        if (Time.time - _ultimoChoque < 0.6f) return;
        _ultimoChoque = Time.time;
        if (golpe > 0.5f)
        {
            SonidosVR.ReproducirEn(SonidosVR.Puerta, Local(0f, 1f, 0f), 1f);
            EntradaVR.VibrarAmbas(Mathf.Clamp01(0.4f + golpe * 0.1f), 0.25f, 0.2f);
        }
        alChocar?.Invoke();
    }

    // Caja de choque: elevada del piso (no choca con el suelo ni con veredas) y recortada
    // arriba en "alturaChoque" (no se engancha con marcos de portones, techos ni ramas).
    const float AlturaLibre = 0.35f;

    float AltoCaja => Mathf.Max(0.4f, Mathf.Min(_alto, alturaChoque) - AlturaLibre - 0.1f);
    Vector3 CentroCaja => Local(0f, AlturaLibre + AltoCaja * 0.5f, 0f);
    Vector3 MitadCaja => new Vector3(_anchoMedio - 0.05f, AltoCaja * 0.5f, _largoMedio - 0.05f);
    Quaternion RotCaja => Quaternion.LookRotation(Fwd, Vector3.up);

    bool HayChoqueEnCamino(Vector3 dir, float distancia)
    {
        var hits = Physics.BoxCastAll(CentroCaja, MitadCaja, dir, RotCaja, distancia, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (Ignorar(h.collider)) continue;
            // distancia 0 = ya estaba superpuesto al empezar: se ignora para no quedar atascado.
            if (h.distance <= 0f) continue;
            return true;
        }
        return false;
    }

    static readonly HashSet<Collider> _tocandoAntes = new HashSet<Collider>();

    /// <summary>Guarda lo que el camion ya toca antes de girar.</summary>
    void RecordarContactos()
    {
        _tocandoAntes.Clear();
        foreach (var c in Physics.OverlapBox(CentroCaja, MitadCaja * 0.97f, RotCaja, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignorar(c)) _tocandoAntes.Add(c);
    }

    /// <summary>
    /// True si al girar el camion se mete en algo NUEVO. Lo que ya tocaba antes no cuenta
    /// (asi, si quedo rozando una pared, igual puede girar para salir y no se queda atascado).
    /// </summary>
    bool HayChoqueAqui()
    {
        foreach (var c in Physics.OverlapBox(CentroCaja, MitadCaja * 0.97f, RotCaja, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignorar(c) && !_tocandoAntes.Contains(c)) return true;
        return false;
    }

    bool Ignorar(Collider c)
    {
        if (c == null) return true;
        if (c.transform.IsChildOf(transform)) return true;
        if (_rig != null && c.transform.IsChildOf(_rig.transform)) return true;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true; // objetos sueltos
        return false;
    }

    // ------------------------------------------------------------------
    // Subir / bajar
    // ------------------------------------------------------------------

    // (El seguimiento del rig se movio a Update, orden -900, para que el SDK de avatares
    // muestree la posicion ya actualizada en el mismo frame. Antes estaba aqui en
    // LateUpdate con orden +10000, lo que causaba que el avatar del jugador fuera
    // 1 frame atras (se veia "a saltos") mientras el camion se movia.)

    public void Subir()
    {
        if (JugadorSentado || _rig == null) return;
        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;

        _rigAlturaSobreZona = rigT.position.y - _zona.position.y;

        ApagarLocomocion();

        // Gira el rig para que la cabeza mire al frente del camion y la mueve al asiento.
        float yawObjetivo = Quaternion.LookRotation(Fwd).eulerAngles.y;
        float delta = Mathf.DeltaAngle(cabeza.eulerAngles.y, yawObjetivo);
        rigT.rotation = Quaternion.Euler(0f, rigT.eulerAngles.y + delta, 0f);
        rigT.position += PosAsiento() - cabeza.position;
        _rigPosLocal = transform.InverseTransformPoint(rigT.position);
        _rigRotLocal = Quaternion.Inverse(transform.rotation) * rigT.rotation;

        JugadorSentado = true;
        _velocidad = 0f;
        _progreso = 0f;
        _pellizcoBajar = 0f;
        _pellizcoLiberado = false;
        _zona.gameObject.SetActive(false);

        // Luces y sirena son independientes: no se encienden solas al subir.
        // Las balizas ya vienen encendidas por lucesEncendidasAlIniciar.
        _motor.pitch = 0.8f;
        _motor.Play();
        SonidosVR.ReproducirEn(SonidosVR.Puerta, PosAsiento(), 1f);
        EntradaVR.VibrarAmbas(0.9f, 0.4f, 0.3f);

        alSubir?.Invoke();
    }

    public void Bajar()
    {
        if (!JugadorSentado) return;
        JugadorSentado = false;
        _velocidad = 0f;
        _zona.gameObject.SetActive(true);

        // Baja al jugador en la zona de subida (donde este ahora el camion), mirando igual que en la cabina.
        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;
        Vector3 piso = _zona.position;
        Vector3 desplaz = piso - cabeza.position;
        desplaz.y = 0f;
        Vector3 nuevaPos = rigT.position + desplaz;
        nuevaPos.y = piso.y + _rigAlturaSobreZona;
        rigT.position = nuevaPos;

        EncenderLocomocion();
        TeletransportarLocomotor(new Vector3(piso.x, nuevaPos.y, piso.z), Quaternion.Euler(0f, cabeza.eulerAngles.y, 0f));

        EncenderSirena(false);
        _motor.Stop();
        _enfriamiento = 2f;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, _zona.position + Vector3.up, 1f);
        EntradaVR.VibrarAmbas(0.5f, 0.15f);

        alBajar?.Invoke();
    }

    void ApagarLocomocion()
    {
        _locomocionApagada.Clear();
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Locomot")) continue;
            // Evita apagar algo que contenga la camara o las manos.
            if (_rig.centerEyeAnchor.IsChildOf(t)) continue;
            if (!t.gameObject.activeSelf) continue;
            _locomocionApagada.Add(t.gameObject);
        }
        foreach (var go in _locomocionApagada) go.SetActive(false);
    }

    void EncenderLocomocion()
    {
        foreach (var go in _locomocionApagada) if (go != null) go.SetActive(true);
        _locomocionApagada.Clear();
    }

    /// <summary>
    /// Avisa al locomotor del Interaction SDK que el jugador se movio (como un teletransporte),
    /// para que no lo devuelva a donde estaba antes de subir. Se hace por reflexion para no
    /// depender de la version exacta del SDK; si no se encuentra, no pasa nada.
    /// Tambien lo usa la Mision 2 para llevar al jugador frente a la casa.
    /// </summary>
    public static void TeletransportarLocomotor(Vector3 pies, Quaternion rotacion)
    {
        try
        {
            MonoBehaviour elegido = null;
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                string n = mb.GetType().Name;
                if (n == "FirstPersonLocomotor") { elegido = mb; break; }
                if (n == "PlayerLocomotor" && elegido == null) elegido = mb;
            }
            if (elegido == null) return;

            var metodo = elegido.GetType().GetMethod("HandleLocomotionEvent");
            if (metodo == null) return;
            var tipoEvento = metodo.GetParameters()[0].ParameterType;
            var tTras = tipoEvento.GetNestedType("TranslationType");
            var tRot = tipoEvento.GetNestedType("RotationType");
            if (tTras == null || tRot == null) return;
            var ctor = tipoEvento.GetConstructor(new[] { typeof(int), typeof(Pose), tTras, tRot });
            if (ctor == null) return;
            object evento = ctor.Invoke(new object[]
            {
                0, new Pose(pies, rotacion), Enum.Parse(tTras, "Absolute"), Enum.Parse(tRot, "Absolute")
            });
            metodo.Invoke(elegido, new[] { evento });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CamionBomberos] No se pudo avisar al locomotor: " + e.Message);
        }
    }

    // ------------------------------------------------------------------
    // Luces
    // ------------------------------------------------------------------

    public bool AlternarLuces()
    {
        EncenderLuces(!_lucesOn);
        return _lucesOn;
    }

    public void EncenderLuces(bool on)
    {
        _lucesOn = on;
        if (_luces[0] == null || _luces[1] == null || _balizas[0] == null || _balizas[1] == null)
            return;

        if (on)
        {
            ActualizarLuces();
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            _luces[i].intensity = 0f;
            _balizas[i].sharedMaterial.color = _luces[i].color * 0.35f;
        }
    }

    public bool AlternarSirena()
    {
        EncenderSirena(!SirenaEncendida);
        return SirenaEncendida;
    }

    public void EncenderSirena(bool on)
    {
        if (_sirena == null) return;
        if (on)
        {
            if (!_sirena.isPlaying) _sirena.Play();
        }
        else if (_sirena.isPlaying)
        {
            _sirena.Stop();
        }
    }

    void ActualizarLuces()
    {
        if (!_lucesOn) return;
        if (_luces[0] == null || _luces[1] == null) return;

        // 1.5 ciclos/s: cada baliza destella 90 veces por minuto, como un camion real.
        int fase = (int)(Time.time * frecuenciaBalizas * 2f) % 2;
        for (int i = 0; i < 2; i++)
        {
            bool on = fase == i;
            _luces[i].intensity = on ? 3f : 0f;
            if (_balizas[i] != null)
                _balizas[i].sharedMaterial.color = on ? _luces[i].color : _luces[i].color * 0.25f;
        }
    }

    void OnDisable()
    {
        if (JugadorSentado) EncenderLocomocion();
    }

    void OnDrawGizmosSelected()
    {
        if (!_listo) return;
        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(CentroCaja, RotCaja, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, MitadCaja * 2f);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(PosAsiento(), 0.12f);
        Gizmos.DrawRay(CentroBase + Vector3.up, Fwd * 3f);
    }
}
