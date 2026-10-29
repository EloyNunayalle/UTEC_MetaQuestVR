using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Moto de bomberos para moverse rapido por la ciudad (en todos los modos).
/// No hay un modelo de moto en el proyecto: se arma con primitivas (roja, con baliza azul).
/// - Subir: parate en el circulo amarillo junto a la moto (se llena) o presiona A estando en el.
/// - Manejar: stick izquierdo adelante/atras = acelerar/frenar/reversa; cualquier stick a los lados = girar.
///   Teclado (editor): W/S/A/D o flechas.
/// - Bajar: B / Y (te deja parado al costado izquierdo).
/// Se detiene contra paredes y objetos (BoxCast elevado: las veredas y cordones no la frenan)
/// y sigue la altura del piso. Mientras se maneja, la locomocion del SDK se apaga (como en el camion).
/// </summary>
[DefaultExecutionOrder(-900)]
public class MotoBomberos : MonoBehaviour
{
    [Header("Manejo")]
    public float velocidadMaxima = 9f;      // ~32 km/h
    public float velocidadReversa = 2f;
    public float aceleracion = 4f;
    public float frenado = 7f;
    public float giroMaximo = 75f;          // grados por segundo a velocidad
    public float zonaMuerta = 0.15f;

    [Header("Subida")]
    public float radioZona = 0.6f;
    public float segundosParaSubir = 1.2f;

    public bool Montado { get; private set; }
    public float VelocidadKmh => Mathf.Abs(_vel) * 3.6f;
    public Vector3 PuntoSubida => _zona != null ? _zona.position : transform.position;

    public Func<bool> puedeSubir;
    public event Action alSubir, alBajar;

    OVRCameraRig _rig;
    Transform _zona, _relleno, _baliza;
    Material _matAnillo, _matBaliza;
    TextMeshPro _etiqueta;
    AudioSource _motor;
    Light _luzBaliza;
    float _vel, _progreso, _enfriamiento, _ultimoChoque, _proximaVibracion, _proximoTic, _alturaRig;
    Vector3 _rigPosLocal;
    Quaternion _rigRotLocal;
    readonly List<GameObject> _locomocion = new List<GameObject>();
    readonly HashSet<Collider> _tocandoAntes = new HashSet<Collider>();

    // Caja de choque: elevada 0.4 m del piso (no choca con veredas) y hasta ~1.1 m de alto.
    static readonly Vector3 MitadCaja = new Vector3(0.3f, 0.35f, 0.92f);
    const float AlturaCaja = 0.75f;
    static readonly Color Amarillo = new Color(1f, 0.85f, 0.1f);
    static readonly Color Verde = new Color(0.3f, 1f, 0.4f);

    public static MotoBomberos Crear(OVRCameraRig rig, Vector3 posicion, Quaternion rotacion)
    {
        var go = new GameObject("Moto_Bomberos");
        go.transform.SetPositionAndRotation(posicion, rotacion);
        var m = go.AddComponent<MotoBomberos>();
        m._rig = rig;
        m.Construir();
        return m;
    }

    // ------------------------------------------------------------------
    // Modelo con primitivas (frente = +Z)
    // ------------------------------------------------------------------

    void Construir()
    {
        var rojo = VisualesVR.MaterialOpaco(new Color(0.82f, 0.07f, 0.05f));
        var negro = VisualesVR.MaterialOpaco(new Color(0.07f, 0.07f, 0.08f));
        var cromo = VisualesVR.MaterialOpaco(new Color(0.75f, 0.76f, 0.78f));
        var blanco = VisualesVR.MaterialOpaco(new Color(0.95f, 0.95f, 0.92f));
        var t = transform;

        Pieza(PrimitiveType.Cylinder, "Rueda_Delantera", negro, new Vector3(0f, 0.31f, 0.68f), new Vector3(0.62f, 0.07f, 0.62f), new Vector3(0f, 0f, 90f));
        Pieza(PrimitiveType.Cylinder, "Llanta_Delantera", cromo, new Vector3(0f, 0.31f, 0.68f), new Vector3(0.3f, 0.075f, 0.3f), new Vector3(0f, 0f, 90f));
        Pieza(PrimitiveType.Cylinder, "Rueda_Trasera", negro, new Vector3(0f, 0.32f, -0.62f), new Vector3(0.64f, 0.09f, 0.64f), new Vector3(0f, 0f, 90f));
        Pieza(PrimitiveType.Cylinder, "Llanta_Trasera", cromo, new Vector3(0f, 0.32f, -0.62f), new Vector3(0.3f, 0.095f, 0.3f), new Vector3(0f, 0f, 90f));

        Pieza(PrimitiveType.Cube, "Chasis", rojo, new Vector3(0f, 0.6f, 0f), new Vector3(0.26f, 0.28f, 0.9f), Vector3.zero);
        Pieza(PrimitiveType.Cube, "Motor", cromo, new Vector3(0f, 0.42f, 0.02f), new Vector3(0.3f, 0.2f, 0.36f), Vector3.zero);
        Pieza(PrimitiveType.Cube, "Tanque", rojo, new Vector3(0f, 0.86f, 0.24f), new Vector3(0.32f, 0.2f, 0.42f), new Vector3(-8f, 0f, 0f));
        Pieza(PrimitiveType.Cube, "Asiento", negro, new Vector3(0f, 0.84f, -0.24f), new Vector3(0.28f, 0.08f, 0.6f), Vector3.zero);
        Pieza(PrimitiveType.Cube, "Caja_Trasera", rojo, new Vector3(0f, 0.98f, -0.66f), new Vector3(0.44f, 0.32f, 0.36f), Vector3.zero);
        Pieza(PrimitiveType.Cube, "Franja", blanco, new Vector3(0f, 0.98f, -0.66f), new Vector3(0.45f, 0.06f, 0.37f), Vector3.zero);
        for (int s = -1; s <= 1; s += 2)
            Pieza(PrimitiveType.Cylinder, "Horquilla", cromo, new Vector3(0.09f * s, 0.68f, 0.6f), new Vector3(0.045f, 0.36f, 0.045f), new Vector3(-18f, 0f, 0f));
        Pieza(PrimitiveType.Cylinder, "Manubrio", negro, new Vector3(0f, 1.04f, 0.47f), new Vector3(0.035f, 0.36f, 0.035f), new Vector3(0f, 0f, 90f));
        Pieza(PrimitiveType.Sphere, "Faro", VisualesVR.MaterialSinLuz(new Color(1f, 0.95f, 0.7f)), new Vector3(0f, 0.92f, 0.72f), Vector3.one * 0.15f, Vector3.zero);

        _matBaliza = VisualesVR.MaterialSinLuz(new Color(0.15f, 0.3f, 1f));
        _baliza = Pieza(PrimitiveType.Sphere, "Baliza", _matBaliza, new Vector3(0f, 1.2f, -0.66f), Vector3.one * 0.1f, Vector3.zero).transform;
        _luzBaliza = new GameObject("Luz_Baliza").AddComponent<Light>();
        _luzBaliza.transform.SetParent(_baliza, false);
        _luzBaliza.type = LightType.Point;
        _luzBaliza.color = new Color(0.2f, 0.4f, 1f);
        _luzBaliza.range = 5f;
        _luzBaliza.shadows = LightShadows.None;
        _luzBaliza.enabled = false;

        // Collider para que no la atravieses caminando.
        var col = gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.6f, 0f);
        col.size = new Vector3(0.5f, 1.15f, 1.9f);
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Circulo de subida al costado izquierdo.
        _zona = new GameObject("Zona_Subida").transform;
        _zona.SetParent(t, false);
        _zona.localPosition = new Vector3(-0.95f, 0.03f, -0.15f);
        _matAnillo = VisualesVR.MaterialSinLuz(Amarillo);
        VisualesVR.ObjetoMalla("Anillo", _zona, VisualesVR.Anillo(radioZona - 0.1f, radioZona), _matAnillo);
        _relleno = VisualesVR.ObjetoMalla("Relleno", _zona, VisualesVR.Disco(radioZona - 0.1f), VisualesVR.MaterialSinLuz(new Color(0.3f, 1f, 0.4f, 0.5f))).transform;
        _relleno.localScale = Vector3.zero;

        _etiqueta = VisualesVR.Texto("Etiqueta", t, 0.06f, new Vector2(1.2f, 0.25f));
        _etiqueta.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        _etiqueta.fontStyle = FontStyles.Bold;
        _etiqueta.outlineWidth = 0.2f;
        _etiqueta.outlineColor = Color.black;
        _etiqueta.text = "MOTO\n<size=60%>Párate en el círculo amarillo o presiona A</size>";

        _motor = SonidosVR.CrearBucle(t, SonidosVR.Motor, 0.45f, 25f);
    }

    GameObject Pieza(PrimitiveType tipo, string nombre, Material mat, Vector3 pos, Vector3 escala, Vector3 euler)
    {
        var p = VisualesVR.Primitiva(tipo, nombre, transform, mat);
        p.transform.localPosition = pos;
        p.transform.localScale = escala;
        p.transform.localRotation = Quaternion.Euler(euler);
        return p;
    }

    // ------------------------------------------------------------------

    void Update()
    {
        if (_rig == null) return;
        var cabeza = _rig.centerEyeAnchor;
        if (cabeza == null) return;

        if (Montado)
        {
            Manejar();
            _rig.transform.SetPositionAndRotation(transform.TransformPoint(_rigPosLocal), transform.rotation * _rigRotLocal);
            bool on = Mathf.Repeat(Time.time, 0.5f) < 0.25f;
            _luzBaliza.intensity = on ? 2.2f : 0.2f;
            _matBaliza.color = on ? new Color(0.3f, 0.5f, 1f) : new Color(0.05f, 0.1f, 0.35f);
            if (EntradaVR.CancelarDown()) Bajar();
            return;
        }

        // Etiqueta mirando al jugador; solo se ve de cerca.
        Vector3 aCabeza = _etiqueta.transform.position - cabeza.position;
        bool cerca = aCabeza.sqrMagnitude < 6f * 6f;
        if (_etiqueta.gameObject.activeSelf != cerca) _etiqueta.gameObject.SetActive(cerca);
        aCabeza.y = 0f;
        if (cerca && aCabeza.sqrMagnitude > 0.001f) _etiqueta.transform.rotation = Quaternion.LookRotation(aCabeza);

        bool interactuar = JuegoBomberos.Instancia == null || JuegoBomberos.Instancia.PermiteInteractuar;
        bool habilitado = puedeSubir == null || puedeSubir();
        Vector3 d = cabeza.position - _zona.position; d.y = 0f;
        bool dentro = interactuar && habilitado && d.magnitude <= radioZona + 0.15f;
        _enfriamiento -= Time.deltaTime;

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
        else _progreso = Mathf.Max(0f, _progreso - Time.deltaTime * 2f);

        float pulso = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        _matAnillo.color = dentro ? Verde : Color.Lerp(Amarillo * 0.7f, Amarillo, pulso);
        float s = Mathf.Clamp01(_progreso);
        _relleno.localScale = new Vector3(s, 1f, s);
    }

    public void Subir()
    {
        if (Montado || _rig == null) return;
        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;
        _alturaRig = rigT.position.y - transform.position.y;

        ApagarLocomocion();

        // Cabeza mirando al frente de la moto, sentada sobre el asiento.
        float delta = Mathf.DeltaAngle(cabeza.eulerAngles.y, transform.eulerAngles.y);
        rigT.rotation = Quaternion.Euler(0f, rigT.eulerAngles.y + delta, 0f);
        rigT.position += transform.TransformPoint(new Vector3(0f, 1.45f, -0.3f)) - cabeza.position;
        _rigPosLocal = transform.InverseTransformPoint(rigT.position);
        _rigRotLocal = Quaternion.Inverse(transform.rotation) * rigT.rotation;

        Montado = true;
        _vel = 0f;
        _progreso = 0f;
        _zona.gameObject.SetActive(false);
        _etiqueta.gameObject.SetActive(false);
        _luzBaliza.enabled = true;
        _motor.pitch = 1.3f;
        _motor.Play();
        SonidosVR.ReproducirEn(SonidosVR.Agarrar, transform.position + Vector3.up, 0.8f);
        EntradaVR.VibrarAmbas(0.7f, 0.3f, 0.4f);
        alSubir?.Invoke();
    }

    public void Bajar()
    {
        if (!Montado) return;
        Montado = false;
        _vel = 0f;
        _zona.gameObject.SetActive(true);
        _luzBaliza.enabled = false;
        _matBaliza.color = new Color(0.15f, 0.3f, 1f);
        _motor.Stop();

        // Te deja parado al costado izquierdo, mirando igual que en la moto.
        var rigT = _rig.transform;
        var cabeza = _rig.centerEyeAnchor;
        Vector3 piso = transform.TransformPoint(new Vector3(-0.95f, 0f, -0.15f));
        if (PisoBajo(piso + Vector3.up * 1.5f, 3f, out float y)) piso.y = y;
        Vector3 desplaz = piso - cabeza.position; desplaz.y = 0f;
        Vector3 nueva = rigT.position + desplaz;
        nueva.y = piso.y + _alturaRig;
        rigT.position = nueva;

        EncenderLocomocion();
        CamionBomberos.TeletransportarLocomotor(new Vector3(piso.x, nueva.y, piso.z), Quaternion.Euler(0f, cabeza.eulerAngles.y, 0f));
        _enfriamiento = 2f;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position + Vector3.up, 0.6f);
        alBajar?.Invoke();
    }

    // ------------------------------------------------------------------
    // Manejo
    // ------------------------------------------------------------------

    void Manejar()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector2 izq = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 der = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        float acel = izq.y;
        float giro = Mathf.Abs(der.x) > Mathf.Abs(izq.x) ? der.x : izq.x;
        float tv = Input.GetAxisRaw("Vertical"), th = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(tv) > Mathf.Abs(acel)) acel = tv;
        if (Mathf.Abs(th) > Mathf.Abs(giro)) giro = th;
        if (Mathf.Abs(acel) < zonaMuerta) acel = 0f;
        if (Mathf.Abs(giro) < zonaMuerta) giro = 0f;

        float objetivo = acel >= 0f ? acel * velocidadMaxima : acel * velocidadReversa;
        bool frenando = acel == 0f || (Mathf.Sign(objetivo) != Mathf.Sign(_vel) && Mathf.Abs(_vel) > 0.1f);
        _vel = Mathf.MoveTowards(_vel, objetivo, (frenando ? frenado : aceleracion) * dt);

        // Giro (solo en movimiento; en reversa se invierte).
        float grados = giro * giroMaximo * Mathf.Clamp(_vel / 2f, -1f, 1f) * dt;
        if (Mathf.Abs(grados) > 0.0001f)
        {
            Quaternion antes = transform.rotation;
            RecordarContactos();
            transform.Rotate(0f, grados, 0f, Space.World);
            if (ChoqueNuevo()) transform.rotation = antes;
        }

        // Avance con deteccion de choques.
        float paso = _vel * dt;
        if (Mathf.Abs(paso) > 0.0001f)
        {
            Vector3 dir = transform.forward * Mathf.Sign(paso);
            if (ChocaEnCamino(dir, Mathf.Abs(paso) + 0.12f)) Chocar();
            else transform.position += transform.forward * paso;
        }
        SeguirPiso(dt);

        float rapidez = Mathf.Abs(_vel) / velocidadMaxima;
        _motor.pitch = 1.2f + 1.1f * rapidez;
        if (Time.time >= _proximaVibracion && rapidez > 0.05f)
        {
            _proximaVibracion = Time.time + 0.12f;
            EntradaVR.VibrarAmbas(0.03f + 0.1f * rapidez, 0.1f, 0.3f);
        }
    }

    void SeguirPiso(float dt)
    {
        if (!PisoBajo(transform.position + Vector3.up * 1.2f, 3f, out float y)) return;
        Vector3 p = transform.position;
        // Sube cordones y rampas suavemente; si queda en el aire, cae.
        p.y = y > p.y ? Mathf.MoveTowards(p.y, y, dt * 2.5f) : Mathf.MoveTowards(p.y, y, dt * 6f);
        transform.position = p;
    }

    bool PisoBajo(Vector3 desde, float distancia, out float y)
    {
        y = 0f;
        float mejor = float.MaxValue;
        foreach (var h in Physics.RaycastAll(desde, Vector3.down, distancia, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (Ignorar(h.collider)) continue;
            if (h.distance < mejor) { mejor = h.distance; y = h.point.y; }
        }
        return mejor < float.MaxValue;
    }

    Vector3 CentroCaja => transform.TransformPoint(new Vector3(0f, AlturaCaja, 0f));

    bool ChocaEnCamino(Vector3 dir, float distancia)
    {
        foreach (var h in Physics.BoxCastAll(CentroCaja, MitadCaja, dir, transform.rotation, distancia, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
        {
            if (Ignorar(h.collider)) continue;
            if (h.distance <= 0f) continue; // ya estaba encimado: se ignora para no quedar atascado
            return true;
        }
        return false;
    }

    void RecordarContactos()
    {
        _tocandoAntes.Clear();
        foreach (var c in Physics.OverlapBox(CentroCaja, MitadCaja * 0.97f, transform.rotation, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
            if (!Ignorar(c)) _tocandoAntes.Add(c);
    }

    bool ChoqueNuevo()
    {
        foreach (var c in Physics.OverlapBox(CentroCaja, MitadCaja * 0.97f, transform.rotation, ~(1 << MangueraBomberos.CapaManguera), QueryTriggerInteraction.Ignore))
            if (!Ignorar(c) && !_tocandoAntes.Contains(c)) return true;
        return false;
    }

    bool Ignorar(Collider c)
    {
        if (c == null) return true;
        if (c.transform.IsChildOf(transform)) return true;
        if (_rig != null && c.transform.IsChildOf(_rig.transform)) return true;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true;
        return false;
    }

    void Chocar()
    {
        float golpe = Mathf.Abs(_vel);
        _vel = 0f;
        if (Time.time - _ultimoChoque < 0.6f || golpe < 0.5f) return;
        _ultimoChoque = Time.time;
        SonidosVR.ReproducirEn(SonidosVR.Puerta, transform.position + Vector3.up, 0.9f);
        EntradaVR.VibrarAmbas(Mathf.Clamp01(0.4f + golpe * 0.08f), 0.2f, 0.2f);
        JuegoBomberos.Instancia?.Aviso("¡Cuidado, chocaste!", JuegoBomberos.TipoAviso.Error, 1.2f);
    }

    // ------------------------------------------------------------------
    // Locomocion del SDK (apagada mientras se maneja)
    // ------------------------------------------------------------------

    void ApagarLocomocion()
    {
        _locomocion.Clear();
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("Locomot") || !t.gameObject.activeSelf) continue;
            if (_rig.centerEyeAnchor.IsChildOf(t)) continue;
            _locomocion.Add(t.gameObject);
        }
        foreach (var go in _locomocion) go.SetActive(false);
    }

    void EncenderLocomocion()
    {
        foreach (var go in _locomocion) if (go != null) go.SetActive(true);
        _locomocion.Clear();
    }

    void OnDisable()
    {
        if (Montado) EncenderLocomocion();
    }
}
