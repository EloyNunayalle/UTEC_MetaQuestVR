using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Camion de bomberos interactivo: zona de abordaje junto a la puerta del conductor,
/// asiento en la cabina, balizas, sirena y motor.
/// - Para subir: parate en el circulo amarillo y espera (se llena) o presiona A / pellizca.
/// - Para bajar: presiona B (o Y), o manten el pellizco 2 segundos.
/// Si el modelo del camion no esta en el proyecto (asset de pago no incluido en git),
/// se genera un camion simple de reemplazo para que el juego funcione igual.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CamionBomberos : MonoBehaviour
{
    [Header("Orientacion")]
    [Tooltip("Invierte el frente detectado del camion (si la cabina queda atras).")]
    public bool invertirFrente;
    [Tooltip("Lado del conductor: true = izquierda (Peru/EE.UU.).")]
    public bool conductorIzquierda = true;

    [Header("Ajustes finos (ejes del camion: X derecha, Y arriba, Z frente)")]
    public Vector3 ajusteAsiento = Vector3.zero;
    public Vector3 ajusteZonaSubida = Vector3.zero;
    public Vector3 ajusteBalizas = Vector3.zero;

    [Header("Abordaje")]
    public float radioZona = 0.7f;
    public float segundosParaSubir = 2f;
    public float segundosPellizcoParaBajar = 2f;

    [Header("Camion de reemplazo (si falta el modelo)")]
    public Vector3 tamanoReemplazo = new Vector3(2.5f, 3.0f, 8.5f);

    public bool JugadorSentado { get; private set; }
    public Vector3 PuntoZonaSubida => _zona != null ? _zona.position : transform.position;
    public Vector3 Frente => _fwd;
    public Bounds Limites => _b;

    public Func<bool> puedeSubir;
    public Func<string> motivoNoPuede;
    public event Action alSubir;
    public event Action alBajar;

    OVRCameraRig _rig;
    Bounds _b;
    Vector3 _fwd, _der, _centroBase;
    Transform _efectos, _zona, _relleno;
    Renderer _anilloR, _rellenoR;
    TextMeshPro _etiqueta;
    Light[] _luces = new Light[2];
    Renderer[] _balizas = new Renderer[2];
    AudioSource _sirena, _motor;
    bool _lucesOn;
    float _progreso, _enfriamiento, _pellizcoBajar, _proximoTic;
    Vector3 _rigPosGuardada, _rigPosSentado;
    Quaternion _rigRotGuardada, _rigRotSentado;
    readonly List<GameObject> _locomocionApagada = new List<GameObject>();
    bool _listo;
    bool _pellizcoLiberado;

    static readonly Color Amarillo = new Color(1f, 0.85f, 0.1f, 0.85f);
    static readonly Color Verde = new Color(0.2f, 1f, 0.35f, 0.9f);
    static readonly Color Gris = new Color(0.6f, 0.6f, 0.6f, 0.5f);

    /// <summary>Configura el camion. referenciaFrente: punto hacia donde mira el frente (salida de la bahia).</summary>
    public void Inicializar(OVRCameraRig rig, Vector3? referenciaFrente)
    {
        _rig = rig;

        if (!VisualesVR.Limites(transform, out _b))
        {
            if (!LimitesPorColliders(out _b))
            {
                Debug.LogWarning("[CamionBomberos] No se encontro el modelo del camion: se genera uno de reemplazo.");
                CrearReemplazo(referenciaFrente);
                VisualesVR.Limites(transform, out _b);
            }
        }

        // Frente: eje horizontal mas largo de los limites, con el signo hacia la referencia.
        Vector3 eje = _b.size.z >= _b.size.x ? Vector3.forward : Vector3.right;
        if (referenciaFrente.HasValue)
        {
            Vector3 hacia = referenciaFrente.Value - _b.center;
            if (Vector3.Dot(hacia, eje) < 0f) eje = -eje;
        }
        _fwd = invertirFrente ? -eje : eje;
        _der = Vector3.Cross(Vector3.up, _fwd);
        _centroBase = new Vector3(_b.center.x, _b.min.y, _b.center.z);

        _efectos = new GameObject("Camion_Efectos").transform;
        _efectos.SetParent(transform, true);
        _efectos.SetPositionAndRotation(_centroBase, Quaternion.LookRotation(_fwd, Vector3.up));

        CrearZonaSubida();
        CrearBalizas();

        _sirena = SonidosVR.CrearBucle(_efectos, SonidosVR.Sirena, 0.55f, 60f);
        _sirena.transform.position = PosBalizas();
        _motor = SonidosVR.CrearBucle(_efectos, SonidosVR.Motor, 0.6f, 25f);
        _motor.transform.position = Local(0f, 1f, LargoMedio() - 1f);

        _listo = true;
    }

    float LargoMedio() => Vector3.Dot(_b.extents, new Vector3(Mathf.Abs(_fwd.x), 0f, Mathf.Abs(_fwd.z)));
    float AnchoMedio() => Vector3.Dot(_b.extents, new Vector3(Mathf.Abs(_der.x), 0f, Mathf.Abs(_der.z)));
    float LadoConductor() => conductorIzquierda ? -1f : 1f;

    /// <summary>Punto en coordenadas del camion (x derecha, y arriba desde el piso, z frente desde el centro).</summary>
    Vector3 Local(float x, float y, float z) => _centroBase + _der * x + Vector3.up * y + _fwd * z;

    Vector3 PosAsiento()
    {
        float alto = Mathf.Clamp(_b.size.y * 0.72f, 1.9f, 2.7f);
        return Local(LadoConductor() * AnchoMedio() * 0.45f + ajusteAsiento.x,
                     alto + ajusteAsiento.y,
                     LargoMedio() - 1.1f + ajusteAsiento.z);
    }

    Vector3 PosBalizas() => Local(ajusteBalizas.x, _b.size.y + 0.08f + ajusteBalizas.y, LargoMedio() - 0.9f + ajusteBalizas.z);

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
        float y = _b.min.y;
        Vector3 p = Local(LadoConductor() * (AnchoMedio() + 0.8f) + ajusteZonaSubida.x, 0f,
                          LargoMedio() - 1.4f + ajusteZonaSubida.z);
        // Ajusta la altura al piso real si hay collider debajo.
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
        _rellenoR = relleno.GetComponent<Renderer>();

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
            b.transform.position = c + _der * (lado * Mathf.Max(0.4f, AnchoMedio() * 0.5f));
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
    // Logica
    // ------------------------------------------------------------------

    void Update()
    {
        if (!_listo) return;
        ActualizarLuces();
        if (JugadorSentado) return;

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
            {
                Rechazar();
            }
            else if (dentro) _progreso += Time.deltaTime / segundosParaSubir;
            else _progreso = 0f;
            _relleno.localScale = Vector3.zero;
            return;
        }

        _etiqueta.text = "SUBIR AL CAMIÓN\n<size=60%>Párate en el círculo o presiona A / pellizca</size>";
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

    void LateUpdate()
    {
        if (!_listo || !JugadorSentado || _rig == null) return;

        // Mantiene al jugador en la cabina (la locomocion esta apagada).
        _rig.transform.SetPositionAndRotation(_rigPosSentado, _rigRotSentado);

        // El pellizco usado para subir debe soltarse antes de contar para bajar.
        bool pellizco = EntradaVR.AlgunPellizco();
        if (!pellizco) _pellizcoLiberado = true;
        if (pellizco && _pellizcoLiberado) _pellizcoBajar += Time.deltaTime;
        else _pellizcoBajar = 0f;

        if (EntradaVR.CancelarDown() || _pellizcoBajar >= segundosPellizcoParaBajar) Bajar();
    }

    public void Subir()
    {
        if (JugadorSentado || _rig == null) return;
        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;

        _rigPosGuardada = rigT.position;
        _rigRotGuardada = rigT.rotation;

        ApagarLocomocion();

        // Gira el rig para que la cabeza mire al frente del camion y la mueve al asiento.
        float yawObjetivo = Quaternion.LookRotation(_fwd).eulerAngles.y;
        float delta = Mathf.DeltaAngle(cabeza.eulerAngles.y, yawObjetivo);
        rigT.rotation = Quaternion.Euler(0f, rigT.eulerAngles.y + delta, 0f);
        rigT.position += PosAsiento() - cabeza.position;
        _rigPosSentado = rigT.position;
        _rigRotSentado = rigT.rotation;

        JugadorSentado = true;
        _progreso = 0f;
        _pellizcoBajar = 0f;
        _pellizcoLiberado = false;
        _zona.gameObject.SetActive(false);

        EncenderLuces(true);
        _sirena.Play();
        _motor.Play();
        SonidosVR.ReproducirEn(SonidosVR.Puerta, PosAsiento(), 1f);
        EntradaVR.VibrarAmbas(0.9f, 0.4f, 0.3f);

        alSubir?.Invoke();
    }

    public void Bajar()
    {
        if (!JugadorSentado) return;
        JugadorSentado = false;
        _rig.transform.SetPositionAndRotation(_rigPosGuardada, _rigRotGuardada);
        EncenderLocomocion();

        _sirena.Stop();
        _motor.Stop();
        _zona.gameObject.SetActive(true);
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

    public void EncenderLuces(bool on)
    {
        _lucesOn = on;
        if (!on)
        {
            for (int i = 0; i < 2; i++)
            {
                _luces[i].intensity = 0f;
                _balizas[i].sharedMaterial.color = _luces[i].color * 0.35f;
            }
        }
    }

    void ActualizarLuces()
    {
        if (!_lucesOn) return;
        int fase = (int)(Time.time * 5f) % 2;
        for (int i = 0; i < 2; i++)
        {
            bool on = fase == i;
            _luces[i].intensity = on ? 3f : 0f;
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
        Gizmos.DrawWireCube(_b.center, _b.size);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(PosAsiento(), 0.12f);
        Gizmos.DrawRay(_b.center, _fwd * 3f);
    }
}
