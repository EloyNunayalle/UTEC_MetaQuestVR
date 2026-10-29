using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Foco de fuego independiente (una ventana / seccion de la fachada).
/// - Visual: llamas (ParticleSystem), humo, luz parpadeante, crepitar y barra de vida flotante.
/// - Vida 0-1: el agua la baja (proporcional a la potencia de cada gota = presion).
///   Si deja de recibir agua antes de apagarse, el fuego se reaviva poco a poco.
/// - Impactos: <see cref="AplicarAgua"/> recibe los puntos de colision del chorro
///   (OnParticleCollision de LanzaAgua) y reparte el dano a las zonas cercanas al punto.
/// Se crea por codigo con <see cref="Crear"/> (lo hace EdificioEnLlamas).
/// </summary>
public class ZonaFuego : MonoBehaviour
{
    [Tooltip("Gotas (a presion maxima) necesarias para apagarla. Mayor = mas resistente.")]
    public float aguaNecesaria = 900f;
    [Tooltip("Radio (m) alrededor del centro de la zona en que un impacto cuenta.")]
    public float radioImpacto = 1.1f;
    [Tooltip("Segundos sin agua antes de que el fuego empiece a reavivarse.")]
    public float esperaReavivar = 4f;
    [Tooltip("Vida por segundo que recupera al reavivarse.")]
    public float velocidadReavivar = 0.04f;

    public float Vida { get; private set; } = 1f;
    public bool Apagada => Vida <= 0f;
    /// <summary>Punto sobre la fachada (centro del area que hay que mojar).</summary>
    public Vector3 Centro => _centroImpacto;

    public event Action<ZonaFuego> alApagarse;

    // Registro global: LanzaAgua no necesita conocer el edificio.
    static readonly List<ZonaFuego> _activas = new List<ZonaFuego>();
    public static IReadOnlyList<ZonaFuego> Activas => _activas;

    ParticleSystem _llamas, _humo;
    Light _luz;
    AudioSource _crepitar;
    Transform _barra, _relleno;
    Renderer _rellenoR;
    Vector3 _centroImpacto;
    float _escala = 1f, _ultimoAgua = -100f, _proximoVapor, _proximoSiseo, _semilla;
    OVRCameraRig _rig;

    static Material _matParticula;

    // ------------------------------------------------------------------
    // Impactos del agua
    // ------------------------------------------------------------------

    /// <summary>
    /// Aplica una gota de agua en un punto del mundo. Devuelve true si mojo algun fuego.
    /// potencia: 0-1 (presion de la bomba, menos en modo niebla).
    /// </summary>
    public static bool AplicarAgua(Vector3 punto, float potencia)
    {
        bool mojo = false;
        for (int i = _activas.Count - 1; i >= 0; i--)
        {
            var z = _activas[i];
            if (z == null || z.Apagada) continue;
            if ((punto - z._centroImpacto).sqrMagnitude > z.radioImpacto * z.radioImpacto) continue;
            z.Recibir(potencia, punto);
            mojo = true;
        }
        return mojo;
    }

    void Recibir(float potencia, Vector3 punto)
    {
        Vida = Mathf.Max(0f, Vida - potencia / Mathf.Max(1f, aguaNecesaria));
        _ultimoAgua = Time.time;

        // Vapor blanco donde pega el agua (limitado para no saturar).
        if (Time.time >= _proximoVapor)
        {
            _proximoVapor = Time.time + 0.05f;
            var p = new ParticleSystem.EmitParams
            {
                position = punto,
                startColor = new Color(0.92f, 0.94f, 0.96f, 0.5f),
                startSize = UnityEngine.Random.Range(0.5f, 0.9f),
                velocity = Vector3.up * UnityEngine.Random.Range(0.8f, 1.6f) + UnityEngine.Random.insideUnitSphere * 0.3f,
                applyShapeToPosition = false
            };
            _humo.Emit(p, 1);
        }
        if (Time.time >= _proximoSiseo)
        {
            _proximoSiseo = Time.time + 0.6f;
            SonidosVR.ReproducirEn(SonidosVR.Agua, punto, 0.35f, 15f);
        }

        if (Apagada) Apagar();
    }

    // ------------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------------

    /// <summary>
    /// Crea un foco de fuego. posicion: delante de la fachada; normal: hacia afuera de la fachada.
    /// ancho: ancho del foco (m). escala: tamano de las llamas (1 = ventana normal).
    /// </summary>
    public static ZonaFuego Crear(string nombre, Transform padre, Vector3 posicion, Vector3 normal, float ancho, float escala)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(posicion, Quaternion.LookRotation(normal, Vector3.up));
        var z = go.AddComponent<ZonaFuego>();
        z._escala = escala;
        z._semilla = UnityEngine.Random.value * 100f;
        z._centroImpacto = posicion - normal * 0.3f + Vector3.up * 0.5f * escala;
        z._llamas = z.CrearLlamas(ancho);
        z._humo = z.CrearHumo(ancho);
        z.CrearLuz();
        z.CrearBarra();
        z.AsegurarSuperficie(normal, ancho);

        z._crepitar = SonidosVR.CrearBucle(go.transform, SonidosVR.Fuego, 0.7f, 22f);
        z._crepitar.time = UnityEngine.Random.value * SonidosVR.Fuego.length;
        z._crepitar.Play();
        return z;
    }

    void OnEnable() { if (!_activas.Contains(this)) _activas.Add(this); }
    void OnDisable() { _activas.Remove(this); }

    /// <summary>Particula circular suave (llamas, humo); tambien la usan el fuego electrico y el CO2 de la Mision 2.</summary>
    public static Material MaterialParticula()
    {
        if (_matParticula != null) return _matParticula;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Llama" };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        tex.Apply();
        _matParticula = VisualesVR.MaterialSinLuz(Color.white); // Sprites/Default: siempre incluido en el build
        _matParticula.mainTexture = tex;
        return _matParticula;
    }

    ParticleSystem CrearSistema(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = MaterialParticula();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    ParticleSystem CrearLlamas(float ancho)
    {
        var ps = CrearSistema("Llamas");
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * _escala, 1.8f * _escala);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f * _escala, 0.75f * _escala);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.08f));
        main.gravityModifier = -0.15f; // el aire caliente sube
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;

        var em = ps.emission;
        em.rateOverTime = 50f * _escala;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(ancho, 0.25f, 0.3f);
        forma.rotation = new Vector3(-90f, 0f, 0f); // emite hacia arriba (el +Z de la forma apunta a +Y)

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.08f, 0.02f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.6f;
        ruido.frequency = 1.2f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        ps.Play();
        return ps;
    }

    ParticleSystem CrearHumo(float ancho)
    {
        var ps = CrearSistema("Humo");
        ps.transform.localPosition = Vector3.up * 0.8f * _escala;
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f * _escala, 1.4f * _escala);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.18f, 0.17f, 0.17f, 0.45f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 110;

        var em = ps.emission;
        em.rateOverTime = 7f * _escala;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(ancho * 0.8f, 0.2f, 0.2f);
        forma.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.6f));

        ps.Play();
        return ps;
    }

    void CrearLuz()
    {
        _luz = new GameObject("Luz_Fuego").AddComponent<Light>();
        _luz.transform.SetParent(transform, false);
        _luz.transform.localPosition = new Vector3(0f, 0.6f * _escala, 0.4f);
        _luz.type = LightType.Point;
        _luz.color = new Color(1f, 0.55f, 0.2f);
        _luz.range = 5f * _escala;
        _luz.shadows = LightShadows.None;
    }

    void CrearBarra()
    {
        _barra = new GameObject("Barra_Vida").transform;
        _barra.SetParent(transform, false);
        _barra.localPosition = Vector3.up * (1.7f * _escala);

        var fondo = VisualesVR.Primitiva(PrimitiveType.Quad, "Fondo", _barra, VisualesVR.MaterialSinLuz(new Color(0f, 0f, 0f, 0.7f)));
        fondo.transform.localScale = new Vector3(0.84f, 0.12f, 1f);
        var relleno = VisualesVR.Primitiva(PrimitiveType.Quad, "Relleno", _barra, VisualesVR.MaterialSinLuz(new Color(1f, 0.45f, 0.1f)));
        relleno.transform.localPosition = new Vector3(0f, 0f, -0.005f);
        relleno.transform.localScale = new Vector3(0.8f, 0.08f, 1f);
        _relleno = relleno.transform;
        _rellenoR = relleno.GetComponent<Renderer>();
    }

    /// <summary>
    /// El chorro necesita un collider para chocar (Collision Module). Si detras del foco
    /// no hay pared con collider, se crea un panel fino en el plano de la fachada.
    /// </summary>
    void AsegurarSuperficie(Vector3 normal, float ancho)
    {
        Vector3 origen = _centroImpacto + normal * 0.8f;
        foreach (var h in Physics.RaycastAll(origen, -normal, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.gameObject.layer != MangueraBomberos.CapaManguera) return; // ya hay pared

        var panel = new GameObject("Superficie_Impacto").AddComponent<BoxCollider>();
        panel.transform.SetParent(transform, false);
        panel.transform.position = _centroImpacto - normal * 0.05f;
        panel.transform.rotation = transform.rotation;
        panel.size = new Vector3(ancho + 0.6f, 1.6f * _escala + 0.6f, 0.1f);
    }

    // ------------------------------------------------------------------
    // Logica
    // ------------------------------------------------------------------

    void Update()
    {
        if (Apagada) return;

        // Se reaviva si se deja de mojar antes de apagarlo del todo.
        if (Vida < 1f && Time.time - _ultimoAgua > esperaReavivar)
            Vida = Mathf.Min(1f, Vida + velocidadReavivar * Time.deltaTime);

        float v = Mathf.Lerp(0.15f, 1f, Vida); // el fuego se achica pero sigue visible hasta apagarse
        var em = _llamas.emission;
        em.rateOverTime = 50f * _escala * v;
        var main = _llamas.main;
        float f = Mathf.Lerp(0.55f, 1f, Vida);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f * _escala * f, 0.75f * _escala * f);
        var emH = _humo.emission;
        emH.rateOverTime = 7f * _escala * v;

        float parpadeo = Mathf.PerlinNoise(Time.time * 6f, _semilla) * 0.6f + 0.7f;
        _luz.intensity = 1.8f * v * parpadeo;
        _crepitar.volume = 0.7f * v;

        ActualizarBarra();
    }

    void ActualizarBarra()
    {
        if (_rig == null) _rig = JuegoBomberos.Instancia != null ? JuegoBomberos.Instancia.rig : FindObjectOfType<OVRCameraRig>();
        if (_rig == null) return;
        Vector3 cabeza = _rig.centerEyeAnchor.position;
        bool visible = (cabeza - transform.position).sqrMagnitude < 35f * 35f;
        if (_barra.gameObject.activeSelf != visible) _barra.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 mirar = _barra.position - cabeza;
        if (mirar.sqrMagnitude > 0.001f) _barra.rotation = Quaternion.LookRotation(mirar, Vector3.up);
        // Escala con la distancia para que se lea de lejos.
        _barra.localScale = Vector3.one * Mathf.Clamp(mirar.magnitude / 6f, 1f, 3f);

        _relleno.localScale = new Vector3(0.8f * Vida, 0.08f, 1f);
        _relleno.localPosition = new Vector3(-0.4f * (1f - Vida), 0f, -0.005f);
        bool recibiendo = Time.time - _ultimoAgua < 0.3f;
        _rellenoR.sharedMaterial.color = recibiendo ? new Color(0.35f, 0.75f, 1f) : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.05f), Vida);
    }

    void Apagar()
    {
        Vida = 0f;
        _llamas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _luz.enabled = false;
        _crepitar.Stop();
        _barra.gameObject.SetActive(false);

        // Bocanada final de vapor; el humo se apaga solo.
        var emH = _humo.emission;
        emH.rateOverTime = 0f;
        _humo.Emit(new ParticleSystem.EmitParams { startColor = new Color(0.9f, 0.92f, 0.95f, 0.6f), applyShapeToPosition = true }, 12);
        SonidosVR.ReproducirEn(SonidosVR.Agua, transform.position, 0.8f, 20f);

        alApagarse?.Invoke(this);
    }

    /// <summary>Vuelve a encender el foco con vida completa.</summary>
    public void Reavivar()
    {
        Vida = 1f;
        _ultimoAgua = -100f;
        _luz.enabled = true;
        _llamas.Play();
        _crepitar.Play();
        var emH = _humo.emission;
        emH.rateOverTime = 7f * _escala;
        _barra.gameObject.SetActive(true);
    }
}
